using SteamSync.Core.Models;
using SteamSync.Core.Steam;
using Xunit;

namespace SteamSync.Core.Tests;

public class SteamInjectorServiceTests
{
    [Fact]
    public void TagsConstants_HaveExpectedValues()
    {
        Assert.Equal("SteamSync", SteamInjectorService.SteamSyncTag);
        Assert.Equal("Installed", SteamInjectorService.InstalledTag);
        Assert.Equal("Uninstalled", SteamInjectorService.UninstalledTag);
    }

    [Fact]
    public void SteamShortcut_IsManagedBySteamSync_RecognizesTags()
    {
        var installedShortcut = new SteamShortcut
        {
            AppName = "Installed Game",
            Tags = new List<string> { SteamInjectorService.SteamSyncTag, SteamInjectorService.InstalledTag }
        };

        var uninstalledShortcut = new SteamShortcut
        {
            AppName = "Uninstalled Game",
            Tags = new List<string> { SteamInjectorService.SteamSyncTag, SteamInjectorService.UninstalledTag }
        };

        var userShortcut = new SteamShortcut
        {
            AppName = "User Game",
            Tags = new List<string> { "CustomTag" }
        };

        Assert.True(installedShortcut.IsManagedBySteamSync);
        Assert.True(uninstalledShortcut.IsManagedBySteamSync);
        Assert.False(userShortcut.IsManagedBySteamSync);
    }

    [Fact]
    public void VdfSerialization_PreservesInstalledAndUninstalledTags()
    {
        var shortcuts = new List<SteamShortcut>
        {
            new SteamShortcut
            {
                Order = 0,
                AppId = 123456u,
                AppName = "Installed Game",
                Exe = "\"C:\\Games\\Installed.exe\"",
                StartDir = "\"C:\\Games\"",
                Tags = new List<string> { SteamInjectorService.SteamSyncTag, SteamInjectorService.InstalledTag, "VR" }
            },
            new SteamShortcut
            {
                Order = 1,
                AppId = 654321u,
                AppName = "Uninstalled Game",
                Exe = "\"C:\\Windows\\System32\\cmd.exe\"",
                StartDir = "\"\"",
                Tags = new List<string> { SteamInjectorService.SteamSyncTag, SteamInjectorService.UninstalledTag }
            }
        };

        var bytes = ShortcutsVdfWriter.Serialize(shortcuts);
        var parsed = ShortcutsVdfParser.Parse(bytes);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(SteamInjectorService.InstalledTag, parsed[0].Tags);
        Assert.Contains("VR", parsed[0].Tags);
        Assert.Contains(SteamInjectorService.SteamSyncTag, parsed[0].Tags);

        Assert.Contains(SteamInjectorService.UninstalledTag, parsed[1].Tags);
        Assert.Contains(SteamInjectorService.SteamSyncTag, parsed[1].Tags);
        Assert.DoesNotContain("VR", parsed[1].Tags);
    }
}
