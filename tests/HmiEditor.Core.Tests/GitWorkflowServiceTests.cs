using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I8: Git 工作流服务单元测试。
/// 测试在临时目录内执行真实 git CLI 操作（隔离、可清理）。
/// 本类**要求** git CLI：缺它是**环境问题**，直接判红 —— 阶段 121 之前是
/// 静默 return 的假绿（没有 skip 报告）。
/// </summary>
public class GitWorkflowServiceTests
{
    /// <summary>
    /// 临时目录**必须位于任何 git 工作树之外**。
    ///
    /// <para>这些测试会在目录里真实执行 <c>git init / commit / log</c>。若该目录落在某个
    /// 仓库内部（例如 <c>TMPDIR</c> 被指向 workspace 下的某个目录），git 会向上发现父仓库的
    /// <c>.git</c>，于是「普通目录」被判定为 <c>Repository</c>、<c>log</c> 读到父仓库的提交历史，
    /// 测试语义完全错乱 —— 表现为一堆看起来像产品缺陷的断言失败。</para>
    ///
    /// <para>所以这里先检查 <c>Path.GetTempPath()</c> 是否落在 git 工作树内，是则回退到
    /// 系统级临时目录（<c>/tmp</c>、<c>/var/tmp</c>，仅 Unix 存在）。</para>
    /// </summary>
    private static string NewTempDir()
    {
        var dir = Path.Combine(ResolveTempRoot(), $"hmi-git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ResolveTempRoot()
    {
        var configured = Path.GetTempPath();
        if (!IsInsideGitWorkTree(configured))
            return configured;

        foreach (var fallback in new[] { "/tmp", "/var/tmp" })
        {
            if (Directory.Exists(fallback) && !IsInsideGitWorkTree(fallback))
                return fallback;
        }

        // 找不到干净位置就照旧用配置值 —— 让测试自身失败暴露问题，而不是静默掩盖。
        return configured;
    }

    private static bool IsInsideGitWorkTree(string path)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(path));
        while (dir is not null)
        {
            // worktree 的根是 .git 目录；submodule/worktree 场景下 .git 是文件。
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return true;
            }
            dir = dir.Parent;
        }
        return false;
    }

    /// <summary>
    /// 健壮删除 —— Windows 上 git 子进程退出后句柄可能延迟释放，
    /// 重试并清除只读属性。
    /// </summary>
    private static void CleanupDir(string dir)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                ClearReadOnly(dir);
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch
            {
                Thread.Sleep(200 * (attempt + 1));
            }
        }
    }

    private static void ClearReadOnly(string dir)
    {
        try
        {
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
        }
        catch
        {
            // 目录被占用时 GetFiles 可能失败，忽略后重试外层
        }
    }

    /// <summary>本类每个用例都要真跑 git；缺 CLI 直接判红，不许静默变绿。</summary>
    private static void RequireGit()
    {
        Assert.True(GitWorkflowService.IsGitAvailable(),
            "本用例需要 git CLI（仓库的 CI 与开发机都有；缺它说明环境没准备好）");
    }

    [Fact]
    public void IsGitAvailable_MatchesAnIndependentCliProbe()
    {
        // 🩸 旧实现是 `Assert.True(result || !result)` —— **恒真**，什么都没判
        //   （阶段 120 修）。而且它看起来"测了 git 探测"，实际连"探测会不会抛"都没验。
        // 现在用**另一条独立路径**（直接起 `git --version` 子进程）交叉验证：
        //   · 机器有 git ⇒ 两边都为 true；没 git ⇒ 两边都为 false。
        //   · 服务内部探测若吞了异常/写死返回值，这条会红。
        var expected = IndependentGitProbe();
        Assert.Equal(expected, GitWorkflowService.IsGitAvailable());
    }

    /// <summary>独立探测：直接起 `git --version`（不经过 GitWorkflowService）。</summary>
    private static bool IndependentGitProbe()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null) return false;
            if (!process.WaitForExit(10_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* 已退出 */ }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;   // 找不到 git / 无权执行 —— 与服务的结论应当一致
        }
    }

    [Fact]
    public void GetState_NonExistentDir_ReturnsNotAvailable()
    {
        var state = GitWorkflowService.GetState(@"Z:\definitely-not-exists-12345");
        Assert.Equal(GitWorkflowState.NotAvailable, state);
    }

    [Fact]
    public void GetState_PlainDir_ReturnsUnversioned()
    {
        var dir = NewTempDir();
        try
        {
            RequireGit();
            Assert.Equal(GitWorkflowState.Unversioned, GitWorkflowService.GetState(dir));
        }
        finally
        {
            CleanupDir(dir);
        }
    }

    [Fact]
    public void InitializeRepository_ThenStateRepository_AndInfo()
    {
        var dir = NewTempDir();
        try
        {
            RequireGit();

            Assert.True(GitWorkflowService.InitializeRepository(dir));
            Assert.Equal(GitWorkflowState.Repository, GitWorkflowService.GetState(dir));

            // 写文件并暂存
            File.WriteAllText(Path.Combine(dir, "test.hmi.json"), "{}");
            Assert.True(GitWorkflowService.StageAll(dir));

            var info = GitWorkflowService.GetInfo(dir);
            Assert.True(info.IsRepository);
            Assert.Equal("main", info.Branch);
            Assert.True(info.UncommittedChanges >= 1);
        }
        finally
        {
            CleanupDir(dir);
        }
    }

    [Fact]
    public void Commit_ThenHistory_ShowsEntry()
    {
        var dir = NewTempDir();
        try
        {
            RequireGit();

            GitWorkflowService.InitializeRepository(dir);
            File.WriteAllText(Path.Combine(dir, "test.hmi.json"), "{\"v\":1}");
            GitWorkflowService.Commit(dir, "feat: initial commit");

            var history = GitWorkflowService.GetHistory(dir, 5);
            Assert.NotEmpty(history);
            Assert.Contains(history, h => h.Contains("initial commit"));
        }
        finally
        {
            CleanupDir(dir);
        }
    }

    [Fact]
    public void Commit_EmptyMessage_StillSucceedsButStateConsistent()
    {
        var dir = NewTempDir();
        try
        {
            RequireGit();

            GitWorkflowService.InitializeRepository(dir);
            File.WriteAllText(Path.Combine(dir, "test.hmi.json"), "{\"v\":2}");

            var (ok, error) = GitWorkflowService.Commit(dir, "调整主画面布局");
            Assert.True(ok, error);

            var info = GitWorkflowService.GetInfo(dir);
            Assert.Equal(0, info.UncommittedChanges);
        }
        finally
        {
            CleanupDir(dir);
        }
    }

    [Fact]
    public void Commit_NonRepository_ReturnsError()
    {
        var dir = NewTempDir();
        try
        {
            var (ok, error) = GitWorkflowService.Commit(dir, "x");
            Assert.False(ok);
            Assert.NotNull(error);
        }
        finally
        {
            CleanupDir(dir);
        }
    }

    [Fact]
    public void InitializeRepository_SecondCall_Idempotent()
    {
        var dir = NewTempDir();
        try
        {
            RequireGit();

            Assert.True(GitWorkflowService.InitializeRepository(dir));
            Assert.True(GitWorkflowService.InitializeRepository(dir));
            Assert.Equal(GitWorkflowState.Repository, GitWorkflowService.GetState(dir));
        }
        finally
        {
            CleanupDir(dir);
        }
    }
}

