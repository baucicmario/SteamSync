using Microsoft.Win32;

namespace SteamSync.Core.Steam;

/// <summary>
/// Locates the Steam installation directory and userdata paths on Windows.
/// Reads the Steam install path from the registry and enumerates user directories.
/// </summary>
public class SteamPathResolver
{
    private const string SteamRegistryPath = @"SOFTWARE\WOW6432Node\Valve\Steam";
    private const string SteamRegistryPath32 = @"SOFTWARE\Valve\Steam";

    /// <summary>
    /// Gets the Steam installation directory from the registry.
    /// </summary>
    public static string? GetSteamInstallPath()
    {
        // Try 64-bit registry view first
        var path = Registry.LocalMachine.OpenSubKey(SteamRegistryPath)?.GetValue("InstallPath") as string;
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            return path;

        // Try 32-bit registry view
        path = Registry.LocalMachine.OpenSubKey(SteamRegistryPath32)?.GetValue("InstallPath") as string;
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            return path;

        // Try common default locations
        var defaults = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\SteamLibrary",
        };

        return defaults.FirstOrDefault(Directory.Exists);
    }

    /// <summary>
    /// Gets the Steam executable path.
    /// </summary>
    public static string? GetSteamExePath()
    {
        var installPath = GetSteamInstallPath();
        if (installPath == null) return null;

        var exePath = Path.Combine(installPath, "steam.exe");
        return File.Exists(exePath) ? exePath : null;
    }

    /// <summary>
    /// Gets the userdata directory containing per-user configuration.
    /// </summary>
    public static string? GetUserDataPath()
    {
        var installPath = GetSteamInstallPath();
        if (installPath == null) return null;

        var userDataPath = Path.Combine(installPath, "userdata");
        return Directory.Exists(userDataPath) ? userDataPath : null;
    }

    /// <summary>
    /// Enumerates all Steam user IDs (numeric directory names under userdata/).
    /// </summary>
    public static IReadOnlyList<string> GetUserIds()
    {
        var userDataPath = GetUserDataPath();
        if (userDataPath == null)
            return Array.Empty<string>();

        return Directory.GetDirectories(userDataPath)
            .Select(Path.GetFileName)
            .Where(name => name != null && name.All(char.IsDigit))
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Gets the shortcuts.vdf file path for a specific user.
    /// </summary>
    public static string? GetShortcutsVdfPath(string userId)
    {
        var userDataPath = GetUserDataPath();
        if (userDataPath == null) return null;

        var vdfPath = Path.Combine(userDataPath, userId, "config", "shortcuts.vdf");
        return vdfPath; // May not exist yet (first sync)
    }

    /// <summary>
    /// Gets the grid artwork directory for a specific user.
    /// </summary>
    public static string? GetGridPath(string userId)
    {
        var userDataPath = GetUserDataPath();
        if (userDataPath == null) return null;

        var gridPath = Path.Combine(userDataPath, userId, "config", "grid");
        Directory.CreateDirectory(gridPath); // Ensure it exists
        return gridPath;
    }
}
