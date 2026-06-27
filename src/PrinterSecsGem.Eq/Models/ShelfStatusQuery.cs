namespace PrinterSecsGem.Eq.Models;

public sealed record ShelfStatusQuery(string ShelfId, string LocationId, int ReadLengthBytes = 32);
