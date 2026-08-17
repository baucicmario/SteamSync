using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Interface for all game detectors. Each implementation scans a specific source
/// (launcher, custom folder, Playnite worker) and returns detected games.
/// </summary>
public interface IGameDetector
{
    /// <summary>Human-readable name of this detector (e.g., "Epic Games", "GOG Galaxy").</summary>
    string Name { get; }

    /// <summary>Platform identifier used in DetectedGame.Platform.</summary>
    string PlatformId { get; }

    /// <summary>
    /// Scans for games and returns all detected titles.
    /// Implementations should handle their own errors gracefully and return
    /// an empty list rather than throwing.
    /// </summary>
    Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default);
}
