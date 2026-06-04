namespace PrinterSecsGem.Eq.Hardware.ERack;

internal sealed record ERackFrame(byte Command, byte Address, byte[] Payload)
{
    public bool IsSuccessAck => Payload.Length == 1 && Payload[0] == ERackCommand.Success;
}
