using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamSync.Core.Models;

/// <summary>
/// Represents a game detected by any source (launcher or custom folder scanner).
/// Central data model used across all modules.
/// Inherits from ObservableObject so UI bindings update in real time.
/// </summary>
public partial class DetectedGame : ObservableObject
{
    /// <summary>Database primary key.</summary>
    [ObservableProperty]
    private int _id;

    /// <summary>Display title of the game (cleaned/sanitized).</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>Source platform: "Epic", "GOG", "Ubisoft", "EA", "BattleNet", "Custom", etc.</summary>
    [ObservableProperty]
    private string _platform = string.Empty;

    /// <summary>Whether the user owns this title on the platform.</summary>
    [ObservableProperty]
    private bool _isOwned;

    /// <summary>Whether the game is currently installed on disk.</summary>
    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>Full path to the game executable.</summary>
    [ObservableProperty]
    private string? _exePath;

    /// <summary>Working directory for launch (defaults to exe parent directory).</summary>
    [ObservableProperty]
    private string? _startDir;

    /// <summary>Command-line arguments to pass when launching.</summary>
    [ObservableProperty]
    private string? _launchArguments;

    /// <summary>Calculated Non-Steam AppID (CRC32-based, matches BoilR/Steam spec).</summary>
    [ObservableProperty]
    private uint _steamAppId;

    /// <summary>The official Steam Store AppID if known (used as fallback for artwork).</summary>
    [ObservableProperty]
    private uint? _officialSteamAppId;

    /// <summary>SteamGridDB game ID for artwork lookups.</summary>
    [ObservableProperty]
    private int? _steamGridDbId;

    /// <summary>Last time this game was synced to Steam shortcuts.</summary>
    [ObservableProperty]
    private DateTime? _lastSynced;

    /// <summary>Whether artwork has been cached locally.</summary>
    [ObservableProperty]
    private bool _artworkCached;

    /// <summary>Icon path for display in the UI.</summary>
    [ObservableProperty]
    private string? _iconPath;

    /// <summary>Whether this game is selected for import. Transient UI state, not persisted.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Whether this game is known to be VR compatible or VR only.</summary>
    [ObservableProperty]
    private bool _isVR;

    public override string ToString() => $"[{Platform}] {Title} (Installed={IsInstalled})";
}
