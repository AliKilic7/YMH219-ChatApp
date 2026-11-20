using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YMH219.ChatApp.Services
{
    /// <summary>
    /// Sohbet geçmişini UTF-8 metin dosyasına kaydeder.
    /// </summary>
    public sealed class ChatHistoryExportService
    {
        public void Export(string filePath, IEnumerable<string> lines)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Geçerli bir dosya yolu seçiniz.", nameof(filePath));
            }

            var sb = new StringBuilder();
            sb.AppendLine("YMH 219 - TCP/IP Sohbet Geçmişi");
            sb.AppendLine($"Dışa Aktarım: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', 50));
            foreach (string line in lines)
            {
                sb.AppendLine(line);
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
