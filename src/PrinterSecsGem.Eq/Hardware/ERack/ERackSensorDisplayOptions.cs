namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackSensorDisplayOptions
{
    public const string SensorPresenceMode = "Sensor";

    public const string RfidPollingPresenceMode = "RfidPolling";

    public bool Enabled { get; set; }

    public string PresenceMode { get; set; } = SensorPresenceMode;

    public int PollIntervalMilliseconds { get; set; } = 500;

    public int RfidPollingReadTimeoutMilliseconds { get; set; } = 700;

    public int RfidPollingEmptyConfirmCount { get; set; } = 3;

    public byte SensorCommand { get; set; } = ERackCommand.GetSensorState;

    public int SensorPayloadIndex { get; set; } = 1;

    public byte CheckLevel { get; set; }

    public int SensorWaitTimeMilliseconds { get; set; } = 5;

    public int SensorWaitCount { get; set; } = 6;

    public int DisplayWaitTimeMilliseconds { get; set; } = 20;

    public int DisplayMinWaitCount { get; set; } = 5;

    public int DisplayMaxBytes { get; set; } = 512;

    public int ReadLengthBytes { get; set; } = 32;

    public string NoIdDisplayText { get; set; } = "NO ID  ";

    public int NoIdFailureCode { get; set; } = 2001;

    public bool UpdateDisplayOnEveryPoll { get; set; }

    public string NormalizedPresenceMode
    {
        get
        {
            var mode = PresenceMode?.Trim();
            return mode != null && mode.Equals(RfidPollingPresenceMode, StringComparison.OrdinalIgnoreCase)
                ? RfidPollingPresenceMode
                : SensorPresenceMode;
        }
    }

    public bool IsRfidPollingMode =>
        NormalizedPresenceMode.Equals(RfidPollingPresenceMode, StringComparison.OrdinalIgnoreCase);

    public bool HasKnownPresenceMode
    {
        get
        {
            var mode = PresenceMode?.Trim();
            return string.IsNullOrWhiteSpace(mode) ||
                mode.Equals(SensorPresenceMode, StringComparison.OrdinalIgnoreCase) ||
                mode.Equals(RfidPollingPresenceMode, StringComparison.OrdinalIgnoreCase);
        }
    }
}
