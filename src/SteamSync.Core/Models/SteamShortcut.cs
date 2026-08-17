namespace SteamSync.Core.Models;

/// <summary>
/// Represents a single entry in Steam's shortcuts.vdf file.
/// Mirrors the fields from BoilR's steam_shortcuts_util Shortcut struct.
/// </summary>
public class SteamShortcut
{
    /// <summary>Order/index of the shortcut in the VDF file.</summary>
    public int Order { get; set; }

    /// <summary>
    /// Non-Steam AppID. Generated via CRC32 of (exe + appName).
    /// Stored as unsigned 32-bit integer in the VDF.
    /// </summary>
    public uint AppId { get; set; }

    /// <summary>Display name of the shortcut.</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>Full path to the executable (target).</summary>
    public string Exe { get; set; } = string.Empty;

    /// <summary>Working directory (StartDir).</summary>
    public string StartDir { get; set; } = string.Empty;

    /// <summary>Path to the shortcut icon.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Shortcut path (usually empty for non-steam games).</summary>
    public string ShortcutPath { get; set; } = string.Empty;

    /// <summary>Launch options / command-line arguments.</summary>
    public string LaunchOptions { get; set; } = string.Empty;

    /// <summary>Whether this shortcut is hidden in Steam.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Allow desktop configuration.</summary>
    public bool AllowDesktopConfig { get; set; } = true;

    /// <summary>Allow Steam overlay.</summary>
    public bool AllowOverlay { get; set; } = true;

    /// <summary>OpenVR flag.</summary>
    public uint OpenVr { get; set; }

    /// <summary>DevKit ID.</summary>
    public uint DevKit { get; set; }

    /// <summary>DevKit game ID string.</summary>
    public string DevKitGameId { get; set; } = string.Empty;

    /// <summary>DevKit override app ID.</summary>
    public uint DevKitOverrideAppId { get; set; }

    /// <summary>Last play time as Unix timestamp (seconds).</summary>
    public uint LastPlayTime { get; set; }

    /// <summary>Tags associated with this shortcut.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Whether this shortcut was managed/created by SteamSync.
    /// Used to distinguish from user-created shortcuts during merge.
    /// </summary>
    public bool IsManagedBySteamSync => Tags.Contains("SteamSync");
}
