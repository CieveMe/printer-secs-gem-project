namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed record ERackSensorStateResult(
    bool Success,
    byte Code,
    string Description,
    ERackLocation Location,
    byte Command,
    byte[] Payload,
    bool IsLoaded)
{
    public static ERackSensorStateResult Ok(
        ERackLocation location,
        byte command,
        byte[] payload,
        bool isLoaded)
    {
        return new ERackSensorStateResult(true, 0, "sensor state read", location, command, payload, isLoaded);
    }

    public static ERackSensorStateResult Fail(
        ERackLocation location,
        byte command,
        byte code,
        string description)
    {
        return new ERackSensorStateResult(false, code, description, location, command, Array.Empty<byte>(), false);
    }
}
