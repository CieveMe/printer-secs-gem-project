namespace PrinterSecsGem.Eq.Models;

public sealed record PrintCommand(string ShelfId, string PrinterId, string Content, byte Copies);
