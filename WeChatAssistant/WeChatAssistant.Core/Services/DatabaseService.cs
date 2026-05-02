using Microsoft.Data.Sqlite;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

/// <summary>
/// 数据库服务类
/// 提供消息存储、查询和管理功能
/// 使用SQLite作为本地数据库，支持轻量级数据持久化
/// </summary>
/// <remarks>
/// 数据库设计：
/// - Messages表：存储抓取的聊天消息
/// - ScheduleTasks表：存储定时任务配置
/// 
/// 数据库位置：
/// - 默认：%LocalAppData%\WeChatAssistant\data.db
/// - 可通过构造函数参数自定义路径
/// </remarks>
public class DatabaseService : IDisposable
{
    #region 私有字段

    /// <summary>
    /// 数据库文件完整路径
    /// </summary>
    private readonly string _dbPath;

    /// <summary>
    /// SQLite数据库连接对象
    /// 保持长连接以提高性能
    /// </summary>
    private SqliteConnection? _connection;

    /// <summary>
    /// 资源释放标志
    /// 防止重复释放
    /// </summary>
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建数据库服务实例
    /// 自动初始化数据库结构和连接
    /// </summary>
    /// <param name="dbPath">
    /// 数据库文件路径，为null时使用默认路径
    /// 默认路径：%LocalAppData%\WeChatAssistant\data.db
    /// </param>
    public DatabaseService(string? dbPath = null)
    {
        // 设置数据库路径
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WeChatAssistant",
            "data.db"
        );
        
        // 初始化数据库
        InitializeDatabase();
    }

    #endregion

    #region 数据库初始化

    /// <summary>
    /// 初始化数据库
    /// 创建数据库文件、目录结构和表结构
    /// </summary>
    /// <exception cref="Exception">数据库初始化失败时抛出异常</exception>
    private void InitializeDatabase()
    {
        try
        {
            // 确保数据库目录存在
            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建数据库连接
            // Data Source指定数据库文件路径
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            // 创建表结构
            CreateTables();
        }
        catch (Exception ex)
        {
            throw new Exception($"数据库初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建数据库表结构
    /// 包括消息表、定时任务表和索引
    /// </summary>
    private void CreateTables()
    {
        if (_connection == null) return;

        // 创建消息表
        // 存储抓取的所有聊天消息
        var createMessagesTable = @"
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupName TEXT NOT NULL,
                Sender TEXT NOT NULL,
                Content TEXT NOT NULL,
                MessageTime TEXT NOT NULL,
                CaptureTime TEXT NOT NULL,
                MessageId TEXT UNIQUE,
                IsSent INTEGER DEFAULT 0
            )";

        // 创建定时任务表
        // 存储用户配置的定时抓取/发送任务
        var createScheduleTasksTable = @"
            CREATE TABLE IF NOT EXISTS ScheduleTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                GroupName TEXT NOT NULL,
                CronExpression TEXT NOT NULL,
                IsEnabled INTEGER DEFAULT 1,
                LastRunTime TEXT,
                NextRunTime TEXT,
                ExportPath TEXT
            )";

        // 创建索引以提高查询性能
        // 群名称索引：用于按群查询消息
        // 消息时间索引：用于按时间范围查询
        // 消息ID索引：用于消息去重检查
        var createIndex = @"
            CREATE INDEX IF NOT EXISTS IX_Messages_GroupName ON Messages(GroupName);
            CREATE INDEX IF NOT EXISTS IX_Messages_MessageTime ON Messages(MessageTime);
            CREATE INDEX IF NOT EXISTS IX_Messages_MessageId ON Messages(MessageId)";

        using var cmd = _connection.CreateCommand();
        
        // 执行建表语句
        cmd.CommandText = createMessagesTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createScheduleTasksTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createIndex;
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 消息存储操作

    /// <summary>
    /// 保存单条消息到数据库
    /// 使用INSERT OR IGNORE实现消息去重（基于MessageId）
    /// </summary>
    /// <param name="message">要保存的消息对象</param>
    /// <returns>
    /// 新插入记录的ID，如果消息已存在则返回0
    /// </returns>
    public long SaveMessage(ChatMessage message)
    {
        if (_connection == null) return 0;

        // SQL语句使用INSERT OR IGNORE
        // 当MessageId已存在时忽略插入，实现去重
        var sql = @"
            INSERT OR IGNORE INTO Messages 
            (GroupName, Sender, Content, MessageTime, CaptureTime, MessageId, IsSent)
            VALUES (@GroupName, @Sender, @Content, @MessageTime, @CaptureTime, @MessageId, @IsSent);
            SELECT last_insert_rowid();";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        
        // 绑定参数
        cmd.Parameters.AddWithValue("@GroupName", message.GroupName);
        cmd.Parameters.AddWithValue("@Sender", message.Sender);
        cmd.Parameters.AddWithValue("@Content", message.Content);
        // 时间转换为ISO 8601格式字符串存储
        cmd.Parameters.AddWithValue("@MessageTime", message.MessageTime.ToString("O"));
        cmd.Parameters.AddWithValue("@CaptureTime", message.CaptureTime.ToString("O"));
        cmd.Parameters.AddWithValue("@MessageId", message.MessageId);
        cmd.Parameters.AddWithValue("@IsSent", message.IsSent ? 1 : 0);

        // 执行并返回新记录ID
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    /// <summary>
    /// 批量保存消息
    /// 使用事务提高性能，自动去重
    /// </summary>
    /// <param name="messages">要保存的消息集合</param>
    /// <returns>实际新插入的消息数量（排除重复）</returns>
    public int SaveMessages(IEnumerable<ChatMessage> messages)
    {
        if (_connection == null) return 0;

        int count = 0;
        var messageList = messages.ToList();
        
        // 使用事务批量插入
        using var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var msg in messageList)
            {
                if (SaveMessage(msg) > 0)
                    count++;
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            // 回退到逐行插入模式
            count = 0;
            foreach (var msg in messageList)
            {
                if (SaveMessage(msg) > 0)
                    count++;
            }
        }
        
        return count;
    }

    #endregion

    #region 消息查询操作

    /// <summary>
    /// 查询指定群的消息记录
    /// 支持时间范围过滤和数量限制
    /// </summary>
    /// <param name="groupName">群名称</param>
    /// <param name="startTime">开始时间（可选）</param>
    /// <param name="endTime">结束时间（可选）</param>
    /// <param name="limit">最大返回数量，默认1000条</param>
    /// <returns>消息列表，按时间倒序排列</returns>
    public List<ChatMessage> GetMessages(string groupName, DateTime? startTime = null, DateTime? endTime = null, int limit = 1000)
    {
        var messages = new List<ChatMessage>();
        if (_connection == null) return messages;

        // 构建SQL查询语句
        var sql = @"
            SELECT Id, GroupName, Sender, Content, MessageTime, CaptureTime, MessageId, IsSent
            FROM Messages 
            WHERE GroupName = @GroupName";

        // 添加时间范围条件
        if (startTime.HasValue)
            sql += " AND MessageTime >= @StartTime";
        if (endTime.HasValue)
            sql += " AND MessageTime <= @EndTime";

        // 按时间倒序，限制数量
        sql += " ORDER BY MessageTime DESC LIMIT @Limit";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        
        // 绑定参数
        cmd.Parameters.AddWithValue("@GroupName", groupName);
        cmd.Parameters.AddWithValue("@Limit", limit);

        if (startTime.HasValue)
            cmd.Parameters.AddWithValue("@StartTime", startTime.Value.ToString("O"));
        if (endTime.HasValue)
            cmd.Parameters.AddWithValue("@EndTime", endTime.Value.ToString("O"));

        // 执行查询并读取结果
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                GroupName = reader.GetString(1),
                Sender = reader.GetString(2),
                Content = reader.GetString(3),
                MessageTime = DateTime.Parse(reader.GetString(4)),
                CaptureTime = DateTime.Parse(reader.GetString(5)),
                MessageId = reader.GetString(6),
                IsSent = reader.GetInt32(7) == 1
            });
        }

        return messages;
    }

    /// <summary>
    /// 获取所有群名称列表
    /// 从消息记录中提取不重复的群名称
    /// </summary>
    /// <returns>群名称列表，按字母排序</returns>
    public List<string> GetGroupNames()
    {
        var groups = new List<string>();
        if (_connection == null) return groups;

        // 使用DISTINCT获取不重复的群名称
        var sql = "SELECT DISTINCT GroupName FROM Messages ORDER BY GroupName";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            groups.Add(reader.GetString(0));
        }

        return groups;
    }

    /// <summary>
    /// 获取指定群的消息总数
    /// </summary>
    /// <param name="groupName">群名称</param>
    /// <returns>消息数量</returns>
    public int GetMessageCount(string groupName)
    {
        if (_connection == null) return 0;

        var sql = "SELECT COUNT(*) FROM Messages WHERE GroupName = @GroupName";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@GroupName", groupName);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// 删除过期的消息记录
    /// 根据抓取时间清理旧数据
    /// </summary>
    /// <param name="daysToKeep">保留天数，默认30天</param>
    /// <returns>删除的记录数</returns>
    public int DeleteOldMessages(int daysToKeep = 30)
    {
        if (_connection == null) return 0;

        // 删除抓取时间早于截止日期的记录
        var sql = "DELETE FROM Messages WHERE CaptureTime < @CutoffTime";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CutoffTime", DateTime.Now.AddDays(-daysToKeep).ToString("O"));

        return cmd.ExecuteNonQuery();
    }

    #endregion

    #region 定时任务操作

    /// <summary>
    /// 保存新的定时任务
    /// </summary>
    /// <param name="task">任务对象</param>
    /// <returns>新任务的ID</returns>
    public int SaveScheduleTask(ScheduleTask task)
    {
        if (_connection == null) return 0;

        var sql = @"
            INSERT INTO ScheduleTasks (Name, GroupName, CronExpression, IsEnabled, LastRunTime, NextRunTime, ExportPath)
            VALUES (@Name, @GroupName, @CronExpression, @IsEnabled, @LastRunTime, @NextRunTime, @ExportPath);
            SELECT last_insert_rowid();";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Name", task.Name);
        cmd.Parameters.AddWithValue("@GroupName", task.GroupName);
        cmd.Parameters.AddWithValue("@CronExpression", task.CronExpression);
        cmd.Parameters.AddWithValue("@IsEnabled", task.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@LastRunTime", task.LastRunTime.ToString("O"));
        cmd.Parameters.AddWithValue("@NextRunTime", task.NextRunTime.ToString("O"));
        cmd.Parameters.AddWithValue("@ExportPath", task.ExportPath);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>
    /// 获取所有定时任务
    /// </summary>
    /// <returns>任务列表</returns>
    public List<ScheduleTask> GetScheduleTasks()
    {
        var tasks = new List<ScheduleTask>();
        if (_connection == null) return tasks;

        var sql = "SELECT Id, Name, GroupName, CronExpression, IsEnabled, LastRunTime, NextRunTime, ExportPath FROM ScheduleTasks";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(new ScheduleTask
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                GroupName = reader.GetString(2),
                CronExpression = reader.GetString(3),
                IsEnabled = reader.GetInt32(4) == 1,
                LastRunTime = DateTime.Parse(reader.GetString(5)),
                NextRunTime = DateTime.Parse(reader.GetString(6)),
                ExportPath = reader.GetString(7)
            });
        }

        return tasks;
    }

    /// <summary>
    /// 更新定时任务
    /// </summary>
    /// <param name="task">要更新的任务对象（必须包含有效ID）</param>
    public void UpdateScheduleTask(ScheduleTask task)
    {
        if (_connection == null) return;

        var sql = @"
            UPDATE ScheduleTasks 
            SET Name = @Name, GroupName = @GroupName, CronExpression = @CronExpression,
                IsEnabled = @IsEnabled, LastRunTime = @LastRunTime, NextRunTime = @NextRunTime,
                ExportPath = @ExportPath
            WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", task.Id);
        cmd.Parameters.AddWithValue("@Name", task.Name);
        cmd.Parameters.AddWithValue("@GroupName", task.GroupName);
        cmd.Parameters.AddWithValue("@CronExpression", task.CronExpression);
        cmd.Parameters.AddWithValue("@IsEnabled", task.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@LastRunTime", task.LastRunTime.ToString("O"));
        cmd.Parameters.AddWithValue("@NextRunTime", task.NextRunTime.ToString("O"));
        cmd.Parameters.AddWithValue("@ExportPath", task.ExportPath);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除定时任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void DeleteScheduleTask(int taskId)
    {
        if (_connection == null) return;

        var sql = "DELETE FROM ScheduleTasks WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", taskId);

        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 资源释放

    /// <summary>
    /// 释放数据库资源
    /// 关闭连接并清理资源
    /// </summary>
    public void Dispose()
    {
        // 防止重复释放
        if (_disposed) return;

        // 关闭并释放数据库连接
        _connection?.Close();
        _connection?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
