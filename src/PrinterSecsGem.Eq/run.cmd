@echo off
setlocal
set DOTNET_ROLL_FORWARD=Major
cd /d "%~dp0"
start "" "%~dp0PrinterSecsGem.Eq.exe" --secs
