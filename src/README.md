# 源码入口

[PrinterSecsGem.Eq](PrinterSecsGem.Eq/README.md) 是正式 .NET 8 Windows 应用，集成 WinForms 状态界面、SECS/GEM、ERACK/RFID 和 Zebra USB 打印。

- `Secs/`：消息处理、事件组包、MES RFID 字段过滤。
- `Hardware/`：真实与模拟硬件、RFID 写后回读工作流。
- `ErackNetwork/`：服务器和远端单元路由。
- `Printing/`：ZPL 与打印机接口。
- `StatusUi/`：中文状态界面。
- `Validation/`：本地验证运行入口。

协议回归验证位于仓库根目录的 [tests/PrinterSecsGem.Eq.ProtocolValidation](../tests/PrinterSecsGem.Eq.ProtocolValidation/)。
