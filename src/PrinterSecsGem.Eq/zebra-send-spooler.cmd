@echo off
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  echo Usage:
  echo   zebra-send-spooler.cmd "Printer Name" ["zpl file path"]
  echo.
  echo Example:
  echo   zebra-send-spooler.cmd "ZDesigner ZD888-203dpi ZPL"
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

echo Sending ZPL file through Windows RAW spooler:
echo   %ZPL%
echo To printer:
echo   "%~1"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0raw-print.ps1" -PrinterName "%~1" -FilePath "%ZPL%"
pause
exit /b %ERRORLEVEL%
