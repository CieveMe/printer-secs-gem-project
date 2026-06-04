param(
    [Parameter(Mandatory = $true)]
    [string]$PrinterName,

    [Parameter(Mandatory = $true)]
    [string]$FilePath
)

$ErrorActionPreference = "Stop"

$resolvedFile = Resolve-Path -LiteralPath $FilePath

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DocInfo
    {
        public string pDocName;
        public string pOutputFile;
        public string pDatatype;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    public static void Send(string printerName, byte[] data)
    {
        IntPtr printerHandle;
        if (!OpenPrinter(printerName, out printerHandle, IntPtr.Zero))
        {
            ThrowLastError("OpenPrinter");
        }

        try
        {
            DocInfo docInfo = new DocInfo
            {
                pDocName = "PrinterSecsGem ZPL",
                pOutputFile = null,
                pDatatype = "RAW"
            };

            if (StartDocPrinter(printerHandle, 1, ref docInfo) == 0)
            {
                ThrowLastError("StartDocPrinter");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    ThrowLastError("StartPagePrinter");
                }

                try
                {
                    int written;
                    if (!WritePrinter(printerHandle, data, data.Length, out written))
                    {
                        ThrowLastError("WritePrinter");
                    }

                    if (written != data.Length)
                    {
                        throw new InvalidOperationException("WritePrinter wrote " + written + " of " + data.Length + " bytes.");
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    private static void ThrowLastError(string action)
    {
        throw new InvalidOperationException(action + " failed. Win32Error=" + Marshal.GetLastWin32Error());
    }
}
"@

$bytes = [System.IO.File]::ReadAllBytes($resolvedFile)
[RawPrinter]::Send($PrinterName, $bytes)

Write-Host "Raw print submitted."
Write-Host "Printer: $PrinterName"
Write-Host "File: $resolvedFile"
Write-Host "Bytes: $($bytes.Length)"
