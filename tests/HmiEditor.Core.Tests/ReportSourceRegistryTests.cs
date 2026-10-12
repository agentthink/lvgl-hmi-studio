using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **运行期数据源注册表判据**（阶段 185，登记 `#87`）：注册/取数/注销/缺失的口径，
/// 以及"键必须来自 <see cref="ReportSourceKeys"/>"那条防错。
///
/// <para>为什么值得单测：这张表是"定时导出"与"三个面板"之间**唯一**的接缝 ——
/// 它一旦静默错（键写错、注销漏了、缺失不报），表现是"某个源永远不进报表包"，**不报错**。</para>
/// </summary>
public class ReportSourceRegistryTests
{
    private static ReportTable Table(string title) => new()
    {
        Title = title,
        Columns = new List<ReportColumn> { new("k", "K") },
        Rows = new List<IReadOnlyList<string>> { new[] { title } },
    };

    [Fact]
    public void RegisteredSource_CanBeBuilt()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.Audit, () => Table("audit"));

        Assert.Equal(new[] { ReportSourceKeys.Audit }, registry.Registered);
        Assert.Equal("audit", registry.TryBuild(ReportSourceKeys.Audit)!.Title);
        // 前提自检：三个已知源里只注册了一个 ⇒ 另两个必须被如实报成"缺"
        Assert.Equal(new[] { ReportSourceKeys.Alarm, ReportSourceKeys.History }, registry.Missing);
    }

    /// <summary>注册的委托每次**现取**（不是注册时的快照）—— 定时导出要的就是"那一刻的数据"。</summary>
    [Fact]
    public void Provider_IsCalledEveryTime()
    {
        var registry = new ReportSourceRegistry();
        var n = 0;
        registry.Register(ReportSourceKeys.Alarm, () => Table($"run-{++n}"));

        Assert.Equal("run-1", registry.TryBuild(ReportSourceKeys.Alarm)!.Title);
        Assert.Equal("run-2", registry.TryBuild(ReportSourceKeys.Alarm)!.Title);
    }

    /// <summary>没注册 ⇒ <c>null</c>（正常状态，不抛）且出现在 <c>Missing</c> 里（导出侧据此写 skipped）。</summary>
    [Fact]
    public void UnregisteredSources_AreMissingNotErrors()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.Audit, () => Table("audit"));

        Assert.Null(registry.TryBuild(ReportSourceKeys.History));
        Assert.Equal(
            new[] { ReportSourceKeys.Alarm, ReportSourceKeys.History },
            registry.Missing);
    }

    /// <summary>顺序一律按 <see cref="ReportSourceKeys.All"/>（判据与索引都靠它稳定，不靠字典序）。</summary>
    [Fact]
    public void BothLists_FollowTheDeclaredOrder()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.History, () => Table("h"));
        registry.Register(ReportSourceKeys.Audit, () => Table("a"));

        Assert.Equal(new[] { ReportSourceKeys.Audit, ReportSourceKeys.History }, registry.Registered);
        Assert.Equal(new[] { ReportSourceKeys.Alarm }, registry.Missing);
    }

    /// <summary>重复注册 ＝ 覆盖（不是"两个委托都留着"）；注销没注册过的键是空操作、不抛。</summary>
    [Fact]
    public void RegisterOverwrites_AndUnregisterIsIdempotent()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.Audit, () => Table("first"));
        registry.Register(ReportSourceKeys.Audit, () => Table("second"));

        Assert.Single(registry.Registered);
        Assert.Equal("second", registry.TryBuild(ReportSourceKeys.Audit)!.Title);

        registry.Unregister(ReportSourceKeys.Audit);
        registry.Unregister(ReportSourceKeys.Audit);   // 关闭路径上重复注销不该抛

        Assert.Empty(registry.Registered);
        Assert.Null(registry.TryBuild(ReportSourceKeys.Audit));
    }

    /// <summary>未知键当场抛 —— 它会一路变成报表包文件名与偏好键，"写错一个字母"必须在这里红。</summary>
    [Fact]
    public void UnknownSourceKey_Throws()
    {
        var registry = new ReportSourceRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register("audti", () => Table("typo")));
        Assert.Throws<ArgumentException>(() => registry.Register("", () => Table("empty")));
        Assert.Throws<ArgumentNullException>(() => registry.Register(ReportSourceKeys.Audit, null!));
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.Audit, () => Table("a"));
        registry.Register(ReportSourceKeys.Alarm, () => Table("b"));

        registry.Clear();

        Assert.Empty(registry.Registered);
        Assert.Equal(ReportSourceKeys.All.Length, registry.Missing.Count);
    }

    /// <summary>
    /// **线程面**：注册发生在 UI 线程、取数发生在定时器线程 ⇒ 一边注册/注销一边取数不许抛
    /// （判据只断言"不炸 ＋ 终态正确"；不断言中间态 —— 那是调度噪声，不是契约）。
    /// </summary>
    [Fact]
    public async Task ConcurrentRegisterAndBuild_DoesNotThrow()
    {
        var registry = new ReportSourceRegistry();
        registry.Register(ReportSourceKeys.Audit, () => Table("audit"));

        var errors = new List<Exception>();
        var writer = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 500; i++)
                {
                    registry.Register(ReportSourceKeys.Alarm, () => Table("alarm"));
                    registry.Unregister(ReportSourceKeys.Alarm);
                }
            }
            catch (Exception ex) { lock (errors) errors.Add(ex); }
        });

        for (var i = 0; i < 500; i++)
        {
            Assert.NotNull(registry.TryBuild(ReportSourceKeys.Audit));
            _ = registry.Registered;
            _ = registry.Missing;
        }

        // ⚠ 用 await 而不是 Wait()：`xUnit1031` 会把阻塞式等待判成警告，而本仓门禁是"警告即失败"
        // （这一条是阶段 185 门禁第一次红时抓到的 —— 我当时只看 `dotnet test` 的尾行，没看构建段）。
        await writer;
        Assert.Empty(errors);
        Assert.Equal(new[] { ReportSourceKeys.Audit }, registry.Registered);
    }
}
