@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  echo Usage:
  echo   zebra-send-driver.cmd "Printer Name" ["zpl file path"]
  echo.
  echo Run zebra-discover.cmd first, then copy the printer name from driver discovery.
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

set "ZPL=%~2"

if "%ZPL%"=="" (
  echo Running local validation to generate a fresh ZPL file...
  set DOTNET_ROLL_FORWARD=Major
  PrinterSecsGem.Eq.exe --validate-local
  for /f "usebackq delims=" %%F in (`powershell -NoProfile -Command "$f=Get-ChildItem -Path 'output\zpl' -Filter '*.zpl' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if($f){$f.FullName}"`) do set "ZPL=%%F"
)

if "%ZPL%"=="" (
  echo Still no ZPL file found under output\zpl.
  pause
  exit /b 1
)

echo Sending ZPL file:
echo   %ZPL%
echo To printer:
echo   "%~1"
dotnet "%SDK%" send "%~1" "%ZPL%" --driver --verbose
pause
exit /b %ERRORLEVEL%
