using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Secs4Net;

namespace PrinterSecsGem.Eq.Secs;

internal static class SecsItemReader
{
    public static string ReadAscii(SecsMessage message, int index, string fallback = "")
    {
        try
        {
            return message.SecsItem?[index].GetString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static byte ReadU1(SecsMessage message, int index, byte fallback = 0)
    {
        try
        {
            return message.SecsItem?[index].FirstValue<byte>() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static string ReadAsciiWithRawLog(
        SecsMessage message,
        int index,
        string fieldName,
        string fallback,
        ILogger logger)
    {
        var item = TryGetItem(message, index);
        var parsed = fallback;
        if (item is not null)
        {
            try
            {
                parsed = item.GetString() ?? fallback;
            }
            catch
            {
                parsed = fallback;
            }
        }

        var bytes = Encoding.ASCII.GetBytes(parsed);
        LogRawItem(logger, fieldName, index, item, bytes, parsed);
        return parsed;
    }

    public static byte ReadU1WithRawLog(
        SecsMessage message,
        int index,
        string fieldName,
        byte fallback,
        ILogger logger)
    {
        var item = TryGetItem(message, index);
        var parsed = fallback;
        var bytes = Array.Empty<byte>();
        if (item is not null)
        {
            try
            {
                parsed = item.FirstValue<byte>();
            }
            catch
            {
                parsed = fallback;
            }

            try
            {
                bytes = item.GetMemory<byte>().ToArray();
            }
            catch
            {
                bytes = new[] { parsed };
            }
        }

        LogRawItem(logger, fieldName, index, item, bytes, parsed.ToString());
        return parsed;
    }

    private static Item? TryGetItem(SecsMessage message, int index)
    {
        try
        {
            return message.SecsItem?[index];
        }
        catch
        {
            return null;
        }
    }

    private static void LogRawItem(
        ILogger logger,
        string fieldName,
        int index,
        Item? item,
        byte[] bytes,
        string parsed)
    {
        var itemLength = GetItemLengthOrFallback(item, bytes.Length);
        logger.LogInformation(
            "S5F11 item raw: field={FieldName}, index={Index}, itemLength={ItemLength}, bytesLength={BytesLength}, rawHex={RawHex}, parsed={Parsed}",
            fieldName,
            index,
            itemLength,
            bytes.Length,
            ToHex(bytes),
            parsed);
    }

    private static int GetItemLengthOrFallback(Item? item, int fallback)
    {
        if (item is null)
        {
            return fallback;
        }

        try
        {
            var property = item.GetType().GetProperty(
                "ItemLength",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(item) is { } value)
            {
                return Convert.ToInt32(value);
            }
        }
        catch
        {
        }

        return fallback;
    }

    private static string ToHex(byte[] data)
    {
        return data.Length == 0
            ? "<empty>"
            : BitConverter.ToString(data).Replace("-", " ");
    }
}
