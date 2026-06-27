namespace PrinterSecsGem.Eq;

public sealed class RuntimeOptions
{
    public string Mode { get; set; } = "Unit";

    public bool IsUnitEnabled =>
        Mode.Equals("Unit", StringComparison.OrdinalIgnoreCase) ||
        Mode.Equals("Both", StringComparison.OrdinalIgnoreCase) ||
        !IsKnownMode;

    public bool IsServerEnabled =>
        Mode.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
        Mode.Equals("Both", StringComparison.OrdinalIgnoreCase);

    public string NormalizedMode =>
        IsUnitEnabled && IsServerEnabled
            ? "Both"
            : IsServerEnabled
                ? "Server"
                : "Unit";

    private bool IsKnownMode =>
        Mode.Equals("Unit", StringComparison.OrdinalIgnoreCase) ||
        Mode.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
        Mode.Equals("Both", StringComparison.OrdinalIgnoreCase);
}
