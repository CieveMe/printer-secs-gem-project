# third_party

第三方依赖说明放在这里，完整 SDK 包默认不入库。

## Zebra Link-OS SDK

- 当前参考版本：`v4.0.3435`
- 本地参考目录：`D:\Desktop\打印机项目\v4.0.3435`
- 官方 demo：`demos-desktop/Source/DeveloperDemo_Windows.sln`
- 现有改动位置：`demos-desktop/Source/Zebra/Windows/DevDemo/Demos/SendFile/SendFileView.xaml.cs`
- 关键方式：生成 ZPL 字符串后调用 `printerConnection.Write(byte[])`

## 入库策略

不直接提交以下内容：

- SDK 完整目录
- `.rar/.zip/.7z`
- `.nupkg`
- `.dll/.exe/.pdb`
- `bin/obj/.vs`

如果后续必须固定 SDK 版本，建议通过内部制品库、网盘或私有 NuGet 源管理，并在本文档补充下载地址和校验信息。
