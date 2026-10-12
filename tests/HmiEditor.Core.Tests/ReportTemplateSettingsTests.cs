using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表模板的持久化判据**（阶段 182）：模板是**结构化**的偏好（字典 → 条目数组），
/// 而 `UserSettings` 是 `Dictionary&lt;string, object?&gt;`、读回来是 <c>JsonElement</c>
/// ⇒ 没有 `Coerce&lt;T&gt;` 的"复杂类型按目标类型反序列化"那一条，它会**存得进去、重启就没了**。
/// 所以这里第一条判据就是**往返**（负控 nc1 把那条回落拿掉，它必须红）。
/// </summary>
public class ReportTemplateSettingsTests
{
    private static ReportTemplate Template(params ReportTemplateEntry[] entries) => new(entries);

    private static readonly ReportTemplate Sample = Template(
        new ReportTemplateEntry("user.mgr.audit.user", "操作者"),
        new ReportTemplateEntry("user.mgr.audit.time", Hidden: true),
        new ReportTemplateEntry("user.mgr.audit.detail", "说明"));

    [Fact]
    public void Template_SurvivesReload()
    {
        var path = TestTempDir.NewFilePath("report-template", ".json");

        using (var svc = new SettingsService(path))
        {
            svc.ReportTemplates.Set(ReportSourceKeys.Audit, Sample);
            Assert.NotNull(svc.ReportTemplates.Get(ReportSourceKeys.Audit));   // 内存里先立住
        }

        Assert.True(File.Exists(path));

        using var reloaded = new SettingsService(path);
        var back = reloaded.ReportTemplates.Get(ReportSourceKeys.Audit);

        Assert.NotNull(back);
        Assert.Equal(3, back!.Entries.Count);
        Assert.Equal("user.mgr.audit.user", back.Entries[0].Key);
        Assert.Equal("操作者", back.Entries[0].DisplayName);
        Assert.True(back.Entries[1].Hidden);
        Assert.Null(back.Entries[1].DisplayName);
        Assert.Equal("说明", back.Entries[2].DisplayName);
        Assert.Equal(new[] { ReportSourceKeys.Audit }, reloaded.ReportTemplates.Configured);
    }

    /// <summary>
    /// **新分支不许劫持老路径**（正控）：同一份文件里"基元偏好"与"结构化偏好"都要活着。
    /// </summary>
    [Fact]
    public void PrimitivesAndTemplates_RoundTripTogether()
    {
        var path = TestTempDir.NewFilePath("report-template-mixed", ".json");

        using (var svc = new SettingsService(path))
        {
            svc.Interaction.SetGridSize(16);
            svc.Display.SetZoomLevel(1.5);
            svc.ReportTemplates.Set(ReportSourceKeys.History, Template(new ReportTemplateEntry("v1", "温度")));
        }

        using var reloaded = new SettingsService(path);
        Assert.Equal(16, reloaded.Interaction.GridSize);
        Assert.Equal(1.5, reloaded.Display.ZoomLevel);
        Assert.Equal("温度", reloaded.ReportTemplates.Get(ReportSourceKeys.History)!.Entries.Single().DisplayName);
    }

    /// <summary>没设过 ⇒ <c>null</c>；三个数据源互相独立（设一个不影响另一个）。</summary>
    [Fact]
    public void UnsetSources_AreNull_AndSourcesAreIndependent()
    {
        var path = TestTempDir.NewFilePath("report-template-unset", ".json");
        using var svc = new SettingsService(path);

        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Audit));
        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Alarm));
        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.History));
        Assert.Empty(svc.ReportTemplates.Configured);

        svc.ReportTemplates.Set(ReportSourceKeys.Alarm, Template(new ReportTemplateEntry("alarm.name", "名称")));

        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Audit));
        Assert.NotNull(svc.ReportTemplates.Get(ReportSourceKeys.Alarm));
        Assert.Equal(new[] { ReportSourceKeys.Alarm }, svc.ReportTemplates.Configured);
    }

    /// <summary>
    /// 空模板等同"删掉"（两者在行为上都是恒等 ⇒ 磁盘上不留空壳）。
    ///
    /// <para>⚠ 只断言"<c>Get</c> 返回 <c>null</c>"是**判不出**这条口径的：读回时"没有这一项"与
    /// "存了个空数组"本来就被同等对待（负控 nc2 第一版就是这么漏的 —— 注入"空模板照存"照样绿）。
    /// 所以这里直接核对**偏好文件的形状**：那个数据源键不该留在 <c>ReportTemplates</c> 里。</para>
    /// </summary>
    [Fact]
    public void EmptyTemplate_ClearsTheEntry()
    {
        var path = TestTempDir.NewFilePath("report-template-empty", ".json");
        using var svc = new SettingsService(path);

        svc.ReportTemplates.Set(ReportSourceKeys.Audit, Template(new ReportTemplateEntry("user.mgr.audit.time")));
        Assert.NotNull(svc.ReportTemplates.Get(ReportSourceKeys.Audit));

        svc.ReportTemplates.Set(ReportSourceKeys.Audit, ReportTemplate.Empty);

        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Audit));
        Assert.Empty(svc.ReportTemplates.Configured);
        AssertNoStoredEntry(path, ReportSourceKeys.Audit);

        svc.ReportTemplates.Set(ReportSourceKeys.Audit, Template(new ReportTemplateEntry("user.mgr.audit.time")));
        svc.ReportTemplates.Clear(ReportSourceKeys.Audit);

        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Audit));
        AssertNoStoredEntry(path, ReportSourceKeys.Audit);
    }

    /// <summary>偏好文件里那个数据源键**不存在**（"没有这一项"才是空模板的落盘形态）。</summary>
    private static void AssertNoStoredEntry(string path, string source)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("ReportTemplates", out var map) ||
            map.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return;   // 整个键都没落盘 —— 同样满足"不留空壳"
        }

        Assert.False(map.TryGetProperty(source, out _),
            $"空模板在偏好文件里留下了空壳：ReportTemplates.{source}");
    }

    /// <summary>未知数据源键一律忽略（脏数据不该被写进偏好文件，也不该打穿启动）。</summary>
    [Fact]
    public void UnknownSourceKeys_AreIgnored()
    {
        var path = TestTempDir.NewFilePath("report-template-bogus", ".json");
        using var svc = new SettingsService(path);

        svc.ReportTemplates.Set("bogus", Template(new ReportTemplateEntry("x")));
        svc.ReportTemplates.Clear("bogus");

        Assert.Null(svc.ReportTemplates.Get("bogus"));
        Assert.Empty(svc.ReportTemplates.Configured);
        Assert.False(ReportSourceKeys.IsKnown("bogus"));
    }

    /// <summary>偏好文件是用户可手改的：形状不对时**退回默认**（<c>null</c>），不打穿启动。</summary>
    [Fact]
    public void CorruptStoredValue_FallsBackToDefault()
    {
        var path = TestTempDir.NewFilePath("report-template-corrupt", ".json");
        File.WriteAllText(path, """{ "SchemaVersion": 2, "ReportTemplates": "这不是字典" }""");

        using var svc = new SettingsService(path);

        Assert.Null(svc.ReportTemplates.Get(ReportSourceKeys.Audit));
        Assert.Empty(svc.ReportTemplates.Configured);
    }

    /// <summary>数据源键是**产物文件名与偏好键共用**的稳定标识：三个、互不相同、判据认得。</summary>
    [Fact]
    public void SourceKeys_AreStableAndDistinct()
    {
        Assert.Equal(3, ReportSourceKeys.All.Length);
        Assert.Equal(3, ReportSourceKeys.All.Distinct(StringComparer.Ordinal).Count());

        foreach (var key in ReportSourceKeys.All)
        {
            Assert.True(ReportSourceKeys.IsKnown(key));
            Assert.Equal(key, key.Trim());                       // 不许有空白（它同时是文件名前缀）
            Assert.False(key.Contains('/'), $"数据源键不该含路径分隔符：{key}");
        }
    }
}
