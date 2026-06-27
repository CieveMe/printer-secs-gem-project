using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackClientWorker : BackgroundService
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ERackClientOptions _options;
    private readonly ERackLocationRegistry _locations;
    private readonly IHardwareGateway _hardwareGateway;
    private readonly IPrinterGateway _printerGateway;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<ERackClientWorker> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private StreamWriter? _writer;

    public ERackClientWorker(
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackClientOptions> options,
        ERackLocationRegistry locations,
        IHardwareGateway hardwareGateway,
        IPrinterGateway printerGateway,
        StatusUiEventBus statusEvents,
        ILogger<ERackClientWorker> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _options = options.Value;
        _locations = locations;
        _hardwareGateway = hardwareGateway;
        _printerGateway = printerGateway;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    public async Task<bool> TrySendEventAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var writer = _writer;
        if (writer is null)
        {
            return false;
        }

        try
        {
            await ERackWireProtocol.WriteAsync(writer, envelope, _writeLock, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send ERACK client event: type={MessageType}, shelf={ShelfId}",
                envelope.MessageType,
                envelope.ShelfId);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!UseRemoteClient)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "ERACK unit client connection failed; retrying in {DelayMilliseconds} ms",
                    NormalizeReconnectDelay());
                PublishClientStatus($"Disconnected, retrying in {NormalizeReconnectDelay()} ms: {ex.Message}");
            }
            finally
            {
                _writer = null;
            }

            await Task.Delay(NormalizeReconnectDelay(), stoppingToken);
        }
    }

    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        _logger.LogInformation(
            "Connecting ERACK unit client to server: {Host}:{Port}",
            _options.ServerHost,
            _options.ServerPort);
        PublishClientStatus($"Connecting {_options.ServerHost}:{_options.ServerPort}, unit={NormalizeText(_options.UnitId, Environment.MachineName)}, shelf={GetShelfId()}");
        await tcpClient.ConnectAsync(_options.ServerHost, _options.ServerPort, cancellationToken);
        PublishClientStatus($"Connected {_options.ServerHost}:{_options.ServerPort}, registering shelf={GetShelfId()}");

        await using var stream = tcpClient.GetStream();
        using var reader = ERackWireProtocol.CreateReader(stream);
        using var writer = ERackWireProtocol.CreateWriter(stream);
        _writer = writer;

        await SendRegisterAsync(writer, cancellationToken);
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(NormalizeHeartbeatInterval()));

        var receiveTask = ReceiveLoopAsync(reader, cancellationToken);
        var heartbeatTask = SendHeartbeatLoopAsync(heartbeatTimer, cancellationToken);
        await await Task.WhenAny(receiveTask, heartbeatTask);
    }

    private async Task SendRegisterAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        var shelfId = GetShelfId();
        var payload = new RegisterUnitPayload(
            string.IsNullOrWhiteSpace(_options.UnitId) ? Environment.MachineName : _options.UnitId,
            shelfId,
            _locations.Locations
                .Where(location => location.ShelfId.Equals(shelfId, StringComparison.OrdinalIgnoreCase))
                .Select(location => new ERackWireLocation(location.LocationId))
                .ToArray());

        await ERackWireProtocol.WriteAsync(
            writer,
            ERackWireEnvelope.Create(ERackWireMessageTypes.RegisterUnit, shelfId, payload),
            _writeLock,
            cancellationToken);

        _logger.LogInformation(
            "ERACK unit client registered request sent: unitId={UnitId}, shelf={ShelfId}, locations={LocationCount}",
            payload.UnitId,
            payload.ShelfId,
            payload.Locations.Count);
        PublishClientStatus($"Registered unit={payload.UnitId}, shelf={payload.ShelfId}, locations={payload.Locations.Count}");
    }

    private async Task ReceiveLoopAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var envelope = await ERackWireProtocol.ReadAsync(reader, cancellationToken);
            if (envelope is null)
            {
                throw new IOException("ERACK server closed the connection.");
            }

            await HandleServerMessageAsync(envelope, cancellationToken);
        }
    }

    private async Task SendHeartbeatLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var writer = _writer;
            if (writer is null)
            {
                return;
            }

            await ERackWireProtocol.WriteAsync(
                writer,
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.Heartbeat,
                    GetShelfId(),
                    new { UnitId = _options.UnitId }),
                _writeLock,
                cancellationToken);
        }
    }

    private async Task HandleServerMessageAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.MessageType)
        {
            case ERackWireMessageTypes.RegisterUnitResponse:
            case ERackWireMessageTypes.HeartbeatResponse:
                return;
            case ERackWireMessageTypes.ReadShelfStatus:
                await HandleReadShelfStatusAsync(envelope, cancellationToken);
                return;
            case ERackWireMessageTypes.WriteRfid:
                await HandleWriteRfidAsync(envelope, cancellationToken);
                return;
            case ERackWireMessageTypes.Print:
                await HandlePrintAsync(envelope, cancellationToken);
                return;
            default:
                _logger.LogWarning(
                    "ERACK unit client ignored unknown server message: type={MessageType}, shelf={ShelfId}",
                    envelope.MessageType,
                    envelope.ShelfId);
                return;
        }
    }

    private async Task HandleReadShelfStatusAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ReadPayload<ReadShelfStatusPayload>();
        var result = await _hardwareGateway.QueryShelfStatusAsync(
            new ShelfStatusQuery(payload.ShelfId, payload.LocationId, payload.ReadLengthBytes),
            cancellationToken);

        await SendResponseAsync(
            envelope,
            ERackWireMessageTypes.ReadShelfStatusResponse,
            payload.ShelfId,
            ShelfStatusResultPayload.FromModel(result),
            cancellationToken);
    }

    private async Task HandleWriteRfidAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ReadPayload<WriteRfidPayload>();
        var result = await _hardwareGateway.WriteTagAsync(
            new TagWriteCommand(payload.ShelfId, payload.LocationId, payload.Tag),
            cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

        await SendResponseAsync(
            envelope,
            ERackWireMessageTypes.WriteRfidResponse,
            payload.ShelfId,
            new BasicResultPayload(result.Success, resultCode, result.Description),
            cancellationToken);

        await TrySendEventAsync(
            ERackWireEnvelope.Create(
                ERackWireMessageTypes.RfidWriteEvent,
                payload.ShelfId,
                RfidWriteEventPayload.FromModel(new RfidWriteEvent(
                    payload.ShelfId,
                    payload.LocationId,
                    payload.Tag,
                    resultCode,
                    result.Description,
                    DateTimeOffset.Now))),
            cancellationToken);
    }

    private async Task HandlePrintAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.ReadPayload<PrintPayload>();
        var result = await _printerGateway.PrintAsync(
            new PrintCommand(payload.ShelfId, payload.PrinterId, payload.Content, payload.Copies),
            cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

        await SendResponseAsync(
            envelope,
            ERackWireMessageTypes.PrintResponse,
            payload.ShelfId,
            new BasicResultPayload(result.Success, resultCode, result.Description),
            cancellationToken);

        await TrySendEventAsync(
            ERackWireEnvelope.Create(
                ERackWireMessageTypes.PrintEvent,
                payload.ShelfId,
                PrintEventPayload.FromModel(new PrintEvent(
                    payload.ShelfId,
                    payload.PrinterId,
                    payload.Content,
                    resultCode,
                    result.Description,
                    DateTimeOffset.Now))),
            cancellationToken);
    }

    private async Task SendResponseAsync<TPayload>(
        ERackWireEnvelope request,
        string responseType,
        string shelfId,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var writer = _writer ?? throw new IOException("ERACK client is not connected.");
        await ERackWireProtocol.WriteAsync(
            writer,
            ERackWireEnvelope.Create(responseType, shelfId, payload, request.MessageId),
            _writeLock,
            cancellationToken);
    }

    private string GetShelfId()
    {
        return string.IsNullOrWhiteSpace(_options.ShelfId)
            ? _locations.DefaultLocation.ShelfId
            : _options.ShelfId.Trim();
    }

    private int NormalizeReconnectDelay()
    {
        return Math.Max(500, _options.ReconnectDelayMilliseconds);
    }

    private int NormalizeHeartbeatInterval()
    {
        return Math.Max(1000, _options.HeartbeatIntervalMilliseconds);
    }

    private bool UseRemoteClient =>
        _runtimeOptions.IsUnitEnabled &&
        (_options.Enabled || _runtimeOptions.IsServerEnabled);

    private void PublishClientStatus(string message)
    {
        _statusEvents.Publish(StatusUiEventCategories.ERackUnitClientStatus, message);
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
