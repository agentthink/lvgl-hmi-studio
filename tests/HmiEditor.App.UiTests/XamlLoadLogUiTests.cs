using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Logging;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **XAML 装载期的 Avalonia 日志守卫** —— 建在登记表 §4 #38 遗留① 的复核过程中，
/// 但**它不是那一项的收口方式**：它是「其它机制都没接住的那一类故障」的**探测网**。
///
/// <para><b>先说清楚它与编译绑定的分工（2026-09-22 实测，阶段 69）：</b>
/// 本仓的 <c>AvaloniaUseCompiledBindingsByDefault</c> 是 <c>true</c>
/// （现已改成**显式声明**，见 <see cref="XamlCompiledBindingGuardTests"/>）
/// ⇒ XAML 里的 <c>{Binding}</c> 在**编译期**就解析完了。实测三类注入的落点：</para>
/// <list type="bullet">
///   <item><description><b>绑定 typo</b>（`DataTemplate` 里绑一个不存在的属性）⇒
///     **编译期** <c>error AVLN2000</c>，根本到不了本层；</description></item>
///   <item><description><b>类型不匹配</b>（`StreamGeometry` 塞给 `IBrush` 属性）⇒
///     **构造期** <c>InvalidCastException</c>，由 <see cref="WindowProductionPathUiTests"/> 抓，
///     也不走 <c>Logger</c>；</description></item>
///   <item><description><b>资源键缺失</b> ⇒ 唯一真静默的那类，由
///     <see cref="XamlResourceKeyClosureTests"/> 的**静态面**守。</description></item>
/// </list>
/// <para>⇒ 今天**没有已知的产品侧缺陷**只被本层覆盖。本层的价值因此不在「抓今天这条缺陷」，
/// 而在三件事：① 它第一次把 <c>MainWindow</c> 铺进了 XAML 冒烟面 —— 它在命名空间
/// <c>HmiEditor.App</c>，不在 <c>WindowViewSmokeUiTests.WindowCatalog()</c> 的
/// <c>HmiEditor.App.Views</c> 口径里，全仓 XAML 面最大的那个窗口此前从没被装载过
/// （补进来第一条就吐出了 5 条 Dock 噪音）；② 它把「Avalonia 的错误出口**存在且可接**」
/// 钉成一条自检（<see cref="Sink_ReceivesAvaloniaOwnFailures"/>）—— 全仓 <c>grep ILogSink</c>
/// 零命中，此前没人知道这个出口长什么样；③ 它是一张**探测网**：<c>{StaticResource}</c> 的延迟赋值、
/// Dock 重建后的新实例、运行期才发生的绑定失败，这些编译期看不见的路径一旦出问题，它会发出声音。</para>
///
/// <para><b>#38 遗留① 的原文定性已被实测推翻（留档，免得下一个人照它去改代码）：</b>
/// 那条遗留的依据是「<c>{StaticResource X}</c> 取到 <c>Color</c> 而属性要 <c>IBrush</c>
/// 这类不匹配在 Avalonia 里同样是静默的」。<b>这个例子不成立</b> ——
/// <c>StaticResourceExtension.ProvideValue</c> 把**目标属性的 <c>PropertyType</c>**
/// 一路传给 <c>ColorToBrushConverter.Convert(value, targetType)</c>，而那个转换器的全部内容就是
/// 「<c>targetType == typeof(IBrush)</c> 且值是 <c>Color</c> ⇒ 包成 <c>ImmutableSolidColorBrush</c>」；
/// 两条路径（立即赋值 / 延迟赋值）**都调它**。<c>Color</c> → <c>IBrush</c> 是被专门支持的用法。</para>
///
/// <para><b>真正的机制（同一份 IL 里的下一层）：</b>控件的样式属性走的是**延迟赋值** ——
/// <c>DelayedBinding.Add(element, IPropertyInfo, Func&lt;…&gt;)</c>，
/// 而这个延迟项被执行时是这么写的：</para>
/// <code>
/// try   { Property.Set(control, Value(control)); }          // ← 真正赋值，类型不对就在这里炸
/// catch (Exception) { Logger.TryGet(LogEventLevel.Error, "Property")
///                       .Log("Error setting {Property} on {Target}: {Exception}", …); }
/// </code>
/// <para>即：<b>异常是真的会发生，只是被吞进了 Avalonia 的 <c>Logger</c>（Error 级 / area <c>Property</c>）</b>。
/// 「静默」的准确含义不是「跳过赋值」，而是「**异常没有能看见它的出口**」——
/// 全仓 <c>grep ILogSink</c> 零命中，<c>Logger.Sink</c> 从来没被设置过。所以：</para>
///
/// <para><b>判据 = 装载期一条 Warning 及以上的日志都不许有</b>（唯一的豁免见
/// <see cref="IsFrameworkNoise"/>）。它同时覆盖 ①键缺失（<c>XamlResourceKeyClosureTests</c>
/// 那一族守的）②类型不匹配 ③任何其它让赋值/绑定失败的原因 —— 因为它们在 Avalonia 侧
/// 都汇到同一个出口。这就是「判现象本体，不判代理指标」。</para>
///
/// <para><b>sink 必须装在平台初始化之前：</b><c>App.axaml</c> 是在
/// <c>EnsureIsolatedApplication()</c>（每次派发都跑）里装载的，装在
/// <c>UiTestHost.Run</c> 的 lambda 里就正好漏掉它。实测 <c>EnsureIsolatedApplication</c>
/// **不重置** <c>Logger.Sink</c>（<see cref="Sink_IsInstalledBeforePlatformStartup_AndSurvivesIt"/> 把这条钉住）。</para>
///
/// <para><b>为什么在 <c>Language-global</c> 集合里：</b><c>Logger.Sink</c> 是**进程级静态**，
/// 而这一层读的是「别的代码装载 XAML 时发出的日志」⇒ 任何并行用例的 XAML 装载都会串进来。
/// 该集合 <c>DisableParallelization = true</c>（独占进程）。</para>
///
/// <para><b>本层不声称的：</b>① 不判「日志内容对不对」，只判「有没有」（豁免除外）；
/// ② 构造/关窗**抛异常**由 <see cref="WindowProductionPathUiTests"/> 与
/// <see cref="ViewSmokeUiTests"/> 负责判，本层只问日志（抛异常时本层记读数、不重复判）。</para>
/// </summary>
[Collection("Language-global")]
public class XamlLoadLogUiTests
{
    private readonly ITestOutputHelper _output;

    public XamlLoadLogUiTests(ITestOutputHelper output) => _output = output;

    // ──────────────────────────────────────────────
    // 收集器
    // ──────────────────────────────────────────────

    /// <summary>
    /// 一条日志事件，**按字段存**而不是拼成字符串。
    ///
    /// <para>豁免判定必须按字段判：如果只拿到一整行拼好的文本，判定就只能靠子串匹配，
    /// 而「子串匹配」在这个仓库是有前科的（判据被自己的文档/字面量满足）。
    /// 尤其是 <see cref="SourceType"/> —— 豁免的**可证伪条件**就落在它上面。</para>
    /// </summary>
    private sealed record Captured(
        LogEventLevel Level, string Area, string? SourceType, string Template, object?[] Values)
    {
        /// <summary>绑定类日志的表达式（Avalonia 的模板里是第 2 个占位符）；取不到就是空串。</summary>
        public string Expression => Values.Length > 1 ? Values[1]?.ToString() ?? "" : "";

        /// <summary>模板的最后一个占位符（绑定类是 <c>{Message}</c>，赋值类是 <c>{Exception}</c>）。</summary>
        public string Last => Values.Length > 0 ? Values[^1]?.ToString() ?? "" : "";

        public override string ToString()
            => $"[{Level}] {Area} (src={SourceType ?? "<null>"}): {Template}"
               + (Values.Length == 0
                   ? ""
                   : " :: " + string.Join(" | ", Values.Select(v => v?.ToString() ?? "<null>")));
    }

    /// <summary>
    /// 只收 <see cref="LogEventLevel.Warning"/> 及以上。
    ///
    /// <para>全收（<c>IsEnabled</c> 恒真）会把 Debug/Verbose 的主题解析日志一并收进来 ——
    /// 既是噪音，又会**改变被测对象**：<c>Logger</c> 的调用点普遍在
    /// <c>if (Logger.TryGet(...).HasValue)</c> 后面，开了级别就等于让它们真的开始拼字符串、分配对象。</para>
    /// </summary>
    private sealed class RecordingSink : ILogSink
    {
        private readonly List<Captured> _events = new();

        public int Count => _events.Count;

        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            => _events.Add(new Captured(level, area, source?.GetType().FullName, messageTemplate,
                                        Array.Empty<object?>()));

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate,
                        params object?[] propertyValues)
            => _events.Add(new Captured(level, area, source?.GetType().FullName, messageTemplate,
                                        propertyValues));

        public void Clear() => _events.Clear();

        /// <summary>取「第 <paramref name="mark"/> 条之后」的那些（即某一段动作期间发生的）。</summary>
        public List<Captured> Since(int mark) => _events.Skip(mark).ToList();
    }

    /// <summary>
    /// 一次「装 sink → 跑 → 收事件 → 还原」。
    ///
    /// <para><b>装在外面</b>（不是 lambda 里）：平台初始化期装载的 <c>App.axaml</c> 也算装载期。
    /// <c>Clear()</c> 紧贴在 <c>Run</c> 之前 —— 这是本层唯一一处竞态窗口：
    /// 若此刻恰有别的用例在另一个线程派发，它的日志会串进来。但该集合独占进程
    /// （<c>DisableParallelization = true</c>），且「别的窗口有 XAML 缺陷」本身就是该红的，
    /// 所以这个窗口只会指向真问题，不会指向假问题。</para>
    /// </summary>
    private static (List<Captured> All, T Result) Capture<T>(Func<RecordingSink, T> body)
    {
        var sink = new RecordingSink();
        var previous = Logger.Sink;

        Logger.Sink = sink;
        try
        {
            sink.Clear();
            var result = UiTestHost.Run(() => body(sink));
            return (sink.Since(0), result);
        }
        finally
        {
            Logger.Sink = previous;
        }
    }

    // ──────────────────────────────────────────────
    // 唯一一条豁免（有理由、且理由由代码本身核实）
    // ──────────────────────────────────────────────

    /// <summary>
    /// **Dock 框架自己的绑定噪音** —— <c>DockControl</c> 的项容器模板在 <c>Layout</c>
    /// 还没被挂上之前就绑了一次 <c>Layout.*</c>，于是发出 5 条 <c>Value is null</c> 的绑定警告
    /// （<c>Layout.Id</c> / <c>Layout.Title</c> / <c>Layout.CanDrag</c> / <c>Layout.CanDrop</c> /
    /// <c>Layout.DockGroup</c>，实测于 2026-09-22 首跑）。本仓的 XAML 里**没有任何** <c>Layout.*</c>
    /// 绑定（<c>grep "Binding\s+Layout\."</c> 零命中）⇒ 来源是 Dock 的模板，不是我们的接线。
    ///
    /// <para><b>为什么这条豁免不是「一个能把缺陷合法化的字段」</b>（登记表 #54 的教训）：
    /// 条件里**必须**同时成立「来源类型属于 <c>Dock.*</c>」—— 我们自己任何控件发出同形状的警告
    /// 都不满足它。也就是说「理由」本身是被**机械核实**的，不是一句注释。
    /// <see cref="Exemption_DoesNotSwallowOurOwnEvents"/> 拿一条合成的自家事件做负控。</para>
    ///
    /// <para><b>条件写成「模板 + 占位符」的形状而不是整行子串</b>：整行子串会把
    /// <c>src=…</c> 那段也算进去，而那段里就有我们要判的来源类型 —— 写宽一点就会自证。</para>
    /// </summary>
    private static bool IsFrameworkNoise(Captured e)
        => e.Level == LogEventLevel.Warning
           && e.Area == "Binding"
           && e.SourceType is not null
           && e.SourceType.StartsWith("Dock", StringComparison.Ordinal)
           && e.Expression.StartsWith("Layout.", StringComparison.Ordinal)
           && e.Last.Contains("Value is null", StringComparison.Ordinal);

    // ──────────────────────────────────────────────
    // 判据一：前提自检（通道 + 时机）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 两条前提，缺一条下面的「0 条」就变成**恒真的假绿**：
    /// <list type="number">
    ///   <item>sink 装进平台初始化之前、且**活过** <c>EnsureIsolatedApplication</c> ——
    ///     否则初始化期（<c>App.axaml</c>）根本不在计时窗口里；</item>
    ///   <item><c>Logger.IsEnabled(Error, "Property")</c> 为真 —— 否则 Avalonia 那些
    ///     <c>TryGet(...).HasValue</c> 判断全部为假，一条日志都不会发出来。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void Sink_IsInstalledBeforePlatformStartup_AndSurvivesIt()
    {
        var sink = new RecordingSink();
        var previous = Logger.Sink;
        Logger.Sink = sink;

        (bool Identity, bool Enabled, int SetupEvents) probe;
        try
        {
            sink.Clear();
            probe = UiTestHost.Run(() => (
                Identity: ReferenceEquals(Logger.Sink, sink),
                Enabled: Logger.IsEnabled(LogEventLevel.Error, "Property"),
                SetupEvents: sink.Count));
        }
        finally
        {
            Logger.Sink = previous;
        }

        Assert.True(probe.Identity,
            "进入派发后 Logger.Sink 已经不是我们装的那个了 ⇒ 平台重建会重置 sink。"
            + "那么「装在平台初始化之前」这个前提不成立，App.axaml 那一段落在计时窗口之外");
        Assert.True(probe.Enabled,
            "Logger.IsEnabled(Error, \"Property\") 为假 ⇒ Avalonia 所有的 `Logger.TryGet(...).HasValue` "
            + "判断都为假、一条日志都不会发出，下面几条「0 条」是恒真的假绿");

        JudgeReadings.Write(_output, "XAML装载日志/前提", new[]
        {
            "sink 活过平台初始化=True / IsEnabled(Error,Property)=True",
            $"平台初始化期（App.axaml + 主题）事件 {probe.SetupEvents} 条",
        });
    }

    // ──────────────────────────────────────────────
    // 判据二：正控 —— sink 真的收得到 Avalonia 自己发的日志
    // ──────────────────────────────────────────────

    /// <summary>
    /// 「0 条」有两个可能的解释：（A）真的没有缺陷、（B）sink 与 Avalonia 的日志通道没接上。
    /// 这条用**一个刻意造出来的绑定错误**把 (B) 排除掉 —— 它证明的是通道，
    /// 不是产品（对照 <c>XamlResourceKeyClosureTests.RuntimeLookup_IsUsableAsEvidence</c> 的形状）。
    ///
    /// <para><c>DataContext</c> 必须非 null：绑到一个 <c>null</c> 的 DataContext 上，
    /// Avalonia 视为「没值」而不是「错误」，实测一条日志都不发（第一版自检就是这么被绕过去的）。</para>
    /// </summary>
    [Fact]
    public void Sink_ReceivesAvaloniaOwnFailures()
    {
        var (events, _) = Capture(_ =>
        {
            var probe = new Border { DataContext = new object() };
            probe.Bind(Border.BackgroundProperty, new Binding("NoSuchPropertyForProbe"));
            return 0;
        });

        Assert.True(events.Count > 0,
            "刻意造了一条绑定错误（DataContext 非 null + 不存在的属性），却一条日志都没收到 ⇒ "
            + "sink 没接上 Avalonia 的日志通道。那么下面那些「0 条」什么都证明不了（假绿）");
        Assert.Contains(events, e => e.Area == "Binding");

        JudgeReadings.Write(_output, "XAML装载日志/正控",
            events.Select(e => "刻意注入的绑定错误 → " + e).ToList());
    }

    // ──────────────────────────────────────────────
    // 判据三：豁免自身不许变成一块遮羞布
    // ──────────────────────────────────────────────

    /// <summary>
    /// 豁免的**负控**：同一条日志形状，只把来源类型换成我们自己的，豁免就**不许**成立。
    ///
    /// <para>没有这条，<see cref="IsFrameworkNoise"/> 退化成「area == Binding 且消息含
    /// <c>Value is null</c>」也照样全绿 —— 那就是一块遮羞布，而这正是登记表 #54
    /// 「豁免表里的理由没人去代码里核」的形状。</para>
    /// </summary>
    [Fact]
    public void Exemption_DoesNotSwallowOurOwnEvents()
    {
        static object?[] BindingValues(string expression, string message)
            => new object?[] { "SomeProperty", expression, expression, message };

        // 同一形状、来源是 Dock ⇒ 豁免成立
        var dock = new Captured(LogEventLevel.Warning, "Binding", "Dock.Avalonia.Controls.ToolChrome",
            "An error occurred binding {Property} to {Expression} at {ExpressionErrorPoint}: {Message}",
            BindingValues("Layout.Title", "Value is null."));
        Assert.True(IsFrameworkNoise(dock), "实测的那条 Dock 噪音应当被豁免（否则判据会红在框架上）");

        // 同一形状、来源是我们自己的控件 ⇒ 豁免**不**成立
        var ours = dock with { SourceType = "HmiEditor.App.Views.AlarmListView" };
        Assert.False(IsFrameworkNoise(ours),
            "同一条警告只是换了来源类型就已经不是框架噪音了，豁免却仍然成立 ⇒ "
            + "豁免条件里那条「来源属于 Dock.*」没在起作用，它成了一句注释");

        // 来源类型拿不到时也必须不豁免（fail-closed：宁可红，不可静默放过）
        Assert.False(IsFrameworkNoise(dock with { SourceType = null }));

        // 我们自己那些形态（area=Property 的赋值失败 / 真实存在的表达式）一律不豁免
        Assert.False(IsFrameworkNoise(dock with { Area = "Property" }));
        Assert.False(IsFrameworkNoise(dock with { Values = BindingValues("Layout", "Value is null.") }));
        Assert.False(IsFrameworkNoise(dock with { Values = BindingValues("Layout.Title", "别的错误") }));
        Assert.False(IsFrameworkNoise(dock with { Level = LogEventLevel.Error }));
    }

    // ──────────────────────────────────────────────
    // 判据四：主判据 A —— 逐窗口
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>面</b>：<see cref="WindowProductionPathUiTests.Entries"/>（30 条**生产路径**，
    /// 两边双向差集在那里守）+ <c>MainWindow</c>。
    ///
    /// <para><b>为什么 <c>MainWindow</c> 要单独铺：</b>它在命名空间 <c>HmiEditor.App</c>，
    /// 而 <c>WindowViewSmokeUiTests.WindowCatalog()</c> 的口径是
    /// <c>Namespace.StartsWith("HmiEditor.App.Views")</c> ⇒ **全仓 XAML 面最大的那个窗口
    /// 从来不在任何窗口冒烟面里**。这里把它补上 —— 补上之后它第一条被抓出来的就是上面那 5 条。</para>
    ///
    /// <para><b>那个窗口不 <c>Close()</c>：</b><c>ChromeWindow</c> 关窗会往数据根写窗口几何 ——
    /// 判据不该有这个副作用（同 <c>XamlResourceKeyClosureTests</c> 里那条不 Show 的理由）。
    /// 其余窗口照 <see cref="WindowProductionPathUiTests"/> 的形状走完 Show → Pump → Close。</para>
    /// </summary>
    [Fact]
    public void NoWarningOrWorse_WhileLoadingEveryWindowOnTheProductionPathTable()
    {
        using var sandbox = new DataRootSandbox("xamllog-windows");

        var (_, report) = Capture(sink =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var readings = new List<string>();
            var offenders = new List<string>();
            var suppressedDetail = new List<string>();

            static (List<Captured> Clean, List<Captured> Noise) Split(IReadOnlyList<Captured> captured)
                => (captured.Where(e => !IsFrameworkNoise(e)).ToList(),
                    captured.Where(IsFrameworkNoise).ToList());

            foreach (var entry in WindowProductionPathUiTests.Entries)
            {
                var mark = sink.Count;
                string note;
                try
                {
                    var window = entry.Build(depot);
                    window.Show();
                    UiTestHost.Pump();
                    window.Close();
                    UiTestHost.Pump();
                    note = "";
                }
                catch (Exception ex)
                {
                    // 抛异常是另一族判据的事（WindowProductionPathUiTests / WindowViewSmokeUiTests）。
                    // 本层只记读数，避免同一条缺陷在两处各红一次、还互相掩盖。
                    note = $"（本项另抛了 {ex.GetType().Name} —— 归 WindowProductionPathUiTests）";
                }

                var captured = sink.Since(mark);
                var (clean, noise) = Split(captured);
                suppressedDetail.AddRange(noise.Select(e => $"{entry.Key} :: {e}"));

                readings.Add($"{entry.Key}: 日志 {captured.Count} 条"
                             + (clean.Count == 0 ? "" : $"，其中**该红** {clean.Count} 条")
                             + (note.Length == 0 ? "" : " " + note));
                offenders.AddRange(clean.Select(e => $"{entry.Key} :: {e}"));
            }

            // 主窗口单独铺：不在 Entries 里（见方法注释），刻意不 Close。
            var mainMark = sink.Count;
            var main = new MainWindow(depot.Settings);
            main.Show();
            UiTestHost.Pump();
            var (mainClean, mainNoise) = Split(sink.Since(mainMark));
            suppressedDetail.AddRange(mainNoise.Select(e => $"MainWindow :: {e}"));

            readings.Add($"MainWindow（不在 WindowCatalog 口径内，本层单独铺）: "
                         + $"日志 {mainClean.Count + mainNoise.Count} 条"
                         + (mainClean.Count == 0 ? "" : $"，其中**该红** {mainClean.Count} 条"));
            offenders.AddRange(mainClean.Select(e => $"MainWindow :: {e}"));

            // 被抑制的那几条**原文进读数**：豁免是「有理由的过滤器」还是「遮羞布」，
            // 只能靠每次都看得见它到底吞了什么来判（登记表 #54 的教训）。抑制数变 0 ⇒ 豁免已成死代码。
            readings.Add($"框架噪音（来源 Dock.* 的 Layout.* 绑定，见 IsFrameworkNoise）共抑制 {suppressedDetail.Count} 条");
            readings.AddRange(suppressedDetail.Select(e => "  （抑制）" + e));

            return (Readings: readings, Offenders: offenders);
        });

        JudgeReadings.Write(_output, "XAML装载日志/窗口面", report.Readings);

        Assert.True(report.Offenders.Count == 0,
            $"装载 {WindowProductionPathUiTests.Entries.Count + 1} 个窗口（构造 → Show → 布局 → 关窗）"
            + $"期间，Avalonia 发出了 {report.Offenders.Count} 条 Warning 及以上的日志。\n"
            + "  这些日志的含义：Avalonia 在**属性赋值 / 绑定 / 模板**上遇到了异常，把它吞进了 Logger\n"
            + "  （延迟赋值的执行点写着 `catch (Exception) { Logger.TryGet(Error, \"Property\").Log(...) }`）。\n"
            + "  area=Property ⇒ 属性赋值失败（含类型不匹配、键缺失）；area=Binding ⇒ 绑定失败。\n"
            + "  两条路编译期都完全不可见，运行期也没有别的出口 —— 本判据是它唯一的消费者。\n"
            + Environment.NewLine + string.Join(Environment.NewLine, report.Offenders));
    }

    // ──────────────────────────────────────────────
    // 判据五：主判据 B —— 逐 UserControl 视图
    // ──────────────────────────────────────────────

    /// <summary>
    /// <b>面</b>：<see cref="ViewSmokeUiTests.ViewCatalog"/>（15 个无参 <c>UserControl</c> 视图，
    /// 含 12 个停靠面板）。窗口之外的另一半 XAML 面。
    ///
    /// <para>塞进一个宿主 <c>Window</c> 再 <c>Show()</c>：<c>{StaticResource}</c> 在构造函数里
    /// 就求值了，但那条**延迟赋值**是挂在「附着到逻辑树」上的 —— 不 Show 就触发不到，
    /// 也就测不到本判据要测的那条路（这条正是 <c>ViewSmokeUiTests</c> 里
    /// 「本层接不住 <c>{StaticResource}</c> 键缺失」那句的成因）。</para>
    /// </summary>
    [Fact]
    public void NoWarningOrWorse_WhileLoadingEveryUserControlView()
    {
        var (_, report) = Capture(sink =>
        {
            var readings = new List<string>();
            var offenders = new List<string>();
            var suppressedDetail = new List<string>();

            foreach (var type in ViewSmokeUiTests.ViewCatalog())
            {
                var mark = sink.Count;
                string note;
                try
                {
                    var view = (UserControl)Activator.CreateInstance(type)!;
                    var host = new Window { Width = 1024, Height = 640, Content = view };
                    host.Show();
                    UiTestHost.Pump();
                    host.Measure(new Avalonia.Size(1024, 640));
                    host.Arrange(new Avalonia.Rect(0, 0, 1024, 640));
                    UiTestHost.Pump();
                    host.Close();
                    UiTestHost.Pump();
                    note = "";
                }
                catch (Exception ex)
                {
                    note = $"（本项另抛了 {ex.GetType().Name} —— 归 ViewSmokeUiTests）";
                }

                var captured = sink.Since(mark);
                var clean = captured.Where(e => !IsFrameworkNoise(e)).ToList();
                suppressedDetail.AddRange(captured.Where(IsFrameworkNoise).Select(e => $"{type.Name} :: {e}"));

                readings.Add($"{type.Name}: 日志 {captured.Count} 条"
                             + (clean.Count == 0 ? "" : $"，其中**该红** {clean.Count} 条")
                             + (note.Length == 0 ? "" : " " + note));
                offenders.AddRange(clean.Select(e => $"{type.Name} :: {e}"));
            }

            if (suppressedDetail.Count > 0)
            {
                readings.Add($"框架噪音共抑制 {suppressedDetail.Count} 条");
                readings.AddRange(suppressedDetail.Select(e => "  （抑制）" + e));
            }

            return (Readings: readings, Offenders: offenders);
        });

        JudgeReadings.Write(_output, "XAML装载日志/UserControl 面", report.Readings);

        Assert.True(report.Offenders.Count == 0,
            $"装载 {ViewSmokeUiTests.ViewCatalog().Count} 个 UserControl 视图期间，"
            + $"Avalonia 发出了 {report.Offenders.Count} 条 Warning 及以上的日志：\n"
            + Environment.NewLine + string.Join(Environment.NewLine, report.Offenders));
    }
}
