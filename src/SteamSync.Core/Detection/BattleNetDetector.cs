using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using ProtoBuf;
using SteamSync.Core.Models;
using SteamSync.Core.Utilities;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects Battle.net games strictly offline using Windows registry entries,
/// Battle.net Agent product database (product.db), local Battle.net configuration files,
/// SQLite cache (CachedData.db) user licenses, and local client logs/cache fragments.
/// Supports detecting both installed games and uninstalled owned games.
/// </summary>
public class BattleNetDetector : IGameDetector
{
    public string Name => "Battle.net";
    public string PlatformId => "BattleNet";

    // Maps known Battle.net account license IDs to canonical product codes
    private static readonly Dictionary<int, string> LicenseToProductCode = new()
    {
        [29256] = "Pro",
        [5272175] = "Pro",
        [43336] = "ODIN",
        [43337] = "ODIN",
        [43338] = "ODIN",
        [43339] = "ODIN",
        [43356] = "ODIN",
        [1329875278] = "ODIN",
        [52713] = "ZEUS",
        [53036] = "ZEUS",
        [53037] = "ZEUS",
        [53038] = "ZEUS",
        [53039] = "ZEUS",
        [53056] = "ZEUS",
        [1514493267] = "ZEUS",
        [17459] = "D3",
        [4613486] = "Fen",
        [5730135] = "WoW",
        [1465140039] = "WTCG",
        [1214607983] = "Hero",
        [21297] = "S1",
        [21298] = "S2",
        [22323] = "W3",
        [1447645266] = "VIPR",
        [1279351378] = "LAZR",
        [1179603525] = "FORE",
        [1096108883] = "AUKS",
        [1095647827] = "ANBS",
        [5198665] = "OSI",
        [1464615513] = "WLBY",
        [1381257807] = "RTRO",
        [1463898673] = "W1",
        [1462911566] = "W2",
        [5714258] = "W1R",
        [5714514] = "W2R",
        [1095911763] = "ARIS",
        [1396920146] = "SCOR",
        [4280907] = "ARK",
        [1279414849] = "LBRA",
        [1095849281] = "AQUA",
    };

    // Known Battle.net product codes / internal IDs / license IDs and their official display names
    private static readonly Dictionary<string, string> KnownProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        // Blizzard Core Titles
        ["Pro"] = "Overwatch",
        ["pro"] = "Overwatch",
        ["prometheus"] = "Overwatch",
        ["ow"] = "Overwatch",
        ["overwatch"] = "Overwatch",
        ["overwatch2"] = "Overwatch",
        ["29256"] = "Overwatch",
        ["5272175"] = "Overwatch",

        ["D1"] = "Diablo",
        ["diablo"] = "Diablo",
        ["1146246220"] = "Diablo",

        ["D2"] = "Diablo II",
        ["diablo2"] = "Diablo II",

        ["D2X"] = "Diablo II: Lord of Destruction",
        ["diablo2lod"] = "Diablo II: Lord of Destruction",

        ["OSI"] = "Diablo II: Resurrected",
        ["osi"] = "Diablo II: Resurrected",
        ["d2r"] = "Diablo II: Resurrected",
        ["diablo2resurrected"] = "Diablo II: Resurrected",
        ["5198665"] = "Diablo II: Resurrected",

        ["D3"] = "Diablo III",
        ["diablo3"] = "Diablo III",
        ["diabloiii"] = "Diablo III",
        ["d3cn"] = "Diablo III",
        ["17459"] = "Diablo III",

        ["Fen"] = "Diablo IV",
        ["fen"] = "Diablo IV",
        ["fenris"] = "Diablo IV",
        ["diablo4"] = "Diablo IV",
        ["diabloiv"] = "Diablo IV",
        ["4613486"] = "Diablo IV",

        ["ANBS"] = "Diablo Immortal",
        ["anbs"] = "Diablo Immortal",
        ["diabloimmortal"] = "Diablo Immortal",
        ["1095647827"] = "Diablo Immortal",

        ["WTCG"] = "Hearthstone",
        ["wtcg"] = "Hearthstone",
        ["hs_beta"] = "Hearthstone",
        ["hearthstone"] = "Hearthstone",
        ["1465140039"] = "Hearthstone",

        ["Hero"] = "Heroes of the Storm",
        ["hero"] = "Heroes of the Storm",
        ["heroes"] = "Heroes of the Storm",
        ["heroesofthestorm"] = "Heroes of the Storm",
        ["1214607983"] = "Heroes of the Storm",

        ["S1"] = "StarCraft Remastered",
        ["s1"] = "StarCraft Remastered",
        ["scr"] = "StarCraft Remastered",
        ["starcraft"] = "StarCraft Remastered",
        ["starcraftremastered"] = "StarCraft Remastered",
        ["21297"] = "StarCraft Remastered",

        ["S2"] = "StarCraft II",
        ["s2"] = "StarCraft II",
        ["sc2"] = "StarCraft II",
        ["starcraft2"] = "StarCraft II",
        ["starcraftii"] = "StarCraft II",
        ["21298"] = "StarCraft II",

        ["W1"] = "Warcraft: Orcs & Humans",
        ["w1"] = "Warcraft: Orcs & Humans",
        ["warcraftorcsandhumans"] = "Warcraft: Orcs & Humans",
        ["1463898673"] = "Warcraft: Orcs & Humans",

        ["W2"] = "Warcraft II: Battle.net Edition",
        ["w2"] = "Warcraft II: Battle.net Edition",
        ["warcraft2battlenetedition"] = "Warcraft II: Battle.net Edition",
        ["1462911566"] = "Warcraft II: Battle.net Edition",

        ["W1R"] = "Warcraft: Remastered",
        ["w1r"] = "Warcraft: Remastered",
        ["warcraftremastered"] = "Warcraft: Remastered",
        ["5714258"] = "Warcraft: Remastered",

        ["W2R"] = "Warcraft II: Remastered",
        ["w2r"] = "Warcraft II: Remastered",
        ["warcraft2remastered"] = "Warcraft II: Remastered",
        ["5714514"] = "Warcraft II: Remastered",

        ["W3"] = "Warcraft III: Reforged",
        ["w3"] = "Warcraft III: Reforged",
        ["w3r"] = "Warcraft III: Reforged",
        ["wc3"] = "Warcraft III: Reforged",
        ["warcraft3reforged"] = "Warcraft III: Reforged",
        ["22323"] = "Warcraft III: Reforged",

        ["W3C"] = "Warcraft III: Reign of Chaos",
        ["w3c"] = "Warcraft III: Reign of Chaos",
        ["warcraft3reignofchaos"] = "Warcraft III: Reign of Chaos",

        ["W3CX"] = "Warcraft III: The Frozen Throne",
        ["w3cx"] = "Warcraft III: The Frozen Throne",
        ["warcraft3thefrozenthrone"] = "Warcraft III: The Frozen Throne",

        ["WoW"] = "World of Warcraft",
        ["wow"] = "World of Warcraft",
        ["worldofwarcraft"] = "World of Warcraft",
        ["wow_classic"] = "World of Warcraft Classic",
        ["wow_classic_era"] = "World of Warcraft Classic Era",
        ["5730135"] = "World of Warcraft",

        ["GRY"] = "Warcraft Rumble",
        ["gry"] = "Warcraft Rumble",
        ["gryphon"] = "Warcraft Rumble",
        ["warcraftrumble"] = "Warcraft Rumble",
        ["4674137"] = "Warcraft Rumble",

        ["RTRO"] = "Blizzard Arcade Collection",
        ["rtro"] = "Blizzard Arcade Collection",
        ["1381257807"] = "Blizzard Arcade Collection",

        // Call of Duty Franchise
        ["VIPR"] = "Call of Duty: Black Ops 4",
        ["vipr"] = "Call of Duty: Black Ops 4",
        ["viper"] = "Call of Duty: Black Ops 4",
        ["codbo4"] = "Call of Duty: Black Ops 4",
        ["1447645266"] = "Call of Duty: Black Ops 4",

        ["ODIN"] = "Call of Duty: Modern Warfare",
        ["odin"] = "Call of Duty: Modern Warfare",
        ["codmw"] = "Call of Duty: Modern Warfare",
        ["1329875278"] = "Call of Duty: Modern Warfare",

        ["LAZR"] = "Call of Duty: Modern Warfare 2 Campaign Remastered",
        ["lazr"] = "Call of Duty: Modern Warfare 2 Campaign Remastered",
        ["lazarus"] = "Call of Duty: Modern Warfare 2 Campaign Remastered",
        ["codmw2cr"] = "Call of Duty: Modern Warfare 2 Campaign Remastered",
        ["1279351378"] = "Call of Duty: Modern Warfare 2 Campaign Remastered",

        ["ZEUS"] = "Call of Duty: Black Ops Cold War",
        ["zeus"] = "Call of Duty: Black Ops Cold War",
        ["codbo5"] = "Call of Duty: Black Ops Cold War",
        ["codcw"] = "Call of Duty: Black Ops Cold War",
        ["1514493267"] = "Call of Duty: Black Ops Cold War",

        ["FORE"] = "Call of Duty: Vanguard",
        ["fore"] = "Call of Duty: Vanguard",
        ["codvg"] = "Call of Duty: Vanguard",
        ["1179603525"] = "Call of Duty: Vanguard",

        ["AUKS"] = "Call of Duty: Modern Warfare II",
        ["auks"] = "Call of Duty: Modern Warfare II",
        ["codmw2"] = "Call of Duty: Modern Warfare II",
        ["1096108883"] = "Call of Duty: Modern Warfare II",

        ["PNTA"] = "Call of Duty: Modern Warfare III",
        ["pinta"] = "Call of Duty: Modern Warfare III",
        ["codmw3"] = "Call of Duty: Modern Warfare III",

        ["CODBO6"] = "Call of Duty: Black Ops 6",
        ["codbo6"] = "Call of Duty: Black Ops 6",
        ["cerberus"] = "Call of Duty: Black Ops 6",
        ["VIPR_COD"] = "Call of Duty",

        // Partner / Published Titles
        ["WLBY"] = "Crash Bandicoot 4: It's About Time",
        ["wlby"] = "Crash Bandicoot 4: It's About Time",
        ["crash4"] = "Crash Bandicoot 4: It's About Time",
        ["1464615513"] = "Crash Bandicoot 4: It's About Time",

        ["ARIS"] = "Doom: The Dark Ages",
        ["aris"] = "Doom: The Dark Ages",
        ["1095911763"] = "Doom: The Dark Ages",

        ["SCOR"] = "Sea of Thieves",
        ["scorpio"] = "Sea of Thieves",
        ["1396920146"] = "Sea of Thieves",

        ["ARK"] = "The Outer Worlds 2",
        ["ark"] = "The Outer Worlds 2",
        ["arkansas"] = "The Outer Worlds 2",
        ["4280907"] = "The Outer Worlds 2",

        ["LBRA"] = "Tony Hawk's Pro Skater 3 + 4",
        ["libra"] = "Tony Hawk's Pro Skater 3 + 4",
        ["1279414849"] = "Tony Hawk's Pro Skater 3 + 4",

        ["AQUA"] = "Avowed",
        ["aqua"] = "Avowed",
        ["1095849281"] = "Avowed",

        ["GEAR"] = "Gears of War: E-Day",
        ["gear"] = "Gears of War: E-Day",
    };

    // Non-game utility components and system tools to ignore
    private static readonly HashSet<string> IgnoredComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent", "battle.net", "bna", "battle_net", "services", "setup", "blizzard uninstaller",
        "blizzard_uninstaller", "blizzard error", "blizzarderror", "launcher", "battle.net launcher",
        "app", "temp", "cache", "browser_stats", "homepage", "login", "default", "spot", "gdt",
        "kelp", "corn", "nina", "btlr"
    };

    private static readonly string[] ProductDbPaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Battle.net", "Agent", "product.db"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Battle.net", "Agent", "data", "product.db"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net", "Agent", "product.db"),
    };

    private static readonly string CachedDataDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net", "CachedData.db");

    private static readonly string[] ConfigSearchDirectories = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net"),
    };

    private static readonly string[] LogSearchDirectories = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net", "Logs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Battle.net", "Agent", "Logs"),
    };

    private static readonly string LocalCacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net", "Cache");

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Registry for installed & classic games
        ScanRegistry(gamesMap, cancellationToken);

        // 2. Scan Battle.net Agent product.db
        await ScanProductDbAsync(gamesMap, cancellationToken);

        // 3. Scan Battle.net configuration files (Battle.net.config & *.config)
        await ScanConfigFilesAsync(gamesMap, cancellationToken);

        // 4. Scan SQLite CachedData.db & Cache fragments for owned licenses
        await ScanUserLicensesAndCacheFragmentsAsync(gamesMap, cancellationToken);

        return gamesMap.Values.Distinct().ToList();
    }

    private void ScanRegistry(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var uninstallPaths = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (rootKey, regPath) in uninstallPaths)
        {
            try
            {
                using var key = rootKey.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IgnoredComponents.Contains(subKeyName))
                        continue;

                    try
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var publisher = gameKey.GetValue("Publisher") as string;
                        var uninstallString = gameKey.GetValue("UninstallString") as string;

                        bool isBlizzard = publisher != null &&
                            (publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) ||
                             publisher.Contains("Activision", StringComparison.OrdinalIgnoreCase));

                        bool isBattleNetUninstall = uninstallString != null &&
                            uninstallString.Contains("Battle.net", StringComparison.OrdinalIgnoreCase);

                        if (!isBlizzard && !isBattleNetUninstall)
                            continue;

                        var displayName = gameKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName) || IgnoredComponents.Contains(displayName))
                            continue;

                        // Skip the Battle.net launcher itself or helper tools
                        if (displayName.Contains("Battle.net", StringComparison.OrdinalIgnoreCase) && !displayName.Contains("Edition", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var installLocation = gameKey.GetValue("InstallLocation") as string;
                        var displayIcon = gameKey.GetValue("DisplayIcon") as string;
                        var iconExe = displayIcon?.Split(',').FirstOrDefault()?.Trim('"');

                        // Extract product code from subKeyName or uninstall string if available (--uid=xxx)
                        var productCode = subKeyName;
                        if (!string.IsNullOrWhiteSpace(uninstallString))
                        {
                            var match = Regex.Match(uninstallString, @"--uid=([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                productCode = match.Groups[1].Value;
                            }
                        }

                        var title = ResolveTitle(productCode, displayName);
                        var isInstalled = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);
                        var exePath = isInstalled
                            ? (File.Exists(iconExe) ? iconExe : FindMainExecutable(installLocation!))
                            : null;

                        AddOrUpdateGame(gamesMap, productCode, title, isInstalled, exePath, isInstalled ? installLocation : null);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading registry subkey {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading registry path {regPath}: {ex.Message}");
            }
        }

        // Check classic Blizzard Entertainment registry entries (e.g. Diablo II, Warcraft III, StarCraft)
        var blizzardPaths = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Blizzard Entertainment"),
            (Registry.LocalMachine, @"SOFTWARE\Blizzard Entertainment"),
            (Registry.CurrentUser, @"Software\Blizzard Entertainment")
        };

        foreach (var (rootKey, regPath) in blizzardPaths)
        {
            try
            {
                using var key = rootKey.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IgnoredComponents.Contains(subKeyName))
                        continue;

                    try
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var installPath = (gameKey.GetValue("InstallPath") as string)
                                          ?? (gameKey.GetValue("Path") as string)
                                          ?? (gameKey.GetValue("Install_Path") as string);

                        var title = ResolveTitle(subKeyName, subKeyName);
                        if (IgnoredComponents.Contains(title))
                            continue;

                        var isInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath);
                        var exePath = isInstalled ? FindMainExecutable(installPath!) : null;

                        AddOrUpdateGame(gamesMap, subKeyName, title, isInstalled, exePath, isInstalled ? installPath : null);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading Blizzard registry key {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading Blizzard registry path {regPath}: {ex.Message}");
            }
        }
    }

    private async Task ScanProductDbAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        string? existingDbPath = ProductDbPaths.FirstOrDefault(File.Exists);
        if (existingDbPath == null)
            return;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(existingDbPath, cancellationToken);
            using var stream = new MemoryStream(bytes);

            var products = Serializer.Deserialize<BNetInstalledProductInfo[]>(stream);
            if (products == null || products.Length == 0)
                return;

            foreach (var p in products)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var code = !string.IsNullOrWhiteSpace(p.InternalId) ? p.InternalId : p.ProductId;
                if (string.IsNullOrWhiteSpace(code) || IgnoredComponents.Contains(code))
                    continue;

                var title = ResolveTitle(code);
                var installDir = p.Data?.Path;
                var isInstalled = !string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir);
                var exePath = isInstalled ? FindMainExecutable(installDir!) : null;

                AddOrUpdateGame(gamesMap, code, title, isInstalled, exePath, isInstalled ? installDir : null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading product.db: {ex.Message}");
        }
    }

    private async Task ScanConfigFilesAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        foreach (var dir in ConfigSearchDirectories)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var configFiles = Directory.GetFiles(dir, "*.config", SearchOption.AllDirectories);
                foreach (var configFile in configFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var json = await File.ReadAllTextAsync(configFile, cancellationToken);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        // Check "Games" dictionary
                        if (root.TryGetProperty("Games", out var gamesProp) && gamesProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var gameElem in gamesProp.EnumerateObject())
                            {
                                var productCode = gameElem.Name;
                                if (IgnoredComponents.Contains(productCode))
                                    continue;

                                var title = ResolveTitle(productCode);
                                string? installPath = null;

                                if (gameElem.Value.ValueKind == JsonValueKind.Object)
                                {
                                    if (gameElem.Value.TryGetProperty("InstallPath", out var ipProp))
                                        installPath = ipProp.GetString();
                                    else if (gameElem.Value.TryGetProperty("Path", out var pProp))
                                        installPath = pProp.GetString();
                                }

                                var isInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath);
                                var exePath = isInstalled ? FindMainExecutable(installPath!) : null;

                                AddOrUpdateGame(gamesMap, productCode, title, isInstalled, exePath, isInstalled ? installPath : null);
                            }
                        }

                        // Check default install directory for detected installed games
                        if (root.TryGetProperty("Client", out var clientProp) &&
                            clientProp.TryGetProperty("Install", out var installProp) &&
                            installProp.TryGetProperty("DefaultInstallPath", out var defaultPathProp))
                        {
                            var defaultPath = defaultPathProp.GetString();
                            if (!string.IsNullOrWhiteSpace(defaultPath) && Directory.Exists(defaultPath))
                            {
                                ScanInstallDirectory(gamesMap, defaultPath, cancellationToken);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed JSON configs
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading config file {configFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error scanning config dir {dir}: {ex.Message}");
            }
        }
    }

    private void ScanInstallDirectory(Dictionary<string, DetectedGame> gamesMap, string rootDirectory, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var subDir in Directory.GetDirectories(rootDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirName = Path.GetFileName(subDir);
                if (string.IsNullOrWhiteSpace(dirName) || IgnoredComponents.Contains(dirName))
                    continue;

                // Check if directory contains Battle.net game markers or an executable
                bool hasBnetMarkers = File.Exists(Path.Combine(subDir, ".build.info")) ||
                                      File.Exists(Path.Combine(subDir, ".flavor.info")) ||
                                      File.Exists(Path.Combine(subDir, ".patch.result"));

                var mainExe = FindMainExecutable(subDir);
                if (hasBnetMarkers || mainExe != null)
                {
                    var title = ResolveTitle(dirName, dirName);
                    if (!IgnoredComponents.Contains(title))
                    {
                        AddOrUpdateGame(gamesMap, dirName, title, isInstalled: true, mainExe, subDir);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error scanning install dir {rootDirectory}: {ex.Message}");
        }
    }

    private async Task ScanUserLicensesAndCacheFragmentsAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var userLicenses = new HashSet<int>();

        // 1. Read user licenses from CachedData.db key_value_store
        if (File.Exists(CachedDataDbPath))
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={CachedDataDbPath};Mode=ReadOnly");
                await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM key_value_store WHERE key='features_cached_data_points'";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var json = reader.GetString(0);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("licenses", out var licsProp) && licsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in licsProp.EnumerateArray())
                        {
                            if (elem.TryGetInt32(out var licId))
                            {
                                userLicenses.Add(licId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading licenses from CachedData.db: {ex.Message}");
            }
        }

        // 2. Also harvest licenses from recent client logs
        foreach (var logDir in LogSearchDirectories)
        {
            if (!Directory.Exists(logDir)) continue;

            try
            {
                var logFiles = Directory.GetFiles(logDir, "*.log", SearchOption.TopDirectoryOnly);
                foreach (var logFile in logFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);
                        string? line;
                        while ((line = await sr.ReadLineAsync(cancellationToken)) != null)
                        {
                            if (line.Contains("accountLevelInfo") || line.Contains("licenses:"))
                            {
                                var matches = Regex.Matches(line, @"id=(\d+)");
                                foreach (Match m in matches)
                                {
                                    if (int.TryParse(m.Groups[1].Value, out var lid))
                                    {
                                        userLicenses.Add(lid);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading licenses from log {logFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error scanning log dir {logDir}: {ex.Message}");
            }
        }

        // 3. Directly map known core license IDs to canonical product codes and games
        foreach (var licId in userLicenses)
        {
            if (LicenseToProductCode.TryGetValue(licId, out var prodCode))
            {
                var title = ResolveTitle(prodCode);
                AddOrUpdateGame(gamesMap, prodCode, title, isInstalled: false, exePath: null, startDir: null);
            }
            else
            {
                var licStr = licId.ToString();
                if (KnownProducts.TryGetValue(licStr, out var title))
                {
                    AddOrUpdateGame(gamesMap, licStr, title, isInstalled: false, exePath: null, startDir: null);
                }
            }
        }

        // 4. Scan Cache fragments to match license IDs to products
        if (Directory.Exists(LocalCacheDirectory))
        {
            try
            {
                var cacheFiles = Directory.GetFiles(LocalCacheDirectory, "*", SearchOption.AllDirectories);
                foreach (var cacheFile in cacheFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fi = new FileInfo(cacheFile);
                        if (fi.Length > 1_000_000) continue;

                        using var fs = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var ms = new MemoryStream();
                        await fs.CopyToAsync(ms, cancellationToken);
                        var bytes = ms.ToArray();
                        var text = System.Text.Encoding.UTF8.GetString(bytes);

                        if (!text.StartsWith("{")) continue;

                        // Check if any user license is contained in this cache fragment
                        bool hasMatchingLicense = false;
                        foreach (var lic in userLicenses)
                        {
                            if (text.Contains($"\"license_id\":{lic}") ||
                                text.Contains($"\"license_id\": {lic}") ||
                                text.Contains($"\"licenses\":[{lic}") ||
                                text.Contains($"\"licenses\": [{lic}") ||
                                text.Contains($"[ {lic}") ||
                                text.Contains($",{lic}") ||
                                text.Contains($", {lic}") ||
                                text.Contains($"{lic},") ||
                                text.Contains($"{lic} ]") ||
                                text.Contains($"{lic}]"))
                            {
                                hasMatchingLicense = true;
                                break;
                            }
                        }

                        if (!hasMatchingLicense) continue;

                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;

                        string? prodId = null;
                        if (root.TryGetProperty("fragment_id", out var fragProp))
                            prodId = fragProp.GetString();

                        if (root.TryGetProperty("products", out var prodsProp) && prodsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var p in prodsProp.EnumerateArray())
                            {
                                if (p.TryGetProperty("id", out var idProp))
                                {
                                    prodId = idProp.GetString();
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(prodId) && !IgnoredComponents.Contains(prodId))
                        {
                            var title = ResolveTitle(prodId);
                            if (!IgnoredComponents.Contains(title))
                            {
                                AddOrUpdateGame(gamesMap, prodId, title, isInstalled: false, exePath: null, startDir: null);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error parsing cache fragment {cacheFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error scanning cache directory: {ex.Message}");
            }
        }
    }

    private static string ResolveTitle(string productCode, string? fallback = null)
    {
        if (KnownProducts.TryGetValue(productCode, out var knownTitle))
            return knownTitle;

        // Try normalized lookup (strip non-alphanumeric)
        var normalizedCode = NormalizeKey(productCode);
        if (KnownProducts.TryGetValue(normalizedCode, out var normTitle))
            return normTitle;

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            var normalizedFallback = NormalizeKey(fallback);
            if (KnownProducts.TryGetValue(normalizedFallback, out var fbTitle))
                return fbTitle;

            return TitleSanitizer.Sanitize(fallback);
        }

        return TitleSanitizer.Sanitize(productCode);
    }

    private static string NormalizeKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private void AddOrUpdateGame(
        Dictionary<string, DetectedGame> gamesMap,
        string productCode,
        string title,
        bool isInstalled,
        string? exePath,
        string? startDir)
    {
        if (string.IsNullOrWhiteSpace(title) || IgnoredComponents.Contains(title))
            return;

        // Use normalized title as the primary deduplication key across different codes/aliases/formats
        var key = NormalizeKey(title);
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (gamesMap.TryGetValue(key, out var existing))
        {
            // If already detected as uninstalled but this source provides install info, upgrade it
            if (!existing.IsInstalled && isInstalled)
            {
                existing.IsInstalled = true;
                existing.ExePath = exePath;
                existing.StartDir = startDir;
                if (!string.IsNullOrWhiteSpace(productCode))
                {
                    existing.LaunchArguments = $"battlenet://launch/{productCode}";
                }
            }
            else if (existing.IsInstalled && string.IsNullOrWhiteSpace(existing.ExePath) && !string.IsNullOrWhiteSpace(exePath))
            {
                existing.ExePath = exePath;
                existing.StartDir = startDir;
            }
        }
        else
        {
            gamesMap[key] = new DetectedGame
            {
                Title = title,
                Platform = PlatformId,
                IsOwned = true,
                IsInstalled = isInstalled,
                ExePath = exePath,
                StartDir = startDir,
                LaunchArguments = $"battlenet://launch/{productCode}",
            };
        }
    }

    /// <summary>
    /// Finds the most likely main game executable in a directory,
    /// filtering out known non-game executables (crash reporters, updaters, installers).
    /// </summary>
    private static string? FindMainExecutable(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return null;

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

    /// <summary>
    /// Resolves the installed Battle.net launcher executable path,
    /// checking registry and standard installation locations with explorer.exe fallback.
    /// </summary>
    public static string GetBattleNetLauncherPath()
    {
        try
        {
            var uninstallPaths = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net"
            };

            foreach (var subKey in uninstallPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                if (key != null)
                {
                    var installLocation = key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                    {
                        var bnetExe = Path.Combine(installLocation, "Battle.net.exe");
                        if (File.Exists(bnetExe)) return bnetExe;
                    }

                    var displayIcon = key.GetValue("DisplayIcon") as string;
                    var iconExe = displayIcon?.Split(',').FirstOrDefault()?.Trim('"');
                    if (!string.IsNullOrWhiteSpace(iconExe) && File.Exists(iconExe))
                        return iconExe;
                }
            }
        }
        catch { }

        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net", "Battle.net.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Battle.net", "Battle.net.exe"),
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

[ProtoContract]
internal class BNetInstalledProductInfo
{
    [ProtoContract]
    public class InstallData
    {
        [ProtoMember(1)]
        public string? Path { get; set; }
    }

    [ProtoMember(1)]
    public string? InternalId { get; set; }

    [ProtoMember(2)]
    public string? ProductId { get; set; }

    [ProtoMember(3)]
    public InstallData? Data { get; set; }
}
