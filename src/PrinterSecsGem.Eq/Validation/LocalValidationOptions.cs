namespace PrinterSecsGem.Eq.Validation;

public sealed class LocalValidationOptions
{
    public string ShelfId { get; set; } = "SHELF001";

    public string LocationId { get; set; } = "LOC001";

    public string PrinterId { get; set; } = "PRINTER001";

    public string Content { get; set; } = "EFS08IZS";

    public byte Copies { get; set; } = 1;
}
