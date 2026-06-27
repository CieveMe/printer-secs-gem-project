namespace PrinterSecsGem.Eq.Models;

public sealed record RfidWriteEvent(
    string ShelfId,
    string LocationId,
    string Tag,
    byte ResultCode,
    string Description,
    DateTimeOffset Timestamp);
