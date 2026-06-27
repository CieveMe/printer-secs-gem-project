namespace PrinterSecsGem.Eq.StatusUi;

public static class StatusUiEventCategories
{
    public const string SecsState = "SecsState";
    public const string SecsLog = "SecsLog";
    public const string RfidStatus = "RfidStatus";
    public const string DisplayStatus = "DisplayStatus";
    public const string LastPrint = "LastPrint";
    public const string ERackServerStatus = "ERackServerStatus";
    public const string ERackUnitClientStatus = "ERackUnitClientStatus";
    public const string ERackRoutesStatus = "ERackRoutesStatus";
    public const string SimulationStatus = "SimulationStatus";
}

public sealed record StatusUiEvent(string Category, string Message);

public sealed class StatusUiEventBus
{
    public event EventHandler<StatusUiEvent>? EventReceived;

    public void Publish(string category, string message)
    {
        EventReceived?.Invoke(this, new StatusUiEvent(category, message));
    }
}
