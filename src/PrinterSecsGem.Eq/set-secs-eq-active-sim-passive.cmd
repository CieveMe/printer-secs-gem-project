@echo off
setlocal
cd /d "%~dp0"

call :update_config "true" "127.0.0.1"
if errorlevel 1 exit /b 1

echo SECS mode updated: EQ_ACTIVE_SIM_PASSIVE.
echo.
echo Simulator setting:
echo   Connect Mode = Passive
echo   Local Port   = 5000
echo.
echo Restart PrinterSecsGem.Eq.exe for the change to take effect.
ping -n 4 127.0.0.1 >nul
exit /b 0

:update_config
set "IS_ACTIVE=%~1"
set "IP_ADDRESS=%~2"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base = '%~dp0';" ^
  "$paths = @((Join-Path $base 'App.config'));" ^
  "$updated = 0;" ^
  "foreach($path in $paths){" ^
  "  if(!(Test-Path -LiteralPath $path)){ continue }" ^
  "  [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8;" ^
  "  if($null -eq $xml.configuration){ $root = $xml.CreateElement('configuration'); $xml.AppendChild($root) | Out-Null }" ^
  "  if($null -eq $xml.configuration.appSettings){ $node = $xml.CreateElement('appSettings'); $xml.configuration.AppendChild($node) | Out-Null }" ^
  "  function Set-Key([string]$key,[string]$value){ $item = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key } | Select-Object -First 1; if($null -eq $item){ $item = $xml.CreateElement('add'); $item.SetAttribute('key',$key); $xml.configuration.appSettings.AppendChild($item) | Out-Null }; $item.SetAttribute('value',$value) }" ^
  "  Set-Key 'secs4net:IsActive' '%IS_ACTIVE%';" ^
  "  Set-Key 'secs4net:IpAddress' '%IP_ADDRESS%';" ^
  "  Set-Key 'secs4net:Port' '5000';" ^
  "  $settings=New-Object System.Xml.XmlWriterSettings; $settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false; $settings.Indent=$true; $writer=[System.Xml.XmlWriter]::Create($path,$settings); try{ $xml.Save($writer) } finally{ $writer.Close() };" ^
  "  Write-Host ('Updated ' + $path);" ^
  "  $updated++;" ^
  "}" ^
  "if($updated -eq 0){ throw 'App.config was not found.' }"
exit /b %ERRORLEVEL%
