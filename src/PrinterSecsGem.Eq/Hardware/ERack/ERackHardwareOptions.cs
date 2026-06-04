namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackHardwareOptions
{
    public bool Enabled { get; set; }

    public string PortName { get; set; } = "COM1";

    public int BaudRate { get; set; } = 57600;

    public byte DeviceAddress { get; set; } = 1;

    public byte InventoryMode { get; set; } = 4;

    public int InventoryWaitTimeMilliseconds { get; set; } = 50;

    public int InventoryWaitCount { get; set; } = 20;

    public bool KeepPortOpen { get; set; } = true;

    public byte WriteTagStartPage { get; set; }

    public int WriteTagWaitTimeMilliseconds { get; set; } = 200;

    public int WriteTagWaitCount { get; set; } = 20;

    public string DefaultShelfId { get; set; } = "SHELF001";

    public string DefaultLocationId { get; set; } = "LOC001";
}
