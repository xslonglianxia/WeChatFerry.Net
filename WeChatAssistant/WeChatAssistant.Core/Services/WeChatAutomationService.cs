using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;

namespace WeChatAssistant.Core.Services;

public class WeChatAutomationService : IDisposable
{
    private readonly string _wechatClassName = "WeChatMainWndForPC";
    private readonly string _wechatProcessName = "WeChat";
    private AutomationBase? _automation;
    private Window? _wechatWindow;
    private bool _disposed;
    private bool _isInitialized;

    public event LogEventHandler? OnLog;
    public event StatusChangedEventHandler? OnStatusChanged;

    public bool IsWeChatRunning => _wechatWindow != null && !_wechatWindow.IsOffscreen;

    public WeChatAutomationService()
    {
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            _automation = new UIA3Automation();
            _automation.Configuration.TransitionTimeout = TimeSpan.FromSeconds(2);
            _automation.Configuration.WaitForElementTimeout = TimeSpan.FromSeconds(5);
            _isInitialized = true;
            Log("INFO", "UI自动化服务初始化成功");
        }
        catch (Exception ex)
        {
            Log("ERROR", $"初始化失败: {ex.Message}");
            _isInitialized = false;
        }
    }

    public bool ConnectToWeChat()
    {
        if (!_isInitialized || _automation == null)
        {
            Log("ERROR", "自动化服务未初始化");
            return false;
        }

        try
        {
            var desktop = _automation.GetDesktop();
            
            _wechatWindow = desktop.FindFirstChild(cf => cf.ByClassName(_wechatClassName))
                ?.AsWindow();

            if (_wechatWindow == null)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(_wechatProcessName);
                if (processes.Length > 0)
                {
                    Log("INFO", "检测到微信进程，尝试连接窗口...");
                    Thread.Sleep(1000);
                    _wechatWindow = desktop.FindFirstChild(cf => cf.ByClassName(_wechatClassName))
                        ?.AsWindow();
                }
            }

            if (_wechatWindow != null)
            {
                Log("INFO", "成功连接到微信窗口");
                OnStatusChanged?.Invoke(this, new StatusEventArgs { Status = "已连接", IsRunning = true });
                return true;
            }

            Log("WARN", "未找到微信窗口，请确保微信PC客户端已启动");
            return false;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"连接微信失败: {ex.Message}");
            return false;
        }
    }

    public List<string> GetGroupList()
    {
        var groups = new List<string>();
        
        if (!EnsureWeChatConnected())
        {
            return groups;
        }

        try
        {
            ActivateWindow();
            Thread.Sleep(300);

            var chatList = FindChatList();
            if (chatList == null)
            {
                Log("WARN", "未找到聊天列表");
                return groups;
            }

            var listItems = chatList.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            
            foreach (var item in listItems)
            {
                try
                {
                    var nameElement = item.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text));
                    if (nameElement != null)
                    {
                        var name = nameElement.Name;
                        if (!string.IsNullOrEmpty(name) && !groups.Contains(name))
                        {
                            groups.Add(name);
                        }
                    }
                }
                catch
                {
                    // 忽略单个项目错误
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

    public CaptureResult CaptureGroupMessages(string groupName, int maxMessages = 100)
    {
        var result = new CaptureResult();

        if (!EnsureWeChatConnected())
        {
            result.Message = "微信未连接";
            return result;
        }

        try
        {
            Log("INFO", $"开始抓取群 [{groupName}] 的聊天记录");
            ActivateWindow();
            Thread.Sleep(500);

            if (!SelectChatByName(groupName))
            {
                result.Message = $"未找到群 [{groupName}]";
                Log("WARN", result.Message);
                return result;
            }

            Thread.Sleep(500);

            var messages = ExtractMessagesFromChat(maxMessages);
            
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

    public SendResult SendMessage(string groupName, string message)
    {
        var result = new SendResult();

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
            ActivateWindow();
            Thread.Sleep(300);

            if (!SelectChatByName(groupName))
            {
                result.Message = $"未找到群 [{groupName}]";
                return result;
            }

            Thread.Sleep(300);

            var inputBox = FindInputBox();
            if (inputBox == null)
            {
                result.Message = "未找到输入框";
                Log("ERROR", result.Message);
                return result;
            }

            inputBox.Click();
            Thread.Sleep(100);
            
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Thread.Sleep(50);
            
            Clipboard.SetText(message);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
            Thread.Sleep(200);

            var sendButton = FindSendButton();
            if (sendButton != null)
            {
                sendButton.Click();
            }
            else
            {
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

    private List<ChatMessage> ExtractMessagesFromChat(int maxMessages)
    {
        var messages = new List<ChatMessage>();

        try
        {
            var messageList = FindMessageList();
            if (messageList == null)
            {
                Log("WARN", "未找到消息列表区域");
                return messages;
            }

            var messageItems = messageList.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            
            int count = 0;
            foreach (var item in messageItems)
            {
                if (count >= maxMessages) break;

                try
                {
                    var msg = ParseMessageItem(item);
                    if (msg != null && !string.IsNullOrWhiteSpace(msg.Content))
                    {
                        messages.Add(msg);
                        count++;
                    }
                }
                catch
                {
                    // 忽略解析错误
                }
            }

            messages.Reverse();
        }
        catch (Exception ex)
        {
            Log("ERROR", $"提取消息失败: {ex.Message}");
        }

        return messages;
    }

    private ChatMessage? ParseMessageItem(AutomationElement item)
    {
        try
        {
            var msg = new ChatMessage
            {
                CaptureTime = DateTime.Now,
                MessageId = Guid.NewGuid().ToString("N")
            };

            var allTexts = item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            
            string? sender = null;
            string? content = null;
            string? timeStr = null;

            foreach (var text in allTexts)
            {
                var textValue = text.Name;
                if (string.IsNullOrWhiteSpace(textValue)) continue;

                if (IsTimeFormat(textValue))
                {
                    timeStr = textValue;
                }
                else if (sender == null && textValue.Length < 20)
                {
                    sender = textValue;
                }
                else if (content == null || textValue.Length > (content?.Length ?? 0))
                {
                    content = textValue;
                }
            }

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

    private bool IsTimeFormat(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, 
            @"^\d{1,2}:\d{2}$|^\d{4}-\d{2}-\d{2}$|^\d{4}/\d{2}/\d{2}$|昨天|前天|星期");
    }

    private DateTime ParseTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr))
            return DateTime.Now;

        try
        {
            if (timeStr.Contains("昨天"))
                return DateTime.Today.AddDays(-1);
            if (timeStr.Contains("前天"))
                return DateTime.Today.AddDays(-2);

            if (DateTime.TryParse(timeStr, out var time))
            {
                if (time.Year == 1)
                    return DateTime.Today.Add(time.TimeOfDay);
                return time;
            }
        }
        catch { }

        return DateTime.Now;
    }

    private bool SelectChatByName(string chatName)
    {
        try
        {
            var searchBox = FindSearchBox();
            if (searchBox != null)
            {
                searchBox.Click();
                Thread.Sleep(100);
                
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Thread.Sleep(50);
                
                Keyboard.Type(chatName);
                Thread.Sleep(800);

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

    private void ActivateWindow()
    {
        if (_wechatWindow != null)
        {
            try
            {
                _wechatWindow.Focus();
                _wechatWindow.SetForeground();
            }
            catch
            {
                // 忽略窗口激活错误
            }
        }
    }

    private bool EnsureWeChatConnected()
    {
        if (_wechatWindow == null || _wechatWindow.IsOffscreen)
        {
            return ConnectToWeChat();
        }
        return true;
    }

    private AutomationElement? FindChatList()
    {
        if (_wechatWindow == null) return null;

        try
        {
            var list = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("ChatList"));
            if (list != null) return list;

            list = _wechatWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            return list;
        }
        catch
        {
            return null;
        }
    }

    private AutomationElement? FindMessageList()
    {
        if (_wechatWindow == null) return null;

        try
        {
            var list = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("MessageList"));
            if (list != null) return list;

            var lists = _wechatWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.List));
            return lists.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private AutomationElement? FindInputBox()
    {
        if (_wechatWindow == null) return null;

        try
        {
            var input = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("EditBox"));
            if (input != null) return input;

            input = _wechatWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            return input;
        }
        catch
        {
            return null;
        }
    }

    private AutomationElement? FindSendButton()
    {
        if (_wechatWindow == null) return null;

        try
        {
            var btn = _wechatWindow.FindFirstDescendant(cf => cf.ByName("发送"));
            if (btn != null) return btn;

            btn = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("SendButton"));
            return btn;
        }
        catch
        {
            return null;
        }
    }

    private AutomationElement? FindSearchBox()
    {
        if (_wechatWindow == null) return null;

        try
        {
            var search = _wechatWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchInput"));
            if (search != null) return search;

            search = _wechatWindow.FindFirstDescendant(cf => cf.ByName("搜索"));
            return search;
        }
        catch
        {
            return null;
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

        _wechatWindow?.Dispose();
        _automation?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
