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

set "USB_ADDRESS=%~1"
if "%USB_ADDRESS%"=="" (
  echo Discovering USB printer...
  for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$lines = & dotnet '%SDK%' discover --usb; $lines | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('USB', [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith('\\?\usb', [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1"`) do set "USB_ADDRESS=%%A"
)

if "%USB_ADDRESS%"=="" (
  echo USB printer was not discovered.
  pause
  exit /b 1
)

set "SGD_FILE=%TEMP%\zebra-set-zpl-mode-%RANDOM%.sgd"
(
  echo ! U1 setvar "device.languages" "zpl"
  echo ! U1 setvar "device.pnp_option" "zpl"
  echo ! U1 do "device.reset" ""
) > "%SGD_FILE%"

echo Setting Zebra language mode to ZPL.
echo To USB printer:
echo   "%USB_ADDRESS%"
dotnet "%SDK%" send "%USB_ADDRESS%" "%SGD_FILE%" --usb --verbose
set "EXIT_CODE=%ERRORLEVEL%"
del "%SGD_FILE%" >nul 2>nul
echo.
echo If the command worked, the printer should reset. Wait until the green light is steady, then run zebra-send-minimal.cmd.
pause
exit /b %EXIT_CODE%
