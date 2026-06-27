using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackTcpUnitRouter : IERackUnitRouter
{
    private const string RouteTimeoutDescription = "ERACK单元响应超时";
    private const string RouteFailedDescription = "ERACK单元转发失败";

    private readonly ERackServerOptions _options;
    private readonly SecsEventMessageFactory _eventMessageFactory;
    private readonly SecsEventPublisher _eventPublisher;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<ERackTcpUnitRouter> _logger;
    private readonly ConcurrentDictionary<string, UnitSession> _sessionsByShelf = new(StringComparer.OrdinalIgnoreCase);

    public ERackTcpUnitRouter(
        IOptions<ERackServerOptions> options,
        SecsEventMessageFactory eventMessageFactory,
        SecsEventPublisher eventPublisher,
        StatusUiEventBus statusEvents,
        ILogger<ERackTcpUnitRouter> logger)
    {
        _options = options.Value;
        _eventMessageFactory = eventMessageFactory;
        _eventPublisher = eventPublisher;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    public async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "<unknown>";
        UnitSession? session = null;
        try
        {
            await using var stream = tcpClient.GetStream();
            using var reader = ERackWireProtocol.CreateReader(stream);
            using var writer = ERackWireProtocol.CreateWriter(stream);

            var registerEnvelope = await ERackWireProtocol.ReadAsync(reader, cancellationToken);
            if (registerEnvelope is null ||
                !registerEnvelope.MessageType.Equals(ERackWireMessageTypes.RegisterUnit, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ERACK unit must send RegisterUnit as the first message.");
            }

            var register = registerEnvelope.ReadPayload<RegisterUnitPayload>();
            session = new UnitSession(register, endpoint, writer, _logger);
            _sessionsByShelf[register.ShelfId] = session;

            _logger.LogInformation(
                "ERACK unit registered: unitId={UnitId}, shelf={ShelfId}, endpoint={Endpoint}, locations={LocationCount}",
                register.UnitId,
                register.ShelfId,
                endpoint,
                register.Locations.Count);
            PublishRouteStatus($"Registered unit={register.UnitId}, shelf={register.ShelfId}, endpoint={endpoint}");

            await ERackWireProtocol.WriteAsync(
                writer,
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.RegisterUnitResponse,
                    register.ShelfId,
                    new BasicResultPayload(true, 0, "registered"),
                    registerEnvelope.MessageId),
                session.WriteLock,
                cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await ERackWireProtocol.ReadAsync(reader, cancellationToken);
                if (envelope is null)
                {
                    throw new IOException("ERACK unit connection closed.");
                }

                await HandleUnitMessageAsync(session, envelope, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ERACK unit disconnected: endpoint={Endpoint}", endpoint);
        }
        finally
        {
            if (session is not null)
            {
                _sessionsByShelf.TryRemove(session.ShelfId, out _);
                session.Close();
                _logger.LogInformation(
                    "ERACK unit route removed: unitId={UnitId}, shelf={ShelfId}, endpoint={Endpoint}",
                    session.UnitId,
                    session.ShelfId,
                    session.Endpoint);
                PublishRouteStatus($"Removed unit={session.UnitId}, shelf={session.ShelfId}, endpoint={session.Endpoint}");
            }
        }
    }

    public async Task<ShelfStatusResult> QueryShelfStatusAsync(
        ShelfStatusQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(query.ShelfId, out var session, out var failDescription))
        {
            return ShelfStatusResult.Fail(query.ShelfId, 6, failDescription);
        }

        try
        {
            var response = await session.SendRequestAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.ReadShelfStatus,
                    query.ShelfId,
                    new ReadShelfStatusPayload(query.ShelfId, query.LocationId, query.ReadLengthBytes)),
                NormalizeRequestTimeout(),
                cancellationToken);

            return response.ReadPayload<ShelfStatusResultPayload>().ToModel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ERACK read route failed: shelf={ShelfId}, location={LocationId}", query.ShelfId, query.LocationId);
            return ShelfStatusResult.Fail(
                query.ShelfId,
                7,
                ex is TimeoutException ? RouteTimeoutDescription : RouteFailedDescription);
        }
    }

    public async Task<OperationResult> WriteTagAsync(
        TagWriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(command.ShelfId, out var session, out var failDescription))
        {
            return OperationResult.Fail(6, failDescription);
        }

        try
        {
            var response = await session.SendRequestAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.WriteRfid,
                    command.ShelfId,
                    new WriteRfidPayload(command.ShelfId, command.LocationId, command.Tag)),
                NormalizeRequestTimeout(),
                cancellationToken);

            var payload = response.ReadPayload<BasicResultPayload>();
            return payload.Success
                ? OperationResult.Ok(payload.Description)
                : OperationResult.Fail(payload.Code, payload.Description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ERACK write route failed: shelf={ShelfId}, location={LocationId}", command.ShelfId, command.LocationId);
            return OperationResult.Fail(
                7,
                ex is TimeoutException ? RouteTimeoutDescription : RouteFailedDescription);
        }
    }

    public async Task<OperationResult> PrintAsync(
        PrintCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetSession(command.ShelfId, out var session, out var failDescription))
        {
            return OperationResult.Fail(6, failDescription);
        }

        try
        {
            var response = await session.SendRequestAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.Print,
                    command.ShelfId,
                    new PrintPayload(command.ShelfId, command.PrinterId, command.Content, command.Copies)),
                NormalizeRequestTimeout(),
                cancellationToken);

            var payload = response.ReadPayload<BasicResultPayload>();
            return payload.Success
                ? OperationResult.Ok(payload.Description)
                : OperationResult.Fail(payload.Code, payload.Description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ERACK print route failed: shelf={ShelfId}, printer={PrinterId}", command.ShelfId, command.PrinterId);
            return ex is TimeoutException
                ? PrintProtocolResult.Fail(PrintProtocolResult.PrinterOffline)
                : OperationResult.Fail(7, RouteFailedDescription);
        }
    }

    private async Task HandleUnitMessageAsync(
        UnitSession session,
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.MessageType)
        {
            case ERackWireMessageTypes.ReadShelfStatusResponse:
            case ERackWireMessageTypes.WriteRfidResponse:
            case ERackWireMessageTypes.PrintResponse:
            case ERackWireMessageTypes.ErrorResponse:
                session.CompleteResponse(envelope);
                return;
            case ERackWireMessageTypes.Heartbeat:
                await ERackWireProtocol.WriteAsync(
                    session.Writer,
                    ERackWireEnvelope.Create(
                        ERackWireMessageTypes.HeartbeatResponse,
                        session.ShelfId,
                        new BasicResultPayload(true, 0, "alive"),
                        envelope.MessageId),
                    session.WriteLock,
                    cancellationToken);
                return;
            case ERackWireMessageTypes.ShelfStateChanged:
                _eventPublisher.TryPublish(_eventMessageFactory.CreateShelfStateEvent(
                    envelope.ReadPayload<ShelfStateEventPayload>().ToModel()));
                return;
            case ERackWireMessageTypes.RfidWriteEvent:
                _eventPublisher.TryPublish(_eventMessageFactory.CreateRfidWriteEvent(
                    envelope.ReadPayload<RfidWriteEventPayload>().ToModel()));
                return;
            case ERackWireMessageTypes.PrintEvent:
                _eventPublisher.TryPublish(_eventMessageFactory.CreatePrintEvent(
                    envelope.ReadPayload<PrintEventPayload>().ToModel()));
                return;
            default:
                _logger.LogWarning(
                    "ERACK server ignored unknown unit message: type={MessageType}, shelf={ShelfId}, unit={UnitId}",
                    envelope.MessageType,
                    envelope.ShelfId,
                    session.UnitId);
                return;
        }
    }

    private bool TryGetSession(string shelfId, out UnitSession session, out string failDescription)
    {
        if (string.IsNullOrWhiteSpace(shelfId))
        {
            failDescription = "货架编号为空";
            session = null!;
            return false;
        }

        if (_sessionsByShelf.TryGetValue(shelfId, out session!))
        {
            failDescription = string.Empty;
            return true;
        }

        failDescription = $"货架未在线：{shelfId}";
        return false;
    }

    private TimeSpan NormalizeRequestTimeout()
    {
        return TimeSpan.FromMilliseconds(Math.Max(1000, _options.RequestTimeoutMilliseconds));
    }

    private void PublishRouteStatus(string lastEvent)
    {
        var shelfIds = _sessionsByShelf.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var onlineText = shelfIds.Length == 0
            ? "0 online"
            : $"{shelfIds.Length} online: {string.Join(",", shelfIds)}";
        _statusEvents.Publish(
            StatusUiEventCategories.ERackRoutesStatus,
            $"{onlineText}; last={lastEvent}");
    }

    private sealed class UnitSession
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ERackWireEnvelope>> _pendingRequests = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger _logger;

        public UnitSession(
            RegisterUnitPayload register,
            string endpoint,
            StreamWriter writer,
            ILogger logger)
        {
            UnitId = register.UnitId;
            ShelfId = register.ShelfId;
            Endpoint = endpoint;
            Writer = writer;
            _logger = logger;
        }

        public string UnitId { get; }

        public string ShelfId { get; }

        public string Endpoint { get; }

        public StreamWriter Writer { get; }

        public SemaphoreSlim WriteLock { get; } = new(1, 1);

        public async Task<ERackWireEnvelope> SendRequestAsync(
            ERackWireEnvelope request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ERackWireEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pendingRequests.TryAdd(request.MessageId, completion))
            {
                throw new InvalidOperationException($"Duplicate ERACK request id: {request.MessageId}");
            }

            try
            {
                await ERackWireProtocol.WriteAsync(Writer, request, WriteLock, cancellationToken);
                return await completion.Task.WaitAsync(timeout, cancellationToken);
            }
            finally
            {
                _pendingRequests.TryRemove(request.MessageId, out _);
            }
        }

        public void CompleteResponse(ERackWireEnvelope response)
        {
            if (_pendingRequests.TryRemove(response.MessageId, out var completion))
            {
                completion.TrySetResult(response);
                return;
            }

            _logger.LogWarning(
                "ERACK response did not match a pending request: unit={UnitId}, shelf={ShelfId}, type={MessageType}, id={MessageId}",
                UnitId,
                ShelfId,
                response.MessageType,
                response.MessageId);
        }

        public void Close()
        {
            foreach (var item in _pendingRequests.ToArray())
            {
                if (_pendingRequests.TryRemove(item.Key, out var completion))
                {
                    completion.TrySetException(new IOException("ERACK unit connection closed."));
                }
            }
        }
    }
}
