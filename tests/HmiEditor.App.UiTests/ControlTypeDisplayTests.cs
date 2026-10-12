using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **控件类型的显示名**在界面上必须走唯一来源 <see cref="ControlTypeTextKeys"/>（阶段 160 收口）。
///
/// <para>背景：阶段 96（登记 #61o）把「控件类型 → 显示名」收敛成一份、并修掉了交叉引用面板那一列；
/// 但**其余露出点没被枚举到** —— 属性面板标题行、工程树节点、动画「目标控件」下拉、画布回落标签、
/// 面单控件摘要都还在直显**英文枚举名**（`Button` / `TextArea`），中文界面下也是英文。
/// 本判据把这条面**逐文件计数**钉住（同族：<see cref="ControlDefaultNameTests"/> 的显示面守卫）。</para>
///
/// <para>⚠ 本类切全局语言 ⇒ 挂 <c>Language-global</c> 独占集合（同其它本地化判据）。</para>
/// </summary>
[Collection("Language-global")]
public class ControlTypeDisplayTests
{
    private readonly ITestOutputHelper _out;

    public ControlTypeDisplayTests(ITestOutputHelper output) => _out = output;

    private static T InLanguage<T>(string language, Func<T> body)
    {
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage(language);
            return body();
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    private static void InLanguage(string language, Action body)
        => InLanguage<object?>(language, () => { body(); return null; });

    // ═══════════════════════════ J1 映射本身 ═══════════════════════════

    [Fact]
    public void Describe_IsLocalizedForEveryType_AndNeverLeaksTheEnumNameInChinese()
    {
        var types = Enum.GetValues<HmiControlType>();
        var sameInBothLanguages = new List<string>();
        var leaked = new List<string>();

        foreach (var type in types)
        {
            var zh = InLanguage("zh-CN", () => ControlTypeTextKeys.Describe(type));
            var en = InLanguage("en-US", () => ControlTypeTextKeys.Describe(type));

            Assert.False(string.IsNullOrWhiteSpace(zh), $"{type} 的中文显示名为空");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{type} 的英文显示名为空");
            if (zh == en) sameInBothLanguages.Add($"{type} = {zh}");
            // 中文界面下**不许**露出英文枚举名（`Button` / `TextArea` …）
            if (zh == type.ToString()) leaked.Add(type.ToString());
        }

        _out.WriteLine($"类型数 {types.Length}；两种语言同名的 {sameInBothLanguages.Count} 个");
        Assert.Empty(sameInBothLanguages);
        Assert.Empty(leaked);
    }

    // ═══════════════════════════ J2 显示面（逐文件计数） ═══════════════════════════

    /// <summary>
    /// **类型显示面**：这些文件里引用 <see cref="ControlTypeTextKeys"/> 的**处数**必须与表一致。
    ///
    /// <para>⚠ 为什么是**计数**而不是"有没有"：阶段 159 的显示面守卫只查"文件里有没有一次正确调用"，
    /// 于是 `MainWindow.axaml.cs` 里**另外两处**（多选/单选状态栏）直显中性名 + 英文枚举名
    /// **照样绿灯** —— 本阶段就是被这条漏洞放过去的那一处。计数式守卫让「动面」必须**重数**。</para>
    /// </summary>
    [Fact]
    public void TypeDisplaySurface_RoutesThroughControlTypeTextKeys()
    {
        (string File, int Count)[] surfaces =
        [
            ("Controls/DesignCanvas.cs", 1),                                  // Text 为空时的回落标签
            ("Controls/DesignCanvas.Renderers.cs", 1),
            ("Controls/DesignCanvas.Editing.cs", 1),                          // 新建控件的默认**显示文案**
            ("Views/PropertyPanelView.axaml.cs", 2),                          // 标题行（已命名 / 未命名两档）
            ("Views/ProjectExplorerView.axaml.cs", 1),                        // 工程树节点
            ("Views/AnimationEditorView.axaml.cs", 1),                        // 动画「目标控件」下拉
            ("Views/FaceplateTemplateEditorView.axaml.cs", 1),                // 面单「控件摘要」的类型列表
            ("Views/ReferenceRow.cs", 1),                                     // 交叉引用「类型」列（阶段 96 就在了）
            ("MainWindow.axaml.cs", 2),                                       // 多选 / 单选两条状态栏
            ("Views/ToolboxView.axaml.cs", 26),                               // 工具箱条目名（原始消费者）
        ];

        var problems = new List<string>();
        foreach (var (file, expected) in surfaces)
        {
            var actual = DisplaySurfaceCount.Count("ControlTypeTextKeys.Describe(", file);
            if (actual != expected)
                problems.Add($"{file}: 期望 {expected} 处、实测 {actual} 处");
        }

        Assert.True(problems.Count == 0,
            "控件类型显示面的引用处数与表不一致（**动面就重数**；多/少都说明有人改了显示面而没更新这张表）：\n"
            + string.Join("\n", problems));
    }

    // ═══════════════════════════ J3/J4 两个行为见证 ═══════════════════════════

    [Fact]
    public void PropertyPanel_Header_ShowsLocalizedType()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Id = "s1", Name = "S1", Width = 800, Height = 480 };
            var project = new HmiProject();
            project.Screens.Add(screen);
            var control = new HmiControl
            {
                Type = HmiControlType.Button, Name = "Button1",
                X = 10, Y = 10, Width = 120, Height = 44,
            };
            screen.Controls.Add(control);

            var panel = new PropertyPanelView { CurrentScreen = screen };
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            panel.SetProject(project);

            var info = panel.GetLogicalDescendants().OfType<TextBlock>()
                .First(t => t.Name == "InfoText");

            InLanguage("zh-CN", () =>
            {
                panel.BindControl(control);
                UiTestHost.Pump(5);
                _out.WriteLine($"zh 标题行：{info.Text}");
                Assert.Equal("按钮1 (按钮)", info.Text);
            });

            InLanguage("en-US", () =>
            {
                panel.BindControl(control);
                UiTestHost.Pump(5);
                _out.WriteLine($"en 标题行：{info.Text}");
                Assert.Equal("Button1 (Button)", info.Text);
            });

            window.Close();
            UiTestHost.Pump();
        });
    }

    [Fact]
    public void ProjectExplorer_NodeHeader_ShowsLocalizedType()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Id = "s1", Name = "S1", Width = 800, Height = 480 };
            var project = new HmiProject();
            project.Screens.Add(screen);
            screen.Controls.Add(new HmiControl
            {
                Type = HmiControlType.Button, Name = "Button1",
                X = 10, Y = 10, Width = 120, Height = 44,
            });

            var explorer = new ProjectExplorerView();
            explorer.LoadProject(project, screen);

            var headers = InLanguage("zh-CN", () => Headers(explorer));
            var headersEn = InLanguage("en-US", () =>
            {
                explorer.LoadProject(project, screen);   // 重建树 ⇒ 标题按当前语言重算
                return Headers(explorer);
            });

            _out.WriteLine($"zh 树节点：{string.Join(" | ", headers.Where(h => h.Contains('1')))}");
            Assert.Contains("按钮1 (按钮)", headers);
            Assert.Contains("Button1 (Button)", headersEn);
            Assert.DoesNotContain("Button1 (Button)", headers);   // 中文界面下不许出现英文枚举名
        });
    }

    /// <summary>递归收集树里所有节点的标题（`TreeViewItem.Header` 可能不是字符串）。</summary>
    private static List<string> Headers(ProjectExplorerView explorer)
    {
        var tree = explorer.GetLogicalDescendants().OfType<TreeView>()
            .First(t => t.Name == "ProjectTree");
        var result = new List<string>();
        foreach (var item in tree.Items.OfType<TreeViewItem>())
            Walk(item);
        return result;

        void Walk(TreeViewItem node)
        {
            if (node.Header?.ToString() is { } h) result.Add(h);
            foreach (var child in node.Items.OfType<TreeViewItem>())
                Walk(child);
        }
    }
}

/// <summary>
/// 数某个 <c>src/HmiEditor.App</c> 下的文件里某个成员被引用了几处（**先剥注释**，
/// 免得规则被自己的文档判为已满足 —— 本仓 `SourceScanner.StripComments` 的同一条纪律）。
/// </summary>
internal static class DisplaySurfaceCount
{
    internal static int Count(string member, string relativePathUnderApp)
    {
        var path = System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", relativePathUnderApp);
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(path));

        var count = 0;
        var index = 0;
        while ((index = code.IndexOf(member, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += member.Length;
        }
        return count;
    }
}
