# Printer SECS/GEM Project

本仓库用于整理电子货架项目的对接资料、技术调研记录和后续实现代码。

## 项目背景

项目涉及三部分：

- Zebra/ZD888TA 类 USB 标签打印机控制。
- 电子货架设备私有协议，当前描述为串口设备，现场原系统可能为 Win7。
- SECS/GEM-HSMS 协议对接，附件文档中定义了格口状态、RFID 设置、打印指令和事件上报。

当前已提供 Zebra Link-OS SDK demo，并确认曾改动 `SendFileView.xaml.cs`，通过直接发送 ZPL 字符串字节流打印出一维码。

## 当前判断

- 打印内容本身可以采用动态生成 ZPL 字符串的方式实现。
- 打印机不支持网口，因此不能走 TCP 9100。
- 打印机连接方式目前确认为 USB，但需要进一步确认 Zebra SDK 底层连接对象是 `DriverPrinterConnection` 还是 `UsbConnection`。
- Zebra SDK v4.0.3435 官方目标环境是 Windows 10/11、.NET 9，不适合作为 Win7 最终交付方案直接定型。
- 如果现场可升级到 Win10，优先沿用已跑通的 Zebra SDK 路线。
- 如果必须 Win7，需要单独验证 USB 打印通道，可能改用 Win7 兼容的 RAW ZPL 打印队列方案或旧版 Zebra SDK。

## 仓库结构

```text
docs/          项目附件、协议摘要、项目资料
notes/         对接问题、调研记录、沟通结论
src/           后续自研源码
third_party/   第三方 SDK 使用说明，不直接提交完整 SDK 包
```

## 不入库内容

以下内容默认不直接入库：

- Zebra SDK 完整解压包 `v4.0.3435/`
- 编译产物 `.vs/`、`bin/`、`obj/`
- 大体积压缩包、DLL、EXE、PDB、NuGet 包

第三方 SDK 请保留在本地或通过内部文件共享分发，并在 `third_party/README.md` 中记录版本与来源。

## 下一步

优先确认运行系统是否能升级 Win10。如果不能升级，需要先做 Win7 USB 打印验证，再进入 SECS/GEM 与串口协议开发。
