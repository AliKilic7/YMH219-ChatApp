using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using YMH219.ChatApp.Core;
using YMH219.ChatApp.Interfaces;
using YMH219.ChatApp.Models;
using YMH219.ChatApp.Services;

namespace YMH219.ChatApp
{
    /// <summary>
    /// Aynı pencerede hem TcpListener Sunucu hem TcpClient İstemci yöneten sohbet arayüzü.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly IChatServer _server = new ChatServer();
        private readonly IChatClient _client = new ChatClient();
        private readonly ChatHistoryExportService _exportService = new();
        private readonly List<string> _chatLines = new();

        private readonly TextBox _txtServerIp;
        private readonly TextBox _txtServerPort;
        private readonly Button _btnStartServer;
        private readonly Button _btnStopServer;
        private readonly Label _lblServerStatus;
        private readonly ListBox _lstConnectedClients;

        private readonly TextBox _txtClientIp;
        private readonly TextBox _txtClientPort;
        private readonly TextBox _txtUsername;
        private readonly Button _btnConnect;
        private readonly Button _btnDisconnect;
        private readonly Label _lblClientStatus;
        private readonly RichTextBox _rtbMessages;
        private readonly TextBox _txtMessageInput;
        private readonly Button _btnSend;
        private readonly Button _btnSaveHistory;

        public MainForm()
        {
            Text = "YMH 219 - TCP/IP İstemci & Sunucu Sohbet Uygulaması";
            Size = new Size(980, 650);
            MinimumSize = new Size(880, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310f));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // SOL PANEL: SUNUCU
            var serverGroup = new GroupBox
            {
                Text = "Sunucu Yönetimi (TcpListener)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(10),
                BackColor = Color.White
            };

            var serverPanel = new Panel { Dock = DockStyle.Fill };
            int y = 8;

            serverPanel.Controls.Add(CreateFieldLabel("Dinlenecek IP:", 4, y));
            _txtServerIp = new TextBox { Text = "127.0.0.1", Location = new Point(4, y + 20), Width = 165, Font = new Font("Segoe UI", 9.5f) };
            serverPanel.Controls.Add(_txtServerIp);

            serverPanel.Controls.Add(CreateFieldLabel("Port:", 180, y));
            _txtServerPort = new TextBox { Text = "5000", Location = new Point(180, y + 20), Width = 92, Font = new Font("Segoe UI", 9.5f) };
            serverPanel.Controls.Add(_txtServerPort);

            y += 56;
            _btnStartServer = CreateButton("Sunucuyu Başlat", 4, y, 132, Color.FromArgb(22, 163, 74));
            _btnStopServer = CreateButton("Sunucuyu Durdur", 142, y, 130, Color.FromArgb(220, 38, 38));
            _btnStopServer.Enabled = false;
            serverPanel.Controls.Add(_btnStartServer);
            serverPanel.Controls.Add(_btnStopServer);

            y += 42;
            _lblServerStatus = new Label
            {
                Text = "Sunucu Durumu: Kapalı",
                Location = new Point(4, y),
                Size = new Size(270, 34),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            serverPanel.Controls.Add(_lblServerStatus);

            y += 38;
            serverPanel.Controls.Add(CreateFieldLabel("Bağlı İstemciler (Canlı):", 4, y));
            _lstConnectedClients = new ListBox
            {
                Location = new Point(4, y + 22),
                Size = new Size(268, 360),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };
            serverPanel.Controls.Add(_lstConnectedClients);
            serverGroup.Controls.Add(serverPanel);

            // SAĞ PANEL: İSTEMCİ & SOHBET
            var clientGroup = new GroupBox
            {
                Text = "İstemci & Sohbet Alanı (TcpClient)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(10),
                BackColor = Color.White
            };

            var clientLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            clientLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));
            clientLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            clientLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

            // Bağlantı üst çubuğu
            var connPanel = new Panel { Dock = DockStyle.Fill };
            connPanel.Controls.Add(CreateFieldLabel("Sunucu IP:", 4, 4));
            _txtClientIp = new TextBox { Text = "127.0.0.1", Location = new Point(4, 24), Width = 125, Font = new Font("Segoe UI", 9.5f) };
            connPanel.Controls.Add(_txtClientIp);

            connPanel.Controls.Add(CreateFieldLabel("Port:", 138, 4));
            _txtClientPort = new TextBox { Text = "5000", Location = new Point(138, 24), Width = 68, Font = new Font("Segoe UI", 9.5f) };
            connPanel.Controls.Add(_txtClientPort);

            connPanel.Controls.Add(CreateFieldLabel("Kullanıcı Adı:", 215, 4));
            _txtUsername = new TextBox { Text = $"Kullanici_{Random.Shared.Next(100, 999)}", Location = new Point(215, 24), Width = 140, Font = new Font("Segoe UI", 9.5f) };
            connPanel.Controls.Add(_txtUsername);

            _btnConnect = CreateButton("Bağlan", 365, 22, 92, Color.FromArgb(37, 99, 235));
            _btnDisconnect = CreateButton("Bağlantıyı Kes", 464, 22, 122, Color.FromArgb(220, 38, 38));
            _btnDisconnect.Enabled = false;
            connPanel.Controls.Add(_btnConnect);
            connPanel.Controls.Add(_btnDisconnect);

            _lblClientStatus = new Label
            {
                Text = "İstemci Durumu: Bağlı değil",
                Location = new Point(4, 62),
                Size = new Size(420, 24),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            connPanel.Controls.Add(_lblClientStatus);

            _btnSaveHistory = CreateButton("Sohbeti .txt Kaydet", 464, 58, 135, Color.FromArgb(71, 85, 105));
            connPanel.Controls.Add(_btnSaveHistory);

            // Mesaj geçmişi alanı
            _rtbMessages = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10f),
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                DetectUrls = false
            };

            // Mesaj gönderme alt çubuğu
            var sendPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            _txtMessageInput = new TextBox
            {
                Location = new Point(4, 12),
                Width = 460,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Font = new Font("Segoe UI", 10f),
                Enabled = false,
                PlaceholderText = "Mesajınızı yazın ve Enter tuşuna basın..."
            };
            _btnSend = CreateButton("Mesaj Gönder", 472, 10, 125, Color.FromArgb(22, 163, 74));
            _btnSend.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnSend.Enabled = false;

            sendPanel.Controls.Add(_txtMessageInput);
            sendPanel.Controls.Add(_btnSend);

            clientLayout.Controls.Add(connPanel, 0, 0);
            clientLayout.Controls.Add(_rtbMessages, 0, 1);
            clientLayout.Controls.Add(sendPanel, 0, 2);
            clientGroup.Controls.Add(clientLayout);

            mainLayout.Controls.Add(serverGroup, 0, 0);
            mainLayout.Controls.Add(clientGroup, 1, 0);
            Controls.Add(mainLayout);

            WireEvents();
        }

        private void WireEvents()
        {
            _btnStartServer.Click += BtnStartServer_Click;
            _btnStopServer.Click += (_, _) => _server.Stop();
            _btnConnect.Click += BtnConnect_Click;
            _btnDisconnect.Click += (_, _) => _client.Disconnect();
            _btnSend.Click += BtnSend_Click;
            _btnSaveHistory.Click += BtnSaveHistory_Click;
            _txtMessageInput.KeyDown += TxtMessageInput_KeyDown;

            _server.ClientsChanged += Server_ClientsChanged;
            _server.ServerStatusChanged += Server_StatusChanged;

            _client.MessageReceived += Client_MessageReceived;
            _client.ConnectionStatusChanged += Client_StatusChanged;
            _client.Disconnected += Client_Disconnected;
        }

        private static Label CreateFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85)
            };
        }

        private static Button CreateButton(string text, int x, int y, int width, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private async void BtnStartServer_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(_txtServerPort.Text.Trim(), out int port))
                {
                    MessageBox.Show("Geçerli bir sayısal port giriniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await _server.StartAsync(_txtServerIp.Text.Trim(), port);
                _btnStartServer.Enabled = false;
                _btnStopServer.Enabled = true;
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Sunucu başlatılamadı (Port kullanımda olabilir):\n{ex.Message}", "Ağ Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sunucu başlatılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(_txtClientPort.Text.Trim(), out int port))
                {
                    MessageBox.Show("Geçerli bir sayısal port giriniz.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _btnConnect.Enabled = false;
                await _client.ConnectAsync(_txtClientIp.Text.Trim(), port, _txtUsername.Text.Trim());

                _btnDisconnect.Enabled = true;
                _txtMessageInput.Enabled = true;
                _btnSend.Enabled = true;
                _txtMessageInput.Focus();
            }
            catch (SocketException ex)
            {
                _btnConnect.Enabled = true;
                MessageBox.Show($"Sunucuya bağlanılamadı (Bağlantı reddedildi veya sunucu kapalı):\n{ex.Message}", "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _btnConnect.Enabled = true;
                MessageBox.Show($"Bağlantı sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            await SendCurrentMessageAsync();
        }

        private async void TxtMessageInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendCurrentMessageAsync();
            }
        }

        private async System.Threading.Tasks.Task SendCurrentMessageAsync()
        {
            string text = _txtMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                await _client.SendMessageAsync(text);
                _txtMessageInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mesaj gönderilemedi:\n{ex.Message}", "Gönderim Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Server_ClientsChanged(object? sender, ClientListChangedEventArgs e)
        {
            RunOnUi(() =>
            {
                _lstConnectedClients.Items.Clear();
                foreach (ConnectedClientInfo client in e.Clients)
                {
                    _lstConnectedClients.Items.Add(client.ToString());
                }
            });
        }

        private void Server_StatusChanged(object? sender, NetworkStatusEventArgs e)
        {
            RunOnUi(() =>
            {
                _lblServerStatus.Text = e.StatusMessage;
                _lblServerStatus.ForeColor = e.IsError ? Color.Crimson : Color.FromArgb(22, 163, 74);

                if (!_server.IsRunning)
                {
                    _btnStartServer.Enabled = true;
                    _btnStopServer.Enabled = false;
                }
            });
        }

        private void Client_MessageReceived(object? sender, ChatMessageReceivedEventArgs e)
        {
            RunOnUi(() =>
            {
                string line = e.Message.ToDisplayString();
                _chatLines.Add(line);

                _rtbMessages.SelectionStart = _rtbMessages.TextLength;
                _rtbMessages.SelectionColor = e.Message.Kind == Models.ChatMessageKind.SystemNotice
                    ? Color.FromArgb(100, 116, 139)
                    : Color.FromArgb(15, 23, 42);
                _rtbMessages.AppendText(line + Environment.NewLine);
                _rtbMessages.ScrollToCaret();
            });
        }

        private void Client_StatusChanged(object? sender, NetworkStatusEventArgs e)
        {
            RunOnUi(() =>
            {
                _lblClientStatus.Text = e.StatusMessage;
                _lblClientStatus.ForeColor = e.IsError ? Color.Crimson : Color.FromArgb(37, 99, 235);
            });
        }

        private void Client_Disconnected(object? sender, EventArgs e)
        {
            RunOnUi(() =>
            {
                _btnConnect.Enabled = true;
                _btnDisconnect.Enabled = false;
                _txtMessageInput.Enabled = false;
                _btnSend.Enabled = false;
            });
        }

        private void BtnSaveHistory_Click(object? sender, EventArgs e)
        {
            if (_chatLines.Count == 0)
            {
                MessageBox.Show("Kaydedilecek sohbet mesajı bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Metin Dosyası (*.txt)|*.txt",
                FileName = $"sohbet_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "Sohbet Geçmişini Kaydet"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                _exportService.Export(dialog.FileName, _chatLines);
                MessageBox.Show("Sohbet geçmişi kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client.Dispose();
            _server.Dispose();
            base.OnFormClosing(e);
        }
    }
}
