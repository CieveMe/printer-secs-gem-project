@echo off
setlocal
echo Stopping PrinterSecsGem.Eq.exe ...
taskkill /F /IM PrinterSecsGem.Eq.exe
if errorlevel 1 (
  echo.
  echo PrinterSecsGem.Eq.exe was not running, or it has already exited.
) else (
  echo.
  echo PrinterSecsGem.Eq.exe has been stopped.
)
echo.
echo You can now replace PrinterSecsGem.Eq.exe if needed.
ping -n 4 127.0.0.1 >nul
