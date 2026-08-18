using System.Text.Json;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Reads Epic Games Store manifests from ProgramData to detect installed games,
/// and reads the Epic Games launcher catalog cache for owned (but uninstalled) games.
/// Epic stores .item manifest files in: C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\
/// Epic stores catalog cache in: C:\ProgramData\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin
/// </summary>
public class EpicGamesDetector : IGameDetector
{
    public string Name => "Epic Games";
    public string PlatformId => "Epic";

    private static readonly string ManifestsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private static readonly string CatalogCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Catalog", "catcache.bin");

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan for installed games using manifests
        try
        {
            if (Directory.Exists(ManifestsPath))
            {
                foreach (var file in Directory.GetFiles(ManifestsPath, "*.item"))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var json = await File.ReadAllTextAsync(file, cancellationToken);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var catalogItemId = root.TryGetProperty("CatalogItemId", out var cid) ? cid.GetString() : null;
                        var displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                        var installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                        var launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;

                        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(installLocation))
                            continue;

                        var exePath = !string.IsNullOrWhiteSpace(launchExe)
                            ? Path.Combine(installLocation, launchExe)
                            : null;

                        // Fallback to a random Guid if CatalogItemId is missing, to still detect it as installed
                        var gameId = !string.IsNullOrWhiteSpace(catalogItemId) ? catalogItemId : Guid.NewGuid().ToString();

                        gamesMap[gameId] = new DetectedGame
                        {
                            Title = displayName,
                            Platform = PlatformId,
                            IsOwned = true,
                            IsInstalled = true,
                            ExePath = exePath,
                            StartDir = installLocation,
                        };
                    }
                    catch (JsonException)
                    {
                        // Corrupt manifest, skip
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[EpicGamesDetector] Error reading manifests: {ex.Message}");
        }

        // 2. Scan catalog cache for all owned games
        if (File.Exists(CatalogCachePath))
        {
            try
            {
                var base64 = await File.ReadAllTextAsync(CatalogCachePath, cancellationToken);
                var bytes = Convert.FromBase64String(base64);
                var json = System.Text.Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;

                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                            continue;

                        // Skip if already added as an installed game
                        if (gamesMap.ContainsKey(id))
                            continue;

                        bool isGame = false;
                        if (item.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cat in cats.EnumerateArray())
                            {
                                var path = cat.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
                                // Many games are categorized under "games" or "applications"
                                if (path != null && (path.Equals("games", StringComparison.OrdinalIgnoreCase) || path.Equals("applications", StringComparison.OrdinalIgnoreCase)))
                                {
                                    isGame = true;
                                    break;
                                }
                            }
                        }

                        if (isGame)
                        {
                            gamesMap[id] = new DetectedGame
                            {
                                Title = title,
                                Platform = PlatformId,
                                IsOwned = true,
                                IsInstalled = false,
                                ExePath = null,
                                StartDir = null,
                            };
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EpicGamesDetector] Error reading catalog cache: {ex.Message}");
            }
        }

        return gamesMap.Values.ToList();
    }
}
