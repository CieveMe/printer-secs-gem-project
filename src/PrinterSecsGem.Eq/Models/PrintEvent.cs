namespace PrinterSecsGem.Eq.Models;

public sealed record PrintEvent(
    string ShelfId,
    string PrinterId,
    string Content,
    byte ResultCode,
    string Description,
    DateTimeOffset Timestamp);
