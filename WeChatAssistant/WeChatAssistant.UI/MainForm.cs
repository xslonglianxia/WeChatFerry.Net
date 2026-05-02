using WeChatAssistant.Core.Events;
using WeChatAssistant.Core.Models;
using WeChatAssistant.Core.Services;

namespace WeChatAssistant.UI;

public partial class MainForm : Form
{
    private readonly WeChatAutomationService _automationService;
    private readonly DatabaseService _databaseService;
    private readonly ExportService _exportService;
    private SchedulerService? _schedulerService;
    private bool _isConnected;
    private string _currentGroup = string.Empty;

    private ListView _lvGroups = null!;
    private ListView _lvMessages = null!;
    private TextBox _txtLog = null!;
    private ComboBox _cboGroups = null!;
    private Button _btnConnect = null!;
    private Button _btnRefreshGroups = null!;
    private Button _btnCapture = null!;
    private Button _btnSend = null!;
    private Button _btnExport = null!;
    private TextBox _txtMessage = null!;
    private TabControl _tabControl = null!;
    private DataGridView _dgvScheduleTasks = null!;
    private Button _btnAddTask = null!;
    private Button _btnEditTask = null!;
    private Button _btnDeleteTask = null!;
    private Button _btnStartScheduler = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _lblStatus = null!;
    private ToolStripStatusLabel _lblConnection = null!;

    public MainForm()
    {
        InitializeComponent();
        
        _databaseService = new DatabaseService();
        _automationService = new WeChatAutomationService();
        _exportService = new ExportService();

        SetupEventHandlers();
        LoadSavedData();
    }

    private void InitializeComponent()
    {
        this.Text = "微信群聊助手";
        this.Size = new Size(1000, 700);
        this.MinimumSize = new Size(800, 500);
        this.StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbarPanel = CreateToolbarPanel();
        mainPanel.Controls.Add(toolbarPanel, 0, 0);

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.TabPages.Add(CreateCaptureTabPage());
        _tabControl.TabPages.Add(CreateScheduleTabPage());
        _tabControl.TabPages.Add(CreateHistoryTabPage());

        mainPanel.Controls.Add(_tabControl, 0, 1);

        _statusStrip = new StatusStrip();
        _lblConnection = new ToolStripStatusLabel("未连接");
        _lblStatus = new ToolStripStatusLabel("就绪");
        _statusStrip.Items.Add(_lblConnection);
        _statusStrip.Items.Add(new ToolStripStatusLabel(" | "));
        _statusStrip.Items.Add(_lblStatus);

        this.Controls.Add(mainPanel);
        this.Controls.Add(_statusStrip);
    }

    private Panel CreateToolbarPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control
        };

        _btnConnect = new Button
        {
            Text = "连接微信",
            Location = new Point(10, 10),
            Size = new Size(100, 30)
        };
        _btnConnect.Click += BtnConnect_Click;

        _btnRefreshGroups = new Button
        {
            Text = "刷新群列表",
            Location = new Point(120, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnRefreshGroups.Click += BtnRefreshGroups_Click;

        _cboGroups = new ComboBox
        {
            Location = new Point(230, 12),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDown
        };

        _btnCapture = new Button
        {
            Text = "抓取记录",
            Location = new Point(440, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnCapture.Click += BtnCapture_Click;

        _btnSend = new Button
        {
            Text = "发送消息",
            Location = new Point(550, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnSend.Click += BtnSend_Click;

        _btnExport = new Button
        {
            Text = "导出记录",
            Location = new Point(660, 10),
            Size = new Size(100, 30),
            Enabled = false
        };
        _btnExport.Click += BtnExport_Click;

        panel.Controls.AddRange(new Control[] {
            _btnConnect, _btnRefreshGroups, _cboGroups,
            _btnCapture, _btnSend, _btnExport
        });

        return panel;
    }

    private TabPage CreateCaptureTabPage()
    {
        var page = new TabPage("消息抓取");

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 400
        };

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

        _lvGroups = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _lvGroups.Columns.Add("群名称", 200);
        _lvGroups.Columns.Add("消息数", 80);
        _lvGroups.SelectedIndexChanged += LvGroups_SelectedIndexChanged;

        var messagePanel = new Panel { Dock = DockStyle.Fill };
        
        var lblMessage = new Label
        {
            Text = "发送内容:",
            Location = new Point(5, 5),
            AutoSize = true
        };

        _txtMessage = new TextBox
        {
            Location = new Point(5, 25),
            Size = new Size(messagePanel.Width - 20, 80),
            Multiline = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

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
        messagePanel.Resize += (s, e) =>
        {
            _txtMessage.Width = messagePanel.Width - 20;
            _lvMessages.Size = new Size(messagePanel.Width - 20, messagePanel.Height - 125);
        };

        topPanel.Controls.Add(_lvGroups, 0, 0);
        topPanel.Controls.Add(messagePanel, 1, 0);

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
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9)
        };

        logPanel.Controls.AddRange(new Control[] { _txtLog, lblLog });

        splitContainer.Panel1.Controls.Add(topPanel);
        splitContainer.Panel2.Controls.Add(logPanel);

        page.Controls.Add(splitContainer);
        return page;
    }

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
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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
        _dgvScheduleTasks.Columns["Id"]!.Visible = false;

        mainPanel.Controls.Add(toolbarPanel, 0, 0);
        mainPanel.Controls.Add(_dgvScheduleTasks, 0, 1);

        page.Controls.Add(mainPanel);
        return page;
    }

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
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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

        LoadHistoryGroups(cboHistoryGroup);

        return page;
    }

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

    private void SetupEventHandlers()
    {
        _automationService.OnLog += AutomationService_OnLog;
        _automationService.OnStatusChanged += AutomationService_OnStatusChanged;
    }

    private void LoadSavedData()
    {
        LoadScheduleTasks();
    }

    private void AutomationService_OnLog(object sender, LogEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => AutomationService_OnLog(sender, e));
            return;
        }

        var logMessage = $"[{e.Time:HH:mm:ss}] [{e.Level}] {e.Message}";
        _txtLog.AppendText(logMessage + Environment.NewLine);
        _txtLog.ScrollToCaret();
    }

    private void AutomationService_OnStatusChanged(object sender, StatusEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => AutomationService_OnStatusChanged(sender, e));
            return;
        }

        _lblStatus.Text = e.Status;
    }

    private async void BtnConnect_Click(object sender, EventArgs e)
    {
        _btnConnect.Enabled = false;
        _btnConnect.Text = "连接中...";

        var connected = await Task.Run(() => _automationService.ConnectToWeChat());

        _isConnected = connected;
        _btnConnect.Text = connected ? "已连接" : "连接微信";
        _btnConnect.Enabled = !connected;
        _lblConnection.Text = connected ? "已连接" : "未连接";
        _lblConnection.ForeColor = connected ? Color.Green : Color.Red;

        if (connected)
        {
            _btnRefreshGroups.Enabled = true;
            _btnCapture.Enabled = true;
            _btnSend.Enabled = true;
            _btnExport.Enabled = true;
        }
    }

    private async void BtnRefreshGroups_Click(object sender, EventArgs e)
    {
        _btnRefreshGroups.Enabled = false;
        _cboGroups.Items.Clear();
        _lvGroups.Items.Clear();

        var groups = await Task.Run(() => _automationService.GetGroupList());

        foreach (var group in groups)
        {
            _cboGroups.Items.Add(group);
            
            var count = _databaseService.GetMessageCount(group);
            var item = new ListViewItem(group);
            item.SubItems.Add(count.ToString());
            _lvGroups.Items.Add(item);
        }

        if (_cboGroups.Items.Count > 0)
            _cboGroups.SelectedIndex = 0;

        _btnRefreshGroups.Enabled = true;
    }

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

        var result = await Task.Run(() => _automationService.CaptureGroupMessages(groupName));

        if (result.Success)
        {
            foreach (var msg in result.Messages)
            {
                msg.GroupName = groupName;
            }
            var savedCount = _databaseService.SaveMessages(result.Messages);
            
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

        var result = await Task.Run(() => _automationService.SendMessage(groupName, message));

        if (result.Success)
        {
            MessageBox.Show("消息发送成功", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtMessage.Clear();
        }
        else
        {
            MessageBox.Show(result.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _btnSend.Enabled = true;
        _lblStatus.Text = "就绪";
    }

    private void BtnExport_Click(object sender, EventArgs e)
    {
        var groupName = _cboGroups.Text;
        if (string.IsNullOrEmpty(groupName))
        {
            MessageBox.Show("请选择群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            FileName = $"chat_{groupName}_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = "文本文件|*.txt|CSV文件|*.csv|JSON文件|*.json",
            Title = "导出聊天记录"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            var messages = _databaseService.GetMessages(groupName);
            
            try
            {
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

    private void LvGroups_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_lvGroups.SelectedItems.Count > 0)
        {
            var groupName = _lvGroups.SelectedItems[0].Text;
            _cboGroups.Text = groupName;
            _currentGroup = groupName;

            var messages = _databaseService.GetMessages(groupName, limit: 100);
            ShowMessagesInListView(messages);
        }
    }

    private void ShowMessagesInListView(List<ChatMessage> messages)
    {
        _lvMessages.Items.Clear();

        foreach (var msg in messages.Take(200))
        {
            var item = new ListViewItem(msg.MessageTime.ToString("yyyy-MM-dd HH:mm:ss"));
            item.SubItems.Add(msg.Sender);
            item.SubItems.Add(msg.Content.Length > 100 ? msg.Content.Substring(0, 100) + "..." : msg.Content);
            item.Tag = msg;
            _lvMessages.Items.Add(item);
        }
    }

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

    private void BtnStartScheduler_Click(object sender, EventArgs e)
    {
        if (_schedulerService == null)
        {
            _schedulerService = new SchedulerService(_automationService, _databaseService, _exportService);
            _schedulerService.OnLog += AutomationService_OnLog;
            _schedulerService.OnTaskExecuted += (s, args) =>
            {
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

    private void BtnAddTask_Click(object sender, EventArgs e)
    {
        using var form = new ScheduleTaskForm(_cboGroups.Items.Cast<string>().ToList());
        if (form.ShowDialog() == DialogResult.OK)
        {
            _databaseService.SaveScheduleTask(form.Task);
            LoadScheduleTasks();
        }
    }

    private void BtnEditTask_Click(object sender, EventArgs e)
    {
        if (_dgvScheduleTasks.SelectedRows.Count == 0)
        {
            MessageBox.Show("请选择要编辑的任务", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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

    private void BtnDeleteTask_Click(object sender, EventArgs e)
    {
        if (_dgvScheduleTasks.SelectedRows.Count == 0)
        {
            MessageBox.Show("请选择要删除的任务", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show("确定要删除选中的任务吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            var taskId = Convert.ToInt32(_dgvScheduleTasks.SelectedRows[0].Cells["Id"].Value);
            _databaseService.DeleteScheduleTask(taskId);
            LoadScheduleTasks();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _schedulerService?.Dispose();
        _automationService?.Dispose();
        _databaseService?.Dispose();
        base.OnFormClosing(e);
    }
}
