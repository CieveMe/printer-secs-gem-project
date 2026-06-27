@echo off
setlocal
cd /d "%~dp0"

set "CONFIG=%~dp0App.config"
if not exist "%CONFIG%" (
  echo App.config was not found:
  echo   %CONFIG%
  pause
  exit /b 1
)

set "BACKUP=%~dp0App.config.backup-%DATE:~0,4%%DATE:~5,2%%DATE:~8,2%-%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%"
set "BACKUP=%BACKUP: =0%"
copy "%CONFIG%" "%BACKUP%" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$path = '%CONFIG%';" ^
  "[xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8;" ^
  "$settings = $xml.configuration.appSettings.add;" ^
  "function Set-Key([string]$key,[string]$value){ $node = $settings | Where-Object { $_.key -eq $key } | Select-Object -First 1; if($null -eq $node){ $node = $xml.CreateElement('add'); $node.SetAttribute('key',$key); $xml.configuration.appSettings.AppendChild($node) | Out-Null }; $node.SetAttribute('value',$value) }" ^
  "Set-Key 'LabelTemplate:UseMinimalCompatibleCommands' 'false';" ^
  "Set-Key 'LabelTemplate:ResetPrinterState' 'false';" ^
  "Set-Key 'LabelTemplate:Orientation' 'Normal';" ^
  "Set-Key 'LabelTemplate:PrintOrientation' 'N';" ^
  "Set-Key 'LabelTemplate:PrintDarkness' '0';" ^
  "Set-Key 'LabelTemplate:WidthDots' '480';" ^
  "Set-Key 'LabelTemplate:HeightDots' '320';" ^
  "Set-Key 'LabelTemplate:LabelLengthAppliesToAllMedia' 'false';" ^
  "Set-Key 'LabelTemplate:LabelTop' '0';" ^
  "Set-Key 'LabelTemplate:LabelShift' '0';" ^
  "Set-Key 'LabelTemplate:LabelHomeX' '0';" ^
  "Set-Key 'LabelTemplate:LabelHomeY' '0';" ^
  "Set-Key 'LabelTemplate:TopTextX' '55';" ^
  "Set-Key 'LabelTemplate:TopTextY' '35';" ^
  "Set-Key 'LabelTemplate:TopTextSize' '40';" ^
  "Set-Key 'LabelTemplate:TopTextHeight' '70';" ^
  "Set-Key 'LabelTemplate:TopTextWidth' '55';" ^
  "Set-Key 'LabelTemplate:TopTextBlockWidth' '370';" ^
  "Set-Key 'LabelTemplate:BarcodeX' '75';" ^
  "Set-Key 'LabelTemplate:BarcodeY' '95';" ^
  "Set-Key 'LabelTemplate:BarcodeModuleWidth' '2';" ^
  "Set-Key 'LabelTemplate:BarcodeHeight' '80';" ^
  "Set-Key 'LabelTemplate:BarcodeHumanReadable' 'false';" ^
  "Set-Key 'LabelTemplate:BarcodeHumanReadableAbove' 'false';" ^
  "Set-Key 'LabelTemplate:BarcodePrintCheckDigit' 'false';" ^
  "Set-Key 'LabelTemplate:BarcodeTextEnabled' 'true';" ^
  "Set-Key 'LabelTemplate:BarcodeTextX' '120';" ^
  "Set-Key 'LabelTemplate:BarcodeTextY' '190';" ^
  "Set-Key 'LabelTemplate:BarcodeTextFont' '0';" ^
  "Set-Key 'LabelTemplate:BarcodeTextRenderMode' 'ZplFont';" ^
  "Set-Key 'LabelTemplate:BarcodeTextBitmapFontFamily' 'Arial';" ^
  "Set-Key 'LabelTemplate:BarcodeTextBitmapFontSize' '0';" ^
  "Set-Key 'LabelTemplate:BarcodeTextBitmapThreshold' '150';" ^
  "Set-Key 'LabelTemplate:BarcodeTextSize' '22';" ^
  "Set-Key 'LabelTemplate:BarcodeTextHeight' '38';" ^
  "Set-Key 'LabelTemplate:BarcodeTextWidth' '34';" ^
  "$settings=New-Object System.Xml.XmlWriterSettings; $settings.Encoding=New-Object System.Text.UTF8Encoding -ArgumentList $false; $settings.Indent=$true; $writer=[System.Xml.XmlWriter]::Create($path,$settings); try{ $xml.Save($writer) } finally{ $writer.Close() };"

if errorlevel 1 (
  echo Failed to update App.config. Backup:
  echo   %BACKUP%
  pause
  exit /b 1
)

echo App.config updated to conservative label layout.
echo Backup:
echo   %BACKUP%
echo.
echo Save App.config, then click Test Print again. No restart is needed with the latest exe.
pause
