# PrinterSecsGem.Eq

Lightweight EQ-side forwarding service.

## Current scope

- Active EQ connection to Host through `secs4net`.
- Primary message dispatch for:
  - `S1F1 -> S1F2`
  - `S1F3 -> S1F4`
  - `S5F11 -> S5F12`
  - `S8F3 -> S8F4`
- `S10F11 -> S10F12`
- Tag read event message factories:
  - `S6F11`
- ZPL label generation for large text + Code128 barcode + readable text.
- Replaceable printer and serial-hardware interfaces.

## Configuration

Edit `appsettings.json`:

```json
{
  "secs4net": {
    "DeviceId": 0,
    "IsActive": true,
    "IpAddress": "127.0.0.1",
    "Port": 5000
  }
}
```

`IsActive: true` means this EQ service actively connects to the Host.

Label settings are also configured in `appsettings.json`. The current target label is 2.5 x 1.575 inches at 203dpi, about 508 x 320 dots:

```json
{
  "LabelTemplate": {
    "Dpi": 203,
    "WidthDots": 508,
    "HeightDots": 320
  }
}
```

## Hardware integration point

The serial hardware code will be provided as C# source, DLL, or a sample project. Replace `MockHardwareGateway` with that actual implementation:

```csharp
services.AddSingleton<IHardwareGateway, ActualSerialHardwareGateway>();
```

Replace `FilePrinterGateway` with the real Zebra USB implementation when the target OS and SDK path are fixed:

```csharp
services.AddSingleton<IPrinterGateway, ZebraUsbPrinterGateway>();
```

The file printer currently writes generated ZPL to `output/zpl` for quick validation.

## Local validation

See `docs/本地验证指南.md`.

Quick local mock validation:

```powershell
dotnet run --project .\src\PrinterSecsGem.Eq\PrinterSecsGem.Eq.csproj -- --validate-local
```

The program writes runtime logs to `logs/printer-secs-gem.log` by default.
