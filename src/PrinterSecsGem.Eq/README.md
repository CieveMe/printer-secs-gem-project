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
  - `S6F21`
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

## Hardware integration point

Replace `MockHardwareGateway` with the actual C# serial implementation:

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
