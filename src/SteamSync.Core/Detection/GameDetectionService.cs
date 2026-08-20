using SteamSync.Core.Logging;
using SteamSync.Core.Models;
using SteamSync.Core.Steam;

namespace SteamSync.Core.Detection;

/// <summary>
/// Orchestrates all game detectors and merges results into a unified list.
/// Handles deduplication across sources and assigns Steam AppIDs.
/// </summary>
public class GameDetectionService
{
    private readonly List<IGameDetector> _detectors = new();
    private readonly SyncLogger _logger;

    /// <summary>Whether to include uninstalled games in detection results.</summary>
    public bool IncludeUninstalledGames { get; set; } = true;

    public GameDetectionService(SyncLogger? logger = null)
    {
        _logger = logger ?? new SyncLogger();
    }

    /// <summary>
    /// Registers a detector to be used during game scanning.
    /// </summary>
    public void RegisterDetector(IGameDetector detector)
    {
        _detectors.Add(detector);
    }

    /// <summary>
    /// Configures the default set of detectors based on application settings.
    /// </summary>
    public void ConfigureDefaults(AppSettings settings)
    {
        _detectors.Clear();
        IncludeUninstalledGames = settings.IncludeUninstalledGames;
        _logger.Log("Debug", $"ConfigureDefaults called. DetectGog is: {settings.DetectGog}, IncludeUninstalled: {IncludeUninstalledGames}");

        if (settings.DetectEpic)
            _detectors.Add(new EpicGamesDetector(settings.IncludeUninstalledGames));

        if (settings.DetectGog)
            _detectors.Add(new GogDetector(settings.IncludeUninstalledGames));

        if (settings.DetectUbisoft)
            _detectors.Add(new UbisoftDetector(settings.IncludeUninstalledGames));

        if (settings.DetectEa)
            _detectors.Add(new EaAppDetector(settings.IncludeUninstalledGames));

        if (settings.DetectBattleNet)
            _detectors.Add(new BattleNetDetector(settings.IncludeUninstalledGames));

        if (settings.DetectRockstar)
            _detectors.Add(new RockstarDetector(settings.IncludeUninstalledGames));

        if (settings.DetectXbox)
            _detectors.Add(new XboxDetector());

        if (settings.CustomScanDirectories.Count > 0)
            _detectors.Add(new CustomFolderScanner(settings.CustomScanDirectories, _logger));

        if (settings.UsePlayniteWorker && !string.IsNullOrWhiteSpace(settings.PlayniteWorkerPath))
        {
            _detectors.Add(new PlayniteWorkerClient(
                settings.PlayniteWorkerPath,
                "all",
                settings.PlayniteWorkerTimeoutSeconds,
                _logger));
        }

        _logger.Log("Detection", $"Configured {_detectors.Count} detector(s): {string.Join(", ", _detectors.Select(d => d.Name))}");
    }

    /// <summary>
    /// Configures detectors for Cloud/Playnite mode.
    /// Uses the out-of-process PlayniteWorker to authenticate and scrape platform APIs.
    /// Custom folder scanner is still included if configured.
    /// </summary>
    public void ConfigurePlaynite(AppSettings settings)
    {
        _detectors.Clear();
        IncludeUninstalledGames = settings.IncludeUninstalledGames;

        _detectors.Add(new PlayniteWorkerClient(
            settings.PlayniteWorkerPath, "all", settings.PlayniteWorkerTimeoutSeconds, _logger));

        // Still add custom folder scanner since Playnite doesn't cover DRM-free folders
        if (settings.CustomScanDirectories.Count > 0)
            _detectors.Add(new CustomFolderScanner(settings.CustomScanDirectories, _logger));

        _logger.Log("Detection", $"Configured Playnite Cloud mode with {_detectors.Count} detector(s): {string.Join(", ", _detectors.Select(d => d.Name))}");
    }

    /// <summary>
    /// Runs all registered detectors and returns a deduplicated, merged list of games.
    /// Reports progress via the optional callback.
    /// </summary>
    public async Task<List<DetectedGame>> DetectAllGamesAsync(
        IProgress<(string detectorName, int gamesFound)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allGames = new List<DetectedGame>();
        _logger.Log("Detection", $"Starting detection with {_detectors.Count} detector(s)...");

        foreach (var detector in _detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.Log("Detection", $"Running detector: {detector.Name}...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var games = await detector.DetectGamesAsync(cancellationToken);
                sw.Stop();

                allGames.AddRange(games);
                _logger.Log("Detection", $"{detector.Name} found {games.Count} game(s) in {sw.ElapsedMilliseconds}ms");
                progress?.Report((detector.Name, games.Count));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Detection", $"{detector.Name} failed", ex);
                progress?.Report((detector.Name, 0));
                
                // If the core Playnite Worker fails, bubble it up so the UI can show the Fallback Dialog
                if (detector is PlayniteWorkerClient)
                {
                    throw new Exception($"Cloud Detection Failed: {ex.Message}", ex);
                }
            }
        }

        if (!IncludeUninstalledGames)
        {
            allGames = allGames.Where(g => g.IsInstalled).ToList();
        }

        // Deduplicate by title (case-insensitive)
        var deduplicatedGroups = allGames
            .GroupBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var deduplicated = new List<DetectedGame>();

        // Load existing Steam shortcuts to match AppIDs or VR status if already configured
        var existingShortcutsByTitle = new Dictionary<string, SteamShortcut>(StringComparer.OrdinalIgnoreCase);
        var existingShortcutsByExe = new Dictionary<string, SteamShortcut>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var userIds = SteamPathResolver.GetUserIds();
            foreach (var userId in userIds)
            {
                var vdfPath = SteamPathResolver.GetShortcutsVdfPath(userId);
                if (vdfPath != null && File.Exists(vdfPath))
                {
                    var shortcuts = ShortcutsVdfParser.Parse(vdfPath);
                    foreach (var sc in shortcuts)
                    {
                        if (!string.IsNullOrWhiteSpace(sc.AppName))
                            existingShortcutsByTitle[sc.AppName] = sc;
                        if (!string.IsNullOrWhiteSpace(sc.Exe))
                            existingShortcutsByExe[sc.Exe.Trim('"')] = sc;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log("Detection", $"Could not read existing Steam shortcuts: {ex.Message}");
        }

        int processedCount = 0;
        foreach (var group in deduplicatedGroups)
        {
            processedCount++;
            
            // Prefer the version with the most information
            var best = group
                .OrderByDescending(g => g.IsInstalled ? 1 : 0)
                .ThenByDescending(g => g.ExePath != null ? 1 : 0)
                .First();

            // Merge IsOwned across all sources
            best.IsOwned = group.Any(g => g.IsOwned);
            best.IsInstalled = group.Any(g => g.IsInstalled);

            // Calculate SteamAppId
            if (!string.IsNullOrWhiteSpace(best.ExePath))
            {
                best.SteamAppId = Steam.AppIdGenerator.GenerateShortcutAppId(best.ExePath, best.Title);
            }
            else if (existingShortcutsByTitle.TryGetValue(best.Title, out var scByTitle))
            {
                best.SteamAppId = scByTitle.AppId;
            }

            // Match with existing shortcut if present
            if (existingShortcutsByTitle.TryGetValue(best.Title, out var match) ||
                (!string.IsNullOrWhiteSpace(best.ExePath) && existingShortcutsByExe.TryGetValue(best.ExePath, out match)))
            {
                if (match.OpenVr == 1)
                    best.IsVR = true;
            }

            // Detect VR if not already flagged
            if (!best.IsVR)
            {
                best.IsVR = await Utilities.VrDetectionUtility.IsVrGameAsync(best, _logger);
            }

            // Check if artwork is cached on disk in Steam's grid directory
            best.ArtworkCached = Artwork.ArtworkManager.IsArtworkCached(best.SteamAppId);

            deduplicated.Add(best);

            // Report progress every few games or on the last game
            if (processedCount % 5 == 0 || processedCount == deduplicatedGroups.Count)
            {
                progress?.Report(($"Metadata (VR/Artwork) [{processedCount}/{deduplicatedGroups.Count}]", processedCount));
            }
        }

        deduplicated = deduplicated.OrderBy(g => g.Title).ToList();

        _logger.Log("Detection", $"Detection complete: {allGames.Count} raw → {deduplicated.Count} deduplicated games.");
        return deduplicated;
    }
}
