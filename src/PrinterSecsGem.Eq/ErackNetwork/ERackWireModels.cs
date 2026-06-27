using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed record ERackWireLocation(string LocationId);

public sealed record RegisterUnitPayload(
    string UnitId,
    string ShelfId,
    IReadOnlyList<ERackWireLocation> Locations);

public sealed record BasicResultPayload(bool Success, byte Code, string Description);

public sealed record ReadShelfStatusPayload(string ShelfId, string LocationId, int ReadLengthBytes);

public sealed record ShelfLocationStatusPayload(string LocationId, string Tag, bool IsLoaded);

public sealed record ShelfStatusResultPayload(
    bool Success,
    string ShelfId,
    IReadOnlyList<ShelfLocationStatusPayload> Locations,
    byte Code,
    string Description)
{
    public ShelfStatusResult ToModel()
    {
        return new ShelfStatusResult(
            Success,
            ShelfId,
            Locations.Select(location =>
                new ShelfLocationStatus(location.LocationId, location.Tag, location.IsLoaded)).ToArray(),
            Code,
            Description);
    }

    public static ShelfStatusResultPayload FromModel(ShelfStatusResult result)
    {
        return new ShelfStatusResultPayload(
            result.Success,
            result.ShelfId,
            result.Locations.Select(location =>
                new ShelfLocationStatusPayload(location.LocationId, location.Tag, location.IsLoaded)).ToArray(),
            result.Code,
            result.Description);
    }
}

public sealed record WriteRfidPayload(string ShelfId, string LocationId, string Tag);

public sealed record PrintPayload(string ShelfId, string PrinterId, string Content, byte Copies);

public sealed record RfidWriteEventPayload(
    string ShelfId,
    string LocationId,
    string Tag,
    byte ResultCode,
    string Description,
    DateTimeOffset Timestamp)
{
    public RfidWriteEvent ToModel()
    {
        return new RfidWriteEvent(ShelfId, LocationId, Tag, ResultCode, Description, Timestamp);
    }

    public static RfidWriteEventPayload FromModel(RfidWriteEvent value)
    {
        return new RfidWriteEventPayload(
            value.ShelfId,
            value.LocationId,
            value.Tag,
            value.ResultCode,
            value.Description,
            value.Timestamp);
    }
}

public sealed record PrintEventPayload(
    string ShelfId,
    string PrinterId,
    string Content,
    byte ResultCode,
    string Description,
    DateTimeOffset Timestamp)
{
    public PrintEvent ToModel()
    {
        return new PrintEvent(ShelfId, PrinterId, Content, ResultCode, Description, Timestamp);
    }

    public static PrintEventPayload FromModel(PrintEvent value)
    {
        return new PrintEventPayload(
            value.ShelfId,
            value.PrinterId,
            value.Content,
            value.ResultCode,
            value.Description,
            value.Timestamp);
    }
}

public sealed record ShelfStateEventPayload(
    string ShelfId,
    string LocationId,
    string Tag,
    bool IsLoaded,
    DateTimeOffset Timestamp)
{
    public ShelfStateEvent ToModel()
    {
        return new ShelfStateEvent(ShelfId, LocationId, Tag, IsLoaded, Timestamp);
    }

    public static ShelfStateEventPayload FromModel(ShelfStateEvent value)
    {
        return new ShelfStateEventPayload(
            value.ShelfId,
            value.LocationId,
            value.Tag,
            value.IsLoaded,
            value.Timestamp);
    }
}
