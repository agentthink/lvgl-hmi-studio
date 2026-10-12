using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **门禁的负载记账**（阶段 223，落实登记 `#45`/阶段 59 立的规矩）。
///
/// <para><b>为什么要有这条判据</b>：阶段 59 立了规矩「凡引用墙钟须同时记 <c>/proc/loadavg</c>，
/// 没有负载读数的墙钟只能同批内部比较」—— 起因是门禁日志从来没记过负载，而同一批连跑的负载
/// 从 <c>1.89</c> 爬到 <c>5.09</c>、UI 段随之从 27 s 飘到 36 s，于是那次「UI 段 141 秒」的异常
/// **事后无从归因**（登记 `#45` 仍未闭环的那一半）。</para>
///
/// <para><b>规矩不落到工具里就会忘</b>：这条守卫把三件事钉住 —— ① `ci-gate.sh` 必须真的记
/// （三处：开始 / 测试前 / 测试后）；② `gate-readings.py` 必须把读数显示出来（否则记了也没人看）；
/// ③ 用一个**合成日志**端到端跑一遍读数脚本，断言它确实把三个负载值打了出来。</para>
/// </summary>
public class GateLoadavgBookkeepingTests
{
    private static string RepoRoot => SourceScanner.RepoRoot;

    private static string GateScript => File.ReadAllText(Path.Combine(RepoRoot, "scripts", "ci-gate.sh"));
    private static string ReadingsScript => File.ReadAllText(Path.Combine(RepoRoot, "scripts", "gate-readings.py"));

    [Fact]
    public void GateScript_RecordsLoadAverageAtThreePoints()
    {
        var text = GateScript;

        Assert.Contains("/proc/loadavg", text, StringComparison.Ordinal);

        foreach (var point in new[] { "开始", "测试前", "测试后" })
            Assert.Contains($"负载 loadavg({point})", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingsScript_SurfacesTheLoadAverage()
    {
        Assert.Contains("loadavg", ReadingsScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("负载", ReadingsScript, StringComparison.Ordinal);
    }

    /// <summary>
    /// **端到端**：造一份带负载标记的最小日志，跑真脚本，断言三个值都出现在输出里
    /// （记了不显示 = 白记；显示不出来 = 事后照样无从归因）。
    /// </summary>
    [Fact]
    public void ReadingsScript_PrintsTheLoadAverageForASyntheticLog()
    {
        // ⚠ 不许用 Path.GetTempPath()（TestTempDirUsageGuardTests 当场红过）—— 走 TestTempDir 的探测/回退链
        var temp = TestTempDir.NewFilePath("gate-loadavg", ".log");
        // ⚠ 合成日志要**过得了**读数脚本自己的"关键项"检查（GATE_EXIT / 脚本数 / 路径卫生 /
        //   9 条程序集）—— 否则脚本 exit 1，这条端到端判据就变成"在测它的自检"。
        var lines = new List<string>
        {
            "负载 loadavg(开始): 1.11 2.22 3.33",
            "===== [1] bash -n 脚本语法检查 =====",
            "✅ 29 个脚本语法正确",
            "已检查 700 个文件",
            "负载 loadavg(测试前): 2.22 3.33 4.44",
            "===== [5] dotnet test (Release) —— 大队 =====",
        };
        for (var i = 1; i <= 9; i++)
            lines.Add($"已通过! - 失败: 0，通过: {i}，已跳过: 0，总计: {i}，持续时间: 1 s - Asm{i}.dll (net10.0)");
        lines.Add("负载 loadavg(测试后): 3.33 4.44 5.55");
        lines.Add("===== [7] 收尾 =====");
        lines.Add("GATE_EXIT=0");

        File.WriteAllText(temp, string.Join("\n", lines));

        try
        {
            var psi = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = RepoRoot,
            };
            psi.ArgumentList.Add("scripts/gate-readings.py");
            psi.ArgumentList.Add(temp);

            using var proc = Process.Start(psi)!;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60_000);

            Assert.True(proc.ExitCode == 0, $"读数脚本退出码 {proc.ExitCode}\n{stdout}\n{stderr}");
            Assert.Contains("负载", stdout, StringComparison.Ordinal);
            Assert.Contains("1.11", stdout, StringComparison.Ordinal);
            Assert.Contains("2.22", stdout, StringComparison.Ordinal);
            Assert.Contains("3.33", stdout, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// 负控用（也在守卫里钉住）：读数脚本把负载当成**可选**项 —— 老日志没有这些行时**不许红**
    /// （否则历史日志全都不可读）。这里直接断言脚本里没有把负载列进 `missing()` 的必检项。
    /// </summary>
    [Fact]
    public void LoadAverage_IsOptionalInTheDigest()
    {
        var missingBody = Regex.Match(ReadingsScript, @"def missing\(s: dict\) -> list:(.*?)\n\n", RegexOptions.Singleline);
        Assert.True(missingBody.Success, "找不到 missing() 的定义 —— 读数脚本结构变了，这条守卫要同步");

        Assert.DoesNotContain("loadavg", missingBody.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("负载", missingBody.Groups[1].Value, StringComparison.Ordinal);
    }
}
