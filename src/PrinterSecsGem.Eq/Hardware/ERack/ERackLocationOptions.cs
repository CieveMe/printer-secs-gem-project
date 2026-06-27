namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackLocationOptions
{
    public string LocationId { get; set; } = string.Empty;

    public string ShelfId { get; set; } = string.Empty;

    public string PortName { get; set; } = string.Empty;

    public int? BaudRate { get; set; }

    public byte? DeviceAddress { get; set; }

    public byte? InventoryMode { get; set; }

    public int? InventoryWaitTimeMilliseconds { get; set; }

    public int? InventoryWaitCount { get; set; }

    public bool? KeepPortOpen { get; set; }

    public byte? WriteTagStartPage { get; set; }

    public int? WriteTagWaitTimeMilliseconds { get; set; }

    public int? WriteTagWaitCount { get; set; }
}
