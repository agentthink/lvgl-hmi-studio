using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 控件类生成符号（<c>obj_*</c> / <c>create_screen_*</c> / <c>label_*</c> / <c>_rec_*</c> /
/// <c>_logbuf_*</c> / <c>_chart_*</c> … 共 30 余族）的**净化不变量**守卫。
///
/// <para><b>为什么需要这个守卫</b>：这一族符号由生成器在<b>一处声明、另一处引用</b>，
/// 有的还跨 partial、跨文件、跨编译单元 —— <c>SimulatorProjectExporter</c> 也要拼
/// <c>create_screen_&lt;id&gt;</c>，而它的输入是**从生成代码里正则提取**的名字。
/// 收口前全仓有 116 处直接写 <c>$"前缀_{x.Id}"</c>，等于把"声明与引用必须逐字符相同"
/// 交给 116 个调用点各自维持。实测已知踩到的：
/// <list type="bullet">
///   <item><c>_knobtri_</c> / <c>_curtri_</c>：创建侧裸拼、声明与刷新侧走净化；</item>
///   <item><c>_cur_</c>：创建侧写 <c>lv_obj_t* _cur_x = …</c>，<b>遮蔽</b>了同名文件级 static，
///   于是 static 恒为 NULL，而 <c>sim_refresh_bindings</c> 里对它做无保护 <c>lv_obj_align</c>。</item>
/// </list></para>
///
/// <para><b>断言为什么这样写</b>：不去逐个枚举符号族（那样新家族必然漏），而是断一条
/// **族无关**的不变量 —— <b>产物里不得出现「标识符字符 + 原始 id」的粘连</b>。
/// 原始 id 含 <c>-</c> / 空格时，<c>obj_我的-控件</c> 这类非法拼接必然被它抓住，
/// 而字符串字面量里的 id（前面是 <c>"</c>）与注释里的 id（前面是空格）不会误报。
/// 语料必须真的经过每个发射器，所以另配一组正向断言（见
/// <see cref="SanitizedNames_ReachEveryEmitterFamily"/>）—— <b>守卫的上限由语料决定</b>。</para>
/// </summary>
public class ControlSymbolSanitizationTests
{
    // ── 语料：id 全部含需净化字符（`-` / 空格），净化后 = 把这两类字符换成 `_` ──

    private const string ScreenId = "scr-main 1";      // 启动屏 → create_screen_scr_main_1
    private const string LayerId = "scr-hdr 0";        // 被引用为图层的屏
    private const string OtherId = "scr-aux 2";        // 普通屏（用于让"启动屏重排"可观测）

    /// <summary>产物里绝不允许出现「标识符字符 + 原始 id」的粘连。</summary>
    [Fact]
    public void IllegalIds_NeverLeakIntoIdentifierPosition()
    {
        foreach (var code in AllGeneratedCode())
        {
            var offenders = new List<string>();
            foreach (var raw in AllRawIds)
            {
                // 前一个字符是标识符字符 ⇒ 这个 id 被拼进了某个符号名里
                foreach (Match m in Regex.Matches(code, @"[A-Za-z0-9_]" + Regex.Escape(raw)))
                    offenders.Add($"‑{raw} @ {m.Index}：…{Excerpt(code, m.Index)}…");
            }

            Assert.True(offenders.Count == 0,
                "产物里出现了未净化的控件/屏幕/报警/记录器 id（同一符号名有两种构造方式）：" +
                Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", offenders.Take(12)));
        }
    }

    /// <summary>
    /// **变量遮蔽**守卫：产物里不得出现「文件级 static 声明了 X」+「函数内又用类型前缀声明 X」。
    ///
    /// <para>这条是实测踩出来的：<c>ControlParameters</c> 里 Bar 游标写
    /// <c>lv_obj_t* _cur_x = lv_obj_create(...)</c>，而同名文件级 static 由
    /// <c>GeneratePointerDeclarations</c> 声明（<c>static lv_obj_t* _cur_x = NULL;</c>）——
    /// 局部把 static **遮蔽**了，static 恒为 NULL，而 <c>sim_refresh_bindings</c> 对它做
    /// <b>无空指针保护</b>的 <c>lv_obj_align</c>。修法是把局部声明改成赋值（去掉类型前缀）。</para>
    ///
    /// <para>注意这条与 id 是否合法无关，所以"非法 id 不泄漏"那条守卫抓不到它 ——
    /// 「一组破坏只能证明它打算证明的那件事」，守不同失效模式必须配不同的断言。</para>
    /// </summary>
    [Fact]
    public void FileScopeStatics_AreNeverShadowedByIndentedLocals()
    {
        // 文件级 static 声明里的标识符（static lv_obj_t* x; / static volatile uint16_t y = 0; / static T z[N];）
        var fileStatic = new Regex(@"^static\s+[\w\s\*]*?\b([A-Za-z_]\w*)\s*(?:\[|=|;)",
            RegexOptions.Multiline | RegexOptions.Compiled);
        // 函数内**带类型前缀**的局部指针声明（缩进开头）
        var localPtr = new Regex(@"^\s+lv_obj_t\*\s+([A-Za-z_]\w*)\s*=",
            RegexOptions.Multiline | RegexOptions.Compiled);

        foreach (var code in AllGeneratedCode())
        {
            var statics = fileStatic.Matches(code).Select(m => m.Groups[1].Value).ToHashSet();
            var shadowed = localPtr.Matches(code).Select(m => m.Groups[1].Value)
                .Where(statics.Contains).Distinct().ToList();

            Assert.True(shadowed.Count == 0,
                "文件级 static 被函数内同名局部指针遮蔽（static 会永远保持初值，跨函数引用拿到 NULL）：" +
                Environment.NewLine + "  " + string.Join(", ", shadowed));
        }
    }

    /// <summary>
    /// **「展开后清单」自洽**守卫：文件级符号的「声明 / 定义侧」与「使用侧」必须面对**同一份控件清单**。
    ///
    /// <para>实测缺陷：<c>create_screen_*</c> 走 <c>expander.ExpandScreen(screen)</c>（展开 Faceplate），
    /// 而 <c>GeneratePointerDeclarations</c> / <c>AppendPointerValueCallbacks</c> /
    /// <c>GenerateRefreshBindings</c> 遍历的是 <c>project.Screens</c> 的**原始**控件 ——
    /// Faceplate 模板含 Slider/Bar + ShowPointer 时，克隆控件会发射
    /// <c>_cur_&lt;cloneId&gt; = lv_obj_create(o);</c> 与
    /// <c>lv_obj_add_event_cb(o, _bar_&lt;cloneId&gt;_val_cb, …)</c>，
    /// 而那个 static 从未声明、那个回调从未定义 → **C 编译错误**（未声明的标识符 / 隐式函数声明），
    /// 且编辑器侧完全无感（只有真去编译产物才暴露）。</para>
    ///
    /// <para>断言刻意「族无关 + 集合级」：不枚举具体符号名，只断
    /// <b>每个赋值点 / 引用点都能在产物里找到同名的声明 / 定义</b>。
    /// 新增任何指针族都会自动被覆盖 —— 而"某个具体符号存在"那种写法会随新家族漏掉。</para>
    /// </summary>
    [Fact]
    public void ExpandedControls_HaveMatchingPointerDeclarationsAndCallbacks()
    {
        // 文件级 static 声明里的标识符
        var fileStatic = new Regex(@"^static\s+[\w\s\*]*?\b([A-Za-z_]\w*)\s*(?:\[|=|;)",
            RegexOptions.Multiline | RegexOptions.Compiled);
        // 指针族赋值点 —— 声明缺失时这里展开成「未声明的标识符」
        var pointerAssign = new Regex(
            @"^\s+(_(?:cur|knobtri|curtri)_[A-Za-z0-9_]+)\s*=\s*lv_(?:obj|canvas)_create\(",
            RegexOptions.Multiline | RegexOptions.Compiled);
        // 值回调的定义 / 引用点
        var callbackDef = new Regex(
            @"^static void (_(?:slider|bar)_[A-Za-z0-9_]+_val_cb)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);
        var callbackRef = new Regex(
            @"lv_obj_add_event_cb\([^,]+,\s*(_(?:slider|bar)_[A-Za-z0-9_]+_val_cb)\s*,",
            RegexOptions.Multiline | RegexOptions.Compiled);

        foreach (var code in AllGeneratedCode())
        {
            var statics = fileStatic.Matches(code).Select(m => m.Groups[1].Value).ToHashSet();
            var undeclared = pointerAssign.Matches(code).Select(m => m.Groups[1].Value)
                .Distinct().Where(n => !statics.Contains(n)).ToList();
            Assert.True(undeclared.Count == 0,
                "产物里有指针符号被赋值却**没有文件级 static 声明**（C 编译错误：未声明的标识符）—— " +
                "声明侧没有遍历 Faceplate 展开后的控件清单："
                + Environment.NewLine + "  " + string.Join(", ", undeclared));

            var defined = callbackDef.Matches(code).Select(m => m.Groups[1].Value).ToHashSet();
            var undefined = callbackRef.Matches(code).Select(m => m.Groups[1].Value)
                .Distinct().Where(n => !defined.Contains(n)).ToList();
            Assert.True(undefined.Count == 0,
                "产物里有值回调被引用却**没有同名函数定义**（C 编译错误：隐式函数声明）—— " +
                "定义侧没有遍历 Faceplate 展开后的控件清单："
                + Environment.NewLine + "  " + string.Join(", ", undefined));
        }
    }

    /// <summary>
    /// 语料必须真的走到每个发射器 —— 逐个断言净化后的名字**确实出现**。
    /// 没有这组断言，"不出现非法符号"可以因为"根本没生成"而平凡成立（假绿）。
    /// </summary>
    [Fact]
    public void SanitizedNames_ReachEveryEmitterFamily()
    {
        var simulator = GenerateSimulator();
        var shared = new CodeGenerator().GenerateSharedLibraryProject(BuildProject());
        // `screen_*` / `layer_*` 只在 GenerateFullProject 的 main() 里发射 —— 漏了这条链，
        // 恰好会漏掉本轮修掉的 `screen_{bootScreen.Id}` 与 `layer_{layerId}` 两处。
        var full = new CodeGenerator().GenerateFullProject(BuildProject());

        // 控件对象 + canvas 缓冲（CodeGenerator.Screen.cs / ProcessControlCodeGen / ShapeControlCodeGen / SvgToLvglConverter）
        foreach (var t in new[]
                 {
                     "obj_lbl_1", "obj_btn_2", "obj_bar_3", "obj_sld_4", "obj_gau_5", "obj_lin_6",
                     "obj_tnk_7", "obj_pmp_8", "obj_vlv_9", "obj_elp_10", "obj_svg_11",
                     "_buf_tnk_7", "_buf_pmp_8", "_buf_vlv_9", "_shapebuf_elp_10", "buf_svg_11",
                 })
        {
            CSource.AssertText(simulator, t);
        }

        // 函数内局部族（Screen / VisualStates / SliderBarIndicators / ControlParameters）
        foreach (var t in new[]
                 {
                     "label_btn_2",          // Button 的子 Label
                     "_lbl_btn_2",           // Button 非居中时的 9 宫格对齐
                     "_h_lbl_1",             // Label 垂直对齐
                     "_pts_lin_6",           // Line 顶点数组
                     "_tick_bar_3_0", "_ticklbl_bar_3_0",   // Bar 刻度 + 数值标签
                     "_knobtri_sld_4",       // Slider knob 三角
                     "_cur_bar_3", "_curtri_bar_3",         // Bar 游标 + 三角
                     "_gauge_lbl_gau_5",     // Gauge 中心数值
                 })
        {
            CSource.AssertText(simulator, t);
        }

        // Faceplate 展开后的**克隆控件**族 —— 实例 id "fp-1" 净化为 fp_1，模板内序号 0=Bar、1=Slider。
        // 这组是「守卫的上限由语料决定」那条教训的落地：没有它，
        // ExpandedControls_HaveMatchingPointerDeclarationsAndCallbacks 可以因为"语料里根本没有
        // Faceplate"而平凡全绿。
        foreach (var t in new[]
                 {
                     "_cur_fp_1_0", "_curtri_fp_1_0", "_bar_fp_1_0_val_cb",  // 克隆 Bar：游标 + 三角 + 值回调
                     "_knobtri_fp_1_1", "_slider_fp_1_1_val_cb",            // 克隆 Slider：knob 三角 + 值回调
                 })
        {
            CSource.AssertText(simulator, t);
        }

        // 克隆控件的**绑定刷新**（GenerateRefreshBindings 也必须走展开后的清单，
        // 否则克隆控件的绑定静默不刷新 —— 不是编译错误，但功能缺失）
        var refreshBody = simulator[simulator.IndexOf("void sim_refresh_bindings", StringComparison.Ordinal)..];
        CSource.AssertText(refreshBody, "obj_fp_1_0");

        // 屏幕 / 图层 / 自适应布局（跨函数、跨文件）
        foreach (var t in new[]
                 {
                     CIdentifier.ScreenFactoryInner(ScreenId),
                     CIdentifier.ScreenFactoryInner(LayerId),
                     CIdentifier.ScreenFactory(ScreenId),
                     CIdentifier.AdaptiveLayoutFn(ScreenId),
                 })
        {
            CSource.AssertText(simulator, t);
        }

        foreach (var t in new[] { CIdentifier.ScreenObject(ScreenId), CIdentifier.LayerObject(LayerId) })
        {
            CSource.AssertText(full, t);
        }

        // 报警（文件级 static + 回调，跨函数）
        CSource.AssertText(simulator, CIdentifier.AlarmRecord("al-1"));
        CSource.AssertText(simulator, CIdentifier.AlarmCheckFn("al-1"));

        // 记录器 / 趋势：模拟器侧
        foreach (var t in new[]
                 {
                     CIdentifier.LogBuffer("lg-1"), CIdentifier.LogWriteIndex("lg-1"),
                     CIdentifier.LogCount("lg-1"), CIdentifier.LogFullFlag("lg-1"),
                     CIdentifier.DataLogInitFn("lg-1"), CIdentifier.DataLogSampleFn("lg-1"),
                     CIdentifier.ChartObject("tr-1"), CIdentifier.ChartSeries("tr-1"),
                     CIdentifier.TrendInitFn("tr-1"), CIdentifier.TrendUpdateFn("tr-1"),
                 })
        {
            CSource.AssertText(simulator, t);
        }

        // 记录器 CSV 导出：**只在 .so 模式**发射（Trend 定义 + SharedLibrary 分派）
        CSource.AssertText(shared, CIdentifier.DataLogExportCsvFn("lg-1"));
    }

    /// <summary>
    /// 跨模块契约：导出器把**正则提取**到的工厂名与启动屏 id 比对后重排，
    /// 两边必须出自同一个构造器 —— 否则 id 含非法字符时重排静默失效，
    /// <c>screens[0]</c> 不是启动屏（窗口尺寸 / 首屏都跟着错）。
    /// </summary>
    [Fact]
    public void Exporter_PlacesBootScreenFirst_EvenWithIllegalIdCharacters()
    {
        var project = BuildProject();
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);

        var extracted = SimulatorProjectExporter.ExtractScreenFunctions(code);
        Assert.True(extracted.Contains(CIdentifier.ScreenFactory(ScreenId)),
            $"从生成代码提取的屏幕工厂里没有启动屏（提取到：[{string.Join(", ", extracted)}]）");

        var dir = TestTempDir.NewDir("sym-guard");
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir);

            Assert.Equal(CIdentifier.ScreenFactory(ScreenId), result.ScreenFunctions[0]);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ───────────────────────── fixture ─────────────────────────

    /// <summary>全部含非法字符的原始 id（断言"不得粘连"时的搜索集）。</summary>
    private static readonly string[] AllRawIds =
    {
        "lbl-1", "btn 2", "bar-3", "sld 4", "gau-5", "lin 6", "tnk-7", "pmp 8", "vlv-9", "elp 10", "svg 11",
        ScreenId, LayerId, OtherId, "al-1", "lg-1", "tr-1", "fp-1",
    };

    /// <summary>三代生成入口的产物（模拟器模块 / .so / 完整工程 main）。</summary>
    private static IEnumerable<string> AllGeneratedCode()
    {
        var project = BuildProject();
        yield return new CodeGenerator().GenerateSimulatorModule(project, null);
        yield return new CodeGenerator().GenerateSharedLibraryProject(project);
        yield return new CodeGenerator().GenerateFullProject(project);
    }

    private static string GenerateSimulator() => new CodeGenerator().GenerateSimulatorModule(BuildProject(), null);

    private static string Excerpt(string code, int index)
    {
        var start = Math.Max(0, index - 30);
        var end = Math.Min(code.Length, index + 30);
        return code[start..end].Replace('\n', ' ').Replace('\r', ' ');
    }

    private static HmiProject BuildProject()
    {
        var project = new HmiProject { Name = "SymbolSanitization", LvglVersion = "9.5" };
        project.RuntimeConfig.BootScreenId = ScreenId;

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "温度", Address = "40001", DataType = "Float", ConnectionId = "c1",
        });

        // 三块屏，启动屏**不排第一** —— 否则"按 BootScreenId 重排"的断言会平凡成立。
        project.Screens.Add(new HmiScreen { Id = LayerId, Name = "公共页眉", Width = 1024, Height = 40 });
        project.Screens.Add(new HmiScreen { Id = OtherId, Name = "辅助屏", Width = 1024, Height = 600 });

        var main = new HmiScreen
        {
            Id = ScreenId, Name = "主画面", Width = 1024, Height = 600,
            AdaptiveMode = ScreenAdaptiveMode.Fit,
        };
        main.ReferencedLayerIds.Add(LayerId);
        foreach (var (id, type) in new (string, HmiControlType)[]
                 {
                     ("lbl-1", HmiControlType.Label),
                     ("btn 2", HmiControlType.Button),
                     ("bar-3", HmiControlType.Bar),
                     ("sld 4", HmiControlType.Slider),
                     ("gau-5", HmiControlType.Gauge),
                     ("lin 6", HmiControlType.Line),
                     ("tnk-7", HmiControlType.Tank),
                     ("pmp 8", HmiControlType.Pipe),
                     ("vlv-9", HmiControlType.Valve),
                     ("elp 10", HmiControlType.Ellipse),
                     ("svg 11", HmiControlType.Svg),
                 })
        {
            main.Controls.Add(MakeControl(id, type));
        }
        project.Screens.Add(main);

        // 报警：id 直接进 `_rec_*` / `check_alarm_*`
        var group = new AlarmGroup { Id = "ag-1", Name = "分组" };
        group.Alarms.Add(new AlarmConfig
        {
            Id = "al-1", Name = "高温", Severity = AlarmSeverity.Warning, IsEnabled = true,
            Condition = new AlarmCondition { VariableId = "v1", Type = AlarmConditionType.HighLimit, Threshold = 80 },
        });
        project.AlarmGroups.Add(group);

        // 记录器 + 趋势
        project.DataLogConfigs.Add(new DataLogConfig
        {
            Id = "lg-1", SampleIntervalMs = 1000, MaxRecords = 256, IsEnabled = true,
            VariableIds = { "v1" },
        });
        project.TrendConfigs.Add(new TrendConfig
        {
            Id = "tr-1", Name = "温度趋势", MaxPoints = 120, IsEnabled = true,
            Pens = { new TrendPen { Label = "温度", VariableId = "v1", Color = "#38bdf8" } },
        });

        // ── Faceplate 语料 ──
        // 模板含 Bar + Slider（都带 ShowPointer），实例 id 含 `-`。
        // 展开后是**克隆控件**（id = 净化的实例 id + 模板内序号 = fp_1_0 / fp_1_1），而
        //   · `_cur_*` / `_knobtri_*` / `_curtri_*` 的文件级 static 声明在 GeneratePointerDeclarations，
        //   · `_bar_*_val_cb` / `_slider_*_val_cb` 的定义在 AppendPointerValueCallbacks，
        //   · 绑定刷新语句在 GenerateRefreshBindings，
        // 三处若仍遍历**原始** project.Screens 控件列表（而 create_screen_* 遍历展开后的列表），
        // 克隆控件就会「被发射却无声明/无定义」→ C 编译错误（未声明标识符 / 隐式函数声明）。
        // 语料不含它，上述三处**永远不会被经过**，不变量照样绿 —— 守卫的上限由语料决定。
        var template = new ControlTemplate { Id = "tpl-1", Name = "阀组面板", Version = 1 };
        foreach (var (tplControlId, tplType) in new[]
                 {
                     ("t_bar", HmiControlType.Bar),
                     ("t_sld", HmiControlType.Slider),
                 })
        {
            var tplControl = MakeControl(tplControlId, tplType);
            // 模板内部控件通过参数占位符引用变量（实例再绑定实参 → 替换成 v1）
            foreach (var b in tplControl.VariableBindings) b.VariableId = "{{Parameters.Var}}";
            template.Controls.Add(tplControl);
        }
        template.Parameters.Add(new TemplateParameter
        {
            Name = "Var", DisplayName = "变量", Type = ParameterType.VariableId, DefaultValue = "v1",
        });
        project.Templates.Add(template);

        var faceplate = new HmiControl
        {
            Id = "fp-1", Type = HmiControlType.Bar, Name = "阀组",
            X = 20, Y = 20, Width = 200, Height = 40,
            TemplateId = "tpl-1", TemplateVersion = 1,
        };
        faceplate.ParameterBindings.Add(new ParameterBinding { ParameterName = "Var", VariableId = "v1" });
        main.Controls.Add(faceplate);

        return project;
    }

    private static HmiControl MakeControl(string id, HmiControlType type) => new()
    {
        Id = id,
        Type = type,
        Name = id,
        X = 10, Y = 10, Width = 160, Height = 40,
        Text = type is HmiControlType.Button or HmiControlType.Label or HmiControlType.Gauge ? "文本" : null,
        MinValue = 0, MaxValue = 100, StepValue = 10, Value = 40,
        ShowTicks = type is HmiControlType.Slider or HmiControlType.Bar,
        MajorTickCount = 4,
        ShowPointer = type is HmiControlType.Slider or HmiControlType.Bar ? true : null,
        // 非居中 → 触发 `_lbl_*`（Button 的 9 宫格对齐）与 `_h_*`（Label 垂直对齐）
        TextAlign = TextAlignMode.Left,
        TextVAlign = TextVAlignMode.Top,
        Points = type is HmiControlType.Line
            ? new List<PointData> { new(0, 0), new(160, 40) }
            : null,
        VariableBindings = type is HmiControlType.Slider or HmiControlType.Bar
            ? new ObservableCollection<VariableBindingData>
            {
                new() { VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value" },
            }
            : new ObservableCollection<VariableBindingData>(),
    };
}
