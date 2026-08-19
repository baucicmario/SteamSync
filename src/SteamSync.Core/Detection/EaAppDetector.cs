using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Win32;
using SteamSync.Core.Models;
using SteamSync.Core.Utilities;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects EA App and Origin games strictly offline using Windows registry entries,
/// EA Desktop and Origin local content manifests, InstallerData XML manifests, and EA license files.
/// Supports detecting both installed games and uninstalled owned games.
/// </summary>
public class EaAppDetector : IGameDetector
{
    public string Name => "EA App";
    public string PlatformId => "EA";

    private static readonly string[] ManifestSearchPaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "InstallData"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "InstallData"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "LocalContent"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Origin", "LocalContent"),
    };

    private static readonly string LicensesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Electronic Arts", "EA Services", "License");

    private static readonly string EaDesktopAppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Electronic Arts", "EA Desktop");

    private static readonly string[] LogSearchPaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "Logs", "EADesktopVerbose.log"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "Logs", "EADesktop.log"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "Logs", "EABackgroundServiceVerbose.log"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "Logs", "EABackgroundService.log"),
    };

    private static readonly string[] CefCachePaths = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "CEF"),
    };

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Registry for EA / Origin games (installed & uninstalled)
        ScanRegistry(gamesMap, cancellationToken);

        // 2. Scan EA Desktop (.json) and Origin (.mfst) manifests
        await ScanManifestsAsync(gamesMap, cancellationToken);

        // 3. Scan installerdata.xml across EA install directories & configured library
        await ScanInstallerDataXmlAsync(gamesMap, cancellationToken);

        // 4. Scan EA license (.dlf) files
        ScanLicenseFiles(gamesMap, cancellationToken);

        // 5. Scan local EA Desktop logs and CEF cache for owned offline games
        await ScanLocalLogsAndCefCacheAsync(gamesMap, cancellationToken);

        return gamesMap.Values.Distinct().ToList();
    }

    private void ScanRegistry(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var originRegPaths = new[]
        {
            @"SOFTWARE\WOW6432Node\Origin Games",
            @"SOFTWARE\Origin Games"
        };

        foreach (var regPath in originRegPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var displayName = gameKey.GetValue("DisplayName") as string;
                        var installDir = (gameKey.GetValue("InstallDir") as string)
                                         ?? (gameKey.GetValue("path") as string);

                        if (string.IsNullOrWhiteSpace(displayName))
                            displayName = subKeyName;

                        var title = TitleSanitizer.Sanitize(displayName);
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        bool isInstalled = !string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir);
                        var exePath = isInstalled ? FindMainExecutable(installDir!) : null;

                        AddOrUpdateGame(gamesMap, subKeyName, title, installDir, exePath, isInstalled);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading registry subkey {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading registry path {regPath}: {ex.Message}");
            }
        }

        var eaRegPaths = new[]
        {
            @"SOFTWARE\WOW6432Node\Electronic Arts",
            @"SOFTWARE\Electronic Arts",
            @"SOFTWARE\WOW6432Node\EA Games",
            @"SOFTWARE\EA Games"
        };

        var ignoredSubKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EA Core", "EA Desktop", "EADM", "EA Services", "Electronic Arts", "EA Games"
        };

        foreach (var regPath in eaRegPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ignoredSubKeys.Contains(subKeyName)) continue;

                    try
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var displayName = (gameKey.GetValue("DisplayName") as string)
                                          ?? (gameKey.GetValue("Title") as string)
                                          ?? subKeyName;

                        var installDir = (gameKey.GetValue("Install Dir") as string)
                                         ?? (gameKey.GetValue("InstallDir") as string)
                                         ?? (gameKey.GetValue("path") as string);

                        var title = TitleSanitizer.Sanitize(displayName);
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        bool isInstalled = !string.IsNullOrWhiteSpace(installDir) && Directory.Exists(installDir);
                        var exePath = isInstalled ? FindMainExecutable(installDir!) : null;

                        AddOrUpdateGame(gamesMap, subKeyName, title, installDir, exePath, isInstalled);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading EA subkey {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading EA registry path {regPath}: {ex.Message}");
            }
        }
    }

    private async Task ScanManifestsAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        foreach (var basePath in ManifestSearchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            // 1. JSON Manifests (EA Desktop)
            foreach (var file in Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = root.TryGetProperty("DiplayName", out var dn2) ? dn2.GetString() : null;
                    }

                    if (string.IsNullOrWhiteSpace(displayName) && root.TryGetProperty("title", out var tProp))
                    {
                        displayName = tProp.GetString();
                    }

                    var id = root.TryGetProperty("softwareId", out var sid) ? sid.GetString()
                        : root.TryGetProperty("offerId", out var oid) ? oid.GetString()
                        : root.TryGetProperty("contentId", out var cid) ? cid.GetString()
                        : Path.GetFileNameWithoutExtension(file);

                    var installPath = root.TryGetProperty("baseInstallPath", out var ip) ? ip.GetString() : null;
                    if (string.IsNullOrWhiteSpace(installPath))
                    {
                        installPath = root.TryGetProperty("installPath", out var ip2) ? ip2.GetString() : null;
                    }

                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    var title = TitleSanitizer.Sanitize(displayName);
                    bool isInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath);
                    var exePath = isInstalled ? FindMainExecutable(installPath!) : null;

                    AddOrUpdateGame(gamesMap, id ?? title, title, installPath, exePath, isInstalled);
                }
                catch (JsonException)
                {
                    // Corrupt manifest, skip
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error parsing manifest {file}: {ex.Message}");
                }
            }

            // 2. MFST Manifests (Origin)
            foreach (var file in Directory.GetFiles(basePath, "*.mfst", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    var (id, title, installPath) = ParseOriginMfst(content, Path.GetFileNameWithoutExtension(file));

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var sanitizedTitle = TitleSanitizer.Sanitize(title);
                    bool isInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath);
                    var exePath = isInstalled ? FindMainExecutable(installPath!) : null;

                    AddOrUpdateGame(gamesMap, id ?? sanitizedTitle, sanitizedTitle, installPath, exePath, isInstalled);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error parsing mfst {file}: {ex.Message}");
                }
            }
        }
    }

    private async Task ScanInstallerDataXmlAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var directoriesToScan = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EA Desktop", "InstallData"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA Games"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Origin Games"),
        };

        // Extract configured EA download directory from user_*.ini if present
        if (Directory.Exists(EaDesktopAppData))
        {
            try
            {
                foreach (var iniFile in Directory.GetFiles(EaDesktopAppData, "user_*.ini"))
                {
                    foreach (var line in File.ReadLines(iniFile))
                    {
                        if (line.StartsWith("user.downloadinplacedir=", StringComparison.OrdinalIgnoreCase))
                        {
                            var customDir = line.Substring("user.downloadinplacedir=".Length).Trim();
                            if (Directory.Exists(customDir) && !directoriesToScan.Contains(customDir, StringComparer.OrdinalIgnoreCase))
                            {
                                directoriesToScan.Add(customDir);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading EA user.ini: {ex.Message}");
            }
        }

        foreach (var dir in directoriesToScan)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                foreach (var xmlFile in Directory.GetFiles(dir, "installerdata.xml", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var xmlContent = await File.ReadAllTextAsync(xmlFile, cancellationToken);
                        var xdoc = XDocument.Parse(xmlContent);
                        var root = xdoc.Root;
                        if (root == null) continue;

                        var contentId = root.Element("contentIDs")?.Element("contentID")?.Value?.Trim();
                        var titles = root.Element("gameTitles")?.Elements("gameTitle").ToList();
                        
                        string? gameTitle = null;
                        if (titles != null && titles.Count > 0)
                        {
                            var enTitle = titles.FirstOrDefault(t => (string?)t.Attribute("locale") == "en_US")?.Value;
                            gameTitle = enTitle ?? titles.First().Value;
                        }

                        if (string.IsNullOrWhiteSpace(gameTitle)) continue;

                        var title = TitleSanitizer.Sanitize(gameTitle);
                        var gameDir = Directory.GetParent(xmlFile)?.Parent?.FullName;
                        bool isInstalled = !string.IsNullOrWhiteSpace(gameDir) && Directory.Exists(gameDir);
                        var exePath = isInstalled ? FindMainExecutable(gameDir!) : null;

                        var id = !string.IsNullOrWhiteSpace(contentId) ? contentId : title;
                        AddOrUpdateGame(gamesMap, id, title, gameDir, exePath, isInstalled);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error parsing installerdata.xml {xmlFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error scanning installer XML in {dir}: {ex.Message}");
            }
        }
    }

    private void ScanLicenseFiles(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(LicensesPath)) return;

        try
        {
            foreach (var dlf in Directory.GetFiles(LicensesPath, "*.dlf"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var contentId = Path.GetFileNameWithoutExtension(dlf);
                if (string.IsNullOrWhiteSpace(contentId)) continue;

                if (gamesMap.TryGetValue(contentId, out var existing))
                {
                    existing.IsOwned = true;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading licenses: {ex.Message}");
        }
    }

    private async Task ScanLocalLogsAndCefCacheAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var offerSlugMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan EA logs
        foreach (var logFile in LogSearchPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(logFile)) continue;

            try
            {
                using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                string? line;
                var regex = new System.Text.RegularExpressions.Regex(@"offerId=\[([^\]]+)\]\s+slug=\[([^\]]+)\]");
                while ((line = await sr.ReadLineAsync(cancellationToken)) != null)
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        var offerId = match.Groups[1].Value.Trim();
                        var slug = match.Groups[2].Value.Trim();
                        offerSlugMap[offerId] = slug;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading log {logFile}: {ex.Message}");
            }
        }

        // 2. Scan CEF Cache for GraphQL queries with launchEnforcements or game titles
        foreach (var cefDir in CefCachePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(cefDir)) continue;

            try
            {
                var cacheFiles = Directory.GetFiles(cefDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".log") && !f.EndsWith(".txt") && new FileInfo(f).Length < 20000000);

                var leRegex = new System.Text.RegularExpressions.Regex("\"offerId\"\\s*:\\s*\"([^\"]+)\"");

                foreach (var file in cacheFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                        var text = System.Text.Encoding.UTF8.GetString(bytes);

                        foreach (System.Text.RegularExpressions.Match m in leRegex.Matches(text))
                        {
                            var offerId = m.Groups[1].Value;
                            if (!offerSlugMap.ContainsKey(offerId))
                            {
                                offerSlugMap[offerId] = offerId;
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Ignore binary cache read errors
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error reading CEF cache: {ex.Message}");
            }
        }

        // 3. Process extracted offer IDs and slugs
        foreach (var (offerId, slugOrId) in offerSlugMap)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsDlc(offerId, slugOrId))
                continue;

            var title = FormatSlugOrIdToTitle(slugOrId);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var sanitizedTitle = TitleSanitizer.Sanitize(title);
            if (string.IsNullOrWhiteSpace(sanitizedTitle)) continue;

            // If game is not already detected from registry/manifests as installed
            AddOrUpdateGame(gamesMap, offerId, sanitizedTitle, null, null, isInstalled: false);
        }
    }

    private static bool IsDlc(string offerId, string slug)
    {
        if (offerId.StartsWith("OFB-MASS:", StringComparison.OrdinalIgnoreCase) ||
            offerId.StartsWith("OFB-DRGN:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dlcKeywords = new[]
        {
            "-pack", "-dlc", "-armor", "-bundle", "-season-pass", "-expansion",
            "-deluxe-content", "-upgrade", "-content", "-kit", "-addon", "-bonus",
            "-multiplayer-expansion", "spoils-of-", "flames-of-", "feastday-",
            "cerberus-", "-shortcut", "-pass", "-items", "-suit", "-weapons",
            "-appearance-", "-mount", "-drop-", "holiday-celebration", "robbery",
            "dragon-s-teeth", "legacy-operations", "bespin", "death-star", "outer-rim",
            "rogue-one-scarif", "jaws-of-hakkon", "the-black-emporium", "the-descent",
            "trespasser", "underground-r-201", "expedition", "frontier-s-edge",
            "imc-rising"
        };

        foreach (var kw in dlcKeywords)
        {
            if (slug.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var specificDlcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "arrival", "genesis", "kasumi-stolen-memory", "lair-of-the-shadow-broker",
            "m-29-incisor-sniper-rifle", "normandy-crash-site", "recon-hood",
            "sentry-interface", "umbra-visor", "zaeed-the-price-of-revenge",
            "the-stone-prisoner", "warden-s-keep", "golems-of-amgarrak", "the-golems-of-amgarrak"
        };

        return specificDlcs.Contains(slug);
    }

    private static string FormatSlugOrIdToTitle(string slug)
    {
        // Custom name overrides for known slugs
        var slugOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "dead-space-classic", "Dead Space" },
            { "simcity-2000", "SimCity 2000" },
            { "command-and-conquer-red-alert-2", "Command & Conquer: Red Alert 2" },
            { "star-wars-battlefront-2", "Star Wars Battlefront II" },
            { "star-wars-jedi-fallen-order", "Star Wars Jedi: Fallen Order" },
            { "star-wars-jedi-survivor", "Star Wars Jedi: Survivor" },
            { "star-wars-squadrons", "Star Wars: Squadrons" },
            { "dragon-age-origins-awakening", "Dragon Age: Origins - Awakening" },
            { "dragon-age-origins", "Dragon Age: Origins" },
            { "dragon-age-inquisition", "Dragon Age: Inquisition" },
            { "plants-vs-zombies", "Plants vs. Zombies" },
            { "zumas-revenge", "Zuma's Revenge" },
            { "syndicate-1993", "Syndicate (1993)" },
            { "ultima-8-pagan", "Ultima VIII: Pagan" },
            { "wing-commander-3-heart-of-the-tiger", "Wing Commander III: Heart of the Tiger" },
            { "steamworld-dig", "SteamWorld Dig" },
            { "syberia-2", "Syberia II" },
            { "need-for-speed-heat", "Need for Speed: Heat" },
            { "medal-of-honor-pacific-assault", "Medal of Honor: Pacific Assault" },
            { "crusader-no-remorse", "Crusader: No Remorse" },
            { "mass-effect-2", "Mass Effect 2" },
            { "mass-effect-legendary-edition", "Mass Effect Legendary Edition" },
            { "the-sims-4", "The Sims 4" },
            { "apex-legends", "Apex Legends" },
            { "battlefield-3", "Battlefield 3" },
            { "bejeweled-3", "Bejeweled 3" },
            { "dungeon-keeper", "Dungeon Keeper" },
            { "peggle", "Peggle" },
            { "theme-hospital", "Theme Hospital" },
            { "titanfall-2", "Titanfall 2" },
            { "skate", "skate." }
        };

        if (slugOverrides.TryGetValue(slug, out var customTitle))
            return customTitle;

        var parts = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var titleParts = parts.Select(p =>
        {
            if (p.Equals("2", StringComparison.OrdinalIgnoreCase)) return "2";
            if (p.Equals("3", StringComparison.OrdinalIgnoreCase)) return "3";
            if (p.Equals("4", StringComparison.OrdinalIgnoreCase)) return "4";
            if (p.Equals("ii", StringComparison.OrdinalIgnoreCase)) return "II";
            if (p.Equals("iii", StringComparison.OrdinalIgnoreCase)) return "III";
            if (p.Equals("iv", StringComparison.OrdinalIgnoreCase)) return "IV";
            if (p.Equals("v", StringComparison.OrdinalIgnoreCase)) return "V";
            if (p.Equals("ea", StringComparison.OrdinalIgnoreCase)) return "EA";
            if (p.Equals("pvz", StringComparison.OrdinalIgnoreCase)) return "PvZ";
            if (p.Equals("dlc", StringComparison.OrdinalIgnoreCase)) return "DLC";
            return char.ToUpper(p[0]) + p.Substring(1);
        });

        return string.Join(" ", titleParts);
    }

    private static (string? id, string? title, string? installPath) ParseOriginMfst(string mfstContent, string fallbackId)
    {
        string? id = fallbackId;
        string? title = null;
        string? installPath = null;

        var content = mfstContent.TrimStart('?');
        var pairs = content.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = Uri.UnescapeDataString(parts[1]);

            if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                id = value;
            else if (key.Equals("title", StringComparison.OrdinalIgnoreCase))
                title = value;
            else if (key.Equals("dipinstallpath", StringComparison.OrdinalIgnoreCase))
                installPath = value;
        }

        return (id, title, installPath);
    }

    private void AddOrUpdateGame(
        Dictionary<string, DetectedGame> gamesMap,
        string id,
        string title,
        string? installDir,
        string? exePath,
        bool isInstalled)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        // Check if existing by ID or Title
        if (gamesMap.TryGetValue(id, out var existing) || gamesMap.TryGetValue(title, out existing))
        {
            existing.Title = title;
            existing.IsOwned = true;
            if (isInstalled)
            {
                existing.IsInstalled = true;
                if (!string.IsNullOrWhiteSpace(exePath)) existing.ExePath = exePath;
                if (!string.IsNullOrWhiteSpace(installDir)) existing.StartDir = installDir;
            }
            var resolvedId = EaContentIdResolver.ResolveContentId(id);
            if (string.IsNullOrWhiteSpace(existing.LaunchArguments))
            {
                existing.LaunchArguments = $"origin2://game/launch/?offerIds={resolvedId}";
            }
            gamesMap[id] = existing;
            gamesMap[title] = existing;
            return;
        }

        var contentId = EaContentIdResolver.ResolveContentId(id);
        var detected = new DetectedGame
        {
            Title = title,
            Platform = PlatformId,
            IsOwned = true,
            IsInstalled = isInstalled,
            ExePath = exePath,
            StartDir = installDir,
            LaunchArguments = $"origin2://game/launch/?offerIds={contentId}",
        };

        gamesMap[id] = detected;
        gamesMap[title] = detected;
    }

    /// <summary>
    /// Finds the most likely main game executable in a directory,
    /// filtering out known non-game executables.
    /// </summary>
    private static string? FindMainExecutable(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            var exes = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
            return exes
                .Where(e => !CustomFolderScanner.IsBlacklistedExecutable(Path.GetFileName(e)))
                .OrderByDescending(e => new FileInfo(e).Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

