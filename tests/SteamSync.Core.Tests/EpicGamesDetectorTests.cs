using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class EpicGamesDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new EpicGamesDetector();
        Assert.Equal("Epic Games", detector.Name);
        Assert.Equal("Epic", detector.PlatformId);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new EpicGamesDetector();
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
        var detector = new EpicGamesDetector();
        var games = await detector.DetectGamesAsync();
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("Epic", game.Platform);
            Assert.True(game.IsOwned);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
        }
    }

    [Fact]
    public void GetEpicLauncherPath_ReturnsValidPathOrExplorerFallback()
    {
        var path = EpicGamesDetector.GetEpicLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
