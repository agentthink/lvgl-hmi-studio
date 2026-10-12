using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **屏幕安全区 ⇒ 生成几何**（阶段 227，登记 `#98` 的"进导出"那半）。
///
/// <para><b>语义</b>：`ScreenDefaultsConfig.SafeAreaPadding` 的用途写在面板标签里 ——
/// 「安全区域 (异形屏避让)」⇒ 导出时**把越界控件吸附进安全区**（只动越界的，不动本来就在区内的），
/// 并在生成代码里留注释说明吸附了什么。屏幕尺寸与背景**不动**（背景本来就该铺满）。</para>
///
/// <para><b>零影响不变量</b>：默认工程（无工程 / 全零内边距）⇒ 生成物与"不带工程"**逐字节相同**。
/// 这一条是本面的地基：它保证这次改动不会悄悄改掉既有工程或既有判据的产物。</para>
/// </summary>
public class ScreenSafeAreaExportTests
{
    private static readonly double[] NoInset = { 0, 0, 0, 0 };

    private static HmiScreen ScreenWith(params HmiControl[] controls)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        foreach (var c in controls) screen.Controls.Add(c);
        return screen;
    }

    private static HmiControl Ctl(string id, double x, double y, double w = 100, double h = 40)
        => new() { Id = id, Name = id, Type = HmiControlType.Rectangle, X = x, Y = y, Width = w, Height = h };

    private static HmiProject ProjectWith(double l, double t, double r, double b)
    {
        var project = new HmiProject { Name = "P" };
        project.ScreenDefaultsConfig.SafeAreaPadding = new ScreenPadding { Left = l, Top = t, Right = r, Bottom = b };
        return project;
    }

    private static string Gen(HmiScreen screen, HmiProject? project)
        => new CodeGenerator().GenerateScreenCode(screen, null, null, null, project);

    /// <summary>
    /// **地基判据**：全零内边距（或干脆没有工程）⇒ 与"不带工程"逐字节相同。
    /// </summary>
    [Fact]
    public void ZeroInset_ChangesNothing()
    {
        // ⚠ 夹具里必须有一个**比屏幕还大**的控件：否则"零内边距时吸附是恒等变换"这件事会掩盖
        //   `_safeActive` 早退是否真的承重（负控 nc2 第一版就是这么没咬住的）。
        var screen = ScreenWith(Ctl("c1", 10, 20), Ctl("c2", 700, 400), Ctl("oversized", 5, 5, w: 900, h: 600));
        var withoutProject = Gen(screen, null);
        var zeroProject = Gen(screen, new HmiProject { Name = "Z" });

        Assert.Equal(withoutProject, zeroProject);

        var zeroPadding = Gen(screen, ProjectWith(0, 0, 0, 0));
        Assert.Equal(withoutProject, zeroPadding);

        // ⚠ 光比较"两次生成相同"是**不够**的（负控 nc2 第一版就证明了：把"头注释无条件发"注入进去，
        //   两边都带注释 ⇒ 照样相等）。所以再加**绝对**断言：没有生效的安全区时，生成物里
        //   连"安全区"三个字都不该有。
        Assert.DoesNotContain("安全区", withoutProject, StringComparison.Ordinal);
    }

    /// <summary>越界控件被吸附进安全区，并在代码里留注释。</summary>
    [Fact]
    public void OutOfBoundsControls_AreSnappedIntoTheSafeArea()
    {
        var screen = ScreenWith(
            Ctl("outside_left_top", 5, 5),        // 左上越界
            Ctl("outside_right_bottom", 760, 460));  // 右下越界（100×40 的矩形）

        var code = Gen(screen, ProjectWith(10, 20, 30, 40));

        // 安全区 = (10,20)–(770,440)
        CSource.AssertCode(code, "lv_obj_set_pos(obj_outside_left_top, 10, 20);");
        CSource.AssertCode(code, "lv_obj_set_pos(obj_outside_right_bottom, 670, 400);");   // 770-100, 440-40
        CSource.AssertText(code, "// 安全区吸附（设计空间）：(5, 5) → (10, 20)");
        CSource.AssertText(code, "// 安全区吸附（设计空间）：(760, 460) → (670, 400)");
        CSource.AssertText(code, "// 安全区（异形屏避让）：内容区 (10, 20)–(770, 440)，越界控件会被吸附进来（坐标为设计空间）");
    }

    /// <summary>本来就在安全区里的控件**一个字节都不动**（吸附只动越界的）。</summary>
    [Fact]
    public void ControlsAlreadyInside_AreUntouched()
    {
        var screen = ScreenWith(Ctl("inside", 100, 100), Ctl("inside2", 300, 200));

        var code = Gen(screen, ProjectWith(10, 20, 30, 40));

        CSource.AssertCode(code, "lv_obj_set_pos(obj_inside, 100, 100);");
        CSource.AssertCode(code, "lv_obj_set_pos(obj_inside2, 300, 200);");
        // 尺寸也必须原样（⚠ 只判位置的话，"把尺寸也夹一下"这类注入打不到 —— nc4 第一版就是这样漏的）
        CSource.AssertCode(code, "lv_obj_set_size(obj_inside, 100, 40);");
        CSource.AssertCode(code, "lv_obj_set_size(obj_inside2, 100, 40);");
        CSource.AssertNoText(code, "// 安全区吸附");
    }

    /// <summary>控件比安全区还大 ⇒ 只能贴左上（不缩尺寸、不静默丢控件），注释照写。</summary>
    [Fact]
    public void ControlBiggerThanTheSafeArea_IsPinnedToTheCorner()
    {
        var screen = ScreenWith(Ctl("huge", 0, 0, w: 900, h: 600));

        var code = Gen(screen, ProjectWith(10, 20, 30, 40));

        CSource.AssertCode(code, "lv_obj_set_pos(obj_huge, 10, 20);");
        CSource.AssertText(code, "// 安全区吸附（设计空间）：(0, 0) → (10, 20)");
    }

    /// <summary>屏幕尺寸与背景不因安全区改变（避让只约束控件位置）。</summary>
    [Fact]
    public void ScreenSize_IsNotInset()
    {
        var screen = ScreenWith(Ctl("c1", 5, 5));

        var code = Gen(screen, ProjectWith(10, 20, 30, 40));

        CSource.AssertCode(code, "lv_obj_set_size(screen, 800, 480);");
    }

    /// <summary>
    /// **面自检**：吸附注释的条数必须与"越界控件数"一致 —— 防止"算得出、没发射"
    /// （判据读的是生成物本身，不依赖内部计数器）。
    /// </summary>
    [Fact]
    public void Face_EmitsOneClampCommentPerSnappedControl()
    {
        var screen = ScreenWith(Ctl("a", 5, 5), Ctl("b", 100, 100), Ctl("c", 1000, 1000));

        var code = Gen(screen, ProjectWith(10, 20, 30, 40));

        var comments = code.Split('\n').Count(l => l.Contains("// 安全区吸附（设计空间）："));
        Assert.Equal(2, comments);
    }
}
