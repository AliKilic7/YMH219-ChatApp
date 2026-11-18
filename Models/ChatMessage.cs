using System;
using System.Text.Json;

namespace YMH219.ChatApp.Models
{
    public enum ChatMessageKind
    {
        UserMessage,
        SystemNotice,
        JoinHandshake
    }

    /// <summary>
    /// Ağ üzerinden iletilen zaman damgalı sohbet mesajı modeli.
    /// </summary>
    public sealed class ChatMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ChatMessageKind Kind { get; set; } = ChatMessageKind.UserMessage;

        public ChatMessage()
        {
        }

        public ChatMessage(string sender, string content, ChatMessageKind kind = ChatMessageKind.UserMessage, DateTime? timestamp = null)
        {
            Sender = string.IsNullOrWhiteSpace(sender) ? "Bilinmeyen" : sender.Trim();
            Content = content ?? string.Empty;
            Kind = kind;
            Timestamp = timestamp ?? DateTime.Now;
        }

        public string ToDisplayString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
        }

        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        public static bool TryDeserialize(string json, out ChatMessage? message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                message = JsonSerializer.Deserialize<ChatMessage>(json);
                return message != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
