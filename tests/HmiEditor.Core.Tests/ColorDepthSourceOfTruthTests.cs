using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hmi.Hardware.Contracts;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 色深建模的单一来源守卫。
///
/// <para><b>背景：</b>「色深」在本仓库一共有四处，语义各不相同（见
/// <see cref="ScreenDefaultsConfig.DefaultColorDepth"/> 的文档）。其中两处曾经<b>同名</b>
/// 且都是枚举，而用到它们的地方同时 import 了两个命名空间 —— 编译器直接 CS0104 歧义引用。
/// 更隐蔽的是「候选项硬编码」：工程设置面板手写了一份
/// <c>new[] { "RGB565", "ARGB8888", "Indexed8" }</c>，
/// 而 <c>ConfigDrivenTarget</c> 又手写了一份名字 → 枚举的 switch。
/// 枚举增删成员时，这两份清单都不会跟着变，症状是「新成员在界面里根本没有选项」。</para>
///
/// <para>本文件守三件事：① 默认值必须是枚举的合法成员；② 面板候选项来自枚举而非硬编码；
/// ③ 设备 JSON 解析不再手写映射表。</para>
/// </summary>
public class ColorDepthSourceOfTruthTests
{
    // ── ① 默认值是枚举的合法成员 ──

    [Fact]
    public void ScreenDefaultsConfig_DefaultColorDepth_IsDefinedInContractsEnum()
    {
        var value = new ScreenDefaultsConfig().DefaultColorDepth;

        Assert.True(
            Enum.TryParse<ColorDepth>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed),
            $"ScreenDefaultsConfig.DefaultColorDepth 的默认值 '{value}' 不是 {nameof(ColorDepth)} 的合法成员。"
            + $"当前合法值：{string.Join(", ", Enum.GetNames<ColorDepth>())}");
    }

    [Fact]
    public void ContractsColorDepth_HasNoDuplicateMemberNames()
    {
        // 面板存的是成员名字符串，重名成员会让往返丢信息。
        var names = Enum.GetNames<ColorDepth>();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    // ── ② 面板候选项来自枚举 ──

    private const string SettingsPanelRelativePath = "src/HmiEditor.App/Views/ProjectSettingsView.axaml.cs";

    /// <summary>硬编码的色深名清单特征：形如 <c>{ "RGB565", "ARGB8888" }</c> 的字符串数组。</summary>
    private static readonly Regex HardcodedDepthArray =
        new(@"new\[\]\s*\{[^}]*""(RGB565|ARGB8888|Indexed8)""", RegexOptions.Compiled);

    [Fact]
    public void SettingsPanel_TakesColorDepthOptionsFromEnum()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, SettingsPanelRelativePath);
        var code = SourceScanner.ReadCode(path);

        Assert.Contains("Enum.GetNames(typeof(Hmi.Hardware.Contracts.ColorDepth))", code);
    }

    [Fact]
    public void SettingsPanel_DoesNotHardcodeColorDepthList()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, SettingsPanelRelativePath);
        var code = SourceScanner.ReadCode(path);

        var m = HardcodedDepthArray.Match(code);
        Assert.False(m.Success,
            $"工程设置面板里又出现了硬编码的色深清单：{m.Value}\n"
            + "请改为 Enum.GetNames(typeof(Hmi.Hardware.Contracts.ColorDepth))，"
            + "否则枚举增删成员时面板选项不会跟着变。");
    }

    [Fact]
    public void GuardItself_DetectsHardcodedDepthList()
    {
        // 守卫自检（正反两面）：判据必须能认出被禁止的写法，
        // 且不能把「从枚举取名字」的正常写法误判为违规。
        Assert.Matches(HardcodedDepthArray,
            "CmbColorDepth.ItemsSource = new[] { \"RGB565\", \"ARGB8888\", \"Indexed8\" };");
        Assert.Matches(HardcodedDepthArray, "var x = new[] { \"ARGB8888\" };");
        Assert.DoesNotMatch(HardcodedDepthArray,
            "CmbColorDepth.ItemsSource = Enum.GetNames(typeof(Hmi.Hardware.Contracts.ColorDepth));");
    }

    [Fact]
    public void GuardItself_CommentStrippingKeepsRuleFromFailingOnItsOwnDocs()
    {
        // 本规则的说明文字里必然出现被禁的字面量（上面那条 <summary> 就写着）。
        // 剥注释是判据能长期存活的前提 —— 否则下一个人只会看到一条莫名其妙的红，然后把注释删掉。
        const string docOnly =
            "/// <summary>此前是硬编码的 new[] { \"RGB565\", \"ARGB8888\", \"Indexed8\" }。</summary>";

        Assert.Matches(HardcodedDepthArray, docOnly); // 样本有效
        Assert.DoesNotMatch(HardcodedDepthArray, SourceScanner.StripComments(docOnly));
    }

    // ── ③ 设备 JSON 解析不再手写映射表 ──

    private const string ConfigDrivenTargetRelativePath = "src/Hmi.Hardware.Loader/ConfigDrivenTarget.cs";

    private static readonly Regex HandRolledEnumSwitch =
        new(@"""(RGB565|ARGB8888|Indexed8|RGB666|MIPI_DSI|I80|CanBus|Bluetooth)""\s*=>", RegexOptions.Compiled);

    [Fact]
    public void ConfigDrivenTarget_DoesNotHandRollEnumNameMappings()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, ConfigDrivenTargetRelativePath);
        var code = SourceScanner.ReadCode(path);

        var matches = HandRolledEnumSwitch.Matches(code);
        Assert.True(matches.Count == 0,
            "ConfigDrivenTarget 里又出现了手写的「字符串 → 枚举」映射：\n"
            + string.Join("\n", matches.Select(x => x.Value))
            + "\n请改用 Enum.TryParse + Enum.IsDefined —— 手写清单在枚举增删成员时会静默过期。");
    }

    [Fact]
    public void GuardItself_DetectsHandRolledEnumSwitch()
    {
        Assert.Matches(HandRolledEnumSwitch, "\"ARGB8888\" => ColorDepth.ARGB8888,");
        Assert.Matches(HandRolledEnumSwitch, "\"MIPI_DSI\" => DisplayInterface.MIPI_DSI,");
        Assert.DoesNotMatch(HandRolledEnumSwitch,
            "Enum.TryParse<ColorDepth>(value, ignoreCase: true, out var parsed)");
    }

    // ── 扫描器自身 ──

    [Fact]
    public void SourceScanner_FindsProductSourcesUnderRepoRoot()
    {
        Assert.True(File.Exists(Path.Combine(SourceScanner.RepoRoot, "HmiEditor.slnx")));

        var sources = SourceScanner.ProductSources();
        Assert.True(sources.Count > 100, $"只枚举到 {sources.Count} 个产品源码文件");
        Assert.DoesNotContain(sources, f => f.Contains("/obj/") || f.Contains("/bin/"));
    }

    [Fact]
    public void SourceScanner_StripComments_HandlesAllThreeCommentForms()
    {
        const string sample =
            "// 行注释 new[] { \"RGB565\" }\n"
            + "/* 块注释 new[] { \"RGB565\" } */\n"
            + "var keep = 1; // 尾注释\n";

        var stripped = SourceScanner.StripComments(sample);

        Assert.DoesNotContain("RGB565", stripped);
        Assert.Contains("var keep = 1;", stripped);
    }

    /// <summary>
    /// 曾经的实现是「先正则替换块注释、再替换行注释」，被这一行注释打崩：
    /// 注释正文里的 <c>firmware/*/firmware.json</c> 含 <c>/*</c>，
    /// 正则会一路吞到下一个 <c>*/</c>，把中间上百行代码一起删掉。
    /// 这条用例就是那个缺陷的回归钉子。
    /// </summary>
    [Fact]
    public void SourceScanner_StripComments_DoesNotEatCodeOnGlobLikeComment()
    {
        const string sample =
            "/// <summary>已加载的固件型号（由 firmware/*/firmware.json 生成）。</summary>\n"
            + "public void Load() { }\n"
            + "// 型号由固件决定：扫描 firmware/*/firmware.json 生成固定型号列表。\n"
            + "public int Count => 3;\n";

        var stripped = SourceScanner.StripComments(sample);

        Assert.Contains("public void Load() { }", stripped);
        Assert.Contains("public int Count => 3;", stripped);
        Assert.DoesNotContain("firmware.json", stripped);
    }

    [Fact]
    public void SourceScanner_StripComments_KeepsStringLiteralsIntact()
    {
        const string sample =
            "var a = \"http://example.com//x\";\n"
            + "var b = @\"C:\\path//name\";\n"
            + "var c = '\\'';\n"
            + "// var d = 1;\n";

        var stripped = SourceScanner.StripComments(sample);

        Assert.Contains("\"http://example.com//x\"", stripped);
        Assert.Contains("@\"C:\\path//name\"", stripped);
        Assert.Contains("'\\''", stripped);
        Assert.DoesNotContain("var d", stripped);
    }

    /// <summary>
    /// 全局不变量：剥注释后<b>长度与行数都不变</b>（注释字符换成空格、换行原样保留）。
    /// 这是能覆盖整库的判据 —— 任何「吞掉一段代码」的缺陷都会立刻打破它。
    /// </summary>
    [Fact]
    public void SourceScanner_StripComments_PreservesLengthAndLineCountOnWholeCorpus()
    {
        var sources = SourceScanner.ProductSources();
        Assert.True(sources.Count > 100, $"只枚举到 {sources.Count} 个产品源码文件，覆盖度判据失效");

        var offenders = new List<string>();
        foreach (var file in sources)
        {
            var raw = File.ReadAllText(file);
            var stripped = SourceScanner.StripComments(raw);

            if (stripped.Length != raw.Length)
                offenders.Add($"{SourceScanner.Relative(file)}: 长度 {raw.Length} → {stripped.Length}");

            if (stripped.Split('\n').Length != raw.Split('\n').Length)
                offenders.Add($"{SourceScanner.Relative(file)}: 行数 {raw.Split('\n').Length} → {stripped.Split('\n').Length}");
        }

        Assert.True(offenders.Count == 0,
            "剥注释破坏了「长度与行数不变」不变量 —— 说明有代码被当成注释吃掉了：\n"
            + string.Join("\n", offenders));
    }
}
