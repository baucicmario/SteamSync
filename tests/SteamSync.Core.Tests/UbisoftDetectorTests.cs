using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class UbisoftDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new UbisoftDetector();
        Assert.Equal("Ubisoft Connect", detector.Name);
        Assert.Equal("Ubisoft", detector.PlatformId);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new UbisoftDetector();
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
        var detector = new UbisoftDetector();
        var games = await detector.DetectGamesAsync();
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("Ubisoft", game.Platform);
            Assert.True(game.IsOwned);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
        }
    }

    [Fact]
    public void GetUbisoftLauncherPath_ReturnsValidPathOrExplorerFallback()
    {
        var path = UbisoftDetector.GetUbisoftLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
