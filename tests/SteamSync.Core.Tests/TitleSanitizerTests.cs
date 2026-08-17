using SteamSync.Core.Utilities;

namespace SteamSync.Core.Tests;

public class TitleSanitizerTests
{
    [Theory]
    [InlineData("Cyberpunk 2077 [FitGirl Repack]", "Cyberpunk 2077")]
    [InlineData("Hogwarts Legacy - RUNE", "Hogwarts Legacy")]
    [InlineData("The.Witcher.3.v4.0.0-GOG", "The Witcher 3")]
    [InlineData("Terraria_v1.4.4.9", "Terraria")]
    [InlineData("Elden Ring (x64)", "Elden Ring")]
    [InlineData("  Messy   Spacing  - CODEX  ", "Messy Spacing")]
    public void Sanitize_CleansSceneAndRepackerTags(string input, string expected)
    {
        var actual = TitleSanitizer.Sanitize(input);
        Assert.Equal(expected, actual);
    }
}
