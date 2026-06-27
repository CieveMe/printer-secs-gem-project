namespace PrinterSecsGem.Eq.ErackNetwork;

internal static class ERackWireMessageTypes
{
    public const string RegisterUnit = "RegisterUnit";
    public const string RegisterUnitResponse = "RegisterUnitResponse";
    public const string Heartbeat = "Heartbeat";
    public const string HeartbeatResponse = "HeartbeatResponse";
    public const string ReadShelfStatus = "ReadShelfStatus";
    public const string ReadShelfStatusResponse = "ReadShelfStatusResponse";
    public const string WriteRfid = "WriteRfid";
    public const string WriteRfidResponse = "WriteRfidResponse";
    public const string Print = "Print";
    public const string PrintResponse = "PrintResponse";
    public const string ShelfStateChanged = "ShelfStateChanged";
    public const string RfidWriteEvent = "RfidWriteEvent";
    public const string PrintEvent = "PrintEvent";
    public const string ErrorResponse = "ErrorResponse";
}
