using WeChatAssistant.Core.Models;

namespace WeChatAssistant.UI;

public class ScheduleTaskForm : Form
{
    private readonly List<string> _groupNames;
    public ScheduleTask Task { get; private set; }

    private TextBox _txtName = null!;
    private ComboBox _cboGroupName = null!;
    private ComboBox _cboCronPreset = null!;
    private TextBox _txtCronExpression = null!;
    private CheckBox _chkEnabled = null!;
    private TextBox _txtExportPath = null!;
    private Button _btnBrowse = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    public ScheduleTaskForm(List<string> groupNames, ScheduleTask? existingTask = null)
    {
        _groupNames = groupNames;
        Task = existingTask ?? new ScheduleTask
        {
            IsEnabled = true,
            CronExpression = "0 9 * * *"
        };

        InitializeComponent();
        LoadData();
    }

    private void InitializeComponent()
    {
        this.Text = Task.Id > 0 ? "编辑定时任务" : "添加定时任务";
        this.Size = new Size(450, 350);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 7,
            ColumnCount = 2
        };

        for (int i = 0; i < 6; i++)
        {
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        }
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblName = new Label { Text = "任务名称:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _txtName = new TextBox { Dock = DockStyle.Fill };

        var lblGroup = new Label { Text = "群名称:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _cboGroupName = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };

        var lblCronPreset = new Label { Text = "预设时间:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _cboCronPreset = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
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
        var exportPanel = new Panel { Dock = DockStyle.Fill };
        _txtExportPath = new TextBox { Dock = DockStyle.Fill };
        _btnBrowse = new Button { Text = "...", Dock = DockStyle.Right, Width = 30 };
        _btnBrowse.Click += BtnBrowse_Click;
        exportPanel.Controls.AddRange(new Control[] { _txtExportPath, _btnBrowse });

        var lblEnabled = new Label { Text = "启用:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        _chkEnabled = new CheckBox { Dock = DockStyle.Fill };

        mainPanel.Controls.AddRange(new Control[] {
            lblName, _txtName,
            lblGroup, _cboGroupName,
            lblCronPreset, _cboCronPreset,
            lblCron, _txtCronExpression,
            lblExportPath, exportPanel,
            lblEnabled, _chkEnabled
        });

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = SystemColors.Control
        };

        _btnOK = new Button { Text = "确定", DialogResult = DialogResult.OK, Size = new Size(80, 30) };
        _btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(80, 30) };

        _btnOK.Click += BtnOK_Click;

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };
        btnPanel.Controls.AddRange(new Control[] { _btnCancel, _btnOK });

        buttonPanel.Controls.Add(btnPanel);

        this.Controls.Add(mainPanel);
        this.Controls.Add(buttonPanel);

        this.AcceptButton = _btnOK;
        this.CancelButton = _btnCancel;
    }

    private void LoadData()
    {
        _txtName.Text = Task.Name;
        _txtCronExpression.Text = Task.CronExpression;
        _chkEnabled.Checked = Task.IsEnabled;
        _txtExportPath.Text = Task.ExportPath;

        foreach (var group in _groupNames)
        {
            _cboGroupName.Items.Add(group);
        }
        _cboGroupName.Text = Task.GroupName;

        UpdateCronPresetSelection();
    }

    private void CboCronPreset_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = _cboCronPreset.SelectedItem?.ToString();
        _txtCronExpression.Text = selected switch
        {
            "每天 09:00" => "0 9 * * *",
            "每天 12:00" => "0 12 * * *",
            "每天 18:00" => "0 18 * * *",
            "每小时" => "0 * * * *",
            "每30分钟" => "*/30 * * * *",
            _ => _txtCronExpression.Text
        };
    }

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

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("请输入任务名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_cboGroupName.Text))
        {
            MessageBox.Show("请选择或输入群名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtCronExpression.Text))
        {
            MessageBox.Show("请输入Cron表达式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Task.Name = _txtName.Text.Trim();
        Task.GroupName = _cboGroupName.Text.Trim();
        Task.CronExpression = _txtCronExpression.Text.Trim();
        Task.IsEnabled = _chkEnabled.Checked;
        Task.ExportPath = _txtExportPath.Text.Trim();

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
