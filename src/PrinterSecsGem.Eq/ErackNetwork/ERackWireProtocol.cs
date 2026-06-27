using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PrinterSecsGem.Eq.ErackNetwork;

public static class ERackWireProtocol
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static async Task<ERackWireEnvelope?> ReadAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ERackWireEnvelope>(line, JsonOptions);
    }

    public static async Task WriteAsync(
        StreamWriter writer,
        ERackWireEnvelope envelope,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public static StreamReader CreateReader(NetworkStream stream)
    {
        return new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
    }

    public static StreamWriter CreateWriter(NetworkStream stream)
    {
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true)
        {
            NewLine = "\n",
            AutoFlush = false
        };
    }
}
