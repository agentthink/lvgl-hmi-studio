using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvaloniaUI = FluentAvalonia.UI.Controls;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **输入控件接线的共享工具**（阶段 190 立，阶段 195 从 <c>TextBoxWiringFace</c> 改名）——
/// 普查面与各视图的驱动面共用同一套扫描/驱动原语。现在管**两个族**：
///
/// <list type="bullet">
/// <item><b>TextBox 族</b>：<c>X.TextChanged += …</c>（普查面 ⇒
/// <see cref="AppTextBoxWiringCensusTests"/>）。⚠ 程序化写 <c>TextBox.Text</c>
/// **不触发** <c>TextChanged</c>（阶段 187 实测）⇒ 驱动必须**补发** <c>TextChangedEvent</c>，
/// 否则驱动的不是生产路径。补发不会让判据恒真：接线缺失时补发也没有人接（阶段 188 负控 nc1 证明）。</item>
/// <item><b>数字框族</b>（阶段 195）：<c>X.ValueChanged += …</c>，元素是 <c>NumericUpDown</c> 或
/// <c>fa:FANumberBox</c>（后者继承前者）。⚠ 与 TextBox **相反**：程序化写 <c>Value</c>
/// **会**触发 <c>ValueChanged</c>（阶段 188 在 <c>NumAutoExportInterval</c> 上实测）⇒
/// 驱动就是写值，不需要补发事件。</item>
/// <item><b>XAML 属性式</b>（阶段 199 量出来、单独成面 ⇒
/// <see cref="AppXamlAttributeWiringFaceTests"/>）：<c>&lt;TextBox TextChanged="OnX"/&gt;</c>。
/// 上面两个扫描原语**都扫不到它** —— 26 个具名 TextBox 就藏在那个缝里（专题
/// <c>docs/2026-10-01-xaml-attribute-wiring-face.md</c> 记了这次勘误）。</item>
/// </list>
///
/// <para><b>为什么要抽出来：</b>每个驱动判据都要做一次**同一份**双向差集
/// （XAML 里的具名控件 ↔ 代码里的接线 ↔ 表里的驱动条目）。两处各写一份正则，迟早会漂 ——
/// 漂了之后「普查说 7 条、驱动面说 6 条」这种读数只能靠人肉发现。所以扫描原语与差集逻辑
/// 只有**一处**实现，两个族共用它（阶段 195 把差集抽成 <c>AssertBidirectional</c>）。</para>
/// </summary>
internal static class ControlWiringFace
{
    /// <summary>视图源码目录（相对仓库根）。</summary>
    internal const string ViewsRelative = "src/HmiEditor.App/Views";

    // ────────────────────────── 扫描原语 ──────────────────────────

    /// <summary>
    /// 去掉 XAML 注释（<c>&lt;!-- … --&gt;</c>）—— 注释里被"停用"的控件**不算**在任何一面里。
    /// （阶段 199 的自测当场抓到：不剥注释时，注释里那句
    /// <c>&lt;TextBox x:Name="InProse" TextChanged="OnGhost"/&gt;</c> 会被当成一条活接线。
    /// 当时实测 `Views/` 里注释掉的具名控件是 **0 个**，所以剥了不改任何读数。）
    /// </summary>
    private static string StripXamlComments(string axaml) =>
        Regex.Replace(axaml, @"<!--.*?-->", " ", RegexOptions.Singleline);

    /// <summary>按元素正则取"具名控件"（标签内第一个 <c>x:Name</c>）。</summary>
    private static HashSet<string> NamedBoxes(string axaml, string elementAlternation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var pattern = $@"<(?:{elementAlternation})\b[^>]*>";
        foreach (Match tag in Regex.Matches(StripXamlComments(axaml), pattern, RegexOptions.Singleline))
        {
            var name = Regex.Match(tag.Value, @"x:Name=""([A-Za-z_]\w*)""");
            if (name.Success) names.Add(name.Groups[1].Value);
        }

        return names;
    }

    /// <summary>从 XAML 里取出所有 <c>&lt;TextBox x:Name="…"&gt;</c>。</summary>
    internal static HashSet<string> NamedTextBoxes(string axaml) => NamedBoxes(axaml, "TextBox");

    /// <summary>
    /// 从 XAML 里取出所有具名**数字框**：<c>NumericUpDown</c> 与 <c>fa:FANumberBox</c>
    /// （后者是 FluentAvalonia 的子类；本仓两种写法都在用）。
    /// </summary>
    internal static HashSet<string> NamedNumberBoxes(string axaml) =>
        NamedBoxes(axaml, @"fa:FANumberBox|NumericUpDown");

    /// <summary>从**已剥注释**的代码里取出所有 <c>X.&lt;事件&gt; += …</c> 的 X 名。</summary>
    private static HashSet<string> WiredBy(string strippedCode, string eventName) =>
        Regex.Matches(strippedCode, $@"(\w+)\.{eventName}\s*\+=")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>从**已剥注释**的代码里取出所有 <c>X.TextChanged += …</c> 的 X 名。</summary>
    internal static HashSet<string> WiredNames(string strippedCode) => WiredBy(strippedCode, "TextChanged");

    /// <summary>从**已剥注释**的代码里取出所有 <c>X.ValueChanged += …</c> 的 X 名。</summary>
    internal static HashSet<string> WiredByValueChanged(string strippedCode) => WiredBy(strippedCode, "ValueChanged");

    // ── 第三个接线形态：**XAML 属性式**（阶段 199 量出来）──────────────────

    /// <summary>
    /// 从 XAML 里取出**用属性写的事件接线**（第三个形态）：<c>&lt;TextBox x:Name="X" TextChanged="OnX"/&gt;</c>
    /// 的「控件名 ⇒ 处理器名」表（属性值非空才算接线）。
    ///
    /// <para>⚠ **阶段 199 的勘误**：<see cref="WiredNames"/> / <see cref="WiredByValueChanged"/> 只认
    /// code-behind 的 <c>X.&lt;事件&gt; += …</c>，于是这两个形态被当成了"没接线"。实测
    /// <c>Views/</c> 下有 **26 个具名 TextBox** 是属性式接的，而且**没有一条**同时出现在 code-behind 里
    /// （两形态互斥）⇒ 阶段 189~198 那几张账里"没有 TextChanged 接线"的说法对这 26 个框是**错的**。
    /// 已有的先例：选择族面（<c>SelectionFaceClassificationGuardTests</c>）从阶段 115 起就把三种形态
    /// （XAML 属性 / code-behind <c>+=</c> / 辅助函数注入）一起算。数字框族实测是 **0 条**属性式。</para>
    /// </summary>
    internal static Dictionary<string, string> XamlWiredBy(string axaml, string elementAlternation, string eventName)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match tag in Regex.Matches(StripXamlComments(axaml), $@"<(?:{elementAlternation})\b[^>]*>", RegexOptions.Singleline))
        {
            var name = Regex.Match(tag.Value, @"x:Name=""([A-Za-z_]\w*)""");
            if (!name.Success) continue;

            var handler = Regex.Match(tag.Value, $@"\b{eventName}=""([^""]+)""");
            if (!handler.Success || handler.Groups[1].Value.Length == 0) continue;

            map[name.Groups[1].Value] = handler.Groups[1].Value;
        }

        return map;
    }

    /// <summary><c>&lt;TextBox x:Name="X" TextChanged="…"/&gt;</c>（属性式；阶段 199 实测 26 条）。</summary>
    internal static Dictionary<string, string> XamlWiredTextBoxes(string axaml) =>
        XamlWiredBy(axaml, "TextBox", "TextChanged");

    /// <summary><c>&lt;NumericUpDown|fa:FANumberBox x:Name="X" ValueChanged="…"/&gt;</c>（属性式；实测 0 条）。</summary>
    internal static Dictionary<string, string> XamlWiredNumberBoxes(string axaml) =>
        XamlWiredBy(axaml, @"fa:FANumberBox|NumericUpDown", "ValueChanged");

    // ────────────────────────── 驱动原语 ──────────────────────────

    /// <summary>在可视树里按名取控件（取不到当场红）。</summary>
    internal static T Find<T>(Control root, string name) where T : Control
    {
        var found = root.FindControl<T>(name);
        Assert.NotNull(found);
        return found!;
    }

    /// <summary>
    /// 把文本写进框里 ＋ **按框架的做法补发 <c>TextChangedEvent</c>**（真实输入时框架会做这一步），
    /// 再推进渲染若干轮让副作用落地。
    /// </summary>
    internal static void Write(Control root, string name, string text, int pump = 2) =>
        Write(root, Find<TextBox>(root, name), text, pump);

    /// <summary>
    /// 同上，但**直接给框**（面 B 那些按名找不到的框走这条：调用方自己按可视树/坐标拿到它们 ——
    /// 阶段 194）。
    /// </summary>
    internal static void Write(Control root, TextBox box, string text, int pump = 2)
    {
        box.Text = text;
        box.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
        UiTestHost.Pump(pump);
    }

    /// <summary>
    /// **数字框**的驱动：写 <c>Value</c> 就够 —— 与 TextBox 相反，程序化赋值**会**触发
    /// <c>ValueChanged</c>（阶段 188 实测）。写**同值**时框架不发事件，所以手势要用不同的值。
    ///
    /// <para>⚠ 本仓的"数字框"有**两种类型**，值类型还不一样：<c>NumericUpDown.Value</c> 是
    /// <c>decimal?</c>，而 <c>fa:FANumberBox.Value</c> 是 <c>double?</c>（后者**不继承**
    /// 前者 —— FluentAvalonia 自己的控件，属性名也换成了 <c>MinValue/MaxValue/StepValue</c>）
    /// ⇒ 这里按**运行时类型**分派，调用方一律传 <c>double</c>。</para>
    /// </summary>
    internal static void WriteNumber(Control root, string name, double value, int pump = 2)
    {
        var box = FindControlByName(root, name);
        switch (box)
        {
            case FluentAvaloniaUI.FANumberBox fanumber:
                fanumber.Value = value;
                break;
            case NumericUpDown nud:
                nud.Value = (decimal)value;
                break;
            default:
                Assert.Fail($"'{name}' 是 {box.GetType().Name}，不是数字框（`NumericUpDown` / `fa:FANumberBox`）");
                break;
        }

        UiTestHost.Pump(pump);
    }

    /// <summary>读数字框的当前值（两种类型都支持）；不是数字框就当场红。</summary>
    internal static double? NumberValue(Control root, string name) => FindControlByName(root, name) switch
    {
        FluentAvaloniaUI.FANumberBox fanumber => fanumber.Value,
        NumericUpDown nud => (double?)nud.Value,
        var other => throw new InvalidOperationException(
            $"'{name}' 是 {other.GetType().Name}，不是数字框（`NumericUpDown` / `fa:FANumberBox`）"),
    };

    /// <summary>
    /// 按名取控件但**不限定类型** —— 数字框族必须先走这条：
    /// `FindControl&lt;NumericUpDown&gt;` 对 <c>fa:FANumberBox</c> **不成立**（它不继承
    /// <c>NumericUpDown</c>，Avalonia 的 `Find&lt;T&gt;` 会当场抛）。
    /// </summary>
    internal static Control FindControlByName(Control root, string name)
    {
        var found = root.FindControl<Control>(name);
        Assert.NotNull(found);
        return found!;
    }

    // ────────────────────────── 面 B 认账 ──────────────────────────

    /// <summary>
    /// **面 B 的逐条认账（TextBox 族）**（阶段 194）：某个文件的接线**全部按名找不到**时用这条 ——
    /// 声明的那批接线标识必须与扫描结果**完全一致**（两个方向都判），而且它们**都不许**是具名框
    /// （按名找得到的框该走 <see cref="AssertFullyDriven"/>，不许往暗面里塞）。
    ///
    /// <para><paramref name="scanFloor"/> 是扫描面自检（扫到的接线数不低于实测值）。</para>
    /// </summary>
    internal static void AssertAnonymousFace(
        string axamlFileName, IReadOnlyCollection<string> anonymousWired, int scanFloor) =>
        AssertAnonymousCore(axamlFileName, anonymousWired, scanFloor,
            "TextBox", "TextChanged", NamedTextBoxes, WiredNames);

    /// <summary>**面 B 的逐条认账（数字框族）**（阶段 195）—— 与 TextBox 版同形。</summary>
    internal static void AssertAnonymousNumberFace(
        string axamlFileName, IReadOnlyCollection<string> anonymousWired, int scanFloor) =>
        AssertAnonymousCore(axamlFileName, anonymousWired, scanFloor,
            "数字框", "ValueChanged", NamedNumberBoxes, WiredByValueChanged);

    private static void AssertAnonymousCore(
        string axamlFileName,
        IReadOnlyCollection<string> anonymousWired,
        int scanFloor,
        string family,
        string eventName,
        Func<string, HashSet<string>> declaredOf,
        Func<string, HashSet<string>> wiredOf)
    {
        var axamlPath = Path.Combine(SourceScanner.RepoRoot, ViewsRelative, axamlFileName);
        var declared = declaredOf(File.ReadAllText(axamlPath));
        var wired = wiredOf(SourceScanner.StripComments(File.ReadAllText(axamlPath + ".cs")));
        var claimed = anonymousWired.ToHashSet(StringComparer.Ordinal);

        Assert.True(wired.Count >= scanFloor,
            $"{axamlFileName}.cs 只扫到 {wired.Count} 条 {eventName} 接线 —— 扫描面可能塌了（下限 {scanFloor}）");

        var missing = wired.Where(n => !claimed.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var stale = claimed.Where(n => !wired.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var namedButClaimed = claimed.Where(n => declared.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            $"{axamlFileName}.cs 里这些接线没有认账（新接线必须进面 B 表）：{string.Join(", ", missing)}");
        Assert.True(stale.Length == 0,
            $"面 B 表里写了这些接线标识，但 {axamlFileName}.cs 里没有（改名后要同步）：{string.Join(", ", stale)}");
        Assert.True(namedButClaimed.Length == 0,
            $"这些名字其实是**具名{family}**（`x:Name` 里有）—— 该走驱动面，不许当「按名找不到」认账："
            + string.Join(", ", namedButClaimed));
    }

    // ────────────────────────── 单文件双向差集 ──────────────────────────

    /// <summary>
    /// **单文件双向差集（TextBox 族）**：把某个视图从「只普查」升级成「已驱动」时，表里必须恰好覆盖
    /// 该文件的**每个具名 <c>TextBox</c>** 与**每条 <c>TextChanged</c> 接线** ——
    /// 少一条是「接了线没人驱动」，多一条是「表里说接了其实没接」。
    ///
    /// <para><b>「没（直接）接线」也要显式登记（阶段 191 补的口径，阶段 194 说明）</b>：一个具名框
    /// **没有直接的** <c>X.TextChanged +=</c> **不是**缺口 —— 它可能是走"点保存/应用"被一次性读取，
    /// 也可能是**辅助函数注入**（`AttachChangeHandler(TxtAuthor)`）或**代表见证**（由同一个处理器
    /// 的另一条手势代跑）。这些都必须在 <paramref name="withoutWiring"/> 里显式登记并写清**是哪一种**。
    /// 这样"既没驱动、也没登记"的新框会当场红，而"没接线"这件事也不会被悄悄当成已覆盖。</para>
    ///
    /// <para><b>面 B 的接线要单独认账（阶段 193 补）</b>：有些文件同时有"按名找不到"的接线
    /// （代码里 <c>new</c> 出来的框，或<b>辅助函数里的泛型参数</b>）—— 它们属于普查面的**面 B**。
    /// 把它们放进 <paramref name="anonymousWired"/> 才算"这条接线有主"；反过来，
    /// **按名找得到**的框不许记在这里。</para>
    ///
    /// <para><paramref name="scanFloor"/> 是扫描面自检：枚举失效（改名、目录挪走）时
    /// 两边的集合都会塌成空集、差集恒空 ⇒ 判据会**假绿**，所以先断言扫到的条数不低于实测值。</para>
    /// </summary>
    internal static void AssertFullyDriven(
        string axamlFileName,
        IReadOnlyCollection<string> driven,
        int scanFloor,
        IReadOnlyDictionary<string, string>? withoutWiring = null,
        IReadOnlyCollection<string>? anonymousWired = null) =>
        AssertBidirectional(axamlFileName, "TextBox", "TextChanged", driven, scanFloor,
            withoutWiring, anonymousWired, NamedTextBoxes, WiredNames);

    /// <summary>
    /// **单文件双向差集（数字框族）**（阶段 195）—— 与 TextBox 版**同形**，只换族与事件名：
    /// 具名 <c>NumericUpDown</c>/<c>fa:FANumberBox</c> ↔ <c>ValueChanged</c> 接线 ↔ 驱动条目。
    /// </summary>
    internal static void AssertFullyDrivenNumberBoxes(
        string axamlFileName,
        IReadOnlyCollection<string> driven,
        int scanFloor,
        IReadOnlyDictionary<string, string>? withoutWiring = null,
        IReadOnlyCollection<string>? anonymousWired = null) =>
        AssertBidirectional(axamlFileName, "数字框", "ValueChanged", driven, scanFloor,
            withoutWiring, anonymousWired, NamedNumberBoxes, WiredByValueChanged);

    private static void AssertBidirectional(
        string axamlFileName,
        string family,
        string eventName,
        IReadOnlyCollection<string> driven,
        int scanFloor,
        IReadOnlyDictionary<string, string>? withoutWiring,
        IReadOnlyCollection<string>? anonymousWired,
        Func<string, HashSet<string>> declaredOf,
        Func<string, HashSet<string>> wiredOf)
    {
        var axamlPath = Path.Combine(SourceScanner.RepoRoot, ViewsRelative, axamlFileName);
        var declared = declaredOf(File.ReadAllText(axamlPath));
        var wired = wiredOf(SourceScanner.StripComments(File.ReadAllText(axamlPath + ".cs")));
        var drivenSet = driven.ToHashSet(StringComparer.Ordinal);
        var noWiringSet = (withoutWiring ?? new Dictionary<string, string>()).Keys.ToHashSet(StringComparer.Ordinal);
        var anonSet = (anonymousWired ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= scanFloor,
            $"{axamlFileName} 只扫到 {declared.Count} 个具名{family} —— 扫描面可能塌了（下限 {scanFloor}）");
        Assert.True(wired.Count >= scanFloor,
            $"{axamlFileName}.cs 只扫到 {wired.Count} 条 {eventName} 接线 —— 扫描面可能塌了（下限 {scanFloor}）");

        var undrivenDeclared = declared
            .Where(n => !drivenSet.Contains(n) && !noWiringSet.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var undrivenWired = wired.Where(n => !drivenSet.Contains(n) && !anonSet.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var stale = drivenSet.Where(n => !declared.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var notWired = drivenSet.Where(n => !wired.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // 登记表自己的双向差集
        var orphanRegistered = noWiringSet.Where(n => !declared.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var registeredButWired = noWiringSet.Where(n => wired.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var doubleRegistered = noWiringSet.Where(n => drivenSet.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // 面 B 登记表自己的双向差集
        var staleAnonymous = anonSet.Where(n => !wired.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var namedAnonymous = anonSet.Where(n => declared.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var doubleAnonymous = anonSet.Where(n => drivenSet.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(undrivenDeclared.Length == 0,
            $"{axamlFileName} 里这些具名{family}既没被驱动条目覆盖、也不在「没有接线」登记表里"
            + $"（没接线不是缺口，但必须显式登记 ＋ 写理由）：{string.Join(", ", undrivenDeclared)}");
        Assert.True(undrivenWired.Length == 0,
            $"{axamlFileName}.cs 里这些 {eventName} 接线没有驱动条目"
            + $"（接了线却没人驱动 ⇒ 与 #88 同一个形状；面 B 的接线请显式记进 anonymousWired）："
            + string.Join(", ", undrivenWired));
        Assert.True(stale.Length == 0,
            $"驱动面表里写了这些{family}，但 {axamlFileName} 里没有：{string.Join(", ", stale)}");
        Assert.True(notWired.Length == 0,
            $"驱动面表里声明了这些 {eventName} 接线，代码里却没有：{string.Join(", ", notWired)}");
        Assert.True(orphanRegistered.Length == 0,
            $"「没有接线」登记表里写了这些{family}，但 {axamlFileName} 里没有"
            + $"（名字写歪就等于漏登记）：{string.Join(", ", orphanRegistered)}");
        Assert.True(registeredButWired.Length == 0,
            $"「没有接线」登记表里写了这些框，但它们其实**接了** {eventName}"
            + $"（该进驱动面，不许在登记表里躲着）：{string.Join(", ", registeredButWired)}");
        Assert.True(doubleRegistered.Length == 0,
            $"这些框同时出现在驱动面和「没有接线」登记表里（两处只能有一处）：{string.Join(", ", doubleRegistered)}");
        Assert.True(staleAnonymous.Length == 0,
            $"面 B 登记表里写了这些接线，但 {axamlFileName}.cs 里没有：{string.Join(", ", staleAnonymous)}");
        Assert.True(namedAnonymous.Length == 0,
            $"面 B 登记表里写了这些框，但它们**按名找得到**（该进驱动面，不许往暗面里塞）："
            + string.Join(", ", namedAnonymous));
        Assert.True(doubleAnonymous.Length == 0,
            $"这些名字同时出现在驱动面和面 B 登记表里：{string.Join(", ", doubleAnonymous)}");
    }
}
