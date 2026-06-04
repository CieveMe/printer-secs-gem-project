using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Validation;

public sealed class WriteTagValidationWorker : BackgroundService
{
    private readonly IHardwareGateway _hardwareGateway;
    private readonly LocalValidationOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<WriteTagValidationWorker> _logger;

    public WriteTagValidationWorker(
        IHardwareGateway hardwareGateway,
        IOptions<LocalValidationOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<WriteTagValidationWorker> logger)
    {
        _hardwareGateway = hardwareGateway;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(
                "Starting local RFID write validation: shelf={ShelfId}, location={LocationId}, tag={Tag}",
                _options.ShelfId,
                _options.LocationId,
                _options.Content);

            var result = await _hardwareGateway.WriteTagAsync(
                new TagWriteCommand(_options.ShelfId, _options.LocationId, _options.Content),
                stoppingToken);

            if (!result.Success)
            {
                _logger.LogError(
                    "Local RFID write failed: code={Code}, description={Description}",
                    result.Code,
                    result.Description);
                return;
            }

            _logger.LogInformation(
                "Local RFID write completed: code={Code}, description={Description}",
                result.Code,
                result.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local RFID write validation failed");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
