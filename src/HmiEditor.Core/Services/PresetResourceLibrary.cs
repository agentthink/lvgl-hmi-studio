using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 内置 HMI 图库资源预置定义器。
/// 为工业、汽车、智能家居等场景提供标准化的图标、模板和过程图形资源。
/// </summary>
public static class PresetResourceLibrary
{
    /// <summary>
    /// 生成内置资源列表 —— 按场景分类的标准化 HMI 资源。
    /// 所有资源均为矢量图标（Path Data，viewBox 0 0 24 24）或模板引用。
    /// </summary>
    public static List<HmiResourceItem> CreateBuiltIn()
    {
        var items = new List<HmiResourceItem>();

        // ═══════════════════════════════════════════════════════════
        // 工业控制场景（Industrial）
        // ═══════════════════════════════════════════════════════════

        // 状态指示
        items.Add(New("alarm-critical", "报警-紧急", "严重报警状态指示灯", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "报警", "#ef4444", "#991b1b", ["alarm", "critical", "emergency", "报警"],
            "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"));
        items.Add(New("alarm-warning", "报警-警告", "警告状态指示灯", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "报警", "#f59e0b", "#92400e", ["alarm", "warning", "报警"],
            "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"));
        items.Add(New("alarm-info", "报警-信息", "信息状态指示灯", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "报警", "#3b82f6", "#1d4ed8", ["alarm", "info", "通知"],
            "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"));
        items.Add(New("status-running", "状态-运行", "设备运行状态", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "状态", "#22c55e", "#166534", ["status", "running", "运行"],
            "M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4 12 14.01l-3-3"));
        items.Add(New("status-stopped", "状态-停止", "设备停止状态", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "状态", "#ef4444", "#991b1b", ["status", "stopped", "停止"],
            "M18 6 6 18M6 6l12 12"));
        items.Add(New("status-idle", "状态-待机", "设备待机状态", ResourceItemType.StatusIndicator, ApplicationScenario.Industrial, "状态", "#6b7280", "#374151", ["status", "idle", "待机"],
            "M12 2v10M18.4 6.6a9 9 0 1 1-12.8 0"));

        // 工业控制图标
        items.Add(New("motor-start", "电机启动", "电机启动控制", ResourceItemType.Icon, ApplicationScenario.Industrial, "电机", "#3b82f6", "#1d4ed8", ["motor", "start", "启动"],
            "M5 4l14 8-14 8V4z"));
        items.Add(New("motor-stop", "电机停止", "电机停止控制", ResourceItemType.Icon, ApplicationScenario.Industrial, "电机", "#ef4444", "#991b1b", ["motor", "stop", "停止"],
            "M6 6h12v12H6V6z"));
        items.Add(New("motor-reverse", "电机反转", "电机反转控制", ResourceItemType.Icon, ApplicationScenario.Industrial, "电机", "#8b5cf6", "#6d28d9", ["motor", "reverse", "反转"],
            "M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8M3 3v5h5M12 7v5l3 3"));
        items.Add(New("valve-open", "阀门-开启", "阀门开启状态", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "阀门", "#22c55e", "#166534", ["valve", "open", "阀门", "开启"],
            "M3 12h18M12 3v18M5 5l14 14M19 5L5 19"));
        items.Add(New("valve-close", "阀门-关闭", "阀门关闭状态", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "阀门", "#ef4444", "#991b1b", ["valve", "close", "阀门", "关闭"],
            "M18 6 6 18M6 6l12 12M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z"));
        items.Add(New("valve-half", "阀门-半开", "阀门半开状态", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "阀门", "#f59e0b", "#92400e", ["valve", "half", "半开"],
            "M3 12h18M12 3v9M5 5l7 7M19 5l-7 7"));
        items.Add(New("pump-on", "泵-运行", "水泵运行状态", ResourceItemType.Icon, ApplicationScenario.Industrial, "泵", "#3b82f6", "#1d4ed8", ["pump", "on", "泵", "运行"],
            "M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"));
        items.Add(New("pump-off", "泵-停止", "水泵停止状态", ResourceItemType.Icon, ApplicationScenario.Industrial, "泵", "#6b7280", "#374151", ["pump", "off", "泵", "停止"],
            "M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83M18 6 6 18M6 6l12 12"));
        items.Add(New("pressure-gauge", "压力表", "压力测量仪表", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "仪表", "#8b5cf6", "#6d28d9", ["pressure", "gauge", "压力表"],
            "M12 13a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM12 9v2l3 3M12 1v2M4.22 4.22l1.42 1.42M3 12H1M12 21v2M19.78 19.78l-1.42-1.42M21 12h2"));
        items.Add(New("temperature-gauge", "温度计", "温度测量仪表", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "仪表", "#f97316", "#c2410c", ["temperature", "gauge", "温度计"],
            "M14 4v10.54a4 4 0 1 1-4 0V4a2 2 0 0 1 4 0z"));
        items.Add(New("flow-meter", "流量计", "流量测量仪表", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "仪表", "#06b6d4", "#0891b2", ["flow", "meter", "流量计"],
            "M2 12h6l3-9 6 18 3-9h2"));
        items.Add(New("level-tank", "液位计", "液位测量仪表", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "仪表", "#06b6d4", "#0891b2", ["level", "tank", "液位"],
            "M5 3h14v18H5zM5 12h14M9 6h6M9 18h6"));
        items.Add(New("timer", "定时器", "时间控制", ResourceItemType.Icon, ApplicationScenario.Industrial, "控制", "#3b82f6", "#1d4ed8", ["timer", "time", "定时"],
            "M10 2v4M14 2v4M5 6h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zM12 10v4l2 2"));
        items.Add(New("emergency-stop", "急停", "紧急停止按钮", ResourceItemType.Icon, ApplicationScenario.Industrial, "安全", "#ef4444", "#991b1b", ["emergency", "stop", "急停", "安全"],
            "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zM18 6 6 18M6 6l12 12"));
        items.Add(New("safety-valve", "安全阀", "安全泄压阀", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "安全", "#ef4444", "#991b1b", ["safety", "valve", "安全", "泄压"],
            "M12 2a8 8 0 0 0-8 8c0 3.5 2 6.5 8 11 6-4.5 8-7.5 8-11a8 8 0 0 0-8-8z"));

        // 管道流程
        items.Add(New("pipe-horizontal", "管道-水平", "水平管道连接", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "管道", "#06b6d4", "#0891b2", ["pipe", "horizontal", "管道"],
            "M2 12h20M2 12v4M22 12v4"));
        items.Add(New("pipe-vertical", "管道-垂直", "垂直管道连接", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "管道", "#06b6d4", "#0891b2", ["pipe", "vertical", "管道"],
            "M12 2v20M12 2h4M12 22h4"));
        items.Add(New("pipe-elbow", "管道-弯头", "管道弯头连接", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "管道", "#06b6d4", "#0891b2", ["pipe", "elbow", "弯头"],
            "M17 2v10h10M7 22V12H20"));
        items.Add(New("pipe-tee", "管道-三通", "管道三通连接", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "管道", "#06b6d4", "#0891b2", ["pipe", "tee", "三通"],
            "M12 2v20M2 12h20M12 12h10"));
        items.Add(New("tank-simple", "储罐", "简单储罐", ResourceItemType.ProcessGraphic, ApplicationScenario.Industrial, "储罐", "#06b6d4", "#0891b2", ["tank", "storage", "储罐"],
            "M5 3h14v18H5zM5 8h14M9 3v5M15 3v5M9 16v5M15 16v5"));

        // ═══════════════════════════════════════════════════════════
        // 汽车信息娱乐场景（Automotive）
        // ═══════════════════════════════════════════════════════════

        // 车辆状态
        items.Add(New("car-door-open", "车门-开启", "车门开启状态", ResourceItemType.StatusIndicator, ApplicationScenario.Automotive, "车门", "#ef4444", "#991b1b", ["car", "door", "车门", "开启"],
            "M4 4h16v16H4zM4 9h16M9 9V5h6v4M4 13h6M14 13h6"));
        items.Add(New("car-door-closed", "车门-关闭", "车门关闭状态", ResourceItemType.StatusIndicator, ApplicationScenario.Automotive, "车门", "#22c55e", "#166534", ["car", "door", "车门", "关闭"],
            "M5 4h14v16H5zM5 10h14M5 16h3M16 16h3"));
        items.Add(New("car-hazard", "危险警示", "危险警示灯", ResourceItemType.StatusIndicator, ApplicationScenario.Automotive, "警示", "#f59e0b", "#92400e", ["hazard", "warning", "警示"],
            "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01"));
        items.Add(New("car-headlight", "大灯", "远光灯状态", ResourceItemType.Icon, ApplicationScenario.Automotive, "灯光", "#3b82f6", "#1d4ed8", ["headlight", "light", "大灯"],
            "M9 18h6M10 22h4M12 2a8 8 0 0 0-4 14.9v2.1L8 21h8l-2-3v-2.1A8 8 0 0 0 12 2z"));
        items.Add(New("car-ac", "空调", "空调控制", ResourceItemType.Icon, ApplicationScenario.Automotive, "空调", "#06b6d4", "#0891b2", ["ac", "air", "空调"],
            "M12 2v8l5 5M7 7l5 5-5 5M17 7l-5 5 5 5"));
        items.Add(New("car-seat-heat", "座椅加热", "座椅加热状态", ResourceItemType.Icon, ApplicationScenario.Automotive, "舒适", "#f97316", "#c2410c", ["seat", "heat", "座椅", "加热"],
            "M4 16V8a8 8 0 0 1 16 0v8M4 20h16M8 12v4M12 12v4M16 12v4"));
        items.Add(New("car-battery", "电池", "电池电量", ResourceItemType.StatusIndicator, ApplicationScenario.Automotive, "电池", "#22c55e", "#166534", ["battery", "power", "电池"],
            "M13 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-4h2a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2z"));
        items.Add(New("car-ev", "电动车", "电动车状态", ResourceItemType.Icon, ApplicationScenario.Automotive, "电动车", "#22c55e", "#166534", ["ev", "electric", "电动车"],
            "M13 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-4h2a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zM8 12l-2 4h4l-2 4M12 12l-2 4h4l-2 4M16 12l-2 4h4l-2 4"));

        // 车速与导航
        items.Add(New("speedometer", "速度表", "车速显示", ResourceItemType.ProcessGraphic, ApplicationScenario.Automotive, "仪表", "#3b82f6", "#1d4ed8", ["speed", "speedometer", "速度"],
            "M12 13a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM12 9v4l2 2M12 1v2M4.22 4.22l1.42 1.42M3 12H1M12 21v2M19.78 19.78l-1.42-1.42M21 12h2"));
        items.Add(New("navigation", "导航", "导航模式", ResourceItemType.Icon, ApplicationScenario.Automotive, "导航", "#06b6d4", "#0891b2", ["nav", "navigation", "导航"],
            "M3 11l19-9-9 19-2-8-8-2z"));
        items.Add(New("media-play", "媒体-播放", "媒体播放控制", ResourceItemType.Icon, ApplicationScenario.Automotive, "媒体", "#22c55e", "#166534", ["media", "play", "播放"],
            "M5 3l14 9-14 9V3z"));
        items.Add(New("media-pause", "媒体-暂停", "媒体暂停控制", ResourceItemType.Icon, ApplicationScenario.Automotive, "媒体", "#f59e0b", "#92400e", ["media", "pause", "暂停"],
            "M6 4h4v16H6zM14 4h4v16h-4z"));
        items.Add(New("volume", "音量", "音量控制", ResourceItemType.Icon, ApplicationScenario.Automotive, "媒体", "#8b5cf6", "#6d28d9", ["volume", "sound", "音量"],
            "M11 5 6 9H2v6h4l5 4V5zM19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"));

        // ═══════════════════════════════════════════════════════════
        // 智能家居场景（SmartHome）
        // ═══════════════════════════════════════════════════════════

        // 环境控制
        items.Add(New("home-light-on", "灯光-开", "灯光开启状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "灯光", "#f59e0b", "#92400e", ["light", "on", "灯", "开"],
            "M9 18h6M10 22h4M12 2a8 8 0 0 0-4 14.9v2.1L8 21h8l-2-3v-2.1A8 8 0 0 0 12 2z"));
        items.Add(New("home-light-off", "灯光-关", "灯光关闭状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "灯光", "#6b7280", "#374151", ["light", "off", "灯", "关"],
            "M9 18h6M10 22h4M12 2a8 8 0 0 0-4 14.9v2.1L8 21h8l-2-3v-2.1A8 8 0 0 0 12 2zM18 6 6 18"));
        items.Add(New("home-ac-on", "空调-开", "空调开启状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "空调", "#06b6d4", "#0891b2", ["ac", "on", "空调"],
            "M12 2v8l5 5M7 7l5 5-5 5M17 7l-5 5 5 5"));
        items.Add(New("home-ac-cool", "空调-制冷", "空调制冷模式", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "空调", "#3b82f6", "#1d4ed8", ["ac", "cool", "制冷"],
            "M12 2v8l5 5M7 7l5 5-5 5M17 7l-5 5 5 5M12 18v4M8 22h8"));
        items.Add(New("home-ac-heat", "空调-制热", "空调制热模式", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "空调", "#ef4444", "#991b1b", ["ac", "heat", "制热"],
            "M12 2v8l5 5M7 7l5 5-5 5M17 7l-5 5 5 5M8 12h8"));
        items.Add(New("home-humidity", "湿度", "湿度状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "环境", "#06b6d4", "#0891b2", ["humidity", "湿度"],
            "M12 2a7 7 0 0 0-7 7c0 5 7 13 7 13s7-8 7-13a7 7 0 0 0-7-7zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"));
        items.Add(New("home-temp", "温度", "室内温度", ResourceItemType.Icon, ApplicationScenario.SmartHome, "环境", "#ef4444", "#991b1b", ["temp", "temperature", "温度"],
            "M14 4v10.54a4 4 0 1 1-4 0V4a2 2 0 0 1 4 0z"));

        // 安全与门禁
        items.Add(New("home-door-open", "门锁-开", "门锁开启状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "门锁", "#ef4444", "#991b1b", ["door", "lock", "门锁", "开"]));
        items.Add(New("home-door-locked", "门锁-锁", "门锁关闭状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "门锁", "#22c55e", "#166534", ["door", "lock", "门锁", "锁"]));
        items.Add(New("home-motion", "移动侦测", "移动侦测传感器", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "安防", "#f59e0b", "#92400e", ["motion", "sensor", "移动侦测"]));
        items.Add(New("home-smoke", "烟雾报警", "烟雾探测器状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "安防", "#ef4444", "#991b1b", ["smoke", "alarm", "烟雾"]));
        items.Add(New("home-camera", "摄像头", "摄像头状态", ResourceItemType.Icon, ApplicationScenario.SmartHome, "安防", "#8b5cf6", "#6d28d9", ["camera", "监控"]));
        items.Add(New("home-lock", "智能锁", "智能门锁控制", ResourceItemType.Icon, ApplicationScenario.SmartHome, "门锁", "#3b82f6", "#1d4ed8", ["lock", "smart", "智能锁"]));

        // 能源管理
        items.Add(New("home-power", "电源", "电源状态", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "能源", "#22c55e", "#166534", ["power", "电源"],
            "M18.36 5.64a9 9 0 1 1-12.73 0M12 2v10"));
        items.Add(New("home-solar", "太阳能", "太阳能板状态", ResourceItemType.Icon, ApplicationScenario.SmartHome, "能源", "#f59e0b", "#92400e", ["solar", "太阳能"],
            "M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83M9 7h6v10H9z"));
        items.Add(New("home-battery", "家用电池", "家用电池电量", ResourceItemType.StatusIndicator, ApplicationScenario.SmartHome, "能源", "#22c55e", "#166534", ["battery", "home", "电池"],
            "M13 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-4h2a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2z"));
        items.Add(New("home-power-saving", "节能", "节能模式", ResourceItemType.Icon, ApplicationScenario.SmartHome, "能源", "#22c55e", "#166534", ["saving", "energy", "节能"],
            "M13 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2v-4h2a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2zM8 12h8M12 8v8"));

        // 窗帘与遮阳
        items.Add(New("curtain-open", "窗帘-开", "窗帘开启状态", ResourceItemType.Icon, ApplicationScenario.SmartHome, "窗帘", "#8b5cf6", "#6d28d9", ["curtain", "open", "窗帘"],
            "M3 3h18v18H3zM8 3v18M16 3v18M3 12h18"));
        items.Add(New("curtain-close", "窗帘-关", "窗帘关闭状态", ResourceItemType.Icon, ApplicationScenario.SmartHome, "窗帘", "#6b7280", "#374151", ["curtain", "close", "窗帘"],
            "M5 3h14v18H5zM5 8h14M5 16h14"));

        // 场景模式
        items.Add(New("home-scene-home", "场景-回家", "回家模式", ResourceItemType.Icon, ApplicationScenario.SmartHome, "场景", "#22c55e", "#166534", ["scene", "home", "回家"]));
        items.Add(New("home-scene-away", "场景-离家", "离家模式", ResourceItemType.Icon, ApplicationScenario.SmartHome, "场景", "#f59e0b", "#92400e", ["scene", "away", "离家"]));
        items.Add(New("home-scene-sleep", "场景-睡眠", "睡眠模式", ResourceItemType.Icon, ApplicationScenario.SmartHome, "场景", "#3b82f6", "#1d4ed8", ["scene", "sleep", "睡眠"]));
        items.Add(New("home-scene-night", "场景-夜间", "夜间模式", ResourceItemType.Icon, ApplicationScenario.SmartHome, "场景", "#8b5cf6", "#6d28d9", ["scene", "night", "夜间"]));

        // ═══════════════════════════════════════════════════════════
        // 通用 HMI 控件图标
        // ═══════════════════════════════════════════════════════════

        // 基本控件
        items.Add(New("btn-ok", "按钮-确认", "确认按钮", ResourceItemType.Icon, ApplicationScenario.Industrial, "按钮", "#22c55e", "#166534", ["btn", "ok", "确认"],
            "M20 6 9 17l-5-5"));
        items.Add(New("btn-cancel", "按钮-取消", "取消按钮", ResourceItemType.Icon, ApplicationScenario.Industrial, "按钮", "#ef4444", "#991b1b", ["btn", "cancel", "取消"],
            "M18 6 6 18M6 6l12 12"));
        items.Add(New("btn-reset", "按钮-复位", "复位按钮", ResourceItemType.Icon, ApplicationScenario.Industrial, "按钮", "#3b82f6", "#1d4ed8", ["btn", "reset", "复位"],
            "M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8M3 3v5h5"));
        items.Add(New("btn-menu", "菜单", "弹出菜单", ResourceItemType.Icon, ApplicationScenario.Industrial, "导航", "#6b7280", "#374151", ["menu", "菜单"],
            "M4 6h16M4 12h16M4 18h16"));
        items.Add(New("btn-back", "返回", "返回按钮", ResourceItemType.Icon, ApplicationScenario.Industrial, "导航", "#6b7280", "#374151", ["back", "返回"],
            "M19 12H5M12 19l-7-7 7-7"));

        // 数据可视化
        items.Add(New("chart-line", "折线图", "趋势折线图", ResourceItemType.ChartSeries, ApplicationScenario.Industrial, "图表", "#3b82f6", "#1d4ed8", ["chart", "line", "趋势"],
            "M3 3v18h18M3 21l6-6 4 4 7-10"));
        items.Add(New("chart-bar", "柱状图", "柱状统计图", ResourceItemType.ChartSeries, ApplicationScenario.Industrial, "图表", "#8b5cf6", "#6d28d9", ["chart", "bar", "柱状"],
            "M3 3v18h18M7 16V9M11 16V5M15 16v-7"));
        items.Add(New("chart-pie", "饼图", "饼状比例图", ResourceItemType.ChartSeries, ApplicationScenario.Industrial, "图表", "#06b6d4", "#0891b2", ["chart", "pie", "饼图"],
            "M21.21 15.89A10 10 0 1 1 8 2.83 10 10 0 0 1 21.21 15.89z"));
        items.Add(New("chart-area", "面积图", "面积趋势图", ResourceItemType.ChartSeries, ApplicationScenario.Industrial, "图表", "#22c55e", "#166534", ["chart", "area", "面积"],
            "M3 21v-8a2 2 0 0 1 2-2h2.5a2 2 0 0 1 1.93 1.44l2.07 7.56M13 21v-6a2 2 0 0 1 2-2h1.5a2 2 0 0 1 1.93 1.44l.57 4.56"));

        // 导航控件
        items.Add(New("nav-prev", "上一页", "上一页导航", ResourceItemType.Icon, ApplicationScenario.Industrial, "导航", "#6b7280", "#374151", ["nav", "prev", "上一页"],
            "M15 18l-6-6 6-6"));
        items.Add(New("nav-next", "下一页", "下一页导航", ResourceItemType.Icon, ApplicationScenario.Industrial, "导航", "#6b7280", "#374151", ["nav", "next", "下一页"],
            "M9 18l6-6-6-6"));
        items.Add(New("nav-home", "首页", "返回主页", ResourceItemType.Icon, ApplicationScenario.Industrial, "导航", "#3b82f6", "#1d4ed8", ["nav", "home", "首页"],
            "M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"));
        items.Add(New("nav-settings", "设置", "系统设置", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#6b7280", "#374151", ["nav", "settings", "设置"],
            "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z"));
        items.Add(New("nav-help", "帮助", "帮助信息", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#6b7280", "#374151", ["nav", "help", "帮助"],
            "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20zM9 9a3 3 0 1 1 3 3 2.5 2.5 0 0 0-2 2.5M12 17v.01"));

        // 通用状态
        items.Add(New("sync", "同步", "数据同步状态", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#3b82f6", "#1d4ed8", ["sync", "同步"],
            "M21 2v6h-6M3 12a9 9 0 0 1 15-6.7L21 8M3 22v-6h6M21 12a9 9 0 0 1-15 6.7L3 16"));
        items.Add(New("reload", "刷新", "数据刷新", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#06b6d4", "#0891b2", ["reload", "refresh", "刷新"],
            "M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"));
        items.Add(New("filter", "筛选", "数据筛选", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#8b5cf6", "#6d28d9", ["filter", "筛选"],
            "M22 3H2l8 9.46V19l4 2v-8.54L22 3z"));
        items.Add(New("zoom-in", "放大", "画面放大", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#3b82f6", "#1d4ed8", ["zoom", "in", "放大"],
            "M11 3a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM21 21l-4.35-4.35M11 8v6M8 11h6"));
        items.Add(New("zoom-out", "缩小", "画面缩小", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#3b82f6", "#1d4ed8", ["zoom", "out", "缩小"],
            "M11 3a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM21 21l-4.35-4.35M8 11h6"));
        items.Add(New("fullscreen", "全屏", "全屏模式", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#06b6d4", "#0891b2", ["fullscreen", "全屏"],
            "M8 3H5a2 2 0 0 0-2 2v3M21 8V5a2 2 0 0 0-2-2h-3M3 16v3a2 2 0 0 0 2 2h3M16 21h3a2 2 0 0 0 2-2v-3"));
        items.Add(New("alarm-history", "报警历史", "历史报警查询", ResourceItemType.Icon, ApplicationScenario.Industrial, "报警", "#f59e0b", "#92400e", ["alarm", "history", "历史"],
            "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zM12 16v-4M12 8h.01M10 2h4"));
        items.Add(New("report", "报表", "数据报表生成", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#8b5cf6", "#6d28d9", ["report", "报表"],
            "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M8 13h2M8 17h2M12 13h2M12 17h2M16 13h2M16 17h2"));
        items.Add(New("export", "导出", "数据导出", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#22c55e", "#166534", ["export", "导出"],
            "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12"));
        items.Add(New("user", "用户", "用户管理", ResourceItemType.Icon, ApplicationScenario.Industrial, "系统", "#6b7280", "#374151", ["user", "用户"],
            "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z"));

        return items;
    }

    // ── 辅助方法 ──

    private static HmiResourceItem New(
        string id, string name, string desc,
        ResourceItemType type, ApplicationScenario scenario,
        string subCat, string primaryColor, string? secondaryColor = null,
        string[]? tags = null, string? iconPathData = null) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Type = type,
            Scenario = scenario,
            SubCategory = subCat,
            Description = desc,
            DefaultWidth = 48,
            DefaultHeight = 48,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            Tags = tags ?? [],
            IconPathData = iconPathData,
        };
}
