@echo off
setlocal
cd /d "%~dp0"

call :update_config
if errorlevel 1 exit /b 1

echo Deployment profile updated: REMOTE_BOTH_SIM.
echo.
echo Use this profile for remote desktop tests without MCU/Zebra.
echo Runtime=Both, MockHardware, FilePrinter, ERackSimulation enabled.
echo COM will not be opened and real Zebra print is disabled.
echo SECS reset to EQ Active / Simulator Passive on 127.0.0.1:5000.
echo.
echo Restart PrinterSecsGem.Eq.exe for the change to take effect.
ping -n 4 127.0.0.1 >nul
exit /b 0

:update_config
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base='%~dp0';" ^
  "$values=[ordered]@{" ^
  "  'StatusUi:Language'='zh-CN';" ^
  "  'Runtime:Mode'='Both';" ^
  "  'secs4net:IsActive'='true';" ^
  "  'secs4net:IpAddress'='127.0.0.1';" ^
  "  'secs4net:Port'='5000';" ^
  "  'ERackServer:Enabled'='true';" ^
  "  'ERackServer:ListenIp'='127.0.0.1';" ^
  "  'ERackServer:Port'='7801';" ^
  "  'ERackServer:RequestTimeoutMilliseconds'='60000';" ^
  "  'ERackClient:Enabled'='true';" ^
  "  'ERackClient:ServerHost'='127.0.0.1';" ^
  "  'ERackClient:ServerPort'='7801';" ^
  "  'ERackClient:UnitId'='UNIT001';" ^
  "  'ERackClient:ShelfId'='SHELF001';" ^
  "  'ERackSimulation:Enabled'='true';" ^
  "  'ERackSimulation:ShelfId'='SHELF001';" ^
  "  'ERackSimulation:LocationId'='LOC001';" ^
  "  'ERackSimulation:Tag'='RFID1234567890';" ^
  "  'ERackHardware:Enabled'='false';" ^
  "  'ERackSensorDisplay:Enabled'='false';" ^
  "  'ERackSensorDisplay:PollIntervalMilliseconds'='500';" ^
  "  'Printer:RealPrintEnabled'='false';" ^
  "  'LocalValidation:ShelfId'='SHELF001';" ^
  "  'LocalValidation:LocationId'='LOC001';" ^
  "  'Locations:LOC001:ShelfId'='SHELF001'" ^
  "};" ^
  "$paths=@((Join-Path $base 'App.config'));" ^
  "$updated=0;" ^
  "foreach($path in $paths){" ^
  "  if(!(Test-Path -LiteralPath $path)){ continue }" ^
  "  [xml]$xml=Get-Content -LiteralPath $path -Raw -Encoding UTF8;" ^
  "  if($null -eq $xml.configuration){ $root=$xml.CreateElement('configuration'); $xml.AppendChild($root) | Out-Null }" ^
  "  if($null -eq $xml.configuration.appSettings){ $node=$xml.CreateElement('appSettings'); $xml.configuration.AppendChild($node) | Out-Null }" ^
  "  foreach($key in $values.Keys){ $item=$xml.configuration.appSettings.add | Where-Object { $_.key -eq $key } | Select-Object -First 1; if($null -eq $item){ $item=$xml.CreateElement('add'); $item.SetAttribute('key',$key); $xml.configuration.appSettings.AppendChild($item) | Out-Null }; $item.SetAttribute('value',[string]$values[$key]) }" ^
  "  Copy-Item -LiteralPath $path -Destination ($path + '.bak-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) -Force; $settings=New-Object System.Xml.XmlWriterSettings; $settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false; $settings.Indent=$true; $writer=[System.Xml.XmlWriter]::Create($path,$settings); try{ $xml.Save($writer) } finally{ $writer.Close() }; Write-Host ('Updated ' + $path); $updated++" ^
  "}" ^
  "if($updated -eq 0){ throw 'App.config was not found.' }"
exit /b %ERRORLEVEL%
