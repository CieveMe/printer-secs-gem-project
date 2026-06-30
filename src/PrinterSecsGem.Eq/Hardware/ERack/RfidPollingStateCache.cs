using System.Collections.Concurrent;
using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class RfidPollingStateCache
{
    private readonly ERackLocationRegistry _locations;
    private readonly ConcurrentDictionary<string, CachedRfidPollingState> _states = new(StringComparer.OrdinalIgnoreCase);

    public RfidPollingStateCache(ERackLocationRegistry locations)
    {
        _locations = locations;
    }

    public void Update(ERackLocation location, string tag, bool isLoaded)
    {
        var normalizedTag = tag?.Trim() ?? string.Empty;
        _states[BuildKey(location.ShelfId, location.LocationId)] = new CachedRfidPollingState(
            normalizedTag,
            isLoaded && !string.IsNullOrWhiteSpace(normalizedTag),
            DateTimeOffset.Now);
    }

    public ShelfStatusResult Query(ShelfStatusQuery query)
    {
        var shelfId = string.IsNullOrWhiteSpace(query.ShelfId)
            ? _locations.DefaultLocation.ShelfId
            : query.ShelfId.Trim();
        var locationId = string.IsNullOrWhiteSpace(query.LocationId)
            ? "ALL"
            : query.LocationId.Trim();

        var locations = _locations.FindAll(shelfId, locationId);
        if (locations.Count == 0)
        {
            return ShelfStatusResult.Fail(shelfId, 6, $"location not configured: {locationId}");
        }

        var statuses = locations
            .Select(location =>
            {
                if (_states.TryGetValue(BuildKey(location.ShelfId, location.LocationId), out var state))
                {
                    return new ShelfLocationStatus(location.LocationId, state.Tag, state.IsLoaded);
                }

                return new ShelfLocationStatus(location.LocationId, string.Empty, false);
            })
            .ToArray();

        return ShelfStatusResult.Ok(shelfId, statuses);
    }

    private static string BuildKey(string shelfId, string locationId)
    {
        return $"{shelfId}|{locationId}";
    }

    private sealed record CachedRfidPollingState(string Tag, bool IsLoaded, DateTimeOffset UpdatedAt);
}
