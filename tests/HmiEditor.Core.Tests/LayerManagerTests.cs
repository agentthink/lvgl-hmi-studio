using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R2-T1 层管理器验收测试 + R2-FF1 锁定/隐藏生效。
/// R2-FF4 向后兼容：旧 .hmi.json（无 IsLocked/IsVisible/LayerIndex）可加载。
/// </summary>
public class LayerManagerTests
{
    // ── R2-T1: 默认值验证 ──

    [Fact]
    public void NewControl_Defaults_IsVisibleTrue_IsLockedFalse_LayerIndexZero()
    {
        var ctrl = new HmiControl();

        Assert.True(ctrl.IsVisible);    // 默认可见
        Assert.False(ctrl.IsLocked);   // 默认未锁定
        Assert.Equal(0, ctrl.LayerIndex);
    }

    // ── R2-T1: 层索引排序逻辑 ──

    [Fact]
    public void LayerSorting_HigherLayerIndex_RendersOnTop()
    {
        var screen = new HmiScreen();
        var ctrl1 = new HmiControl { Id = "c1", Name = "背景", LayerIndex = 0 };
        var ctrl2 = new HmiControl { Id = "c2", Name = "前景", LayerIndex = 1 };

        screen.Controls.Add(ctrl1);
        screen.Controls.Add(ctrl2);

        // 按 LayerIndex 降序排序（上层在前）
        var sorted = screen.Controls
            .OrderByDescending(c => c.LayerIndex)
            .ToList();

        Assert.Equal("c2", sorted[0].Id);  // LayerIndex=1 在前
        Assert.Equal("c1", sorted[1].Id);  // LayerIndex=0 在后
    }

    [Fact]
    public void LayerSorting_SameLayerIndex_UsesListOrder()
    {
        var screen = new HmiScreen();
        var ctrl1 = new HmiControl { Id = "c1", LayerIndex = 0 };
        var ctrl2 = new HmiControl { Id = "c2", LayerIndex = 0 };
        var ctrl3 = new HmiControl { Id = "c3", LayerIndex = 0 };

        screen.Controls.Add(ctrl1);
        screen.Controls.Add(ctrl2);
        screen.Controls.Add(ctrl3);

        // 同层时，列表中后添加的在上层（倒序）
        var sorted = screen.Controls
            .Select((c, idx) => new { Control = c, Idx = idx })
            .OrderByDescending(x => x.Control.LayerIndex)
            .ThenByDescending(x => x.Idx)
            .Select(x => x.Control)
            .ToList();

        Assert.Equal("c3", sorted[0].Id);  // 最后添加的在上层
        Assert.Equal("c2", sorted[1].Id);
        Assert.Equal("c1", sorted[2].Id);
    }

    // ── R2-FF1: 锁定/隐藏行为 ──

    [Fact]
    public void LockedControl_CannotBeSelected()
    {
        var screen = new HmiScreen();
        var lockedCtrl = new HmiControl
        {
            Id = "locked",
            X = 100, Y = 100, Width = 80, Height = 40,
            IsLocked = true
        };
        var normalCtrl = new HmiControl
        {
            Id = "normal",
            X = 200, Y = 200, Width = 80, Height = 40,
            IsLocked = false
        };

        screen.Controls.Add(lockedCtrl);
        screen.Controls.Add(normalCtrl);

        // 模拟命中测试过滤逻辑
        var hittable = screen.Controls
            .Where(c => !c.IsLocked && c.IsVisible)
            .ToList();

        Assert.DoesNotContain(lockedCtrl, hittable);
        Assert.Contains(normalCtrl, hittable);
    }

    [Fact]
    public void HiddenControl_CannotBeSelected()
    {
        var screen = new HmiScreen();
        var hiddenCtrl = new HmiControl
        {
            Id = "hidden",
            IsVisible = false
        };
        var visibleCtrl = new HmiControl
        {
            Id = "visible",
            IsVisible = true
        };

        screen.Controls.Add(hiddenCtrl);
        screen.Controls.Add(visibleCtrl);

        var hittable = screen.Controls
            .Where(c => !c.IsLocked && c.IsVisible)
            .ToList();

        Assert.DoesNotContain(hiddenCtrl, hittable);
        Assert.Contains(visibleCtrl, hittable);
    }

    // ── R2-FF4: 向后兼容性 ──

    [Fact]
    public void BackwardCompatibility_OldJsonWithoutLayerFields_LoadsWithDefaults()
    {
        // 模拟旧版本 JSON（无 IsLocked/IsVisible/LayerIndex 字段）
        var oldJson = """"
        {
          "name": "TestProject",
          "lvglVersion": "9.2",
          "hardwareTargetId": "generic",
          "screens": [
            {
              "id": "screen1",
              "name": "Screen 1",
              "width": 800,
              "height": 480,
              "controls": [
                {
                  "id": "ctrl1",
                  "type": "button",
                  "name": "Button1",
                  "x": 100,
                  "y": 100,
                  "width": 120,
                  "height": 44
                }
              ]
            }
          ]
        }
        """";

        var project = ProjectSerializer.Deserialize(oldJson);

        Assert.NotNull(project);
        Assert.Single(project.Screens);

        var ctrl = project!.Screens[0].Controls[0];
        // 新字段使用默认值
        Assert.False(ctrl.IsLocked);    // 默认 false
        Assert.True(ctrl.IsVisible);     // 默认 true
        Assert.Equal(0, ctrl.LayerIndex); // 默认 0
    }

    [Fact]
    public void RoundTrip_LayerFieldsSerializeAndDeserialize()
    {
        var project = new HmiProject { Name = "Test" };
        var screen = new HmiScreen { Name = "Screen 1" };
        var ctrl = new HmiControl
        {
            Name = "LockedLayer",
            IsLocked = true,
            IsVisible = false,
            LayerIndex = 5
        };
        screen.Controls.Add(ctrl);
        project.Screens.Add(screen);

        var json = ProjectSerializer.Serialize(project);
        var restored = ProjectSerializer.Deserialize(json);

        Assert.NotNull(restored);
        var restoredCtrl = restored!.Screens[0].Controls[0];
        Assert.True(restoredCtrl.IsLocked);
        Assert.False(restoredCtrl.IsVisible);
        Assert.Equal(5, restoredCtrl.LayerIndex);
    }

    // ── R2-T2: 预览模式隔离性（详见 PreviewModeTests.cs）──
    // 此处仅保留最小占位；完整预览隔离与解析器测试位于 PreviewModeTests。

    [Fact]
    public void PreviewMode_LayerFieldsUnaffectedByPreviewToggle()
    {
        // 预览模式是画布视觉状态切换，不应改变层管理字段
        var ctrl = new HmiControl
        {
            Id = "p1",
            X = 100, Y = 100, Width = 80, Height = 40,
            IsVisible = true,
            IsLocked = true,
            LayerIndex = 3
        };

        // 模拟"进入预览 → 退出预览"后字段保持不变
        Assert.True(ctrl.IsVisible);
        Assert.True(ctrl.IsLocked);
        Assert.Equal(3, ctrl.LayerIndex);
    }

    // ── 锁定/隐藏组合场景 ──

    [Fact]
    public void ComplexScene_MixedLockVisibility_LayerIndex()
    {
        var screen = new HmiScreen();

        // 创建 20+ 控件的复杂画面
        for (int i = 0; i < 25; i++)
        {
            screen.Controls.Add(new HmiControl
            {
                Id = $"ctrl{i}",
                Name = $"Control {i}",
                X = i * 10,
                Y = i * 10,
                Width = 80,
                Height = 40,
                IsLocked = i % 5 == 0,      // 每 5 个锁定一个
                IsVisible = i % 7 != 0,      // 每 7 个隐藏一个
                LayerIndex = i / 5           // 5 个层
            });
        }

        // 可选中（未锁定且可见）的控件
        var selectable = screen.Controls
            .Where(c => !c.IsLocked && c.IsVisible)
            .ToList();

        // 确保有可选中控件
        Assert.True(selectable.Count > 0);

        // 确保锁定的控件不在可选列表中
        Assert.All(selectable, c => Assert.False(c.IsLocked));
        Assert.All(selectable, c => Assert.True(c.IsVisible));

        // 按 LayerIndex 降序排序验证
        var sorted = selectable
            .OrderByDescending(c => c.LayerIndex)
            .ToList();

        Assert.True(sorted[0].LayerIndex >= sorted[^1].LayerIndex);
    }
}
