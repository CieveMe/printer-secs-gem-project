using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.StatusUi;
using Secs4Net;
using static Secs4Net.Item;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsMessageDispatcher
{
    private readonly IPrinterGateway _printerGateway;
    private readonly IHardwareGateway _hardwareGateway;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly IERackUnitRouter _unitRouter;
    private readonly IERackEventSink _eventSink;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<SecsMessageDispatcher> _logger;

    public SecsMessageDispatcher(
        IPrinterGateway printerGateway,
        IHardwareGateway hardwareGateway,
        IOptions<RuntimeOptions> runtimeOptions,
        IERackUnitRouter unitRouter,
        IERackEventSink eventSink,
        StatusUiEventBus statusEvents,
        ILogger<SecsMessageDispatcher> logger)
    {
        _printerGateway = printerGateway;
        _hardwareGateway = hardwareGateway;
        _runtimeOptions = runtimeOptions.Value;
        _unitRouter = unitRouter;
        _eventSink = eventSink;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    public async Task<SecsMessage?> DispatchAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatch SECS message S{Stream}F{Function}", primaryMessage.S, primaryMessage.F);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"Received Host message S{primaryMessage.S}F{primaryMessage.F}.");

        var reply = (primaryMessage.S, primaryMessage.F) switch
        {
            (1, 1) => CreateS1F2(),
            (1, 3) => CreateS1F4(),
            (5, 11) => await HandleShelfStatusQueryAsync(primaryMessage, cancellationToken),
            (8, 3) => await HandlePrintAsync(primaryMessage, cancellationToken),
            (10, 11) => await HandleWriteTagAsync(primaryMessage, cancellationToken),
            _ => null
        };

        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            reply is null
                ? $"No handler for S{primaryMessage.S}F{primaryMessage.F}."
                : $"Reply S{reply.S}F{reply.F} prepared for Host.");

        return reply;
    }

    private async Task<SecsMessage> HandlePrintAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var command = new PrintCommand(
            SecsItemReader.ReadAscii(primaryMessage, 0, "SHELF001"),
            SecsItemReader.ReadAscii(primaryMessage, 1, "PRINTER001"),
            SecsItemReader.ReadAscii(primaryMessage, 2),
            SecsItemReader.ReadU1(primaryMessage, 3, 1));

        _logger.LogInformation(
            "Handle print command: shelf={ShelfId}, printer={PrinterId}, content={Content}, copies={Copies}",
            command.ShelfId,
            command.PrinterId,
            command.Content,
            command.Copies);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S8F3 print command: content={command.Content}, copies={command.Copies}.");

        var result = UseRemoteRouting
            ? await _unitRouter.PrintAsync(command, cancellationToken)
            : await _printerGateway.PrintAsync(command, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;
        var secsDescription = PrintProtocolResult.GetSecsDescription(resultCode);

        _logger.LogInformation(
            "Print command result: success={Success}, code={Code}, description={Description}",
            result.Success,
            resultCode,
            result.Description);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S8F3 print result: code={resultCode}, description={result.Description}.");
        _statusEvents.Publish(
            StatusUiEventCategories.LastPrint,
            result.Success
                ? result.Description
                : $"Print failed: code={resultCode}, {result.Description}");
        if (!UseRemoteRouting)
        {
            await _eventSink.PublishPrintAsync(
                new PrintEvent(
                    command.ShelfId,
                    command.PrinterId,
                    command.Content,
                    resultCode,
                    secsDescription,
                    DateTimeOffset.Now),
                cancellationToken);
        }

        return new SecsMessage(8, 4)
        {
            Name = "PrintResult",
            SecsItem = L(
                A(command.ShelfId),
                A(command.PrinterId),
                U1(resultCode),
                A(secsDescription))
        };
    }

    private async Task<SecsMessage> HandleWriteTagAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var command = new TagWriteCommand(
            SecsItemReader.ReadAscii(primaryMessage, 0, "SHELF001"),
            SecsItemReader.ReadAscii(primaryMessage, 1, "LOC001"),
            SecsItemReader.ReadAscii(primaryMessage, 2));

        _logger.LogInformation(
            "Handle write tag command: shelf={ShelfId}, location={LocationId}, tag={Tag}",
            command.ShelfId,
            command.LocationId,
            command.Tag);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S10F11 write tag command: location={command.LocationId}, tag={command.Tag}.");

        var result = UseRemoteRouting
            ? await _unitRouter.WriteTagAsync(command, cancellationToken)
            : await _hardwareGateway.WriteTagAsync(command, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

        _logger.LogInformation(
            "Write tag result: success={Success}, code={Code}, description={Description}",
            result.Success,
            resultCode,
            result.Description);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S10F11 write tag result: code={resultCode}, description={result.Description}.");
        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            result.Success
                ? $"Written: {command.Tag}"
                : $"Write failed: code={resultCode}, {result.Description}");
        if (!UseRemoteRouting)
        {
            await _eventSink.PublishRfidWriteAsync(
                new RfidWriteEvent(
                    command.ShelfId,
                    command.LocationId,
                    command.Tag,
                    resultCode,
                    result.Description,
                    DateTimeOffset.Now),
                cancellationToken);
        }

        return new SecsMessage(10, 12)
        {
            Name = "WriteTagResult",
            SecsItem = L(
                A(command.ShelfId),
                A(command.LocationId),
                U1(resultCode),
                A(result.Description))
        };
    }

    private async Task<SecsMessage> HandleShelfStatusQueryAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var query = new ShelfStatusQuery(
            SecsItemReader.ReadAscii(primaryMessage, 0, "SHELF001"),
            SecsItemReader.ReadAscii(primaryMessage, 1, "ALL"),
            SecsItemReader.ReadU1(primaryMessage, 2, 32));

        _logger.LogInformation(
            "Handle shelf status query: shelf={ShelfId}, location={LocationId}, readLength={ReadLength}",
            query.ShelfId,
            query.LocationId,
            query.ReadLengthBytes);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S5F11 shelf status query: shelf={query.ShelfId}, location={query.LocationId}, readLength={query.ReadLengthBytes}.");

        var result = UseRemoteRouting
            ? await _unitRouter.QueryShelfStatusAsync(query, cancellationToken)
            : await _hardwareGateway.QueryShelfStatusAsync(query, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

        _logger.LogInformation(
            "Shelf status result: success={Success}, code={Code}, shelf={ShelfId}, locations={LocationCount}",
            result.Success,
            resultCode,
            result.ShelfId,
            result.Locations.Count);
        var firstLocation = result.Locations.FirstOrDefault();
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S5F11 shelf status result: code={resultCode}, tag={firstLocation?.Tag ?? string.Empty}, loaded={firstLocation?.IsLoaded}.");
        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            result.Success
                ? string.IsNullOrWhiteSpace(firstLocation?.Tag)
                    ? "No tag"
                    : firstLocation.Tag
                : $"Read failed: code={resultCode}");

        return new SecsMessage(5, 12)
        {
            Name = "ShelfStatus",
            SecsItem = L(
                A(result.ShelfId),
                L(result.Locations.Select(location =>
                    L(
                        A(location.LocationId),
                        A(location.Tag),
                        U1(location.IsLoaded ? (byte)1 : (byte)0))).ToArray()),
                U1(resultCode))
        };
    }

    private static SecsMessage CreateS1F2()
    {
        return new SecsMessage(1, 2)
        {
            Name = "AreYouThereReply",
            SecsItem = L()
        };
    }

    private static SecsMessage CreateS1F4()
    {
        return new SecsMessage(1, 4)
        {
            Name = "EquipmentStatus",
            SecsItem = L(
                A("ONLINE"),
                U1(0))
        };
    }

    private bool UseRemoteRouting => _runtimeOptions.IsServerEnabled;
}
