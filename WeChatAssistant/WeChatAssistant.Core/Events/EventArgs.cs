using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Events;

/// <summary>
/// 消息捕获事件委托
/// 当成功捕获到聊天消息时触发此事件
/// </summary>
/// <param name="sender">事件发送者对象</param>
/// <param name="e">包含捕获消息详情的事件参数</param>
public delegate void MessageCapturedEventHandler(object sender, ChatMessageEventArgs e);

/// <summary>
/// 日志事件委托
/// 当系统需要输出日志信息时触发此事件
/// </summary>
/// <param name="sender">事件发送者对象</param>
/// <param name="e">包含日志详情的事件参数</param>
public delegate void LogEventHandler(object sender, LogEventArgs e);

/// <summary>
/// 状态变更事件委托
/// 当系统运行状态发生变化时触发此事件
/// </summary>
/// <param name="sender">事件发送者对象</param>
/// <param name="e">包含新状态信息的事件参数</param>
public delegate void StatusChangedEventHandler(object sender, StatusEventArgs e);

/// <summary>
/// 聊天消息事件参数
/// 封装消息捕获事件的详细数据
/// </summary>
public class ChatMessageEventArgs : EventArgs
{
    /// <summary>
    /// 被捕获的聊天消息对象
    /// 包含消息的完整信息（发送者、内容、时间等）
    /// </summary>
    public ChatMessage Message { get; set; } = new();

    /// <summary>
    /// 消息所属的群名称
    /// 标识该消息来自哪个微信群
    /// </summary>
    public string GroupName { get; set; } = string.Empty;
}

/// <summary>
/// 日志事件参数
/// 封装日志输出事件的详细数据
/// </summary>
public class LogEventArgs : EventArgs
{
    /// <summary>
    /// 日志产生时间
    /// 记录日志条目的时间戳
    /// </summary>
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>
    /// 日志级别
    /// 支持的级别：INFO、WARN、ERROR、DEBUG
    /// </summary>
    public string Level { get; set; } = "INFO";

    /// <summary>
    /// 日志消息内容
    /// 具体的日志文本信息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 状态变更事件参数
/// 封装系统状态变更事件的详细数据
/// </summary>
public class StatusEventArgs : EventArgs
{
    /// <summary>
    /// 当前状态描述
    /// 例如："已连接"、"正在抓取"、"就绪"等
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 系统是否正在运行
    /// true：系统正在执行任务
    /// false：系统处于空闲状态
    /// </summary>
    public bool IsRunning { get; set; }
}
