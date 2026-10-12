using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **控件发射器 → 判据点**（阶段 204）—— 把"这些发射路径有判据看着"从**印象**变成**可核对的指针**。
///
/// <para><b>为什么需要它（阶段 203 的教训）</b>：那一阶段试过两条机械代理，**两条都不可靠** ——
/// ① 按**字段名** grep 生成器 ⇒ 漏掉间接消费（`Rotation`/`RotationDirection` 经 `EffectiveRotation()`）；
/// ② 按 **LVGL API 名** grep 测试 ⇒ "某个 API 名 0 命中"**不能**推出"那一族零判据"
/// （`lv_canvas_draw_text` 没人断言，但 Tank/Pipe/Valve 的绘制**有**判据，只是断言的是别的名字）。
/// ⇒ 本文的做法换一层：**在发射器粒度上指名判据**，并且要求那条判据里**真的存在**一段断言原文
/// （`Snippet`）—— 于是"只有一行注释的发射器"（`#93` 的 Keyboard）**不可能**通过。</para>
///
/// <para><b>三道核对</b>：① 表 ↔ `CodeGenerator.ControlParameters.cs` 里**实际定义**的
/// <c>Emit*Parameters</c> **双向**闭合；② 每条 <c>Snippet</c> 必须在指名的判据文件里**逐字存在**；
/// ③ 面板写回的 **58 个** `HmiControl` 字段，**第一个真实读取点**所在的发射器必须落在
/// <see cref="Emitters"/> 或 <see cref="SharedConsumers"/> 里 —— 新加一个读取点（新文件/新方法）就会红，
/// 逼着补登记（而不是让"没人看着"悄悄出现）。</para>
/// </summary>
public class ControlEmitterJudgePointersTests
{
    /// <summary>一条发射器指针：方法 → 它负责的字段（人读）→ 判据文件 → 判据里**逐字存在**的断言片段。</summary>
    private sealed record Emitter(string Method, string Fields, string JudgeFile, string Snippet);

    /// <summary>共用的消费点（不在类型分发表里，被多个发射器间接用到）。</summary>
    private sealed record SharedConsumer(string Method, string Where, string JudgeFile, string Snippet);

    private const string EmittersFile = "src/HmiEditor.CodeGen/CodeGenerator.ControlParameters.cs";
    private const string TestDir = "tests/HmiEditor.Core.Tests";

    private static readonly Emitter[] Emitters =
    {
        new("EmitSliderParameters", "MinValue/MaxValue/Value/ValueMode/ShowPointer/Radius",
            "ControlEmitterCoverageTests.cs", "lv_slider_set_range(obj_sl1, 10, 200);"),
        new("EmitBarParameters", "MinValue/MaxValue/Value/ValueMode/ShowPointer",
            "ControlEmitterCoverageTests.cs", "lv_bar_set_range(obj_b1, 0, 100);"),
        new("EmitDropdownParameters", "Options/SelectedIndex",
            "ControlEmitterCoverageTests.cs", "lv_dropdown_set_selected(obj_dd1, 3);"),
        new("EmitTextAreaParameters", "PlaceholderText/MaxLength/PasswordMode/KeyboardMode",
            "ControlEmitterCoverageTests.cs", "lv_textarea_set_password_mode(obj_ta1, true);"),
        new("EmitGaugeParameters", "MinValue/MaxValue/Value/MajorTickCount/BorderWidth/IndicatorColor",
            "ControlEmitterCoverageTests.cs", "// Gauge 主刻度: 6 段"),
        new("EmitLedParameters", "Brightness/BgColor",
            "ControlEmitterCoverageTests.cs", "lv_led_set_brightness(obj_l1, 7);"),
        new("EmitSpinnerParameters", "SpinSpeed",
            "ControlEmitterCoverageTests.cs", "lv_spinner_set_anim_params(obj_sp1, 1000, 60);"),
        new("EmitTableParameters", "RowCount/ColumnCount/ColumnWidths/RowHeight/HeaderTexts",
            "ControlEmitterCoverageTests.cs", "lv_table_set_column_width(obj_tb1, 1, LV_COORD_MAX);"),
        new("EmitTabViewParameters", "TabTitles/SelectedIndex",
            "ControlEmitterCoverageTests.cs", "lv_tabview_set_active(obj_tv1, 2, LV_ANIM_OFF);"),
        new("EmitLabelParameters", "LabelLongMode",
            "ControlEmitterCoverageTests.cs", "lv_label_set_long_mode(obj_l1, "),
        new("EmitKeyboardParameters", "KeyboardMode（#93 修的：此前只发注释）",
            "ControlEmitterCoverageTests.cs", "lv_keyboard_set_mode(obj_kb1, "),
        new("EmitImageParameters", "StretchMode",
            "ControlEmitterCoverageTests.cs", "lv_image_set_inner_align(obj_i1, LV_IMAGE_ALIGN_CONTAIN);"),
        new("EmitLineParameters", "LineCap/BgColor/BorderWidth/Width/Height",
            "ControlEmitterCoverageTests.cs", "lv_obj_set_style_line_rounded(obj_ln1, true, 0);"),
        new("EmitPolylineParameters", "Points/LineCap/BgColor/BorderWidth",
            "ControlEmitterCoverageTests.cs", "lv_line_set_points(obj_pl1, _pts_pl1, 3);"),
        new("EmitArcParameters", "MinValue/MaxValue/Value/BorderWidth/IndicatorColor",
            "ControlEmitterCoverageTests.cs", "lv_arc_set_bg_angles(obj_a1, 0, 270);"),
        new("WriteStyleCalls", "X/Y/Width/Height/BgColor/TextColor/BorderColor/BorderWidth/Radius",
            "CodeGenTests.cs", "lv_obj_set_style_bg_color(obj_c10, lv_color_hex(0x334155), 0)"),
        new("WriteTextCall", "Text",
            "CodeGenTests.cs", "lv_label_set_text(obj_c1, "),
        new("AppendTransform", "Rotation/RotationDirection/MirrorX/MirrorY",
            "TransformFeatureTests.cs", "lv_obj_set_style_transform_scale_x(obj_m1, -256, 0);"),
        new("GenerateVisualStates", "VisualStates（状态条目 7 字段）",
            "VisualStateCodeGenConsumptionTests.cs", "// State vertical align: Bottom"),
    };

    /// <summary>
    /// 共用的消费点：面板字段的"第一个读取点"落在这些方法里（而不是某个 <c>Emit*Parameters</c>）。
    /// 每条都要指名一个**存在**的判据文件 —— 新冒出一个消费点会让核对 ③ 红，逼着在这里补一行。
    /// </summary>
    private static readonly SharedConsumer[] SharedConsumers =
    {
        new("SafeClampPosition", "src/HmiEditor.CodeGen/CodeGenerator.Screen.cs",
            "ScreenSafeAreaExportTests.cs", "// 安全区吸附（设计空间）：(5, 5) → (10, 20)"),
        new("BuiltInFontSizeFor", "src/HmiEditor.CodeGen/CodeGenerator.Mappings.cs",
            "TextEmissionFaceTests.cs", "#if LV_FONT_MONTSERRAT_16"),
        new("TableColumnWidthExpr", "src/HmiEditor.CodeGen/CodeGenerator.Mappings.cs",
            "ControlEmitterCoverageTests.cs", "lv_table_set_column_width(obj_tb1, 1, LV_COORD_MAX);"),
        new("GetControlFontExpression", "src/HmiEditor.CodeGen/CodeGenerator.Mappings.cs",
            "ControlEmitterCoverageTests.cs", "lv_obj_set_style_text_font("),
        new("GenerateScreenCode", "src/HmiEditor.CodeGen/CodeGenerator.Screen.cs",
            "ControlEmitterCoverageTests.cs", "lv_obj_create(obj_"),
        new("GenerateTankCode", "src/HmiEditor.CodeGen/ProcessControlCodeGen.cs",
            "ProcessControlCodeGenTests.cs", "lv_draw_rect"),
        new("GeneratePipeCode", "src/HmiEditor.CodeGen/ProcessControlCodeGen.cs",
            "ProcessControlCodeGenTests.cs", "流动动画周期: 2000ms"),
        new("GenerateValveCode", "src/HmiEditor.CodeGen/ProcessControlCodeGen.cs",
            "ProcessControlCodeGenTests.cs", "lv_draw_arc"),
        new("GeneratePointerDeclarations", "src/HmiEditor.CodeGen/CodeGenerator.SliderBarIndicators.cs",
            "SliderBarFeatureCodeGenTests.cs", "lv_obj_create(obj_"),
        new("AppendSliderTicks", "src/HmiEditor.CodeGen/CodeGenerator.SliderBarIndicators.cs",
            "SliderBarFeatureCodeGenTests.cs", "lv_label"),
        new("SliderTickCount", "src/HmiEditor.CodeGen/CodeGenerator.SliderBarIndicators.cs",
            "SliderBarFeatureCodeGenTests.cs", "lv_label"),
        new("GenerateControlParameters", "src/HmiEditor.CodeGen/CodeGenerator.ControlParameters.cs（公共尾：不透明度/隐藏/指示器色）",
            "ControlEmitterCoverageTests.cs", "lv_obj_set_style_opa(obj_l1,"),
        new("GenerateActionCallbacks", "src/HmiEditor.CodeGen/CodeGenerator.Actions.cs",
            "ActionCallbacksCodeGenTests.cs", "lv_obj_add_event_cb"),
    };

    /// <summary>
    /// **间接消费**登记：生成器里**只出现方法名、不出现字段名**的那些字段
    /// （阶段 203 的教训：按字段名 grep 生成器会把它们误判成"没人读"）。
    /// 三条自检：① 该字段确实是面板写回的；② 判据文件里真有一句断言；③ 生成器里**确实没有**
    /// 对该字段的直接读取（一旦有了，就该挪进 <see cref="Emitters"/> 那张表）。
    /// </summary>
    private sealed record Indirect(string Field, string Via, string JudgeFile, string Snippet);

    private static readonly Indirect[] IndirectFields =
    {
        new("Rotation", "HmiControl.EffectiveRotation()（生成器写的是 `control.EffectiveRotation()`，字段名不出现）",
            "TransformFeatureTests.cs", "lv_obj_set_style_transform_rotation(obj_r1, 900, 0);"),
        new("RotationDirection", "HmiControl.EffectiveRotation()（同上；方向只体现在角度符号）",
            "TransformFeatureTests.cs", "lv_obj_set_style_transform_rotation(obj_r1, -900, 0);"),
    };

    /// <summary>面板**不写**、但同一个发射器会读的模型字段（登记表里出现它们是合理的）。</summary>
    private static readonly HashSet<string> NonPanelFields = new(StringComparer.Ordinal)
    {
        "Value", "Points", "Cells", "IsVisible", "Type", "Id", "VisualStates",
    };

    private static string Root => SourceScanner.RepoRoot;

    private static string ReadRepo(string relative) =>
        File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    // ── 面板写回的字段（唯一事实源：PropertyPanelView 的 ValidateAndWrite(nameof(HmiControl.X), …)）──

    private static HashSet<string> PanelFields()
    {
        var dir = Path.Combine(Root, "src/HmiEditor.App/Views");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(dir, "PropertyPanelView*.cs"))
        {
            var code = SourceScanner.StripComments(File.ReadAllText(file));
            foreach (Match m in Regex.Matches(code, @"nameof\(HmiControl\.(\w+)\)"))
                names.Add(m.Groups[1].Value);
        }

        return names;
    }

    /// <summary>字段 → 它在 `HmiEditor.CodeGen` 里**第一个真实读取点**所在的方法名。</summary>
    private static Dictionary<string, string> FirstReaderMethods(IEnumerable<string> fields)
    {
        var genDir = Path.Combine(Root, "src/HmiEditor.CodeGen");
        var files = Directory.GetFiles(genDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        var methodPattern = new Regex(@"\n\s*(?:public|private|protected|internal|static)[^\n(]*?\s(\w+)\s*\(");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var s = lines[i].TrimStart();
                    if (s.StartsWith("//", StringComparison.Ordinal) || s.StartsWith("*", StringComparison.Ordinal))
                        continue;
                    if (!Regex.IsMatch(lines[i], $@"\b(?:control|c)\.{Regex.Escape(field)}\b"))
                        continue;

                    var head = string.Join("\n", lines.Take(i));
                    var matches = methodPattern.Matches(head);
                    result[field] = matches.Count > 0 ? matches[^1].Groups[1].Value : "(顶层)";
                    break;
                }

                if (result.ContainsKey(field)) break;
            }
        }

        return result;
    }

    [Fact]
    public void EveryEmitterMethod_IsInTheTable_AndViceVersa()
    {
        var code = SourceScanner.StripComments(ReadRepo(EmittersFile));

        var defined = Regex.Matches(code, @"private\s+(?:static\s+)?void\s+(Emit\w+Parameters)\s*\(")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var tabulated = Emitters.Select(e => e.Method)
            .Where(m => m.StartsWith("Emit", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(defined.Count >= 15, $"只找到 {defined.Count} 个 Emit*Parameters 方法 —— 扫描面塌了（阶段 204 实测 15 个）");

        var missing = defined.Except(tabulated).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var ghosts = tabulated.Except(defined).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "这些发射器没有登记判据点（新加的发射器必须点名一条真判据）：" + string.Join(", ", missing));
        Assert.True(ghosts.Length == 0,
            "登记表里这些发射器已经不存在了（改名/删除后要同步）：" + string.Join(", ", ghosts));
    }

    [Fact]
    public void EveryPointer_PointsAtAnExistingSnippet()
    {
        var problems = new List<string>();

        foreach (var e in Emitters)
        {
            var path = Path.Combine(Root, TestDir, e.JudgeFile);
            if (!File.Exists(path))
            {
                problems.Add($"{e.Method}：判据文件不存在 {e.JudgeFile}");
                continue;
            }

            var text = File.ReadAllText(path);

            // 判据点必须**逐字存在**，而且必须"像一句断言"（太短的指针等于没指）
            if (!text.Contains(e.Snippet, StringComparison.Ordinal))
                problems.Add($"{e.Method}：{e.JudgeFile} 里找不到断言片段 `{e.Snippet}`");
            else if (e.Snippet.Length < 12)
                problems.Add($"{e.Method}：断言片段太短（{e.Snippet.Length} 字符），指不出什么");
        }

        Assert.True(problems.Count == 0,
            "发射器判据点核对不过：\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void EveryPanelField_IsConsumedByATabulatedEmitter()
    {
        var panel = PanelFields();
        Assert.True(panel.Count >= 55,
            $"只扫到 {panel.Count} 个面板字段 —— 扫描面塌了（阶段 204 实测 58 个）");

        var readers = FirstReaderMethods(panel);
        var known = Emitters.Select(e => e.Method)
            .Concat(SharedConsumers.Select(s => s.Method))
            .ToHashSet(StringComparer.Ordinal);

        // 间接消费：字段确实是面板写回的、判据真存在、而且生成器里**确实没有**直接读取
        var indirectProblems = new List<string>();
        foreach (var ind in IndirectFields)
        {
            if (!panel.Contains(ind.Field))
                indirectProblems.Add($"{ind.Field}：不在面板写回的字段里（登记过时？）");
            if (readers.ContainsKey(ind.Field))
                indirectProblems.Add($"{ind.Field}：生成器里**已经有**直接读取了（应挪进 Emitters 表，而不是留在间接登记里）");

            var judgePath = Path.Combine(Root, TestDir, ind.JudgeFile);
            if (!File.Exists(judgePath) || !File.ReadAllText(judgePath).Contains(ind.Snippet, StringComparison.Ordinal))
                indirectProblems.Add($"{ind.Field}：{ind.JudgeFile} 里找不到断言片段 `{ind.Snippet}`");
        }

        Assert.True(indirectProblems.Count == 0,
            "间接消费登记核对不过：\n  " + string.Join("\n  ", indirectProblems));

        var indirectNames = IndirectFields.Select(i => i.Field).ToHashSet(StringComparer.Ordinal);

        var orphan = panel
            .Where(f => !indirectNames.Contains(f))
            .Where(f => !readers.TryGetValue(f, out var m) || !known.Contains(m))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => $"{f} → {(readers.TryGetValue(f, out var m) ? m : "(CodeGen 里没有直接读取)")}")
            .ToArray();

        Assert.True(orphan.Length == 0,
            "这些面板字段的**第一个读取点**落在一个没登记的发射器/消费点里 —— "
            + "要么补进 Emitters，要么补进 SharedConsumers（并给一条真判据）：\n  "
            + string.Join("\n  ", orphan));
    }

    [Fact]
    public void SharedConsumers_AreReal_AndTheirFieldsArePanelWritten()
    {
        var panel = PanelFields();
        var allFields = Emitters.SelectMany(e => e.Fields.Split('/', ',', '（', '）'))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && Regex.IsMatch(s, @"^\w+$"))
            .ToHashSet(StringComparer.Ordinal);

        // 表里提到的字段：要么是面板写回的，要么在显式登记的"面板不写"名单里
        var unknown = allFields.Except(panel).Except(NonPanelFields)
            .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        Assert.True(unknown.Length == 0,
            "登记表里提到了既不是面板写回、也不在 NonPanelFields 名单里的字段：" + string.Join(", ", unknown));

        // 共用消费点：方法必须真存在（源文件里能找到定义），判据文件必须真存在
        var problems = new List<string>();
        foreach (var s in SharedConsumers)
        {
            var path = Path.Combine(Root, s.Where.Split('（')[0].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                problems.Add($"{s.Method}：源文件不存在 {s.Where}");
                continue;
            }

            if (!Regex.IsMatch(File.ReadAllText(path), $@"\b{Regex.Escape(s.Method)}\s*\("))
                problems.Add($"{s.Method}：{Path.GetFileName(path)} 里找不到这个方法的定义/调用");

            var judgePath = Path.Combine(Root, TestDir, s.JudgeFile);
            if (!File.Exists(judgePath))
            {
                problems.Add($"{s.Method}：判据文件不存在 {s.JudgeFile}");
            }
            else if (!File.ReadAllText(judgePath).Contains(s.Snippet, StringComparison.Ordinal))
            {
                problems.Add($"{s.Method}：{s.JudgeFile} 里找不到断言片段 `{s.Snippet}`");
            }
        }

        Assert.True(problems.Count == 0,
            "共用消费点核对不过：\n  " + string.Join("\n  ", problems));
    }


    // ────────────────────────────────────────────────────────────────
    // 逐**字段**索引（阶段 211）：58 个面板字段 → 消费点 → 判据文件
    // ────────────────────────────────────────────────────────────────

    /// <summary>逐字段索引的一行（与文档里的表格一一对应）。</summary>
    private sealed record FieldRow(string Field, string Consumer, string JudgeFile);

    /// <summary>
    /// **算出来**的逐字段索引：对每个面板字段取其第一个真实读取点，再按
    /// <see cref="Emitters"/> / <see cref="SharedConsumers"/> / <see cref="IndirectFields"/> 解析出判据文件。
    /// </summary>
    private static List<FieldRow> ComputedFieldIndex()
    {
        var panel = PanelFields().OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var readers = FirstReaderMethods(panel);
        var byMethod = Emitters.ToDictionary(e => e.Method, e => e.JudgeFile, StringComparer.Ordinal);
        var shared = SharedConsumers.ToDictionary(s => s.Method, s => s.JudgeFile, StringComparer.Ordinal);
        var indirect = IndirectFields.ToDictionary(i => i.Field, i => i.JudgeFile, StringComparer.Ordinal);

        var rows = new List<FieldRow>();
        foreach (var f in panel)
        {
            if (indirect.TryGetValue(f, out var viaJudge))
            {
                rows.Add(new FieldRow(f, "(间接) EffectiveRotation()", viaJudge));
                continue;
            }

            if (!readers.TryGetValue(f, out var method))
            {
                rows.Add(new FieldRow(f, "(无直接读取)", "—"));
                continue;
            }

            var judge = byMethod.TryGetValue(method, out var j1) ? j1
                : shared.TryGetValue(method, out var j2) ? j2
                : "(未登记)";
            rows.Add(new FieldRow(f, method, judge));
        }

        return rows;
    }

    private const string IndexDoc = "docs/2026-10-01-control-field-judge-index.md";

    /// <summary>从文档表格里解析出同样的行 —— 形如 `| Field | Consumer | JudgeFile |`。</summary>
    private static List<FieldRow> DocFieldIndex()
    {
        var path = Path.Combine(Root, IndexDoc);
        Assert.True(File.Exists(path), $"逐字段索引文档不存在：{IndexDoc}");

        var rows = new List<FieldRow>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|') || !line.EndsWith('|')) continue;
            // 单元格里允许用反引号包代码（文档更好读）—— 解析时剥掉
            var cells = line.Split('|').Skip(1).SkipLast(1).Select(c => c.Trim().Trim('`')).ToArray();
            if (cells.Length != 3) continue;
            if (cells[0] is "字段" or "---" or ":---") continue;      // 表头 / 分隔行
            if (cells[0].StartsWith("---", StringComparison.Ordinal)) continue;
            rows.Add(new FieldRow(cells[0], cells[1], cells[2]));
        }

        return rows;
    }

    /// <summary>
    /// **逐字段索引必须与文档一致**（阶段 211，专题 §"逐字段索引"）。
    ///
    /// <para>三道：① 每个面板字段都**解析得出判据**（没有 `(未登记)` / `(无直接读取)` 的行）；
    /// ② 文档里的行与算出来的行**双向闭合**（新加一个面板字段、或把某字段的消费点挪走 ⇒ 红）；
    /// ③ 指名的判据文件必须**真实存在**。</para>
    /// </summary>
    [Fact]
    public void ControlFieldJudgeIndex_MatchesTheDoc()
    {
        var computed = ComputedFieldIndex();
        var doc = DocFieldIndex();

        // ① 不许有解析不出来的行
        var orphans = computed.Where(r => r.JudgeFile is "—" or "(未登记)").ToArray();
        Assert.True(orphans.Length == 0,
            $"{orphans.Length} 个面板字段解析不出判据（要么补登记、要么确认它不该被面板写）："
            + string.Join(", ", orphans.Select(r => $"{r.Field}→{r.Consumer}")));

        // ③ 指名的判据文件必须存在
        var missingFiles = computed.Select(r => r.JudgeFile)
            .Distinct(StringComparer.Ordinal)
            .Where(f => !File.Exists(Path.Combine(Root, TestDir, f)))
            .ToArray();
        Assert.True(missingFiles.Length == 0,
            "索引指向了不存在的判据文件：" + string.Join(", ", missingFiles));

        // ② 文档 ↔ 代码 双向闭合
        string Key(FieldRow r) => $"{r.Field} → {r.Consumer} → {r.JudgeFile}";
        var computedKeys = computed.Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var docKeys = doc.Select(Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        var missing = computedKeys.Except(docKeys).ToArray();
        var ghosts = docKeys.Except(computedKeys).ToArray();

        Assert.True(missing.Length == 0,
            $"{missing.Length} 行在文档里没有（改了执行侧就得同步 {IndexDoc}）：\n  "
            + string.Join("\n  ", missing));
        Assert.True(ghosts.Length == 0,
            $"{ghosts.Length} 行在文档里但对不上执行侧（字段改名/消费点搬家/判据换了）：\n  "
            + string.Join("\n  ", ghosts));

        // 面自检：阶段 211 实测 58 行
        Assert.True(computed.Count >= 58,
            $"逐字段索引只剩 {computed.Count} 行 —— 阶段 211 实测 58 行（面板字段扫描面可能塌了）");
    }
}
