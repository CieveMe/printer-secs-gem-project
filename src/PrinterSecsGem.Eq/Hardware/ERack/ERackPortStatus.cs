namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed record ERackPortStatus(
    bool Enabled,
    string PortName,
    int BaudRate,
    bool KeepPortOpen,
    bool IsOpen,
    string Description);
