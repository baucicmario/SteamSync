using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using SteamSync.Core.Logging;
using SteamSync.Core.Models;

namespace SteamSync.Core.Utilities;

public static class VrDetectionUtility
{
    private static readonly Regex VrTitleRegex = new(@"\b(VR|Virtual Reality)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] VrLaunchArgs = { "-vr", "-vrmode", "vr" };
    private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    private static readonly string CacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteamSync", "vr_cache.json");
    private static Dictionary<string, bool>? _vrCache = null;
    private static readonly object _cacheLock = new object();

    private static void LoadCache()
    {
        lock (_cacheLock)
        {
            if (_vrCache != null) return;
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    var json = File.ReadAllText(CacheFilePath);
                    _vrCache = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _vrCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                _vrCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void SaveCache()
    {
        lock (_cacheLock)
        {
            try
            {
                if (_vrCache == null) return;
                var dir = Path.GetDirectoryName(CacheFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_vrCache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CacheFilePath, json);
            }
            catch { /* Ignore write errors */ }
        }
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        return title.Replace("™", "").Replace("®", "").Replace("©", "").Trim();
    }

    public static async Task<bool> IsVrGameAsync(DetectedGame game, SyncLogger logger)
    {
        bool isVr = false;
        string reason = string.Empty;

        LoadCache();

        if (!string.IsNullOrWhiteSpace(game.Title) && _vrCache != null)
        {
            lock (_cacheLock)
            {
                if (_vrCache.TryGetValue(game.Title, out bool cachedIsVr))
                {
                    if (cachedIsVr)
                    {
                        logger.Log("VR Detection", $"[VR MATCH] Game '{game.Title}' identified as VR (from cache).");
                    }
                    return cachedIsVr;
                }
            }
        }

        // 1. Check Launch Arguments
        if (!string.IsNullOrWhiteSpace(game.LaunchArguments))
        {
            var argsLower = game.LaunchArguments.ToLowerInvariant();
            if (VrLaunchArgs.Any(arg => argsLower.Contains(arg)))
            {
                isVr = true;
                reason = $"Launch arguments contain VR flag: '{game.LaunchArguments}'";
            }
        }

        // 2. Check Title
        if (!isVr && !string.IsNullOrWhiteSpace(game.Title))
        {
            if (VrTitleRegex.IsMatch(game.Title) || game.Title.Equals("VRChat", StringComparison.OrdinalIgnoreCase))
            {
                isVr = true;
                reason = $"Title matches VR pattern: '{game.Title}'";
            }
        }

        // 3. Check ExePath / Folder Name
        if (!isVr && !string.IsNullOrWhiteSpace(game.ExePath))
        {
            var folderName = Path.GetFileName(Path.GetDirectoryName(game.ExePath)) ?? string.Empty;
            var exeName = Path.GetFileNameWithoutExtension(game.ExePath) ?? string.Empty;

            if (VrTitleRegex.IsMatch(folderName))
            {
                isVr = true;
                reason = $"Folder name matches VR pattern: '{folderName}'";
            }
            else if (VrTitleRegex.IsMatch(exeName) || exeName.EndsWith("_vr", StringComparison.OrdinalIgnoreCase))
            {
                isVr = true;
                reason = $"Executable name matches VR pattern: '{exeName}'";
            }
        }

        // 4. Fallback: Steam Store API Lookup
        if (!isVr && !string.IsNullOrWhiteSpace(game.Title))
        {
            try
            {
                // Add a small delay to respect rate limits
                await Task.Delay(250);

                // Note: This API can be rate-limited. We catch HttpRequestException.
                var searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(game.Title)}&l=english&cc=US";
                var searchJson = await HttpClient.GetStringAsync(searchUrl);
                var searchData = JsonSerializer.Deserialize<SteamStoreSearchResponse>(searchJson);

                if (searchData != null && searchData.Items.Count > 0)
                {
                    // Check matches. Only process exact name matches to prevent false positives (e.g. "Game" matching "Game VR").
                    var normalizedGameTitle = NormalizeTitle(game.Title);
                    foreach (var match in searchData.Items)
                    {
                        var normalizedMatchName = NormalizeTitle(match.Name);
                        if (!string.Equals(normalizedGameTitle, normalizedMatchName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip fuzzy matches
                        }

                        try 
                        {
                            // Add a small delay for the appdetails API call as well
                            await Task.Delay(250);
                            var detailsUrl = $"https://store.steampowered.com/api/appdetails?appids={match.Id}";
                            var detailsJson = await HttpClient.GetStringAsync(detailsUrl);
                            
                            // The appdetails endpoint returns a dictionary keyed by the appid string
                            using var document = JsonDocument.Parse(detailsJson);
                            var appIdString = match.Id.ToString();

                            if (document.RootElement.TryGetProperty(appIdString, out var appElement))
                            {
                                var appDetails = JsonSerializer.Deserialize<SteamStoreAppDetailsResponse>(appElement.GetRawText());
                                
                                if (appDetails?.Success == true && appDetails.Data != null)
                                {
                                    // Skip anything that isn't a base game (e.g., soundtracks, DLCs)
                                    if (!appDetails.Data.Type.Equals("game", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                    
                                    // Cache the Official Steam AppId if it's not set
                                    game.OfficialSteamAppId ??= (uint)match.Id;

                                    // 53 = VR Supported, 54 = VR Only
                                    if (appDetails.Data.Categories.Any(c => c.Id == 53 || c.Id == 54))
                                    {
                                        isVr = true;
                                        reason = $"Steam Store API reports VR Supported/Only for AppId {match.Id} ({match.Name})";
                                        break; // Found VR, stop checking
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Log("VR Detection", $"[VR API] Failed to fetch details for AppId {match.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log("VR Detection", $"[VR API] Steam API lookup failed for '{game.Title}': {ex.Message}");
            }
        }

        if (isVr)
        {
            logger.Log("VR Detection", $"[VR MATCH] Game '{game.Title}' identified as VR. Reason: {reason}");
        }

        if (!string.IsNullOrWhiteSpace(game.Title) && _vrCache != null)
        {
            lock (_cacheLock)
            {
                _vrCache[game.Title] = isVr;
            }
            SaveCache();
        }

        return isVr;
    }
}
