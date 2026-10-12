using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HmiEditor.App.Controls;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 窗口视图装载冒烟测试：把 <c>HmiEditor.App.Views</c> 下每一个**窗口**（<see cref="Window"/> 子类）
/// 真正构造出来、<c>Show()</c>、跑一次布局、断言没有异常。
///
/// <para><b>为什么要有这一类（此前是空面）：</b><see cref="ViewSmokeUiTests"/> 的清单口径是
/// <c>typeof(UserControl).IsAssignableFrom(t)</c> —— 它只收 UserControl 视图，
/// <b>一个窗口都不收</b>。本命名空间里的窗口类型（28 个 <see cref="ChromeWindow"/> 子类 +
/// <c>SplashWindow</c> + 3 个代码里造的嵌套对话框）因此长期不在任何运行期判据的面内：
/// 它们的 XAML 只在人工启动编辑器点到那个对话框时才会被解析。</para>
///
/// <para><b>口径</b>：命名空间以 <c>HmiEditor.App.Views</c> 开头、非抽象、非开放泛型、
/// 是 <see cref="Window"/>、且**有无参构造函数** —— 实测收走 <b>32</b> 个。
/// 其中 3 个（<c>RecipeEditorView</c> 里的 <c>TextInputDialog</c>/<c>ConfirmDialog</c>、
/// <c>AnimationEditorView</c> 里的 <c>ConfirmDeleteDialog</c>）是 private 嵌套类型，
/// 由宿主视图在代码里构造；它们照样进来，因为「没人构造过」正是本层要防的事。</para>
///
/// <para><b>为什么不直接放宽那一层的口径：</b>两者宿主方式不同 —— UserControl 要包进一个
/// 宿主 <see cref="Window"/> 才能 <c>Show()</c>，而 <see cref="ChromeWindow"/> 本身就是窗口，
/// 不能当 Content。断言集也不同（外壳契约与缩放手柄只对 <see cref="ChromeWindow"/> 有意义）。
/// 两条判据各自成类，再由
/// <see cref="EveryViewType_IsInSomeSmokeSurface"/> 把两个面**并起来**守一遍，
/// 防止将来再有第三种视图形状悄悄漏网。</para>
///
/// <para><b>本层抓什么：</b>① 构造期 / 布局期异常（缺依赖、初始化顺序、XAML 解析失败）；
/// ② <b>外壳结构静默破坏</b> —— <see cref="ChromeWindow.InjectResizeGrips"/> 要求
/// <c>Content is Border shell &amp;&amp; shell.Child is Grid</c>，不符就 <c>return</c>，
/// 症状是「窗口用鼠标拖不动边、也拉不了角」，**不抛异常、不留日志**。</para>
///
/// <para><b>首跑就抓到的东西（2026-09-20，取证在修复之前）：</b>32 个里 3 个红 + 1 个面外类型。</para>
/// <list type="number">
///   <item><c>DeviceManagerView</c> —— <b>生产路径构造即崩</b>。XAML 里
///     <c>CmbChannelType</c> 的 <c>SelectedIndex="0"</c> 在 <c>InitializeComponent()</c> 期间
///     触发 <c>OnChannelTypeChanged</c>，而它读的 <c>SerialPanel</c>/<c>BaudPanel</c>/<c>TcpPanel</c>
///     声明在后面、那一刻还是 null ⇒ NRE 从 <c>new DeviceManagerView()</c> 抛出，
///     <c>Wiring.Panels.cs</c> 的 <c>ShowDialog</c> 走不到 ⇒ 「设备管理」菜单点了没反应。
///     该行自 <c>2adb1f8</c>(2026-08-11) 就在，属于长期未被发现的缺陷（无 try/catch 兜底，
///     也正因为没有任何判据构造过这个窗口）。</item>
///   <item><c>AlarmListView</c> / <c>SimulatorView</c> —— <c>OnClosed</c> 里 NRE。
///     两者的无参构造是**设计器路径**（<c>_engine = null!</c> / 四个字段 <c>null!</c>），
///     退订代码没容下 null ⇒ 关闭设计器预览即崩。这是「public 构造产出关不掉的窗口」，
///     属真实缺陷（注释写「不要直接使用」不是守卫），已一并修好。</item>
///   <item><c>TrendChartPanel</c> —— 见 <see cref="ReachedByAHost"/>：它不是视图，
///     但没被任何一个面收走，正面回答「还有没有第三类视图形状」的那个问题就是它。</item>
/// </list>
///
/// <para><b>本层不抓什么（2026-09-20 明确，避免高估）：</b><c>{StaticResource}</c> 键缺失
/// **不是**本层的职责。阶段 47 实测定性：键缺失落在控件属性上时是**静默跳过赋值**
/// （属性回落主题默认，不抛、不报）；落在资源字典内对象的属性上是**推迟到读才抛**。
/// 也就是说窗口在构造与布局期都不会因为它红。那类缺陷归
/// <c>XamlResourceKeyClosureTests</c>（静态扫描全部 axaml，覆盖 47 个文件）。
/// <b>别把这一层当成「窗口的资源键有人看着了」。</b></para>
///
/// <para><b>为什么走无参构造（诚实边界）：</b>统一用无参构造，好处是清单口径简单到一个谓词。
/// 代价是：对 5 个类型（<c>AiTemplateDialog</c>/<c>AiScriptDialog</c>/<c>SplashWindow</c>/
/// <c>AlarmListView</c>/<c>SimulatorView</c>）走的是**标注了「仅用于 XAML 设计器」的那条构造**，
/// 它们不接 Click、不订阅事件、不读最近工程。两条路径都执行 <c>InitializeComponent()</c>，
/// 因此 XAML 解析与布局覆盖等价，但<b>本层不声称覆盖事件接线</b>；
/// 对 3 个代码里造的嵌套对话框（<c>ConfirmDialog</c> 等）则连「用哪个构造」都取决于宿主视图，
/// 本层只保证「能造出来、能布局、能关掉」。</para>
///
/// <para><b>为什么在 <c>Language-global</c> 集合里：</b>与
/// <see cref="MainWindowConstructionTests"/> 同因 —— 本类碰两个进程级单例：数据根环境变量
/// （<see cref="ChromeWindow"/> 的 <c>Opened</c>/<c>Closing</c> 会经 <c>WindowGeometryService</c>
/// 读写 <c>windows.json</c>）与本地化服务。该集合 <c>DisableParallelization = true</c>，
/// 于是「改了必须还原」不会影响旁人。</para>
/// </summary>
[Collection("Language-global")]
public class WindowViewSmokeUiTests
{
    /// <summary>面：名单见 <see cref="WindowCatalog"/>，一个窗口一个用例（便于失败时点名）。</summary>
    public static IEnumerable<object[]> WindowTypes() =>
        WindowCatalog().Select(t => new object[] { t.Name });

    /// <summary>
    /// 清单口径：命名空间在 <c>HmiEditor.App.Views</c> 下、非抽象、非开放泛型、
    /// 是 <see cref="Window"/>、且有无参构造函数。
    ///
    /// <para>按**类型关系**取面（而不是列名单），这样新增一个窗口视图时它会自动进来；
    /// 反之若有人把某个视图改成需要必填参数的构造，它会**静默离开**本面 ——
    /// <see cref="WindowCatalog_IsNotEmpty"/> 与
    /// <see cref="EveryViewType_IsInSomeSmokeSurface"/> 就是为这件事设的两道守卫。</para>
    /// </summary>
    internal static IReadOnlyList<Type> WindowCatalog()
    {
        var asm = typeof(HmiEditor.App.Views.FaceplateLibraryView).Assembly;
        return asm.GetTypes()
            .Where(t => t.Namespace is not null
                     && t.Namespace.StartsWith("HmiEditor.App.Views", StringComparison.Ordinal)
                     && !t.IsAbstract
                     && !t.ContainsGenericParameters
                     && typeof(Window).IsAssignableFrom(t)
                     && t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 面自检。这一层最大的盲区是「扫了个空所以全绿」，所以先把两件事钉住：
    /// 面不能变窄（数量下限），而且面里确实有 <see cref="ChromeWindow"/> 子类
    /// （否则下面的外壳断言会全部跳过，等于没测）。
    /// </summary>
    [Fact]
    public void WindowCatalog_IsNotEmpty()
    {
        var catalog = WindowCatalog();

        Assert.True(catalog.Count >= 32,
            $"只推导出 {catalog.Count} 个窗口类型（应为 32 = 28 个 ChromeWindow 子类 + SplashWindow "
            + "+ 3 个代码里造的嵌套对话框）"
            + "，清单可能失效 —— 面变窄时下面的逐视图用例会静默变少，不会红。"
            + $"本次收走的：[{string.Join(", ", catalog.Select(t => t.Name))}]");

        var chrome = catalog.Count(t => typeof(ChromeWindow).IsAssignableFrom(t));
        Assert.True(chrome >= 28,
            $"面里的 ChromeWindow 子类只有 {chrome} 个（应为 28）—— "
            + "外壳契约与缩放手柄那两条断言会静默跳过");
    }

    /// <summary>
    /// 豁免：**不是视图**、但已被某个受测视图构造出来的类型。键是 <c>FullName</c>。
    ///
    /// <para><b>豁免必须可验证</b>：宿主视图的那个用例会断言它真的出现在可视树里
    /// （见 <see cref="Window_LoadsAndLaysOut"/> 结尾），所以这里不是一句注释 ——
    /// 宿主哪天不再构造它，判据就红。</para>
    ///
    /// <para>为什么用类型名而不是 <c>typeof</c>：<c>TrendChartPanel</c> 是
    /// <c>internal</c>，测试程序集够不着它。</para>
    /// </summary>
    private static readonly Dictionary<string, (Type Host, string Why)> ReachedByAHost = new()
    {
        ["HmiEditor.App.Views.TrendChartPanel"] = (typeof(HmiEditor.App.Views.TrendConfigView),
            "TrendConfigView 在 OnOpened 里把 ChartPanel 换成它（趋势折线自绘面板），没有独立入口"),
    };

    /// <summary>
    /// **面覆盖守卫**：两个冒烟面（本类的窗口面 + <see cref="ViewSmokeUiTests"/> 的
    /// UserControl 面）的并集（外加 <see cref="ReachedByAHost"/> 里已验证的宿主可达项），
    /// 必须收走 <c>HmiEditor.App.Views</c> 下**所有**非抽象、有无参构造的 <see cref="Control"/> 派生类。
    ///
    /// <para>这条守卫才是「窗口视图长期没人测」这件事的真正解药：原来两个面各自守自己的清单，
    /// 谁都不问「还有没有第三类视图」。阶段 47 的实测就是这个形状 ——
    /// **28** 个 <c>: Window</c> 的视图从任何面里掉出去（按类名 grep 只数到 26 —— 漏掉经泛型基类
    /// <c>AiGenerateDialogBase&lt;TResult&gt;</c> 继承的那两个），而两侧守卫都是绿的。
    /// 本守卫首跑就抓到了 <c>TrendChartPanel</c>（一个 <c>: Control</c> 的自绘面板，
    /// 两边都不收）。</para>
    /// </summary>
    [Fact]
    public void EveryViewType_IsInSomeSmokeSurface()
    {
        var covered = WindowCatalog().Concat(ViewSmokeUiTests.ViewCatalog()).ToHashSet();

        var all = typeof(HmiEditor.App.Views.FaceplateLibraryView).Assembly.GetTypes()
            .Where(t => t.Namespace is not null
                     && t.Namespace.StartsWith("HmiEditor.App.Views", StringComparison.Ordinal)
                     && !t.IsAbstract
                     && !t.ContainsGenericParameters
                     && typeof(Control).IsAssignableFrom(t)
                     && t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();

        // 空集守卫：命名空间改了名会让下面的差集恒空 —— 那正是「假门禁」的经典形状。
        // ⚠ 阶段 137：两个死视图（`VariableBrowserView` / `PrototypeCanvas`）下架 ⇒ 48 → **46**
        //   （32 窗口 + **13** UserControl + 1 豁免）。
        Assert.True(all.Count >= 46,
            $"在 HmiEditor.App.Views 下只找到 {all.Count} 个可构造的视图类型（应为 46 = 32 窗口 "
            + "+ 13 UserControl + 1 豁免）—— 命名空间前缀可能已改，下面的差集检查会变成恒真");

        var orphans = all.Where(t => !covered.Contains(t) && !ReachedByAHost.ContainsKey(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            $"{orphans.Count} 个视图类型不在任何冒烟面里（既不是窗口类型，也没被 UserControl 面收走，"
            + "也不在 ReachedByAHost 豁免表里）。新增视图请让它落进某个面，否则它的 XAML 只在人工点到时"
            + "才会被解析；若确实是「被宿主构造的辅助控件」，加进 ReachedByAHost 并把宿主填对 —— "
            + "宿主用例会去可视树里核实："
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", orphans)}");
    }

    /// <summary>
    /// 构造 → <c>Show()</c> → 布局 → 关闭。任何会让这条路径炸掉的改动都会在这里红，
    /// 并**点名是哪个窗口**（<see cref="WindowTypes"/> 是 <c>Theory</c>，一个窗口一个用例）。
    ///
    /// <para>本用例还要为 <see cref="ReachedByAHost"/> 里的豁免项**作证**：若某个被豁免的类型
    /// 声明宿主是这个窗口，就必须在它的可视树里找到实例。</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowTypes))]
    public void Window_LoadsAndLaysOut(string typeName)
    {
        var type = WindowCatalog().Single(t => t.Name == typeName);
        using var sandbox = new DataRootSandbox("winview");

        var r = UiTestHost.Run(() => Snap(type));

        Assert.True(r.VisualCount > 0, $"{typeName} 构造后可视树为空（XAML 未加载？）");
        Assert.True(r.NamedCount > 0,
            $"{typeName} 的 XAML 似乎未加载 —— 可视树里 {r.VisualCount} 个控件，没有一个带 x:Name");

        // 豁免作证：ReachedByAHost 里认这个窗口当宿主的类型，必须真的被构造出来。
        foreach (var (exempt, host) in ReachedByAHost.Where(kv => kv.Value.Host == type))
        {
            Assert.True(r.VisualTypeNames.Contains(exempt),
                $"{exempt} 被 ReachedByAHost 豁免的理由是「{host.Why}」，"
                + $"但 {typeName} 显示并布局之后，可视树里没有它 —— "
                + "豁免的宿主填错了，或者宿主已经不再构造它。");
        }

        if (!typeof(ChromeWindow).IsAssignableFrom(type))
            return;

        Assert.True(r.HasBorderShell && r.HasRootGrid,
            $"{typeName} 的外壳不符 ChromeWindow 契约（要求 Content 是 Border、其 Child 是 Grid），"
            + $"实测 Content={r.ContentKind}。这是**静默**缺陷："
            + "InjectResizeGrips 会直接 return，症状是窗口拖不动边、拉不了角，不抛异常也不留日志。");

        Assert.True(r.RowCount >= 2,
            $"{typeName} 的根 Grid 只有 {r.RowCount} 行 —— 注入的 overlay 用 Grid.SetRowSpan(..., 2)，"
            + "行数不足时它被约束在第 0 行内，南/北缩放手柄会落在标题栏下沿而不是窗口边缘。");

        Assert.True(r.GripCount == 8,
            $"{typeName} 只注入了 {r.GripCount} 个缩放手柄（应为 8 = 4 边 + 4 角）。"
            + "手柄由 ChromeWindow.OnOpened 注入，数量不对说明注入被跳过或结构变了。");
    }

    /// <summary>构造、显示、布局、读回形状、关闭。</summary>
    private static Snapshot Snap(Type type)
    {
        var window = (Window)Activator.CreateInstance(type)!;
        window.Show();
        UiTestHost.Pump();

        // 触发布局：布局期异常（无法解析的模板、NaN 尺寸）会在这里抛出。
        window.Measure(new Size(1024, 600));
        window.Arrange(new Rect(0, 0, 1024, 600));
        UiTestHost.Pump();

        var visual = window.GetVisualDescendants().OfType<Control>().ToList();
        var named = visual.Count(c => c.Name is not null);
        var typeNames = visual.Select(c => c.GetType().FullName!).ToHashSet(StringComparer.Ordinal);

        var shell = window.Content as Border;
        var root = shell?.Child as Grid;

        var snapshot = new Snapshot(
            VisualCount: visual.Count,
            NamedCount: named,
            VisualTypeNames: typeNames,
            HasBorderShell: shell is not null,
            HasRootGrid: root is not null,
            RowCount: root?.RowDefinitions.Count ?? 0,
            GripCount: root is null ? 0 : CountGrips(root),
            ContentKind: window.Content?.GetType().Name ?? "(null)");

        // 关窗会把窗口几何写进 windows.json（见 ChromeWindow.OnClosingPersistGeometry）——
        // 这正是每个用例都要套 DataRootSandbox 的原因。
        window.Close();
        UiTestHost.Pump();

        return snapshot;
    }

    /// <summary>
    /// 数出 <see cref="ChromeWindow"/> 注入的八向缩放手柄：它们是注入到根 Grid 上的一个
    /// overlay（<c>Grid.SetRowSpan(overlay, 2)</c>），内含 4 个 4px 边手柄 + 4 个 10×10 角手柄，
    /// 全部是 <see cref="Brushes.Transparent"/> 的 <see cref="Border"/>。
    /// </summary>
    private static int CountGrips(Grid root) =>
        root.Children.OfType<Grid>()
            .Where(g => Grid.GetRowSpan(g) == 2)
            .SelectMany(g => g.Children.OfType<Border>())
            .Count(b => (b.Width == 4 && b.Height == 0)
                     || (b.Width == 0 && b.Height == 4)
                     || (b.Width == 10 && b.Height == 10));

    private readonly record struct Snapshot(
        int VisualCount,
        int NamedCount,
        HashSet<string> VisualTypeNames,
        bool HasBorderShell,
        bool HasRootGrid,
        int RowCount,
        int GripCount,
        string ContentKind);
}
