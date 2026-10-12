using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class AlignmentToolTests
{
    [Fact]
    public void AlignLeft_MovesToTargetX()
    {
        var ctrl = new HmiControl { X = 200, Y = 100, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(10, 10, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Left);

        Assert.Equal(10, ctrl.X);
        Assert.Equal(100, ctrl.Y);
    }

    [Fact]
    public void AlignRight_MovesToTargetRightEdge()
    {
        var ctrl = new HmiControl { X = 200, Y = 100, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(10, 10, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Right);

        Assert.Equal(760, ctrl.X); // 10 + 800 - 50
    }

    [Fact]
    public void AlignCenterHorizontal_CentersInTarget()
    {
        var ctrl = new HmiControl { X = 0, Y = 100, Width = 100, Height = 40 };
        var target = new Avalonia.Rect(0, 0, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.CenterHorizontal);

        Assert.Equal(350, ctrl.X); // (800 - 100) / 2
    }

    [Fact]
    public void AlignTop_MovesToTargetY()
    {
        var ctrl = new HmiControl { X = 200, Y = 300, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(0, 20, 800, 460);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Top);

        Assert.Equal(20, ctrl.Y);
        Assert.Equal(200, ctrl.X);
    }

    [Fact]
    public void AlignBottom_MovesToTargetBottom()
    {
        var ctrl = new HmiControl { X = 200, Y = 100, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(10, 10, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Bottom);

        Assert.Equal(460, ctrl.Y); // 10 + 480 - 30
    }

    [Fact]
    public void AlignCenterVertical_CentersVertically()
    {
        var ctrl = new HmiControl { X = 100, Y = 0, Width = 50, Height = 40 };
        var target = new Avalonia.Rect(0, 0, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.CenterVertical);

        Assert.Equal(220, ctrl.Y); // (480 - 40) / 2
    }

    // ── 多选对齐：锚点 = 最后选择的控件 ──

    /// <summary>通过反射注入选中集，验证 AlignSelectedControls 以最后一个选中控件为锚点。</summary>
    private static DesignCanvas CreateCanvasWithSelection(params HmiControl[] controls)
    {
        var canvas = new DesignCanvas();
        var field = typeof(DesignCanvas).GetField("_selectedControls",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var list = (System.Collections.Generic.List<HmiControl>)field.GetValue(canvas)!;
        list.Clear();
        list.AddRange(controls);
        return canvas;
    }

    [Fact]
    public void AlignLeft_LastSelectedIsAnchor()
    {
        // 先选 anchor（最后选择），再选其它两个控件
        var anchor = new HmiControl { X = 300, Y = 100, Width = 60, Height = 30 };
        var c1 = new HmiControl { X = 10, Y = 200, Width = 50, Height = 40 };
        var c2 = new HmiControl { X = 500, Y = 50, Width = 70, Height = 20 };
        var canvas = CreateCanvasWithSelection(c1, c2, anchor);

        Assert.True(canvas.AlignSelectedControls(DesignCanvas.MultiAlignDirection.Left));
        Assert.Equal(300, c1.X); // 对齐到 anchor.X
        Assert.Equal(300, c2.X);
        Assert.Equal(300, anchor.X); // 锚点自身不动
        Assert.Equal(200, c1.Y);    // 只动 X
        Assert.Equal(100, anchor.Y);
    }

    [Fact]
    public void AlignRight_LastSelectedIsAnchor()
    {
        var anchor = new HmiControl { X = 300, Y = 100, Width = 60, Height = 30 };
        var c1 = new HmiControl { X = 10, Y = 200, Width = 50, Height = 40 };
        var canvas = CreateCanvasWithSelection(c1, anchor);

        Assert.True(canvas.AlignSelectedControls(DesignCanvas.MultiAlignDirection.Right));
        // anchor 右边缘 = 360；c1 右边缘 = c1.X + 50 = 360 → c1.X = 310
        Assert.Equal(310, c1.X);
        Assert.Equal(300, anchor.X);
    }

    [Fact]
    public void AlignCenterVertical_LastSelectedIsAnchor()
    {
        var anchor = new HmiControl { X = 300, Y = 100, Width = 60, Height = 30 };
        var c1 = new HmiControl { X = 10, Y = 200, Width = 50, Height = 40 };
        var canvas = CreateCanvasWithSelection(c1, anchor);

        Assert.True(canvas.AlignSelectedControls(DesignCanvas.MultiAlignDirection.CenterVertical));
        // anchor 中心 Y = 100 + 30/2 = 115；c1.Y = 115 - 40/2 = 95
        Assert.Equal(95, c1.Y);
        Assert.Equal(100, anchor.Y);
    }

    [Fact]
    public void AlignSelectedControls_SingleSelection_NoOp()
    {
        var only = new HmiControl { X = 10, Y = 10, Width = 50, Height = 40 };
        var canvas = CreateCanvasWithSelection(only);

        Assert.False(canvas.AlignSelectedControls(DesignCanvas.MultiAlignDirection.Left));
        Assert.Equal(10, only.X);
    }

    // ── 等距排列：所有空隙应一致 ──

    [Fact]
    public void DistributeHorizontal_GapsEqual()
    {
        var c1 = new HmiControl { X = 0, Y = 0, Width = 100, Height = 20 };
        var c2 = new HmiControl { X = 200, Y = 0, Width = 50, Height = 20 };
        var c3 = new HmiControl { X = 500, Y = 0, Width = 100, Height = 20 };
        var canvas = CreateCanvasWithSelection(c1, c2, c3);

        Assert.True(canvas.DistributeSelectedHorizontal());

        // 空隙 = 后一控件左边缘 - 前一控件右边缘
        var gap1 = c2.X - (c1.X + c1.Width);
        var gap2 = c3.X - (c2.X + c2.Width);
        Assert.Equal(gap1, gap2, 3);
        Assert.Equal(175, gap1, 3); // (500 - 100 - 50) / 2 = 175
        Assert.Equal(0, c1.X);      // 最左控件原位
        Assert.Equal(500, c3.X);    // 最右控件原位
    }

    [Fact]
    public void DistributeHorizontal_GapNotDraggedToLast_GapSymmetricWithinGrid()
    {
        // 回归：旧实现漏减最左控件宽度，导致最后一组空隙被压缩（差异可达 100px）；
        // 新实现误差补偿，空隙对称分布，最大差不超过 1 个网格步长（5）。
        var c1 = new HmiControl { X = 0, Y = 0, Width = 100, Height = 20 };
        var c2 = new HmiControl { X = 150, Y = 0, Width = 40, Height = 20 };
        var c3 = new HmiControl { X = 400, Y = 0, Width = 60, Height = 20 };
        var c4 = new HmiControl { X = 700, Y = 0, Width = 80, Height = 20 };
        var canvas = CreateCanvasWithSelection(c1, c2, c3, c4);

        Assert.True(canvas.DistributeSelectedHorizontal());

        var gaps = new[]
        {
            c2.X - (c1.X + c1.Width),
            c3.X - (c2.X + c2.Width),
            c4.X - (c3.X + c3.Width)
        };
        Assert.True(gaps.Max() - gaps.Min() <= 5, $"空隙差异应 ≤ 1 网格步长(5)，实际 {gaps.Max() - gaps.Min()}");
        Assert.Equal(0, c1.X);
        Assert.Equal(700, c4.X);
    }

    [Fact]
    public void DistributeVertical_GapsEqual()
    {
        var r1 = new HmiControl { X = 0, Y = 0, Width = 20, Height = 100 };
        var r2 = new HmiControl { X = 0, Y = 200, Width = 20, Height = 50 };
        var r3 = new HmiControl { X = 0, Y = 500, Width = 20, Height = 100 };
        var canvas = CreateCanvasWithSelection(r1, r2, r3);

        Assert.True(canvas.DistributeSelectedVertical());

        var gap1 = r2.Y - (r1.Y + r1.Height);
        var gap2 = r3.Y - (r2.Y + r2.Height);
        Assert.Equal(gap1, gap2, 3);
        Assert.Equal(175, gap1, 3); // (500 - 100 - 50) / 2 = 175
        Assert.Equal(0, r1.Y);
        Assert.Equal(500, r3.Y);
    }

    [Fact]
    public void DistributeSelected_FewerThanThree_NoOp()
    {
        var c1 = new HmiControl { X = 0, Y = 0, Width = 100, Height = 20 };
        var c2 = new HmiControl { X = 200, Y = 0, Width = 50, Height = 20 };
        var canvas = CreateCanvasWithSelection(c1, c2);

        Assert.False(canvas.DistributeSelectedHorizontal());
        Assert.False(canvas.DistributeSelectedVertical());
        Assert.Equal(0, c1.X);
        Assert.Equal(200, c2.X);
    }
}
