namespace PrinterSecsGem.Eq.Models;

public sealed record ShelfLocationStatus(string LocationId, string Tag, bool IsLoaded);

public sealed record ShelfStatusResult(
    bool Success,
    string ShelfId,
    IReadOnlyList<ShelfLocationStatus> Locations,
    byte Code,
    string Description)
{
    public static ShelfStatusResult Ok(string shelfId, IReadOnlyList<ShelfLocationStatus> locations) =>
        new(true, shelfId, locations, 0, "success");

    public static ShelfStatusResult Fail(string shelfId, byte code, string description) =>
        new(false, shelfId, Array.Empty<ShelfLocationStatus>(), code, description);
}
