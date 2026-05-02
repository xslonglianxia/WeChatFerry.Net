using WeChatAssistant.Core.Models;

namespace WeChatAssistant.UI;

/// <summary>
/// 定时任务编辑窗体
/// 用于创建和编辑定时抓取/发送任务
/// </summary>
/// <remarks>
/// 功能说明：
/// - 设置任务名称和目标群
/// - 配置执行时间（支持预设和自定义Cron表达式）
/// - 设置导出路径（可选）
/// - 启用/禁用任务
/// </remarks>
public class ScheduleTaskForm : Form
{
    #region 私有字段

    /// <summary>
    /// 可选的群名称列表
    /// 用于填充群选择下拉框
    /// </summary>
    private readonly List<string> _groupNames;

    #endregion

    #region 公共属性

    /// <summary>
    /// 获取或设置正在编辑的任务对象
    /// 新建任务时返回新创建的任务
    /// 编辑任务时返回修改后的任务
    /// </summary>
    public ScheduleTask Task { get; private set; }

    #endregion

    #region UI控件

    private TextBox _txtName = null!;              // 任务名称输入框
    private ComboBox _cboGroupName = null!;        // 群名称下拉框
    private ComboBox _cboCronPreset = null!;       // Cron预设下拉框
    private TextBox _txtCronExpression = null!;    // Cron表达式输入框
    private CheckBox _chkEnabled = null!;          // 启用复选框
    private TextBox _txtExportPath = null!;        // 导出路径输入框
    private Button _btnBrowse = null!;             // 浏览按钮
    private Button _btnOK = null!;                 // 确定按钮
    private Button _btnCancel = null!;             // 取消按钮

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建定时任务编辑窗体
    /// </summary>
    /// <param name="groupNames">可选的群名称列表</param>
    /// <param name="existingTask">
    /// 现有任务对象（编辑模式）
    /// 为null时表示新建任务
    /// </param>
    public ScheduleTaskForm(List<string> groupNames, ScheduleTask? existingTask = null)
    {
        _groupNames = groupNames;
        
        // 初始化任务对象
        Task = existingTask ?? new ScheduleTask
        {
            IsEnabled = true,
            CronExpression = "0 9 * * *"  // 默认每天9:00执行
        };

        // 初始化界面组件
        InitializeComponent();
        
        // 加载数据到界面
        LoadData();
    }

    #endregion

    #region 界面初始化

    /// <summary>
    /// 初始化界面组件
    /// 创建所有控件并设置布局
    /// </summary>
    private void InitializeComponent()
    {
        // 设置窗体属性
        this.Text = Task.Id > 0 ? "编辑定时任务" : "添加定时任务";
        this.Size = new Size(450, 350);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;

        // 创建主布局面板
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 7,
            ColumnCount = 2
        };

        // 设置行高
        for (int i = 0; i < 6; i++)
        {
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        }
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 最后一行填充剩余空间

        // 设置列宽
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));  // 标签列
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // 输入列

        // 创建控件
        var lblName = new Label { Text = "任务名称:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _txtName = new TextBox { Dock = DockStyle.Fill };

        var lblGroup = new Label { Text = "群名称:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _cboGroupName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };

        var lblCronPreset = new Label { Text = "预设时间:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _cboCronPreset = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        
        // 添加预设选项
        _cboCronPreset.Items.AddRange(new object[] {
            "每天 09:00",
            "每天 12:00",
            "每天 18:00",
            "每小时",
            "每30分钟",
            "自定义"
        });
        _cboCronPreset.SelectedIndexChanged += CboCronPreset_SelectedIndexChanged;

        var lblCron = new Label { Text = "Cron表达式:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _txtCronExpression = new TextBox { Dock = DockStyle.Fill };

        var lblExportPath = new Label { Text = "导出路径:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        
        // 导出路径面板（包含文本框和浏览按钮）
        var exportPanel = new Panel { Dock = DockStyle.Fill };
        _txtExportPath = new TextBox { Dock = DockStyle.Fill };
        _btnBrowse = new Button { Text = "...", Dock = DockStyle.Right, Width = 30 };
        _btnBrowse.Click += BtnBrowse_Click;
        exportPanel.Controls.AddRange(new Control[] { _txtExportPath, _btnBrowse });

        var lblEnabled = new Label { Text = "启用:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _chkEnabled = new CheckBox { Dock = DockStyle.Fill };

        // 添加控件到面板
        mainPanel.Controls.AddRange(new Control[] {
            lblName, _txtName,
            lblGroup, _cboGroupName,
            lblCronPreset, _cboCronPreset,
            lblCron, _txtCronExpression,
            lblExportPath, exportPanel,
            lblEnabled, _chkEnabled
        });

        // 创建底部按钮面板
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = SystemColors.Control
        };

        _btnOK = new Button { Text = "确定", DialogResult = DialogResult.OK, Size = new Size(80, 30) };
        _btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(80, 30) };

        _btnOK.Click += BtnOK_Click;

        // 按钮容器（右对齐）
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };
        btnPanel.Controls.AddRange(new Control[] { _btnCancel, _btnOK });

        buttonPanel.Controls.Add(btnPanel);

        // 添加控件到窗体
        this.Controls.Add(mainPanel);
        this.Controls.Add(buttonPanel);

        // 设置窗体的接受和取消按钮
        this.AcceptButton = _btnOK;
        this.CancelButton = _btnCancel;
    }

    #endregion

    #region 数据加载

    /// <summary>
    /// 加载数据到界面控件
    /// 将任务对象的属性显示在对应的输入控件中
    /// </summary>
    private void LoadData()
    {
        _txtName.Text = Task.Name;
        _txtCronExpression.Text = Task.CronExpression;
        _chkEnabled.Checked = Task.IsEnabled;
        _txtExportPath.Text = Task.ExportPath;

        // 填充群名称下拉框
        foreach (var group in _groupNames)
        {
            _cboGroupName.Items.Add(group);
        }
        _cboGroupName.Text = Task.GroupName;

        // 根据Cron表达式选择对应的预设项
        UpdateCronPresetSelection();
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// Cron预设下拉框选择变更事件
    /// 根据预设选项自动填充Cron表达式
    /// </summary>
    private void CboCronPreset_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = _cboCronPreset.SelectedItem?.ToString();
        
        // 根据预设设置对应的Cron表达式
        _txtCronExpression.Text = selected switch
        {
            "每天 09:00" => "0 9 * * *",
            "每天 12:00" => "0 12 * * *",
            "每天 18:00" => "0 18 * * *",
            "每小时" => "0 * * * *",
            "每30分钟" => "*/30 * * * *",
            _ => _txtCronExpression.Text  // 自定义时保持原值
        };
    }

    /// <summary>
    /// 根据当前Cron表达式更新预设下拉框的选择
    /// </summary>
    private void UpdateCronPresetSelection()
    {
        var cron = Task.CronExpression;
        _cboCronPreset.SelectedItem = cron switch
        {
            "0 9 * * *" => "每天 09:00",
            "0 12 * * *" => "每天 12:00",
            "0 18 * * *" => "每天 18:00",
            "0 * * * *" => "每小时",
            "*/30 * * * *" => "每30分钟",
            _ => "自定义"
        };
    }

    /// <summary>
    /// 浏览按钮点击事件
    /// 打开文件夹选择对话框设置导出路径
    /// </summary>
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "选择导出文件保存目录"
        };

        if (fbd.ShowDialog() == DialogResult.OK)
        {
            _txtExportPath.Text = fbd.SelectedPath;
        }
    }

    /// <summary>
    /// 确定按钮点击事件
    /// 验证输入并保存到任务对象
    /// </summary>
    private void BtnOK_Click(object? sender, EventArgs e)
    {
        // 验证任务名称
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("请输入任务名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 验证群名称
        if (string.IsNullOrWhiteSpace(_cboGroupName.Text))
        {
            MessageBox.Show("请选择或输入群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 验证Cron表达式
        if (string.IsNullOrWhiteSpace(_txtCronExpression.Text))
        {
            MessageBox.Show("请输入Cron表达式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 保存到任务对象
        Task.Name = _txtName.Text.Trim();
        Task.GroupName = _cboGroupName.Text.Trim();
        Task.CronExpression = _txtCronExpression.Text.Trim();
        Task.IsEnabled = _chkEnabled.Checked;
        Task.ExportPath = _txtExportPath.Text.Trim();

        // 设置对话框结果并关闭
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    #endregion
}
