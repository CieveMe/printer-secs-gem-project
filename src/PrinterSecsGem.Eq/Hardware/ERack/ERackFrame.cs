namespace PrinterSecsGem.Eq.Hardware.ERack;

internal sealed record ERackFrame(byte Command, byte Address, byte[] Payload, byte[] RawBytes)
{
    public ERackFrame(byte command, byte address, byte[] payload)
        : this(command, address, payload, Array.Empty<byte>())
    {
    }

    public bool IsSuccessAck => Payload.Length == 1 && Payload[0] == ERackCommand.Success;
}
