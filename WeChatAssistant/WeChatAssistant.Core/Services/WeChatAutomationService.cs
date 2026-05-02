using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Windows.Forms;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

/// <summary>
/// 微信自动化服务核心类
/// 使用FlaUI框架通过UI自动化技术操作微信PC客户端
/// 实现消息抓取、消息发送等核心功能
/// </summary>
/// <remarks>
/// 技术原理：
/// 1. 使用UI Automation技术访问微信窗口的UI元素树
/// 2. 通过控件类型、自动化ID、名称等属性定位目标控件
/// 3. 模拟用户操作（点击、输入、快捷键）完成自动化任务
/// 
/// 注意事项：
/// - 需要微信PC客户端已启动并登录
/// - 微信窗口需要可见（部分操作需要窗口前台）
/// - 微信版本更新可能导致控件定位失效
/// - 必须使用 STA 线程模式
/// </remarks>
public class WeChatAutomationService : IDisposable
{
    #region 常量定义

    /// <summary>
    /// 微信主窗口的窗口类名
    /// 用于通过类名查找微信窗口
    /// </summary>
    private readonly string _wechatClassName = "WeChatMainWndForPC";

    /// <summary>
    /// 微信进程名称
    /// 用于检测微信是否运行
    /// </summary>
    private readonly string _wechatProcessName = "WeChat";

    /// <summary>
    /// 查找窗口超时时间（毫秒）
    /// </summary>
    private const int FindWindowTimeoutMs = 5000;

    #endregion

    #region 私有字段

    /// <summary>
    /// UI自动化框架实例
    /// 使用UIA3实现（Windows UI Automation 3.0）
    /// </summary>
    private AutomationBase? _automation;

    /// <summary>
    /// 微信主窗口引用
    /// 缓存窗口对象以提高性能
    /// </summary>
    private Window? _wechatWindow;

    /// <summary>
    /// 资源释放标志
    /// 防止重复释放资源
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 服务初始化标志
    /// 标记自动化服务是否成功初始化
    /// </summary>
    private bool _isInitialized;

    #endregion

    #region 事件定义

    /// <summary>
    /// 日志事件
    /// 当服务产生日志信息时触发，用于界面显示日志
    /// </summary>
    public event LogEventHandler? OnLog;

    /// <summary>
    /// 状态变更事件
    /// 当服务运行状态发生变化时触发，用于更新界面状态显示
    /// </summary>
    public event StatusChangedEventHandler? OnStatusChanged;

    #endregion

    #region 公共属性

    /// <summary>
    /// 检查微信是否正在运行且窗口可用
    /// </summary>
    public bool IsWeChatRunning => _wechatWindow != null && !_wechatWindow.IsOffscreen;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化微信自动化服务实例
    /// 创建UI自动化对象并配置超时参数
    /// </summary>
    public WeChatAutomationService()
    {
        Initialize();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化UI自动化框架
    /// 配置自动化参数，准备连接微信
    /// </summary>
    private void Initialize()
    {
        try
        {
            // 检查线程 Apartment 状态
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Log("WARN", "当前线程不是 STA 模式，这可能导致 UI Automation 无法正常工作");
                Log("INFO", "建议使用 STA 线程或在主线程中调用此服务");
            }

            // 创建UIA3自动化实例
            _automation = new UIA3Automation();
            
            // 配置控件查找超时时间（3秒）
            // 超时过短可能导致查找失败，过长影响响应速度
            _automation.Configuration.TransitionTimeout = TimeSpan.FromSeconds(3);
            
            // 配置元素等待超时时间（5秒）
            // 用于等待动态加载的UI元素
            _automation.Configuration.WaitForElementTimeout = TimeSpan.FromSeconds(5);
            
            // 配置查找失败时不抛出异常
            _automation.Configuration.AlwaysTryFindChildren = false;
            
            _isInitialized = true;
            Log("INFO", "UI自动化服务初始化成功");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"初始化失败: {ex.Message}");
            _isInitialized = false;
        }
    }

    #endregion

    #region 连接管理

    /// <summary>
    /// 连接到微信窗口
    /// 查找并缓存微信主窗口对象，后续操作都基于此窗口
    /// </summary>
    /// <returns>
    /// true：成功连接到微信窗口
    /// false：连接失败（微信未启动或窗口不可访问）
    /// </returns>
    /// <remarks>
    /// 连接流程：
    /// 1. 首先检查线程 Apartment 状态
    /// 2. 检查微信进程是否存在
    /// 3. 使用多策略查找窗口
    /// 4. 带超时控制，避免无限等待
    /// </remarks>
    public bool ConnectToWeChat()
    {
        // 检查自动化服务是否已初始化
        if (!_isInitialized || _automation == null)
        {
            Log("ERROR", "自动化服务未初始化");
            return false;
        }

        try
        {
            Log("INFO", "开始连接微信窗口...");
            var startTime = DateTime.Now;

            // 首先检查微信进程是否存在
            var processes = System.Diagnostics.Process.GetProcessesByName(_wechatProcessName);
            if (processes.Length == 0)
            {
                Log("WARN", "未找到微信进程，请确保微信PC客户端已启动");
                return false;
            }

            Log("INFO", $"检测到微信进程 (PID: {processes[0].Id})");

            // 尝试多种方法查找窗口
            _wechatWindow = TryFindWindowWithStrategies(processes[0]);

            // 检查连接结果
            if (_wechatWindow != null)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log("INFO", $"成功连接到微信窗口 (耗时: {elapsed:F0}ms)");
                
                // 验证窗口是否有效
                if (_wechatWindow.IsOffscreen)
                {
                    Log("WARN", "微信窗口不可见，可能被最小化或隐藏");
                }
                
                // 触发状态变更事件
                OnStatusChanged?.Invoke(this, new StatusEventArgs { Status = "已连接", IsRunning = true });
                return true;
            }

            // 所有方法都失败
            Log("WARN", "未找到微信窗口，请确保微信PC客户端已启动并可见");
            return false;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"连接微信失败: {ex.Message}");
            Log("ERROR", $"异常类型: {ex.GetType().Name}");
            return false;
        }
    }

    /// <summary>
    /// 使用多种策略查找窗口
    /// </summary>
    private Window? TryFindWindowWithStrategies(System.Diagnostics.Process process)
    {
        // 策略1：使用原生 Windows API 直接查找
        Log("INFO", "策略1: 使用原生 Windows API 查找...");
        var window = FindWindowByNativeApi();
        if (window != null) return window;

        // 策略2：使用进程主窗口句柄
        if (process.MainWindowHandle != IntPtr.Zero)
        {
            Log("INFO", $"策略2: 使用进程主窗口句柄 (0x{process.MainWindowHandle:X})...");
            window = FindWindowByHandle(process.MainWindowHandle);
            if (window != null) return window;
        }

        // 策略3：带超时的 FlaUI 查找
        Log("INFO", $"策略3: 使用 FlaUI 带超时查找 (超时: {FindWindowTimeoutMs}ms)...");
        window = FindWindowByFlaUIWithTimeout();
        if (window != null) return window;

        // 策略4：重试机制
        Log("INFO", "策略4: 使用重试机制...");
        for (int i = 0; i < 3; i++)
        {
            Log($"INFO", $"重试 {i + 1}/3...");
            Thread.Sleep(1000);
            
            window = FindWindowByNativeApi();
            if (window != null) return window;
            
            window = FindWindowByHandle(process.MainWindowHandle);
            if (window != null) return window;
        }

        return null;
    }

    /// <summary>
    /// 使用原生 Windows API 查找窗口
    /// </summary>
    private Window? FindWindowByNativeApi()
    {
        try
        {
            IntPtr windowHandle = IntPtr.Zero;

            // 定义回调函数
            EnumWindowsDelegate callback = (hWnd, lParam) =>
            {
                // 检查窗口是否可见
                if (!IsWindowVisible(hWnd))
                    return true;

                // 获取窗口类名
                var className = new System.Text.StringBuilder(256);
                GetClassName(hWnd, className, 256);

                // 检查是否是微信窗口
                if (className.ToString().Contains("WeChat"))
                {
                    windowHandle = hWnd;
                    Log("DEBUG", $"找到微信相关窗口: {className} (0x{hWnd:X})");
                    return false; // 停止枚举
                }

                return true; // 继续枚举
            };

            // 枚举所有窗口
            if (!EnumWindows(callback, IntPtr.Zero))
            {
                if (windowHandle != IntPtr.Zero)
                {
                    Log("INFO", $"通过原生 API 找到窗口，句柄: 0x{windowHandle:X}");
                    return _automation!.GetDesktop().FromHandle(windowHandle)?.AsWindow();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"原生 API 查找失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 使用窗口句柄查找窗口
    /// </summary>
    private Window? FindWindowByHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var element = _automation!.GetDesktop().FromHandle(handle);
            if (element != null)
            {
                var window = element.AsWindow();
                if (window != null)
                {
                    // 验证窗口
                    var className = window.Properties.ClassName.ValueOrDefault;
                    Log("INFO", $"通过句柄找到窗口，类名: {className}");
                    return window;
                }
            }
        }
        catch (Exception ex)
        {
            Log("DEBUG", $"句柄查找失败: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 使用 FlaUI 带超时查找
    /// </summary>
    private Window? FindWindowByFlaUIWithTimeout()
    {
        try
        {
            var desktop = _automation!.GetDesktop();
            var startTime = DateTime.Now;
            var maxRetries = 5;

            for (int i = 0; i < maxRetries; i++)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (elapsed >= FindWindowTimeoutMs)
                {
                    Log("DEBUG", $"FlaUI 查找超时 ({elapsed:F0}ms)");
                    break;
                }

                Log($"DEBUG", $"FlaUI 查找尝试 {i + 1}/{maxRetries}...");

                try
                {
                    var window = desktop.FindFirstChild(cf => cf.ByClassName(_wechatClassName))?.AsWindow();
                    if (window != null)
                    {
                        Log("INFO", $"FlaUI 找到窗口");
                        return window;
                    }
                }
                catch (Exception ex)
                {
                    Log($"DEBUG", $"FlaUI 查找异常: {ex.Message}");
                }

                // 等待后重试
                if (i < maxRetries - 1)
                {
                    Thread.Sleep(500);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"FlaUI 查找失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 确保微信已连接
    /// 如果未连接则尝试重新连接
    /// </summary>
    /// <returns>true：已连接；false：连接失败</returns>
    private bool EnsureWeChatConnected()
    {
        // 检查窗口是否有效（非空且未离屏）
        if (_wechatWindow == null || _wechatWindow.IsOffscreen)
        {
            return ConnectToWeChat();
        }
        return true;
    }

    #endregion

    #region 原生 Windows API

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

    #endregion

    #region 群列表获取

    /// <summary>
    /// 获取微信聊天列表中的所有群/联系人
    /// 通过遍历聊天列表控件提取所有条目
    /// </summary>
    /// <returns>群名称列表，失败时返回空列表</returns>
    /// <remarks>
    /// 实现原理：
    /// 1. 定位聊天列表控件
    /// 2. 遍历所有ListItem类型的子元素
    /// 3. 从每个ListItem中提取显示名称
    /// </remarks>
    public List<string> GetGroupList()
    {
        var groups = new List<string>();
        
        // 确保微信已连接
        if (!EnsureWeChatConnected())
        {
            return groups;
        }

        try
        {
            // 激活微信窗口到前台
            ActivateWindow();
            
            // 等待窗口激活完成
            Thread.Sleep(300);

            // 查找聊天列表控件
            var chatList = FindChatList();
            if (chatList == null)
            {
                Log("WARN", "未找到聊天列表");
                return groups;
            }

            // 查找所有列表项（每个列表项代表一个聊天）
            var listItems = chatList.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            
            // 遍历提取名称
            foreach (var item in listItems)
            {
                try
                {
                    // 在列表项中查找文本控件，文本控件包含显示名称
                    var nameElement = item.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text));
                    if (nameElement != null)
                    {
                        var name = nameElement.Name;
                        // 去重添加
                        if (!string.IsNullOrEmpty(name) && !groups.Contains(name))
                        {
                            groups.Add(name);
                        }
                    }
                }
                catch
                {
                    // 忽略单个项目解析错误，继续处理其他项目
                }
            }

            Log("INFO", $"获取到 {groups.Count} 个聊天项");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"获取群列表失败: {ex.Message}");
        }

        return groups;
    }

    #endregion

    #region 消息抓取

    /// <summary>
    /// 抓取指定群的聊天记录
    /// 自动选择目标群并提取消息列表
    /// </summary>
    /// <param name="groupName">目标群名称</param>
    /// <param name="maxMessages">最大抓取消息数量，默认100条</param>
    /// <returns>抓取结果，包含成功状态、消息列表等信息</returns>
    /// <remarks>
    /// 抓取流程：
    /// 1. 连接并激活微信窗口
    /// 2. 通过搜索或列表选择目标群
    /// 3. 定位消息列表区域
    /// 4. 遍历消息元素并解析内容
    /// </remarks>
    public CaptureResult CaptureGroupMessages(string groupName, int maxMessages = 100)
    {
        var result = new CaptureResult();

        // 确保微信已连接
        if (!EnsureWeChatConnected())
        {
            result.Message = "微信未连接";
            return result;
        }

        try
        {
            Log("INFO", $"开始抓取群 [{groupName}] 的聊天记录");
            
            // 激活微信窗口
            ActivateWindow();
            Thread.Sleep(500);

            // 选择目标群聊天窗口
            if (!SelectChatByName(groupName))
            {
                result.Message = $"未找到群 [{groupName}]";
                Log("WARN", result.Message);
                return result;
            }

            // 等待聊天内容加载
            Thread.Sleep(500);

            // 提取消息列表
            var messages = ExtractMessagesFromChat(maxMessages);
            
            // 构建成功结果
            result.Success = true;
            result.CapturedCount = messages.Count;
            result.Messages = messages;
            result.Message = $"成功抓取 {messages.Count} 条消息";

            Log("INFO", result.Message);
        }
        catch (Exception ex)
        {
            result.Message = $"抓取失败: {ex.Message}";
            Log("ERROR", result.Message);
        }

        return result;
    }

    /// <summary>
    /// 从当前聊天窗口提取消息列表
    /// 遍历消息列表控件，解析每条消息的详细信息
    /// </summary>
    /// <param name="maxMessages">最大提取数量</param>
    /// <returns>解析得到的消息列表</returns>
    /// <remarks>
    /// 消息解析策略：
    /// 1. 查找消息列表容器
    /// 2. 遍历所有ListItem类型的消息元素
    /// 3. 从每个消息元素中提取：发送者、内容、时间
    /// 4. 根据文本特征区分时间戳和消息内容
    /// </remarks>
    private List<ChatMessage> ExtractMessagesFromChat(int maxMessages)
    {
        var messages = new List<ChatMessage>();

        try
        {
            // 查找消息列表区域
            var messageList = FindMessageList();
            if (messageList == null)
            {
                Log("WARN", "未找到消息列表区域");
                return messages;
            }

            // 查找所有消息项（ListItem类型）
            var messageItems = messageList.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            
            int count = 0;
            foreach (var item in messageItems)
            {
                // 达到最大数量限制
                if (count >= maxMessages) break;

                try
                {
                    // 解析单条消息
                    var msg = ParseMessageItem(item);
                    if (msg != null && !string.IsNullOrWhiteSpace(msg.Content))
                    {
                        messages.Add(msg);
                        count++;
                    }
                }
                catch
                {
                    // 忽略单条消息解析错误
                }
            }

            // 消息列表是倒序的（最新在上），需要反转为正序
            messages.Reverse();
        }
        catch (Exception ex)
        {
            Log("ERROR", $"提取消息失败: {ex.Message}");
        }

        return messages;
    }

    /// <summary>
    /// 解析单个消息元素
    /// 从UI元素中提取发送者、内容、时间等信息
    /// </summary>
    /// <param name="item">消息UI元素</param>
    /// <returns>解析得到的消息对象，解析失败返回null</returns>
    /// <remarks>
    /// 解析逻辑：
    /// 1. 查找消息元素中的所有文本控件
    /// 2. 根据格式特征识别时间戳（如"12:30"、"昨天"等）
    /// 3. 较短的文本可能是发送者名称
    /// 4. 较长的文本通常是消息内容
    /// </remarks>
    private ChatMessage? ParseMessageItem(AutomationElement item)
    {
        try
        {
            // 创建消息对象
            var msg = new ChatMessage
            {
                CaptureTime = DateTime.Now,
                MessageId = Guid.NewGuid().ToString("N")  // 生成唯一ID用于去重
            };

            // 查找消息中的所有文本元素
            var allTexts = item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            
            string? sender = null;
            string? content = null;
            string? timeStr = null;

            // 遍历所有文本元素进行分类
            foreach (var text in allTexts)
            {
                var textValue = text.Name;
                if (string.IsNullOrWhiteSpace(textValue)) continue;

                // 判断是否为时间格式
                if (IsTimeFormat(textValue))
                {
                    timeStr = textValue;
                }
                // 较短的文本（<20字符）可能是发送者名称
                else if (sender == null && textValue.Length < 20)
                {
                    sender = textValue;
                }
                // 较长的文本作为消息内容（取最长的）
                else if (content == null || textValue.Length > (content?.Length ?? 0))
                {
                    content = textValue;
                }
            }

            // 必须有内容才算有效消息
            if (!string.IsNullOrWhiteSpace(content))
            {
                msg.Sender = sender ?? "未知";
                msg.Content = content;
                msg.MessageTime = ParseTime(timeStr);
                return msg;
            }
        }
        catch
        {
            // 解析失败返回null
        }

        return null;
    }

    /// <summary>
    /// 判断文本是否为时间格式
    /// 支持多种时间格式的识别
    /// </summary>
    /// <param name="text">待判断的文本</param>
    /// <returns>true：是时间格式；false：不是时间格式</returns>
    private bool IsTimeFormat(string text)
    {
        // 匹配以下时间格式：
        // - HH:mm（如 12:30）
        // - yyyy-MM-dd（如 2024-01-15）
        // - yyyy/MM/dd（如 2024/01/15）
        // - 中文日期（昨天、前天、星期X）
        return System.Text.RegularExpressions.Regex.IsMatch(text, 
            @"^\d{1,2}:\d{2}$|^\d{4}-\d{2}-\d{2}$|^\d{4}/\d{2}/\d{2}$|昨天|前天|星期");
    }

    /// <summary>
    /// 解析时间字符串为DateTime对象
    /// 处理相对时间（昨天、前天）和绝对时间
    /// </summary>
    /// <param name="timeStr">时间字符串</param>
    /// <returns>解析后的DateTime，解析失败返回当前时间</returns>
    private DateTime ParseTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            return DateTime.Now;

        try
        {
            // 处理相对时间
            if (timeStr.Contains("昨天"))
                return DateTime.Today.AddDays(-1);
            if (timeStr.Contains("前天"))
                return DateTime.Today.AddDays(-2);

            // 尝试解析绝对时间
            if (DateTime.TryParse(timeStr, out var time))
            {
                // 如果年份为1，说明只解析了时间部分，需要设置日期
                if (time.Year == 1)
                    return DateTime.Today.Add(time.TimeOfDay);
                return time;
            }
        }
        catch { }

        return DateTime.Now;
    }

    #endregion

    #region 消息发送

    /// <summary>
    /// 向指定群发送文本消息
    /// 自动选择目标群并输入发送内容
    /// </summary>
    /// <param name="groupName">目标群名称</param>
    /// <param name="message">要发送的消息内容</param>
    /// <returns>发送结果</returns>
    /// <remarks>
    /// 发送流程：
    /// 1. 激活微信窗口
    /// 2. 选择目标群
    /// 3. 定位输入框
    /// 4. 清空现有内容并输入新内容（通过剪贴板）
    /// 5. 点击发送按钮或按回车键
    /// </remarks>
    public SendResult SendMessage(string groupName, string message)
    {
        var result = new SendResult();

        // 前置检查
        if (!EnsureWeChatConnected())
        {
            result.Message = "微信未连接";
            return result;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            result.Message = "消息内容为空";
            return result;
        }

        try
        {
            Log("INFO", $"向群 [{groupName}] 发送消息");
            
            // 激活窗口
            ActivateWindow();
            Thread.Sleep(300);

            // 选择目标群
            if (!SelectChatByName(groupName))
            {
                result.Message = $"未找到群 [{groupName}]";
                return result;
            }

            Thread.Sleep(300);

            // 查找输入框
            var inputBox = FindInputBox();
            if (inputBox == null)
            {
                result.Message = "未找到输入框";
                Log("ERROR", result.Message);
                return result;
            }

            // 点击输入框获取焦点
            inputBox.Click();
            Thread.Sleep(100);
            
            // 保存原始剪贴板内容
            IDataObject? originalClipboard = null;
            try
            {
                if (Clipboard.ContainsData(DataFormats.Text))
                    originalClipboard = Clipboard.GetDataObject();
            }
            catch { } // 忽略剪贴板读取错误
            
            try
            {
                // 清空现有内容（Ctrl+A全选）
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Thread.Sleep(50);
                
                // 通过剪贴板粘贴内容（支持中文和特殊字符）
                Clipboard.SetText(message);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                Thread.Sleep(200);
            }
            finally
            {
                // 恢复原始剪贴板内容
                try
                {
                    if (originalClipboard != null)
                        Clipboard.SetDataObject(originalClipboard);
                }
                catch { } // 忽略恢复时的错误
            }

            // 尝试点击发送按钮
            var sendButton = FindSendButton();
            if (sendButton != null)
            {
                sendButton.Click();
            }
            else
            {
                // 如果找不到发送按钮，使用回车键发送
                Keyboard.Type(VirtualKeyShort.ENTER);
            }

            result.Success = true;
            result.Message = "消息发送成功";
            Log("INFO", result.Message);
        }
        catch (Exception ex)
        {
            result.Message = $"发送失败: {ex.Message}";
            Log("ERROR", result.Message);
        }

        return result;
    }

    #endregion

    #region 窗口操作

    /// <summary>
    /// 激活微信窗口到前台
    /// 确保窗口可见并获得焦点
    /// </summary>
    private void ActivateWindow()
    {
        if (_wechatWindow != null)
        {
            try
            {
                // 设置窗口焦点
                _wechatWindow.Focus();
                // 将窗口置于前台
                _wechatWindow.SetForeground();
            }
            catch
            {
                // 忽略窗口激活错误（可能窗口已关闭）
            }
        }
    }

    /// <summary>
    /// 通过名称选择聊天
    /// 使用搜索功能快速定位目标聊天
    /// </summary>
    /// <param name="chatName">聊天名称</param>
    /// <returns>true：选择成功；false：选择失败</returns>
    /// <remarks>
    /// 选择流程：
    /// 1. 定位搜索框
    /// 2. 输入聊天名称进行搜索
    /// 3. 按回车键选择第一个搜索结果
    /// </remarks>
    private bool SelectChatByName(string chatName)
    {
        try
        {
            // 查找搜索框
            var searchBox = FindSearchBox();
            if (searchBox != null)
            {
                // 点击搜索框获取焦点
                searchBox.Click();
                Thread.Sleep(100);
                
                // 清空现有搜索内容（Ctrl+A）
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Thread.Sleep(50);
                
                // 输入聊天名称
                Keyboard.Type(chatName);
                
                // 等待搜索结果
                Thread.Sleep(800);

                // 按回车选择第一个结果
                Keyboard.Type(VirtualKeyShort.ENTER);
                Thread.Sleep(500);
                
                return true;
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", $"选择聊天失败: {ex.Message}");
        }

        return false;
    }

    #endregion

    #region 控件查找

    /// <summary>
    /// 查找聊天列表控件
    /// 尝试多种方式定位聊天列表
    /// </summary>
    /// <returns>聊天列表元素，未找到返回null</returns>
    private AutomationElement? FindChatList()
    {
        if (_wechatWindow == null) return null;

        try
        {
            // 方式1：通过AutomationId查找
            var list = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("ChatList"));
            if (list != null) return list;

            // 方式2：通过控件类型查找（List类型）
            list = _wechatWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            return list;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查找消息列表控件
    /// 定位当前聊天的消息显示区域
    /// </summary>
    /// <returns>消息列表元素，未找到返回null</returns>
    private AutomationElement? FindMessageList()
    {
        if (_wechatWindow == null) return null;

        try
        {
            // 方式1：通过AutomationId查找
            var list = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("MessageList"));
            if (list != null) return list;

            // 方式2：查找所有List，返回第一个（通常消息列表是主要的List）
            var lists = _wechatWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
            return lists.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查找输入框控件
    /// 定位消息输入区域
    /// </summary>
    /// <returns>输入框元素，未找到返回null</returns>
    private AutomationElement? FindInputBox()
    {
        if (_wechatWindow == null) return null;

        try
        {
            // 方式1：通过AutomationId查找
            var input = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("EditBox"));
            if (input != null) return input;

            // 方式2：通过控件类型查找（Edit类型）
            input = _wechatWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            return input;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查找发送按钮控件
    /// </summary>
    /// <returns>发送按钮元素，未找到返回null</returns>
    private AutomationElement? FindSendButton()
    {
        if (_wechatWindow == null) return null;

        try
        {
            // 方式1：通过名称查找（"发送"按钮）
            var btn = _wechatWindow.FindFirstDescendant(cf => cf.ByName("发送"));
            if (btn != null) return btn;

            // 方式2：通过AutomationId查找
            btn = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"));
            return btn;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查找搜索框控件
    /// </summary>
    /// <returns>搜索框元素，未找到返回null</returns>
    private AutomationElement? FindSearchBox()
    {
        if (_wechatWindow == null) return null;

        try
        {
            // 方式1：通过AutomationId查找
            var search = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchInput"));
            if (search != null) return search;

            // 方式2：通过名称查找
            search = _wechatWindow.FindFirstDescendant(cf => cf.ByName("搜索"));
            return search;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 日志输出

    /// <summary>
    /// 输出日志信息
    /// 触发OnLog事件，将日志传递给订阅者
    /// </summary>
    /// <param name="level">日志级别（INFO/WARN/ERROR）</param>
    /// <param name="message">日志消息内容</param>
    private void Log(string level, string message)
    {
        try
        {
            OnLog?.Invoke(this, new LogEventArgs
            {
                Level = level,
                Message = message,
                Time = DateTime.Now
            });
        }
        catch
        {
            // 忽略日志输出错误
        }
    }

    #endregion

    #region 资源释放

    /// <summary>
    /// 释放资源
    /// 清理UI自动化相关资源
    /// </summary>
    public void Dispose()
    {
        // 防止重复释放
        if (_disposed) return;

        // 释放窗口引用
        _wechatWindow?.Dispose();
        
        // 释放自动化实例
        _automation?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
