using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **定时/自动导出服务判据**（阶段 186，登记 `#87`）：时钟、选项、注册表、写盘全是注入的
/// ⇒ 判据给固定的时刻与假的写盘，**不睡觉、不碰真磁盘、不碰线程**。
///
/// <para>四条口径各有一组判据：每个到期点只跑一次 · 错过不补 · 没源不写空包 · 失败不打断节拍。</para>
/// </summary>
public class AutoExportServiceTests
{
    private static readonly DateTimeOffset T0 = new(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));

    private sealed class Harness
    {
        public DateTimeOffset Now = T0;
        public AutoExportOptions Options = new(true, 30, "/tmp/out");
        public List<string> Registered = new() { ReportSourceKeys.Audit };
        public List<string> Missing = new() { ReportSourceKeys.Alarm, ReportSourceKeys.History };
        public Func<string, ReportTable?> Build = _ => Table("t");
        public List<AutoExportWriteRequest> Writes = new();
        public Func<AutoExportWriteRequest, AutoExportWriteResult>? WriteOverride;
        public List<AutoExportTick> Ticks = new();

        public AutoExportService Service() => new()
        {
            Now = () => Now,
            Options = () => Options,
            RegisteredSources = () => Registered,
            MissingSources = () => Missing,
            BuildSource = s => Build(s),
            Write = r =>
            {
                Writes.Add(r);
                return WriteOverride?.Invoke(r) ?? new AutoExportWriteResult(4, Array.Empty<string>());
            },
            Report = t => Ticks.Add(t),
        };

        public void Advance(double minutes) => Now = Now.AddMinutes(minutes);
    }

    private static ReportTable Table(string title) => new()
    {
        Title = title,
        Columns = new List<ReportColumn> { new("k", "K") },
        Rows = new List<IReadOnlyList<string>> { new[] { title } },
    };

    /// <summary>**起跑不立刻导**：第一拍（锚点那一刻）什么都不做。</summary>
    [Fact]
    public void FirstTick_DoesNothing()
    {
        var h = new Harness();
        var service = h.Service();

        Assert.False(service.TickOnce());
        Assert.Empty(h.Writes);
        Assert.Equal(AutoExportOutcome.NotDue, service.LastTick!.Value.Outcome);
    }

    /// <summary>**每个到期点只跑一次**：同一个时刻连拍多少次都只写一次。</summary>
    [Fact]
    public void EachDuePoint_RunsExactlyOnce()
    {
        var h = new Harness();
        var service = h.Service();

        service.TickOnce();            // 起跑（锚点）
        h.Advance(30);
        Assert.True(service.TickOnce());
        Assert.False(service.TickOnce());   // 同一时刻再拍
        Assert.False(service.TickOnce());
        h.Advance(30);
        Assert.True(service.TickOnce());

        Assert.Equal(2, h.Writes.Count);
        Assert.Equal(2, service.Runs);
    }

    /// <summary>**错过的一律不补**：时钟一次跳过 2.5 个周期 ⇒ 只写一次（不是三次）。</summary>
    [Fact]
    public void MissedDuePoints_AreNotReplayed()
    {
        var h = new Harness();
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);
        Assert.True(service.TickOnce());

        h.Advance(75);                 // 路上还有 60/90 两个到期点
        Assert.True(service.TickOnce());
        Assert.False(service.TickOnce());

        Assert.Equal(2, h.Writes.Count);
    }

    /// <summary>**失败不打断节拍**（也不重跑这一拍）：异常被吞、报 <c>Failed</c>、下一拍照走。</summary>
    [Fact]
    public void WriteFailure_DoesNotBreakTheRhythm()
    {
        var h = new Harness();
        var thrown = false;
        h.WriteOverride = _ =>
        {
            if (!thrown) { thrown = true; throw new IOException("磁盘满了"); }
            return new AutoExportWriteResult(4, Array.Empty<string>());
        };
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);

        Assert.False(service.TickOnce());   // 抛了 ⇒ 这一拍不算成功
        Assert.Equal(AutoExportOutcome.Failed, service.LastTick!.Value.Outcome);
        Assert.Equal("磁盘满了", service.LastTick!.Value.Detail);
        Assert.Equal(0, service.Runs);

        // 同一时刻再拍：这一拍已经消费掉了，不会疯狂重试
        Assert.False(service.TickOnce());
        Assert.Single(h.Writes);

        h.Advance(30);
        Assert.True(service.TickOnce());    // 下一个到期点自然重试 ⇒ 成功
        Assert.Equal(1, service.Runs);
    }

    /// <summary>
    /// **开了但没配目录** ⇒ 不写盘、报 <c>NotConfigured</c>（**不弹选择器**：后台节拍里弹窗是最坏的行为）。
    /// </summary>
    [Fact]
    public void EnabledWithoutDirectory_DoesNotWrite()
    {
        foreach (var options in new[]
                 {
                     new AutoExportOptions(true, 30, ""),     // 没配目录
                     new AutoExportOptions(true, 30, "   "),  // 全是空白
                 })
        {
            var h = new Harness { Options = options };
            var service = h.Service();

            service.TickOnce();
            h.Advance(30);

            Assert.False(service.TickOnce());
            Assert.Empty(h.Writes);
            Assert.Equal(AutoExportOutcome.NotConfigured, service.LastTick!.Value.Outcome);
        }
    }

    /// <summary>
    /// **关掉开关（或间隔 ≤ 0）⇒ 连"到点"都不存在**：报 <c>NotDue</c>、不写盘。
    /// （排程在功能关闭时一律不到期 ⇒ 根本走不到"没配目录"那条检查 —— 这是刻意的：
    /// 关掉的功能不该每 30 秒报一次"你没配目录"。）
    /// </summary>
    [Fact]
    public void DisabledFeature_IsNotEvenDue()
    {
        foreach (var options in new[]
                 {
                     new AutoExportOptions(false, 30, "/tmp/out"),   // 开关关
                     new AutoExportOptions(false, 30, ""),           // 开关关 ＋ 没目录
                     new AutoExportOptions(true, 0, "/tmp/out"),     // 间隔 0（＝关闭）
                     new AutoExportOptions(true, -5, "/tmp/out"),    // 负数同理
                 })
        {
            var h = new Harness { Options = options };
            var service = h.Service();

            service.TickOnce();
            h.Advance(1440);   // 一整天也还是不导

            Assert.False(service.TickOnce());
            Assert.Empty(h.Writes);
            Assert.Equal(AutoExportOutcome.NotDue, service.LastTick!.Value.Outcome);
        }
    }

    /// <summary>一个源都没有 ⇒ 报 <c>NoSources</c>、**不写空包**（只剩 index.txt 的目录毫无意义）。</summary>
    [Fact]
    public void NoSources_DoesNotWriteAnEmptyBundle()
    {
        var h = new Harness { Registered = new(), Build = _ => null };
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);

        Assert.False(service.TickOnce());
        Assert.Empty(h.Writes);
        Assert.Equal(AutoExportOutcome.NoSources, service.LastTick!.Value.Outcome);
    }

    /// <summary>取不到表的源不算数（注册了但暂时取不到 ⇒ 与没注册一样不进产物）。</summary>
    [Fact]
    public void SourcesThatYieldNoTable_AreSkipped()
    {
        var h = new Harness { Registered = new() { ReportSourceKeys.Audit, ReportSourceKeys.Alarm } };
        h.Build = source => source == ReportSourceKeys.Audit ? Table("audit") : null;
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);
        Assert.True(service.TickOnce());

        var request = h.Writes.Single();
        Assert.Equal(new[] { ReportSourceKeys.Audit }, request.Items.Select(i => i.Source).ToArray());
    }

    /// <summary>
    /// **缺的源如实交给写盘侧**（进 <c>index.txt</c> 的 skipped 段）；
    /// 时间戳用**时钟那一刻**（不是"现在"）——判据才能确定性地核对产物名。
    /// </summary>
    [Fact]
    public void MissingSourcesAndTimestamp_ArePassedToTheWriter()
    {
        var h = new Harness();
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);
        Assert.True(service.TickOnce());

        var request = h.Writes.Single();
        Assert.Equal(h.Missing, request.MissingSources);
        Assert.Equal(T0.AddMinutes(30), request.Timestamp);
        Assert.Equal("/tmp/out", request.Directory);
    }

    /// <summary>
    /// 停表**不重置**进度（否则每次开关设置都会立刻多导一份）；恢复后仍按原来的节奏走。
    /// ⚠ 这里**不调 <c>Start()</c>**：它要建 <c>DispatcherTimer</c>（需要 UI 线程），
    /// 而本类跑在纯 Core 判据进程里 —— "起表"那一半由 App 侧的 <c>AutoExportWiringGuardTests</c> 覆盖。
    /// </summary>
    [Fact]
    public void Stop_DoesNotResetTheSchedule()
    {
        var h = new Harness();
        var service = h.Service();

        service.TickOnce();
        h.Advance(30);
        Assert.True(service.TickOnce());

        service.Stop();
        Assert.False(service.TickOnce());   // 刚导过，还没到下一个到期点

        h.Advance(30);
        Assert.True(service.TickOnce());
        Assert.Equal(2, h.Writes.Count);
    }
}
