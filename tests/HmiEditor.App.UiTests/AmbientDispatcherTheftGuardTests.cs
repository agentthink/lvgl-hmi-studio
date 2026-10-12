using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HmiEditor.App;
using HmiEditor.App.Controls;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **派发之外触碰"环境画刷"不许抢走 Dispatcher**（阶段 295；登记 `#47` 的一条**可复现**根因）。
///
/// <para><b>实测到的机制</b>（本阶段探针，读数见 <c>project-status.md</c> 阶段 295）：派发收尾后
/// <c>Dispatcher.s_uiThread</c> 是 <c>null</c>（窗口开着）；此时**任何线程**构造一个
/// <c>SolidColorBrush</c>（AvaloniaObject）就会把它**当场抢到该线程**
/// （实测 <c>owner: null → 调用线程</c>）；而下一次派发的平台初始化要在会话线程上
/// <c>DefaultRenderLoop.Add → VerifyAccess()</c> ⇒ 直接抛
/// <c>"The calling thread cannot access this object because a different thread owns it."</c>
/// —— 正是 `#47` 那条间歇红的错误消息。<c>ImmutableSolidColorBrush</c> 实测**不抢**
/// （<c>null → null</c>）：它是 Avalonia 提供的无 Dispatcher 版本。</para>
///
/// <para><b>为什么这些字段危险</b>：<c>static readonly</c> 画刷的构造发生在**类型初始化器**里，
/// 跑在"谁先碰到这个类型"的那个线程上 —— 完全可能不是会话线程，也不在任何派发内。</para>
///
/// <para><b>判据形状</b>：先建平台并让派发收尾（前提自检：归属必须是 <c>null</c> 或会话线程），
/// 然后**每个画刷字段各起一条新线程**去触碰，断言"触碰之后归属 ≠ 触碰线程"。
/// 用新线程而不是当前线程：这样"抢"一定归因到触碰那一支，而不是本测试线程。</para>
/// </summary>
[Collection("Language-global")]   // 会动进程级平台状态（建会话），与大队串行
public class AmbientDispatcherTheftGuardTests
{
    private readonly ITestOutputHelper _o;

    public AmbientDispatcherTheftGuardTests(ITestOutputHelper o) => _o = o;

    private static int? Owner() => UiTestHost.AmbientUIThreadOwnerId();
    private static int Session() => UiTestHost.SessionThreadId;

    private static void TouchStaticField(Type type, string name) =>
        type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

    private static readonly (string Label, Action Touch)[] Ambients =
    {
        ("ColorPickerPopup.DarkPanelBg", () => TouchStaticField(typeof(ColorPickerPopup), "DarkPanelBg")),
        ("ColorPickerPopup.White", () => TouchStaticField(typeof(ColorPickerPopup), "White")),
        ("MainWindow.ResourceBrushes.Normal", () => TouchStaticField(
            typeof(MainWindow).GetNestedType("ResourceBrushes", BindingFlags.NonPublic)!, "Normal")),
        ("AssetRow.OrphanBrush", () => TouchStaticField(typeof(AssetRow), "OrphanBrush")),
        ("AssetRow.NormalRefBrush", () => TouchStaticField(typeof(AssetRow), "NormalRefBrush")),
        // 资源缺失时的回退分支（`Theme.Brush` 的兜底）——它也会被静态初始化器在任意线程上走到
        ("Theme.Brush 回退", () => _ = Theme.Brush("NoSuchResourceKey-Stage295")),
    };

    [Fact]
    public void TouchingAmbientBrushFields_OnAWorkerThread_NeverStealsTheDispatcher()
    {
        UiTestHost.Run(() => { });   // 建立平台，并让派发收尾（此时抢手窗口是开着的）

        var owner0 = Owner();
        Assert.True(owner0 is null || owner0 == Session(),
            $"前提不成立：派发收尾后 s_uiThread 归属={owner0?.ToString() ?? "null"}，会话线程={Session()} —— "
            + "它必须为 null（窗口开着）或由会话线程持有，否则本判据量不到'抢'这件事");

        var rows = new List<string>();
        var thefts = new List<string>();

        foreach (var (label, touch) in Ambients)
        {
            var tid = 0;
            string after = "";
            var t = new Thread(() =>
            {
                tid = Environment.CurrentManagedThreadId;
                touch();
                after = $"owner={Owner()?.ToString() ?? "null"}";
            });
            t.Start();
            t.Join();

            rows.Add($"{label}: thread={tid} session={Session()} → {after}");
            if (Owner() == tid && tid != Session())
                thefts.Add($"{label}（线程 {tid} 把 Dispatcher 抢走了）");
        }

        JudgeReadings.Write(_o, "派发之外触碰环境画刷不抢 Dispatcher（阶段 295）", rows);

        Assert.True(thefts.Count == 0,
            "以下位置在**类型初始化器/回退**里构造了会绑 Dispatcher 的画刷，把 s_uiThread 抢到了调用线程"
            + "（登记 #47 的根因形状；下一次派发会以 'different thread owns it' 失败）："
            + Environment.NewLine + string.Join(Environment.NewLine, thefts)
            + Environment.NewLine + "修法：静态字段改用 `ImmutableSolidColorBrush`（无 Dispatcher 版本），"
            + "别在静态初始化器里 new SolidColorBrush/Pen/FormattedText。");
    }
}
