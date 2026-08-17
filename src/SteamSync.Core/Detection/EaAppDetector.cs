using System.Text.Json;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects EA App (formerly Origin) games by reading local content data files.
/// EA App stores install info in: %ProgramData%\EA Desktop\ or %LocalAppData%\Electronic Arts\EA Desktop\
/// </summary>
public class EaAppDetector : IGameDetector
{
    public string Name => "EA App";
    public string PlatformId => "EA";

    public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();

        try
        {
            // EA App stores install manifests in ProgramData
            var installDataPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "EA Desktop", "InstallData"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Electronic Arts", "EA Desktop", "InstallData"),
            };

            foreach (var basePath in installDataPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                foreach (var file in Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = root.TryGetProperty("DiplayName", out var dn2) ? dn2.GetString() : null;
                        }

                        var installPath = root.TryGetProperty("baseInstallPath", out var ip) ? ip.GetString() : null;
                        if (string.IsNullOrWhiteSpace(installPath))
                        {
                            installPath = root.TryGetProperty("installPath", out var ip2) ? ip2.GetString() : null;
                        }

                        if (string.IsNullOrWhiteSpace(displayName))
                            continue;

                        var exePath = !string.IsNullOrWhiteSpace(installPath)
                            ? FindMainExecutable(installPath)
                            : null;

                        games.Add(new DetectedGame
                        {
                            Title = displayName,
                            Platform = PlatformId,
                            IsOwned = true,
                            IsInstalled = !string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath),
                            ExePath = exePath,
                            StartDir = installPath,
                        });
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
            System.Diagnostics.Debug.WriteLine($"[EaAppDetector] Error: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

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
