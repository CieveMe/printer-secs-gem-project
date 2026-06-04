namespace PrinterSecsGem.Eq.Printing;

public sealed class LabelTemplateOptions
{
    public bool ResetPrinterState { get; set; } = true;

    public string Orientation { get; set; } = "Normal";

    public string PrintOrientation { get; set; } = "N";

    public int Dpi { get; set; } = 203;

    public int WidthDots { get; set; } = 508;

    public int HeightDots { get; set; } = 320;

    public bool LabelLengthAppliesToAllMedia { get; set; } = true;

    public int LabelTop { get; set; }

    public int LabelShift { get; set; }

    public int TearOffAdjust { get; set; }

    public int LabelHomeX { get; set; }

    public int LabelHomeY { get; set; }

    public int TopTextX { get; set; } = 0;

    public int TopTextY { get; set; } = 42;

    public int TopTextHeight { get; set; } = 96;

    public int TopTextWidth { get; set; } = 88;

    public int TopTextBlockWidth { get; set; } = 508;

    public int BarcodeX { get; set; } = 66;

    public int BarcodeY { get; set; } = 156;

    public int BarcodeModuleWidth { get; set; } = 3;

    public int BarcodeHeight { get; set; } = 90;

    public bool BarcodeHumanReadable { get; set; } = true;

    public bool BarcodeHumanReadableAbove { get; set; }

    public bool BarcodePrintCheckDigit { get; set; }
}
