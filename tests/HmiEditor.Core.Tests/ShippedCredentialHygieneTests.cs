using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **出厂默认凭据的卫生守卫**（2026-10-11，阶段 296）。
///
/// <para><b>背景</b>：设备侧板级默认口令 `firmware/t113/app/runtime/runtime_config.h` 里
/// <c>HMI_MQTT_PASS "hmi123"</c> 是硬编码的——它是**出厂演示默认值**（板的 host 测试就用 hmi/hmi123），
/// 但现场部署必须改。此前没有任何标注、也没有判据 ⇒ 下一个人看不出它是默认值，
/// 也无法阻止**新增**第二个硬编码凭据。</para>
///
/// <para><b>两条腿</b>：① **必须有** —— 该口令行的邻近注释里必须有"部署必改"这类警示；
/// ② **不许有** —— 设备源码里不得再出现**其它**硬编码凭据宏（`*PASS*`/`*PASSWORD*`/`*SECRET*`/`*TOKEN*` = "…"）。
/// 第 ② 条自带**面自检**：同一张正则必须在同一份文件里真的命中 `HMI_MQTT_PASS`（否则正则是死的，
/// 该条会退化成永远通过）。</para>
///
/// <para><b>已知边界</b>：只扫 <c>firmware/t113/app/</c>（设备侧出厂默认）；
/// 编辑器侧的凭据是运行期存储、不在本判据面上。</para>
/// </summary>
public class ShippedCredentialHygieneTests
{
    private readonly ITestOutputHelper _o;

    public ShippedCredentialHygieneTests(ITestOutputHelper o) => _o = o;

    private const string CredentialDefineFile = "firmware/t113/app/runtime/runtime_config.h";

    private static readonly Regex CredentialDefine = new(
        @"^\s*#\s*define\s+(?<name>[A-Za-z0-9_]*(?:PASS|PASSWORD|SECRET|TOKEN)[A-Za-z0-9_]*)\s+""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static string RepoFile(string relative) =>
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void ShippedDefaultPassword_CarriesADeploymentWarning()
    {
        var path = RepoFile(CredentialDefineFile);
        Assert.True(File.Exists(path), $"找不到板级默认配置文件：{CredentialDefineFile}");

        var lines = File.ReadAllLines(path);
        var idx = Array.FindIndex(lines, l => l.Contains("HMI_MQTT_PASS", StringComparison.Ordinal));
        Assert.True(idx >= 0, $"{CredentialDefineFile} 里找不到 HMI_MQTT_PASS 定义 —— 口令改成别处了？");

        // 窗口取该行往上 12 行（含定义行自身的行尾注释）
        var from = Math.Max(0, idx - 12);
        var window = string.Join('\n', lines[from..(idx + 1)]);

        JudgeReadings.Write(_o, "出厂默认口令的标注（阶段 296）", new[]
        {
            $"面：{CredentialDefineFile} 的 HMI_MQTT_PASS 行 ±12 行内必须有'部署必改'警示",
            "不许有：设备源码里不得再出现其它硬编码凭据宏（面自检见同文件的正则命中）",
        });

        Assert.True(window.Contains("部署必改", StringComparison.Ordinal),
            "出厂默认口令缺'部署必改'标注 —— 现场很容易把它当成生产凭据照搬。"
            + $"（{CredentialDefineFile} 第 {idx + 1} 行附近）");
    }

    [Fact]
    public void NoOtherHardcodedCredential_InDeviceSources()
    {
        var appDir = RepoFile("firmware/t113/app");
        Assert.True(Directory.Exists(appDir), $"找不到设备源码目录：{appDir}");

        var files = Directory.EnumerateFiles(appDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".h", StringComparison.Ordinal) || f.EndsWith(".c", StringComparison.Ordinal))
            .ToList();

        var offenders = new List<string>();
        var face = 0;   // 面自检：正则必须真的命中那个已知的凭据宏
        var knownFile = Path.GetFullPath(RepoFile(CredentialDefineFile));

        foreach (var file in files)
        {
            var code = SourceScanner.ReadCode(file);
            foreach (Match m in CredentialDefine.Matches(code))
            {
                if (Path.GetFullPath(file) == knownFile && m.Groups["name"].Value == "HMI_MQTT_PASS")
                {
                    face++;
                    continue;   // 它由上面那条"必须有标注"的判据负责
                }
                offenders.Add($"{SourceScanner.Relative(file)}: {m.Groups["name"].Value} = \"{m.Groups["value"].Value}\"");
            }
        }

        Assert.True(face >= 1,
            "面自检失败：正则在这份已知含 `HMI_MQTT_PASS` 的文件里一条都没命中 —— "
            + "正则/扫描面失效，本判据会退化成永远通过。");

        Assert.True(offenders.Count == 0,
            "设备源码里出现**其它**硬编码凭据（现场部署要改，且容易随代码泄露）："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders)
            + Environment.NewLine + "修法：挪到运行时下发（/mnt/UDISK/hmi/comm_config.ini）或命令行/环境变量；"
            + "确属出厂默认值的，照 HMI_MQTT_PASS 的形状加注释并在本判据里显式登记。");
    }
}
