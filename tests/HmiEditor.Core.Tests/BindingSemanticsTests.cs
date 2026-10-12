using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 回归测试：<see cref="BindingSemantics"/> 作为设计器 / PC 模拟器 / 固件生成代码的
/// 唯一事实源（2026-09-13 审查 P1-1）。
///
/// 覆盖：缩放、小数位、前后缀（含变量单位兜底）、布尔判真、以及"设计器显示 == 生成代码格式"。
/// </summary>
public class BindingSemanticsTests
{
    private static HmiControl BoundLabel(string variableId, string text = "值")
        => new()
        {
            Id = "lbl", Name = "标签", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 40, Text = text,
            VariableBindings = { new VariableBindingData { VariableId = variableId, Direction = BindingDirection.ReadOnly, PropertyName = "Text" } },
        };

    private static HmiProject ProjectWith(HmiControl control, string dataType, string? unit)
    {
        var project = new HmiProject { Name = "Semantics", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        scr.Controls.Add(control);
        project.Screens.Add(scr);
        project.VariableManager.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = control.VariableBindings[0].VariableId, Name = "v", Address = "40001",
            DataType = dataType, ConnectionId = "c1", Unit = unit,
        });
        return project;
    }

    // ── 1. 小数位 / 缩放 / 前后缀：设计器与生成代码一致 ────────────────────────

    [Fact]
    public void Float_UsesDecimalPlaces_ForBothDesignerAndGeneratedCode()
    {
        var control = BoundLabel("v1");
        control.ValueDecimalPlaces = 2;
        var project = ProjectWith(control, "Float", "℃");

        // 设计器
        var display = PreviewValueResolver.FormatDisplayValue(control, 12.3, "Float", "℃");
        Assert.Equal("12.30℃", display);

        // 生成代码（模拟器路径）
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        CSource.AssertCode(code, "lv_label_set_text_fmt(obj_lbl, \"%.2f℃\", ((double)(v)));");
    }

    [Fact]
    public void DecimalPlaces_AreHonored()
    {
        var control = BoundLabel("v1");
        control.ValueDecimalPlaces = 3;
        var project = ProjectWith(control, "Float", null);

        Assert.Equal("12.346", PreviewValueResolver.FormatDisplayValue(control, 12.3456, "Float"));
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        Assert.Contains("\"%.3f\"", code);
    }

    [Fact]
    public void ScaleFactor_IsAppliedInDesignerAndCode()
    {
        var control = BoundLabel("v1");
        control.ValueScaleFactor = 0.1;
        control.ValueDecimalPlaces = 1;
        var project = ProjectWith(control, "Float", null);

        Assert.Equal("12.3", PreviewValueResolver.FormatDisplayValue(control, 123, "Float"));
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        CSource.AssertCode(code, "lv_label_set_text_fmt(obj_lbl, \"%.1f\", ((double)(v) * 0.1f));");
    }

    [Fact]
    public void ValueSuffix_OverridesVariableUnit()
    {
        var control = BoundLabel("v1");
        control.ValueSuffix = "kPa";
        var project = ProjectWith(control, "Float", "℃");

        Assert.Equal("12.30kPa", PreviewValueResolver.FormatDisplayValue(control, 12.3, "Float", "℃"));
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        Assert.Contains("\"%.2fkPa\"", code);
    }

    [Fact]
    public void VariableUnit_IsFallbackSuffix_InDesignerAndCode()
    {
        var control = BoundLabel("v1");
        var project = ProjectWith(control, "Float", "%");

        Assert.Equal("12.30%", PreviewValueResolver.FormatDisplayValue(control, 12.3, "Float", "%"));
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        // 单位 '%' 必须转义为 '%%'，否则 "%.2f%" 是非法转换说明符
        Assert.Contains("\"%.2f%%\"", code);
    }

    // ── 2. 前后缀（无变量单位时） ────────────────────────────────────────────

    [Fact]
    public void PrefixAndSuffix_AreApplied()
    {
        var control = BoundLabel("v1");
        control.ValuePrefix = "≈";
        control.ValueSuffix = " rpm";
        Assert.Equal("≈12.30 rpm", PreviewValueResolver.FormatDisplayValue(control, 12.3, "Float"));
    }

    // ── 3. 布尔判真：设计器与生成代码同一阈值 ────────────────────────────────

    [Fact]
    public void Bool_NonZeroIsOn_InDesignerAndCode()
    {
        var control = BoundLabel("v1");
        var sem = BindingSemantics.For(control, "Bool");

        Assert.True(sem.IsOn(0.3));
        Assert.False(sem.IsOn(0.0));
        Assert.Equal("ON", sem.FormatDisplay(0.3));
        Assert.Equal("OFF", sem.FormatDisplay(0.0));

        // Led 生成代码使用同一阈值（!= 0.0f），不再硬编码 > 0.5f
        var led = new HmiControl
        {
            Id = "led1", Name = "灯", Type = HmiControlType.Led,
            X = 0, Y = 0, Width = 20, Height = 20,
            VariableBindings = { new VariableBindingData { VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value" } },
        };
        var project = ProjectWith(led, "Bool", null);
        var code = new CodeGenerator().GenerateSimulatorModule(project, null);
        CSource.AssertCode(code, "lv_led_set_brightness(obj_led1, (v) != 0.0f ? 255 : 0);");
        Assert.DoesNotContain("0.5f) ? 255", code);
    }

    // ── 4. 整数类型 ─────────────────────────────────────────────────────────

    [Fact]
    public void Int_WithUnitScale_RoundsToInteger()
    {
        var control = BoundLabel("v1");
        var sem = BindingSemantics.For(control, "Int16");
        Assert.Equal(BindingDisplayKind.Int, sem.Kind);
        Assert.Equal("13", sem.FormatDisplay(12.6));
        Assert.Equal("%.0f", sem.CConversion);
    }

    // ── 5. Auto 兜底：无数据类型时按控件类型 ─────────────────────────────────

    [Fact]
    public void Auto_FallsBackToControlType()
    {
        var label = BoundLabel("v1");
        Assert.Equal(BindingDisplayKind.Float, BindingSemantics.For(label, null).Kind);
        Assert.Equal(2, BindingSemantics.For(label, null).Decimals);

        var bar = new HmiControl { Id = "b1", Type = HmiControlType.Bar };
        Assert.Equal(1, BindingSemantics.For(bar, null).Decimals);

        var slider = new HmiControl { Id = "s1", Type = HmiControlType.Slider };
        Assert.Equal(BindingDisplayKind.Int, BindingSemantics.For(slider, null).Kind);
    }

    // ── 6. 格式串字面量硬化 ─────────────────────────────────────────────────

    [Theory]
    [InlineData("%", "%.2f%%")]
    [InlineData("%RH", "%.2f%%RH")]
    [InlineData("℃", "%.2f℃")]
    [InlineData(null, "%.2f")]
    public void CFormatLiteral_EscapesPercent(string? unit, string expected)
    {
        var control = BoundLabel("v1");
        var sem = BindingSemantics.For(control, "Float", unit);
        Assert.Equal(expected, sem.CFormatLiteral());
    }

    // ── 7. 设备端（.so）路径与模拟器路径一致 ─────────────────────────────────

    [Fact]
    public void DevicePath_UsesSameSemanticsAsSimulator()
    {
        var control = BoundLabel("v1");
        control.ValueDecimalPlaces = 1;
        control.ValueScaleFactor = 2.0;
        var project = ProjectWith(control, "Float", "kPa");

        var device = new CodeGenerator().GenerateSharedLibraryProject(project);
        Assert.Contains("\"%.1fkPa\"", device);
        Assert.Contains("* 2.0f", device);
    }
}
