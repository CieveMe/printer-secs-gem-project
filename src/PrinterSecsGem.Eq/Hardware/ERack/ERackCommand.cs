namespace PrinterSecsGem.Eq.Hardware.ERack;

internal static class ERackCommand
{
    public const byte WriteTagWithResponse = 0x09;
    public const byte InventoryNoResponse = 0x0A;
    public const byte ReadInventoryResult = 0x0B;
    public const byte InventoryWithResponse = 0x0C;
    public const byte St8400Read = 0x0D;
    public const byte SetDisplayAll = 0x14;
    public const byte SetDisplayFlash = 0x15;
    public const byte SetLedState = 0x16;
    public const byte SetConfig = 0x30;
    public const byte GetConfig = 0x40;
    public const byte Restart = 0x68;

    public const byte Success = 0;
}
