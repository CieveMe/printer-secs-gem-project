using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using Secs4Net;
using static Secs4Net.Item;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsEventMessageFactory
{
    private readonly SecsEventOptions _options;
    private int _nextDataId;

    public SecsEventMessageFactory(IOptions<SecsEventOptions> options)
    {
        _options = options.Value;
        _nextDataId = _options.InitialDataId - 1;
    }

    public SecsMessage CreateTagReadEvent(TagReadEvent tagReadEvent)
    {
        return CreateS6F11(
            "TagReadEvent",
            _options.TagReadCeid,
            _options.TagReadRptid,
            L(
                A(tagReadEvent.ShelfId),
                A(tagReadEvent.LocationId),
                A(tagReadEvent.Tag),
                U1(tagReadEvent.IsLoaded ? (byte)1 : (byte)0),
                U4((uint)tagReadEvent.Timestamp.ToUnixTimeSeconds())));
    }

    public SecsMessage CreateShelfStateEvent(ShelfStateEvent shelfStateEvent)
    {
        return new SecsMessage(6, 21, replyExpected: _options.ActiveEventReplyExpected)
        {
            Name = "ShelfStateChanged",
            SecsItem = L(
                A(shelfStateEvent.ShelfId),
                A(shelfStateEvent.LocationId),
                A(shelfStateEvent.Tag),
                U1(shelfStateEvent.IsLoaded ? (byte)1 : (byte)0),
                U4((uint)shelfStateEvent.Timestamp.ToUnixTimeSeconds()))
        };
    }

    public SecsMessage CreateRfidWriteEvent(RfidWriteEvent rfidWriteEvent)
    {
        var ceid = rfidWriteEvent.ResultCode == 0
            ? _options.RfidWriteCeid
            : _options.RfidWriteFailedCeid;

        return CreateS6F11(
            "RfidWriteResultEvent",
            ceid,
            _options.RfidWriteRptid,
            L(
                A(rfidWriteEvent.ShelfId),
                A(rfidWriteEvent.LocationId),
                A(rfidWriteEvent.Tag),
                U1(rfidWriteEvent.ResultCode),
                A(rfidWriteEvent.Description),
                U4((uint)rfidWriteEvent.Timestamp.ToUnixTimeSeconds())));
    }

    public SecsMessage CreatePrintEvent(PrintEvent printEvent)
    {
        var secsDescription = PrintProtocolResult.GetSecsDescription(printEvent.ResultCode);
        var ceid = printEvent.ResultCode == PrintProtocolResult.Success
            ? _options.PrintCompletedCeid
            : _options.PrintFailedCeid;

        return CreateS6F11(
            "PrintResultEvent",
            ceid,
            _options.PrintRptid,
            L(
                A(printEvent.ShelfId),
                A(printEvent.PrinterId),
                A(printEvent.Content),
                U1(printEvent.ResultCode),
                A(secsDescription),
                U4((uint)printEvent.Timestamp.ToUnixTimeSeconds())));
    }

    private SecsMessage CreateS6F11(string name, uint ceid, uint rptid, Item reportValues)
    {
        return new SecsMessage(6, 11, replyExpected: _options.ActiveEventReplyExpected)
        {
            Name = name,
            SecsItem = L(
                U2(NextDataId()),
                U4(ceid),
                L(
                    L(
                        U4(rptid),
                        reportValues)))
        };
    }

    private ushort NextDataId()
    {
        return unchecked((ushort)Interlocked.Increment(ref _nextDataId));
    }
}
