using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
// ⚠ 不能用 `using Avalonia.Controls.Shapes;` —— 它引入的 Path 与文件里在用的
//   System.IO.Path（Path.Combine）撞名 ⇒ CS0104。用具名别名。
using ShapePath = Avalonia.Controls.Shapes.Path;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 主窗口（<see cref="MainWindow"/>）的构造与装配判据。
///
/// <para><b>为什么要有这一类：</b>覆盖率读数（<c>docs/2026-09-20-coverage-tooling.md</c>）
/// 显示 <c>MainWindow.axaml.cs</c> 在**每一份**报告里都是 <c>0/N</c> ——
/// <c>grep -rn "new MainWindow(" tests/</c> 零命中。也就是说：改主窗口的构造、菜单装配、
/// 文件打开/保存路径，**没有任何自动判据会红**。</para>
///
/// <para>此前不做这件事的理由写在 <see cref="DataRootMigrationWiringTests"/> 的注释里 ——
/// "行为测试要构造整个 MainWindow 才够得着，成本高且脆弱"。成本是真的（隔离数据根 +
/// 真实 <see cref="SettingsService"/> + 关闭时的 <c>CleanupOnShutdown</c>，见
/// <see cref="DataRootSandbox"/>），但实测**一次构造 928 ms**，代价可以接受。</para>
///
/// <para><b>为什么在 <c>Language-global</c> 集合里：</b>本类碰两个进程级单例 ——
/// 数据根环境变量（构造期审计日志/图库/布局都往那里落）与本地化服务（语言是全局状态）。
/// 该集合 <c>DisableParallelization = true</c> ⇒ 本类不与任何其它集合并发执行，
/// 两个单例的"改了必须还原"才不会影响旁人。</para>
///
/// <para><b>本类没做的事（有意）：</b>① 只挑了一条菜单做**行为**验证（语言），其余 60 余个菜单项
/// 只有静态绊线，不证明"接了正确的处理器"；② "连续构造两轮都成功"这条本来能守
/// <c>Close()</c> 是否把资源放干净，但**找不到能单独隔离它的最小注入**（删掉某处 <c>Dispose</c>
/// 之后第二轮照样成功），按"每条判据都要有自己的负控"的纪律**没有发布**；
/// ③ 文件打开/保存路径仍未覆盖。</para>
///
/// <para><b>扫描面提醒：</b><see cref="MainWindow"/> 由 <c>MainWindow.axaml.cs</c> 加若干个
/// <c>Wiring.*.cs</c> partial 共同构成。只 glob <c>MainWindow*.axaml.cs</c> 会把 127 个具名
/// 控件里的 **100 个**误判成"未接线"（本类第一版就踩了）—— 因此按**声明**取面：
/// 凡含 <c>partial class MainWindow</c> 的文件都算。</para>
/// </summary>
[Collection("Language-global")]
public class MainWindowConstructionTests
{
    /// <summary>
    /// 构造、跑通布局、干净关闭。这是"主窗口这一层"从 0 到有的第一条判据：
    /// 任何会让构造路径炸掉的改动（接线顺序、缺依赖、XAML 资源、面板构造）都会在这里红。
    /// </summary>
    [Fact]
    public void MainWindow_ConstructsAndLaysOut()
    {
        using var sandbox = new DataRootSandbox("mainwin");

        var r = UiTestHost.Run(() => Build(sandbox));

        Assert.True(r.Total > 250, r.Report());
        Assert.True(r.Named > 70, r.Report());
    }

    /// <summary>
    /// 主窗口 XAML 里**每一个具名控件**都必须在 code-behind（全部 partial）里被真正用到
    /// —— 成员访问（<c>MenuNewProject.Click</c>）或作为实参，而不是只在注释/别处被提一句。
    ///
    /// <para>守的是"加了控件忘了接线"与"接线被删掉"：这两件事都能编译通过，且没有任何
    /// 运行期症状（点了没反应）。<b>它是绊线不是证明</b> —— 判据只问"有没有被碰过"，
    /// 不问"接的是不是对的处理函数"；后者只能靠逐个行为测试，见
    /// <see cref="MainWindow_LanguageMenuItem_ActuallySwitchesTheUi"/> 那种写法。</para>
    /// </summary>
    [Fact]
    public void EveryNamedControl_IsTouchedByTheCodeBehind()
    {
        var partials = MainWindowPartials();
        var names = XamlNames();

        // 扫描面自检：这一层最大的盲区是"扫了个空所以全绿"。
        Assert.True(partials.Count >= 6,
            $"MainWindow 的 partial 只找到 {partials.Count} 个（应为 7：MainWindow.axaml.cs + 6 个 Wiring.*.cs），"
            + $"清单可能失效：[{string.Join(", ", partials.Select(p => p.Relative))}]");
        Assert.True(names.Count >= 140,
            $"MainWindow.axaml 只推导出 {names.Count} 个具名控件（应约 143），清单可能失效");

        // 剥注释后再找：注释里提一句名字不算接线（保长度、保行数，见 SourceScanner）。
        var code = string.Join("\n", partials.Select(p => SourceScanner.StripComments(p.Text)));

        var missing = names
            .Where(n => !Regex.IsMatch(code, $@"(?<![A-Za-z0-9_]){Regex.Escape(n)}\s*[.,)]"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{missing.Count} 个具名控件在 MainWindow 的 code-behind 里从未被使用"
            + "（新增控件要接线；删了接线代码要连着 XAML 一起删）："
            + $"{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", missing)}");
    }

    /// <summary>
    /// 菜单装配**真的通**：点"English"菜单项，界面文案当场变成英文。
    ///
    /// <para>这条是行为判据（而不是"名字出现过"），因此能抓到 <see cref="EveryNamedControl_IsTouchedByTheCodeBehind"/>
    /// 抓不到的东西：接了空处理器、接错了处理器、语言切了但界面没刷新。选语言菜单是因为它的
    /// 效果**在窗口内部可观察**（<c>MenuNewProject.Header</c> 是 <c>{res:Loc}</c> 绑定），
    /// 不需要打开对话框或碰文件系统。</para>
    /// </summary>
    [Fact]
    public void MainWindow_LanguageMenuItem_ActuallySwitchesTheUi()
    {
        using var sandbox = new DataRootSandbox("mainwin");
        var service = AppLocalization.Service;
        var original = service.CurrentLanguage;

        try
        {
            var probe = UiTestHost.Run(() =>
            {
                var settings = new SettingsService(Path.Combine(sandbox.DataRoot, "settings.json"));
                var window = new MainWindow(settings);
                window.Show();
                UiTestHost.Pump();

                service.SwitchLanguage("zh-CN");
                UiTestHost.Pump();

                var menu = window.GetLogicalDescendants().OfType<MenuItem>()
                    .Single(m => m.Name == "MenuNewProject");
                var english = window.GetLogicalDescendants().OfType<MenuItem>()
                    .Single(m => m.Name == "MenuLangEnUS");

                var zh = menu.Header as string;

                english.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                UiTestHost.Pump();

                var en = menu.Header as string;
                var language = service.CurrentLanguage;

                window.Close();
                return (Chinese: zh, English: en, Language: language);
            });

            Assert.False(string.IsNullOrWhiteSpace(probe.Chinese), "前提不成立：zh-CN 下菜单项没有文案");
            Assert.Equal("en-US", probe.Language);
            Assert.NotEqual(probe.Chinese, probe.English);
            Assert.False(string.Equals(probe.English, "project.new", StringComparison.Ordinal),
                "切到英文后菜单项显示的还是键名 —— {res:Loc} 绑定没有刷新");
        }
        finally
        {
            // 语言是静态单例：不还原会污染同程序集里的其它 UI 测试
            service.SwitchLanguage(original);
        }
    }

    /// <summary>
    /// 绘图工具栏 / 控件快捷那 21 个图标：每个 <c>&lt;Path&gt;</c> 的 <c>Data</c> 必须**就是**
    /// <c>Assets/Icons.axaml</c> 里那一个 <c>StreamGeometry</c> 实例（引用相等），而不是内联几何。
    ///
    /// <para><b>守的是什么（2026-09-22，阶段 71）：</b>这 21 条几何曾经在两个文件里各存一份 ——
    /// <c>Assets/Icons.axaml</c> 定义了 <c>IconDraw*</c>(7) / <c>IconQuick*</c>(14)，
    /// 而 <c>MainWindow.axaml</c> 把**同样的字符串逐字内联**（成因见那段注释：<c>PathIcon</c>
    /// 模板用 <c>Fill</c>，描边型图标会被渲染成实心色块或空白 ⇒ 换 <c>Path</c> 控件；
    /// 但"换控件类型"与"必须内联几何"是两件事）。后果：同一份几何两处存放，
    /// 改一处不改另一处会**静默分叉** —— 编译期零警告、运行期不抛，只是图标画得不一样。
    /// 当时全仓 3000+ 条判据**没有一条**看得见它，因为反向（定义 ⇒ 有人引用）那一半从来没查过。</para>
    ///
    /// <para><b>为什么必须引用相等：</b>写成"几何字符串相等"会放过"又内联了一份一模一样的"，
    /// 而那正是要防的回归形状。<c>{StaticResource}</c> 取的本来就是字典里那个对象、
    /// 没有类型转换（<c>Geometry</c> → <c>Geometry</c>）⇒ 引用相等是"真的走到了字典"的最强陈述。</para>
    ///
    /// <para><b>为什么还要断 <c>Stroke</c> 与 <c>Stretch</c>：</b>它们是"仍然是 <c>Path</c> 控件 +
    /// 描边样式"的证据 —— 当初内联就是为了绕开 <c>PathIcon</c> 的 <c>Fill</c> 模板；
    /// 若日后换回 <c>PathIcon</c>，几何引用可能还在，但渲染会退化成色块/空白。
    /// 断言这两个属性把那条回头路堵住。</para>
    ///
    /// <para><b>不写"名字 → 图标键"的表</b>：那是把规格抄一份、会过期。这里用**结构**取面
    /// （<c>&lt;Path&gt;</c> 挂在绘图 <c>ToggleButton</c> 上或带 <c>drw-qbtn</c> 类的 <c>Button</c> 上），
    /// 键从 XAML 现场读。</para>
    /// </summary>
    [Fact]
    public void DrawingToolbarIcons_AreTheIconDictionaryInstances()
    {
        using var sandbox = new DataRootSandbox("mainwin-icons");

        var r = UiTestHost.Run(() =>
        {
            var settings = new SettingsService(Path.Combine(sandbox.DataRoot, "settings.json"));
            var window = new MainWindow(settings);
            window.Show();
            UiTestHost.Pump();
            window.Measure(new Size(1024, 600));
            window.Arrange(new Rect(0, 0, 1024, 600));
            UiTestHost.Pump();

            var paths = window.GetLogicalDescendants().OfType<ShapePath>()
                .Where(p => p.Parent is ToggleButton
                            || (p.Parent is Button { } b && b.Classes.Contains("drw-qbtn")))
                .ToList();

            var keys = Regex.Matches(
                    AppSourceScanner.ScanAppXaml()
                        .Single(s => s.Relative.EndsWith("MainWindow.axaml", StringComparison.Ordinal)).Text,
                    @"<Path\s+Data=""\{StaticResource\s+(?<key>[A-Za-z_][A-Za-z0-9_.]*)\}")
                .Select(m => m.Groups["key"].Value)
                .ToList();

            // 只用 x:Key 名解析（与 XamlResourceKeyClosureTests 同一口径）—— 拿到的是**真实例**
            var resources = Application.Current!.Resources;
            var instances = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var key in keys)
                if (resources.TryGetResource(key, null, out var v) && v is not null)
                    instances.Add(v);

            var problems = new List<string>();
            foreach (var p in paths)
            {
                var host = (Control)p.Parent!;
                var where = string.IsNullOrEmpty(host.Name) ? host.GetType().Name : host.Name;
                if (p.Data is null)
                    problems.Add($"{where}: Data 为空");
                else if (!instances.Contains(p.Data))
                    problems.Add($"{where}: Data 不是字典里的那个实例（{p.Data.Bounds.Width:0.#}×{p.Data.Bounds.Height:0.#}）");
                else if (p.Stroke is null)
                    problems.Add($"{where}: Stroke 为空 —— 描边样式没生效（可能退回了 PathIcon 的 Fill 渲染）");
                else if (p.Stretch != Stretch.Uniform)
                    problems.Add($"{where}: Stretch={p.Stretch}（应为 Uniform）");
            }

            window.Close();
            return (PathCount: paths.Count, KeyCount: keys.Count, Instances: instances.Count, Problems: problems);
        });

        var observed = $"实测：工具栏 Path {r.PathCount} 个 / XAML 里 {r.KeyCount} 个 {{StaticResource}} 形态 / "
                     + $"字典解析出 {r.Instances} 个实例";

        Assert.True(r.PathCount == 21, $"只探到 {r.PathCount} 个绘图工具栏 Path（应 21）。{observed}");
        Assert.True(r.KeyCount == 21,
            $"MainWindow.axaml 里只有 {r.KeyCount} 个 {{StaticResource}} 形态的工具栏 Data（应 21）"
            + $"—— 又有人把几何内联回去了。{observed}");
        Assert.True(r.Instances == 21, $"只从字典解析出 {r.Instances} 个实例（应 21）。{observed}");

        Assert.True(r.Problems.Count == 0,
            $"{r.Problems.Count} 个绘图工具栏图标没有落到图标字典那一个实例上：\n    "
            + string.Join("\n    ", r.Problems) + "\n" + observed);
    }

    /// <summary>构造一个主窗口 → 布局 → 关闭，并把规模读回来。</summary>
    private static Snapshot Build(DataRootSandbox sandbox)
    {
        var settings = new SettingsService(Path.Combine(sandbox.DataRoot, "settings.json"));

        var window = new MainWindow(settings);
        window.Show();
        UiTestHost.Pump();

        window.Measure(new Size(1024, 600));
        window.Arrange(new Rect(0, 0, 1024, 600));
        UiTestHost.Pump();

        var visual = window.GetVisualDescendants().OfType<Control>().ToList();
        var named = visual.Count(c => c.Name is not null);
        var menus = window.GetLogicalDescendants().OfType<MenuItem>().Count();

        window.Close();

        return new Snapshot(visual.Count, named, menus);
    }

    private readonly record struct Snapshot(int Total, int Named, int Menus)
    {
        public string Report() =>
            $"主窗口读数：可视树控件 {Total}、其中有名字 {Named}、菜单项 {Menus}";
    }

    /// <summary>从 <c>MainWindow.axaml</c> 推导出全部 <c>x:Name</c>。</summary>
    private static IReadOnlyList<string> XamlNames()
    {
        var xaml = AppSourceScanner.ScanAppXaml()
            .Single(s => s.Relative.EndsWith("MainWindow.axaml", StringComparison.Ordinal));

        return Regex.Matches(xaml.Text, "x:Name=\"([A-Za-z_][A-Za-z0-9_]*)\"")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <see cref="MainWindow"/> 的全部 partial —— 按**声明**取面，不按文件名，
    /// 否则 <c>Wiring.*.cs</c> 会被漏掉（那正是"100 个控件看起来没接线"的成因）。
    /// </summary>
    private static List<(string Relative, string Text)> MainWindowPartials() =>
        AppSourceScanner.ScanAppSources()
            .Where(s => s.Text.Contains("partial class MainWindow", StringComparison.Ordinal))
            .ToList();
}
