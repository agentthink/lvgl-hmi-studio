using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HmiEditor.TestSupport;

/// <summary>
/// 测试进程的**应用路径重定向**：把 <c>HMI_APP_DATA_ROOT</c> 指到仓库内可清理的
/// <c>.hmi-test-tmp/appdata/</c>，使被测产品代码（<see cref="HmiEditor.Core.Services.AppPaths"/>）
/// 不往**真实用户配置目录**写。
///
/// <para><b>为什么需要：</b>产品侧原先的中间产物落在系统临时目录（污染 <c>/tmp</c>）；
/// 收口到 <c>AppPaths</c> 后，默认根变成 <c>%APPDATA%/zhishu-hmi</c> —— 若不重定向，
/// 测试会把编译/部署的中间产物写进开发者**真实的**配置目录。
/// 两个根都不该被测试污染，所以测试进程显式接管。</para>
///
/// <para><b>历史根同样要接管：</b><c>HMI_APP_LEGACY_DATA_ROOT</c> 指向
/// <c>%APPDATA%/HmiEditor</c>（升级前的目录名）。它本身是只读的，但**读**也会出问题 ——
/// 验证"升级后首次读取回退历史配置"的测试会读到开发者**真实的** <c>ai.config.json</c>
/// （里面可能有真的 API Key），结论随机器而变。所以两个变量一起接管，
/// 且历史根指到**同级但不同名**的子目录，保留"两个根确实不同"这个事实。</para>
///
/// <para><b>为什么用 <c>ModuleInitializer</c>：</b>必须在任何被测代码首次读
/// <c>AppPaths</c> <b>之前</b>生效。放在每个测试类里都不可靠（xunit 的执行顺序不保证），
/// 而模块初始化器在程序集加载时就跑。</para>
///
/// <para><b>幂等与可覆盖：</b>外部已设置对应环境变量时**不覆盖**
/// （CI / 人工排查可以显式指定）。本文件被多个测试程序集各编译一份，
/// 但写入的是同一个值，重复执行无副作用。</para>
/// </summary>
internal static class TestAppDataRootSetup
{
    /// <summary>与应用侧同名：显式指定后本重定向让位。</summary>
    private const string EnvVar = "HMI_APP_DATA_ROOT";

    /// <summary>历史数据根（只读）的覆盖变量，与应用侧同名。</summary>
    private const string LegacyEnvVar = "HMI_APP_LEGACY_DATA_ROOT";

    private const string RepoFallbackDir = ".hmi-test-tmp";

    [ModuleInitializer]
    internal static void Redirect()
    {
        try
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot is null)
                return;   // 找不到仓库根就别动环境：宁可保持默认，也不猜一个位置

            var baseDir = Path.Combine(repoRoot, RepoFallbackDir);
            RedirectOne(EnvVar, Path.Combine(baseDir, "appdata"));
            RedirectOne(LegacyEnvVar, Path.Combine(baseDir, "appdata-legacy"));
        }
        catch (Exception)
        {
            // 模块初始化器里抛异常会让整个测试程序集加载失败，代价远大于收益。
            // 重定向失败只是"可能写进真实配置目录"，不影响测试结论。
        }
    }

    /// <summary>把一个环境变量指到 <paramref name="root"/>；外部已设置时不动。</summary>
    private static void RedirectOne(string envVar, string root)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVar)))
            return;

        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(envVar, root);
    }

    /// <summary>从测试程序集输出目录向上找含 <c>HmiEditor.slnx</c> 的仓库根。</summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
