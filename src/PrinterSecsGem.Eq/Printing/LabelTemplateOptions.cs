namespace PrinterSecsGem.Eq.Printing;

public sealed class LabelTemplateOptions
{
    public bool UseMinimalCompatibleCommands { get; set; }

    public bool ResetPrinterState { get; set; } = true;

    public string Orientation { get; set; } = "Normal";

    public string PrintOrientation { get; set; } = "N";

    public int Dpi { get; set; } = 203;

    public int PrintDarkness { get; set; }

    public int WidthDots { get; set; } = 480;

    public int HeightDots { get; set; } = 320;

    public bool LabelLengthAppliesToAllMedia { get; set; } = true;

    public int LabelTop { get; set; }

    public int LabelShift { get; set; }

    public int TearOffAdjust { get; set; }

    public int LabelHomeX { get; set; }

    public int LabelHomeY { get; set; }

    public int TopTextX { get; set; } = 55;

    public int TopTextY { get; set; } = 35;

    public int TopTextSize { get; set; } = 40;

    public int TopTextHeight { get; set; } = 96;

    public int TopTextWidth { get; set; } = 88;

    public int TopTextBlockWidth { get; set; } = 370;

    public int BarcodeX { get; set; } = 75;

    public int BarcodeY { get; set; } = 95;

    public int BarcodeModuleWidth { get; set; } = 2;

    public int BarcodeHeight { get; set; } = 80;

    public bool BarcodeHumanReadable { get; set; }

    public bool BarcodeHumanReadableAbove { get; set; }

    public bool BarcodePrintCheckDigit { get; set; }

    public bool BarcodeTextEnabled { get; set; } = true;

    public int BarcodeTextX { get; set; } = 120;

    public int BarcodeTextY { get; set; } = 190;

    public string BarcodeTextFont { get; set; } = "0";

    public string BarcodeTextRenderMode { get; set; } = "ZplFont";

    public string BarcodeTextBitmapFontFamily { get; set; } = "Arial";

    public int BarcodeTextBitmapFontSize { get; set; }

    public int BarcodeTextBitmapThreshold { get; set; } = 150;

    public int BarcodeTextSize { get; set; } = 22;

    public int BarcodeTextHeight { get; set; } = 38;

    public int BarcodeTextWidth { get; set; } = 34;
}
