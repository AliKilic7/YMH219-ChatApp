using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YMH219.ChatApp.Core;
using YMH219.ChatApp.Models;

namespace YMH219.ChatApp.Interfaces
{
    public interface IChatServer : IDisposable
    {
        bool IsRunning { get; }
        event EventHandler<ChatMessageReceivedEventArgs>? MessageBroadcasted;
        event EventHandler<ClientListChangedEventArgs>? ClientsChanged;
        event EventHandler<NetworkStatusEventArgs>? ServerStatusChanged;

        Task StartAsync(string ipAddress, int port);
        void Stop();
    }

    public interface IChatClient : IDisposable
    {
        bool IsConnected { get; }
        event EventHandler<ChatMessageReceivedEventArgs>? MessageReceived;
        event EventHandler<NetworkStatusEventArgs>? ConnectionStatusChanged;
        event EventHandler? Disconnected;

        Task ConnectAsync(string ipAddress, int port, string username);
        Task SendMessageAsync(string text);
        void Disconnect();
    }
}
