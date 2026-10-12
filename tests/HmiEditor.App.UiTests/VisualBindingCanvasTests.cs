using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// P1-1：预览模式下，表达式驱动的视觉属性在**画布上真的生效**。
///
/// 用 <see cref="CanvasRenderSpy"/> 把 <see cref="DesignCanvas.Render"/> 的绘制操作录下来，
/// 断言填充画刷的实际颜色 —— 这是「设计时即时生效」的唯一硬证据：
/// 表达式求值正确不等于画布用了它（历史缺陷正是「模型接受、无人消费」）。
///
/// 同时锁住隔离契约：**编辑态不受表达式影响**，渲染仍是静态设计值。
/// </summary>
public class VisualBindingCanvasTests
{
    /// <summary>控件静态背景色（编辑态应看到的就是这个）。</summary>
    private const string StaticBlue = "#0000FF";

    /// <summary>temp &gt; 100 时红（16711680 = 0xFF0000），否则绿（65280 = 0x00FF00）。</summary>
    private const string ColorExpression = "temp > 100 ? 16711680 : 65280";

    private static readonly Color Blue = Color.Parse("#FF0000FF");
    private static readonly Color Red = Color.Parse("#FFFF0000");
    private static readonly Color Green = Color.Parse("#FF00FF00");

    [Fact]
    public void PreviewMode_BgColorExpression_DrivesFillBrush()
    {
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas("BgColor", ColorExpression);
            canvas.PreviewValueProvider = name => name == "temp" ? 120 : null;
            canvas.IsPreviewMode = true;

            var colors = FillColors(CanvasRenderSpy.Record(canvas));

            Assert.Contains(Red, colors);
            Assert.DoesNotContain(Blue, colors);
        });
    }

    [Fact]
    public void PreviewMode_BgColorExpression_FollowsValueChange()
    {
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas("BgColor", ColorExpression);
            canvas.IsPreviewMode = true;

            canvas.PreviewValueProvider = name => name == "temp" ? 120 : null;
            Assert.Contains(Red, FillColors(CanvasRenderSpy.Record(canvas)));

            // 同一个画布换一个模拟值 —— 颜色必须跟着走（而不是只在首帧求值一次）
            canvas.PreviewValueProvider = name => name == "temp" ? 20 : null;
            var colors = FillColors(CanvasRenderSpy.Record(canvas));
            Assert.Contains(Green, colors);
            Assert.DoesNotContain(Red, colors);
        });
    }

    [Fact]
    public void PreviewMode_VisibilityExpression_HidesControlWhenFalse()
    {
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas("IsVisible", "temp > 100");
            canvas.IsPreviewMode = true;

            // temp > 100 为真 → 可见
            canvas.PreviewValueProvider = name => name == "temp" ? 200 : null;
            Assert.Contains(Blue, FillColors(CanvasRenderSpy.Record(canvas)));

            // temp > 100 为假 → 隐藏（控件不再被画出来，只剩预览横幅那几个非蓝色填充）
            canvas.PreviewValueProvider = name => name == "temp" ? 0 : null;
            Assert.DoesNotContain(Blue, FillColors(CanvasRenderSpy.Record(canvas)));
        });
    }

    [Fact]
    public void EditMode_ExpressionDoesNotAffectRendering()
    {
        // 隔离契约：非预览态一律用静态设计值 —— 否则用户在编辑器里根本看不到自己配的颜色。
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas("BgColor", ColorExpression);
            canvas.PreviewValueProvider = name => name == "temp" ? 120 : null;
            canvas.IsPreviewMode = false;

            var colors = FillColors(CanvasRenderSpy.Record(canvas));

            Assert.Contains(Blue, colors);
            Assert.DoesNotContain(Red, colors);
        });
    }

    [Fact]
    public void PreviewMode_UnboundControl_KeepsStaticColor()
    {
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas(propertyName: null, expression: null);
            canvas.PreviewValueProvider = name => name == "temp" ? 120 : null;
            canvas.IsPreviewMode = true;

            Assert.Contains(Blue, FillColors(CanvasRenderSpy.Record(canvas)));
        });
    }

    [Fact]
    public void PreviewMode_InvalidExpression_FallsBackToStaticColor()
    {
        UiTestHost.Run(() =>
        {
            var canvas = BuildCanvas("BgColor", "temp >");
            canvas.PreviewValueProvider = name => name == "temp" ? 120 : null;
            canvas.IsPreviewMode = true;

            Assert.Contains(Blue, FillColors(CanvasRenderSpy.Record(canvas)));
        });
    }

    // ── helpers ──

    private static DesignCanvas BuildCanvas(string? propertyName, string? expression)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 400, Height = 200 };
        // 刻意用 Rectangle（无文本）：本组测试只关心「填充色/是否被画」，
        // 不引入 FormattedText 的字体 shaping 路径（那是另一类失败源，会掩盖真正的断言）。
        var control = new HmiControl
        {
            Id = "lamp", Name = "lamp", Type = HmiControlType.Rectangle,
            X = 10, Y = 10, Width = 100, Height = 40,
            BgColor = StaticBlue,
        };
        if (propertyName is not null && expression is not null)
        {
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = propertyName,
                Kind = BindingSourceKind.Expression,
                Expression = expression,
                Direction = BindingDirection.ReadOnly,
            });
        }
        screen.Controls.Add(control);

        return new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };
    }

    /// <summary>
    /// 取出渲染结果里所有「纯色填充」的颜色 —— 用来判定控件被画成了什么颜色。
    /// 用接口 <see cref="ISolidColorBrush"/> 而不是具体类：画刷来自共享缓存，
    /// 实际类型可能是不可变实现（只认 <c>SolidColorBrush</c> 会静默过滤掉全部结果）。
    /// </summary>
    private static List<Color> FillColors(IEnumerable<Drawing> drawings)
        => drawings.OfType<GeometryDrawing>()
            .Where(g => g.Brush is ISolidColorBrush)
            .Select(g => ((ISolidColorBrush)g.Brush!).Color)
            .ToList();
}
