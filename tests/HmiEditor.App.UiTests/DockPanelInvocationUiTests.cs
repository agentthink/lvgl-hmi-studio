using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 停靠面板**内部**按钮（`Btn*`）的行为判据。承接登记表 #41（阶段 49 遗留① 的最后一块）。
///
/// <para><b>与阶段 50 / 51 的关系</b>：阶段 50 覆盖 6 个**宿主对话框**内部的按钮；阶段 51 覆盖
/// **主窗口自己的工具栏**（65 个按钮）。本层是第三种容器：主窗口里**停靠的面板**。
/// 它既**不在主窗口的 NameScope 里**（每个 <c>UserControl</c> 有自己的 NameScope
/// ⇒ 取实例必须走 <c>panel.FindControl</c>），也不是"从菜单 <c>ShowDialog</c> 出来"的 ——
/// 它的"打开"路径是点 12 个 <c>MenuPanelXxx</c> 菜单项（见 <c>PanelMenus</c>，
/// 默认布局只渲染 5/12，因为 <c>DeferredContentPresenter</c> 是惰性的）。</para>
///
/// <para><b>面取证（4 轮，全部实测）</b>：</para>
/// <list type="number">
/// <item>默认布局只有 **5/12** 个面板被渲染（<c>ViewLocator</c> 的活动视图注册表只在 <c>Build</c> 时填充）；
///   面板内 <c>Btn*</c> 只有 6 个；主窗口 <c>FindControl</c> 对面板内按钮 **0 命中**。</item>
/// <item>用生产路径点 12 个 <c>MenuPanelXxx</c> 菜单项 ⇒ **12/12** 解析；面板内 <c>Btn*</c> 合计 **22**；
///   面板自己的 <c>FindControl</c> **22/22**；12 个面板里 **7 个不在可视树里**（同 tab 组只有激活页在树上）。</item>
/// <item>逐个真点 22 个：**19 静默 / 1 抛异常 / 2 弹窗**。弹窗增量：<c>AiIcon</c> +1、<c>AiTemplate</c> **+4**（当时读数）。阶段 54 之后为 **20 静默 / 0 抛异常 / 2 弹窗**，<c>AiTemplate</c> 也是 +1。</item>
/// <item>反射数事件订阅者 + 框架语义实验，解释了 4 的成因（见下）。</item>
/// </list>
///
/// <para><b>`AiTemplate` 点一次弹 4 个的成因（缺陷 #43 + #46，均已修）</b>：
/// ① <c>FaceplateLibraryView.axaml</c> 写了 <c>Click="OnAiTemplateClicked"</c>，
/// 而 <c>.axaml.cs</c> 又写了 <c>BtnAiTemplate.Click += OnAiTemplateClicked</c> —— **同一 handler 接了两路**。
/// Avalonia 12 **不做去重**（实测：<c>Click += H</c> 两次 ⇒ H 被执行 **2 次**，
/// 且 <c>Click += H</c> 与 <c>AddHandler(ClickEvent, H)</c> 混用同样是 2 次）。
/// ② 每次引发 <c>AiTemplateRequested</c> 又会命中 **2 个**订阅者（视图构造里的转发器 → 静态
/// <c>DockLayoutState.FaceplateAiTemplateRequested</c>，以及 <c>MainWindow.WireFaceplateAndAiEvents</c> 的直订）。
/// ⇒ 2 × 2 = 4。</para>
///
/// <para><b>两个阶段各修一半，读数是"台阶"</b>：阶段 53 删掉 ①（12 处双接线，含本处）⇒ **+2**；
/// 阶段 54 按「**删宿主直订、只留总线**」删掉 ②（全仓 28 处实例直订）⇒ **+1**。
/// 对照：<c>Assets.BtnAiIcon</c> 从没变过（一直 +1）—— 它本来就**单接线**，
/// 且 <c>OpenAiIconDialogAsync</c> 有 <c>_suppressAiIconEvent</c> 压住第二路。</para>
///
/// <para><b>本判据现在**判数量**（阶段 53 更正）</b>：当初只判类型，因为数量里混着待修缺陷（#43），
/// 断言数量等于把缺陷固化成期望。两路删干净后，数量＝**订阅者个数**×1，且本类独占进程（#45）、
/// 只有一条建窗用例 ⇒ 每个面板视图只构造一次、只转发一次 ⇒ 数量是确定的：多一个就是多一路接线。
/// 顺序不能颠倒 —— **先判类型 → 修掉多出来的那一路 → 再判数量**。</para>
///
/// <para><b>为什么只有一条用例建窗口并打开面板（重要约束）</b>：
/// <c>ViewLocator._activeViews</c> 是**静态**字典，Dock layout 对象的 <c>Context</c> 又是
/// <c>ViewLocator.Build</c> 复用视图实例的依据 —— 两者都是**进程级**的。同一个进程里第二次
/// "建主窗口 + 打开面板"时，新窗口会拿到**已经挂在旧窗口可视树上**的视图实例，
/// 布局阶段直接抛 <c>InvalidOperationException: ... already has a visual parent ...</c>。
/// （阶段 51 的工具栏用例各建一个窗口却没问题，因为它们从不打开面板 ⇒ <c>DeferredContentPresenter</c>
/// 惰性，面板视图根本没 Build。首跑实测：本类原来 4 条用例里 2 条建窗又开面板 ⇒ 那 2 条红。）
/// ⇒ 行为 / 枚举 / NameScope 三段断言合并进一条用例，共用同一个窗口。</para>
///
/// <para><b>本类必须与「大队」分开跑（否则宿主僵死）</b>：<c>UiTestHost</c> 的 headless 会话是
/// **进程级单例**（2026-09-21 更正：原写「、绑死在第一个会话的 UI 线程上」，反编译核实不成立 —— 那句描述的是渲染循环，而渲染循环没有静态实例，见专题 §8 的 #47）（该文件里的注释说明了同一进程开不出第二套平台配置，
/// 另一个渲染工程就是为此走独立 testhost 进程）。本类是仓库里**唯一**会一次打开 12 个停靠面板、
/// 并在秒级持续占用那条 UI 线程的用例 —— 与大队同进程时实测**宿主僵死**：零输出、零异常、零 CPU，
/// blame 报「90 秒无活动」。取证：全量跑 4/4 挂（blame 单独点名本类）；把它剔掉 **321/321 绿 / 20 s**。
/// ⇒ 门禁把它单独成段跑（见登记表 #45）。</para>
///
/// <para><b>不抓什么</b>：① 不判副作用（切换语言、写状态栏、改内存对象都不可见也不该在这层判）；
/// ② 不做真实命中测试 ⇒ 按钮"可不可达"不在面内；③ 走 <c>IStorageProvider</c> 的按钮不点
/// （headless 下退化静默，观测到的是平台桩而不是接线）；④ **窗口数量只判 <c>Popup</c> 桶的本次增量**，
/// 不判绝对总数 —— <c>DockLayoutState</c> 是静态事件总线且**订阅从不解绑**，绝对值会随
/// "本进程此前建过几个主窗口"漂移，增量才是确定的（前提是本类独占进程，见上面 #45 那段）。</para>
/// </summary>
[Collection("Language-global")]
public class DockPanelInvocationUiTests
{
    private readonly ITestOutputHelper _out;

    public DockPanelInvocationUiTests(ITestOutputHelper o) => _out = o;

    private enum Bucket
    {
        /// <summary>点了必然出现新窗口（owner = 主窗口）。</summary>
        Popup,

        /// <summary>接上了，但在本层"默认状态"下不动（早退条件不成立 / 纯内存改写）。</summary>
        Silent,

        /// <summary>点了**必然抛异常** —— 已登记的缺陷，作为回归守卫留在这里。</summary>
        Broken,

        /// <summary>本层不点：行为由平台桩决定，观测不到接线。</summary>
        Never,
    }

    /// <param name="ExpectCount">
    /// 点一次应新增的窗口数（仅 <see cref="Bucket.Popup"/> 用）。
    /// <para>为什么现在**可以**判数量（阶段 52 更正）：当初不判是因为数量里混着待修缺陷（#43）。
    /// 双接线删掉后，剩余数量＝**订阅者个数**×1，而本类独占进程（#45）、又只有一条建窗用例
    /// ⇒ 每个面板视图只被构造一次、每次构造只转发一次到静态总线，订阅者个数是**确定的**。
    /// 数量多一个就说明多了一路订阅/接线。</para>
    /// </param>
    private sealed record BtnSpec(string Panel, string Name, Bucket Bucket, Type? ExpectWindow, string Why,
        int ExpectCount = 1);

    /// <summary>22 个面板内具名按钮的分类表。`Why` 是**实测结论**，不是从名字猜的。</summary>
    private static readonly BtnSpec[] Buttons =
    {
        // ── Popup（2）：点了必然出现新窗口 ────────────────────────────────
        new("Assets", "BtnAiIcon", Bucket.Popup, typeof(AiIconDialog),
            "→ 视图转发器 → DockLayoutState.AiIconRequested → OpenAiIconDialogAsync（单路；实测 +1）"),
        new("Faceplate", "BtnAiTemplate", Bucket.Popup, typeof(AiTemplateDialog),
            "→ FaceplateAiTemplateRequested → AiTemplateDialog（读数链 **+4 → +2 → +1**："
            + "+4 = 「XAML 双接线 × 事件双路」，阶段 53 删双接线、阶段 54 删实例直订）"),

        // ── Silent（16）：接上了，默认状态下不动 ───────────────────────────
        new("Layers", "BtnMoveUp", Bucket.Silent, null, "LayerList.SelectedItem 为空 ⇒ if 不成立"),
        new("Layers", "BtnMoveDown", Bucket.Silent, null, "同上"),
        new("Layers", "BtnLockAll", Bucket.Silent, null, "纯内存：遍历 _layers 置 IsLocked + 发 LayerChanged"),
        new("Layers", "BtnUnlockAll", Bucket.Silent, null, "同上（反向）"),
        new("Navigation", "BtnRefresh", Bucket.Silent, null, "只重画自己的列表"),
        new("Navigation", "BtnSave", Bucket.Silent, null, "_graph 为空 ⇒ 只写状态栏；非空才发 GraphChanged（由宿主决定落盘）"),
        new("Navigation", "BtnLoad", Bucket.Silent, null, "发 GraphChanged + 写状态栏，本视图不碰磁盘"),
        new("Property", "BtnClearScreenBgImage", Bucket.Silent, null, "_isUpdating 或 CurrentScreen 为空 ⇒ 早退"),
        new("Property", "BtnVsImageClear", Bucket.Silent, null, "_currentControl 为空 ⇒ 早退"),
        new("Property", "BtnResetRotation", Bucket.Silent, null, "_currentControl 为空 ⇒ 早退"),
        new("ProjectExplorer", "BtnDelete", Bucket.Silent, null, "ProjectTree.SelectedItem 不是 HmiControl ⇒ 不发 DeleteRequested"),
        new("Faceplate", "BtnNewTemplate", Bucket.Silent, null, "无选中控件 ⇒ CreateTemplateFromSelection 只写状态栏"),
        new("Faceplate", "BtnEditTemplate", Bucket.Silent, null, "_selectedTemplate 为空 ⇒ 不发事件"),
        new("Faceplate", "BtnDeleteTemplate", Bucket.Silent, null, "_selectedTemplate 为空 ⇒ 不弹确认框（选中有值时才会弹，见 #43）"),
        new("Assets", "BtnRefresh", Bucket.Silent, null, "重载自己的列表 + 写状态栏"),
        new("Property", "BtnAutoFix", Bucket.Silent, null,
            "缺陷 #44 **已随 #46 的删除一并消失**（阶段 54 实测）：_currentControl 为 null 时它仍以 `_currentControl!` " +
            "调 PropertyChanged，但**唯一**消费方 PropertyPanelView 的转发器带 `if (control != null)` ⇒ null 被丢弃。" +
            "此前会抛 ArgumentNullException 的是宿主那条**没有 null 守卫的实例直订** —— 已删除。" +
            "⇒ 本行的角色从「缺陷回归守卫」变成「**不许把实例直订加回来**」：谁加回去，这行立刻变红"),

        // ── Broken（0）：本桶**当前为空**，刻意保留 ──────────────────────
        // 它是"已知必然抛异常"的暂存位；缺陷修好就把该行移入 Silent（#44 就是这么走的）。
        // 保留空桶是为了下一次登记缺陷时不必先改 switch 分支。

        // ── Never（4）：本层不点 ────────────────────────────────────────
        new("Assets", "BtnImportImage", Bucket.Never, null,
            "走 TopLevel.StorageProvider.OpenFilePickerAsync：headless 下退化为静默（实测无窗口、无异常），" +
            "即观测到的是**平台桩**而非接线；真机上会打开系统文件选择器"),
        new("Assets", "BtnImportSvg", Bucket.Never, null, "同上"),
        new("Assets", "BtnImportFont", Bucket.Never, null, "同上"),
        new("Property", "BtnChooseScreenBgImage", Bucket.Never, null, "同上（OpenFilePickerAsync）"),
    };

    /// <summary>打开全部面板用的菜单项（与 <c>ViewLocator</c> 注册的 12 个 dockable id 一一对应）。</summary>
    private static readonly (string Menu, string Id)[] PanelMenus =
    {
        ("MenuPanelToolbox", "Toolbox"),
        ("MenuPanelFaceplate", "Faceplate"),
        ("MenuPanelCanvas", "Canvas"),
        ("MenuPanelProjectExplorer", "ProjectExplorer"),
        ("MenuPanelProperty", "Property"),
        ("MenuPanelLayers", "Layers"),
        ("MenuPanelAssets", "Assets"),
        ("MenuPanelResourceLibrary", "ResourceLibrary"),
        ("MenuPanelNavigation", "Navigation"),
        ("MenuPanelAuditLog", "AuditLog"),
        ("MenuPanelCrossReference", "CrossReference"),
        ("MenuPanelAiChat", "AiChat"),
    };

    // ══════════════════════════════════════════════════════════════════
    //  主判据（唯一建窗用例）：行为 + 登记完整性 + NameScope 归属
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void DockPanelButtons_BehaveAsDeclared()
    {
        using var sandbox = new DataRootSandbox("dockpanel-behave");
        var errors = new List<string>();
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            var window = ShowMainWindow(sandbox);
            try
            {
                OpenAllPanels(window);

                CheckBehaviour(window, errors, readings);
                CheckEnumeratedMatchesTable(errors, readings);
                CheckNameScopeOwnership(window, errors, readings);
            }
            finally
            {
                CloseAllWindows(window);
                window.Close();
            }
        });

        // 读数出口（登记表 #42）：三段读数（行为 / 枚举 / NameScope）走 ITestOutputHelper
        // 与 HMI_READINGS_OUT 文件 —— 否则判据全绿时这三段的数字一律拿不到。
        JudgeReadings.Write(_out, "停靠面板内按钮（行为 + 枚举 + NameScope 三面）", readings);

        Assert.True(errors.Count == 0,
            "面板内按钮判据不通过：\n" + string.Join(Environment.NewLine, errors)
            + "\n--- 读数 ---\n" + string.Join(Environment.NewLine, readings));
    }

    /// <summary>段 1：22 个按钮按桶表行事。</summary>
    private static void CheckBehaviour(MainWindow window, List<string> errors, List<string> readings)
    {
        readings.Add("  [行为] 按表逐桶核对");
        var resolved = 0;
        var clicked = 0;

        foreach (var spec in Buttons)
        {
            var label = $"{spec.Panel}.{spec.Name}";
            var panel = ViewLocator.GetActiveView(spec.Panel);
            if (panel is null)
            {
                errors.Add($"[行为][{label}] 面板未解析 —— 菜单路径没能把 {spec.Panel} 建出来");
                continue;
            }

            var control = panel.FindControl<Control>(spec.Name);
            if (control is null)
            {
                errors.Add($"[行为][{label}] 面板自己的 FindControl 未命中 —— 登记了却拿不到实例");
                continue;
            }
            if (control is not Button button)
            {
                errors.Add($"[行为][{label}] 具名控件不是 Button/ToggleButton（实际基类链 {BaseChain(control.GetType())}）");
                continue;
            }
            resolved++;

            if (spec.Bucket == Bucket.Never)
            {
                readings.Add($"    {label,-31} Never    不点（{spec.Why}）");
                continue;
            }

            CloseAllWindows(window);
            var before = window.OwnedWindows.Count;
            Exception? thrown = null;
            try
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(3);
                DriveLayout(window);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            clicked++;
            var opened = window.OwnedWindows.Skip(before).ToArray();
            var kinds = opened.Select(w => w.GetType().Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

            switch (spec.Bucket)
            {
                case Bucket.Popup:
                    if (thrown is not null)
                        errors.Add($"[行为][{label}] 期望弹窗，却抛了 {RootCause(thrown)}");
                    else if (opened.Length == 0)
                        errors.Add($"[行为][{label}] 期望弹出 {spec.ExpectWindow!.Name}，实际**一个窗口都没出现**");
                    else if (!opened.Any(w => w.GetType() == spec.ExpectWindow))
                        errors.Add($"[行为][{label}] 期望 {spec.ExpectWindow!.Name}，实际 [{string.Join(", ", kinds)}]");
                    else if (opened.Length != spec.ExpectCount)
                        errors.Add($"[行为][{label}] 期望点一次只出现 {spec.ExpectCount} 个 {spec.ExpectWindow!.Name}，"
                                   + $"实际 {opened.Length} 个 [{string.Join(", ", kinds)}] —— "
                                   + "每多一个都对应一路**多余的接线或订阅**（缺陷 #43 家族：XAML+code-behind 双接线、"
                                   + "或\"视图转发到静态总线\"与\"宿主直订视图\"两路都活着）");
                    readings.Add($"    {label,-31} Popup    +{opened.Length} [{(kinds.Count == 0 ? "无" : string.Join(", ", kinds))}]"
                                 + $"（期望 {spec.ExpectCount}）");
                    break;

                case Bucket.Silent:
                    if (thrown is not null)
                        errors.Add($"[行为][{label}] 期望静默，却抛了 {RootCause(thrown)}");
                    else if (opened.Length != 0)
                        errors.Add($"[行为][{label}] 期望静默，实际弹出 [{string.Join(", ", kinds)}]");
                    readings.Add($"    {label,-31} Silent   {(thrown is null ? "无异常、无新窗口" : RootCause(thrown))}");
                    break;

                case Bucket.Broken:
                    if (thrown is null)
                        errors.Add($"[行为][{label}] 期望抛异常（登记的缺陷尚未修），实际没抛 —— "
                                   + "缺陷像已修好，请把这一行移入 Silent 桶");
                    else if (thrown.InnerException is not ArgumentNullException ane)
                        errors.Add($"[行为][{label}] 期望内因 ArgumentNullException，实际 {RootCause(thrown)}");
                    else
                        readings.Add($"    {label,-31} Broken   抛 ArgumentNullException: {ane.Message}");
                    break;
            }

            CloseAllWindows(window);
        }

        if (resolved < 20)
            errors.Add($"[行为] 只解析出 {resolved} 个面板内具名按钮（预期 22）—— 主面可能整体失效");
        if (clicked < 16)
            errors.Add($"[行为] 实际只点了 {clicked} 个按钮（预期 18：22 - 4 个 Never）—— 桶表可能被掏空");

        var panelsSeen = Buttons.Select(b => b.Panel).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        readings.Add($"    涉及面板 {panelsSeen.Count} 个：{string.Join(", ", panelsSeen)}");
        readings.Add($"    面：按面板名解析 {resolved} 个；真点 {clicked} 个；不点 {Buttons.Count(b => b.Bucket == Bucket.Never)} 个");
    }

    /// <summary>段 2：面板里**真实枚举到**的具名按钮 ⇄ 分类表双向差集（守"漏登记"与"登记了但没有"）。</summary>
    private static void CheckEnumeratedMatchesTable(List<string> errors, List<string> readings)
    {
        readings.Add("  [枚举] 面板内真实具名按钮 ⇄ 分类表");
        var seen = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (_, id) in PanelMenus)
        {
            var panel = ViewLocator.GetActiveView(id);
            if (panel is null)
            {
                errors.Add($"[枚举] 面板 {id} 未解析");
                continue;
            }
            seen[id] = BtnNamesOf(panel);
        }

        var declared = Buttons.ToDictionary(b => (b.Panel, b.Name), b => b);
        var found = new HashSet<(string, string)>();
        var total = 0;

        foreach (var (id, names) in seen.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            total += names.Count;
            readings.Add($"    {id,-16} {names.Count,2} 个：[{string.Join(", ", names)}]");
            foreach (var n in names)
            {
                if (declared.ContainsKey((id, n))) found.Add((id, n));
                else errors.Add($"[枚举][{id}.{n}] 面板里真实存在，但**分类表里没有** —— 漏登记");
            }
        }

        foreach (var (panel, name) in declared.Keys)
            if (!found.Contains((panel, name)))
                errors.Add($"[枚举][{panel}.{name}] 分类表登记了，但面板里**枚举不到**（名字写错？控件没了？）");

        // 同名跨面板重复**是事实而不是错误**（实测 BtnRefresh 同时在 Navigation 与 Assets）——
        // 它的价值是说明"取实例必须限定在面板自己的 NameScope 内"，所以只记读数。
        var shared = seen
            .SelectMany(kv => kv.Value.Select(n => (Panel: kv.Key, Name: n)))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}（{string.Join(" / ", g.Select(x => x.Panel))}）")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        if (shared.Count > 0)
            readings.Add($"    同名跨面板重复（取实例必须限定面板自己的 NameScope）：{string.Join("；", shared)}");

        readings.Add($"    实测合计 {total} 个；表内 {declared.Count} 个；命中 {found.Count} 个");
        if (total < 20)
            errors.Add($"[枚举] 只拿到 {total} 个面板内具名按钮（预期 22）—— 枚举面可能整体失效");
    }

    /// <summary>
    /// 段 3：把"面选对了"这一步钉死 —— 阶段 51 的教训是"逻辑树看不见集合属性里的控件"，
    /// 本层的对应事实是"**主窗口看不见面板内的 `x:Name`**"。没有这条，将来有人把取实例改回
    /// <c>window.FindControl</c> 会得到 22 个 null，而那时的报错是"面板按钮没了"，不是"你换了面"。
    /// </summary>
    private static void CheckNameScopeOwnership(MainWindow window, List<string> errors, List<string> readings)
    {
        readings.Add("  [NameScope] 面板自己的 FindControl vs 主窗口的 FindControl");
        var viaPanel = 0;
        var viaWindow = 0;

        foreach (var spec in Buttons)
        {
            var panel = ViewLocator.GetActiveView(spec.Panel);
            if (panel is null)
            {
                errors.Add($"[NameScope][{spec.Panel}] 未解析");
                continue;
            }
            if (panel.FindControl<Control>(spec.Name) is not null) viaPanel++;
            else errors.Add($"[NameScope][{spec.Panel}.{spec.Name}] 面板自己的 NameScope 里找不到它");
            if (window.FindControl<Control>(spec.Name) is not null) viaWindow++;
        }

        readings.Add($"    面板自己的 FindControl 命中 {viaPanel}/{Buttons.Length}");
        readings.Add($"    主窗口的   FindControl 命中 {viaWindow}/{Buttons.Length}（预期 0 —— 面板有独立 NameScope）");

        if (viaPanel < 20)
            errors.Add($"[NameScope] 面板自己的 FindControl 只命中 {viaPanel} 个（预期 22）");
        if (viaWindow != 0)
            errors.Add($"[NameScope] 主窗口 FindControl 命中了 {viaWindow} 个面板内按钮（预期 0）—— "
                       + "要么面板的 NameScope 变共享了，要么取实例的路子写错了");
    }

    // ══════════════════════════════════════════════════════════════════
    //  桶表自检（不建窗口，纯表检查）
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void BucketTable_IsSelfConsistent()
    {
        var errors = new List<string>();

        if (Buttons.Length != 22) errors.Add($"表内 {Buttons.Length} 项（面取证实测 22 项）");

        foreach (var g in Buttons.GroupBy(b => (b.Panel, b.Name)))
            if (g.Count() > 1)
                errors.Add($"同一面板内重复登记：{g.Key.Panel}.{g.Key.Name}");

        foreach (var b in Buttons)
        {
            if (string.IsNullOrWhiteSpace(b.Why))
                errors.Add($"[{b.Panel}.{b.Name}] Why 为空 —— 分类理由必须写出来（它同时是文档）");
            if (b.Bucket == Bucket.Popup && b.ExpectWindow is null)
                errors.Add($"[{b.Panel}.{b.Name}] Popup 桶必须声明期望窗口类型");
            if (b.Bucket != Bucket.Popup && b.ExpectWindow is not null)
                errors.Add($"[{b.Panel}.{b.Name}] 非 Popup 桶不应声明期望窗口类型");
        }

        var popup = Buttons.Count(b => b.Bucket == Bucket.Popup);
        var silent = Buttons.Count(b => b.Bucket == Bucket.Silent);
        var broken = Buttons.Count(b => b.Bucket == Bucket.Broken);
        var never = Buttons.Count(b => b.Bucket == Bucket.Never);
        var clickable = popup + silent + broken;

        if (popup < 2) errors.Add($"Popup 桶只有 {popup} 项（实测 2：AiIcon / AiTemplate）");
        if (silent < 12) errors.Add($"Silent 桶只有 {silent} 项（实测 16）");
        if (never < 3) errors.Add($"Never 桶只有 {never} 项（实测 4：3 个 Import + 1 个 ChooseBgImage）");
        if (clickable < 18) errors.Add($"可点项合计 {clickable}（预期 18：22 - 4 个 Never）");

        // Broken 桶**不设下限**，刻意允许为空：它是"已知必然抛异常"的暂存位，缺陷修好就该把该行
        // 移入 Silent（#44 在阶段 54 就是这么走的：它的崩溃随 #46 的实例直订删除而消失）。
        // 曾经这里写的是 `broken != 1`（把"当前有 1 个未修缺陷"当成期望）—— 那是**快照式断言**：
        // 缺陷一修好，判据反过来报红，把修好的人挡在门外。分布只作为读数打印。

        // 桶分布本来只在这条 Assert 的 message 里 ⇒ 全绿时看不见。也走读数出口（#42）。
        var distribution = $"    Popup {popup} / Silent {silent} / Broken {broken} / Never {never}（合计 {Buttons.Length}）";
        JudgeReadings.Write(_out, "停靠面板内按钮（桶分布）", new[] { distribution });

        Assert.True(errors.Count == 0,
            "桶表自检不通过：\n" + string.Join(Environment.NewLine, errors)
            + $"\n--- 桶分布 ---\n{distribution}");
    }

    // ══════════════════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构造主窗口**并把 Dock 的惰性内容推到底**。
    ///
    /// <para><c>DeferredContentPresenter</c> 是惰性的：Dock 内容区的视图要等布局跑完、Load 队列排空后才加载。
    /// headless 下没有真实渲染循环 ⇒ 必须显式 <c>RunJobs(DispatcherPriority.Loaded)</c> 循环，
    /// 否则可视树里只剩 Dock 自己的 chrome（配方来自 <c>DockViewResolutionUiTests</c>）。</para>
    /// </summary>
    private static MainWindow ShowMainWindow(DataRootSandbox sandbox)
    {
        ResetStaticDockBus();
        var settings = new SettingsService(System.IO.Path.Combine(sandbox.DataRoot, "settings.json"));
        var window = new MainWindow(settings);
        window.Show();
        DriveLayout(window);
        return window;
    }

    /// <summary>
    /// 把 <see cref="DockLayoutState"/> 上的**静态**事件订阅清空，让用例在干净的总线上跑。
    ///
    /// <para><b>为什么需要：</b><c>DockLayoutState</c> 是静态事件总线，<c>WireEvents()</c> 只在
    /// <c>MainWindow</c> 构造时订阅、**从不退订**。同一进程里建过 N 个主窗口就有 N 份订阅，
    /// 面板按钮（走静态路由）的弹窗数会随"此前建过几个主窗口"漂移 —— 后面的用例点了按钮，
    /// **前面已关闭的窗口**的 handler 照样会跑（以已关闭窗口为 owner 弹对话框）。
    /// 那是真实的产品行为，但不是本层要测的东西（本层测"接线在不在"）。</para>
    ///
    /// <para>清空是安全的：新构造的 <c>MainWindow</c> 会在 <c>WireEvents()</c> 里重新订阅全部静态事件，
    /// 所以每次调用之后被测对象都是完整接线的。这里只清 <see cref="Delegate"/> 类型的静态字段
    /// （field-like event 的 backing field），不碰 <c>SelectedControl</c> 之类的状态字段。</para>
    /// </summary>
    private static void ResetStaticDockBus()
    {
        foreach (var f in typeof(DockLayoutState).GetFields(
                     BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
        {
            if (typeof(Delegate).IsAssignableFrom(f.FieldType))
                f.SetValue(null, null);
        }
    }

    private static void DriveLayout(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
            window.Measure(new Size(1280, 800));
            window.Arrange(new Rect(0, 0, 1280, 800));
            UiTestHost.Pump(4);
        }
        UiTestHost.UIThread.RunJobs(DispatcherPriority.Background);
    }

    /// <summary>用**生产路径**（点菜单项）把 12 个面板全部打开 —— 面板不是对话框，这是它唯一的"打开"方式。</summary>
    private static void OpenAllPanels(MainWindow window)
    {
        foreach (var (menu, _) in PanelMenus)
        {
            window.FindControl<MenuItem>(menu)?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            UiTestHost.Pump(3);
            DriveLayout(window);
        }
    }

    private static void CloseAllWindows(Window window)
    {
        foreach (var w in window.OwnedWindows.ToArray())
        {
            try { w.Close(); }
            catch { /* 收尾尽力而为 */ }
        }
        UiTestHost.Pump(2);
    }

    /// <summary>面板内所有名字以 <c>Btn</c> 开头的具名控件（逻辑树 ∪ 可视树，按名字去重）。</summary>
    private static List<string> BtnNamesOf(Control panel)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in panel.GetLogicalDescendants().OfType<Control>())
            if (!string.IsNullOrEmpty(c.Name) && c.Name.StartsWith("Btn", StringComparison.Ordinal))
                names.Add(c.Name);
        foreach (var c in panel.GetVisualDescendants().OfType<Control>())
            if (!string.IsNullOrEmpty(c.Name) && c.Name.StartsWith("Btn", StringComparison.Ordinal))
                names.Add(c.Name);
        return names.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>把异常链压成一行（<c>ArgumentNullException</c> 之类的内因不会出现在外层 message 里）。</summary>
    private static string RootCause(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            parts.Add($"{e.GetType().Name}: {e.Message}");
            if (parts.Count >= 3) break;
        }
        return string.Join(" ← ", parts);
    }

    private static string BaseChain(Type type)
    {
        var names = new List<string>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            names.Add(t.Name);
        return string.Join(" → ", names);
    }
}
