打印机 SECS/GEM 客户部署说明
============================

一、启动方式

双击 PrinterSecsGem.Eq.exe 启动程序。

二、目录说明

客户目录根目录只保留运行必需文件：

  PrinterSecsGem.Eq.exe
  App.config
  log4net.config
  CUSTOMER_README_CN.txt
  zebra-command-line\

不要删除 zebra-command-line 文件夹，真实打印依赖该文件夹。

三、版本确认

程序窗口标题和顶部标题会显示版本号，例如 v1.0.1。
启动日志也会记录 Application version。

四、配置修改

现场配置统一修改 App.config，修改后关闭并重新打开 PrinterSecsGem.Eq.exe 生效。

常用配置：

  ERackHardware:PortName
  secs4net:IpAddress
  secs4net:Port
  secs4net:DeviceId
  secs4net:IsActive
  Printer:ZebraPrinterAddress
  ERackSensorDisplay:PresenceMode
  ERackSensorDisplay:PollIntervalMilliseconds

五、后续更新

普通功能更新只覆盖 PrinterSecsGem.Eq.exe。
不要覆盖现场 App.config，避免现场 IP、端口、COM 口、打印机地址等配置被改掉。

六、日志位置

运行日志：

  logs\printer-secs-gem.log

排查问题时优先提供该日志和程序窗口截图。
