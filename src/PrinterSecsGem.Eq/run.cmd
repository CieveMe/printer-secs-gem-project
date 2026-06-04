@echo off
setlocal
set DOTNET_ROLL_FORWARD=Major
cd /d "%~dp0"
PrinterSecsGem.Eq.exe --secs
pause
