@echo off
setlocal
echo [wmic printer status]
wmic printer get Name,DriverName,PortName,PrinterStatus,WorkOffline,Default /format:table
echo.
echo [PowerShell Get-Printer]
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Command Get-Printer -ErrorAction SilentlyContinue) { Get-Printer | Format-Table Name,DriverName,PortName,PrinterStatus,WorkOffline -AutoSize } else { Write-Host 'Get-Printer is not available.' }"
echo.
pause
