using System.Collections.Concurrent;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class SchedulerService : IDisposable
{
    private readonly WeChatAutomationService _automationService;
    private readonly DatabaseService _databaseService;
    private readonly ExportService _exportService;
    private readonly ConcurrentDictionary<int, System.Timers.Timer> _timers;
    private bool _disposed;

    public event LogEventHandler? OnLog;
    public event EventHandler<ScheduleTask>? OnTaskExecuted;

    public SchedulerService(
        WeChatAutomationService automationService,
        DatabaseService databaseService,
        ExportService exportService)
    {
        _automationService = automationService;
        _databaseService = databaseService;
        _exportService = exportService;
        _timers = new ConcurrentDictionary<int, System.Timers.Timer>();
    }

    public void StartAllTasks()
    {
        var tasks = _databaseService.GetScheduleTasks().Where(t => t.IsEnabled);
        foreach (var task in tasks)
        {
            StartTask(task);
        }
        Log("INFO", "所有定时任务已启动");
    }

    public void StopAllTasks()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
            timer.Dispose();
        }
        _timers.Clear();
        Log("INFO", "所有定时任务已停止");
    }

    public void StartTask(ScheduleTask task)
    {
        if (_timers.ContainsKey(task.Id))
        {
            StopTask(task.Id);
        }

        var interval = CalculateInterval(task.CronExpression);
        if (interval <= 0)
        {
            Log("ERROR", $"任务 [{task.Name}] 的Cron表达式无效");
            return;
        }

        var timer = new System.Timers.Timer(interval);
        timer.Elapsed += (s, e) => ExecuteTask(task);
        timer.AutoReset = true;
        timer.Start();

        _timers[task.Id] = timer;
        task.NextRunTime = DateTime.Now.AddMilliseconds(interval);
        _databaseService.UpdateScheduleTask(task);

        Log("INFO", $"任务 [{task.Name}] 已启动，下次执行: {task.NextRunTime:HH:mm:ss}");
    }

    public void StopTask(int taskId)
    {
        if (_timers.TryRemove(taskId, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            Log("INFO", $"任务已停止");
        }
    }

    private void ExecuteTask(ScheduleTask task)
    {
        try
        {
            Log("INFO", $"开始执行任务 [{task.Name}]");

            var result = _automationService.CaptureGroupMessages(task.GroupName);
            
            if (result.Success && result.Messages.Any())
            {
                foreach (var msg in result.Messages)
                {
                    msg.GroupName = task.GroupName;
                }
                _databaseService.SaveMessages(result.Messages);

                if (!string.IsNullOrEmpty(task.ExportPath))
                {
                    var fileName = $"chat_{task.GroupName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    var filePath = Path.Combine(task.ExportPath, fileName);
                    _exportService.ExportToTxt(result.Messages, filePath, task.GroupName);
                    Log("INFO", $"已导出到: {filePath}");
                }

                var formattedMessage = _exportService.FormatMessagesForSend(result.Messages);
                var sendResult = _automationService.SendMessage(task.GroupName, formattedMessage);
                
                if (sendResult.Success)
                {
                    Log("INFO", $"任务 [{task.Name}] 执行成功，已发送 {result.Messages.Count} 条消息汇总");
                }
                else
                {
                    Log("ERROR", $"发送消息失败: {sendResult.Message}");
                }
            }
            else
            {
                Log("WARN", $"任务 [{task.Name}] 未抓取到消息");
            }

            task.LastRunTime = DateTime.Now;
            var nextInterval = CalculateInterval(task.CronExpression);
            task.NextRunTime = DateTime.Now.AddMilliseconds(nextInterval);
            _databaseService.UpdateScheduleTask(task);

            OnTaskExecuted?.Invoke(this, task);
        }
        catch (Exception ex)
        {
            Log("ERROR", $"任务 [{task.Name}] 执行失败: {ex.Message}");
        }
    }

    private double CalculateInterval(string cronExpression)
    {
        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length != 5) return -1;

            var minute = parts[0];
            var hour = parts[1];

            if (minute == "*" && hour == "*")
            {
                return TimeSpan.FromMinutes(1).TotalMilliseconds;
            }

            if (minute != "*" && hour == "*")
            {
                if (int.TryParse(minute, out var minInterval))
                {
                    return TimeSpan.FromMinutes(minInterval).TotalMilliseconds;
                }
            }

            if (minute != "*" && hour != "*")
            {
                if (int.TryParse(minute, out var min) && int.TryParse(hour, out var hr))
                {
                    var now = DateTime.Now;
                    var nextRun = new DateTime(now.Year, now.Month, now.Day, hr, min, 0);
                    if (nextRun <= now)
                    {
                        nextRun = nextRun.AddDays(1);
                    }
                    return (nextRun - now).TotalMilliseconds;
                }
            }

            if (minute.StartsWith("*/") && hour == "*")
            {
                if (int.TryParse(minute.Substring(2), out var interval))
                {
                    return TimeSpan.FromMinutes(interval).TotalMilliseconds;
                }
            }

            return TimeSpan.FromHours(1).TotalMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    private void Log(string level, string message)
    {
        OnLog?.Invoke(this, new LogEventArgs
        {
            Level = level,
            Message = message,
            Time = DateTime.Now
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopAllTasks();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
