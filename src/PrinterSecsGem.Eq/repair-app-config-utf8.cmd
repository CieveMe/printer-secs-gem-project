@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$base='%~dp0';" ^
  "$config=Join-Path $base 'App.config';" ^
  "$template=Join-Path $base 'App.config.reference';" ^
  "if(!(Test-Path -LiteralPath $config)){ throw 'App.config was not found.' }" ^
  "if(!(Test-Path -LiteralPath $template)){ Write-Host 'App.config.reference was not found; only normalizing current App.config encoding.'; $template=$config }" ^
  "[xml]$old=Get-Content -LiteralPath $config -Raw -Encoding UTF8;" ^
  "[xml]$clean=Get-Content -LiteralPath $template -Raw -Encoding UTF8;" ^
  "$values=@{};" ^
  "if($old.configuration -and $old.configuration.appSettings){" ^
  "  foreach($item in $old.configuration.appSettings.add){ if($item.key){ $values[[string]$item.key]=[string]$item.value } }" ^
  "}" ^
  "if($null -eq $clean.configuration){ $root=$clean.CreateElement('configuration'); $clean.AppendChild($root) | Out-Null }" ^
  "if($null -eq $clean.configuration.appSettings){ $node=$clean.CreateElement('appSettings'); $clean.configuration.AppendChild($node) | Out-Null }" ^
  "foreach($item in $clean.configuration.appSettings.add){ if($item.key -and $values.ContainsKey([string]$item.key)){ $item.SetAttribute('value',$values[[string]$item.key]) } }" ^
  "foreach($key in $values.Keys){" ^
  "  $exists=$clean.configuration.appSettings.add | Where-Object { $_.key -eq $key } | Select-Object -First 1;" ^
  "  if($null -eq $exists){ $item=$clean.CreateElement('add'); $item.SetAttribute('key',[string]$key); $item.SetAttribute('value',[string]$values[$key]); $clean.configuration.appSettings.AppendChild($item) | Out-Null }" ^
  "}" ^
  "$backup=$config + '.bak-' + (Get-Date -Format 'yyyyMMdd-HHmmss');" ^
  "Copy-Item -LiteralPath $config -Destination $backup -Force;" ^
  "$settings=New-Object System.Xml.XmlWriterSettings;" ^
  "$settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false;" ^
  "$settings.Indent=$true;" ^
  "$writer=[System.Xml.XmlWriter]::Create($config,$settings);" ^
  "try{ $clean.Save($writer) } finally{ $writer.Close() };" ^
  "$bytes=[System.IO.File]::ReadAllBytes($config);" ^
  "if($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF){ throw 'App.config still has UTF-8 BOM.' }" ^
  "Write-Host ('Repaired App.config as UTF-8 without BOM. Backup: ' + $backup);"

if errorlevel 1 exit /b 1
ping -n 3 127.0.0.1 >nul
exit /b 0
