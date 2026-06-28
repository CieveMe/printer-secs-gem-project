using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq;

public sealed class ERackSensorDisplayWorker : BackgroundService
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ERackHardwareOptions _erackOptions;
    private readonly ERackSensorDisplayOptions _options;
    private readonly ERackLocationRegistry _locations;
    private readonly ERackSerialHardwareGateway _gateway;
    private readonly IERackEventSink _eventSink;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<ERackSensorDisplayWorker> _logger;
    private readonly Dictionary<string, bool> _lastLoadedStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PresenceSnapshot> _lastRfidPollingStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _rfidPollingEmptyCounts = new(StringComparer.OrdinalIgnoreCase);

    public ERackSensorDisplayWorker(
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackHardwareOptions> erackOptions,
        IOptions<ERackSensorDisplayOptions> options,
        ERackLocationRegistry locations,
        ERackSerialHardwareGateway gateway,
        IERackEventSink eventSink,
        StatusUiEventBus statusEvents,
        ILogger<ERackSensorDisplayWorker> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _erackOptions = erackOptions.Value;
        _options = options.Value;
        _locations = locations;
        _gateway = gateway;
        _eventSink = eventSink;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("ERack sensor/display worker is disabled by ERackSensorDisplay:Enabled=false.");
            _statusEvents.Publish(StatusUiEventCategories.DisplayStatus, "Display disabled: ERackSensorDisplay:Enabled=false");
            return;
        }

        if (!_runtimeOptions.IsUnitEnabled)
        {
            _logger.LogInformation("ERack sensor/display worker is skipped because Runtime:Mode does not enable Unit hardware.");
            _statusEvents.Publish(StatusUiEventCategories.DisplayStatus, "Display skipped: Runtime:Mode does not enable Unit");
            return;
        }

        if (!_erackOptions.Enabled)
        {
            _logger.LogInformation("ERack sensor/display worker is skipped because ERackHardware:Enabled=false.");
            _statusEvents.Publish(StatusUiEventCategories.DisplayStatus, "Display skipped: ERackHardware:Enabled=false");
            return;
        }

        if (!_options.HasKnownPresenceMode)
        {
            _logger.LogWarning(
                "Unsupported ERackSensorDisplay:PresenceMode={PresenceMode}; falling back to Sensor.",
                _options.PresenceMode);
        }

        var useRfidPolling = _options.IsRfidPollingMode;
        var presenceMode = _options.NormalizedPresenceMode;
        _logger.LogInformation(
            "ERack sensor/display worker started: locations={LocationCount}, presenceMode={PresenceMode}, pollIntervalMs={PollIntervalMilliseconds}, rfidPollingReadTimeoutMs={RfidPollingReadTimeoutMilliseconds}, emptyConfirmCount={EmptyConfirmCount}, sensorCommand=0x{SensorCommand:X2}, payloadIndex={SensorPayloadIndex}, checkLevel={CheckLevel}",
            _locations.Locations.Count,
            presenceMode,
            NormalizePollInterval(),
            NormalizeRfidPollingReadTimeout(),
            NormalizeRfidPollingEmptyConfirmCount(),
            _options.SensorCommand,
            _options.SensorPayloadIndex,
            _options.CheckLevel);
        _statusEvents.Publish(
            StatusUiEventCategories.DisplayStatus,
            useRfidPolling
                ? "Display enabled: RFID polling presence mode"
                : "Display enabled: waiting for sensor state");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleStopwatch = Stopwatch.StartNew();
                foreach (var location in _locations.Locations)
                {
                    if (useRfidPolling)
                    {
                        await PollLocationByRfidAsync(location, stoppingToken);
                    }
                    else
                    {
                        await PollLocationAsync(location, stoppingToken);
                    }
                }

                var delayMilliseconds = NormalizePollInterval();
                if (useRfidPolling)
                {
                    delayMilliseconds = Math.Max(0, delayMilliseconds - (int)cycleStopwatch.ElapsedMilliseconds);
                }

                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PollLocationAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var sensor = await _gateway.ReadSensorStateAsync(
            location,
            _options.SensorCommand,
            _options.SensorPayloadIndex,
            _options.CheckLevel,
            _options.SensorWaitTimeMilliseconds,
            _options.SensorWaitCount,
            cancellationToken);

        if (!sensor.Success)
        {
            if (_gateway.IsShutdownRequested || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.LogWarning(
                "ERack sensor read failed: location={LocationId}, code={Code}, description={Description}",
                location.LocationId,
                sensor.Code,
                sensor.Description);
            return;
        }

        var stateKey = BuildStateKey(location);
        if (!_options.UpdateDisplayOnEveryPoll &&
            _lastLoadedStates.TryGetValue(stateKey, out var lastLoaded) &&
            lastLoaded == sensor.IsLoaded)
        {
            return;
        }

        _lastLoadedStates[stateKey] = sensor.IsLoaded;
        if (sensor.IsLoaded)
        {
            await ReadTagAndDisplayAsync(location, cancellationToken);
            return;
        }

        await ClearDisplayAsync(location, cancellationToken);
    }

    private async Task PollLocationByRfidAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var status = await QueryShelfStatusForRfidPollingAsync(location, cancellationToken);
        var stateKey = BuildStateKey(location);
        var readFailed = false;
        var failureDescription = string.Empty;
        var tag = string.Empty;

        if (status.Success)
        {
            tag = status.Locations.FirstOrDefault()?.Tag?.Trim() ?? string.Empty;
            _statusEvents.Publish(
                StatusUiEventCategories.RfidStatus,
                string.IsNullOrWhiteSpace(tag)
                    ? "RFID polling empty"
                    : $"RFID polling loaded: {tag}");
        }
        else
        {
            if (_gateway.IsShutdownRequested || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            readFailed = true;
            failureDescription = NormalizeStatusDescription(status.Description);
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            await HandleRfidPollingEmptyCandidateAsync(
                location,
                stateKey,
                readFailed,
                failureDescription,
                status.Code,
                cancellationToken);
            return;
        }

        _rfidPollingEmptyCounts[stateKey] = 0;
        var current = new PresenceSnapshot(true, tag);
        var shouldPublish = HasRfidPollingStateChanged(stateKey, current);
        if (!shouldPublish && !_options.UpdateDisplayOnEveryPoll)
        {
            return;
        }

        var displayResult = await _gateway.SetDisplayTextAsync(
            location,
            current.Tag,
            _options.DisplayWaitTimeMilliseconds,
            _options.DisplayMinWaitCount,
            _options.DisplayMaxBytes,
            cancellationToken);

        LogDisplayResult(location, current.Tag, displayResult, "RFID polling");

        if (shouldPublish)
        {
            await PublishShelfStateEventAsync(location, current.Tag, current.IsLoaded, cancellationToken);
            _lastRfidPollingStates[stateKey] = current;
        }
    }

    private async Task<ShelfStatusResult> QueryShelfStatusForRfidPollingAsync(
        ERackLocation location,
        CancellationToken cancellationToken)
    {
        var timeoutMilliseconds = NormalizeRfidPollingReadTimeout();
        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readTimeout.CancelAfter(timeoutMilliseconds);

        try
        {
            return await _gateway.QueryShelfStatusAsync(
                new ShelfStatusQuery(location.ShelfId, location.LocationId, _options.ReadLengthBytes),
                readTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && readTimeout.IsCancellationRequested)
        {
            return ShelfStatusResult.Fail(
                location.ShelfId,
                8,
                $"RFID polling read timed out after {timeoutMilliseconds} ms");
        }
    }

    private async Task HandleRfidPollingEmptyCandidateAsync(
        ERackLocation location,
        string stateKey,
        bool readFailed,
        string failureDescription,
        byte failureCode,
        CancellationToken cancellationToken)
    {
        var emptyConfirmCount = NormalizeRfidPollingEmptyConfirmCount();
        var emptyCount = IncrementRfidPollingEmptyCount(stateKey);
        var reason = readFailed ? failureDescription : "empty tag";
        var emptyConfirmed = emptyCount >= emptyConfirmCount;

        if (!emptyConfirmed)
        {
            if (ShouldLogRfidPollingEmptyPending(emptyCount, emptyConfirmCount))
            {
                _logger.LogInformation(
                    "ERack RFID polling empty candidate: shelf={ShelfId}, location={LocationId}, emptyCount={EmptyCount}/{EmptyConfirmCount}, reason={Reason}",
                    location.ShelfId,
                    location.LocationId,
                    emptyCount,
                    emptyConfirmCount,
                    reason);
            }

            _statusEvents.Publish(
                StatusUiEventCategories.RfidStatus,
                $"RFID polling empty pending: {emptyCount}/{emptyConfirmCount}");
            return;
        }

        var current = new PresenceSnapshot(false, string.Empty);
        var shouldPublish = HasRfidPollingStateChanged(stateKey, current);
        if (!shouldPublish && !_options.UpdateDisplayOnEveryPoll)
        {
            return;
        }

        _logger.Log(
            readFailed ? LogLevel.Warning : LogLevel.Information,
            "ERack RFID polling empty confirmed: shelf={ShelfId}, location={LocationId}, emptyCount={EmptyCount}/{EmptyConfirmCount}, code={Code}, reason={Reason}",
            location.ShelfId,
            location.LocationId,
            emptyCount,
            emptyConfirmCount,
            failureCode,
            reason);
        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            readFailed
                ? $"RFID polling failure confirmed empty: {reason}"
                : "RFID polling empty confirmed");

        var displayResult = await _gateway.ClearDisplayAsync(
            location,
            _options.DisplayWaitTimeMilliseconds,
            _options.DisplayMinWaitCount,
            cancellationToken);

        LogDisplayResult(location, string.Empty, displayResult, "RFID polling");

        if (readFailed && displayResult.Success)
        {
            _statusEvents.Publish(
                StatusUiEventCategories.DisplayStatus,
                $"Display cleared after RFID polling failure confirmation: {reason}");
        }

        if (shouldPublish)
        {
            await PublishShelfStateEventAsync(location, current.Tag, current.IsLoaded, cancellationToken);
            _lastRfidPollingStates[stateKey] = current;
        }
    }

    private async Task ReadTagAndDisplayAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var status = await _gateway.QueryShelfStatusAsync(
            new ShelfStatusQuery(location.ShelfId, location.LocationId, _options.ReadLengthBytes),
            cancellationToken);

        if (!status.Success)
        {
            await DisplayNoIdAndReportAsync(location, status.Code, status.Description, cancellationToken);
            _logger.LogWarning(
                "ERack sensor-triggered RFID read failed: shelf={ShelfId}, location={LocationId}, code={Code}, description={Description}",
                location.ShelfId,
                location.LocationId,
                status.Code,
                status.Description);
            return;
        }

        var tag = status.Locations.FirstOrDefault()?.Tag ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tag))
        {
            await DisplayNoIdAndReportAsync(location, 0, "RFID ID is empty", cancellationToken);
            return;
        }

        var displayResult = await _gateway.SetDisplayTextAsync(
            location,
            tag,
            _options.DisplayWaitTimeMilliseconds,
            _options.DisplayMinWaitCount,
            _options.DisplayMaxBytes,
            cancellationToken);

        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            string.IsNullOrWhiteSpace(tag) ? "Sensor loaded, no tag" : tag);

        LogDisplayResult(location, tag, displayResult, "sensor state");
        await PublishShelfStateEventAsync(location, tag, isLoaded: true, cancellationToken);
    }

    private async Task ClearDisplayAsync(ERackLocation location, CancellationToken cancellationToken)
    {
        var displayResult = await _gateway.ClearDisplayAsync(
            location,
            _options.DisplayWaitTimeMilliseconds,
            _options.DisplayMinWaitCount,
            cancellationToken);

        _statusEvents.Publish(StatusUiEventCategories.RfidStatus, "Sensor empty");
        LogDisplayResult(location, string.Empty, displayResult, "sensor state");
        await PublishShelfStateEventAsync(location, string.Empty, isLoaded: false, cancellationToken);
    }

    private async Task DisplayNoIdAndReportAsync(
        ERackLocation location,
        byte resultCode,
        string description,
        CancellationToken cancellationToken)
    {
        var displayText = BuildNoIdDisplayText(resultCode);
        var displayResult = await _gateway.SetDisplayTextAsync(
            location,
            displayText,
            _options.DisplayWaitTimeMilliseconds,
            _options.DisplayMinWaitCount,
            _options.DisplayMaxBytes,
            cancellationToken);

        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            $"Sensor loaded, no RFID: {description}");
        LogDisplayResult(location, displayText, displayResult, "sensor state");
        await PublishShelfStateEventAsync(location, string.Empty, isLoaded: true, cancellationToken);
    }

    private string BuildNoIdDisplayText(byte resultCode)
    {
        var code = resultCode == 0 ? _options.NoIdFailureCode : resultCode;
        return $"{_options.NoIdDisplayText}{code}";
    }

    private async Task PublishShelfStateEventAsync(
        ERackLocation location,
        string tag,
        bool isLoaded,
        CancellationToken cancellationToken)
    {
        await _eventSink.PublishShelfStateAsync(
            new ShelfStateEvent(
                location.ShelfId,
                location.LocationId,
                tag,
                isLoaded,
                DateTimeOffset.Now),
            cancellationToken);
    }

    private static string BuildStateKey(ERackLocation location)
    {
        return $"{location.ShelfId}|{location.LocationId}";
    }

    private bool HasRfidPollingStateChanged(string stateKey, PresenceSnapshot current)
    {
        if (!_lastRfidPollingStates.TryGetValue(stateKey, out var last))
        {
            return true;
        }

        if (last.IsLoaded != current.IsLoaded)
        {
            return true;
        }

        return current.IsLoaded && !string.Equals(last.Tag, current.Tag, StringComparison.Ordinal);
    }

    private static string NormalizeStatusDescription(string description)
    {
        return string.IsNullOrWhiteSpace(description) ? "no description" : description.Trim();
    }

    private void LogDisplayResult(ERackLocation location, string displayText, OperationResult result, string source)
    {
        if (result.Success)
        {
            _statusEvents.Publish(
                StatusUiEventCategories.DisplayStatus,
                string.IsNullOrWhiteSpace(displayText) ? "Display cleared" : $"Display text sent: {displayText}");
            _logger.LogInformation(
                "ERack display updated: source={Source}, shelf={ShelfId}, location={LocationId}, text={DisplayText}",
                source,
                location.ShelfId,
                location.LocationId,
                displayText);
            return;
        }

        _statusEvents.Publish(
            StatusUiEventCategories.DisplayStatus,
            $"Display failed: {result.Description}");
        _logger.LogWarning(
            "ERack display update failed: source={Source}, shelf={ShelfId}, location={LocationId}, code={Code}, description={Description}",
            source,
            location.ShelfId,
            location.LocationId,
            result.Code,
            result.Description);
    }

    private int NormalizePollInterval()
    {
        return Math.Max(100, _options.PollIntervalMilliseconds);
    }

    private int NormalizeRfidPollingReadTimeout()
    {
        return Math.Max(100, _options.RfidPollingReadTimeoutMilliseconds);
    }

    private int NormalizeRfidPollingEmptyConfirmCount()
    {
        return Math.Max(1, _options.RfidPollingEmptyConfirmCount);
    }

    private int IncrementRfidPollingEmptyCount(string stateKey)
    {
        _rfidPollingEmptyCounts.TryGetValue(stateKey, out var count);
        count++;
        _rfidPollingEmptyCounts[stateKey] = count;
        return count;
    }

    private static bool ShouldLogRfidPollingEmptyPending(
        int emptyCount,
        int emptyConfirmCount)
    {
        return emptyCount <= 3 ||
            emptyCount == emptyConfirmCount ||
            emptyCount % 5 == 0;
    }

    private sealed record PresenceSnapshot(bool IsLoaded, string Tag);
}
