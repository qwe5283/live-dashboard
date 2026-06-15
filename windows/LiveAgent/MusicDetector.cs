using System.Text.RegularExpressions;

namespace LiveAgent;

public static class MusicDetector
{
    private static readonly Dictionary<string, string> MusicProcessMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spotify.exe"] = "Spotify",
        ["qqmusic.exe"] = "QQ音乐",
        ["cloudmusic.exe"] = "网易云音乐",
        ["foobar2000.exe"] = "foobar2000",
        ["itunes.exe"] = "Apple Music",
        ["applemusic.exe"] = "Apple Music",
        ["kugou.exe"] = "酷狗音乐",
        ["kwmusic.exe"] = "酷我音乐",
        ["aimp.exe"] = "AIMP",
        ["musicbee.exe"] = "MusicBee",
        ["vlc.exe"] = "VLC",
        ["potplayer.exe"] = "PotPlayer",
        ["potplayer64.exe"] = "PotPlayer",
        ["potplayermini.exe"] = "PotPlayer",
        ["potplayermini64.exe"] = "PotPlayer",
        ["wmplayer.exe"] = "Windows Media Player",
    };

    private static readonly HashSet<string> SpotifyIdleTitles = new(StringComparer.Ordinal)
    {
        "Spotify", "Spotify Free", "Spotify Premium",
    };

    private static readonly Regex FoobarSuffix = new(@"\s*\[foobar2000[^\]]*\]\s*$", RegexOptions.Compiled);

    public static Dictionary<string, string>? GetMusicInfo()
    {
        var results = new List<(string App, string Song, string Artist)>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;

            int length = NativeMethods.GetWindowTextLengthW(hwnd);
            if (length <= 0)
                return true;

            var buf = new char[length + 1];
            NativeMethods.GetWindowTextW(hwnd, buf, length + 1);
            string winTitle = new string(buf, 0, length).Trim();
            if (string.IsNullOrEmpty(winTitle))
                return true;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            string procName;
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                procName = proc.ProcessName + ".exe";
            }
            catch (Exception)
            {
                return true;
            }

            string procLower = procName.ToLowerInvariant();
            if (!MusicProcessMap.TryGetValue(procLower, out string? appName))
                return true;

            (string Song, string Artist)? parsed = procLower switch
            {
                "spotify.exe" => ParseSpotifyTitle(winTitle),
                "foobar2000.exe" => ParseFoobarTitle(winTitle),
                _ => ParseDashTitle(winTitle),
            };

            if (parsed.HasValue)
                results.Add((appName, parsed.Value.Song, parsed.Value.Artist));

            return true;
        }, IntPtr.Zero);

        if (results.Count == 0)
            return null;

        var (app, title, artist) = results[0];
        var info = new Dictionary<string, string> { ["app"] = app };
        if (!string.IsNullOrEmpty(title))
            info["title"] = title.Length > 256 ? title[..256] : title;
        if (!string.IsNullOrEmpty(artist))
            info["artist"] = artist.Length > 256 ? artist[..256] : artist;
        return info;
    }

    private static (string Song, string Artist)? ParseSpotifyTitle(string title)
    {
        if (SpotifyIdleTitles.Contains(title))
            return null;
        int idx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (idx >= 0)
            return (title[(idx + 3)..].Trim(), title[..idx].Trim());
        return (title, "");
    }

    private static (string Song, string Artist)? ParseDashTitle(string title, string appSuffix = "")
    {
        if (!string.IsNullOrEmpty(appSuffix) && title.TrimEnd() == appSuffix)
            return null;
        int idx = title.IndexOf(" - ", StringComparison.Ordinal);
        if (idx >= 0)
            return (title[(idx + 3)..].Trim(), title[..idx].Trim());
        return (title, "");
    }

    private static (string Song, string Artist)? ParseFoobarTitle(string title)
    {
        string cleaned = FoobarSuffix.Replace(title, "");
        if (string.IsNullOrEmpty(cleaned) || cleaned == title)
        {
            int idx = title.IndexOf(" - ", StringComparison.Ordinal);
            if (idx >= 0)
                return (title[(idx + 3)..].Trim(), title[..idx].Trim());
            return (title, "");
        }
        int idx2 = cleaned.IndexOf(" - ", StringComparison.Ordinal);
        if (idx2 >= 0)
            return (cleaned[(idx2 + 3)..].Trim(), cleaned[..idx2].Trim());
        return (cleaned, "");
    }
}
