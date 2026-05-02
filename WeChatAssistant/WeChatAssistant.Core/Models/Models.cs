namespace WeChatAssistant.Core.Models;

public class ChatMessage
{
    public long Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime MessageTime { get; set; }
    public DateTime CaptureTime { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public bool IsSent { get; set; }
}

public class GroupInfo
{
    public string Name { get; set; } = string.Empty;
    public DateTime LastCaptureTime { get; set; }
    public int MessageCount { get; set; }
}

public class ScheduleTask
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime LastRunTime { get; set; }
    public DateTime NextRunTime { get; set; }
    public string ExportPath { get; set; } = string.Empty;
}

public class CaptureResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int CapturedCount { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
}

public class SendResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
