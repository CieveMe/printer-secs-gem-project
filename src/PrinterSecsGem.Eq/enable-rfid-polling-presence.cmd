@echo off
setlocal
cd /d "%~dp0"

call :update_config
if errorlevel 1 exit /b 1

echo RFID polling presence workflow enabled.
echo Sensor reads are bypassed. Empty/failed RFID reads are confirmed 3 times before no-goods.
echo RFID polling interval is set to 1200 ms.
echo Restart PrinterSecsGem.Eq.exe for the change to take effect.
ping -n 4 127.0.0.1 >nul
exit /b 0

:update_config
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base='%~dp0';" ^
  "$values=[ordered]@{'ERackSimulation:Enabled'='false';'ERackHardware:Enabled'='true';'ERackSensorDisplay:Enabled'='true';'ERackSensorDisplay:PresenceMode'='RfidPolling';'ERackSensorDisplay:PollIntervalMilliseconds'='1200';'ERackSensorDisplay:RfidPollingReadTimeoutMilliseconds'='700';'ERackSensorDisplay:RfidPollingEmptyConfirmCount'='3'};" ^
  "$paths=@((Join-Path $base 'App.config'));" ^
  "$updated=0;" ^
  "foreach($path in $paths){" ^
  "  if(!(Test-Path -LiteralPath $path)){ continue }" ^
  "  [xml]$xml=Get-Content -LiteralPath $path -Raw -Encoding UTF8;" ^
  "  if($null -eq $xml.configuration.appSettings){ $node=$xml.CreateElement('appSettings'); $xml.configuration.AppendChild($node) | Out-Null }" ^
  "  foreach($key in $values.Keys){ $item=$xml.configuration.appSettings.add | Where-Object { $_.key -eq $key } | Select-Object -First 1; if($null -eq $item){ $item=$xml.CreateElement('add'); $item.SetAttribute('key',$key); $xml.configuration.appSettings.AppendChild($item) | Out-Null }; $item.SetAttribute('value',[string]$values[$key]) }" ^
  "  Copy-Item -LiteralPath $path -Destination ($path + '.bak-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) -Force; $settings=New-Object System.Xml.XmlWriterSettings; $settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false; $settings.Indent=$true; $writer=[System.Xml.XmlWriter]::Create($path,$settings); try{ $xml.Save($writer) } finally{ $writer.Close() }; Write-Host ('Updated ' + $path); $updated++" ^
  "}" ^
  "if($updated -eq 0){ throw 'App.config was not found.' }"
exit /b %ERRORLEVEL%
