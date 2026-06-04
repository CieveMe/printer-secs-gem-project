@echo off
setlocal
set "SIMULATOR_DIR=%~dp0..\..\SECS_Simulator"
set "SIMULATOR_EXE=%SIMULATOR_DIR%\SEComEnabler.SEComSimulator.exe"
set "SMD_FILE=%~dp0..\samples\secs\PrinterSecsGem-UI-SECS-Test.SMD"

if not exist "%SIMULATOR_EXE%" (
  echo SEComSimulator not found:
  echo   "%SIMULATOR_EXE%"
  pause
  exit /b 1
)

if exist "%SMD_FILE%" (
  copy /Y "%SMD_FILE%" "%SIMULATOR_DIR%\PrinterSecsGem-UI-SECS-Test.SMD" >nul
  echo SMD copied to:
  echo   "%SIMULATOR_DIR%\PrinterSecsGem-UI-SECS-Test.SMD"
)

start "" "%SIMULATOR_EXE%"
