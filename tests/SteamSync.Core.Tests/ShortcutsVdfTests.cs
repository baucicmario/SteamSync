using SteamSync.Core.Models;
using SteamSync.Core.Steam;

namespace SteamSync.Core.Tests;

public class ShortcutsVdfTests
{
    [Fact]
    public void VdfParserAndWriter_RoundTrip_PreservesData()
    {
        var originalShortcuts = new List<SteamShortcut>
        {
            new SteamShortcut
            {
                Order = 0,
                AppId = 2147483648u, // 0x80000000
                AppName = "Test Game",
                Exe = "\"C:\\Games\\Test.exe\"",
                StartDir = "\"C:\\Games\"",
                IsHidden = false,
                AllowDesktopConfig = true,
                AllowOverlay = true,
                Tags = new List<string> { "SteamSync", "Favorite" }
            }
        };

        var binaryData = ShortcutsVdfWriter.Serialize(originalShortcuts);
        var parsedShortcuts = ShortcutsVdfParser.Parse(binaryData);

        Assert.Single(parsedShortcuts);
        var parsed = parsedShortcuts[0];

        Assert.Equal(originalShortcuts[0].AppId, parsed.AppId);
        Assert.Equal(originalShortcuts[0].AppName, parsed.AppName);
        Assert.Equal(originalShortcuts[0].Exe, parsed.Exe);
        Assert.Equal(originalShortcuts[0].StartDir, parsed.StartDir);
        Assert.Equal(originalShortcuts[0].Tags, parsed.Tags);
    }
}
