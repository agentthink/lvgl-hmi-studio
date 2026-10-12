using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R2-T2 设计时预览模式测试 —— 验证 PreviewValueResolver 纯函数行为
/// 与适应度函数 R2-FF（预览模式隔离：0 写操作）。
/// </summary>
public class PreviewModeTests
{
    // ── 解析器：基本行为 ──

    [Fact]
    public void ResolveDisplayText_NullProvider_ReturnsNull()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Gauge,
            VariableBindingId = "temp"
        };

        // 未启用预览（提供器为 null）→ 回退到编辑态
        var result = PreviewValueResolver.ResolveDisplayText(ctrl, null);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveDisplayText_NoBinding_ReturnsNull()
    {
        var ctrl = new HmiControl { Type = HmiControlType.Gauge };
        var provider = new Func<string, double?>(_ => 42.0);

        var result = PreviewValueResolver.ResolveDisplayText(ctrl, provider);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveDisplayText_BoundGauge_ReturnsFormattedValue()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Gauge,
            VariableBindingId = "temperature"
        };
        var provider = new Func<string, double?>(id => id == "temperature" ? 73.5 : null);

        var result = PreviewValueResolver.ResolveDisplayText(ctrl, provider);
        Assert.Equal("73.5", result);
    }

    [Fact]
    public void ResolveDisplayText_LedNonZero_ReturnsOn()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Led,
            VariableBindingId = "running"
        };
        var provider = new Func<string, double?>(_ => 1.0);

        Assert.Equal("ON", PreviewValueResolver.ResolveDisplayText(ctrl, provider));
    }

    [Fact]
    public void ResolveDisplayText_LedZero_ReturnsOff()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Led,
            VariableBindingId = "running"
        };
        var provider = new Func<string, double?>(_ => 0.0);

        Assert.Equal("OFF", PreviewValueResolver.ResolveDisplayText(ctrl, provider));
    }

    [Fact]
    public void ResolveDisplayText_ProviderReturnsNull_ReturnsNull()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Label,
            VariableBindingId = "missing"
        };
        var provider = new Func<string, double?>(_ => null);

        Assert.Null(PreviewValueResolver.ResolveDisplayText(ctrl, provider));
    }

    [Fact]
    public void FormatValue_NaN_ReturnsPlaceholder()
    {
        Assert.Equal("---", PreviewValueResolver.FormatValue(HmiControlType.Gauge, double.NaN));
        Assert.Equal("---", PreviewValueResolver.FormatValue(HmiControlType.Bar, double.PositiveInfinity));
    }

    [Fact]
    public void FormatValue_Slider_RoundsToInteger()
    {
        Assert.Equal("42", PreviewValueResolver.FormatValue(HmiControlType.Slider, 41.7));
    }

    // ── ResolveProgress：归一化进度 ──

    [Fact]
    public void ResolveProgress_MidRange_ReturnsClampedProgress()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Gauge,
            VariableBindingId = "v"
        };
        var provider = new Func<string, double?>(_ => 75.0);

        var progress = PreviewValueResolver.ResolveProgress(ctrl, provider, min: 0, max: 100);
        Assert.Equal(0.75, progress);
    }

    [Fact]
    public void ResolveProgress_Overflow_ClampsToOne()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Bar,
            VariableBindingId = "v"
        };
        var provider = new Func<string, double?>(_ => 200.0);

        var progress = PreviewValueResolver.ResolveProgress(ctrl, provider, min: 0, max: 100);
        Assert.Equal(1.0, progress);
    }

    [Fact]
    public void ResolveProgress_NonPositiveRange_ReturnsNull()
    {
        var ctrl = new HmiControl
        {
            Type = HmiControlType.Gauge,
            VariableBindingId = "v"
        };
        var provider = new Func<string, double?>(_ => 50.0);

        Assert.Null(PreviewValueResolver.ResolveProgress(ctrl, provider, min: 100, max: 100));
    }

    // ── 适应度函数 R2-FF：预览模式隔离（0 写操作）──

    [Fact]
    public void PreviewMode_Isolation_ResolverDoesNotModifyControlModel()
    {
        // 预览模式下解析器读取模拟值，但绝不写回模型属性。
        // 这是适应度函数 R2-FF 的核心断言：预览后模型保持原样。
        var ctrl = new HmiControl
        {
            Id = "ctrl-iso",
            Name = "TemperatureGauge",
            Type = HmiControlType.Gauge,
            X = 100, Y = 50, Width = 160, Height = 160,
            BgColor = "#2563eb",
            VariableBindingId = "temp1",
            IsLocked = false,
            IsVisible = true,
            LayerIndex = 2
        };

        // 快照解析前的全部模型字段
        var idBefore = ctrl.Id;
        var nameBefore = ctrl.Name;
        var typeBefore = ctrl.Type;
        var xBefore = ctrl.X;
        var yBefore = ctrl.Y;
        var wBefore = ctrl.Width;
        var hBefore = ctrl.Height;
        var bgBefore = ctrl.BgColor;
        var bindingBefore = ctrl.VariableBindingId;
        var lockedBefore = ctrl.IsLocked;
        var visibleBefore = ctrl.IsVisible;
        var layerBefore = ctrl.LayerIndex;

        var provider = new Func<string, double?>(id => id == "temp1" ? 88.8 : null);

        // 多次调用解析器（模拟持续刷新的预览帧）
        for (int i = 0; i < 100; i++)
        {
            PreviewValueResolver.ResolveDisplayText(ctrl, provider);
            PreviewValueResolver.ResolveProgress(ctrl, provider);
        }

        // 断言所有模型字段未被修改 —— 0 写操作
        Assert.Equal(idBefore, ctrl.Id);
        Assert.Equal(nameBefore, ctrl.Name);
        Assert.Equal(typeBefore, ctrl.Type);
        Assert.Equal(xBefore, ctrl.X);
        Assert.Equal(yBefore, ctrl.Y);
        Assert.Equal(wBefore, ctrl.Width);
        Assert.Equal(hBefore, ctrl.Height);
        Assert.Equal(bgBefore, ctrl.BgColor);
        Assert.Equal(bindingBefore, ctrl.VariableBindingId);
        Assert.Equal(lockedBefore, ctrl.IsLocked);
        Assert.Equal(visibleBefore, ctrl.IsVisible);
        Assert.Equal(layerBefore, ctrl.LayerIndex);
    }

    [Fact]
    public void PreviewMode_Isolation_ExitPreview_RestoresEditMode()
    {
        // 进入预览（提供器非空）→ 退出预览（提供器设回 null）
        // 模型数据应与编辑态完全一致。
        var ctrl = new HmiControl
        {
            Name = "Gauge1",
            Type = HmiControlType.Gauge,
            VariableBindingId = "v"
        };

        var values = new Dictionary<string, double> { ["v"] = 42.0 };
        Func<string, double?>? provider = id => values.TryGetValue(id, out var val) ? val : null;

        // 预览态：解析出模拟值
        var previewText = PreviewValueResolver.ResolveDisplayText(ctrl, provider);
        Assert.NotNull(previewText);

        // 退出预览：provider 置空
        provider = null;
        var editText = PreviewValueResolver.ResolveDisplayText(ctrl, provider);
        Assert.Null(editText);  // 回退到编辑态（由画布渲染控件名）

        // 模型未变
        Assert.Equal("Gauge1", ctrl.Name);
        Assert.Equal("v", ctrl.VariableBindingId);
    }
}
