using Microsoft.Extensions.Options;

namespace PrinterSecsGem.Eq.Printing;

public sealed class ZplLabelTemplate
{
    private readonly LabelTemplateOptions _options;

    public ZplLabelTemplate(IOptions<LabelTemplateOptions> options)
    {
        _options = options.Value;
    }

    public string Create(string content)
    {
        var safeContent = EscapeZpl(content.Trim());

        return $"""
            ^XA
            ^CI28
            ^PW{_options.WidthDots}
            ^LL{_options.HeightDots}
            ^FO{_options.TopTextX},{_options.TopTextY}^A0N,{_options.TopTextHeight},{_options.TopTextWidth}^FD{safeContent}^FS
            ^FO{_options.BarcodeX},{_options.BarcodeY}^BY{_options.BarcodeModuleWidth}
            ^BCN,{_options.BarcodeHeight},Y,N,N
            ^FD{safeContent}^FS
            ^XZ
            """;
    }

    private static string EscapeZpl(string value)
    {
        return value
            .Replace("^", string.Empty, StringComparison.Ordinal)
            .Replace("~", string.Empty, StringComparison.Ordinal);
    }
}
