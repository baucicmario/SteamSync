using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;
using SteamSync.Core.Data;
using SteamSync.Core.Logging;
using SteamSync.Core.Models;

namespace SteamSync.Core.Artwork;

public class UninstalledImageProcessor
{
    private readonly SteamGridDbClient _client;
    private readonly SyncLogger _logger;
    private readonly GameRepository _gameRepo;
    private readonly HttpClient _httpClient;
    


    public UninstalledImageProcessor(SteamGridDbClient client, SyncLogger logger, GameRepository gameRepo)
    {
        _client = client;
        _logger = logger;
        _gameRepo = gameRepo;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SteamSync/1.0 (https://github.com/baucicmario/SteamSync)");
    }

    public async Task<List<DetectedGame>> ProcessUninstalledGamesAsync(IProgress<string>? progress = null, IProgress<double>? percentageProgress = null, CancellationToken ct = default)
    {
        var dummyGamesToSync = new List<DetectedGame>();
        var uninstalledGames = _gameRepo.GetUninstalledOwnedGames();
        if (!uninstalledGames.Any())
        {
            _logger.Log("UninstalledImageProcessor", "No uninstalled owned games found. Skipping image processing.");
            progress?.Report("No uninstalled games to process.");
            percentageProgress?.Report(100);
            return dummyGamesToSync;
        }

        progress?.Report($"Processing artwork for {uninstalledGames.Count} uninstalled games...");
        _logger.Log("UninstalledImageProcessor", $"Starting processing for {uninstalledGames.Count} uninstalled games.");

        var cacheDir = AppSettings.GetUninstalledImagesCacheDirectory();

        int total = uninstalledGames.Count;
        int current = 0;

        foreach (var game in uninstalledGames)
        {
            if (ct.IsCancellationRequested) break;

            current++;
            progress?.Report($"({current}/{total}) {game.Title}");
            percentageProgress?.Report((double)current / total * 100);

            try
            {
                await ProcessGameArtworkAsync(game, cacheDir, null, ct);
                
                if (game.Platform == "BattleNet" || game.Platform == "Battle.net" || game.Platform == "Epic" || game.Platform == "GOG" || game.Platform == "Gog" || game.Platform == "Ubisoft" || game.Platform == "Ubisoft Connect" || game.Platform == "Uplay" || game.Platform == "EA" || game.Platform == "EA App" || game.Platform == "Origin")
                {
                    bool generated = false;
                    string exePath = string.Empty;
                    
                    if (game.Platform == "BattleNet" || game.Platform == "Battle.net")
                    {
                        var gameUid = game.LaunchArguments;
                        if (!string.IsNullOrWhiteSpace(gameUid))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(gameUid, @"(?:battlenet://launch/|--game=)([^&/\s""]+)");
                            if (match.Success)
                            {
                                gameUid = match.Groups[1].Value;
                            }
                            else
                            {
                                gameUid = gameUid.Replace("battlenet://launch/", "").Trim('/');
                            }
                        }
                        if (string.IsNullOrWhiteSpace(gameUid))
                        {
                            gameUid = Utilities.TitleSanitizer.Sanitize(game.Title);
                        }

                        var launcherPath = Detection.BattleNetDetector.GetBattleNetLauncherPath();

                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        game.ExePath = launcherPath;
                        game.StartDir = Path.GetDirectoryName(launcherPath) ?? string.Empty;
                        game.LaunchArguments = $"--game={gameUid}";
                        game.IsInstalled = true; // Required by the injector

                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;

                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach (var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }

                        dummyGamesToSync.Add(game);
                    }
                    else if (game.Platform == "Epic")
                    {
                        var launchArg = game.LaunchArguments;
                        string url;

                        if (!string.IsNullOrWhiteSpace(launchArg) && launchArg.StartsWith("com.epicgames.launcher://", StringComparison.OrdinalIgnoreCase))
                        {
                            url = launchArg.Equals("com.epicgames.launcher://store/library", StringComparison.OrdinalIgnoreCase)
                                ? "com.epicgames.launcher://"
                                : launchArg;
                        }
                        else if (!string.IsNullOrWhiteSpace(launchArg) && !launchArg.Contains("://") && !launchArg.Contains(" ") && !launchArg.Contains("?"))
                        {
                            url = $"com.epicgames.launcher://store/p/{launchArg}";
                        }
                        else if (!string.IsNullOrWhiteSpace(game.Title))
                        {
                            var sanitized = Utilities.TitleSanitizer.Sanitize(game.Title);
                            var slug = System.Text.RegularExpressions.Regex.Replace(sanitized.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                            url = !string.IsNullOrWhiteSpace(slug) ? $"com.epicgames.launcher://store/p/{slug}" : "com.epicgames.launcher://";
                        }
                        else
                        {
                            url = "com.epicgames.launcher://";
                        }

                        var launcherPath = Detection.EpicGamesDetector.GetEpicLauncherPath();

                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        game.ExePath = launcherPath;
                        game.StartDir = Path.GetDirectoryName(launcherPath) ?? string.Empty;
                        game.LaunchArguments = $"\"{url}\"";
                        game.IsInstalled = true; // Required by the injector

                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;

                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach (var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }

                        dummyGamesToSync.Add(game);
                    }
                    else if (game.Platform == "GOG" || game.Platform == "Gog")
                    {
                        var gameId = game.LaunchArguments;
                        if (!string.IsNullOrWhiteSpace(gameId))
                        {
                            gameId = gameId.Replace("goggalaxy://openGameView/", "").Trim('/');
                        }
                        if (string.IsNullOrWhiteSpace(gameId))
                        {
                            gameId = Utilities.TitleSanitizer.Sanitize(game.Title);
                        }

                        var launcherPath = Detection.GogDetector.GetGogLauncherPath();
                        var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        game.ExePath = File.Exists(cmdPath) ? cmdPath : launcherPath;
                        game.StartDir = Path.GetDirectoryName(launcherPath) ?? string.Empty;
                        game.LaunchArguments = File.Exists(cmdPath)
                            ? $"/c start \"\" \"{launcherPath}\" /gameId={gameId} /command=installGame & ping 127.0.0.1 -n 2 >nul & start \"\" \"goggalaxy://openGameView/{gameId}\""
                            : $"/gameId={gameId} /command=installGame";
                        game.IsInstalled = true; // Required by the injector

                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;

                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach (var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }

                        dummyGamesToSync.Add(game);
                    }
                    else if (game.Platform == "Ubisoft" || game.Platform == "Ubisoft Connect" || game.Platform == "Uplay")
                    {
                        var gameId = game.LaunchArguments;
                        if (!string.IsNullOrWhiteSpace(gameId))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(gameId, @"uplay://(?:launch|install)/([a-zA-Z0-9_-]+)");
                            if (match.Success)
                            {
                                gameId = match.Groups[1].Value;
                            }
                            else
                            {
                                gameId = gameId.Replace("uplay://launch/", "").Replace("uplay://install/", "").Trim('/').Split('/')[0];
                            }
                        }

                        var launcherPath = Detection.UbisoftDetector.GetUbisoftLauncherPath();

                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        game.ExePath = launcherPath;
                        game.StartDir = Path.GetDirectoryName(launcherPath) ?? string.Empty;
                        game.LaunchArguments = $"\"uplay://install/{gameId}\"";
                        game.IsInstalled = true; // Required by the injector

                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;

                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach (var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }

                        dummyGamesToSync.Add(game);
                    }
                    else if (game.Platform == "EA" || game.Platform == "EA App" || game.Platform == "Origin")
                    {
                        var offerId = game.LaunchArguments;
                        if (!string.IsNullOrWhiteSpace(offerId))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(offerId, @"(?:offerIds?=|ea://launch/)([^&]+)");
                            if (match.Success)
                            {
                                offerId = match.Groups[1].Value;
                            }
                            else if (offerId.StartsWith("origin2://", StringComparison.OrdinalIgnoreCase) || offerId.StartsWith("ea://", StringComparison.OrdinalIgnoreCase))
                            {
                                offerId = offerId.Split(new[] { '=', '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                            }
                        }

                        var contentId = Detection.EaContentIdResolver.ResolveContentId(offerId);
                        var launcherPath = Detection.EaContentIdResolver.GetEaLauncherPath();

                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        game.ExePath = launcherPath;
                        game.StartDir = Path.GetDirectoryName(launcherPath) ?? string.Empty;
                        game.LaunchArguments = $"\"origin2://game/launch/?offerIds={contentId}\"";
                        game.IsInstalled = true; // Required by the injector

                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;

                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach (var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }

                        dummyGamesToSync.Add(game);
                    }
                    
                    if (generated)
                    {
                        var oldAppId = game.SteamAppId != 0 
                            ? game.SteamAppId 
                            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

                        // Set up the dummy game properties for Steam injection
                        game.ExePath = exePath;
                        game.IsInstalled = true; // Required by the injector
                        
                        var newAppId = Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
                        game.SteamAppId = newAppId;
                        
                        var userIds = Steam.SteamPathResolver.GetUserIds();
                        foreach(var userId in userIds)
                        {
                            var gridPath = Steam.SteamPathResolver.GetGridPath(userId);
                            if (gridPath != null)
                            {
                                var platformDir = Path.Combine(cacheDir, game.Platform);
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}p.png"), Path.Combine(gridPath, $"{newAppId}p.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}.png"), Path.Combine(gridPath, $"{newAppId}.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_hero.png"), Path.Combine(gridPath, $"{newAppId}_hero.png"));
                                CopyIfExist(Path.Combine(platformDir, $"{oldAppId}_logo.png"), Path.Combine(gridPath, $"{newAppId}_logo.png"));
                            }
                        }
                        
                        dummyGamesToSync.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("UninstalledImageProcessor", $"Failed to process artwork for '{game.Title}'", ex);
            }
        }
        
        progress?.Report("Finished processing uninstalled games artwork.");
        percentageProgress?.Report(100);
        return dummyGamesToSync;
    }
    
    private void CopyIfExist(string source, string dest)
    {
        if (File.Exists(source))
        {
            try
            {
                File.Copy(source, dest, true);
            }
            catch { }
        }
    }

    private async Task ProcessGameArtworkAsync(DetectedGame game, string cacheDir, IProgress<string>? progress, CancellationToken ct)
    {
        // Resolve SteamGridDB game ID if missing
        int? gameId = game.SteamGridDbId;
        if (gameId == null)
        {
            var searchResults = await _client.SearchGamesAsync(game.Title, ct);
            if (searchResults.Count == 0)
            {
                _logger.Log("UninstalledImageProcessor", $"No SteamGridDB results for '{game.Title}'");
                return;
            }
            var bestMatch = searchResults.FirstOrDefault(r => r.Verified) ?? searchResults[0];
            gameId = bestMatch.Id;
            
            // Save it back so we don't have to search next time
            game.SteamGridDbId = gameId;
            _gameRepo.Upsert(game);
        }

        var appId = game.SteamAppId != 0 
            ? game.SteamAppId 
            : Steam.AppIdGenerator.GenerateShortcutAppId(game.ExePath ?? game.Title, game.Title);

        var platformDir = Path.Combine(cacheDir, game.Platform);
        Directory.CreateDirectory(platformDir);

        // Download and process portrait (600x900)
        await DownloadAndProcessImageAsync(
            gameId.Value, 
            () => _client.GetGridsAsync(gameId.Value, "600x900", ct), 
            Path.Combine(platformDir, $"{appId}p.png"), 
            "portrait", game.Platform, progress, ct);

        // Download and process landscape (460x215 or 920x430)
        await DownloadAndProcessImageAsync(
            gameId.Value, 
            () => _client.GetGridsAsync(gameId.Value, "460x215,920x430", ct), 
            Path.Combine(platformDir, $"{appId}.png"), 
            "landscape", game.Platform, progress, ct);

        // Download and process hero
        await DownloadAndProcessImageAsync(
            gameId.Value, 
            () => _client.GetHeroesAsync(gameId.Value, ct), 
            Path.Combine(platformDir, $"{appId}_hero.png"), 
            "hero", game.Platform, progress, ct);

        // Download and process logo
        await DownloadAndProcessImageAsync(
            gameId.Value, 
            () => _client.GetLogosAsync(gameId.Value, ct), 
            Path.Combine(platformDir, $"{appId}_logo.png"), 
            "logo", game.Platform, progress, ct);
    }

    private async Task DownloadAndProcessImageAsync(
        int gameId, 
        Func<Task<List<SteamGridDbImage>>> getImages, 
        string savePath, 
        string typeName, 
        string platform,
        IProgress<string>? progress, 
        CancellationToken ct)
    {
        try
        {
            // Skip if already processed and saved
            if (File.Exists(savePath)) return;

            var images = await getImages();
            var best = images.Where(i => !i.Nsfw && !i.Humor).OrderByDescending(i => i.Score).FirstOrDefault();

            if (best != null)
            {
                progress?.Report($"Processing {typeName} for {platform} game...");
                var data = await _client.DownloadImageAsync(best.Url, ct);
                
                // Process the image using ImageSharp
                await ProcessAndSaveImageAsync(data, savePath, platform, typeName, ct);
                
                _logger.Log("UninstalledImageProcessor", $"Processed and saved {typeName} to {savePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("UninstalledImageProcessor", $"Failed to process {typeName} for SteamGridDB ID {gameId}", ex);
        }
    }

    private async Task ProcessAndSaveImageAsync(byte[] imageData, string savePath, string platform, string typeName, CancellationToken ct)
    {
        using var image = Image.Load<Rgba32>(imageData);
        
        // Grayscale the image
        image.Mutate(x => x.Grayscale());

        // For covers, add a distinct platform overlay
        // Widest images (landscape, hero) and transparent images (logo, icon) are just grayscaled.
        if (typeName == "portrait")
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Logos", $"{platform}.png");
            byte[]? logoBytes = null;
            if (File.Exists(logoPath))
            {
                try
                {
                    logoBytes = await File.ReadAllBytesAsync(logoPath, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError("UninstalledImageProcessor", $"Failed to load platform logo from {logoPath}", ex);
                }
            }

            image.Mutate(x => 
            {
                if (logoBytes != null)
                {
                    using var logo = Image.Load<Rgba32>(logoBytes);
                    using var shadow = logo.Clone();
                    shadow.Mutate(l => l.Brightness(0f)); // Make silhouette black

                    int targetSize = Math.Max(20, (int)(image.Height * 0.12)); // Approx 10% larger
                    
                    shadow.Mutate(l => l.Resize(new ResizeOptions
                    {
                        Size = new Size(targetSize, targetSize),
                        Mode = ResizeMode.Max
                    }));

                    logo.Mutate(l => l.Resize(new ResizeOptions
                    {
                        Size = new Size(targetSize, targetSize),
                        Mode = ResizeMode.Max
                    }));
                    
                    int padding = Math.Max(10, (int)(image.Width * 0.02));
                    // Draw shadow offset by 2 pixels with 60% opacity
                    x.DrawImage(shadow, new Point(padding + 2, padding + 2), 0.6f);
                    // Draw logo
                    x.DrawImage(logo, new Point(padding, padding), 1f);
                }
            });
        }

        await image.SaveAsync(savePath, ct);
    }
}
