namespace LiveAgent;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        Logger.Info("Live Dashboard Windows Agent");

        while (true)
        {
            var cfg = ConfigManager.Load();

            // No valid config → show settings dialog
            if (string.IsNullOrEmpty(cfg.ServerUrl)
                || string.IsNullOrEmpty(cfg.Token)
                || cfg.Token == "YOUR_TOKEN_HERE")
            {
                cfg = ShowSettingsDialog(cfg);
                if (cfg == null) return;
                cfg = ConfigManager.Load();
            }

            string? err = ConfigManager.Validate(cfg);
            if (err != null)
            {
                Logger.Warning($"Invalid config: {err}");
                cfg = ShowSettingsDialog(cfg);
                if (cfg == null) return;
                cfg = ConfigManager.Load();
                continue;
            }

            // Apply log preference
            Logger.SetFileLogging(cfg.EnableLog);
            if (cfg.EnableLog)
                Logger.Info(cfg.ServerUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "HTTP: HTTPS" : "HTTP: HTTP");

            var ctx = new TrayApplicationContext(cfg);
            Application.Run(ctx);

            if (ctx.SettingsRequested)
            {
                var newCfg = ShowSettingsDialog(cfg);
                if (newCfg == null)
                    continue; // Cancelled, restart with old config
                continue; // Restart with new config
            }
            else
            {
                break; // Quit
            }
        }

        Logger.Info("Agent stopped");
    }

    private static AgentConfig? ShowSettingsDialog(AgentConfig currentConfig)
    {
        using var form = new SettingsForm(currentConfig);
        if (form.ShowDialog() == DialogResult.OK)
            return form.ResultConfig;
        return null;
    }
}
