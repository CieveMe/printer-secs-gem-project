namespace PrinterSecsGem.Eq.Printing;

public sealed class LabelTemplateOptions
{
    public int WidthDots { get; set; } = 800;

    public int HeightDots { get; set; } = 500;

    public int TopTextX { get; set; } = 170;

    public int TopTextY { get; set; } = 80;

    public int TopTextHeight { get; set; } = 120;

    public int TopTextWidth { get; set; } = 120;

    public int BarcodeX { get; set; } = 130;

    public int BarcodeY { get; set; } = 230;

    public int BarcodeModuleWidth { get; set; } = 4;

    public int BarcodeHeight { get; set; } = 150;
}
