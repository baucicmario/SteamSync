namespace SteamSync.Core.Models;

/// <summary>
/// Application settings persisted as JSON in %AppData%\SteamSync\settings.json.
/// </summary>
public class AppSettings
{
    /// <summary>SteamGridDB API key (Bearer token).</summary>
    public string SteamGridDbApiKey { get; set; } = string.Empty;

    /// <summary>Custom directories to scan for standalone/DRM-free games.</summary>
    public List<string> CustomScanDirectories { get; set; } = new();

    /// <summary>Whether to auto-detect Epic Games installations.</summary>
    public bool DetectEpic { get; set; } = true;

    /// <summary>Whether to auto-detect GOG Galaxy installations.</summary>
    public bool DetectGog { get; set; } = true;

    /// <summary>Whether to auto-detect Ubisoft Connect installations.</summary>
    public bool DetectUbisoft { get; set; } = true;

    /// <summary>Whether to auto-detect EA App installations.</summary>
    public bool DetectEa { get; set; } = true;

    /// <summary>Whether to auto-detect Battle.net installations.</summary>
    public bool DetectBattleNet { get; set; } = true;

    /// <summary>Whether to use the Playnite worker for detection (fallback).</summary>
    public bool UsePlayniteWorker { get; set; } = false;

    /// <summary>Path to the PlayniteWorker executable.</summary>
    public string PlayniteWorkerPath { get; set; } = "SteamSync.PlayniteWorker.exe";

    /// <summary>Timeout in seconds for the Playnite worker process.</summary>
    public int PlayniteWorkerTimeoutSeconds { get; set; } = 30;

    /// <summary>Steam installation directory (auto-detected if empty).</summary>
    public string? SteamInstallPath { get; set; }

    /// <summary>Gets the app data directory for SteamSync.</summary>
    public static string GetAppDataDirectory()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamSync");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Gets the full path to the settings file.</summary>
    public static string GetSettingsFilePath()
        => Path.Combine(GetAppDataDirectory(), "settings.json");

    /// <summary>Gets the full path to the SQLite database.</summary>
    public static string GetDatabaseFilePath()
        => Path.Combine(GetAppDataDirectory(), "steamsync.db");
}
