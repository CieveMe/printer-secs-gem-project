# Project Context

This file is the fast handoff note for future sessions. Read it first, then inspect
`README.md`, `docs/`, and `notes/` only when needed.

## Goal

Build a Windows 10 x64 EQ-side transfer application for:

- Host connection through SECS/GEM HSMS TCP.
- Zebra label printing through Zebra SDK USB Direct and ZPL.
- ERack / RFID reader-writer through a C# serial COM protocol.

The formal runtime shape is now one process:

- Double-click `PrinterSecsGem.Eq.exe`.
- The WinForms status UI opens.
- The SECS background worker starts in the same process.
- UI buttons and Host commands share the same hardware/printer services.
- Only one process owns the RFID COM port, avoiding COM conflicts.

Do not use wording like "customer" in repo-facing docs or commit messages.

## Stack

- .NET 8 / WinForms
- secs4net
- log4net
- System.IO.Ports
- Zebra SDK `command_line`

Zebra SDK source:

```text
D:\Desktop\printer project\v4.0.3435\command_line
```

Actual local path is under the shared workspace root and contains Chinese
characters. Use the current repo root's parent directory, then:

```text
v4.0.3435\command_line
```

## Delivery Model

First deployment is a complete folder containing:

```text
PrinterSecsGem.Eq.exe
App.config
log4net.config
zebra-command-line/
```

Normal later updates replace only:

```text
PrinterSecsGem.Eq.exe
```

The target machine must have `.NET 8 Desktop Runtime x64` installed. Zebra SDK is
not embedded in the exe. It stays in `zebra-command-line/` beside the exe.

## Latest Packages

Formal UI + SECS merged build:

```text
publish\printer-secs-gem-folder-realprint-ui-secs-20260605.zip
publish\printer-secs-gem-exe-update-ui-secs-20260605.zip
```

Sizes:

- Full first-deployment package: about 55.03 MB.
- Update-only exe package: about 0.86 MB.
- `PrinterSecsGem.Eq.exe`: about 2.07 MB.

## Run Modes

Formal mode:

```text
Double-click PrinterSecsGem.Eq.exe
```

Expected behavior:

- Status UI opens.
- SECS/GEM EQ worker starts in the background.
- Program actively connects to Host using `App.config`.
- UI auto-opens COM once on startup and logs the result.
- SECS connection state and Host command results appear in the UI log box.

Backup headless mode:

```bat
run.cmd
```

Equivalent to:

```bat
PrinterSecsGem.Eq.exe --secs
```

Local utility scripts:

```bat
run-read-tag-local.cmd
run-write-tag-local.cmd
run-validate-local.cmd
```

## Completed Capabilities

- Default double-click opens the status UI.
- UI mode now also starts the SECS background worker.
- UI and Host commands share one `ERackSerialHardwareGateway`.
- Status UI shows SECS, Print, COM, RFID, Last Print, and Config.
- Status UI supports `Open COM`, `Close COM`, `Read RFID`, `Write RFID`, `Test Print`.
- UI auto-runs `Open COM` after showing the window.
- SECS connection state is published to the UI log.
- Host command processing is published to the UI log.
- Zebra USB Direct real printing has been verified.
- RFID read has been verified on real serial hardware.
- RFID write has been verified on real serial hardware.
- `ERackHardware:WriteTagStartPage=1` is the verified working start page.
- RFID write reverses each 8-byte block before sending.
- Short RFID writes of 8/16/24 chars write only that length. They do not pad spaces
  to clear old tail data.
- RFID read still reads 32 bytes, reverses each 8-byte block, and trims trailing
  spaces / null chars for display.

## Supported SECS Messages

- `S1F1 -> S1F2`: Are You There
- `S1F3 -> S1F4`: basic equipment status
- `S8F3 -> S8F4`: print command/result
- `S10F11 -> S10F12`: write RFID tag command/result
- `S5F11 -> S5F12`: shelf/location status query, currently used as RFID read

Implemented as a factory but not yet wired as a continuous trigger:

- `S6F11` tag-read event report

## Key App.config Values

```xml
<add key="secs4net:IpAddress" value="127.0.0.1" />
<add key="secs4net:Port" value="5000" />
<add key="secs4net:DeviceId" value="1" />
<add key="secs4net:IsActive" value="true" />

<add key="ERackHardware:Enabled" value="true" />
<add key="ERackHardware:PortName" value="COM11" />
<add key="ERackHardware:BaudRate" value="57600" />
<add key="ERackHardware:DeviceAddress" value="1" />
<add key="ERackHardware:InventoryMode" value="4" />
<add key="ERackHardware:KeepPortOpen" value="true" />
<add key="ERackHardware:WriteTagStartPage" value="1" />
<add key="ERackHardware:WriteTagWaitCount" value="20" />

<add key="Printer:RealPrintEnabled" value="true" />
<add key="Printer:Mode" value="ZebraCommandLine" />
<add key="Printer:ZebraCommandLineAssembly" value="zebra-command-line/SdkApi.Desktop.CommandLine.dll" />
<add key="Printer:ZebraConnectionType" value="Usb" />
<add key="Printer:ZebraPrinterAddress" value="" />
```

When `Printer:ZebraPrinterAddress` is empty, the app auto-discovers a USB Zebra
printer. If more than one Zebra printer exists, pin this value.

## SECS Simulator Test Notes

Simulator settings:

- Role: Host
- HSMS: Passive / Listen
- IP: `127.0.0.1`
- Port: `5000`
- Device ID: `1`
- Protocol Type: HSMS, not SECS1

With the latest build, there is no need to close the UI and run `run.cmd` just to
test SECS. Double-clicking the exe runs UI and SECS in the same process.

Write tag test:

```sml
S10F11 W
<L
  <A "SHELF001">
  <A "LOC001">
  <A "EFS08IAA">
>.
```

Read tag test:

```sml
S5F11 W
<L
  <A "SHELF001">
  <A "ALL">
>.
```

Print test:

```sml
S8F3 W
<L
  <A "SHELF001">
  <A "PRINTER001">
  <A "EFS08IZS">
  <U1 1>
>.
```

## Field Notes

- Do not open the ERack test tool at the same time as this app if both use the
  same COM port.
- In formal mode, UI buttons and Host commands share the same serial gateway and
  are protected by the gateway lock.
- If the COM port disappears, check cable, USB port, power, driver, and reader
  hardware before changing software.
- To demo software flow without hardware, set `ERackHardware:Enabled=false`.
- To avoid consuming labels, set `Printer:RealPrintEnabled=false`.

## Important Code

```text
src\PrinterSecsGem.Eq\Program.cs
src\PrinterSecsGem.Eq\StatusUi\StatusDashboardForm.cs
src\PrinterSecsGem.Eq\StatusUi\StatusUiEventBus.cs
src\PrinterSecsGem.Eq\Secs\SecsPrimaryMessageWorker.cs
src\PrinterSecsGem.Eq\Secs\SecsMessageDispatcher.cs
src\PrinterSecsGem.Eq\Hardware\ERack\ERackSerialHardwareGateway.cs
src\PrinterSecsGem.Eq\Hardware\ERack\ERackTagDecoder.cs
src\PrinterSecsGem.Eq\Printing\ZebraCommandLinePrinterGateway.cs
```

## Next Priorities

1. Use SECS Simulator to verify UI mode with `S1F1`, `S10F11`, `S5F11`, `S8F3`.
2. Verify on real site: auto Open COM, Host write tag, Host read tag, Test Print.
3. If Host requires active event reporting, wire `S6F11` as a configurable trigger.
4. Update `App.config` after official CEID/RPTID/VID ids are confirmed.
5. Pin `Printer:ZebraPrinterAddress` if multiple Zebra printers are present.
