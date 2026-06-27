using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Printing;

public static class PrintProtocolResult
{
    public const byte Success = 0;
    public const byte PrinterOffline = 1;
    public const byte PaperOut = 2;
    public const byte EmptyContent = 3;

    public const string SuccessDescription = "打印成功";
    public const string PrinterOfflineDescription = "打印机脱机";
    public const string PaperOutDescription = "缺纸";
    public const string EmptyContentDescription = "内容为空";

    public const string SuccessSecsDescription = "PRINT_SUCCESS";
    public const string PrinterOfflineSecsDescription = "PRINTER_OFFLINE";
    public const string PaperOutSecsDescription = "PAPER_EMPTY";
    public const string EmptyContentSecsDescription = "CONTENT_EMPTY";
    public const string UnitNotOnlineSecsDescription = "UNIT_NOT_ONLINE";
    public const string PrintFailedSecsDescription = "PRINT_FAILED";

    public static OperationResult Ok()
    {
        return OperationResult.Ok(SuccessDescription);
    }

    public static OperationResult Fail(byte code)
    {
        return OperationResult.Fail(code, GetDescription(code));
    }

    public static string GetDescription(byte code)
    {
        return code switch
        {
            Success => SuccessDescription,
            PrinterOffline => PrinterOfflineDescription,
            PaperOut => PaperOutDescription,
            EmptyContent => EmptyContentDescription,
            _ => PrinterOfflineDescription
        };
    }

    public static string GetSecsDescription(byte code)
    {
        return code switch
        {
            Success => SuccessSecsDescription,
            PrinterOffline => PrinterOfflineSecsDescription,
            PaperOut => PaperOutSecsDescription,
            EmptyContent => EmptyContentSecsDescription,
            6 => UnitNotOnlineSecsDescription,
            _ => PrintFailedSecsDescription
        };
    }
}
