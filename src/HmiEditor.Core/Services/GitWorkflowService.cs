using System.Diagnostics;
using System.Text;

namespace HmiEditor.Core.Services;

/// <summary>
/// Git 工作流状态。
/// </summary>
public enum GitWorkflowState
{
    /// <summary>项目目录不是 Git 仓库，也未检测到 git 可执行文件。</summary>
    NotAvailable,

    /// <summary>已是 Git 仓库。</summary>
    Repository,

    /// <summary>项目目录尚未纳入版本控制（可一键初始化）。</summary>
    Unversioned
}

/// <summary>Git 仓库快照信息。</summary>
public class GitRepositoryInfo
{
    public bool IsRepository { get; set; }
    public string? Branch { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public int UncommittedChanges { get; set; }
    public string? LastCommit { get; set; }
    public string? Remote { get; set; }
}

/// <summary>
/// I8: Git 工作流服务 —— 通过 git CLI 对项目文件实施版本控制保障。
///
/// 设计决策：
///   - 仅调用 `git` 命令行（零第三方依赖，与 NativeAOT 完全兼容；
///     LibGit2Sharp 等托管绑定会破坏 rd.xml 约束并显著增大体积）。
///   - 全部操作只读探测或对"项目目录内"执行，不修改用户 git 配置。
///   - 功能：仓库检测 / 一键初始化 / 暂存项目文件 / 状态快照 /
///     提供常用 git 命令入口（提交、历史、推送）。
/// </summary>
public static class GitWorkflowService
{
    /// <summary>
    /// 等待一个 git 子进程退出的**统一窗口**（毫秒）。
    ///
    /// <para><b>单一来源</b>：本类里每一处「等 git 进程退出」都必须走它，不得各写各的数字。
    /// 曾出现「探测用 3 s、执行用 15 s」两套值，表现为满负载门禁下探测先失效
    /// （详见 <see cref="IsGitAvailable"/> 的注释）。结构守卫见
    /// <c>GitProcessWaitWindowTests</c>。</para>
    /// </summary>
    public const int GitProcessWaitMs = 15000;

    /// <summary>
    /// 判断 git CLI 是否可用。
    ///
    /// <para>窗口必须与 <see cref="RunGit"/> 的命令窗口一致（15 s），**不得更紧**。二者是同一个
    /// 语义「等一个 git 进程退出」的两处硬窗口，一旦不一致，满负载下先失效的必然是窗口更紧的那个：
    /// 2026-09-16 全量门禁（7 个测试宿主并行）连续两跑红在
    /// <c>GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent</c>，失败点是
    /// <c>InitializeRepository</c> 的第一个守卫 <c>!IsGitAvailable()</c> —— 同一个测试里前一次
    /// 探测为真、后一次为假，因为此处原为 3 s 而 <c>RunGit</c> 为 15 s，测试自身的
    /// <c>GitAvailable()</c> 也是转发到本方法（没有第二个窗口可对照）。
    /// 单跑（整个 Core 程序集 1894/1894 绿、filtered 8/8 绿）复现不出来。</para>
    /// </summary>
    public static bool IsGitAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(GitProcessWaitMs);
            return p.HasExited && p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 评估项目目录的 Git 状态。
    /// </summary>
    public static GitWorkflowState GetState(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            return GitWorkflowState.NotAvailable;

        if (!IsGitAvailable())
            return GitWorkflowState.NotAvailable;

        if (IsRepository(projectDirectory))
            return GitWorkflowState.Repository;

        return GitWorkflowState.Unversioned;
    }

    /// <summary>判断目录是否已是 Git 仓库。</summary>
    public static bool IsRepository(string projectDirectory)
    {
        var result = RunGit(projectDirectory, "rev-parse --is-inside-work-tree");
        return result.ExitCode == 0 &&
               result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在项目目录初始化 Git 仓库（幂等：已是仓库则直接返回成功）。
    /// </summary>
    public static bool InitializeRepository(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory)) return false;
        if (!Directory.Exists(projectDirectory)) return false;
        if (!IsGitAvailable()) return false;

        if (IsRepository(projectDirectory)) return true;

        var result = RunGit(projectDirectory, "init");
        if (result.ExitCode != 0) return false;

        // 默认分支统一为 main（不覆盖用户已有的 defaultBranch 配置之外的偏好）
        RunGit(projectDirectory, "symbolic-ref HEAD refs/heads/main");

        return IsRepository(projectDirectory);
    }

    /// <summary>
    /// 暂存项目文件（.hmi.json / .gitignore / README 等）。
    /// 使用 `git add -A` 暂存目录内全部变更。
    /// </summary>
    public static bool StageAll(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory)) return false;
        if (!IsRepository(projectDirectory)) return false;

        var result = RunGit(projectDirectory, "add -A");
        return result.ExitCode == 0;
    }

    /// <summary>收集仓库状态快照。</summary>
    public static GitRepositoryInfo GetInfo(string projectDirectory)
    {
        var info = new GitRepositoryInfo();
        if (string.IsNullOrWhiteSpace(projectDirectory) || !IsRepository(projectDirectory))
            return info;

        info.IsRepository = true;

        var branch = RunGit(projectDirectory, "branch --show-current");
        if (branch.ExitCode == 0)
            info.Branch = branch.StandardOutput.Trim();

        var status = RunGit(projectDirectory, "status --porcelain");
        if (status.ExitCode == 0)
        {
            info.UncommittedChanges = status.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        var last = RunGit(projectDirectory, "log -1 --format=%h %s");
        if (last.ExitCode == 0)
            info.LastCommit = last.StandardOutput.Trim();

        var remote = RunGit(projectDirectory, "remote get-url origin");
        if (remote.ExitCode == 0)
            info.Remote = remote.StandardOutput.Trim();

        var counts = RunGit(projectDirectory, "rev-list --left-right --count HEAD...@{upstream}");
        if (counts.ExitCode == 0)
        {
            var parts = counts.StandardOutput.Trim().Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out var ahead)) info.Ahead = ahead;
                if (int.TryParse(parts[1], out var behind)) info.Behind = behind;
            }
        }

        return info;
    }

    /// <summary>提交全部已暂存变更。</summary>
    public static (bool Success, string? Error) Commit(string projectDirectory, string message)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || !IsRepository(projectDirectory))
            return (false, "项目目录不是 Git 仓库");

        var add = RunGit(projectDirectory, "add -A");
        if (add.ExitCode != 0)
            return (false, add.StandardError);

        // 消息使用 -m 参数传递，避免注入风险
        // 工程级提交必须可在没有全局 Git 身份的干净环境中工作；
        // 使用仓库本地命令配置，避免修改用户全局配置，同时保持提交可追溯。
        var commit = RunGit(projectDirectory, $"-c user.name=zhishu-hmi -c user.email=zhishu-hmi@localhost commit -m \"{EscapeForArg(message)}\"");
        if (commit.ExitCode != 0)
            return (false, commit.StandardError);

        return (true, null);
    }

    /// <summary>查看最近提交历史（最多 <paramref name="count"/> 条）。</summary>
    public static IReadOnlyList<string> GetHistory(string projectDirectory, int count = 10)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(projectDirectory) || !IsRepository(projectDirectory))
            return result;

        var log = RunGit(projectDirectory, $"log --oneline -{Math.Max(1, Math.Min(count, 50))}");
        if (log.ExitCode != 0) return result;

        foreach (var line in log.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            result.Add(line.Trim());
        return result;
    }

    /// <summary>获取差异统计（自上次提交以来变更的行数）。</summary>
    public static string GetDiffStat(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || !IsRepository(projectDirectory))
            return "";

        var stat = RunGit(projectDirectory, "diff --stat HEAD");
        return stat.ExitCode == 0 ? stat.StandardOutput.Trim() : "";
    }

    // ── 内部 ──

    private static (int ExitCode, string StandardOutput, string StandardError) RunGit(
        string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "", "无法启动 git 进程");

            // 同步读取避免管道死锁
            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(GitProcessWaitMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
                return (-1, "", "git 命令超时");
            }

            var stdout = outputTask.GetAwaiter().GetResult();
            var stderr = errorTask.GetAwaiter().GetResult();
            return (p.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    private static string EscapeForArg(string value)
        => value.Replace("\"", "\\\"");
}
