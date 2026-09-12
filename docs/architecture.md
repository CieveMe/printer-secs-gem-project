# 系统架构与维护边界

## 1. 系统定位

`PrinterSecsGem.Eq`（当前版本 `v1.0.8`）是运行在 Windows EQ 侧的 .NET 8 WinForms 应用。它把 Host/MES 的 HSMS/SECS-GEM 请求转换为电子货架、RFID、传感器、点阵屏和 Zebra 打印机的操作，并把结果转换回协议应答或主动事件。

程序界面和后台服务在同一个进程中运行。界面负责中文状态展示和人工操作；后台负责通信、硬件访问、事件发布和日志记录。

## 2. 数据流

```text
Host / MES
    │ HSMS / SECS-GEM
    ▼
SecsMessageDispatcher
    │ 请求解析与协议应答
    ├── RFID 查询/写入 ──► ERACK 硬件网关 ──► RFID 读写器
    ├── Sensor 状态 ────► ERACK 硬件网关 ──► 传感器
    ├── 点阵屏设置 ─────► 本机执行或 ERACK TCP 路由 ──► 远端单元
    └── 打印请求 ───────► ZebraCommandLinePrinterGateway ──► Zebra

RFID / Sensor / 打印结果
    ▼
ERackEventSink / SecsEventPublisher
    ▼
S6F11、S6F21 等主动事件
```

本机模式直接访问串口或打印机；服务模式接收远端单元请求；`Both` 同时提供两类能力。远端路由只改变执行位置，不改变协议字段和结果码组装规则。

## 3. 主要模块

| 模块 | 职责 |
|---|---|
| `SecsMessageDispatcher` | 接收并分发 Host 请求，校验 Item 结构，生成 SxFy 应答 |
| `SecsEventMessageFactory` | 按事件和报告定义组装 S6F11/S6F21 消息 |
| `SecsEventPublisher` | 发送主动事件并处理发送失败日志 |
| `ERackEventSink` | 接收硬件状态、RFID 和打印结果并转成事件 |
| `RfidWriteWorkflow` | 执行写入、Sensor 模式回读校验及显示刷新边界 |
| `ERackSensorDisplayWorker` | 轮询 Sensor/RFID 状态并维护后台状态 |
| `DisplayCommandService` | 校验显示内容，执行本机显示或交给远端单元 |
| `ERackSerialHardwareGateway` | 通过串口访问 RFID、传感器和点阵屏硬件 |
| `ERackTcpUnitRouter` | 将货架号/格口号路由到远端 ERACK 单元 |
| `ZebraCommandLinePrinterGateway` | 生成并发送 ZPL，返回打印结果 |
| `StatusUi` | 显示中文版本、连接状态、硬件状态和运行日志 |

## 4. RFID 数据边界

硬件写入、Sensor 回读校验、轮询缓存和原始日志保存原始字节/字符串。MES 输出在协议消息组装边界过滤 RFID，仅保留 ASCII 英文字母和数字；过滤为空时只清空 RFID 字段，不改变有无货状态、结果码或事件类型。显示屏使用独立的显示文本规则，不复用 MES 输出过滤器。

Sensor 写入流程只在启用 Sensor 显示功能且 `PresenceMode=Sensor` 时执行：写入成功后读取一次，以实际写入长度比较前 N 个原始字节，完全一致后才刷新显示。`RfidPolling` 使用后台缓存，不在 S5F11 或写入请求中增加同步读卡。

## 5. 配置和运行

现场配置入口是程序目录下的 `App.config`。重点配置包括：

- `Runtime:Mode`：`Unit`、`Server` 或 `Both`。
- `secs4net:*`：HSMS 主动/被动连接、地址、端口和设备号。
- `ERackHardware:*`：串口和硬件参数。
- `ERackSensorDisplay:*`：Sensor/RFID 轮询、显示和超时参数。
- `Printer:*`：Zebra 打印机地址及打印参数。

程序更新时只替换 `PrinterSecsGem.Eq.exe`，保留现场 `App.config`、日志配置、SDK 和其他运行文件。配置修改后必须重启程序。

## 6. 构建、验证和交付

在仓库根目录执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\publish-exe-only-win-x64.ps1 `
  -OutputPath .\publish\exe-only-<功能>-v<版本>
```

发布目录应只有约 2 MB 的 `PrinterSecsGem.Eq.exe`。构建后检查版本信息、SHA256、`git diff --check` 和协议回归验证；不要把小型 apphost 当作功能更新包。客户验证通过后再提交并推送已验收状态。

## 7. 维护原则

- 协议字段和结果码以当前客户确认版本为准，变更必须同步测试定义和说明文档。
- 原始值与对外展示值分层处理，避免为满足显示格式而破坏硬件校验。
- 失败路径必须记录请求上下文、结果码和必要的原始 HEX，便于现场复核。
- 新增协议字段时同时覆盖正常、空值、格式错误、超长、硬件失败和远端路由失败场景。
