using System.Text.Json;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Reads Epic Games Store manifests from ProgramData to detect installed games.
/// Epic stores .item manifest files in: C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\
/// </summary>
public class EpicGamesDetector : IGameDetector
{
    public string Name => "Epic Games";
    public string PlatformId => "Epic";

    private static readonly string ManifestsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();

        try
        {
            if (!Directory.Exists(ManifestsPath))
                return Task.FromResult<IReadOnlyList<DetectedGame>>(games);

            foreach (var file in Directory.GetFiles(ManifestsPath, "*.item"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                    var installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                    var launchExe = root.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;

                    if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(installLocation))
                        continue;

                    var exePath = !string.IsNullOrWhiteSpace(launchExe)
                        ? Path.Combine(installLocation, launchExe)
                        : null;

                    games.Add(new DetectedGame
                    {
                        Title = displayName,
                        Platform = PlatformId,
                        IsOwned = true,
                        IsInstalled = true,
                        ExePath = exePath,
                        StartDir = installLocation,
                    });
                }
                catch (JsonException)
                {
                    // Corrupt manifest, skip
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log and return what we have
            System.Diagnostics.Debug.WriteLine($"[EpicGamesDetector] Error: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
