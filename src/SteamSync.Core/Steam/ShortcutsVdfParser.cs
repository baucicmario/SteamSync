using System.Text;
using SteamSync.Core.Models;

namespace SteamSync.Core.Steam;

/// <summary>
/// Parses Steam's binary shortcuts.vdf format.
///
/// Binary VDF format markers:
/// - 0x00 = Object/Map start
/// - 0x01 = String value
/// - 0x02 = 32-bit integer value
/// - 0x08 = Object/Section end
///
/// File structure:
///   0x00 "shortcuts" 0x00
///     0x00 "0" 0x00 [key-value pairs...] 0x08
///     0x00 "1" 0x00 [key-value pairs...] 0x08
///   0x08 0x08
/// </summary>
public static class ShortcutsVdfParser
{
    private const byte TYPE_OBJECT = 0x00;
    private const byte TYPE_STRING = 0x01;
    private const byte TYPE_INT32 = 0x02;
    private const byte TYPE_END = 0x08;

    /// <summary>
    /// Parses a shortcuts.vdf binary file and returns all shortcuts.
    /// </summary>
    /// <param name="filePath">Path to shortcuts.vdf.</param>
    /// <returns>List of parsed shortcuts.</returns>
    public static List<SteamShortcut> Parse(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<SteamShortcut>();

        var data = File.ReadAllBytes(filePath);
        return Parse(data);
    }

    /// <summary>
    /// Parses shortcuts.vdf binary data and returns all shortcuts.
    /// </summary>
    public static List<SteamShortcut> Parse(byte[] data)
    {
        var shortcuts = new List<SteamShortcut>();
        var offset = 0;

        // Skip the root "shortcuts" object header
        // Expected: 0x00 "shortcuts" 0x00
        if (offset < data.Length && data[offset] == TYPE_OBJECT)
        {
            offset++;
            var rootName = ReadNullTerminatedString(data, ref offset);
            // rootName should be "shortcuts"
        }

        // Read each shortcut entry
        while (offset < data.Length)
        {
            if (data[offset] == TYPE_END)
            {
                offset++;
                break; // End of shortcuts section
            }

            if (data[offset] == TYPE_OBJECT)
            {
                offset++;
                var orderStr = ReadNullTerminatedString(data, ref offset);
                var shortcut = ReadShortcutObject(data, ref offset);

                if (int.TryParse(orderStr, out var order))
                    shortcut.Order = order;

                shortcuts.Add(shortcut);
            }
            else
            {
                break; // Unexpected format
            }
        }

        return shortcuts;
    }

    private static SteamShortcut ReadShortcutObject(byte[] data, ref int offset)
    {
        var shortcut = new SteamShortcut();

        while (offset < data.Length)
        {
            var type = data[offset];

            if (type == TYPE_END)
            {
                offset++;
                break;
            }

            offset++;
            var key = ReadNullTerminatedString(data, ref offset);

            switch (type)
            {
                case TYPE_STRING:
                    var strValue = ReadNullTerminatedString(data, ref offset);
                    SetStringProperty(shortcut, key, strValue);
                    break;

                case TYPE_INT32:
                    var intValue = ReadInt32(data, ref offset);
                    SetIntProperty(shortcut, key, intValue);
                    break;

                case TYPE_OBJECT:
                    // Nested object (e.g., "tags")
                    if (key.Equals("tags", StringComparison.OrdinalIgnoreCase))
                    {
                        shortcut.Tags = ReadTagsObject(data, ref offset);
                    }
                    else
                    {
                        SkipObject(data, ref offset);
                    }
                    break;
            }
        }

        return shortcut;
    }

    private static void SetStringProperty(SteamShortcut shortcut, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "appname": shortcut.AppName = value; break;
            case "exe": shortcut.Exe = value; break;
            case "startdir": shortcut.StartDir = value; break;
            case "icon": shortcut.Icon = value; break;
            case "shortcutpath": shortcut.ShortcutPath = value; break;
            case "launchoptions": shortcut.LaunchOptions = value; break;
            case "devkitgameid": shortcut.DevKitGameId = value; break;
        }
    }

    private static void SetIntProperty(SteamShortcut shortcut, string key, uint value)
    {
        switch (key.ToLowerInvariant())
        {
            case "appid": shortcut.AppId = value; break;
            case "ishidden": shortcut.IsHidden = value != 0; break;
            case "allowdesktopconfig": shortcut.AllowDesktopConfig = value != 0; break;
            case "allowoverlay": shortcut.AllowOverlay = value != 0; break;
            case "openvr": shortcut.OpenVr = value; break;
            case "devkit": shortcut.DevKit = value; break;
            case "devkitoverrideappid": shortcut.DevKitOverrideAppId = value; break;
            case "lastplaytime": shortcut.LastPlayTime = value; break;
        }
    }

    private static List<string> ReadTagsObject(byte[] data, ref int offset)
    {
        var tags = new List<string>();

        while (offset < data.Length)
        {
            var type = data[offset];

            if (type == TYPE_END)
            {
                offset++;
                break;
            }

            offset++;
            var key = ReadNullTerminatedString(data, ref offset);

            if (type == TYPE_STRING)
            {
                var value = ReadNullTerminatedString(data, ref offset);
                tags.Add(value);
            }
            else if (type == TYPE_INT32)
            {
                ReadInt32(data, ref offset); // Skip
            }
        }

        return tags;
    }

    private static void SkipObject(byte[] data, ref int offset)
    {
        while (offset < data.Length)
        {
            var type = data[offset];

            if (type == TYPE_END)
            {
                offset++;
                break;
            }

            offset++;
            ReadNullTerminatedString(data, ref offset); // Skip key

            switch (type)
            {
                case TYPE_STRING:
                    ReadNullTerminatedString(data, ref offset);
                    break;
                case TYPE_INT32:
                    ReadInt32(data, ref offset);
                    break;
                case TYPE_OBJECT:
                    SkipObject(data, ref offset);
                    break;
            }
        }
    }

    private static string ReadNullTerminatedString(byte[] data, ref int offset)
    {
        var start = offset;
        while (offset < data.Length && data[offset] != 0x00)
            offset++;

        var result = Encoding.UTF8.GetString(data, start, offset - start);
        offset++; // Skip the null terminator
        return result;
    }

    private static uint ReadInt32(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length)
        {
            offset = data.Length;
            return 0;
        }

        var value = BitConverter.ToUInt32(data, offset);
        offset += 4;
        return value;
    }
}
