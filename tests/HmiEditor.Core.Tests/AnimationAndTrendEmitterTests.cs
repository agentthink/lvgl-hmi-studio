using HmiEditor.Core.CodeGen;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 动画与趋势图的发射输出覆盖（补既有测试的空白路径）。
///
/// <para>盘点（API 锚点法）发现零覆盖的路径：
/// <c>lv_anim_set_path_cb</c> / <c>lv_anim_set_var</c> / <c>lv_anim_set_repeat_count</c>、
/// <c>LV_CHART_TYPE_LINE</c> / <c>LV_CHART_AXIS_PRIMARY_X</c> / <c>LV_CHART_AXIS_PRIMARY_Y</c>。
/// 其中路径映射当年有<b>三份同体实现</b>（<see cref="AnimationConfig.GetPathCCode"/>、
/// <c>CodeGenerator</c> 私有静态、动画编辑器预览私有静态），本类覆盖了其中两个调用点 ——
/// 正是这张网让后续统一它们有据可依。现已收敛为
/// <see cref="AnimationPathHelper.GetPathCCode"/> 唯一实现，单一来源契约见
/// <see cref="AnimationPathSingleSourceTests"/>。</para>
/// </summary>
public class AnimationAndTrendEmitterTests
{
    // ───────────────────────── fixture ─────────────────────────

    private static AnimationConfig Anim(Action<AnimationConfig>? cfg = null)
    {
        var a = new AnimationConfig
        {
            Id = "an1", Name = "淡入", ControlId = "c1",
            PropertyName = "Opacity", StartValue = 0, EndValue = 255,
            DurationMs = 500, DelayMs = 0, Trigger = "OnLoad",
        };
        cfg?.Invoke(a);
        return a;
    }

    private static string GenAnim(params AnimationConfig[] anims)
        => new CodeGenerator().GenerateAnimationCode(anims);

    private static TrendConfig Chart(Action<TrendConfig>? cfg = null)
    {
        var c = new TrendConfig { Id = "ch1", Name = "温度曲线", MaxPoints = 600 };
        c.Pens.Add(new TrendPen { Label = "温度", VariableId = "v1", Color = "#38bdf8" });
        cfg?.Invoke(c);
        return c;
    }

    private static string GenTrend(params TrendConfig[] charts)
        => new CodeGenerator().GenerateTrendCode(charts, Array.Empty<DataLogConfig>());

    /// <summary>
    /// **判据（阶段 176）**：设备侧 CSV 的**分隔符取工程配置**（`DataLogConfig.CsvDelimiter`），
    /// 不再硬编码逗号 —— 该字段的文档写着「CSV 导出分隔符」，而设备 CSV 是设备侧唯一的 CSV 出口
    /// （阶段 176 实测：修之前配置成 `;` 也照样写逗号 ⇒ 这个设置在设备上完全无效）。
    /// </summary>
    [Theory]
    [InlineData(",", "Temp")]
    [InlineData(";", "Temp")]
    [InlineData("|", "Temp")]
    public void TrendCsvExport_UsesConfiguredDelimiter(string delimiter, string variableId)
    {
        var log = new DataLogConfig
        {
            Id = "log1",
            SampleIntervalMs = 1000,
            MaxRecords = 100,
            VariableIds = new List<string> { variableId },
            CsvDelimiter = delimiter,
            IsEnabled = true,
        };

        var code = new CodeGenerator().GenerateTrendCode(new[] { Chart() }, new[] { log });
        var fn = CIdentifier.DataLogExportCsvFn(log.Id);

        // 前提自检：导出函数真的生成了（否则下面的断言是空转）
        Assert.Contains(fn, code, StringComparison.Ordinal);

        // 表头与数据行都用配置的分隔符
        // ⚠ 用 CSource 的**结构化/规范化**断言，不写"精确调用文本 + 字符串包含"
        //   （TestAssertionStyleGuardTests 禁止那种写法：排版一改就假红、出现在注释里又假绿）
        CSource.AssertText(code, $"write_fn(\"{delimiter}{variableId}\");");
        CSource.AssertText(code, $"\"{delimiter}%.3f\"");

        // 反向：配置成别的分隔符时，**不许**再出现硬编码的逗号形态
        if (delimiter != ",")
        {
            CSource.AssertNoText(code, $"write_fn(\",{variableId}\");");
            CSource.AssertNoText(code, "\",%.3f\"");
        }
    }

    // ───────────────────────── 动画：缓动路径映射（CodeGenerator 侧）─────────────────────────

    [Theory]
    [InlineData(AnimationPathType.Linear, "lv_anim_path_linear")]
    [InlineData(AnimationPathType.EaseInOut, "lv_anim_path_ease_in_out")]
    [InlineData(AnimationPathType.EaseIn, "lv_anim_path_ease_in")]
    [InlineData(AnimationPathType.EaseOut, "lv_anim_path_ease_out")]
    [InlineData(AnimationPathType.Bounce, "lv_anim_path_bounce")]
    [InlineData(AnimationPathType.Overshoot, "lv_anim_path_overshoot")]
    [InlineData(AnimationPathType.Step, "lv_anim_path_step")]
    public void Animation_PathType_MapsToLvglPathCallback(AnimationPathType path, string expected)
    {
        var code = GenAnim(Anim(a => a.PathType = path));
        CSource.AssertCode(code, $"lv_anim_set_path_cb(&a, {expected});");
    }

    [Fact]
    public void Animation_KeyframePathType_OverridesGlobalPath()
    {
        // 关键帧自带 PathType 时以它为准（关键帧以注释形式输出）
        var anim = Anim(a =>
        {
            a.PathType = AnimationPathType.Linear;
            a.Keyframes.Add(new AnimationKeyframe { TimePercent = 50, Value = 128, PathType = AnimationPathType.Bounce });
        });
        var code = GenAnim(anim);

        CSource.AssertText(code, "50% -> 128.0  (lv_anim_path_bounce)");
        // 全局路径仍是 Linear（关键帧不改变全局设置）
        CSource.AssertCode(code, "lv_anim_set_path_cb(&a, lv_anim_path_linear);");
    }

    [Fact]
    public void Animation_KeyframeWithoutPathType_UsesGlobalPath()
    {
        var anim = Anim(a =>
        {
            a.PathType = AnimationPathType.Step;
            a.Keyframes.Add(new AnimationKeyframe { TimePercent = 25, Value = 64 });
        });
        var code = GenAnim(anim);

        CSource.AssertText(code, "25% -> 64.0  (lv_anim_path_step)");
    }

    // ───────────────────────── 动画：其余设置项 ─────────────────────────

    [Fact]
    public void Animation_EmitsVarValuesDurationDelayAndStart()
    {
        var code = GenAnim(Anim(a => { a.DurationMs = 800; a.DelayMs = 120; }));

        CSource.AssertCode(code, "lv_anim_init(&a);");
        // set_var 绑的是启动函数的形参 target（对象由 apply_animations 调用时传入），
        // 不是硬编码的 obj_xxx —— 见 Animation_OnLoadTrigger_PassesTargetAsArgument
        CSource.AssertCode(code, "lv_anim_set_var(&a, target);");
        CSource.AssertCode(code, "lv_anim_set_values(&a, 0.0, 255.0);");
        CSource.AssertCode(code, "lv_anim_set_duration(&a, 800);");
        CSource.AssertCode(code, "lv_anim_set_delay(&a, 120);");
        CSource.AssertCode(code, "lv_anim_start(&a);");
    }

    [Fact]
    public void Animation_StartFunction_TakesTargetAsParameter()
    {
        var code = GenAnim(Anim());
        CSource.AssertText(code, "static void start_anim_an1(lv_obj_t* target) {");
    }

    [Fact]
    public void Animation_OnLoadTrigger_PassesTargetAsArgument()
    {
        var code = GenAnim(Anim(a => a.ControlId = "c1"));
        CSource.AssertCode(code, "start_anim_an1(obj_c1);");
    }

    [Fact]
    public void Animation_EmptyControlId_TargetsActiveScreen()
    {
        var code = GenAnim(Anim(a => a.ControlId = ""));
        CSource.AssertCode(code, "start_anim_an1(lv_screen_active());");
    }

    [Theory]
    [InlineData(-1, "LV_ANIM_REPEAT_INFINITE")]
    [InlineData(0, "0")]
    [InlineData(5, "5")]
    public void Animation_RepeatCount_MapsToLvglMacro(int repeat, string expected)
    {
        var code = GenAnim(Anim(a => a.RepeatCount = repeat));
        CSource.AssertCode(code, $"lv_anim_set_repeat_count(&a, {expected});");
    }

    [Theory]
    [InlineData("X", "lv_obj_set_x")]
    [InlineData("Y", "lv_obj_set_y")]
    [InlineData("Width", "lv_obj_set_width")]
    [InlineData("Height", "lv_obj_set_height")]
    [InlineData("Opacity", "lv_obj_set_style_opa")]
    [InlineData("Radius", "lv_obj_set_style_radius")]
    [InlineData("BorderWidth", "lv_obj_set_style_border_width")]
    [InlineData("Value", "lv_bar_set_value")]
    public void Animation_PropertyName_MapsToExecCallback(string property, string expectedCb)
    {
        var code = GenAnim(Anim(a => a.PropertyName = property));
        CSource.AssertCode(code, $"lv_anim_set_exec_cb(&a, (lv_anim_exec_xcb_t){expectedCb});");
    }

    [Fact]
    public void Animation_UnknownProperty_FallsBackToSetX()
    {
        var code = GenAnim(Anim(a => a.PropertyName = "NotAProperty"));
        CSource.AssertCode(code, "lv_anim_set_exec_cb(&a, (lv_anim_exec_xcb_t)lv_obj_set_x);");
    }

    [Fact]
    public void Animation_AllDisabled_EmitsPlaceholderOnly()
    {
        var code = GenAnim(Anim(a => a.IsEnabled = false));
        CSource.AssertText(code, "// No animations configured.");
        CSource.AssertNoText(code, "lv_anim_start");
    }

    [Fact]
    public void Animation_EmptyList_EmitsPlaceholderOnly()
    {
        var code = new CodeGenerator().GenerateAnimationCode(Array.Empty<AnimationConfig>());
        CSource.AssertText(code, "// No animations configured.");
    }

    [Fact]
    public void Animation_NonOnLoadTrigger_CallsStartOnEventInsteadOfApply()
    {
        var code = GenAnim(Anim(a => a.Trigger = "OnClick"));
        CSource.AssertText(code, "/* OnClick: call start_anim_an1(obj_c1) on event */");
        CSource.AssertNoCode(code, "start_anim_an1(obj_c1);");   // apply_animations 里不直接启动
    }

    [Fact]
    public void Animation_OnLoadTrigger_CalledFromApplyAnimations()
    {
        var code = GenAnim(Anim());
        CSource.AssertCode(code, "start_anim_an1(obj_c1);");
    }

    // ───────────────────────── 趋势图：chart 骨架 ─────────────────────────

    [Fact]
    public void Trend_EmitsChartHandlesAndSeriesArray()
    {
        var code = GenTrend(Chart());

        CSource.AssertText(code, "static lv_obj_t* _chart_ch1 = NULL;");
        CSource.AssertText(code, "static lv_chart_series_t* _series_ch1[1] = {0};");
    }

    [Fact]
    public void Trend_EmitsLineChartTypeAndPointCount()
    {
        var code = GenTrend(Chart(c => c.MaxPoints = 300));

        CSource.AssertCode(code, "lv_chart_set_type(_chart_ch1, LV_CHART_TYPE_LINE);");
        CSource.AssertCode(code, "lv_chart_set_point_count(_chart_ch1, 300);");
    }

    [Fact]
    public void Trend_Series_IsBoundToPrimaryYAxis()
    {
        // lv_chart_set_next_value 要求有效 series；轴必须是 PRIMARY_Y
        var code = GenTrend(Chart());
        CSource.AssertCode(code,
            "lv_chart_add_series(_chart_ch1, lv_color_hex(0x38bdf8), LV_CHART_AXIS_PRIMARY_Y);");
    }

    [Fact]
    public void Trend_XAxisRange_UsesPrimaryXAxisAndMaxPoints()
    {
        var code = GenTrend(Chart(c => c.MaxPoints = 450));
        CSource.AssertCode(code, "lv_chart_set_range(_chart_ch1, LV_CHART_AXIS_PRIMARY_X, 0, 450);");
    }

    [Fact]
    public void Trend_ShowGrid_EmitsDivLineCount()
    {
        var code = GenTrend(Chart(c => c.ShowGrid = true));
        CSource.AssertCode(code, "lv_chart_set_div_line_count(_chart_ch1, 5, 5);");
    }

    [Fact]
    public void Trend_NoShowGrid_EmitsNoDivLineCount()
    {
        var code = GenTrend(Chart(c => c.ShowGrid = false));
        CSource.AssertNoText(code, "lv_chart_set_div_line_count(_chart_ch1,");
    }

    [Fact]
    public void Trend_BgColor_IsAppliedToChartBackground()
    {
        var code = GenTrend(Chart(c => c.BgColor = "#101820"));
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(_chart_ch1, lv_color_hex(0x101820), 0);");
    }

    [Fact]
    public void Trend_NoBgColor_EmitsNoBackgroundColor()
    {
        var code = GenTrend(Chart(c => c.BgColor = null));
        CSource.AssertNoText(code, "lv_obj_set_style_bg_color(_chart_ch1,");
    }

    [Fact]
    public void Trend_DisabledChart_IsSkippedEntirely()
    {
        var code = GenTrend(Chart(c => c.IsEnabled = false));
        CSource.AssertNoText(code, "_chart_ch1");
        CSource.AssertNoText(code, "trend_init_ch1");
    }

    [Fact]
    public void Trend_DisabledPens_AreExcludedFromSeriesArrayAndCalls()
    {
        var code = GenTrend(Chart(c =>
        {
            c.Pens.Add(new TrendPen { Label = "禁用笔", VariableId = "v2", Color = "#ff0000", IsEnabled = false });
        }));

        // 只有 1 支启用笔 → series 数组长度 1，且不发射第二笔
        CSource.AssertText(code, "static lv_chart_series_t* _series_ch1[1] = {0};");
        CSource.AssertNoText(code, "lv_color_hex(0xff0000)");
    }

    [Fact]
    public void Trend_EmitsInitAndUpdateEntryPoints()
    {
        var code = GenTrend(Chart());
        CSource.AssertText(code, "void trend_init_ch1(lv_obj_t* parent) {");
        CSource.AssertText(code, "void trend_update_ch1(float* values, uint8_t count) {");
        CSource.AssertCode(code, "lv_chart_refresh(_chart_ch1);");
    }

    [Fact]
    public void Trend_ChartFillsParent()
    {
        var code = GenTrend(Chart());
        CSource.AssertCode(code, "lv_obj_set_size(_chart_ch1, LV_PCT(100), LV_PCT(100));");
    }
}
