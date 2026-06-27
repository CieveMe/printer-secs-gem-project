using System.Text;

namespace PrinterSecsGem.Eq.Hardware.ERack;

internal static class ERackTagDecoder
{
    private const int BlockSize = 8;
    private const int TagStorageSize = 32;

    public static string DecodeInventoryTag(ReadOnlySpan<byte> payload, int readLengthBytes = TagStorageSize)
    {
        var tagBytes = DecodeInventoryTagBytes(payload);
        if (tagBytes.Length == 0)
        {
            return string.Empty;
        }

        var safeLength = Math.Clamp(RoundUpToBlock(readLengthBytes), BlockSize, TagStorageSize);
        return Encoding.ASCII.GetString(tagBytes.AsSpan(0, safeLength)).TrimEnd('\0', ' ');
    }

    public static byte[] DecodeInventoryTagBytes(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < TagStorageSize)
        {
            return Array.Empty<byte>();
        }

        var tagBytes = new byte[TagStorageSize];
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
            return Array.Empty<byte>();
        }

        return tagBytes;
    }

    public static byte[] EncodeWriteTag(ReadOnlySpan<byte> tagBytes)
    {
        var encodedBytes = new byte[tagBytes.Length];

        for (var blockStart = 0; blockStart < tagBytes.Length; blockStart += BlockSize)
        {
            var blockLength = Math.Min(BlockSize, tagBytes.Length - blockStart);
            for (var offset = 0; offset < BlockSize; offset++)
            {
                if (offset >= blockLength)
                {
                    break;
                }

                encodedBytes[blockStart + offset] = tagBytes[blockStart + (blockLength - 1 - offset)];
            }
        }

        return encodedBytes;
    }

    public static int RoundUpToBlock(int length)
    {
        var safeLength = Math.Clamp(length, 1, TagStorageSize);
        return ((safeLength + BlockSize - 1) / BlockSize) * BlockSize;
    }
}
