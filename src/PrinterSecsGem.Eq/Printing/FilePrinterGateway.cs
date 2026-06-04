using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Printing;

public sealed class FilePrinterGateway : IPrinterGateway
{
    private readonly PrinterOptions _options;
    private readonly ZplLabelTemplate _template;
    private readonly ILogger<FilePrinterGateway> _logger;

    public FilePrinterGateway(
        IOptions<PrinterOptions> options,
        ZplLabelTemplate template,
        ILogger<FilePrinterGateway> logger)
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
        Directory.CreateDirectory(_options.OutputDirectory);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{SanitizeFileName(command.Content)}.zpl";
        var filePath = Path.Combine(_options.OutputDirectory, fileName);
        await File.WriteAllTextAsync(filePath, zpl, cancellationToken);

        _logger.LogInformation(
            "Generated ZPL file: printer={PrinterId}, shelf={ShelfId}, content={Content}, copies={Copies}, file={FilePath}",
            command.PrinterId,
            command.ShelfId,
            command.Content,
            copies,
            filePath);

        return OperationResult.Ok($"zpl generated: {filePath}");
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var fileName = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(fileName) ? "label" : fileName;
    }
}
