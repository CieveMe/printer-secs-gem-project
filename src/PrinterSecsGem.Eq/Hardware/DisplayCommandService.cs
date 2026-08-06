using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware;

public sealed class DisplayCommandService
{
    private const string FormatErrorDescription = "Display Content Format Error";

    private readonly IHardwareGateway _hardwareGateway;
    private readonly ERackSensorDisplayOptions _options;

    public DisplayCommandService(
        IHardwareGateway hardwareGateway,
        IOptions<ERackSensorDisplayOptions> options)
    {
        _hardwareGateway = hardwareGateway;
        _options = options.Value;
    }

    public async Task<OperationResult> ExecuteAsync(
        DisplayCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = Validate(command);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var result = await _hardwareGateway.SetDisplayAsync(command, cancellationToken);
        if (result.Success)
        {
            return OperationResult.Ok("Set Success");
        }

        if (IsLocationNotFound(result))
        {
            return OperationResult.Fail(1, "Location Not Found");
        }

        var resultCode = result.Code is 0 or 1 or 2 ? (byte)7 : result.Code;
        var description = string.IsNullOrWhiteSpace(result.Description)
            ? $"Display Failed: code {resultCode}"
            : result.Description;
        return OperationResult.Fail(resultCode, description);
    }

    public OperationResult Validate(DisplayCommand command)
    {
        return IsValid(command)
            ? OperationResult.Ok("display command valid")
            : OperationResult.Fail(2, FormatErrorDescription);
    }

    private bool IsValid(DisplayCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ShelfId) ||
            string.IsNullOrWhiteSpace(command.LocationId))
        {
            return false;
        }

        var content = command.Content ?? string.Empty;
        if (content.Any(character => character > 0x7F || char.IsControl(character)))
        {
            return false;
        }

        var maxBytes = Math.Clamp(_options.DisplayMaxBytes, 1, 512);
        return content.Length <= maxBytes;
    }

    private static bool IsLocationNotFound(OperationResult result)
    {
        return result.Code == 1 ||
            (result.Code == 6 &&
                result.Description.Contains("location not configured", StringComparison.OrdinalIgnoreCase));
    }
}
