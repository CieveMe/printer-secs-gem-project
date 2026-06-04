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

set "REAL_PRINT_ENABLED=false"
for /f "usebackq delims=" %%E in (`powershell -NoProfile -Command "$p='App.config'; if(Test-Path $p){[xml]$x=Get-Content $p; $v=($x.configuration.appSettings.add | Where-Object { $_.key -eq 'Printer:RealPrintEnabled' } | Select-Object -First 1).value; if($v){$v}else{'false'}}else{'false'}"`) do set "REAL_PRINT_ENABLED=%%E"

set "USB_ADDRESS=%~1"
set "ZPL=%~2"

if /I not "%REAL_PRINT_ENABLED%"=="true" (
  echo Real printing is disabled by App.config.
  echo Set Printer:RealPrintEnabled to true only when you want to feed label paper.

  if "%ZPL%"=="" (
    echo Running local validation to generate a fresh ZPL preview file...
    set DOTNET_ROLL_FORWARD=Major
    PrinterSecsGem.Eq.exe --validate-local
    for /f "usebackq delims=" %%F in (`powershell -NoProfile -Command "$f=Get-ChildItem -Path 'output\zpl' -Filter '*.zpl' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if($f){$f.FullName}"`) do set "ZPL=%%F"
  )

  if not "%ZPL%"=="" (
    echo Latest ZPL preview file:
    echo   %ZPL%
  )

  pause
  exit /b 0
)

if "%USB_ADDRESS%"=="" (
  echo Discovering USB printer...
  for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$lines = & dotnet '%SDK%' discover --usb; $lines | ForEach-Object { $_.Trim() } | Where-Object { $_.StartsWith('USB', [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith('\\?\usb', [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1"`) do set "USB_ADDRESS=%%A"
)

if "%USB_ADDRESS%"=="" (
  echo USB printer was not discovered.
  echo Run zebra-discover.cmd to check whether Zebra SDK can see the printer.
  pause
  exit /b 1
)

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
echo To USB printer:
echo   "%USB_ADDRESS%"
dotnet "%SDK%" send "%USB_ADDRESS%" "%ZPL%" --usb --verbose
pause
exit /b %ERRORLEVEL%
