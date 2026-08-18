using SteamSync.Core.Artwork;
using SteamSync.Core.Data;
using SteamSync.Core.Models;
using SteamSync.Core.Steam;

namespace SteamSync.Core.Tests;

public class ArtworkManagerTests
{
    [Fact]
    public void IsArtworkCached_ReturnsFalse_ForZeroAppId()
    {
        var result = ArtworkManager.IsArtworkCached(0);
        Assert.False(result);
    }

    [Fact]
    public void CheckArtworkCached_ComputesAppIdIfMissing()
    {
        var game = new DetectedGame
        {
            Title = "Cyberpunk 2077",
            Platform = "GOG",
            ExePath = "C:\\Games\\Cyberpunk 2077\\bin\\x64\\Cyberpunk2077.exe"
        };

        Assert.Equal(0u, game.SteamAppId);

        _ = ArtworkManager.CheckArtworkCached(game);

        var expectedAppId = AppIdGenerator.GenerateShortcutAppId(game.ExePath, game.Title);
        Assert.Equal(expectedAppId, game.SteamAppId);
        Assert.NotEqual(0u, game.SteamAppId);
    }

    [Fact]
    public void DetectedGame_ObservableProperties_TriggerNotifications()
    {
        var game = new DetectedGame
        {
            Title = "Test",
            SteamAppId = 0,
            ArtworkCached = false
        };

        var changedProperties = new List<string>();
        game.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        game.SteamAppId = 12345678;
        game.ArtworkCached = true;
        game.IsVR = true;

        Assert.Contains(nameof(DetectedGame.SteamAppId), changedProperties);
        Assert.Contains(nameof(DetectedGame.ArtworkCached), changedProperties);
        Assert.Contains(nameof(DetectedGame.IsVR), changedProperties);
    }

    [Fact]
    public void GameRepository_Upsert_PreservesExistingValuesWhenNewValuesAreDefault()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"steamsync_test_{Guid.NewGuid():N}.db");
        try
        {
            using var db = new SteamSyncDbContext(dbPath);
            var repo = new GameRepository(db);

            var game1 = new DetectedGame
            {
                Title = "Half-Life 2",
                Platform = "Steam",
                IsInstalled = true,
                ExePath = "C:\\Steam\\hl2.exe",
                SteamAppId = 987654,
                ArtworkCached = true,
                SteamGridDbId = 42,
                OfficialSteamAppId = 220
            };

            repo.Insert(game1);

            // Re-detect with same title and platform but 0 / false default metadata
            var game2 = new DetectedGame
            {
                Title = "Half-Life 2",
                Platform = "Steam",
                IsInstalled = true,
                ExePath = "C:\\Steam\\hl2.exe",
                SteamAppId = 0,
                ArtworkCached = false
            };

            repo.Upsert(game2);

            var loaded = repo.GetByTitleAndPlatform("Half-Life 2", "Steam");
            Assert.NotNull(loaded);
            Assert.Equal(987654u, loaded.SteamAppId);
            Assert.True(loaded.ArtworkCached);
            Assert.Equal(42, loaded.SteamGridDbId);
            Assert.Equal(220u, loaded.OfficialSteamAppId);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
