namespace PrinterSecsGem.Eq.Models;

public sealed record OperationResult(bool Success, byte Code, string Description)
{
    public static OperationResult Ok(string description = "success") => new(true, 0, description);

    public static OperationResult Fail(byte code, string description) => new(false, code, description);
}
