using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 「系统动作 → 目标屏」候选表（<see cref="ScreenNavTargets"/>）与两个消费方的一致性：
/// ① 固件 <c>hmi_actions.c</c> 里那三份数组是它的**孪生副本**（逐项 + 顺序比对）；
/// ② 导出的模拟器 <c>main.c</c> 用**生成期解析结果**（直呼屏名），不是运行时再猜一次。
///
/// <para><b>为什么必须有孪生守卫：</b>设备端在运行时模糊匹配候选名、模拟器在生成期解析 ——
/// 两边名字表一旦不同步，就会出现「同一块工程：模拟器切到报警屏、设备切到另一块（或原地不动）」
/// 这种极难归因的不一致。名字表是**约定**，约定只能有一份权威 + 一条同步守卫。</para>
/// </summary>
public class ScreenNavTargetsTests
{
    // ── 孪生守卫：固件里的候选数组 == C# 的候选表 ──

    [Fact]
    public void FirmwareCandidateTables_MatchSingleSource_ItemByItem()
    {
        var path = Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime/hmi_actions.c");
        Assert.True(File.Exists(path), $"未找到固件动作实现：{path}");
        var source = File.ReadAllText(path);

        // 按文件里出现的顺序取出三份 candidates[] 数组（login → alarm → trends）
        var parsed = Regex.Matches(source, @"const char\*\s+candidates\[\]\s*=\s*\{([^}]*)\}")
            .Select(m => Regex.Matches(m.Groups[1].Value, "\"((?:[^\"\\\\]|\\\\.)*)\"")
                .Select(x => Regex.Unescape(x.Groups[1].Value))
                .ToList())
            .ToList();

        Assert.True(parsed.Count == ScreenNavTargets.All.Count,
            $"固件里有 {parsed.Count} 份 candidates[] 数组，单一来源有 {ScreenNavTargets.All.Count} 份 —— "
            + "新增/删除系统动作时两边都要动");

        for (var i = 0; i < parsed.Count; i++)
        {
            Assert.Equal(ScreenNavTargets.All[i].ToList(), parsed[i]);   // 逐项 + **顺序**
        }
    }

    /// <summary>
    /// 守护卫自己：解析器必须能解析出东西，否则"两份空表相等"会假绿。
    /// （用正则从 C 源码里取字符串数组，判据取自源码文本 ——务必自检。）
    /// </summary>
    [Fact]
    public void FirmwareCandidateTables_ParserIsNotEmpty()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime/hmi_actions.c"));
        var parsed = Regex.Matches(source, @"const char\*\s+candidates\[\]\s*=\s*\{([^}]*)\}");
        Assert.Equal(3, parsed.Count);   // 三份：login / alarm / trends
        Assert.All(ScreenNavTargets.All, c => Assert.NotEmpty(c));
    }

    // ── 生成期解析语义 ──

    [Theory]
    // 候选顺序优先于屏幕顺序：alarm 在前，所以即使 报警列表 先建也选 alarm
    [InlineData("报警列表,alarm", "alarm")]
    [InlineData("alarm,报警列表", "alarm")]
    [InlineData("报警列表", "报警列表")]
    [InlineData("AlarmList", "AlarmList")]
    // 一个都不匹配 ⇒ null（生成器据此只写日志、不切屏）
    [InlineData("主画面,设置", null)]
    public void Resolve_PicksFirstCandidateByCandidateOrder(string screenNames, string? expected)
    {
        var project = new HmiProject { Name = "Nav" };
        var i = 0;
        foreach (var name in screenNames.Split(','))
            project.Screens.Add(new HmiScreen { Id = $"s{i++}", Name = name, Width = 800, Height = 480 });

        Assert.Equal(expected, ScreenNavTargets.Resolve(project, ScreenNavTargets.Alarm));
    }

    /// <summary>匹配是**精确**的（与固件 <c>strcmp</c> 一致），不做大小写/包含匹配。</summary>
    [Fact]
    public void Resolve_IsExactMatchOnly()
    {
        var project = new HmiProject { Name = "Nav" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "AlarmScreen", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "alarm ", Width = 800, Height = 480 });

        Assert.Null(ScreenNavTargets.Resolve(project, ScreenNavTargets.Alarm));
    }

    // ── 生成侧：模拟器直呼解析出的屏名 ──

    [Fact]
    public void ExportedSimulator_MainC_SwitchesToResolvedScreen()
    {
        var project = new HmiProject { Name = "Nav", LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "主页", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "报警列表", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s3", Name = "趋势图", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s4", Name = "登录屏", Width = 800, Height = 480 });
        var screen = project.Screens[0];
        var btn = new HmiControl { Id = "c1", Name = "B", Type = HmiControlType.Button };
        btn.Actions.Add(HmiAction.ShowAlarms);
        btn.Actions.Add(HmiAction.ShowTrends);
        btn.Actions.Add(HmiAction.Login);
        screen.Controls.Add(btn);

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("nav-screens");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            // 三个动作各自切到生成期解析出的屏（而不是把候选表搬到运行时再猜）
            CSource.AssertCode(mainC, "void hmi_show_alarms(void)");
            CSource.AssertCall(mainC, "hmi_screen_switch(\"报警列表\")");
            CSource.AssertCall(mainC, "hmi_screen_switch(\"趋势图\")");
            CSource.AssertCall(mainC, "hmi_screen_switch(\"登录屏\")");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>工程里没有对应的屏时：只写日志、不切屏（与固件 try_switch 失败后的行为一致）。</summary>
    [Fact]
    public void ExportedSimulator_NoTargetScreen_LogsInsteadOfSwitching()
    {
        var project = new HmiProject { Name = "Nav", LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "主页", Width = 800, Height = 480 });
        var btn = new HmiControl { Id = "c1", Name = "B", Type = HmiControlType.Button };
        btn.Actions.Add(HmiAction.ShowAlarms);
        project.Screens[0].Controls.Add(btn);

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("nav-screens-missing");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            CSource.AssertCode(mainC, "fprintf(stderr, \"[sim] hmi_show_alarms: 工程内没有报警屏\\n\")");
            // 报警屏那块不该出现切屏调用（趋势/登录另有各自解析结果）
            var alarmsBody = ExtractFunctionBody(mainC, "hmi_show_alarms");
            Assert.DoesNotContain("hmi_screen_switch(", alarmsBody);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 辅助 ──

    /// <summary>取某个 C 函数的函数体文本（从定义行到配平的右花括号）。</summary>
    private static string ExtractFunctionBody(string source, string funcName)
    {
        var start = source.IndexOf($"void {funcName}(void)", StringComparison.Ordinal);
        Assert.True(start >= 0, $"没找到函数 {funcName}");
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return source[open..(i + 1)];
            }
        }
        throw new InvalidOperationException($"{funcName} 的花括号不配平");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");
    }
}
