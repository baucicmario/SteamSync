using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class RockstarDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new RockstarDetector();
        Assert.Equal("Rockstar Games", detector.Name);
        Assert.Equal("Rockstar", detector.PlatformId);
    }

    [Fact]
    public void KnownRockstarGames_ContainsCoreTitles()
    {
        Assert.True(RockstarDetector.KnownRockstarGames.ContainsKey("gta5"));
        Assert.True(RockstarDetector.KnownRockstarGames.ContainsKey("gta5_gen9"));
        Assert.True(RockstarDetector.KnownRockstarGames.ContainsKey("rdr2"));
        Assert.True(RockstarDetector.KnownRockstarGames.ContainsKey("gtasa"));
        Assert.True(RockstarDetector.KnownRockstarGames.ContainsKey("lanoirevr"));
        Assert.True(RockstarDetector.KnownRockstarGames["lanoirevr"].IsVR);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new RockstarDetector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await detector.DetectGamesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task DetectGamesAsync_ExecutesGracefully()
    {
        var detector = new RockstarDetector();
        var games = await detector.DetectGamesAsync();
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("Rockstar", game.Platform);
            Assert.True(game.IsOwned);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
        }
    }

    [Fact]
    public async Task DetectGamesAsync_DetectsOfflineOwnedGames()
    {
        var detector = new RockstarDetector();
        var games = await detector.DetectGamesAsync();
        
        // If the local Rockstar Launcher has cache files present, verify they were detected
        var localAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rockstar Games", "Launcher");
        if (Directory.Exists(localAppData) && Directory.GetFiles(localAppData, "buildcollection_gta5_*.xml").Length > 0)
        {
            Assert.Contains(games, g => g.Title.Contains("Grand Theft Auto V", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(games, g => g.Title.Contains("Grand Theft Auto: San Andreas", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task DetectGamesAsync_ExcludesLauncherAndServices()
    {
        var detector = new RockstarDetector();
        var games = await detector.DetectGamesAsync();

        Assert.DoesNotContain(games, g => string.Equals(g.Title, "Launcher", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(games, g => string.Equals(g.Title, "Rockstar Games Launcher", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(games, g => string.Equals(g.Title, "Rockstar Games SDK", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(games, g => string.Equals(g.Title, "Social Club", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetRockstarLauncherPath_ReturnsValidPathOrExplorerFallback()
    {
        var path = RockstarDetector.GetRockstarLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
