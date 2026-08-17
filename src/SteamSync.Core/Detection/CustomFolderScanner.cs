using SteamSync.Core.Logging;
using SteamSync.Core.Models;
using SteamSync.Core.Utilities;

namespace SteamSync.Core.Detection;

/// <summary>
/// Heuristic scanner for standalone/DRM-free/pirated games in custom directories.
/// Applies an expanded blacklist, reads .exe metadata for title extraction,
/// and falls back to regex-sanitized folder names for SteamGridDB matching.
/// </summary>
public class CustomFolderScanner : IGameDetector
{
    public string Name => "Custom Folders";
    public string PlatformId => "Custom";

    private readonly List<string> _scanDirectories;
    private readonly SyncLogger _logger;

    /// <summary>
    /// Executables that should never be treated as game launchers.
    /// Includes installers, uninstallers, crash handlers, engine tools,
    /// redistributable installers, and scene/crack utilities.
    /// </summary>
    private static readonly HashSet<string> BlacklistedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Uninstallers
        "unins000.exe", "unins001.exe", "uninstall.exe", "uninst.exe",

        // DirectX / Redistributable installers
        "dxsetup.exe", "dxwebsetup.exe", "vcredist_x86.exe", "vcredist_x64.exe",
        "vc_redist.x86.exe", "vc_redist.x64.exe", "dotnetfx35.exe",
        "oalinst.exe", "physxloader.exe",

        // Engine crash handlers
        "unitycrashhandler64.exe", "unitycrashhandler32.exe",
        "crashreporter.exe", "crashhandler.exe", "crashpad_handler.exe",
        "ue4prereqsetup_x64.exe", "unrealcefsubprocess.exe",

        // Scene/crack tools
        "steamclient_loader.exe", "steamclientloader.exe",
        "goldberg_emulator.exe", "creaminstaller.exe",
        "crackinit.exe", "codex.exe", "codex64dll.exe",

        // Launchers/updaters that aren't the game
        "launcher.exe", "updater.exe", "update.exe", "patcher.exe",
        "gameoverlayui.exe", "bootstrapper.exe",

        // Common support files
        "report.exe", "bugreport.exe", "sendbug.exe",
        "7z.exe", "7za.exe",
    };

    /// <summary>Patterns that indicate a file is likely NOT the main game executable.</summary>
    private static readonly string[] BlacklistedPatterns =
    {
        "setup", "install", "redist", "dotnet", "vcrun",
        "directx", "support", "tools", "editor", "server",
        "dedicated", "benchmark", "config", "settings",
    };

    public CustomFolderScanner(IEnumerable<string> scanDirectories, SyncLogger? logger = null)
    {
        _scanDirectories = scanDirectories.ToList();
        _logger = logger ?? new SyncLogger();
    }

    public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootDir in _scanDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(rootDir))
            {
                _logger.Log("Scanner", $"Directory does not exist, skipping: {rootDir}");
                continue;
            }

            _logger.Log("Scanner", $"Scanning root directory: {rootDir}");

            try
            {
                // Scan one level deep: each subdirectory is potentially a game
                var subdirs = Directory.GetDirectories(rootDir);
                _logger.Log("Scanner", $"Found {subdirs.Length} subdirectories in {rootDir}");

                foreach (var gameDir in subdirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var game = ScanGameDirectory(gameDir, seenPaths);
                    if (game != null)
                    {
                        games.Add(game);
                        _logger.Log("Scanner", $"✓ Detected: '{game.Title}' → {game.ExePath}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Scanner", $"Error scanning {rootDir}", ex);
            }
        }

        _logger.Log("Scanner", $"Custom folder scan complete. Found {games.Count} games total.");
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    /// <summary>
    /// Scans a single game directory for the best executable candidate
    /// and extracts a title from metadata or folder name.
    /// </summary>
    private DetectedGame? ScanGameDirectory(string gameDir, HashSet<string> seenPaths)
    {
        try
        {
            var folderName = Path.GetFileName(gameDir);

            // Find all .exe files up to 4 levels deep to catch Binaries\Win64 or GameName\bin\win7 patterns
            var exeFiles = GetExecutables(gameDir, maxDepth: 4);
            _logger.Log("Scanner", $"  [{folderName}] Found {exeFiles.Count} .exe files");

            // Filter out blacklisted executables
            var candidates = exeFiles
                .Where(e => !IsBlacklistedExecutable(Path.GetFileName(e)))
                .Where(e => !seenPaths.Contains(e))
                .ToList();

            if (candidates.Count == 0)
            {
                _logger.Log("Scanner", $"  [{folderName}] No candidates after blacklist filter (all {exeFiles.Count} filtered out)");
                return null;
            }

            _logger.Log("Scanner", $"  [{folderName}] {candidates.Count} candidates after blacklist filter");

            // Pick the best candidate: prefer the largest exe (usually the game)
            var bestExe = candidates
                .OrderByDescending(e => new FileInfo(e).Length)
                .First();

            var bestSize = new FileInfo(bestExe).Length;
            _logger.Log("Scanner", $"  [{folderName}] Best candidate: {Path.GetFileName(bestExe)} ({bestSize:N0} bytes)");

            seenPaths.Add(bestExe);

            // Extract title: try metadata first, then folder name
            var title = ExecutableMetadataReader.GetGameTitle(bestExe);
            if (!string.IsNullOrWhiteSpace(title))
            {
                _logger.Log("Scanner", $"  [{folderName}] Title from exe metadata: '{title}'");
            }
            else
            {
                title = TitleSanitizer.Sanitize(folderName ?? Path.GetFileNameWithoutExtension(bestExe));
                _logger.Log("Scanner", $"  [{folderName}] Title from folder name: '{folderName}' → sanitized: '{title}'");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.Log("Scanner", $"  [{folderName}] Empty title after sanitization, skipping");
                return null;
            }

            return new DetectedGame
            {
                Title = title,
                Platform = PlatformId,
                IsOwned = true,
                IsInstalled = true,
                ExePath = bestExe,
                StartDir = Path.GetDirectoryName(bestExe),
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogError("Scanner", $"Skipping {gameDir}", ex);
            return null;
        }
    }

    private List<string> GetExecutables(string directory, int maxDepth, int currentDepth = 0)
    {
        var result = new List<string>();
        if (currentDepth > maxDepth)
            return result;

        try
        {
            result.AddRange(Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly));

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                result.AddRange(GetExecutables(subDir, maxDepth, currentDepth + 1));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return result;
    }

    /// <summary>
    /// Checks if an executable filename is in the blacklist.
    /// Public so other detectors can reuse this filter.
    /// </summary>
    public static bool IsBlacklistedExecutable(string fileName)
    {
        if (BlacklistedFileNames.Contains(fileName))
            return true;

        var lower = fileName.ToLowerInvariant();
        return BlacklistedPatterns.Any(p => lower.Contains(p));
    }
}
