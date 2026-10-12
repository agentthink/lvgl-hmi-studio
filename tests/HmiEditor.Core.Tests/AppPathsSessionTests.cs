using System;
using System.Collections.Generic;
using System.IO;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 本测试类会改**进程级**状态（应用数据根环境变量、以及本进程的会话临时目录），
/// 因此禁止与其他测试并行 —— 与 <c>UiOperationGuardTests</c> 临时替换全局
/// <c>AppLog.Current</c> 是同一类问题。与之配对的是 <see cref="AppPathsTests"/>。
/// </summary>
[CollectionDefinition("AppPaths-global", DisableParallelization = true)]
public sealed class AppPathsGlobalCollection;

/// <summary>
/// 行为测试：<see cref="AppPaths"/> 的**会话层** —— 唯一名临时产物的生命周期。
///
/// <para><b>要解决的问题：</b>唯一名目录（<c>&lt;prefix&gt;-&lt;guid&gt;</c>）解决了"并发/多实例
/// 不互相踩"，但没解决"**谁负责删**" —— 唯一名等于**无主名**。旧实现把它们平铺在临时根下
/// 且从不删除（每次打开工程建两个，里面是解包后的工程与资产副本），
/// 只因落在系统临时目录（重启会清）才长期没被发现。</para>
///
/// <para><b>不变量：</b>① 唯一名产物一律在本进程会话目录内（可整体释放）；
/// ② 清理判据一律偏保守 —— 漏删只是浪费空间，误删会让另一个正在运行的实例当场失败。</para>
/// </summary>
[Collection("AppPaths-global")]
public class AppPathsSessionTests
{
    // ─────────────────────────── 会话层：产物落点 ───────────────────────────

    /// <summary>
    /// 唯一名临时产物必须收在本进程的会话目录里 —— 这正是"释放"能退化成删一个目录的前提。
    /// </summary>
    [Fact]
    public void NewTempSubdir_LandsInsideTheProcessSessionDirectory()
    {
        var dir = AppPaths.NewTempSubdir("probe");
        var file = AppPaths.NewTempFilePath("probe", ".ini");

        try
        {
            Assert.True(Directory.Exists(dir), $"应已创建：{dir}");
            Assert.True(IsUnder(dir, AppPaths.SessionTempRoot), $"应落在会话目录下：{dir}");
            Assert.True(IsUnder(file, AppPaths.SessionTempRoot), $"应落在会话目录下：{file}");

            // 会话目录本身要在临时根之下，否则"扫临时根清孤儿"扫不到它。
            Assert.True(IsUnder(AppPaths.SessionTempRoot, AppPaths.TempRoot),
                $"会话目录必须在临时根之下：{AppPaths.SessionTempRoot}");
        }
        finally
        {
            AppPaths.ReleaseSessionTemp();
        }
    }

    /// <summary>
    /// <b>回归守卫</b>：无论建多少个唯一名目录，临时根**直下**一个都不增长。
    /// 旧实现（<c>TempRoot/&lt;prefix&gt;-&lt;guid&gt;</c>）在这里会堆出 5 个目录 —— 本断言即变红。
    ///
    /// <para>用**隔离根**而不是全局临时根：那个根是跨次运行持久共享的，
    /// 断言「绝对为空」会被上一次运行（或某次注入实验）留下的同名目录污染，
    /// 变成一条会自己变红的测试。判据只该依赖本次运行产生的增量。</para>
    /// </summary>
    [Fact]
    public void NewTempSubdir_DoesNotAccumulateUnderTempRoot()
    {
        using var scope = new IsolatedRoot();
        var created = new List<string>();

        for (var i = 0; i < 5; i++)
            created.Add(AppPaths.NewTempSubdir("leak-probe"));

        Assert.All(created, d => Assert.True(Directory.Exists(d)));

        var leaked = Directory.GetFileSystemEntries(AppPaths.TempRoot, "leak-probe*");
        Assert.Empty(leaked);
    }

    /// <summary>
    /// 会话 id 在进程内稳定，且名字里带得上 pid —— 带不上 pid 就没法判"这个目录还有主吗"。
    /// </summary>
    [Fact]
    public void SessionTempRoot_IsStableWithinTheProcess_AndCarriesOurPid()
    {
        var first = AppPaths.SessionTempRoot;
        try
        {
            AppPaths.NewTempSubdir("stability-probe");   // 建产物不该改变会话身份

            Assert.Equal(first, AppPaths.SessionTempRoot);
            Assert.StartsWith(Environment.ProcessId + "-", Path.GetFileName(first), StringComparison.Ordinal);
        }
        finally
        {
            AppPaths.ReleaseSessionTemp();               // 测试自己别在临时根里留东西
        }
    }

    // ─────────────────────────── 会话层：释放 ───────────────────────────

    /// <summary>
    /// 释放要真的把会话目录删掉；且释放后再用必须开**新**会话 —— 否则会把文件写回
    /// 已删掉的目录（看似成功，实际无主，下次清理也扫不到）。
    /// </summary>
    [Fact]
    public void ReleaseSessionTemp_DeletesTheSessionDirectory_AndNextUseStartsANewSession()
    {
        var dir = AppPaths.NewTempSubdir("release-probe");
        var session = AppPaths.SessionTempRoot;
        Assert.True(Directory.Exists(dir));

        try
        {
            Assert.True(AppPaths.ReleaseSessionTemp(), "应报告删除了会话目录");
            Assert.False(Directory.Exists(session), $"会话目录应已释放：{session}");

            var next = AppPaths.NewTempSubdir("release-probe");
            Assert.True(Directory.Exists(next), $"释放后应能继续工作：{next}");
            Assert.False(IsUnder(next, session), "释放后再用不得写回已删的旧会话目录");
        }
        finally
        {
            AppPaths.ReleaseSessionTemp();
        }
    }

    // ─────────────────────────── 会话层：路径与目录**分开** ───────────────────────────

    /// <summary>
    /// **只给路径、不建目录**是「失败的打开不留工作区目录」那条不变量所依赖的性质。
    ///
    /// <para><b>为什么需要这一半</b>：<c>HmiPackageService.Unpack</c> 需要一个资产落点参数，
    /// 但那个落点**不能**在"包还没校验"时就建出来 —— 包是坏的时它一个文件都收不到、
    /// 也没人再引用它，而会话目录的兜底释放点在**进程退出**（阶段 80 实测：5 次失败 = 5 个空目录）。
    /// 消费者改成在**第一次写盘之前**才建（<c>Unpack</c> 在全部前置校验通过之后才
    /// <c>Directory.CreateDirectory(assetDir)</c>）⇒ 失败路径**按构造**不留痕迹。</para>
    ///
    /// <para><b>成对断言的理由</b>：只断言"不建"会被"这个方法根本什么都没干"满足；
    /// 只断言"建"则与 <see cref="NewTempSubdir_LandsInsideTheProcessSessionDirectory"/> 重复。
    /// 两侧同场，才是"分成两半"这件事的证据。</para>
    /// </summary>
    [Fact]
    public void NewTempSubdirPath_GivesAPathWithoutCreatingIt()
    {
        var fromPath = AppPaths.NewTempSubdirPath("lazy-probe");
        try
        {
            Assert.True(IsUnder(fromPath, AppPaths.SessionTempRoot), $"应落在会话目录下：{fromPath}");

            // 必须是会话根**直下**那一层（"解包落点"就是这一层，不能再深）。
            Assert.Equal(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppPaths.SessionTempRoot)),
                Path.GetDirectoryName(Path.GetFullPath(fromPath)));

            Assert.False(Directory.Exists(fromPath), $"只给路径的那一半不得创建目录：{fromPath}");

            // 唯一名：两次调用必须给出不同路径（并发/多实例不互相踩的前提）。
            var second = AppPaths.NewTempSubdirPath("lazy-probe");
            Assert.NotEqual(fromPath, second);
            Assert.False(Directory.Exists(second), $"只给路径的那一半不得创建目录：{second}");

            // 对照侧：**创建版必须真的建出来** —— 否则"不建"不是"这一半不建"，而是整个机制失灵。
            var created = AppPaths.NewTempSubdir("lazy-probe");
            Assert.True(Directory.Exists(created), $"创建版应建出目录：{created}");

            Assert.Throws<ArgumentException>(() => AppPaths.NewTempSubdirPath("   "));
        }
        finally
        {
            AppPaths.ReleaseSessionTemp();
        }
    }

    // ─────────────────────────── 孤儿清理：判据的四个方向 ───────────────────────────

    [Fact]
    public void PruneStaleSessions_RemovesSessionsOfDeadProcesses()
    {
        using var scope = new IsolatedRoot();
        var orphan = scope.MakeSession("999999-deadbeef");       // 探针判死
        var alive = scope.MakeSession("4242-cafebabe");          // 探针判活

        var removed = AppPaths.PruneStaleSessions(pid => pid == 4242);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(orphan), "死进程的会话目录必须清掉");
        Assert.True(Directory.Exists(alive), "存活进程的会话目录不得删");
    }

    /// <summary>
    /// 保守方向：名字里解析不出 pid 的一律**不动**。这不是"漏了"，是刻意的 ——
    /// 误删的代价（另一个实例当场失败 / 丢失正在用的文件）远大于漏删（浪费点空间）。
    /// </summary>
    [Fact]
    public void PruneStaleSessions_KeepsDirectoriesItCannotAttributeToAProcess()
    {
        using var scope = new IsolatedRoot();
        var foreign = scope.MakeSession("not-a-session-name");
        var noSeparator = scope.MakeSession("12345");
        var zeroPid = scope.MakeSession("0-bad");

        // 探针一律判死 = 最激进的清理者；即便如此，认不出归属的目录也不能删。
        var removed = AppPaths.PruneStaleSessions(_ => false);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(foreign), $"不得删认不出归属的目录：{foreign}");
        Assert.True(Directory.Exists(noSeparator), $"不得删认不出归属的目录：{noSeparator}");
        Assert.True(Directory.Exists(zeroPid), $"不得删认不出归属的目录：{zeroPid}");
    }

    /// <summary>自己的会话永远不删 —— 哪怕探针把"我"也判死（正在用的目录不能被清掉）。</summary>
    [Fact]
    public void PruneStaleSessions_NeverRemovesTheCurrentSession()
    {
        using var scope = new IsolatedRoot();
        var own = AppPaths.NewTempSubdir("own-probe");
        var session = AppPaths.SessionTempRoot;

        var removed = AppPaths.PruneStaleSessions(_ => false);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(session), $"当前会话不得删：{session}");
        Assert.True(Directory.Exists(own));
    }

    /// <summary>
    /// 同一 pid 号但 token 不同 = 该 pid 的**前任**（pid 已被本进程复用）⇒ 必删。
    ///
    /// <para>这种残留单靠"pid 存活判定"永远发现不了 —— 因为那个 pid 现在活着，
    /// 活的就是**我们自己**。所以这里让探针一律报活，把这条判据单独隔离出来：
    /// 去掉前任分支，本测试必红，其余清理测试全绿。</para>
    /// </summary>
    [Fact]
    public void PruneStaleSessions_RemovesPredecessorThatReusedOurPid()
    {
        using var scope = new IsolatedRoot();
        var predecessor = scope.MakeSession($"{Environment.ProcessId}-0000000000000000");

        var removed = AppPaths.PruneStaleSessions(_ => true);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(predecessor), "同 pid 的前任会话必须清掉（否则永久残留）");
    }

    /// <summary>
    /// 清理面**只**是会话目录。稳定名工作目录（so-build / compile / download / sim）承载
    /// 增量编译复用，不属于本机制管理 —— 被顺手清掉会让"下次还得重编"。
    /// </summary>
    [Fact]
    public void PruneStaleSessions_DoesNotTouchStableNameWorkDirectories()
    {
        using var scope = new IsolatedRoot();
        var soBuild = AppPaths.TempSubdir("so-build");
        var compile = AppPaths.TempSubdir("compile");
        var sim = AppPaths.TempSubdir("sim", "Demo");

        var removed = AppPaths.PruneStaleSessions(_ => false);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(soBuild), "so-build 不在清理面内");
        Assert.True(Directory.Exists(compile), "compile 不在清理面内");
        Assert.True(Directory.Exists(sim), "sim/<name> 不在清理面内");
    }

    // ─────────────────────────── 默认探针（不注入） ───────────────────────────

    /// <summary>
    /// 真实探针确实能把"已不存在的 pid"判死。否则注入路径可以全绿，
    /// 而生产路径永远只漏删 —— 注入探针把生产判据整个掩盖掉。
    /// </summary>
    [Fact]
    public void PruneStaleSessions_RealProbeRecognisesDeadPids()
    {
        using var scope = new IsolatedRoot();
        // int.MaxValue 不可能是存活进程的 pid（Linux 上限 4194304，Windows 的 pid 远小于此）。
        var impossible = scope.MakeSession($"{int.MaxValue}-ffffffff");

        var removed = AppPaths.PruneStaleSessions();

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(impossible));
    }

    /// <summary>真实探针下"自己的会话"同样留住 —— 顺带证明默认路径不会把本进程的目录清掉。</summary>
    [Fact]
    public void PruneStaleSessions_RealProbeKeepsOwnSession()
    {
        using var scope = new IsolatedRoot();
        var own = AppPaths.NewTempSubdir("own-probe");

        Assert.Equal(0, AppPaths.PruneStaleSessions());
        Assert.True(Directory.Exists(own));
    }

    // ─────────────────── 历史平铺残留：按名字形态清理 ───────────────────

    /// <summary>
    /// 「远未来」的 cutoff：让刚造出来的目录都算「旧」。
    /// 反向场景（要验"新的不删"）自己传过去的时刻。
    /// </summary>
    private static DateTime FutureCutoff => DateTime.UtcNow.AddMinutes(1);

    /// <summary>
    /// 第十轮～第十一轮之间直接平铺在临时根**直下**的 <c>&lt;prefix&gt;-&lt;guid&gt;</c>
    /// 目录要被清掉 —— 这就是本机 <c>~/.config/zhishu-hmi/temp/</c> 里那批残留的形态。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_RemovesFlatGuidDirs()
    {
        using var scope = new IsolatedRoot();
        var a = scope.MakeFlatDir("so-build-" + Guid.NewGuid().ToString("N"), withFile: true);
        var b = scope.MakeFlatDir("written-" + Guid.NewGuid().ToString("N"), withFile: true);

        var removed = AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff);

        Assert.Equal(2, removed);
        Assert.False(Directory.Exists(a), $"应已删除：{a}");
        Assert.False(Directory.Exists(b), $"应已删除：{b}");
    }

    /// <summary>
    /// **稳定名工作目录一个都不能碰**。它们是跨会话复用（增量编译）的载体，
    /// 名字里**没有** guid 后缀 —— 这正是与历史形态的区别，也是本清理判据成立的前提。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_KeepsStableNameWorkDirs()
    {
        using var scope = new IsolatedRoot();
        var stable = new List<string>();
        foreach (var name in new[] { "so-build", "compile", "download", "sim" })
            stable.Add(scope.MakeFlatDir(name, withFile: true));

        Assert.Equal(0, AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff));
        Assert.All(stable, d => Assert.True(Directory.Exists(d), $"不该删：{d}"));
    }

    /// <summary>
    /// 形态判据必须**严格**：差一个字符就不该删。
    /// 判据越贴近「只有旧实现能造出来」，误删面越小 —— 放宽一位，以后任何
    /// 「前缀 + 短后缀」的目录都会被误伤。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_KeepsNamesThatAreNotGuidSuffixed()
    {
        using var scope = new IsolatedRoot();
        const string hex32 = "0123456789abcdef0123456789abcdef";   // 固定值：可预测，且含 'a'

        var kept = new List<string>
        {
            scope.MakeFlatDir("so-build-abc"),                         // 后缀太短
            scope.MakeFlatDir("so-build-" + hex32[..31]),              // 少一位
            scope.MakeFlatDir("so-build-" + hex32 + "x"),              // 多一位
            scope.MakeFlatDir("so-build-" + hex32.ToUpperInvariant()), // 大写（ToString("N") 只产小写）
            scope.MakeFlatDir("so-build-" + hex32.Replace('a', 'g')),  // 非十六进制字符
            scope.MakeFlatDir(hex32),                                  // 没有前缀段
            scope.MakeFlatDir("-" + hex32),                            // 空前缀
        };

        Assert.Equal(0, AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff));
        Assert.All(kept, d => Assert.True(Directory.Exists(d), $"不该删：{d}"));
    }

    /// <summary>
    /// 比 cutoff 新的目录不删。这是「删别人的目录」这条路上唯一的保守判据：
    /// 另一个还在运行的实例（旧版本）刚建的工作目录可能正被使用。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_KeepsDirsNewerThanCutoff()
    {
        using var scope = new IsolatedRoot();
        var fresh = scope.MakeFlatDir("so-build-" + Guid.NewGuid().ToString("N"), withFile: true);

        var removed = AppPaths.PruneLegacyFlatWorkDirs(DateTime.UtcNow.AddHours(-1));

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(fresh), $"不该删（可能另有活主）：{fresh}");
    }

    /// <summary>
    /// 现行唯一名产物（落在 <c>.sessions/</c> 之下）绝不能被这条清理误伤 ——
    /// 否则「按形态清」就变成了「把现行产物也清掉」。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_DoesNotRemoveCurrentUniqueNameProducts()
    {
        using var scope = new IsolatedRoot();
        var current = AppPaths.NewTempSubdir("so-build");
        Assert.True(IsUnder(current, AppPaths.SessionTempRoot), $"前提不成立：{current}");

        var removed = AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(current), "现行唯一名产物不该被历史清理删掉");
    }

    /// <summary>
    /// 幂等：没有残留时返回 0、重复调用无副作用。
    /// 这正是「按形态清」优于「打标记只做一次」的地方 —— 不需要记录"迁移已做"。
    /// </summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_IsIdempotent()
    {
        using var scope = new IsolatedRoot();
        scope.MakeFlatDir("so-build-" + Guid.NewGuid().ToString("N"), withFile: true);

        Assert.Equal(1, AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff));
        Assert.Equal(0, AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff));
    }

    /// <summary>临时根还不存在时是空操作（首次运行就是这个状态）。</summary>
    [Fact]
    public void PruneLegacyFlatWorkDirs_IsANoOpWhenTempRootIsMissing()
    {
        using var scope = new IsolatedRoot();
        Assert.False(Directory.Exists(AppPaths.TempRoot), "前提：隔离根下还没有临时根");

        Assert.Equal(0, AppPaths.PruneLegacyFlatWorkDirs(FutureCutoff));
    }

    // ─────────────────────────── 脚手架 ───────────────────────────

    /// <summary>
    /// 把应用数据根临时指到独立目录，跑完还原。清理类测试要造"别的进程的会话目录"，
    /// 必须在**自己的**临时根里造 —— 否则会删到并行跑的其他测试正在用的东西。
    /// </summary>
    private sealed class IsolatedRoot : IDisposable
    {
        private readonly string? _previous;

        public IsolatedRoot()
        {
            _previous = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);
            Root = TestTempDir.NewDir("apppaths-prune");
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, Root);
        }

        public string Root { get; }

        /// <summary>在会话父目录下造一个指定名字的会话目录（带一个文件，确保非空）。</summary>
        public string MakeSession(string sessionDirName)
        {
            var dir = Path.Combine(AppPaths.TempRoot, ".sessions", sessionDirName);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "payload.txt"), "x");
            return dir;
        }

        /// <summary>
        /// 在临时根**直下**造一个指定名字的目录（历史残留与稳定名工作目录都是这个位置，
        /// 区别只在名字形态）。<paramref name="withFile"/> 用于模拟"里面有中间产物"。
        /// </summary>
        public string MakeFlatDir(string name, bool withFile = false)
        {
            var dir = Path.Combine(AppPaths.TempRoot, name);
            Directory.CreateDirectory(dir);
            if (withFile)
                File.WriteAllText(Path.Combine(dir, "payload.txt"), "x");
            return dir;
        }

        public void Dispose()
        {
            // 先在**隔离根**上释放本进程的会话目录，再还原环境变量 ——
            // 顺序反了就会去删原始根里的会话（与本测试无关的目录）。
            AppPaths.ReleaseSessionTemp();
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, _previous);
            TestTempDir.DeleteDir(Root);
        }
    }

    /// <summary>严格前缀判断：`/a/bc` 不算在 `/a/b` 之下。</summary>
    private static bool IsUnder(string path, string ancestor)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ancestor));
        return p.Equals(a, StringComparison.Ordinal)
            || p.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
