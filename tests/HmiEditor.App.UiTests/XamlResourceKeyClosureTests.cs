using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **资源键闭合守卫** —— XAML / 代码里引用到的每一个资源键，都必须真的能解析出来。
///
/// <para><b>为什么需要它（2026-09-20 实测）：</b>Avalonia 12 里 <c>{StaticResource}</c> 的键
/// 写错在编译期**完全不可见**（0 warning / 0 error，<c>App.dll</c> 确实重建）。运行期有**两种**形态，
/// 都不可依赖：
/// <list type="number">
///   <item><b>落在控件的样式属性上</b>（<c>Background=</c>）⇒ **静默跳过赋值**，属性回落到主题默认值。
///     复现实测（把 <c>HmiWarningDeepBrush</c> 的定义键改名）：<c>CommunicationConfigView.BtnAddSlave</c> /
///     <c>BtnBatchAddSlaveVars</c> / <c>VariableManagerView.BtnValidateIntegrity</c> 的
///     <c>Background</c> 全是 <c>#0FFFFFFF</c>（Fluent 暗色主题的默认按钮底）—— 不抛、不报，
///     只是**颜色错了**。这是最危险的一种，本轮的主缺陷就是它。</item>
///   <item><b>落在资源字典里的对象上</b>（<c>App.axaml</c> 里 <c>HmiWarningDeepBrush</c> 的
///     <c>Color="{StaticResource HmiWarningDeep}"</c>）⇒ 解析被推迟（<c>DeferredResourceReference</c>），
///     字典里"看着有值"；谁去**读那个属性**谁才吃到
///     <c>KeyNotFoundException: Static resource 'HmiWarningDeep' not found.</c> ——
///     异常没消失，只是挪到了读取点（可能在千里之外的 UI 代码里）。</item>
/// </list>
/// 实证：<c>HmiWarningDeepBrush</c> 这个键在本仓**从未被定义过**
/// （<c>git log --all -S</c> 只查到 3 处引用、没有任何一处定义），
/// 于是那 3 个按钮的背景一直是默认灰，而全仓 3000 条判据没有一条会红。</para>
///
/// <para><b>为什么 <see cref="ViewSmokeUiTests"/> 兜不住（两条，实测）：</b>
/// ①那层的注释写着它能接住"XAML 里的 <c>{StaticResource ...}</c> 名字写错" —— **不成立**：
/// 它能接住的是「缺失导致<b>布局期抛异常</b>」那一类（缺 <c>DataTemplate</c>、缺 <c>ControlTheme</c>），
/// 而缺一个 <c>Brush</c> 只是属性没被赋值，构造、布局、渲染三步全都不报错；
/// ②那层的清单口径是 <c>typeof(UserControl).IsAssignableFrom(t)</c> ⇒
/// <b>26 个基于 <c>ChromeWindow</c>（<c>: Window</c>）的视图根本不在它的面里</b>，
/// 而这 3 个受害控件恰好全在那些窗口里（<c>CommunicationConfigView</c> /
/// <c>VariableManagerView</c>）。换句话说：冒烟测试的<b>注释高估了它自己</b>，
/// 这一层才是那句话本该落地的位置。</para>
///
/// <para><b>两条腿，缺一个都不算通过：</b>
/// <list type="number">
///   <item><b>静态定义</b> —— 仓库内 <c>.axaml</c> 的 <c>x:Key</c>；
///     <c>AppSourceScanner.ScanAppXaml</c> 递归 <c>src/HmiEditor.App</c>，因此
///     <c>Assets/Icons.axaml</c>（靠 <c>ResourceInclude</c> 挂进来的）也在面内。</item>
///   <item><b>运行期解析</b> —— <c>Application.Current.Resources.TryGetResource</c>，
///     与产品自己的取值口径 <see cref="HmiEditor.App.Theme.Brush"/> **同源**。
///     这条腿接住静态面外的东西：Dock 预设（来自 NuGet 包，
///     <c>avares://Dock.Avalonia.Themes.Fluent/Presets/Ide/Default.axaml</c>）、
///     以及视图自己的 <c>&lt;UserControl.Resources&gt;</c>（这类键在 App 作用域
///     静态可见但运行期查不到，只有静态那条腿能兜）。</item>
/// </list>
/// 幽灵键（拼写漂移 / 从未定义）两条腿都不过 ⇒ 红。</para>
///
/// <para><b>剥注释是前提，不是优化：</b>引用侧必须先 <see cref="SourceScanner.StripXmlComments"/>
/// —— <c>App.axaml:20</c> 的XML 注释里就写着 <c>（使用 {StaticResource IconXxx} 引用…）</c>，
/// 不剥的话 <c>IconXxx</c> 会被报成"引用了从未定义"的假缺口（本轮实测踩到）。</para>
///
/// <para><b>本类没做的事（有意）：</b>只查"键能不能解析"，不查"解析出来的东西**类型对不对**"。
/// 🩸 当初这里写的是「那类不匹配在 Avalonia 里同样是静默的」—— **该说法已于 2026-09-22
/// （阶段 69）被实测推翻**：`ColorToBrushConverter` 就是为 `Color → IBrush` 写的
/// （`StaticResourceExtension.ProvideValue` 会把目标属性的 `PropertyType` 传进去），
/// 三类 XAML 失效**各有拦截点** —— 缺失键才是唯一真静默（本类守它）、
/// **类型不匹配在构造期抛 `InvalidCastException` 且带行号**、**绑定路径 typo 是编译期 `AVLN2000`**。
/// ⇒ 这条不是"本类没做的事"，而是"本来就不需要在这里守"。见登记 #38① 与阶段 69。</para>
///
/// <para><b>反向闭合（阶段 71 补）：</b>上面几条都只走「引用 ⇒ 定义」这一个方向。
/// 反方向「定义 ⇒ 有人引用」从 2026-09-20 建类起一直是空的 —— 实测 App 根下
/// **50 个 `x:Key` 定义了却取不到引用**，其中 **21 个是真缺陷**：绘图工具栏/控件快捷的几何
/// 被**逐字内联复制**进了 `MainWindow.axaml`（同一份几何两处存放，改一处不改另一处会静默分叉）。
/// 现在两条方向都有判据，引用面也从 3 类扩到 5 类（见 <see cref="ManagedResourceWrite"/> /
/// <see cref="ThemeAccessorRef"/> 的注释：**每类证据强度不同**）。</para>
/// </summary>
public class XamlResourceKeyClosureTests
{
    /// <summary>一次引用：键、文件（相对 App 根）、行号、面（xaml / cs）。</summary>
    private sealed record RefSite(string Key, string Relative, int Line, string Kind);

    /// <summary>XAML 侧取值：<c>{StaticResource Key}</c> / <c>{DynamicResource Key}</c>。</summary>
    private static readonly Regex XamlRef = new(
        @"\{(?:Static|Dynamic)Resource\s+(?<key>[A-Za-z_][A-Za-z0-9_.\-]*)", RegexOptions.Compiled);

    /// <summary>代码侧取值：<c>Resources["Key"]</c> / <c>FindResource("Key")</c> / <c>TryFindResource("Key")</c>。</summary>
    private static readonly Regex CodeRef = new(
        @"(?:\.Resources\s*\[|FindResource\s*\(|TryFindResource\s*\()\s*""(?<key>[^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// **第 4 类引用形状**（阶段 71 补）：<c>AppThemeService</c> 对资源的**写入** ——
    /// <c>SetOrUpdateBrush(resources, "Key", hex)</c> / <c>SetOrUpdateColor(resources, "Key", hex)</c>。
    ///
    /// <para><b>为什么单列一类、且要写明它证明什么：</b>键是**字符串字面量**，但调用形状
    /// （<c>helper(字典, "键", 值)</c>）不属于 <see cref="CodeRef"/> 的任何一种 ⇒ 原来扫不到，
    /// 于是这 18 个键被误判成"从未被引用"（实测：真实数目是 50 里的 18 个）。
    /// ⚠ **它只证明"本应用在按名字管理这个键"，不证明"有人读它"** ——
    /// 读它的消费者在**包外**：FluentTheme 的 `MenuBase`/`ContextMenu`/`Popup` 模板
    /// 与 Dock 主题（`App.axaml` 那段注释自己写着"必须在主字典中定义以确保 ApplyPalette
    /// 能找到并更新它们"）。所以这一类的语义是「**活着的被管理资源**」，不是「被消费的键」。</para>
    /// </summary>
    private static readonly Regex ManagedResourceWrite = new(
        @"SetOrUpdate(?:Brush|Color)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*,\s*""(?<key>[^""]+)""",
        RegexOptions.Compiled);

    /// <summary>
    /// **第 5 类引用形状**（阶段 71 补）：本项目自己的取值口径
    /// <c>Theme.Brush("Key")</c> / <c>Theme.Color("Key")</c>（定义在 <c>Theme.cs:15,27</c>）。
    ///
    /// <para><b>为什么要单列：</b>它与 <see cref="CodeRef"/> 是**两套 API** ——
    /// `Theme.Brush` 内部才去碰 Avalonia 的资源字典，调用点只写键名。实测 26 处、
    /// 全在 <c>Theme.cs</c>（`public static IBrush CatIndustrial =&gt; Brush("HmiCatIndustrialBrush")`
    /// 这一族），旧引用面同样取不到 ⇒ 5 个 <c>HmiCat*Brush</c> 被误报成孤儿。</para>
    ///
    /// <para>形状刻意收紧到「**唯一实参且是标识符形状的字面量**」+ 负向环视排除
    /// <c>RenderResourceCache.Brush(...)</c> 这类同名但收颜色值的工厂：那个重载收的是
    /// <c>Color</c>/hex，不会把资源键名传进去（实测该重载的实参全是变量或
    /// <c>Color.Parse(...)</c>）。</para>
    /// </summary>
    private static readonly Regex ThemeAccessorRef = new(
        @"(?<![A-Za-z0-9_.])(?:Brush|Color)\s*\(\s*""(?<key>[A-Za-z][A-Za-z0-9_.]*)""\s*\)",
        RegexOptions.Compiled);

    /// <summary>定义侧：<c>x:Key="…"</c>。</summary>
    private static readonly Regex KeyDef = new(@"x:Key=""(?<key>[^""]+)""", RegexOptions.Compiled);

    /// <summary>问题清单最多列几条（超出只报数目）。</summary>
    private const int MaxListed = 40;

    private static int LineOf(string text, int index) => text[..index].Count(c => c == '\n') + 1;

    /// <summary>扫 XAML + 代码两侧的全部资源键引用点（五类形状）。</summary>
    private static List<RefSite> CollectRefs()
    {
        var sites = new List<RefSite>();

        foreach (var (relative, text) in AppSourceScanner.ScanAppXaml())
        {
            var masked = SourceScanner.StripXmlComments(text);
            foreach (Match m in XamlRef.Matches(masked))
                sites.Add(new RefSite(m.Groups["key"].Value, relative, LineOf(text, m.Index), "xaml"));
        }

        foreach (var (relative, text) in AppSourceScanner.ScanAppSources())
        {
            var code = SourceScanner.StripComments(text);
            foreach (Match m in CodeRef.Matches(code))
                sites.Add(new RefSite(m.Groups["key"].Value, relative, LineOf(code, m.Index), "cs"));
            foreach (Match m in ManagedResourceWrite.Matches(code))
                sites.Add(new RefSite(m.Groups["key"].Value, relative, LineOf(code, m.Index), "managed"));
            foreach (Match m in ThemeAccessorRef.Matches(code))
                sites.Add(new RefSite(m.Groups["key"].Value, relative, LineOf(code, m.Index), "theme"));
        }

        return sites;
    }

    /// <summary>静态定义侧：App 根下全部 <c>.axaml</c> 的 <c>x:Key</c>（已剥 XML 注释）。</summary>
    private static HashSet<string> DefinedKeys() =>
        AppSourceScanner.ScanAppXaml()
            .SelectMany(s => KeyDef.Matches(SourceScanner.StripXmlComments(s.Text))
                .Select(m => m.Groups["key"].Value))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 运行期解析侧：在 headless UI 线程上**一次派发**把整批键查完
    /// （逐键派发 = 137 次线程切换，没必要）。
    /// </summary>
    private static HashSet<string> RuntimeResolvable(IReadOnlyCollection<string> keys) =>
        UiTestHost.Run(() =>
        {
            var resources = Application.Current!.Resources;
            return keys.Where(k => resources.TryGetResource(k, null, out _))
                .ToHashSet(StringComparer.Ordinal);
        });

    // ──────────────────────────────────────────
    // 1. 面自检：扫描面为空/变窄时，下面两条判据会变成空跑恒绿
    // ──────────────────────────────────────────

    [Fact]
    public void Scanner_SeesBothSides()
    {
        var xaml = AppSourceScanner.ScanAppXaml();
        var cs = AppSourceScanner.ScanAppSources();
        var sites = CollectRefs();
        var keys = sites.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var defined = DefinedKeys();

        Assert.True(xaml.Count >= 45, $"只扫到 {xaml.Count} 个 .axaml（应 ≥45）—— 扫描面可能失效");
        Assert.True(cs.Count >= 110, $"只扫到 {cs.Count} 个 .cs（应 ≥110）—— 扫描面可能失效");
        Assert.True(keys.Count >= 120, $"只解析出 {keys.Count} 个不同的引用键（应 ≥120）—— 引用侧正则可能失效");
        Assert.True(defined.Count >= 170, $"只解析出 {defined.Count} 个 x:Key（应 ≥170）—— 定义侧正则可能失效");

        // 代码侧那条腿极易被静默掏空（正则改动后 0 命中 = 面消失但判据仍绿）
        Assert.True(sites.Count(s => s.Kind == "cs") >= 3,
            $"代码侧只解析到 {sites.Count(s => s.Kind == "cs")} 处引用（实测应 ≥3）—— Resources[...] 那条正则可能失效");

        // 定义侧至少要有 App.axaml 的调色板（否则可能是只扫到了 Icons.axaml 之类）
        Assert.Contains("HmiBgPanelBrush", defined);
    }

    // ──────────────────────────────────────────
    // 2. 运行期那条腿自身的正控 + 负控（同场）
    // ──────────────────────────────────────────

    [Fact]
    public void RuntimeLookup_IsUsableAsEvidence()
    {
        var (known, phantom) = UiTestHost.Run(() =>
        {
            var resources = Application.Current!.Resources;
            return (
                Known: resources.TryGetResource("HmiBgPanelBrush", null, out _),
                Phantom: resources.TryGetResource("HmiNoSuchKey_ClosureGuard", null, out _));
        });

        Assert.True(known,
            "已知键 HmiBgPanelBrush 解析不到 ⇒ App.axaml 资源字典没加载，'运行期解析'这条腿的前提不成立");
        Assert.False(phantom,
            "不存在的键却解析成功 ⇒ '解析不到'这条证据恒真，主判据会形同虚设（这正是要防的假门）");
    }

    // ──────────────────────────────────────────
    // 3. 主判据
    // ──────────────────────────────────────────

    [Fact]
    public void EveryReferencedResourceKey_IsDefinedOrResolvable()
    {
        var sites = CollectRefs();
        var defined = DefinedKeys();

        // 静态面外（外部主题预置、视图本地字典）的候选交运行期那条腿去判
        var candidates = sites.Select(s => s.Key)
            .Where(k => !defined.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var resolvable = RuntimeResolvable(candidates);

        var missing = sites
            .Where(s => !defined.Contains(s.Key) && !resolvable.Contains(s.Key))
            .GroupBy(s => s.Key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} ← " + string.Join("、", g
                .OrderBy(s => s.Relative, StringComparer.Ordinal)
                .ThenBy(s => s.Line)
                .Select(s => $"{s.Relative}:{s.Line}({s.Kind})")))
            .ToList();

        var shown = missing.Take(MaxListed);
        var more = missing.Count > MaxListed ? $"\n    …另有 {missing.Count - MaxListed} 个未列出" : "";

        Assert.True(missing.Count == 0,
            $"{missing.Count} 个资源键**既没有 x:Key 定义、运行期也解析不到** —— " +
            $"引用处会静默取空（编译不报、运行不抛）：\n    " + string.Join("\n    ", shown) + more);
    }

    // ──────────────────────────────────────────
    // 4. 行为判据：幽灵键真的会造成"取空"，修好后真的会取到
    // ──────────────────────────────────────────

    /// <summary>
    /// 2026-09-20 幽灵键事故的 3 个受害控件 —— 断言它们的 <c>Background</c> **真的**是
    /// 那个 warning-deep 画刷。
    ///
    /// <para><b>为什么静态闭合判据之外还要这一条：</b>静态那条只证明"键存在"，
    /// 不证明"这个引用点确实取到了值"（引用可能落在未实例化的模板里、或被后面的
    /// setter 覆盖）。这条走**真实控件属性**，是"静默取空"这个现象本体的判据。</para>
    ///
    /// <para>走<b>逻辑树</b>而不是可视树：这 3 个控件都在 <c>TabItem</c> / 折叠区内，
    /// 未选中的页签内容不进可视树（实测：<c>VariableManagerView</c> 的
    /// <c>BtnValidateIntegrity</c> 就不在）。而 <c>{StaticResource}</c> 是**装载期**求值的，
    /// 逻辑树上读到的就是当时赋进去的值。</para>
    ///
    /// <para>**不调 <c>Show()</c>**：这 3 个视图的基类是 <c>ChromeWindow</c>（<c>: Window</c>），
    /// 而 <c>ChromeWindow</c> 会在**打开时**恢复窗口几何、**关闭时**写盘
    /// （<c>WindowGeometryService</c>）—— 判据不该有这种副作用，而
    /// <c>{StaticResource}</c> 在构造函数里就求完了，不需要开窗。</para>
    /// </summary>
    [Fact]
    public void PhantomKeyVictimButtons_ActuallyCarryTheirBackground()
    {
        var (expected, probes) = UiTestHost.Run(() =>
        {
            var resources = Application.Current!.Resources;
            var color = resources.TryGetResource("HmiWarningDeepBrush", null, out var brush)
                ? (brush as ISolidColorBrush)?.Color
                : null;

            var found = new List<(string Where, Color? Actual)>();
            var views = new (Window View, string[] Names)[]
            {
                (new CommunicationConfigView(), ["BtnAddSlave", "BtnBatchAddSlaveVars"]),
                (new VariableManagerView(), ["BtnValidateIntegrity"]),
            };

            foreach (var (view, names) in views)
            {
                foreach (var name in names)
                {
                    var button = view.GetLogicalDescendants().OfType<Button>()
                        .FirstOrDefault(b => b.Name == name);
                    found.Add(($"{view.GetType().Name}.{name}",
                        (button?.Background as ISolidColorBrush)?.Color));
                }
            }

            return (color, found);
        });

        // 每条失败消息都带上"实测值"：只报"不符"会让人再去复现一次才能知道当时读到了什么
        var observed = "实测 " + string.Join("、", probes.Select(p =>
            $"{p.Where}={(p.Actual is null ? "未赋值" : p.Actual.ToString())}"));

        // 自检：控件没找到时"背景等于期望值"是空跑，必须单独钉住
        Assert.True(probes.Count == 3,
            $"只探查到 {probes.Count} 个控件（应 3 个）—— 逻辑树面可能失效。{observed}");

        Assert.True(expected is not null,
            "HmiWarningDeepBrush 解析不到 ⇒ XAML 里的 {StaticResource HmiWarningDeepBrush} 静默取空" +
            $"（这 3 个按钮的背景因此从未被赋过值）。{observed}");

        var wrong = probes
            .Where(p => p.Actual != expected!.Value)
            .Select(p => $"{p.Where}: Background={(p.Actual is null ? "未赋值" : p.Actual.ToString())} ≠ {expected}")
            .ToList();

        Assert.True(wrong.Count == 0,
            $"{wrong.Count}/{probes.Count} 个受害控件的背景不是 HmiWarningDeepBrush 的颜色：\n    "
            + string.Join("\n    ", wrong));
    }

    // ──────────────────────────────────────────
    // 5. 定义侧反向：定义的每一个 x:Key 必须「有人用」或进差异表
    // ──────────────────────────────────────────

    /// <summary>
    /// 定义侧差异表：**定义了、引用面却取不到**的键。
    /// <para>⚠ 这不是豁免表 —— 它是「本条判据**看不见**的那一部分」的逐条说明，
    /// 每条都必须写清**消费者是谁 / 为什么取不到**。双向判定：键一旦变得可达
    /// （说明引用面已经能表达它，这条理由作废）或从定义里消失 ⇒ 红，逼着人来删行
    /// （与 <c>LocalizationKeyReachabilityGuardTests.KeptUnreachable</c> 同一纪律）。</para>
    /// <para><b>建表时实测（2026-09-22，阶段 71）：</b>扩面 + 修掉 21 条几何重复之前，
    /// 这里是 **50 条**；之后只剩下面 3 条 —— 其中 2 条是同一个机制（主题变体字典键），
    /// 1 条消费者在包外。**没有一条是真死资源**。</para>
    /// </summary>
    private static readonly Dictionary<string, string> NotReferencedOnPurpose = new(StringComparer.Ordinal)
    {
        ["Light"] = "主题变体字典键（ResourceDictionary.ThemeDictionaries 的 x:Key）—— 由框架按 RequestedThemeVariant 选，永不显式引用",
        ["Dark"] = "同上：主题变体字典键（Dark）",
        ["DockToolChromeIconBrush"] = "消费者在包外 —— Dock 主题（Dock.Avalonia.Themes.Fluent.dll 里有该键名的字面量）；App.axaml 这里是有意覆盖它的默认值",
    };

    /// <summary>差异表键集（供跨判据核对 / 读数用）。</summary>
    internal static IReadOnlyCollection<string> NotReferencedOnPurposeKeys => NotReferencedOnPurpose.Keys;

    [Fact]
    public void EveryDefinedResourceKey_IsReferencedOrExplicitlyExempt()
    {
        var defined = DefinedKeys();
        var referenced = CollectRefs().Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        var orphans = defined
            .Where(k => !referenced.Contains(k) && !NotReferencedOnPurpose.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            $"{orphans.Count} 个 x:Key **定义了却没有任何引用面取得到** —— 两种可能，别混："
            + "① 它真的是死资源（删掉定义）；"
            + "② 引用面又漏了一种调用形状（**补形状，不要往差异表里塞** —— 塞进去等于把缺陷合法化）：\n    "
            + string.Join("\n    ", orphans.Take(MaxListed)));
    }

    /// <summary>
    /// 差异表的**过期检查**：表里的键必须仍然「已定义且仍取不到」。
    /// 少了任何一半都说明这张表在说假话。
    /// </summary>
    [Fact]
    public void UnreferencedExemptions_AreStillUnreferencedAndStillDefined()
    {
        var defined = DefinedKeys();
        var referenced = CollectRefs().Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        var stale = NotReferencedOnPurpose.Keys
            .Where(k => referenced.Contains(k) || !defined.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => $"{k}   （{NotReferencedOnPurpose[k]}）")
            .ToList();

        Assert.True(stale.Count == 0,
            "差异表里这些条目已经**不再适用** —— 键已可达（理由作废，删行）"
            + "或已从定义里消失（删行）：\n    " + string.Join("\n    ", stale));
    }

    /// <summary>
    /// 面自检：反向判据的两侧都不许退化 —— 定义面收 0 个键、或引用面一个都取不到时，
    /// 「没有孤儿」是**恒真**的（这正是 #48 那一族陷阱的形状）。
    ///
    /// <para>五类引用形状**各自**也要下界：本阶段刚补的 <c>managed</c>(③) 与 <c>theme</c>(④)
    /// 一旦正则写坏、一条都不命中，反向判据就会开始误报孤儿 —— 而"误报孤儿"会被当成
    /// "发现了缺陷"，比空跑更危险。</para>
    /// </summary>
    [Fact]
    public void ReverseClosureFace_IsNotDegenerate()
    {
        var refs = CollectRefs();
        var defined = DefinedKeys();
        var referenced = refs.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var both = defined.Intersect(referenced, StringComparer.Ordinal).Count();

        var byKind = refs.GroupBy(s => s.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var observed = $"定义 {defined.Count} / 引用面 {referenced.Count} / 交集 {both}；"
                     + "各类命中 " + string.Join("、", byKind.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                         .Select(kv => $"{kv.Key}={kv.Value}"));

        Assert.True(defined.Count >= 150, $"定义面只收到 {defined.Count} 个 x:Key —— 扫描面可能失效。{observed}");
        Assert.True(both >= 150, $"定义 ∩ 引用 只有 {both} 个 —— 有一侧的口径跑偏了。{observed}");

        // 下界跟着口径走：降了先确认是"引用点被删了"还是"正则写坏了"。
        var minimums = new (string Kind, int Min)[]
        {
            ("xaml", 1000), ("cs", 1), ("managed", 10), ("theme", 10),
        };
        var degenerate = minimums
            .Where(m => byKind.GetValueOrDefault(m.Kind) < m.Min)
            .Select(m => $"{m.Kind}: 命中 {byKind.GetValueOrDefault(m.Kind)} < 下界 {m.Min}")
            .ToList();

        Assert.True(degenerate.Count == 0,
            "有引用形状退化成了空集（反向判据会开始误报孤儿）：\n    "
            + string.Join("\n    ", degenerate) + "\n" + observed);
    }
}
