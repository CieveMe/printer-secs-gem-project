@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  echo Usage:
  echo   zebra-status-driver.cmd "Printer Name"
  echo.
  echo Example:
  echo   zebra-status-driver.cmd "ZDesigner ZD888-203dpi ZPL"
  pause
  exit /b 1
)

set "SDK=%~dp0zebra-command-line\SdkApi.Desktop.CommandLine.dll"
if not exist "%SDK%" set "SDK=%~dp0command_line\SdkApi.Desktop.CommandLine.dll"

if not exist "%SDK%" (
  echo Zebra command line SDK not found.
  echo Copy v4.0.3435\command_line to this folder and rename it to zebra-command-line.
  pause
  exit /b 1
)

echo [printer status]
dotnet "%SDK%" status "%~1" --driver --printer --verbose
echo.
echo [port status]
dotnet "%SDK%" status "%~1" --driver --portstatus --verbose
pause
