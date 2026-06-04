using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Printing;

public sealed class ZebraCommandLinePrinterGateway : IPrinterGateway
{
    private readonly PrinterOptions _options;
    private readonly ZplLabelTemplate _template;
    private readonly ILogger<ZebraCommandLinePrinterGateway> _logger;

    public ZebraCommandLinePrinterGateway(
        IOptions<PrinterOptions> options,
        ZplLabelTemplate template,
        ILogger<ZebraCommandLinePrinterGateway> logger)
    {
        _options = options.Value;
        _template = template;
        _logger = logger;
    }

    public async Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return OperationResult.Fail(3, "print content is empty");
        }

        var copies = Math.Max(1, (int)command.Copies);
        var zpl = _template.Create(command.Content, copies);
        var zplFilePath = await WriteZplFileAsync(command.Content, zpl, cancellationToken);
        var printerAddress = string.IsNullOrWhiteSpace(_options.ZebraPrinterAddress)
            ? await DiscoverUsbPrinterAsync(cancellationToken)
            : _options.ZebraPrinterAddress.Trim();

        if (string.IsNullOrWhiteSpace(printerAddress))
        {
            return OperationResult.Fail(4, "zebra usb printer was not discovered");
        }

        var connectionOption = GetConnectionOption(_options.ZebraConnectionType);
        var result = await RunZebraCommandAsync(
            new[]
            {
                "send",
                printerAddress,
                zplFilePath,
                connectionOption,
                "--verbose"
            },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError(
                "Zebra SDK send failed: exitCode={ExitCode}, stdout={StdOut}, stderr={StdErr}",
                result.ExitCode,
                result.StdOut,
                result.StdErr);

            return OperationResult.Fail(5, $"zebra send failed: {result.StdErr}{result.StdOut}");
        }

        _logger.LogInformation(
            "Sent ZPL through Zebra SDK: printer={PrinterAddress}, content={Content}, copies={Copies}, file={FilePath}",
            printerAddress,
            command.Content,
            copies,
            zplFilePath);

        return OperationResult.Ok($"zpl sent to zebra printer: {printerAddress}");
    }

    private async Task<string> WriteZplFileAsync(string content, string zpl, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.OutputDirectory);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{SanitizeFileName(content)}.zpl";
        var filePath = Path.Combine(_options.OutputDirectory, fileName);
        await File.WriteAllTextAsync(filePath, zpl, cancellationToken);
        return filePath;
    }

    private async Task<string> DiscoverUsbPrinterAsync(CancellationToken cancellationToken)
    {
        var result = await RunZebraCommandAsync(
            new[]
            {
                "discover",
                "--usb"
            },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning(
                "Zebra USB discovery failed: exitCode={ExitCode}, stdout={StdOut}, stderr={StdErr}",
                result.ExitCode,
                result.StdOut,
                result.StdErr);
            return string.Empty;
        }

        return result.StdOut
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(IsUsbPrinterAddress) ?? string.Empty;
    }

    private async Task<ProcessResult> RunZebraCommandAsync(string[] zebraArgs, CancellationToken cancellationToken)
    {
        var commandPath = ResolveZebraCommandPath();
        var useExecutable = Path.GetExtension(commandPath).Equals(".exe", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = useExecutable ? commandPath : _options.DotnetExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(commandPath) ?? AppContext.BaseDirectory
        };
        startInfo.Environment["DOTNET_ROLL_FORWARD"] = "Major";
        if (!useExecutable)
        {
            startInfo.ArgumentList.Add(commandPath);
        }

        foreach (var arg in zebraArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Zebra SDK command.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        return new ProcessResult(process.ExitCode, stdOut.Trim(), stdErr.Trim());
    }

    private string ResolveZebraCommandPath()
    {
        var configuredPath = ResolvePath(_options.ZebraCommandLineAssembly);
        if (File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var configuredDirectory = Path.GetDirectoryName(configuredPath);
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            var configuredExePath = Path.Combine(configuredDirectory, "ZSDK.API.DESKTOP.exe");
            if (File.Exists(configuredExePath))
            {
                return configuredExePath;
            }
        }

        throw new FileNotFoundException(
            "Zebra command line SDK was not found. Put zebra-command-line beside PrinterSecsGem.Eq.exe or update Printer:ZebraCommandLineAssembly.",
            configuredPath);
    }

    private static string GetConnectionOption(string connectionType)
    {
        return connectionType.Trim().ToLowerInvariant() switch
        {
            "driver" => "--driver",
            "usb" => "--usb",
            _ => "--usb"
        };
    }

    private static bool IsUsbPrinterAddress(string line)
    {
        return line.StartsWith("USB", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith(@"\\?\usb", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var fileName = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(fileName) ? "label" : fileName;
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
