namespace LiveAgent;

public static class SystemMonitor
{
    public static double GetIdleSeconds()
    {
        var lii = new NativeMethods.LASTINPUTINFO();
        lii.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>();
        if (!NativeMethods.GetLastInputInfo(ref lii))
            return 0.0;
        uint now = NativeMethods.GetTickCount();
        uint elapsedMs = (now - lii.dwTime) & 0xFFFFFFFF;
        return elapsedMs / 1000.0;
    }

    public static bool IsAudioPlaying()
    {
        try
        {
            var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(
                NAudio.CoreAudioApi.DataFlow.Render,
                NAudio.CoreAudioApi.DeviceState.Active);

            foreach (var device in devices)
            {
                var mgr = device.AudioSessionManager;
                if (mgr == null) continue;
                var sessions = mgr.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i].State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive)
                        return true;
                }
            }
        }
        catch
        {
            // COM not available or any error → treat as no audio
        }
        return false;
    }

    public static bool IsForegroundFullscreen()
    {
        try
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;
            if (!NativeMethods.GetWindowRect(hwnd, out var rect))
                return false;
            int w = NativeMethods.GetSystemMetrics(0); // SM_CXSCREEN
            int h = NativeMethods.GetSystemMetrics(1); // SM_CYSCREEN
            return rect.Left <= 0 && rect.Top <= 0 && rect.Right >= w && rect.Bottom >= h;
        }
        catch
        {
            return false;
        }
    }

    public static (string ProcessName, string Title)? GetForegroundInfo()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        int length = NativeMethods.GetWindowTextLengthW(hwnd);
        if (length <= 0)
            return null;

        var buf = new char[length + 1];
        NativeMethods.GetWindowTextW(hwnd, buf, length + 1);
        string title = new string(buf, 0, length).Trim();
        if (string.IsNullOrEmpty(title))
            return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string procName;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            procName = proc.ProcessName + ".exe";
        }
        catch (Exception)
        {
            procName = "unknown";
        }
        return (procName, title);
    }

    public static Dictionary<string, object> GetBatteryExtra()
    {
        try
        {
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            if (ps.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
                return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                ["battery_percent"] = (int)(ps.BatteryLifePercent * 100),
                ["battery_charging"] = ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online,
            };
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }
}
