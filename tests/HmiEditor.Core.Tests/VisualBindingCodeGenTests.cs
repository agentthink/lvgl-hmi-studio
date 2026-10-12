using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// P1-1：视觉属性绑定（颜色 / 可见性）的**生成端**行为。
///
/// 这里锁三件事：
/// <list type="number">
///   <item>两端（<c>sim_refresh_bindings</c> / <c>hmi_ui_refresh_timer_cb</c>）都真的发射视觉调用 ——
///     不能出现「设计器变了、设备没变」的半成品；</item>
///   <item>值来源复用**既有**取值机制（设备 <c>get_tag_value(TAG_x)</c>、模拟器局部正弦波），
///     不引入新的运行时取值函数（那会给设备链路加新的 extern 定义面）；</item>
///   <item>负控：值属性绑定 / 语法错 / 未替换的 Faceplate 占位符都**不得**进入视觉通路。</item>
/// </list>
/// </summary>
public class VisualBindingCodeGenTests
{
    // ── 正向：设备端发射 ──

    [Fact]
    public void Device_TextColorExpression_EmitsLvglStyleCall()
    {
        var project = ProjectWithVisualBinding("TextColor", "temp > 100 ? 16711680 : 65280");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        CSource.AssertCode(device, "lv_obj_set_style_text_color(obj_lamp, lv_color_hex((uint32_t)(v)), 0);");
        Assert.Contains("get_tag_value(TAG_temp)", device);
        Assert.Contains("16711680", device);
        Assert.Contains("65280", device);
        // 设备端控件可能尚未创建 —— 视觉语句同样要套空指针守卫
        Assert.Contains("if (obj_lamp) {", device);
    }

    [Fact]
    public void Device_BgColorExpression_AlsoSetsOpacity()
    {
        // 只设颜色不设 opa，Label 这类默认 bg_opa=0 的控件在设备上"颜色生效了但看不见"。
        var project = ProjectWithVisualBinding("BgColor", "temp > 100 ? 16711680 : 65280");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        CSource.AssertCode(device, "lv_obj_set_style_bg_color(obj_lamp, lv_color_hex((uint32_t)(v)), 0);");
        CSource.AssertCode(device, "lv_obj_set_style_bg_opa(obj_lamp, LV_OPA_COVER, 0);");
    }

    [Fact]
    public void Device_VisibilityExpression_EmitsFlagToggle()
    {
        var project = ProjectWithVisualBinding("IsVisible", "temp > 50");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        CSource.AssertCode(device, "lv_obj_clear_flag(obj_lamp, LV_OBJ_FLAG_HIDDEN)");
        CSource.AssertCode(device, "lv_obj_add_flag(obj_lamp, LV_OBJ_FLAG_HIDDEN)");
    }

    // ── 正向：模拟器端同样发射（否则模拟器里视觉绑定是死的）──

    [Fact]
    public void Simulator_TextColorExpression_UsesLocalSineVariables()
    {
        var project = ProjectWithVisualBinding("TextColor", "temp > 100 ? 16711680 : 65280");

        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);

        CSource.AssertCode(sim, "lv_obj_set_style_text_color(obj_lamp, lv_color_hex((uint32_t)(v)), 0);");
        // 模拟器没有真实值来源 —— 表达式引用的变量退化为局部正弦波（与变量绑定同口径）
        Assert.Contains("float _bxv_temp = ", sim);
        Assert.Contains("sinf(t +", sim);
    }

    [Fact]
    public void Simulator_VisibilityExpression_EmitsFlagToggle()
    {
        var project = ProjectWithVisualBinding("IsVisible", "temp > 50");

        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);

        CSource.AssertCode(sim, "lv_obj_clear_flag(obj_lamp, LV_OBJ_FLAG_HIDDEN)");
        CSource.AssertCode(sim, "lv_obj_add_flag(obj_lamp, LV_OBJ_FLAG_HIDDEN)");
    }

    // ── 对拍：两端视觉调用必须由同一实现产出 ──

    [Theory]
    [InlineData("TextColor", "lv_obj_set_style_text_color(obj_lamp")]
    [InlineData("BgColor", "lv_obj_set_style_bg_color(obj_lamp")]
    [InlineData("IsVisible", "lv_obj_add_flag(obj_lamp, LV_OBJ_FLAG_HIDDEN)")]
    public void BothPaths_EmitTheSameVisualCall(string property, string marker)
    {
        var project = ProjectWithVisualBinding(property, "temp > 1 ? 16711680 : 65280");

        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.Contains(marker, sim);
        Assert.Contains(marker, device);
    }

    // ── 设计时与生成期同规则 ──

    [Fact]
    public void GeneratedColorAndDesignerColor_ShareTheSameNumericRule()
    {
        // 生成期：lv_color_hex((uint32_t)(expr))；设计时：ColorFromNumber(expr)。
        // 同一个数值必须落到同一个颜色，否则就是「设计器显示绿、设备显示红」那类漂移。
        var project = ProjectWithVisualBinding("TextColor", "temp > 100 ? 16711680 : 65280");
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        CSource.AssertCode(device, "lv_color_hex((uint32_t)(");
        Assert.Equal("#FF0000", BindingVisualProperty.ColorFromNumber(16711680d));
        Assert.Equal("#00FF00", BindingVisualProperty.ColorFromNumber(65280d));
    }

    // ── 负控 ──

    [Fact]
    public void ValuePropertyBinding_DoesNotEmitVisualCalls()
    {
        // Text 是值属性 —— 走值通路（lv_label_set_text_fmt），不得被视觉发射污染。
        var project = ProjectWithVisualBinding("Text", "temp > 100 ? 1 : 0");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.DoesNotContain("lv_obj_set_style_text_color(obj_lamp", device);
        Assert.DoesNotContain("LV_OBJ_FLAG_HIDDEN", device);
    }

    [Fact]
    public void InvalidExpression_DoesNotEmitVisualCalls()
    {
        var project = ProjectWithVisualBinding("TextColor", "temp >");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.DoesNotContain("lv_obj_set_style_text_color(obj_lamp", device);
    }

    [Fact]
    public void UnresolvedFaceplatePlaceholder_DoesNotEmitVisualCalls()
    {
        // Faceplate 参数未替换时表达式里还留着 {{arg}} —— 发出去就是错的 C。
        var project = ProjectWithVisualBinding("TextColor", "{{arg}} > 100 ? 16711680 : 65280");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.DoesNotContain("lv_obj_set_style_text_color(obj_lamp", device);
    }

    [Fact]
    public void EmptyExpression_DoesNotEmitVisualCalls()
    {
        var project = ProjectWithVisualBinding("TextColor", "");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.DoesNotContain("lv_obj_set_style_text_color(obj_lamp", device);
    }

    // ── helper ──

    private static HmiProject ProjectWithVisualBinding(string property, string expression)
    {
        var project = new HmiProject { Name = "Visual", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "temp", Name = "temp", Address = "40001", DataType = "Float", ConnectionId = "c1",
        });

        scr.Controls.Add(new HmiControl
        {
            Id = "lamp", Name = "lamp", Type = HmiControlType.Label,
            X = 0, Y = 0, Width = 120, Height = 30,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = property,
                    Kind = BindingSourceKind.Expression,
                    Expression = expression,
                    Direction = BindingDirection.ReadOnly,
                }
            }
        });
        return project;
    }
}
