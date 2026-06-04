using System.Text;

namespace PrinterSecsGem.Eq.Hardware.ERack;

internal static class ERackTagDecoder
{
    private const int BlockSize = 8;
    private const int TagStorageSize = 32;

    public static string DecodeInventoryTag(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < TagStorageSize)
        {
            return string.Empty;
        }

        Span<byte> tagBytes = stackalloc byte[TagStorageSize];
        var checksum = 0;

        for (var group = 0; group < 4; group++)
        {
            for (var offset = 0; offset < BlockSize; offset++)
            {
                var value = payload[group * BlockSize + (BlockSize - 1 - offset)];
                tagBytes[group * BlockSize + offset] = value;
                checksum += value;
            }
        }

        if (checksum == 0)
        {
            return string.Empty;
        }

        return Encoding.ASCII.GetString(tagBytes).TrimEnd('\0', ' ');
    }

    public static byte[] EncodeWriteTag(string tag)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        var encodedBytes = new byte[tagBytes.Length];

        for (var blockStart = 0; blockStart < tagBytes.Length; blockStart += BlockSize)
        {
            for (var offset = 0; offset < BlockSize; offset++)
            {
                encodedBytes[blockStart + offset] = tagBytes[blockStart + (BlockSize - 1 - offset)];
            }
        }

        return encodedBytes;
    }
}
