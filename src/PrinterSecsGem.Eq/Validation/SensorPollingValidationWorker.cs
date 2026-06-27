using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Secs;

namespace PrinterSecsGem.Eq.Validation;

public sealed class SensorPollingValidationWorker : BackgroundService
{
    private readonly ERackHardwareOptions _erackOptions;
    private readonly ERackSensorDisplayOptions _sensorOptions;
    private readonly ERackLocationRegistry _locations;
    private readonly ERackSerialHardwareGateway _gateway;
    private readonly SecsEventMessageFactory _secsEventMessageFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SensorPollingValidationWorker> _logger;

    public SensorPollingValidationWorker(
        IOptions<ERackHardwareOptions> erackOptions,
        IOptions<ERackSensorDisplayOptions> sensorOptions,
        ERackLocationRegistry locations,
        ERackSerialHardwareGateway gateway,
        SecsEventMessageFactory secsEventMessageFactory,
        IHostApplicationLifetime lifetime,
        ILogger<SensorPollingValidationWorker> logger)
    {
        _erackOptions = erackOptions.Value;
        _sensorOptions = sensorOptions.Value;
        _locations = locations;
        _gateway = gateway;
        _secsEventMessageFactory = secsEventMessageFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (!_erackOptions.Enabled)
            {
                throw new InvalidOperationException("Sensor polling validation requires ERackHardware:Enabled=true.");
            }

            _logger.LogInformation(
                "Starting sensor polling validation: locations={LocationCount}, sensorCommand=0x{SensorCommand:X2}, payloadIndex={SensorPayloadIndex}, checkLevel={CheckLevel}",
                _locations.Locations.Count,
                _sensorOptions.SensorCommand,
                _sensorOptions.SensorPayloadIndex,
                _sensorOptions.CheckLevel);

            foreach (var location in _locations.Locations)
            {
                await ValidateLocationAsync(location, stoppingToken);
            }

            _logger.LogInformation("Sensor polling validation completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sensor polling validation failed.");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private async Task ValidateLocationAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var sensor = await _gateway.ReadSensorStateAsync(
            location,
            _sensorOptions.SensorCommand,
            _sensorOptions.SensorPayloadIndex,
            _sensorOptions.CheckLevel,
            _sensorOptions.SensorWaitTimeMilliseconds,
            _sensorOptions.SensorWaitCount,
            cancellationToken);

        if (!sensor.Success)
        {
            throw new InvalidOperationException(
                $"sensor poll failed: location={location.LocationId}, code={sensor.Code}, description={sensor.Description}");
        }

        _logger.LogInformation(
            "Sensor poll result: shelf={ShelfId}, location={LocationId}, payload={PayloadHex}, isLoaded={IsLoaded}",
            location.ShelfId,
            location.LocationId,
            ToHex(sensor.Payload),
            sensor.IsLoaded);

        var tag = sensor.IsLoaded
            ? await TryReadTagAsync(location, cancellationToken)
            : string.Empty;

        using var s6f21 = _secsEventMessageFactory.CreateShelfStateEvent(
            new ShelfStateEvent(
                location.ShelfId,
                location.LocationId,
                tag,
                sensor.IsLoaded,
                DateTimeOffset.Now));

        _logger.LogInformation(
            "Prepared sensor polling event message: S{Stream}F{Function} {Name}, shelf={ShelfId}, location={LocationId}, isLoaded={IsLoaded}, tag={Tag}",
            s6f21.S,
            s6f21.F,
            s6f21.Name,
            location.ShelfId,
            location.LocationId,
            sensor.IsLoaded,
            tag);
    }

    private async Task<string> TryReadTagAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var status = await _gateway.QueryShelfStatusAsync(
            new ShelfStatusQuery(location.ShelfId, location.LocationId, _sensorOptions.ReadLengthBytes),
            cancellationToken);

        if (!status.Success)
        {
            _logger.LogWarning(
                "Sensor polling validation RFID read failed: shelf={ShelfId}, location={LocationId}, code={Code}, description={Description}",
                location.ShelfId,
                location.LocationId,
                status.Code,
                status.Description);
            return string.Empty;
        }

        var tag = status.Locations.FirstOrDefault()?.Tag ?? string.Empty;
        _logger.LogInformation(
            "Sensor polling validation RFID read result: shelf={ShelfId}, location={LocationId}, tag={Tag}",
            location.ShelfId,
            location.LocationId,
            tag);
        return tag;
    }

    private static string ToHex(byte[] payload)
    {
        return payload.Length == 0 ? "<empty>" : Convert.ToHexString(payload);
    }
}
