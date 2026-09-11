# PrinterSecsGem.Eq 维护说明

正式 EQ 侧应用，当前版本 v1.0.8。启动 `PrinterSecsGem.Eq.exe` 后，同一进程运行中文 WinForms 界面和后台通信服务。界面标题、启动日志、S1F2/S1F14 使用真实应用版本。

## 配置与运行模式

现场修改程序目录的 `App.config`（XML 的 `appSettings/add`，UTF-8 无 BOM），保存后退出并重启程序。不要按旧文档仅修改 `appsettings.json`；现场配置入口是 `App.config`。具体键以 [App.config](App.config) 和 [Program.cs](Program.cs) 为准，示例值不能直接覆盖现场参数。

| 配置键 | 用途 |
|---|---|
| Runtime:Mode | Unit、Server、Both，按部署角色选择 |
| secs4net:IsActive | true 主动连接 Host；false 被动监听 |
| secs4net:IpAddress / Port / DeviceId | HSMS 网络与设备参数 |
| ERackHardware:PortName | 串口，例如现场实际 COM 号 |
| ERackSensorDisplay:Enabled | 是否启用传感器/显示后台流程 |
| ERackSensorDisplay:PresenceMode | Sensor 或临时 RfidPolling |
| ERackSensorDisplay:PollIntervalMilliseconds | 后台轮询间隔 |
| ERackSensorDisplay:RfidPollingReadTimeoutMilliseconds | RFID 轮询读超时 |
| ERackSensorDisplay:RfidPollingEmptyConfirmCount | 轮询空/失败连续确认次数 |
| Printer:ZebraPrinterAddress | Zebra 打印机地址 |

Sensor 是正常传感器检测路径。仅在 `Enabled=true` 且 `PresenceMode=Sensor` 时，RFID 成功写入后立即回读一次，以实际写入长度 N 比较前 N 个 ASCII 字节，包括尾部空格；N 之后旧数据不参与比较。匹配后刷新点阵屏；回读失败或不一致返回 S10F12 码 7，描述为 `RFID Verify Read Failed` 或 `RFID Verify Mismatch`，上报 CE2005。校验成功后的显示失败只记日志，写入仍成功并上报 CE2002。

RfidPolling 用于传感器故障时临时检测有无货。S5F11 使用单元后台缓存，不触发额外读卡；配置格口无缓存时返回空状态成功。该模式写入后不增加同步回读/显示操作，事件触发和有无货判断仍使用现有原始状态逻辑。

## 协议与 RFID 字段

- S1F2 返回 `L(A("ERACK"), A("v1.0.8"))`；版本由应用动态取得。
- S1F14 返回 `L(B(00), L(A("ERACK"), A("v1.0.8")))`。
- S10F3 要求三个 A Item：货架号、格口号、显示内容；显式空内容清屏。内容限 printable ASCII，超过 DisplayMaxBytes 或格式不合法返回 S10F4 码 2。线上描述为 `Display Content Format Error`，界面显示“显示内容格式错误”。
- S5F12 包含格口列表、有无货状态、结果码和结果描述；码 0/1/2 分别代表成功/格口不存在/读取失败。
- S10F11 空 RFID 仍按格式错误处理。S10F12 只有标识、结果码及描述，没有 RFID 字段；携带 RFID 的写入结果在 S6F11 中。
- RFID 写入成功/失败的 S6F11 分别使用 CE2002/CE2005；格口状态变化走 S6F21。S5F1/ALID 当前不实现。
- S8F3/S8F4 和打印结果 S6F11 沿用既有打印流程。完整消息分发和事件组包见 [SecsMessageDispatcher](Secs/SecsMessageDispatcher.cs) 与 [SecsEventMessageFactory](Secs/SecsEventMessageFactory.cs)。

v1.0.8 的 [RfidMesValueFilter](Secs/RfidMesValueFilter.cs) 只在 SECS 消息组包边界调用，覆盖 S5F12 所有格口、S6F21、RFID 写入结果 S6F11，以及已有 RFID 读取 S6F11 构造入口。本机与远端单元数据最终采用相同组包规则。

| 原始 RFID | MES RFID |
|---|---|
| Abc123_测试!@#XyZ789 | Abc123XyZ789 |
| ABC 123 | ABC123 |
| TEST123 后跟空格 | TEST123 |
| 测试!@# | 长度为 0 的 A("") |

只保留 ASCII 英文字母和数字，保持大小写和顺序，不在首个非法字符处截断。过滤后为空不重算结果码、有无货状态或 CEID；过滤也不能识别并清除尾部旧数据中的合法字母数字。

硬件写入、Sensor 精确校验、缓存、内部路由模型和原始日志保留原值。RFID 派生显示仅去除尾部空格/空字节，Host 直接 S10F3 显示内容按原输入处理；显示屏不使用 MES 过滤器。

## 构建与验证

以下命令均在仓库根目录运行。优先使用本机工作区上一级的 SDK；其他开发机可用 PATH 中已安装的 .NET 8 SDK。依赖优先使用已有本地包，缺失时先核对离线依赖来源。

```powershell
$workspace = (Resolve-Path ..).Path
$dotnet = Join-Path $workspace 'dotnet-sdk-8.0.421-win-x64\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = Join-Path $workspace '.dotnet-sdk\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_HOME = Join-Path $workspace '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspace '.nuget-packages'
$env:APPDATA = Join-Path $workspace '.appdata\Roaming'
$env:LOCALAPPDATA = Join-Path $workspace '.appdata\Local'
$env:TEMP = Join-Path $workspace '.dotnet-temp'
$env:TMP = $env:TEMP
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:APPDATA, $env:LOCALAPPDATA, $env:TEMP | Out-Null

& $dotnet build .\src\PrinterSecsGem.Eq\PrinterSecsGem.Eq.csproj -c Release --no-restore
& $dotnet run --project .\tests\PrinterSecsGem.Eq.ProtocolValidation\PrinterSecsGem.Eq.ProtocolValidation.csproj -c Release --no-restore
```

上述命令要求已有还原结果。首次克隆时，分别对应用和测试 csproj 执行 `& $dotnet restore <项目路径> --configfile .\NuGet.Config`，并确认配置中的本地包源可用。测试通过标准为全部验证完成；v1.0.8 已运行 17 项。模拟器定义在 [SMD](../../samples/secs/PrinterSecsGem-UI-SECS-Test.SMD)。

需要真实启动界面时，沿用已核对的运行配置执行 `& $dotnet run --project .\src\PrinterSecsGem.Eq\PrinterSecsGem.Eq.csproj --no-restore`；这会启动实际后台服务，不能代替协议回归测试。

## 发布与日志

沿用上面的 SDK/临时目录环境，在仓库根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-exe-only-win-x64.ps1 -OutputPath .\publish\exe-only-win-x64
```

发布脚本依赖工作区现有离线 NuGet 源；若缺失，先准备依赖，不使用不完整 apphost 代替。交付约 2 MB 的单文件 EXE，目录仅含 `PrinterSecsGem.Eq.exe`。当前发布依赖 .NET 8 Desktop Runtime x64 8.0.26 或后续兼容 8.0.x；不包含运行时安装。

客户直接发送 EXE，不需要 ZIP。关闭程序、备份旧 EXE 后仅替换它，保留现场 App.config、log4net.config、Zebra SDK 和已有运行文件。运行日志为 `logs\printer-secs-gem.log`，包含版本和原始 RFID/HEX；修改文档不会改变已交付 EXE。客户部署步骤见 [客户说明](CUSTOMER_README_CN.txt)。
