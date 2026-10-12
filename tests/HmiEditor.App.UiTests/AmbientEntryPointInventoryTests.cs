using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Threading;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **"派发之外会抢 Dispatcher"的入口清点**（阶段 297；`#47` 普查表里那几类"未逐条验证"的收口）。
///
/// <para><b>背景</b>：阶段 60 用 IL 普查 + 仓库交集把入口收敛到 `UiTestHost.Pump`（已闸门化），
/// 但表里还有几类**没逐条验证**：`AvaloniaSynchronizationContext` 三个构造、`MediaContext.get_Instance`、
/// `GestureRecognizers`、`TransformTrackingHelper`。本判据把这件事做到底：
/// 先**实测**每一类到底会不会抢（机制面），再**销案**仓库里没有调用点的那几类（静态面）。</para>
///
/// <para><b>实测结论（2026-10-11，本阶段）</b>（派发收尾后 `s_uiThread == null`，由非会话线程触碰）：
/// <c>Dispatcher.UIThread</c> / <c>Dispatcher.CurrentDispatcher</c> / <c>new AvaloniaSynchronizationContext()</c>
/// / <c>new DispatcherTimer()</c> / <c>MediaContext.Instance</c> —— <b>五条全会抢</b>
/// （<>`null` → 触碰线程）；<c>GestureRecognizers</c> 与 <c>TransformTrackingHelper</c> 在
/// 本版本 Avalonia 里**不存在这两个类型**（显式销案）。仓库侧：前两类
/// 在 <c>src/</c> 有调用点（都在 UI 线程路径上）、在 <c>tests/</c> **零直接调用**；
/// <c>AvaloniaSynchronizationContext</c> 与 <c>MediaContext</c> 全仓**零调用点**。</para>
///
/// <para><b>为什么"会抢"这件事要留在判据里</b>：它是本轮那条修法（静态画刷改 `Immutable*`）的**前提**。
/// 框架哪天改了机制，这条会红 —— 那时该做的是**重新推导**这批入口，而不是删掉判据。</para>
/// </summary>
// 会动进程级平台状态（建会话 + 故意抢一次），与大队串行
[Collection("Language-global")]
public class AmbientEntryPointInventoryTests
{
    private readonly ITestOutputHelper _o;

    public AmbientEntryPointInventoryTests(ITestOutputHelper o) => _o = o;

    private static Type? Find(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, throwOnError: false))
            .FirstOrDefault(x => x != null);

    private static string Owner() => UiTestHost.AmbientUIThreadOwnerId()?.ToString() ?? "null";

    /// <summary>
    /// **销案面**：测试代码里不许出现的环境入口直接调用。
    ///
    /// <para>阶段 297 时 `Dispatcher.UIThread` 还只能降为**读数**（当时它在测试里有二十多处代码用法，
    /// "在不在派发体内"静态判不出来）。阶段 298 把它**收进强制面**：那二十多处已改走
    /// <see cref="UiTestHost.UIThread"/>（该访问器自带"必须在派发体内"的运行期断言），
    /// 于是这里可以要求**直接引用 0 命中** —— 静态 + 运行期两道。</para>
    ///
    /// <para>扫描前**剥注释 + 掩字符串**（`MaskLiterals`）：判据说明文字与断言消息里大量提到这些名字，
    /// 不掩就会被自己人误伤。</para>
    /// </summary>
    private static readonly (string Pattern, string Why)[] ClosedInTests =
    {
        // ⚠ 用**正则**而不是字面子串：负控实测过，写成 "new DispatcherTimer" 会被
        //    `new Avalonia.Threading.DispatcherTimer()`（全限定名）绕过 —— 那正是"守卫看着在、其实没守"。
        (@"new\s+(?:[A-Za-z_][A-Za-z0-9_]*\.)*DispatcherTimer\s*\(", "无参构造读 CurrentDispatcher ⇒ 派发之外一律走 UiTestHost"),
        (@"new\s+(?:[A-Za-z_][A-Za-z0-9_]*\.)*AvaloniaSynchronizationContext\s*\(", "无参构造读 CurrentDispatcher ⇒ 同上"),
        (@"(?:[A-Za-z_][A-Za-z0-9_]*\.)*Dispatcher\s*\.\s*CurrentDispatcher", "getter 在 s_uiThread==null 时当场构造并抢走 ⇒ 同上"),
        (@"(?:[A-Za-z_][A-Za-z0-9_]*\.)*MediaContext\s*\.\s*Instance", "getter 读 Dispatcher ⇒ 同上"),
        (@"(?:[A-Za-z_][A-Za-z0-9_]*\.)*Dispatcher\s*\.\s*UIThread", "同上；派发体内请用 UiTestHost.UIThread（它自带运行期断言）"),
    };

    private static readonly (string Label, Func<Action> Touch)[] Thieves =
    {
        ("Dispatcher.UIThread", () => () => _ = Dispatcher.UIThread),
        ("Dispatcher.CurrentDispatcher", () => () => _ = Dispatcher.CurrentDispatcher),
        ("new AvaloniaSynchronizationContext()", () => () => _ = new AvaloniaSynchronizationContext()),
        ("new DispatcherTimer()", () => () => _ = new DispatcherTimer()),
        // MediaContext 是 internal：按名反射拿 Instance（getter 本身是 public）
        ("MediaContext.Instance", () =>
        {
            var mc = Find("Avalonia.Media.MediaContext");
            var prop = mc?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return prop == null ? null! : () => _ = prop.GetValue(null);
        }),
    };

    [Fact]
    public void TestSources_NeverCallTheAmbientEntryPointsDirectly()
    {
        var self = "AmbientEntryPointInventoryTests.cs";
        var dir = Path.Combine(SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests");

        var hits = new List<string>();
        var hostRawReads = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == self) continue;          // 本文件按定义要写出这些模式
            if (SourceScanner.IsBuildOutput(file)) continue;

            // 剥注释 + **掩字符串**：断言消息与说明文字里大量提到这些名字，不掩就会误伤自己人
            var code = SourceScanner.MaskLiterals(SourceScanner.ReadCode(file));
            var rel = SourceScanner.Relative(file);

            if (Path.GetFileName(file) == "UiTestHost.cs")
            {
                // 宿主是**唯一**允许直接读原始 getter 的地方（`UIThread` 访问器内部那一处）。
                // 但只许一处 —— 多出来就是绕过访问器的暗门。
                hostRawReads = Regex.Matches(code, @"(?:[A-Za-z_][A-Za-z0-9_]*\.)*Dispatcher\s*\.\s*UIThread").Count;
                continue;
            }

            foreach (var (pattern, _) in ClosedInTests)
            {
                foreach (Match m in Regex.Matches(code, pattern))
                {
                    var line = code.Take(m.Index).Count(ch => ch == '\n') + 1;
                    hits.Add($"{rel}:{line}: {m.Value}");
                }
            }
        }

        JudgeReadings.Write(_o, "派发之外的环境入口清点（阶段 298）", new[]
        {
            "机制面：Dispatcher.UIThread / CurrentDispatcher / new AvaloniaSynchronizationContext() / "
            + "new DispatcherTimer() / MediaContext.Instance —— 实测五条全会抢（单跑诊断）",
            "销案面（强制）：tests/ 里不得直接调用上述五条；派发体内一律走 UiTestHost.UIThread"
            + "（它自带'必须在派发体内'的运行期断言）",
            "扫描前剥注释 + 掩字符串，避免断言消息/说明文字里的提及被误判",
        });

        Assert.True(hits.Count == 0,
            "测试代码里出现了「派发之外会抢 Dispatcher」的环境入口直接调用（登记 #47）——"
            + "一律改走 UiTestHost（Run/RunAsync/Pump；派发体内取 Dispatcher 用 UiTestHost.UIThread）："
            + Environment.NewLine + string.Join(Environment.NewLine, hits));

        Assert.True(hostRawReads == 1,
            $"UiTestHost.cs 里直接读 Dispatcher.UIThread 的地方有 {hostRawReads} 处（应为 **1** 处："
            + "`UIThread` 访问器内部那一处，且已剥注释/掩字符串）——"
            + "多出来就是绕过访问器的暗门，0 处说明访问器被删了。");
    }

    /// <summary>
    /// **机制面（诊断，默认不跑）**：证实这几条入口在 `s_uiThread == null` 时确实会抢。
    ///
    /// <para><b>为什么默认不跑</b>：实测（2026-10-11）在**套件进程内**故意抢一次，
    /// 即便抢完立刻把 <c>s_uiThread</c>/<c>s_currentThreadDispatcher</c> 写回 <c>null</c>
    /// （<see cref="UiTestHost.ResetAmbientUIThreadOwner"/>，全程还占着派发闸门），
    /// **全量套件仍然跑出 38 条红** —— 全是 `#47` 的平台初始化签名
    /// （<c>The calling thread cannot access this object because a different thread owns it</c>）。
    /// 也就是说这些入口被触碰时会污染**进程级静态缓存**（不是只改一个字段就能还原的）。
    /// ⇒ "机制判据"在套件里就是"间歇红制造机"，只能**单跑**。</para>
    ///
    /// <para><b>怎么单跑</b>：
    /// <c>HMI_RUN_THIEF_DIAGNOSTIC=1 dotnet test tests/HmiEditor.App.UiTests --filter
    /// FullyQualifiedName~KnownAmbientEntryPoints_StillStealTheDispatcher</c></para>
    ///
    /// <para>框架升级后若要重新确认机制，跑上面那条命令；红了就**重新推导**这批入口
    /// （含阶段 295 的静态画刷结论），别把本文件删掉。</para>
    /// </summary>
    [Fact]
    public void KnownAmbientEntryPoints_StillStealTheDispatcher()
    {
        if (Environment.GetEnvironmentVariable("HMI_RUN_THIEF_DIAGNOSTIC") != "1")
        {
            JudgeReadings.Write(_o, "会抢 Dispatcher 的环境入口（阶段 297，机制面·默认不跑）", new[]
            {
                "本期未开诊断（默认不跑：在套件进程内抢一次会污染进程级静态缓存 ⇒ 全量 38 红）",
                "单跑命令：HMI_RUN_THIEF_DIAGNOSTIC=1 dotnet test tests/HmiEditor.App.UiTests "
                + "--filter FullyQualifiedName~KnownAmbientEntryPoints_StillStealTheDispatcher",
                "已记录的实测（阶段 297 探针）：五条全会抢；GestureRecognizers / TransformTrackingHelper 本版本不存在",
            });
            return;
        }

        UiTestHost.Run(() => { });   // 建平台，并让派发收尾 ⇒ 窗口开着

        var rows = new List<string>();
        var notStealing = new List<string>();
        var closed = new List<string>();

        foreach (var (label, makeTouch) in Thieves)
        {
            var touch = makeTouch();
            if (touch == null)                 // 类型/成员在本版本不存在 ⇒ 显式销案
            {
                closed.Add(label);
                continue;
            }

            var tid = 0;
            var after = "?";

            // ⚠ 占住闸门再抢：否则这一抢会和并发用例的派发撞上。
            // ⚠⚠ 而且**抢完必须还**（ResetAmbientUIThreadOwner）：实测不还的话，全量套件跑出
            //     38 条红，全是 #47 的平台初始化签名（different thread owns it）——
            //     "机制判据"当场变成"间歇红制造机"。
            var restored = false;
            UiTestHost.WithoutDispatch(() =>
            {
                var t = new Thread(() =>
                {
                    tid = Environment.CurrentManagedThreadId;
                    touch();
                    after = Owner();
                });
                t.Start();
                t.Join();

                restored = UiTestHost.ResetAmbientUIThreadOwner();
            });

            rows.Add($"{label}: thread={tid} after={after} restored={restored} ownerNow={Owner()}");
            if (after != tid.ToString())
                notStealing.Add($"{label}（after={after}，非触碰线程 {tid}）");
            if (!restored)
                notStealing.Add($"{label}（抢手**没能还回去** —— s_uiThread/s_currentThreadDispatcher 字段名变了？）");
            if (Owner() != "null")
                notStealing.Add($"{label}（还回去之后归属仍非 null）");
        }

        JudgeReadings.Write(_o, "会抢 Dispatcher 的环境入口（阶段 297）", rows);

        Assert.True(notStealing.Count == 0,
            "以下入口**不再**抢 Dispatcher —— 与阶段 295 那条修法的前提不符（框架机制变了？）："
            + Environment.NewLine + string.Join(Environment.NewLine, notStealing)
            + Environment.NewLine + "该做的是**重新推导**这批入口（含阶段 295 的静态画刷结论），而不是删掉本判据。"
            + $"{Environment.NewLine}（已显式销案：{string.Join("、", closed)}）");
    }

    /// <summary>
    /// <see cref="UiTestHost.UIThread"/> 这道的**运行期**断言（阶段 298）。
    /// 静态面禁止直接引用 <c>Dispatcher.UIThread</c>；而访问器自己必须真的拦得住"派发之外"——
    /// 否则它就成了**比直接写 getter 更危险**的东西（看起来安全，实则照样抢）。
    /// </summary>
    [Fact]
    public void UIThreadAccessor_RefusesOutsideADispatch_AndWorksInsideIt()
    {
        // ① 派发之外：必须抛，且异常里点出 #47
        var refused = false;
        try
        {
            _ = UiTestHost.UIThread;
        }
        catch (InvalidOperationException ex)
        {
            refused = ex.Message.Contains("#47", StringComparison.Ordinal);
        }
        Assert.True(refused,
            "派发之外读 UiTestHost.UIThread 没有抛 —— 它必须拦住这件事（否则它就是新的抢手入口："
            + "getter 在 s_uiThread==null 时会当场构造并抢走，登记 #47）");

        // ② 派发体内：返回的 Dispatcher 必须归**当前线程**（＝派发线程）
        var owner = UiTestHost.Run(() =>
        {
            var d = UiTestHost.UIThread;
            return d.GetType()
                .GetProperty("Thread", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(d) as Thread;
        });

        Assert.NotNull(owner);
        Assert.Equal(UiTestHost.SessionThreadId, owner!.ManagedThreadId);

        JudgeReadings.Write(_o, "派发体内取 Dispatcher 的唯一入口（阶段 298）", new[]
        {
            "派发之外：抛 InvalidOperationException（消息点出 #47）",
            $"派发体内：返回的 Dispatcher 归派发线程（sessionThread={UiTestHost.SessionThreadId}）",
        });
    }
}
