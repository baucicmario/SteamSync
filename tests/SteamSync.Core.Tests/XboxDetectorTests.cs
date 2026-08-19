using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class XboxDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new XboxDetector();
        Assert.Equal("Xbox (Installed)", detector.Name);
        Assert.Equal("Xbox", detector.PlatformId);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new XboxDetector();
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
        var detector = new XboxDetector();
        var games = await detector.DetectGamesAsync();
        
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("Xbox", game.Platform);
            Assert.True(game.IsOwned);
            Assert.True(game.IsInstalled);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
        }
    }

    [Fact]
    public void GetXboxLauncherPath_ReturnsValidPath()
    {
        var path = XboxDetector.GetXboxLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
