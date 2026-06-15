using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveAgent;

public class Reporter
{
    private const int MaxBackoff = 60;
    private const int PauseAfterFailures = 5;
    private const int PauseDuration = 300;

    private readonly string _endpoint;
    private readonly string _token;
    private readonly HttpClient _http;
    private int _consecutiveFailures;
    private int _currentBackoff;
    private long _pauseUntil; // Stopwatch ticks

    public double Backoff => _currentBackoff;

    public double PauseRemaining
    {
        get
        {
            double remaining = (_pauseUntil - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
            if (remaining <= 0)
            {
                _pauseUntil = 0;
                return 0;
            }
            return remaining;
        }
    }

    public double RetryDelay => PauseRemaining > 0 ? PauseRemaining : Backoff;

    public Reporter(string serverUrl, string token)
    {
        _endpoint = serverUrl.TrimEnd('/') + "/api/report";
        _token = token;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    public bool Send(string appId, string windowTitle, Dictionary<string, object>? extra = null)
    {
        if (PauseRemaining > 0)
            return false;

        var payload = new Dictionary<string, object>
        {
            ["app_id"] = appId,
            ["window_title"] = windowTitle.Length > 256 ? windowTitle[..256] : windowTitle,
            ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        };
        if (extra != null && extra.Count > 0)
            payload["extra"] = extra;

        try
        {
            string json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _http.PostAsync(_endpoint, content).GetAwaiter().GetResult();

            int code = (int)response.StatusCode;
            if (code is 200 or 201 or 409)
            {
                _consecutiveFailures = 0;
                _currentBackoff = 0;
                _pauseUntil = 0;
                return true;
            }

            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            Logger.Warning($"Server {code}: {body[..Math.Min(200, body.Length)]}");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Request failed: {ex.Message}");
        }

        _consecutiveFailures++;
        _currentBackoff = _currentBackoff == 0 ? 5 : Math.Min(_currentBackoff * 2, MaxBackoff);

        if (_consecutiveFailures >= PauseAfterFailures)
        {
            Logger.Warning($"Failed {_consecutiveFailures} times, pausing {PauseDuration}s");
            _pauseUntil = Stopwatch.GetTimestamp() + (long)(PauseDuration * Stopwatch.Frequency);
            _consecutiveFailures = 0;
            _currentBackoff = 0;
        }
        return false;
    }
}
