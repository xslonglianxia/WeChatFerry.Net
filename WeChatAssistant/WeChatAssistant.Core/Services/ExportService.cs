using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class ExportService
{
    public void ExportToTxt(List<ChatMessage> messages, string filePath, string groupName)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        writer.WriteLine($"群聊记录导出");
        writer.WriteLine($"群名称: {groupName}");
        writer.WriteLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"消息数量: {messages.Count}");
        writer.WriteLine(new string('=', 50));
        writer.WriteLine();

        foreach (var msg in messages)
        {
            writer.WriteLine($"[{msg.MessageTime:yyyy-MM-dd HH:mm:ss}] {msg.Sender}:");
            writer.WriteLine($"  {msg.Content}");
            writer.WriteLine();
        }
    }

    public void ExportToCsv(List<ChatMessage> messages, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        writer.WriteLine("时间,发送者,内容");

        foreach (var msg in messages)
        {
            var content = msg.Content.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
            writer.WriteLine($"\"{msg.MessageTime:yyyy-MM-dd HH:mm:ss}\",\"{msg.Sender}\",\"{content}\"");
        }
    }

    public void ExportToJson(List<ChatMessage> messages, string filePath, string groupName)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var exportData = new
        {
            GroupName = groupName,
            ExportTime = DateTime.Now,
            MessageCount = messages.Count,
            Messages = messages.Select(m => new
            {
                m.MessageTime,
                m.Sender,
                m.Content
            })
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
    }

    public string FormatMessagesForSend(List<ChatMessage> messages, int maxMessages = 20)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"【聊天记录汇总】({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine($"共 {messages.Count} 条消息");
        sb.AppendLine(new string('-', 30));

        var displayMessages = messages.Take(maxMessages).ToList();
        foreach (var msg in displayMessages)
        {
            sb.AppendLine($"[{msg.MessageTime:HH:mm}] {msg.Sender}: {msg.Content}");
        }

        if (messages.Count > maxMessages)
        {
            sb.AppendLine($"... 还有 {messages.Count - maxMessages} 条消息未显示");
        }

        return sb.ToString();
    }
}
