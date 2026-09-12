# 电子货架 SECS/GEM 与打印应用

Windows EQ 侧应用，将 Host/MES 的 SECS/GEM HSMS 命令转发到电子货架 RFID、传感器、点阵屏及 Zebra USB 标签打印机。WinForms 状态界面与后台服务运行在同一进程内。

## 当前状态

- 当前版本：v1.0.8；功能提交 `8876e18` 已按用户要求推送至 Gitee `origin/master` 和 GitHub `github/master`。
- 本地协议验证通过 17 项。客户已确认 RFID 过滤需求，尚未收到 v1.0.8 客户实机验收通过反馈。
- 最近明确现场通过的回退版本为 v1.0.3；v1.0.4 已获客户远程测试通过确认。
- v1.0.8 采用直接 EXE 交付，不需要 ZIP；现场配置保留原样。

## 已实现能力

- 基于 .NET 8 / WinForms 的状态界面，显示版本、连接状态和运行日志。
- 基于 secs4net 的 HSMS 通信，支持配置主动或被动连接。
- ERACK 本机串口控制和远端单元路由、RFID 读写、传感器检测及点阵屏设置。
- Zebra USB 打印、ZPL 标签生成及打印结果上报；保留 mock/file 接口用于本地验证。

| 请求/事件 | 行为 |
|---|---|
| S1F1 → S1F2 | 返回 ERACK 设备标识及真实软件版本 |
| S1F3 → S1F4 | 状态变量查询 |
| S1F13 → S1F14 | COMMACK 通信建立应答 |
| S5F11 → S5F12 | 查询格口 RFID、有无货状态及结果描述 |
| S8F3 → S8F4 | 打印及即时结果 |
| S10F3 → S10F4 | 点阵屏设置或清屏 |
| S10F11 → S10F12 | RFID 写入及即时结果 |
| S6F21 | 格口状态变化主动上报 |
| S6F11 | RFID 写入结果、打印结果；另保留 RFID 读取事件构造入口 |

写标签成功事件使用 CE2002，失败使用 CE2005；S5F1/ALID 当前不实现。具体字段和边界见[应用维护说明](src/PrinterSecsGem.Eq/README.md)。

## RFID 数据边界

v1.0.8 在组装 S5F12、S6F21 和 RFID 读写 S6F11 时，只对 RFID 字段保留 `A-Z/a-z/0-9`，保持顺序和大小写。例如 `ABC 123_!` 变为 `ABC123`。过滤后为空返回长度为 0 的 `A("")`，不改变过滤前的结果码或有无货状态。

硬件写入、回读校验、轮询缓存和原始 HEX 日志保留原始 RFID。显示屏沿用自己的处理规则。该过滤不会修改货架号、格口号、描述或打印内容。

## 目录与入口

| 路径 | 用途 |
|---|---|
| [应用源码](src/PrinterSecsGem.Eq/) | WinForms、协议、硬件及打印服务 |
| [系统架构](docs/architecture.md) | 数据流、模块职责、配置边界和交付流程 |
| [协议验证](tests/PrinterSecsGem.Eq.ProtocolValidation/) | 可执行的本地回归验证 |
| [SECS 测试定义](samples/secs/PrinterSecsGem-UI-SECS-Test.SMD) | 模拟器测试入口 |
| [发布脚本](scripts/publish-exe-only-win-x64.ps1) | framework-dependent 单文件 EXE |
| [客户部署说明](src/PrinterSecsGem.Eq/CUSTOMER_README_CN.txt) | 启停、配置和更新步骤 |
| [第三方依赖说明](third_party/README.md) | SDK 来源和集成说明 |

`docs/`、`notes/` 和 `PROJECT_CONTEXT.md` 保留历史资料，旧“骨架/待接入”描述不代表当前实现。本机工作区上一级的 `wiki/`、`raw/` 是项目记忆，不在本 Git 仓库内；克隆仓库不会获得这些目录。

## 构建与交付

需要 Windows x64、.NET 8 SDK 和已有依赖。可复现的 PowerShell 构建、验证与发布命令见[应用维护说明](src/PrinterSecsGem.Eq/README.md#构建与验证)。

客户运行需 .NET 8 Desktop Runtime x64；当前单文件发布要求 8.0.26 或后续兼容的 8.0.x 版本。普通更新时退出进程、备份旧 EXE，再只替换 `PrinterSecsGem.Eq.exe`；保留 `App.config`、日志配置、Zebra SDK 及其他现场运行文件。单文件 EXE 约 2 MB，151 KB 左右的 apphost 不能用于仅替换 EXE 的功能更新。

不将编译产物、完整第三方 SDK、现场日志和现场配置作为正常源码交付内容。
