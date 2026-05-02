using System.Text.Json;

namespace WeChatAssistant.Core.Services;

/// <summary>
/// 配置服务
/// 负责读取和管理应用配置
/// </summary>
public static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    private static AppConfig? _config;
    private static readonly object _lock = new object();

    /// <summary>
    /// 获取配置
    /// </summary>
    public static AppConfig Config
    {
        get
        {
            if (_config == null)
            {
                LoadConfig();
            }
            return _config!;
        }
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    private static void LoadConfig()
    {
        lock (_lock)
        {
            if (_config != null) return;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    _config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                }

                // 使用默认配置
                _config ??= new AppConfig
                {
                    Logging = new LoggingConfig
                    {
                        LogLevel = "INFO"
                    },
                    Database = new DatabaseConfig(),
                    Capture = new CaptureConfig
                    {
                        MaxMessages = 100,
                        ScrollDelay = 300,
                        CaptureDelay = 500
                    },
                    Schedule = new ScheduleConfig()
                };
            }
            catch
            {
                // 如果加载失败，使用默认配置
                _config = new AppConfig
                {
                    Logging = new LoggingConfig
                    {
                        LogLevel = "INFO"
                    },
                    Database = new DatabaseConfig(),
                    Capture = new CaptureConfig
                    {
                        MaxMessages = 100,
                        ScrollDelay = 300,
                        CaptureDelay = 500
                    },
                    Schedule = new ScheduleConfig()
                };
            }
        }
    }
}

/// <summary>
/// 应用配置
/// </summary>
public class AppConfig
{
    public LoggingConfig? Logging { get; set; }
    public DatabaseConfig? Database { get; set; }
    public CaptureConfig? Capture { get; set; }
    public ScheduleConfig? Schedule { get; set; }
}

/// <summary>
/// 日志配置
/// </summary>
public class LoggingConfig
{
    public string? LogLevel { get; set; }
    public string? LogPath { get; set; }
}

/// <summary>
/// 数据库配置
/// </summary>
public class DatabaseConfig
{
    public string? Path { get; set; }
}

/// <summary>
/// 消息抓取配置
/// </summary>
public class CaptureConfig
{
    public int MaxMessages { get; set; }
    public int ScrollDelay { get; set; }
    public int CaptureDelay { get; set; }
}

/// <summary>
/// 定时任务配置
/// </summary>
public class ScheduleConfig
{
    public string? DefaultExportPath { get; set; }
}
