using System.Text.RegularExpressions;

namespace SteamSync.Core.Utilities;

/// <summary>
/// Sanitizes game titles extracted from folder names by stripping repacker tags,
/// scene group names, version numbers, and replacing separators with spaces.
/// Designed to produce clean titles for accurate SteamGridDB fuzzy matching.
/// </summary>
public static partial class TitleSanitizer
{
    // Regex patterns for common scene/repacker artifacts
    // Order matters: apply broader patterns first

    /// <summary>Matches repacker tags like [FitGirl], [DODI], [Tiny Repacks], etc.</summary>
    [GeneratedRegex(@"\[.*?\]", RegexOptions.Compiled)]
    private static partial Regex RepackerTagsRegex();

    /// <summary>Matches scene group suffixes like -RUNE, -CODEX, -SKIDROW, -PLAZA, -GOG, etc.</summary>
    [GeneratedRegex(@"\s*-\s*(RUNE|CODEX|SKIDROW|PLAZA|HOODLUM|RELOADED|FLT|DARKSiDERS|TiNYiSO|GOG|DOGE|P2P|I_KnoW)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SceneGroupRegex();

    /// <summary>
    /// Matches generic trailing release-group suffixes: a hyphen followed by
    /// an ALL-CAPS word at the end of the string (e.g., -VIREGA, -RUNE, -EMPRESS).
    /// Must be at least 3 characters to avoid stripping legitimate subtitles like "-VR".
    /// </summary>
    [GeneratedRegex(@"\s*-\s*[A-Z][A-Z0-9]{2,}$", RegexOptions.Compiled)]
    private static partial Regex GenericReleaseGroupRegex();

    /// <summary>Matches version strings like v1.0, v2.3.1, Build.12345, etc.</summary>
    [GeneratedRegex(@"\s*[vV]?\d+\.\d+[\.\d]*\b", RegexOptions.Compiled)]
    private static partial Regex VersionNumberRegex();

    /// <summary>Matches build identifiers like Build 12345, Build.67890.</summary>
    [GeneratedRegex(@"\s*Build[\.\\s]*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BuildNumberRegex();

    /// <summary>Matches common suffixes like (x64), (x86), (64-bit), etc.</summary>
    [GeneratedRegex(@"\s*\((?:x64|x86|64-bit|32-bit)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ArchSuffixRegex();

    /// <summary>Matches trailing hyphens and whitespace.</summary>
    [GeneratedRegex(@"[\s\-]+$", RegexOptions.Compiled)]
    private static partial Regex TrailingJunkRegex();

    /// <summary>Matches multiple consecutive spaces.</summary>
    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    /// <summary>
    /// Non-title suffixes commonly appended to pirated/standalone game folder names.
    /// These are stripped in SanitizeForSearch() to produce better SteamGridDB queries.
    /// </summary>
    private static readonly string[] SearchStripSuffixes = new[]
    {
        "HD", "Remastered", "Remaster", "Collection",
        "Definitive Edition", "Complete Edition", "Gold Edition",
        "Game of the Year Edition", "GOTY", "Ultimate Edition",
        "Enhanced Edition", "Directors Cut", "Director's Cut",
    };

    /// <summary>
    /// Sanitizes a raw folder/file name into a clean game title suitable for
    /// SteamGridDB lookups and display.
    /// </summary>
    /// <param name="rawName">The raw folder name or file name (without extension).</param>
    /// <returns>A cleaned title string.</returns>
    public static string Sanitize(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        var result = rawName.Trim();

        // Remove repacker tags: [FitGirl], [DODI], etc.
        result = RepackerTagsRegex().Replace(result, "");

        // Remove known scene group suffixes: -CODEX, -SKIDROW, etc.
        result = SceneGroupRegex().Replace(result, "");

        // Remove generic trailing release-group suffixes: -VIREGA, -EMPRESS, etc.
        result = GenericReleaseGroupRegex().Replace(result, "");

        // Remove version numbers: v1.0, 2.3.1, etc.
        result = VersionNumberRegex().Replace(result, "");

        // Remove build numbers: Build 12345
        result = BuildNumberRegex().Replace(result, "");

        // Remove architecture suffixes: (x64), (x86), etc.
        result = ArchSuffixRegex().Replace(result, "");

        // Replace periods and underscores with spaces (common in scene releases)
        result = result.Replace('_', ' ');
        result = result.Replace('.', ' ');

        // Clean up trailing hyphens and whitespace
        result = TrailingJunkRegex().Replace(result, "");

        // Collapse multiple spaces
        result = MultiSpaceRegex().Replace(result, " ");

        return result.Trim();
    }

    /// <summary>
    /// Produces an aggressively sanitized title optimized for SteamGridDB search.
    /// Strips non-title suffixes like "VR", "HD", "Remastered", "Collection", etc.
    /// Use this for the search query, NOT for display.
    /// </summary>
    /// <param name="title">The already-sanitized title (output of Sanitize()).</param>
    /// <returns>A search-optimized title.</returns>
    public static string SanitizeForSearch(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var result = Sanitize(title); // Ensure base sanitization is applied

        // Strip known non-title suffixes (case-insensitive, whole-word)
        foreach (var suffix in SearchStripSuffixes)
        {
            // Try removing as trailing suffix first (most common)
            if (result.EndsWith($" {suffix}", StringComparison.OrdinalIgnoreCase))
            {
                result = result[..^(suffix.Length + 1)].Trim();
            }
        }

        // Clean up any trailing hyphens/spaces left over
        result = TrailingJunkRegex().Replace(result, "");
        result = MultiSpaceRegex().Replace(result, " ");

        return result.Trim();
    }
}
