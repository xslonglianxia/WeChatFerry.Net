using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

/// <summary>
/// 导出服务类
/// 提供聊天记录的多格式导出功能
/// 支持TXT、CSV、JSON三种导出格式
/// </summary>
/// <remarks>
/// 导出格式说明：
/// - TXT：纯文本格式，适合人工阅读
/// - CSV：逗号分隔格式，可用Excel打开
/// - JSON：结构化数据格式，适合程序处理
/// </remarks>
public class ExportService
{
    #region TXT导出

    /// <summary>
    /// 导出聊天记录为TXT文本文件
    /// 格式化输出，便于人工阅读
    /// </summary>
    /// <param name="messages">要导出的消息列表</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="groupName">群名称（用于文件头信息）</param>
    /// <remarks>
    /// 输出格式示例：
    /// 群聊记录导出
    /// 群名称: 测试群
    /// 导出时间: 2024-01-15 10:30:00
    /// 消息数量: 100
    /// ==================================================
    /// 
    /// [2024-01-15 09:00:00] 张三:
    ///   大家早上好
    /// 
    /// [2024-01-15 09:01:00] 李四:
    ///   早上好！
    /// </remarks>
    public void ExportToTxt(List<ChatMessage> messages, string filePath, string groupName)
    {
        // 确保目标目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 使用UTF-8编码写入文件
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        // 写入文件头信息
        writer.WriteLine($"群聊记录导出");
        writer.WriteLine($"群名称: {groupName}");
        writer.WriteLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"消息数量: {messages.Count}");
        writer.WriteLine(new string('=', 50));  // 分隔线
        writer.WriteLine();

        // 遍历写入每条消息
        foreach (var msg in messages)
        {
            // 格式：[时间] 发送者:
            writer.WriteLine($"[{msg.MessageTime:yyyy-MM-dd HH:mm:ss}] {msg.Sender}:");
            // 消息内容缩进两个空格
            writer.WriteLine($"  {msg.Content}");
            writer.WriteLine();  // 空行分隔
        }
    }

    #endregion

    #region CSV导出

    /// <summary>
    /// 导出聊天记录为CSV文件
    /// 可用Excel或其他表格软件打开
    /// </summary>
    /// <param name="messages">要导出的消息列表</param>
    /// <param name="filePath">目标文件路径</param>
    /// <remarks>
    /// CSV格式说明：
    /// - 第一行为表头：时间,发送者,内容
    /// - 字段使用双引号包裹，防止内容中的逗号影响解析
    /// - 内容中的双引号转义为两个双引号
    /// - 换行符替换为空格，保证每条消息占一行
    /// </remarks>
    public void ExportToCsv(List<ChatMessage> messages, string filePath)
    {
        // 确保目标目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        // 写入CSV表头
        writer.WriteLine("时间,发送者,内容");

        // 写入每条消息
        foreach (var msg in messages)
        {
            // 处理内容中的特殊字符
            // 1. 双引号转义为两个双引号
            // 2. 换行符替换为空格
            var content = msg.Content
                .Replace("\"", "\"\"")
                .Replace("\r", " ")
                .Replace("\n", " ");
            
            // 格式："时间","发送者","内容"
            writer.WriteLine($"\"{msg.MessageTime:yyyy-MM-dd HH:mm:ss}\",\"{msg.Sender}\",\"{content}\"");
        }
    }

    #endregion

    #region JSON导出

    /// <summary>
    /// 导出聊天记录为JSON文件
    /// 结构化数据格式，便于程序处理和数据分析
    /// </summary>
    /// <param name="messages">要导出的消息列表</param>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="groupName">群名称</param>
    /// <remarks>
    /// JSON结构示例：
    /// {
    ///   "GroupName": "测试群",
    ///   "ExportTime": "2024-01-15T10:30:00",
    ///   "MessageCount": 100,
    ///   "Messages": [
    ///     {
    ///       "MessageTime": "2024-01-15T09:00:00",
    ///       "Sender": "张三",
    ///       "Content": "大家早上好"
    ///     }
    ///   ]
    /// }
    /// </remarks>
    public void ExportToJson(List<ChatMessage> messages, string filePath, string groupName)
    {
        // 确保目标目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 构建导出数据结构
        var exportData = new
        {
            GroupName = groupName,
            ExportTime = DateTime.Now,
            MessageCount = messages.Count,
            // 只保留核心字段，简化JSON结构
            Messages = messages.Select(m => new
            {
                m.MessageTime,
                m.Sender,
                m.Content
            })
        };

        // 序列化为JSON并写入文件
        // Formatting.Indented 使输出格式化（有缩进），便于阅读
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
    }

    #endregion

    #region 消息格式化

    /// <summary>
    /// 格式化消息列表用于发送
    /// 生成适合在微信中发送的文本格式
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="maxMessages">最大显示消息数，默认20条</param>
    /// <returns>格式化后的文本字符串</returns>
    /// <remarks>
    /// 输出格式示例：
    /// 【聊天记录汇总】(2024-01-15 10:30)
    /// 共 100 条消息
    /// ------------------------------
    /// [09:00] 张三: 大家早上好
    /// [09:01] 李四: 早上好！
    /// ... 还有 80 条消息未显示
    /// </remarks>
    public string FormatMessagesForSend(List<ChatMessage> messages, int maxMessages = 20)
    {
        var sb = new System.Text.StringBuilder();
        
        // 写入标题和统计信息
        sb.AppendLine($"【聊天记录汇总】({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine($"共 {messages.Count} 条消息");
        sb.AppendLine(new string('-', 30));  // 分隔线

        // 只显示前N条消息，避免消息过长
        var displayMessages = messages.Take(maxMessages).ToList();
        foreach (var msg in displayMessages)
        {
            // 简化时间格式只显示时:分
            sb.AppendLine($"[{msg.MessageTime:HH:mm}] {msg.Sender}: {msg.Content}");
        }

        // 如果消息总数超过显示数量，添加提示
        if (messages.Count > maxMessages)
        {
            sb.AppendLine($"... 还有 {messages.Count - maxMessages} 条消息未显示");
        }

        return sb.ToString();
    }

    #endregion
}
