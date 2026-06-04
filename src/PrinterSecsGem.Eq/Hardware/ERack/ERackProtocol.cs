namespace PrinterSecsGem.Eq.Hardware.ERack;

internal static class ERackProtocol
{
    private const byte Header0 = 0xAA;
    private const byte Header1 = 0x55;
    private const int FrameOverhead = 10;
    private const int LengthFieldBase = 8;

    public static byte[] BuildRequest(byte address, byte command, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[payload.Length + FrameOverhead];
        var index = 0;

        frame[index++] = Header0;
        frame[index++] = Header1;

        var length = payload.Length + LengthFieldBase;
        frame[index++] = (byte)((length >> 8) & 0xFF);
        frame[index++] = (byte)(length & 0xFF);

        frame[index++] = command;
        frame[index++] = 0;
        frame[index++] = 0;
        frame[index++] = address;

        payload.CopyTo(frame.AsSpan(index));
        index += payload.Length;

        var checksum = Sum(frame.AsSpan(0, index));
        frame[index++] = (byte)((checksum >> 8) & 0xFF);
        frame[index] = (byte)(checksum & 0xFF);

        return frame;
    }

    public static bool TryParseResponse(ReadOnlySpan<byte> frame, out ERackFrame response)
    {
        response = new ERackFrame(0, 0, Array.Empty<byte>());

        if (frame.Length < FrameOverhead ||
            frame[0] != Header0 ||
            frame[1] != Header1)
        {
            return false;
        }

        var length = (frame[2] << 8) | frame[3];
        var expectedFrameLength = length + 2;
        if (frame.Length < expectedFrameLength)
        {
            return false;
        }

        var packet = frame[..expectedFrameLength];
        var expectedChecksum = (packet[^2] << 8) | packet[^1];
        var actualChecksum = Sum(packet[..^2]);
        if (expectedChecksum != actualChecksum)
        {
            return false;
        }

        var payloadLength = length - LengthFieldBase;
        if (payloadLength < 0)
        {
            return false;
        }

        var payload = payloadLength == 0
            ? Array.Empty<byte>()
            : packet.Slice(8, payloadLength).ToArray();

        response = new ERackFrame(packet[4], packet[7], payload);
        return true;
    }

    private static int Sum(ReadOnlySpan<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum += value;
        }

        return sum;
    }
}
