using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class NativeWin32AutomationService : IDisposable
{
    #region Win32 API 声明

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, string? lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr RealGetWindowClass(IntPtr hWnd, StringBuilder pszType, int cchType);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    private static extern bool ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    private const int WM_GETTEXT = 0x000D;
    private const int WM_GETTEXTLENGTH = 0x000E;
    private const int WM_SETTEXT = 0x000C;
    private const int WM_CLICK = 0x00F5;
    private const int BM_CLICK = 0x00F5;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_CHAR = 0x0102;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_GETDLGCODE = 0x0087;
    private const int WM_COMMAND = 0x0111;
    private const int EN_HSCROLL = 0x0606;
    private const int EN_VSCROLL = 0x0607;

    private const uint GW_CHILD = 0x0005;
    private const uint GW_HWNDNEXT = 0x0002;
    private const int GWL_ID = (-8);
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const int SW_SHOWNORMAL = 1;

    private const uint KLF_ACTIVATE = 0x00000001;

    #endregion

    #region 常量定义

    private readonly string[] _wechatClassNames = { "WeChatMainWndForPC", "WeChatMainWndClass", "WeChatLoginWndForPC" };
    private readonly string _wechatProcessName = "WeChat";

    #endregion

    #region 私有字段

    private IntPtr _wechatWindowHandle;
    private bool _disposed;
    private IntPtr _inputEditHandle;
    private IntPtr _sendButtonHandle;

    #endregion

    #region 事件

    public event LogEventHandler? OnLog;
    public event StatusChangedEventHandler? OnStatusChanged;

    #endregion

    #region 公共属性

    public bool IsConnected => _wechatWindowHandle != IntPtr.Zero;

    #endregion

    #region 核心方法

    public bool Connect()
    {
        try
        {
            Log("INFO", "开始使用 Native Win32 API 连接微信...");

            var processes = System.Diagnostics.Process.GetProcessesByName(_wechatProcessName);
            if (processes.Length == 0)
            {
                Log("WARN", "未找到微信进程");
                return false;
            }

            var process = processes[0];
            Log($"INFO", $"找到微信进程 PID: {process.Id}");

            _wechatWindowHandle = FindWeChatWindow();
            if (_wechatWindowHandle == IntPtr.Zero)
            {
                Log("WARN", "未找到微信主窗口");
                return false;
            }

            var className = GetWindowClassName(_wechatWindowHandle);
            var title = GetWindowText(_wechatWindowHandle);
            Log($"INFO", $"成功连接窗口: 类名={className}, 标题={title}");

            ActivateWindow();
            InitializeControls();

            OnStatusChanged?.Invoke(this, new StatusEventArgs { Status = "已连接(Native)", IsRunning = true });
            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"连接失败: {ex.Message}");
            return false;
        }
    }

    private IntPtr FindWeChatWindow()
    {
        foreach (var className in _wechatClassNames)
        {
            var hwnd = FindWindow(className, null);
            if (hwnd != IntPtr.Zero)
            {
                Log($"INFO", $"通过类名 '{className}' 找到窗口");
                return hwnd;
            }
        }

        hwnd = FindWindow(null, "微信");
        if (hwnd != IntPtr.Zero)
        {
            Log("INFO", "通过标题 '微信' 找到窗口");
            return hwnd;
        }

        hwnd = FindWindow(null, "WeChat");
        if (hwnd != IntPtr.Zero)
        {
            Log("INFO", "通过标题 'WeChat' 找到窗口");
            return hwnd;
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
        var edit = FindChildWindow(_wechatWindowHandle, "Edit", -1);
        if (edit != IntPtr.Zero) return edit;

        edit = FindChildWindow(_wechatWindowHandle, "RichEdit", -1);
        if (edit != IntPtr.Zero) return edit;

        return IntPtr.Zero;
    }

    private IntPtr FindSendButton()
    {
        var button = FindChildWindow(_wechatWindowHandle, "Button", -1);
        if (button != IntPtr.Zero)
        {
            var text = GetWindowText(button);
            if (text.Contains("发送") || text.Contains("Send"))
                return button;
        }

        return IntPtr.Zero;
    }

    private IntPtr FindChildWindow(IntPtr parent, string? className, int index)
    {
        var children = new List<IntPtr>();
        GetChildWindows(parent, children);

        if (index >= 0)
        {
            int currentIndex = -1;
            foreach (var hwnd in children)
            {
                if (className == null || GetWindowClassName(hwnd).Contains(className))
                {
                    currentIndex++;
                    if (currentIndex == index)
                        return hwnd;
                }
            }
        }
        else
        {
            foreach (var hwnd in children)
            {
                if (className == null || GetWindowClassName(hwnd).Contains(className))
                    return hwnd;
            }
        }

        return IntPtr.Zero;
    }

    private void GetChildWindows(IntPtr parent, List<IntPtr> children)
    {
        EnumChildWindows(parent, (hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                children.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);
    }

    #endregion

    #region 消息发送

    public SendResult SendMessage(string groupName, string message)
    {
        var result = new SendResult();

        if (!IsConnected)
        {
            result.Message = "微信未连接";
            return result;
        }

        try
        {
            Log($"INFO", $"发送消息到群: {groupName}");

            ActivateWindow();
            Thread.Sleep(200);

            if (!SelectGroupBySearch(groupName))
            {
                result.Message = $"未找到群: {groupName}";
                return result;
            }

            Thread.Sleep(300);

            if (!EnterTextToInput(message))
            {
                result.Message = "无法输入消息";
                return result;
            }

            Thread.Sleep(100);

            ClickSendButton();

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

    private bool SelectGroupBySearch(string groupName)
    {
        try
        {
            var searchBox = FindSearchBox();
            if (searchBox == IntPtr.Zero)
            {
                Log("WARN", "未找到搜索框");
                return false;
            }

            SetFocus(searchBox);
            Thread.Sleep(100);

            SendText(searchBox, groupName);
            Thread.Sleep(500);

            SendKey(Keys.Enter);

            Log($"INFO", $"已选择群: {groupName}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR", $"选择群失败: {ex.Message}");
            return false;
        }
    }

    private IntPtr FindSearchBox()
    {
        var children = new List<(IntPtr Hwnd, string ClassName, string Text)>();
        GetAllChildWindows(_wechatWindowHandle, children);

        foreach (var (hwnd, className, text) in children)
        {
            if (className.Contains("Edit") && (text.Contains("搜索") || text.Contains("Search")))
            {
                return hwnd;
            }
        }

        return FindChildWindow(_wechatWindowHandle, "Edit", 0);
    }

    private void GetAllChildWindows(IntPtr parent, List<(IntPtr, string, string)> results)
    {
        EnumChildWindows(parent, (hwnd, lParam) =>
        {
            var className = GetWindowClassName(hwnd);
            var text = GetWindowText(hwnd);
            if (IsWindowVisible(hwnd))
            {
                results.Add((hwnd, className, text));
            }
            GetAllChildWindows(hwnd, results);
            return true;
        }, IntPtr.Zero);
    }

    private bool EnterTextToInput(string text)
    {
        try
        {
            var inputBox = _inputEditHandle != IntPtr.Zero ? _inputEditHandle : FindInputEdit();
            if (inputBox == IntPtr.Zero)
            {
                Log("ERROR", "未找到输入框");
                return false;
            }

            SetFocus(inputBox);
            Thread.Sleep(50);

            ClearInputBox(inputBox);

            ActivateKeyboardLayout(LoadKeyboardLayout("00000804", KLF_ACTIVATE), KLF_ACTIVATE);

            SendText(inputBox, text);

            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR", $"输入文本失败: {ex.Message}");
            return false;
        }
    }

    private void ClearInputBox(IntPtr hwnd)
    {
        SendMessage(hwnd, WM_SETTEXT, IntPtr.Zero, "");
    }

    private void SendText(IntPtr hwnd, string text)
    {
        foreach (char c in text)
        {
            short vk = VkKeyScan(c);
            byte vkCode = (byte)(vk & 0xFF);
            bool shift = (vk & 0x100) != 0;

            if (shift)
                SendMessage(hwnd, WM_KEYDOWN, (IntPtr)0x10, IntPtr.Zero);

            SendMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);

            if (shift)
                SendMessage(hwnd, WM_KEYUP, (IntPtr)0x10, IntPtr.Zero);
        }
    }

    private void SendKey(Keys key)
    {
        int vk = (int)key;
        SendMessage(_wechatWindowHandle, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(10);
        SendMessage(_wechatWindowHandle, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    }

    private void ClickSendButton()
    {
        var button = _sendButtonHandle;
        if (button == IntPtr.Zero)
        {
            button = FindChildWindow(_wechatWindowHandle, "Button", 0);
        }

        if (button != IntPtr.Zero)
        {
            SendMessage(button, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Log("DEBUG", "点击发送按钮");
        }
        else
        {
            SendKey(Keys.Enter);
            Log("DEBUG", "按回车发送");
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
        SendMessage(hwnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
    }

    #endregion

    #region 辅助方法

    private string GetWindowClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, 256);
        return sb.ToString();
    }

    private string GetWindowText(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static short VkKeyScan(char ch)
    {
        return (short)SendMessage(IntPtr.Zero, 0x0101, IntPtr.Zero, ((IntPtr)(unchecked((ulong)ch))));
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
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
