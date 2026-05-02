using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;
using WeChatAssistant.Core.Services;

namespace WeChatAssistant.UI;

/// <summary>
/// 应用程序主窗体
/// 提供微信群聊助手的完整用户界面
/// 包括消息抓取、发送、定时任务管理和历史记录查看功能
/// </summary>
/// <remarks>
/// 界面结构：
/// - 工具栏：连接、刷新、抓取、发送、导出等操作按钮
/// - 选项卡：
///   - 消息抓取：群列表、消息列表、日志输出
///   - 定时任务：任务列表管理
///   - 历史记录：按条件查询历史消息
/// - 状态栏：显示连接状态和操作状态
/// </remarks>
public partial class MainForm : Form
{
    #region 私有字段 - 服务实例

    /// <summary>
    /// 微信自动化服务
    /// 处理与微信客户端的交互操作
    /// </summary>
    private readonly WeChatAutomationService _automationService;

    /// <summary>
    /// 数据库服务
    /// 处理消息和任务的持久化存储
    /// </summary>
    private readonly DatabaseService _databaseService;

    /// <summary>
    /// 导出服务
    /// 处理消息的多格式导出
    /// </summary>
    private readonly ExportService _exportService;

    /// <summary>
    /// 定时任务调度服务
    /// 管理定时任务的执行
    /// </summary>
    private SchedulerService? _schedulerService;

    #endregion

    #region 私有字段 - 状态变量

    /// <summary>
    /// 微信连接状态
    /// true：已连接；false：未连接
    /// </summary>
    private bool _isConnected;

    /// <summary>
    /// 当前选中的群名称
    /// 用于消息操作的目标群
    /// </summary>
    private string _currentGroup = string.Empty;

    #endregion

    #region 私有字段 - UI控件

    // 消息抓取页面控件
    private ListView _lvGroups = null!;          // 群列表视图
    private ListView _lvMessages = null!;        // 消息列表视图
    private TextBox _txtLog = null!;             // 日志输出文本框
    private ComboBox _cboGroups = null!;         // 群选择下拉框
    private TextBox _txtMessage = null!;         // 消息输入文本框

    // 工具栏按钮
    private Button _btnConnect = null!;          // 连接微信按钮
    private Button _btnRefreshGroups = null!;    // 刷新群列表按钮
    private Button _btnCapture = null!;          // 抓取记录按钮
    private Button _btnSend = null!;             // 发送消息按钮
    private Button _btnExport = null!;           // 导出记录按钮

    // 选项卡和容器
    private TabControl _tabControl = null!;      // 选项卡容器

    // 定时任务页面控件
    private DataGridView _dgvScheduleTasks = null!;  // 任务列表网格
    private Button _btnAddTask = null!;           // 添加任务按钮
    private Button _btnEditTask = null!;          // 编辑任务按钮
    private Button _btnDeleteTask = null!;        // 删除任务按钮
    private Button _btnStartScheduler = null!;    // 启动/停止调度按钮

    // 状态栏控件
    private StatusStrip _statusStrip = null!;     // 状态栏容器
    private ToolStripStatusLabel _lblStatus = null!;      // 状态标签
    private ToolStripStatusLabel _lblConnection = null!;  // 连接状态标签

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化主窗体
    /// 创建服务实例、初始化界面组件、绑定事件处理
    /// </summary>
    public MainForm()
    {
        // 初始化界面组件
        InitializeComponent();
        
        // 创建服务实例
        _databaseService = new DatabaseService();
        _automationService = new WeChatAutomationService();
        _exportService = new ExportService();

        // 设置事件处理
        SetupEventHandlers();
        
        // 加载已保存的数据
        LoadSavedData();
    }

    #endregion

    #region 界面初始化

    /// <summary>
    /// 初始化所有界面组件
    /// 创建窗体布局和控件实例
    /// </summary>
    private void InitializeComponent()
    {
        // 设置窗体基本属性
        this.Text = "微信群聊助手";
        this.Size = new Size(1000, 700);
        this.MinimumSize = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterScreen;

        // 创建主布局面板（2行：工具栏 + 内容区）
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));    // 工具栏高度
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 内容区填充剩余空间

        // 创建工具栏面板
        var toolbarPanel = CreateToolbarPanel();
        mainPanel.Controls.Add(toolbarPanel, 0, 0);

        // 创建选项卡容器
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.TabPages.Add(CreateCaptureTabPage());    // 消息抓取页
        _tabControl.TabPages.Add(CreateScheduleTabPage());   // 定时任务页
        _tabControl.TabPages.Add(CreateHistoryTabPage());    // 历史记录页

        mainPanel.Controls.Add(_tabControl, 0, 1);

        // 创建状态栏
        _statusStrip = new StatusStrip();
        _lblConnection = new ToolStripStatusLabel("未连接");
        _lblStatus = new ToolStripStatusLabel("就绪");
        _statusStrip.Items.Add(_lblConnection);
        _statusStrip.Items.Add(new ToolStripStatusLabel(" | "));
        _statusStrip.Items.Add(_lblStatus);

        // 添加控件到窗体
        this.Controls.Add(mainPanel);
        this.Controls.Add(_statusStrip);
    }

    /// <summary>
    /// 创建工具栏面板
    /// 包含连接、刷新、抓取、发送、导出等操作按钮
    /// </summary>
    private Panel CreateToolbarPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control
        };

        // 连接微信按钮
        _btnConnect = new Button
        {
            Text = "连接微信",
            Location = new Point(10, 10),
            Size = new Size(100, 30)
        };
        _btnConnect.Click += BtnConnect_Click;

        // 刷新群列表按钮
        _btnRefreshGroups = new Button
        {
            Text = "刷新群列表",
            Location = new Point(120, 10),
            Size = new Size(100, 30),
            Enabled = false  // 初始禁用，连接后启用
        };
        _btnRefreshGroups.Click += BtnRefreshGroups_Click;

        // 群选择下拉框
        _cboGroups = new ComboBox
        {
            Location = new Point(230, 12),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDown  // 允许手动输入
        };

        // 抓取记录按钮
        _btnCapture = new Button
        {
            Text = "抓取记录",
            Location = new Point(440, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnCapture.Click += BtnCapture_Click;

        // 发送消息按钮
        _btnSend = new Button
        {
            Text = "发送消息",
            Location = new Point(550, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnSend.Click += BtnSend_Click;

        // 导出记录按钮
        _btnExport = new Button
        {
            Text = "导出记录",
            Location = new Point(660, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnExport.Click += BtnExport_Click;

        // 添加所有控件到面板
        panel.Controls.AddRange(new Control[] {
            _btnConnect, _btnRefreshGroups, _cboGroups,
            _btnCapture, _btnSend, _btnExport
        });

        return panel;
    }

    /// <summary>
    /// 创建消息抓取选项卡页面
    /// 包含群列表、消息列表和日志输出区域
    /// </summary>
    private TabPage CreateCaptureTabPage()
    {
        var page = new TabPage("消息抓取");

        // 创建上下分割容器（上部：群列表+消息列表，下部：日志）
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400
        };

        // 上部面板：左右分割（左：群列表，右：消息列表）
        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));  // 群列表占30%
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));  // 消息列表占70%

        // 群列表视图
        _lvGroups = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _lvGroups.Columns.Add("群名称", 200);
        _lvGroups.Columns.Add("消息数", 80);
        _lvGroups.SelectedIndexChanged += LvGroups_SelectedIndexChanged;  // 选择变更事件

        // 消息面板（包含输入框和消息列表）
        var messagePanel = new Panel { Dock = DockStyle.Fill };
        
        // 消息输入标签
        var lblMessage = new Label
        {
            Text = "发送内容:",
            Location = new Point(5, 5),
            AutoSize = true
        };

        // 消息输入文本框
        _txtMessage = new TextBox
        {
            Location = new Point(5, 25),
            Size = new Size(messagePanel.Width - 20, 80),
            Multiline = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // 消息列表视图
        _lvMessages = new ListView
        {
            Location = new Point(5, 115),
            Size = new Size(messagePanel.Width - 20, messagePanel.Height - 125),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _lvMessages.Columns.Add("时间", 120);
        _lvMessages.Columns.Add("发送者", 100);
        _lvMessages.Columns.Add("内容", 300);

        messagePanel.Controls.AddRange(new Control[] { lblMessage, _txtMessage, _lvMessages });
        
        // 响应面板大小变化
        messagePanel.Resize += (s, e) =>
        {
            _txtMessage.Width = messagePanel.Width - 20;
            _lvMessages.Size = new Size(messagePanel.Width - 20, messagePanel.Height - 125);
        };

        topPanel.Controls.Add(_lvGroups, 0, 0);
        topPanel.Controls.Add(messagePanel, 1, 0);

        // 下部面板：日志输出
        var logPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblLog = new Label
        {
            Text = "运行日志:",
            Dock = DockStyle.Top,
            Height = 20
        };

        _txtLog = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,      // 黑色背景
            ForeColor = Color.LightGreen,  // 绿色文字（终端风格）
            Font = new Font("Consolas", 9)
        };

        logPanel.Controls.AddRange(new Control[] { _txtLog, lblLog });

        splitContainer.Panel1.Controls.Add(topPanel);
        splitContainer.Panel2.Controls.Add(logPanel);

        page.Controls.Add(splitContainer);
        return page;
    }

    /// <summary>
    /// 创建定时任务选项卡页面
    /// 包含任务列表和任务管理按钮
    /// </summary>
    private TabPage CreateScheduleTabPage()
    {
        var page = new TabPage("定时任务");

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));    // 工具栏
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 任务列表

        // 工具栏面板
        var toolbarPanel = new Panel { Dock = DockStyle.Fill };

        _btnStartScheduler = new Button
        {
            Text = "启动调度",
            Location = new Point(10, 10),
            Size = new Size(100, 30)
        };
        _btnStartScheduler.Click += BtnStartScheduler_Click;

        _btnAddTask = new Button
        {
            Text = "添加任务",
            Location = new Point(120, 10),
            Size = new Size(100, 30)
        };
        _btnAddTask.Click += BtnAddTask_Click;

        _btnEditTask = new Button
        {
            Text = "编辑任务",
            Location = new Point(230, 10),
            Size = new Size(100, 30)
        };
        _btnEditTask.Click += BtnEditTask_Click;

        _btnDeleteTask = new Button
        {
            Text = "删除任务",
            Location = new Point(340, 10),
            Size = new Size(100, 30)
        };
        _btnDeleteTask.Click += BtnDeleteTask_Click;

        toolbarPanel.Controls.AddRange(new Control[] {
            _btnStartScheduler, _btnAddTask, _btnEditTask, _btnDeleteTask
        });

        // 任务列表数据网格
        _dgvScheduleTasks = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _dgvScheduleTasks.Columns.Add("Id", "ID");
        _dgvScheduleTasks.Columns.Add("Name", "任务名称");
        _dgvScheduleTasks.Columns.Add("GroupName", "群名称");
        _dgvScheduleTasks.Columns.Add("CronExpression", "Cron表达式");
        _dgvScheduleTasks.Columns.Add("IsEnabled", "启用");
        _dgvScheduleTasks.Columns.Add("LastRunTime", "上次执行");
        _dgvScheduleTasks.Columns.Add("NextRunTime", "下次执行");
        _dgvScheduleTasks.Columns["Id"]!.Visible = false;  // 隐藏ID列

        mainPanel.Controls.Add(toolbarPanel, 0, 0);
        mainPanel.Controls.Add(_dgvScheduleTasks, 0, 1);

        page.Controls.Add(mainPanel);
        return page;
    }

    /// <summary>
    /// 创建历史记录选项卡页面
    /// 提供按群名称和时间范围查询历史消息的功能
    /// </summary>
    private TabPage CreateHistoryTabPage()
    {
        var page = new TabPage("历史记录");

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));    // 筛选条件
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 结果列表

        // 筛选条件面板
        var filterPanel = new Panel { Dock = DockStyle.Fill };

        var lblGroup = new Label
        {
            Text = "群名称:",
            Location = new Point(10, 10),
            AutoSize = true
        };

        var cboHistoryGroup = new ComboBox
        {
            Location = new Point(60, 8),
            Size = new Size(150, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var lblStartDate = new Label
        {
            Text = "开始日期:",
            Location = new Point(230, 10),
            AutoSize = true
        };

        var dtpStartDate = new DateTimePicker
        {
            Location = new Point(300, 8),
            Size = new Size(150, 25),
            Format = DateTimePickerFormat.Short
        };

        var lblEndDate = new Label
        {
            Text = "结束日期:",
            Location = new Point(470, 10),
            AutoSize = true
        };

        var dtpEndDate = new DateTimePicker
        {
            Location = new Point(540, 8),
            Size = new Size(150, 25),
            Format = DateTimePickerFormat.Short
        };

        var btnQuery = new Button
        {
            Text = "查询",
            Location = new Point(710, 6),
            Size = new Size(80, 28)
        };
        
        // 查询按钮点击事件
        btnQuery.Click += (s, e) =>
        {
            var groupName = cboHistoryGroup.SelectedItem?.ToString() ?? "";
            var messages = _databaseService.GetMessages(
                groupName,
                dtpStartDate.Value.Date,
                dtpEndDate.Value.Date.AddDays(1).AddSeconds(-1)
            );
            ShowMessagesInListView(messages);
        };

        filterPanel.Controls.AddRange(new Control[] {
            lblGroup, cboHistoryGroup, lblStartDate, dtpStartDate,
            lblEndDate, dtpEndDate, btnQuery
        });

        // 历史消息列表
        var lvHistory = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        lvHistory.Columns.Add("时间", 150);
        lvHistory.Columns.Add("发送者", 100);
        lvHistory.Columns.Add("内容", 400);

        panel.Controls.Add(filterPanel, 0, 0);
        panel.Controls.Add(lvHistory, 0, 1);

        page.Controls.Add(panel);

        // 加载群名称到下拉框
        LoadHistoryGroups(cboHistoryGroup);

        return page;
    }

    /// <summary>
    /// 加载群名称到历史记录筛选下拉框
    /// </summary>
    private void LoadHistoryGroups(ComboBox cbo)
    {
        var groups = _databaseService.GetGroupNames();
        cbo.Items.Clear();
        foreach (var group in groups)
        {
            cbo.Items.Add(group);
        }
        if (cbo.Items.Count > 0)
            cbo.SelectedIndex = 0;
    }

    #endregion

    #region 事件处理设置

    /// <summary>
    /// 设置服务事件处理程序
    /// 将服务的日志和状态事件绑定到界面更新方法
    /// </summary>
    private void SetupEventHandlers()
    {
        // 绑定自动化服务的日志事件
        _automationService.OnLog += AutomationService_OnLog;
        // 绑定自动化服务的状态变更事件
        _automationService.OnStatusChanged += AutomationService_OnStatusChanged;
    }

    /// <summary>
    /// 加载已保存的数据
    /// 从数据库读取定时任务列表
    /// </summary>
    private void LoadSavedData()
    {
        LoadScheduleTasks();
    }

    #endregion

    #region 服务事件处理

    /// <summary>
    /// 处理自动化服务的日志事件
    /// 将日志信息显示在日志文本框中
    /// </summary>
    private void AutomationService_OnLog(object sender, LogEventArgs e)
    {
        // 跨线程调用需要使用Invoke
        if (InvokeRequired)
        {
            Invoke(() => AutomationService_OnLog(sender, e));
            return;
        }

        // 格式化日志消息并追加到文本框
        var logMessage = $"[{e.Time:HH:mm:ss}] [{e.Level}] {e.Message}";
        _txtLog.AppendText(logMessage + Environment.NewLine);
        _txtLog.ScrollToCaret();  // 滚动到最新日志

        // 限制日志行数，防止内存占用过高
        TrimLogIfNeeded();
    }

    /// <summary>
    /// 如果日志行数超过限制，则删除旧日志
    /// </summary>
    private void TrimLogIfNeeded()
    {
        const int maxLogLines = 2000; // 最大保留2000行日志
        const int trimThreshold = 2200; // 超过2200行时开始清理
        
        var lines = _txtLog.Lines;
        if (lines.Length >= trimThreshold)
        {
            // 删除前200行，保留最新的2000行
            var newLines = lines.Skip(lines.Length - maxLogLines).ToArray();
            _txtLog.Lines = newLines;
            _txtLog.SelectionStart = _txtLog.Text.Length;
            _txtLog.ScrollToCaret();
        }
    }

    /// <summary>
    /// 处理自动化服务的状态变更事件
    /// 更新状态栏显示
    /// </summary>
    private void AutomationService_OnStatusChanged(object sender, StatusEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => AutomationService_OnStatusChanged(sender, e));
            return;
        }

        _lblStatus.Text = e.Status;
    }

    #endregion

    #region 工具栏按钮事件处理

    /// <summary>
    /// 连接微信按钮点击事件
    /// 尝试连接到微信PC客户端
    /// </summary>
    private async void BtnConnect_Click(object sender, EventArgs e)
    {
        _btnConnect.Enabled = false;
        _btnConnect.Text = "连接中...";

        // 异步执行连接操作，避免阻塞UI
        var connected = await Task.Run(() => _automationService.ConnectToWeChat());

        // 更新界面状态
        _isConnected = connected;
        _btnConnect.Text = connected ? "已连接" : "连接微信";
        _btnConnect.Enabled = !connected;
        _lblConnection.Text = connected ? "已连接" : "未连接";
        _lblConnection.ForeColor = connected ? Color.Green : Color.Red;

        // 连接成功后启用操作按钮
        if (connected)
        {
            _btnRefreshGroups.Enabled = true;
            _btnCapture.Enabled = true;
            _btnSend.Enabled = true;
            _btnExport.Enabled = true;
        }
    }

    /// <summary>
    /// 刷新群列表按钮点击事件
    /// 从微信获取所有聊天列表
    /// </summary>
    private async void BtnRefreshGroups_Click(object sender, EventArgs e)
    {
        _btnRefreshGroups.Enabled = false;
        _cboGroups.Items.Clear();
        _lvGroups.Items.Clear();

        // 异步获取群列表
        var groups = await Task.Run(() => _automationService.GetGroupList());

        // 填充群列表到界面
        foreach (var group in groups)
        {
            _cboGroups.Items.Add(group);
            
            // 获取该群的消息数量
            var count = _databaseService.GetMessageCount(group);
            var item = new ListViewItem(group);
            item.SubItems.Add(count.ToString());
            _lvGroups.Items.Add(item);
        }

        // 默认选中第一个
        if (_cboGroups.Items.Count > 0)
            _cboGroups.SelectedIndex = 0;

        _btnRefreshGroups.Enabled = true;
    }

    /// <summary>
    /// 抓取记录按钮点击事件
    /// 抓取当前选中群的聊天记录
    /// </summary>
    private async void BtnCapture_Click(object sender, EventArgs e)
    {
        var groupName = _cboGroups.Text;
        if (string.IsNullOrEmpty(groupName))
        {
            MessageBox.Show("请选择或输入群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnCapture.Enabled = false;
        _lblStatus.Text = "正在抓取...";

        // 异步执行抓取操作
        var result = await Task.Run(() => _automationService.CaptureGroupMessages(groupName));

        if (result.Success)
        {
            // 设置群名称并保存到数据库
            foreach (var msg in result.Messages)
            {
                msg.GroupName = groupName;
            }
            var savedCount = _databaseService.SaveMessages(result.Messages);
            
            // 显示抓取到的消息
            ShowMessagesInListView(result.Messages);
            
            MessageBox.Show($"成功抓取 {result.CapturedCount} 条消息，保存 {savedCount} 条新消息", 
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(result.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _btnCapture.Enabled = true;
        _lblStatus.Text = "就绪";
    }

    /// <summary>
    /// 发送消息按钮点击事件
    /// 将输入框中的消息发送到选中的群
    /// </summary>
    private async void BtnSend_Click(object sender, EventArgs e)
    {
        var groupName = _cboGroups.Text;
        var message = _txtMessage.Text;

        if (string.IsNullOrEmpty(groupName))
        {
            MessageBox.Show("请选择群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            MessageBox.Show("请输入消息内容", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnSend.Enabled = false;
        _lblStatus.Text = "正在发送...";

        // 异步执行发送操作
        var result = await Task.Run(() => _automationService.SendMessage(groupName, message));

        if (result.Success)
        {
            MessageBox.Show("消息发送成功", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtMessage.Clear();  // 清空输入框
        }
        else
        {
            MessageBox.Show(result.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _btnSend.Enabled = true;
        _lblStatus.Text = "就绪";
    }

    /// <summary>
    /// 导出记录按钮点击事件
    /// 将消息导出为TXT、CSV或JSON文件
    /// </summary>
    private void BtnExport_Click(object sender, EventArgs e)
    {
        var groupName = _cboGroups.Text;
        if (string.IsNullOrEmpty(groupName))
        {
            MessageBox.Show("请选择群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 显示保存文件对话框
        using var sfd = new SaveFileDialog
        {
            FileName = $"chat_{groupName}_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = "文本文件|*.txt|CSV文件|*.csv|JSON文件|*.json",
            Title = "导出聊天记录"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            // 从数据库获取该群的所有消息
            var messages = _databaseService.GetMessages(groupName);
            
            try
            {
                // 根据文件扩展名选择导出格式
                switch (Path.GetExtension(sfd.FileName).ToLower())
                {
                    case ".txt":
                        _exportService.ExportToTxt(messages, sfd.FileName, groupName);
                        break;
                    case ".csv":
                        _exportService.ExportToCsv(messages, sfd.FileName);
                        break;
                    case ".json":
                        _exportService.ExportToJson(messages, sfd.FileName, groupName);
                        break;
                }

                MessageBox.Show($"成功导出 {messages.Count} 条消息", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    #endregion

    #region 列表视图事件处理

    /// <summary>
    /// 群列表选择变更事件
    /// 当用户选择不同的群时，加载该群的消息记录
    /// </summary>
    private void LvGroups_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_lvGroups.SelectedItems.Count > 0)
        {
            var groupName = _lvGroups.SelectedItems[0].Text;
            _cboGroups.Text = groupName;
            _currentGroup = groupName;

            // 加载该群的历史消息
            var messages = _databaseService.GetMessages(groupName, limit: 100);
            ShowMessagesInListView(messages);
        }
    }

    /// <summary>
    /// 在消息列表视图中显示消息
    /// </summary>
    /// <param name="messages">要显示的消息列表</param>
    private void ShowMessagesInListView(List<ChatMessage> messages)
    {
        _lvMessages.Items.Clear();

        // 最多显示200条消息，避免界面卡顿
        foreach (var msg in messages.Take(200))
        {
            var item = new ListViewItem(msg.MessageTime.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(msg.Sender);
            // 内容过长时截断显示
            item.SubItems.Add(msg.Content.Length > 100 ? msg.Content.Substring(0, 100) + "..." : msg.Content);
            item.Tag = msg;  // 保存完整消息对象
            _lvMessages.Items.Add(item);
        }
    }

    #endregion

    #region 定时任务管理

    /// <summary>
    /// 加载定时任务列表到数据网格
    /// </summary>
    private void LoadScheduleTasks()
    {
        _dgvScheduleTasks.Rows.Clear();
        var tasks = _databaseService.GetScheduleTasks();

        foreach (var task in tasks)
        {
            _dgvScheduleTasks.Rows.Add(
                task.Id,
                task.Name,
                task.GroupName,
                task.CronExpression,
                task.IsEnabled ? "是" : "否",
                task.LastRunTime.ToString("yyyy-MM-dd HH:mm:ss"),
                task.NextRunTime.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
    }

    /// <summary>
    /// 启动/停止调度按钮点击事件
    /// 切换定时任务调度的运行状态
    /// </summary>
    private void BtnStartScheduler_Click(object sender, EventArgs e)
    {
        // 延迟创建调度服务实例
        if (_schedulerService == null)
        {
            _schedulerService = new SchedulerService(_automationService, _databaseService, _exportService);
            _schedulerService.OnLog += AutomationService_OnLog;
            _schedulerService.OnTaskExecuted += (s, args) =>
            {
                // 任务执行完成后刷新任务列表
                if (InvokeRequired)
                {
                    Invoke(() => LoadScheduleTasks());
                }
                else
                {
                    LoadScheduleTasks();
                }
            };
        }

        // 切换调度状态
        if (_btnStartScheduler.Text == "启动调度")
        {
            _schedulerService.StartAllTasks();
            _btnStartScheduler.Text = "停止调度";
        }
        else
        {
            _schedulerService.StopAllTasks();
            _btnStartScheduler.Text = "启动调度";
        }
    }

    /// <summary>
    /// 添加任务按钮点击事件
    /// 打开任务编辑对话框创建新任务
    /// </summary>
    private void BtnAddTask_Click(object sender, EventArgs e)
    {
        using var form = new ScheduleTaskForm(_cboGroups.Items.Cast<string>().ToList());
        if (form.ShowDialog() == DialogResult.OK)
        {
            _databaseService.SaveScheduleTask(form.Task);
            LoadScheduleTasks();
        }
    }

    /// <summary>
    /// 编辑任务按钮点击事件
    /// 打开任务编辑对话框修改选中的任务
    /// </summary>
    private void BtnEditTask_Click(object sender, EventArgs e)
    {
        if (_dgvScheduleTasks.SelectedRows.Count == 0)
        {
            MessageBox.Show("请选择要编辑的任务", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 获取选中任务的ID
        var taskId = Convert.ToInt32(_dgvScheduleTasks.SelectedRows[0].Cells["Id"].Value);
        var tasks = _databaseService.GetScheduleTasks();
        var task = tasks.FirstOrDefault(t => t.Id == taskId);

        if (task != null)
        {
            using var form = new ScheduleTaskForm(_cboGroups.Items.Cast<string>().ToList(), task);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _databaseService.UpdateScheduleTask(form.Task);
                LoadScheduleTasks();
            }
        }
    }

    /// <summary>
    /// 删除任务按钮点击事件
    /// 删除选中的定时任务
    /// </summary>
    private void BtnDeleteTask_Click(object sender, EventArgs e)
    {
        if (_dgvScheduleTasks.SelectedRows.Count == 0)
        {
            MessageBox.Show("请选择要删除的任务", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 确认删除
        if (MessageBox.Show("确定要删除选中的任务吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var taskId = Convert.ToInt32(_dgvScheduleTasks.SelectedRows[0].Cells["Id"].Value);
            _databaseService.DeleteScheduleTask(taskId);
            LoadScheduleTasks();
        }
    }

    #endregion

    #region 窗体关闭

    /// <summary>
    /// 窗体关闭事件
    /// 释放所有服务资源
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _schedulerService?.Dispose();
        _automationService?.Dispose();
        _databaseService?.Dispose();
        base.OnFormClosing(e);
    }

    #endregion
}
