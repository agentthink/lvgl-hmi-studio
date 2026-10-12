namespace HmiEditor.AI.Generators;

/// <summary>
/// 图标语义分类器 —— 根据用户描述关键词将图标归类到 <see cref="IconCategory"/>。
/// 分类结果用于注入提示词(明确语义)并驱动变体选择策略。
/// 采用中英文双关键词匹配, 覆盖 HMI 常见图标语义。
/// </summary>
public static class IconCategoryClassifier
{
    /// <summary>将图标描述分类。无法匹配时返回 <see cref="IconCategory.General"/>。</summary>
    public static IconCategory Classify(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return IconCategory.General;
        var text = description.ToLowerInvariant();

        // 优先级: 过程 > 设备 > 动作 > 状态 > 安全 > 通信 > 导航 > 环境
        if (ContainsAny(text, ProcessKeywords)) return IconCategory.Process;
        if (ContainsAny(text, DeviceKeywords)) return IconCategory.Device;
        if (ContainsAny(text, ActionKeywords)) return IconCategory.Action;
        if (ContainsAny(text, StatusKeywords)) return IconCategory.Status;
        if (ContainsAny(text, SafetyKeywords)) return IconCategory.Safety;
        if (ContainsAny(text, CommunicationKeywords)) return IconCategory.Communication;
        if (ContainsAny(text, NavigationKeywords)) return IconCategory.Navigation;
        if (ContainsAny(text, EnvironmentKeywords)) return IconCategory.Environment;
        return IconCategory.General;
    }

    private static bool ContainsAny(string text, string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>设备类关键词: 泵/电机/阀门/压缩机/传感器/马达/风机等。</summary>
    private static readonly string[] DeviceKeywords =
    {
        "泵", "电机", "马达", "阀门", "阀", "压缩机", "风机", "风扇", "加热器", "冷却器",
        "传感器", "传动", "输送", "搅拌", "pump", "motor", "valve", "compressor",
        "fan", "heater", "cooler", "sensor", "actuator", "conveyor", "agitator"
    };

    /// <summary>动作类关键词: 启动/停止/复位/切换/确认/删除/保存等。</summary>
    private static readonly string[] ActionKeywords =
    {
        "启动", "停止", "复位", "暂停", "恢复", "切换", "确认", "取消", "删除", "添加",
        "保存", "导出", "导入", "运行", "关闭", "开启", "重置", "开始", "play", "stop",
        "pause", "reset", "start", "toggle", "confirm", "delete", "add", "save",
        "export", "import", "restart", "power on", "power off"
    };

    /// <summary>状态类关键词: 运行/报警/待机/故障/电量/正常等。</summary>
    private static readonly string[] StatusKeywords =
    {
        "报警", "警告", "错误", "正常", "待机", "故障", "电量", "状态", "提示", "信息",
        "alarm", "warning", "error", "ok", "normal", "standby", "fault", "battery",
        "status", "info", "notification", "ready", "idle"
    };

    /// <summary>安全类关键词: 急停/锁定/防护/安全/警告(安全语境)等。</summary>
    private static readonly string[] SafetyKeywords =
    {
        "急停", "锁定", "防护", "安全", "保护", "禁入", "危险", "emergency", "lock",
        "guard", "safety", "protect", "shield", "danger", "restrict"
    };

    /// <summary>通信类关键词: 网络/无线/串口/蓝牙/连接/信号等。</summary>
    private static readonly string[] CommunicationKeywords =
    {
        "网络", "无线", "串口", "蓝牙", "通信", "连接", "信号", "天线", "以太网",
        "network", "wifi", "wireless", "bluetooth", "serial", "communication",
        "connect", "signal", "antenna", "ethernet", "link"
    };

    /// <summary>导航类关键词: 返回/前进/首页/菜单/设置/上一页等。</summary>
    private static readonly string[] NavigationKeywords =
    {
        "返回", "前进", "首页", "菜单", "设置", "上一页", "下一页", "箭头", "方向",
        "back", "forward", "home", "menu", "settings", "next", "prev", "arrow",
        "direction", "navigate", "exit", "left", "right", "up", "down"
    };

    /// <summary>环境类关键词: 温度/湿度/光照/空气质量/CO2 等。</summary>
    private static readonly string[] EnvironmentKeywords =
    {
        "温度", "湿度", "光照", "亮度", "空气质量", "pm2.5", "co2", "氧气", "气压",
        "temperature", "humidity", "light", "brightness", "air", "pm2.5", "co2",
        "oxygen", "pressure", "weather"
    };

    /// <summary>过程类关键词: 管道/液位/流量/压力/罐体/仪表等(优先级最高)。</summary>
    private static readonly string[] ProcessKeywords =
    {
        "管道", "管线", "液位", "流量", "压力", "罐体", "储罐", "仪表", "液面",
        "pipe", "pipeline", "level", "flow", "pressure", "tank", "gauge",
        "meter", "manometer", "thermometer"
    };
}
