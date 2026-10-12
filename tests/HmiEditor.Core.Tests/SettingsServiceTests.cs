using System;
using System.IO;
using HmiEditor.App.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 应用级用户偏好（<see cref="SettingsService"/>）的持久化与迁移测试。
///
/// <para><b>为什么这个文件存在：</b>应用级偏好是编辑器偏好的**唯一权威**（工程级
/// <c>EditorConfig</c> 已废弃）。一个"看起来能存、重启就回默认"的设置服务会让
/// 整个权威收口变成空转 —— 所以持久化本身必须有回归网。</para>
/// </summary>
public class SettingsServiceTests
{
    [Fact]
    public void WrittenValues_SurviveReload()
    {
        var path = TestTempDir.NewFilePath("settings-roundtrip", ".json");

        using (var svc = new SettingsService(path))
        {
            svc.Interaction.SetGridSize(16);
            svc.Interaction.SetSnapStep(4);
            svc.Interaction.SetShowGrid(false);
            svc.Layout.SetLeftPanelProportion(0.25);
            svc.Display.SetZoomLevel(1.5);
            svc.RecentProjects.Record("/tmp/zhishu-demo.hmi");
        }

        Assert.True(File.Exists(path), "设置文件应当落盘");

        using var reloaded = new SettingsService(path);
        Assert.Equal(16, reloaded.Interaction.GridSize);
        Assert.Equal(4, reloaded.Interaction.SnapStep);
        Assert.False(reloaded.Interaction.ShowGrid);
        Assert.Equal(0.25, reloaded.Layout.LeftPanelProportion);
        Assert.Equal(1.5, reloaded.Display.ZoomLevel);
        Assert.Single(reloaded.RecentProjects.List);
    }

    [Fact]
    public void FreshProfile_DefaultsMatchCanvasHardcodedValues()
    {
        var path = TestTempDir.NewFilePath("settings-fresh", ".json");

        using var svc = new SettingsService(path);

        // 铁律：应用级默认值必须等于本批次之前画布的硬编码值，
        // 否则"接线"就等于偷偷改了默认外观/手感。
        Assert.Equal(10, svc.Interaction.GridSize);   // SnapGrid GridSize="10"
        Assert.Equal(5, svc.Interaction.SnapStep);    // DesignCanvas.SnapThreshold = 5
        Assert.True(svc.Interaction.ShowGrid);
        Assert.True(svc.Interaction.EnableSnapToGrid);
    }

    [Fact]
    public void LegacyProfile_InertGridSize_IsNormalizedToRealCanvasValue()
    {
        var path = TestTempDir.NewFilePath("settings-legacy", ".json");
        // 旧版本：SettingsDialog 把 GridSize 默认写成 8，但没有任何消费点读它
        //（画布恒用硬编码 10）⇒ 文件里的 8 不代表用户意图。
        File.WriteAllText(path, """
        {
          "GridSize": 8,
          "EnableSnapToGrid": true,
          "ShowGrid": true
        }
        """);

        using var svc = new SettingsService(path);

        Assert.Equal(10, svc.Interaction.GridSize);
        Assert.Equal(5, svc.Interaction.SnapStep);
    }

    [Fact]
    public void LegacyProfile_ExplicitlyChangedGridSize_IsKept()
    {
        var path = TestTempDir.NewFilePath("settings-legacy-custom", ".json");
        File.WriteAllText(path, """
        {
          "GridSize": 16
        }
        """);

        using var svc = new SettingsService(path);

        Assert.Equal(16, svc.Interaction.GridSize);
    }

    [Fact]
    public void CorruptedFile_FallsBackToDefaults()
    {
        var path = TestTempDir.NewFilePath("settings-broken", ".json");
        File.WriteAllText(path, "{ this is not json");

        using var svc = new SettingsService(path);

        Assert.Equal(10, svc.Interaction.GridSize);
    }

    [Fact]
    public void UnknownKeys_ArePreservedAcrossReload()
    { 
        var path = TestTempDir.NewFilePath("settings-forward-compat", ".json");
        File.WriteAllText(path, """
        {
          "GridSize": 12,
          "SomeFutureKey": "keep-me"
        }
        """);

        using var svc = new SettingsService(path);
        // 触发一次落盘
        svc.Interaction.SetShowGrid(true);

        var json = File.ReadAllText(path);
        Assert.Contains("SomeFutureKey", json);
    }
}
