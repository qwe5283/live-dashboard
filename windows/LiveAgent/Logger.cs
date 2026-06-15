namespace LiveAgent;

public static class Logger
{
    private static readonly string BaseDir = AppContext.BaseDirectory;
    private static readonly string LogFilePath = Path.Combine(BaseDir, "agent.log");
    private static StreamWriter? _fileWriter;
    private static DateTime _currentLogDate;
    private static readonly object Lock = new();

    public static void Info(string message) => Log("INFO", message);
    public static void Warning(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);
    public static void Debug(string message) => Log("DEBUG", message);

    public static void SetFileLogging(bool enabled)
    {
        lock (Lock)
        {
            if (enabled && _fileWriter == null)
            {
                OpenLogFile();
            }
            else if (!enabled && _fileWriter != null)
            {
                _fileWriter.Dispose();
                _fileWriter = null;
            }
        }
    }

    public static void Shutdown()
    {
        lock (Lock)
        {
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
    }

    private static void OpenLogFile()
    {
        try
        {
            _currentLogDate = DateTime.Today;
            _fileWriter = new StreamWriter(LogFilePath, append: true, encoding: new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            CleanupOldLogs();
        }
        catch (Exception)
        {
            _fileWriter = null;
        }
    }

    private static void CleanupOldLogs()
    {
        // Keep at most 2 days of logs (rotate by date in filename pattern)
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath) ?? BaseDir;
            var files = Directory.GetFiles(dir, "agent_*.log");
            var cutoff = DateTime.Today.AddDays(-2);
            foreach (var f in files)
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var datePart = name.Length > 6 ? name[6..] : "";
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date)
                    && date < cutoff)
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch { }
    }

    private static void RotateIfNeeded()
    {
        if (_fileWriter == null) return;
        if (DateTime.Today > _currentLogDate)
        {
            _fileWriter.Dispose();
            string archiveName = Path.Combine(
                Path.GetDirectoryName(LogFilePath) ?? BaseDir,
                $"agent_{_currentLogDate:yyyyMMdd}.log");
            try
            {
                if (File.Exists(LogFilePath))
                    File.Move(LogFilePath, archiveName, true);
            }
            catch { }
            OpenLogFile();
        }
    }

    private static void Log(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string line = $"{timestamp} [{level}] {message}";
        Console.WriteLine(line);

        lock (Lock)
        {
            try
            {
                RotateIfNeeded();
                _fileWriter?.WriteLine(line);
            }
            catch (Exception)
            {
                // Silently ignore file write errors
            }
        }
    }
}
