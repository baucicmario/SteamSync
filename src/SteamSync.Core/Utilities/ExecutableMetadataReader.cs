using System.Diagnostics;

namespace SteamSync.Core.Utilities;

/// <summary>
/// Reads embedded Windows executable metadata (FileVersionInfo) to extract
/// the true game name from .exe files. Falls back to file/folder name if
/// no meaningful metadata is found.
/// </summary>
public static class ExecutableMetadataReader
{
    /// <summary>
    /// Attempts to extract a meaningful game title from the executable's embedded metadata.
    /// Checks ProductName first, then FileDescription, then falls back to null.
    /// </summary>
    /// <param name="exePath">Full path to the .exe file.</param>
    /// <returns>The extracted title, or null if no meaningful metadata was found.</returns>
    public static string? GetGameTitle(string exePath)
    {
        try
        {
            if (!File.Exists(exePath))
                return null;

            var info = FileVersionInfo.GetVersionInfo(exePath);

            // Prefer ProductName (most likely to be "Cyberpunk 2077", "The Witcher 3", etc.)
            if (!string.IsNullOrWhiteSpace(info.ProductName) && IsUsefulMetadata(info.ProductName))
                return info.ProductName.Trim();

            // Fall back to FileDescription
            if (!string.IsNullOrWhiteSpace(info.FileDescription) && IsUsefulMetadata(info.FileDescription))
                return info.FileDescription.Trim();

            return null;
        }
        catch
        {
            // Access denied, corrupt PE, etc. — silently fall back
            return null;
        }
    }

    /// <summary>
    /// Determines if a metadata string contains useful information (not just a generic
    /// engine name or the exe filename repeated).
    /// </summary>
    private static bool IsUsefulMetadata(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Filter out generic engine/framework names that aren't game titles
        var genericNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Unity", "Unreal Engine", "UnrealEngine", "UE4", "UE5",
            "Godot Engine", "GameMaker", "RPG Maker",
            "Application", "Game", "Launcher",
            "Setup", "Installer", "Uninstall",
            "CrashHandler", "CrashReporter",
            "DirectX", "Microsoft", ".NET",
        };

        return !genericNames.Contains(value.Trim());
    }

    /// <summary>
    /// Gets the product version from the executable metadata.
    /// </summary>
    public static string? GetProductVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            return info.ProductVersion;
        }
        catch
        {
            return null;
        }
    }
}
