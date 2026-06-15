using System.Diagnostics;

namespace LiveAgent;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly System.Threading.Timer _monitorTimer;
    private readonly AgentConfig _config;
    private readonly Reporter _reporter;

    private readonly Icon _greenIcon;
    private readonly Icon _orangeIcon;
    private readonly Icon _grayIcon;

    private readonly object _lock = new();
    private string _status = "初始化中";
    private string _currentTarget = "";
    private bool _settingsRequested;

    // Monitor state
    private string? _prevApp;
    private string? _prevTitle;
    private long _lastReportTime;
    private bool _wasIdle;
    private readonly long _monitorStartTime;

    public bool SettingsRequested => _settingsRequested;

    public TrayApplicationContext(AgentConfig config)
    {
        _config = config;
        _reporter = new Reporter(config.ServerUrl, config.Token);
        _monitorStartTime = Stopwatch.GetTimestamp();

        _greenIcon = CreateCircleIcon(System.Drawing.Color.FromArgb(76, 175, 80));
        _orangeIcon = CreateCircleIcon(System.Drawing.Color.FromArgb(255, 152, 0));
        _grayIcon = CreateCircleIcon(System.Drawing.Color.FromArgb(158, 158, 158));

        _menu = new ContextMenuStrip();
        RebuildMenu();

        string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        Icon trayIcon;
        if (File.Exists(iconPath))
        {
            try { trayIcon = new Icon(iconPath); }
            catch { trayIcon = _grayIcon; }
        }
        else
        {
            trayIcon = _grayIcon;
        }

        _trayIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "Live Dashboard",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        _trayIcon.MouseDoubleClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenSettings();
        };

        _monitorTimer = new System.Threading.Timer(MonitorTick, null,
            TimeSpan.Zero, TimeSpan.FromSeconds(_config.IntervalSeconds));
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        var statusItem = new ToolStripMenuItem($"状态: {_status}") { Enabled = false };
        _menu.Items.Add(statusItem);

        string current;
        lock (_lock) { current = _currentTarget; }
        var currentItem = new ToolStripMenuItem($"当前: {(string.IsNullOrEmpty(current) ? "无" : current)}") { Enabled = false };
        _menu.Items.Add(currentItem);

        _menu.Items.Add(new ToolStripSeparator());

        var logItem = new ToolStripMenuItem("日志文件")
        {
            CheckOnClick = true,
            Checked = _config.EnableLog,
        };
        logItem.Click += (s, e) => ToggleLog(logItem);
        _menu.Items.Add(logItem);

        var autostartItem = new ToolStripMenuItem("开机自启")
        {
            CheckOnClick = true,
            Checked = AutostartManager.IsAutostartEnabled(),
        };
        autostartItem.Click += (s, e) => ToggleAutostart(autostartItem);
        _menu.Items.Add(autostartItem);

        var settingsItem = new ToolStripMenuItem("设置");
        settingsItem.Click += (s, e) => OpenSettings();
        _menu.Items.Add(settingsItem);

        _menu.Items.Add(new ToolStripSeparator());

        var quitItem = new ToolStripMenuItem("退出");
        quitItem.Click += (s, e) => Quit();
        _menu.Items.Add(quitItem);

        // Refresh dynamic items on open
        _menu.Opening += (s, e) =>
        {
            statusItem.Text = $"状态: {_status}";
            string ct;
            lock (_lock) { ct = _currentTarget; }
            currentItem.Text = $"当前: {(string.IsNullOrEmpty(ct) ? "无" : ct)}";
            logItem.Checked = _config.EnableLog;
            autostartItem.Checked = AutostartManager.IsAutostartEnabled();
        };
    }

    private void UpdateStatus(string status, string? currentTarget = null)
    {
        lock (_lock)
        {
            _status = status;
            if (currentTarget != null)
                _currentTarget = currentTarget;
        }

        string colorName = status switch
        {
            "在线" => "green",
            "AFK" => "orange",
            _ => "gray",
        };
        var icon = colorName switch
        {
            "green" => _greenIcon,
            "orange" => _orangeIcon,
            _ => _grayIcon,
        };
        _trayIcon.Icon = icon;

        string ct;
        lock (_lock) { ct = _currentTarget; }
        string tip = "Live Dashboard";
        if (!string.IsNullOrEmpty(ct))
            tip += $"\n当前: {ct}";
        tip += $"\n{status}";
        _trayIcon.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    private void ToggleLog(ToolStripMenuItem item)
    {
        _config.EnableLog = !_config.EnableLog;
        Logger.SetFileLogging(_config.EnableLog);
        ConfigManager.Save(_config);
        item.Checked = _config.EnableLog;
    }

    private void ToggleAutostart(ToolStripMenuItem item)
    {
        bool wasEnabled = AutostartManager.IsAutostartEnabled();
        if (wasEnabled)
        {
            if (!AutostartManager.ToggleAutostart(false))
            {
                NativeMethods.MessageBoxW(IntPtr.Zero,
                    "关闭开机自启时未能清理全部启动项。\n请检查任务计划程序中的 LiveDashboardAgent。",
                    "Live Dashboard", 0x10);
            }
            else
            {
                Logger.Info("Autostart disabled");
            }
        }
        else
        {
            if (AutostartManager.ToggleAutostart(true))
            {
                Logger.Info("Autostart enabled");
            }
            else
            {
                NativeMethods.MessageBoxW(IntPtr.Zero,
                    "无法开启开机自启，请检查当前账户是否有写入启动项的权限。",
                    "Live Dashboard", 0x10);
            }
        }
        item.Checked = AutostartManager.IsAutostartEnabled();
    }

    private void OpenSettings()
    {
        _settingsRequested = true;
        _monitorTimer.Dispose();
        _trayIcon.Visible = false;
        ExitThread();
    }

    private void Quit()
    {
        _monitorTimer.Dispose();
        _trayIcon.Visible = false;
        Logger.Shutdown();
        Environment.Exit(0);
    }

    private long NowSeconds() => Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    private void MonitorTick(object? state)
    {
        try
        {
            double idleSecs = SystemMonitor.GetIdleSeconds();
            bool isIdle = idleSecs >= _config.IdleThresholdSeconds
                          && !SystemMonitor.IsAudioPlaying()
                          && !SystemMonitor.IsForegroundFullscreen();

            long nowTicks = Stopwatch.GetTimestamp();
            double nowSec = (double)nowTicks / Stopwatch.Frequency;

            if (isIdle && !_wasIdle)
            {
                Logger.Info($"User idle ({idleSecs:F0}s)");
                _wasIdle = true;
                UpdateStatus("AFK");
            }
            else if (!isIdle && _wasIdle)
            {
                Logger.Info("User returned");
                _wasIdle = false;
            }

            if (isIdle)
            {
                double elapsedSinceReport = nowSec - (double)_lastReportTime / Stopwatch.Frequency;
                if (elapsedSinceReport >= _config.HeartbeatSeconds)
                {
                    var extra = SystemMonitor.GetBatteryExtra();
                    string idleTarget = FormatReportTarget("idle", "User is away");
                    if (_reporter.Send("idle", "User is away", extra))
                    {
                        _prevApp = "idle";
                        _prevTitle = "User is away";
                        _lastReportTime = nowTicks;
                        UpdateStatus("AFK", idleTarget);
                    }
                    else if (_reporter.RetryDelay > 0)
                    {
                        // Will retry on next tick
                    }
                }
                return;
            }

            var info = SystemMonitor.GetForegroundInfo();
            if (info == null)
                return;

            var (appId, title) = info.Value;
            UpdateStatus("在线");

            bool changed = appId != _prevApp || title != _prevTitle;
            double heartbeatElapsed = nowSec - (double)_lastReportTime / Stopwatch.Frequency;
            bool heartbeatDue = heartbeatElapsed >= _config.HeartbeatSeconds;

            if (changed || heartbeatDue)
            {
                var extra = SystemMonitor.GetBatteryExtra();
                var music = MusicDetector.GetMusicInfo();
                if (music != null)
                    extra["music"] = music;

                string reportedTarget = FormatReportTarget(appId, title);
                if (_reporter.Send(appId, title, extra))
                {
                    _prevApp = appId;
                    _prevTitle = title;
                    _lastReportTime = nowTicks;
                    UpdateStatus("在线", reportedTarget);
                    if (changed)
                        Logger.Info($"Reported: {reportedTarget}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error: {ex.Message}");
        }
    }

    private static string FormatReportTarget(string appId, string windowTitle)
    {
        string app = string.IsNullOrWhiteSpace(appId) ? "unknown" : appId.Trim();
        string title = (windowTitle ?? "").Trim();
        if (string.IsNullOrEmpty(title) || title == app)
            return app;
        string truncated = title.Length > 80 ? title[..80] : title;
        return $"{app} — {truncated}";
    }

    private static Icon CreateCircleIcon(System.Drawing.Color color)
    {
        using var bmp = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 8, 8, 48, 48);
        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitorTimer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _menu.Dispose();
            _greenIcon.Dispose();
            _orangeIcon.Dispose();
            _grayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
