using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **屏幕 DPI ⇒ 生成几何的整体换算**（阶段 228，登记 `#98` 的"进导出"第二半）。
///
/// <para><b>语义</b>：`DpiScale` 是"逻辑像素 → 设备像素"的倍率 ⇒ 生成物里**几何量**（坐标、尺寸、
/// 折线点、字号）一律乘它；**语义数值**（值比例 `scale=`、阈值、动画时长）**不乘**。
/// 默认 1.0 ⇒ 与不换算**逐字节相同**（这一条是整个既有测试套仍然全绿的原因）。</para>
/// </summary>
public class DpiScaleExportTests
{
    private static HmiScreen ScreenWith(params HmiControl[] controls)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        foreach (var c in controls) screen.Controls.Add(c);
        return screen;
    }

    private static HmiControl Ctl(string id, double x = 10, double y = 20, double w = 100, double h = 40)
        => new() { Id = id, Name = id, Type = HmiControlType.Rectangle, X = x, Y = y, Width = w, Height = h };

    private static HmiProject ProjectWithDpi(double dpi)
    {
        var project = new HmiProject { Name = "P" };
        project.ScreenDefaultsConfig.DpiScale = dpi;
        return project;
    }

    private static string Gen(HmiScreen screen, HmiProject? project, params string[] fontIds)
        => new CodeGenerator().GenerateScreenCode(screen, null, null, null, project, null,
            fontIds.Length == 0 ? null : new HashSet<string>(fontIds, StringComparer.Ordinal));

    /// <summary>**地基**：默认 DPI（1.0）或无工程 ⇒ 与从前逐字节相同。</summary>
    [Fact]
    public void DefaultDpi_ChangesNothing()
    {
        var screen = ScreenWith(Ctl("c1"), Ctl("c2", 700, 400), Ctl("c3", 5, 5, w: 900, h: 600));
        var withoutProject = Gen(screen, null);

        Assert.Equal(withoutProject, Gen(screen, ProjectWithDpi(1.0)));
        Assert.Equal(withoutProject, Gen(screen, new HmiProject { Name = "Z" }));
        CSource.AssertCode(withoutProject, "lv_obj_set_pos(obj_c1, 10, 20);");
        CSource.AssertCode(withoutProject, "lv_obj_set_size(obj_c1, 100, 40);");
    }

    /// <summary>几何（位置/尺寸）按 DPI 换算。</summary>
    [Fact]
    public void DpiScale_ScalesControlGeometry()
    {
        var code = Gen(ScreenWith(Ctl("c1")), ProjectWithDpi(1.5));

        CSource.AssertCode(code, "lv_obj_set_pos(obj_c1, 15, 30);");
        CSource.AssertCode(code, "lv_obj_set_size(obj_c1, 150, 60);");
    }

    /// <summary>屏幕尺寸与背景尺寸也跟着换算（设备分辨率变了）。</summary>
    [Fact]
    public void DpiScale_ScalesScreenSize()
    {
        var code = Gen(ScreenWith(Ctl("c1")), ProjectWithDpi(2.0));

        CSource.AssertCode(code, "lv_obj_set_size(screen, 1600, 960);");
    }

    /// <summary>字号是几何量：**内置档位**在换算后比较（20 × 1.5 ＝ 30 &gt; 主题 14 × 1.5 ＝ 21 ⇒ 取 16）。</summary>
    [Fact]
    public void DpiScale_ScalesFontSizeForBuiltInFallback()
    {
        var control = Ctl("c1");
        control.Type = HmiControlType.Label;
        control.FontSize = 20;

        var code = Gen(ScreenWith(control), ProjectWithDpi(1.5));

        CSource.AssertText(code, "#if LV_FONT_MONTSERRAT_16");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_c1, &lv_font_montserrat_16, 0);");
    }

    /// <summary>有字体资产时，**变体名里的字号**也要换算（`font_x_20` → `font_x_30`）。</summary>
    [Fact]
    public void DpiScale_ScalesFontAssetVariantName()
    {
        var control = Ctl("c1");
        control.Type = HmiControlType.Label;
        control.FontFamily = "myfont";
        control.FontSize = 20;

        var code = Gen(ScreenWith(control), ProjectWithDpi(1.5), "myfont");

        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_c1, &font_myfont_30, 0);");
    }

    /// <summary>折线点是几何量，也要换算。</summary>
    [Fact]
    public void DpiScale_ScalesPolylinePoints()
    {
        var control = new HmiControl
        {
            Id = "pl1", Name = "pl1", Type = HmiControlType.Polyline,
            X = 10, Y = 20, Width = 100, Height = 40,
            Points = new List<PointData>
            {
                new(0, 0),
                new(40, 20),
            },
        };

        var code = Gen(ScreenWith(control), ProjectWithDpi(2.0));

        CSource.AssertText(code, "{ 0, 0 }");
        CSource.AssertText(code, "{ 80, 40 }");
    }

    /// <summary>脏数据（DPI ≤ 0）按 1 处理 —— 既不出现负尺寸，也不出现 `Infinity`。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    public void NonPositiveDpi_IsTreatedAsOne(double dpi)
    {
        var code = Gen(ScreenWith(Ctl("c1")), ProjectWithDpi(dpi));

        CSource.AssertCode(code, "lv_obj_set_pos(obj_c1, 10, 20);");
        CSource.AssertCode(code, "lv_obj_set_size(obj_c1, 100, 40);");
        Assert.DoesNotContain("Infinity", code, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// **语义数值不换算**：`ValueScaleFactor` 是"值 → 显示值"的比例（不是像素），
    /// DPI 2× 时它必须原样出现在注释里。
    /// </summary>
    [Fact]
    public void SemanticNumbers_AreNotScaled()
    {
        var control = Ctl("c1");
        control.Type = HmiControlType.Slider;
        control.ValueScaleFactor = 2;
        control.VariableBindingId = "v1";

        var code = Gen(ScreenWith(control), ProjectWithDpi(2.0));

        CSource.AssertText(code, "scale=2,");
        CSource.AssertNoText(code, "scale=4,");
    }

    /// <summary>安全区（阶段 227）与 DPI 同时启用：**先在设计空间吸附，再按 DPI 换算**。</summary>
    [Fact]
    public void SafeAreaAndDpi_ComposeInTheRightOrder()
    {
        var project = ProjectWithDpi(1.5);
        project.ScreenDefaultsConfig.SafeAreaPadding = new ScreenPadding { Left = 10, Top = 20, Right = 30, Bottom = 40 };

        var screen = ScreenWith(Ctl("outside", 5, 5));

        var code = Gen(screen, project);

        // 设计空间：吸附 (5,5) → (10,20)；设备空间 ×1.5 ⇒ (15,30)，尺寸 100×40 ⇒ 150×60
        CSource.AssertCode(code, "lv_obj_set_pos(obj_outside, 15, 30);");
        CSource.AssertCode(code, "lv_obj_set_size(obj_outside, 150, 60);");
        // 注释是**设计空间**（与编辑器一致），代码是设备空间
        CSource.AssertText(code, "// 安全区吸附（设计空间）：(5, 5) → (10, 20)");
    }
}
