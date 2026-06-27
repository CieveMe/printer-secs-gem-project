@echo off
setlocal
cd /d "%~dp0"

call :update_config
if errorlevel 1 exit /b 1

echo Real sensor/display workflow disabled.
echo Restart PrinterSecsGem.Eq.exe for the change to take effect.
ping -n 4 127.0.0.1 >nul
exit /b 0

:update_config
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base='%~dp0';" ^
  "$values=[ordered]@{'ERackSensorDisplay:Enabled'='false'};" ^
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
