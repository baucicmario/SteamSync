namespace SteamSync.Core.Models;

/// <summary>
/// Represents a game detected by any source (launcher, custom folder scanner, or Playnite worker).
/// Central data model used across all modules.
/// </summary>
public class DetectedGame
{
    /// <summary>Database primary key.</summary>
    public int Id { get; set; }

    /// <summary>Display title of the game (cleaned/sanitized).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Source platform: "Epic", "GOG", "Ubisoft", "EA", "BattleNet", "Custom", etc.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Whether the user owns this title on the platform.</summary>
    public bool IsOwned { get; set; }

    /// <summary>Whether the game is currently installed on disk.</summary>
    public bool IsInstalled { get; set; }

    /// <summary>Full path to the game executable.</summary>
    public string? ExePath { get; set; }

    /// <summary>Working directory for launch (defaults to exe parent directory).</summary>
    public string? StartDir { get; set; }

    /// <summary>Command-line arguments to pass when launching.</summary>
    public string? LaunchArguments { get; set; }

    /// <summary>Calculated Non-Steam AppID (CRC32-based, matches BoilR/Steam spec).</summary>
    public uint SteamAppId { get; set; }

    /// <summary>The official Steam Store AppID if known (used as fallback for artwork).</summary>
    public uint? OfficialSteamAppId { get; set; }

    /// <summary>SteamGridDB game ID for artwork lookups.</summary>
    public int? SteamGridDbId { get; set; }

    /// <summary>Last time this game was synced to Steam shortcuts.</summary>
    public DateTime? LastSynced { get; set; }

    /// <summary>Whether artwork has been cached locally.</summary>
    public bool ArtworkCached { get; set; }

    /// <summary>Icon path for display in the UI.</summary>
    public string? IconPath { get; set; }

    /// <summary>Whether this game is selected for import. Transient UI state, not persisted.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>Whether this game is known to be VR compatible or VR only.</summary>
    public bool IsVR { get; set; }

    public override string ToString() => $"[{Platform}] {Title} (Installed={IsInstalled})";
}
