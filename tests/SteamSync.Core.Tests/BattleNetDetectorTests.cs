using SteamSync.Core.Detection;

namespace SteamSync.Core.Tests;

public class BattleNetDetectorTests
{
    [Fact]
    public void Properties_ReturnExpectedPlatformAndName()
    {
        var detector = new BattleNetDetector();
        Assert.Equal("Battle.net", detector.Name);
        Assert.Equal("BattleNet", detector.PlatformId);
    }

    [Fact]
    public async Task DetectGamesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        var detector = new BattleNetDetector();
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
        var detector = new BattleNetDetector();
        var games = await detector.DetectGamesAsync();
        Assert.NotNull(games);
        foreach (var game in games)
        {
            Assert.Equal("BattleNet", game.Platform);
            Assert.True(game.IsOwned);
            Assert.False(string.IsNullOrWhiteSpace(game.Title));
        }
    }

    [Fact]
    public void GetBattleNetLauncherPath_ReturnsValidPathOrExplorerFallback()
    {
        var path = BattleNetDetector.GetBattleNetLauncherPath();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}
