using System.Collections.Concurrent;

namespace HmiEditor.TestSupport;

/// <summary>
/// 测试专用临时目录：解析、可用性探测与安全清理。
///
/// <para><b>要解决的问题：</b>本仓库的导出类测试会写出整套模拟器工程
/// （<c>main.c</c>、<c>generated_*.c</c>、<c>CMakeLists.txt</c>、资源副本）。
/// 当 <c>TMPDIR</c> 指向小容量 tmpfs 时（本机 <c>/tmp</c> 曾只有 10 MB），写入会以
/// <c>IOException</c> "disk was full" 失败 —— 堆栈里看起来像产品缺陷，实际是环境问题，
/// 排查成本很高。</para>
///
/// <para><b>策略：</b>按下列顺序挑选第一个「真的写得进去」的目录，逐个探测而不是假定：</para>
/// <list type="number">
///   <item><c>HMI_TEST_TMPDIR</c>（显式指定 —— 指定了就只用它，失败直接报错，不静默降级）；</item>
///   <item><c>TMPDIR</c>；</item>
///   <item><c>Path.GetTempPath()</c>；</item>
///   <item>仓库根下的 <c>.hmi-test-tmp/</c>（已 gitignore）—— 兜底，保证测试在任何机器上都能跑。</item>
/// </list>
///
/// <para><b>为什么必须唯一：</b>任何测试都**不要**直接用 <c>Path.GetTempPath()</c>——那正是
/// 这条回退链要绕开的东西。需要目录用 <see cref="NewDir"/>，需要文件用
/// <see cref="NewFilePath"/>，清理用 <see cref="DeleteDir"/> / <see cref="DeleteFile"/>。</para>
///
/// <para><b>两个根，两种需求：</b></para>
/// <list type="bullet">
///   <item><see cref="Root"/> / <see cref="NewDir"/> —— <b>要空间</b>。导出/编译类测试会写出整套工程，
///   优先大盘，磁盘紧张时才回退到仓库内兜底目录。</item>
///   <item><see cref="IsolatedRoot"/> / <see cref="NewIsolatedDir"/> —— <b>要位置</b>。
///   「向上搜索」类测试（找 SDK、找仓库根、执行 git）绝不能落在仓库树内，否则搜索会逃逸到真实仓库。
///   空间要求低（256 KB 探测），但必须是仓库之外的目录；系统临时目录被写满时会兜底到「仓库同级」目录。</item>
/// </list>
///
/// <para><b>清理为什么也要包：</b><c>finally</c> 里的裸 <c>Directory.Delete</c> 一旦抛异常，
/// 会**替换掉**测试的真实失败原因（.NET 异常语义）。清理失败不应该改变测试结论，
/// 因此统一吞掉 —— 这也正是磁盘紧张时最容易走到的路径。</para>
///
/// <para><b>约束（重要）：</b>兜底目录 <c>.hmi-test-tmp/</c> 位于**仓库内部**，所以**不要**把它用于
/// 会执行 <c>git</c> 命令的测试 —— git 会向上发现父仓库的 <c>.git</c>，把「普通目录」判定成
/// 仓库、把历史读成父仓库的提交。这类测试应该用 <see cref="NewIsolatedDir"/>（保证在仓库之外）；
/// 现有的 <c>GitWorkflowServiceTests.ResolveTempRoot</c> 是自建等价逻辑的历史实现。</para>
/// </summary>
public static class TestTempDir
{
    /// <summary>
    /// 可用性探测阈值。取得比单个测试工程的实际需求（数百 KB 级）大得多，
    /// 目的是「能通过探测」等价于「这个卷空间充裕」，而不是刚好够用。
    /// </summary>
    private const int RequiredBytes = 16 * 1024 * 1024;

    /// <summary>
    /// 「隔离型」临时目录的探测阈值。这类测试写入量是 KB 级（甚至不写），
    /// 它们要的是**位置**（必须在仓库之外），不是空间。
    /// </summary>
    private const int RequiredBytesIsolated = 256 * 1024;

    private const string ExplicitOverrideVariable = "HMI_TEST_TMPDIR";
    private const string RepoFallbackDirName = ".hmi-test-tmp";
    private const string SolutionFileName = "HmiEditor.slnx";
    private const string SecondaryUnixTempDir = "/var/tmp";

    private static readonly Lazy<string> LazyRoot = new(ResolveRoot);
    private static readonly Lazy<string> LazyIsolatedRoot = new(ResolveIsolatedRoot);

    // ── 退出兜底清扫（阶段 120）──
    //
    // 契约是"调用方用 DeleteDir/DeleteFile 收尾"，但审计实测：**10+ 个用例只建不收**
    // （`.hmi-test-tmp` 累积到 11,921 项 / 37 MB，且此前没有任何全局清理）。
    // 逐个人工补 finally 既容易漏，又会让"清理失败"混进断言路径。
    // 这里加一张**兜底网**：创建即登记，进程退出时把没被显式清理的删掉。
    // 它不替代显式清理（显式清理及时释放句柄、回收空间），只保证"忘了一个"不会变成永久垃圾。
    private static readonly ConcurrentDictionary<string, byte> PendingCleanup = new(StringComparer.Ordinal);
    private static int _cleanupRegistered;

    private static void TrackForCleanup(string path)
    {
        PendingCleanup.TryAdd(path, 0);
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 0)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupPending();
        }
    }

    private static void Untrack(string path) => PendingCleanup.TryRemove(path, out _);

    /// <summary>
    /// 进程退出时的兜底清扫：删掉所有登记过、却没被显式清理的临时路径。
    /// 公开只为让"到底还剩多少"可被观测；正常代码路径不应调用它（会清掉别的用例的工作目录）。
    /// </summary>
    public static void CleanupPending()
    {
        foreach (var path in PendingCleanup.Keys)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 进程退出中：尽力而为，删不掉下次运行再说
            }
        }
    }

    /// <summary>所有测试临时目录的父目录（首次访问时解析并探测，结果缓存）。</summary>
    public static string Root => LazyRoot.Value;

    /// <summary>
    /// 「隔离型」临时目录的父目录：**保证位于本仓库之外**。详见 <see cref="NewIsolatedDir"/>。
    /// </summary>
    public static string IsolatedRoot => LazyIsolatedRoot.Value;

    /// <summary>在根目录下创建一个唯一子目录并返回其路径。</summary>
    public static string NewDir(string prefix)
    {
        var dir = Path.Combine(Root, $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        TrackForCleanup(dir);
        return dir;
    }

    /// <summary>
    /// 在**仓库之外**创建一个唯一子目录，用于「向上搜索」类测试。
    ///
    /// <para><b>为什么不能用 <see cref="NewDir"/>：</b><see cref="Root"/> 在磁盘紧张时会回退到
    /// 仓库内的 <c>.hmi-test-tmp/</c>。而「向上找 SDK / 仓库根」的测试，语义正是<b>从给定目录
    /// 向上走</b> —— 一旦起点落在仓库里，搜索就会逃逸到真实仓库，把「找不到」翻成「找到了」
    /// （实测：<c>FindSdkRoot</c> 会返回真实的 <c>firmware/Tina-SDK</c>）。
    /// 这跟 git 类测试不能落在工作树里是同一个失效模式。</para>
    ///
    /// <para>所以这里单独解析一条「空间要求低、位置要求硬」的路径：候选目录必须不在仓库树内。</para>
    /// </summary>
    public static string NewIsolatedDir(string prefix)
    {
        var dir = Path.Combine(IsolatedRoot, $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        TrackForCleanup(dir);
        return dir;
    }

    /// <summary>
    /// 在根目录下返回一个唯一文件路径（**不创建文件** —— 调用方自己决定写不写）。
    /// 用于「产品代码负责新建文件」的场景，例如主题配置、审计日志、导航图快照。
    /// </summary>
    public static string NewFilePath(string prefix, string extension = "")
    {
        var path = Path.Combine(Root, $"{prefix}-{Guid.NewGuid():N}{extension}");
        TrackForCleanup(path);
        return path;
    }

    /// <summary>
    /// 删除临时目录。清理失败不影响测试结论（原始异常优先），因此显式吞掉。
    /// 会跳过不存在的目录。
    /// </summary>
    public static void DeleteDir(string dir)
    {
        Untrack(dir);
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (Exception)
        {
            // 测试结论由断言决定；清理失败（文件句柄未释放、并发删除等）不应掩盖它。
        }
    }

    /// <summary>删除临时文件。理由同 <see cref="DeleteDir"/>。</summary>
    public static void DeleteFile(string path)
    {
        Untrack(path);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
            // 忽略：清理失败不应改变测试结论。
        }
    }

    private static string ResolveRoot()
    {
        // 显式指定：只用它。失败了要让人看见，而不是悄悄换到别处。
        var explicitDir = Environment.GetEnvironmentVariable(ExplicitOverrideVariable);
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            var explicitRoot = Path.GetFullPath(explicitDir);
            try
            {
                Directory.CreateDirectory(explicitRoot);
                ProbeWritable(explicitRoot);
                return explicitRoot;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"环境变量 {ExplicitOverrideVariable} 指定的目录不可用：{explicitRoot}" +
                    $"（需要在其中写入约 {RequiredBytes / (1024 * 1024)} MB 的探测数据）。", ex);
            }
        }

        List<(string Dir, string Reason)> failures = [];
        foreach (var candidate in EnumerateCandidates())
        {
            var root = Path.GetFullPath(candidate);
            try
            {
                Directory.CreateDirectory(root);
                ProbeWritable(root);
                return root;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add((root, ex.Message));
            }
        }

        throw new InvalidOperationException(
            "找不到可用的测试临时目录。已尝试：" + Environment.NewLine +
            string.Join(Environment.NewLine, failures.Select(f => $"  - {f.Dir}：{f.Reason}")) +
            Environment.NewLine +
            $"可设置环境变量 {ExplicitOverrideVariable}=<有空间的目录> 显式指定。");
    }

    /// <summary>
    /// 解析「隔离型」根：候选目录必须**不在仓库树内**，且能通过小体积写探测。
    /// 找不到就报错而不是回退到仓库内 —— 回退会让上层测试静默失去隔离语义。
    /// </summary>
    private static string ResolveIsolatedRoot()
    {
        var repoRoot = FindRepoRoot();
        List<(string Dir, string Reason)> rejected = [];

        foreach (var candidate in EnumerateIsolatedCandidates())
        {
            var root = Path.GetFullPath(candidate);
            if (repoRoot is not null && IsSameOrInside(root, repoRoot))
            {
                rejected.Add((root, "位于仓库树内，向上搜索会逃逸到真实仓库"));
                continue;
            }

            try
            {
                Directory.CreateDirectory(root);
                ProbeWritable(root, RequiredBytesIsolated);
                return root;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                rejected.Add((root, ex.Message));
            }
        }

        throw new InvalidOperationException(
            "找不到可用的「隔离型」测试临时目录（必须位于仓库之外）。已尝试：" + Environment.NewLine +
            string.Join(Environment.NewLine, rejected.Select(r => $"  - {r.Dir}：{r.Reason}")));
    }

    /// <summary>
    /// 「隔离型」根的候选顺序：显式指定 → <c>TMPDIR</c> → 系统临时目录 → <c>/var/tmp</c>（若已存在）→ 仓库同级目录。
    ///
    /// <para><b>为什么最后要留一个「仓库同级」兜底：</b>系统临时目录常常是小容量 tmpfs ——
    /// 本机 <c>/tmp</c> 只有 10 MB，全量并发跑时 7 个测试宿主会把它写满，此时前面所有候选
    /// 都会以 <c>No space left on device</c> 落空（实测 256 KB 探测即失败）。
    /// 没有这个兜底时，一次瞬时占满会把「临时目录不够用」升级成 12 个测试硬失败。
    /// 该目录与仓库同盘，空间等于仓库所在卷，且确定在仓库之外。</para>
    ///
    /// <para>顺序上把它排在系统临时目录<b>之后</b>：它只是补救，不是默认 ——
    /// 正常情况下不该在开发者的仓库父目录里留下目录。</para>
    /// </summary>
    private static IEnumerable<string> EnumerateIsolatedCandidates()
    {
        var explicitDir = Environment.GetEnvironmentVariable(ExplicitOverrideVariable);
        if (!string.IsNullOrWhiteSpace(explicitDir))
            yield return explicitDir;

        var tmpEnv = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpEnv))
            yield return tmpEnv;

        yield return Path.GetTempPath();

        // 只挑**已存在**的 Unix 次级临时目录：不存在就不去创建它
        // （以 root 跑测试时会把 /var/tmp 凭空建出来，属于不可接受的副作用）。
        if (!OperatingSystem.IsWindows() && Directory.Exists(SecondaryUnixTempDir))
            yield return SecondaryUnixTempDir;

        // 兜底（仅当上面全被写满时才会用到）。
        foreach (var candidate in EnumerateRepoExternalSiblings())
            yield return candidate;
    }

    /// <summary>
    /// 仓库**同级**的隐藏目录（例如 <c>&lt;repo&gt;.test-isolated-tmp/</c>）。
    /// 「同级」而不是「父目录内」：既在仓库树之外（向上搜索不会进入仓库），
    /// 又与仓库同盘（继承仓库所在卷的容量）。
    /// </summary>
    private static IEnumerable<string> EnumerateRepoExternalSiblings()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
            yield break;

        var normalized = Path.TrimEndingDirectorySeparator(repoRoot);
        var parent = Path.GetDirectoryName(normalized);
        var name = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            yield break;

        yield return Path.Combine(parent, $"{name}.test-isolated-tmp");
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        var tmpEnv = Environment.GetEnvironmentVariable("TMPDIR");
        if (!string.IsNullOrWhiteSpace(tmpEnv))
            yield return tmpEnv;

        yield return Path.GetTempPath();

        var repoFallback = FindRepoFallback();
        if (repoFallback is not null)
            yield return repoFallback;
    }

    /// <summary>从测试程序集输出目录向上找到解决方案根，返回仓库根目录；找不到返回 null。</summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>仓库内兜底目录（<c>.hmi-test-tmp/</c>）。</summary>
    private static string? FindRepoFallback()
    {
        var repoRoot = FindRepoRoot();
        return repoRoot is null ? null : Path.Combine(repoRoot, RepoFallbackDirName);
    }

    /// <summary>判断 <paramref name="path"/> 是否就是 <paramref name="ancestor"/> 或位于其下。</summary>
    private static bool IsSameOrInside(string path, string ancestor)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var normalizedAncestor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ancestor));

        if (string.Equals(normalizedPath, normalizedAncestor, PathComparison))
            return true;

        return normalizedPath.StartsWith(normalizedAncestor + Path.DirectorySeparatorChar, PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void ProbeWritable(string root) => ProbeWritable(root, RequiredBytes);

    private static void ProbeWritable(string root, int requiredBytes)
    {
        var probe = Path.Combine(root, $".hmi-space-probe-{Guid.NewGuid():N}");
        try
        {
            using (var fs = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var block = new byte[64 * 1024];
                var written = 0;
                while (written < requiredBytes)
                {
                    fs.Write(block, 0, block.Length);
                    written += block.Length;
                }
                fs.Flush();
            }
        }
        finally
        {
            try
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            catch (Exception)
            {
                // 探测文件清理失败无需上报。
            }
        }
    }
}
