using System.Text;
using SteamSync.Core.Models;

namespace SteamSync.Core.Steam;

/// <summary>
/// Writes Steam's binary shortcuts.vdf format.
/// Produces output compatible with Steam's expected binary VDF structure.
/// </summary>
public static class ShortcutsVdfWriter
{
    private const byte TYPE_OBJECT = 0x00;
    private const byte TYPE_STRING = 0x01;
    private const byte TYPE_INT32 = 0x02;
    private const byte TYPE_END = 0x08;

    /// <summary>
    /// Writes a list of shortcuts to a shortcuts.vdf file.
    /// </summary>
    public static void Write(string filePath, IReadOnlyList<SteamShortcut> shortcuts)
    {
        var data = Serialize(shortcuts);

        // Ensure directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(filePath, data);
    }

    /// <summary>
    /// Serializes shortcuts to binary VDF format.
    /// </summary>
    public static byte[] Serialize(IReadOnlyList<SteamShortcut> shortcuts)
    {
        using var ms = new MemoryStream();

        // Root object header: 0x00 "shortcuts" 0x00
        ms.WriteByte(TYPE_OBJECT);
        WriteNullTerminatedString(ms, "shortcuts");

        // Write each shortcut
        for (int i = 0; i < shortcuts.Count; i++)
        {
            var shortcut = shortcuts[i];

            // Shortcut object header: 0x00 "{index}" 0x00
            ms.WriteByte(TYPE_OBJECT);
            WriteNullTerminatedString(ms, i.ToString());

            // Write all properties
            WriteInt32(ms, "appid", shortcut.AppId);
            WriteString(ms, "AppName", shortcut.AppName);
            WriteString(ms, "Exe", shortcut.Exe);
            WriteString(ms, "StartDir", shortcut.StartDir);
            WriteString(ms, "icon", shortcut.Icon);
            WriteString(ms, "ShortcutPath", shortcut.ShortcutPath);
            WriteString(ms, "LaunchOptions", shortcut.LaunchOptions);
            WriteInt32(ms, "IsHidden", shortcut.IsHidden ? 1u : 0u);
            WriteInt32(ms, "AllowDesktopConfig", shortcut.AllowDesktopConfig ? 1u : 0u);
            WriteInt32(ms, "AllowOverlay", shortcut.AllowOverlay ? 1u : 0u);
            WriteInt32(ms, "OpenVR", shortcut.OpenVr);
            WriteInt32(ms, "Devkit", shortcut.DevKit);
            WriteString(ms, "DevkitGameID", shortcut.DevKitGameId);
            WriteInt32(ms, "DevkitOverrideAppID", shortcut.DevKitOverrideAppId);
            WriteInt32(ms, "LastPlayTime", shortcut.LastPlayTime);

            // Write tags
            ms.WriteByte(TYPE_OBJECT);
            WriteNullTerminatedString(ms, "tags");
            for (int t = 0; t < shortcut.Tags.Count; t++)
            {
                WriteString(ms, t.ToString(), shortcut.Tags[t]);
            }
            ms.WriteByte(TYPE_END); // End tags

            ms.WriteByte(TYPE_END); // End shortcut
        }

        ms.WriteByte(TYPE_END); // End shortcuts root
        ms.WriteByte(TYPE_END); // Final terminator

        return ms.ToArray();
    }

    private static void WriteString(MemoryStream ms, string key, string value)
    {
        ms.WriteByte(TYPE_STRING);
        WriteNullTerminatedString(ms, key);
        WriteNullTerminatedString(ms, value ?? string.Empty);
    }

    private static void WriteInt32(MemoryStream ms, string key, uint value)
    {
        ms.WriteByte(TYPE_INT32);
        WriteNullTerminatedString(ms, key);
        ms.Write(BitConverter.GetBytes(value), 0, 4);
    }

    private static void WriteNullTerminatedString(MemoryStream ms, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        ms.Write(bytes, 0, bytes.Length);
        ms.WriteByte(0x00); // Null terminator
    }
}
