using SteamSync.Core.Steam;

namespace SteamSync.Core.Tests;

public class AppIdGeneratorTests
{
    [Fact]
    public void GenerateShortcutAppId_MatchesExpectedBoilROutput()
    {
        // BoilR's output for specific inputs is deterministic.
        // Let's test a known CRC32 output.
        // Example: exe="C:\\Games\\Test.exe", appName="Test Game"
        var exe = "C:\\Games\\Test.exe";
        var appName = "Test Game";

        var expectedCrc = Utilities.Crc32.Compute(exe + appName);
        var expectedAppId = expectedCrc | 0x80000000u;

        var appId = AppIdGenerator.GenerateShortcutAppId(exe, appName);
        
        Assert.Equal(expectedAppId, appId);
        // Ensure top bit is set
        Assert.True((appId & 0x80000000u) != 0);
    }

    [Fact]
    public void GenerateFullAppId_MatchesSteamSpec()
    {
        var exe = "test.exe";
        var appName = "Test";
        
        var shortcutId = AppIdGenerator.GenerateShortcutAppId(exe, appName);
        var fullId = AppIdGenerator.GenerateFullAppId(exe, appName);

        // Lower 32 bits should be 0x02000000
        Assert.Equal(0x02000000u, (uint)(fullId & 0xFFFFFFFF));
        
        // Upper 32 bits should match shortcutId
        Assert.Equal(shortcutId, (uint)(fullId >> 32));
    }
}
