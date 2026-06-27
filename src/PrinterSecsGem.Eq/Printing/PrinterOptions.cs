namespace PrinterSecsGem.Eq.Printing;

public sealed class PrinterOptions
{
    public bool RealPrintEnabled { get; set; }

    public string Mode { get; set; } = "File";

    public string OutputDirectory { get; set; } = "output/zpl";

    public string DefaultPrinterId { get; set; } = "PRINTER001";

    public string ZebraCommandLineAssembly { get; set; } = "zebra-command-line/SdkApi.Desktop.CommandLine.dll";

    public string ZebraConnectionType { get; set; } = "Usb";

    public string ZebraPrinterAddress { get; set; } = string.Empty;

    public bool ZebraPreflightStatusEnabled { get; set; }

    public int ZebraCommandTimeoutMilliseconds { get; set; } = 10000;

    public string DotnetExecutable { get; set; } = "dotnet";
}
