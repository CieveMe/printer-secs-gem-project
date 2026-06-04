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

set "ZPL=%~dp0calibration-label.zpl"
if not exist "%ZPL%" (
  echo Calibration ZPL was not found:
  echo   %ZPL%
  pause
  exit /b 1
)

echo Discovering USB printer...
for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$lines = & dotnet '%SDK%' discover --usb; $lines | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('USB', [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith('\\?\usb', [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1"`) do set "USB_ADDRESS=%%A"

if "%USB_ADDRESS%"=="" (
  echo USB printer was not discovered.
  pause
  exit /b 1
)

echo Sending calibration ZPL:
echo   %ZPL%
echo To USB printer:
echo   "%USB_ADDRESS%"
dotnet "%SDK%" send "%USB_ADDRESS%" "%ZPL%" --usb --verbose
pause
exit /b %ERRORLEVEL%
