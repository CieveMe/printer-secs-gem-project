using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware;

public sealed class MockHardwareGateway : IHardwareGateway
{
    private readonly MockHardwareOptions _options;
    private readonly ILogger<MockHardwareGateway> _logger;

    public MockHardwareGateway(IOptions<MockHardwareOptions> options, ILogger<MockHardwareGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Mock write tag: shelf={ShelfId}, location={LocationId}, tag={Tag}",
            command.ShelfId, command.LocationId, command.Tag);

        if (string.IsNullOrWhiteSpace(command.Tag))
        {
            return Task.FromResult(OperationResult.Fail(2, "tag is empty"));
        }

        return Task.FromResult(OperationResult.Ok("tag written"));
    }

    public Task<ShelfStatusResult> QueryShelfStatusAsync(ShelfStatusQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Mock query shelf status: shelf={ShelfId}, location={LocationId}",
            query.ShelfId, query.LocationId);

        var shelfId = string.IsNullOrWhiteSpace(query.ShelfId) ? _options.DefaultShelfId : query.ShelfId;
        var locationId = query.LocationId.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? _options.DefaultLocationId
            : query.LocationId;

        var location = new ShelfLocationStatus(locationId, _options.DefaultTag, true);
        return Task.FromResult(ShelfStatusResult.Ok(shelfId, new[] { location }));
    }
}
