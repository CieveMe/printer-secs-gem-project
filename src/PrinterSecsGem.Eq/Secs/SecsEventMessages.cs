using PrinterSecsGem.Eq.Models;
using Secs4Net;
using static Secs4Net.Item;

namespace PrinterSecsGem.Eq.Secs;

public static class SecsEventMessages
{
    public static SecsMessage CreateTagReadEvent(TagReadEvent tagReadEvent)
    {
        return new SecsMessage(6, 11, replyExpected: false)
        {
            Name = "TagReadEvent",
            SecsItem = L(
                A(tagReadEvent.ShelfId),
                A(tagReadEvent.LocationId),
                A(tagReadEvent.Tag),
                U1(tagReadEvent.IsLoaded ? (byte)1 : (byte)0),
                U4((uint)tagReadEvent.Timestamp.ToUnixTimeSeconds()))
        };
    }

    public static SecsMessage CreateShelfStatusChangedEvent(TagReadEvent tagReadEvent)
    {
        return new SecsMessage(6, 21, replyExpected: false)
        {
            Name = "ShelfStatusChanged",
            SecsItem = L(
                A(tagReadEvent.ShelfId),
                A(tagReadEvent.LocationId),
                A(tagReadEvent.Tag),
                U1(tagReadEvent.IsLoaded ? (byte)1 : (byte)0),
                U4((uint)tagReadEvent.Timestamp.ToUnixTimeSeconds()))
        };
    }
}
