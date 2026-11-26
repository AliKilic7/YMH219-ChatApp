using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
    /// Çoklu TCP istemcisini asenkron olarak kabul eden ve mesajları yayınlayan sunucu sınıfı.
    /// </summary>
    public sealed class ChatServer : IChatServer
    {
        private sealed class ClientSession
        {
            public string Id { get; }
            public TcpClient TcpClient { get; }
            public StreamReader Reader { get; }
            public StreamWriter Writer { get; }
            public ConnectedClientInfo Info { get; }
            public SemaphoreSlim WriteLock { get; } = new(1, 1);

            public ClientSession(string id, TcpClient tcpClient, StreamReader reader, StreamWriter writer, ConnectedClientInfo info)
            {
                Id = id;
                TcpClient = tcpClient;
                Reader = reader;
                Writer = writer;
                Info = info;
            }
        }

        private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public bool IsRunning { get; private set; }

        public event EventHandler<ChatMessageReceivedEventArgs>? MessageBroadcasted;
        public event EventHandler<ClientListChangedEventArgs>? ClientsChanged;
        public event EventHandler<NetworkStatusEventArgs>? ServerStatusChanged;

        public Task StartAsync(string ipAddress, int port)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIp))
            {
                throw new FormatException("Geçerli bir IP adresi giriniz (ör. 127.0.0.1).");
            }

            if (port is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Port 1 ile 65535 arasında olmalıdır.");
            }

            _listener = new TcpListener(parsedIp, port);
            _listener.Start();
            _cts = new CancellationTokenSource();
            IsRunning = true;

            ServerStatusChanged?.Invoke(this, new NetworkStatusEventArgs($"Sunucu {parsedIp}:{port} üzerinde başlatıldı."));
            _ = AcceptClientsLoopAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch
            {
                // Kapanış sırasında dinleyici hatası yoksayılır.
            }

            foreach (string clientId in _clients.Keys.ToList())
            {
                RemoveClient(clientId, notifyLeave: false);
            }

            NotifyClientsChanged();
            ServerStatusChanged?.Invoke(this, new NetworkStatusEventArgs("Sunucu durduruldu."));
        }

        private async Task AcceptClientsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    _ = HandleClientAsync(tcpClient, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        ServerStatusChanged?.Invoke(this, new NetworkStatusEventArgs($"İstemci kabul hatası: {ex.Message}", isError: true));
                    }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken token)
        {
            string clientId = Guid.NewGuid().ToString("N")[..8];
            ClientSession? session = null;

            try
            {
                string remoteEp = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Bilinmeyen";
                NetworkStream stream = tcpClient.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                var info = new ConnectedClientInfo(clientId, $"İstemci-{clientId}", remoteEp);
                session = new ClientSession(clientId, tcpClient, reader, writer, info);
                _clients[clientId] = session;
                NotifyClientsChanged();

                while (!token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    if (!ChatMessage.TryDeserialize(line, out ChatMessage? incoming) || incoming == null)
                    {
                        continue;
                    }

                    if (incoming.Kind == ChatMessageKind.JoinHandshake)
                    {
                        session.Info.Username = string.IsNullOrWhiteSpace(incoming.Sender)
                            ? session.Info.Username
                            : incoming.Sender.Trim();
                        NotifyClientsChanged();

                        var joinNotice = new ChatMessage("Sistem", $"{session.Info.Username} sohbete katıldı.", ChatMessageKind.SystemNotice);
                        await BroadcastAsync(joinNotice).ConfigureAwait(false);
                    }
                    else
                    {
                        incoming.Sender = session.Info.Username;
                        incoming.Timestamp = DateTime.Now;
                        await BroadcastAsync(incoming).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Sunucu kapatıldı.
            }
            catch (IOException)
            {
                // İstemci bağlantıyı kapattı.
            }
            catch (Exception ex)
            {
                string name = session?.Info.Username ?? clientId;
                ServerStatusChanged?.Invoke(this, new NetworkStatusEventArgs($"İstemci hatası ({name}): {ex.Message}", isError: true));
            }
            finally
            {
                if (session != null)
                {
                    RemoveClient(clientId, notifyLeave: IsRunning);
                }
                else
                {
                    try { tcpClient.Close(); } catch { }
                }
            }
        }

        private async Task BroadcastAsync(ChatMessage message)
        {
            MessageBroadcasted?.Invoke(this, new ChatMessageReceivedEventArgs(message));
            string payload = message.Serialize();

            foreach (ClientSession session in _clients.Values.ToList())
            {
                try
                {
                    await session.WriteLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await session.Writer.WriteLineAsync(payload).ConfigureAwait(false);
                    }
                    finally
                    {
                        session.WriteLock.Release();
                    }
                }
                catch
                {
                    RemoveClient(session.Id, notifyLeave: false);
                }
            }
        }

        private void RemoveClient(string clientId, bool notifyLeave)
        {
            if (!_clients.TryRemove(clientId, out ClientSession? session))
            {
                return;
            }

            try
            {
                session.Reader.Dispose();
                session.Writer.Dispose();
                session.TcpClient.Close();
                session.WriteLock.Dispose();
            }
            catch
            {
                // Kaynak temizleme hatası yoksayılır.
            }

            NotifyClientsChanged();

            if (notifyLeave)
            {
                var leaveNotice = new ChatMessage("Sistem", $"{session.Info.Username} ayrıldı.", ChatMessageKind.SystemNotice);
                _ = BroadcastAsync(leaveNotice);
            }
        }

        private void NotifyClientsChanged()
        {
            IReadOnlyList<ConnectedClientInfo> snapshot = _clients.Values
                .Select(c => c.Info)
                .OrderBy(c => c.ConnectedAt)
                .ToList();
            ClientsChanged?.Invoke(this, new ClientListChangedEventArgs(snapshot));
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
