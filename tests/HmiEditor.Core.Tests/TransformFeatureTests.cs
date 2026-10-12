using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 控件旋转方向与镜像转换：模型语义、变换数学映射、LVGL 代码生成。
/// </summary>
public class TransformFeatureTests
{
    // ── 模型：EffectiveRotation / NormalizeRotation / HasTransform ──

    [Fact]
    public void EffectiveRotation_Cw_IsPositive()
    {
        var c = new HmiControl { Rotation = 90, RotationDirection = "CW" };
        Assert.Equal(90, c.EffectiveRotation());
    }

    [Fact]
    public void EffectiveRotation_Ccw_IsNegative()
    {
        var c = new HmiControl { Rotation = 90, RotationDirection = "CCW" };
        Assert.Equal(-90, c.EffectiveRotation());
    }

    [Fact]
    public void EffectiveRotation_NullDirection_DefaultsCw()
    {
        var c = new HmiControl { Rotation = 180 };
        Assert.Equal(180, c.EffectiveRotation());
    }

    [Fact]
    public void NormalizeRotation_WrapsInto360()
    {
        Assert.Equal(270, HmiControl.NormalizeRotation(-90));
        Assert.Equal(90, HmiControl.NormalizeRotation(450));
        Assert.Equal(0, HmiControl.NormalizeRotation(360));
    }

    [Fact]
    public void HasTransform_TrueOnlyWhenSet()
    {
        Assert.False(new HmiControl().HasTransform);
        Assert.True(new HmiControl { Rotation = 90 }.HasTransform);
        Assert.True(new HmiControl { MirrorX = true }.HasTransform);
        Assert.False(new HmiControl { Rotation = 0, MirrorX = false, MirrorY = false }.HasTransform);
    }

    // ── 变换数学：旋转/镜像后的点映射（围绕中心，内容对称无偏移） ──
    // 说明：用 100x100 正方形验证旋转映射（非正方形旋转后角点坐标不落在整格上）；
    // y 向下坐标系，正角度 = 视觉顺时针。

    [Fact]
    public void TransformPoint_Rotate90_Cw_MapsCornersSymmetrically()
    {
        // 100x100 绕中心顺时针 90°：左上 (0,0) → 右上 (100,0)；右下 (100,100) → 左下 (0,100)
        var (x, y) = TransformMath.TransformPoint(0, 0, 100, 100, 90, false, false);
        Assert.Equal(100, x, 4);
        Assert.Equal(0, y, 4);
        var (x2, y2) = TransformMath.TransformPoint(100, 100, 100, 100, 90, false, false);
        Assert.Equal(0, x2, 4);
        Assert.Equal(100, y2, 4);
    }

    [Fact]
    public void TransformPoint_Rotate90_CenterStaysFixed()
    {
        // 旋转是刚性变换：中心点不动，任意点到中心的距离不变
        var (cx, cy) = TransformMath.TransformPoint(50, 50, 100, 100, 37, true, false);
        Assert.Equal(50, cx, 4);
        Assert.Equal(50, cy, 4);
    }

    [Fact]
    public void TransformPoint_Rotate180_MapsOppositeCorner()
    {
        var (x, y) = TransformMath.TransformPoint(0, 0, 100, 100, 180, false, false);
        Assert.Equal(100, x, 4);
        Assert.Equal(100, y, 4);
    }

    [Fact]
    public void TransformPoint_Rotate270_EqualsCcw90()
    {
        // 顺时针 270° 与逆时针 90° 等效：左上 (0,0) → 左下 (0,100)
        var (x, y) = TransformMath.TransformPoint(0, 0, 100, 100, 270, false, false);
        Assert.Equal(0, x, 4);
        Assert.Equal(100, y, 4);
    }

    [Fact]
    public void TransformPoint_MirrorX_FlipsHorizontal()
    {
        var (x, y) = TransformMath.TransformPoint(0, 0, 100, 60, 0, true, false);
        Assert.Equal(100, x, 4);   // 左 → 右
        Assert.Equal(0, y, 4);
    }

    [Fact]
    public void TransformPoint_MirrorY_FlipsVertical()
    {
        var (x, y) = TransformMath.TransformPoint(0, 0, 100, 60, 0, false, true);
        Assert.Equal(0, x, 4);
        Assert.Equal(60, y, 4);    // 上 → 下
    }

    [Fact]
    public void TransformPoint_MirrorX_AppliedTwice_ReturnsOriginal()
    {
        var once = TransformMath.TransformPoint(20, 10, 100, 60, 0, true, false);
        var twice = TransformMath.TransformPoint(once.X, once.Y, 100, 60, 0, true, false);
        Assert.Equal(20, twice.X, 4);
        Assert.Equal(10, twice.Y, 4);
    }

    [Fact]
    public void TransformPoint_Rotate90_Twice_Equals180()
    {
        var once = TransformMath.TransformPoint(0, 0, 100, 60, 90, false, false);
        var twice = TransformMath.TransformPoint(once.X, once.Y, 100, 60, 90, false, false);
        Assert.Equal(100, twice.X, 4);
        Assert.Equal(60, twice.Y, 4);
    }

    // ── LVGL 代码生成 ──

    private static string GenScreen(HmiControl control)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 400, Height = 300 };
        screen.Controls.Add(control);
        return new CodeGenerator().GenerateScreenCode(screen);
    }

    [Fact]
    public void Gen_Rotation90Cw_EmitsRotation900()
    {
        var code = GenScreen(new HmiControl { Id = "r1", Type = HmiControlType.Label, Width = 100, Height = 60, Rotation = 90 });
        CSource.AssertCode(code, "lv_obj_set_style_transform_pivot_x(obj_r1, 50, 0);");
        CSource.AssertCode(code, "lv_obj_set_style_transform_pivot_y(obj_r1, 30, 0);");
        CSource.AssertCode(code, "lv_obj_set_style_transform_rotation(obj_r1, 900, 0);");
    }

    [Fact]
    public void Gen_Rotation90Ccw_EmitsNegativeRotation()
    {
        var code = GenScreen(new HmiControl { Id = "r1", Type = HmiControlType.Label, Width = 100, Height = 60, Rotation = 90, RotationDirection = "CCW" });
        CSource.AssertCode(code, "lv_obj_set_style_transform_rotation(obj_r1, -900, 0);");
    }

    [Fact]
    public void Gen_MirrorX_EmitsScaleXNegative()
    {
        // LVGL 负 scale 镜像缺陷已修复（lv_draw_image/lv_draw_sw_transform/lv_refr），
        // 生成器输出真实 transform_scale_x = -256
        var code = GenScreen(new HmiControl { Id = "m1", Type = HmiControlType.Label, Width = 100, Height = 60, MirrorX = true });
        CSource.AssertCode(code, "lv_obj_set_style_transform_scale_x(obj_m1, -256, 0);");
        Assert.DoesNotContain("transform_scale_y", code);
        CSource.AssertCode(code, "lv_obj_set_style_transform_pivot_x(obj_m1, 50, 0);");
    }

    [Fact]
    public void Gen_MirrorY_EmitsScaleYNegative()
    {
        var code = GenScreen(new HmiControl { Id = "m1", Type = HmiControlType.Label, Width = 100, Height = 60, MirrorY = true });
        CSource.AssertCode(code, "lv_obj_set_style_transform_scale_y(obj_m1, -256, 0);");
    }

    [Fact]
    public void Gen_MirrorBoth_EmitsBothScaleNegatives()
    {
        var code = GenScreen(new HmiControl { Id = "m1", Type = HmiControlType.Label, Width = 100, Height = 60, MirrorX = true, MirrorY = true });
        CSource.AssertCode(code, "lv_obj_set_style_transform_scale_x(obj_m1, -256, 0);");
        CSource.AssertCode(code, "lv_obj_set_style_transform_scale_y(obj_m1, -256, 0);");
    }

    [Fact]
    public void Gen_NoTransform_EmitsNothing()
    {
        var code = GenScreen(new HmiControl { Id = "n1", Type = HmiControlType.Label, Width = 100, Height = 60 });
        Assert.DoesNotContain("transform_rotation", code);
        Assert.DoesNotContain("transform_scale_x", code);
        Assert.DoesNotContain("transform_scale_y", code);
    }

    [Fact]
    public void Gen_Transform_EmitsOverflowVisible()
    {
        var code = GenScreen(new HmiControl { Id = "r1", Type = HmiControlType.Label, Width = 100, Height = 60, Rotation = 90 });
        CSource.AssertCode(code, "lv_obj_add_flag(obj_r1, LV_OBJ_FLAG_OVERFLOW_VISIBLE);");
    }
}
