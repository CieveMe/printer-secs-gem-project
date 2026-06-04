using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;
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

    private SecsMessage CreateS6F11(string name, uint ceid, uint rptid, Item reportValues)
    {
        return new SecsMessage(6, 11, replyExpected: true)
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
