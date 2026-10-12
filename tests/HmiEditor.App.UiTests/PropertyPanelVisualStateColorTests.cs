using System;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **「视觉状态」三色在 `Normal` 态的行为面**（阶段 201，判据对着缺陷 `#91`）——
/// 提示语写着「其它态留空 = 继承 Normal」，装载侧在 Normal 也把三格当"控件当前色"填；
/// 但**写回侧原先只写文本/对齐/图片**，于是 `VsBgColor` / `VsTextColor` / `VsBorderColor`
/// 在 Normal 态**改了什么都不发生**（连状态条目都不建），而 `UpdateVisualStateSwatches()`
/// 会把色块刷成新颜色 ⇒ 用户看到"改成功了"的假象。
///
/// <para><b>修好之后这一面判两件事</b>：① Normal 态改三色 ⇒ **写进控件自己的三色**，
/// 并把内容区同字段的框（`PropBgColor` / `PropTextColor` / `PropBorderColor`）同步过去；
/// ② 非 Normal 态改三色 ⇒ **不碰控件三色**，只写状态条目（清空到全空则移除条目）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class PropertyPanelVisualStateColorTests
{
    /// <summary>三色一族：视觉状态框 → 控件字段 → 内容区那个同字段的框。</summary>
    private sealed record Spec(string VsBox, string Field, Func<HmiControl, string?> Read, string ContentBox);

    /// <summary>
    /// **导出侧悬停表：控件 → 提示键**（阶段 208 立两格 ＋ 阶段 215 加 `#96`/`#97` ＋ 阶段 218 加 `#98`）。
    /// 绑定与非空由 <see cref="Panel_MarksTheFieldsThatCannotBeExportedPerState"/> 判，
    /// 文案内容由 <see cref="ExportHints_TrackTheCurrentBehavior"/> 判 —— **同一条事实不两处判**。
    /// </summary>
    private static readonly (string Control, string Key)[] HintBindings =
    {
        ("VsText", "prop.visual.state.export.hint.text"),
        ("VsTextVAlign", "prop.visual.state.export.hint.valign"),
        ("PropTextAlign", "prop.text.align.export.hint"),
        ("PropFontSize", "prop.font.size.export.hint"),
        // 阶段 218：登记的 #98（安全区 ＋ DPI 是"存了没人用"的死设置）也按同一口径说给用户听
        ("ScreenDpi", "prop.screen.dpi.export.hint"),
        ("SafeLeft", "prop.screen.safearea.export.hint"),
        ("SafeTop", "prop.screen.safearea.export.hint"),
        ("SafeRight", "prop.screen.safearea.export.hint"),
        ("SafeBottom", "prop.screen.safearea.export.hint"),
    };

    /// <summary>一条悬停文案的**内容规则**：旧事实的逐字原句（禁止出现）＋ 当前行为的锚词（必须出现，按语言给）。</summary>
    private sealed record HintRule(string Key, string[] StaleSentences, Dictionary<string, string[]> RequiredTokens);

    /// <summary>
    /// 阶段 230：六条键的**双向规则**。旧句是**逐字**的（取自 git 历史里那几句被行为甩下的文案），
    /// 锚词与生成器/面板代码同词 —— 行为再变时，改文案的人会被这张表拦下来。
    /// </summary>
    private static readonly HintRule[] HintRules =
    {
        new("prop.visual.state.export.hint.text",
            new[]
            {
                "非默认态的状态文本：导出只留一行声明注释（LVGL 静态样式表达不了文本切换",
                "State text on non-default states: the export only leaves a declaration comment",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "回调", "LV_EVENT_STATE_CHANGED", "Label" },
                ["en-US"] = new[] { "callback", "LV_EVENT_STATE_CHANGED", "Label" },
            }),
        new("prop.visual.state.export.hint.valign",
            new[]
            {
                "非默认态的垂直对齐：导出只留一行声明注释（LVGL 没有状态化的 vertical-align",
                "Vertical align on non-default states: the export only leaves a declaration comment",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "回调", "LV_EVENT_STATE_CHANGED", "Label" },
                ["en-US"] = new[] { "callback", "LV_EVENT_STATE_CHANGED", "Label" },
            }),
        new("prop.text.align.export.hint",
            new[]
            {
                "导出现在只在「下拉框」上写水平对齐",
                "writes horizontal align only for Dropdown",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "Label", "CheckBox", "Dropdown" },
                ["en-US"] = new[] { "Label", "CheckBox", "Dropdown" },
            }),
        new("prop.font.size.export.hint",
            new[]
            {
                "否则导出沿用主题默认字体",
                "otherwise the export keeps the theme default font",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "montserrat", "近似" },
                ["en-US"] = new[] { "montserrat", "approximate" },
            }),
        new("prop.screen.safearea.export.hint",
            new[]
            {
                "DPI 缩放仍未进导出",
                "DPI scaling is still export-only-pending",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "吸附", "DPI" },
                ["en-US"] = new[] { "snaps", "DPI" },
            }),
        new("prop.screen.dpi.export.hint",
            new[]
            {
                "目前尚未生效：DPI 既不写进导出代码",
                "Not effective yet: the DPI scale is written to neither",
            },
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["zh-CN"] = new[] { "倍率", "几何", "语义数值" },
                ["en-US"] = new[] { "geometry", "semantic" },
            }),
    };

    private static readonly Spec[] Specs =
    {
        new("VsBgColor", "HmiControl.BgColor", c => c.BgColor, "PropBgColor"),
        new("VsTextColor", "HmiControl.TextColor", c => c.TextColor, "PropTextColor"),
        new("VsBorderColor", "HmiControl.BorderColor", c => c.BorderColor, "PropBorderColor"),
    };

    /// <summary>夹具：`Button` 才显示「视觉状态」那块（`PropertyPanelView.Binding.cs` 的 supportsVisualStates）。</summary>
    private static HmiControl MakeControl() => new()
    {
        Id = "c1",
        Type = HmiControlType.Button,
        Name = "Button1",
        Text = "标签",
        BgColor = "#0EA5E9",
        TextColor = "#111827",
        BorderColor = "#334155",
    };

    private static ComboBox StateCombo(PropertyPanelView panel) =>
        ControlWiringFace.Find<ComboBox>(panel, "CboVisualState");

    [Fact]
    public void NormalStateColors_WriteBackToControl_AndSyncContentBoxes()
    {
        using var sandbox = new DataRootSandbox("propertypanel-vs-normal-color");

        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var panel = new PropertyPanelView { CurrentScreen = new HmiScreen { Id = "s1", Name = "S1" } };
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            panel.BindControl(control);
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：状态下拉默认停在 Normal，且三个框装载的是**控件自己的三色**（装载侧的对称）
                Assert.Equal(0, StateCombo(panel).SelectedIndex);
                foreach (var spec in Specs)
                {
                    Assert.Equal(spec.Read(control), ControlWiringFace.Find<TextBox>(panel, spec.VsBox).Text);
                    Assert.Equal(spec.Read(control), ControlWiringFace.Find<TextBox>(panel, spec.ContentBox).Text);
                }

                // 三条手势：Normal 态改色 ⇒ 落进控件，且内容区同字段框同步
                var values = new[] { "#123456", "#654321", "#ABCDEF" };
                for (var i = 0; i < Specs.Length; i++)
                {
                    var spec = Specs[i];
                    ControlWiringFace.Write(panel, spec.VsBox, values[i], pump: 1);

                    Assert.True(values[i] == spec.Read(control),
                        $"Normal 态在 {spec.VsBox} 里写 {values[i]} ⇒ {spec.Field} 应当是 {values[i]}，实际 '{spec.Read(control)}'（#91：改了个寂寞）");
                    Assert.Equal(values[i], ControlWiringFace.Find<TextBox>(panel, spec.ContentBox).Text);
                }

                // 口径：Normal 态**清空**某一格 ≠ "把控件颜色清成 null"（"留空"只在其它态表示继承）
                var bgBefore = control.BgColor;
                ControlWiringFace.Write(panel, "VsBgColor", "", pump: 1);
                Assert.Equal(bgBefore, control.BgColor);
                Assert.Equal(bgBefore, ControlWiringFace.Find<TextBox>(panel, "PropBgColor").Text);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public void OtherStates_WriteStateEntry_NotTheControl()
    {
        using var sandbox = new DataRootSandbox("propertypanel-vs-other-color");

        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            var panel = new PropertyPanelView { CurrentScreen = new HmiScreen { Id = "s1", Name = "S1" } };
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            panel.BindControl(control);
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：下拉第 2 项是 Pressed（`<ComboBoxItem Tag="Pressed"/>`）
                var combo = StateCombo(panel);
                Assert.Equal(4, combo.ItemCount);   // Normal / Pressed / Checked / Disabled
                combo.SelectedIndex = 1;   // Pressed（`SelectionChanged` 在生产里也是这么来的）
                UiTestHost.Pump(2);

                // 前提自检：切到 Pressed 之后三格装载的是**状态条目**（还没有 ⇒ 空），
                // 而不是 Normal 那样装载控件当前色 —— 这条把"真的切过去了"量出来
                Assert.Equal("", ControlWiringFace.Find<TextBox>(panel, "VsBgColor").Text);
                Assert.Equal("", ControlWiringFace.Find<TextBox>(panel, "VsTextColor").Text);
                Assert.Equal("", ControlWiringFace.Find<TextBox>(panel, "VsBorderColor").Text);

                var before = (control.BgColor, control.TextColor, control.BorderColor);

                // 反面见证：非 Normal 态改色 ⇒ **控件三色一字不动**，只落进状态条目
                ControlWiringFace.Write(panel, "VsBgColor", "#0F0F0F", pump: 1);
                ControlWiringFace.Write(panel, "VsTextColor", "#F0F0F0", pump: 1);
                ControlWiringFace.Write(panel, "VsBorderColor", "#00FF00", pump: 1);

                Assert.Equal(before, (control.BgColor, control.TextColor, control.BorderColor));

                var pressed = control.GetStateStyle(VisualState.Pressed);
                Assert.NotNull(pressed);
                Assert.Equal("#0F0F0F", pressed!.BgColor);
                Assert.Equal("#F0F0F0", pressed.TextColor);
                Assert.Equal("#00FF00", pressed.BorderColor);

                // 继承口径：把三条都清空 ⇒ 条目被移除（"留空 = 继承 Normal"）
                ControlWiringFace.Write(panel, "VsBgColor", "", pump: 1);
                ControlWiringFace.Write(panel, "VsTextColor", "", pump: 1);
                ControlWiringFace.Write(panel, "VsBorderColor", "", pump: 1);
                ControlWiringFace.Write(panel, "VsText", "", pump: 1);
                Assert.Null(control.GetStateStyle(VisualState.Pressed));

                // 控件三色仍然一字不动
                Assert.Equal(before, (control.BgColor, control.TextColor, control.BorderColor));
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **面板必须把"导不出去"这件事说出来**（阶段 208 立，阶段 215 扩到四格）：
    /// 非 `Normal` 态的**状态文本**与**垂直对齐**在导出侧**结构上表达不了**（阶段 202 量出来的：
    /// LVGL 没有状态化 vertical-align、静态样式也切不了文本）—— 此前只有代码里的注释，
    /// **用户看不到**。现在这两格各带一句悬停说明。
    ///
    /// <para>两向核对：① XAML 里那两格必须挂着对应的 `ToolTip.Tip` 键；
    /// ② 那个键在 **zh-CN 与 en-US 两本字典**里都要有非空值（否则用户看到的是键名）。</para>
    /// </summary>
    [Fact]
    public void Panel_MarksTheFieldsThatCannotBeExportedPerState()
    {
        var axaml = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src/HmiEditor.App/Views/PropertyPanelView.axaml"));

        // 阶段 208 立的两格 ＋ 阶段 215（#96/#97）＋ 阶段 218（#98 的安全区与 DPI）——**同一张表**
        foreach (var (control, key) in HintBindings)
        {
            var tag = System.Text.RegularExpressions.Regex.Match(
                axaml, $@"<\w+[^>]*x:Name=""{control}""[^>]*>",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.True(tag.Success, $"XAML 里找不到控件 {control}");
            Assert.True(tag.Value.Contains($"ToolTip.Tip=\"{{res:Loc {key}}}\"", StringComparison.Ordinal),
                $"{control} 没有挂「导出侧只留声明」的悬停说明（期望 ToolTip.Tip={{res:Loc {key}}}）");
        }

        // 两本字典都要有这个键、且非空（否则界面上会显示键名）
        foreach (var lang in new[] { "zh-CN", "en-US" })
        {
            var path = Path.Combine(SourceScanner.RepoRoot, "src/HmiEditor.Core/Resources", $"Strings.{lang}.json");
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            foreach (var (_, key) in HintBindings)
            {
                Assert.True(doc.RootElement.TryGetProperty(key, out var value),
                    $"{lang} 字典里缺键 {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()),
                    $"{lang} 字典里 {key} 是空值");
            }
        }
    }

    /// <summary>
    /// **悬停文案不得落后于行为**（阶段 230）—— 把上面那张表的判据从「键在、非空」升级到
    /// 「**内容与当前行为一致**」，直接盯住一次真实事故形状：
    ///
    /// <para>`#96`（导出补齐 ＋ 统一可见性，阶段 219/220）、`#97`（内置字号近似，阶段 220）、
    /// `#98`（安全区/DPI 进导出，阶段 227/228）、`#92`（状态文本/垂直对齐的运行时回调，阶段 229）
    /// 都**改了行为**，而阶段 215/218 挂上去的那几句悬停说明**一字未动** ⇒ 用户在面板上读到的仍是旧事实
    /// （"只在 Dropdown 上写对齐" / "否则沿用主题默认字体" / "DPI 仍未进导出" / "状态文本只留声明注释"），
    /// 而旧判据只查「键在、非空」⇒ 陈旧文案能**全绿**通过。</para>
    ///
    /// <para>**怎么判**：每条键一条双向规则 —— ① **禁止**旧事实的逐字原句；② **必含**当前行为的锚词
    /// （与生成器/面板代码同词，如 `montserrat` / `LV_EVENT_STATE_CHANGED`）。两本字典都判。
    /// 另有**元判据**：把规则喂给同一个检查函数 ＋ 一段合成的旧文案 ⇒ 必须报红（否则规则表可能是空转的）。</para>
    /// </summary>
    [Fact]
    public void ExportHints_TrackTheCurrentBehavior()
    {
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        // 面自检：悬停表的**每个键**都要有一条内容规则，一条不多一条不少
        var tableKeys = HintBindings.Select(b => b.Key).Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var ruleKeys = HintRules.Select(r => r.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(tableKeys, ruleKeys);

        var problems = new List<string>();
        foreach (var rule in HintRules)
        {
            foreach (var (lang, dictionary) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                Assert.True(dictionary.TryGetValue(rule.Key, out var value), $"{lang} 字典里缺键 {rule.Key}");
                problems.AddRange(FindHintViolations(rule, value!, lang));
            }
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} 处悬停文案与当前行为不符（行为改了就要跟着改文案）：\n  " + string.Join("\n  ", problems));

        // 元判据：规则表不是空转的 —— 拿**旧文案**喂同一个检查函数，必须报红
        var staleText = string.Concat(HintRules[0].StaleSentences);
        Assert.True(FindHintViolations(HintRules[0], staleText, "zh-CN").Count > 0,
            "元判据失效：把旧文案喂进检查函数居然不报红 —— 这张规则表可能是空转的");
    }

    /// <summary>把一条规则喂给一段文案，回它违反了哪几条（空 = 与当前行为一致）。</summary>
    private static List<string> FindHintViolations(HintRule rule, string value, string language)
    {
        var problems = new List<string>();

        foreach (var stale in rule.StaleSentences)
        {
            if (value.Contains(stale, StringComparison.Ordinal))
                problems.Add($"{language}:{rule.Key} 还写着已失效的旧事实「{stale}」");
        }

        if (!rule.RequiredTokens.TryGetValue(language, out var tokens))
        {
            problems.Add($"{language}:{rule.Key} 没有给这一语的内容规则");
            return problems;
        }

        foreach (var token in tokens)
        {
            if (!value.Contains(token, StringComparison.Ordinal))
                problems.Add($"{language}:{rule.Key} 没写出当前行为的关键词「{token}」");
        }

        return problems;
    }

    private static Dictionary<string, string> LoadDictionary(string language) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(
                SourceScanner.RepoRoot, "src/HmiEditor.Core/Resources", $"Strings.{language}.json")))!;
}
