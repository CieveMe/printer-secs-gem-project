using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware;

public sealed class MockHardwareGateway : IHardwareGateway
{
    private readonly ConcurrentDictionary<string, string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly MockHardwareOptions _options;
    private readonly ILogger<MockHardwareGateway> _logger;

    public MockHardwareGateway(IOptions<MockHardwareOptions> options, ILogger<MockHardwareGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken)
    {
        var shelfId = NormalizeText(command.ShelfId, _options.DefaultShelfId);
        var locationId = NormalizeText(command.LocationId, _options.DefaultLocationId);

        _logger.LogInformation("Mock write tag: shelf={ShelfId}, location={LocationId}, tag={Tag}",
            shelfId, locationId, command.Tag);

        if (string.IsNullOrWhiteSpace(command.Tag))
        {
            return Task.FromResult(OperationResult.Fail(2, "tag is empty"));
        }

        _tags[BuildKey(shelfId, locationId)] = command.Tag;
        return Task.FromResult(OperationResult.Ok("tag written"));
    }

    public Task<ShelfStatusResult> QueryShelfStatusAsync(ShelfStatusQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Mock query shelf status: shelf={ShelfId}, location={LocationId}",
            query.ShelfId, query.LocationId);

        var shelfId = NormalizeText(query.ShelfId, _options.DefaultShelfId);
        var locationId = query.LocationId.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? _options.DefaultLocationId
            : NormalizeText(query.LocationId, _options.DefaultLocationId);
        var tag = _tags.TryGetValue(BuildKey(shelfId, locationId), out var writtenTag)
            ? writtenTag
            : _options.DefaultTag;

        var location = new ShelfLocationStatus(locationId, TrimToReadLength(tag, query.ReadLengthBytes), true);
        return Task.FromResult(ShelfStatusResult.Ok(shelfId, new[] { location }));
    }

    private static string BuildKey(string shelfId, string locationId)
    {
        return $"{shelfId}|{locationId}";
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string TrimToReadLength(string tag, int readLengthBytes)
    {
        var readLength = readLengthBytes <= 0 ? 32 : Math.Min(readLengthBytes, 32);
        return tag.Length <= readLength ? tag : tag[..readLength];
    }
}
