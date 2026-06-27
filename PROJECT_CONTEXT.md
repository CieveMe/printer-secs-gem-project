# Project Context

This file is the fast handoff note for future sessions. Read it first, then inspect
`README.md`, `docs/`, and `notes/` only when needed.

## Critical Current Handoff

Latest handoff archive:

```text
notes\20260625-未解决问题收敛归档.md
```

Current unresolved items are now intentionally narrow:

- Final field/simulator confirmation for `S8F4` print result codes and
  descriptions after the timeout/protocol changes.
- Final field/simulator confirmation for print-result `S6F11`, especially the
  description encoding strategy if Chinese still appears as `????`.
- One final remote/site smoke test for the latest GUI baseline and exe-only
  update path.

Do not treat `CEID / RPTID / VID` as a current blocker unless the integration
party raises a new formal numbering requirement. Other tested SECS messages are
not reopened by this handoff.

Current priority: the GUI readability and light-polish pass has been implemented
locally in `StatusDashboardForm.cs`. It fixes clipped status rows and truncated
`Discover/Save` buttons, removes the harsh black status boxes, and uses light
bordered status panels. Local Release build passed with 0 errors; `NU1900` is
only the offline NuGet vulnerability-source warning. Computer Use screenshots
verified the local Release GUI.

This latest GUI pass is intended to be UI-only. It should not change SECS,
ERACK, serial, RFID, printing, protocol, publish-script, or `App.config` logic.
If the remote folder already has the current phase-2 complete deployment
structure, this GUI increment should be exe-only: replace `PrinterSecsGem.Eq.exe`
and do not overwrite the site `App.config`. If the remote folder is incomplete
or was deleted, prepare a complete folder deployment instead of exe-only.

The latest real-site state before this handoff: COM11 was available,
`Runtime:Mode=Both`, SECS was passive listening on `0.0.0.0:5000`, ERACK Server
was listening on `127.0.0.1:7801`, local Unit was registered as
`UNIT001` / `SHELF001`, routes showed `1 online: SHELF001`, simulation was
disabled, and real Zebra printing was enabled.

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

Local .NET SDK candidates, in preferred order:

```text
D:\Desktop\打印机项目\dotnet-sdk-8.0.421-win-x64\dotnet.exe
D:\Desktop\打印机项目\.dotnet-sdk\dotnet.exe
```

Do not trust the system `dotnet` first. On this machine it can resolve to
`C:\Program Files\dotnet\dotnet.exe`, which may have only runtime components.
Always set `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `APPDATA`, and `LOCALAPPDATA` to
workspace-local paths before build/publish.

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

`App.config` must remain UTF-8 without BOM. Keep the Chinese comments, but every
program/script path that writes `App.config` must save with UTF-8 no BOM. Publish
outputs must not include `PrinterSecsGem.Eq.dll.config` or
`PrinterSecsGem.Eq.exe.config`; the customer-facing config is only `App.config`.

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

Latest field-easy v2 package after the 2026-06-24 quality/encoding fix:

```text
publish\printer-secs-gem-folder-field-easy-status-protocol-v2-20260624-2229.zip
publish\printer-secs-gem-min-update-field-easy-status-protocol-v2-20260624-2229.zip
```

The older `20260624-2153`, `2212`, `2217`, and `2224` field-easy packages are
superseded. Use the `2229` packages because they include UTF-8 no-BOM script
read/write fixes, split Zebra status calls, and script smoke-test fixes.

Later internal GUI/config-repair/timeout v3 package:

```text
publish\printer-secs-gem-folder-gui-config-repair-timeout-v3-20260624-234829.zip
publish\printer-secs-gem-min-update-gui-config-repair-timeout-v3-20260624-234829.zip
```

The `234829` package includes the 60s ERACK request timeout, config repair
scripts, and the clean `PrinterSecsGem-UI-SECS-Test.SMD`, but the GUI is still
not acceptable. Do not treat it as the final field UI package.

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
- Zebra command line `status` can query only one status type per call here. Do
  not pass `--printer` and `--portstatus` in the same command; run two commands
  and merge the interpretation in code/log review.

## Publish Quality Checklist

- Use a local SDK candidate, not the system `dotnet`, unless both local SDK
  candidates are missing.
- Build Release with 0 errors. `NU1900` network vulnerability-source warning is
  acceptable in this offline environment.
- Verify `App.config` and zip-contained config references start with `3C 3F 78`,
  not UTF-8 BOM `EF BB BF`.
- Verify full package contains `PrinterSecsGem.Eq.exe`, `App.config`,
  `log4net.config`, `zebra-command-line/`, `secs-simulator/`, and key scripts.
- Verify full/minimal packages do not contain `PrinterSecsGem.Eq.dll.config` or
  `PrinterSecsGem.Eq.exe.config`.
- Minimal update packages must not overwrite site `App.config`; include only an
  `App.config.reference` when a reference is useful.

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

1. Keep the current light GUI polish as the baseline. If the user wants another
   pass, adjust only spacing, title weight, and status text density. Verify on
   the real remote desktop before further layout changes.
2. Decide SECS Description encoding. Test `J("打印成功")`; if the simulator and
   target Host support JIS8, use `J` for Chinese descriptions. Otherwise keep
   protocol Description ASCII and show Chinese only in GUI/logs.
3. Retest real print after the 60s ERACK request timeout change. S8F4 should not
   time out before the Unit returns, and S8F4/S6F11 print results should agree.
4. Use only `secs-simulator\PrinterSecsGem-UI-SECS-Test.SMD` for normal field
   tests. Do not use old SMD files containing `SHELF999` negative cases.
5. Update `App.config` after official CEID/RPTID/VID ids are confirmed.
