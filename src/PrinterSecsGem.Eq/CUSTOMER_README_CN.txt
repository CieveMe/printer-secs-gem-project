打印机 SECS/GEM 客户部署说明
============================

一、运行环境

Windows 10 及以上 x64。
需要 .NET 8 Desktop Runtime x64，当前 EXE 要求 8.0.26 或后续兼容的 8.0.x 版本。
真实打印需要现场 Zebra 驱动及 zebra-command-line 文件夹。

二、启动与版本确认

双击 PrinterSecsGem.Eq.exe。
窗口标题及顶部标题显示版本号，本次为 v1.0.8。
启动日志也记录 Application version。

三、目录与更新

首次部署需要完整运行目录。已有完整环境的普通功能更新可直接发送 EXE，无须压缩。

1. 关闭程序，确认任务管理器中 PrinterSecsGem.Eq.exe 进程已退出。
2. 备份原有 PrinterSecsGem.Eq.exe。
3. 只替换 PrinterSecsGem.Eq.exe。
4. 保留 App.config、log4net.config、zebra-command-line 及现场其他运行文件。
5. 启动程序，核对版本 v1.0.8 和现场连接状态。

不要覆盖现场 App.config，避免 IP、端口、COM 口、打印机地址等参数改变。
不要为此次更新删除现场已有 DLL、deps 或 runtimeconfig 文件。
本次交付是约 2 MB 的单文件 EXE。

四、RFID 数据规则

上传 MES 的查询返回、状态上报、RFID 读写事件中的 RFID 字段只保留英文字母和数字，大小写及顺序保持不变。

例如 ABC 123_! 上传为 ABC123。
如果过滤后为空，返回长度为 0 的字符串，原有有货状态和结果码不因过滤而改变。

硬件写入、回读校验、缓存和原始日志保留原始 RFID。点阵屏仍使用原有显示规则。

五、配置修改

现场统一修改程序目录的 App.config，保持 UTF-8 无 BOM，保存后重启程序。

常用配置：
  secs4net:IpAddress
  secs4net:Port
  secs4net:DeviceId
  secs4net:IsActive
  ERackHardware:PortName
  Printer:ZebraPrinterAddress
  ERackSensorDisplay:Enabled
  ERackSensorDisplay:PresenceMode
  ERackSensorDisplay:PollIntervalMilliseconds

正常传感器模式为 Sensor；传感器故障时临时使用 RfidPolling。模式及其他数值按现场确认配置，不直接套用开发机参数。

六、测试与排查

v1.0.8 已完成本地验证；客户实机测试结果仍待反馈。
日志位置：logs\printer-secs-gem.log。

排查时提供测试时间、程序版本、操作顺序、预期/实际结果、日志和界面截图。
