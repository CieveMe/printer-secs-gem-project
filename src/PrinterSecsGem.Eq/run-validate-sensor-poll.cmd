@echo off
setlocal
set DOTNET_ROLL_FORWARD=Major
cd /d "%~dp0"

echo Running one-shot sensor polling validation...
echo This does not modify App.config and does not write to the display.
echo.

start "" /wait "%~dp0PrinterSecsGem.Eq.exe" --validate-sensor-poll
set EXITCODE=%ERRORLEVEL%

echo.
echo Validation process exited with code %EXITCODE%.
echo.

if exist "%~dp0logs\printer-secs-gem.log" (
  echo Recent sensor validation log lines:
  powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$log=Join-Path '%~dp0' 'logs\printer-secs-gem.log';" ^
    "$patterns='Sensor polling validation|Sensor poll result|Prepared sensor polling event|ERack sensor read failed|sensor poll failed|failed during startup|ERROR|WARN';" ^
    "Get-Content -LiteralPath $log -Tail 120 -Encoding UTF8 | Select-String -Pattern $patterns"
) else (
  echo Log file was not found: %~dp0logs\printer-secs-gem.log
)

echo.
pause
exit /b %EXITCODE%
