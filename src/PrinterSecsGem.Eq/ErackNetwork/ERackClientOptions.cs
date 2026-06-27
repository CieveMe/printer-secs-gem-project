namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackClientOptions
{
    public bool Enabled { get; set; }

    public string ServerHost { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = 7801;

    public string UnitId { get; set; } = "UNIT001";

    public string ShelfId { get; set; } = "SHELF001";

    public int ReconnectDelayMilliseconds { get; set; } = 3000;

    public int HeartbeatIntervalMilliseconds { get; set; } = 5000;
}
