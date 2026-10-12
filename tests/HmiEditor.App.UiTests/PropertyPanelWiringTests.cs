using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using FluentAvaloniaUI = FluentAvalonia.UI.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 属性面板**接线完整性**的行为测试 —— 取代原先读
/// <c>PropertyPanelView.axaml</c> / <c>PropertyPanelView.axaml.cs</c> 源码做字符串匹配的两个守卫
/// （<c>EveryNumberBox_HasValueChangedSubscription_InCodeBehind</c>、
/// <c>EveryComboAndCheckBox_HasSelectionOrCheckedEvent</c>）。
///
/// <para><b>旧守卫的问题：</b>它断言「XAML 里每个 <c>x:Name</c> 都能在代码后置里找到
/// <c>名字.ValueChanged +=</c> 这段字」。这是静态文本匹配 —— 换个写法、抽成循环、
/// 重命名变量，行为没变也会碎；而且它只能证明「有这行字」，<b>证明不了真的写回了模型</b>。</para>
///
/// <para><b>现在的做法：</b>把真实面板建起来，从**逻辑树**枚举全部编辑器，逐个改值，
/// 然后断言绑定的模型（<see cref="HmiControl"/> 或 <see cref="HmiScreen"/>）真的变了。
/// 不读任何源码文本，重构不会误伤。</para>
///
/// <para><b>为什么用逻辑树而不是可视树：</b>Avalonia 里 <c>IsVisible=false</c> 的元素
/// 不在可视树中 —— 实测可视树只有 12 个编辑器，而逻辑树有全部 <b>91</b> 个
/// （31 个 FANumberBox + 30 个 ComboBox + 9 个 CheckBox + 21 个 TextBox，与 XAML 声明数一致）。
/// 走可视树会漏掉 58 个，守卫就废了。</para>
///
/// <para><b>为什么要跑多个场景：</b>编辑器按控件类型/功能区显隐，单个场景驱动不到全部。
/// 因此对每种控件类型各跑一遍，加上「屏幕」场景，取并集。
/// 若有人新增了编辑器却没有任何场景能驱动它，覆盖断言会失败 —— 这就保住了
/// 旧守卫「穷尽列举」的价值，而且不需要解析源码。</para>
/// </summary>
public class PropertyPanelWiringTests
{
    /// <summary>
    /// 反射快照的递归深度上限。
    /// 模型嵌套最深的一条链是 HmiProject → ScreenDefaultsConfig → ScreenPadding（3 层），
    /// 截太浅会把嵌套字段变成 "&lt;deep&gt;"，导致「改了但检测不到」（曾经踩过：深度 2 时
    /// 安全区左/上/右三个编辑器的写入被吞掉）。
    /// </summary>
    private const int MaxSnapshotDepth = 4;

    /// <summary>
    /// 豁免清单：结构性无法通过「改一个值 → 看模型变化」验证的编辑器，必须带原因。
    /// 任何未在此列、又没被任何场景驱动的编辑器都会让测试失败。
    /// </summary>
    private static readonly Dictionary<string, string> Exemptions = new(StringComparer.Ordinal)
    {
        // 表达式框：出厂 `IsVisible="False"`，要先把 `BindingKindCombo` 拨到「表达式」才显示 ——
        // 它走 code-behind 接线，由 PropertyPanelTextBoxWiringTests（阶段 193）专门驱动，
        // 那一条判据连"显示出来"的前提都自检了。本面（枚举驱动）够不着它，故豁免。
        ["ExpressionInput"] = "出厂不可见（要先拨 BindingKindCombo 到「表达式」）；由 PropertyPanelTextBoxWiringTests 专门驱动",

        // 属性面板**内嵌**的变量选择器（`VariablePicker`）里那两个框也落在逻辑树里 ——
        // 它们不归本面：搜索框由 VariablePickerTextBoxWiringTests 用 code-behind 那条面驱动，
        // 显示框是只读展示（§112 的 DisplayOnly 登记）。本面够不着它们（驱动它们不改控件/屏幕/工程）。
        ["SearchBox"] = "内嵌 VariablePicker 的搜索框：改了只过滤它自己的变量树（不动控件/屏幕/工程），由 VariablePickerTextBoxWiringTests 驱动",
        ["DisplayField"] = "内嵌 VariablePicker 的只读显示框（IsReadOnly）：由 UpdateDisplay() 回填，没人读它（§112 DisplayOnly 登记）",

        // 资源类下拉：选项由资产库/动作库/字体服务异步填充，测试宿主未注入提供者，ItemCount 恒 < 2，选不动。
        ["ImageAssetCombo"] = "选项依赖图片资产库（宿主未注入资产提供者）",
        ["SvgAssetCombo"] = "选项依赖 SVG 资产库（宿主未注入资产提供者）",
        ["CboVsImage"] = "选项依赖视觉状态图片资产（宿主未注入资产提供者）",
        ["CboActionTarget"] = "选项依赖目标屏/控件列表（宿主未注入工程上下文）",
        ["ScreenBgImageCombo"] = "选项依赖背景图资产库",
        ["ScreenPresetCombo"] = "预设分辨率在打开下拉时才填充",
        ["PropFontFamily"] = "选项依赖字体服务异步回填",

        // 以下两个不是「改了就落盘」的编辑器，本身就不该用「改值 → 看模型变更」判定。
        ["CboVisualState"] = "语义是「切换正在编辑哪个视觉状态」（OnVisualStateChanged 先保存旧态再切态载入字段），" +
                             "不直接写模型字段；状态值本身由 Vs* 一列编辑器写入",
        ["ValueDisplayTypeCombo"] = "改它只切换小数位数下拉的可见性（OnValueDisplayTypeChanged），" +
                                    "真正写回 HmiControl.ValueDisplayType 在批量 SaveProperties 里 —— " +
                                    "即单独改这一项不会立即落盘（见 project-status 记录）",

        // P1-1：换的是「正在编辑哪个属性的绑定」这个视角，OnTargetPropertyChanged 只回填界面，
        // 不写任何模型字段 —— 绑定本身由变量选择器/表达式框写入（PropertyName 取当前目标属性）。
        // 负控见 PropertyPanelTargetPropertyTests.SwitchingTargetProperty_DoesNotMutateModel。
        ["TargetPropertyCombo"] = "语义是「切换正在编辑哪个属性的绑定」（OnTargetPropertyChanged 只回填界面），" +
                                  "不直接写模型字段；绑定由变量选择器/表达式框按当前目标属性写入",
    };

    private static readonly HmiControlType[] AllTypes =
    {
        HmiControlType.Screen, HmiControlType.Button, HmiControlType.Label,
        HmiControlType.Slider, HmiControlType.Switch, HmiControlType.CheckBox,
        HmiControlType.Image, HmiControlType.Bar, HmiControlType.Dropdown,
        HmiControlType.TextArea, HmiControlType.Gauge, HmiControlType.Knob,
        HmiControlType.Keyboard, HmiControlType.Led, HmiControlType.Spinner,
        HmiControlType.TabView, HmiControlType.Line, HmiControlType.Table,
        HmiControlType.Svg, HmiControlType.Tank, HmiControlType.Pipe,
        HmiControlType.Valve, HmiControlType.Rectangle, HmiControlType.Ellipse,
        HmiControlType.Polyline, HmiControlType.Arc,
    };

    private readonly ITestOutputHelper _out;

    public PropertyPanelWiringTests(ITestOutputHelper o) => _out = o;

    private static HmiControl MakeControl(HmiControlType type) => new()
    {
        Id = "c1",
        Type = type,
        Name = "n1",
        Text = "标签",
        X = 10, Y = 20, Width = 120, Height = 30,
        MinValue = 0, MaxValue = 100, Value = 50,
        StepValue = 10,
        BgColor = "#0EA5E9",
        IndicatorColor = "#22C55E",
        TextColor = "#111827",
        BorderWidth = 2,
        TankTickCount = 5,
        TankShowScale = true,
    };

    // ── 模型快照 ─────────────────────────────────────────────

    /// <summary>
    /// 模型快照：反射拍下全部公开可读属性，**递归展开嵌套对象**。
    /// 必须递归 —— 否则像屏幕安全区这种写进嵌套对象的编辑器，
    /// 快照只看到类型名，会被误判成「没接线」。
    /// </summary>
    private static string Snapshot(object model) => SnapshotCore(model, 0);

    private static string SnapshotCore(object? model, int depth)
    {
        if (model is null) return "<null>";
        if (IsLeaf(model)) return model.ToString() ?? "<null>";
        if (depth >= MaxSnapshotDepth) return "<deep>";

        var parts = new List<string>();
        foreach (var p in model.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            parts.Add($"{p.Name}={Describe(p, model, depth)}");
        }
        return "{" + string.Join("|", parts) + "}";
    }

    private static string Describe(PropertyInfo p, object model, int depth)
    {
        object? raw;
        try
        {
            raw = p.GetValue(model);
        }
        catch (Exception ex)
        {
            return $"<err:{ex.GetType().Name}>";
        }

        return raw switch
        {
            null => "<null>",
            string s => s,
            System.Collections.ICollection c => $"<col:{c.Count}>",
            _ when raw.GetType().IsPrimitive => raw.ToString() ?? "<null>",
            _ => SnapshotCore(raw, depth + 1),
        };
    }

    private static bool IsLeaf(object v)
    {
        var t = v.GetType();
        return t.IsPrimitive || t.IsEnum || v is string or decimal or DateTime or Guid or TimeSpan;
    }

    // ── 驱动编辑器 ───────────────────────────────────────────

    private static List<Control> EnumerateEditors(PropertyPanelView panel) =>
        panel.GetLogicalDescendants()
            .OfType<Control>()
            // 阶段 201：**加上 TextBox** —— 面板里有 21 个具名 TextBox（20 个走 XAML 属性式
            // `TextChanged="OnPropertyChanged"` / `OnScreenPropertyChanged` / `OnVisualStateChanged`，
            // 1 个 `ExpressionInput` 走 code-behind），而这一面原先只枚举编辑器族
            // （FANumberBox/ComboBox/CheckBox）⇒ 那 20 个一直是**没判据的**（见
            // AppXamlAttributeWiringFaceTests 的登记表）。
            .Where(c => c is FluentAvaloniaUI.FANumberBox or ComboBox or CheckBox or TextBox)
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 数字框候选值：先 +1，被上限拒绝（如 Opacity 100 → 101 不合法）就退回 -1，
    /// 再退到几个绝对锚点。避免「值不合法 → 模型没变」被误判成「没接线」。
    /// </summary>
    private static IEnumerable<double> NumberCandidates(double current)
    {
        yield return current + 1;
        yield return current - 1;
        yield return current + 0.5;
        yield return 1;
        yield return 0;
        yield return 50;
        yield return 42;
    }

    /// <summary>
    /// 文本框候选值：**顺序就是"哪些写法更可能被收下"** ——
    /// 普通文本（`Name`/`Text`/`Options`…）→ **合法十六进制**（`PropBgColor` 一族的
    /// `SanitizeColor` 会把非法色值退回去，所以必须给合法值）→ 数字串 →
    /// 中性控件名（`ControlNameDefaults.ToPersisted` 会把本地化别名转回 `Button1` 这类）→ 空（清空一支）。
    /// </summary>
    private static IEnumerable<string> TextCandidates()
    {
        yield return "候选文本-1";
        yield return "#123456";
        yield return "1234";
        yield return "Button1";
        yield return "";
    }

    /// <summary>
    /// 反复尝试把编辑器改到一个能引起模型变化的值。
    /// <paramref name="changed"/> 用于判断「当前这一次改动是否真的改变了模型」。
    /// </summary>
    private static bool Drive(Control editor, Func<bool> takeAndCompare)
    {
        switch (editor)
        {
            case FluentAvaloniaUI.FANumberBox nb:
                foreach (var candidate in NumberCandidates(nb.Value))
                {
                    if (takeAndCompare()) return true;   // 先比一次，吃掉上一次的基线
                    nb.Value = candidate;
                    UiTestHost.Pump();
                }
                return takeAndCompare();

            case ComboBox cb:
                if (cb.ItemCount < 2) return false;
                for (var i = 0; i < cb.ItemCount; i++)
                {
                    if (takeAndCompare()) return true;
                    cb.SelectedIndex = (cb.SelectedIndex + 1) % cb.ItemCount;
                    UiTestHost.Pump();
                }
                return takeAndCompare();

            case CheckBox chk:
                if (takeAndCompare()) return true;
                chk.IsChecked = !(chk.IsChecked ?? false);
                UiTestHost.Pump();
                return takeAndCompare();

            case TextBox tb:
                // ⚠ 程序化写 `Text` **不触发** `TextChanged`（阶段 187 实测）⇒ 必须补发事件，
                //   否则驱动的不是生产路径（那一跳正是 `OnPropertyChanged` → `SaveProperties`）。
                foreach (var candidate in TextCandidates())
                {
                    if (takeAndCompare()) return true;
                    tb.Text = candidate;
                    tb.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
                    UiTestHost.Pump();
                }
                return takeAndCompare();

            default:
                return false;
        }
    }

    [Fact]
    public void EveryEditor_MutatesBoundModel_WhenExercised()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Id = "s1", Name = "S1", Width = 1024, Height = 600 };
            var project = new HmiProject();
            project.Screens.Add(screen);

            var panel = new PropertyPanelView { CurrentScreen = screen };
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            // 屏幕侧的 DPI / 安全区 / 启动屏写进的是**工程级**配置
            // （PropertyPanelView.axaml.cs:483 起：模型中无屏幕级字段），
            // 不注入工程这些编辑器会静默不写 —— 不是没接线。
            panel.SetProject(project);
            panel.BindControl(MakeControl(HmiControlType.Label));
            UiTestHost.Pump();

            var editors = EnumerateEditors(panel);
            Assert.True(editors.Count >= 85,
                $"逻辑树里只找到 {editors.Count} 个编辑器，面板可能没建起来（预期 91 个左右："
                + "31 FANumberBox + 30 ComboBox + 9 CheckBox + 21 TextBox）");

            var coveredBy = new Dictionary<string, string>(StringComparer.Ordinal);
            var undrivable = new Dictionary<string, string>(StringComparer.Ordinal);

            void RunScenario(string label, HmiControl? control)
            {
                if (control is null)
                {
                    // 屏幕场景必须走 BindScreen —— 直接设 CurrentScreen 属性不会触发
                    // 面板内部的屏幕上下文（安全区/Dpi/启动屏等编辑器会静默不写）。
                    panel.BindScreen(screen);
                }
                else
                {
                    panel.BindControl(control);
                    panel.CurrentScreen = screen;
                }
                UiTestHost.Pump();

                Func<object> target = () => (object?)control ?? screen;

                foreach (var e in editors)
                {
                    if (coveredBy.ContainsKey(e.Name!)) continue;

                    // 每次改动前后取快照；控件 / 屏幕 / 工程三处都拍进去 ——
                    // 编辑器分别写向这三者（如屏幕 DPI/安全区写工程级默认配置）。
                    string Take() =>
                        Snapshot(target()) + "‖" + Snapshot(screen) + "‖" + Snapshot(project);
                    string? baseline = null;
                    bool Diff()
                    {
                        var now = Take();
                        var changed = baseline is not null && now != baseline;
                        baseline = now;
                        return changed;
                    }

                    if (Drive(e, Diff))
                        coveredBy[e.Name!] = label;
                    else
                        undrivable[e.Name!] = e.GetType().Name;
                }
            }

            foreach (var type in AllTypes)
                RunScenario(type.ToString(), MakeControl(type));

            RunScenario("屏幕", null);

            var uncovered = editors
                .Select(e => e.Name!)
                .Where(n => !coveredBy.ContainsKey(n) && !Exemptions.ContainsKey(n))
                .ToList();

            // 卫生检查：豁免项如果其实能被驱动，就不该留在豁免清单里（会掩盖未来真实的回归）。
            // 注意这里**只报告不断言** —— 个别编辑器（如 ScreenPresetCombo）能否驱动取决于
            // 测试执行顺序（静态选项列表可能已被别的测试填充），做成断言会产生顺序相关的假失败。
            var drivableExemptions = Exemptions.Keys.Where(coveredBy.ContainsKey).ToList();

            _out.WriteLine($"逻辑树编辑器 {editors.Count} 个；驱动成功 {coveredBy.Count} 个；" +
                           $"驱动不动 {undrivable.Count} 个；豁免 {Exemptions.Count} 个；未覆盖 {uncovered.Count} 个");
            if (uncovered.Count > 0)
                _out.WriteLine($"未覆盖：{string.Join(", ", uncovered)}");
            if (drivableExemptions.Count > 0)
                _out.WriteLine($"提示：以下豁免项本次其实可驱动，可考虑从清单移除：{string.Join(", ", drivableExemptions)}");

            Assert.True(uncovered.Count == 0,
                "以下编辑器既没被任何场景驱动出模型变更，也不在豁免清单里 —— " +
                "要么它没接线（改了不保存），要么需要补一个能显示它的场景：" +
                string.Join(", ", uncovered));
        });
    }

    [Fact]
    public void ExemptionList_HasNoStaleEntries()
    {
        UiTestHost.Run(() =>
        {
            var panel = new PropertyPanelView();
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            panel.BindControl(MakeControl(HmiControlType.Label));
            UiTestHost.Pump();

            var names = EnumerateEditors(panel).Select(e => e.Name!).ToHashSet(StringComparer.Ordinal);
            var stale = Exemptions.Keys.Where(k => !names.Contains(k)).ToList();

            Assert.True(stale.Count == 0,
                "豁免清单里有已经不存在的编辑器名（改名或删除后要同步清理）：" + string.Join(", ", stale));
        });
    }
}
