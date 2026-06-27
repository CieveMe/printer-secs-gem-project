using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Secs;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackEventSink : IERackEventSink
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ERackClientOptions _clientOptions;
    private readonly ERackClientWorker _clientWorker;
    private readonly SecsEventMessageFactory _messageFactory;
    private readonly SecsEventPublisher _secsPublisher;
    private readonly ILogger<ERackEventSink> _logger;

    public ERackEventSink(
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackClientOptions> clientOptions,
        ERackClientWorker clientWorker,
        SecsEventMessageFactory messageFactory,
        SecsEventPublisher secsPublisher,
        ILogger<ERackEventSink> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _clientOptions = clientOptions.Value;
        _clientWorker = clientWorker;
        _messageFactory = messageFactory;
        _secsPublisher = secsPublisher;
        _logger = logger;
    }

    public async Task PublishShelfStateAsync(ShelfStateEvent shelfStateEvent, CancellationToken cancellationToken)
    {
        if (UseRemoteClient)
        {
            await PublishRemoteOrLogAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.ShelfStateChanged,
                    shelfStateEvent.ShelfId,
                    ShelfStateEventPayload.FromModel(shelfStateEvent)),
                cancellationToken);
            return;
        }

        _secsPublisher.TryPublish(_messageFactory.CreateShelfStateEvent(shelfStateEvent));
    }

    public async Task PublishRfidWriteAsync(RfidWriteEvent rfidWriteEvent, CancellationToken cancellationToken)
    {
        if (UseRemoteClient)
        {
            await PublishRemoteOrLogAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.RfidWriteEvent,
                    rfidWriteEvent.ShelfId,
                    RfidWriteEventPayload.FromModel(rfidWriteEvent)),
                cancellationToken);
            return;
        }

        _secsPublisher.TryPublish(_messageFactory.CreateRfidWriteEvent(rfidWriteEvent));
    }

    public async Task PublishPrintAsync(PrintEvent printEvent, CancellationToken cancellationToken)
    {
        if (UseRemoteClient)
        {
            await PublishRemoteOrLogAsync(
                ERackWireEnvelope.Create(
                    ERackWireMessageTypes.PrintEvent,
                    printEvent.ShelfId,
                    PrintEventPayload.FromModel(printEvent)),
                cancellationToken);
            return;
        }

        _secsPublisher.TryPublish(_messageFactory.CreatePrintEvent(printEvent));
    }

    private async Task PublishRemoteOrLogAsync(
        ERackWireEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var sent = await _clientWorker.TrySendEventAsync(envelope, cancellationToken);
        if (!sent)
        {
            _logger.LogWarning(
                "ERACK client is not connected; remote event was not sent: type={MessageType}, shelf={ShelfId}",
                envelope.MessageType,
                envelope.ShelfId);
        }
    }

    private bool UseRemoteClient =>
        _runtimeOptions.IsUnitEnabled &&
        (_clientOptions.Enabled || _runtimeOptions.IsServerEnabled);
}
