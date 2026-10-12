using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I7 过程图形控件（Tank/Pipe/Valve）的模型与序列化测试。
/// 覆盖：枚举存在性、字段默认值、旧 JSON 向后兼容、新字段往返保留。
/// </summary>
public class ProcessControlTests
{
    // ── 枚举存在性 ──

    [Fact]
    public void ProcessControlTypes_AreDefined()
    {
        Assert.Equal(HmiControlType.Tank, Enum.Parse<HmiControlType>("Tank"));
        Assert.Equal(HmiControlType.Pipe, Enum.Parse<HmiControlType>("Pipe"));
        Assert.Equal(HmiControlType.Valve, Enum.Parse<HmiControlType>("Valve"));
    }

    // ── 字段默认值 ──

    [Fact]
    public void NewControl_DefaultFieldValues_AreCorrect()
    {
        var ctrl = new HmiControl { Type = HmiControlType.Tank };

        // 罐体
        Assert.True(ctrl.TankShowScale);
        Assert.Equal(5, ctrl.TankTickCount);
        // 管道
        Assert.Null(ctrl.PipeDirection);       // null 表示 Horizontal（渲染层回退）
        Assert.Null(ctrl.PipeFlowDirection);   // null 表示 Forward（渲染层回退）
        Assert.Equal(2000, ctrl.PipeFlowSpeed);
        // 阀门
        Assert.Null(ctrl.ValveType);           // null 表示 Ball（渲染层回退）
        // 复用字段：量程/值
        Assert.Equal(0, ctrl.MinValue);
        Assert.Equal(100, ctrl.MaxValue);
        Assert.Equal(0, ctrl.Value);
    }

    // ── 旧 JSON 向后兼容（缺失新字段 → 默认值）──

    [Fact]
    public void Deserialize_LegacyJsonWithoutProcessFields_UsesDefaults()
    {
        var legacyJson = """
        {
          "name": "Legacy",
          "lvglVersion": "9.2",
          "screens": [
            {
              "name": "Screen1",
              "width": 480,
              "height": 272,
              "controls": [
                { "id": "c1", "name": "罐体1", "type": "Tank", "x": 10, "y": 10, "width": 140, "height": 200 }
              ]
            }
          ]
        }
        """;

        var project = ProjectSerializer.Deserialize(legacyJson);
        Assert.NotNull(project);
        var ctrl = project!.Screens[0].Controls[0];

        Assert.Equal(HmiControlType.Tank, ctrl.Type);
        // 旧 JSON 无 I7 字段 → 全部取默认值
        Assert.True(ctrl.TankShowScale);
        Assert.Equal(5, ctrl.TankTickCount);
        Assert.Equal(2000, ctrl.PipeFlowSpeed);
    }

    // ── 新字段往返保留 ──

    [Fact]
    public void RoundTrip_WithProcessFields_PreservesValues()
    {
        var project = new HmiProject { Name = "ProcessTest" };
        var screen = new HmiScreen { Name = "Screen1", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Name = "罐体1", Type = HmiControlType.Tank,
            X = 20, Y = 30, Width = 140, Height = 200,
            MinValue = 0, MaxValue = 500, Value = 250,
            TankShowScale = false, TankTickCount = 8,
            BgColor = "#0ea5e9", IndicatorColor = "#22c55e"
        });
        screen.Controls.Add(new HmiControl
        {
            Name = "管道1", Type = HmiControlType.Pipe,
            X = 20, Y = 260, Width = 200, Height = 8,
            PipeDirection = "Vertical", PipeFlowDirection = "Reverse", PipeFlowSpeed = 0
        });
        screen.Controls.Add(new HmiControl
        {
            Name = "阀门1", Type = HmiControlType.Valve,
            X = 300, Y = 60, Width = 60, Height = 60,
            ValveType = "Butterfly", Value = 1, BgColor = "#ef4444"
        });
        project.Screens.Add(screen);

        var json = ProjectSerializer.Serialize(project);
        var deserialized = ProjectSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
        var controls = deserialized!.Screens[0].Controls;

        // Tank
        Assert.Equal(HmiControlType.Tank, controls[0].Type);
        Assert.False(controls[0].TankShowScale);
        Assert.Equal(8, controls[0].TankTickCount);
        Assert.Equal(500, controls[0].MaxValue);
        Assert.Equal(250, controls[0].Value);

        // Pipe
        Assert.Equal(HmiControlType.Pipe, controls[1].Type);
        Assert.Equal("Vertical", controls[1].PipeDirection);
        Assert.Equal("Reverse", controls[1].PipeFlowDirection);
        Assert.Equal(0, controls[1].PipeFlowSpeed);

        // Valve
        Assert.Equal(HmiControlType.Valve, controls[2].Type);
        Assert.Equal("Butterfly", controls[2].ValveType);
        Assert.Equal(1, controls[2].Value);
    }

    // ── 三种过程控件均可添加到屏幕并往返 ──

    [Fact]
    public void RoundTrip_AllThreeProcessTypes_AreSupported()
    {
        var project = new HmiProject { Name = "AllProcess" };
        var screen = new HmiScreen { Name = "Screen1", Width = 480, Height = 272 };
        screen.Controls.Add(new HmiControl { Name = "T1", Type = HmiControlType.Tank });
        screen.Controls.Add(new HmiControl { Name = "P1", Type = HmiControlType.Pipe });
        screen.Controls.Add(new HmiControl { Name = "V1", Type = HmiControlType.Valve });
        project.Screens.Add(screen);

        var json = ProjectSerializer.Serialize(project);
        var deserialized = ProjectSerializer.Deserialize(json);
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized!.Screens[0].Controls.Count);
        Assert.Equal(new[]
        {
            HmiControlType.Tank, HmiControlType.Pipe, HmiControlType.Valve
        }, deserialized.Screens[0].Controls.Select(c => c.Type));
    }
}
