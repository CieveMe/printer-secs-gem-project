@echo off
setlocal
cd /d "%~dp0"

set "SDK=%~dp0zebra-command-line\SdkApi.Desktop.CommandLine.dll"
if not exist "%SDK%" set "SDK=%~dp0command_line\SdkApi.Desktop.CommandLine.dll"

if not exist "%SDK%" (
  echo Zebra command line SDK not found.
  echo Copy v4.0.3435\command_line to this folder and rename it to zebra-command-line.
  pause
  exit /b 1
)

echo [1/2] Discovering printers through ZDesigner driver...
dotnet "%SDK%" discover --driver
echo.
echo [2/2] Discovering driverless USB printers...
dotnet "%SDK%" discover --usb
echo.
pause
