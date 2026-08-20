using System.Text.Json;
using SteamSync.Core.Models;
using Xunit;

namespace SteamSync.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_IncludeUninstalledGamesIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.IncludeUninstalledGames);
    }

    [Fact]
    public void AppSettings_Serialization_PreservesIncludeUninstalledGames()
    {
        var settings = new AppSettings
        {
            IncludeUninstalledGames = false,
            SteamGridDbApiKey = "test-api-key"
        };

        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.IncludeUninstalledGames);
        Assert.Equal("test-api-key", deserialized.SteamGridDbApiKey);
    }

    [Fact]
    public void AppSettings_Deserialization_DefaultsToTrueIfMissing()
    {
        var json = "{\"SteamGridDbApiKey\": \"key123\"}";
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        Assert.True(deserialized.IncludeUninstalledGames);
    }
}
