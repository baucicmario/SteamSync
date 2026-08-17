using Microsoft.Win32;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects Ubisoft Connect (formerly Uplay) games via Windows registry.
/// Ubisoft stores install info under: HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\{gameId}
/// </summary>
public class UbisoftDetector : IGameDetector
{
    public string Name => "Ubisoft Connect";
    public string PlatformId => "Ubisoft";

    private const string RegistryPath = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs";

    public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();

        try
        {
            using var installsKey = Registry.LocalMachine.OpenSubKey(RegistryPath);
            if (installsKey == null)
                return Task.FromResult<IReadOnlyList<DetectedGame>>(games);

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

                    // Ubisoft registry doesn't store game names directly.
                    // Use the folder name as a reasonable fallback.
                    var folderName = Path.GetFileName(installDir.TrimEnd('\\', '/'));
                    var title = Utilities.TitleSanitizer.Sanitize(folderName ?? subKeyName);

                    // Try to find main executable in the install directory
                    var exePath = FindMainExecutable(installDir);

                    games.Add(new DetectedGame
                    {
                        Title = title,
                        Platform = PlatformId,
                        IsOwned = true,
                        IsInstalled = true,
                        ExePath = exePath,
                        StartDir = installDir,
                        // Ubisoft games launch via uplay:// protocol
                        LaunchArguments = $"uplay://launch/{subKeyName}/0",
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error reading {subKeyName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[UbisoftDetector] Error: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
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
