using Microsoft.Extensions.Logging;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using Secs4Net;
using static Secs4Net.Item;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsMessageDispatcher
{
    private readonly IPrinterGateway _printerGateway;
    private readonly IHardwareGateway _hardwareGateway;
    private readonly ILogger<SecsMessageDispatcher> _logger;

    public SecsMessageDispatcher(
        IPrinterGateway printerGateway,
        IHardwareGateway hardwareGateway,
        ILogger<SecsMessageDispatcher> logger)
    {
        _printerGateway = printerGateway;
        _hardwareGateway = hardwareGateway;
        _logger = logger;
    }

    public async Task<SecsMessage?> DispatchAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatch SECS message S{Stream}F{Function}", primaryMessage.S, primaryMessage.F);

        return (primaryMessage.S, primaryMessage.F) switch
        {
            (1, 1) => CreateS1F2(),
            (1, 3) => CreateS1F4(),
            (5, 11) => await HandleShelfStatusQueryAsync(primaryMessage, cancellationToken),
            (8, 3) => await HandlePrintAsync(primaryMessage, cancellationToken),
            (10, 11) => await HandleWriteTagAsync(primaryMessage, cancellationToken),
            _ => null
        };
    }

    private async Task<SecsMessage> HandlePrintAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var command = new PrintCommand(
            SecsItemReader.ReadAscii(primaryMessage, 0, "SHELF001"),
            SecsItemReader.ReadAscii(primaryMessage, 1, "PRINTER001"),
            SecsItemReader.ReadAscii(primaryMessage, 2),
            SecsItemReader.ReadU1(primaryMessage, 3, 1));

        var result = await _printerGateway.PrintAsync(command, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

        return new SecsMessage(8, 4)
        {
            Name = "PrintResult",
            SecsItem = L(
                A(command.ShelfId),
                A(command.PrinterId),
                U1(resultCode),
                A(result.Description))
        };
    }

    private async Task<SecsMessage> HandleWriteTagAsync(SecsMessage primaryMessage, CancellationToken cancellationToken)
    {
        var command = new TagWriteCommand(
            SecsItemReader.ReadAscii(primaryMessage, 0, "SHELF001"),
            SecsItemReader.ReadAscii(primaryMessage, 1, "LOC001"),
            SecsItemReader.ReadAscii(primaryMessage, 2));

        var result = await _hardwareGateway.WriteTagAsync(command, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

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
            SecsItemReader.ReadAscii(primaryMessage, 1, "ALL"));

        var result = await _hardwareGateway.QueryShelfStatusAsync(query, cancellationToken);
        var resultCode = result.Success ? (byte)0 : result.Code;

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
}
