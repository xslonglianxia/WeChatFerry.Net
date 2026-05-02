using Microsoft.Data.Sqlite;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class DatabaseService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _disposed;

    public DatabaseService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WeChatAssistant",
            "data.db"
        );
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();

            CreateTables();
        }
        catch (Exception ex)
        {
            throw new Exception($"数据库初始化失败: {ex.Message}");
        }
    }

    private void CreateTables()
    {
        if (_connection == null) return;

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

        var createIndex = @"
            CREATE INDEX IF NOT EXISTS IX_Messages_GroupName ON Messages(GroupName);
            CREATE INDEX IF NOT EXISTS IX_Messages_MessageTime ON Messages(MessageTime);
            CREATE INDEX IF NOT EXISTS IX_Messages_MessageId ON Messages(MessageId)";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = createMessagesTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createScheduleTasksTable;
        cmd.ExecuteNonQuery();

        cmd.CommandText = createIndex;
        cmd.ExecuteNonQuery();
    }

    public long SaveMessage(ChatMessage message)
    {
        if (_connection == null) return 0;

        var sql = @"
            INSERT OR IGNORE INTO Messages 
            (GroupName, Sender, Content, MessageTime, CaptureTime, MessageId, IsSent)
            VALUES (@GroupName, @Sender, @Content, @MessageTime, @CaptureTime, @MessageId, @IsSent);
            SELECT last_insert_rowid();";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@GroupName", message.GroupName);
        cmd.Parameters.AddWithValue("@Sender", message.Sender);
        cmd.Parameters.AddWithValue("@Content", message.Content);
        cmd.Parameters.AddWithValue("@MessageTime", message.MessageTime.ToString("O"));
        cmd.Parameters.AddWithValue("@CaptureTime", message.CaptureTime.ToString("O"));
        cmd.Parameters.AddWithValue("@MessageId", message.MessageId);
        cmd.Parameters.AddWithValue("@IsSent", message.IsSent ? 1 : 0);

        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public int SaveMessages(IEnumerable<ChatMessage> messages)
    {
        int count = 0;
        foreach (var msg in messages)
        {
            if (SaveMessage(msg) > 0)
                count++;
        }
        return count;
    }

    public List<ChatMessage> GetMessages(string groupName, DateTime? startTime = null, DateTime? endTime = null, int limit = 1000)
    {
        var messages = new List<ChatMessage>();
        if (_connection == null) return messages;

        var sql = @"
            SELECT Id, GroupName, Sender, Content, MessageTime, CaptureTime, MessageId, IsSent
            FROM Messages 
            WHERE GroupName = @GroupName";

        if (startTime.HasValue)
            sql += " AND MessageTime >= @StartTime";
        if (endTime.HasValue)
            sql += " AND MessageTime <= @EndTime";

        sql += " ORDER BY MessageTime DESC LIMIT @Limit";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@GroupName", groupName);
        cmd.Parameters.AddWithValue("@Limit", limit);

        if (startTime.HasValue)
            cmd.Parameters.AddWithValue("@StartTime", startTime.Value.ToString("O"));
        if (endTime.HasValue)
            cmd.Parameters.AddWithValue("@EndTime", endTime.Value.ToString("O"));

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

    public List<string> GetGroupNames()
    {
        var groups = new List<string>();
        if (_connection == null) return groups;

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

    public int GetMessageCount(string groupName)
    {
        if (_connection == null) return 0;

        var sql = "SELECT COUNT(*) FROM Messages WHERE GroupName = @GroupName";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@GroupName", groupName);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int DeleteOldMessages(int daysToKeep = 30)
    {
        if (_connection == null) return 0;

        var sql = "DELETE FROM Messages WHERE CaptureTime < @CutoffTime";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CutoffTime", DateTime.Now.AddDays(-daysToKeep).ToString("O"));

        return cmd.ExecuteNonQuery();
    }

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

    public void DeleteScheduleTask(int taskId)
    {
        if (_connection == null) return;

        var sql = "DELETE FROM ScheduleTasks WHERE Id = @Id";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", taskId);

        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _connection?.Close();
        _connection?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
