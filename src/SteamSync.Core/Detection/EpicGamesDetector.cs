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

    private static readonly HashSet<string> ExcludedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "software", "digitalextras", "plugins", "plugins/engine", "engines", "engines/preview",
        "engines/ue4", "engines/ue5", "engines/unstable", "asset-format", "type", "type/asset",
        "type/format-item", "projects", "projects/completeprojects", "projects/tutorials",
        "developer", "audience", "hidden", "appproxy", "subscription", "bundles"
    };

    private static readonly HashSet<string> ExcludedNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "ue", "poodle", "epic"
    };

    private static readonly string[] ExcludedTitlePatterns = new[]
    {
        @"\b(Soundtrack|Sound Track|Art Book|Artbook|Wallpaper|HD Wallpaper)\b",
        @"\b(Beta|Tech Beta|Public Testing|Playtest)\b",
        @"\b(Promotion|Promo|Discount|Audience|Marker)\b",
        @"\b(Twinmotion|RealityCapture|RealityScan|Unreal Engine)\b",
        @"\b(Expansion Pack|DLC|Addon|Add-on|Outfit|Skin Pack)\b"
    };

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

                        // Filter installed DLCs / plugins / non-games
                        if (root.TryGetProperty("AppCategories", out var acProp) && acProp.ValueKind == JsonValueKind.Array)
                        {
                            var appCategories = acProp.EnumerateArray()
                                .Select(c => c.GetString())
                                .Where(c => !string.IsNullOrEmpty(c))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            if (appCategories.Contains("addons") && !appCategories.Contains("addons/launchable"))
                                continue;

                            if (appCategories.Any(a => a != null && (a == "plugins" || a == "plugins/engine" || a.StartsWith("plugins/"))))
                                continue;
                        }

                        if (root.TryGetProperty("TechnicalType", out var ttProp) && ttProp.GetString()?.Contains("plugins/engine", StringComparison.OrdinalIgnoreCase) == true)
                            continue;

                        if (root.TryGetProperty("CompatibleApps", out var caProp) && caProp.ValueKind == JsonValueKind.Array)
                        {
                            if (caProp.EnumerateArray().Any(a => a.GetString()?.StartsWith("UE_", StringComparison.OrdinalIgnoreCase) == true))
                                continue;
                        }

                        if (IsExcludedTitle(displayName))
                            continue;

                        var exePath = !string.IsNullOrWhiteSpace(launchExe)
                            ? Path.Combine(installLocation, launchExe)
                            : null;

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
                        var ns = item.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : "";

                        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                            continue;

                        if (!string.IsNullOrEmpty(ns) && ExcludedNamespaces.Contains(ns))
                            continue;

                        // Skip if already added as an installed game
                        if (gamesMap.ContainsKey(id))
                            continue;

                        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (item.TryGetProperty("categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cat in cats.EnumerateArray())
                            {
                                if (cat.TryGetProperty("path", out var pathProp))
                                {
                                    var path = pathProp.GetString();
                                    if (!string.IsNullOrEmpty(path)) categories.Add(path);
                                }
                            }
                        }

                        // Must belong to a valid game category
                        bool hasValidCategory = categories.Contains("games") || 
                                                categories.Contains("games/experience") || 
                                                categories.Contains("applications") || 
                                                categories.Contains("application") || 
                                                categories.Contains("freegames");

                        if (!hasValidCategory)
                            continue;

                        // Reject excluded non-game categories
                        if (categories.Any(c => ExcludedCategories.Contains(c) || 
                                                c.StartsWith("engines/", StringComparison.OrdinalIgnoreCase) || 
                                                c.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase) || 
                                                c.StartsWith("projects/", StringComparison.OrdinalIgnoreCase) || 
                                                c.StartsWith("asset-format/", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // Filter unlaunchable addons / DLCs
                        bool isAddon = categories.Contains("addons") || categories.Contains("addons/durable");
                        bool isLaunchableAddon = categories.Contains("addons/launchable");
                        if (isAddon && !isLaunchableAddon)
                            continue;

                        // Check mainGameItem (if it points to another main game item, it's a DLC)
                        if (item.TryGetProperty("mainGameItem", out var mainGameProp) && !isLaunchableAddon)
                        {
                            if (mainGameProp.ValueKind == JsonValueKind.Object && mainGameProp.TryGetProperty("id", out var mgId))
                            {
                                var mgIdStr = mgId.GetString();
                                if (!string.IsNullOrWhiteSpace(mgIdStr))
                                    continue;
                            }
                        }

                        if (IsExcludedTitle(title))
                            continue;

                        string? storeSlug = null;
                        if (item.TryGetProperty("customAttributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                        {
                            if (attrs.TryGetProperty("com.epicgames.app.productSlug", out var attr))
                            {
                                var val = attr.TryGetProperty("value", out var v) ? v.GetString() : null;
                                if (!string.IsNullOrEmpty(val))
                                {
                                    storeSlug = val.Replace("/home", "").Split('/')[0];
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(storeSlug) && !string.IsNullOrWhiteSpace(title))
                        {
                            var sanitized = Utilities.TitleSanitizer.Sanitize(title);
                            storeSlug = System.Text.RegularExpressions.Regex.Replace(sanitized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                        }

                        var launchUri = !string.IsNullOrWhiteSpace(storeSlug)
                            ? $"com.epicgames.launcher://store/p/{storeSlug}"
                            : "com.epicgames.launcher://";

                        gamesMap[id] = new DetectedGame
                        {
                            Title = title,
                            Platform = PlatformId,
                            IsOwned = true,
                            IsInstalled = false,
                            ExePath = null,
                            StartDir = null,
                            LaunchArguments = launchUri
                        };
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

    private static bool IsExcludedTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return true;
        foreach (var pattern in ExcludedTitlePatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(title, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves the installed Epic Games launcher executable path,
    /// checking protocol handler registry, standard installation locations, and explorer.exe fallback.
    /// </summary>
    public static string GetEpicLauncherPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"com.epicgames.launcher\shell\open\command");
            if (key != null)
            {
                var command = key.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(command))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(command, "\"([^\"]+)\"");
                    if (match.Success && File.Exists(match.Groups[1].Value))
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
        }
        catch { }

        var defaultPaths = new[]
        {
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe",
            @"C:\Program Files\Epic Games\Launcher\Engine\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Engine\Binaries\Win64\EpicGamesLauncher.exe",
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
