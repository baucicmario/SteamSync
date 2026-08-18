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
                
                if (game.Platform == "BattleNet" || game.Platform == "Battle.net")
                {
                    var gameUid = game.LaunchArguments?.Replace("battlenet://launch/", "") ?? Utilities.TitleSanitizer.Sanitize(game.Title);
                    var generator = new Steam.BattleNetExecutableGenerator(_logger);
                    var exeDir = Path.Combine(cacheDir, "Executables", "BattleNet");
                    var exePath = Path.Combine(exeDir, $"{gameUid}.exe");
                    
                    if (generator.GenerateExecutable(gameUid, exePath))
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
