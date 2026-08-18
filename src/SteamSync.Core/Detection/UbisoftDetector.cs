using Microsoft.Win32;
using SteamSync.Core.Models;
using YamlDotNet.Serialization;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects Ubisoft Connect (formerly Uplay) games via Windows registry (for installed games)
/// and the local Ubisoft Connect configurations cache for all owned (including uninstalled) games.
/// Cache location: %LocalAppData%\Ubisoft Game Launcher\cache\configuration\configurations
/// </summary>
public class UbisoftDetector : IGameDetector
{
    public string Name => "Ubisoft Connect";
    public string PlatformId => "Ubisoft";

    private const string RegistryPath32 = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs";
    private const string RegistryPath64 = @"SOFTWARE\Ubisoft\Launcher\Installs";

    private static readonly string[] CachePaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ubisoft Game Launcher", "cache", "configuration", "configurations"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ubisoft", "Ubisoft Game Launcher", "cache", "configuration", "configurations"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ubisoft", "Ubisoft Game Launcher", "cache", "configuration", "configurations"),
    };

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Registry for installed games
        ScanRegistry(gamesMap, cancellationToken);

        // 2. Scan offline Ubisoft configurations cache for all owned games
        await ScanConfigurationCacheAsync(gamesMap, cancellationToken);

        return gamesMap.Values.Distinct().ToList();
    }

    private void ScanRegistry(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var registryPaths = new[] { RegistryPath32, RegistryPath64 };

        foreach (var regPath in registryPaths)
        {
            try
            {
                using var installsKey = Registry.LocalMachine.OpenSubKey(regPath);
                if (installsKey == null) continue;

                foreach (var subKeyName in installsKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var gameKey = installsKey.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var installDir = gameKey.GetValue("InstallDir") as string;
                        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
                            continue;

                        var folderName = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                        var title = Utilities.TitleSanitizer.Sanitize(folderName ?? subKeyName);
                        var exePath = FindMainExecutable(installDir);

                        gamesMap[subKeyName] = new DetectedGame
                        {
                            Title = title,
                            Platform = PlatformId,
                            IsOwned = true,
                            IsInstalled = true,
                            ExePath = exePath,
                            StartDir = installDir,
                            LaunchArguments = $"uplay://launch/{subKeyName}/0",
                        };
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error reading registry key {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error reading registry path {regPath}: {ex.Message}");
            }
        }
    }

    private async Task ScanConfigurationCacheAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        string? existingCachePath = CachePaths.FirstOrDefault(File.Exists);
        if (existingCachePath == null)
            return;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(existingCachePath, cancellationToken);
            using var stream = new MemoryStream(bytes);

            var cacheData = ProtoBuf.Serializer.Deserialize<UplayCacheGameCollection>(stream);
            if (cacheData?.Games == null || cacheData.Games.Count == 0)
                return;

            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var dlcsToIgnore = new HashSet<uint>();
            var products = new List<UbisoftProductInfo>();

            foreach (var item in cacheData.Games)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(item.GameInfo)) continue;

                try
                {
                    var productInfo = deserializer.Deserialize<UbisoftProductInfo>(item.GameInfo);
                    if (productInfo?.root == null) continue;

                    var root = productInfo.root;
                    var loc = productInfo.localizations?.@default ?? new Dictionary<string, string>();

                    if (!string.IsNullOrWhiteSpace(root.name) && loc.TryGetValue(root.name, out var locName))
                    {
                        root.name = locName;
                    }

                    productInfo.uplay_id = item.UplayId;
                    productInfo.install_id = item.InstallId;
                    products.Add(productInfo);

                    if (root.addons != null)
                    {
                        foreach (var addon in root.addons)
                        {
                            dlcsToIgnore.Add(addon.id);
                        }
                    }

                    if (root.is_ulc)
                    {
                        dlcsToIgnore.Add(item.UplayId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error parsing game metadata for ID {item.UplayId}: {ex.Message}");
                }
            }

            foreach (var p in products)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (p.root?.third_party_platform != null) continue;
                if (p.root?.is_ulc == true) continue;
                if (dlcsToIgnore.Contains(p.uplay_id)) continue;
                if (p.root?.start_game == null) continue;

                var rawTitle = p.root?.name;
                if (string.IsNullOrWhiteSpace(rawTitle)) continue;

                var title = Utilities.TitleSanitizer.Sanitize(rawTitle);
                if (string.IsNullOrWhiteSpace(title)) continue;

                var uplayIdStr = p.uplay_id.ToString();
                var installIdStr = p.install_id.ToString();

                // Check if this game is already detected as installed
                bool foundInstalled = false;
                if (gamesMap.TryGetValue(uplayIdStr, out var installedByUplayId))
                {
                    installedByUplayId.Title = title;
                    gamesMap[installIdStr] = installedByUplayId;
                    foundInstalled = true;
                }
                else if (gamesMap.TryGetValue(installIdStr, out var installedByInstallId))
                {
                    installedByInstallId.Title = title;
                    gamesMap[uplayIdStr] = installedByInstallId;
                    foundInstalled = true;
                }

                if (!foundInstalled)
                {
                    var primaryId = p.uplay_id != 0 ? uplayIdStr : installIdStr;
                    var detected = new DetectedGame
                    {
                        Title = title,
                        Platform = PlatformId,
                        IsOwned = true,
                        IsInstalled = false,
                        ExePath = null,
                        StartDir = null,
                        LaunchArguments = $"uplay://launch/{primaryId}/0",
                    };

                    gamesMap[uplayIdStr] = detected;
                    gamesMap[installIdStr] = detected;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error reading configurations cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the most likely main game executable in a directory,
    /// filtering out known non-game executables.
    /// </summary>
    private static string? FindMainExecutable(string directory)
    {
        try
        {
            var exes = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
            return exes
                .Where(e => !CustomFolderScanner.IsBlacklistedExecutable(Path.GetFileName(e)))
                .OrderByDescending(e => new FileInfo(e).Length) // Largest exe is usually the game
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

[ProtoBuf.ProtoContract]
internal class UplayCacheGame
{
    [ProtoBuf.ProtoMember(1)]
    public uint UplayId { get; set; }

    [ProtoBuf.ProtoMember(2)]
    public uint InstallId { get; set; }

    [ProtoBuf.ProtoMember(3)]
    public string? GameInfo { get; set; }
}

[ProtoBuf.ProtoContract]
internal class UplayCacheGameCollection
{
    [ProtoBuf.ProtoMember(1)]
    public List<UplayCacheGame>? Games { get; set; }
}

internal class UbisoftProductInfo
{
    public class Localizations
    {
        public Dictionary<string, string>? @default { get; set; }
    }

    public class Addon
    {
        public uint id { get; set; }
    }

    public class Product
    {
        public string? name { get; set; }
        public object? third_party_platform { get; set; }
        public List<Addon>? addons { get; set; }
        public bool is_ulc { get; set; }
        public object? start_game { get; set; }
    }

    public Product? root { get; set; }
    public Localizations? localizations { get; set; }
    public uint uplay_id { get; set; }
    public uint install_id { get; set; }
}
