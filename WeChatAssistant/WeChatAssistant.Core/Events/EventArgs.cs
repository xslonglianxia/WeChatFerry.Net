namespace WeChatAssistant.Core.Events;

public delegate void MessageCapturedEventHandler(object sender, ChatMessageEventArgs e);

public delegate void LogEventHandler(object sender, LogEventArgs e);

public delegate void StatusChangedEventHandler(object sender, StatusEventArgs e);

public class ChatMessageEventArgs : EventArgs
{
    public ChatMessage Message { get; set; } = new();
    public string GroupName { get; set; } = string.Empty;
}

public class LogEventArgs : EventArgs
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
}

public class StatusEventArgs : EventArgs
{
    public string Status { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
}
