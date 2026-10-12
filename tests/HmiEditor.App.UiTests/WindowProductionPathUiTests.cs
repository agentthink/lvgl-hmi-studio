using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.Device.Updates;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **窗口的生产构造路径**（登记表 §4 #39 的「构造路径」那一半）。
///
/// <para><b>它补的是什么：</b><see cref="WindowViewSmokeUiTests"/> 的口径是「有无参构造」，
/// 于是 27 个「两路径」窗口**统一走了设计器路径** —— 那条路按注释自己的说法
/// 「仅用于 XAML 设计器」：字段写 <c>_x = null!</c>、<b>不订事件、不装数据</b>。
/// 用户点菜单走的是**生产路径**，而它在运行期判据里长期没有任何消费者。
/// 本类把每个窗口的生产入口都真跑一遍：<b>构造 → Show → 布局 → 关窗</b>，
/// 于是「生产构造里装数据的那段代码」第一次进入覆盖面。</para>
///
/// <para><b>判据形状（三层）：</b></para>
/// <list type="number">
///   <item><b>跑得通</b> —— 构造 / 布局 / <c>Close()</c> 不抛，且可视树非空、有具名控件。
///     阶段 48 用设计器路径就是在这个形状上抓到 3 个真缺陷（<c>DeviceManagerView</c> 构造即崩、
///     <c>AlarmListView</c>/<c>SimulatorView</c> 关窗 NRE）；生产路径会跑更多代码，值得同一形状。</item>
///   <item><b>真的走了生产路径</b> —— 这条是本类存在的理由。做法：<b>同一个窗口类型各构造一份</b>
///     （设计器路径 / 生产路径），给**本仓库自有类型上声明**的每个实例字段算一个「形状签名」
///     （<c>null</c> / 集合元素个数 / 字符串空非空 / 非空），要求两份的签名**至少有一处不同**。
///     生产路径多做的那件事就是把依赖装进字段、把数据装进集合 ⇒ 签名必须变。
///     两份签名都在 <b>Show() 之前</b>读 —— Show/布局会顺带初始化一批惰性字段，
///     那是渲染的功劳、不是构造的，读进去会把判据泡软。
///     取字段时**刻意只走 <c>HmiEditor.*</c> 的基类**：Avalonia 基类的字段两边本就一样，
///     放进来只会把读数淹掉。</item>
///   <item><b>表与代码同源</b> —— 见 <see cref="EveryConstructibleWindow_IsOnTheProductionPathTable"/>：
///     面的两侧都做**双向差集**，外加「这条表项的窗口确实有两个构造 / 确实只有一个」的元检查。
///     表是手写的，没有元检查就成了一份会过期的抄本（本项目在登记表上已经吃过这个亏）。</item>
/// </list>
///
/// <para><b>为什么判据不是「字段有没有从 null 变成非 null」</b>（第一版就是这么写的，被一条真读数打回）：
/// <c>ValidationResultView</c> 的设计器构造把依赖字段赋成了 <c>Array.Empty&lt;T&gt;()</c>
/// —— <b>非空，但是空的</b>。只看「null → 非空」的话，这条窗口的生产路径
/// （装 1 条 finding 并 <c>Build()</c>）与设计器路径**看起来完全一样**，
/// 判据会对它静默放过。加上「集合元素个数」这一维就区分开了。</para>
///
/// <para><b>为什么在 <c>Language-global</c> 集合里：</b>与
/// <see cref="WindowViewSmokeUiTests"/> 同因 —— 窗口的 <c>Opened</c>/<c>Closing</c> 会经
/// <c>WindowGeometryService</c> 读写数据根，本地化服务又是进程级单例。该集合
/// <c>DisableParallelization = true</c>。</para>
///
/// <para><b>本层不声称的：</b>① 不点按钮 —— 按钮的**行为**由另一族判据覆盖
/// （<c>MainWindowMenuInvocationUiTests</c> / <c>MainWindowToolbarInvocationUiTests</c> /
/// <c>DialogInnerInvocationUiTests</c> / <c>DockPanelInvocationUiTests</c>，四层 170 个按钮）；
/// 本层只保证「生产路径把窗口装配起来了」。② 不判 <c>{StaticResource}</c> 类型匹配，
/// 那归 <c>XamlResourceKeyClosureTests</c>。③ 3 个 private 嵌套对话框够不着
/// （见 <see cref="ReachedOnlyByAHost"/>）。④ 「签名不同」证明的是**构造路径不同**，
/// 不是「用户看到的界面正确」——后者的证据在那一族按钮判据里。</para>
/// </summary>
[Collection("Language-global")]
public class WindowProductionPathUiTests
{
    private readonly ITestOutputHelper _output;

    public WindowProductionPathUiTests(ITestOutputHelper output) => _output = output;

    // ──────────────────────────────────────────────
    // 表
    // ──────────────────────────────────────────────

    /// <summary>
    /// 一条生产路径。<paramref name="Build"/> 必须是**生产构造**（带依赖那条），
    /// 不是无参构造 —— <see cref="TwoPaths"/> 与元检查一起守住这件事。
    /// </summary>
    /// <param name="TypeName">窗口类型名，用来与 <see cref="WindowViewSmokeUiTests.WindowCatalog"/> 对齐。</param>
    /// <param name="Variant">
    /// 同一类型有多条生产构造时的区分后缀（空串 = 主条目）。
    /// 例：<c>FaceplateTemplateEditorView</c> 有「从模板进来」与「从选区进来」两条。
    /// </param>
    /// <param name="Build">用夹具装配的生产构造。</param>
    /// <param name="TwoPaths">该窗口是否同时有无参（设计器）构造。false ⇒ 无参构造本身就是生产入口。</param>
    internal sealed record Entry(
        string TypeName,
        string Variant,
        Func<WindowDepot, Window> Build,
        bool TwoPaths = true)
    {
        public string Key => Variant.Length == 0 ? TypeName : $"{TypeName}·{Variant}";
    }

    internal static readonly IReadOnlyList<Entry> Entries = new List<Entry>
    {
        new("AiIconDialog", "", d => new Views.AiIconDialog(d.Ai)),
        new("AiScriptDialog", "", d => new Views.AiScriptDialog(d.Ai)),
        new("AiTemplateDialog", "", d => new Views.AiTemplateDialog(d.Ai)),
        new("AlarmConfigView", "", d => new Views.AlarmConfigView(d.AlarmGroups, d.Project)),
        new("AlarmListView", "", d => new Views.AlarmListView(d.Alarms)),
        new("AnimationEditorView", "", d => new Views.AnimationEditorView(d.Animations, d.Project)),
        new("BuildRunDialog", "",
            d => new Views.BuildRunDialog(d.Project, d.DataRoot, d.DataRoot + "/lvgl", d.Assets)),
        new("CommunicationConfigView", "", d => new Views.CommunicationConfigView(d.Variables, d.Project)),
        new("DeviceManagerView", "", _ => new Views.DeviceManagerView(), TwoPaths: false),
        new("DownloadToHmiView", "",
            d => new Views.DownloadToHmiView(
                new[] { d.DataRoot + "/main.c" },
                d.DataRoot,
                new[] { new HmiDeployAsset(d.DataRoot + "/main.c", "app/main.c") },
                "runtime-config")),
        new("FaceplateTemplateEditorView", "模板",
            d => new Views.FaceplateTemplateEditorView(new ControlTemplate { Name = "泵面板" })),
        new("FaceplateTemplateEditorView", "选区",
            d => new Views.FaceplateTemplateEditorView(new[] { d.Screen.Controls[0] })),
        new("FontSubsetView", "", d => new Views.FontSubsetView(d.Project)),
        new("LoginDialog", "", d => new Views.LoginDialog(d.Auth)),
        new("ProjectSettingsView", "", d => new Views.ProjectSettingsView(d.Project)),
        new("RecipeEditorView", "", d => new Views.RecipeEditorView(d.Recipes, d.Variables)),
        new("ReportRangeDialog", "", _ => new Views.ReportRangeDialog(
            new HmiEditor.Core.Reporting.ReportRangeSelection(HmiEditor.Core.Reporting.ReportRangeChoice.All))),
        new("ReportDesignerDialog", "",
            _ => new Views.ReportDesignerDialog(
                new[]
                {
                    new HmiEditor.Core.Reporting.ReportColumn("user.mgr.audit.time", "时间"),
                    new HmiEditor.Core.Reporting.ReportColumn("user.mgr.audit.user", "用户"),
                },
                new HmiEditor.Core.Reporting.ReportTemplate(new[]
                {
                    new HmiEditor.Core.Reporting.ReportTemplateEntry("user.mgr.audit.user", "操作者"),
                    new HmiEditor.Core.Reporting.ReportTemplateEntry("user.mgr.audit.time", Hidden: true),
                }))),
        new("ReportColumnDialog", "",
            _ => new Views.ReportColumnDialog(new[]
            {
                new HmiEditor.Core.Reporting.ReportColumn("user.mgr.audit.time", "时间"),
                new HmiEditor.Core.Reporting.ReportColumn("alarm.name", "报警名称"),
            })),
        new("ScreenManagerView", "", d => new Views.ScreenManagerView(d.Project, d.Screen)),
        new("ScriptEditorView", "",
            d => new Views.ScriptEditorView(d.Scripts, d.Bindings, d.Project, null, d.Ai)),
        new("SettingsDialog", "", d => new Views.SettingsDialog(d.Settings, d.Theme)),
        new("SimulatorView", "", d => new Views.SimulatorView(d.Simulator, d.Comms)),
        new("SplashWindow", "", d => new Views.SplashWindow(d.Settings)),
        new("StyleEditorView", "", d => new Views.StyleEditorView(d.Project)),
        new("StylePresetPicker", "", _ => new Views.StylePresetPicker(), TwoPaths: false),
        new("TableCellEditorDialog", "",
            d => new Views.TableCellEditorDialog(d.Screen.Controls[0], d.Variables.Variables)),
        new("TagMonitorView", "", d => new Views.TagMonitorView(d.BindingEngine, null)),
        new("TrendConfigView", "", d => new Views.TrendConfigView(d.Trends)),
        new("UserManagerView", "", d => new Views.UserManagerView(d.Auth)),
        new("ValidationResultView", "",
            d => new Views.ValidationResultView(new[]
            {
                // 阶段 89（#61g）：记录多了 `Code`（必填）—— 这个夹具的 `project` 是
                // 「与语言无关的定位」，所以 LocationCode 留空（直出），与生产代码同一口径。
                new GeneratedCodeFinding(FindingSeverity.Warning, FindingCategory.Structure,
                    GeneratedCodeIssueCodes.NO_SCREENS, "空项目", "project"),
            })),
        new("VariableImportView", "", d => new Views.VariableImportView(d.Variables)),
        new("VariableManagerView", "", d => new Views.VariableManagerView(d.Variables)),
    };

    /// <summary>
    /// 够不着本表的窗口：**private 嵌套**类型，只能由宿主视图在代码里构造。
    /// 键是 <c>FullName</c>（<c>GetType()</c> 取得到，测试代码写不出这个类型名）。
    ///
    /// <para><b>豁免必须可验证</b>：宿主视图在 <see cref="Entries"/> 里，且
    /// <see cref="WindowViewSmokeUiTests"/> 有一条判据会去宿主的可视树里核实这些嵌套对话框
    /// **真的被构造出来**（见那边的 <c>ReachedByAHost</c>）。所以这里不是一句注释。</para>
    /// </summary>
    internal static readonly Dictionary<string, string> ReachedOnlyByAHost = new(StringComparer.Ordinal)
    {
        ["HmiEditor.App.Views.RecipeEditorView+TextInputDialog"] =
            "RecipeEditorView 的 private 嵌套对话框，由宿主在代码里 new 出来（无独立入口）",
        ["HmiEditor.App.Views.RecipeEditorView+ConfirmDialog"] =
            "RecipeEditorView 的 private 嵌套对话框，由宿主在代码里 new 出来（无独立入口）",
        ["HmiEditor.App.Views.AnimationEditorView+ConfirmDeleteDialog"] =
            "AnimationEditorView 的 private 嵌套对话框，由宿主在代码里 new 出来（无独立入口）",
    };

    public static IEnumerable<object[]> Keys() => Entries.Select(e => new object[] { e.Key });

    // ──────────────────────────────────────────────
    // 判据一：夹具自检
    // ──────────────────────────────────────────────

    /// <summary>
    /// 夹具自检。这一层最大的盲区与别处一样是「夹具里有个 null，于是生产路径在半装配状态下被测着，
    /// 而判据只看见『没抛异常』」——那种绿是假绿。所以先把 12 个依赖逐个断言非空。
    /// </summary>
    [Fact]
    public void Depot_SuppliesEveryDependency()
    {
        using var sandbox = new DataRootSandbox("winprod-depot");

        var missing = UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            return depot.Dependencies.Where(x => x.Value is null).Select(x => x.Name).ToList();
        });

        Assert.True(missing.Count == 0,
            $"{missing.Count} 个夹具依赖是 null：[{string.Join(", ", missing)}] —— "
            + "生产构造会把它们装进字段，null 会让「生产路径」退化成「设计器路径 + 一次赋值」");

        // 数据也必须真的装进去：空集合会让「装数据」那一段跑成空转。
        var counts = UiTestHost.Run(() =>
        {
            var d = new WindowDepot(sandbox.DataRoot);
            return new[]
            {
                $"屏幕 {d.Project.Screens.Count}", $"变量 {d.Variables.Variables.Count}",
                $"报警组 {d.AlarmGroups.Count}", $"趋势 {d.Trends.Count}",
                $"脚本 {d.Scripts.Count}", $"动画 {d.Animations.Count}",
            };
        });

        Assert.All(counts, c => Assert.DoesNotContain(" 0", c));
        JudgeReadings.Write(_output, "窗口生产路径/夹具", counts);
    }

    // ──────────────────────────────────────────────
    // 判据二：逐窗口（一个窗口一条用例，失败时点名）
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Keys))]
    public void ProductionPath_ConstructsShowsLaysOutCloses_AndStartsWithRealDependencies(string key)
    {
        var entry = Entries.Single(e => e.Key == key);
        using var sandbox = new DataRootSandbox("winprod");

        var report = UiTestHost.Run(() => Probe(entry, sandbox.DataRoot));

        Assert.True(report.WindowTypeName.EndsWith(entry.TypeName, StringComparison.Ordinal),
            $"表项 {key} 的 Build 产出的是 {report.WindowTypeName} —— 表接错了窗口"
            + "（手写表的复制粘贴错误只有这条元检查抓得到）");

        Assert.True(report.VisualCount > 0,
            $"{key} 生产构造后可视树为空（XAML 未加载？）");
        Assert.True(report.NamedCount > 0,
            $"{key} 的可视树里 {report.VisualCount} 个控件、没有一个带 x:Name —— 生产路径可能没跑完 InitializeComponent");

        if (entry.TwoPaths)
        {
            Assert.True(report.DifferingFields.Count > 0,
                $"{key} 的**生产构造与设计器构造产出的字段形状完全一致**"
                + $"（自有字段 {report.OwnFields} 个，逐字段形状签名一处都没变）。"
                + "这说明本用例走的是设计器构造 —— 生产构造要装的依赖没装进去，"
                + "本层对这条窗口等于没测。"
                + Environment.NewLine
                + "  形状签名 = null / 集合元素个数 / 字符串空非空 / 非空"
                + "（「空集合」与「null」是两档，所以 NonEmpty 集合的 0 → 1 也算变了）。"
                + Environment.NewLine
                + "  设计器路径：" + string.Join(" | ", report.DesignerShape)
                + Environment.NewLine
                + "  生产路径：  " + string.Join(" | ", report.ProductionShape));
        }

        JudgeReadings.Write(_output, $"窗口生产路径/{key}", new[]
        {
            $"可视控件 {report.VisualCount} / 具名 {report.NamedCount}",
            $"自有字段 {report.OwnFields}；设计器路径空字段 {report.DesignerNullFields}"
                + $" → 生产路径空字段 {report.ProductionNullFields}",
            report.DifferingFields.Count == 0
                ? "形状签名变化：**无**（单路径窗口，或生产构造只调方法不存字段）"
                : $"形状签名变化 {report.DifferingFields.Count} 处："
                    + string.Join(", ", report.DifferingFields),
        });
    }

    /// <summary>构造（两条路径）→ Show → 布局 → 读字段 → Close。</summary>
    private static Report Probe(Entry entry, string dataRoot)
    {
        var depot = new WindowDepot(dataRoot);
        var type = WindowViewSmokeUiTests.WindowCatalog().Single(t => t.Name == entry.TypeName);

        // 设计器路径实例：**只**为了读形状签名。不 Show、不 Close
        // —— 它没被打开过，所以没有关窗期的副作用要跑。
        var designer = (Window)Activator.CreateInstance(type)!;
        var designerShape = ShapeSignatures(designer);
        var designerNulls = NullNames(designerShape);

        var production = entry.Build(depot);

        // 生产实例的签名在 **Show 之前** 读：Show/布局会顺带初始化一批惰性字段，
        // 那些变化是渲染的功劳、不是构造路径的，读进去会把判据泡软。
        var productionShape = ShapeSignatures(production);
        var productionNulls = NullNames(productionShape);

        // 逐字段比形状：只记「同一个字段、两边形状不同」的那些。
        var differing = designerShape
            .Zip(productionShape, (d, p) => (Designer: d, Production: p))
            .Where(x => !string.Equals(x.Designer, x.Production, StringComparison.Ordinal))
            .Select(x => $"{x.Designer} → {x.Production}")
            .ToList();

        var filled = designerNulls
            .Except(productionNulls)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        production.Show();
        UiTestHost.Pump();

        // 布局期异常（无法解析的模板、NaN 尺寸）在这里抛出。
        production.Measure(new Size(1024, 600));
        production.Arrange(new Rect(0, 0, 1024, 600));
        UiTestHost.Pump();

        var visual = production.GetVisualDescendants().OfType<Control>().ToList();
        var named = visual.Count(c => c.Name is not null);

        var ownFields = productionShape.Count;

        // 关窗会把窗口几何写进 windows.json（见 ChromeWindow.OnClosingPersistGeometry）——
        // 这正是每个用例都要套 DataRootSandbox 的原因。
        production.Close();
        UiTestHost.Pump();

        return new Report(
            WindowTypeName: production.GetType().FullName!,
            VisualCount: visual.Count,
            NamedCount: named,
            OwnFields: ownFields,
            DesignerNullFields: designerNulls.Count,
            ProductionNullFields: productionNulls.Count,
            DifferingFields: differing,
            DesignerShape: designerShape,
            ProductionShape: productionShape);
    }

    private readonly record struct Report(
        string WindowTypeName,
        int VisualCount,
        int NamedCount,
        int OwnFields,
        int DesignerNullFields,
        int ProductionNullFields,
        IReadOnlyList<string> DifferingFields,
        IReadOnlyList<string> DesignerShape,
        IReadOnlyList<string> ProductionShape);

    // ──────────────────────────────────────────────
    // 判据三：表与代码同源
    // ──────────────────────────────────────────────

    /// <summary>
    /// **面守卫**：<see cref="WindowViewSmokeUiTests.WindowCatalog"/> 里的每一个窗口，
    /// 必须要么在本表里、要么在 <see cref="ReachedOnlyByAHost"/> 里**写明理由**，两者都做双向差集。
    ///
    /// <para><b>为什么两侧都要判</b>：只判「面里的类型都在表里」时，表可以塞进一个已经删掉的类型名
    /// 而不红（过期抄本）；只判「表里的类型都在面里」时，新增窗口会静默掉出本层。
    /// 这是本项目在键可达性与豁免表上反复用过的形状。</para>
    /// </summary>
    [Fact]
    public void EveryConstructibleWindow_IsOnTheProductionPathTable()
    {
        var catalog = WindowViewSmokeUiTests.WindowCatalog();

        // 空集守卫：命名空间改名会让下面的差集恒空 —— 那正是「假门禁」的经典形状。
        Assert.True(catalog.Count >= 32,
            $"只推导出 {catalog.Count} 个窗口类型（应为 32）—— 清单口径可能已失效，下面的差集检查会变成恒真");

        var covered = Entries.Select(e => e.TypeName).ToHashSet(StringComparer.Ordinal);

        var missing = catalog
            .Where(t => !covered.Contains(t.Name) && !ReachedOnlyByAHost.ContainsKey(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} 个窗口既不在生产路径表里，也没写明豁免理由 —— "
            + "它的生产构造路径没有任何运行期消费者（本层就是为了这件事存在的）："
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", missing)}");

        var catalogNames = catalog.Select(t => t.FullName!).ToHashSet(StringComparer.Ordinal);

        var staleEntries = Entries
            .Where(e => !catalog.Any(t => t.Name == e.TypeName))
            .Select(e => e.Key)
            .ToList();
        Assert.True(staleEntries.Count == 0,
            $"表里有 {staleEntries.Count} 条指向已不在窗口清单里的类型（类型被改名/删掉后表没跟着改）："
            + $"[{string.Join(", ", staleEntries)}]");

        var staleHosts = ReachedOnlyByAHost.Keys
            .Where(k => !catalogNames.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        Assert.True(staleHosts.Count == 0,
            $"豁免表里有 {staleHosts.Count} 条指向已不存在的类型（豁免过期了就要删掉）："
            + $"[{string.Join(", ", staleHosts)}]");

        // 同一类型的多条表项必须靠 Variant 区分开，否则 Key 撞车 ⇒ Names() 里出现重复
        // ⇒ `Entries.Single(e => e.Key == key)` 抛异常，报错信息反而更难懂。
        var collided = Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(collided.Count == 0,
            $"表里有 {collided.Count} 个重复 Key（同类型多条表项要填不同的 Variant）："
            + $"[{string.Join(", ", collided)}]");
    }

    /// <summary>
    /// **元检查**：表对每个窗口的双路径判定必须**与代码一致** ——
    /// <see cref="Entry.TwoPaths"/> 为 true 的类型必须真的既有无参构造、又有一条带依赖的公共构造；
    /// 为 false 的必须**只有**无参构造（无参那条就是生产入口，所以第 2 条判据对它不适用）。
    ///
    /// <para>没有这条，「表说是两路径但其实只有一个构造」会让判据 2 静默少测一半，
    /// 而它自己不会红。</para>
    /// </summary>
    [Fact]
    public void TwoPathsFlag_AgreesWithTheDeclaredConstructors()
    {
        var problems = new List<string>();

        foreach (var entry in Entries)
        {
            var type = WindowViewSmokeUiTests.WindowCatalog().Single(t => t.Name == entry.TypeName);
            var publicCtors = type
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Select(c => c.GetParameters().Length)
                .ToList();

            var hasNoArg = publicCtors.Contains(0);
            var hasDependencyCtor = publicCtors.Any(n => n > 0);

            Assert.True(type.GetConstructor(Type.EmptyTypes) is not null
                     || !entry.TwoPaths,
                $"{entry.Key} 标了 TwoPaths=true 却没有无参（设计器）构造 —— 表与代码不一致");

            if (entry.TwoPaths && !hasDependencyCtor)
                problems.Add($"{entry.Key}：标了 TwoPaths=true，但只有无参构造");
            if (!entry.TwoPaths && hasDependencyCtor)
                problems.Add($"{entry.Key}：标了 TwoPaths=false，但另有带依赖构造（{string.Join("/", publicCtors)} 个参数）");
            if (!entry.TwoPaths && publicCtors.Count != 1)
                problems.Add($"{entry.Key}：标了 TwoPaths=false，但公共构造有 {publicCtors.Count} 个");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} 条表项的 TwoPaths 判定与真实构造签名不符："
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", problems)}");
    }

    // ──────────────────────────────────────────────
    // 反射：只数「本仓库自有类型」上声明的实例字段
    // ──────────────────────────────────────────────

    /// <summary>
    /// 本仓库自有类型上声明的实例字段。**刻意在遇到非 <c>HmiEditor.*</c> 的基类时停下**：
    /// Avalonia 的 <c>Window</c>/<c>TopLevel</c> 有几十个字段，两侧本就一样，
    /// 放进来会把读数淹掉（也会把「Show 过的那一份多出几个非空」读成"生产路径装了依赖"）。
    /// </summary>
    private static IEnumerable<FieldInfo> OwnFields(Type type)
    {
        for (var cur = type; cur is not null; cur = cur.BaseType)
        {
            if (cur.Namespace is null || !cur.Namespace.StartsWith("HmiEditor", StringComparison.Ordinal))
                yield break;

            foreach (var f in cur.GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!f.IsStatic) yield return f;
            }
        }
    }

    /// <summary>
    /// 逐个自有字段算「形状签名」：<c>名字=形状</c>，按名字排序。
    /// 形状四档：<c>null</c> / <c>count:N</c>（集合，**含 N=0**）/
    /// <c>str:空</c> 或 <c>str:非空</c> / <c>非空</c>。
    ///
    /// <para><b>为什么必须有「集合元素个数」这一维</b>（第一版判据就是栽在这里）：
    /// <c>ValidationResultView</c> 的设计器构造把依赖字段赋成 <c>Array.Empty&lt;T&gt;()</c>
    /// —— <b>非空，但是空的</b>。只看「null → 非空」的话，这条窗口的两条构造路径
    /// 会**完全一致**，判据对它静默放过。</para>
    ///
    /// <para>集合按**非泛型</b> <see cref="ICollection"/> 认：数组、<c>List&lt;T&gt;</c>、
    /// <c>Dictionary&lt;K,V&gt;</c>、<c>ObservableCollection&lt;T&gt;</c> 都实现它，
    /// 声明成只读接口的字段在运行期也是这些具体类型。</para>
    /// </summary>
    private static List<string> ShapeSignatures(object instance) =>
        OwnFields(instance.GetType())
            .Select(f => $"{f.Name}={ShapeOf(f.GetValue(instance))}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    private static string ShapeOf(object? value) => value switch
    {
        null => "null",
        string s => s.Length == 0 ? "str:空" : "str:非空",
        ICollection c => $"count:{c.Count}",
        _ => "非空",
    };

    /// <summary>从签名表里取出「值为 null」的字段名（读数用，不参与断言）。</summary>
    private static List<string> NullNames(IReadOnlyList<string> shapes) =>
        shapes
            .Where(s => s.EndsWith("=null", StringComparison.Ordinal))
            .Select(s => s[..^"=null".Length])
            .ToList();
}
