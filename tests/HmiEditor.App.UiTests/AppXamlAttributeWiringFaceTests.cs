using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **「XAML 属性式接线」面**（阶段 199）—— 本仓的接线有**三种形态**：
/// ① XAML 属性（<c>&lt;TextBox TextChanged="OnX"/&gt;</c>）· ② code-behind <c>X.TextChanged += …</c> ·
/// ③ 辅助函数注入（<c>AttachChangeHandler(X)</c> 这类）。选择族面从阶段 115 起就三种一起算，
/// 而 <see cref="AppTextBoxWiringCensusTests"/> / <see cref="AppNumberBoxWiringCensusTests"/>
/// 只扫了**第二种**。
///
/// <para><b>🩸 这次勘误（阶段 199）</b>：按属性式再扫一遍，<c>Views/</c> 下有
/// <b>26 个具名 TextBox</b> 是这么接的（数字框 0 条），而且**没有一条**同时出现在 code-behind 里
/// （两形态互斥）⇒ 阶段 189~198 那几张账里"这 20 个框没有 TextChanged 接线"的说法
/// <b>对其中 26 个框是错的</b>（见 <see cref="PropertyPanelTextBoxWiringTests"/> 的登记表已同步更正）。</para>
///
/// <para><b>这一面判什么</b>：每条属性式接线都要**归类** —— 要么有判据**点名驱动**它
/// （<see cref="Wiring.Driver"/>，且那个判据文件必须真的提到这个框名），要么在登记里写明
/// **为什么还没驱动、打算怎么做**（<see cref="Wiring.Note"/>）。新冒出来一条属性式接线、或
/// 处理器改名、或登记表里留了幽灵条目 —— 都会红。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AppXamlAttributeWiringFaceTests
{
    /// <summary>一条属性式接线：视图 / 控件名 / 处理器名 / 驱动判据（null ⇒ 待驱动）· 理由。</summary>
    private sealed record Wiring(string View, string Name, string Handler, string? Driver, string Note);

    /// <summary>枚举驱动的指针前缀：`enumerated@判据文件.cs`（与阶段 118/195 的 `enumerated` 同口径）。</summary>
    private const string EnumeratedPrefix = "enumerated@";

    private static readonly Wiring[] Wirings =
    {
        // ── FaceplateTemplateEditorView：元信息三个框（`OnMetadataChanged` → ApplyMetadataToTemplate）──
        new("FaceplateTemplateEditorView.axaml", "TxtName", "OnMetadataChanged",
            "FaceplateTemplateEditorUiTests.cs",
            "真键输入驱动：TypingMetadata_WritesBackTrimmedValues / ClearingName_FallsBackToDefaultName"),
        new("FaceplateTemplateEditorView.axaml", "TxtCategory", "OnMetadataChanged",
            "AppXamlAttributeWiringFaceTests.cs",
            "本面驱动：FaceplateMetadataBoxes_WriteBackTrimmedValues（写值 + 补发事件 → 断言 tpl.Category）"),
        new("FaceplateTemplateEditorView.axaml", "TxtDescription", "OnMetadataChanged",
            "AppXamlAttributeWiringFaceTests.cs",
            "本面驱动：FaceplateMetadataBoxes_WriteBackTrimmedValues（写值 + 补发事件 → 断言 tpl.Description）"),

        // ── 三个库/工具箱的搜索框（阶段 200 起：全部有判据驱动）──
        new("ToolboxView.axaml", "SearchBox", "OnSearchTextChanged",
            "LibrarySearchBoxXamlWiringTests.cs",
            "本面驱动：搜类型名 ⇒ 只剩那个类型；空结果 ⇒ 列表隐藏 + 空状态；清空 ⇒ 回全量"),
        new("ResourceLibraryView.axaml", "SearchBox", "OnSearchTextChanged",
            "LibrarySearchBoxXamlWiringTests.cs",
            "本面驱动：按 Name（大小写不敏感）/ DisplayName 过滤画廊；空结果 ⇒ 画廊空；清空 ⇒ 回 3 张"),
        new("FaceplateLibraryView.axaml", "SearchBox", "OnSearchTextChanged",
            "LibrarySearchBoxXamlWiringTests.cs",
            "本面驱动：按 Name / Category 过滤模板列表；两侧空白被 Trim；清空 ⇒ 回全量"),

        // ── PropertyPanelView：控件属性 / 屏幕属性 / 视觉状态那 20 个 ──
        new("PropertyPanelView.axaml", "PropName", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "PropText", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "PropTextColor", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "PropBgColor", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "PropBorderColor", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "IndicatorColorBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "ValuePrefixBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "ValueSuffixBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "OptionsBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "PlaceholderBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "HeaderBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "TabTitlesBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "ColWidthsBox", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "TxtActionValue", "OnPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "ScreenName", "OnScreenPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "ScreenBgColor", "OnScreenPropertyChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "VsBgColor", "OnVisualStateChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "VsTextColor", "OnVisualStateChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "VsBorderColor", "OnVisualStateChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
        new("PropertyPanelView.axaml", "VsText", "OnVisualStateChanged",
            "enumerated@PropertyPanelWiringTests.cs",
            "枚举驱动：`PropertyPanelWiringTests` 逐**控件类型**枚举面板里**全部**编辑器（阶段 201 起含 TextBox），`uncovered == 0` 是它自己的穷尽见证（与阶段 118/195 的 `enumerated` 同口径）"),
    };

    /// <summary>一扫描的读数：视图 ⇒ （控件名 ⇒ 处理器）。</summary>
    private static Dictionary<string, Dictionary<string, string>> Scan()
    {
        var root = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative.Replace('/', Path.DirectorySeparatorChar));

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var axamlPath in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, axamlPath).Replace('\\', '/');
            var wired = ControlWiringFace.XamlWiredTextBoxes(File.ReadAllText(axamlPath));
            if (wired.Count > 0) result[rel] = wired;
        }

        return result;
    }

    /// <summary>
    /// **双向差集**：扫描到的每条属性式接线都要在登记表里（视图 / 控件名 / **处理器名**三者都对得上），
    /// 登记表里的每条也必须真的存在。处理器改名、接线被摘、新加一条没登记的属性式接线 ⇒ 红。
    /// </summary>
    [Fact]
    public void Face_MatchesTheRegistry()
    {
        var scan = Scan();

        var scanned = scan.SelectMany(kv => kv.Value.Select(v => (View: kv.Key, Name: v.Key, Handler: v.Value)))
            .Select(x => $"{x.View}::{x.Name}={x.Handler}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        var registered = Wirings.Select(w => $"{w.View}::{w.Name}={w.Handler}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var missing = scanned.Except(registered).ToArray();
        var ghosts = registered.Except(scanned).ToArray();

        Assert.True(missing.Length == 0,
            "这些**属性式接线**（XAML 里 `TextChanged=\"…\"`）没有登记 —— 新加/改名的接线要进登记表：\n  "
            + string.Join("\n  ", missing));
        Assert.True(ghosts.Length == 0,
            "登记表里这些条目在 XAML 里对不上（控件名或处理器名漂了、或接线被摘了）：\n  "
            + string.Join("\n  ", ghosts));

        // 面自检：扫描塌了（正则坏、目录挪走）时上面两条差集都是空集 ⇒ 恒绿
        Assert.True(Wirings.Length >= 26,
            $"登记表只剩 {Wirings.Length} 条 —— 阶段 199 实测 26 条，面可能塌了");
        Assert.True(scan.Count >= 4,
            $"只扫到 {scan.Count} 个视图带属性式接线 —— 阶段 199 实测 4 个");
    }

    /// <summary>
    /// **归类的双向核对**：每个登记条目要么 `Driver` 指向的判据文件**真的存在且点名提到这个框**
    /// （"有判据驱动"不是一句空话），要么 `Note` 写清为什么还没驱动；两者都不给 ⇒ 红。
    /// </summary>
    [Fact]
    public void EveryWiring_IsDrivenOrHasAConcreteReason()
    {
        var driven = 0;
        var pending = 0;
        var problems = new List<string>();

        foreach (var w in Wirings)
        {
            if (w.Driver is { Length: > 0 } driver)
            {
                driven++;

                // `enumerated@判据文件` ⇒ **枚举驱动**：不逐条点名（判据自己保证穷尽），
                // 核对是**指针级**的 —— 文件存在 ＋ 真的提到这个视图 ＋ 含它自己的穷尽见证字样。
                var enumerated = driver.StartsWith(EnumeratedPrefix, StringComparison.Ordinal);
                var file = enumerated ? driver[EnumeratedPrefix.Length..] : driver;

                var path = Path.Combine(SourceScanner.RepoRoot, "tests/HmiEditor.App.UiTests", file);
                if (!File.Exists(path))
                {
                    problems.Add($"{w.View}::{w.Name} 的 Driver 文件不存在：{file}");
                    continue;
                }

                var text = File.ReadAllText(path);
                if (enumerated)
                {
                    if (!text.Contains("PropertyPanelView", StringComparison.Ordinal)
                        || !text.Contains("uncovered", StringComparison.Ordinal))
                    {
                        problems.Add($"{w.View}::{w.Name} 说由 {file} **枚举驱动**，"
                            + "但那个判据里没有 `PropertyPanelView` / `uncovered` 字样（穷尽见证不成立）");
                    }

                    continue;
                }

                if (!Regex.IsMatch(text, $@"""{Regex.Escape(w.Name)}"""))
                    problems.Add($"{w.View}::{w.Name} 说由 {file} 驱动，但那个判据里**没有点名**这个框");
            }
            else
            {
                pending++;
                if (w.Note.Length < 20)
                    problems.Add($"{w.View}::{w.Name} 既没有驱动判据，也没写清理由（Note 太短）");
            }
        }

        Assert.True(problems.Count == 0,
            "属性式接线的归类有问题：\n  " + string.Join("\n  ", problems));

        // 🎯 棘轮：阶段 201 起 26 条**全部**有判据（3 元信息 + 3 搜索框 + 20 属性面板枚举驱动），
        //   `pending` 必须是 0 —— 新冒出来的属性式接线要么**驱动起来**，要么显式改这条下限并写清理由。
        Assert.Equal(26, driven);
        Assert.True(pending == 0,
            $"有 {pending} 条属性式接线还挂在「待驱动」里 —— 本面的棘轮是 26 条全部驱动（阶段 201 收口）");
    }

    /// <summary>
    /// **扫描器自测**：把"属性式"与"code-behind 式"分清楚 ——
    /// 属性式只在标签属性里，<c>--&gt;</c> 注释里的不算，空值不算，**code-behind 的 `+=` 不算**
    /// （这一面只认属性；两者混起来会把 26 条算重）。
    /// </summary>
    [Fact]
    public void Scanner_SelfTest()
    {
        const string xaml = """
            <StackPanel>
              <TextBox x:Name="ByAttribute" TextChanged="OnA"/>
              <TextBox x:Name="Complex" TextChanged="OnB" Watermark="x"/>
              <TextBox x:Name="Empty" TextChanged=""/>
              <TextBox x:Name="NoHandler"/>
              <!-- <TextBox x:Name="InProse" TextChanged="OnGhost"/> -->
              <fa:FANumberBox x:Name="NumA" ValueChanged="OnNumA"/>
              <NumericUpDown x:Name="NumB"/>
            </StackPanel>
            """;

        var text = ControlWiringFace.XamlWiredTextBoxes(xaml);
        Assert.Equal(new[] { "ByAttribute", "Complex" }, text.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal("OnB", text["Complex"]);

        // 空值 / 没有处理器 / 注释里的 / 数字框 —— 都不算
        Assert.DoesNotContain("Empty", text.Keys);
        Assert.DoesNotContain("NoHandler", text.Keys);
        Assert.DoesNotContain("InProse", text.Keys);
        Assert.DoesNotContain("NumA", text.Keys);

        // 数字框那一族单独扫（实测 0 条，但原语要能用）
        var numbers = ControlWiringFace.XamlWiredNumberBoxes(xaml);
        Assert.Equal(new[] { "NumA" }, numbers.Keys.ToArray());

        // ⚠ 与 code-behind 原语**互斥**：属性式的那几个名字，`WiredNames` 一个都扫不到
        var codeBehind = ControlWiringFace.WiredNames("ByAttribute.TextChanged += OnA;");
        Assert.Contains("ByAttribute", codeBehind);
        Assert.DoesNotContain("Complex", codeBehind);
    }

    /// <summary>
    /// **本面补的两条驱动**（`TxtCategory` / `TxtDescription`）：写值 ＋ 补发 <c>TextChangedEvent</c>
    /// ⇒ `OnMetadataChanged` → `ApplyMetadataToTemplate` ⇒ 模板元信息被写回（**裁剪后**）。
    /// （`TxtName` 那一条已由 <see cref="FaceplateTemplateEditorUiTests"/> 用真键输入驱动。）
    /// </summary>
    [Fact]
    public void FaceplateMetadataBoxes_WriteBackTrimmedValues()
    {
        var template = new ControlTemplate
        {
            Id = "tpl1",
            Name = "老名字",
            Category = "老分类",
            Description = "老说明",
            Version = 3,
        };

        UiTestHost.Run(() =>
        {
            var view = new FaceplateTemplateEditorView(template);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：起点与手势值不同（否则写回"抄对了"与"压根没抄"同形）
                Assert.Equal("老分类", template.Category);
                Assert.Equal("老说明", template.Description);

                ControlWiringFace.Write(view, "TxtCategory", "  新分类  ", pump: 1);
                ControlWiringFace.Write(view, "TxtDescription", "  新说明  ", pump: 1);

                // ⚠ 两条**不同**的口径（阶段 199 实测）：名字/分类 **裁剪**后写回，说明**原样**写回
                //   （`_template.Description = TxtDescription.Text ?? ""` —— 那一行没有 Trim）
                Assert.Equal("新分类", template.Category);
                Assert.Equal("  新说明  ", template.Description);

                // 清空分类 ⇒ 落**本地化的兜底**（与产品同一个来源：AppLocalization）
                ControlWiringFace.Write(view, "TxtCategory", "   ", pump: 1);
                Assert.Equal(
                    HmiEditor.App.Resources.AppLocalization.Service[
                        HmiEditor.App.Resources.Lang.FaceplateEditorCategoryGeneral],
                    template.Category);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }
}
