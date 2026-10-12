using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 主窗口**菜单点击**的行为判据：点一个菜单项之后，它到底干了什么。
///
/// <para><b>为什么有这一层：</b>此前 62 个具名菜单项里只有 1 个（语言切换）有行为判据
/// （见 <see cref="MainWindowConstructionTests.MainWindow_LanguageMenuItem_ActuallySwitchesTheUi"/>），
/// 其余靠"名字被代码引用过"这类静态绊线 —— 它能证明 <c>MenuX.Click += …</c> 这行存在，
/// 但**证明不了点下去会怎样**。真正的坑恰恰在这里：<c>DeviceManagerView</c> 的构造函数
/// 曾经在 <c>InitializeComponent()</c> 期间抛 NRE（XAML 的 <c>SelectedIndex="0"</c> 同步触发
/// <c>SelectionChanged</c>，而处理器读的命名控件声明在它后面）⇒ 「设备管理」菜单点了没反应，
/// 从 2026-08-11 一直存在，任何静态守卫都抓不到。</para>
///
/// <para><b>判据形状：</b>用 <c>RaiseEvent(MenuItem.ClickEvent)</c> 真点，然后看主窗口的
/// <see cref="Window.OwnedWindows"/> —— <c>dialog.ShowDialog(this)</c> 在 headless 里
/// **不阻塞**（返回 Task），但窗口会**同步**出现在 owner 的 OwnedWindows 里，因此
/// 「增量 +1 且新窗口类型 == 期望类型且 IsVisible」是一条又强又稳的断言：
/// 它同时证明了 Click 接上了、处理器跑通了、对话框构造成功、ShowDialog 执行了、**类型对**。
/// 接错处理器（A 菜单接到 B 的处理器）会被"类型不符"抓到。</para>
///
/// <para><b>为什么不遍历全部 62 个：</b>其中 14 个会起外部进程 / 读写工程文件 / 关掉主窗口
/// （编译、烧录下载、git、新建打开保存工程、退出）。它们进「跳过桶」并**必须写明理由**，
/// 由 <see cref="EveryNamedMenuItem_IsClassified"/> 保证没有任何菜单项被漏掉 ——
/// 新增菜单项会被差集点名，从而强制有人来判断它属于哪个桶。</para>
///
/// <para><b>不抓什么：</b>不做真实命中测试（不点像素），因此菜单**是否可见/可展开**不在面内；
/// 只覆盖 <c>Menu*</c> 菜单项，工具栏与面板内的 <c>Btn*</c>（129 个注册点里的另一大块）未覆盖；
/// 「跳过桶」的行为无判据。</para>
/// </summary>
[Collection("Language-global")]
public class MainWindowMenuInvocationUiTests
{
    private readonly ITestOutputHelper _out;

    public MainWindowMenuInvocationUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>菜单项点下去之后**应该**是什么行为。</summary>
    private enum MenuBucket
    {
        /// <summary>无条件弹出一个对话框：OwnedWindows 增量必须恰好为 1，且类型相符。</summary>
        Popup,

        /// <summary>不弹窗：OwnedWindows 增量必须为 0。</summary>
        Silent,

        /// <summary>依当前状态决定弹不弹（例如需要先选中控件）：增量 ∈ {0,1}；为 1 时类型必须相符。</summary>
        Conditional,

        /// <summary>不点：会起外部进程 / 读写工程文件 / 关掉主窗口，理由必填。</summary>
        Skipped,
    }

    private sealed record MenuSpec(MenuBucket Bucket, Type? Expected, string Why);

    private static MenuSpec Popup(Type expected, string why) => new(MenuBucket.Popup, expected, why);
    private static MenuSpec Silent(string why) => new(MenuBucket.Silent, null, why);
    private static MenuSpec Conditional(Type? expected, string why) => new(MenuBucket.Conditional, expected, why);
    private static MenuSpec Skipped(string why) => new(MenuBucket.Skipped, null, why);

    /// <summary>62 个具名菜单项的完整分类。行的存在性不等于正确性 —— 由运行期行为判据兜。</summary>
    private static readonly Dictionary<string, MenuSpec> Buckets = new(StringComparer.Ordinal)
    {
        // ── 弹窗桶（18）──
        ["MenuAbout"] = Popup(typeof(Window), "内联构造一个裸 Window 作为「关于」消息框"),
        ["MenuAlarmConfig"] = Popup(typeof(AlarmConfigView), "报警组配置"),
        ["MenuAlarmList"] = Popup(typeof(AlarmListView), "实时报警列表"),
        ["MenuAnimationEditor"] = Popup(typeof(AnimationEditorView), "动画编辑器"),
        ["MenuCommConfig"] = Popup(typeof(CommunicationConfigView), "通讯配置"),
        ["MenuDeviceManager"] = Popup(typeof(DeviceManagerView), "设备管理器（本判据抓到的第一个真缺陷就在这）"),
        ["MenuLogin"] = Popup(typeof(LoginDialog), "登录对话框（async void 处理器，ShowDialog 仍同步显示）"),
        ["MenuProjectSettings"] = Popup(typeof(ProjectSettingsView), "工程设置"),
        ["MenuRecipeEditor"] = Popup(typeof(RecipeEditorView), "配方编辑器"),
        ["MenuScreenManager"] = Popup(typeof(ScreenManagerView), "屏幕管理"),
        ["MenuScriptEditor"] = Popup(typeof(ScriptEditorView), "Lua 脚本编辑器"),
        ["MenuSimulator"] = Popup(typeof(SimulatorView), "仿真器窗口"),
        ["MenuStyleEditor"] = Popup(typeof(StyleEditorView), "样式编辑器"),
        ["MenuTagMonitor"] = Popup(typeof(TagMonitorView), "变量监控"),
        ["MenuTrendConfig"] = Popup(typeof(TrendConfigView), "趋势图配置"),
        ["MenuUserManager"] = Popup(typeof(UserManagerView), "用户管理"),
        ["MenuVariableImport"] = Popup(typeof(VariableImportView), "变量导入"),
        ["MenuVariableManager"] = Popup(typeof(VariableManagerView), "变量管理器"),

        // ── 条件桶（2）──
        ["MenuCreateTemplate"] = Conditional(typeof(FaceplateTemplateEditorView),
            "从选中控件创建 Faceplate 模板；当前无选中 ⇒ 只写状态栏提示并 return（见 CreateTemplateFromSelection）"),
        ["MenuFaceplateEditor"] = Conditional(typeof(FaceplateTemplateEditorView),
            "打开模板编辑器；依赖 FaceplatePanel 的选中项或已加载模板集合，非空才开"),

        // ── 静默桶（28）──
        ["MenuAuditLog"] = Silent("切换审计日志面板可见性"),
        ["MenuCrossReference"] = Silent("切换交叉引用面板"),
        ["MenuDelete"] = Silent("删除选中控件；无选中即 return，永不弹窗"),
        ["MenuDockDiag"] = Silent("Dock 诊断信息面板"),
        ["MenuLangEnUS"] = Silent("切换语言为 en-US（全局单例，测试收尾还原）"),
        ["MenuLangZhCN"] = Silent("切换语言为 zh-CN（全局单例，测试收尾还原）"),
        ["MenuPanelAiChat"] = Silent("AI 助手面板显隐"),
        ["MenuPanelAssets"] = Silent("资源库面板显隐"),
        ["MenuPanelAuditLog"] = Silent("审计日志面板显隐"),
        ["MenuPanelCanvas"] = Silent("画布面板显隐"),
        ["MenuPanelCrossReference"] = Silent("交叉引用面板显隐"),
        ["MenuPanelFaceplate"] = Silent("Faceplate 面板显隐"),
        ["MenuPanelLayers"] = Silent("图层面板显隐"),
        ["MenuPanelNavigation"] = Silent("导航面板显隐"),
        ["MenuPanelProjectExplorer"] = Silent("工程浏览器面板显隐"),
        ["MenuPanelProperty"] = Silent("属性面板显隐"),
        ["MenuPanelResourceLibrary"] = Silent("资源库面板显隐"),
        ["MenuPanelToolbox"] = Silent("工具箱面板显隐"),
        ["MenuRedo"] = Silent("重做；无历史即 no-op"),
        ["MenuReloadPanels"] = Silent("重建 Dock 布局"),
        ["MenuSelectAll"] = Silent("画布全选；无控件即 no-op"),
        ["MenuThemeDark"] = Silent("切到 Dark 主题（全局单例，测试收尾还原）"),
        ["MenuThemeLight"] = Silent("切到 Light 主题（全局单例，测试收尾还原）"),
        ["MenuThemeOcean"] = Silent("切到 Ocean 主题（全局单例，测试收尾还原）"),
        ["MenuUndo"] = Silent("撤销；无历史即 no-op"),
        ["MenuZoomIn"] = Silent("画布放大一档"),
        ["MenuZoomOut"] = Silent("画布缩小一档"),
        ["MenuZoomReset"] = Silent("画布缩放复位为 100%"),

        // ── 跳过桶（14）—— 必须写明碰到了什么才不点 ──
        ["MenuBuildRun"] = Skipped("构建并启动模拟器：起外部构建进程"),
        ["MenuCompile"] = Skipped("编译工程：起外部构建进程"),
        ["MenuDownload"] = Skipped("烧录下载到 HMI：走 ADB / 外部进程"),
        ["MenuExit"] = Skipped("关闭主窗口，后续菜单将无法继续点击"),
        ["MenuExportCode"] = Skipped("导出生成代码：写工程目录"),
        ["MenuGitCommit"] = Skipped("git 提交对话框：起 git 进程"),
        ["MenuGitHistory"] = Skipped("git 历史：起 git 进程"),
        ["MenuGitInit"] = Skipped("git init：起 git 进程且改工作树"),
        ["MenuGitRefresh"] = Skipped("刷新 git 状态：起 git 进程"),
        ["MenuNewProject"] = Skipped("新建工程：可能弹未保存确认并重置工程"),
        ["MenuOpenProject"] = Skipped("打开工程：走 StorageProvider 文件选择器"),
        ["MenuSaveProject"] = Skipped("保存工程：写工程文件到磁盘，可能弹未保存确认"),
        ["MenuSimulatorExport"] = Skipped("导出模拟器工程：写整套工程文件到磁盘"),
        ["MenuValidateCode"] = Skipped("校验生成代码：写临时产物并可能起编译器"),
    };

    /// <summary>窗口上具名菜单项的实际名字（去重）。</summary>
    private static HashSet<string> MenuNamesOnTheWindow(Window window) =>
        window.GetLogicalDescendants().OfType<MenuItem>()
            .Where(m => m.Name is not null)
            .Select(m => m.Name!)
            .ToHashSet(StringComparer.Ordinal);

    private static MainWindow ShowMainWindow(DataRootSandbox sandbox)
    {
        var settings = new SettingsService(Path.Combine(sandbox.DataRoot, "settings.json"));
        var window = new MainWindow(settings);
        window.Show();
        UiTestHost.Pump();
        return window;
    }

    /// <summary>
    /// 面守卫：窗口上每个具名菜单项都必须被分到恰好一个桶里，反之桶里每个名字都必须真的在窗口上。
    ///
    /// <para>这是「并集覆盖守卫」的形状（同 <c>WindowViewSmokeUiTests.EveryViewType_IsInSomeSmokeSurface</c>）：
    /// 增量方向防"新加菜单项没分类"，反向防"菜单项被删/改名后分类表腐烂"。
    /// 没有它，<see cref="EveryClassifiedMenuItem_BehavesAsDeclared"/> 会在有人删掉一批接线后依然全绿。</para>
    /// </summary>
    [Fact]
    public void EveryNamedMenuItem_IsClassified()
    {
        using var sandbox = new DataRootSandbox("menucatalog");
        var actual = UiTestHost.Run(() =>
        {
            var window = ShowMainWindow(sandbox);
            var names = MenuNamesOnTheWindow(window);
            window.Close();
            return names;
        });

        var declared = Buckets.Keys.ToHashSet(StringComparer.Ordinal);
        var missing = actual.Except(declared).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var stale = declared.Except(actual).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            "以下菜单项在窗口上存在，但分类表里没有它们 —— 请判断它属于哪个桶：\n  "
            + string.Join("\n  ", missing));
        Assert.True(stale.Count == 0,
            "分类表里登记了这些名字，但窗口上找不到 —— 菜单项被删或改名了：\n  "
            + string.Join("\n  ", stale));
        Assert.True(actual.Count >= 60, $"人口下界不成立：具名菜单项只有 {actual.Count} 个（预期 ≥ 60）");
    }

    /// <summary>
    /// 空集守卫 + 表自身卫生：桶不能被掏空，跳过必须给得出理由，期望类型必须真的是窗口类型。
    /// 没有这几条，"把表删空"就能让行为判据永远为真。
    /// </summary>
    [Fact]
    public void MenuBucketTable_IsNotEmptiedAndIsSelfConsistent()
    {
        Assert.True(Buckets.Count >= 60, $"分类表只有 {Buckets.Count} 行（预期 ≥ 60）");

        var popups = Buckets.Where(kv => kv.Value.Bucket == MenuBucket.Popup).ToList();
        var skips = Buckets.Where(kv => kv.Value.Bucket == MenuBucket.Skipped).ToList();
        Assert.True(popups.Count >= 18, $"弹窗桶只有 {popups.Count} 项（预期 ≥ 18）");
        Assert.True(skips.Count >= 12, $"跳过桶只有 {skips.Count} 项（预期 ≥ 12）");

        foreach (var (name, spec) in Buckets)
        {
            // 弹窗桶/静默桶的行为已经被行为判据精确断言，理由只是注释；
            // 只有「不点」和「容忍两种结果」这两桶，理由才是唯一的凭据 —— 对它们严格。
            if (spec.Bucket is MenuBucket.Skipped or MenuBucket.Conditional)
                Assert.True(spec.Why.Trim().Length >= 10,
                    $"{name} 属于「{spec.Bucket}」桶，理由必须说清为什么可以不验行为（或为什么允许两种结果），当前是：\"{spec.Why}\"");
            if (spec.Expected is not null)
                Assert.True(typeof(Window).IsAssignableFrom(spec.Expected),
                    $"{name} 的期望类型 {spec.Expected.Name} 不是 Window 派生类型");
        }
    }

    /// <summary>
    /// 真点每一个已分类的菜单项，按桶断言行为。
    ///
    /// <para>一条用例走完全部（而不是每个菜单一条 Theory）：建主窗口是这一步里最贵的动作，
    /// 逐条建会把这个文件的开销抬到几十秒。失败信息逐项汇总，仍能点名到具体菜单项。</para>
    /// </summary>
    [Fact]
    public void EveryClassifiedMenuItem_BehavesAsDeclared()
    {
        using var sandbox = new DataRootSandbox("menuclick");
        var errors = new List<string>();
        var readings = new List<string>();
        var clicked = 0;
        var skipped = Buckets.Count(kv => kv.Value.Bucket == MenuBucket.Skipped);

        UiTestHost.Run(() =>
        {
            // 语言与主题是进程级单例：这个用例会点它们，收尾必须还原，否则污染同程序集的其它 UI 测试。
            var localization = AppLocalization.Service;
            var language0 = localization.CurrentLanguage;
            var theme0 = App.ThemeService.Current;

            try
            {
                var window = ShowMainWindow(sandbox);
                var byName = window.GetLogicalDescendants().OfType<MenuItem>()
                    .Where(m => m.Name is not null)
                    .GroupBy(m => m.Name!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                // 读数（登记表 #42）：本文件此前**连红的时候都没有读数** ——
                // 面解析出几个、真点了几个、按桶各几个，一个都没落下来。
                readings.Add($"    面：菜单树上具名 MenuItem {byName.Count} 个；表内 {Buckets.Count} 个"
                             + $"（计划点 {Buckets.Count - skipped} / 不点 {skipped}）");
                readings.Add("    桶分布：" + string.Join(" / ", Buckets
                    .GroupBy(kv => kv.Value.Bucket)
                    .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
                    .Select(g => $"{g.Key} {g.Count()}")));

                // 名字排序 ⇒ 点击顺序确定，失败可复现。
                foreach (var (name, spec) in Buckets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (spec.Bucket == MenuBucket.Skipped) continue;

                    if (!byName.TryGetValue(name, out var item))
                    {
                        errors.Add($"{name}：在窗口的菜单树上找不到（面守卫应该已经先报过）");
                        continue;
                    }

                    clicked++;
                    var before = window.OwnedWindows.Count;
                    try
                    {
                        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        UiTestHost.Pump(5);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{name}：点击抛出 {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    var owned = window.OwnedWindows.ToArray();
                    var opened = owned.Skip(before).ToArray();
                    var delta = owned.Length - before;

                    if (delta > 1)
                        errors.Add($"{name}：一次点击弹出了 {delta} 个窗口（{string.Join(", ", opened.Select(w => w.GetType().Name))}）");

                    switch (spec.Bucket)
                    {
                        case MenuBucket.Popup when delta != 1:
                            errors.Add($"{name}：预期弹出 1 个 {spec.Expected!.Name}，实际增量 {delta}");
                            break;
                        case MenuBucket.Silent when delta != 0:
                            errors.Add($"{name}：预期不弹窗，实际弹出了 {string.Join(", ", opened.Select(w => w.GetType().Name))}");
                            break;
                    }

                    foreach (var w in opened)
                    {
                        if (spec.Expected is not null && w.GetType() != spec.Expected)
                            errors.Add($"{name}：弹出的窗口类型是 {w.GetType().Name}，预期 {spec.Expected.Name}"
                                       + "（这一条同时说明 Click 可能接到了别的处理器上）");
                        if (!w.IsVisible)
                            errors.Add($"{name}：弹出的 {w.GetType().Name} 不可见");
                    }

                    foreach (var w in opened)
                    {
                        try { w.Close(); }
                        catch (Exception ex) { errors.Add($"{name}：关闭弹出的 {w.GetType().Name} 时抛出 {ex.GetType().Name}"); }
                    }
                    UiTestHost.Pump();

                    if (window.OwnedWindows.Count != before)
                        errors.Add($"{name}：关闭对话框后 OwnedWindows 没有回到 {before}（实际 {window.OwnedWindows.Count}）");

                    if (!window.IsVisible)
                    {
                        errors.Add($"{name}：点击后主窗口被关掉了，后续菜单无法继续验证");
                        break;
                    }
                }

                if (window.IsVisible) window.Close();
            }
            finally
            {
                localization.SwitchLanguage(language0);
                App.ThemeService.SetTheme(theme0);
            }
        });

        readings.Add($"    实测：真点 {clicked} 个；红项 {errors.Count} 个");
        JudgeReadings.Write(_out, "主窗口菜单项（点击行为面）", readings);

        Assert.True(errors.Count == 0, "菜单点击行为不符预期：\n" + string.Join("\n", errors));
    }
}
