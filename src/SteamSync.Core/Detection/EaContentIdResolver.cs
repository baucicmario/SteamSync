using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SteamSync.Core.Detection;

public static class EaContentIdResolver
{
    private static readonly Dictionary<string, string> KnownOfferToContentIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Origin.OFR.50.0002694", "194908" },     // Apex Legends™
        { "DR:225064100", "70619" },               // Battlefield 3™
        { "OFB-EAST:49753", "71715" },             // Bejeweled® 3
        { "Origin.OFR.50.0001094", "1001298" },    // Command & Conquer Red Alert™ 2
        { "DR:231812900", "71099" },               // Crusader: No Remorse™
        { "OFB-EAST:109547519", "1009228" },       // Dead Space™ (2008)
        { "OFB-EAST:51582", "70377" },             // Dragon Age™: Origins
        { "Origin.OFR.50.0001535", "70377" },      // Dragon Age™: Origins - Ultimate Edition
        { "Origin.OFR.50.0001131", "1000659" },    // Dragon Age™: Inquisition
        { "DR:231813000", "71100" },               // Dungeon Keeper™
        { "Origin.OFR.50.0005262", "1005288" },    // Mass Effect 2 (2010 Edition)
        { "OFB-EAST:56694", "1005288" },           // Mass Effect™ 2
        { "Origin.OFR.50.0004049", "198196" },     // Mass Effect™ Legendary Edition
        { "Origin.OFR.50.0000357", "1019521" },    // Medal of Honor™ Pacific Assault
        { "Origin.OFR.50.0003425", "195133_oa" },  // Need for Speed™ Heat
        { "Origin.OFR.50.0000461", "1023599" },    // Peggle®
        { "OFB-EAST:48217", "71592" },             // Plants vs. Zombies™
        { "Origin.OFR.50.0004640", "198300" },     // STAR WARS Jedi: Survivor™
        { "DR:235664600", "71104" },               // SimCity 2000™
        { "Origin.OFR.50.0002015", "1035052" },    // STAR WARS™ Battlefront™ II
        { "Origin.OFR.50.0003796", "196485" },     // STAR WARS Jedi: Fallen Order™
        { "Origin.OFR.50.0003112", "16124549" },   // STAR WARS™: Squadrons
        { "Origin.OFR.50.0002355", "196933" },     // SteamWorld Dig
        { "Origin.OFR.50.0001959", "196468" },     // Syberia II
        { "OFB-EAST:60531", "1025174" },           // Syndicate™ (1993)
        { "OFB-EAST:109552299", "1011164" },       // The Sims™ 4
        { "Origin.OFR.50.0000500", "1025169" },    // Theme Hospital™
        { "Origin.OFR.50.0001456", "1039093" },    // Titanfall™ 2
        { "OFB-EAST:39471", "1025181" },           // Ultima™ VIII
        { "DR:235664700", "1025192" },             // Wing Commander™ III
        { "OFB-EAST:52735", "1002139" },           // Zuma's Revenge™
        { "1184493", "1184493" },                  // skate.
    };

    private static Dictionary<string, string>? _cachedPlayniteMap;

    public static string ResolveContentId(string? offerIdOrContentId)
    {
        if (string.IsNullOrWhiteSpace(offerIdOrContentId))
            return string.Empty;

        var cleanId = offerIdOrContentId.Trim();

        // 1. Direct match in known map
        if (KnownOfferToContentIdMap.TryGetValue(cleanId, out var knownContentId))
            return knownContentId;

        // 2. Match in Playnite legacy-offers.json if available
        var playniteMap = GetPlayniteLegacyOffersMap();
        if (playniteMap != null && playniteMap.TryGetValue(cleanId, out var playniteContentId))
            return playniteContentId;

        // 3. If already numeric (e.g. 70619, 194908), return as-is
        return cleanId;
    }

    private static Dictionary<string, string> GetPlayniteLegacyOffersMap()
    {
        if (_cachedPlayniteMap != null)
            return _cachedPlayniteMap;

        _cachedPlayniteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var extensionsDataDir = Path.Combine(appData, "Playnite", "ExtensionsData");
            if (Directory.Exists(extensionsDataDir))
            {
                foreach (var file in Directory.GetFiles(extensionsDataDir, "legacy-offers.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            var elem = prop.Value;
                            var offerId = elem.TryGetProperty("offerId", out var o) ? o.GetString() : null;
                            var contentId = elem.TryGetProperty("contentId", out var c) ? c.GetString() : null;
                            if (!string.IsNullOrEmpty(contentId))
                            {
                                _cachedPlayniteMap[prop.Name] = contentId;
                                if (!string.IsNullOrEmpty(offerId))
                                {
                                    _cachedPlayniteMap[offerId] = contentId;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return _cachedPlayniteMap;
    }

    public static string GetEaLauncherPath()
    {
        try
        {
            string[] eaKeys = new[]
            {
                @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop",
                @"SOFTWARE\Electronic Arts\EA Desktop"
            };

            foreach (var subKey in eaKeys)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
                if (key != null)
                {
                    var launcherPath = key.GetValue("LauncherAppPath") as string;
                    if (!string.IsNullOrEmpty(launcherPath) && File.Exists(launcherPath))
                        return launcherPath;

                    var clientPath = key.GetValue("ClientPath") as string;
                    if (!string.IsNullOrEmpty(clientPath) && File.Exists(clientPath))
                        return clientPath;
                }
            }
        }
        catch { }

        string[] defaultPaths = new[]
        {
            @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe",
            @"C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
            @"C:\Program Files (x86)\Electronic Arts\EA Desktop\EA Desktop\EALauncher.exe",
            @"C:\Program Files (x86)\Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe",
        };

        foreach (var path in defaultPaths)
        {
            if (File.Exists(path))
                return path;
        }

        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        if (File.Exists(explorerPath))
            return explorerPath;

        return "explorer.exe";
    }
}
