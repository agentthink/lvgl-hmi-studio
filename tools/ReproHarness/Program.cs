using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;

// ════════════════════════════════════════════════════════════════════════════
//  导出可复现性 harness —— 「同源同物」的度量仪
//
//  用法:
//      repro-harness <outDir> [--faceplate] [--raw]
//
//  做什么:
//      1. 用**确定性**语料（固定 id、固定时间戳、全 26 种控件 × 3 变体、
//         绑定单元格、报警、趋势、数据记录、动画、多屏 + 公共图层、可选 Faceplate 实例）
//         跑遍 CodeGenerator 的全部公共入口 + SimulatorProjectExporter.Export；
//      2. 把每个产物的内容写成文件（norm/ 下），并算 sha256（hashes.txt）。
//
//  怎么用:
//      # 跨进程可复现：两次运行必须逐字节一致
//      repro-harness /tmp/a ; repro-harness /tmp/b
//      diff -r /tmp/a/norm /tmp/b/norm          # 期望无输出
//
//      # 跨提交等价：同一语料在改动前后各跑一次，比对 norm/ 树
//      diff -r /tmp/before/norm /tmp/after/norm # 期望只差你打算改的东西
//
//  为什么默认要归一化（--raw 可关）:
//      产物头部有 `// Generator: HmiEditor.CodeGen <ver>+<sha> (git <short>)` ——
//      它**有意**嵌了构建时的 git 提交号，所以**跨提交**比对必然全红。
//      默认把该行折叠成固定串，让 diff 反映「产物逻辑」而不是「我提交了」。
//      ⚠️ 做 A/B 时两边最好在同一提交；跨提交比对务必用归一化输出。
// ════════════════════════════════════════════════════════════════════════════

var outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "repro-out";
var withFaceplate = args.Contains("--faceplate");
var raw = args.Contains("--raw");

outDir = Path.GetFullPath(outDir);
var normDir = Path.Combine(outDir, "norm");
if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
Directory.CreateDirectory(normDir);

var project = BuildCorpus(withFaceplate);
var assets = BuildAssets();
var gen = new CodeGenerator();

// ── 1. 收集所有「单元」产物：文件相对路径 → 内容 ──
var units = new SortedDictionary<string, string>(StringComparer.Ordinal);

void Unit(string relPath, string? content)
{
    if (!string.IsNullOrEmpty(content)) units[relPath] = content;
}

Unit("codegen/full_project.c", gen.GenerateFullProject(project, assets));
Unit("codegen/simulator_module.c", gen.GenerateSimulatorModule(project, assets));
Unit("codegen/shared_library.c", gen.GenerateSharedLibraryProject(project, assets));
Unit("codegen/theme.c", gen.GenerateThemeCode(project.Theme, assets));
Unit("codegen/layer_refs.c", gen.GenerateLayerRefsTable(project));
Unit("codegen/alarm.c", gen.GenerateAlarmCode(project.AlarmGroups.ToList()));
Unit("codegen/trend.c", gen.GenerateTrendCode(project.TrendConfigs.ToList(), project.DataLogConfigs.ToList()));
Unit("codegen/animation.c", gen.GenerateAnimationCode(project.AnimationConfigs.ToList()));

foreach (var screen in project.Screens)
{
    Unit($"codegen/screen_{screen.Id}.c", gen.GenerateScreenCode(screen, project.Theme, project: project));
    Unit($"codegen/screen_top_{screen.Id}.c", gen.GenerateScreenTopLevelWrapper(screen));
}

var build = gen.GenerateBuildSystem(project);
if (build != null) Unit($"codegen/{build.FileName}", build.Content);

// ── 2. 跑导出器（会多产出 main.c / lv_conf.h / CMakeLists.txt / generated_*.c …）──
var binding = new RuntimeBindingEngine(project);
binding.BuildRuntimeTags();

var exportDir = Path.Combine(outDir, "export");
var result = SimulatorProjectExporter.Export(
    project, units["codegen/simulator_module.c"], exportDir, assets,
    tags: binding.Tags, connections: project.VariableManager.Connections);

foreach (var f in result.Files)
{
    var full = Path.Combine(exportDir, f);
    if (File.Exists(full))
        Unit($"export/{f}", File.ReadAllText(full));
}

// ── 3. 落地 + 哈希 ──
var banner = new Regex(
    "^// Generator: HmiEditor\\.CodeGen .*$", RegexOptions.Compiled | RegexOptions.Multiline);

var hashLines = new List<string>();
foreach (var (relPath, content) in units)
{
    var text = raw ? content : banner.Replace(content, "// Generator: <stamp>");

    var target = Path.Combine(normDir, relPath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, text, new UTF8Encoding(false));

    hashLines.Add($"{Sha256(text)}  {relPath}");
}

File.WriteAllLines(Path.Combine(outDir, "hashes.txt"), hashLines, new UTF8Encoding(false));

Console.WriteLine($"语料: {(withFaceplate ? "含 2 个 Faceplate 实例" : "无 Faceplate")}"
                + $" / 屏 {project.Screens.Count} / 控件 {project.Screens.Sum(s => s.Controls.Count)}"
                + $" / 报警组 {project.AlarmGroups.Count} / 趋势 {project.TrendConfigs.Count}"
                + $" / 记录器 {project.DataLogConfigs.Count} / 动画 {project.AnimationConfigs.Count}");
Console.WriteLine($"单元数: {units.Count}   归一化: {(raw ? "否(--raw)" : "是")}");
Console.WriteLine($"输出: {normDir}");

static string Sha256(string text)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

// ════════════════════════════════════════════════════════════════════════════
//  确定性语料
// ════════════════════════════════════════════════════════════════════════════

static AssetLibrary BuildAssets()
{
    var lib = new AssetLibrary();
    lib.AddAsset(new AssetItem
    {
        Id = "img_logo", Name = "Logo", Type = AssetType.Image,
        FilePath = "assets/logo.bmp", SizeBytes = 4096,
        Width = 64, Height = 64, ColorDepth = 32, ReferenceCount = 1,
        ImportedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    });
    lib.AddAsset(new AssetItem
    {
        Id = "font_cjk", Name = "思源黑体", Type = AssetType.Font,
        FilePath = "assets/SourceHanSansSC.ttf", SizeBytes = 1048576,
        ReferenceCount = 1,
        ImportedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    });
    return lib;
}

static HmiProject BuildCorpus(bool withFaceplate)
{
    // 固定时间戳：模型默认值是 DateTime(Offset).Now，不钉死会让产物（或未来某天的
    // 产物）随墙钟漂移 —— 那属于「输入不同」而不是「不可复现」，但会污染比对。
    var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    var project = new HmiProject
    {
        Name = "ReproCorpus",
        LvglVersion = "9.5",
        Description = "导出可复现性语料",
        Author = "shu",
        Company = "zhishu",
        ProjectVersion = "1.2.3",
        CreatedAt = t0,
        LastModifiedAt = t0.AddDays(1),
    };

    // ── 通讯 + 变量 ──
    var conn = new DeviceConnection
    {
        Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp", IsEnabled = true,
    };
    conn.Config["ip"] = "192.168.1.10";
    conn.Config["port"] = "502";
    conn.Slaves.Add(new DeviceSlave
    {
        Id = "slave1", Name = "1号从站", ConnectionId = "conn1", SlaveAddress = 1,
    });
    conn.Slaves.Add(new DeviceSlave
    {
        Id = "slave2", Name = "2号从站", ConnectionId = "conn1", SlaveAddress = 2,
    });
    project.VariableManager.Connections.Add(conn);

    var vars = new[]
    {
        new Variable { Id = "v_float", Name = "温度", Address = "40001", DataType = "Float", ConnectionId = "conn1", Unit = "℃", SimulatedValue = "65.5" },
        new Variable { Id = "v_int", Name = "转速", Address = "40002", DataType = "Int16", ConnectionId = "conn1", SlaveId = "slave1" },
        new Variable { Id = "v_bool", Name = "运行", Address = "00001", DataType = "Bool", ConnectionId = "conn1", SlaveId = "slave2" },
        new Variable { Id = "v_str", Name = "配方名", Address = "40100", DataType = "String", ConnectionId = "conn1" },
    };
    foreach (var v in vars) project.VariableManager.Variables.Add(v);

    // ── 数据记录器（两种 LogFullPolicy 都覆盖）──
    project.DataLogConfigs.Add(new DataLogConfig
    {
        Id = "log1", SampleIntervalMs = 1000, MaxRecords = 1024,
        FullPolicy = LogFullPolicy.OverwriteOldest, StoragePath = "/sd/log1.csv",
        VariableIds = { "v_float", "v_int" }, RecordTimestamp = true,
        CsvDelimiter = ",", EnableCompression = true, IsEnabled = true,
    });
    project.DataLogConfigs.Add(new DataLogConfig
    {
        Id = "log2", SampleIntervalMs = 500, MaxRecords = 4096,
        FullPolicy = LogFullPolicy.StopWhenFull, StoragePath = "/sd/log2.csv",
        VariableIds = { "v_int" }, RecordTimestamp = false,
        CsvDelimiter = ";", EnableCompression = false, IsEnabled = true,
    });

    // ── 屏幕 ──
    var main = new HmiScreen
    {
        Id = "scr_main", Name = "主画面", Width = 1024, Height = 600, BgColor = "#0f172a",
    };
    var trend = new HmiScreen
    {
        Id = "scr_trend", Name = "趋势", Width = 1024, Height = 600, BgColor = "#111827",
        AdaptiveMode = ScreenAdaptiveMode.None,
    };
    var header = new HmiScreen
    {
        Id = "scr_header", Name = "公共页眉", Width = 1024, Height = 40,
        IsPublicScreen = true, PublicAccess = ScreenPublicAccess.Public,
    };

    // 主屏引用公共页眉作为背景图层
    main.ReferencedLayerIds.Add("scr_header");

    project.Screens.Add(main);
    project.Screens.Add(trend);
    project.Screens.Add(header);

    // ── 全 26 种控件 × 3 变体，铺在主屏上 ──
    var allTypes = Enum.GetValues<HmiControlType>();
    var seq = 0;
    foreach (var type in allTypes)
    {
        for (var variant = 0; variant < 3; variant++)
        {
            seq++;
            var c = MakeControl(type, variant, seq);
            // 表格控件需要单元格 + 绑定单元格，单独造满
            if (type == HmiControlType.Table)
            {
                c.RowCount = 4;
                c.ColumnCount = 3;
                c.ColumnWidths = "120,150,80";
                c.HeaderTexts = "名称,数值,单位";
                c.Cells = new List<TableCellData>();
                for (var r = 0; r < 4; r++)
                for (var col = 0; col < 3; col++)
                {
                    c.Cells.Add(r == 0
                        ? new TableCellData { Row = r, Col = col, Text = $"表头{col}" }
                        : new TableCellData
                        {
                            Row = r, Col = col, VariableId = "v_float",
                            Prefix = "温:", Suffix = "℃", DecimalPlaces = 2,
                        });
                }
            }
            main.Controls.Add(c);
        }
    }
    main.Controls.Add(MakeTable("tbl_main_4x3"));

    // ── 图片缩放模式的全输入覆盖 ──
    // 主循环每个类型只给 3 个 variant，而 EmitImageParameters 的输入有 6 条：
    // 空 / 默认(Fill) / None / Center / Contain / Tile。其中 **Contain 会走 LVGL 版本兜底分支**，
    // 是该方法里唯一有条件的分支 —— 语料不喂到它，改这段代码时 A/B 仍会报 0 差异，
    // 于是「零影响」的结论是假的（"比到了吗"陷阱）。
    var extraStretchModes = new (string? Mode, int Idx)[] { (null, 0), ("Tile", 1), ("Center", 2) };
    foreach (var (mode, idx) in extraStretchModes)
    {
        var img = MakeControl(HmiControlType.Image, idx, 950 + idx);
        img.Id = $"img_sm_{idx}";     // 主循环已占用 img_0/1/2，避免重名（obj_* 要求 id 工程内唯一）
        img.StretchMode = mode;
        main.Controls.Add(img);
    }

    // ── 趋势屏：图表 + 趋势配置 ──
    var chart1 = MakeControl(HmiControlType.Table, 0, 900); // 占位，下面单独造 Chart 语义用 Table
    chart1.Id = "chart_a";
    chart1.Type = HmiControlType.Table;
    chart1.Name = "趋势图A";
    trend.Controls.Add(chart1);

    project.TrendConfigs.Add(new TrendConfig
    {
        Id = "tr1", ControlId = "chart_a", Name = "温度趋势",
        TimeWindowMs = 60000, MaxPoints = 600, ShowLegend = true, ShowGrid = true,
        Pens =
        {
            new TrendPen { Label = "温度", VariableId = "v_float", Color = "#38bdf8", LineWidth = 2, ShowPoints = true, YAxisMin = 0, YAxisMax = 150 },
            new TrendPen { Label = "转速", VariableId = "v_int", Color = "#f97316", LineWidth = 1, IsEnabled = false },
        },
    });
    project.TrendConfigs.Add(new TrendConfig
    {
        Id = "tr2", ControlId = "chart_b", Name = "转速趋势",
        TimeWindowMs = 30000, MaxPoints = 300, ShowLegend = false, ShowGrid = true,
        Pens =
        {
            new TrendPen { Label = "转速", VariableId = "v_int", Color = "#22c55e", LineWidth = 3 },
        },
    });

    // ── 报警：2 组 × 3 条，覆盖多种条件类型与级别 ──
    var sev = new[] { AlarmSeverity.Info, AlarmSeverity.Warning, AlarmSeverity.Critical, AlarmSeverity.Emergency };
    var cond = new[]
    {
        AlarmConditionType.HighLimit, AlarmConditionType.LowLimit, AlarmConditionType.BitTrigger,
        AlarmConditionType.StateChange, AlarmConditionType.Deviation,
        AlarmConditionType.HighHighLimit, AlarmConditionType.LowLowLimit, AlarmConditionType.RateOfChange,
    };
    for (var g = 0; g < 2; g++)
    {
        var group = new AlarmGroup { Id = $"ag{g}", Name = $"分组{g}" };
        for (var a = 0; a < 3; a++)
        {
            group.Alarms.Add(new AlarmConfig
            {
                Id = $"al{g}_{a}", Name = $"报警{g}-{a}", Description = $"第 {g}-{a} 条",
                Severity = sev[(g + a) % sev.Length], GroupId = $"ag{g}",
                RequiresAck = a != 0, IsEnabled = true,
                Condition = new AlarmCondition
                {
                    VariableId = "v_float", Type = cond[(g * 3 + a) % cond.Length],
                    Threshold = 10 * (a + 1), Deadband = 0.5, DelayMs = 100 * a,
                    BitOffset = a, ExpectedValue = 42.5,
                },
                LinkedControlId = "lbl_0",
            });
        }
        project.AlarmGroups.Add(group);
    }

    // ── 动画：7 种缓动路径各一条 + 2 条带关键帧 ──
    var paths = Enum.GetValues<AnimationPathType>();
    for (var i = 0; i < paths.Length; i++)
    {
        project.AnimationConfigs.Add(new AnimationConfig
        {
            Id = $"a{(char)('a' + i)}111111", Name = $"动画{i}", ControlId = $"lbl_{i}",
            PropertyName = "Opacity", StartValue = 0, EndValue = 255,
            DurationMs = 300 + i * 50, DelayMs = i * 10, PathType = paths[i],
            RepeatCount = i % 2 == 0 ? -1 : i, IsEnabled = true, Trigger = "OnLoad",
            CreatedAt = t1, ModifiedAt = t1,
        });
    }
    project.AnimationConfigs.Add(new AnimationConfig
    {
        Id = "akf00001", Name = "关键帧动画", ControlId = "lbl_0", PropertyName = "X",
        StartValue = 0, EndValue = 200, DurationMs = 1200, PathType = AnimationPathType.EaseInOut,
        Trigger = "OnClick", CreatedAt = t1, ModifiedAt = t1,
        Keyframes =
        {
            new AnimationKeyframe { TimePercent = 0, Value = 0, PathType = AnimationPathType.Linear },
            new AnimationKeyframe { TimePercent = 35, Value = 120, PathType = AnimationPathType.Bounce, Description = "中段" },
            new AnimationKeyframe { TimePercent = 70, Value = 180, PathType = AnimationPathType.Overshoot },
            new AnimationKeyframe { TimePercent = 100, Value = 200 },
        },
    });

    // ── Faceplate（可选）：1 个模板 + 2 个实例 ──
    if (withFaceplate)
    {
        var tpl = new ControlTemplate { Id = "tpl_pump", Name = "泵面板", Category = "过程", Version = 3 };
        tpl.Parameters.Add(new TemplateParameter
        {
            Name = "Title", DisplayName = "标题", Direction = ParameterDirection.Input,
            Type = ParameterType.String, DefaultValue = "P-101",
        });
        tpl.Parameters.Add(new TemplateParameter
        {
            Name = "Fill", DisplayName = "填充色", Direction = ParameterDirection.Input,
            Type = ParameterType.Color, DefaultValue = "#22c55e",
        });
        tpl.Controls.Add(new HmiControl
        {
            Id = "tpl_lbl", Type = HmiControlType.Label, X = 0, Y = 0, Width = 80, Height = 20,
            Text = "{{Parameters.Title}}", TextColor = "#e2e8f0",
        });
        tpl.Controls.Add(new HmiControl
        {
            Id = "tpl_tank", Type = HmiControlType.Tank, X = 0, Y = 24, Width = 60, Height = 90,
            BgColor = "{{Parameters.Fill}}",
        });
        tpl.Controls.Add(new HmiControl
        {
            Id = "tpl_led", Type = HmiControlType.Led, X = 66, Y = 24, Width = 14, Height = 14,
            BgColor = "{{Parameters.Fill}}",
        });
        // 再加 Bar + Slider：这两类控件的**文件级指针符号**（`_cur_*` / `_knobtri_*` / `_curtri_*`）
        // 与值回调，只在"展开后的克隆体"上才会被发射 —— 旧实现遍历原始控件列表，于是克隆体
        // 「被发射却无声明 / 无定义」（C 编译错误）。另外用非默认属性 + 变量绑定，
        // 一并覆盖"属性保真"与"克隆体绑定刷新"两处修复。
        // ⚠️ 没有这段语料，本轮三处修复的 A/B 就是 **0 差异**（什么都没比到）。
        tpl.Controls.Add(new HmiControl
        {
            Id = "tpl_bar", Type = HmiControlType.Bar, X = 0, Y = 118, Width = 80, Height = 10,
            MinValue = 0, MaxValue = 50, StepValue = 5, Value = 20,
            ShowPointer = true, ShowTicks = true, MajorTickCount = 5,
            IndicatorColor = "{{Parameters.Fill}}", Rotation = 90,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Value", VariableId = "v_float", Direction = BindingDirection.ReadOnly,
                },
            },
        });
        tpl.Controls.Add(new HmiControl
        {
            Id = "tpl_sld", Type = HmiControlType.Slider, X = 0, Y = 132, Width = 80, Height = 14,
            MinValue = -20, MaxValue = 120, StepValue = 10, Value = 45,
            ShowPointer = true, ShowTicks = true, MajorTickCount = 7,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Value", VariableId = "v_int", Direction = BindingDirection.ReadWrite,
                },
            },
        });
        project.Templates.Add(tpl);

        for (var i = 0; i < 2; i++)
        {
            var inst = new HmiControl
            {
                Id = i == 0 ? "fp_pump_a" : "fp_pump_b",
                Type = HmiControlType.Rectangle, Name = $"泵{i + 1}",
                TemplateId = "tpl_pump", TemplateVersion = 3,
                X = 700, Y = 40 + i * 130, Width = 80, Height = 120,
            };
            inst.ParameterBindings.Add(new ParameterBinding
            {
                ParameterName = "Title", Value = $"P-10{i + 1}",
            });
            inst.ParameterBindings.Add(new ParameterBinding
            {
                ParameterName = "Fill", Value = i == 0 ? "#22c55e" : "#ef4444",
            });
            main.Controls.Add(inst);
        }
    }

    return project;
}

/// <summary>造一个指定类型的控件，variant 决定样式/参数分支（0/1/2 三态覆盖）。</summary>
static HmiControl MakeControl(HmiControlType type, int variant, int seq)
{
    var c = new HmiControl
    {
        Id = $"{Abbrev(type)}_{variant}",
        Type = type,
        Name = $"{type}-{variant}",
        X = 8 + (seq % 12) * 82,
        Y = 8 + (seq / 12) * 70,
        Width = 70 + variant * 10,
        Height = 44 + variant * 6,
        Text = variant == 0 ? "标签" : $"文{variant}",
        BgColor = variant switch { 0 => "#1e293b", 1 => "#2563eb", _ => null },
        TextColor = variant switch { 0 => "#e2e8f0", 1 => null, _ => "#22c55e" },
        BorderColor = variant == 0 ? "#475569" : null,
        Radius = variant * 4,
        BorderWidth = variant == 2 ? 2 : 0,
        FontSize = 12 + variant * 2,
        MinValue = 0,
        MaxValue = 100 + variant * 50,
        StepValue = 1,
        Value = 30 + variant * 20,
        ShowTicks = variant == 1,
        ShowPointer = variant == 2,
        Opacity = 100 - variant * 15,
        IndicatorColor = variant == 1 ? "#f97316" : null,
        ImageAssetId = type == HmiControlType.Image ? "img_logo" : null,
        StretchMode = type == HmiControlType.Image
            ? variant switch { 0 => "Fill", 1 => "Contain", _ => "None" }
            : null,
        Options = type == HmiControlType.Dropdown ? "选项A\n选项B\n选项C" : null,
        SelectedIndex = type == HmiControlType.Dropdown ? variant : 0,
        PlaceholderText = type == HmiControlType.TextArea ? "请输入" : null,
        MaxLength = type == HmiControlType.TextArea ? 16 : 0,
        PasswordMode = type == HmiControlType.TextArea && variant == 2,
        TabTitles = type == HmiControlType.TabView ? "页A,页B,页C" : null,
        KeyboardMode = type == HmiControlType.Keyboard ? variant switch { 0 => "Default", 1 => "Number", _ => "Special" } : null,
        LabelLongMode = type == HmiControlType.Label ? variant switch { 0 => null, 1 => "Wrap", _ => "ScrollCircular" } : null,
        ValueMode = type is HmiControlType.Slider or HmiControlType.Bar
            ? variant switch { 0 => "Normal", 1 => "Symmetrical", _ => "Range" } : null,
        MajorTickCount = 4 + variant,
        Brightness = 100 + variant * 60,
        SpinSpeed = 800 + variant * 400,
        LineCap = type == HmiControlType.Line ? variant switch { 0 => "Butt", 1 => "Round", _ => "Square" } : null,
        TankShowScale = true,
        TankTickCount = 3 + variant,
        PipeDirection = type == HmiControlType.Pipe ? (variant == 1 ? "Vertical" : "Horizontal") : null,
        PipeFlowDirection = type == HmiControlType.Pipe ? (variant == 2 ? "Reverse" : "Forward") : null,
        PipeFlowSpeed = type == HmiControlType.Pipe ? 1200 + variant * 600 : 0,
        ValveType = type == HmiControlType.Valve
            ? variant switch { 0 => "Ball", 1 => "Gate", 2 => "Butterfly", _ => "Globe" } : null,
        TextAlign = variant switch { 0 => TextAlignMode.Center, 1 => TextAlignMode.Left, _ => TextAlignMode.Right },
        TextVAlign = variant switch { 0 => TextVAlignMode.Middle, 1 => TextVAlignMode.Top, _ => TextVAlignMode.Bottom },
        ValueDisplayType = ValueDisplayType.Auto,
        ValueScaleFactor = 1.0 + variant * 0.5,
        ValueDecimalPlaces = variant,
        ValuePrefix = variant == 1 ? "值:" : null,
        ValueSuffix = variant == 1 ? "单位" : null,
    };

    // Line / Polyline 需要显式顶点
    if (type is HmiControlType.Line or HmiControlType.Polyline)
    {
        c.Points = new List<PointData>
        {
            new(0, 0), new(c.Width / 2, c.Height), new(c.Width, 0),
        };
    }

    // Arc：角度范围
    if (type == HmiControlType.Arc)
    {
        c.MinValue = 0;
        c.MaxValue = 270;
        c.Value = 90 + variant * 45;
    }

    // Svg：形状列表（7 种 Kind 都覆盖）
    if (type == HmiControlType.Svg)
    {
        c.SvgViewBoxWidth = 100;
        c.SvgViewBoxHeight = 100;
        c.BorderColor = "#38bdf8";
        c.BorderWidth = 2;
        c.SvgShapes = new List<SvgShape>
        {
            new() { Kind = SvgShapeKind.Rect, Points = new double[] { 10, 10, 30, 20, 4, 4 }, Fill = "#2563eb", Stroke = "#1e293b", StrokeWidth = 2 },
            new() { Kind = SvgShapeKind.Circle, Points = new double[] { 70, 30, 15 }, Fill = "#22c55e" },
            new() { Kind = SvgShapeKind.Ellipse, Points = new double[] { 40, 70, 25, 12 }, Fill = "none", Stroke = "#f97316", StrokeWidth = 3 },
            new() { Kind = SvgShapeKind.Line, Points = new double[] { 0, 0, 100, 100 }, Stroke = "#e2e8f0", StrokeWidth = 1 },
            new() { Kind = SvgShapeKind.Polyline, Points = new double[] { 5, 90, 30, 60, 55, 80, 80, 50 }, Stroke = "#38bdf8", StrokeWidth = 2 },
            new() { Kind = SvgShapeKind.Polygon, Points = new double[] { 60, 55, 80, 55, 70, 75 }, Fill = "#a855f7" },
            new() { Kind = SvgShapeKind.Path, PathData = "M10 20 L30 40 L50 20 Z", Fill = "#eab308" },
        };
    }

    // 状态化样式（三态覆盖）
    c.VisualStates.Add(new ControlVisualState
    {
        State = VisualState.Pressed, BgColor = "#334155", TextColor = "#ffffff",
        BorderColor = "#94a3b8", TextAlign = TextAlignMode.Center, TextVAlign = TextVAlignMode.Middle,
    });
    if (variant >= 1)
    {
        c.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Checked, BgColor = "#16a34a", Text = "已选中",
            ImageAssetId = "img_logo",
        });
    }
    if (variant == 2)
    {
        c.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Disabled, BgColor = "#1f2937", TextColor = "#6b7280",
        });
    }

    // 变量绑定（读 / 写 / 读写 三种方向 + 表达式）
    c.VariableBindings.Add(new VariableBindingData
    {
        PropertyName = type == HmiControlType.Label ? "Text" : "Value",
        VariableId = "v_float",
        Direction = variant switch
        {
            0 => BindingDirection.ReadOnly,
            1 => BindingDirection.ReadWrite,
            _ => BindingDirection.WriteOnly,
        },
    });
    if (variant == 2)
    {
        c.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "TextColor", VariableId = "", Kind = BindingSourceKind.Expression,
            Expression = "v_float > 100 ? 1 : 0",
        });
    }

    // 动作（按变体取不同的 ActionType / TriggerEvent）
    c.Actions.Add(variant switch
    {
        0 => HmiAction.SwitchScreen("主画面"),
        1 => HmiAction.SetVariable("v_int", "42"),
        _ => HmiAction.ToggleBit("v_bool"),
    });
    if (variant != 0)
    {
        c.Actions.Add(HmiAction.MomentaryOn("v_bool"));
    }

    return c;
}

/// <summary>主屏上额外放一个 4×3、全部绑定的表格 —— 覆盖表格绑定单元格的相位推导。</summary>
static HmiControl MakeTable(string id)
{
    var c = new HmiControl
    {
        Id = id, Type = HmiControlType.Table, Name = "主表",
        X = 520, Y = 300, Width = 420, Height = 180,
        RowCount = 4, ColumnCount = 3, RowHeight = 28,
        ColumnWidths = "140,160,120", HeaderTexts = "名称,数值,单位",
        BgColor = "#1e293b", TextColor = "#e2e8f0", BorderColor = "#475569", BorderWidth = 1,
        Cells = new List<TableCellData>(),
    };
    for (var r = 0; r < 4; r++)
    for (var col = 0; col < 3; col++)
    {
        c.Cells.Add(r == 0
            ? new TableCellData { Row = r, Col = col, Text = $"H{r}{col}" }
            : new TableCellData
            {
                Row = r, Col = col,
                VariableId = col == 0 ? "v_str" : "v_float",
                Prefix = col == 1 ? "温:" : null,
                Suffix = col == 1 ? "℃" : null,
                DecimalPlaces = col == 2 ? 3 : 1,
            });
    }
    c.VariableBindings.Add(new VariableBindingData
    {
        PropertyName = "Value", VariableId = "v_float", Direction = BindingDirection.ReadOnly,
    });
    return c;
}

/// <summary>类型名 → 稳定短前缀（保证同类型不同变体的 id 唯一且可读）。</summary>
static string Abbrev(HmiControlType type) => type switch
{
    HmiControlType.Screen => "scr",
    HmiControlType.Button => "btn",
    HmiControlType.Label => "lbl",
    HmiControlType.Slider => "sld",
    HmiControlType.Switch => "sw",
    HmiControlType.CheckBox => "cb",
    HmiControlType.Image => "img",
    HmiControlType.Bar => "bar",
    HmiControlType.Dropdown => "dd",
    HmiControlType.TextArea => "ta",
    HmiControlType.Gauge => "gau",
    HmiControlType.Knob => "knb",
    HmiControlType.Keyboard => "kbd",
    HmiControlType.Led => "led",
    HmiControlType.Spinner => "spn",
    HmiControlType.TabView => "tab",
    HmiControlType.Line => "lin",
    HmiControlType.Table => "tbl",
    HmiControlType.Svg => "svg",
    HmiControlType.Tank => "tnk",
    HmiControlType.Pipe => "pip",
    HmiControlType.Valve => "vlv",
    HmiControlType.Rectangle => "rct",
    HmiControlType.Ellipse => "ell",
    HmiControlType.Polyline => "ply",
    HmiControlType.Arc => "arc",
    _ => "unk",
};
