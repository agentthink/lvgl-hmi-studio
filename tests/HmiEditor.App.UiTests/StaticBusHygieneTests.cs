using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HmiEditor.App;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="StaticBusHygiene"/> 的**自测**（订阅那一半是阶段 134，数据那一半是阶段 139）
/// —— 量具必须会失败，否则"永不失败的守卫"等于没有（与 <c>TestWaitTests</c> 同一思路）。
///
/// <para><b>它钉的是哪两件事：</b></para>
/// <list type="number">
///   <item><b>订阅</b>："清掉陈旧订阅"这个动作**真的**让总线上的老处理器不再被调用。
///     做法是装一个**会抛的**陈旧订阅（模拟 `#72` 里"打到已关闭的主窗口"那个形状）：
///     清空之后抬事件**不许再抛**；不清则**必须抛**（这两半都在本文件里）。</item>
///   <item><b>数据</b>（阶段 139）：<see cref="StaticBusHygiene.ResetSharedData"/> **真的**把
///     共享静态属性清回基线，**且**它的分类表与 <c>DockLayoutState</c> 的静态字段**两侧闭合**
///     —— 少分类一个就抛（那条抛本身也在合成语料上判）。</item>
/// </list>
///
/// <para>⚠ 本类会**清空进程级静态总线** ⇒ 挂 <c>Language-global</c> 集合
/// （<c>DisableParallelization = true</c>）与其他用例串行，理由与
/// <see cref="StaticBusHygiene"/> 的 xmldoc 里那条相同。</para>
/// </summary>
[Collection("Language-global")]
public class StaticBusHygieneTests
{
    [Fact]
    public void Silence_ClearsStaleSubscriptions_SoAnOldHandlerNoLongerRuns()
    {
        var fired = 0;
        EventHandler<string> stale = (_, _) =>
        {
            fired++;
            throw new InvalidOperationException("陈旧订阅被调到 —— 模拟 #72 里打到已关闭的主窗口");
        };

        DockLayoutState.ConfigNodeActivated += stale;
        try
        {
            // 未清空：抬事件**必然**打到那个会抛的处理器（这正是 #72 的机理）
            var ex = Assert.Throws<InvalidOperationException>(
                () => DockLayoutState.RaiseConfigNodeActivated(new object(), "probe"));
            Assert.Contains("陈旧订阅被调到", ex.Message);

            // 清空之后再抬：**不该**再抛，也不该再计数
            var cleared = StaticBusHygiene.Silence();
            Assert.True(cleared >= 1, $"清掉了 {cleared} 个委托字段 —— 至少要清掉刚装的这一个");

            var before = fired;
            DockLayoutState.RaiseConfigNodeActivated(new object(), "probe");
            Assert.Equal(before, fired);
        }
        finally
        {
            // 尽力还原：把那条陈旧订阅摘掉（此时总线已被清空，摘不摘都安全）
            DockLayoutState.ConfigNodeActivated -= stale;
        }
    }

    [Fact]
    public void Silence_LeavesTheBusQuiet_ForAHandlerThatWouldOtherwiseThrow()
    {
        // 第二条腿：**只清一次**就够 —— 清过之后再抬任何事件都不该有处理器（连装两次也一样）。
        EventHandler<string> first = (_, _) => throw new InvalidOperationException("first");
        EventHandler<string> second = (_, _) => throw new InvalidOperationException("second");

        DockLayoutState.ConfigNodeActivated += first;
        DockLayoutState.ConfigNodeActivated += second;
        try
        {
            StaticBusHygiene.Silence();
            DockLayoutState.RaiseConfigNodeActivated(new object(), "probe");   // 两个都该被清掉

            // 自己再装一个"新的活的"订阅：清空**不能**把"之后装的"也屏蔽掉
            var live = 0;
            EventHandler<string> fresh = (_, key) => { if (key == "probe") live++; };
            DockLayoutState.ConfigNodeActivated += fresh;
            DockLayoutState.RaiseConfigNodeActivated(new object(), "probe");
            Assert.Equal(1, live);
        }
        finally
        {
            DockLayoutState.ConfigNodeActivated -= first;
            DockLayoutState.ConfigNodeActivated -= second;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  数据那一半（阶段 139，登记 #72 的第二个形态）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// **真的清回基线**（且**只清该清的那一个**）：先摆出**真实的**污染（模拟"上一个主窗口留下的"），
    /// 再调 <see cref="StaticBusHygiene.ResetSharedData"/>，断言
    /// ① 被标成 <c>Reset</c> 的字段（当前只有 <c>Templates</c>）回到默认值；
    /// ② 被标成 <c>Keep</c> 的字段**一动不动**（"保守"必须是**可断言**的，不是注释里的承诺）。
    ///
    /// <para>🩸 **为什么只有 `Templates` 是 Reset（阶段 139 实测）**：第一版把 8 个全清了，
    /// 全量跑**崩/挂** —— 别的测试留下的**活着的** <c>HmiDockFactory</c> 工厂会在运行期读它们，
    /// 而 <c>HmiDockFactory.cs:284</c> / <c>:305</c> 两处**没有 null 守卫**。
    /// 详见 <see cref="StaticBusHygiene"/> 的 xmldoc。</para>
    ///
    /// <para>⚠ 前置断言（"污染确实摆上了"）不能省：没有它，一个**什么都不做**的
    /// <c>ResetSharedData()</c> 也能让本用例通过。</para>
    /// </summary>
    [Fact]
    public void SharedData_IsResetToBaseline_AndTheKeptOnesAreUntouched()
    {
        // 摆进去的"上一个主窗口的残留"——按字段名索引，后面**按分类表**逐格判
        var stale = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["CurrentProject"] = new HmiProject { Name = "NcStaleProject" },
            ["CurrentScreen"] = new HmiScreen { Name = "NcStaleScreen" },
            ["SelectedControl"] = new HmiControl { Name = "NcStaleControl" },
            ["AssetLibrary"] = new AssetLibrary(),
            ["ResourceLibrary"] = new HmiResourceLibrary(),
            ["Templates"] = new[] { new ControlTemplate { Name = "NcStaleTemplate" } },
            ["ProjectDirectory"] = "/nc/stale",
            ["ActiveDrawTool"] = HmiControlType.Button,
        };

        try
        {
            // ⚠ **先清订阅再摆数据**（阶段 139 实测：漏了这一步会让全量跑崩/挂）：
            //   `SetDrawTool(…)` 会**当场**抬 `DrawToolChanged`，而别的测试留下的主窗口还订着它
            //   ⇒ 打到已关闭的窗口上正是 `#72` 的崩溃形状。顺序在这里是**前提**，不是装饰。
            StaticBusHygiene.Silence();

            DockLayoutState.CurrentProject = (HmiProject)stale["CurrentProject"]!;
            DockLayoutState.CurrentScreen = (HmiScreen)stale["CurrentScreen"]!;
            DockLayoutState.SelectedControl = (HmiControl)stale["SelectedControl"]!;
            DockLayoutState.AssetLibrary = (AssetLibrary)stale["AssetLibrary"]!;
            DockLayoutState.ResourceLibrary = (HmiResourceLibrary)stale["ResourceLibrary"]!;
            DockLayoutState.Templates = (IEnumerable<ControlTemplate>)stale["Templates"]!;
            DockLayoutState.ProjectDirectory = (string)stale["ProjectDirectory"]!;
            DockLayoutState.SetDrawTool(HmiControlType.Button);

            // 前置：污染**真的**摆上了（否则下面的断言是空跑）
            foreach (var (name, expected) in stale)
                AssertStale(name, expected, FieldByName(name));

            // ② 清
            var reset = StaticBusHygiene.ResetSharedData();

            // ③ **按分类表逐格判**（不写死"只有 Templates" —— 政策一改这条 fact 不该跟着改）
            var resetCount = 0;
            foreach (var field in StaticBusHygiene.StaticFields())
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                var name = StaticBusHygiene.LogicalName(field);
                Assert.True(StaticBusHygiene.SharedDataPolicy.TryGetValue(name, out var rule), name);

                if (rule.Action == StaticBusHygiene.SharedDataAction.Reset)
                {
                    resetCount++;
                    var baseline = field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
                    Assert.True(Equals(field.GetValue(null), baseline),
                        $"`{name}` 标了 Reset，但值不是基线：{field.GetValue(null) ?? "(null)"}");
                }
                else
                {
                    AssertStale(name, stale[name], field);   // Keep ⇒ 必须一动不动
                }
            }

            Assert.Equal(resetCount, reset);   // 计数口径：只数**真的变了**的
        }
        finally
        {
            // ⚠ 收尾：本用例**故意**把 Keep 档留在污染态（那是判据的一部分）⇒ 自己负责清干净，
            //   否则下一个用例看到的是"测试摆的假数据"（正是要治的那个形状）。
            DockLayoutState.CurrentProject = null;
            DockLayoutState.CurrentScreen = null;
            DockLayoutState.SelectedControl = null;
            DockLayoutState.AssetLibrary = null;
            DockLayoutState.ResourceLibrary = null;
            DockLayoutState.Templates = null;
            DockLayoutState.ProjectDirectory = null;
            DockLayoutState.SetDrawTool(null);
        }
    }

    /// <summary>
    /// 断言字段里还是我摆进去的那一个：**引用类型比引用、值类型比值**
    /// （`ActiveDrawTool` 是 `HmiControlType?` —— 两次装箱的同一个枚举值**不是**同一个实例，
    /// 用 `Assert.Same` 会假红；本阶段实测撞到过）。
    /// </summary>
    private static void AssertStale(string name, object? expected, System.Reflection.FieldInfo field)
    {
        var actual = field.GetValue(null);
        if (field.FieldType.IsValueType) Assert.Equal(expected, actual);
        else Assert.Same(expected, actual);
    }

    /// <summary>按**逻辑名**（属性名）取静态字段 —— 自测里用来核对「我摆的那个对象还在不在」。</summary>
    private static System.Reflection.FieldInfo FieldByName(string logicalName)
        => StaticBusHygiene.StaticFields()
            .Single(f => string.Equals(StaticBusHygiene.LogicalName(f), logicalName, StringComparison.Ordinal));

    /// <summary>
    /// **分类表两侧闭合**（阶段 139）：<c>DockLayoutState</c> 的每个静态字段要么是委托
    /// （归 <c>Silence()</c>）、要么在 <see cref="StaticBusHygiene.SharedDataPolicy"/> 里；
    /// 反过来，表里的每一项都必须对应一个**真存在**的静态字段（陈账要清）。
    ///
    /// <para>为什么这条必须有：<c>ResetSharedData</c> 会**抛**，但那是**运行期**才发现；
    /// 而这条在**任何一次**跑测试时都会把"新增了一个没人分类的静态字段"当场摊开 ——
    /// 与静态面守卫是同一种形状（**变动必被点名**）。</para>
    /// </summary>
    [Fact]
    public void SharedDataPolicy_CoversExactlyTheNonDelegateStaticFields()
    {
        var fields = StaticBusHygiene.StaticFields().ToList();
        var dataFields = fields.Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType))
            .Select(StaticBusHygiene.LogicalName)
            .ToHashSet(StringComparer.Ordinal);

        var unclassified = dataFields.Where(n => !StaticBusHygiene.SharedDataPolicy.ContainsKey(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var stale = StaticBusHygiene.SharedDataPolicy.Keys.Where(n => !dataFields.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(unclassified.Count == 0,
            "这些静态字段**没被分类**（新增静态成员时必须来 SharedDataPolicy 里写清 Reset/Keep 与理由）："
            + string.Join("；", unclassified));
        Assert.True(stale.Count == 0,
            "SharedDataPolicy 里这些条目**已经没有对应的静态字段**了（陈账要清）："
            + string.Join("；", stale));

        // 下界：这条判据要真的是在数同一批东西（面塌了就恒真）
        Assert.True(dataFields.Count >= 8, $"只数到 {dataFields.Count} 个共享数据字段 —— 扫描面塌了");
        Assert.True(StaticBusHygiene.SharedDataPolicy.Count >= 8,
            $"分类表只有 {StaticBusHygiene.SharedDataPolicy.Count} 条 —— 表被掏空了");
    }

    /// <summary>
    /// **fail-closed 的抛**（合成语料）：传一张**空表**进去 ⇒ 必须抛，且点名字段。
    /// 这条判的是"没分类就静默跳过"那个形状**不可能**发生 —— 静默跳过正是 `#72` 本身。
    /// </summary>
    [Fact]
    public void AnUnclassifiedStaticField_ThrowsInsteadOfBeingSilentlySkipped()
    {
        var empty = new Dictionary<string, (StaticBusHygiene.SharedDataAction Action, string Reason)>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => StaticBusHygiene.ResetSharedData(empty));

        Assert.Contains("没被分类", ex.Message);
        // 点名的必须是**数据**字段之一（不是某个事件、也不是泛泛一句）
        Assert.Contains(StaticBusHygiene.SharedDataPolicy.Keys,
            key => ex.Message.Contains($"DockLayoutState.{key}", StringComparison.Ordinal));
    }
}
