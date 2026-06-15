namespace LiveAgent;

public class SettingsForm : Form
{
    private readonly TextBox _urlBox;
    private readonly TextBox _tokenBox;
    private readonly NumericUpDown _intervalBox;
    private readonly NumericUpDown _heartbeatBox;
    private readonly NumericUpDown _idleBox;
    private readonly CheckBox _logCheckBox;

    public AgentConfig? ResultConfig { get; private set; }

    public SettingsForm(AgentConfig? currentConfig)
    {
        var cfg = currentConfig ?? new AgentConfig();

        Text = "Live Dashboard - 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: Server URL
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "服务器地址:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _urlBox = new TextBox { Text = cfg.ServerUrl, Width = 300, Anchor = AnchorStyles.Left };
        panel.Controls.Add(_urlBox, 1, 0);

        // Row 1: Token
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "Token:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _tokenBox = new TextBox { Text = cfg.Token, Width = 300, Anchor = AnchorStyles.Left, PasswordChar = '*' };
        panel.Controls.Add(_tokenBox, 1, 1);

        // Row 2: Interval
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "上报间隔 (秒):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _intervalBox = new NumericUpDown { Minimum = 1, Maximum = 300, Value = cfg.IntervalSeconds, Width = 80, Anchor = AnchorStyles.Left };
        panel.Controls.Add(_intervalBox, 1, 2);

        // Row 3: Heartbeat
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "心跳间隔 (秒):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _heartbeatBox = new NumericUpDown { Minimum = 10, Maximum = 600, Value = cfg.HeartbeatSeconds, Width = 80, Anchor = AnchorStyles.Left };
        panel.Controls.Add(_heartbeatBox, 1, 3);

        // Row 4: Idle threshold
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = "AFK 判定 (秒):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _idleBox = new NumericUpDown { Minimum = 30, Maximum = 3600, Value = cfg.IdleThresholdSeconds, Width = 80, Anchor = AnchorStyles.Left };
        panel.Controls.Add(_idleBox, 1, 4);

        // Row 5: Log checkbox
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _logCheckBox = new CheckBox { Text = "开启日志文件 (保留 2 天)", Checked = cfg.EnableLog, AutoSize = true, Anchor = AnchorStyles.Left };
        panel.Controls.Add(_logCheckBox, 0, 5);
        panel.SetColumnSpan(_logCheckBox, 2);

        // Row 6: Buttons
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Anchor = AnchorStyles.None };
        var btnSave = new Button { Text = "保存", Width = 80, Height = 30 };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 80, Height = 30 };
        btnPanel.Controls.Add(btnSave);
        btnPanel.Controls.Add(btnCancel);
        panel.Controls.Add(btnPanel, 0, 6);
        panel.SetColumnSpan(btnPanel, 2);

        btnSave.Click += OnSave;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(panel);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        ClientSize = new System.Drawing.Size(460, 330);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var newCfg = new AgentConfig
        {
            ServerUrl = _urlBox.Text.Trim(),
            Token = _tokenBox.Text.Trim(),
            IntervalSeconds = (int)_intervalBox.Value,
            HeartbeatSeconds = (int)_heartbeatBox.Value,
            IdleThresholdSeconds = (int)_idleBox.Value,
            EnableLog = _logCheckBox.Checked,
        };

        string? err = ConfigManager.Validate(newCfg);
        if (err != null)
        {
            MessageBox.Show(err, "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
            return;
        }

        if (ConfigManager.Save(newCfg))
        {
            ResultConfig = newCfg;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show("无法写入 config.json", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
        }
    }
}
