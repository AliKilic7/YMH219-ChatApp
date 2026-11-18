using System;

namespace YMH219.ChatApp.Models
{
    /// <summary>
    /// Sunucuya bağlı bir istemcinin meta verilerini tutar.
    /// </summary>
    public sealed class ConnectedClientInfo
    {
        public string ClientId { get; }
        public string Username { get; set; }
        public string RemoteEndPoint { get; }
        public DateTime ConnectedAt { get; }

        public ConnectedClientInfo(string clientId, string username, string remoteEndPoint)
        {
            ClientId = clientId;
            Username = username;
            RemoteEndPoint = remoteEndPoint;
            ConnectedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{Username} ({RemoteEndPoint})";
        }
    }
}
