using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **仓库卫生状态守卫**（2026-10-11，阶段 296）—— 把两件已拍板的事机械守住，
/// 免得下次有人"顺手"把它们加回来：
///
/// <list type="number">
///   <item><b>LICENSE 必须是 MIT</b>（用户 2026-10-11 拍板）—— 此前仓库**没有任何许可证文件**，
///     对外分发等于"未授权"。</item>
///   <item><b><c>build/</c> 下不得再有 PowerShell 脚本</b>（用户 2026-10-11 拍板删除）——
///     那 5 个是 Windows 专用（NativeAOT/win-x64 交叉编译 + 进程回收），而本工程 Linux 为主力平台，
///     发布走 <c>scripts/package-release.sh</c> + <c>tools/ReleasePackager</c>。</item>
/// </list>
/// </summary>
public class RepoHygieneGuardTests
{
    private readonly ITestOutputHelper _o;

    public RepoHygieneGuardTests(ITestOutputHelper o) => _o = o;

    private static string RepoPath(string relative) =>
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void License_IsTheDecidedMitLicense()
    {
        var path = RepoPath("LICENSE");
        Assert.True(File.Exists(path), "仓库根缺 LICENSE —— 对外分发等于'未授权'（2026-10-11 拍板用 MIT）");

        var text = File.ReadAllText(path);
        Assert.StartsWith("MIT License", text.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("Permission is hereby granted, free of charge", text, StringComparison.Ordinal);
        Assert.Contains("WITHOUT WARRANTY OF ANY KIND", text, StringComparison.Ordinal);

        JudgeReadings.Write(_o, "仓库卫生状态（阶段 296）", new[]
        {
            "LICENSE = MIT（拍板 2026-10-11）",
            "build/ 下不得再有 .ps1（那 5 个 Windows 专用脚本已按拍板删除）",
        });
    }

    [Fact]
    public void BuildDirectory_HasNoPowerShellScripts()
    {
        var buildDir = RepoPath("build");
        if (!Directory.Exists(buildDir)) return;   // 目录整个没了也算过（删除得更彻底）

        var scripts = Directory.EnumerateFiles(buildDir, "*.ps1", SearchOption.AllDirectories)
            .Select(SourceScanner.Relative)
            .ToList();

        Assert.True(scripts.Count == 0,
            "build/ 下又出现了 PowerShell 脚本（2026-10-11 已拍板删除这 5 个 Windows 专用脚本）："
            + Environment.NewLine + string.Join(Environment.NewLine, scripts)
            + Environment.NewLine + "若确需重新引入，请先改本条判据（它是那个决定的一部分）。");
    }
}
