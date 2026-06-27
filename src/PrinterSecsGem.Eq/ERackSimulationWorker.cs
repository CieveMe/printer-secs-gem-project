using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq;

public sealed class ERackSimulationWorker : BackgroundService
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ERackSimulationOptions _options;
    private readonly IERackEventSink _eventSink;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<ERackSimulationWorker> _logger;

    public ERackSimulationWorker(
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackSimulationOptions> options,
        IERackEventSink eventSink,
        StatusUiEventBus statusEvents,
        ILogger<ERackSimulationWorker> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _options = options.Value;
        _eventSink = eventSink;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _statusEvents.Publish(StatusUiEventCategories.SimulationStatus, "Disabled");
            return;
        }

        if (!_runtimeOptions.IsUnitEnabled)
        {
            _logger.LogInformation("ERACK simulation worker is skipped because Runtime:Mode does not enable Unit.");
            _statusEvents.Publish(StatusUiEventCategories.SimulationStatus, "Skipped: Runtime:Mode does not enable Unit");
            return;
        }

        var shelfId = NormalizeText(_options.ShelfId, "SHELF001");
        var locationId = NormalizeText(_options.LocationId, "LOC001");
        var tag = NormalizeText(_options.Tag, "RFID1234567890");

        _logger.LogInformation(
            "ERACK simulation worker started: shelf={ShelfId}, location={LocationId}, tag={Tag}, startupDelayMs={StartupDelayMilliseconds}, stepIntervalMs={StepIntervalMilliseconds}, loop={Loop}",
            shelfId,
            locationId,
            tag,
            NormalizeStartupDelay(),
            NormalizeStepInterval(),
            _options.Loop);
        _statusEvents.Publish(
            StatusUiEventCategories.SimulationStatus,
            $"Running shelf={shelfId}, location={locationId}, intervalMs={NormalizeStepInterval()}");

        try
        {
            await Task.Delay(NormalizeStartupDelay(), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PublishStateAsync(shelfId, locationId, tag, true, "loaded with RFID", stoppingToken);
                await Task.Delay(NormalizeStepInterval(), stoppingToken);

                await PublishStateAsync(shelfId, locationId, string.Empty, true, "loaded without RFID", stoppingToken);
                await Task.Delay(NormalizeStepInterval(), stoppingToken);

                await PublishStateAsync(shelfId, locationId, string.Empty, false, "empty", stoppingToken);
                if (!_options.Loop)
                {
                    return;
                }

                await Task.Delay(NormalizeStepInterval(), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PublishStateAsync(
        string shelfId,
        string locationId,
        string tag,
        bool isLoaded,
        string description,
        CancellationToken cancellationToken)
    {
        await _eventSink.PublishShelfStateAsync(
            new ShelfStateEvent(
                shelfId,
                locationId,
                tag,
                isLoaded,
                DateTimeOffset.Now),
            cancellationToken);

        _statusEvents.Publish(StatusUiEventCategories.RfidStatus, BuildStatusText(isLoaded, tag));
        _statusEvents.Publish(StatusUiEventCategories.SimulationStatus, BuildStatusText(isLoaded, tag));
        _statusEvents.Publish(StatusUiEventCategories.DisplayStatus, BuildDisplayStatusText(isLoaded, tag));
        _logger.LogInformation(
            "ERACK simulation state published: shelf={ShelfId}, location={LocationId}, loaded={IsLoaded}, tag={Tag}, description={Description}",
            shelfId,
            locationId,
            isLoaded,
            tag,
            description);
    }

    private static string BuildStatusText(bool isLoaded, string tag)
    {
        if (!isLoaded)
        {
            return "Simulation empty";
        }

        return string.IsNullOrWhiteSpace(tag)
            ? "Simulation loaded, no RFID"
            : $"Simulation loaded: {tag}";
    }

    private static string BuildDisplayStatusText(bool isLoaded, string tag)
    {
        if (!isLoaded)
        {
            return "Mock display clear: simulation empty";
        }

        return string.IsNullOrWhiteSpace(tag)
            ? "Mock display text: NO ID"
            : $"Mock display text: {tag}";
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private int NormalizeStartupDelay()
    {
        return Math.Max(0, _options.StartupDelayMilliseconds);
    }

    private int NormalizeStepInterval()
    {
        return Math.Max(500, _options.StepIntervalMilliseconds);
    }
}
