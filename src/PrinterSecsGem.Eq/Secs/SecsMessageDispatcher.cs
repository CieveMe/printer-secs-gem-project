using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
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
    private readonly ERackSensorDisplayOptions _sensorDisplayOptions;
    private readonly RfidPollingStateCache _rfidPollingCache;
    private readonly IERackUnitRouter _unitRouter;
    private readonly IERackEventSink _eventSink;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<SecsMessageDispatcher> _logger;

    public SecsMessageDispatcher(
        IPrinterGateway printerGateway,
        IHardwareGateway hardwareGateway,
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackSensorDisplayOptions> sensorDisplayOptions,
        RfidPollingStateCache rfidPollingCache,
        IERackUnitRouter unitRouter,
        IERackEventSink eventSink,
        StatusUiEventBus statusEvents,
        ILogger<SecsMessageDispatcher> logger)
    {
        _printerGateway = printerGateway;
        _hardwareGateway = hardwareGateway;
        _runtimeOptions = runtimeOptions.Value;
        _sensorDisplayOptions = sensorDisplayOptions.Value;
        _rfidPollingCache = rfidPollingCache;
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
        var protocolResult = ToWriteTagReply(result);

        _logger.LogInformation(
            "Write tag result: success={Success}, code={Code}, description={Description}",
            result.Success,
            protocolResult.Code,
            protocolResult.Description);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S10F11 write tag result: code={protocolResult.Code}, description={protocolResult.Description}.");
        _statusEvents.Publish(
            StatusUiEventCategories.RfidStatus,
            result.Success
                ? $"Written: {command.Tag}"
                : $"Write failed: code={protocolResult.Code}, {protocolResult.Description}");
        if (!UseRemoteRouting)
        {
            await _eventSink.PublishRfidWriteAsync(
                new RfidWriteEvent(
                    command.ShelfId,
                    command.LocationId,
                    command.Tag,
                    protocolResult.Code,
                    protocolResult.Description,
                    DateTimeOffset.Now),
                cancellationToken);
        }

        return new SecsMessage(10, 12)
        {
            Name = "WriteTagResult",
            SecsItem = L(
                A(command.ShelfId),
                A(command.LocationId),
                U1(protocolResult.Code),
                A(protocolResult.Description))
        };
    }

    private async Task<SecsMessage> HandleShelfStatusQueryAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var query = new ShelfStatusQuery(
            SecsItemReader.ReadAsciiWithRawLog(primaryMessage, 0, "ShelfId", "SHELF001", _logger),
            SecsItemReader.ReadAsciiWithRawLog(primaryMessage, 1, "LocationId", "ALL", _logger),
            SecsItemReader.ReadU1WithRawLog(primaryMessage, 2, "ReadLengthBytes", 32, _logger));

        _logger.LogInformation(
            "Handle shelf status query: shelf={ShelfId}, location={LocationId}, readLength={ReadLength}",
            query.ShelfId,
            query.LocationId,
            query.ReadLengthBytes);
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S5F11 shelf status query: shelf={query.ShelfId}, location={query.LocationId}, readLength={query.ReadLengthBytes}.");

        var result = UseRfidPollingCache
            ? _rfidPollingCache.Query(query)
            : UseRemoteRouting
            ? await _unitRouter.QueryShelfStatusAsync(query, cancellationToken)
            : await _hardwareGateway.QueryShelfStatusAsync(query, cancellationToken);
        var resultCode = ToShelfStatusReplyCode(result);
        var resultDescription = ToShelfStatusReplyDescription(result, resultCode);

        if (UseRfidPollingCache)
        {
            _logger.LogInformation(
                "S5F11 shelf status query returned RFID polling cache: shelf={ShelfId}, location={LocationId}, locations={LocationCount}",
                result.ShelfId,
                query.LocationId,
                result.Locations.Count);
        }

        _logger.LogInformation(
            "Shelf status result: success={Success}, code={Code}, shelf={ShelfId}, locations={LocationCount}",
            result.Success,
            resultCode,
            result.ShelfId,
            result.Locations.Count);
        var firstLocation = result.Locations.FirstOrDefault();
        _statusEvents.Publish(
            StatusUiEventCategories.SecsLog,
            $"S5F11 shelf status result: code={resultCode}, description={resultDescription}, tag={firstLocation?.Tag ?? string.Empty}, loaded={firstLocation?.IsLoaded}.");
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
                U1(resultCode),
                A(resultDescription))
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

    private bool UseRfidPollingCache => _runtimeOptions.IsUnitEnabled && _sensorDisplayOptions.IsRfidPollingMode;

    private static byte ToShelfStatusReplyCode(ShelfStatusResult result)
    {
        if (result.Success)
        {
            return 0;
        }

        return result.Code switch
        {
            1 or 6 => 1,
            _ => 2
        };
    }

    private static ProtocolReply ToWriteTagReply(OperationResult result)
    {
        if (result.Success)
        {
            return new ProtocolReply(0, "Write Success");
        }

        if (IsLocationNotFound(result))
        {
            return new ProtocolReply(1, "Location Not Found");
        }

        if (IsRfidFormatError(result))
        {
            return new ProtocolReply(2, "RFID Format Error");
        }

        return new ProtocolReply(
            result.Code,
            ToAsciiProtocolDescription(result.Description, $"Write Failed: code {result.Code}"));
    }

    private static string ToShelfStatusReplyDescription(ShelfStatusResult result, byte resultCode)
    {
        return resultCode switch
        {
            0 => "Read Success",
            1 => "Location Not Found",
            2 => "Read Failed",
            _ => ToAsciiProtocolDescription(result.Description, $"Read Failed: code {resultCode}")
        };
    }

    private static bool IsLocationNotFound(OperationResult result)
    {
        return result.Code == 1 ||
            (result.Code == 6 &&
                result.Description.Contains("location not configured", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRfidFormatError(OperationResult result)
    {
        return result.Code is 2 or 3;
    }

    private static string ToAsciiProtocolDescription(string description, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(description)
            ? fallback
            : description.Trim();

        text = text
            .Replace("货架编号为空", "Shelf ID Empty", StringComparison.OrdinalIgnoreCase)
            .Replace("货架未在线", "Shelf Not Online", StringComparison.OrdinalIgnoreCase)
            .Replace("ERACK单元响应超时", "ERACK Unit Timeout", StringComparison.OrdinalIgnoreCase)
            .Replace("ERACK单元转发失败", "ERACK Unit Route Failed", StringComparison.OrdinalIgnoreCase);

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(character is >= ' ' and <= '~' ? character : ' ');
        }

        var ascii = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(ascii) ? fallback : ascii;
    }

    private sealed record ProtocolReply(byte Code, string Description);
}
