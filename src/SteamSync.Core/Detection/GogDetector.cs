using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects GOG Galaxy games via Windows registry entries (for installed games)
/// and the GOG Galaxy SQLite database (for all owned games).
/// </summary>
public class GogDetector : IGameDetector
{
    public string Name => "GOG Galaxy";
    public string PlatformId => "GOG";

    private const string RegistryPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private static readonly string GalaxyDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        @"GOG.com\Galaxy\storage\galaxy-2.0.db");

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Registry for installed games
        try
        {
            using var gogKey = Registry.LocalMachine.OpenSubKey(RegistryPath);
            if (gogKey != null)
            {
                foreach (var subKeyName in gogKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var gameKey = gogKey.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var gameName = gameKey.GetValue("gameName") as string;
                        var path = gameKey.GetValue("path") as string;
                        var exe = gameKey.GetValue("exe") as string;

                        // Skip DLCs/Mods which don't have an executable
                        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(exe))
                            continue;

                        string? exePath = null;
                        if (!string.IsNullOrWhiteSpace(exe))
                        {
                            exePath = Path.IsPathRooted(exe) ? exe : Path.Combine(path ?? "", exe);
                        }

                        gamesMap[subKeyName] = new DetectedGame
                        {
                            Title = gameName,
                            Platform = PlatformId,
                            IsOwned = true,
                            IsInstalled = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path),
                            ExePath = exePath,
                            StartDir = path,
                            LaunchArguments = subKeyName,
                        };
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GogDetector] Error reading registry key {subKeyName}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[GogDetector] Error reading registry: {ex.Message}");
        }

        // 2. Scan GOG Galaxy SQLite Database for all owned games
        if (File.Exists(GalaxyDbPath))
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={GalaxyDbPath};Mode=ReadOnly");
                await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                // We join LibraryReleases (owned games) with GamePieces (metadata) where gamePieceTypeId = 835 (Title).
                // We also left join ReleaseProperties to exclude DLCs (isDlc = 1).
                // We only care about gog releases (releaseKey like 'gog_%').
                cmd.CommandText = @"
                    SELECT lr.releaseKey, gp.value 
                    FROM LibraryReleases lr 
                    JOIN GamePieces gp ON lr.releaseKey = gp.releaseKey 
                    LEFT JOIN ReleaseProperties rp ON lr.releaseKey = rp.releaseKey
                    WHERE gp.gamePieceTypeId = 835 
                      AND lr.releaseKey LIKE 'gog_%'
                      AND (rp.isDlc IS NULL OR rp.isDlc = 0)
                      AND (rp.isVisibleInLibrary IS NULL OR rp.isVisibleInLibrary = 1)";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var releaseKey = reader.GetString(0);
                    var jsonValue = reader.GetString(1);

                    // Parse the ID (e.g., 'gog_12345' -> '12345')
                    if (!releaseKey.StartsWith("gog_")) continue;
                    var gameId = releaseKey.Substring(4);

                    // Skip if we already added this as an installed game from the registry
                    if (gamesMap.ContainsKey(gameId)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonValue);
                        if (doc.RootElement.TryGetProperty("title", out var titleProp))
                        {
                            var title = titleProp.GetString();
                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                gamesMap[gameId] = new DetectedGame
                                {
                                    Title = title,
                                    Platform = PlatformId,
                                    IsOwned = true,
                                    IsInstalled = false,
                                    ExePath = null,
                                    StartDir = null,
                                    LaunchArguments = gameId,
                                };
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GogDetector] Error parsing game metadata for {releaseKey}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[GogDetector] Error reading Galaxy DB: {ex.Message}");
            }
        }

        return gamesMap.Values.ToList();
    }

    /// <summary>
    /// Resolves the installed GOG Galaxy launcher executable path,
    /// checking registry and standard installation locations with explorer.exe fallback.
    /// </summary>
    public static string GetGogLauncherPath()
    {
        try
        {
            var registryPaths = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths"),
                (Registry.LocalMachine, @"SOFTWARE\GOG.com\GalaxyClient\paths"),
                (Registry.CurrentUser, @"Software\GOG.com\GalaxyClient\paths"),
            };

            foreach (var (rootKey, regPath) in registryPaths)
            {
                using var key = rootKey.OpenSubKey(regPath);
                if (key != null)
                {
                    var clientDir = key.GetValue("client") as string;
                    if (!string.IsNullOrWhiteSpace(clientDir) && Directory.Exists(clientDir))
                    {
                        var galaxyExe = Path.Combine(clientDir, "GalaxyClient.exe");
                        if (File.Exists(galaxyExe)) return galaxyExe;
                    }
                }
            }
        }
        catch { }

        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "GalaxyClient.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "GalaxyClient.exe"),
        };

        foreach (var path in defaultPaths)
        {
            if (File.Exists(path)) return path;
        }

        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        if (File.Exists(explorerPath)) return explorerPath;

        return "explorer.exe";
    }
}
