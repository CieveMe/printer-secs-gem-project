namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsEventOptions
{
    public bool ActiveReportsEnabled { get; set; } = true;

    public bool ActiveEventReplyExpected { get; set; }

    public ushort InitialDataId { get; set; } = 1;

    public uint TagReadCeid { get; set; } = 1001;

    public uint TagReadRptid { get; set; } = 2001;

    public uint RfidWriteCeid { get; set; } = 2002;

    public uint RfidWriteFailedCeid { get; set; } = 2005;

    public uint RfidWriteRptid { get; set; } = 2002;

    public uint PrintCompletedCeid { get; set; } = 2003;

    public uint PrintFailedCeid { get; set; } = 2004;

    public uint PrintRptid { get; set; } = 2003;

    public uint ShelfIdVid { get; set; } = 3001;

    public uint LocationIdVid { get; set; } = 3002;

    public uint TagVid { get; set; } = 3003;

    public uint IsLoadedVid { get; set; } = 3004;

    public uint TimestampVid { get; set; } = 3005;
}
