namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackSensorDisplayOptions
{
    public bool Enabled { get; set; }

    public int PollIntervalMilliseconds { get; set; } = 500;

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
}
