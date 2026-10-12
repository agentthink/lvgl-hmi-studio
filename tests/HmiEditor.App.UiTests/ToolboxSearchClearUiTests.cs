using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ToolboxView` 搜索／清除的判据**（阶段 252；登记 `#39` 残余面清单里"两层都够不到"的最后一格）。
///
/// <para><b>它为什么必须单独立判据：</b>阶段 251 量出第四层（`DockPanelInvocationUiTests`）的面
/// **按 `Btn*` 前缀筛**，而 <c>ToolboxView</c> 唯一那个具名按钮叫 <c>ClearSearchButton</c> ——
/// 名字不以 <c>Btn</c> 开头 ⇒ **第四层面外**；它又**进不了第五层**（第五层的表卫生要求每个宿主
/// ≥3 条声明，而它只有 1 个按钮 —— 那条规则是防"面太薄、判据接近恒真"，这里它与"只有一个按钮"
/// 冲突）⇒ 唯一合适的落点是**专题判据**（与 `TableCellEditorDialogUiTests` 同形）。</para>
///
/// <para><b>判据形状：</b>走生产构造路径（`new ToolboxView()`）装配进 `Window` 并 `Show()`，
/// 然后三条：
/// <list type="number">
///   <item><b>面</b>：视图里**具名**的 <see cref="Button"/> 恰好是 `ClearSearchButton`（双向差集 —— 新增按钮会被点名）；</item>
///   <item><b>搜索方向</b>（本条目标按钮的**前提**，如实划界）：往搜索框写字 ⇒
///     `OnSearchTextChanged` 过滤列表并让 <c>ClearSearchButton.IsVisible = true</c>。
///     ⚠ 驱动走共享原语 <see cref="ControlWiringFace.Write"/>（写文本 ＋ **补发** `TextChangedEvent`）——
///     程序化写 `TextBox.Text` **不触发** `TextChanged`（阶段 187 实测），这条上一版判据就栽在这里；</item>
///   <item><b>`ClearSearchButton`</b>：点它 ⇒ `OnClearSearchClick` → `SearchBox.Clear()` ⇒
///     框被清空；文本变化事件到达时 `OnSearchTextChanged` 再跑一遍 ⇒ **列表恢复全量 ＋ 按钮自己隐藏**。
///     ⚠ 这两步在 headless 下要**分开断言**：`Clear()` 是产品直接做的（必判），而"事件到达后列表恢复"
///     依赖框架在文本变化时抬 `TextChanged` —— headless 下程序化清空**不抬**
///     （本判据实测：裸 `TextBox`「程序化 set = 0 / `Clear()` = 0 / 补发 = 1」，与阶段 187 同结论），
///     所以按本仓驱动纪律补发一次再断言。**不把这一档当成产品缺陷**（同 `#40` 的纪律：headless 限制 ≠ 产品缺陷）。</item>
/// </list></para>
///
/// <para><b>不抓什么：</b>① 不判拖放（那是另一张面，`ToolboxView` 的 pointer 处理属第五层的指针族话题）；
/// ② 不判真实命中测试（不做像素点击）；③ 不判列表项的图标/文案（`ToolboxIconDictionaryTests` 管那块）；
/// ④ 筛选词是**运行时从列表里取的**（取最后一项的 `DisplayName`），不是写死的词 —— 并带一条自检：
/// 若那个词把全部条目都命中，这条判据会**当场报"夹具前提不成立"**而不是静默恒真。</para>
/// </summary>
[Collection("Language-global")]
public class ToolboxSearchClearUiTests
{
    private readonly ITestOutputHelper _out;

    public ToolboxSearchClearUiTests(ITestOutputHelper o) => _out = o;

    private static (Window Window, ToolboxView View) Open()
    {
        var view = new ToolboxView();
        var window = new Window { Content = view, Width = 360, Height = 700 };
        window.Show();
        UiTestHost.Pump(5);
        return (window, view);
    }

    private static TextBox Search(ToolboxView view) =>
        view.FindControl<TextBox>("SearchBox") ?? throw new InvalidOperationException("找不到 SearchBox");

    private static Button Clear(ToolboxView view) =>
        view.FindControl<Button>("ClearSearchButton") ?? throw new InvalidOperationException("找不到 ClearSearchButton");

    private static ListBox List(ToolboxView view) =>
        view.FindControl<ListBox>("ToolboxList") ?? throw new InvalidOperationException("找不到 ToolboxList");

    private static int Count(ToolboxView view) => List(view).Items.Count;

    /// <summary>当前列表里第一条/最后一条的 `DisplayName` —— 筛选词从**运行期的目录**里取，不写死。</summary>
    private static string DisplayNameAt(ToolboxView view, int index) =>
        ((ToolboxItem)List(view).Items[index]!).DisplayName;

    /// <summary>
    /// 布景：往搜索框里填一个**命中一部分**的词（**从运行期目录里挑**，不写死）。
    ///
    /// <para>挑法：拿每个条目的 `DisplayName` 当候选，取第一个能造成 `0 &lt; 命中 &lt; 全量` 的。
    /// 一个都挑不出来就**当场报错并把候选与命中数打出来** —— 而不是静默让判据恒真
    /// （目录换了词、或筛选逻辑变成"匹配全部"，都会从这里暴露）。</para>
    /// </summary>
    private static string TypeFilter(ToolboxView view, int all)
    {
        var candidates = Enumerable.Range(0, all)
            .Select(i => DisplayNameAt(view, i))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var diag = new List<string>();
        foreach (var term in candidates)
        {
            // ⚠ 必须走共享驱动：程序化写 `TextBox.Text` **不触发** `TextChanged`（阶段 187 实测），
            //    真实输入时是框架补发这一步 ⇒ `ControlWiringFace.Write` 写文本 ＋ 补发事件。
            ControlWiringFace.Write(view, "SearchBox", term);
            var filtered = Count(view);
            if (filtered > 0 && filtered < all) return term;
            diag.Add($"「{term}」⇒ {filtered}");
            if (diag.Count >= 8) break;
        }

        Assert.Fail($"夹具挑不出「命中一部分」的筛选词（全量 {all}）—— 试过的词与命中数："
                    + string.Join(" / ", diag) + "。目录换了、或筛选逻辑变成匹配全部，都会长这样");
        return "";   // 不可达（Assert.Fail 已抛）
    }

    [Fact]
    public void Face_HasExactlyOneNamedButton()
    {
        UiTestHost.Run(() =>
        {
            var (window, view) = Open();
            try
            {
                var named = view.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "ClearSearchButton" }, named);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void TypingAFilter_NarrowsTheList_AndRevealsTheClearButton()
    {
        UiTestHost.Run(() =>
        {
            var (window, view) = Open();
            try
            {
                var all = Count(view);
                Assert.True(all > 1, $"工具箱只有 {all} 个条目 —— 判据读不出筛选（目录变了吗？）");
                Assert.False(Clear(view).IsVisible, "清除按钮初值应是隐藏（XAML 里 IsVisible=\"False\"）");

                var term = TypeFilter(view, all);
                var filtered = Count(view);

                Assert.True(Clear(view).IsVisible, "有筛选词时清除按钮应显形（OnSearchTextChanged 写它）");
                Assert.True(filtered < all, $"筛选后条目数 {filtered} 应小于全量 {all}");

                JudgeReadings.Write(_out, "工具箱搜索／清除（#39 残余面 · 阶段 252）", new[]
                {
                    "面：具名 Button 1 个 —— ClearSearchButton（双向差集，已断言）",
                    $"目录全量 {all} 条；筛选词「{term}」命中 {filtered} 条",
                    $"清除按钮可见性：初值 False → 有词 True",
                });
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ClearButton_ResetsTheSearch_AndRestoresTheFullList()
    {
        UiTestHost.Run(() =>
        {
            var (window, view) = Open();
            try
            {
                var all = Count(view);
                var term = TypeFilter(view, all);
                var filtered = Count(view);

                Clear(view).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(3);

                // ① 点它 ⇒ 搜索框被清空（`OnClearSearchClick` → `SearchBox.Clear()`）—— 这一步与框架无关
                Assert.Equal("", Search(view).Text ?? "");

                // ② 框架在文本变化时会抬 `TextChanged`；**headless 下程序化清空不抬**
                //    （本判据实测：裸 TextBox `set=0 / Clear=0 / 补发=1`，与阶段 187 同结论）
                //    ⇒ 按本仓驱动纪律**补发一次**，再断言处理器把列表恢复、按钮隐藏。
                //    ⚠ 这不是"让判据恒真"：接线若断，补发也没人接 ⇒ 列表仍停在筛选结果上（红）。
                Search(view).RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
                UiTestHost.Pump(3);

                // ③ 列表恢复全量（`OnSearchTextChanged` 在 filter 为空时把 ItemsSource 拨回 `_allItems`）
                Assert.Equal(all, Count(view));
                // ④ 按钮自己隐藏（同一个处理器写的；不断言它的话，"点了没反应"也会绿）
                Assert.False(Clear(view).IsVisible, "清除之后按钮应自己隐藏");

                JudgeReadings.Write(_out, "工具箱搜索／清除（#39 残余面 · 阶段 252）", new[]
                {
                    "面：具名 Button 1 个 —— ClearSearchButton（双向差集，已断言）",
                    $"点它之前：筛选词「{term}」⇒ {filtered} / 全量 {all}；按钮可见 True",
                    $"点它之后：搜索框 `{Search(view).Text ?? "(空)"}`；条目 {Count(view)} / 全量 {all}；按钮可见 "
                        + $"{Clear(view).IsVisible}",
                });
            }
            finally { window.Close(); }
        });
    }
}
