using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class GogDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new GogDetector();
        Assert.Equal("GOG Galaxy", detector.Name);
        Assert.Equal("GOG", detector.PlatformId);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new GogDetector();
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
        var detector = new GogDetector();
        var games = await detector.DetectGamesAsync();
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("GOG", game.Platform);
            Assert.True(game.IsOwned);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
            Assert.False(string.IsNullOrWhiteSpace(game.LaunchArguments));
        }
    }

    [Fact]
    public void GetGogLauncherPath_ReturnsValidPathOrExplorerFallback()
    {
        var path = GogDetector.GetGogLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
