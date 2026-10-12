using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 宿主对话框**内部按钮**的行为判据：点一下到底会发生什么。
///
/// <para><b>为什么有这一层：</b>阶段 49 把主窗口的 62 个菜单项点了一遍，但那只覆盖到
/// 「打开一个对话框」；对话框**打开之后**里面几十个按钮的接线仍然没人点过 ——
/// 而 <c>DeviceManagerView</c> 的教训（构造期 NRE ⇒ 菜单点了没反应）说明，
/// 「接线在源码里看着存在」与「点下去真的会发生什么」是两件事。
/// 这一层把「宿主 → 宿主内部按钮 → 二级弹窗」也变成可判。</para>
///
/// <para><b>判据形状：</b>从主窗口菜单打开宿主（**生产构造路径**），按 <see cref="Hosts"/> 里
/// **声明的顺序**枚举宿主内的具名 <see cref="Button"/>，逐个 <c>RaiseEvent(Button.ClickEvent)</c>，
/// 然后看**宿主自己**的 <see cref="Window.OwnedWindows"/> —— 二级弹窗的 owner 就是宿主
/// （<c>dialog.ShowDialog(this)</c> 里的 <c>this</c> 是宿主视图；走
/// <c>AppDialogs.*Async(this, …)</c> 的也一样，因为 <c>TopLevel.GetTopLevel(this)</c> 解析出宿主）。
/// 这与阶段 49 用主窗口 <c>OwnedWindows</c> 是同一手法，只是 owner 下沉了一层。</para>
///
/// <para><b>点击顺序是显式声明的量，不是字母序。</b>这是一个实测出来的必要条件：
/// <c>AnimationEditorView.OnDeleteAnimation</c> 首行是 <c>if (_currentAnimation == null) return;</c>，
/// 所以「删除动画」是否弹确认框**取决于之前点过什么**。表里把 <c>BtnNewAnimation</c> 排在
/// <c>BtnDeleteAnimation</c> 之前，就把后者的初始状态钉成「有动画可删」⇒ 可以断言「必须弹且类型正确」，
/// 而不是退化成「弹或不弹都行」。**改动数组顺序 = 改动被测语义**，别当排版调整。</para>
///
/// <para><b>两个必须知道的事实（都实测过）：</b>
/// ① <b>按钮接线有两种方式</b> —— XAML 的 <c>Click="OnXxxClicked"</c> 属性绑定与
/// code-behind 的 <c>.Click +=</c>。`DeviceManagerView` 全部走前者（它的 <c>.cs</c> 里
/// **一个 <c>.Click +=</c> 都没有**），所以任何「按 <c>.Click +=</c> 统计接线」的静态守卫
/// 会漏掉整整一半。<c>RaiseEvent</c> 对两种方式都生效，这是本层不受该差异影响的原因。
/// ② <c>GetLogicalDescendants</c> <b>会重复枚举同一实例</b>（`UserManagerView` 里 5 个按钮
/// 各出现两次，阶段 49 的 62 个菜单项里也有 3 个重名）⇒ 面要按名字去重，
/// 并断言"同名就是同一实例"（否则 XAML 里真出现两个同名控件会被悄悄抹平）。</para>
///
/// <para><b>面为什么用双向差集：</b>表里写的名字集合与运行期枚举出的集合必须**相等** ——
/// 少一个（控件被删/改名）与多一个（新增按钮没人分类）都会红。于是新增按钮会被点名，
/// 强制有人来判断它「必须弹 / 静默 / 不点」，与阶段 49 菜单表的守卫同形。</para>
///
/// <para><b>不抓什么：</b>① 「不弹窗」是静默的 —— 一个没接线的按钮与一个接了线但
/// 什么都不做的按钮，在这一层长得一样（接线本身要靠负控证明，见专题）；② 动作型按钮
/// （烧录 / 生成 / 起进程）的**副作用**不判，只判"不抛异常 + 弹窗层数"；
/// ③ 需要先选中某项、而**本判据造不出那个状态**的二级弹窗（`BtnResetPassword` 之类）
/// 当前是 no-op ⇒ 只被"增量恰为 0"覆盖到；④ 真实命中测试（不做像素点击）。</para>
/// </summary>
[Collection("Language-global")]
public class DialogInnerInvocationUiTests
{
    private readonly ITestOutputHelper _out;

    public DialogInnerInvocationUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>按钮点下去之后**应该**是什么行为。</summary>
    private enum BtnBucket
    {
        /// <summary>必须弹出恰好 1 个二级窗：增量必须为 1，且类型名相符。</summary>
        Popup,

        /// <summary>
        /// 不弹二级窗：增量必须为 0。这是针对**本判据的初始状态 + 本表的点击顺序**的精确断言，
        /// 不是"它永远不弹"。状态一变就会红，届时有人来看是不是该改成别的桶。
        /// </summary>
        Silent,

        /// <summary>不点：理由必填。</summary>
        Never,
    }

    private sealed record BtnSpec(string Name, BtnBucket Bucket, string? Window, string Why);

    private sealed record HostSpec(string Menu, Type Host, BtnSpec[] Buttons);

    /// <summary>必须弹出恰好 1 个二级窗。</summary>
    private static BtnSpec Pop(string name, string window, string why) => new(name, BtnBucket.Popup, window, why);

    /// <summary>本判据的初始状态下不弹窗（增量必须为 0）。</summary>
    private static BtnSpec Quiet(string name, string why) => new(name, BtnBucket.Silent, null, why);

    /// <summary>不点（理由必填，见 <see cref="BucketTables_AreNotEmptiedAndAreSelfConsistent"/>）。</summary>
    private static BtnSpec Skip(string name, string why) => new(name, BtnBucket.Never, null, why);

    /// <summary>
    /// 宿主清单。**<see cref="HostSpec.Buttons"/> 的数组顺序就是点击顺序**（见类注释）。
    /// 每个宿主都从主窗口菜单以生产路径打开（<see cref="HostSpec.Menu"/>）。
    /// </summary>
    private static readonly HostSpec[] Hosts =
    {
        new("MenuRecipeEditor", typeof(RecipeEditorView), new[]
        {
            Pop("BtnAddGroup", "TextInputDialog", "新增配方分组：弹输入框要名字"),
            Quiet("BtnDeleteGroup", "初始 groups.Count == 0 ⇒ 这个按钮被 IsEnabled=false 关掉，点击只走空处理器"),
            Quiet("BtnNewRecipe", "初始无选中分组 ⇒ 没有可落的组，只写状态栏提示"),
            Quiet("BtnCloneRecipe", "初始无选中配方 ⇒ 提前 return"),
            Quiet("BtnDeleteRecipe", "初始无选中配方 ⇒ 提前 return"),
            Quiet("BtnImportCsv", "走 StorageProvider.OpenFilePickerAsync；headless 的文件选择器桩返回空集合 ⇒ files.Count == 0 提前 return（不抛、不弹）"),
            Quiet("BtnExportCsv", "同上：SaveFilePickerAsync 返回 null ⇒ 提前 return"),
            Quiet("BtnAddParam", "初始无选中配方 ⇒ 提前 return"),
            Quiet("BtnDeleteParam", "初始无选中参数 ⇒ 提前 return"),
            Quiet("BtnMoveUp", "初始无选中参数 ⇒ 提前 return"),
            Quiet("BtnMoveDown", "初始无选中参数 ⇒ 提前 return"),
            Quiet("BtnSaveRecipe", "OnSaveRecipe 首行 if (_selectedRecipe == null) ⇒ 只写状态栏提示，不写盘"),
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身，后续按钮就无从验证了"),
        }),

        new("MenuUserManager", typeof(UserManagerView), new[]
        {
            Quiet("BtnDeleteUser", "初始未选中用户 ⇒ 没有删除对象（选中后会弹确认框，本判据造不出该状态）"),
            Quiet("BtnToggleEnabled", "初始未选中用户 ⇒ 提前 return"),
            Quiet("BtnResetPassword", "初始未选中用户 ⇒ 提前 return（选中后会弹新口令输入框）"),
            Quiet("BtnUnlock", "初始未选中用户 ⇒ 提前 return"),
            Quiet("BtnAddUser", "新增用户是行内草稿，不弹窗"),
            Quiet("BtnSaveRole", "把角色权限写进数据根（沙箱内被隔离），不弹窗"),
            Quiet("BtnRefreshLog", "重读审计日志，不弹窗"),
            Quiet("BtnClearLog", "清空审计日志视图，不弹窗"),
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身，后续按钮就无从验证了"),
        }),

        // 这个宿主只有 1 个具名控件，且它是「关窗」⇒ 整张面都在跳过表里。
        // 它进了 NoClickableHosts（理由在那边），这一层对它只能核对「面」。
        new("MenuStyleEditor", typeof(StyleEditorView), new[]
        {
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身；这个宿主也只有这一个具名控件"),
        }),

        new("MenuScriptEditor", typeof(ScriptEditorView), new[]
        {
            Quiet("BtnNewScript", "新建脚本是列表里的草稿行，不弹窗"),
            Quiet("BtnDeleteScript", "初始未选中脚本 ⇒ 提前 return"),
            Quiet("BtnTemplateOnClick", "往编辑器插入模板文本，不弹窗"),
            Quiet("BtnTemplateOnValueChanged", "同上：插入模板文本"),
            Quiet("BtnTemplateTimer", "同上：插入模板文本"),
            Quiet("BtnTemplateAlarm", "同上：插入模板文本"),
            Pop("BtnAiGenerate", "AiScriptDialog", "AI 生成脚本：弹生成对话框"),
            Quiet("BtnValidate", "校验当前脚本并把结果写进输出面板，不弹窗"),
            Quiet("BtnInsertTemplate", "与模板按钮同路：插入文本"),
            Quiet("BtnDebugRun", "初始无选中脚本 ⇒ 提前 return（有脚本时会在 UI 线程同步跑 Lua，带 2000 ms 超时）"),
            Skip("BtnHelp",
                "点它会 ShowDialog 一个「Lua 帮助」窗，而 Avalonia.Headless 下布局不收敛、testhost 挂死（EXIT=124，是 hang 不是 OOM）。"
                + "**触发条件已定论（阶段 73）**：「垂直可滚动的 ScrollViewer」包着「TextWrapping.Wrap 的多行 TextBlock」，在首次布局里定不下来。"
                + "三条否证把范围钉死了：与**内容是否溢出视口**无关（窗口高 900、视口 828 远超内容 340，照样挂）；"
                + "与**滚动条是否可见**无关（VerticalScrollBarVisibility=Hidden 仍挂）；与**水平侧**无关"
                + "（HorizontalScrollBarVisibility=Disabled 仍挂 —— 这一条推翻了本表原来猜的「水平无限宽测量」）。"
                + "**真平台（X11/Skia）下不挂**（Auto/Disabled 各 2 轮全 EXIT=0）⇒ 是 headless 的限制，不是产品缺陷，产品代码不为此改渲染方式"
                + "（实测 Wrap→NoWrap 会让 en-US 文案最长行 665px 超出 448px 可用宽 ⇒ 用户要横向滚动）。"
                + "帮助窗的**内容与接线**（标题/正文/关闭按钮文案/**点关闭按钮真的调 Close()**）已改由"
                + "LuaHelpDialogUiTests 覆盖 —— 它走 CreateLuaHelpDialog()、只构造不 Show。"
                + "（阶段 75 更正：阶段 73 曾判「关闭接线要真显示窗才观察得到」，**不成立** ——"
                + "从未 Show 的 Window.Close() 不抛且触发 Closed，未 attach 的按钮上 RaiseEvent(Button.ClickEvent)"
                + " 会触发直接挂上的 handler ⇒ 接线可判。本桶仍不放它，因为点 BtnHelp 走的 ShowDialog 才是挂死那一步。）"
                + "取证与两侧对照：docs/2026-09-22-headless-scrollviewer-wrap-hang.md"),
            Quiet("BtnClearOutput", "清空输出面板，不弹窗"),
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身，后续按钮就无从验证了"),
        }),

        new("MenuDeviceManager", typeof(DeviceManagerView), new[]
        {
            Quiet("BtnRefreshPorts", "重新枚举串口，不弹窗"),
            Quiet("BtnOpenClose", "无设备可连时只更新按钮文案与状态栏，不弹窗"),
            Quiet("BtnDetect", "探测无果时只写状态栏，不弹窗"),
            Quiet("BtnCheckUpdate", "无连接时提前 return，不弹窗"),
            Quiet("BtnBrowseHmiSwu", "走 StorageProvider 的文件选择器（headless 桩返回空）⇒ 提前 return，不弹窗"),
            Skip("BtnUpdateHmi", "升级 HMI 运行时：会向设备推送文件，是不可逆的外部动作"),
            Quiet("BtnBrowseFwSwu", "同上：StorageProvider 桩返回空 ⇒ 提前 return"),
            Skip("BtnFlashFw", "烧录固件：不可逆的外部动作，不做"),
            Quiet("BtnBrowseHmiLib", "同上：StorageProvider 桩返回空 ⇒ 提前 return"),
            Quiet("BtnBrowseOutput", "同上：StorageProvider 桩返回空 ⇒ 提前 return"),
            Skip("BtnGenerate", "生成整套代码到输出目录 = 写盘（输出目录还能被 BtnBrowseOutput 改掉）⇒ 本层只覆盖弹窗接线，不点写盘按钮"),
            Quiet("BtnBrowseFelTool", "同上：StorageProvider 桩返回空 ⇒ 提前 return"),
            Quiet("BtnBrowseFelImage", "同上：StorageProvider 桩返回空 ⇒ 提前 return"),
            Skip("BtnFelFlash", "FEL 烧录：不可逆的外部动作，不做"),
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身，后续按钮就无从验证了"),
        }),

        new("MenuAnimationEditor", typeof(AnimationEditorView), new[]
        {
            Quiet("BtnNewAnimation", "新建动画并选中它（不弹窗）—— 它排在 BtnDeleteAnimation 之前，把后者的初始状态钉成「有动画可删」"),
            Pop("BtnDeleteAnimation", "ConfirmDeleteDialog",
                "删除当前动画：弹确认框。**依赖点击顺序** —— OnDeleteAnimation 首行是 if (_currentAnimation == null) return;，所以必须先点 BtnNewAnimation"),
            Quiet("BtnPlayPreview", "切换预览播放，不弹窗"),
            Quiet("BtnAddKeyframe", "往当前动画加关键帧，不弹窗"),
            Quiet("BtnDeleteKeyframe", "初始未选中关键帧 ⇒ 提前 return"),
            Skip("BtnTitleClose", "点了会关掉宿主对话框本身，后续按钮就无从验证了"),
        }),
    };

    /// <summary>
    /// 整张面都在跳过表里、这一层点不到任何按钮的宿主：宿主类型名 → 理由（必填 ≥10 字符）。
    /// 没有它，「宿主里一个可点按钮都没有」会被当成失败；有了它，这种情况必须**写明理由**才放行。
    /// </summary>
    private static readonly Dictionary<string, string> NoClickableHosts = new(StringComparer.Ordinal)
    {
        ["StyleEditorView"] =
            "唯一具名控件是 BtnTitleClose（关宿主）；这个视图的内容全部在 code-behind 里构建、没有任何 x:Name "
            + "⇒ 这一层对它只能核对「面」（按钮清单恰好是这 1 个），做不了行为判据",
    };

    public static IEnumerable<object[]> HostData =>
        Hosts.Select(h => new object[] { h.Menu, h.Host.Name });

    private static MainWindow ShowMainWindow(DataRootSandbox sandbox)
    {
        var settings = new SettingsService(System.IO.Path.Combine(sandbox.DataRoot, "settings.json"));
        var window = new MainWindow(settings);
        window.Show();
        UiTestHost.Pump();
        return window;
    }

    /// <summary>
    /// 面：把运行期枚举出的具名按钮与表里登记的名字做**双向差集**，并按名字去重。
    /// 没有它，「宿主打不开 / 按钮被删光」会让循环空转、判据恒真。
    /// </summary>
    private static List<Button> CollectButtons(Window host, HostSpec spec, List<string> errors)
    {
        var all = host.GetLogicalDescendants().OfType<Button>()
            .Where(b => b.GetType() == typeof(Button) && b.Name is not null)
            .ToList();

        foreach (var group in all.GroupBy(b => b.Name!, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Any(b => !ReferenceEquals(b, first)))
                errors.Add($"{spec.Host.Name}：控件名 {group.Key} 对应 {group.Count()} 个**不同实例** —— XAML 里出现了同名控件");
        }

        var found = all.GroupBy(b => b.Name!, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(b => b.Name, StringComparer.Ordinal)
            .ToList();

        var declared = spec.Buttons.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        var actual = found.Select(b => b.Name!).ToHashSet(StringComparer.Ordinal);

        var missing = declared.Except(actual, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            errors.Add($"{spec.Host.Name}：表里登记了但宿主里找不到这些按钮：{string.Join(", ", missing)}"
                       + " —— 控件被删/改名了，或宿主没打开到带按钮的那一屏");

        var extra = actual.Except(declared, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (extra.Length > 0)
            errors.Add($"{spec.Host.Name}：宿主里有这些具名按钮没被分类：{string.Join(", ", extra)}"
                       + " —— 请判断它该进 Popup（必须弹）还是 Quiet（本初始状态下不弹），或写清为什么不点（Skip）");

        return found;
    }

    [Theory]
    [MemberData(nameof(HostData))]
    public void HostInnerButtons_BehaveAsDeclared(string menuName, string hostTypeName)
    {
        var spec = Hosts.Single(h => h.Menu == menuName);
        using var sandbox = new DataRootSandbox("dialoginner");
        var errors = new List<string>();
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            var window = ShowMainWindow(sandbox);
            try
            {
                var menu = window.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Name == menuName);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                UiTestHost.Pump(5);

                var opened = window.OwnedWindows.Where(w => w.GetType() == spec.Host).ToArray();
                if (opened.Length != 1)
                {
                    errors.Add($"{menuName} 没有恰好打开一个 {hostTypeName}（实际 {opened.Length}）");
                    return;
                }

                var host = opened[0];
                var found = CollectButtons(host, spec, errors);
                var foundByName = found.ToDictionary(b => b.Name!, b => b, StringComparer.Ordinal);
                var clicked = 0;

                // 按表里声明的顺序点（顺序是被测语义的一部分，见类注释）
                foreach (var button in spec.Buttons)
                {
                    if (!foundByName.TryGetValue(button.Name, out var control))
                        continue; // 面已经报过 missing

                    if (button.Bucket == BtnBucket.Never)
                    {
                        readings.Add($"    {button.Name}（跳过）");
                        continue;
                    }

                    var before = host.OwnedWindows.Count;
                    try
                    {
                        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        UiTestHost.Pump(5);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{hostTypeName}.{button.Name}：点击抛出 {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    clicked++;
                    var inner = host.OwnedWindows.Skip(before).ToArray();
                    readings.Add($"    {button.Name} → +{inner.Length}"
                                 + (inner.Length > 0
                                     ? "：" + string.Join(",", inner.Select(w => w.GetType().Name))
                                     : string.Empty));

                    if (inner.Length > 1)
                        errors.Add($"{hostTypeName}.{button.Name}：一次点击弹出了 {inner.Length} 个二级窗"
                                   + $"（{string.Join(",", inner.Select(w => w.GetType().Name))}）");

                    if (button.Bucket == BtnBucket.Popup)
                    {
                        if (inner.Length != 1)
                            errors.Add($"{hostTypeName}.{button.Name}：预期弹出 1 个 {button.Window}，实际增量 {inner.Length}");
                        else if (!string.Equals(inner[0].GetType().Name, button.Window, StringComparison.Ordinal))
                            errors.Add($"{hostTypeName}.{button.Name}：弹出的二级窗类型是 {inner[0].GetType().Name}，"
                                       + $"预期 {button.Window}（这一条同时说明 Click 可能接到了别的处理器上）");
                    }
                    else if (inner.Length != 0)
                    {
                        errors.Add($"{hostTypeName}.{button.Name}：预期本初始状态下不弹窗，实际弹出了 "
                                   + string.Join(",", inner.Select(w => w.GetType().Name))
                                   + " —— 请判断它该进 Popup（必须弹）还是写清为什么是条件性的");
                    }

                    foreach (var w in inner)
                    {
                        if (!w.IsVisible) errors.Add($"{hostTypeName}.{button.Name}：弹出的 {w.GetType().Name} 不可见");
                        try { w.Close(); }
                        catch (Exception ex) { errors.Add($"{hostTypeName}.{button.Name}：关闭 {w.GetType().Name} 时抛出 {ex.GetType().Name}"); }
                    }
                    UiTestHost.Pump();

                    if (host.OwnedWindows.Count != before)
                        errors.Add($"{hostTypeName}.{button.Name}：关闭二级窗后宿主的 OwnedWindows 没有回到 {before}");
                    if (!host.IsVisible)
                    {
                        errors.Add($"{hostTypeName}.{button.Name}：点击后宿主对话框被关掉了，后续按钮无从验证");
                        break;
                    }
                }

                if (clicked < 1)
                {
                    if (!NoClickableHosts.TryGetValue(hostTypeName, out var why) || why.Trim().Length < 10)
                        errors.Add($"{hostTypeName}：一个按钮都没点（全在跳过表里？）—— 判据恒真。"
                                   + "要么把宿主移出 Hosts，要么在 NoClickableHosts 里写清理由");
                    else
                        readings.Add($"    （整张面都在跳过表里，理由已登记：{why.Length} 字符）");
                }
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

        // 读数出口（登记表 #42）：同一份 readings 除了拼进失败消息，也走 ITestOutputHelper
        // 与 HMI_READINGS_OUT 文件 —— 见 JudgeReadings 的说明。
        JudgeReadings.Write(_out, $"{hostTypeName} 内部按钮（宿主对话框内按名解析面）", readings);

        Assert.True(errors.Count == 0,
            $"{hostTypeName} 内部按钮行为不符预期：\n" + string.Join("\n", errors)
            + "\n\n读数：\n" + string.Join("\n", readings));
    }

    /// <summary>
    /// 桶表的卫生：面非空、名字不重复、桶与字段自洽、理由写得出。
    /// 没有它，表可以腐烂成「全 Skip + 空理由」而判据仍然全绿。
    /// </summary>
    [Fact]
    public void BucketTables_AreNotEmptiedAndAreSelfConsistent()
    {
        Assert.True(Hosts.Length >= 3, $"宿主表只剩 {Hosts.Length} 个宿主 —— 面被掏空了");

        var popupCount = 0;
        var clickableCount = 0;
        var seenMenus = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spec in Hosts)
        {
            Assert.True(seenMenus.Add(spec.Menu), $"宿主表里 {spec.Menu} 出现两次 —— 菜单名必须唯一");

            var buttons = spec.Buttons;
            Assert.True(buttons.Length >= 1, $"{spec.Host.Name} 一个按钮都没登记 —— 面被掏空了");

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in buttons)
            {
                Assert.True(seenNames.Add(b.Name), $"{spec.Host.Name} 里 {b.Name} 登记了两次");

                switch (b.Bucket)
                {
                    case BtnBucket.Popup:
                        Assert.False(string.IsNullOrWhiteSpace(b.Window),
                            $"{spec.Host.Name}.{b.Name} 是 Popup 桶却没有期望窗口类型名");
                        Assert.True(b.Why.Trim().Length >= 1, $"{spec.Host.Name}.{b.Name} 的理由是空的");
                        popupCount++;
                        clickableCount++;
                        break;

                    case BtnBucket.Silent:
                        Assert.True(b.Window is null,
                            $"{spec.Host.Name}.{b.Name} 是 Silent 桶却写了期望窗口名 {b.Window} —— 两种桶的字段混了");
                        Assert.True(b.Why.Trim().Length >= 1, $"{spec.Host.Name}.{b.Name} 的理由是空的");
                        clickableCount++;
                        break;

                    case BtnBucket.Never:
                        Assert.True(b.Window is null,
                            $"{spec.Host.Name}.{b.Name} 是 Never 桶却写了期望窗口名 {b.Window}");
                        Assert.True(b.Why.Trim().Length >= 10,
                            $"{spec.Host.Name}.{b.Name} 属于 Never 桶，理由太短、说明不了什么：\"{b.Why}\"");
                        break;

                    default:
                        Assert.Fail($"未处理的桶：{b.Bucket}");
                        break;
                }
            }
        }

        Assert.True(popupCount >= 1, "没有任何 Popup 桶 —— 这一层就不覆盖二级弹窗了");
        Assert.True(clickableCount >= 1, "没有一个按钮会被真的点到 —— 判据恒真");

        // NoClickableHosts 只允许登记「该宿主的所有按钮都是 Never」的宿主
        foreach (var (hostName, why) in NoClickableHosts)
        {
            var spec = Hosts.SingleOrDefault(h => h.Host.Name == hostName);
            Assert.True(spec is not null, $"NoClickableHosts 里的 {hostName} 不在宿主表里 —— 名字写错了或宿主已被删");
            Assert.True(why.Trim().Length >= 10, $"{hostName} 的「没有可点按钮」理由太短：\"{why}\"");
            Assert.True(spec!.Buttons.All(b => b.Bucket == BtnBucket.Never),
                $"{hostName} 被登记为「没有可点按钮」，但表里还有会被点到的按钮 —— 两处不一致");
        }

        // Never 桶里的名字必须真的存在于某个 .axaml（防表腐烂成"名字打错也能过"）
        var sourceDir = FindAppSourceDir();
        var axaml = System.IO.Directory.EnumerateFiles(sourceDir, "*.axaml", System.IO.SearchOption.AllDirectories)
            .Select(System.IO.File.ReadAllText)
            .ToArray();
        foreach (var name in Hosts.SelectMany(h => h.Buttons)
                     .Where(b => b.Bucket == BtnBucket.Never)
                     .Select(b => b.Name)
                     .Distinct(StringComparer.Ordinal))
        {
            Assert.True(axaml.Any(t => t.Contains($"x:Name=\"{name}\"", StringComparison.Ordinal)),
                $"Never 桶里的 {name} 在任何 .axaml 里都找不到 —— 名字写错了或控件已被删");
        }
    }

    /// <summary>从测试输出目录向上找 <c>src/HmiEditor.App</c>。</summary>
    private static string FindAppSourceDir()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "src", "HmiEditor.App");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("找不到 src/HmiEditor.App —— 测试必须从仓库树内的输出目录运行");
    }
}
