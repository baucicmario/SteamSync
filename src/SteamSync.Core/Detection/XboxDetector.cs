using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using SteamSync.Core.Models;

namespace SteamSync.Core.Detection;

/// <summary>
/// Detects installed Xbox / Windows Store games using PowerShell (Get-AppxPackage).
/// 
/// Note: Due to Xbox app limitations, offline detection of uninstalled owned games is not possible. 
/// The Xbox app does not maintain a local, human-readable database of a user's library.
/// Entitlements for uninstalled games are queried dynamically from Microsoft's servers via internal authenticated APIs, 
/// which cannot be accessed offline or easily replicated without an online OAuth flow.
/// Therefore, this detector is strictly limited to locally installed Appx/MSIXVC packages.
/// </summary>
public class XboxDetector : IGameDetector
{
    public string Name => "Xbox (Installed)";
    public string PlatformId => "Xbox";

    public async Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();

        try
        {
            // Execute PowerShell to get installed Appx packages and their InstallLocation.
            // We only look for Main packages to avoid dependencies.
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-AppxPackage -PackageTypeFilter Main | Select-Object -Property Name, PackageFamilyName, InstallLocation | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null) return games;

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;

            if (string.IsNullOrWhiteSpace(output))
                return games;

            using var doc = JsonDocument.Parse(output);
            
            // Output can be a single object or an array.
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    ProcessPackageElement(element, games);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ProcessPackageElement(doc.RootElement, games);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[XboxDetector] Error retrieving Xbox games: {ex.Message}");
        }

        return games;
    }

    private void ProcessPackageElement(JsonElement element, List<DetectedGame> games)
    {
        var name = element.TryGetProperty("Name", out var n) ? n.GetString() : null;
        var packageFamilyName = element.TryGetProperty("PackageFamilyName", out var pfn) ? pfn.GetString() : null;
        var installLocation = element.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installLocation))
            return;

        var manifestPath = Path.Combine(installLocation, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
            return;

        try
        {
            var xDoc = XDocument.Load(manifestPath);
            var ns = xDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var uapNs = xDoc.Root?.GetNamespaceOfPrefix("uap") ?? "http://schemas.microsoft.com/appx/manifest/uap/windows10";

            // Find properties element
            var properties = xDoc.Root?.Element(ns + "Properties");
            if (properties == null) return;

            var category = properties.Element(ns + "Category")?.Value;
            var displayName = properties.Element(ns + "DisplayName")?.Value;

            // Xbox / MS Store games usually have a category containing "game".
            // However, some games (like Sea of Thieves) omit Category. For those, we check:
            // 1. If any application executable is GameLaunchHelper.exe
            // 2. If it has Xbox Live protocols (ms-xbl-*)
            bool isGame = !string.IsNullOrEmpty(category) && category.Contains("game", StringComparison.OrdinalIgnoreCase);
            string? appId = null;

            var appsElement = xDoc.Root?.Element(ns + "Applications");
            if (appsElement != null)
            {
                var firstApp = appsElement.Elements(ns + "Application").FirstOrDefault();
                appId = firstApp?.Attribute("Id")?.Value ?? "App";

                if (!isGame)
                {
                    foreach (var app in appsElement.Elements(ns + "Application"))
                    {
                        var exec = app.Attribute("Executable")?.Value;
                        if (!string.IsNullOrEmpty(exec) && exec.Contains("GameLaunchHelper", StringComparison.OrdinalIgnoreCase))
                        {
                            isGame = true;
                            break;
                        }

                        var exts = app.Element(ns + "Extensions");
                        if (exts != null)
                        {
                            foreach (var ext in exts.Elements(uapNs + "Extension"))
                            {
                                var protocol = ext.Element(uapNs + "Protocol");
                                var protocolName = protocol?.Attribute("Name")?.Value;
                                if (!string.IsNullOrEmpty(protocolName) && protocolName.StartsWith("ms-xbl-", StringComparison.OrdinalIgnoreCase))
                                {
                                    isGame = true;
                                    break;
                                }
                            }
                        }
                        if (isGame) break;
                    }
                }
            }

            if (isGame)
            {
                // Handle ms-resource string indirection if possible, otherwise use name
                var title = !string.IsNullOrEmpty(displayName) && !displayName.StartsWith("ms-resource:") 
                    ? displayName 
                    : name;

                var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                var exePath = File.Exists(explorerPath) ? explorerPath : "explorer.exe";
                var launchArgs = !string.IsNullOrWhiteSpace(packageFamilyName)
                    ? $"shell:AppsFolder\\{packageFamilyName}!{appId ?? "App"}"
                    : string.Empty;

                games.Add(new DetectedGame
                {
                    Title = title,
                    Platform = PlatformId,
                    IsOwned = true,
                    IsInstalled = true,
                    StartDir = installLocation,
                    ExePath = exePath,
                    LaunchArguments = launchArgs
                });
            }
        }
        catch
        {
            // Ignore XML parsing errors for individual manifests
        }
    }

    /// <summary>
    /// Resolves the explorer.exe path or Xbox app / Store launcher path.
    /// </summary>
    public static string GetXboxLauncherPath()
    {
        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        if (File.Exists(explorerPath)) return explorerPath;

        var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        if (File.Exists(cmdPath)) return cmdPath;

        return "explorer.exe";
    }
}
