using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq.Hardware;

public sealed class RfidWriteWorkflow
{
    private readonly IHardwareGateway _hardwareGateway;
    private readonly DisplayCommandService _displayCommands;
    private readonly ERackSensorDisplayOptions _options;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<RfidWriteWorkflow> _logger;

    public RfidWriteWorkflow(
        IHardwareGateway hardwareGateway,
        DisplayCommandService displayCommands,
        IOptions<ERackSensorDisplayOptions> options,
        StatusUiEventBus statusEvents,
        ILogger<RfidWriteWorkflow> logger)
    {
        _hardwareGateway = hardwareGateway;
        _displayCommands = displayCommands;
        _options = options.Value;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(
        TagWriteCommand command,
        CancellationToken cancellationToken)
    {
        var writeResult = await _hardwareGateway.WriteTagAsync(command, cancellationToken);
        if (!writeResult.Success || !ShouldVerifyAndRefresh)
        {
            return writeResult;
        }

        var expectedBytes = Encoding.ASCII.GetBytes(command.Tag ?? string.Empty);
        var status = await _hardwareGateway.QueryShelfStatusAsync(
            new ShelfStatusQuery(command.ShelfId, command.LocationId, expectedBytes.Length),
            cancellationToken);

        if (!status.Success)
        {
            _logger.LogWarning(
                "RFID write verification read failed: shelf={ShelfId}, location={LocationId}, expectedLength={ExpectedLength}, expectedHex={ExpectedHex}, code={Code}, description={Description}",
                command.ShelfId,
                command.LocationId,
                expectedBytes.Length,
                ToHex(expectedBytes),
                status.Code,
                status.Description);
            _statusEvents.Publish(
                StatusUiEventCategories.RfidStatus,
                $"RFID verify read failed: {status.Description}");
            return OperationResult.Fail(7, "RFID Verify Read Failed");
        }

        var locationStatus = status.Locations.FirstOrDefault(location =>
                location.LocationId.Equals(command.LocationId, StringComparison.OrdinalIgnoreCase))
            ?? status.Locations.FirstOrDefault();
        var actualTag = locationStatus?.Tag ?? string.Empty;
        var actualBytes = Encoding.ASCII.GetBytes(actualTag);
        var matches = actualBytes.Length >= expectedBytes.Length &&
            actualBytes.AsSpan(0, expectedBytes.Length).SequenceEqual(expectedBytes);

        _logger.Log(
            matches ? LogLevel.Information : LogLevel.Warning,
            "RFID write verification result: shelf={ShelfId}, location={LocationId}, matched={Matched}, expectedLength={ExpectedLength}, expectedHex={ExpectedHex}, receivedLength={ReceivedLength}, receivedHex={ReceivedHex}",
            command.ShelfId,
            command.LocationId,
            matches,
            expectedBytes.Length,
            ToHex(expectedBytes),
            actualBytes.Length,
            ToHex(actualBytes));

        if (!matches)
        {
            _statusEvents.Publish(
                StatusUiEventCategories.RfidStatus,
                "RFID verify mismatch");
            return OperationResult.Fail(7, "RFID Verify Mismatch");
        }

        var verifiedTag = Encoding.ASCII.GetString(actualBytes, 0, expectedBytes.Length);
        var displayText = ERackTagDecoder.ToDisplayText(verifiedTag);
        var displayResult = await _displayCommands.ExecuteAsync(
            new DisplayCommand(command.ShelfId, command.LocationId, displayText),
            cancellationToken);

        if (displayResult.Success)
        {
            _logger.LogInformation(
                "RFID write verification display updated: shelf={ShelfId}, location={LocationId}, displayText={DisplayText}",
                command.ShelfId,
                command.LocationId,
                displayText);
            _statusEvents.Publish(
                StatusUiEventCategories.DisplayStatus,
                string.IsNullOrEmpty(displayText)
                    ? "Display cleared"
                    : $"Display text sent: {displayText}");
        }
        else
        {
            _logger.LogWarning(
                "RFID write verification succeeded but display update failed: shelf={ShelfId}, location={LocationId}, code={Code}, description={Description}",
                command.ShelfId,
                command.LocationId,
                displayResult.Code,
                displayResult.Description);
            _statusEvents.Publish(
                StatusUiEventCategories.DisplayStatus,
                $"Display failed: {displayResult.Description}");
        }

        return writeResult;
    }

    private bool ShouldVerifyAndRefresh => _options.Enabled && !_options.IsRfidPollingMode;

    private static string ToHex(ReadOnlySpan<byte> data)
    {
        return data.IsEmpty
            ? "<empty>"
            : BitConverter.ToString(data.ToArray()).Replace("-", " ");
    }
}
