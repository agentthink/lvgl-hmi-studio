using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="JudgeReadings"/> 自己的判据（登记表 #42 的收口）。
///
/// <para><b>为什么这个出口也要有判据</b>：它的失效方式是**静默**的 —— 读数写不出去、
/// 或者某个判据类忘了接出口，测试**照样全绿**。这正是 #42 要解决的问题的镜像：
/// 「绿的时候拿不到读数」不会以任何形式自己报警。所以本文件盯三件事：</para>
/// <list type="number">
///   <item><b>文件出口真的落盘</b>（正控：内容与 scope 头都在）；</item>
///   <item><b>不该落盘时不落盘</b>（负控：空读数不留空块；未给路径时不动文件系统）；</item>
///   <item><b>每个动作面判据类都接了出口</b>（静态守卫，**枚举整个目录**而不是写死 4 个文件名 ——
///     新增一个 <c>*InvocationUiTests.cs</c> 会自动进面）。</item>
/// </list>
///
/// <para><b>fail-soft 也是一条判据</b>：路径不可写时 <see cref="JudgeReadings.Write"/> 必须不抛 ——
/// 否则「读数」这个纯证词会变成新的失败源，门禁红的原因就失真了。</para>
/// </summary>
public class JudgeReadingsTests
{
    /// <summary>捕获写入的 <see cref="ITestOutputHelper"/> 桩：判据要验"两条出口内容同源"。</summary>
    private sealed class CapturingOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args)
            => Lines.Add(string.Format(format, args));
    }

    [Fact]
    public void Write_LandsInTheFileWithAScopeHeader()
    {
        var path = TestTempDir.NewFilePath("readings", ".txt");
        try
        {
            Assert.False(File.Exists(path), "前提：NewFilePath 不创建文件");

            JudgeReadings.Write(null, "自检用 scope",
                new[] { "    面：解析 3 个", "    实测：真点 2 个" }, path);

            Assert.True(File.Exists(path), "给了路径就该落盘 —— 这是这个出口存在的唯一理由");
            var text = File.ReadAllText(path);
            Assert.Contains("=== 自检用 scope ===", text);
            Assert.Contains("    面：解析 3 个", text);
            Assert.Contains("    实测：真点 2 个", text);
        }
        finally { TestTempDir.DeleteFile(path); }
    }

    /// <summary>
    /// **负控（真分支，不是同义反复）**：空读数不建文件。
    /// 这条分支确实会被走到 —— 某个判据类在自己那条路径上一行读数都没攒（例如整张面都在跳过表里），
    /// 那时文件里只该少一段，不该多出一段只有标题的 <c>=== X ===</c>。
    /// </summary>
    [Fact]
    public void Write_WithNoReadings_LeavesNoEmptyBlockBehind()
    {
        var path = TestTempDir.NewFilePath("readings-empty", ".txt");
        try
        {
            JudgeReadings.Write(null, "空 scope", Array.Empty<string>(), path);

            Assert.False(File.Exists(path),
                "空读数不该建文件 —— 否则读数文件里会堆满只有标题的空块，把真读数淹掉");
        }
        finally { TestTempDir.DeleteFile(path); }
    }

    /// <summary>
    /// **负控**：默认路径（没有 output、没有文件路径）必须**零副作用且不抛**。
    /// 这是门禁里**实际**走的那条路 —— 门禁不设 <c>HMI_READINGS_OUT</c>、也不开 detailed logger。
    ///
    /// <para><b>⚠ 不许用"数一遍临时目录里的文件个数"当判据</b>（2026-09-21 实测被这条打红）：
    /// <c>TestTempDir.Root</c> 是**整个测试进程共享**的目录，与本用例并行跑的其他用例随时往里加文件，
    /// 于是「前后两次 <c>GetFiles().Length</c> 相等」会随机不成立。实测门禁里报
    /// <c>Expected: 1423 / Actual: 1424</c>（单跑全绿、进门禁就红）。
    /// <b>这是判据自身的缺陷，不是被测对象的问题</b> —— 对共享资源做前后快照比较，
    /// 判据就不自治，并发再大都会误红。</para>
    ///
    /// <para>改成**按内容找标记**：这段文字只有本用例会写，并行用例造不出它，
    /// 所以并发再大也不会误红；而默认路径一旦真的落了盘，它必然被找到。</para>
    ///
    /// <para><b>诚实边界（三条）</b>：① 扫描面是 <c>TestTempDir.Root</c> 的**顶层**
    /// （刻意不递归 —— <c>Root</c> 会按 <c>HMI_TEST_TMPDIR → TMPDIR → Path.GetTempPath()
    /// → 仓库 .hmi-test-tmp/</c> 逐个探测，递归遍历在别的机器上可能走进整个 <c>/tmp</c>）；
    /// ② 因此若默认路径落在 <c>Root</c> 的子目录或 <c>Root</c> 之外，本判据看不见。
    /// 负控覆盖的形态就是"顶层多出一个装着这段文字的文件"；
    /// ③ **只读非空的普通文件**：<c>Root</c> 可能就是 <c>/tmp</c>，那里有 FIFO/socket
    /// （见方法内注释 —— 读 FIFO 会永久阻塞，实测把门禁挂死两次）。
    /// 代价：长度为 0 的文件不读 —— 它本来也不可能含这段文字，无损失。</para>
    /// </summary>
    [Fact]
    public void Write_WithNeitherSink_IsSilentAndSafe()
    {
        const string marker = "    这一行不该出现在任何地方（judge-readings-no-sink）";

        JudgeReadings.Write(null, "无出口", new[] { marker }, filePath: null);

        var leaked = new List<string>();
        foreach (var file in Directory.EnumerateFiles(TestTempDir.Root))
        {
            try
            {
                // 🩸 **只读非空的普通文件**（2026-09-23 实测：不这么做门禁会挂死）。
                // `TMPDIR` / `HMI_TEST_TMPDIR` 都没设时 `TestTempDir.Root` 就是 `/tmp`，
                // 而 `/tmp` 顶层有大量 **FIFO**（`clr-debug-pipe-*`，IDE 调试器建的）与 socket
                // （`MSBuild*` / `dotnet-diagnostic-*`）。`File.ReadAllText` 打开 FIFO 会
                // **永久阻塞**（等一个写端）⇒ 测试主机「无响应」，blame-hang 只能报
                // 「指定的非活动时间已过」，宿主僵死与本用例无关的假象。实测两次，blame 指到本用例。
                // `FileInfo.Length` 走 `stat`（不打开文件，实测 0.001 ms、FIFO 的 st_size=0）；
                // 长度为 0 的文件**不可能**含 marker ⇒ 跳过它在语义上无损。
                if (new FileInfo(file).Length == 0) continue;

                if (File.ReadAllText(file).Contains(marker, StringComparison.Ordinal))
                    leaked.Add(file);
            }
            catch (IOException) { /* 并发删除/占用：与本判据无关 */ }
            catch (UnauthorizedAccessException) { }
        }

        Assert.True(leaked.Count == 0,
            "没有出口时读数不该落到任何文件里，但它在这些文件里被找到了（默认可写路径被引入了？）：\n"
            + string.Join("\n", leaked));
    }

    [Fact]
    public void Write_AppendsRatherThanTruncates()
    {
        var path = TestTempDir.NewFilePath("readings-append", ".txt");
        try
        {
            JudgeReadings.Write(null, "第一段", new[] { "    A" }, path);
            JudgeReadings.Write(null, "第二段", new[] { "    B" }, path);

            var text = File.ReadAllText(path);
            Assert.Contains("=== 第一段 ===", text);
            Assert.Contains("=== 第二段 ===", text);
            Assert.Contains("    A", text);
            Assert.Contains("    B", text);
            Assert.True(text.IndexOf("第一段", StringComparison.Ordinal)
                        < text.IndexOf("第二段", StringComparison.Ordinal),
                "两段都在，且按写入顺序 —— 追加而不是覆盖（覆盖是这条出口最容易被写成的形态）");
        }
        finally { TestTempDir.DeleteFile(path); }
    }

    /// <summary>fail-soft：写不出去（这里用"父目录根本不存在"）不该让判据变红。</summary>
    [Fact]
    public void Write_ToAnUnwritablePath_DoesNotThrow()
    {
        var dir = TestTempDir.NewDir("readings-missing");
        TestTempDir.DeleteDir(dir);
        var path = Path.Combine(dir, "nested", "readings.txt");   // 父目录不存在 ⇒ AppendAllText 必抛

        var ex = Record.Exception(() =>
            JudgeReadings.Write(null, "写不出去", new[] { "    x" }, path));

        Assert.Null(ex);
        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// 两条出口**内容同源**：<see cref="ITestOutputHelper"/> 收到的与文件里写的必须一致。
    /// 不同源的话，「按需 detailed 跑一遍看看」就会给出与门禁不同的答案 —— 那比没有读数更坏。
    /// </summary>
    [Fact]
    public void BothSinks_ReceiveTheSameText()
    {
        var path = TestTempDir.NewFilePath("readings-same", ".txt");
        try
        {
            var output = new CapturingOutput();
            var lines = new[] { "    面：A", "    实测：B" };

            JudgeReadings.Write(output, "同源 scope", lines, path);

            var expected = JudgeReadings.Format("同源 scope", lines);
            Assert.Single(output.Lines);
            Assert.Equal(expected, output.Lines[0]);
            Assert.Equal(expected + Environment.NewLine, File.ReadAllText(path));
        }
        finally { TestTempDir.DeleteFile(path); }
    }

    /// <summary>
    /// **静态守卫**：每个动作面判据类都必须把读数交给出口。
    ///
    /// <para>面是**枚举目录**得来的（<c>*InvocationUiTests.cs</c>），不是写死的 4 个文件名 ——
    /// 阶段 49/50/51/52 这四层是逐个长出来的，第五个一定会再来一个；写死名单等于把
    /// 「新增判据忘了接出口」重新变成静默失效。</para>
    ///
    /// <para><b>先剥注释再匹配</b>（<see cref="SourceScanner.StripComments"/>）：本文件与那四个类
    /// 的注释里都写着 <c>JudgeReadings.Write</c>，不剥注释的话规则会被自己的文档判为已满足。</para>
    /// </summary>
    [Fact]
    public void EveryActionSurfaceJudge_HandsItsReadingsToTheSink()
    {
        var dir = Path.Combine(SourceScanner.RepoRoot, "tests", "HmiEditor.App.UiTests");
        var files = Directory.EnumerateFiles(dir, "*InvocationUiTests.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        // 防退化：面为空时下面那条"全部满足"是白给的。
        Assert.True(files.Length >= 4,
            $"动作面判据类只找到 {files.Length} 个（至少该有 4 个：菜单 / 工具栏 / 对话框内 / 停靠面板）"
            + " —— 面找错了，这条判据失去意义");

        var missing = files
            .Where(f => !SourceScanner.StripComments(File.ReadAllText(f))
                .Contains("JudgeReadings.Write", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToArray();

        Assert.True(missing.Length == 0,
            "这些动作面判据类没有把读数交给出口（登记表 #42）：" + string.Join(", ", missing)
            + "\n⇒ 它们的判据全绿时，面解析/真点数一律拿不到。请在断言前加："
            + "\n   JudgeReadings.Write(_out, \"<scope>\", readings);"
            + "\n   并给类注入 ITestOutputHelper（见 MainWindowToolbarInvocationUiTests）。");
    }
}
