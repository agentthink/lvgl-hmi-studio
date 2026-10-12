using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 设备侧构建的「生成头闭合性」守卫：<c>app/</c> 里**无条件**引用的生成头，
/// 必须在设备生成物目录 <c>firmware/t113/generated/</c> 里真实存在。
///
/// <para><b>为什么需要它（2026-10-10 实机踩到）：</b>
/// <c>firmware/t113/app/comm/mqtt_acl_contract.c</c> 无条件
/// <c>#include "generated_security.h"</c>，而这份头**只有模拟器导出路径**
/// （<c>SimulatorProjectExporter</c>）会生成；设备生成物目录里没有它 —— 于是从 Tina SDK
/// 出包时 <c>make</c> 直接 <c>fatal error: generated_security.h: No such file or directory</c>，
/// 设备固件<b>根本构建不出来</b>。而 CI 是绿的，
/// 因为 <c>firmware/t113/tests/run_host_tests.sh</c> 的编译行带了 <c>-Itests/fixtures</c>，
/// 那份 fixture 恰好有同名头 ⇒ **真机 SDK 构建断了，CI 看不见**。</para>
///
/// <para><b>判据：</b>每条无条件的 <c>#include "generated_*.h"</c> 都要有对应文件；
/// 被 <c>__has_include("同名头")</c> 包住的（如 <c>comm_core.c</c> 的 generated_comm.h）
/// 属**可选**依赖，不要求文件存在。</para>
///
/// <para><b>为什么还要断言"至少扫到一条无条件 include"：</b>否则哪天最后一条也被改成
/// <c>__has_include</c>，本守卫会退化成「检查不到任何东西却永远绿」——判据失效本身要被钉住。</para>
/// </summary>
public class FirmwareGeneratedIncludeClosureTests
{
    private const string DeviceGeneratedDir = "firmware/t113/generated";

    private static readonly Regex IncludePattern =
        new(@"^\s*#\s*include\s+""(?<name>generated_[A-Za-z0-9_]+\.h)""", RegexOptions.Compiled);

    private static readonly Regex HasIncludePattern =
        new(@"__has_include\s*\(\s*""(?<name>[^""]+)""\s*\)", RegexOptions.Compiled);

    [Fact]
    public void EveryUnconditionalGeneratedInclude_ExistsInDeviceGeneratedDir()
    {
        var appDir = Path.Combine(SourceScanner.RepoRoot, "firmware/t113/app");
        Assert.True(Directory.Exists(appDir), $"未找到设备侧源码目录：{appDir}");

        var generatedDir = Path.Combine(SourceScanner.RepoRoot, DeviceGeneratedDir);
        Assert.True(Directory.Exists(generatedDir), $"未找到设备生成物目录：{generatedDir}");

        var missing = new List<string>();
        var unconditional = new List<string>();

        foreach (var file in EnumerateCSources(appDir))
        {
            // 先剥注释：讲解 include 的注释里会出现同一串字面量（本仓多次踩过这类假命中）
            var lines = SourceScanner.ReadCode(file).Split('\n');
            var rel = SourceScanner.Relative(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var m = IncludePattern.Match(lines[i]);
                if (!m.Success) continue;

                var name = m.Groups["name"].Value;
                if (IsGuardedByHasInclude(lines, i, name)) continue;   // 可选依赖

                unconditional.Add($"{rel}:{i + 1} -> {name}");
                if (!File.Exists(Path.Combine(generatedDir, name)))
                {
                    missing.Add($"{rel}:{i + 1} 无条件 include \"{name}\"，"
                                + $"但 {DeviceGeneratedDir}/{name} 不存在");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "设备侧无条件引用的生成头在 " + DeviceGeneratedDir + " 里缺失（SDK 出包会 fatal error 构建不出来）：\n  "
            + string.Join("\n  ", missing));

        Assert.True(unconditional.Count > 0,
            "没扫到任何无条件的 generated_*.h include —— 守卫的扫描面可能已失效"
            + "（判据会退化成永远绿），请检查 app/ 是否已不被扫描或正则失效");
    }

    /// <summary>
    /// 该 include 是否被紧邻前几行的 <c>__has_include(同一文件名)</c> 包住。
    /// 只看前 <see cref="LookBack"/> 行：避免把同文件里**另一处**的守卫误当成这一处的。
    /// </summary>
    private static bool IsGuardedByHasInclude(string[] lines, int index, string name)
    {
        const int LookBack = 6;
        for (var j = Math.Max(0, index - LookBack); j < index; j++)
        {
            foreach (Match m in HasIncludePattern.Matches(lines[j]))
            {
                if (m.Groups["name"].Value == name) return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> EnumerateCSources(string root) =>
        Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".c", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".h", StringComparison.OrdinalIgnoreCase))
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .OrderBy(f => f, StringComparer.Ordinal);
}
