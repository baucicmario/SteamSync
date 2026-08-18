using System.Text.RegularExpressions;
using Microsoft.Win32;
using SteamSync.Core.Models;
using SteamSync.Core.Utilities;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects Rockstar Games Launcher games strictly offline using Windows registry entries (for installed games),
/// local launcher manifests, build collections, profile data, and local launcher logs (for all owned games).
/// </summary>
public class RockstarDetector : IGameDetector
{
    public string Name => "Rockstar Games";
    public string PlatformId => "Rockstar";

    public record RockstarGameInfo(string Title, string? Executable = null, bool IsVR = false);

    // Database of known Rockstar Games title IDs, clean names, and default executables
    public static readonly Dictionary<string, RockstarGameInfo> KnownRockstarGames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gta5"] = new("Grand Theft Auto V", "PlayGTAV.exe"),
        ["gta5_gen9"] = new("Grand Theft Auto V Enhanced", "GTA5_Enhanced_BE.exe"),
        ["rdr"] = new("Red Dead Redemption", "RDR.exe"),
        ["rdr2"] = new("Red Dead Redemption 2", "RDR2.exe"),
        ["lanoire"] = new("L.A. Noire", "LANoire.exe"),
        ["mp3"] = new("Max Payne 3", "MaxPayne3.exe"),
        ["lanoirevr"] = new("L.A. Noire: The VR Case Files", "LANoireVR.exe", IsVR: true),
        ["gtasa"] = new("Grand Theft Auto: San Andreas", "gta_sa.exe"),
        ["gta3"] = new("Grand Theft Auto III", "gta3.exe"),
        ["gtavc"] = new("Grand Theft Auto: Vice City", "gta-vc.exe"),
        ["bully"] = new("Bully: Scholarship Edition", "Bully.exe"),
        ["gta4"] = new("Grand Theft Auto IV", "GTAIV.exe"),
        ["gta3unreal"] = new("Grand Theft Auto III – The Definitive Edition", "Gameface/Binaries/Win64/LibertyCity.exe"),
        ["gtavcunreal"] = new("Grand Theft Auto: Vice City – The Definitive Edition", "Gameface/Binaries/Win64/ViceCity.exe"),
        ["gtasaunreal"] = new("Grand Theft Auto: San Andreas – The Definitive Edition", "Gameface/Binaries/Win64/SanAndreas.exe"),
        ["gtatrilogy"] = new("Grand Theft Auto: The Trilogy – The Definitive Edition", "Launcher.exe"),
        ["rdo"] = new("Red Dead Online", "RDR2.exe"),
        ["manhunt"] = new("Manhunt", "manhunt.exe"),
        ["manhunt2"] = new("Manhunt 2", "Manhunt2.exe"),
        ["mc2"] = new("Midnight Club II", "mc2.exe"),
        ["maxpayne"] = new("Max Payne", "MaxPayne.exe"),
        ["maxpayne2"] = new("Max Payne 2: The Fall of Max Payne", "MaxPayne2.exe"),
    };

    private static readonly HashSet<string> IgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Launcher",
        "Rockstar Games Launcher",
        "Rockstar Games SDK",
        "Rockstar Games Social Club",
        "Rockstar Social Club",
        "Social Club",
        "SocialClub",
        "RGSCRedistributable",
        "Rockstar Games",
        "Rockstar Service",
        "RockstarService",
        "Rockstar Games Services",
        "PlayGTAV",
        "Redistributable",
    };

    private static readonly string LocalAppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rockstar Games", "Launcher");

    private static readonly string ProgramDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Rockstar Games", "Launcher");

    private static readonly string DocumentsLauncherPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Rockstar Games", "Launcher");

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var gamesMap = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan Windows Registry for installed games
        ScanRegistry(gamesMap, cancellationToken);

        // 2. Scan local build collection and manifest XMLs for owned games
        ScanLocalManifests(gamesMap, cancellationToken);

        // 3. Scan local launcher logs for owned / cached titles
        await ScanLocalLogsAsync(gamesMap, cancellationToken);

        // 4. Scan profile and titles cache files
        ScanProfileAndCacheFiles(gamesMap, cancellationToken);

        return gamesMap.Values.Distinct().ToList();
    }

    private void ScanRegistry(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var uninstallPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var regRoot in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var uninstallPath in uninstallPaths)
            {
                try
                {
                    using var uninstallKey = regRoot.OpenSubKey(uninstallPath);
                    if (uninstallKey == null) continue;

                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            using var gameKey = uninstallKey.OpenSubKey(subKeyName);
                            if (gameKey == null) continue;

                            var displayName = gameKey.GetValue("DisplayName") as string;
                            var publisher = gameKey.GetValue("Publisher") as string;
                            var uninstallString = gameKey.GetValue("UninstallString") as string;
                            var installLocation = gameKey.GetValue("InstallLocation") as string;
                            var displayIcon = gameKey.GetValue("DisplayIcon") as string;

                            bool isRockstar = (publisher != null && publisher.Contains("Rockstar", StringComparison.OrdinalIgnoreCase)) ||
                                              (uninstallString != null && Regex.IsMatch(uninstallString, @"(?:Launcher|uninstall)\.exe.+uninstall=(.+)$", RegexOptions.IgnoreCase));

                            if (!isRockstar && displayName != null)
                            {
                                isRockstar = KnownRockstarGames.Values.Any(k => string.Equals(k.Title, displayName, StringComparison.OrdinalIgnoreCase));
                            }

                            if (!isRockstar) continue;
                            if (displayName != null && IgnoredNames.Contains(displayName.Trim())) continue;
                            if (IgnoredNames.Contains(subKeyName.Trim())) continue;

                            string? titleId = null;
                            if (uninstallString != null)
                            {
                                var match = Regex.Match(uninstallString, @"(?:Launcher|uninstall)\.exe.+uninstall=([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                                if (match.Success)
                                {
                                    titleId = match.Groups[1].Value.ToLowerInvariant();
                                }
                            }

                            if (titleId != null && IgnoredNames.Contains(titleId)) continue;

                            if (string.IsNullOrWhiteSpace(titleId) && displayName != null)
                            {
                                var matched = KnownRockstarGames.FirstOrDefault(kvp => string.Equals(kvp.Value.Title, displayName, StringComparison.OrdinalIgnoreCase));
                                if (!string.IsNullOrEmpty(matched.Key))
                                {
                                    titleId = matched.Key;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(titleId) && KnownRockstarGames.TryGetValue(titleId, out var knownInfo))
                            {
                                displayName = knownInfo.Title;
                            }

                            if (string.IsNullOrWhiteSpace(displayName))
                                displayName = subKeyName;

                            if (IgnoredNames.Contains(displayName.Trim())) continue;

                            var title = TitleSanitizer.Sanitize(displayName);
                            if (string.IsNullOrWhiteSpace(title) || IgnoredNames.Contains(title.Trim())) continue;

                            bool isInstalled = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);
                            string? preferredExe = (titleId != null && KnownRockstarGames.TryGetValue(titleId, out var info)) ? info.Executable : null;
                            string? exePath = null;

                            if (isInstalled && !string.IsNullOrWhiteSpace(installLocation))
                            {
                                exePath = FindMainExecutable(installLocation, preferredExe);
                            }

                            if (string.IsNullOrWhiteSpace(exePath) && !string.IsNullOrWhiteSpace(displayIcon))
                            {
                                var iconCandidate = displayIcon.Split(',')[0].Trim('"', ' ');
                                if (iconCandidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconCandidate))
                                {
                                    exePath = iconCandidate;
                                    isInstalled = true;
                                    if (string.IsNullOrWhiteSpace(installLocation))
                                    {
                                        installLocation = Path.GetDirectoryName(iconCandidate);
                                    }
                                }
                            }

                            var key = titleId ?? title;
                            AddOrUpdateGame(gamesMap, key, title, installLocation, exePath, isInstalled, titleId != null && KnownRockstarGames.TryGetValue(titleId, out var vrInfo) && vrInfo.IsVR);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error reading uninstall key {subKeyName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error accessing {uninstallPath}: {ex.Message}");
                }
            }
        }

        // Direct subkey inspection under HKLM\SOFTWARE\Rockstar Games and HKLM\SOFTWARE\WOW6432Node\Rockstar Games
        var rockstarRegPaths = new[]
        {
            @"SOFTWARE\Rockstar Games",
            @"SOFTWARE\WOW6432Node\Rockstar Games",
            @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
            @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
        };

        foreach (var regRoot in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var rockstarRegPath in rockstarRegPaths)
            {
                try
                {
                    using var key = regRoot.OpenSubKey(rockstarRegPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (IgnoredNames.Contains(subKeyName.Trim())) continue;

                        try
                        {
                            using var gameKey = key.OpenSubKey(subKeyName);
                            if (gameKey == null) continue;

                            var installFolder = (gameKey.GetValue("InstallFolder") as string)
                                                ?? (gameKey.GetValue("InstallLocation") as string)
                                                ?? (gameKey.GetValue("InstallDir") as string)
                                                ?? (gameKey.GetValue("Path") as string);

                            if (string.IsNullOrWhiteSpace(installFolder) || !Directory.Exists(installFolder))
                                continue;

                            var title = TitleSanitizer.Sanitize(subKeyName);
                            if (string.IsNullOrWhiteSpace(title) || IgnoredNames.Contains(title.Trim()))
                                continue;

                            var knownMatch = KnownRockstarGames.FirstOrDefault(kvp => string.Equals(kvp.Value.Title, title, StringComparison.OrdinalIgnoreCase));
                            string? preferredExe = knownMatch.Value?.Executable;
                            var exePath = FindMainExecutable(installFolder, preferredExe);

                            var keyId = !string.IsNullOrEmpty(knownMatch.Key) ? knownMatch.Key : title;
                            AddOrUpdateGame(gamesMap, keyId, title, installFolder, exePath, true, knownMatch.Value?.IsVR ?? false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error reading {subKeyName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error reading {rockstarRegPath}: {ex.Message}");
                }
            }
        }
    }

    private void ScanLocalManifests(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(LocalAppDataPath))
            return;

        try
        {
            var xmlFiles = Directory.GetFiles(LocalAppDataPath, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var file in xmlFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(file);
                
                // Examples: buildcollection_gta5_23.xml, manifest_gta5_dev_1165.xml, buildcollection_gta5_gen9_22.xml, manifest_gtasa_dev_1127.xml
                var match = Regex.Match(fileName, @"^(?:buildcollection|manifest)_([a-zA-Z0-9_]+?)(?:_dev)?_\d+\.xml$", RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                var titleId = match.Groups[1].Value.ToLowerInvariant();
                if (titleId.Equals("launcher", StringComparison.OrdinalIgnoreCase) || titleId.Equals("socialclub", StringComparison.OrdinalIgnoreCase))
                    continue;

                AddOwnedGameByTitleId(gamesMap, titleId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error scanning local XML manifests: {ex.Message}");
        }
    }

    private async Task ScanLocalLogsAsync(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        var logFiles = new List<string>();

        if (Directory.Exists(DocumentsLauncherPath))
        {
            logFiles.AddRange(Directory.GetFiles(DocumentsLauncherPath, "launcher*.log", SearchOption.TopDirectoryOnly));
            logFiles.AddRange(Directory.GetFiles(DocumentsLauncherPath, "*.log", SearchOption.TopDirectoryOnly));
        }

        if (Directory.Exists(LocalAppDataPath))
        {
            logFiles.AddRange(Directory.GetFiles(LocalAppDataPath, "*.txt", SearchOption.TopDirectoryOnly));
            logFiles.AddRange(Directory.GetFiles(LocalAppDataPath, "*.log", SearchOption.TopDirectoryOnly));
        }

        if (Directory.Exists(ProgramDataPath))
        {
            logFiles.AddRange(Directory.GetFiles(ProgramDataPath, "*.txt", SearchOption.TopDirectoryOnly));
            logFiles.AddRange(Directory.GetFiles(ProgramDataPath, "*.log", SearchOption.TopDirectoryOnly));
        }

        foreach (var logPath in logFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(logPath)) continue;

            try
            {
                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Match build collections or regular builds: [mergedmanifest] Using (build collection|regular build) for {titleId}
                    var matchBuild = Regex.Match(line, @"\[mergedmanifest\]\s+Using\s+(?:build collection|regular build)\s+for\s+([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                    if (matchBuild.Success)
                    {
                        var titleId = matchBuild.Groups[1].Value.ToLowerInvariant();
                        if (!titleId.Equals("launcher", StringComparison.OrdinalIgnoreCase) && !titleId.Equals("socialclub", StringComparison.OrdinalIgnoreCase))
                        {
                            AddOwnedGameByTitleId(gamesMap, titleId);
                        }
                    }

                    // Match title chunk checks or updater: [titleupdater] Doing chunk checks for {titleId}
                    var matchUpdater = Regex.Match(line, @"\[titleupdater\]\s+(?:Doing chunk checks for|Installing prerequisites for title)\s+([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                    if (matchUpdater.Success)
                    {
                        var titleId = matchUpdater.Groups[1].Value.ToLowerInvariant();
                        if (!titleId.Equals("launcher", StringComparison.OrdinalIgnoreCase))
                        {
                            AddOwnedGameByTitleId(gamesMap, titleId);
                        }
                    }

                    // Match retrieved default title: [titlemanager] Retrieved default title: {titleId}
                    var matchDefaultTitle = Regex.Match(line, @"\[titlemanager\]\s+Retrieved default title:\s*([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                    if (matchDefaultTitle.Success)
                    {
                        var titleId = matchDefaultTitle.Groups[1].Value.ToLowerInvariant();
                        if (!titleId.Equals("launcher", StringComparison.OrdinalIgnoreCase))
                        {
                            AddOwnedGameByTitleId(gamesMap, titleId);
                        }
                    }

                    // Match title version: [title] Current version for title [pv] {TitleName} : {Version}
                    var matchTitleVer = Regex.Match(line, @"\[title\]\s+Current version for title\s+\[\w+\]\s+([^:]+?)\s*:", RegexOptions.IgnoreCase);
                    if (matchTitleVer.Success)
                    {
                        var rawName = matchTitleVer.Groups[1].Value.Trim();
                        if (!rawName.Equals("Rockstar Games Launcher", StringComparison.OrdinalIgnoreCase) &&
                            !rawName.Equals("Rockstar Games Social Club", StringComparison.OrdinalIgnoreCase))
                        {
                            var title = TitleSanitizer.Sanitize(rawName);
                            var matchInfo = KnownRockstarGames.FirstOrDefault(kvp => string.Equals(kvp.Value.Title, title, StringComparison.OrdinalIgnoreCase));
                            var key = !string.IsNullOrEmpty(matchInfo.Key) ? matchInfo.Key : title;
                            AddOrUpdateGame(gamesMap, key, title, null, null, false, matchInfo.Value?.IsVR ?? false);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error reading log {logPath}: {ex.Message}");
            }
        }
    }

    private void ScanProfileAndCacheFiles(Dictionary<string, DetectedGame> gamesMap, CancellationToken cancellationToken)
    {
        // Scan for profiletitles.dat in Documents/Rockstar Games/Launcher/Profiles/*/
        var profileDirs = new List<string>();
        var profilesBase = Path.Combine(DocumentsLauncherPath, "Profiles");
        if (Directory.Exists(profilesBase))
        {
            try
            {
                profileDirs.AddRange(Directory.GetDirectories(profilesBase));
            }
            catch { }
        }

        var datFiles = new List<string>();
        foreach (var pDir in profileDirs)
        {
            var profileTitlesPath = Path.Combine(pDir, "profiletitles.dat");
            if (File.Exists(profileTitlesPath))
                datFiles.Add(profileTitlesPath);
        }

        var machineTitles = Path.Combine(ProgramDataPath, "titles.dat");
        if (File.Exists(machineTitles))
            datFiles.Add(machineTitles);

        var recognisedTitles = Path.Combine(ProgramDataPath, "recognised_titles.dat");
        if (File.Exists(recognisedTitles))
            datFiles.Add(recognisedTitles);

        foreach (var datFile in datFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var bytes = File.ReadAllBytes(datFile);
                var text = System.Text.Encoding.ASCII.GetString(bytes);

                foreach (var kvp in KnownRockstarGames)
                {
                    if (gamesMap.ContainsKey(kvp.Key)) continue;

                    // Check if the title ID or exact game title appears as a string token in the binary dat file
                    if (text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                        text.Contains(kvp.Value.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        AddOwnedGameByTitleId(gamesMap, kvp.Key);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[RockstarDetector] Error reading dat file {datFile}: {ex.Message}");
            }
        }
    }

    private void AddOwnedGameByTitleId(Dictionary<string, DetectedGame> gamesMap, string titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId) || IgnoredNames.Contains(titleId.Trim()))
            return;

        string title;
        bool isVR = false;

        if (KnownRockstarGames.TryGetValue(titleId, out var info))
        {
            title = info.Title;
            isVR = info.IsVR;
        }
        else
        {
            title = TitleSanitizer.Sanitize(titleId);
        }

        if (string.IsNullOrWhiteSpace(title) || IgnoredNames.Contains(title.Trim()))
            return;

        AddOrUpdateGame(gamesMap, titleId, title, null, null, false, isVR);
    }

    private void AddOrUpdateGame(
        Dictionary<string, DetectedGame> gamesMap,
        string key,
        string title,
        string? installDir,
        string? exePath,
        bool isInstalled,
        bool isVR = false)
    {
        if (string.IsNullOrWhiteSpace(title) || IgnoredNames.Contains(title.Trim()) || IgnoredNames.Contains(key.Trim()))
            return;

        // Try finding by primary key or title
        if (gamesMap.TryGetValue(key, out var existing) ||
            gamesMap.Values.FirstOrDefault(g => string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase)) is { } existingByTitle && (existing = existingByTitle) != null)
        {
            if (isInstalled)
            {
                existing.IsInstalled = true;
                if (!string.IsNullOrWhiteSpace(exePath)) existing.ExePath = exePath;
                if (!string.IsNullOrWhiteSpace(installDir)) existing.StartDir = installDir;
            }
            if (isVR) existing.IsVR = true;
            return;
        }

        var detected = new DetectedGame
        {
            Title = title,
            Platform = PlatformId,
            IsOwned = true,
            IsInstalled = isInstalled,
            ExePath = exePath,
            StartDir = installDir,
            IsVR = isVR,
        };

        gamesMap[key] = detected;
    }

    /// <summary>
    /// Finds the game executable in an install directory.
    /// Prefers the known executable name if provided, otherwise scans for non-blacklisted executables.
    /// </summary>
    private static string? FindMainExecutable(string directory, string? preferredExe = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(preferredExe))
            {
                var targetPath = Path.Combine(directory, preferredExe);
                if (File.Exists(targetPath))
                    return targetPath;
            }

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
