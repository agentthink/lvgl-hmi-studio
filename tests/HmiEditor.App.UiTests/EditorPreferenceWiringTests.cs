using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 编辑器偏好接线的行为守卫 —— 「设置里改了，画布真的跟着变」。
///
/// <para><b>它防的是什么：</b>本批次之前，网格点间距与吸附步长在三处各有一个数字：
/// <c>SnapGrid</c> 的 XAML 硬编码 10、<c>DesignCanvas.SnapThreshold</c> 硬编码 5，
/// 以及两个设置界面上各一个<b>谁都不读</b>的输入框。用户改了半天，画布纹丝不动。
/// 这里用真实可视树把它钉住：出厂默认 == 旧硬编码值，改设置 == 立刻生效。</para>
/// </summary>
public class EditorPreferenceWiringTests
{
    private static SettingsService NewSettings(string prefix) =>
        new(TestTempDir.NewFilePath(prefix, ".json"));

    [Fact]
    public void FactoryDefaults_MatchPreWiringHardcodedValues()
    {
        UiTestHost.Run(() =>
        {
            using var settings = NewSettings("ui-editor-prefs-default");
            var view = new DesignCanvasView(null, settings);

            // 出厂值必须等于接线之前写死在代码/XAML 里的值 —— 否则"接线"就成了偷偷改默认外观。
            Assert.Equal(10.0, view.GridControl.GridSize);
            Assert.True(view.GridControl.IsVisible);
            Assert.Equal(5.0, view.Canvas.SnapStep);
            Assert.True(view.Canvas.SnapEnabled);
        });
    }

    [Fact]
    public void ChangingPreferences_UpdatesCanvasLive()
    {
        UiTestHost.Run(() =>
        {
            using var settings = NewSettings("ui-editor-prefs-live");
            var view = new DesignCanvasView(null, settings);
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            UiTestHost.Pump();

            settings.Interaction.SetGridSize(24);
            settings.Interaction.SetShowGrid(false);
            settings.Interaction.SetSnapStep(3);
            settings.Interaction.SetEnableSnapToGrid(false);

            // 订阅挂在 Attached 上，这里已经 Show 过 ⇒ 必须立刻反映，不需要重启编辑器。
            Assert.Equal(24.0, view.GridControl.GridSize);
            Assert.False(view.GridControl.IsVisible);
            Assert.Equal(3.0, view.Canvas.SnapStep);
            Assert.False(view.Canvas.SnapEnabled);

            window.Close();
        });
    }

    [Fact]
    public void NonPositiveSnapStep_FallsBackToDefault()
    {
        UiTestHost.Run(() =>
        {
            using var settings = NewSettings("ui-editor-prefs-degenerate");
            var view = new DesignCanvasView(null, settings);
            var window = new Window { Width = 800, Height = 600, Content = view };
            window.Show();
            UiTestHost.Pump();

            // 0 会让 Math.Round(v/0) 变 NaN ⇒ 控件坐标全成 NaN。退化输入必须回退。
            settings.Interaction.SetSnapStep(0);
            Assert.Equal(5.0, view.Canvas.SnapStep);

            window.Close();
        });
    }

    [Fact]
    public void SnapGridControlDefault_MatchesPreferenceDefault()
    {
        UiTestHost.Run(() =>
        {
            // SnapGrid 自己的默认值只在"没人注入"时兜底。它必须与用户偏好出厂值一致，
            // 否则同一块画布在两条路径（注入 / 未注入）下会显示两种点距。
            Assert.Equal(10.0, HmiEditor.App.Controls.SnapGrid.GridSizeProperty
                .GetDefaultValue(typeof(HmiEditor.App.Controls.SnapGrid)));
        });
    }

    [Fact]
    public void CanvasView_DoesNotHardcodeGridSizeInXaml()
    {
        // 画布点距唯一来源 = 应用级设置。XAML 里再写一个 GridSize="10"
        // 虽然会被代码覆盖，但它是"第二个数字"，下一个人会以为要改那里。
        var axaml = File.ReadAllText(Path.Combine(AppSourceScanner.AppRoot,
            "Views", "DesignCanvasView.axaml"));

        Assert.DoesNotContain("GridSize=", axaml);
    }

    // ── 结构守卫：被废掉的工程级副本不许回来 ──

    [Fact]
    public void ProjectSettingsView_HasNoEditorSettingsCategory()
    {
        var sources = AppSourceScanner.ScanAppSources();
        var axaml = File.ReadAllText(Path.Combine(AppSourceScanner.AppRoot,
            "Views", "ProjectSettingsView.axaml"));
        var code = sources.Single(s => s.Relative == "Views/ProjectSettingsView.axaml.cs").Text;

        Assert.DoesNotContain("PanelEditorSettings", axaml);
        Assert.DoesNotContain("PanelEditorSettings", code);
        // 分类清单里也不该再出现 editor 这一项
        Assert.DoesNotContain("ProjectSettingsCategoryEditor", code);
    }

    [Fact]
    public void ProductCode_NoLongerReferencesEditorConfig()
    {
        // EditorConfig 已降级为「只读兼容载体」：只剩模型自己 + HmiProject 的持有属性。
        // 任何别的产品代码重新读/写它，都说明"两个权威"又回来了。
        var allow = new[]
        {
            "src/HmiEditor.Core/Models/EditorConfig.cs",   // 定义本身
            "src/HmiEditor.Core/Models/HmiProject.cs",     // 持有属性（JSON 形状）
        };

        var sources = SourceScanner.ProductSources();
        Assert.True(sources.Count >= 300, $"只扫到 {sources.Count} 个产品源文件，扫描面可能失效");

        var offenders = sources
            // 必须先剥注释：讲解"EditorConfig 已废弃"的注释里必然出现这个词。
            .Where(path => Regex.IsMatch(SourceScanner.ReadCode(path), @"\bEditorConfig\b"))
            .Select(SourceScanner.Relative)
            .Where(rel => !allow.Contains(rel))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"这些产品源文件还在引用 EditorConfig：{string.Join(", ", offenders)}");
    }
}
