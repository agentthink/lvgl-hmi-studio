using System;
using System.Collections.ObjectModel;
using System.IO;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Serialization;

// === 输出路径 ===
// 参数：<输出目录>，缺省为当前工作目录下的 mock-hmi-out。
// 不硬编码开发机家目录：否则在别的机器/CI 上会在 /home/<别人> 下凭空创建目录，
// 且产物路径随机器漂移（scripts/verify.sh 显式传入临时目录）。
var baseDir = Path.GetFullPath(args.Length > 0 ? args[0] : "mock-hmi-out");
var workDir = Path.Combine(baseDir, "MockHmiAssets");
var hmiPath = Path.Combine(baseDir, "MockChineseProject.hmi");
Directory.CreateDirectory(workDir);

Console.WriteLine("=== Mock HMI 工程生成工具 ===");
Console.WriteLine($"资源目录: {workDir}");
Console.WriteLine($"输出 .hmi: {hmiPath}");

// =========================================
// 1. 生成 3 张 BMP 图片（含丰富色彩、渐变色块、几何形状）
// =========================================
Console.WriteLine();
Console.WriteLine("[1/3] 生成测试图片...");

var imgBanner = Path.Combine(workDir, "banner_zh.bmp");
var imgLogo = Path.Combine(workDir, "logo_chip.bmp");
var imgBackground = Path.Combine(workDir, "bg_panels.bmp");

// 800x200 Banner 渐变 + 中文标题图案（像素色块模拟）
GenerateBannerBmp(imgBanner, 800, 200);
Console.WriteLine($"  ✓ banner_zh.bmp  (800x200)");

// 300x300 Logo 同心圆 + 芯片风格
GenerateChipLogoBmp(imgLogo, 300, 300);
Console.WriteLine($"  ✓ logo_chip.bmp (300x300)");

// 1024x600 完整背景（蓝紫色工业面板风）
GenerateIndustrialBgBmp(imgBackground, 1024, 600);
Console.WriteLine($"  ✓ bg_panels.bmp (1024x600)");

// =========================================
// 2. 构造 HmiProject（含中文控件 + 图片绑定）
// =========================================
Console.WriteLine();
Console.WriteLine("[2/3] 构造 HMI 工程模型...");

var project = new HmiProject
{
    Name = "Mock中文显示测试工程",
    LvglVersion = "9.5",
    HardwareTargetId = "allwinner-t113",
    Description = "用于验证模拟器中文与复杂图片显示修复效果的 mock 工程",
    Author = "自动化测试",
    Company = "指数智能 HMI 模拟器测试组",
    ProjectVersion = "2.0.0-test-cn-img",
};

// 屏幕尺寸与背景
project.ScreenDefaultsConfig.DefaultWidth = 1024;
project.ScreenDefaultsConfig.DefaultHeight = 600;

// 主屏幕（以复杂背景图为 BgImage，Stretch 拉伸）
var screenMain = new HmiScreen
{
    Id = "screen001",
    Name = "主控面板 · Main",
    Width = 1024,
    Height = 600,
    BgColor = "#0f172a",
    // 背景图稍后通过 asset.Id 绑定
};

// ============= 构造控件 =============

// (1) 屏幕顶部 Banner 图片
var imgBannerCtrl = new HmiControl
{
    Id = "img_banner_top",
    Name = "顶部Banner图",
    Type = HmiControlType.Image,
    X = 0, Y = 0,
    Width = 1024, Height = 120,
    // ImageAssetId 稍后绑定
};

// (2) Banner 上叠加的中文 Label —— 大标题
var lblTitle = new HmiControl
{
    Id = "lbl_title",
    Name = "主标题大字号",
    Type = HmiControlType.Label,
    X = 40, Y = 20,
    Width = 600, Height = 60,
    Text = "智慧工厂中央控制屏",
    TextColor = "#ffffff",
    BgColor = null,
};

// (3) 副标题
var lblSubtitle = new HmiControl
{
    Id = "lbl_subtitle",
    Name = "副标题",
    Type = HmiControlType.Label,
    X = 40, Y = 72,
    Width = 700, Height = 30,
    Text = "Industrial HMI · 中文渲染验证 · 图片加载自测",
    TextColor = "#93c5fd",
};

// (4) 右上角 Logo 图（芯片Logo）
var imgLogoCtrl = new HmiControl
{
    Id = "img_logo_right",
    Name = "右上角 Logo 图",
    Type = HmiControlType.Image,
    X = 1024 - 150, Y = 10,
    Width = 130, Height = 100,
    StretchMode = "Contain",
};

// (5) 左侧数据区 - 多条中文 label + 数值
var labelsData = new (string Id, string Name, double X, double Y, string Text, string TextColor, string? BgColor)[]
{
    ("lbl_temp",   "温度指示",  40, 150, "温度：27.5 ℃",      "#f97316", null),
    ("lbl_press",  "压力指示",  40, 190, "压力：0.85 MPa",    "#22d3ee", null),
    ("lbl_flow",   "流量指示",  40, 230, "流量：142.6 L/min", "#a3e635", null),
    ("lbl_power",  "功率指示",  40, 270, "功率：37.4 kW",     "#f472b6", null),
    ("lbl_status", "设备状态",  40, 310, "设备状态：运行中",   "#10b981", null),
    ("lbl_mode",   "运行模式",  40, 350, "运行模式：全自动",   "#60a5fa", "#1e293b"),
    ("lbl_alarms", "报警计数",  40, 390, "报警计数：0 / 总 12", "#ef4444", null),
    ("lbl_user",   "登录用户",  40, 430, "当前用户：管理员 (Admin)", "#e2e8f0", null),
    ("lbl_chinese", "罕用字测试", 40, 470, "CJK字形测试：壹贰叁肆伍 · 龍鳳龘靐 · 你好世界！", "#facc15", null),
};
foreach (var l in labelsData)
{
    var c = new HmiControl
    {
        Id = l.Id,
        Name = l.Name,
        Type = HmiControlType.Label,
        X = l.X, Y = l.Y,
        Width = 460, Height = 30,
        Text = l.Text,
        TextColor = l.TextColor,
        BgColor = l.BgColor,
    };
    screenMain.Controls.Add(c);
}

// (6) 中文字符按钮组（验证 Pressed/Normal 状态的中文和图片）
var btnStart = new HmiControl
{
    Id = "btn_start",
    Name = "启动按钮",
    Type = HmiControlType.Button,
    X = 40, Y = 530,
    Width = 150, Height = 50,
    Text = "启动生产线",
    BgColor = "#16a34a",
    TextColor = "#ffffff",
    Radius = 8,
};
var btnStop = new HmiControl
{
    Id = "btn_stop",
    Name = "停止按钮",
    Type = HmiControlType.Button,
    X = 210, Y = 530,
    Width = 150, Height = 50,
    Text = "紧急停止",
    BgColor = "#dc2626",
    TextColor = "#ffffff",
    Radius = 8,
};
var btnReset = new HmiControl
{
    Id = "btn_reset",
    Name = "复位按钮",
    Type = HmiControlType.Button,
    X = 380, Y = 530,
    Width = 150, Height = 50,
    Text = "复位/清零",
    BgColor = "#2563eb",
    TextColor = "#ffffff",
    Radius = 8,
};

// (7) 中文下拉选项
var ddRecipes = new HmiControl
{
    Id = "dd_recipe",
    Name = "配方选择",
    Type = HmiControlType.Dropdown,
    X = 600, Y = 150,
    Width = 380, Height = 40,
    Options = "默认配方\n高温烘焙\n低温慢煮\n消毒模式\n自定义方案",
    SelectedIndex = 0,
};

// (8) 滑块（带中文单位）
var sliderSpeed = new HmiControl
{
    Id = "slider_speed",
    Name = "传送速度",
    Type = HmiControlType.Slider,
    X = 600, Y = 210,
    Width = 380, Height = 30,
    MinValue = 0, MaxValue = 100, Value = 42,
    ValuePrefix = "速度：",
    ValueSuffix = " 米/分",
    IndicatorColor = "#8b5cf6",
};

// (9) 条形图（Bar）
var barLevel = new HmiControl
{
    Id = "bar_level",
    Name = "罐体液位",
    Type = HmiControlType.Bar,
    X = 600, Y = 260,
    Width = 380, Height = 30,
    MinValue = 0, MaxValue = 100, Value = 68,
    ValuePrefix = "液位：",
    ValueSuffix = " %",
    IndicatorColor = "#06b6d4",
};

// (10) 中央大图（右侧，展示复杂图片）
var imgCenterRight = new HmiControl
{
    Id = "img_right_panel",
    Name = "右侧大图",
    Type = HmiControlType.Image,
    X = 600, Y = 310,
    Width = 380, Height = 200,
    StretchMode = "Fill",
};

// (11) 表格（中文表头 + 数据）
var tableInfo = new HmiControl
{
    Id = "tbl_lines",
    Name = "生产线状态表",
    Type = HmiControlType.Table,
    X = 40, Y = 510,  // 被按钮挡住，调整位置到... 屏幕中部
    Width = 500, Height = 10, // 实际上调整
};
// 放按钮上方
tableInfo.X = 40;
tableInfo.Y = 420;
tableInfo.Width = 500;
tableInfo.Height = 100;
tableInfo.RowCount = 4;
tableInfo.ColumnCount = 3;
tableInfo.HeaderTexts = "产线编号,状态,良品率";
// Table content isn't strongly typed in HmiControl; header and content is rendered by label texts
// Leave as default.

// 先移除之前 lbl_user 以下的，调整布局避免与 table 重叠
var toRemoveLbl = screenMain.Controls.FirstOrDefault(c => c.Id == "lbl_user");
if (toRemoveLbl != null) { toRemoveLbl.Y = 350; toRemoveLbl.X = 560; toRemoveLbl.Width = 440; }
var toRemoveLbl2 = screenMain.Controls.FirstOrDefault(c => c.Id == "lbl_chinese");
if (toRemoveLbl2 != null) { toRemoveLbl2.Y = 390; toRemoveLbl2.X = 560; toRemoveLbl2.Width = 440; }

// 添加控件到屏幕
screenMain.Controls.Insert(0, imgBannerCtrl);
screenMain.Controls.Insert(1, imgLogoCtrl);
screenMain.Controls.Insert(2, lblTitle);
screenMain.Controls.Insert(3, lblSubtitle);
screenMain.Controls.Add(btnStart);
screenMain.Controls.Add(btnStop);
screenMain.Controls.Add(btnReset);
screenMain.Controls.Add(ddRecipes);
screenMain.Controls.Add(sliderSpeed);
screenMain.Controls.Add(barLevel);
screenMain.Controls.Add(imgCenterRight);
screenMain.Controls.Add(tableInfo);

// (10) 启动屏输入框（验证模拟器软键盘：点击弹出，Number 模式）
screenMain.Controls.Add(new HmiControl
{
    Id = "ta_s1_input",
    Name = "S1-参数输入",
    Type = HmiControlType.TextArea,
    X = 80, Y = 520,
    Width = 300, Height = 50,
    PlaceholderText = "点击弹出软键盘",
    Text = "",
    BgColor = "#0f172a",
    TextColor = "#ffffff",
    BorderWidth = 2,
    BorderColor = "#475569",
    KeyboardMode = "Number",   /* 验证：模拟器数字键盘标记 */

});

// (11) canvas 类控件：SVG / Tank / Pipe / Valve / Ellipse
// 这些控件走 lv_canvas + lv_draw_* 路径，必须有真实 C 编译覆盖。
// 历史教训：Golden 工程原先不含任何 canvas 控件，导致两个在 LVGL 9.x 中
// 并不存在的 API（lv_canvas_set_px_opa / lv_draw_polygon）以及缓冲少分配 25%
// 的缺陷长期未被端到端验收发现。
screenMain.Controls.Add(new HmiControl
{
    Id = "svg_g1", Name = "SVG-泵", Type = HmiControlType.Svg,
    X = 80, Y = 310, Width = 120, Height = 120,
    SvgViewBoxWidth = 100, SvgViewBoxHeight = 100,
    SvgShapes = new List<SvgShape>
    {
        new SvgShape { Kind = SvgShapeKind.Rect, Points = new[] { 10.0, 10, 80, 60 }, Fill = "#38bdf8", Stroke = "#0ea5e9", StrokeWidth = 2 },
        new SvgShape { Kind = SvgShapeKind.Circle, Points = new[] { 50.0, 40, 20 }, Fill = "#f59e0b" },
        new SvgShape { Kind = SvgShapeKind.Path, PathData = "M10 80 L50 60 L90 80 Z", Fill = "#22c55e" },
    },
});
screenMain.Controls.Add(new HmiControl
{
    Id = "tank_g1", Name = "罐体液位", Type = HmiControlType.Tank,
    X = 220, Y = 300, Width = 100, Height = 140,
    BgColor = "#64748b", IndicatorColor = "#0ea5e9",
    MinValue = 0, MaxValue = 100, Value = 60,
    TankShowScale = true, TankTickCount = 5,
});
screenMain.Controls.Add(new HmiControl
{
    Id = "pipe_g1", Name = "管路", Type = HmiControlType.Pipe,
    X = 340, Y = 360, Width = 160, Height = 40,
    BgColor = "#64748b", IndicatorColor = "#38bdf8", PipeFlowSpeed = 500,
});
screenMain.Controls.Add(new HmiControl
{
    Id = "valve_g1", Name = "球阀", Type = HmiControlType.Valve,
    X = 520, Y = 350, Width = 60, Height = 60,
    ValveType = "Ball", Value = 1, BgColor = "#ef4444", IndicatorColor = "#22c55e",
});
screenMain.Controls.Add(new HmiControl
{
    Id = "ellipse_g1", Name = "椭圆", Type = HmiControlType.Ellipse,
    X = 600, Y = 350, Width = 120, Height = 80,
    BgColor = "#334155", BorderWidth = 2, BorderColor = "#94a3b8",
});

// 背景图绑定（先创建 AssetLibrary 再绑定）
project.Screens.Add(screenMain);
// 运行时 Golden 场景：变量绑定 + 报警 + 趋势，覆盖动态主循环路径。
project.VariableManager.Connections.Add(new DeviceConnection { Id = "conn_golden", Name = "Golden PLC", DriverId = "modbus-tcp" });
project.VariableManager.Variables.Add(new Variable { Id = "v_golden_temp", Name = "测试温度", Address = "40001", DataType = "Float", ConnectionId = "conn_golden", Unit = "℃" });
project.VariableManager.Variables.Add(new Variable { Id = "v_golden_state", Name = "测试状态", Address = "00001", DataType = "Bool", ConnectionId = "conn_golden" });
btnStart.Actions.Add(HmiAction.SetVariable("v_golden_temp", "75.0f"));
var goldenLabel = new HmiControl { Id = "golden_runtime_label", Name = "运行时温度", Type = HmiControlType.Label, X = 560, Y = 80, Width = 300, Height = 40, Text = "温度" };
goldenLabel.VariableBindings.Add(new VariableBindingData { VariableId = "v_golden_temp", Direction = BindingDirection.ReadOnly, PropertyName = "Text" });
screenMain.Controls.Add(goldenLabel);
var goldenSwitch = new HmiControl { Id = "golden_runtime_switch", Name = "运行时状态", Type = HmiControlType.Switch, X = 860, Y = 80, Width = 100, Height = 40 };
goldenSwitch.VariableBindings.Add(new VariableBindingData { VariableId = "v_golden_state", Direction = BindingDirection.ReadOnly, PropertyName = "Value" });
screenMain.Controls.Add(goldenSwitch);
// 单位含 '%' 的绑定标签：验证 printf 格式串中的 '%' 必须被转义为 '%%'，
// 否则生成 "%.1f%" 属于非法转换说明符（UB）。
project.VariableManager.Variables.Add(new Variable { Id = "v_golden_pct", Name = "占空比", Address = "40003", DataType = "Float", ConnectionId = "conn_golden", Unit = "%" });
var goldenPct = new HmiControl { Id = "golden_runtime_pct", Name = "占空比", Type = HmiControlType.Label, X = 560, Y = 130, Width = 300, Height = 40, Text = "占空比" };
goldenPct.VariableBindings.Add(new VariableBindingData { VariableId = "v_golden_pct", Direction = BindingDirection.ReadOnly, PropertyName = "Text" });
screenMain.Controls.Add(goldenPct);
project.AlarmGroups.Add(new AlarmGroup { Id = "golden_alarm_group", Name = "Golden 报警", Alarms = { new AlarmConfig { Id = "golden_alarm", Name = "温度高", IsEnabled = true, Condition = new AlarmCondition { VariableId = "v_golden_temp", Type = AlarmConditionType.HighLimit, Threshold = 80 } } } });
project.TrendConfigs.Add(new TrendConfig { Id = "golden_trend", Name = "温度趋势", IsEnabled = true, MaxPoints = 100, Pens = { new TrendPen { Label = "温度", Color = "#ef4444", VariableId = "v_golden_temp", IsEnabled = true } } });



// ============= Asset Library（图片资源） =============
var assetLib = new AssetLibrary();

AssetItem CreateImageAsset(string assetId, string assetName, string filePath)
{
    var fi = new FileInfo(filePath);
    var (w, h) = ReadBmpSize(filePath);
    var item = new AssetItem
    {
        Id = assetId,
        Name = assetName,
        Type = AssetType.Image,
        FilePath = filePath,
        SizeBytes = fi.Length,
        Width = w,
        Height = h,
        ColorDepth = 32,
    };
    assetLib.AddAsset(item);
    assetLib.IncrementReference(assetId);
    return item;
}

var assetBanner = CreateImageAsset("asset_banner", "顶部Banner(中文渐变)", imgBanner);
var assetLogo = CreateImageAsset("asset_logo", "芯片Logo", imgLogo);
var assetBg = CreateImageAsset("asset_bg", "工业面板背景", imgBackground);
var assetCenter = CreateImageAsset("asset_center", "右侧大图(同背景)", imgBackground);
assetLib.IncrementReference(assetBg.Id); // 屏幕 + 图片控件两处引用

// 将 Asset Id 绑定到控件
imgBannerCtrl.ImageAssetId = assetBanner.Id;
imgLogoCtrl.ImageAssetId = assetLogo.Id;
imgCenterRight.ImageAssetId = assetCenter.Id;
screenMain.BgImageAssetId = assetBg.Id;

// ============= 再添加一屏（第二屏），验证屏幕切换时的中文渲染 =============
var screen2 = new HmiScreen
{
    Id = "screen002",
    Name = "参数设置 · 第二屏",
    Width = 1024, Height = 600,
    BgColor = "#1e293b",
};

screen2.Controls.Add(new HmiControl
{
    Id = "lbl_s2_title",
    Name = "S2标题",
    Type = HmiControlType.Label,
    X = 40, Y = 40,
    Width = 900, Height = 50,
    Text = "参数设置页 · 中文繁体与符号测试：繁體中文測試 · αβγ · ΣΠΩ · ≠≤≥∞",
    TextColor = "#fde68a",
});
screen2.Controls.Add(new HmiControl
{
    Id = "lbl_s2_1",
    Name = "S2-设定值标签",
    Type = HmiControlType.Label,
    X = 80, Y = 120,
    Width = 260, Height = 30,
    Text = "目标温度设定值 (℃)",
    TextColor = "#e2e8f0",
});
screen2.Controls.Add(new HmiControl
{
    Id = "ta_s2_temp",
    Name = "S2-温度输入",
    Type = HmiControlType.TextArea,
    X = 80, Y = 160,
    Width = 300, Height = 50,
    PlaceholderText = "请输入温度（0-200）",
    Text = "125",
    BgColor = "#0f172a",
    TextColor = "#ffffff",
    BorderWidth = 2,
    BorderColor = "#475569",
    KeyboardMode = "Number",   /* 验证：模拟器数字键盘标记 */
});
screen2.Controls.Add(new HmiControl
{
    Id = "sw_s2_heat",
    Name = "S2-加热开关",
    Type = HmiControlType.Switch,
    X = 430, Y = 170,
    Width = 90, Height = 40,
    IsChecked = true,
});
screen2.Controls.Add(new HmiControl
{
    Id = "lbl_s2_heat",
    Name = "S2-开关右侧提示",
    Type = HmiControlType.Label,
    X = 540, Y = 170,
    Width = 200, Height = 40,
    Text = "◉ 预加热：已启用",
    TextColor = "#4ade80",
});
screen2.Controls.Add(new HmiControl
{
    Id = "btn_s2_save",
    Name = "S2-保存",
    Type = HmiControlType.Button,
    X = 800, Y = 520,
    Width = 180, Height = 50,
    Text = "💾 保存设置到文件",
    BgColor = "#0891b2",
    TextColor = "#ffffff",
    Radius = 10,
});
project.Screens.Add(screen2);
btnStart.Actions.Add(HmiAction.SwitchScreen("参数设置 · 第二屏"));


Console.WriteLine($"  ✓ 构造 {project.Screens.Count} 个屏幕，控件 {project.Screens.Sum(s => s.Controls.Count)} 个");
Console.WriteLine($"  ✓ 资产库包含 {assetLib.Assets.Count(a => a.Type == AssetType.Image)} 张图片");

// ============= Font Asset（完整 CJK 字体，供 FreeType 运行时加载） =============
Console.WriteLine();
Console.WriteLine("[2.5/3] 添加字体资源（Noto Sans CJK 完整字形集）...");

// 候选系统中文字体：优先选择 NotoSansCJK Regular（全字形，含 龍鳳龘靐 等罕见字）
string[] candidateFonts = new[]
{
    "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc",
    "/usr/share/fonts/wenquanyi/wqy-zenhei/wqy-zenhei.ttc",
    "/usr/share/fonts/noto-cjk/NotoSerifCJK-Regular.ttc",
};
string? foundSystemFont = null;
foreach (var f in candidateFonts)
    if (File.Exists(f)) { foundSystemFont = f; break; }

if (foundSystemFont != null)
{
    var fontExt = Path.GetExtension(foundSystemFont).TrimStart('.').ToLowerInvariant();
    var fontDest = Path.Combine(workDir, $"NotoSansCJK_Regular.{fontExt}");
    File.Copy(foundSystemFont, fontDest, overwrite: true);
    var fi = new FileInfo(fontDest);
    var fontAsset = new AssetItem
    {
        Id = "asset_font_cjk",
        Name = "Noto Sans CJK 全字形中文字体",
        Type = AssetType.Font,
        FilePath = fontDest,
        SizeBytes = fi.Length,
        Width = 0,
        Height = 20,   // 默认像素字号 20，由 generated_fonts.c 读取
        ColorDepth = 0,
    };
    assetLib.AddAsset(fontAsset);
    assetLib.IncrementReference(fontAsset.Id);
    Console.WriteLine($"  ✓ 已复制系统字体 {Path.GetFileName(foundSystemFont)} → {fontDest}");
    Console.WriteLine($"    字体大小: {fi.Length / 1024.0 / 1024.0:0.0} MB，资产 ID: {fontAsset.Id}");
}
else
{
    Console.WriteLine("  ⚠ 未找到系统中文字体（Noto CJK / WenQuanYi），字体资产未添加。");
    Console.WriteLine("    模拟器将 fallback 到内置 SimSun_16_CJK 子集（部分罕见字可能缺失）。");
}

// =========================================
// 3. 打包为 .hmi
// =========================================
Console.WriteLine();
Console.WriteLine("[3/3] 打包为 .hmi 文件...");
var package = new HmiPackageContent(
    Project: project,
    AssetLibrary: assetLib,
    ResourceLibrary: null,
    AssetFileResolver: asset => asset.FilePath);

using (var fs = File.Create(hmiPath))
{
    HmiPackageService.Pack(fs, package, editorVersion: "MockHmiGen/1.0");
}
var sizeKB = (new FileInfo(hmiPath).Length) / 1024.0;
Console.WriteLine($"  ✓ .hmi 大小: {sizeKB:0.0} KB → {hmiPath}");
Console.WriteLine($"     包含资源:");
Console.WriteLine($"       · 图片: {assetLib.Assets.Count(a => a.Type == AssetType.Image)} 张");
Console.WriteLine($"       · 字体: {assetLib.Assets.Count(a => a.Type == AssetType.Font)} 个");

Console.WriteLine();
Console.WriteLine("✅ Mock 工程生成完毕！");
Console.WriteLine($"   下一步（模拟器输出目录，LVGL 源码目录由第 3 个参数或环境变量 LVGL_DIR 指定）:");
Console.WriteLine($"     dotnet run --project tools/SimExport/SimExport.csproj -- \\");
Console.WriteLine($"       \"{hmiPath}\" \"{Path.Combine(baseDir, "sim-out")}\" \"$LVGL_DIR\"");

// =====================================================
// Helper: 生成 BMP 文件（32-bit ARGB，LV_USE_BMP 可直接解码）
// =====================================================
static void GenerateBannerBmp(string path, int w, int h)
{
    // 水平蓝紫渐变 + 斜纹条带 + 右下角黄色菱形装饰
    var pixels = new uint[w * h];
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            // Gradient: #1e3a8a (left) -> #7c3aed (center) -> #06b6d4 (right)
            double t = x / (double)(w - 1);
            byte r, g, b;
            if (t < 0.5)
            {
                double s = t / 0.5;
                r = (byte)(0x1e + (0x7c - 0x1e) * s);
                g = (byte)(0x3a + (0x3a - 0x3a) * s);
                b = (byte)(0x8a + (0xed - 0x8a) * s);
            }
            else
            {
                double s = (t - 0.5) / 0.5;
                r = (byte)(0x7c + (0x06 - 0x7c) * s);
                g = (byte)(0x3a + (0xb6 - 0x3a) * s);
                b = (byte)(0xed + (0xd4 - 0xed) * s);
            }
            // Diagonal stripes lighter every 12 px
            int stripe = (x + y) % 24;
            if (stripe < 4)
            {
                r = (byte)Math.Min(255, r + 35);
                g = (byte)Math.Min(255, g + 35);
                b = (byte)Math.Min(255, b + 35);
            }
            uint a = 0xFF;
            pixels[y * w + x] = (a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
        }
    }
    // 绘制黄色菱形装饰 (右下角)
    int cx = w - 100, cy = h - 40;
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dx = Math.Abs(x - cx), dy = Math.Abs(y - cy);
            if (dx + dy < 30)
                pixels[y * w + x] = 0xFF_FF_D7_00; // amber
        }
    // 左上角红色圆点装饰
    (cx, cy) = (60, h / 2);
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy < 22 * 22)
                pixels[y * w + x] = 0xFF_EF_44_44;
        }
    WriteBmp32(path, w, h, pixels);
}

static void GenerateChipLogoBmp(string path, int w, int h)
{
    var pixels = new uint[w * h];
    Array.Fill(pixels, 0xFF_0B_12_20); // dark bg
    int cx = w / 2, cy = h / 2;
    // Outer ring
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int dx = x - cx, dy = y - cy;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d is >= 110 and <= 130) pixels[y * w + x] = 0xFF_22_D3_EE; // cyan
            else if (d is >= 70 and <= 85) pixels[y * w + x] = 0xFF_A7_8B_FA; // violet
            else if (d < 50)
            {
                // concentric checker (chip die pattern)
                int bx = (x - cx + 300) / 14;
                int by = (y - cy + 300) / 14;
                if ((bx + by) % 2 == 0) pixels[y * w + x] = 0xFF_10_B9_81; else pixels[y * w + x] = 0xFF_06_5F_46;
            }
        }
    // 4 pins on each side
    for (int i = 0; i < 6; i++)
    {
        int yy = 40 + i * ((h - 80) / 5);
        FillRect(pixels, w, h, 0, yy, 20, 12, 0xFF_F5_9E_0B); // orange pins left
        FillRect(pixels, w, h, w - 20, yy, 20, 12, 0xFF_F5_9E_0B);
    }
    // Text-like stripes (pseudo-"芯片" silhouette)
    FillRect(pixels, w, h, cx - 40, cy - 6, 80, 12, 0xFF_FB_71_85);
    FillRect(pixels, w, h, cx - 40, cy + 8, 80, 6, 0xFF_38_BD_F8);
    WriteBmp32(path, w, h, pixels);
}

static void GenerateIndustrialBgBmp(string path, int w, int h)
{
    var pixels = new uint[w * h];
    // Diagonal dark gradient
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double t = (x * 0.55 + y * 0.45) / (0.55 * w + 0.45 * h);
            byte r = (byte)(0x0F + (0x1E - 0x0F) * t);
            byte g = (byte)(0x17 + (0x3A - 0x17) * t);
            byte b = (byte)(0x2A + (0x6B - 0x2A) * t);
            // fine grid
            bool grid = (x % 32 == 0) || (y % 32 == 0);
            if (grid) { r += 14; g += 14; b += 18; }
            pixels[y * w + x] = 0xFF_00_00_00 | ((uint)b << 16) | ((uint)g << 8) | r;
        }
    // Right-side big panel: rounded corner lighter box
    int px = 550, py = 140, pw = 450, ph = 400;
    for (int y = py; y < py + ph; y++)
        for (int x = px; x < px + pw; x++)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) continue;
            int rx = Math.Min(x - px, (px + pw - 1) - x);
            int ry = Math.Min(y - py, (py + ph - 1) - y);
            int radius = 18;
            bool inside = (rx >= radius || ry >= radius) || ((rx - radius) * (rx - radius) + (ry - radius) * (ry - radius) <= radius * radius);
            if (inside)
            {
                uint c = pixels[y * w + x];
                byte br = (byte)(c & 0xFF), bg_ = (byte)((c >> 8) & 0xFF), bb_ = (byte)((c >> 16) & 0xFF);
                br = (byte)Math.Min(255, br + 40);
                bg_ = (byte)Math.Min(255, bg_ + 50);
                bb_ = (byte)Math.Min(255, bb_ + 70);
                pixels[y * w + x] = 0xFF_00_00_00 | ((uint)bb_ << 16) | ((uint)bg_ << 8) | br;
            }
        }
    // Red alarm ribbon (top-left warning strip)
    for (int y = 140; y < 220; y++)
        for (int x = 40; x < 500; x++)
        {
            int dx = x - 40;
            if (((dx / 16) % 2) == 0) pixels[y * w + x] = 0xFF_B9_1C_1C; // red stripes
            else pixels[y * w + x] = 0xFF_FE_F2_F2; // white stripes
        }
    // Green status bar (bottom strip)
    for (int y = h - 36; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte rr = 0x16, gg = 0x65, bb = 0x16;
            pixels[y * w + x] = 0xFF_00_00_00 | ((uint)bb << 16) | ((uint)gg << 8) | rr;
        }
    WriteBmp32(path, w, h, pixels);
}

static void FillRect(uint[] px, int w, int h, int x, int y, int bw, int bh, uint color)
{
    for (int j = y; j < y + bh; j++)
        for (int i = x; i < x + bw; i++)
            if (i >= 0 && i < w && j >= 0 && j < h) px[j * w + i] = color;
}

// Minimal 32-bit BGRA BMP writer (Windows format, LV_USE_BMP supports it out-of-box)
static void WriteBmp32(string path, int w, int h, uint[] rgbaPixels)
{
    // Convert RGBA -> BGRA (BMP native byte order per pixel)
    byte[] row = new byte[w * 4];
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    int dataOffset = 54;
    int dataSize = w * h * 4;
    int fileSize = dataOffset + dataSize;
    // File header (14 bytes)
    bw.Write((byte)'B'); bw.Write((byte)'M');
    bw.Write((uint)fileSize);
    bw.Write((ushort)0); bw.Write((ushort)0);
    bw.Write((uint)dataOffset);
    // Info header (40 bytes, BITMAPINFOHEADER, BI_RGB)
    bw.Write((uint)40);
    bw.Write((int)w);
    bw.Write((int)h);        // positive = bottom-up
    bw.Write((ushort)1);
    bw.Write((ushort)32);     // bits per pixel
    bw.Write((uint)0);        // compression: BI_RGB
    bw.Write((uint)dataSize);
    bw.Write((int)2835);      // X ppm
    bw.Write((int)2835);      // Y ppm
    bw.Write((uint)0);
    bw.Write((uint)0);
    // Pixels (bottom-up row order)
    for (int y = h - 1; y >= 0; y--)
    {
        for (int x = 0; x < w; x++)
        {
            uint c = rgbaPixels[y * w + x];
            byte r = (byte)(c & 0xFF);
            byte g = (byte)((c >> 8) & 0xFF);
            byte b = (byte)((c >> 16) & 0xFF);
            byte a = (byte)((c >> 24) & 0xFF);
            row[x * 4 + 0] = b;
            row[x * 4 + 1] = g;
            row[x * 4 + 2] = r;
            row[x * 4 + 3] = a;
        }
        bw.Write(row);
    }
}

// Quick BMP size reader (header validation)
static (int w, int h) ReadBmpSize(string path)
{
    using var fs = File.OpenRead(path);
    using var br = new BinaryReader(fs);
    if (br.ReadByte() != 'B' || br.ReadByte() != 'M') throw new InvalidOperationException("Not a BMP file: " + path);
    fs.Seek(18, SeekOrigin.Begin);
    int w = br.ReadInt32();
    int h = br.ReadInt32();
    return (Math.Abs(w), Math.Abs(h));
}
