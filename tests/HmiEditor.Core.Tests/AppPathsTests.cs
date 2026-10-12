using System;
using System.IO;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="AppPaths"/> 行为测试：两个根的语义、唯一性、以及"拼路径不建目录"的区分。
///
/// <para>这些性质原来没有任何测试 —— 10 处调用点各写各的
/// <c>Path.Combine(Path.GetTempPath(), "固定名")</c>，直到一次全量跑随机红
/// （填满 <c>/tmp</c> 必红）才暴露。测试进程的应用数据根由
/// <c>tests/_shared/TestAppDataRootSetup.cs</c> 统一重定向到仓库内目录。</para>
///
/// <para>归入 <c>AppPaths-global</c> 集合（见 <see cref="AppPathsSessionTests"/>）：
/// 本类会中途改 <c>HMI_APP_DATA_ROOT</c>，同集合内串行执行。</para>
/// </summary>
[Collection("AppPaths-global")]
public class AppPathsTests
{
    /// <summary>应用数据根被测试基础设施重定向过（否则本文件会往真实配置目录写）。</summary>
    [Fact]
    public void DataRoot_IsRedirectedInTestProcess()
    {
        var redirected = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);
        Assert.False(string.IsNullOrWhiteSpace(redirected),
            "测试进程必须设置 HMI_APP_DATA_ROOT（见 tests/_shared/TestAppDataRootSetup.cs）");
        Assert.Equal(Path.GetFullPath(redirected!), AppPaths.DataRoot);
    }

    [Fact]
    public void TempRoot_IsTheTempChildOfDataRoot()
    {
        Assert.Equal(Path.Combine(AppPaths.DataRoot, "temp"), AppPaths.TempRoot);
    }

    [Fact]
    public void DataRoot_HonoursEnvironmentOverride_AndFollowsTempRoot()
    {
        var original = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);
        var custom = TestTempDir.NewFilePath("apppaths-override-root");
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, custom);

            // 有意不做缓存：改环境变量必须**立刻**生效。
            // 若用 Lazy 缓存，"先访问后改"的调用方会静默拿到旧值。
            Assert.Equal(Path.GetFullPath(custom), AppPaths.DataRoot);

            // 派生值必须跟着走，否则"统一工作根"就只统一了一半。
            Assert.Equal(Path.Combine(Path.GetFullPath(custom), "temp"), AppPaths.TempRoot);
        }
        finally
        {
            // 还原成重定向值（不是 null）—— 同进程其它测试依赖它。
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, original);
        }
    }

    [Fact]
    public void NewTempSubdir_CreatesUniqueDirectoriesUnderTempRoot()
    {
        try
        {
            var a = AppPaths.NewTempSubdir("so-build");
            var b = AppPaths.NewTempSubdir("so-build");

            Assert.True(Directory.Exists(a), $"应已创建：{a}");
            Assert.True(Directory.Exists(b), $"应已创建：{b}");
            Assert.NotEqual(a, b);                       // 并发调用不互相踩
            Assert.True(IsUnder(a, AppPaths.TempRoot), $"应落在临时根下：{a}");
            Assert.True(IsUnder(b, AppPaths.TempRoot), $"应落在临时根下：{b}");
            Assert.Contains("so-build", Path.GetFileName(a));   // 前缀可读，便于人工清理
        }
        finally
        {
            // 测试自己不留垃圾：不释放的话，每跑一次全量就在临时根里多一个 so-build-<guid>。
            AppPaths.ReleaseSessionTemp();
        }
    }

    [Fact]
    public void NewTempFilePath_IsUniqueAndDoesNotCreateTheFile()
    {
        var a = AppPaths.NewTempFilePath("comm-config", ".ini");
        var b = AppPaths.NewTempFilePath("comm-config", ".ini");

        Assert.NotEqual(a, b);                       // 固定名会让两个实例互相覆盖
        Assert.False(File.Exists(a), "本方法只给路径，不建文件");
        Assert.False(File.Exists(b), "本方法只给路径，不建文件");
        Assert.Equal(".ini", Path.GetExtension(a));
        Assert.True(IsUnder(a, AppPaths.TempRoot), $"应落在临时根下：{a}");

        // 但父目录要就绪：调用方拿到路径就该能直接写。
        Assert.True(Directory.Exists(Path.GetDirectoryName(a)), "父目录应已创建");
    }

    [Fact]
    public void TempPath_OnlyCombines_WithoutCreatingAnything()
    {
        var unique = "not-created-" + Guid.NewGuid().ToString("N");
        var path = AppPaths.TempPath(unique, "libhmi_ui.so");

        Assert.Equal(Path.Combine(AppPaths.TempRoot, unique, "libhmi_ui.so"), path);
        // 读侧（探测候选是否存在）不该有建目录的副作用。
        Assert.False(Directory.Exists(Path.Combine(AppPaths.TempRoot, unique)),
            "TempPath 不得创建目录");
    }

    [Fact]
    public void TempSubdir_IsTheWritingSideOfTempPath()
    {
        var unique = "written-" + Guid.NewGuid().ToString("N");
        var dir = AppPaths.TempSubdir(unique, "sub");

        try
        {
            Assert.Equal(AppPaths.TempPath(unique, "sub"), dir);
            Assert.True(Directory.Exists(dir), "TempSubdir 必须建目录");
        }
        finally
        {
            // 稳定名目录不受会话释放管，得自己收 —— 否则每跑一次多一个 written-<guid>。
            TestTempDir.DeleteDir(Path.Combine(AppPaths.TempRoot, unique));
        }
    }

    /// <summary>
    /// 用户产物（AI 图标/图像）**不能**落在可清理的临时根：原实现落在系统临时目录，
    /// 系统清理临时目录时用户"保存"的资产会直接消失。
    /// </summary>
    [Fact]
    public void AssetDir_IsPersistentUnderDataRoot_NotUnderTempRoot()
    {
        var dir = AppPaths.AssetDir("ai-icons-unit-" + Guid.NewGuid().ToString("N"));

        Assert.True(Directory.Exists(dir), $"应已创建：{dir}");
        Assert.True(IsUnder(dir, AppPaths.DataRoot), $"应落在持久数据根下：{dir}");
        Assert.False(IsUnder(dir, AppPaths.TempRoot), $"不得落在可清理的临时根下：{dir}");
    }

    [Fact]
    public void AssetDir_RejectsBlankKind()
    {
        Assert.Throws<ArgumentException>(() => AppPaths.AssetDir("   "));
    }

    [Fact]
    public void NewTempFilePath_RejectsBlankPrefix()
    {
        Assert.Throws<ArgumentException>(() => AppPaths.NewTempFilePath("  "));
    }

    // ── 历史数据根（只读兼容） ───────────────────────────────────────────
    // 测试进程把两个根**都**重定向了（见 tests/_shared/TestAppDataRootSetup.cs），
    // 所以"默认目录名"这类性质只在**没有覆盖**时才可见 —— 这几条测试按需临时清空变量。

    /// <summary>
    /// 历史数据根用的是**旧目录名**，不是当前名 —— 两者必须真的不同，否则"回退读"就退到同一个目录，
    /// 兼容逻辑退化成空操作（读得到也看不出错，但升级用户的数据就再也接不上了）。
    /// </summary>
    [Fact]
    public void LegacyDataRoot_DefaultsToTheHistoricalFolderName_NotTheCurrentOne()
    {
        var saved = SuspendDataRootOverrides();
        try
        {
            var legacy = AppPaths.LegacyDataRoot;

            Assert.Equal(AppPaths.LegacyAppFolderName, Path.GetFileName(legacy));
            Assert.Equal(AppPaths.AppFolderName, Path.GetFileName(AppPaths.DataRoot));
            Assert.NotEqual(AppPaths.DataRoot, legacy);

            // 同一个 %APPDATA% 之下，只有末段目录名不同 —— 否则"历史位置"会跑到另一个盘上。
            Assert.Equal(Path.GetDirectoryName(AppPaths.DataRoot),
                         Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(legacy)));
        }
        finally
        {
            RestoreDataRootOverrides(saved);
        }
    }

    /// <summary>
    /// 解析历史根**不得创建目录**：新装用户根本没有它，创建会凭空造出一个空目录
    /// （这也是"它是只读的"这句话的字面含义）。
    /// </summary>
    [Fact]
    public void LegacyDataRoot_IsResolvedWithoutCreatingTheDirectory()
    {
        var saved = SuspendDataRootOverrides();
        var absent = TestTempDir.NewFilePath("legacy-root-absent");   // 只给路径，不建任何东西
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, absent);

            Assert.Equal(Path.GetFullPath(absent), AppPaths.LegacyDataRoot);
            Assert.False(Directory.Exists(absent), $"解析历史根不该创建目录：{absent}");

            // 拼文件路径同样不该有副作用（读侧要能在"文件不存在"时直接判定）。
            Assert.False(Directory.Exists(absent));
        }
        finally
        {
            RestoreDataRootOverrides(saved);
        }
    }

    /// <summary>
    /// 新旧路径必须由**同一个文件名字面量**推导：调用方只写一次文件名，
    /// 改名时不会漏改另一半。
    ///
    /// <para>"同名**不同根**"这一半必须在**默认解析链**下验：覆盖变量下两个根是外部给定的，
    /// 断言恒真 ⇒ 看着像被守，其实没守（这条初版正是这么写的，NV 少红一条才暴露）。</para>
    /// </summary>
    [Fact]
    public void LegacyDataFile_UsesTheSameFileNameAsDataFile()
    {
        const string name = "ai.config.json";

        Assert.Equal(Path.Combine(AppPaths.DataRoot, name), AppPaths.DataFile(name));
        Assert.Equal(Path.Combine(AppPaths.LegacyDataRoot, name), AppPaths.LegacyDataFile(name));

        var saved = SuspendDataRootOverrides();
        try
        {
            // 默认链下两个根落在不同目录 ⇒ 同一个文件名得到两条不同路径 —— 这正是"回退读"的前提。
            Assert.NotEqual(AppPaths.DataFile(name), AppPaths.LegacyDataFile(name));
        }
        finally
        {
            RestoreDataRootOverrides(saved);
        }

        Assert.Throws<ArgumentException>(() => AppPaths.LegacyDataFile("  "));
    }

    /// <summary>临时清空两个数据根变量，返回还原用的快照（默认解析链只在无覆盖时可见）。</summary>
    private static (string? Data, string? Legacy) SuspendDataRootOverrides()
    {
        var saved = (Data: Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar),
                     Legacy: Environment.GetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar));
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, null);
        Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, null);
        return saved;
    }

    /// <summary>还原 <see cref="SuspendDataRootOverrides"/> 清掉的变量 —— 同进程其它测试依赖它们。</summary>
    private static void RestoreDataRootOverrides((string? Data, string? Legacy) saved)
    {
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, saved.Data);
        Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, saved.Legacy);
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
