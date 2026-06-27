@echo off
setlocal
cd /d "%~dp0"

if not exist "repair-app-config-utf8.cmd" (
  echo repair-app-config-utf8.cmd was not found in this folder.
  exit /b 1
)

call repair-app-config-utf8.cmd
if errorlevel 1 exit /b 1

echo.
echo App.config has been repaired as UTF-8 without BOM.
echo Existing site values were preserved where matching keys exist.
echo Restart PrinterSecsGem.Eq.exe for changes to take effect.
ping -n 4 127.0.0.1 >nul
exit /b 0
