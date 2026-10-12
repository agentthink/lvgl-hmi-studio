using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 控件**默认名**的两条纪律（登记 #61r，阶段 159 闭环）：
///
/// <list type="number">
///   <item><b>持久化名与界面语言无关</b> —— 同一份工程在 zh / en 下编辑得到**逐字相同**的控件名
///     （进而相同的生成代码符号、可复现的工程文件）；</item>
///   <item><b>界面上显示本地化别名</b> —— 默认名 <c>Button1</c> 显示成 <c>按钮1</c>（zh）/
///     <c>Button1</c>（en），而「显示什么就写回什么」是**不动点**（写回走
///     <see cref="ControlNameDefaults.ToPersisted"/>）。</item>
/// </list>
///
/// <para>改动前的症状（本判据的靶子）：`DesignCanvas.BuildControl` 拿
/// <see cref="ControlTypeTextKeys.Describe"/> 当**持久化名字** ⇒ 中文界面建出来叫 <c>按钮1</c>、
/// 英文界面叫 <c>Button1</c>，同一工程两次编辑得到两份工程。</para>
///
/// <para>⚠ 本类会**切换全局当前语言** ⇒ 必须挂 <c>Language-global</c> 独占集合
/// （与其它本地化判据同规矩；<c>LocalizationRefreshUiTests</c> 有守卫盯着，漏挂即红）。</para>
/// </summary>
[Collection("Language-global")]
public class ControlDefaultNameTests
{
    private readonly ITestOutputHelper _out;

    public ControlDefaultNameTests(ITestOutputHelper output) => _out = output;

    /// <summary>把语言钉住跑一段、结束后还原（既有本地化判据的共同做法）。</summary>
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

    /// <summary>同上，无返回值那一档。</summary>
    private static void InLanguage(string language, Action body)
        => InLanguage<object?>(language, () => { body(); return null; });

    private static HmiScreen NewScreen() =>
        new() { Id = "s1", Name = "主画面", Width = 800, Height = 480 };

    /// <summary>
    /// 走**生产路径**（<see cref="DesignCanvas.AddControlAt"/>）各建 3 个控件，返回它们的持久化名。
    /// 每次都在**全新的屏幕**上建 ⇒ 序号从 1 起，与既有语言无关。
    /// </summary>
    private static string[] CreateThroughCanvas()
    {
        var screen = NewScreen();
        var canvas = new DesignCanvas { Screen = screen };
        return new[]
        {
            canvas.AddControlAt(HmiControlType.Button, 10, 10, 120, 44).Name,
            canvas.AddControlAt(HmiControlType.Button, 200, 10, 120, 44).Name,
            canvas.AddControlAt(HmiControlType.Screen, 0, 0, 800, 480).Name,
        };
    }

    // ═══════════════════════════ J1 核心：跨语言可复现 ═══════════════════════════

    [Fact]
    public void DefaultNames_AreLanguageIndependent_AndNeutral()
    {
        var zh = InLanguage("zh-CN", CreateThroughCanvas);
        var en = InLanguage("en-US", CreateThroughCanvas);

        _out.WriteLine($"zh-CN: {string.Join(", ", zh)}");
        _out.WriteLine($"en-US: {string.Join(", ", en)}");

        // 中性形状：枚举名 + 序号（ASCII ⇒ 直接可做生成代码符号）
        Assert.Equal(new[] { "Button1", "Button2", "Screen1" }, zh);
        // 🎯 本项的核心：两种语言下**逐字相同**。改动前这里是 [按钮1, 按钮2, 屏幕1] vs [Button1, …]。
        Assert.Equal(zh, en);
    }

    [Fact]
    public void UniqueName_SkipsTakenIndices_AndIgnoresLanguage()
    {
        var zh = InLanguage("zh-CN", () => new[]
        {
            ControlNameDefaults.UniqueName(HmiControlType.Button, Array.Empty<string>()),
            ControlNameDefaults.UniqueName(HmiControlType.Button, new[] { "Button1", "Button3" }),
            ControlNameDefaults.UniqueName(HmiControlType.TextArea, new[] { "Button1" }),
        });
        var en = InLanguage("en-US", () => new[]
        {
            ControlNameDefaults.UniqueName(HmiControlType.Button, Array.Empty<string>()),
            ControlNameDefaults.UniqueName(HmiControlType.Button, new[] { "Button1", "Button3" }),
            ControlNameDefaults.UniqueName(HmiControlType.TextArea, new[] { "Button1" }),
        });

        Assert.Equal(new[] { "Button1", "Button2", "TextArea1" }, zh);
        Assert.Equal(zh, en);
    }

    // ═══════════════════════════ J2 显示别名与写回 ═══════════════════════════

    [Fact]
    public void Display_IsLocalizedAlias_AndToPersistedIsItsLeftInverse()
    {
        var types = Enum.GetValues<HmiControlType>();
        var zhAliases = InLanguage("zh-CN", () => types.ToDictionary(t => t, t => ControlNameDefaults.Display("Button7", t)));
        var enAliases = InLanguage("en-US", () => types.ToDictionary(t => t, t => ControlNameDefaults.Display("Button7", t)));

        var zhButton = InLanguage("zh-CN", () => ControlNameDefaults.Display("Button1", HmiControlType.Button));
        var enButton = InLanguage("en-US", () => ControlNameDefaults.Display("Button1", HmiControlType.Button));
        _out.WriteLine($"Button1 → zh:{zhButton} / en:{enButton}");

        Assert.Equal("按钮1", zhButton);
        Assert.Equal("Button1", enButton);
        Assert.Equal("按钮7", zhAliases[HmiControlType.Button]);
        Assert.Equal("Button7", enAliases[HmiControlType.Button]);
        // 只有**本类型**的中性前缀才翻：`Button7` 在 Slider 那一格不该变成「滑块7」，
        // 而 `Slider7` 在 Slider 那一格必须翻
        Assert.Equal("Button7", zhAliases[HmiControlType.Slider]);
        Assert.Equal("滑动条7", InLanguage("zh-CN", () => ControlNameDefaults.Display("Slider7", HmiControlType.Slider)));
        Assert.Equal("Slider7", InLanguage("en-US", () => ControlNameDefaults.Display("Slider7", HmiControlType.Slider)));

        // 左逆：把每个类型的中性名显示一遍、再写回，必须原样回来（26 个类型逐个走）
        foreach (var type in types)
        {
            foreach (var language in new[] { "zh-CN", "en-US" })
            {
                var neutral = $"{ControlNameDefaults.NeutralPrefix(type)}3";
                var roundTrip = InLanguage(language, () =>
                    ControlNameDefaults.ToPersisted(ControlNameDefaults.Display(neutral, type), type));
                Assert.Equal(neutral, roundTrip);
            }
        }
    }

    [Fact]
    public void LegacyLocalizedNames_KeepTheirDisplay_ButMigrateOnWrite()
    {
        // 旧工程（改动前在中文界面建出来的）里存的是 `按钮1`
        var shownZh = InLanguage("zh-CN", () => ControlNameDefaults.Display("按钮1", HmiControlType.Button));
        var shownEn = InLanguage("en-US", () => ControlNameDefaults.Display("按钮1", HmiControlType.Button));
        Assert.Equal("按钮1", shownZh);   // 显示不变（用户看不出区别）
        Assert.Equal("按钮1", shownEn);   // 英文界面下旧名也原样显示（不假装它是 Button1）

        // 写回时归一到中性名 —— 一次**迁移**：显示不变、工程从此可复现
        var migrated = InLanguage("zh-CN", () => ControlNameDefaults.ToPersisted("按钮1", HmiControlType.Button));
        Assert.Equal("Button1", migrated);
        var shownAfter = InLanguage("zh-CN", () => ControlNameDefaults.Display(migrated, HmiControlType.Button));
        Assert.Equal("按钮1", shownAfter);   // 迁移前后界面一字不变
    }

    [Fact]
    public void UserNamedAndShapeMismatched_AreNeverAliased()
    {
        foreach (var language in new[] { "zh-CN", "en-US" })
        {
            var (alias, writeBack, mismatch, notDigits, zero) = InLanguage(language, () => (
                ControlNameDefaults.Display("报警灯", HmiControlType.Button),
                ControlNameDefaults.ToPersisted("报警灯", HmiControlType.Button),
                ControlNameDefaults.Display("按钮1", HmiControlType.Slider),
                ControlNameDefaults.Display("ButtonX", HmiControlType.Button),
                ControlNameDefaults.Display("Button0", HmiControlType.Button)));

            Assert.Equal("报警灯", alias);        // 用户自己起的名字原样显示
            Assert.Equal("报警灯", writeBack);    // …也原样落盘
            Assert.Equal("按钮1", mismatch);      // 前缀不属于该类型 ⇒ 不翻
            Assert.Equal("ButtonX", notDigits);   // 非纯数字 ⇒ 不翻
            Assert.Equal("Button0", zero);        // 序号从 1 起 ⇒ 0 不算默认名
        }
    }

    [Fact]
    public void CrossReferenceRow_ShowsAlias_ForDefaultNames()
    {
        var row = new ReferenceRow(new ReferenceLocation
        {
            ControlName = "Button1",
            ControlType = HmiControlType.Button,
            ScreenName = "主画面",
        });

        Assert.Equal("按钮1", InLanguage("zh-CN", () => row.ControlName));
        Assert.Equal("Button1", InLanguage("en-US", () => row.ControlName));
    }

    /// <summary>
    /// **名字显示面守卫**：控件名在界面上露出的那些文件里，`ControlNameDefaults.Display` 的**处数**
    /// 必须与表一致（改一处漏一处 ⇒ 同一个控件在画布/面板/列表里名字不一致，而别的判据一条都不会红）。
    ///
    /// <para>🩸 **为什么从"有没有"改成"计数"**（阶段 160 实测）：阶段 159 的这守卫只查
    /// "文件里有没有一次正确调用"，于是 `MainWindow.axaml.cs` 里**另外两处**（多选/单选状态栏）
    /// 直显中性名 + 英文枚举名**照样绿灯** —— 那两处是到阶段 160 才发现并补上的。
    /// 计数式守卫让「动面」必须**重数**（与 `OutsideFaceCensus` 同规矩）。</para>
    ///
    /// <para>⚠ 诚实边界：仍然是**清单式**的 —— 新加一个文件里的显示点时它不会自动发现
    /// （源码上看不出某个 `.Name` 是给人看的还是当标识符用的）；它保证的是
    /// **已列文件里的处数被改动时当场红**。</para>
    /// </summary>
    [Fact]
    public void NameDisplaySurface_RoutesThroughControlNameDefaults()
    {
        (string File, int Count)[] surfaces =
        [
            ("Controls/DesignCanvas.cs", 1),                                  // Text 为空时的回落标签
            ("Controls/DesignCanvas.IndustrialRenderers.cs", 3),              // 过程图形位号文字
            ("Controls/DesignCanvas.Renderers.cs", 4),                        // 多处渲染支的回落标签
            ("Controls/UndoRedoManager.cs", 4),                               // 移动/新增/删除/修改四条撤销描述
            ("MainWindow.axaml.cs", 3),                                       // 撤销描述 ＋ 多选/单选两条状态栏
            ("Views/AnimationEditorView.axaml.cs", 1),                        // 动画「目标控件」下拉
            ("Views/ProjectExplorerView.axaml.cs", 3),                        // 树节点 ＋ 改名预填 ＋ 改名提示
            ("Views/PropertyPanelView.axaml.cs", 2),                          // 标题行 ＋ 名称输入框
            ("Views/ReferenceRow.cs", 1),                                     // 交叉引用「控件名」列
            ("Wiring.DockAggregate.cs", 2),                                   // 删除快照描述 ＋ 预览状态栏
        ];

        var problems = new List<string>();
        foreach (var (file, expected) in surfaces)
        {
            var actual = DisplaySurfaceCount.Count("ControlNameDefaults.Display(", file);
            if (actual != expected)
                problems.Add($"{file}: 期望 {expected} 处、实测 {actual} 处");
        }

        Assert.True(problems.Count == 0,
            "控件名显示面的引用处数与表不一致（**动面就重数**；多/少都说明有人改了显示面而没更新这张表）：\n"
            + string.Join("\n", problems));
    }

    /// <summary>
    /// **写回的真行为**（阶段 160 补）：用**真实键盘输入**替换名称框内容（本仓
    /// `PropertyPanelUiTests.TypeInto` 的注释早写着「Avalonia 12 里程序化赋值 `TextBox.Text`
    /// **不会**触发 `TextChanged`，只有用户输入才会」）⇒ 输入**别名**落盘成中性名、输入自定义名原样落盘。
    ///
    /// <para>🩸 阶段 159 我只拿**程序化赋值**试了一次、就把它写进"诚实边界"说"驱动不动"——
    /// 那是**量的方法不够**，不是路径不可驱动。本判据把那一半从"源码契约"升级成**行为见证**
    /// （源码契约那两条留着当守卫，不撤）。</para>
    /// </summary>
    [Fact]
    public void PropertyPanel_TypedAlias_WritesBackNeutralName()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Id = "s1", Name = "S1", Width = 1024, Height = 600 };
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
            var nameBox = panel.GetLogicalDescendants().OfType<TextBox>()
                .First(b => b.Name == "PropName");

            InLanguage("zh-CN", () =>
            {
                panel.BindControl(control);
                UiTestHost.Pump(5);
                Assert.Equal("按钮1", nameBox.Text);

                TypeInto(window, nameBox, "按钮2");            // 用户照着自己看到的形式改名
                Assert.Equal("Button2", control.Name);        // ⇒ 落盘的是**中性名**（左逆）

                TypeInto(window, nameBox, "报警灯");           // 自定义名
                Assert.Equal("报警灯", control.Name);          // ⇒ 原样落盘

                TypeInto(window, nameBox, "Button9");          // 直接敲中性名也原样
                Assert.Equal("Button9", control.Name);
            });

            window.Close();
            UiTestHost.Pump();
        });
    }

    /// <summary>真实键盘输入（照搬 `PropertyPanelUiTests.TypeInto` 的做法）。</summary>
    private static void TypeInto(Window window, TextBox box, string text)
    {
        box.Focus();
        UiTestHost.Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        UiTestHost.Pump();
    }

    // ═══════════════════════════ J3 面板：显示别名、写回中性名 ═══════════════════════════

    [Fact]
    public void PropertyPanel_ShowsLocalizedAlias_ForDefaultNames()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Id = "s1", Name = "S1", Width = 1024, Height = 600 };
            var project = new HmiProject();
            project.Screens.Add(screen);

            var neutral = new HmiControl
            {
                Type = HmiControlType.Button, Name = "Button1",
                X = 10, Y = 10, Width = 120, Height = 44,
            };
            var custom = new HmiControl
            {
                Type = HmiControlType.Label, Name = "报警灯",
                X = 200, Y = 10, Width = 160, Height = 30,
            };
            screen.Controls.Add(neutral);
            screen.Controls.Add(custom);

            var panel = new PropertyPanelView { CurrentScreen = screen };
            var window = new Window { Width = 900, Height = 900, Content = panel };
            window.Show();
            panel.SetProject(project);

            var nameBox = panel.GetLogicalDescendants().OfType<TextBox>()
                .First(b => b.Name == "PropName");

            // ① 默认名：面板显示本地化别名（工程里仍是中性名）
            Assert.Equal("按钮1", InLanguage("zh-CN", () => { panel.BindControl(neutral); UiTestHost.Pump(5); return nameBox.Text; }));
            Assert.Equal("Button1", InLanguage("en-US", () => { panel.BindControl(neutral); UiTestHost.Pump(5); return nameBox.Text; }));
            Assert.Equal("Button1", neutral.Name);   // 显示了两轮，工程里的名字一字未动

            // ② 用户改过的名：两种语言下都**原样**显示（不翻译）
            Assert.Equal("报警灯", InLanguage("zh-CN", () => { panel.BindControl(custom); UiTestHost.Pump(5); return nameBox.Text; }));
            Assert.Equal("报警灯", InLanguage("en-US", () => { panel.BindControl(custom); UiTestHost.Pump(5); return nameBox.Text; }));

            window.Close();
            UiTestHost.Pump();
        });
    }

    /// <summary>
    /// 面板的**名字写回**走 <see cref="ControlNameDefaults.ToPersisted"/> —— 源码契约守卫。
    ///
    /// <para>行为那一半在 <see cref="PropertyPanel_TypedAlias_WritesBackNeutralName"/>（**真实键盘输入**）；
    /// 本条的职责是"这条唯一出口被绕过时当场红"，即使某天行为判据因为装配方式变化跑不到。</para>
    /// </summary>
    [Fact]
    public void PropertyPanel_NameWrite_GoesThroughToPersisted()
    {
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "PropertyPanelView.Changes.cs")));

        Assert.Contains("ControlNameDefaults.ToPersisted", code);
        // 不许再是改动前那条直写（那正是"照着自己看到的名字改一遍 ⇒ 写回本地化名"的形状）
        Assert.DoesNotContain("_currentControl.Name = PropertyValidator.SanitizeName", code);
    }

    /// <summary>
    /// **两个写回点**都必须走 <see cref="ControlNameDefaults.ToPersisted"/> —— 属性面板的文本框
    /// （上一条）与资源管理器的改名输入框。少一处，用户在那条路径上"照着自己看到的名字改一遍"
    /// 就会把工程写回成随语言变的形状。
    /// </summary>
    [Fact]
    public void RenameWritePoints_GoThroughToPersisted()
    {
        foreach (var rel in new[] { "Views/PropertyPanelView.Changes.cs", "Views/ProjectExplorerView.axaml.cs" })
        {
            var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
                SourceScanner.RepoRoot, "src", "HmiEditor.App", rel)));
            Assert.True(code.Contains("ControlNameDefaults.ToPersisted", StringComparison.Ordinal),
                $"{rel} 的改名写回没有经过 ControlNameDefaults.ToPersisted");
        }
    }
}
