using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YMH219.ChatApp.Core;
using YMH219.ChatApp.Interfaces;
using YMH219.ChatApp.Models;

namespace YMH219.ChatApp.Services
{
    /// <summary>
    /// TCP sunucusuna asenkron bağlanıp zaman damgalı mesaj gönderen ve alan istemci sınıfı.
    /// </summary>
    public sealed class ChatClient : IChatClient
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private string _username = "Kullanıcı";

        public bool IsConnected => _tcpClient?.Connected == true;

        public event EventHandler<ChatMessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<NetworkStatusEventArgs>? ConnectionStatusChanged;
        public event EventHandler? Disconnected;

        public async Task ConnectAsync(string ipAddress, int port, string username)
        {
            if (IsConnected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                throw new ArgumentException("Sunucu IP adresi boş olamaz.", nameof(ipAddress));
            }

            if (port is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Port 1 ile 65535 arasında olmalıdır.");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Kullanıcı adı boş olamaz.", nameof(username));
            }

            _username = username.Trim();
            _tcpClient = new TcpClient();
            _cts = new CancellationTokenSource();

            try
            {
                await _tcpClient.ConnectAsync(ipAddress.Trim(), port).ConfigureAwait(false);
                NetworkStream stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                var handshake = new ChatMessage(_username, "Bağlandı", ChatMessageKind.JoinHandshake);
                await WriteLineSafeAsync(handshake.Serialize()).ConfigureAwait(false);

                ConnectionStatusChanged?.Invoke(
                    this,
                    new NetworkStatusEventArgs($"Sunucuya bağlanıldı ({ipAddress}:{port}) - Kullanıcı: {_username}"));

                _ = ReceiveLoopAsync(_cts.Token);
            }
            catch
            {
                CleanupConnection();
                throw;
            }
        }

        public async Task SendMessageAsync(string text)
        {
            if (!IsConnected || _writer == null)
            {
                throw new InvalidOperationException("Sunucuya aktif bir bağlantı bulunmuyor.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var message = new ChatMessage(_username, text.Trim(), ChatMessageKind.UserMessage);
            await WriteLineSafeAsync(message.Serialize()).ConfigureAwait(false);
        }

        public void Disconnect()
        {
            bool wasConnected = _tcpClient != null;
            CleanupConnection();

            if (wasConnected)
            {
                ConnectionStatusChanged?.Invoke(this, new NetworkStatusEventArgs("Bağlantı kesildi."));
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task WriteLineSafeAsync(string payload)
        {
            if (_writer == null)
            {
                return;
            }

            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(payload).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _reader != null)
                {
                    string? line = await _reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    if (ChatMessage.TryDeserialize(line, out ChatMessage? message) && message != null)
                    {
                        MessageReceived?.Invoke(this, new ChatMessageReceivedEventArgs(message));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Kullanıcı tarafından kapatıldı.
            }
            catch (IOException)
            {
                ConnectionStatusChanged?.Invoke(this, new NetworkStatusEventArgs("Sunucu ile bağlantı koptu.", isError: true));
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, new NetworkStatusEventArgs($"Alma hatası: {ex.Message}", isError: true));
            }
            finally
            {
                if (_tcpClient != null)
                {
                    CleanupConnection();
                    Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void CleanupConnection()
        {
            try
            {
                _cts?.Cancel();
                _reader?.Dispose();
                _writer?.Dispose();
                _tcpClient?.Close();
            }
            catch
            {
                // Kapanış hataları yoksayılır.
            }
            finally
            {
                _reader = null;
                _writer = null;
                _tcpClient = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Dispose()
        {
            Disconnect();
            _writeLock.Dispose();
        }
    }
}
