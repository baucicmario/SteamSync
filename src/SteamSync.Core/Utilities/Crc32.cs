namespace SteamSync.Core.Utilities;

/// <summary>
/// IEEE 802.3 CRC32 implementation matching the algorithm used by Steam and BoilR
/// for Non-Steam shortcut AppID generation. Uses the standard polynomial 0xEDB88320.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = GenerateTable();

    private static uint[] GenerateTable()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320u;

        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) == 1)
                    crc = (crc >> 1) ^ polynomial;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// Computes the CRC32 checksum of the given byte array.
    /// </summary>
    public static uint Compute(byte[] data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// Computes the CRC32 checksum of a UTF-8 encoded string.
    /// </summary>
    public static uint Compute(string text)
    {
        return Compute(System.Text.Encoding.UTF8.GetBytes(text));
    }
}
