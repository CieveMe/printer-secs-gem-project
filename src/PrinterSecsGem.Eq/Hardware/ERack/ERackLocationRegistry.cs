using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PrinterSecsGem.Eq.Hardware.ERack;

public sealed class ERackLocationRegistry
{
    private readonly ERackHardwareOptions _fallbackOptions;
    private readonly IReadOnlyList<ERackLocation> _locations;

    public ERackLocationRegistry(
        IConfiguration configuration,
        IOptions<ERackHardwareOptions> fallbackOptions)
    {
        _fallbackOptions = fallbackOptions.Value;
        _locations = LoadLocations(configuration, _fallbackOptions);
    }

    public IReadOnlyList<ERackLocation> Locations => _locations;

    public ERackLocation DefaultLocation => _locations[0];

    public ERackLocation? Find(string shelfId, string locationId)
    {
        if (!string.IsNullOrWhiteSpace(locationId) &&
            !locationId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            return _locations.FirstOrDefault(location =>
                location.LocationId.Equals(locationId, StringComparison.OrdinalIgnoreCase) &&
                ShelfMatches(location, shelfId));
        }

        var shelfLocation = _locations.FirstOrDefault(location => ShelfMatches(location, shelfId));
        if (shelfLocation is not null)
        {
            return shelfLocation;
        }

        return string.IsNullOrWhiteSpace(shelfId) ? DefaultLocation : null;
    }

    public IReadOnlyList<ERackLocation> FindAll(string shelfId, string locationId)
    {
        if (!string.IsNullOrWhiteSpace(locationId) &&
            !locationId.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var location = Find(shelfId, locationId);
            return location is null ? Array.Empty<ERackLocation>() : new[] { location };
        }

        var shelfLocations = _locations
            .Where(location => ShelfMatches(location, shelfId))
            .ToArray();

        return string.IsNullOrWhiteSpace(shelfId) ? _locations : shelfLocations;
    }

    private static IReadOnlyList<ERackLocation> LoadLocations(
        IConfiguration configuration,
        ERackHardwareOptions fallbackOptions)
    {
        var locations = new List<ERackLocation>();
        var configuredLocations = configuration.GetSection("Locations").GetChildren().ToArray();
        var useGlobalPortForSingleLocation = configuredLocations.Length <= 1;
        foreach (var child in configuredLocations)
        {
            var options = child.Get<ERackLocationOptions>() ?? new ERackLocationOptions();
            var locationId = string.IsNullOrWhiteSpace(options.LocationId)
                ? child.Key
                : options.LocationId.Trim();

            if (string.IsNullOrWhiteSpace(locationId))
            {
                continue;
            }

            locations.Add(CreateLocation(locationId, options, fallbackOptions, useGlobalPortForSingleLocation));
        }

        return locations.Count > 0
            ? locations
            : new[] { CreateFallbackLocation(fallbackOptions) };
    }

    private static ERackLocation CreateFallbackLocation(ERackHardwareOptions fallbackOptions)
    {
        return new ERackLocation(
            fallbackOptions.DefaultLocationId,
            fallbackOptions.DefaultShelfId,
            fallbackOptions.PortName,
            fallbackOptions.BaudRate,
            fallbackOptions.DeviceAddress,
            fallbackOptions.InventoryMode,
            fallbackOptions.InventoryWaitTimeMilliseconds,
            fallbackOptions.InventoryWaitCount,
            fallbackOptions.KeepPortOpen,
            fallbackOptions.WriteTagStartPage,
            fallbackOptions.WriteTagWaitTimeMilliseconds,
            fallbackOptions.WriteTagWaitCount);
    }

    private static ERackLocation CreateLocation(
        string locationId,
        ERackLocationOptions options,
        ERackHardwareOptions fallbackOptions,
        bool useGlobalPortForSingleLocation)
    {
        return new ERackLocation(
            locationId,
            string.IsNullOrWhiteSpace(options.ShelfId) ? fallbackOptions.DefaultShelfId : options.ShelfId.Trim(),
            useGlobalPortForSingleLocation || string.IsNullOrWhiteSpace(options.PortName)
                ? fallbackOptions.PortName
                : options.PortName.Trim(),
            options.BaudRate ?? fallbackOptions.BaudRate,
            options.DeviceAddress ?? fallbackOptions.DeviceAddress,
            options.InventoryMode ?? fallbackOptions.InventoryMode,
            options.InventoryWaitTimeMilliseconds ?? fallbackOptions.InventoryWaitTimeMilliseconds,
            options.InventoryWaitCount ?? fallbackOptions.InventoryWaitCount,
            options.KeepPortOpen ?? fallbackOptions.KeepPortOpen,
            options.WriteTagStartPage ?? fallbackOptions.WriteTagStartPage,
            options.WriteTagWaitTimeMilliseconds ?? fallbackOptions.WriteTagWaitTimeMilliseconds,
            options.WriteTagWaitCount ?? fallbackOptions.WriteTagWaitCount);
    }

    private static bool ShelfMatches(ERackLocation location, string shelfId)
    {
        return string.IsNullOrWhiteSpace(shelfId) ||
            location.ShelfId.Equals(shelfId, StringComparison.OrdinalIgnoreCase);
    }
}
