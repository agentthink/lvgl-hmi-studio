using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **固件诊断日志的卫生守卫**（2026-10-11，阶段 299）。
///
/// <para><b>背景</b>：台账 §5-③ 记着「诊断 DBG 日志（<c>main.c</c> / <c>lcd_sunxifb.c</c> / <c>sunxifb.c</c>）待清理」。
/// 清理前的实际形态：19 处 <c>HMI_LOG_DEBUG("DBG: …")</c>（**已被级别闸门管住**，只是消息里带冗余前缀）
/// ＋ <c>sunxifb.c</c> 里 **6 处裸输出**（2 处 <c>fprintf(stderr, "DBG: …")</c> ＋ 4 处 <c>printf("Turn on …")</c>）
/// —— 后者**绕过级别闸门**：设备端默认门槛是 Warning，这些却照样打到 stderr/stdout。</para>
///
/// <para><b>清理后的形状（本判据守的就是它）</b>：设备日志一律走
/// <c>hmi_log.h</c> 的分级宏（级别可见、可被工程设置裁剪），消息里不再自带 <c>"DBG:"</c> 前缀；
/// 唯一的裸输出例外是 <c>sunxifb.c</c> 里那份 FPS 统计 —— 它在 <c>#if LV_USE_SUNXIFB_DEBUG</c>
/// 编译期开关内（要看得自己打开），<c>perror(...)</c> 这类**错误**上报不受限。</para>
/// </summary>
public class FirmwareDebugLogHygieneTests
{
    private readonly ITestOutputHelper _o;

    public FirmwareDebugLogHygieneTests(ITestOutputHelper o) => _o = o;

    /// <summary>裸输出：<c>printf(</c> / <c>fprintf(stderr,</c>。排除 <c>snprintf(</c> 等（负向后顾）与 <c>HMI_LOG_*(</c>。</summary>
    private static readonly Regex RawStdout = new(
        @"(?<![A-Za-z0-9_])(?:f?printf)\s*\(", RegexOptions.Compiled);

    private static string RepoFile(string relative) =>
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Code(string relative) =>
        SourceScanner.MaskLiterals(SourceScanner.ReadCode(RepoFile(relative)));

    private const string SunxiFb = "firmware/t113/app/drivers/sunxifb.c";
    private const string LcdSunxiFb = "firmware/t113/app/drivers/lcd_sunxifb.c";
    private const string MainC = "firmware/t113/app/main.c";

    [Fact]
    public void DeviceSources_HaveNoDbgPrefixedDiagnostics()
    {
        var appDir = RepoFile("firmware/t113/app");
        Assert.True(Directory.Exists(appDir), $"找不到设备源码目录：{appDir}");

        var hits = new List<string>();
        var logCalls = 0;

        foreach (var file in Directory.EnumerateFiles(appDir, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".c", StringComparison.Ordinal) || f.EndsWith(".h", StringComparison.Ordinal)))
        {
            var code = SourceScanner.MaskLiterals(SourceScanner.ReadCode(file));
            var rel = SourceScanner.Relative(file);

            // ⚠ 掩字符串后再找 `"DBG:` 是找不到的（引号内容被抹掉）⇒ 这里在**原始**文本里找，
            //    因为"消息文本里带 DBG 前缀"本身就是被清掉的那个形状。
            var raw = SourceScanner.ReadCode(file);
            foreach (Match m in Regex.Matches(raw, "\"DBG:"))
            {
                var line = raw.Take(m.Index).Count(ch => ch == '\n') + 1;
                hits.Add($"{rel}:{line}");
            }

            logCalls += Regex.Matches(code, @"\bHMI_LOG_(?:DEBUG|INFO|WARN|ERR)\s*\(").Count;
        }

        JudgeReadings.Write(_o, "固件诊断日志卫生（阶段 299）", new[]
        {
            "不许有：设备源码里不得再出现消息自带 \"DBG:\" 前缀的诊断（一律用 hmi_log.h 的分级宏）",
            $"反向腿：设备源码里分级日志调用共 {logCalls} 处（下界 20）—— 防「日志被一刀删光」混过去",
            "裸输出限定：sunxifb.c 的 printf/fprintf(stderr) ≤ 1（那份 LV_USE_SUNXIFB_DEBUG 内的 FPS 统计）；"
            + "lcd_sunxifb.c 与 main.c 必须为 0；perror(...) 不受限",
        });

        Assert.True(hits.Count == 0,
            "设备源码里又出现了自带 \"DBG:\" 前缀的诊断日志 —— 它要么游离在级别闸门之外（裸 printf/fprintf），"
            + "要么与分级宏语义重复。请改用 HMI_LOG_DEBUG/INFO（见 runtime/hmi_log.h）："
            + Environment.NewLine + string.Join(Environment.NewLine, hits));

        Assert.True(logCalls >= 20,
            $"反向腿失败：整个设备源码里只找到 {logCalls} 处分级日志调用（下界 20）—— "
            + "要么日志被一刀删光了（那不是清理，是把现场排查能力删掉），要么本判据的匹配形状没对上。");
    }

    [Fact]
    public void FramebufferDriver_HasNoUnconditionalDebugOutput()
    {
        var sunxi = Code(SunxiFb);
        var lcd = Code(LcdSunxiFb);
        var main = Code(MainC);

        var sunxiRaw = RawStdout.Matches(sunxi).Count;
        var lcdRaw = RawStdout.Matches(lcd).Count;
        var mainRaw = RawStdout.Matches(main).Count;
        var sunxiStderr = Regex.Matches(sunxi, @"fprintf\s*\(\s*stderr").Count;
        var hasGatedFps = sunxi.Contains("LV_USE_SUNXIFB_DEBUG", StringComparison.Ordinal);
        var perror = Regex.Matches(sunxi, @"\bperror\s*\(").Count;

        JudgeReadings.Write(_o, "裸调试输出（阶段 299）", new[]
        {
            $"sunxifb.c: 裸 printf/fprintf(stderr)={sunxiRaw}（上限 1，且需有 LV_USE_SUNXIFB_DEBUG 开关：{hasGatedFps}）"
            + $"；fprintf(stderr)={sunxiStderr}（应为 0）；perror={perror}（不受限）",
            $"lcd_sunxifb.c: 裸输出={lcdRaw}（应为 0）；main.c: 裸输出={mainRaw}（应为 0）",
        });

        Assert.True(lcdRaw == 0 && mainRaw == 0,
            $"lcd_sunxifb.c={lcdRaw} / main.c={mainRaw} 处裸 printf/fprintf(stderr) —— 它们绕过 HMI_LOG_LEVEL 闸门，"
            + "默认门槛（Warning）下也会打到 stderr/stdout。请改用 HMI_LOG_DEBUG/INFO。");

        Assert.True(sunxiStderr == 0,
            $"sunxifb.c 里还有 {sunxiStderr} 处裸 fprintf(stderr, …) —— 同上，请改用 HMI_LOG_DEBUG。"
            + "（错误路径请用 perror 或 HMI_LOG_ERR——本次清理只动诊断打印。）");

        Assert.True(sunxiRaw <= 1,
            $"sunxifb.c 里还有 {sunxiRaw} 处裸 printf/fprintf(stderr)（上限 1：那份 LV_USE_SUNXIFB_DEBUG 内的 FPS 统计）。");

        Assert.True(hasGatedFps,
            "sunxifb.c 里那处保留的裸 printf 必须受编译期开关 LV_USE_SUNXIFB_DEBUG 管着 —— 找不到该开关，"
            + "说明豁免的理由不成立了，应当把它也改成分级日志。");
    }
}
