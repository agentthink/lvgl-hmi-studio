using System.Collections.Generic;
using System.IO;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 守卫：测试不得直接使用**系统临时目录**（<c>Path.GetTempPath()</c> 一族）。
///
/// <para><b>为什么需要守卫：</b>本机 <c>/tmp</c> 是 10 MB tmpfs，而本仓库的导出/编译类测试
/// 会写出整套模拟器工程（数百 KB 级）。全量并行跑时多个测试同时往 tmpfs 写，会以
/// <c>IOException</c> "disk was full" 随机失败 —— 堆栈看起来像产品缺陷，实际是环境问题。
/// 唯一正确的入口是 <see cref="TestTempDir"/>（探测 + 四级回退 + 仓库内兜底）。</para>
///
/// <para>历史上这个不变量只靠人工纪律维持，结果 14 个文件、190 处绕过，
/// 直到一次全量跑随机红才被发现。所以把它固化成测试。</para>
///
/// <para><b>本守卫的范围：</b>只拦「会真的产生临时文件」的 API。测试里那些纯元数据用的
/// 假路径字面量（例如 <c>AssetItem.FilePath = "/tmp/fake.bmp"</c>，从不读写）不在范围内 ——
/// 拦它们会产生几十处无收益改动，反而稀释这条守卫的信号。</para>
/// </summary>
public class TestTempDirUsageGuardTests
{
    /// <summary>
    /// 例外白名单：文件相对 <c>tests/</c> 的路径（<c>/</c> 分隔）→ 豁免理由。
    /// 新增条目必须写明**为什么这里非用系统临时目录不可**。
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new()
    {
        ["HmiEditor.TestSupport/TestTempDir.cs"] = "回退链的候选之一，这里正是要真的调用它",
        ["HmiEditor.Core.Tests/GitWorkflowServiceTests.cs"] =
            "会真实执行 git init，必须避开仓库内兜底目录（见 ResolveTempRoot 的说明）",
        ["HmiEditor.Core.Tests/TestTempDirUsageGuardTests.cs"] =
            "本守卫自身把被禁模式作为**数据**列出，不是调用",
        ["HmiEditor.Core.Tests/AppPathsSingleSourceGuardTests.cs"] =
            "产品侧对应守卫，同样把被禁模式作为**数据**列出，不是调用",
    };

    /// <summary>禁止出现的调用形态（不含注释里的提及 —— 注释不产生实际写入）。</summary>
    private static readonly string[] ForbiddenCalls =
    {
        "Path.GetTempPath()",
        "Directory.CreateTempSubdirectory(",
        "Path.GetTempFileName()",
    };

    [Fact]
    public void NoTestUsesSystemTempDirectoryDirectly()
    {
        var testsRoot = FindTestsRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
            if (IsBuildOutput(rel) || Allowed.ContainsKey(rel))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // 跳过纯注释行：文档里提到这些 API 是允许的（TestTempDir 的注释就是这么写的）。
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///"))
                    continue;

                foreach (var forbidden in ForbiddenCalls)
                {
                    if (line.Contains(forbidden, StringComparison.Ordinal))
                        offenders.Add($"{rel}:{i + 1}  {forbidden}   → {trimmed}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"测试不得直接使用系统临时目录（本机 /tmp 仅 10 MB，会随机写满导致假红）。{Environment.NewLine}" +
            $"请改用 TestTempDir.NewDir / NewFilePath（见 tests/HmiEditor.TestSupport/TestTempDir.cs）。{Environment.NewLine}" +
            $"违规点（{offenders.Count}）：{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", offenders));
    }

    [Fact]
    public void AllowedExceptions_PointToExistingFiles()
    {
        // 白名单失效是「守卫静默退化」的典型路径：文件被删/改名后，
        // 守卫会继续全绿，但实际上已经放行了别的地方。所以白名单本身也要断言。
        var testsRoot = FindTestsRoot();

        foreach (var (rel, reason) in Allowed)
        {
            var full = Path.Combine(testsRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(full), $"白名单指向的文件不存在：{rel}（豁免理由：{reason}）");
        }
    }

    private static bool IsBuildOutput(string relativePath)
        => relativePath.Contains("/obj/") || relativePath.Contains("/bin/");

    /// <summary>从测试程序集输出目录向上找到仓库根，返回 <c>tests/</c> 绝对路径。</summary>
    private static string FindTestsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
                return Path.Combine(dir.FullName, "tests");
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");
    }
}
