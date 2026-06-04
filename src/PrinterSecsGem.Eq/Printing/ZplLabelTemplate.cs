using Microsoft.Extensions.Options;

namespace PrinterSecsGem.Eq.Printing;

public sealed class ZplLabelTemplate
{
    private readonly LabelTemplateOptions _options;

    public ZplLabelTemplate(IOptions<LabelTemplateOptions> options)
    {
        _options = options.Value;
    }

    public string Create(string content, int copies = 1)
    {
        var safeContent = EscapeZpl(content.Trim());
        var safeCopies = Math.Max(1, copies);

        return IsRotateClockwise(_options.Orientation)
            ? CreateRotateClockwise(safeContent, safeCopies)
            : CreateNormal(safeContent, safeCopies);
    }

    private string CreateNormal(string safeContent, int safeCopies)
    {
        var resetCommands = CreateResetCommands();
        var printOrientationCommand = CreatePrintOrientationCommand();
        var labelLengthCommand = CreateLabelLengthCommand(_options.HeightDots);
        var barcodeHumanReadable = ToZplFlag(_options.BarcodeHumanReadable);
        var barcodeHumanReadableAbove = ToZplFlag(_options.BarcodeHumanReadableAbove);
        var barcodePrintCheckDigit = ToZplFlag(_options.BarcodePrintCheckDigit);

        return $"""
            ^XA
            ^CI28
            {resetCommands}
            {printOrientationCommand}
            ^PW{_options.WidthDots}
            {labelLengthCommand}
            ^LT{_options.LabelTop}
            ^LS{_options.LabelShift}
            ^LH{_options.LabelHomeX},{_options.LabelHomeY}
            ^FO{_options.TopTextX},{_options.TopTextY}^A0N,{_options.TopTextHeight},{_options.TopTextWidth}^FB{_options.TopTextBlockWidth},1,0,C,0^FD{safeContent}^FS
            ^FO{_options.BarcodeX},{_options.BarcodeY}^BY{_options.BarcodeModuleWidth}
            ^BCN,{_options.BarcodeHeight},{barcodeHumanReadable},{barcodeHumanReadableAbove},{barcodePrintCheckDigit}
            ^FD{safeContent}^FS
            ^PQ{safeCopies}
            ^XZ
            """;
    }

    private string CreateRotateClockwise(string safeContent, int safeCopies)
    {
        var resetCommands = CreateResetCommands();
        var printOrientationCommand = CreatePrintOrientationCommand();
        var barcodeHumanReadable = ToZplFlag(_options.BarcodeHumanReadable);
        var barcodeHumanReadableAbove = ToZplFlag(_options.BarcodeHumanReadableAbove);
        var barcodePrintCheckDigit = ToZplFlag(_options.BarcodePrintCheckDigit);
        var physicalWidth = _options.HeightDots;
        var physicalLength = _options.WidthDots;
        var labelLengthCommand = CreateLabelLengthCommand(physicalLength);
        var topTextX = RotateClockwiseX(_options.TopTextY, _options.TopTextHeight);
        var topTextY = _options.TopTextX;
        var barcodeX = RotateClockwiseX(_options.BarcodeY, _options.BarcodeHeight);
        var barcodeY = _options.BarcodeX;

        return $"""
            ^XA
            ^CI28
            {resetCommands}
            {printOrientationCommand}
            ^PW{physicalWidth}
            {labelLengthCommand}
            ^LT{_options.LabelTop}
            ^LS{_options.LabelShift}
            ^LH{_options.LabelHomeX},{_options.LabelHomeY}
            ^FO{topTextX},{topTextY}^A0R,{_options.TopTextHeight},{_options.TopTextWidth}^FD{safeContent}^FS
            ^FO{barcodeX},{barcodeY}^BY{_options.BarcodeModuleWidth}
            ^BCR,{_options.BarcodeHeight},{barcodeHumanReadable},{barcodeHumanReadableAbove},{barcodePrintCheckDigit}
            ^FD{safeContent}^FS
            ^PQ{safeCopies}
            ^XZ
            """;
    }

    private int RotateClockwiseX(int logicalY, int logicalHeight)
    {
        return Math.Max(0, _options.HeightDots - logicalY - logicalHeight);
    }

    private static bool IsRotateClockwise(string value)
    {
        return value.Equals("RotateClockwise", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Clockwise", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("R", StringComparison.OrdinalIgnoreCase);
    }

    private string CreateResetCommands()
    {
        return _options.ResetPrinterState
            ? $"""
                ^LH0,0
                ^LT0
                ^LS0
                ^PON
                ^TA{_options.TearOffAdjust}
                """
            : string.Empty;
    }

    private string CreateLabelLengthCommand(int labelLength)
    {
        return _options.LabelLengthAppliesToAllMedia
            ? $"^LL{labelLength},Y"
            : $"^LL{labelLength}";
    }

    private string CreatePrintOrientationCommand()
    {
        var orientation = NormalizePrintOrientation(_options.PrintOrientation);
        return _options.ResetPrinterState && orientation == "N"
            ? string.Empty
            : $"^PO{orientation}";
    }

    private static string NormalizePrintOrientation(string value)
    {
        var orientation = value.Trim().ToUpperInvariant();
        return orientation is "N" or "I" or "R" or "B" ? orientation : "N";
    }

    private static string ToZplFlag(bool value)
    {
        return value ? "Y" : "N";
    }

    private static string EscapeZpl(string value)
    {
        return value
            .Replace("^", string.Empty, StringComparison.Ordinal)
            .Replace("~", string.Empty, StringComparison.Ordinal);
    }
}
