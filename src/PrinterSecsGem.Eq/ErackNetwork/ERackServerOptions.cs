namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackServerOptions
{
    public bool Enabled { get; set; }

    public string ListenIp { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 7801;

    public int RequestTimeoutMilliseconds { get; set; } = 60000;
}
