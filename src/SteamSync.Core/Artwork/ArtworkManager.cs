using SteamSync.Core.Logging;
using SteamSync.Core.Models;
using SteamSync.Core.Steam;
using SteamSync.Core.Utilities;

namespace SteamSync.Core.Artwork;

/// <summary>
/// Manages the artwork pipeline: searches SteamGridDB for game art,
/// downloads assets, and saves them to Steam's grid directory using
/// the official naming scheme.
///
/// Steam grid naming:
/// - {appid}p.png    — Grid/Cover (portrait, 600x900)
/// - {appid}_hero.png — Hero (wide banner, 1920x620)
/// - {appid}_logo.png — Logo (transparent)
/// - {appid}_icon.png — Icon (square)
/// </summary>
public class ArtworkManager
{
    private readonly SteamGridDbClient _client;
    private readonly SyncLogger _logger;

    public ArtworkManager(SteamGridDbClient client, SyncLogger logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Updates the API key used for SteamGridDB requests.
    /// </summary>
    public void UpdateApiKey(string apiKey)
    {
        _client.UpdateApiKey(apiKey);
    }

    /// <summary>
    /// Uses SteamGridDB search to resolve a heuristic title into its exact official name.
    /// Returns null if no match is found.
    /// </summary>
    public async Task<string?> ResolveGameTitleAsync(string heuristicTitle)
    {
        try
        {
            // First, produce a search-friendly version of the title
            var searchTitle = TitleSanitizer.SanitizeForSearch(heuristicTitle);
            _logger.Log("TitleResolve", $"Resolving '{heuristicTitle}' → search query: '{searchTitle}'");

            var results = await _client.SearchGamesAsync(searchTitle);
            if (results != null && results.Count > 0)
            {
                // 1. Try exact match on the search title
                var exact = results.FirstOrDefault(r => string.Equals(r.Name, searchTitle, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    _logger.Log("TitleResolve", $"Exact match: '{exact.Name}' for '{heuristicTitle}'");
                    return exact.Name;
                }

                // 2. Try exact match on the original heuristic title
                exact = results.FirstOrDefault(r => string.Equals(r.Name, heuristicTitle, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    _logger.Log("TitleResolve", $"Exact match (original): '{exact.Name}' for '{heuristicTitle}'");
                    return exact.Name;
                }

                // 3. If search title still contains "VR", try without it
                if (searchTitle.EndsWith(" VR", StringComparison.OrdinalIgnoreCase))
                {
                    var baseTitle = searchTitle[..^3].Trim();
                    _logger.Log("TitleResolve", $"Stripping VR suffix, retrying with: '{baseTitle}'");
                    var baseResults = await _client.SearchGamesAsync(baseTitle);

                    if (baseResults != null && baseResults.Count > 0)
                    {
                        var baseExact = baseResults.FirstOrDefault(r => string.Equals(r.Name, baseTitle, StringComparison.OrdinalIgnoreCase));
                        if (baseExact != null)
                        {
                            _logger.Log("TitleResolve", $"Exact match (base): '{baseExact.Name}' for '{heuristicTitle}'");
                            return baseExact.Name;
                        }

                        var baseBest = baseResults.FirstOrDefault(r => r.Verified) ?? baseResults[0];
                        _logger.Log("TitleResolve", $"Best fuzzy match (base): '{baseBest.Name}' for '{heuristicTitle}'");
                        return baseBest.Name;
                    }
                }

                // 4. Fallback to best fuzzy match from original search
                var best = results.FirstOrDefault(r => r.Verified) ?? results[0];
                _logger.Log("TitleResolve", $"Best fuzzy match: '{best.Name}' for '{heuristicTitle}'");
                return best.Name;
            }

            _logger.Log("TitleResolve", $"No results found for '{heuristicTitle}'");
        }
        catch (Exception ex)
        {
            _logger.LogError("TitleResolve", $"Failed to resolve title '{heuristicTitle}'", ex);
        }
        return null;
    }

    /// <summary>
    /// Fetches and saves all artwork types for a detected game.
    /// </summary>
    /// <param name="game">The game to fetch artwork for.</param>
    /// <param name="userId">Steam user ID to save artwork under.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>True if at least one artwork type was downloaded.</returns>
    public async Task<bool> FetchAndSaveArtworkAsync(
        DetectedGame game,
        string userId,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var gridPath = SteamPathResolver.GetGridPath(userId);
        if (gridPath == null)
        {
            _logger.LogError("Artwork", $"Grid path not found for user {userId}");
            return false;
        }

        // Resolve SteamGridDB game ID if not already cached
        int? gameId = game.SteamGridDbId;
        if (gameId == null)
        {
            var msg = $"Searching SteamGridDB for '{game.Title}'...";
            progress?.Report(msg);
            _logger.Log("Artwork", msg);
            var searchResults = await _client.SearchGamesAsync(game.Title, ct);

            if (searchResults.Count == 0)
            {
                var noResultsMsg = $"No results found for '{game.Title}'";
                progress?.Report(noResultsMsg);
                _logger.Log("Artwork", noResultsMsg);
                return false;
            }

            // Prefer verified matches, then best fuzzy match
            var bestMatch = searchResults.FirstOrDefault(r => r.Verified) ?? searchResults[0];
            gameId = bestMatch.Id;
            game.SteamGridDbId = gameId;
            _logger.Log("Artwork", $"Matched '{game.Title}' → '{bestMatch.Name}' (SteamGridDB ID: {gameId})");
        }

        // Calculate the AppID used for file naming
        if (string.IsNullOrWhiteSpace(game.ExePath))
        {
            _logger.LogError("Artwork", $"No exe path for '{game.Title}', skipping artwork");
            return false;
        }

        var appId = AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
        _logger.Log("Artwork", $"AppID for '{game.Title}': {appId}, grid path: {gridPath}");
        var anyDownloaded = false;
        
        // Dynamically fetch Official Steam AppID if missing (needed for fallbacks)
        uint? steamAppId = game.OfficialSteamAppId;
        if (steamAppId == null && !string.IsNullOrWhiteSpace(game.Title))
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(game.Title)}&l=english&cc=US";
                var searchJson = await http.GetStringAsync(searchUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(searchJson);
                var items = doc.RootElement.GetProperty("items");
                
                if (items.GetArrayLength() > 0)
                {
                    var bestItem = items[0];
                    bool foundExact = false;

                    foreach (var item in items.EnumerateArray())
                    {
                        var name = item.GetProperty("name").GetString() ?? "";
                        
                        // If game is VR, prefer the ' VR' suffixed version if it exists
                        if (game.IsVR && name.EndsWith(" VR", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(name.Substring(0, name.Length - 3), game.Title, StringComparison.OrdinalIgnoreCase))
                        {
                            bestItem = item;
                            break;
                        }

                        // Otherwise fallback to exact match
                        if (!foundExact && string.Equals(name, game.Title, StringComparison.OrdinalIgnoreCase))
                        {
                            bestItem = item;
                            foundExact = true;
                        }
                    }

                    steamAppId = (uint)bestItem.GetProperty("id").GetInt32();
                    game.OfficialSteamAppId = steamAppId;
                    _logger.Log("Artwork", $"Dynamically resolved Official Steam AppID {steamAppId} for '{game.Title}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Artwork", $"Failed to dynamically resolve Steam AppID for '{game.Title}'", ex);
            }
        }

        // Download portrait grid/cover
        anyDownloaded |= await DownloadFirstImageAsync(
            gameId.Value,
            () => _client.GetGridsAsync(gameId.Value, "600x900", ct),
            Path.Combine(gridPath, $"{appId}p.png"),
            "portrait grid", progress, ct,
            steamAppId != null ? $"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId.Value}/library_600x900.jpg" : null);

        // Download landscape grid/banner (used in Big Picture Mode & Recent Games)
        anyDownloaded |= await DownloadFirstImageAsync(
            gameId.Value,
            () => _client.GetGridsAsync(gameId.Value, "460x215,920x430", ct),
            Path.Combine(gridPath, $"{appId}.png"),
            "landscape grid", progress, ct,
            steamAppId != null ? $"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId.Value}/header.jpg" : null);

        // Download hero
        anyDownloaded |= await DownloadFirstImageAsync(
            gameId.Value,
            () => _client.GetHeroesAsync(gameId.Value, ct),
            Path.Combine(gridPath, $"{appId}_hero.png"),
            "hero", progress, ct,
            steamAppId != null ? $"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId.Value}/library_hero.jpg" : null);

        // Download logo
        anyDownloaded |= await DownloadFirstImageAsync(
            gameId.Value,
            () => _client.GetLogosAsync(gameId.Value, ct),
            Path.Combine(gridPath, $"{appId}_logo.png"),
            "logo", progress, ct,
            steamAppId != null ? $"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId.Value}/logo.png" : null);

        // Download icon
        anyDownloaded |= await DownloadFirstImageAsync(
            gameId.Value,
            () => _client.GetIconsAsync(gameId.Value, ct),
            Path.Combine(gridPath, $"{appId}_icon.png"),
            "icon", progress, ct);

        if (anyDownloaded)
            game.ArtworkCached = true;

        _logger.Log("Artwork", $"Artwork for '{game.Title}': {(anyDownloaded ? "downloaded" : "none available")}");
        return anyDownloaded;
    }

    /// <summary>
    /// Downloads the first (highest-scored) non-NSFW image from a list, with an optional Steam CDN fallback.
    /// </summary>
    private async Task<bool> DownloadFirstImageAsync(
        int gameId,
        Func<Task<List<SteamGridDbImage>>> getImages,
        string savePath,
        string typeName,
        IProgress<string>? progress,
        CancellationToken ct,
        string? steamCdnFallbackUrl = null)
    {
        try
        {
            var images = await getImages();
            var best = images
                .Where(i => !i.Nsfw && !i.Humor)
                .OrderByDescending(i => i.Score)
                .FirstOrDefault();

            if (best != null)
            {
                progress?.Report($"Downloading {typeName}...");
                var data = await _client.DownloadImageAsync(best.Url, ct);
                await File.WriteAllBytesAsync(savePath, data, ct);
                _logger.Log("Artwork", $"Downloaded {typeName} → {savePath} ({data.Length:N0} bytes)");
                return true;
            }
            
            _logger.Log("Artwork", $"No suitable {typeName} found for SteamGridDB ID {gameId}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Artwork", $"Failed to download {typeName} for SteamGridDB ID {gameId}", ex);
        }

        // Fallback to Steam CDN if provided
        if (!string.IsNullOrWhiteSpace(steamCdnFallbackUrl))
        {
            try
            {
                _logger.Log("Artwork", $"Fallback: Downloading {typeName} from Steam CDN...");
                progress?.Report($"Downloading {typeName} (Steam Fallback)...");
                
                using var http = new HttpClient();
                var fallbackData = await http.GetByteArrayAsync(steamCdnFallbackUrl, ct);
                await File.WriteAllBytesAsync(savePath, fallbackData, ct);
                
                _logger.Log("Artwork", $"Downloaded {typeName} (Fallback) → {savePath} ({fallbackData.Length:N0} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Artwork", $"Steam CDN fallback failed for {typeName}", ex);
            }
        }

        return false;
    }

    /// <summary>
    /// Removes all artwork files for a specific AppID from the grid directory.
    /// </summary>
    public static void RemoveArtwork(string userId, uint appId)
    {
        var gridPath = SteamPathResolver.GetGridPath(userId);
        if (gridPath == null) return;

        var patterns = new[] { $"{appId}.*", $"{appId}p.*", $"{appId}_hero.*", $"{appId}_logo.*", $"{appId}_icon.*" };
        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.GetFiles(gridPath, pattern))
            {
                try { File.Delete(file); } catch { /* best effort */ }
            }
        }
    }
}
