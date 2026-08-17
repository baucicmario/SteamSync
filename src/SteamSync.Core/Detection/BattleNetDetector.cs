using Microsoft.Win32;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects Battle.net games via registry entries and known install paths.
/// Battle.net stores game data under: HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{product}
/// </summary>
public class BattleNetDetector : IGameDetector
{
    public string Name => "Battle.net";
    public string PlatformId => "BattleNet";

    // Known Battle.net product codes and their display names
    private static readonly Dictionary<string, string> KnownProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pro"] = "Overwatch 2",
        ["D3"] = "Diablo III",
        ["Fen"] = "Diablo IV",
        ["WTCG"] = "Hearthstone",
        ["Hero"] = "Heroes of the Storm",
        ["S1"] = "StarCraft Remastered",
        ["S2"] = "StarCraft II",
        ["W3"] = "Warcraft III: Reforged",
        ["WoW"] = "World of Warcraft",
        ["VIPR"] = "Call of Duty",
        ["ODIN"] = "Call of Duty: Modern Warfare",
        ["LAZR"] = "Call of Duty: MW II",
        ["ZEUS"] = "Call of Duty: Black Ops 6",
        ["Anbs"] = "Diablo Immortal",
        ["OSI"] = "Diablo II: Resurrected",
        ["GRY"] = "Warcraft Rumble",
    };

    public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();

        try
        {
            // Check uninstall registry for Battle.net products
            var uninstallPath = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
            using var uninstallKey = Registry.LocalMachine.OpenSubKey(uninstallPath);
            if (uninstallKey == null)
                return Task.FromResult<IReadOnlyList<DetectedGame>>(games);

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var gameKey = uninstallKey.OpenSubKey(subKeyName);
                    if (gameKey == null) continue;

                    var publisher = gameKey.GetValue("Publisher") as string;
                    if (publisher == null ||
                        (!publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) &&
                         !publisher.Contains("Activision", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var displayName = gameKey.GetValue("DisplayName") as string;
                    var installLocation = gameKey.GetValue("InstallLocation") as string;
                    var displayIcon = gameKey.GetValue("DisplayIcon") as string;

                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Skip the Battle.net launcher itself
                    if (displayName.Contains("Battle.net", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var exePath = displayIcon?.Split(',').FirstOrDefault()?.Trim('"');

                    games.Add(new DetectedGame
                    {
                        Title = displayName,
                        Platform = PlatformId,
                        IsOwned = true,
                        IsInstalled = !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation),
                        ExePath = exePath,
                        StartDir = installLocation,
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error reading {subKeyName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[BattleNetDetector] Error: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
