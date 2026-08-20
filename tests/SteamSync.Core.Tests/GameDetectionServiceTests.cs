using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SteamSync.Core.Detection;
using SteamSync.Core.Logging;
using SteamSync.Core.Models;
using Xunit;

namespace SteamSync.Core.Tests;

public class GameDetectionServiceTests
{
    private class MockDetector : IGameDetector
    {
        public string Name => "Mock Launcher";
        public string PlatformId => "Mock";

        public Task<IReadOnlyList<DetectedGame>> DetectGamesAsync(CancellationToken cancellationToken = default)
        {
            var games = new List<DetectedGame>
            {
                new DetectedGame
                {
                    Title = "Installed Game A",
                    Platform = "Mock",
                    IsInstalled = true,
                    IsOwned = true,
                    ExePath = "C:\\Games\\GameA\\game.exe"
                },
                new DetectedGame
                {
                    Title = "Uninstalled Game B",
                    Platform = "Mock",
                    IsInstalled = false,
                    IsOwned = true
                }
            };

            return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
        }
    }

    [Fact]
    public void ConfigureDefaults_SetsIncludeUninstalledGamesFromSettings()
    {
        var service = new GameDetectionService();
        var settings = new AppSettings { IncludeUninstalledGames = false };

        service.ConfigureDefaults(settings);

        Assert.False(service.IncludeUninstalledGames);
    }

    [Fact]
    public async Task DetectAllGamesAsync_WhenIncludeUninstalledIsFalse_FiltersOutUninstalledGames()
    {
        var service = new GameDetectionService();
        service.IncludeUninstalledGames = false;
        service.RegisterDetector(new MockDetector());

        var result = await service.DetectAllGamesAsync();

        Assert.Single(result);
        Assert.Equal("Installed Game A", result[0].Title);
        Assert.True(result[0].IsInstalled);
    }

    [Fact]
    public async Task DetectAllGamesAsync_WhenIncludeUninstalledIsTrue_IncludesUninstalledGames()
    {
        var service = new GameDetectionService();
        service.IncludeUninstalledGames = true;
        service.RegisterDetector(new MockDetector());

        var result = await service.DetectAllGamesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Title == "Installed Game A" && g.IsInstalled);
        Assert.Contains(result, g => g.Title == "Uninstalled Game B" && !g.IsInstalled);
    }

    [Fact]
    public async Task Detectors_WithIncludeUninstalledFalse_ExecuteGracefully()
    {
        var epic = new EpicGamesDetector(includeUninstalled: false);
        var gog = new GogDetector(includeUninstalled: false);
        var ubi = new UbisoftDetector(includeUninstalled: false);
        var ea = new EaAppDetector(includeUninstalled: false);
        var bnet = new BattleNetDetector(includeUninstalled: false);
        var rockstar = new RockstarDetector(includeUninstalled: false);

        var epicGames = await epic.DetectGamesAsync();
        var gogGames = await gog.DetectGamesAsync();
        var ubiGames = await ubi.DetectGamesAsync();
        var eaGames = await ea.DetectGamesAsync();
        var bnetGames = await bnet.DetectGamesAsync();
        var rockstarGames = await rockstar.DetectGamesAsync();

        Assert.NotNull(epicGames);
        Assert.NotNull(gogGames);
        Assert.NotNull(ubiGames);
        Assert.NotNull(eaGames);
        Assert.NotNull(bnetGames);
        Assert.NotNull(rockstarGames);

        Assert.All(epicGames, g => Assert.True(g.IsInstalled));
        Assert.All(eaGames, g => Assert.True(g.IsInstalled));
        Assert.All(bnetGames, g => Assert.True(g.IsInstalled));
        Assert.All(rockstarGames, g => Assert.True(g.IsInstalled));
    }
}
