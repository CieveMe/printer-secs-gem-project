@echo off
setlocal
cd /d "%~dp0"

set "SDK=%~dp0zebra-command-line\SdkApi.Desktop.CommandLine.dll"
if not exist "%SDK%" set "SDK=%~dp0command_line\SdkApi.Desktop.CommandLine.dll"

if not exist "%SDK%" (
  echo Zebra command line SDK not found.
  pause
  exit /b 1
)

echo Discovering USB Zebra printer address...
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$lines = & dotnet '%SDK%' discover --usb; $lines | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('USB', [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith('\\?\usb', [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1"`) do set "USB_ADDRESS=%%A"

if "%USB_ADDRESS%"=="" (
  echo USB printer was not discovered.
  pause
  exit /b 1
)

echo.
echo Raw USB address:
echo %USB_ADDRESS%
echo.
echo App.config line:
powershell -NoProfile -Command "$raw = '%USB_ADDRESS%'; $escaped = [System.Security.SecurityElement]::Escape($raw); '<add key=\"Printer:ZebraPrinterAddress\" value=\"' + $escaped + '\" />'"
echo.
echo Copy only the value into Printer:ZebraPrinterAddress, or replace the whole App.config line above.
pause
