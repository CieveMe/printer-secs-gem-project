@echo off
setlocal
cd /d "%~dp0"

set "SHELF_ID=SHELF001"
set "LOCATION_ID=LOC001"
set "UNIT_ID=UNIT001"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base='%~dp0';" ^
  "$values=[ordered]@{" ^
  "  'Runtime:Mode'='Unit';" ^
  "  'ERackServer:Enabled'='false';" ^
  "  'ERackClient:Enabled'='true';" ^
  "  'ERackClient:ServerPort'='7801';" ^
  "  'ERackClient:UnitId'='%UNIT_ID%';" ^
  "  'ERackClient:ShelfId'='%SHELF_ID%';" ^
  "  'ERackSimulation:Enabled'='false';" ^
  "  'ERackHardware:Enabled'='true';" ^
  "  'ERackSensorDisplay:Enabled'='false';" ^
  "  'ERackSensorDisplay:PollIntervalMilliseconds'='500';" ^
  "  'Printer:RealPrintEnabled'='true';" ^
  "  'LocalValidation:ShelfId'='%SHELF_ID%';" ^
  "  'LocalValidation:LocationId'='%LOCATION_ID%';" ^
  "  'Locations:%LOCATION_ID%:ShelfId'='%SHELF_ID%';" ^
  "  'Locations:%LOCATION_ID%:PortName'='';" ^
  "  'Locations:%LOCATION_ID%:BaudRate'='57600';" ^
  "  'Locations:%LOCATION_ID%:DeviceAddress'='1'" ^
  "};" ^
  "$path=Join-Path $base 'App.config';" ^
  "if(!(Test-Path -LiteralPath $path)){ throw 'App.config was not found.' }" ^
  "[xml]$xml=Get-Content -LiteralPath $path -Raw -Encoding UTF8;" ^
  "if($null -eq $xml.configuration){ $root=$xml.CreateElement('configuration'); $xml.AppendChild($root) | Out-Null }" ^
  "if($null -eq $xml.configuration.appSettings){ $node=$xml.CreateElement('appSettings'); $xml.configuration.AppendChild($node) | Out-Null }" ^
  "foreach($key in $values.Keys){ $item=$xml.configuration.appSettings.add | Where-Object { $_.key -eq $key } | Select-Object -First 1; if($null -eq $item){ $item=$xml.CreateElement('add'); $item.SetAttribute('key',$key); $xml.configuration.appSettings.AppendChild($item) | Out-Null }; $item.SetAttribute('value',[string]$values[$key]) }" ^
  "Copy-Item -LiteralPath $path -Destination ($path + '.bak-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) -Force;" ^
  "$settings=New-Object System.Xml.XmlWriterSettings; $settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false; $settings.Indent=$true; $writer=[System.Xml.XmlWriter]::Create($path,$settings); try{ $xml.Save($writer) } finally{ $writer.Close() };" ^
  "Write-Host ('Updated ' + $path)"
if errorlevel 1 exit /b 1

echo Deployment profile updated: REAL_UNIT_CLIENT_SAFE.
echo.
echo This PC runs Unit Client and real local MCU/Zebra only.
echo Local SECS/GEM is not started in this profile.
echo Sensor/display auto workflow remains disabled. Run enable-real-sensor-display.cmd only after the sensor/display rule is confirmed.
echo ERACK server IP and MCU COM port are read from App.config.
echo.
echo Restart PrinterSecsGem.Eq.exe for the change to take effect.
ping -n 4 127.0.0.1 >nul
