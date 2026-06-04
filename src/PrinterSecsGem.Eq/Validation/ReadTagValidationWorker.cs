using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Validation;

public sealed class ReadTagValidationWorker : BackgroundService
{
    private readonly IHardwareGateway _hardwareGateway;
    private readonly LocalValidationOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ReadTagValidationWorker> _logger;

    public ReadTagValidationWorker(
        IHardwareGateway hardwareGateway,
        IOptions<LocalValidationOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<ReadTagValidationWorker> logger)
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
                "Starting local RFID read validation: shelf={ShelfId}, location={LocationId}",
                _options.ShelfId,
                _options.LocationId);

            var result = await _hardwareGateway.QueryShelfStatusAsync(
                new ShelfStatusQuery(_options.ShelfId, _options.LocationId),
                stoppingToken);

            if (!result.Success)
            {
                _logger.LogError(
                    "Local RFID read failed: code={Code}, description={Description}",
                    result.Code,
                    result.Description);
                return;
            }

            foreach (var location in result.Locations)
            {
                _logger.LogInformation(
                    "Local RFID read result: location={LocationId}, loaded={Loaded}, tag={Tag}",
                    location.LocationId,
                    location.IsLoaded,
                    location.Tag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local RFID read validation failed");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
