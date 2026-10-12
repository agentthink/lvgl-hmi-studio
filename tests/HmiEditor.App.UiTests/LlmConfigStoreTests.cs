using System;
using System.IO;
using System.Text.Json;
using HmiEditor.AI.Inference;
using HmiEditor.App.Services;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 本集合里的测试会临时把 <c>HMI_APP_DATA_ROOT</c> / <c>HMI_APP_LEGACY_DATA_ROOT</c>
/// 指到隔离目录；期间**任何**并发读 <see cref="AppPaths"/> 的测试都会看到隔离值，
/// 所以禁止并行。与 Core.Tests 的 <c>AppPaths-global</c> 是同一类问题、同一解法——
/// 两个测试程序集各自一个 testhost 进程，集合名不需要跨程序集一致。
/// </summary>
[CollectionDefinition("AppDataRoot-global", DisableParallelization = true)]
public sealed class AppDataRootGlobalCollection;

/// <summary>
/// <see cref="LlmConfigStore"/> 的行为测试 —— 重点是**升级回退**这条静默失效路径。
///
/// <para><b>为什么必须有行为测试：</b>数据目录名从 <c>HmiEditor</c> 改成 <c>zhishu-hmi</c> 时，
/// 若"读一份、写另一份"的方向写反了，用户**已配置的端点/密钥会静默失效**——
/// 对话框照常显示、不报错，只是发出去的请求全都不带凭据。源码级守卫
/// （<c>AppPathsSingleSourceGuardTests</c>）只能证明"路径只有一个来源"，
/// 证明不了"读回退、写不回退"这个方向。</para>
///
/// <para><b>最容易写错的一行</b>是回退判据：<c>File.Exists(新位置)</c> 还是"新位置读不出来"。
/// 前者只在"升级后第一次运行"回退；后者会在新文件**损坏**时把历史配置
/// （可能含已废弃的密钥）静默复活。<see cref="Load_CorruptNewLocation_ReturnsDefaults_NotLegacy"/>
/// 专门守这条分界。</para>
///
/// <para>路径本身（<c>ConfigFilePath</c> / <c>LegacyConfigFilePath</c>）由
/// <c>AppPathsTests</c> 在 Core 侧覆盖，这里只验**存储行为**。</para>
/// </summary>
[Collection("AppDataRoot-global")]
public class LlmConfigStoreTests
{
    /// <summary>文件名字面量：这里有意重复一次，因为"新旧同名"正是被验的性质之一。</summary>
    private const string FileName = "ai.config.json";

    private const string NewKey = "NEW-KEY-from-current-location";
    private const string LegacyKey = "LEGACY-KEY-from-old-folder";

    [Fact]
    public void Paths_CurrentUnderDataRoot_LegacyUnderLegacyRoot()
    {
        using var box = new Sandbox();

        Assert.Equal(Path.Combine(box.DataRoot, FileName), LlmConfigStore.ConfigFilePath);
        Assert.Equal(Path.Combine(box.LegacyRoot, FileName), LlmConfigStore.LegacyConfigFilePath);
        Assert.NotEqual(LlmConfigStore.ConfigFilePath, LlmConfigStore.LegacyConfigFilePath);
    }

    /// <summary>升级后第一次运行：新位置还没有文件 ⇒ 读历史位置，用户配置不丢。</summary>
    [Fact]
    public void Load_CurrentLocationMissing_FallsBackToLegacy()
    {
        using var box = new Sandbox();
        WriteConfig(box.LegacyFilePath, new LlmProviderConfig
        {
            Provider = "openai",
            ApiKey = LegacyKey,
            Endpoint = "https://legacy.example/v1",
        });

        var config = LlmConfigStore.Load();

        Assert.Equal("openai", config.Provider);
        Assert.Equal(LegacyKey, config.ApiKey);
        Assert.Equal("https://legacy.example/v1", config.Endpoint);

        // 读历史位置不该顺手"搬"到新位置 —— 写入只发生在用户显式保存时。
        Assert.False(File.Exists(box.NewFilePath), "Load 不得在新位置落文件");
    }

    /// <summary>
    /// 新位置已有文件 ⇒ **一律**用它，不看历史位置。两个位置的值刻意做成可区分
    /// （不同 Provider + 不同 ApiKey），否则"读错那一份"可能偶然通过。
    /// </summary>
    [Fact]
    public void Load_CurrentLocationPresent_DoesNotFallBackToLegacy()
    {
        using var box = new Sandbox();
        WriteConfig(box.LegacyFilePath, new LlmProviderConfig { Provider = "openai", ApiKey = LegacyKey });
        WriteConfig(box.NewFilePath, new LlmProviderConfig { Provider = "claude", ApiKey = NewKey });

        var config = LlmConfigStore.Load();

        Assert.Equal("claude", config.Provider);
        Assert.Equal(NewKey, config.ApiKey);
    }

    /// <summary>
    /// 新位置**损坏** ≠ 新位置**不存在**：前者回默认值（并让用户重新配置），
    /// 后者才回历史。若把两者混为一谈，新文件被改坏时会静默启用历史凭据 ——
    /// 用户改了设置却"没生效"，比从头配一遍难查得多。
    /// </summary>
    [Fact]
    public void Load_CorruptNewLocation_ReturnsDefaults_NotLegacy()
    {
        using var box = new Sandbox();
        WriteConfig(box.LegacyFilePath, new LlmProviderConfig { Provider = "openai", ApiKey = LegacyKey });
        Directory.CreateDirectory(box.DataRoot);
        File.WriteAllText(box.NewFilePath, "{ this is definitely not valid json");

        var config = LlmConfigStore.Load();

        Assert.Equal(new LlmProviderConfig().Provider, config.Provider);
        Assert.Equal("", config.ApiKey);
        Assert.NotEqual(LegacyKey, config.ApiKey);
    }

    /// <summary>两处都没有 ⇒ 默认配置，且**不留副作用**（只读不得创建任何目录）。</summary>
    [Fact]
    public void Load_NeitherLocationExists_ReturnsDefaults_WithoutCreatingAnything()
    {
        using var box = new Sandbox();

        var config = LlmConfigStore.Load();

        Assert.Equal(new LlmProviderConfig().Provider, config.Provider);
        Assert.Equal("", config.ApiKey);
        Assert.False(Directory.Exists(box.DataRoot), "只读不得创建数据根");
        Assert.False(Directory.Exists(box.LegacyRoot), "只读不得创建历史根");
    }

    /// <summary>
    /// 保存**只写新位置**，历史文件逐字节不动。这条守的是方向：
    /// 一旦有人把 <c>Save</c> 改成"同时写两处"（或写回历史），回退就从"冻结旧文件"
    /// 变成"新旧互相覆盖"，两个实例会按启动顺序争夺同一份配置。
    /// </summary>
    [Fact]
    public void Save_WritesOnlyCurrentLocation_AndLeavesLegacyFrozen()
    {
        using var box = new Sandbox();
        WriteConfig(box.LegacyFilePath, new LlmProviderConfig { Provider = "openai", ApiKey = LegacyKey });
        var legacyBefore = File.ReadAllBytes(box.LegacyFilePath);

        Assert.True(LlmConfigStore.Save(new LlmProviderConfig { Provider = "openai", ApiKey = NewKey }));

        Assert.True(File.Exists(box.NewFilePath), "保存必须落在新位置");
        Assert.Equal(legacyBefore, File.ReadAllBytes(box.LegacyFilePath));

        // 存完再读，拿到的必须是刚写的那份（顺带证明序列化格式可往返）。
        Assert.Equal(NewKey, LlmConfigStore.Load().ApiKey);
    }

    /// <summary>数据根还不存在时也要能存下来（首次配置就是这种情况）。</summary>
    [Fact]
    public void Save_CreatesDataRoot_WhenMissing()
    {
        using var box = new Sandbox();
        Assert.False(Directory.Exists(box.DataRoot));

        Assert.True(LlmConfigStore.Save(new LlmProviderConfig { ApiKey = NewKey }));

        Assert.True(File.Exists(box.NewFilePath));
        Assert.Equal(NewKey, LlmConfigStore.Load().ApiKey);
    }

    /// <summary>用与产品侧**同一个**序列化预设造文件，保证造出来的历史文件与真实升级场景同格式。</summary>
    private static void WriteConfig(string path, LlmProviderConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptionsDefaults.LlmConfig));
    }

    /// <summary>
    /// 一对隔离的数据根 / 历史根 + 环境变量接管。历史根**有意不预建**——
    /// "新装用户没有这个目录"正是要验的场景。
    ///
    /// <para>两个根从**当前**（已被 <c>tests/_shared/TestAppDataRootSetup.cs</c> 重定向过的）
    /// 数据根的同级派生，于是落在仓库内可清理的 <c>.hmi-test-tmp/</c> 下，
    /// 既不污染开发者真实配置目录，也不会留在 <c>/tmp</c>。</para>
    /// </summary>
    private sealed class Sandbox : IDisposable
    {
        private readonly string? _savedDataRoot;
        private readonly string? _savedLegacyRoot;

        public Sandbox()
        {
            Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar)),
                "测试进程必须已被重定向（见 tests/_shared/TestAppDataRootSetup.cs）" +
                "，否则下面的隔离目录会建到开发者真实配置目录下");

            var baseline = AppPaths.DataRoot;
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(baseline))!;
            var id = Guid.NewGuid().ToString("N");
            DataRoot = Path.Combine(parent, $"llm-store-data-{id}");
            LegacyRoot = Path.Combine(parent, $"llm-store-legacy-{id}");

            _savedDataRoot = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvVar);
            _savedLegacyRoot = Environment.GetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar);
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvVar, DataRoot);
            Environment.SetEnvironmentVariable(AppPaths.LegacyDataRootEnvVar, LegacyRoot);
        }

        public string DataRoot { get; }

        public string LegacyRoot { get; }

        public string NewFilePath => Path.Combine(DataRoot, FileName);

        public string LegacyFilePath => Path.Combine(LegacyRoot, FileName);

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
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
