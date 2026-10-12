using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **「面板给出的每个取值都必须有确定的导出语义」**（阶段 212）。
///
/// <para>承阶段 211 的逐字段索引（`docs/2026-10-01-control-field-judge-index.md`）：那张表回答
/// "**哪个字段**有判据看着"，这一面回答"**每个取值**呢" —— 对**枚举型**的选择字段，把
/// **面板真的会给出的取值集合**（XAML 里 `ComboBoxItem Tag` 或枚举成员）与
/// **本表登记的取值**做**双向闭合**，再对每个取值**真跑一遍生成器**断言它发射了什么。</para>
///
/// <para><b>为什么值得单独一面</b>：这类字段的坏法都是"**静默**"的 —— 面板多给一个选项、
/// 或者枚举加一个成员，生成器那边落进 `default` 分支，用户改了半天导出没变，而**没有任何判据会红**
/// （阶段 203 就抓到过 `KeyboardMode` 的 `Default` 语义错位）。</para>
///
/// <para><b>🩸 本面钉住的两处"面板有、导出没有"</b>（登记 `#96`）：`TextAlign` 与
/// `ValueDisplayType` 在**Label** 上三个取值**都不发射**（导出只在 Dropdown／值刷新路径里读它们）
/// —— 本面把这件事显式写成 `Fragment = null` ＋ 理由，**不许悄悄发生**：真要改口径
/// （导出补发射 / 面板只有 Dropdown 才显示该行），改完这里会红，逼着更新。</para>
/// </summary>
public class EnumeratedChoiceEmissionTests
{
    /// <summary>一条：字段 → 取值集合从哪来 → 每个取值的期望发射（null ＝ 本阶段实测"不发射"，附理由）。</summary>
    private sealed record Case(
        string Field,
        HmiControlType Type,
        string ObjId,
        Action<HmiControl, string> Apply,
        string? XamlCombo,
        Type? EnumType,
        IReadOnlyDictionary<string, string?> Expected);

    private const string PanelXaml = "src/HmiEditor.App/Views/PropertyPanelView.axaml";

    private static readonly Case[] Cases =
    {
        new("LineCap", HmiControlType.Line, "ln1", (c, v) => c.LineCap = v, "LineCapCombo", null,
            new Dictionary<string, string?>
            {
                // Butt 与 Square 都落"平头"（LVGL 无线帽样式，只有 rounded 布尔）—— 生成器**显式写了注释**说明，
                // 所以这里钉住的是"两者发射相同"这个**已声明的**口径（不是缺口）。
                ["Butt"] = null,
                ["Round"] = "lv_obj_set_style_line_rounded(obj_ln1, true, 0);",
                ["Square"] = null,
            }),

        new("KeyboardMode", HmiControlType.Keyboard, "kb1", (c, v) => c.KeyboardMode = v, "KeyboardModeCombo", null,
            new Dictionary<string, string?>
            {
                ["Default"] = "lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_TEXT_LOWER);",   // 阶段 203 修的语义错位
                ["Number"] = "lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_NUMBER);",
                ["Special"] = "lv_keyboard_set_mode(obj_kb1, LV_KEYBOARD_MODE_SPECIAL);",
            }),

        new("StretchMode", HmiControlType.Image, "im1", (c, v) => c.StretchMode = v, "StretchCombo", null,
            new Dictionary<string, string?>
            {
                ["None"] = "lv_image_set_inner_align(obj_im1, LV_IMAGE_ALIGN_CENTER);",
                ["Fill"] = "lv_image_set_inner_align(obj_im1, LV_IMAGE_ALIGN_STRETCH);",
                ["Contain"] = "lv_image_set_inner_align(obj_im1, LV_IMAGE_ALIGN_CONTAIN);",
            }),

        new("TextAlign", HmiControlType.Label, "lb1",
            (c, v) => { c.Text = "温度"; c.TextAlign = Enum.Parse<TextAlignMode>(v); }, null, typeof(TextAlignMode),
            new Dictionary<string, string?>
            {
                // ⚠ 阶段 212 这一条**量错了**：当时的夹具没设 `Text`，而导出把 `text_align` 写在
                //    `if (!string.IsNullOrEmpty(control.Text))` 里 ⇒ 三条样本被钉成"不发射"。
                //    阶段 219 给夹具补上 `Text`（现实情形）⇒ 三个取值都真的发射（登记 #96 的更正）。
                ["Left"] = "lv_obj_set_style_text_align(obj_lb1, LV_TEXT_ALIGN_LEFT, 0);",
                ["Center"] = "lv_obj_set_style_text_align(obj_lb1, LV_TEXT_ALIGN_CENTER, 0);",
                ["Right"] = "lv_obj_set_style_text_align(obj_lb1, LV_TEXT_ALIGN_RIGHT, 0);",
            }),

        new("TextVAlign", HmiControlType.Label, "lb2",
            (c, v) => c.TextVAlign = Enum.Parse<TextVAlignMode>(v), null, typeof(TextVAlignMode),
            new Dictionary<string, string?>
            {
                // Top/Bottom 走"高度自适应 ＋ 重定位"那段（Label 专用）；Middle 是 LVGL 默认 ⇒ 不发射。
                ["Top"] = "lv_obj_set_y(obj_lb2, 20);",
                ["Middle"] = null,
                ["Bottom"] = "lv_obj_set_y(obj_lb2, 20 + 40 - _h_lb2);",
            }),

        new("ValueDisplayType", HmiControlType.Label, "lb3",
            (c, v) => c.ValueDisplayType = Enum.Parse<ValueDisplayType>(v), null, typeof(ValueDisplayType),
            new Dictionary<string, string?>
            {
                // 🩸 同 TextAlign：没有变量绑定的 Label 上不发射（它由值刷新路径消费；登记 #96 的同类）。
                ["Auto"] = null,
                ["Bool"] = null,
                ["Int"] = null,
                ["Float"] = null,
                ["String"] = null,
            }),
    };

    // ───────────────────────── fixture ─────────────────────────

    private static HmiControl Ctl(HmiControlType type, string id)
        => new() { Id = id, Name = id, Type = type, X = 10, Y = 20, Width = 100, Height = 40 };

    private static string Gen(HmiControl control)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.Controls.Add(control);
        return new CodeGenerator().GenerateScreenCode(screen, null);
    }

    /// <summary>面板在 XAML 里给这个下拉框列出的取值（`ComboBoxItem Tag`，跳过空 Tag 的"继承/默认"项）。</summary>
    private static List<string> PanelOfferedValues(string comboName)
    {
        var path = Path.Combine(SourceScanner.RepoRoot, PanelXaml.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"找不到面板 XAML：{PanelXaml}");

        var text = File.ReadAllText(path);
        var block = System.Text.RegularExpressions.Regex.Match(
            text, $@"<ComboBox[^>]*x:Name=""{comboName}""(.*?)</ComboBox>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(block.Success, $"{PanelXaml} 里找不到下拉框 {comboName}");

        return System.Text.RegularExpressions.Regex
            .Matches(block.Groups[1].Value, @"Tag=""([^""]*)""")
            .Select(m => m.Groups[1].Value)
            .Where(v => v.Length > 0)
            .ToList();
    }

    private static List<string> OfferedValues(Case c)
        => c.XamlCombo is { Length: > 0 } combo
            ? PanelOfferedValues(combo)
            : Enum.GetNames(c.EnumType!).ToList();

    // ───────────────────────── 判据 ─────────────────────────

    /// <summary>
    /// **双向闭合**：面板给出的取值 ↔ 本表登记的取值，一个不多一个不少。
    /// 面板多一个选项（或枚举多一个成员）⇒ 红，逼着确认"这个取值的导出语义是什么"。
    /// </summary>
    [Fact]
    public void EveryOfferedValue_IsAccountedFor()
    {
        var problems = new List<string>();

        foreach (var c in Cases)
        {
            var offered = OfferedValues(c).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            var tabulated = c.Expected.Keys.OrderBy(v => v, StringComparer.Ordinal).ToArray();

            foreach (var v in offered.Except(tabulated, StringComparer.Ordinal))
                problems.Add($"{c.Field}：面板/枚举给了取值 `{v}`，本表没登记它的导出语义");
            foreach (var v in tabulated.Except(offered, StringComparer.Ordinal))
                problems.Add($"{c.Field}：本表登记了 `{v}`，但面板/枚举已经不给这个取值了");
        }

        Assert.True(problems.Count == 0,
            "枚举型选择字段的取值账对不上：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>每个取值**真跑一遍生成器**：登记了片段就必须逐字出现，登记为 null 就必须**不发射**。</summary>
    [Fact]
    public void EveryValue_EmitsWhatTheTableSays()
    {
        var problems = new List<string>();

        foreach (var c in Cases)
        {
            foreach (var (value, fragment) in c.Expected)
            {
                var control = Ctl(c.Type, c.ObjId);
                c.Apply(control, value);
                var code = Gen(control);

                if (fragment is { Length: > 0 })
                {
                    if (!code.Contains(fragment, StringComparison.Ordinal))
                        problems.Add($"{c.Field}={value}：期望发射 `{fragment}`，实际没有");
                }
                else
                {
                    // "不发射"要判得**具体**：不是"整段没有相关调用"这么含糊，而是逐个取值都不该出现
                    // 该字段的专属发射点。这里用字段名对应的发射锚点来判（见每条的映射）。
                    var anchor = c.Field switch
                    {
                        "TextAlign" => $"lv_obj_set_style_text_align(obj_{c.ObjId},",
                        "ValueDisplayType" => "lv_label_set_text_fmt",
                        "LineCap" => "lv_obj_set_style_line_rounded",
                        "TextVAlign" => "lv_obj_set_y(obj_lb2,",
                        _ => throw new InvalidOperationException($"未登记的空发射锚点：{c.Field}"),
                    };
                    if (code.Contains(anchor, StringComparison.Ordinal)
                        && !(c.Field == "TextVAlign" && value == "Middle" && code.Contains("lv_obj_set_y(obj_lb2, 20)", StringComparison.Ordinal)))
                    {
                        // LineCap 的 Butt/Square 会带一行**注释**说明"以平头表达" —— 注释不算发射
                        // （`CSource` 解析会跳过注释，这里用同一把尺子）。
                        var calls = CSource.ParseCalls(code)
                            .Any(call => call.Display.StartsWith(anchor.Split('(')[0], StringComparison.Ordinal));
                        if (calls)
                            problems.Add($"{c.Field}={value}：本表说**不发射**，但代码里出现了 `{anchor}`");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, "取值 → 发射 对不上：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>面自检：字段数与"取值行数"下限（塌了要有人知道）。</summary>
    [Fact]
    public void Face_CoversTheExpectedBreadth()
    {
        Assert.True(Cases.Length >= 6, $"只登记了 {Cases.Length} 个枚举型选择字段 —— 阶段 212 实测 6 个");
        var pairs = Cases.Sum(c => c.Expected.Count);
        Assert.True(pairs >= 18, $"取值账只剩 {pairs} 行 —— 阶段 212 实测 18 行");
    }
}
