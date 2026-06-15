using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveAgent;

public class AgentConfig
{
    [JsonPropertyName("server_url")]
    public string ServerUrl { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("interval_seconds")]
    public int IntervalSeconds { get; set; } = 5;

    [JsonPropertyName("heartbeat_seconds")]
    public int HeartbeatSeconds { get; set; } = 60;

    [JsonPropertyName("idle_threshold_seconds")]
    public int IdleThresholdSeconds { get; set; } = 300;

    [JsonPropertyName("enable_log")]
    public bool EnableLog { get; set; }

    public AgentConfig Clone()
    {
        return new AgentConfig
        {
            ServerUrl = ServerUrl,
            Token = Token,
            IntervalSeconds = IntervalSeconds,
            HeartbeatSeconds = HeartbeatSeconds,
            IdleThresholdSeconds = IdleThresholdSeconds,
            EnableLog = EnableLog,
        };
    }
}

public static class ConfigManager
{
    private static readonly string BaseDir = AppContext.BaseDirectory;
    public static readonly string ConfigPath = Path.Combine(BaseDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AgentConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AgentConfig();

            string json = File.ReadAllText(ConfigPath, System.Text.Encoding.UTF8);
            var cfg = JsonSerializer.Deserialize<AgentConfig>(json);
            if (cfg == null)
                return new AgentConfig();

            cfg.ServerUrl = (cfg.ServerUrl ?? "").Trim();
            cfg.Token = (cfg.Token ?? "").Trim();

            cfg.IntervalSeconds = ClampInt(cfg.IntervalSeconds, 5, 1, 300);
            cfg.HeartbeatSeconds = ClampInt(cfg.HeartbeatSeconds, 60, 10, 600);
            cfg.IdleThresholdSeconds = ClampInt(cfg.IdleThresholdSeconds, 300, 30, 3600);

            return cfg;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Error($"config.json: {ex.Message}");
            return new AgentConfig();
        }
    }

    public static bool Save(AgentConfig cfg)
    {
        try
        {
            string json = JsonSerializer.Serialize(cfg, JsonOptions);
            string tmpPath = Path.Combine(
                Path.GetDirectoryName(ConfigPath) ?? BaseDir,
                $".config_{Path.GetRandomFileName()}.tmp");

            File.WriteAllText(tmpPath, json, new System.Text.UTF8Encoding(false));
            File.Delete(ConfigPath);
            File.Move(tmpPath, ConfigPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Config save failed: {ex.Message}");
            return false;
        }
    }

    public static string? Validate(AgentConfig cfg)
    {
        string url = (cfg.ServerUrl ?? "").Trim();
        string token = (cfg.Token ?? "").Trim();

        if (string.IsNullOrEmpty(url))
            return "服务器地址不能为空";
        if (string.IsNullOrEmpty(token) || token == "YOUR_TOKEN_HERE")
            return "Token 不能为空";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "服务器地址无效";

        string scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("http" or "https"))
            return "服务器地址必须使用 http:// 或 https://";
        if (string.IsNullOrEmpty(uri.Host))
            return "服务器地址无效";

        return null;
    }

    private static int ClampInt(int value, int defaultValue, int lo, int hi)
    {
        return (value < lo || value > hi) ? defaultValue : value;
    }
}
