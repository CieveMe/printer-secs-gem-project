using System.Text.Json;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackWireEnvelope
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    public string MessageType { get; set; } = string.Empty;

    public string ShelfId { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public static ERackWireEnvelope Create<TPayload>(
        string messageType,
        string shelfId,
        TPayload payload,
        string? messageId = null)
    {
        return new ERackWireEnvelope
        {
            MessageId = string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId,
            MessageType = messageType,
            ShelfId = shelfId,
            Payload = JsonSerializer.SerializeToElement(payload, ERackWireProtocol.JsonOptions),
            Timestamp = DateTimeOffset.Now
        };
    }

    public TPayload ReadPayload<TPayload>()
    {
        return Payload.Deserialize<TPayload>(ERackWireProtocol.JsonOptions)
            ?? throw new InvalidOperationException($"Payload cannot be parsed as {typeof(TPayload).Name}");
    }
}
