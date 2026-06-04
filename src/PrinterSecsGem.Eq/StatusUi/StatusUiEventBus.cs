namespace PrinterSecsGem.Eq.StatusUi;

public static class StatusUiEventCategories
{
    public const string SecsState = "SecsState";
    public const string SecsLog = "SecsLog";
    public const string RfidStatus = "RfidStatus";
    public const string LastPrint = "LastPrint";
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
