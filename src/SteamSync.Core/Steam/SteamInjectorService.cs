using System.Diagnostics;
using SteamSync.Core.Logging;
using SteamSync.Core.Models;

namespace SteamSync.Core.Steam;

/// <summary>
/// Orchestrates the full Steam injection workflow:
/// 1. Read existing shortcuts.vdf
/// 2. Merge new games (preserving user-created shortcuts)
/// 3. Calculate AppIDs for new entries
/// 4. Write updated shortcuts.vdf
/// 5. Handle Steam process lifecycle for Force Sync
/// </summary>
public class SteamInjectorService
{
    private const string SteamSyncTag = "SteamSync";
    private readonly SyncLogger _logger;

    public SteamInjectorService(SyncLogger? logger = null)
    {
        _logger = logger ?? new SyncLogger();
    }

    /// <summary>
    /// Syncs detected games into Steam shortcuts for all user profiles.
    /// Does NOT restart Steam (use ForceSyncAsync for that).
    /// </summary>
    public Task<SyncResult> SyncAsync(
        IReadOnlyList<DetectedGame> games,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var userIds = SteamPathResolver.GetUserIds();

        _logger.Log("Sync", $"Starting sync for {games.Count} game(s) across {userIds.Count} user(s)...");

        if (userIds.Count == 0)
        {
            result.Errors.Add("No Steam user profiles found in userdata directory.");
            _logger.LogError("Sync", "No Steam user profiles found.");
            return Task.FromResult(result);
        }

        foreach (var userId in userIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userResult = SyncForUser(userId, games);
            result.Merge(userResult);
        }

        _logger.Log("Sync", $"Sync complete: Added={result.ShortcutsAdded}, Updated={result.ShortcutsUpdated}, Removed={result.ShortcutsRemoved}");
        return Task.FromResult(result);
    }

    /// <summary>
    /// Force syncs: kills Steam immediately, applies changes, relaunches Steam.
    /// Uses BoilR-style aggressive kill for speed.
    /// </summary>
    public async Task<SyncResult> ForceSyncAsync(
        IReadOnlyList<DetectedGame> games,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var totalSw = Stopwatch.StartNew();

        // Step 1: Force-kill Steam (BoilR-style: no graceful shutdown, direct kill)
        if (SteamProcessManager.IsSteamRunning())
        {
            result.Messages.Add("Force-killing Steam...");
            _logger.Log("ForceSync", "Step 1: Killing Steam processes...");
            var killSw = Stopwatch.StartNew();

            var shutdown = await SteamProcessManager.ForceKillAndWaitAsync(_logger);
            killSw.Stop();

            if (!shutdown)
            {
                result.Errors.Add("Failed to kill Steam. Please close it manually.");
                _logger.LogError("ForceSync", "Failed to kill Steam after timeout.");
                return result;
            }
            _logger.Log("ForceSync", $"Steam killed in {killSw.ElapsedMilliseconds}ms.");
        }
        else
        {
            _logger.Log("ForceSync", "Steam was not running, skipping kill step.");
        }

        // Step 2: Apply changes (no delay — file locks are released immediately after process death)
        _logger.Log("ForceSync", "Step 2: Writing shortcuts...");
        var syncResult = await SyncAsync(games, cancellationToken);
        result.Merge(syncResult);

        // Step 3: Relaunch Steam (direct exe launch, no shell execute)
        _logger.Log("ForceSync", "Step 3: Relaunching Steam...");
        result.Messages.Add("Relaunching Steam...");
        SteamProcessManager.LaunchSteam(_logger);

        totalSw.Stop();
        _logger.Log("ForceSync", $"Force sync completed in {totalSw.ElapsedMilliseconds}ms total.");
        return result;
    }

    /// <summary>
    /// Syncs shortcuts for a specific Steam user ID.
    /// </summary>
    private SyncResult SyncForUser(string userId, IReadOnlyList<DetectedGame> games)
    {
        var result = new SyncResult();

        try
        {
            var vdfPath = SteamPathResolver.GetShortcutsVdfPath(userId);
            if (vdfPath == null)
            {
                result.Errors.Add($"Could not resolve shortcuts.vdf path for user {userId}");
                _logger.LogError("Sync", $"shortcuts.vdf path not found for user {userId}");
                return result;
            }

            _logger.Log("Sync", $"User {userId}: Reading {vdfPath}");

            // Read existing shortcuts
            var existingShortcuts = File.Exists(vdfPath)
                ? ShortcutsVdfParser.Parse(vdfPath)
                : new List<SteamShortcut>();

            _logger.Log("Sync", $"User {userId}: {existingShortcuts.Count} existing shortcuts in VDF");

            // Separate user-created shortcuts from SteamSync-managed ones
            var userShortcuts = existingShortcuts.Where(s => !s.IsManagedBySteamSync).ToList();
            var managedShortcuts = existingShortcuts.Where(s => s.IsManagedBySteamSync).ToList();

            _logger.Log("Sync", $"User {userId}: {userShortcuts.Count} user shortcuts, {managedShortcuts.Count} managed shortcuts");

            // Build new managed shortcuts from detected games
            var newManagedShortcuts = new List<SteamShortcut>();
            foreach (var game in games.Where(g => g.IsInstalled && !string.IsNullOrWhiteSpace(g.ExePath)))
            {
                var exe = game.ExePath!;
                var appId = AppIdGenerator.GenerateShortcutAppId(exe, game.Title);

                // Check if this game already exists in managed shortcuts
                var existing = managedShortcuts.FirstOrDefault(s => s.AppId == appId);

                var shortcut = new SteamShortcut
                {
                    AppId = appId,
                    AppName = game.Title,
                    Exe = $"\"{exe}\"",
                    StartDir = $"\"{game.StartDir ?? Path.GetDirectoryName(exe)}\"",
                    Icon = game.IconPath ?? string.Empty,
                    LaunchOptions = game.LaunchArguments ?? string.Empty,
                    AllowDesktopConfig = true,
                    AllowOverlay = true,
                    LastPlayTime = existing?.LastPlayTime ?? 0, // Preserve play time
                    Tags = new List<string> { SteamSyncTag },
                };

                newManagedShortcuts.Add(shortcut);
                _logger.Log("Sync", $"  → {game.Title} (AppID: {appId}, exe: {exe})");
            }

            // Merge: user shortcuts first, then managed shortcuts
            var mergedShortcuts = new List<SteamShortcut>();
            mergedShortcuts.AddRange(userShortcuts);
            mergedShortcuts.AddRange(newManagedShortcuts);

            // Reassign order indices
            for (int i = 0; i < mergedShortcuts.Count; i++)
                mergedShortcuts[i].Order = i;

            // Write the merged shortcuts
            _logger.Log("Sync", $"User {userId}: Writing {mergedShortcuts.Count} shortcuts to {vdfPath}");
            ShortcutsVdfWriter.Write(vdfPath, mergedShortcuts);
            _logger.Log("Sync", $"User {userId}: VDF written successfully ({new FileInfo(vdfPath).Length:N0} bytes)");

            result.ShortcutsAdded += newManagedShortcuts.Count(n =>
                !managedShortcuts.Any(m => m.AppId == n.AppId));
            result.ShortcutsUpdated += newManagedShortcuts.Count(n =>
                managedShortcuts.Any(m => m.AppId == n.AppId));
            result.ShortcutsRemoved += managedShortcuts.Count(m =>
                !newManagedShortcuts.Any(n => n.AppId == m.AppId));

            result.Messages.Add($"User {userId}: {mergedShortcuts.Count} total shortcuts " +
                $"({result.ShortcutsAdded} added, {result.ShortcutsUpdated} updated, {result.ShortcutsRemoved} removed)");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error syncing user {userId}: {ex.Message}");
            _logger.LogError("Sync", $"Error syncing user {userId}", ex);
        }

        return result;
    }
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public class SyncResult
{
    public int ShortcutsAdded { get; set; }
    public int ShortcutsUpdated { get; set; }
    public int ShortcutsRemoved { get; set; }
    public List<string> Messages { get; } = new();
    public List<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;

    public void Merge(SyncResult other)
    {
        ShortcutsAdded += other.ShortcutsAdded;
        ShortcutsUpdated += other.ShortcutsUpdated;
        ShortcutsRemoved += other.ShortcutsRemoved;
        Messages.AddRange(other.Messages);
        Errors.AddRange(other.Errors);
    }
}
