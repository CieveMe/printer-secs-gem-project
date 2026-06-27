namespace PrinterSecsGem.Eq.StatusUi;

public sealed class StatusUiOptions
{
    public string Language { get; set; } = "zh-CN";

    public bool IsChinese =>
        Language.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
        Language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
        Language.Equals("cn", StringComparison.OrdinalIgnoreCase);
}
