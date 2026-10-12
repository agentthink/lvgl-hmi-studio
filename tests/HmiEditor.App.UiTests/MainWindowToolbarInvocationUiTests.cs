using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using HmiEditor.App.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 主窗口**工具栏**按钮（<c>Btn*</c>）的行为判据：点一下到底会发生什么。
///
/// <para><b>为什么有这一层：</b>阶段 49 把 62 个菜单项点了一遍，阶段 50 把 6 个宿主对话框
/// **内部**的按钮点了一遍，但主窗口**自己**那 65 个工具栏 <c>Btn*</c> 一直只有静态绊线
/// （<see cref="MainWindowConstructionTests.EveryNamedControl_IsTouchedByTheCodeBehind"/>，
/// 它只问"名字有没有被碰过"，不问"接的是不是正确的处理器"）。
/// 而工具栏里**有 10 个与菜单共用同一个处理器委托**（<c>BtnUserManager.Click += openUserManager</c>
/// 与 <c>MenuUserManager.Click += openUserManager</c> 是同一个 lambda）⇒ 把接线接错、
/// 或者接到另一个对话框上，静态绊线完全看不见。</para>
///
/// <para><b>判据形状：</b>构造主窗口（生产构造路径），按 <see cref="Buttons"/> 里
/// **声明的顺序**逐个把按钮 <c>RaiseEvent(Button.ClickEvent)</c>，
/// 然后断言主窗口 <see cref="Window.OwnedWindows"/> 的**增量与类型**。</para>
///
/// <para><b>三个面，各有各的漏法（前两个守登记，第三个只做读数）：</b></para>
/// <list type="number">
///   <item><b>NameScope 面</b>（<see cref="CollectButtons"/> 的主路）：按表里的名字逐个
///   <c>FindControl</c> 解析实例。<c>x:Name</c> 是在 XAML 加载时注册进窗口 NameScope 的，
///   **与渲染无关** ⇒ 这是唯一能拿到全部 65 个的面，也是真正拿来点的那个面。</item>
///   <item><b>XAML 声明面</b>（<see cref="XamlDeclaredButtons_MatchTheClassifiedTable"/>）：
///   <c>MainWindow.axaml</c> 里写了 <c>x:Name="Btn…"</c> 的全部名字。与表做双向差集 ——
///   它独立于运行期，抓的是"新增按钮没人分类 / 按钮被删改名"。</item>
///   <item><b>逻辑树 / 可视树面</b>：**只作读数与"多出来"的检查**。它反映"哪些真被渲染出来了"，
///   是个有用的信号，但**不能当登记判据**（见下）。</item>
/// </list>
///
/// <para><b>为什么逻辑树不能当登记判据（首跑实测）：</b><c>MainWindow.axaml</c> 里 65 个具名
/// <c>Btn*</c> 中有 **44 个是 <c>&lt;CommandBarButton&gt;</c>**，声明在
/// <c>&lt;CommandBar.PrimaryCommands&gt;</c> 这个**集合属性**里。集合属性不是
/// <c>LogicalChildren</c> —— Avalonia 只在控件模板里把它渲染成可视子级 ⇒
/// <c>GetLogicalDescendants()</c> 里根本没有它们（首跑：65 里只找到 21，少的正好是这 44 个，
/// 而 21 恰好是 <c>StackPanel</c> 里的 <c>BtnTool*</c>(7) + <c>BtnQuick*</c>(14)）。
/// 用逻辑树当判据就会把整族正式按钮误报成"被删了"。</para>
///
/// <para><b>点击顺序是显式声明的量</b>（阶段 50 的教训）：数组顺序 = 点击顺序。
/// 本表的排序不是字母序，而是按"对状态的扰动从小到大"：
/// 先点**无选中即 no-op** 的对齐/剪贴板/变换组 → 绘图工具切换 → 缩放 → 弹窗入口 →
/// **最后**才是会真正改状态的（<c>BtnQuick*</c> 往画布加控件、<c>BtnNewScreen</c> 加屏幕、
/// <c>BtnReloadPanels</c> 重建 Dock 布局）。**改动数组顺序 = 改动被测语义。**</para>
///
/// <para><b>不抓什么：</b>① 「不弹窗」是静默的 —— 没接线与接了线但什么都不做，在这一层长得一样；
/// ② 动作型按钮（编译 / 构建 / 烧录 / 写盘）的**副作用**不判，只判"不抛异常 + 弹窗层数"；
/// ③ 不做真实命中测试 ⇒ 按钮的**可达性**不在面内：<c>CommandBar</c> 放不下的项会被收进溢出区，
/// 本判据照样点它（`RaiseEvent` 直接投递，不经过命中测试）—— 管的是"接线通不通"，
/// 不是"用户在 1024×600 下能不能够到"；④ 画布上的实际效果（对齐算得对不对、控件加没加上）
/// 不在面内。</para>
/// </summary>
[Collection("Language-global")]
public class MainWindowToolbarInvocationUiTests
{
    private readonly ITestOutputHelper _out;

    public MainWindowToolbarInvocationUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>按钮点下去之后**应该**是什么行为。</summary>
    private enum BtnBucket
    {
        /// <summary>必须弹出恰好 1 个窗口：增量必须为 1，且类型名相符。</summary>
        Popup,

        /// <summary>不弹窗：增量必须为 0（针对本判据的初始状态 + 本表的点击顺序）。</summary>
        Silent,

        /// <summary>不点：理由必填。</summary>
        Never,
    }

    private sealed record BtnSpec(string Name, BtnBucket Bucket, string? Window, string Why);

    private static BtnSpec Pop(string name, string window, string why) => new(name, BtnBucket.Popup, window, why);
    private static BtnSpec Quiet(string name, string why) => new(name, BtnBucket.Silent, null, why);
    private static BtnSpec Skip(string name, string why) => new(name, BtnBucket.Never, null, why);

    /// <summary>
    /// 面包屑：**数组顺序 = 点击顺序**（见类注释）。65 项 = XAML 里全部具名 <c>Btn*</c>。
    /// </summary>
    private static readonly BtnSpec[] Buttons =
    {
        // ── ① 多选对齐/尺寸/组合：无选中即 no-op（且这些按钮初始 IsVisible=false）──
        Quiet("BtnAlignLeft", "无选中 ⇒ ApplySelectionEdit 的判据不成立，不弹窗"),
        Quiet("BtnAlignRight", "同上：无选中 ⇒ no-op"),
        Quiet("BtnAlignTop", "同上：无选中 ⇒ no-op"),
        Quiet("BtnAlignBottom", "同上：无选中 ⇒ no-op"),
        Quiet("BtnAlignCenterH", "同上：无选中 ⇒ no-op"),
        Quiet("BtnAlignCenterV", "同上：无选中 ⇒ no-op"),
        Quiet("BtnSameSize", "需 ≥2 个选中，初始 0 个 ⇒ no-op"),
        Quiet("BtnDistributeH", "需 ≥3 个选中，初始 0 个 ⇒ no-op"),
        Quiet("BtnDistributeV", "同上：需 ≥3 个选中 ⇒ no-op"),
        Quiet("BtnGroup", "需 ≥2 个选中；GroupSelectedControls 返回 null ⇒ 不弹窗（但会置未保存标记）"),
        Quiet("BtnUngroup", "需选中里含组合成员，初始没有 ⇒ UngroupSelectedControls 返回 0 ⇒ no-op"),

        // ── ② 剪贴板/删除：无选中即 no-op ──
        Quiet("BtnCopy", "无选中 ⇒ 复制为空，不弹窗（会写内部剪贴板）"),
        Quiet("BtnPaste", "剪贴板为空 ⇒ 粘贴为 no-op（复制那步没东西可复制）"),
        Quiet("BtnDuplicate", "无选中 ⇒ 不弹窗"),
        Quiet("BtnDeleteControl", "无选中 ⇒ 不弹窗"),

        // ── ③ 旋转/镜像/复位：无选中即 no-op ──
        Quiet("BtnRotateLeft", "无选中 ⇒ 不弹窗"),
        Quiet("BtnRotateRight", "同上：无选中 ⇒ no-op"),
        Quiet("BtnMirrorH", "同上：无选中 ⇒ no-op"),
        Quiet("BtnMirrorV", "同上：无选中 ⇒ no-op"),
        Quiet("BtnResetTransform", "同上：无选中 ⇒ no-op"),

        // ── ④ 撤销/重做：历史为空 ──
        Quiet("BtnUndo", "撤销栈为空 ⇒ Undo() 为 no-op"),
        Quiet("BtnRedo", "重做栈为空 ⇒ Redo() 为 no-op"),

        // ── ⑤ 绘图工具切换（ToggleButton，只切画布绘图模式）──
        Quiet("BtnToolSelect", "切回选择工具：只改 DockLayoutState.ActiveDrawTool，不弹窗"),
        Quiet("BtnToolRectangle", "切到矩形工具：同上"),
        Quiet("BtnToolEllipse", "切到椭圆工具：同上"),
        Quiet("BtnToolPolyline", "切到折线工具：同上"),
        Quiet("BtnToolArc", "切到圆弧工具：同上"),
        Quiet("BtnToolLine", "切到直线工具：同上"),
        Quiet("BtnToolText", "切到文本工具：同上"),

        // ── ⑥ 缩放 ──
        Quiet("BtnZoomIn", "放大一档（0.25），不弹窗"),
        Quiet("BtnZoomOut", "缩小一档（0.25），不弹窗"),

        // ── ⑦ 弹窗入口：11 个。前 10 个与菜单**共用同一个处理器委托**（见类注释）──
        Pop("BtnUserManager", "UserManagerView", "与 MenuUserManager 共用处理器：用户管理"),
        Pop("BtnAlarmConfig", "AlarmConfigView", "与 MenuAlarmConfig 共用处理器：报警组配置"),
        Pop("BtnTrendConfig", "TrendConfigView", "与 MenuTrendConfig 共用处理器：趋势图配置"),
        Pop("BtnScriptEditor", "ScriptEditorView", "与 MenuScriptEditor 共用处理器：Lua 脚本编辑器"),
        Pop("BtnRecipeEditor", "RecipeEditorView", "与 MenuRecipeEditor 共用处理器：配方编辑器"),
        Pop("BtnVariableImport", "VariableImportView", "与 MenuVariableImport 共用处理器：变量批量导入"),
        Pop("BtnVariableManager", "VariableManagerView", "与 MenuVariableManager 共用处理器：变量管理器"),
        Pop("BtnSimulator", "SimulatorView", "与 MenuSimulator 共用处理器：离线仿真器"),
        Pop("BtnScreenManager", "ScreenManagerView", "与 MenuScreenManager 共用处理器：屏幕管理"),
        Pop("BtnStyleEditor", "StyleEditorView", "与 MenuStyleEditor 共用处理器：主题/样式编辑器"),
        Pop("BtnSettings", "SettingsDialog", "设置对话框 —— 工具栏独有入口（菜单里没有）"),

        // ── ⑧ 不点：起外部进程 / 写盘 / 换工程 ──
        Skip("BtnNewProject", "新建工程：会丢掉当前内存里的工程"),
        Skip("BtnOpenProject", "打开工程：走 StorageProvider 选文件 + 换掉当前工程"),
        Skip("BtnSaveProject", "保存工程：写工程文件到磁盘 —— **本用例跳过的只是这一层**："
                               + "这里的窗口从没绑定过工程文件路径（`_currentHmiPath` 为空）⇒ 点了会走选择器分支，"
                               + "而 headless 下选择器静默返回空（不抛、不弹窗 ⇒ 连「没弹窗」都不可判）。"
                               + "保存/打开路径的真判据在 `ProjectFileOpenSaveRoundTripUiTests`（隔离目录里真写盘、真回读）"),
        Skip("BtnExportCode", "导出生成代码：写整套代码到磁盘"),
        Skip("BtnCompile", "编译：起外部编译进程"),
        Skip("BtnBuildRun", "构建并运行仿真器：起外部构建进程"),
        Skip("BtnDownloadHmi", "下载到 HMI：向设备推送文件，不可逆的外部动作"),

        // ── ⑨ 会真正改状态：放在最后，之后不再有点击 ──
        Quiet("BtnQuickButton", "在画布中心加一个按钮控件（改状态、不弹窗）"),
        Quiet("BtnQuickLabel", "同上：加标签控件"),
        Quiet("BtnQuickSlider", "同上：加滑块控件"),
        Quiet("BtnQuickSwitch", "同上：加开关控件"),
        Quiet("BtnQuickCheckBox", "同上：加复选框控件"),
        Quiet("BtnQuickLed", "同上：加指示灯控件"),
        Quiet("BtnQuickGauge", "同上：加仪表盘控件"),
        Quiet("BtnQuickBar", "同上：加条形图控件"),
        Quiet("BtnQuickDropdown", "同上：加下拉框控件"),
        Quiet("BtnQuickTable", "同上：加表格控件"),
        Quiet("BtnQuickImage", "同上：加图片控件"),
        Quiet("BtnQuickKnob", "同上：加旋钮控件"),
        Quiet("BtnQuickTextArea", "同上：加多行文本控件"),
        Quiet("BtnQuickKeyboard", "同上：加键盘控件"),
        Quiet("BtnNewScreen", "新建一个屏幕并切过去（改状态、不弹窗）—— 排在加控件之后"),
        Quiet("BtnReloadPanels", "重新加载全部 Dock 面板内容（会重建面板树）—— **必须最后**，它会动到控件树"),
    };

    private static MainWindow ShowMainWindow(DataRootSandbox sandbox)
    {
        var settings = new SettingsService(System.IO.Path.Combine(sandbox.DataRoot, "settings.json"));
        var window = new MainWindow(settings);
        window.Show();
        UiTestHost.Pump();
        window.Measure(new Size(1024, 600));
        window.Arrange(new Rect(0, 0, 1024, 600));
        UiTestHost.Pump();
        return window;
    }

    /// <summary>
    /// 主面：**按表里的名字逐个从窗口 NameScope 解析实例**（<c>FindControl</c>），
    /// 也就是真正拿去点的那个面。
    ///
    /// <para><b>为什么不是遍历逻辑树</b>：65 个里有 44 个是声明在
    /// <c>&lt;CommandBar.PrimaryCommands&gt;</c> 集合属性里的 <c>CommandBarButton</c>，
    /// 它们不进 <c>LogicalChildren</c> ⇒ 逻辑树看不见（首跑实测 65→21）。
    /// 而 <c>x:Name</c> 注册在窗口 NameScope 里，与渲染无关。</para>
    ///
    /// <para>逻辑树与可视树在这里退居**读数与"多出来"的检查**：
    /// 读数是"哪些真被渲染出来了"（CommandBar 的溢出项、折叠容器内容都不在其中），
    /// 而"多出来"的方向仍要报 —— 那是"树上有个 Btn* 没人分类"。</para>
    ///
    /// <para>类型必须是 <see cref="Button"/>（<see cref="ToggleButton"/> 派生自它，自动包含）。
    /// 不符的那一刻把**基类链**打进消息里，省得下次还要猜 <c>CommandBarButton</c> 到底继承谁。</para>
    /// </summary>
    private static List<Button> CollectButtons(Window window, List<string> errors, List<string> readings)
    {
        var logical = window.GetLogicalDescendants().OfType<Control>()
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c => c.Name!)
            .ToHashSet(StringComparer.Ordinal);

        var visual = window.GetVisualDescendants().OfType<Control>()
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c => c.Name!)
            .ToHashSet(StringComparer.Ordinal);

        var found = new List<Button>();
        var missing = new List<string>();

        foreach (var spec in Buttons)
        {
            var control = window.FindControl<Control>(spec.Name);
            if (control is null)
            {
                var where = logical.Contains(spec.Name) ? "逻辑树里有"
                    : visual.Contains(spec.Name) ? "只有可视树里有（模板渲染出来但不在 NameScope）"
                    : "两个树里都没有";
                missing.Add($"{spec.Name}（{where}）");
                continue;
            }

            if (control is not Button button)
            {
                errors.Add($"具名控件 {spec.Name} 不是 Button/ToggleButton（实际基类链 {BaseChain(control.GetType())}）"
                           + " —— 面里混进了不该混的东西，本判据点不了它");
                continue;
            }

            found.Add(button);
        }

        if (missing.Count > 0)
            errors.Add($"表里登记了但主窗口里解析不出这些按钮：{string.Join(", ", missing)}"
                       + " —— 控件被删/改名，或它压根不在 MainWindow.axaml 的 NameScope 里");

        if (found.Count < 60)
            errors.Add($"只解析出 {found.Count} 个具名 Btn* 实例（预期 65）—— 主面可能整体失效");

        readings.Add($"    面：按名解析 {found.Count} 个；逻辑树已渲染 "
                     + $"{Buttons.Count(b => logical.Contains(b.Name))} 个，可视树 "
                     + $"{Buttons.Count(b => visual.Contains(b.Name))} 个");

        // "多出来"：树上出现了 Btn* 但表里没登记（逻辑树 + 可视树合起来看，防漏）
        var undeclared = logical.Union(visual, StringComparer.Ordinal)
            .Where(n => n.StartsWith("Btn", StringComparison.Ordinal))
            .Except(Buttons.Select(b => b.Name), StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        if (undeclared.Length > 0)
            errors.Add($"主窗口里有这些具名按钮没被分类：{string.Join(", ", undeclared)}"
                       + " —— 请判断它该进 Popup（必须弹）还是 Quiet（本初始状态下不弹），或写清为什么不点（Skip）");

        return found;
    }

    private static string BaseChain(Type type)
    {
        var parts = new List<string>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            parts.Add(t.Name);
        return string.Join(" < ", parts);
    }

    [Fact]
    public void MainWindowToolbarButtons_BehaveAsDeclared()
    {
        using var sandbox = new DataRootSandbox("toolbar");
        var errors = new List<string>();
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            var window = ShowMainWindow(sandbox);
            try
            {
                var found = CollectButtons(window, errors, readings);
                var byName = found.ToDictionary(b => b.Name!, b => b, StringComparer.Ordinal);
                var clicked = 0;

                foreach (var spec in Buttons)
                {
                    if (spec.Bucket == BtnBucket.Never)
                    {
                        readings.Add($"    {spec.Name}（跳过）");
                        continue;
                    }

                    if (!byName.TryGetValue(spec.Name, out var control))
                        continue; // 面已经报过 missing

                    var before = window.OwnedWindows.Count;
                    try
                    {
                        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        UiTestHost.Pump(5);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{spec.Name}：点击抛出 {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    clicked++;
                    var inner = window.OwnedWindows.Skip(before).ToArray();
                    readings.Add($"    {spec.Name} → +{inner.Length}"
                                 + (inner.Length > 0
                                     ? "：" + string.Join(",", inner.Select(w => w.GetType().Name))
                                     : string.Empty));

                    if (inner.Length > 1)
                        errors.Add($"{spec.Name}：一次点击弹出了 {inner.Length} 个窗口"
                                   + $"（{string.Join(",", inner.Select(w => w.GetType().Name))}）");

                    if (spec.Bucket == BtnBucket.Popup)
                    {
                        if (inner.Length != 1)
                            errors.Add($"{spec.Name}：预期弹出 1 个 {spec.Window}，实际增量 {inner.Length}");
                        else if (!string.Equals(inner[0].GetType().Name, spec.Window, StringComparison.Ordinal))
                            errors.Add($"{spec.Name}：弹出的窗口类型是 {inner[0].GetType().Name}，"
                                       + $"预期 {spec.Window}（这一条同时说明 Click 可能接到了别的处理器上）");
                    }
                    else if (inner.Length != 0)
                    {
                        errors.Add($"{spec.Name}：预期本初始状态下不弹窗，实际弹出了 "
                                   + string.Join(",", inner.Select(w => w.GetType().Name))
                                   + " —— 请判断它该进 Popup（必须弹）还是写清为什么是条件性的");
                    }

                    foreach (var w in inner)
                    {
                        if (!w.IsVisible) errors.Add($"{spec.Name}：弹出的 {w.GetType().Name} 不可见");
                        try { w.Close(); }
                        catch { errors.Add($"{spec.Name}：关闭 {w.GetType().Name} 时抛出异常"); }
                    }
                    UiTestHost.Pump();

                    if (window.OwnedWindows.Count != before)
                        errors.Add($"{spec.Name}：关闭窗口后主窗口的 OwnedWindows 没有回到 {before}");
                    if (!window.IsVisible)
                    {
                        errors.Add($"{spec.Name}：点击后主窗口被关掉了，后续按钮无从验证");
                        break;
                    }
                }

                if (clicked < 40)
                    errors.Add($"只点了 {clicked} 个按钮（预期 58）—— 判据可能退化了");
            }
            finally
            {
                foreach (var w in window.OwnedWindows.ToArray())
                {
                    try { w.Close(); } catch { /* 收尾尽力而为 */ }
                }
                window.Close();
            }
        });

        // 读数出口（登记表 #42）：判据全绿时这些数字原本拿不到 —— 它们只拼进了下面那条
        // Assert 的 message，而 message **只在红的时候打印**。现在同一份 readings 也走
        // ITestOutputHelper（--logger "console;verbosity=detailed" 可见）与
        // HMI_READINGS_OUT 文件（门禁的 minimal logger 下也拿得到）。
        JudgeReadings.Write(_out, "主窗口工具栏（NameScope 按名解析面）", readings);

        Assert.True(errors.Count == 0,
            $"主窗口工具栏按钮行为不符预期：\n" + string.Join("\n", errors)
            + "\n\n读数：\n" + string.Join("\n", readings));
    }

    /// <summary>
    /// **XAML 声明面**：<c>MainWindow.axaml</c> 里写下的每个 <c>x:Name="Btn…"</c> 都必须在表里，
    /// 表里每个名字也必须在 XAML 里 —— 双向差集。
    ///
    /// <para>它独立于运行期，是"登记完整性"最干净的那条判据：新增按钮没人分类、按钮被删改名，
    /// 都在这里点名。之所以与 NameScope 面并存而不是合并：一个纯读源码（不需要起 Avalonia），
    /// 一个要真构造窗口；前者挂了能立刻区分"是我表写错了"还是"是窗口行为不对"。</para>
    /// </summary>
    [Fact]
    public void XamlDeclaredButtons_MatchTheClassifiedTable()
    {
        var xaml = AppSourceScanner.ScanAppXaml()
            .Single(s => s.Relative.EndsWith("MainWindow.axaml", StringComparison.Ordinal));

        var declared = Regex.Matches(xaml.Text, "x:Name=\"(Btn[A-Za-z0-9_]*)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= 60,
            $"MainWindow.axaml 只推导出 {declared.Count} 个具名 Btn*（应约 65）—— 扫描面可能失效");

        var table = Buttons.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);

        var missing = declared.Except(table, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var extra = table.Except(declared, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0 && extra.Length == 0,
            "主窗口工具栏的 XAML 声明与分类表不一致（新增按钮要分类；删按钮要连表一起删）："
            + $"{Environment.NewLine}  XAML 有、表里没有（{missing.Length}）：{string.Join(", ", missing)}"
            + $"{Environment.NewLine}  表里有、XAML 没有（{extra.Length}）：{string.Join(", ", extra)}");
    }

    /// <summary>
    /// 桶表的卫生：面非空、名字不重复、桶与字段自洽、理由写得出、不点的名字真的存在。
    /// </summary>
    [Fact]
    public void BucketTable_IsNotEmptiedAndIsSelfConsistent()
    {
        Assert.True(Buttons.Length >= 60, $"表只剩 {Buttons.Length} 个按钮 —— 面被掏空了");

        var popupCount = 0;
        var clickableCount = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var b in Buttons)
        {
            Assert.True(seen.Add(b.Name), $"{b.Name} 登记了两次");

            switch (b.Bucket)
            {
                case BtnBucket.Popup:
                    Assert.False(string.IsNullOrWhiteSpace(b.Window), $"{b.Name} 是 Popup 桶却没有期望窗口类型名");
                    Assert.True(b.Why.Trim().Length >= 1, $"{b.Name} 的理由是空的");
                    popupCount++;
                    clickableCount++;
                    break;

                case BtnBucket.Silent:
                    Assert.True(b.Window is null, $"{b.Name} 是 Silent 桶却写了期望窗口名 {b.Window} —— 两种桶的字段混了");
                    Assert.True(b.Why.Trim().Length >= 1, $"{b.Name} 的理由是空的");
                    clickableCount++;
                    break;

                case BtnBucket.Never:
                    Assert.True(b.Window is null, $"{b.Name} 是 Never 桶却写了期望窗口名 {b.Window}");
                    Assert.True(b.Why.Trim().Length >= 10, $"{b.Name} 属于 Never 桶，理由太短、说明不了什么：\"{b.Why}\"");
                    break;

                default:
                    Assert.Fail($"未处理的桶：{b.Bucket}");
                    break;
            }
        }

        Assert.True(popupCount >= 5, $"Popup 桶只剩 {popupCount} 个 —— 工具栏的弹窗入口几乎都没了");
        Assert.True(clickableCount >= 40, $"会被真点的只有 {clickableCount} 个 —— 判据快恒真了");

        // Never 桶里的名字必须在 MainWindow.axaml 里真的出现（防表腐烂成"名字打错也能过"）
        var xaml = AppSourceScanner.ScanAppXaml()
            .Single(s => s.Relative.EndsWith("MainWindow.axaml", StringComparison.Ordinal));
        foreach (var name in Buttons.Where(b => b.Bucket == BtnBucket.Never).Select(b => b.Name))
        {
            Assert.True(xaml.Text.Contains($"x:Name=\"{name}\"", StringComparison.Ordinal),
                $"Never 桶里的 {name} 在 MainWindow.axaml 里找不到 —— 名字写错了或控件已被删");
        }
    }
}
