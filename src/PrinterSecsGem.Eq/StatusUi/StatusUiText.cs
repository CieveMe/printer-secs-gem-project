namespace PrinterSecsGem.Eq.StatusUi;

public sealed class StatusUiText
{
    private readonly bool _isChinese;

    private static readonly Dictionary<string, string> Chinese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WindowTitle"] = "打印机 SECS/GEM 状态",
        ["DashboardTitle"] = "运行状态面板",
        ["SecsGroup"] = "SECS / HSMS",
        ["ServerGroup"] = "ERACK 服务端",
        ["ClientGroup"] = "Unit 客户端",
        ["SimulationGroup"] = "仿真",
        ["LocalDeviceGroup"] = "本机设备",
        ["OperationGroup"] = "本地操作",
        ["SecsState"] = "连接",
        ["SecsRole"] = "角色",
        ["ServerState"] = "服务",
        ["Routes"] = "路由",
        ["ClientState"] = "客户",
        ["SimulationState"] = "仿真",
        ["Print"] = "打印",
        ["Com"] = "串口",
        ["Rfid"] = "RFID",
        ["Display"] = "屏幕",
        ["LastPrint"] = "打印",
        ["RecentSecs"] = "最近SECS",
        ["Config"] = "配置",
        ["ConfigPath"] = "配置文件",
        ["Printer"] = "打印机",
        ["Content"] = "内容",
        ["Discover"] = "发现",
        ["Save"] = "保存",
        ["Refresh"] = "刷新",
        ["OpenCom"] = "打开串口",
        ["CloseCom"] = "关闭串口",
        ["ReadRfid"] = "读RFID",
        ["WriteRfid"] = "写RFID",
        ["TestPrint"] = "测试打印",
        ["NoRfidOperation"] = "暂无 RFID",
        ["NoDisplayOperation"] = "暂无屏幕",
        ["NoPrintOperation"] = "暂无打印",
        ["NoSecsOperation"] = "暂无SECS收发",
        ["AutoUsb"] = "自动 USB",
        ["UnitDisabled"] = "Runtime:Mode 未启用本机 Unit",
        ["MockHardware"] = "模拟硬件",
        ["RealPrintOn"] = "真实打印开启",
        ["RealPrintOff"] = "真实打印关闭，仅生成 ZPL",
        ["ActiveRole"] = "主动连接 Host",
        ["PassiveRole"] = "被动监听",
        ["Mode"] = "模式",
        ["Host"] = "Host",
        ["DeviceId"] = "设备ID"
    };

    public StatusUiText(StatusUiOptions options)
    {
        _isChinese = options.IsChinese;
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_isChinese && Chinese.TryGetValue(key, out var value))
        {
            return value;
        }

        return key switch
        {
            "WindowTitle" => "Printer SECS GEM Status",
            "DashboardTitle" => "Status Dashboard",
            "SecsGroup" => "SECS / HSMS",
            "ServerGroup" => "ERACK Server",
            "ClientGroup" => "Unit Client",
            "SimulationGroup" => "Simulation",
            "LocalDeviceGroup" => "Local Device",
            "OperationGroup" => "Local Operations",
            "SecsState" => "State",
            "SecsRole" => "Role",
            "ServerState" => "Server",
            "Routes" => "Routes",
            "ClientState" => "Client",
            "SimulationState" => "Simulation",
            "Print" => "Print",
            "Com" => "COM",
            "Rfid" => "RFID",
            "Display" => "Display",
            "LastPrint" => "Last Print",
            "RecentSecs" => "Recent SECS",
            "Config" => "Config",
            "ConfigPath" => "Config File",
            "Printer" => "Printer",
            "Content" => "Content",
            "Discover" => "Discover",
            "Save" => "Save",
            "Refresh" => "Refresh",
            "OpenCom" => "Open COM",
            "CloseCom" => "Close COM",
            "ReadRfid" => "Read RFID",
            "WriteRfid" => "Write RFID",
            "TestPrint" => "Test Print",
            "NoRfidOperation" => "No RFID operation yet",
            "NoDisplayOperation" => "No display operation yet",
            "NoPrintOperation" => "No print operation yet",
            "NoSecsOperation" => "No SECS operation yet",
            "AutoUsb" => "auto USB",
            "UnitDisabled" => "Unit disabled by Runtime:Mode",
            "MockHardware" => "mock hardware",
            "RealPrintOn" => "Real print ON",
            "RealPrintOff" => "Real print OFF, ZPL only",
            "ActiveRole" => "Active connecting to Host",
            "PassiveRole" => "Passive listening on",
            "Mode" => "Mode",
            "Host" => "Host",
            "DeviceId" => "DeviceId",
            _ => key
        };
    }

    public string Status(string message)
    {
        if (!_isChinese || string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var text = message;
        text = text.Replace("Starting ", "启动中 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Disabled", "已禁用", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Stopped", "已停止", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Listening ", "监听中 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Connecting ", "连接中 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Connected ", "已连接 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Disconnected, retrying in ", "已断开，重连等待 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Registered unit=", "已注册 unit=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Removed unit=", "已移除 unit=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Simulation loaded, no RFID", "仿真：有料，无 RFID", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Simulation loaded: ", "仿真：有料，RFID=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Simulation empty", "仿真：空位", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Mock display clear: simulation empty", "模拟屏幕：空位清屏，未下发真实屏", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Mock display text: NO ID", "模拟屏幕：显示 NO ID，未下发真实屏", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Mock display text: ", "模拟屏幕：显示 ", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Mock display: real screen not connected", "模拟屏幕：未连接真实屏", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display text sent: ", "真实屏幕已显示：", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display cleared", "真实屏幕已清屏", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display failed: ", "屏幕显示失败：", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display disabled: ", "屏幕显示已禁用：", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display skipped: ", "屏幕显示已跳过：", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Display enabled: waiting for sensor state", "屏幕显示已启用：等待传感器状态", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Skipped: Runtime:Mode does not enable Unit", "已跳过：Runtime:Mode 未启用 Unit", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Enabled shelf=", "已启用 shelf=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("0 online", "0 个在线", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("Routes are maintained by ERACK Server", "仅服务端维护路由", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("1 online:", "1 个在线:", StringComparison.OrdinalIgnoreCase);
        text = text.Replace(" online:", " 个在线:", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("; last=", "；最近=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("registering shelf=", "注册 shelf=", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("retrying in ", "重连等待 ", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
