using System;
using System.IO;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 隔离的数据根 —— 凡是**构造真实窗口/主窗口**的用例都要用它。
///
/// <para><b>它挡的是什么：</b>窗口构造与打开会往应用数据根写东西 —— 窗口几何
/// （<c>WindowGeometryService</c>，注意它的 <c>Load</c> 也会 <c>CreateDirectory</c>）、
/// 审计日志、图库、Dock 布局。不隔离的话，用例之间会通过磁盘互相影响，
/// 而且会写进开发者自己的配置目录。</para>
///
/// <para><b>为什么抽成共用类：</b>先是 <c>MainWindowConstructionTests</c> 需要（阶段 46），
/// 然后是 <c>WindowViewSmokeUiTests</c>（阶段 48）。两个消费者各抄一份 55 行的 fixture，
/// 等于把「先还原环境变量再删目录」这条顺序约定抄两遍 —— 它一旦漂移，
/// 症状是"某个用例偶尔读到即将消失的数据根"，最难查的那种红。</para>
///
/// <para><b>它从哪派生：</b>当前数据根（已被 <c>tests/_shared/TestAppDataRootSetup.cs</c>
/// 重定向过）的**同级**目录，于是落在仓库内可清理的 <c>.hmi-test-tmp/</c> 下。
/// 构造函数会断言"重定向确实发生了" —— 否则隔离目录会建到开发者真实配置目录旁边。</para>
/// </summary>
internal sealed class DataRootSandbox : IDisposable
{
    private readonly string? _savedDataRoot;
    private readonly string? _savedLegacyRoot;

    /// <param name="prefix">目录前缀，用于在 <c>.hmi-test-tmp/</c> 里认出是哪一类用例留下的。</param>
    public DataRootSandbox(string prefix = "data")
    {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar)),
            "测试进程必须已被重定向（见 tests/_shared/TestAppDataRootSetup.cs）"
            + "，否则下面的隔离目录会建到开发者真实配置目录下");

        var baseline = AppPaths.DataRoot;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(baseline))!;
        var id = Guid.NewGuid().ToString("N");
        DataRoot = Path.Combine(parent, $"{prefix}-data-{id}");
        LegacyRoot = Path.Combine(parent, $"{prefix}-legacy-{id}");

        _savedDataRoot = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);
        _savedLegacyRoot = Environment.GetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar);
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, DataRoot);
        Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, LegacyRoot);
    }

    public string DataRoot { get; }

    public string LegacyRoot { get; }

    public void Dispose()
    {
        // 先还原环境变量再删目录：否则别的测试可能在这中间读到即将消失的根。
        Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, _savedDataRoot);
        Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, _savedLegacyRoot);

        TryDeleteTree(DataRoot);
        TryDeleteTree(LegacyRoot);
    }

    private static void TryDeleteTree(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // 测试不留垃圾是目的，不是断言；删不掉不该让测试变红。
        }
    }
}
