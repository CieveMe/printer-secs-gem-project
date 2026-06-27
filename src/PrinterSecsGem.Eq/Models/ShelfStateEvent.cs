namespace PrinterSecsGem.Eq.Models;

public sealed record ShelfStateEvent(
    string ShelfId,
    string LocationId,
    string Tag,
    bool IsLoaded,
    DateTimeOffset Timestamp);
