using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class HybridAutomationService : IDisposable
{
    #region Win32 API 声明

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, string? lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    private static extern bool ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    private const int WM_SETTEXT = 0x000C;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_CHAR = 0x0102;
    private const int WM_SETFOCUS = 0x0007;
    private const int BM_CLICK = 0x00F5;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private const uint GW_CHILD = 0x0005;
    private const uint GW_HWNDNEXT = 0x0002;
    private const int SW_SHOWNORMAL = 1;
    private const uint KLF_ACTIVATE = 0x00000001;

    #endregion

    #region 常量定义

    private readonly string[] _wechatClassNames = { "WeChatMainWndForPC", "WeChatMainWndClass", "WeChatLoginWndForPC" };
    private readonly string _wechatProcessName = "WeChat";
    private const int MaxSearchRetries = 3;

    #endregion

    #region 私有字段

    private IntPtr _wechatWindowHandle;
    private bool _disposed;
    private IntPtr _inputEditHandle;
    private IntPtr _sendButtonHandle;
    private AutomationBase? _flaUIAutomation;
    private Window? _flaUIWindow;
    private bool _useNativeMode = true;
    private readonly object _lockObject = new();

    #endregion

    #region 事件

    public event LogEventHandler? OnLog;
    public event StatusChangedEventHandler? OnStatusChanged;

    #endregion

    #region 公共属性

    public bool IsConnected => _wechatWindowHandle != IntPtr.Zero;

    public string CurrentMode => _useNativeMode ? "Native Win32" : "FlaUI";

    #endregion

    #region 核心方法

    public bool Connect()
    {
        lock (_lockObject)
        {
            try
            {
                Log("INFO", "========== 开始连接微信 ==========");
                Log("INFO", $"当前自动化模式: {(_useNativeMode ? "Native Win32 API" : "FlaUI")}");

                var processes = System.Diagnostics.Process.GetProcessesByName(_wechatProcessName);
                if (processes.Length == 0)
                {
                    Log("WARN", "未找到微信进程，请确保微信已启动");
                    return false;
                }

                var process = processes[0];
                Log($"INFO", $"检测到微信进程 PID: {process.Id}");

                _wechatWindowHandle = FindWeChatWindow();
                if (_wechatWindowHandle == IntPtr.Zero)
                {
                    Log("WARN", "未找到微信主窗口句柄");
                    return false;
                }

                var className = GetWindowClassName(_wechatWindowHandle);
                var title = GetWindowText(_wechatWindowHandle);
                Log($"INFO", $"窗口信息: 类名=[{className}], 标题=[{title}]");

                if (!IsWindowVisible(_wechatWindowHandle))
                {
                    Log("WARN", "微信窗口当前不可见，正在恢复...");
                    ShowWindow(_wechatWindowHandle, SW_SHOWNORMAL);
                    Thread.Sleep(300);
                }

                ActivateWindow();

                InitializeControls();

                InitializeFlaUI();

                OnStatusChanged?.Invoke(this, new StatusEventArgs
                {
                    Status = $"已连接 ({CurrentMode})",
                    IsRunning = true
                });

                Log("INFO", "========== 微信连接成功 ==========");
                return true;
            }
            catch (Exception ex)
            {
                Log("ERROR", $"连接失败: {ex.Message}");
                Log("ERROR", $"异常详情: {ex.StackTrace}");
                return false;
            }
        }
    }

    private void InitializeFlaUI()
    {
        try
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Log("WARN", "当前线程不是 STA 模式，FlaUI 功能可能受限");
            }

            _flaUIAutomation = new UIA3Automation();
            _flaUIAutomation.Configuration.TransitionTimeout = TimeSpan.FromSeconds(2);
            _flaUIAutomation.Configuration.WaitForElementTimeout = TimeSpan.FromSeconds(3);

            if (_wechatWindowHandle != IntPtr.Zero)
            {
                var desktop = _flaUIAutomation.GetDesktop();
                var element = desktop.FromHandle(_wechatWindowHandle);
                _flaUIWindow = element?.AsWindow();
                if (_flaUIWindow != null)
                {
                    Log("INFO", "FlaUI 初始化成功，窗口已绑定");
                }
            }
        }
        catch (Exception ex)
        {
            Log("WARN", $"FlaUI 初始化失败: {ex.Message}，将使用纯 Native 模式");
            _flaUIAutomation?.Dispose();
            _flaUIAutomation = null;
        }
    }

    private IntPtr FindWeChatWindow()
    {
        foreach (var className in _wechatClassNames)
        {
            var hwnd = FindWindow(className, null);
            if (hwnd != IntPtr.Zero)
            {
                Log($"INFO", $"通过类名 '{className}' 找到窗口 (0x{hwnd:X})");
                return hwnd;
            }
        }

        var titles = new[] { "微信", "WeChat" };
        foreach (var title in titles)
        {
            var hwnd = FindWindow(null, title);
            if (hwnd != IntPtr.Zero)
            {
                Log($"INFO", $"通过标题 '{title}' 找到窗口");
                return hwnd;
            }
        }

        return IntPtr.Zero;
    }

    private void InitializeControls()
    {
        _inputEditHandle = FindInputEdit();
        _sendButtonHandle = FindSendButton();

        Log($"DEBUG", $"输入框句柄: 0x{_inputEditHandle:X}");
        Log($"DEBUG", $"发送按钮句柄: 0x{_sendButtonHandle:X}");
    }

    private IntPtr FindInputEdit()
    {
        var children = new List<(IntPtr Hwnd, string ClassName, string Text)>();
        GetAllChildWindows(_wechatWindowHandle, children);

        foreach (var (hwnd, className, text) in children)
        {
            if (className.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            {
                return hwnd;
            }
        }

        return FindChildByClass(_wechatWindowHandle, "Edit", 0);
    }

    private IntPtr FindSendButton()
    {
        var children = new List<(IntPtr Hwnd, string ClassName, string Text)>();
        GetAllChildWindows(_wechatWindowHandle, children);

        foreach (var (hwnd, className, text) in children)
        {
            if (className.Contains("Button", StringComparison.OrdinalIgnoreCase) &&
                (text.Contains("发送", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("Send", StringComparison.OrdinalIgnoreCase)))
            {
                return hwnd;
            }
        }

        return FindChildByClass(_wechatWindowHandle, "Button", 0);
    }

    private IntPtr FindSearchBox()
    {
        var children = new List<(IntPtr Hwnd, string ClassName, string Text)>();
        GetAllChildWindows(_wechatWindowHandle, children);

        foreach (var (hwnd, className, text) in children)
        {
            if (className.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            {
                if (text.Contains("搜索", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Search", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("search", StringComparison.OrdinalIgnoreCase))
                {
                    return hwnd;
                }
            }
        }

        return FindChildByClass(_wechatWindowHandle, "Edit", 0);
    }

    private IntPtr FindChildByClass(IntPtr parent, string className, int index)
    {
        var children = new List<IntPtr>();
        GetChildWindowsRecursive(parent, children, className);

        if (index >= 0 && index < children.Count)
        {
            return children[index];
        }

        return children.FirstOrDefault();
    }

    private void GetChildWindowsRecursive(IntPtr parent, List<IntPtr> results, string? classNameFilter)
    {
        EnumChildWindows(parent, (hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                var className = GetWindowClassName(hwnd);
                if (classNameFilter == null || className.Contains(classNameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(hwnd);
                }
            }
            GetChildWindowsRecursive(hwnd, results, classNameFilter);
            return true;
        }, IntPtr.Zero);
    }

    private void GetChildWindows(IntPtr parent, List<IntPtr> results)
    {
        EnumChildWindows(parent, (hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                results.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);
    }

    private void GetAllChildWindows(IntPtr parent, List<(IntPtr, string, string)> results)
    {
        EnumChildWindows(parent, (hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                var className = GetWindowClassName(hwnd);
                var text = GetWindowText(hwnd);
                results.Add((hwnd, className, text));
            }
            GetAllChildWindows(hwnd, results);
            return true;
        }, IntPtr.Zero);
    }

    #endregion

    #region 消息发送 (Native Win32)

    public SendResult SendMessage(string groupName, string message)
    {
        var result = new SendResult();

        if (!IsConnected)
        {
            if (!Connect())
            {
                result.Message = "微信未连接";
                return result;
            }
        }

        try
        {
            Log($"INFO", $"========== 开始发送消息 ==========");
            Log($"INFO", $"目标群: {groupName}");
            Log($"INFO", $"消息长度: {message.Length} 字符");

            ActivateWindow();
            Thread.Sleep(200);

            if (!SelectGroupBySearch(groupName))
            {
                result.Message = $"无法找到群: {groupName}";
                Log("ERROR", result.Message);
                return result;
            }

            Thread.Sleep(300);

            if (!EnterTextToInput(message))
            {
                result.Message = "无法输入消息到输入框";
                Log("ERROR", result.Message);
                return result;
            }

            Thread.Sleep(100);

            ClickSendButton();

            result.Success = true;
            result.Message = "消息发送成功";
            Log("INFO", "========== 消息发送成功 ==========");
        }
        catch (Exception ex)
        {
            result.Message = $"发送失败: {ex.Message}";
            Log("ERROR", result.Message);
        }

        return result;
    }

    private bool SelectGroupBySearch(string groupName)
    {
        for (int retry = 0; retry < MaxSearchRetries; retry++)
        {
            try
            {
                var searchBox = FindSearchBox();
                if (searchBox == IntPtr.Zero)
                {
                    Log($"WARN", $"第 {retry + 1} 次尝试: 未找到搜索框");
                    Thread.Sleep(500);
                    continue;
                }

                SetFocus(searchBox);
                Thread.Sleep(100);

                SendMessage(searchBox, WM_SETTEXT, IntPtr.Zero, "");

                SendKeysViaClipboard(groupName);
                Thread.Sleep(500);

                SendKey(Keys.Enter);
                Thread.Sleep(300);

                Log($"INFO", $"成功选择群: {groupName}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR", $"第 {retry + 1} 次选择群失败: {ex.Message}");
                Thread.Sleep(300);
            }
        }

        return false;
    }

    private void SendKeysViaClipboard(string text)
    {
        IDataObject? originalClipboard = null;

        try
        {
            if (Clipboard.ContainsText())
            {
                originalClipboard = Clipboard.GetDataObject();
            }

            Clipboard.SetText(text);

            SendKey(Keys.Control, false);
            SendKey(Keys.V, true);

            Application.DoEvents();
        }
        finally
        {
            try
            {
                if (originalClipboard != null)
                {
                    Clipboard.SetDataObject(originalClipboard);
                }
            }
            catch { }
        }
    }

    private void SendKey(Keys key, bool keyUp = false)
    {
        int vk = (int)key;

        if (!keyUp)
        {
            PostMessage(_wechatWindowHandle, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        }
        else
        {
            PostMessage(_wechatWindowHandle, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        }

        Thread.Sleep(10);
    }

    private void SendKey(Keys key1, Keys key2)
    {
        int vk1 = (int)key1;
        int vk2 = (int)key2;

        PostMessage(_wechatWindowHandle, WM_KEYDOWN, (IntPtr)vk1, IntPtr.Zero);
        Thread.Sleep(10);
        PostMessage(_wechatWindowHandle, WM_KEYDOWN, (IntPtr)vk2, IntPtr.Zero);
        Thread.Sleep(10);
        PostMessage(_wechatWindowHandle, WM_KEYUP, (IntPtr)vk2, IntPtr.Zero);
        Thread.Sleep(10);
        PostMessage(_wechatWindowHandle, WM_KEYUP, (IntPtr)vk1, IntPtr.Zero);
    }

    private bool EnterTextToInput(string text)
    {
        try
        {
            var inputBox = _inputEditHandle;
            if (inputBox == IntPtr.Zero)
            {
                inputBox = FindInputEdit();
            }

            if (inputBox == IntPtr.Zero)
            {
                Log("ERROR", "无法找到输入框");
                return false;
            }

            SetFocus(inputBox);
            Thread.Sleep(50);

            SendMessage(inputBox, WM_SETTEXT, IntPtr.Zero, "");

            SendKeysViaClipboard(text);

            _inputEditHandle = inputBox;

            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR", $"输入文本失败: {ex.Message}");
            return false;
        }
    }

    private void ClickSendButton()
    {
        var button = _sendButtonHandle;
        if (button == IntPtr.Zero)
        {
            button = FindSendButton();
        }

        if (button != IntPtr.Zero)
        {
            SendMessage(button, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Log("DEBUG", $"点击发送按钮 (0x{button:X})");
        }
        else
        {
            SendKey(Keys.Enter);
            Log("DEBUG", "按回车发送消息");
        }
    }

    #endregion

    #region 窗口操作

    private void ActivateWindow()
    {
        if (_wechatWindowHandle != IntPtr.Zero)
        {
            ShowWindow(_wechatWindowHandle, SW_SHOWNORMAL);
            SetForegroundWindow(_wechatWindowHandle);
        }
    }

    private void SetFocus(IntPtr hwnd)
    {
        PostMessage(hwnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(50);
    }

    #endregion

    #region 辅助方法

    private string GetWindowClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, 256);
        return sb.ToString();
    }

    private string GetWindowText(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        int length = GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

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
        catch { }
    }

    #endregion

    #region 资源释放

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            _flaUIWindow?.Dispose();
            _flaUIAutomation?.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
