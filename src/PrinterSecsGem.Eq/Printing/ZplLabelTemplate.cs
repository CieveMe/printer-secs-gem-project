using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PrinterSecsGem.Eq.Printing;

public sealed class ZplLabelTemplate
{
    private readonly LabelTemplateOptions _startupOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ZplLabelTemplate> _logger;

    public ZplLabelTemplate(
        IOptions<LabelTemplateOptions> options,
        IHostEnvironment environment,
        ILogger<ZplLabelTemplate> logger)
    {
        _startupOptions = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public string Create(string content, int copies = 1)
    {
        var options = LoadCurrentOptions(out var configSources);
        var safeContent = EscapeZpl(content.Trim());
        var safeCopies = Math.Max(1, copies);

        _logger.LogInformation(
            "Using label template config: sources={ConfigSources}, minimal={UseMinimal}, darkness={PrintDarkness}, width={Width}, height={Height}, topText=({TopX},{TopY},{TopHeight},{TopWidth}), barcode=({BarcodeX},{BarcodeY},module={Module},height={BarcodeHeight}), barcodeText=({BarcodeTextX},{BarcodeTextY},mode={BarcodeTextMode},font={BarcodeTextFont},bitmapFont={BitmapFont},bitmapSize={BitmapSize},threshold={BitmapThreshold},{BarcodeTextHeight},{BarcodeTextWidth})",
            configSources,
            options.UseMinimalCompatibleCommands,
            options.PrintDarkness,
            options.WidthDots,
            options.HeightDots,
            options.TopTextX,
            options.TopTextY,
            GetTopTextHeight(options),
            GetTopTextWidth(options),
            options.BarcodeX,
            options.BarcodeY,
            options.BarcodeModuleWidth,
            options.BarcodeHeight,
            options.BarcodeTextX,
            options.BarcodeTextY,
            NormalizeRenderMode(options.BarcodeTextRenderMode),
            NormalizeFontName(options.BarcodeTextFont),
            options.BarcodeTextBitmapFontFamily,
            GetBarcodeTextBitmapFontSize(options),
            options.BarcodeTextBitmapThreshold,
            GetBarcodeTextHeight(options),
            GetBarcodeTextWidth(options));

        if (options.UseMinimalCompatibleCommands)
        {
            return CreateMinimalCompatible(options, safeContent, safeCopies);
        }

        return IsRotateClockwise(options.Orientation)
            ? CreateRotateClockwise(options, safeContent, safeCopies)
            : CreateNormal(options, safeContent, safeCopies);
    }

    private string CreateMinimalCompatible(LabelTemplateOptions options, string safeContent, int safeCopies)
    {
        var barcodePrintCheckDigit = ToZplFlag(options.BarcodePrintCheckDigit);
        var barcodeText = CreateBarcodeTextCommand(options, safeContent);
        var printDarknessCommand = CreatePrintDarknessCommand(options);

        return $"""
            ^XA
            {printDarknessCommand}
            ^FO{options.TopTextX},{options.TopTextY}^A0N,{GetTopTextHeight(options)},{GetTopTextWidth(options)}^FD{safeContent}^FS
            ^FO{options.BarcodeX},{options.BarcodeY}^BY{options.BarcodeModuleWidth}
            ^BCN,{options.BarcodeHeight},N,N,{barcodePrintCheckDigit}
            ^FD{safeContent}^FS
            {barcodeText}
            ^PQ{safeCopies}
            ^XZ
            """;
    }

    private string CreateNormal(LabelTemplateOptions options, string safeContent, int safeCopies)
    {
        var resetCommands = CreateResetCommands(options);
        var printOrientationCommand = CreatePrintOrientationCommand(options);
        var labelLengthCommand = CreateLabelLengthCommand(options, options.HeightDots);
        var barcodeHumanReadable = ToZplFlag(options.BarcodeHumanReadable);
        var barcodeHumanReadableAbove = ToZplFlag(options.BarcodeHumanReadableAbove);
        var barcodePrintCheckDigit = ToZplFlag(options.BarcodePrintCheckDigit);
        var barcodeText = CreateBarcodeTextCommand(options, safeContent);
        var printDarknessCommand = CreatePrintDarknessCommand(options);

        return $"""
            ^XA
            ^CI28
            {printDarknessCommand}
            {resetCommands}
            {printOrientationCommand}
            ^PW{options.WidthDots}
            {labelLengthCommand}
            ^LT{options.LabelTop}
            ^LS{options.LabelShift}
            ^LH{options.LabelHomeX},{options.LabelHomeY}
            ^FO{options.TopTextX},{options.TopTextY}^A0N,{GetTopTextHeight(options)},{GetTopTextWidth(options)}^FB{options.TopTextBlockWidth},1,0,C,0^FD{safeContent}^FS
            ^FO{options.BarcodeX},{options.BarcodeY}^BY{options.BarcodeModuleWidth}
            ^BCN,{options.BarcodeHeight},{barcodeHumanReadable},{barcodeHumanReadableAbove},{barcodePrintCheckDigit}
            ^FD{safeContent}^FS
            {barcodeText}
            ^PQ{safeCopies}
            ^XZ
            """;
    }

    private string CreateRotateClockwise(LabelTemplateOptions options, string safeContent, int safeCopies)
    {
        var resetCommands = CreateResetCommands(options);
        var printOrientationCommand = CreatePrintOrientationCommand(options);
        var barcodeHumanReadable = ToZplFlag(options.BarcodeHumanReadable);
        var barcodeHumanReadableAbove = ToZplFlag(options.BarcodeHumanReadableAbove);
        var barcodePrintCheckDigit = ToZplFlag(options.BarcodePrintCheckDigit);
        var barcodeText = CreateBarcodeTextCommand(options, safeContent);
        var physicalWidth = options.HeightDots;
        var physicalLength = options.WidthDots;
        var labelLengthCommand = CreateLabelLengthCommand(options, physicalLength);
        var topTextX = RotateClockwiseX(options, options.TopTextY, GetTopTextHeight(options));
        var topTextY = options.TopTextX;
        var barcodeX = RotateClockwiseX(options, options.BarcodeY, options.BarcodeHeight);
        var barcodeY = options.BarcodeX;
        var printDarknessCommand = CreatePrintDarknessCommand(options);

        return $"""
            ^XA
            ^CI28
            {printDarknessCommand}
            {resetCommands}
            {printOrientationCommand}
            ^PW{physicalWidth}
            {labelLengthCommand}
            ^LT{options.LabelTop}
            ^LS{options.LabelShift}
            ^LH{options.LabelHomeX},{options.LabelHomeY}
            ^FO{topTextX},{topTextY}^A0R,{GetTopTextHeight(options)},{GetTopTextWidth(options)}^FD{safeContent}^FS
            ^FO{barcodeX},{barcodeY}^BY{options.BarcodeModuleWidth}
            ^BCR,{options.BarcodeHeight},{barcodeHumanReadable},{barcodeHumanReadableAbove},{barcodePrintCheckDigit}
            ^FD{safeContent}^FS
            {barcodeText}
            ^PQ{safeCopies}
            ^XZ
            """;
    }

    private string CreateBarcodeTextCommand(LabelTemplateOptions options, string safeContent)
    {
        if (!options.BarcodeTextEnabled)
        {
            return string.Empty;
        }

        if (NormalizeRenderMode(options.BarcodeTextRenderMode) == "Bitmap")
        {
            return CreateBarcodeTextBitmapCommand(options, safeContent);
        }

        var fontCommand = CreateFontCommand(options.BarcodeTextFont, "N", GetBarcodeTextHeight(options), GetBarcodeTextWidth(options));
        return $"^FO{options.BarcodeTextX},{options.BarcodeTextY}{fontCommand}^FD{safeContent}^FS";
    }

    private string CreateBarcodeTextBitmapCommand(LabelTemplateOptions options, string safeContent)
    {
        var zplGraphic = CreateTextGraphic(
            safeContent,
            options.BarcodeTextBitmapFontFamily,
            GetBarcodeTextBitmapFontSize(options),
            options.BarcodeTextBitmapThreshold);
        return $"^FO{options.BarcodeTextX},{options.BarcodeTextY}{zplGraphic}^FS";
    }

    private int RotateClockwiseX(LabelTemplateOptions options, int logicalY, int logicalHeight)
    {
        return Math.Max(0, options.HeightDots - logicalY - logicalHeight);
    }

    private static bool IsRotateClockwise(string value)
    {
        return value.Equals("RotateClockwise", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Clockwise", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("R", StringComparison.OrdinalIgnoreCase);
    }

    private string CreateResetCommands(LabelTemplateOptions options)
    {
        return options.ResetPrinterState
            ? $"""
                ^LH0,0
                ^LT0
                ^LS0
                ^PON
                ^TA{options.TearOffAdjust}
                """
            : string.Empty;
    }

    private string CreateLabelLengthCommand(LabelTemplateOptions options, int labelLength)
    {
        return options.LabelLengthAppliesToAllMedia
            ? $"^LL{labelLength},Y"
            : $"^LL{labelLength}";
    }

    private string CreatePrintOrientationCommand(LabelTemplateOptions options)
    {
        var orientation = NormalizePrintOrientation(options.PrintOrientation);
        return options.ResetPrinterState && orientation == "N"
            ? string.Empty
            : $"^PO{orientation}";
    }

    private static string CreatePrintDarknessCommand(LabelTemplateOptions options)
    {
        if (options.PrintDarkness == 0)
        {
            return string.Empty;
        }

        var darkness = Math.Clamp(options.PrintDarkness, -30, 30);
        return $"^MD{darkness}";
    }

    private LabelTemplateOptions LoadCurrentOptions(out string configSources)
    {
        var options = CloneOptions(_startupOptions);
        var appliedConfigPaths = new List<string>();

        foreach (var configPath in GetAppConfigPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(configPath))
            {
                continue;
            }

            try
            {
                ApplyAppConfigOverrides(options, configPath);
                appliedConfigPaths.Add(configPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                _logger.LogWarning(ex, "Failed to reload label template config from {ConfigPath}; startup values will be used for unreadable keys.", configPath);
            }
        }

        configSources = appliedConfigPaths.Count == 0
            ? "<startup-defaults-only>"
            : string.Join(" | ", appliedConfigPaths);

        return options;
    }

    private IEnumerable<string> GetAppConfigPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "PrinterSecsGem.Eq.dll.config");
        yield return Path.Combine(AppContext.BaseDirectory, "PrinterSecsGem.Eq.exe.config");
        yield return Path.Combine(_environment.ContentRootPath, "PrinterSecsGem.Eq.dll.config");
        yield return Path.Combine(_environment.ContentRootPath, "PrinterSecsGem.Eq.exe.config");
        yield return Path.Combine(AppContext.BaseDirectory, "App.config");
        yield return Path.Combine(_environment.ContentRootPath, "App.config");
    }

    private static void ApplyAppConfigOverrides(LabelTemplateOptions options, string configPath)
    {
        var document = XDocument.Load(configPath);
        var appSettings = document.Root?.Element("appSettings");
        if (appSettings is null)
        {
            return;
        }

        var configuredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var add in appSettings.Elements("add"))
        {
            var key = add.Attribute("key")?.Value;
            if (string.IsNullOrWhiteSpace(key) ||
                !key.StartsWith("LabelTemplate:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var propertyName = key["LabelTemplate:".Length..];
            configuredKeys.Add(propertyName);
            SetOptionValue(options, propertyName, add.Attribute("value")?.Value);
        }

        if (configuredKeys.Contains(nameof(LabelTemplateOptions.TopTextHeight)) ||
            configuredKeys.Contains(nameof(LabelTemplateOptions.TopTextWidth)))
        {
            options.TopTextSize = 0;
        }

        if (configuredKeys.Contains(nameof(LabelTemplateOptions.BarcodeTextHeight)) ||
            configuredKeys.Contains(nameof(LabelTemplateOptions.BarcodeTextWidth)))
        {
            options.BarcodeTextSize = 0;
        }
    }

    private static void SetOptionValue(LabelTemplateOptions options, string propertyName, string? value)
    {
        if (value is null)
        {
            return;
        }

        var property = typeof(LabelTemplateOptions).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(string))
        {
            property.SetValue(options, value);
            return;
        }

        if (property.PropertyType == typeof(int) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            property.SetValue(options, intValue);
            return;
        }

        if (property.PropertyType == typeof(bool) &&
            bool.TryParse(value, out var boolValue))
        {
            property.SetValue(options, boolValue);
        }
    }

    private static LabelTemplateOptions CloneOptions(LabelTemplateOptions source)
    {
        var target = new LabelTemplateOptions();
        foreach (var property in typeof(LabelTemplateOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead && property.CanWrite)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }

        return target;
    }

    private static int GetTopTextHeight(LabelTemplateOptions options)
    {
        return options.TopTextSize > 0 ? options.TopTextSize : options.TopTextHeight;
    }

    private static int GetTopTextWidth(LabelTemplateOptions options)
    {
        return options.TopTextSize > 0 ? options.TopTextSize : options.TopTextWidth;
    }

    private static int GetBarcodeTextHeight(LabelTemplateOptions options)
    {
        return options.BarcodeTextSize > 0 ? options.BarcodeTextSize : options.BarcodeTextHeight;
    }

    private static int GetBarcodeTextWidth(LabelTemplateOptions options)
    {
        return options.BarcodeTextSize > 0 ? options.BarcodeTextSize : options.BarcodeTextWidth;
    }

    private static int GetBarcodeTextBitmapFontSize(LabelTemplateOptions options)
    {
        return options.BarcodeTextBitmapFontSize > 0
            ? options.BarcodeTextBitmapFontSize
            : GetBarcodeTextHeight(options);
    }

    private static string CreateTextGraphic(string text, string fontFamily, int fontSize, int threshold)
    {
        var safeFontSize = Math.Clamp(fontSize, 8, 128);
        var safeThreshold = Math.Clamp(threshold, 1, 254);
        using var font = new Font(
            string.IsNullOrWhiteSpace(fontFamily) ? "Arial" : fontFamily,
            safeFontSize,
            FontStyle.Regular,
            GraphicsUnit.Pixel);

        using var measureBitmap = new Bitmap(1, 1);
        using var measureGraphics = Graphics.FromImage(measureBitmap);
        measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var measured = measureGraphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
        var width = Math.Max(1, (int)Math.Ceiling(measured.Width) + 8);
        var height = Math.Max(1, (int)Math.Ceiling(measured.Height) + 8);

        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.DrawString(text, font, Brushes.Black, new PointF(4, 4), StringFormat.GenericTypographic);
        }

        var bounds = FindInkBounds(bitmap, safeThreshold);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return "^GFA,0,0,0,";
        }

        return CreateGraphicField(bitmap, bounds, safeThreshold);
    }

    private static Rectangle FindInkBounds(Bitmap bitmap, int threshold)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!IsInk(bitmap.GetPixel(x, y), threshold))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? Rectangle.Empty
            : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static string CreateGraphicField(Bitmap bitmap, Rectangle bounds, int threshold)
    {
        var bytesPerRow = (bounds.Width + 7) / 8;
        var totalBytes = bytesPerRow * bounds.Height;
        var hex = new StringBuilder(totalBytes * 2);

        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (var byteIndex = 0; byteIndex < bytesPerRow; byteIndex++)
            {
                var value = 0;
                for (var bit = 0; bit < 8; bit++)
                {
                    var x = bounds.Left + byteIndex * 8 + bit;
                    if (x < bounds.Right && IsInk(bitmap.GetPixel(x, y), threshold))
                    {
                        value |= 1 << (7 - bit);
                    }
                }

                hex.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hex}";
    }

    private static bool IsInk(Color color, int threshold)
    {
        var brightness = (color.R + color.G + color.B) / 3;
        return brightness < threshold;
    }

    private static string CreateFontCommand(string fontName, string orientation, int height, int width)
    {
        return $"^A{NormalizeFontName(fontName)}{orientation},{height},{width}";
    }

    private static string NormalizeRenderMode(string value)
    {
        return value.Equals("Bitmap", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Graphic", StringComparison.OrdinalIgnoreCase)
            ? "Bitmap"
            : "ZplFont";
    }

    private static string NormalizeFontName(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return "0";
        }

        var normalized = fontName.Trim().ToUpperInvariant();
        var ch = normalized[0];
        return char.IsLetterOrDigit(ch) ? ch.ToString() : "0";
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
