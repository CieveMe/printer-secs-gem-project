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
}
