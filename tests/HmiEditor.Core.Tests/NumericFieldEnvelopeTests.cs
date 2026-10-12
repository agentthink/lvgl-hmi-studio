using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **数值型字段的取值包络**（阶段 213）—— 承阶段 212 的枚举取值账：那一面管"下拉框给的每个取值"，
/// 这一面管"**数字框给的每一段范围**"。
///
/// <para><b>口径</b>：每个字段的取值样本**必须覆盖面板那一格声明的 `Minimum` 与 `Maximum`**
/// （从 `PropertyPanelView.axaml` 里读，改名/改范围都会红），并对每个样本**真跑一遍生成器**，
/// 断言它发射了什么（`null` ＝ 本阶段实测"不发射" ＋ 理由）。</para>
///
/// <para><b>为什么不能只靠"范围大致对"</b>：这类字段的坏法同样是静默的 —— 越界值被悄悄钳到边界、
/// 或者干脆**不发射**（于是用户看到的与导出不一致）。本面把每条口径钉住：
/// `Opacity` 的百分比→0..255 换算与 `>= 100 不发射` · `Rotation` 的 `×10` 与**不回绕/不钳位** ·
/// `Radius`/`BorderWidth` 的 `<= 0 不发射` 且**无上限钳位** · `Brightness` 的**原样透传（不钳 255）** ·
/// `FontSize` 的"**没有自定义字体资产就不发射**"（见登记 `#97`）。</para>
/// </summary>
public class NumericFieldEnvelopeTests
{
    /// <summary>一条样本：值 → 期望发射（null ＝ 不发射）。</summary>
    private sealed record Sample(double Value, string? Fragment, string Note);

    /// <summary>一个字段：面板那一格（读 Min/Max 用）→ 控件类型 → 赋值 → 样本。</summary>
    private sealed record Field(
        string Name,
        string PanelBox,
        HmiControlType Type,
        string ObjId,
        Action<HmiControl, double> Apply,
        string NoEmitAnchor,
        string? AvailableFont,
        IReadOnlyList<Sample> Samples,
        Action<HmiScreen, double>? ScreenHook = null,
        string? AvailableImage = null,
        bool Dead = false);

    private const string PanelXaml = "src/HmiEditor.App/Views/PropertyPanelView.axaml";

    private static readonly Field[] Fields =
    {
        new("Opacity", "OpacityBox", HmiControlType.Rectangle, "rc1",
            (c, v) => c.Opacity = v, "lv_obj_set_style_opa", null,
            new Sample[]
            {
                new(-1, "lv_obj_set_style_opa(obj_rc1, 0, 0);", "越下界 ⇒ 钳到 0"),
                new(0, "lv_obj_set_style_opa(obj_rc1, 0, 0);", "面板下界：0% ⇒ 全透明"),
                new(50, "lv_obj_set_style_opa(obj_rc1, 127, 0);", "百分比 → 0..255 四舍五入"),
                new(100, null, "面板上界：100% ＝ LVGL 默认全不透明 ⇒ 不发射"),
                new(101, null, "越上界 ⇒ 当作全不透明（静默 ⇒ 这里钉住）"),
            }),

        new("Rotation", "RotationBox", HmiControlType.Rectangle, "rc2",
            (c, v) => c.Rotation = v, "lv_obj_set_style_transform_rotation", null,
            new Sample[]
            {
                new(-10, "lv_obj_set_style_transform_rotation(obj_rc2, -100, 0);", "负角度照发（LVGL 认符号）"),
                new(0, null, "面板下界：0° ＝ 无旋转 ⇒ 不发射"),
                new(45, "lv_obj_set_style_transform_rotation(obj_rc2, 450, 0);", "×10（0.1° 单位）"),
                new(360, "lv_obj_set_style_transform_rotation(obj_rc2, 3600, 0);", "面板上界：**不回绕**成 0"),
                new(361, "lv_obj_set_style_transform_rotation(obj_rc2, 3610, 0);", "越上界：**不钳位**（LVGL 运行时归一）"),
            }),

        new("Radius", "PropRadius", HmiControlType.Rectangle, "rc3",
            (c, v) => c.Radius = (int)v, "lv_obj_set_style_radius", null,
            new Sample[]
            {
                new(-5, null, "负值 ⇒ 不发射"),
                new(0, null, "面板下界：0 ＝ 直角 ⇒ 不发射"),
                new(12, "lv_obj_set_style_radius(obj_rc3, 12, 0);", "原样"),
                new(999, "lv_obj_set_style_radius(obj_rc3, 999, 0);", "**无上限钳位**（面板也没写 Maximum）"),
            }),

        new("BorderWidth", "PropBorderWidth", HmiControlType.Rectangle, "rc4",
            (c, v) => c.BorderWidth = (int)v, "lv_obj_set_style_border_width", null,
            new Sample[]
            {
                new(-5, null, "负值 ⇒ 不发射"),
                new(0, null, "面板下界：0 ＝ 无边框 ⇒ 不发射"),
                new(3, "lv_obj_set_style_border_width(obj_rc4, 3, 0);", "原样"),
                new(999, "lv_obj_set_style_border_width(obj_rc4, 999, 0);", "**无上限钳位**"),
            }),

        new("Brightness", "BrightnessBox", HmiControlType.Led, "led1",
            (c, v) => c.Brightness = (int)v, "lv_led_set_brightness", null,
            new Sample[]
            {
                new(0, "lv_led_set_brightness(obj_led1, 0);", "面板下界：0 ⇒ 照发（LVGL 认 0）"),
                new(128, "lv_led_set_brightness(obj_led1, 128);", "原样"),
                new(255, "lv_led_set_brightness(obj_led1, 255);", "面板上界"),
                new(256, "lv_led_set_brightness(obj_led1, 256);", "越上界：**不钳 255**（LVGL 运行时钳）"),
            }),

        new("FontSize（无自定义字体资产 ⇒ 内置字号近似）", "PropFontSize", HmiControlType.Label, "lb1",
            (c, v) => c.FontSize = (int)v, "lv_obj_set_style_text_font", null,
            new Sample[]
            {
                new(6, "lv_obj_set_style_text_font(obj_lb1, &lv_font_montserrat_12, 0);",
                    "面板下界：**没有** FontFamily 资产 ⇒ 走内置字号近似（比主题默认小 ⇒ 12，见 #97）"),
                new(72, "lv_obj_set_style_text_font(obj_lb1, &lv_font_montserrat_16, 0);",
                    "面板上界：同上（比主题默认大 ⇒ 16）"),
            }),

        new("FontSize（有自定义字体资产）", "PropFontSize", HmiControlType.Label, "lb2",
            (c, v) => { c.FontFamily = "myfont"; c.FontSize = (int)v; }, "lv_obj_set_style_text_font", "myfont",
            new Sample[]
            {
                new(6, "#if LV_USE_FREETYPE", "有资产时进 `#if LV_USE_FREETYPE` 分支"),
                new(6, "lv_obj_set_style_text_font(obj_lb2, &font_myfont_6, 0);", "尺寸进了字体变量名"),
                new(72, "lv_obj_set_style_text_font(obj_lb2, &font_myfont_72, 0);", "面板上界"),
            }),

        // ── 阶段 214：续做（同一把尺子：端点必采 ＋ 逐样本真跑生成器）──

        new("X", "PropX", HmiControlType.Rectangle, "rc5",
            (c, v) => c.X = v, "lv_obj_set_pos", null,
            new Sample[]
            {
                new(0, "lv_obj_set_pos(obj_rc5, 0, 20);", "面板下界：0 **照发**（位置没有「默认即省略」一说）"),
                new(4096, "lv_obj_set_pos(obj_rc5, 4096, 20);", "面板上界：不钳位"),
            }),

        new("Y", "PropY", HmiControlType.Rectangle, "rc6",
            (c, v) => c.Y = v, "lv_obj_set_pos", null,
            new Sample[]
            {
                new(0, "lv_obj_set_pos(obj_rc6, 10, 0);", "面板下界：0 照发"),
                new(4096, "lv_obj_set_pos(obj_rc6, 10, 4096);", "无上限（面板也没写 Maximum）"),
            }),

        new("Width", "PropWidth", HmiControlType.Rectangle, "rc7",
            (c, v) => c.Width = v, "lv_obj_set_size", null,
            new Sample[]
            {
                new(0, "lv_obj_set_size(obj_rc7, 0, 40);", "0 **照发**（比面板下界还小 ⇒ 不钳）"),
                new(10, "lv_obj_set_size(obj_rc7, 10, 40);", "面板下界"),
                new(4096, "lv_obj_set_size(obj_rc7, 4096, 40);", "大值照发"),
            }),

        new("Height", "PropHeight", HmiControlType.Rectangle, "rc8",
            (c, v) => c.Height = v, "lv_obj_set_size", null,
            new Sample[]
            {
                new(0, "lv_obj_set_size(obj_rc8, 100, 0);", "0 照发"),
                new(10, "lv_obj_set_size(obj_rc8, 100, 10);", "面板下界"),
            }),

        new("SelectedIndex", "SelectedIndexBox", HmiControlType.Dropdown, "dd1",
            (c, v) => c.SelectedIndex = (int)v, "lv_dropdown_set_selected", null,
            new Sample[]
            {
                new(0, "lv_dropdown_set_selected(obj_dd1, 0);", "面板下界：0 ＝ 选第一项，照发"),
                new(10000, "lv_dropdown_set_selected(obj_dd1, 10000);", "面板上界：**不按选项数校验**（越界由 LVGL 兜）"),
            }),

        new("MaxLength", "MaxLengthBox", HmiControlType.TextArea, "ta1",
            (c, v) => c.MaxLength = (int)v, "lv_textarea_set_max_length", null,
            new Sample[]
            {
                new(0, null, "面板下界：**0 ＝ 不限长度 ⇒ 不发射**"),
                new(20, "lv_textarea_set_max_length(obj_ta1, 20);", "正常值原样"),
                new(10000, "lv_textarea_set_max_length(obj_ta1, 10000);", "面板上界"),
            }),

        new("SpinSpeed", "SpinSpeedBox", HmiControlType.Spinner, "sp1",
            (c, v) => c.SpinSpeed = (int)v, "lv_spinner_set_anim_params", null,
            new Sample[]
            {
                new(100, "lv_spinner_set_anim_params(obj_sp1, 100, 60);", "面板下界（转角固定 60）"),
                new(10000, "lv_spinner_set_anim_params(obj_sp1, 10000, 60);", "面板上界"),
            }),

        new("ScreenBgImageOpacity", "ScreenBgImageOpacity", HmiControlType.Rectangle, "rc9",
            (c, v) => { }, "lv_obj_set_style_opa", null,
            new Sample[]
            {
                new(0, "lv_obj_set_style_opa(bg_img, 0, 0);", "屏幕背景图：0 ⇒ 照发"),
                new(50, "lv_obj_set_style_opa(bg_img, 127, 0);", "百分比 ×2.55 四舍五入"),
                new(100, "lv_obj_set_style_opa(bg_img, 255, 0);", "面板上界：**照发 255**（与控件 `Opacity` 的「100 ⇒ 不发射」**不对称** ⇒ 这里钉住）"),
            },
            (screen, v) => { screen.BgImageAssetId = "img1"; screen.BgImageOpacity = (int)v; },
            "img1"),

        // ── 阶段 216：把剩下的数字框清完（端点必采 ＋ 逐样本真跑；死设置改判"逐字节相同"）──

        new("MinValue", "MinValueBox", HmiControlType.Slider, "sl9",
            (c, v) => { c.MinValue = v; c.MaxValue = 100; }, "lv_slider_set_range", null,
            new Sample[]
            {
                new(-1000000, "lv_slider_set_range(obj_sl9, -1000000, 100);", "面板下界（负值照发）"),
                new(1000000, "lv_slider_set_range(obj_sl9, 1000000, 100);", "面板上界；**不校验 min<=max**（倒置照发 ⇒ 这里钉住）"),
            }),

        new("MaxValue", "MaxValueBox", HmiControlType.Slider, "sl10",
            (c, v) => { c.MinValue = 0; c.MaxValue = v; }, "lv_slider_set_range", null,
            new Sample[]
            {
                new(-1000000, "lv_slider_set_range(obj_sl10, 0, -1000000);", "面板下界：负上界照发"),
                new(1000000, "lv_slider_set_range(obj_sl10, 0, 1000000);", "面板上界"),
            }),



        new("RowCount", "RowCountBox", HmiControlType.Table, "tb9",
            (c, v) => { c.RowCount = (int)v; c.ColumnCount = 1; }, "lv_table_set_row_count", null,
            new Sample[]
            {
                new(1, "lv_table_set_row_count(obj_tb9, 1);", "面板下界"),
                new(100, "lv_table_set_row_count(obj_tb9, 100);", "面板上界"),
            }),

        new("ColumnCount", "ColCountBox", HmiControlType.Table, "tb10",
            (c, v) => { c.RowCount = 1; c.ColumnCount = (int)v; }, "lv_table_set_column_count", null,
            new Sample[]
            {
                new(1, "lv_table_set_column_count(obj_tb10, 1);", "面板下界"),
                new(100, "lv_table_set_column_count(obj_tb10, 100);", "面板上界"),
            }),

        new("RowHeight", "RowHeightBox", HmiControlType.Table, "tb11",
            (c, v) => { c.RowCount = 2; c.ColumnCount = 1; c.RowHeight = v; }, "lv_obj_set_style_min_height", null,
            new Sample[]
            {
                new(0, null, "面板下界：0 ＝ 用主题/默认行高 ⇒ 不发射"),
                new(10000, "lv_obj_set_style_min_height(obj_tb11, 10000, LV_PART_ITEMS);", "面板上界"),
            }),

        new("PipeFlowSpeed", "PipeFlowSpeedBox", HmiControlType.Pipe, "pp9",
            (c, v) => c.PipeFlowSpeed = (int)v, "流动动画周期", null,
            new Sample[]
            {
                new(0, null, "面板下界：0 ＝ 不流动 ⇒ 连注释都不写"),
                new(10000, "// 流动动画周期: 10000ms（运行时由 lv_anim 驱动，方向: 正向）", "面板上界：只写注释（实际动画由运行时创建）"),
            }),

        new("ScreenWidth", "ScreenWidth", HmiControlType.Rectangle, "rc20",
            (c, v) => { }, "lv_obj_set_size(screen", null,
            new Sample[]
            {
                new(1, "lv_obj_set_size(screen, 1, 600);", "面板下界（屏幕尺寸**不钳位**）"),
                new(4096, "lv_obj_set_size(screen, 4096, 600);", "面板上界"),
            },
            (screen, v) => { screen.Width = v; }),

        new("StepValue", "StepValueBox", HmiControlType.Slider, "sl11",
            (c, v) => { c.MinValue = 0; c.MaxValue = 100; c.StepValue = v; c.ShowTicks = true; },
            "lv_label_set_text", null,
            new Sample[]
            {
                new(0.0001, "lv_label_set_text(_ticklbl_sl11_20, \"0\");", "面板下界 0.0001（取整成 0）⇒ 21 个刻度**全标 \"0\"**（刻度数上限 21）"),
                new(1, "lv_label_set_text(_ticklbl_sl11_20, \"20\");", "步长 1 ⇒ 21 个刻度（0..20）"),
                new(1000000, "lv_label_set_text(_ticklbl_sl11_1, \"1000000\");", "面板上界 ⇒ 只有 2 个刻度（0 与 1000000）"),
            }),

        new("ValueScaleFactor", "ValueScaleBox", HmiControlType.Slider, "sl12",
            (c, v) => { c.ValueScaleFactor = v; c.VariableBindingId = "v1"; }, "// Value display", null,
            new Sample[]
            {
                new(0.0001, "// Value display: displayType=Auto, scale=0.0001, decimals=2", "面板下界：screen 代码只写这行注释（真换算在**值刷新**路径）"),
                new(2, "// Value display: displayType=Auto, scale=2, decimals=2", "正常值"),
                new(1000000, "// Value display: displayType=Auto, scale=1000000, decimals=2", "面板上界"),
            }),

        new("ScreenHeight", "ScreenHeight", HmiControlType.Rectangle, "rc26",
            (c, v) => { }, "lv_obj_set_size(screen", null,
            new Sample[]
            {
                new(1, "lv_obj_set_size(screen, 1024, 1);", "面板下界"),
                new(4096, "lv_obj_set_size(screen, 1024, 4096);", "面板上界"),
            },
            (screen, v) => { screen.Height = v; }),

        // ── 死设置（阶段 216 的 #98）：面板能改、存进工程，但**预览与导出都不读** ──
        //    判法不是"没发射某行"（那太弱），而是**生成物与不设该字段逐字节相同**。
        new("ScreenDpi", "ScreenDpi", HmiControlType.Rectangle, "rc21",
            (c, v) => { }, "dpi", null,
            new Sample[]
            {
                new(0.5, null, "面板下界：工程里的 DpiScale（0.5..3）—— 全仓只有两个设置面板读写它"),
                new(3, null, "面板上界；生成物逐字节相同 ⇒ 死设置（登记 #98）"),
            },
            (screen, v) => { }, null, Dead: true),

        new("SafeLeft", "SafeLeft", HmiControlType.Rectangle, "rc22",
            (c, v) => { }, "safe", null,
            new Sample[]
            {
                new(0, null, "SafeAreaPadding.Left —— 面板范围 [0,2000]"),
                new(2000, null, "同上；生成物逐字节相同 ⇒ 死设置（登记 #98）"),
            },
            (screen, v) => { }, null, Dead: true),

        new("SafeTop", "SafeTop", HmiControlType.Rectangle, "rc23",
            (c, v) => { }, "safe", null,
            new Sample[]
            {
                new(0, null, "SafeAreaPadding.Top"),
                new(2000, null, "同上；生成物逐字节相同 ⇒ 死设置（登记 #98）"),
            },
            (screen, v) => { }, null, Dead: true),

        new("SafeRight", "SafeRight", HmiControlType.Rectangle, "rc24",
            (c, v) => { }, "safe", null,
            new Sample[]
            {
                new(0, null, "SafeAreaPadding.Right"),
                new(2000, null, "同上；生成物逐字节相同 ⇒ 死设置（登记 #98）"),
            },
            (screen, v) => { }, null, Dead: true),

        new("SafeBottom", "SafeBottom", HmiControlType.Rectangle, "rc25",
            (c, v) => { }, "safe", null,
            new Sample[]
            {
                new(0, null, "SafeAreaPadding.Bottom"),
                new(2000, null, "同上；生成物逐字节相同 ⇒ 死设置（登记 #98）"),
            },
            (screen, v) => { }, null, Dead: true),
    };

    // ───────────────────────── fixture ─────────────────────────

    /// <summary>不施加该字段的基线生成（只用于死设置的"逐字节相同"判据）。</summary>
    private static string GenBaseline(Field f)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.Controls.Add(Ctl(f.Type, f.ObjId));

        return new CodeGenerator().GenerateScreenCode(
            screen, null, null, null, null,
            f.AvailableImage is { Length: > 0 } image
                ? new HashSet<string>(StringComparer.Ordinal) { image }
                : null,
            f.AvailableFont is { Length: > 0 } font
                ? new HashSet<string>(StringComparer.Ordinal) { font }
                : null);
    }

    private static HmiControl Ctl(HmiControlType type, string id)
        => new() { Id = id, Name = id, Type = type, X = 10, Y = 20, Width = 100, Height = 40 };

    private static string Gen(Field f, double value)
    {
        var control = Ctl(f.Type, f.ObjId);
        f.Apply(control, value);

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.Controls.Add(control);
        f.ScreenHook?.Invoke(screen, value);

        return new CodeGenerator().GenerateScreenCode(
            screen, null, null, null, null,
            f.AvailableImage is { Length: > 0 } image
                ? new HashSet<string>(StringComparer.Ordinal) { image }
                : null,
            f.AvailableFont is { Length: > 0 } font
                ? new HashSet<string>(StringComparer.Ordinal) { font }
                : null);
    }

    // ───────────────────────── 判据 ─────────────────────────

    /// <summary>面板 XAML 里那一格声明的范围（`Minimum` / `Maximum`）。</summary>
    private static (double? Min, double? Max) PanelRange(string boxName)
    {
        var path = Path.Combine(SourceScanner.RepoRoot, PanelXaml.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"找不到面板 XAML：{PanelXaml}");

        var text = File.ReadAllText(path);
        var tag = Regex.Match(text, $@"<(?:NumericUpDown|fa:FANumberBox)\b[^>]*x:Name=""{boxName}""[^>]*?/?>",
            RegexOptions.Singleline);
        Assert.True(tag.Success, $"{PanelXaml} 里找不到数字框 {boxName}");

        double? Num(string attr)
        {
            var m = Regex.Match(tag.Value, attr + @"=""(-?[\d.]+)""");
            return m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        return (Num("Minimum"), Num("Maximum"));
    }

    /// <summary>
    /// **取值样本必须覆盖面板声明的两个端点**：面板把范围改了（或这一格改名了）⇒ 红，
    /// 逼着重新确认边界语义（越界/端点到底发射什么）。
    /// </summary>
    [Fact]
    public void EveryField_CoversItsPanelRangeEndpoints()
    {
        var problems = new List<string>();

        foreach (var f in Fields)
        {
            var (min, max) = PanelRange(f.PanelBox);
            var sampled = f.Samples.Select(s => s.Value).ToHashSet();

            // 阶段 217：样本类型从 int 改成 double ⇒ **连死设置与十进制下界（0.0001 / 0.5）也能端点必采**，
            // 这里不再给任何字段开后门。
            if (min is { } lo && !sampled.Contains(lo))
                problems.Add($"{f.Name}（面板 {f.PanelBox}）：面板下界 {lo} 没有被采样");
            if (max is { } hi && !sampled.Contains(hi))
                problems.Add($"{f.Name}（面板 {f.PanelBox}）：面板上界 {hi} 没有被采样");
        }

        Assert.True(problems.Count == 0,
            "数值字段的样本没盖住面板声明的范围：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>每个样本**真跑一遍生成器**：登记了片段必须逐字出现；登记 `null` 必须不发射。</summary>
    [Fact]
    public void EverySample_EmitsWhatTheTableSays()
    {
        var problems = new List<string>();

        foreach (var f in Fields)
        {
            if (f.Dead)
            {
                var baseline = GenBaseline(f);
                foreach (var s in f.Samples)
                {
                    if (Gen(f, s.Value) != baseline)
                        problems.Add($"{f.Name}={s.Value}：本表说它**没人消费**，但生成物变了 —— 死设置被接线了？");
                }
                continue;
            }

            foreach (var s in f.Samples)
            {
                var code = Gen(f, s.Value);

                if (s.Fragment is { Length: > 0 } expected)
                {
                    if (!code.Contains(expected, StringComparison.Ordinal))
                        problems.Add($"{f.Name}={s.Value}（{s.Note}）：期望 `{expected}`，实际没有");
                }
                else
                {
                    // "不发射"判**调用**（注释不算）—— 与阶段 212 同一把尺子
                    var calls = CSource.ParseCalls(code)
                        .Where(c => c.Display.StartsWith(f.NoEmitAnchor, StringComparison.Ordinal))
                        .ToArray();
                    if (calls.Length > 0)
                        problems.Add($"{f.Name}={s.Value}（{s.Note}）：本表说不发射，但出现了 `{calls[0].Display}`");
                }
            }
        }

        Assert.True(problems.Count == 0, "数值取值 → 发射 对不上：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>面自检：字段数与样本行数下限。</summary>
    [Fact]
    public void Face_CoversTheExpectedBreadth()
    {
        Assert.True(Fields.Length >= 30, $"只登记了 {Fields.Length} 个数值字段 —— 阶段 217 实测 31 条（30 个字段）");
        var rows = Fields.Sum(f => f.Samples.Count);
        Assert.True(rows >= 71, $"取值样本只剩 {rows} 行 —— 阶段 217 实测 71 行");
    }

    /// <summary>
    /// 面板里这两个数字框**不在本面**：它们的消费者在**过程控制/绘制回调**那条 C 代码路径上
    /// （screen 代码里连 `tick` 字样都没有）⇒ 由阶段 203 的指针表 + `ProcessControlCodeGenTests` 守着。
    /// 写在这里是为了让下面那条"面板数字框一个不落"的闭合判据有出口（**不是**遮羞布：
    /// 它们已经有判据，只是不在本面）。
    /// </summary>
    private static readonly Dictionary<string, string> Elsewhere = new(StringComparer.Ordinal)
    {
        ["TickCountBox"] = "Gauge `MajorTickCount` ⇒ ProcessControlCodeGenTests（绘制回调路径）",
        ["TankTickCountBox"] = "Tank `TankTickCount` ⇒ ProcessControlCodeGenTests（同上）",
    };

    /// <summary>
    /// **面板里的每个具名数字框都必须有出口**（阶段 217 的闭合判据）：要么在本面表里（`PanelBox`），
    /// 要么在 <see cref="Elsewhere"/> 登记（并说明它归哪儿）。新增一个数字框、或改了某一格的名字 ⇒ 红。
    ///
    /// <para>阶段 217 把样本类型从 `int` 改成 `double` 之后，本面连 `0.0001`（StepValue/ValueScale）
    /// 与 `0.5`（ScreenDpi）这样的十进制端点也能采 ⇒ **数值面到此清空**，这条闭合判据把它钉住。</para>
    /// </summary>
    [Fact]
    public void EveryPanelNumericBox_HasAnOutlet()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, PanelXaml.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(path);

        var boxes = Regex.Matches(text, @"<(?:NumericUpDown|fa:FANumberBox)\b[^>]*?/?>", RegexOptions.Singleline)
            .Select(m => Regex.Match(m.Value, @"x:Name=""([A-Za-z_]\w*)"""))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.True(boxes.Length >= 31, $"只扫到 {boxes.Length} 个具名数字框 —— 阶段 217 实测 31 个");

        var inFace = Fields.Select(f => f.PanelBox).ToHashSet(StringComparer.Ordinal);
        var missing = boxes.Where(b => !inFace.Contains(b) && !Elsewhere.ContainsKey(b)).ToArray();
        var ghosts = inFace.Where(b => !boxes.Contains(b, StringComparer.Ordinal)).ToArray();

        Assert.True(missing.Length == 0,
            "这些面板数字框既不在本面、也没登记出口（新加的数字框必须二选一）：" + string.Join(", ", missing));
        Assert.True(ghosts.Length == 0,
            "本面表里这些面板数字框在 XAML 里已经不存在了（改名/删除后要同步）：" + string.Join(", ", ghosts));
    }
}
