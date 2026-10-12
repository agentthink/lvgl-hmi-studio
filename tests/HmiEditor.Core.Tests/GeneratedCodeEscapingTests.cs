using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 回归测试：生成 C 代码的字面量 / 格式串硬化。
///
/// 覆盖 2026-09-13 审查报告 P0-5 / P0-6：
///   1. 变量单位进入 printf 格式串时必须把 '%' 转义为 '%%'（否则 "%.2f%" 非法 / UB）；
///   2. 屏幕名、动作目标、报警名、脚本名等用户文本必须转义（引号/反斜杠/换行/'*/'）；
///   3. 标签表浮点字面量必须 InvariantCulture 且不产出非法的 "0f"；
///   4. canvas 缓冲必须按字节（LV_CANVAS_BUF_SIZE）分配，且不得使用 LVGL 9.x 不存在的 API。
/// </summary>
public class GeneratedCodeEscapingTests
{
    private const string VarId = "var_t";

    private static HmiProject MakeProject(string unit, string lvglVersion = "9.5")
    {
        var project = new HmiProject { Name = "Escaping", LvglVersion = lvglVersion };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl_t", Name = "温度", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 40, Text = "温度"
        });
        scr.Controls[0].VariableBindings.Add(new VariableBindingData
        {
            VariableId = VarId, Direction = BindingDirection.ReadOnly, PropertyName = "Text"
        });

        var varMgr = project.VariableManager;
        varMgr.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        varMgr.Variables.Add(new Variable
        {
            Id = VarId, Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = unit
        });
        return project;
    }

    // ── 1. 单位中的 '%' 必须转义为 '%%' ──────────────────────────────────────

    [Fact]
    public void LabelUnit_Percent_IsEscapedInSimulatorPath()
    {
        var code = new CodeGenerator().GenerateSimulatorModule(MakeProject("%"), null);

        // 小数位统一来自 ValueDecimalPlaces（默认 2），与设计器预览一致
        CSource.AssertCode(code, "lv_label_set_text_fmt(obj_lbl_t, \"%.2f%%\", ((double)(v)));");
        // 未转义的 "%.2f%" 是非法转换说明符，绝不允许出现在生成代码里
        CSource.AssertNoCode(code, "\"%.2f%\", ((double)(v))");
    }

    [Fact]
    public void LabelUnit_Percent_IsEscapedInDevicePath()
    {
        var code = new CodeGenerator().GenerateSharedLibraryProject(MakeProject("%"));

        Assert.Contains("\"%.2f%%\"", code);
        CSource.AssertNoCode(code, "\"%.2f%\", ((double)(v))");
    }

    [Fact]
    public void LabelUnit_WithExtraFormatSpecifiers_IsNeutralized()
    {
        // 单位写成 "%s%n" 时，旧实现会读取不存在的变参（%s）甚至写内存（%n）
        var code = new CodeGenerator().GenerateSimulatorModule(MakeProject("%s%n"), null);

        Assert.Contains("\"%.2f%%s%%n\"", code);
        Assert.DoesNotContain("\"%.2f%s%n\"", code);
    }

    // ── 2. 用户文本的 C 字符串 / 注释转义 ─────────────────────────────────────

    [Fact]
    public void ScreenName_WithQuotesAndBackslash_IsEscapedInScreenTable()
    {
        var project = MakeProject("℃");
        project.Screens[0].Name = "He said \"hi\"\\path";

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.Contains("\"He said \\\"hi\\\"\\\\path\"", code);
        Assert.DoesNotContain("He said \"hi\"\\path", code);
    }

    [Fact]
    public void AlarmName_WithPercentAndQuote_IsEscapedInTraceFormatString()
    {
        var group = new AlarmGroup
        {
            Id = "g1",
            Name = "组",
            Alarms =
            {
                new AlarmConfig
                {
                    Id = "a1", Name = "温度%高\"报警", IsEnabled = true,
                    Condition = new AlarmCondition
                    {
                        VariableId = VarId, Type = AlarmConditionType.HighLimit, Threshold = 80
                    }
                }
            }
        };

        var code = new CodeGenerator().GenerateAlarmCode(new List<AlarmGroup> { group });

        Assert.Contains("温度%%高\\\"报警", code);
    }

    [Fact]
    public void ScriptName_WithCommentTerminator_DoesNotBreakComment()
    {
        var scripts = new List<HmiScript>
        {
            new HmiScript { Id = "s1", Name = "evil*/int x;/*" }
        };

        var code = new ScriptDispatchCodeGen().Generate(scripts);

        // "*/" 被中和，块注释不会被提前结束
        Assert.DoesNotContain("evil*/int x;/*", code);
        Assert.Contains("evil* /int x;/*", code);
    }

    // ── 3. 标签表浮点字面量 ──────────────────────────────────────────────────

    [Fact]
    public void RuntimeTags_FloatLiterals_AreInvariantAndNeverIllegalZeroSuffix()
    {
        var tags = new List<RuntimeTag>
        {
            new RuntimeTag { VariableId = "v1", Address = "40001", RawAddress = "40001", Scale = 0.5, Offset = 0 },
            new RuntimeTag { VariableId = "v2", Address = "40002", RawAddress = "40002", Scale = 0.00001, Offset = 0 },
        };

        var previous = CultureInfo.CurrentCulture;
        try
        {
            // 逗号小数区域：旧实现会产出 "0,5f"（非法 C 字面量）
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var code = new RuntimeTagsCodeGen().Generate(tags);

            Assert.Contains("0.5f", code);
            Assert.DoesNotContain("0,5f", code);
            // 旧实现 "0.###" 会把 1e-5 变成 "0" → 拼出非法的 "0f"
            Assert.Contains("1E-05f", code);
            Assert.DoesNotContain(" 0f,", code);
            Assert.DoesNotContain("{0f", code);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void RuntimeTags_EscapesIdAndAddress()
    {
        var tags = new List<RuntimeTag>
        {
            new RuntimeTag { VariableId = "v \"q\"", Address = "1:40001", RawAddress = "1:40001" },
        };

        var code = new RuntimeTagsCodeGen().Generate(tags);

        Assert.Contains("\"v \\\"q\\\"\"", code);
    }

    // ── 4. canvas 缓冲与 API 契约（设备 .so 路径） ───────────────────────────

    [Fact]
    public void SharedLibraryProject_CanvasControls_UseByteBuffersAndExistingApis()
    {
        var project = MakeProject("℃");
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "svg1", Name = "SVG", Type = HmiControlType.Svg,
            X = 10, Y = 300, Width = 100, Height = 80,
            SvgViewBoxWidth = 100, SvgViewBoxHeight = 100,
            SvgShapes = new List<SvgShape>
            {
                new SvgShape { Kind = SvgShapeKind.Path, PathData = "M0 0 L50 0 L25 50 Z", Fill = "#123456" }
            }
        });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "tank1", Name = "Tank", Type = HmiControlType.Tank,
            X = 130, Y = 300, Width = 80, Height = 120, Value = 50
        });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "ell1", Name = "Ellipse", Type = HmiControlType.Ellipse,
            X = 230, Y = 300, Width = 100, Height = 60, BgColor = "#334155"
        });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 缓冲按字节分配（lv_color_t 在 LVGL 9.x 是 3 字节，ARGB8888 需 4 B/px）
        CSource.AssertCode(code, "static uint8_t buf_svg1[LV_CANVAS_BUF_SIZE(100, 80, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        CSource.AssertCode(code, "static uint8_t _buf_tank1[LV_CANVAS_BUF_SIZE(80, 120, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        CSource.AssertCode(code, "static uint8_t _shapebuf_ell1[LV_CANVAS_BUF_SIZE(100, 60, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");

        // LVGL 9.x 不存在的 API 一律不得出现
        Assert.DoesNotContain("lv_canvas_set_px_opa", code);
        Assert.DoesNotContain("lv_draw_polygon", code);
        CSource.AssertCode(code, "lv_canvas_fill_bg(obj_svg1, lv_color_black(), LV_OPA_TRANSP);");
        CSource.AssertCode(code, "lv_draw_triangle(&layer_svg1, &dsc);");

        // LVGL 9.x 的 lv_draw_line / lv_draw_arc 只接受 (layer, dsc)
        Assert.DoesNotContain(", pts, ", code);
        Assert.DoesNotContain("&dsc, lv_point_precise_t", code);
    }

    // ── 5. 过程控件更新函数的作用域 ──────────────────────────────────────────
    [Fact]
    public void ProcessControl_UpdateFunction_IsEmittedInFileScope()
    {
        var tank = new HmiControl { Id = "tk1", Type = HmiControlType.Tank, Width = 80, Height = 120, Value = 50 };

        // 拆分方法本身：canvas 代码不得包含函数定义
        Assert.DoesNotContain("static void update_", ProcessControlCodeGen.GenerateCanvasCode(tank));
        Assert.Contains("static void update_tank_tk1", ProcessControlCodeGen.GenerateUpdateFunction(tank));

        // 端到端：update_* 必须出现在 create_screen_*() 之前（文件作用域）。
        // 放进函数体内会成为嵌套函数定义，GCC 报 "存储类无效"。
        var project = MakeProject("℃");
        project.Screens[0].Controls.Add(tank);
        var code = new CodeGenerator().GenerateScreenCode(project.Screens[0]);

        var idxCreate = code.IndexOf("static lv_obj_t* create_screen_scr1_inner", System.StringComparison.Ordinal);
        var idxUpdate = code.IndexOf("static void update_tank_tk1", System.StringComparison.Ordinal);
        Assert.True(idxCreate >= 0, "未找到 create_screen 函数");
        Assert.True(idxUpdate >= 0, "未找到 update_tank_tk1 定义");
        Assert.True(idxUpdate < idxCreate, "update_tank_* 必须在 create_screen_* 之前的文件作用域");
    }

    // ── 6. 生成物追溯戳（**只有生成器版本号**） ─────────────────────────────

    /// <summary>
    /// ⚠ 2026-10-11（阶段 317，用户拍板 ④=1）：本判据原来叫
    /// <c>GeneratedCode_CarriesGeneratorVersionAndCommit</c>，断言生成代码带
    /// <c>(git &lt;短哈希&gt;)</c>。改成"**只留版本号**"是因为设备侧产物是**入库**的：
    /// 戳里带 commit ⇒ 每次提交都改产物，"可再导出"就没法用逐字节相等来判、diff 永远有噪声。
    /// **判据跟着决定走**：现在反过来钉住"**不许**出现 commit 段"，把这条件变成机械保证。
    /// </summary>
    [Fact]
    public void GeneratedCode_CarriesGeneratorVersionOnly_NoCommitHash()
    {
        var sim = new CodeGenerator().GenerateSimulatorModule(MakeProject("℃"), null);
        Assert.Contains("// Generator: HmiEditor.CodeGen", sim);
        var so = new CodeGenerator().GenerateSharedLibraryProject(MakeProject("℃"));
        Assert.Contains("// Generator: HmiEditor.CodeGen", so);

        foreach (var (name, code) in new[] { ("sim", sim), ("so", so) })
        {
            var line = code.Split('\n').First(l => l.StartsWith("// Generator:", StringComparison.Ordinal));
            Assert.DoesNotContain("(git ", line);
            // SDK 会把 commit 拼进 InformationalVersion（`1.0.0+<sha>`）⇒ 那一截也不许漏出来
            Assert.DoesNotContain("+", line);
            // 面自检：版本号本身要在（否则"没有 commit"可能只是因为整行被删了）
            var ver = line["// Generator: HmiEditor.CodeGen ".Length..].Trim();
            Assert.False(string.IsNullOrWhiteSpace(ver), $"{name}: Generator 行没有版本号：{line}");
        }
    }

    // ── 7. 画布几何的小数字面量（阶段 120） ──────────────────────────────────

    [Fact]
    public void ScreenGeometry_FractionalCoordinates_AreInvariantUnderCommaDecimalCulture()
    {
        // `HmiControl.X/Y/Width/Height` 与 `HmiScreen.Width/Height` 都是 double。
        // 旧实现直接 `$"{value}"` ⇒ 小数点跟随进程区域设置：de-DE 下生成
        // `lv_obj_set_pos(obj, 12,5, 30)`，**实参个数都变了**、C 编译不过。
        var project = new HmiProject { Name = "Geometry" };
        var screen = new HmiScreen { Id = "scr1", Name = "Main", Width = 800.5, Height = 480.25 };
        screen.Controls.Add(new HmiControl
        {
            Id = "lbl_g", Name = "几何", Type = HmiControlType.Label,
            X = 12.5, Y = 30.25, Width = 100.5, Height = 40.75, Text = "几何"
        });
        project.Screens.Add(screen);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var code = new CodeGenerator().GenerateScreenCode(screen);

            Assert.Contains("12.5", code);
            Assert.Contains("30.25", code);
            Assert.Contains("800.5", code);
            Assert.DoesNotContain("12,5", code);
            Assert.DoesNotContain("480,25", code);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
