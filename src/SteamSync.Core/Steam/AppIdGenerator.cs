using SteamSync.Core.Utilities;

namespace SteamSync.Core.Steam;

/// <summary>
/// Generates Non-Steam shortcut AppIDs matching the Steam/BoilR specification.
/// The algorithm creates a deterministic 32-bit ID from the executable path and game name
/// using CRC32 with specific bitwise flags.
/// </summary>
public static class AppIdGenerator
{
    /// <summary>
    /// Calculates the 32-bit AppID used in shortcuts.vdf.
    /// This is the "short" AppID that Steam stores in the binary VDF file.
    ///
    /// Algorithm (matching BoilR):
    /// 1. Concatenate exe path + app name as a single UTF-8 string
    /// 2. Compute CRC32 of the concatenated string
    /// 3. Apply: result = (crc | 0x80000000)
    /// This is what's stored in the VDF appid field.
    /// </summary>
    /// <param name="exePath">Full path to the executable (the "target").</param>
    /// <param name="appName">Display name of the game.</param>
    /// <returns>32-bit AppID suitable for shortcuts.vdf.</returns>
    public static uint GenerateShortcutAppId(string exePath, string appName)
    {
        // BoilR concatenates: target + app_name
        var input = exePath + appName;
        var crc = Crc32.Compute(input);

        // Set the top bit to distinguish non-steam shortcuts from regular AppIDs
        return crc | 0x80000000u;
    }

    /// <summary>
    /// Calculates the full 64-bit AppID used for steam://rungameid/ URLs
    /// and grid artwork file naming.
    ///
    /// Algorithm:
    /// 1. Get the 32-bit shortcut AppID
    /// 2. Construct 64-bit: (shortcutId &lt;&lt; 32) | 0x02000000
    /// </summary>
    /// <param name="exePath">Full path to the executable.</param>
    /// <param name="appName">Display name of the game.</param>
    /// <returns>64-bit AppID for use in steam:// URLs and artwork naming.</returns>
    public static ulong GenerateFullAppId(string exePath, string appName)
    {
        var shortcutId = (ulong)GenerateShortcutAppId(exePath, appName);
        return (shortcutId << 32) | 0x02000000UL;
    }

    /// <summary>
    /// Converts a 64-bit AppID to the shortened format used for grid artwork naming.
    /// Grid images use the top 32 bits of the full AppID.
    /// </summary>
    public static uint GetGridAppId(string exePath, string appName)
    {
        var fullId = GenerateFullAppId(exePath, appName);
        return (uint)(fullId >> 32);
    }
}
