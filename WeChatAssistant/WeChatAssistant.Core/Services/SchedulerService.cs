using System.Collections.Concurrent;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

/// <summary>
/// 定时任务调度服务
/// 管理定时抓取和发送任务的执行
/// 支持基于简化Cron表达式的时间调度
/// </summary>
/// <remarks>
/// 功能说明：
/// - 支持多个定时任务并行运行
/// - 每个任务独立计时，互不干扰
/// - 任务执行时自动抓取消息并发送汇总
/// 
/// 使用方式：
/// 1. 创建SchedulerService实例，注入依赖服务
/// 2. 调用StartAllTasks()启动所有已启用的任务
/// 3. 调用StopAllTasks()停止所有任务
/// 4. 可单独启动/停止某个任务
/// </remarks>
public class SchedulerService : IDisposable
{
    #region 私有字段

    /// <summary>
    /// 微信自动化服务实例
    /// 用于执行消息抓取和发送操作
    /// </summary>
    private readonly WeChatAutomationService _automationService;

    /// <summary>
    /// 数据库服务实例
    /// 用于读取/保存任务配置和消息数据
    /// </summary>
    private readonly DatabaseService _databaseService;

    /// <summary>
    /// 导出服务实例
    /// 用于导出聊天记录到文件
    /// </summary>
    private readonly ExportService _exportService;

    /// <summary>
    /// 任务计时器字典
    /// Key：任务ID
    /// Value：对应的Timer对象
    /// 使用ConcurrentDictionary保证线程安全
    /// </summary>
    private readonly ConcurrentDictionary<int, System.Timers.Timer> _timers;

    /// <summary>
    /// 资源释放标志
    /// </summary>
    private bool _disposed;

    #endregion

    #region 事件定义

    /// <summary>
    /// 日志事件
    /// 输出任务执行过程中的日志信息
    /// </summary>
    public event LogEventHandler? OnLog;

    /// <summary>
    /// 任务执行完成事件
    /// 当任务执行完毕时触发，可用于更新界面显示
    /// </summary>
    public event EventHandler<ScheduleTask>? OnTaskExecuted;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建定时任务调度服务实例
    /// </summary>
    /// <param name="automationService">微信自动化服务</param>
    /// <param name="databaseService">数据库服务</param>
    /// <param name="exportService">导出服务</param>
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

    #endregion

    #region 任务管理

    /// <summary>
    /// 启动所有已启用的定时任务
    /// 从数据库读取任务列表并创建对应的计时器
    /// </summary>
    public void StartAllTasks()
    {
        // 从数据库获取所有已启用的任务
        var tasks = _databaseService.GetScheduleTasks().Where(t => t.IsEnabled);
        
        foreach (var task in tasks)
        {
            StartTask(task);
        }
        
        Log("INFO", "所有定时任务已启动");
    }

    /// <summary>
    /// 停止所有定时任务
    /// 清理所有计时器资源
    /// </summary>
    public void StopAllTasks()
    {
        // 遍历所有计时器，停止并释放
        foreach (var timer in _timers.Values)
        {
            timer.Stop();
            timer.Dispose();
        }
        
        // 清空计时器字典
        _timers.Clear();
        
        Log("INFO", "所有定时任务已停止");
    }

    /// <summary>
    /// 启动单个定时任务
    /// 创建计时器并设置执行间隔
    /// </summary>
    /// <param name="task">要启动的任务对象</param>
    public void StartTask(ScheduleTask task)
    {
        // 如果任务已存在计时器，先停止
        if (_timers.ContainsKey(task.Id))
        {
            StopTask(task.Id);
        }

        // 计算执行间隔（毫秒）
        var interval = CalculateInterval(task.CronExpression);
        if (interval <= 0)
        {
            Log("ERROR", $"任务 [{task.Name}] 的Cron表达式无效");
            return;
        }

        // 创建计时器
        var timer = new System.Timers.Timer(interval);
        
        // 绑定执行事件
        timer.Elapsed += (s, e) => ExecuteTask(task);
        
        // 设置为重复执行
        timer.AutoReset = true;
        timer.Start();

        // 添加到字典
        _timers[task.Id] = timer;

        // 更新任务的下次执行时间
        task.NextRunTime = DateTime.Now.AddMilliseconds(interval);
        _databaseService.UpdateScheduleTask(task);

        Log("INFO", $"任务 [{task.Name}] 已启动，下次执行: {task.NextRunTime:HH:mm:ss}");
    }

    /// <summary>
    /// 停止单个定时任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void StopTask(int taskId)
    {
        // 从字典中移除并释放计时器
        if (_timers.TryRemove(taskId, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            Log("INFO", $"任务已停止");
        }
    }

    #endregion

    #region 任务执行

    /// <summary>
    /// 执行定时任务
    /// 完成消息抓取、存储、导出和发送的完整流程
    /// </summary>
    /// <param name="task">要执行的任务对象</param>
    /// <remarks>
    /// 执行流程：
    /// 1. 抓取指定群的聊天消息
    /// 2. 将消息保存到数据库
    /// 3. 如果配置了导出路径，导出到文件
    /// 4. 格式化消息并发送到群
    /// 5. 更新任务的执行时间
    /// </remarks>
    private void ExecuteTask(ScheduleTask task)
    {
        try
        {
            Log("INFO", $"开始执行任务 [{task.Name}]");

            // 步骤1：抓取消息
            var result = _automationService.CaptureGroupMessages(task.GroupName);
            
            if (result.Success && result.Messages.Any())
            {
                // 步骤2：设置群名称并保存到数据库
                foreach (var msg in result.Messages)
                {
                    msg.GroupName = task.GroupName;
                }
                _databaseService.SaveMessages(result.Messages);

                // 步骤3：导出到文件（如果配置了路径）
                if (!string.IsNullOrEmpty(task.ExportPath))
                {
                    var fileName = $"chat_{task.GroupName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                    var filePath = Path.Combine(task.ExportPath, fileName);
                    _exportService.ExportToTxt(result.Messages, filePath, task.GroupName);
                    Log("INFO", $"已导出到: {filePath}");
                }

                // 步骤4：格式化并发送消息汇总
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

            // 步骤5：更新任务执行时间
            task.LastRunTime = DateTime.Now;
            var nextInterval = CalculateInterval(task.CronExpression);
            task.NextRunTime = DateTime.Now.AddMilliseconds(nextInterval);
            _databaseService.UpdateScheduleTask(task);

            // 触发任务执行完成事件
            OnTaskExecuted?.Invoke(this, task);
        }
        catch (Exception ex)
        {
            Log("ERROR", $"任务 [{task.Name}] 执行失败: {ex.Message}");
        }
    }

    #endregion

    #region Cron解析

    /// <summary>
    /// 计算Cron表达式对应的执行间隔（毫秒）
    /// 使用简化的Cron解析，支持常用的时间模式
    /// </summary>
    /// <param name="cronExpression">Cron表达式（5字段格式）</param>
    /// <returns>执行间隔（毫秒），无效表达式返回-1</returns>
    /// <remarks>
    /// 支持的Cron格式：
    /// - "分 时 * * *"：每天指定时间执行
    /// - "* * * * *"：每分钟执行
    /// - "*/N * * * *"：每N分钟执行
    ///
    /// 示例：
    /// - "0 9 * * *"：每天9:00执行
    /// - "*/30 * * * *"：每30分钟执行
    /// - "0 * * * *"：每小时执行
    /// </remarks>
    private double CalculateInterval(string cronExpression)
    {
        try
        {
            // Cron表达式格式：分 时 日 月 周
            var parts = cronExpression.Split(' ');
            if (parts.Length != 5)
            {
                Log("WARN", $"无效的Cron表达式格式: {cronExpression}，需要5个字段");
                return -1;
            }

            var minute = parts[0];  // 分钟字段
            var hour = parts[1];    // 小时字段

            // 情况1：每分钟执行 "* * * * *"
            if (minute == "*" && hour == "*")
            {
                Log("DEBUG", "Cron表达式解析为每分钟执行");
                return TimeSpan.FromMinutes(1).TotalMilliseconds;
            }

            // 情况2：每N分钟执行 "*/N * * * *"
            if (minute.StartsWith("*/") && hour == "*")
            {
                if (int.TryParse(minute.Substring(2), out var interval))
                {
                    if (interval <= 0 || interval > 60)
                    {
                        Log("WARN", $"Cron表达式中的分钟间隔无效: {interval}，必须在1-60之间");
                        return -1;
                    }
                    Log("DEBUG", $"Cron表达式解析为每{interval}分钟执行");
                    return TimeSpan.FromMinutes(interval).TotalMilliseconds;
                }
            }

            // 情况3：每天指定时间执行 "M H * * *"
            if (minute != "*" && hour != "*")
            {
                if (int.TryParse(minute, out var min) && int.TryParse(hour, out var hr))
                {
                    if (min < 0 || min > 59 || hr < 0 || hr > 23)
                    {
                        Log("WARN", $"Cron表达式中的时间无效: {hr}:{min}");
                        return -1;
                    }

                    var now = DateTime.Now;
                    // 计算下次执行时间
                    var nextRun = new DateTime(now.Year, now.Month, now.Day, hr, min, 0);

                    // 如果今天的时间已过，设置为明天
                    if (nextRun <= now)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    Log("DEBUG", $"Cron表达式解析为每天{hr:D2}:{min:D2}执行，下次执行时间: {nextRun:yyyy-MM-dd HH:mm:ss}");
                    // 返回距离下次执行的毫秒数
                    return (nextRun - now).TotalMilliseconds;
                }
            }

            // 情况4：每小时执行 "0 * * * *"
            if (minute != "*" && hour == "*")
            {
                if (int.TryParse(minute, out var minInterval))
                {
                    Log("DEBUG", $"Cron表达式解析为每小时第{minInterval}分钟执行");
                    return TimeSpan.FromMinutes(minInterval).TotalMilliseconds;
                }
            }

            Log("WARN", $"无法解析的Cron表达式: {cronExpression}，使用默认每小时执行");
            // 默认：每小时执行
            return TimeSpan.FromHours(1).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"解析Cron表达式时出错: {ex.Message}");
            return -1;
        }
    }

    #endregion

    #region 日志输出

    /// <summary>
    /// 输出日志信息
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志消息</param>
    private void Log(string level, string message)
    {
        OnLog?.Invoke(this, new LogEventArgs
        {
            Level = level,
            Message = message,
            Time = DateTime.Now
        });
    }

    #endregion

    #region 资源释放

    /// <summary>
    /// 释放资源
    /// 停止所有任务并清理计时器
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        StopAllTasks();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
