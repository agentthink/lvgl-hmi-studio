using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="ScreenFactory"/> 的行为契约 + 「所有新建屏幕路径都必须走它」的结构守卫。
///
/// <para><b>背景（为什么要这个文件）：</b>此前工程内有 6 处各自造屏幕，尺寸来源互不相同 ——
/// 屏幕管理器读 <see cref="ScreenDefaultsConfig"/>、工具栏与新建工程硬编码 1024×600、
/// AI Agent 工具默认 1024×600、AI 自然语言生成默认 <b>800×480</b>、
/// 模拟器导出器兜底硬编码。于是「屏幕默认模板」这个分组在界面上摆着，
/// 实际只有 1/6 的路径听它的话，而剩下 5 条里还有一个数字（800×480）
/// 跟运行时目标（1024×600）都对不上。</para>
/// </summary>
public class ScreenFactoryTests
{
    // ── 默认值来源 ──

    [Fact]
    public void Create_TakesSizeAndAdaptiveAndBgColorFromProjectDefaults()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultWidth = 800;
        project.ScreenDefaultsConfig.DefaultHeight = 480;
        project.ScreenDefaultsConfig.DefaultAdaptiveMode = ScreenAdaptiveMode.Fit;
        project.ScreenDefaultsConfig.DefaultBgColor = "#112233";

        var screen = ScreenFactory.Create(project);

        Assert.Equal(800, screen.Width);
        Assert.Equal(480, screen.Height);
        Assert.Equal(ScreenAdaptiveMode.Fit, screen.AdaptiveMode);
        Assert.Equal("#112233", screen.BgColor);
    }

    [Fact]
    public void Create_MissingDimensions_FallToProjectDefaults_NotHardcoded800x480()
    {
        // 回归钉子：AI 两条路径曾经在「LLM 没给尺寸」时写死 800×480，
        // 把工程默认（1024×600）静默压掉。判据是「缺参 → 等于工程默认」，
        // 而不是任何具体数字。
        var project = new HmiProject();
        Assert.Equal(1024, project.ScreenDefaultsConfig.DefaultWidth);

        var screen = ScreenFactory.Create(project, name: "AI 生成屏幕");

        Assert.Equal(project.ScreenDefaultsConfig.DefaultWidth, screen.Width);
        Assert.Equal(project.ScreenDefaultsConfig.DefaultHeight, screen.Height);
        Assert.NotEqual(800, screen.Width);
        Assert.NotEqual(480, screen.Height);
    }

    // ── 显式参数优先级 ──

    [Fact]
    public void Create_ExplicitDimensions_WinOverProjectDefaults()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultWidth = 1024;
        project.ScreenDefaultsConfig.DefaultHeight = 600;

        var screen = ScreenFactory.Create(project, width: 320, height: 240);

        Assert.Equal(320, screen.Width);
        Assert.Equal(240, screen.Height);
    }

    [Fact]
    public void Create_ExplicitBgColor_WinsOverProjectDefault()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultBgColor = "#000000";

        var screen = ScreenFactory.Create(project, bgColor: "#ffffff");

        Assert.Equal("#ffffff", screen.BgColor);
    }

    [Fact]
    public void Create_OnlyOneDimensionGiven_OtherFallsToProjectDefault()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultWidth = 1024;
        project.ScreenDefaultsConfig.DefaultHeight = 600;

        var screen = ScreenFactory.Create(project, width: 480);

        Assert.Equal(480, screen.Width);
        Assert.Equal(600, screen.Height);
    }

    // ── 非法尺寸兜底 ──

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveProjectDefault_FallsBackToT113Size(int bad)
    {
        // 工程设置面板允许手输，0 / 负数在 JSON 里是合法值，
        // 但会生成 lv_obj_set_size(screen, 0, 0) —— 一个看不见的屏幕。
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultWidth = bad;
        project.ScreenDefaultsConfig.DefaultHeight = bad;

        var screen = ScreenFactory.Create(project);

        Assert.Equal(ScreenFactory.FallbackWidth, screen.Width);
        Assert.Equal(ScreenFactory.FallbackHeight, screen.Height);
    }

    [Fact]
    public void Create_NullProject_UsesFallbackSize()
    {
        var screen = ScreenFactory.Create(null);

        Assert.Equal(ScreenFactory.FallbackWidth, screen.Width);
        Assert.Equal(ScreenFactory.FallbackHeight, screen.Height);
        Assert.Equal(ScreenAdaptiveMode.None, screen.AdaptiveMode);
        Assert.Null(screen.BgColor);
    }

    // ── 命名 ──

    [Fact]
    public void Create_NoName_UsesNextScreenNumber()
    {
        var project = new HmiProject();

        var first = ScreenFactory.Create(project);
        Assert.Equal("Screen 1", first.Name);

        project.Screens.Add(first);
        var second = ScreenFactory.Create(project);
        Assert.Equal("Screen 2", second.Name);
    }

    [Fact]
    public void Create_BlankName_FallsBackToNumberedName()
    {
        var project = new HmiProject();
        project.Screens.Add(ScreenFactory.Create(project));

        Assert.Equal("Screen 2", ScreenFactory.Create(project, name: "   ").Name);
    }

    [Fact]
    public void Create_ExplicitName_IsTrimmed()
    {
        var project = new HmiProject();

        Assert.Equal("主画面", ScreenFactory.Create(project, name: "  主画面  ").Name);
    }

    // ── 契约：工厂不负责把屏幕挂进工程 ──

    [Fact]
    public void Create_DoesNotMutateProjectScreenList()
    {
        var project = new HmiProject();
        var before = project.Screens.Count;

        var screen = ScreenFactory.Create(project);

        Assert.Equal(before, project.Screens.Count);
        Assert.DoesNotContain(screen, project.Screens);
    }

    [Fact]
    public void Create_TwoCalls_ProduceDistinctIds()
    {
        var project = new HmiProject();

        Assert.NotEqual(ScreenFactory.Create(project).Id, ScreenFactory.Create(project).Id);
    }

    // ── 结构守卫：任何新增的「造屏幕」路径都必须收敛到工厂 ──

    /// <summary>允许出现 <c>new HmiScreen</c> 的源文件（相对仓库根，正斜杠）。</summary>
    private static readonly string[] AllowedConstructionSites =
    {
        "src/HmiEditor.Core/Services/ScreenFactory.cs",
    };

    private static readonly Regex HmiScreenConstruction =
        new(@"\bnew\s+HmiScreen\b", RegexOptions.Compiled);

    /// <summary>剥注释后数构造点 —— 不剥注释的话，讲解本规则的注释会被规则自己判为违规。</summary>
    private static int CountConstructions(string source) =>
        HmiScreenConstruction.Matches(SourceScanner.StripComments(source)).Count;

    [Fact]
    public void OnlyScreenFactoryConstructsHmiScreenInProductCode()
    {
        var root = SourceScanner.RepoRoot;
        var sources = SourceScanner.ProductSources();

        // 扫描器自检①：枚举本身要有量。若枚举坏了（路径写错/全被过滤掉），
        // 下面的 offenders 天然为空，守卫会退化成「永不失败的摆设」。
        Assert.True(sources.Count > 100,
            $"src/ 下只枚举到 {sources.Count} 个 .cs 文件，扫描器判据可能已失效");

        // 扫描器自检②：判据必须能命中已知的构造点。
        var factoryRelative = "src/HmiEditor.Core/Services/ScreenFactory.cs";
        Assert.Contains(factoryRelative, sources.Select(SourceScanner.Relative).ToList());
        var factoryHits = CountConstructions(File.ReadAllText(Path.Combine(root, factoryRelative)));
        Assert.True(factoryHits == 1,
            $"ScreenFactory.cs 里应有且仅有 1 处 new HmiScreen，实测 {factoryHits} 处");

        var offenders = new List<string>();
        var total = 0;
        foreach (var file in sources)
        {
            var hits = CountConstructions(File.ReadAllText(file));
            if (hits == 0) continue;
            total += hits;

            var relative = SourceScanner.Relative(file);
            if (!AllowedConstructionSites.Contains(relative, StringComparer.Ordinal))
                offenders.Add($"{relative} ({hits} 处)");
        }

        // 扫描器自检③：全库命中数不为零 —— 判据没被剥注释剥过头。
        Assert.True(total == factoryHits,
            $"src/ 下 new HmiScreen 命中 {total} 处，与工厂内的 {factoryHits} 处不一致");

        Assert.True(offenders.Count == 0,
            "以下文件绕过了 ScreenFactory 直接 new HmiScreen —— 新屏的尺寸/自适应/背景色会再次各说各话。"
            + "请改为 ScreenFactory.Create(project, ...)，需要显式尺寸时传 null 表示「用工程默认」：\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void GuardItself_DetectsConstructionPattern()
    {
        // 守卫自检（不落盘）：判据必须有正反两面的分辨力，
        // 否则上一条测试只是「恒真」或「恒假」。
        Assert.Equal(1, CountConstructions("        var s = new HmiScreen { Width = 1024 };"));
        Assert.Equal(1, CountConstructions("Screens.Add(new  HmiScreen());"));
        Assert.Equal(2, CountConstructions("var a = new HmiScreen(); var b = new HmiScreen();"));

        // 反向：不是构造的写法不能被命中。
        Assert.Equal(0, CountConstructions("var s = ScreenFactory.Create(project);"));
        Assert.Equal(0, CountConstructions("HmiScreenDefaults defaults = null!;"));

        // 反向：注释里提到构造也不是违规（这正是第一条自检踩到的坑）。
        Assert.Equal(0, CountConstructions("// new HmiScreen 已统一走 ScreenFactory"));
        Assert.Equal(0, CountConstructions("/// 不要直接 <c>new HmiScreen { ... }</c>"));
        Assert.Equal(0, CountConstructions("/* new HmiScreen */"));
        Assert.Equal(1, CountConstructions("/* 注释 */ var s = new HmiScreen();// 尾注释"));

        // 反向：剥注释不能剥掉真正的代码。
        Assert.Equal(0, CountConstructions("// var s = new HmiScreen();"));
    }
}
