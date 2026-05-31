# src

后续自研源码放在这里。

建议模块划分：

```text
src/
  Printer/       Zebra USB/ZPL 打印封装
  SecsGem/       HSMS 与 SECS-II 消息处理
  SerialShelf/   串口电子货架私有协议
  App/           桌面程序或后台服务入口
  Tests/         协议、模板、串口解析测试
```

在运行系统确认前，不建议直接把 Zebra 官方 demo 改造成正式工程。更稳的做法是先保留 demo 作为参考，再新建干净的业务工程。
