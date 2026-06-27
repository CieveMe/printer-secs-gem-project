namespace PrinterSecsGem.Eq;

public sealed class ERackSimulationOptions
{
    public bool Enabled { get; set; }

    public string ShelfId { get; set; } = "SHELF001";

    public string LocationId { get; set; } = "LOC001";

    public string Tag { get; set; } = "RFID1234567890";

    public int StartupDelayMilliseconds { get; set; } = 3000;

    public int StepIntervalMilliseconds { get; set; } = 5000;

    public bool Loop { get; set; } = true;
}
