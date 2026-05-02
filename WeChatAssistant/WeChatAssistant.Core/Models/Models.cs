namespace WeChatAssistant.Core.Models;

/// <summary>
/// 聊天消息数据模型
/// 用于存储单条微信消息的完整信息
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// 消息唯一标识符（数据库自增主键）
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 消息所属的群名称
    /// 例如："项目开发群"、"产品讨论组"等
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// 消息发送者昵称
    /// 从微信界面解析得到的发送者名称
    /// </summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// 消息文本内容
    /// 仅存储文本消息，图片、文件等类型暂不支持
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息发送时间
    /// 从微信界面解析得到的原始发送时间
    /// </summary>
    public DateTime MessageTime { get; set; }

    /// <summary>
    /// 消息被抓取的时间
    /// 系统抓取该消息的时间戳，用于记录抓取时机
    /// </summary>
    public DateTime CaptureTime { get; set; }

    /// <summary>
    /// 消息唯一标识字符串
    /// 用于消息去重，防止重复抓取同一条消息
    /// 生成规则：GUID
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// 消息是否已发送标识
    /// 用于标记该消息是否已经通过定时任务发送
    /// </summary>
    public bool IsSent { get; set; }
}

/// <summary>
/// 群组信息数据模型
/// 用于存储微信群的基本信息和统计信息
/// </summary>
public class GroupInfo
{
    /// <summary>
    /// 群名称
    /// 微信群的显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 最后一次抓取该群消息的时间
    /// 用于判断是否需要重新抓取
    /// </summary>
    public DateTime LastCaptureTime { get; set; }

    /// <summary>
    /// 该群已抓取的消息总数
    /// 用于界面显示统计信息
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
/// 定时任务数据模型
/// 用于配置和管理定时抓取/发送任务
/// </summary>
public class ScheduleTask
{
    /// <summary>
    /// 任务唯一标识符（数据库自增主键）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 任务名称
    /// 用户自定义的任务名称，便于识别
    /// 例如："每日晨报发送"、"工作日提醒"等
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 目标群名称
    /// 指定该任务要操作的微信群
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Cron表达式
    /// 用于定义任务执行的时间规则
    /// 格式：分 时 日 月 周
    /// 例如："0 9 * * *" 表示每天9:00执行
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// 任务是否启用
    /// true：任务将按计划执行
    /// false：任务暂停执行
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 上次执行时间
    /// 记录任务最后一次执行的时间戳
    /// </summary>
    public DateTime LastRunTime { get; set; }

    /// <summary>
    /// 下次执行时间
    /// 根据Cron表达式计算的下次执行时间
    /// </summary>
    public DateTime NextRunTime { get; set; }

    /// <summary>
    /// 导出文件保存路径
    /// 任务执行时导出聊天记录的目标目录
    /// 为空则不导出文件
    /// </summary>
    public string ExportPath { get; set; } = string.Empty;
}

/// <summary>
/// 消息抓取结果模型
/// 用于封装抓取操作的返回结果
/// </summary>
public class CaptureResult
{
    /// <summary>
    /// 抓取操作是否成功
    /// true：成功抓取消息
    /// false：抓取失败
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 结果描述消息
    /// 成功时描述抓取数量，失败时描述错误原因
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 实际抓取的消息数量
    /// </summary>
    public int CapturedCount { get; set; }

    /// <summary>
    /// 抓取到的消息列表
    /// 包含所有成功解析的聊天消息
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();
}

/// <summary>
/// 消息发送结果模型
/// 用于封装发送操作的返回结果
/// </summary>
public class SendResult
{
    /// <summary>
    /// 发送操作是否成功
    /// true：消息已成功发送
    /// false：发送失败
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 结果描述消息
    /// 成功时确认发送完成，失败时描述错误原因
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
