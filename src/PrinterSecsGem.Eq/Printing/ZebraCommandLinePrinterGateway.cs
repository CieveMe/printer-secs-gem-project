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
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    private IReadOnlyList<string>? _cachedUsbPrinters;

    public ZebraCommandLinePrinterGateway(
        IOptions<PrinterOptions> options,
        ZplLabelTemplate template,
        ILogger<ZebraCommandLinePrinterGateway> logger)
    {
        _options = options.Value;
        _template = template;
        _logger = logger;
    }

    public void SetPrinterAddress(string printerAddress)
    {
        _options.ZebraPrinterAddress = printerAddress.Trim();
    }

    public async Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return PrintProtocolResult.Fail(PrintProtocolResult.EmptyContent);
        }

        var copies = Math.Max(1, (int)command.Copies);
        var zpl = _template.Create(command.Content, copies);
        var zplFilePath = await WriteZplFileAsync(command.Content, zpl, cancellationToken);
        var printerAddress = await ResolvePrinterAddressAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(printerAddress))
        {
            return PrintProtocolResult.Fail(PrintProtocolResult.PrinterOffline);
        }

        var connectionOption = GetConnectionOption(_options.ZebraConnectionType);
        if (_options.ZebraPreflightStatusEnabled)
        {
            var preflightStatus = await TryDetectBlockingPrinterStatusAsync(
                printerAddress,
                connectionOption,
                treatUnknownFailureAsOffline: false,
                cancellationToken);
            if (preflightStatus is not null)
            {
                return PrintProtocolResult.Fail(preflightStatus.Value);
            }
        }
        else
        {
            _logger.LogDebug("Zebra preflight status query skipped by Printer:ZebraPreflightStatusEnabled=false.");
        }

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

        _logger.LogInformation(
            "Zebra SDK send output: exitCode={ExitCode}, stdout={StdOut}, stderr={StdErr}",
            result.ExitCode,
            result.StdOut,
            result.StdErr);

        if (result.ExitCode != 0)
        {
            _logger.LogError(
                "Zebra SDK send failed: exitCode={ExitCode}, stdout={StdOut}, stderr={StdErr}",
                result.ExitCode,
                result.StdOut,
                result.StdErr);

            var failureStatus = await TryDetectBlockingPrinterStatusAsync(
                printerAddress,
                connectionOption,
                treatUnknownFailureAsOffline: true,
                cancellationToken);
            return PrintProtocolResult.Fail(failureStatus ?? PrintProtocolResult.PrinterOffline);
        }

        _logger.LogInformation(
            "Sent ZPL through Zebra SDK: printer={PrinterAddress}, content={Content}, copies={Copies}, file={FilePath}",
            printerAddress,
            command.Content,
            copies,
            zplFilePath);

        return PrintProtocolResult.Ok();
    }

    private async Task<string> WriteZplFileAsync(string content, string zpl, CancellationToken cancellationToken)
    {
        var outputDirectory = ResolvePath(_options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{SanitizeFileName(content)}.zpl";
        var filePath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
        await File.WriteAllTextAsync(filePath, zpl, cancellationToken);
        return filePath;
    }

    public async Task<IReadOnlyList<string>> DiscoverUsbPrintersAsync(CancellationToken cancellationToken)
    {
        if (_cachedUsbPrinters is { Count: > 0 })
        {
            return _cachedUsbPrinters;
        }

        await _discoveryLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedUsbPrinters is { Count: > 0 })
            {
                return _cachedUsbPrinters;
            }

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
                return Array.Empty<string>();
            }

            var printers = result.StdOut
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(IsUsbPrinterAddress)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (printers.Length > 0)
            {
                _cachedUsbPrinters = printers;
            }

            return printers;
        }
        finally
        {
            _discoveryLock.Release();
        }
    }

    private async Task<string> ResolvePrinterAddressAsync(CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(_options.ZebraPrinterAddress)
            ? (await DiscoverUsbPrintersAsync(cancellationToken)).FirstOrDefault() ?? string.Empty
            : _options.ZebraPrinterAddress.Trim();
    }

    private async Task<ProcessResult> RunZebraCommandAsync(string[] zebraArgs, CancellationToken cancellationToken)
    {
        var commandPath = ResolveZebraCommandPath();
        var useExecutable = Path.GetExtension(commandPath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        var workingDirectory = AppContext.BaseDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = useExecutable ? commandPath : _options.DotnetExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
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

        _logger.LogInformation(
            "Running Zebra SDK command: fileName={FileName}, commandPath={CommandPath}, workingDirectory={WorkingDirectory}, args={Args}",
            startInfo.FileName,
            commandPath,
            startInfo.WorkingDirectory,
            string.Join(" ", startInfo.ArgumentList.Select(QuoteArgument)));

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Zebra SDK command.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(NormalizeCommandTimeout());

        try
        {
            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.WaitForExitAsync(timeout.Token);
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            return new ProcessResult(process.ExitCode, stdOut.Trim(), stdErr.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            TryKill(process);
            var timeoutMilliseconds = NormalizeCommandTimeout();
            _logger.LogWarning(
                "Zebra SDK command timed out after {TimeoutMilliseconds} ms: args={Args}",
                timeoutMilliseconds,
                string.Join(" ", startInfo.ArgumentList.Select(QuoteArgument)));

            return new ProcessResult(-1, string.Empty, $"timeout after {timeoutMilliseconds} ms");
        }
    }

    private async Task<byte?> TryDetectBlockingPrinterStatusAsync(
        string printerAddress,
        string connectionOption,
        bool treatUnknownFailureAsOffline,
        CancellationToken cancellationToken)
    {
        try
        {
            var printerStatus = await QueryPrinterStatusAsync(
                printerAddress,
                connectionOption,
                "--printer",
                cancellationToken);
            var printerStatusCode = MapPrinterStatus(printerStatus, treatUnknownFailureAsOffline: false);
            if (printerStatusCode is not null)
            {
                return printerStatusCode;
            }

            var portStatus = await QueryPrinterStatusAsync(
                printerAddress,
                connectionOption,
                "--portstatus",
                cancellationToken);
            var portStatusCode = MapPrinterStatus(portStatus, treatUnknownFailureAsOffline: false);
            if (portStatusCode is not null)
            {
                return portStatusCode;
            }

            return (printerStatus.ExitCode == 0 && portStatus.ExitCode == 0) || !treatUnknownFailureAsOffline
                ? null
                : PrintProtocolResult.PrinterOffline;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Zebra printer status before/after print command.");
            return null;
        }
    }

    private async Task<ProcessResult> QueryPrinterStatusAsync(
        string printerAddress,
        string connectionOption,
        string statusOption,
        CancellationToken cancellationToken)
    {
        var result = await RunZebraCommandAsync(
            new[]
            {
                "status",
                printerAddress,
                connectionOption,
                statusOption,
                "--verbose"
            },
            cancellationToken);

        _logger.LogInformation(
            "Zebra SDK status output: statusOption={StatusOption}, exitCode={ExitCode}, stdout={StdOut}, stderr={StdErr}",
            statusOption,
            result.ExitCode,
            result.StdOut,
            result.StdErr);

        return result;
    }

    private static byte? MapPrinterStatus(ProcessResult result, bool treatUnknownFailureAsOffline)
    {
        var text = $"{result.StdOut}\n{result.StdErr}".ToLowerInvariant();
        if (ContainsAny(text, "paper out", "out of paper", "media out", "out of media", "no media", "缺纸"))
        {
            return PrintProtocolResult.PaperOut;
        }

        if (ContainsAny(
                text,
                "offline",
                "not connected",
                "unable to connect",
                "cannot connect",
                "failed to connect",
                "connectionexception",
                "connection exception",
                "timed out",
                "timeout",
                "printer not found",
                "no printer"))
        {
            return PrintProtocolResult.PrinterOffline;
        }

        return result.ExitCode == 0 || !treatUnknownFailureAsOffline
            ? null
            : PrintProtocolResult.PrinterOffline;
    }

    private static bool ContainsAny(string text, params string[] patterns)
    {
        return patterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private int NormalizeCommandTimeout()
    {
        return Math.Max(1000, _options.ZebraCommandTimeoutMilliseconds);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The timeout result is still returned even if the process exits between checks.
        }
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
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
