namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed record ERackLocation(
    string LocationId,
    string ShelfId,
    string PortName,
    int BaudRate,
    byte DeviceAddress,
    byte InventoryMode,
    int InventoryWaitTimeMilliseconds,
    int InventoryWaitCount,
    bool KeepPortOpen,
    byte WriteTagStartPage,
    int WriteTagWaitTimeMilliseconds,
    int WriteTagWaitCount);
