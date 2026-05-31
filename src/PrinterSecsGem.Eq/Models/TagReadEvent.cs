namespace PrinterSecsGem.Eq.Models;

public sealed record TagReadEvent(
    string ShelfId,
    string LocationId,
    string Tag,
    bool IsLoaded,
    DateTimeOffset Timestamp);
