using System;
using System.Collections.Generic;
using YMH219.ChatApp.Models;

namespace YMH219.ChatApp.Core
{
    public sealed class ChatMessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; }

        public ChatMessageReceivedEventArgs(ChatMessage message)
        {
            Message = message;
        }
    }

    public sealed class ClientListChangedEventArgs : EventArgs
    {
        public IReadOnlyList<ConnectedClientInfo> Clients { get; }

        public ClientListChangedEventArgs(IReadOnlyList<ConnectedClientInfo> clients)
        {
            Clients = clients;
        }
    }

    public sealed class NetworkStatusEventArgs : EventArgs
    {
        public string StatusMessage { get; }
        public bool IsError { get; }

        public NetworkStatusEventArgs(string statusMessage, bool isError = false)
        {
            StatusMessage = statusMessage;
            IsError = isError;
        }
    }
}
