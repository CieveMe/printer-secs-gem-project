using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;

namespace PrinterSecsGem.Eq.Validation;

public sealed class LocalValidationWorker : BackgroundService
{
    private readonly IPrinterGateway _printerGateway;
    private readonly IHardwareGateway _hardwareGateway;
    private readonly SecsEventMessageFactory _secsEventMessageFactory;
    private readonly LocalValidationOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<LocalValidationWorker> _logger;

    public LocalValidationWorker(
        IPrinterGateway printerGateway,
        IHardwareGateway hardwareGateway,
        SecsEventMessageFactory secsEventMessageFactory,
        IOptions<LocalValidationOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<LocalValidationWorker> logger)
    {
        _printerGateway = printerGateway;
        _hardwareGateway = hardwareGateway;
        _secsEventMessageFactory = secsEventMessageFactory;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting local validation with content={Content}", _options.Content);

            var printResult = await _printerGateway.PrintAsync(
                new PrintCommand(_options.ShelfId, _options.PrinterId, _options.Content, _options.Copies),
                stoppingToken);
            EnsureSuccess("print", printResult);

            var writeTagResult = await _hardwareGateway.WriteTagAsync(
                new TagWriteCommand(_options.ShelfId, _options.LocationId, _options.Content),
                stoppingToken);
            EnsureSuccess("write tag", writeTagResult);

            var shelfStatus = await _hardwareGateway.QueryShelfStatusAsync(
                new ShelfStatusQuery(_options.ShelfId, _options.LocationId),
                stoppingToken);
            if (!shelfStatus.Success)
            {
                throw new InvalidOperationException(
                    $"query shelf status failed: code={shelfStatus.Code}, description={shelfStatus.Description}");
            }

            var tagEvent = new TagReadEvent(
                _options.ShelfId,
                _options.LocationId,
                _options.Content,
                true,
                DateTimeOffset.Now);

            using var s6f11 = _secsEventMessageFactory.CreateTagReadEvent(tagEvent);

            _logger.LogInformation("Prepared event message: S{Stream}F{Function} {Name}", s6f11.S, s6f11.F, s6f11.Name);
            _logger.LogInformation("Local validation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local validation failed");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private static void EnsureSuccess(string operation, OperationResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"{operation} failed: code={result.Code}, description={result.Description}");
        }
    }
}
