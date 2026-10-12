using HmiEditor.Core.Services;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Updates;
using System.Text;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// HmiDeployService 部署流程测试：
/// 源码 → /mnt/UDISK/hmi/src/，资源 → /mnt/UDISK/hmi/assets/，
/// libhmi_ui.so → /mnt/UDISK/hmi/ + killall hmi_runtime（procd 自动拉起），
/// respawn 失败时 /etc/init.d/S97hmiruntime restart 兜底。
/// </summary>
public class HmiDeployServiceTests
{
    private sealed class FakeChannel : IDeviceChannel
    {
        public string Name => "fake";
        public bool IsOpen => true;
        public string Kind => "fake";

        public List<string> Commands { get; } = new();
        public List<(string Local, string Remote)> Transfers { get; } = new();

        /// <summary>
        /// 每个被传输的本地文件在**传输那一刻**的内容快照（键为本地路径）。
        ///
        /// <para>为什么要快照：产品现在把中转文件用完即删（唯一名 + <c>finally</c> 清理），
        /// 事后 <c>File.ReadAllText(transfer.Local)</c> 会读不到文件。这同时也让断言
        /// **不再耦合产品的中转目录** —— 那正是"测试依赖产品往共享临时目录写文件"
        /// 这条耦合的来源。</para>
        /// </summary>
        public Dictionary<string, byte[]> Captured { get; } = new();

        /// <summary>命令响应器：返回 null 时回复空串。</summary>
        public Func<string, string?>? Responder { get; set; }

        public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CloseAsync() => Task.CompletedTask;

        public Task<string> SendCommandAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Commands.Add(command);
            return Task.FromResult(Responder?.Invoke(command) ?? "");
        }

        public Task TransferFileAsync(string localPath, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            // 先快照再登记：若产品在 finally 里删了文件，事后反读就没了。
            if (File.Exists(localPath))
                Captured[localPath] = File.ReadAllBytes(localPath);

            Transfers.Add((localPath, remotePath));
            progress?.Report(1.0);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private static string MakeTempFile(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>构造合法 ARM ELF 头文件（IsArmElf 校验通过）。</summary>
    private static string MakeArmElf(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        var bytes = new byte[64];
        bytes[0] = 0x7F; bytes[1] = (byte)'E'; bytes[2] = (byte)'L'; bytes[3] = (byte)'F';
        bytes[4] = 1; // ELFCLASS32
        bytes[5] = 1; // 小端
        bytes[18] = 40; // EM_ARM
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task DeployAsync_WithSoAndAssets_DeploysSourcesAssetsSoAndRestartsRuntime()
    {
        // Arrange
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var src1 = MakeTempFile(dir, "generated_hmi.c", "/* ui */");
            var src2 = MakeTempFile(dir, "Makefile", "all:");
            var so = MakeArmElf(dir, "libhmi_ui.so");
            var asset = MakeTempFile(dir, "logo.png", "png");

            var channel = new FakeChannel
            {
                Responder = cmd => cmd.Contains("pgrep") ? "HMI_RESTART_OK" : null
            };
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };
            var logs = new List<string>();
            service.Log += logs.Add;

            // Act
            var result = await service.DeployAsync(
                channel,
                new[] { src1, src2 },
                so,
                new[] { new HmiDeployAsset(asset, "logo.png") });

            // Assert
            Assert.True(result.SoDeployed);
            Assert.True(result.RuntimeRestarted);
            Assert.Null(result.SoDeployError);
            Assert.Equal(2, result.SourceFilesUploaded);
            Assert.Equal(1, result.AssetsUploaded);
            Assert.Equal(so, result.SharedLibraryPath);

            // 目录准备
            Assert.Contains("mkdir -p /mnt/UDISK/hmi/src", channel.Commands);
            Assert.Contains("mkdir -p /mnt/UDISK/hmi/assets", channel.Commands);
            Assert.Contains("mkdir -p /mnt/UDISK/hmi", channel.Commands);

            // 重启命令必须分开执行（killall 与 pgrep 不同一条命令，避免静默判定截断 sleep）
            var killCmd = channel.Commands.First(c => c.Contains("killall hmi_runtime"));
            Assert.Contains("HMI_KILLED", killCmd);
            Assert.DoesNotContain("HMI_RESTART_OK", killCmd);
            Assert.DoesNotContain("sleep", killCmd);
            Assert.Contains(channel.Commands, c => c.Contains("pgrep hmi_runtime"));
            // 首轮 pgrep 成功 → 不应触发 init 脚本兜底
            Assert.DoesNotContain(channel.Commands, c => c.Contains("S97hmiruntime restart"));

            // 传输目标路径
            Assert.Contains((src1, "/mnt/UDISK/hmi/src/generated_hmi.c"), channel.Transfers);
            Assert.Contains((src2, "/mnt/UDISK/hmi/src/Makefile"), channel.Transfers);
            Assert.Contains((asset, "/mnt/UDISK/hmi/assets/logo.png"), channel.Transfers);
            Assert.Contains((so, "/mnt/UDISK/hmi/libhmi_ui.so"), channel.Transfers);

            Assert.Contains(logs, l => l.Contains("重启 hmi_runtime"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_RespawnFails_FallsBackToInitScriptRestart()
    {
        // Arrange：首轮 pgrep 失败 → S97 兜底重启 → 二轮 pgrep 成功
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var so = MakeArmElf(dir, "libhmi_ui.so");
            var pgrepCount = 0;
            var channel = new FakeChannel
            {
                Responder = cmd =>
                {
                    if (cmd.Contains("pgrep"))
                    {
                        pgrepCount++;
                        return pgrepCount >= 2 ? "HMI_RESTART_OK" : "HMI_RESTART_FAIL";
                    }
                    return null;
                }
            };
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };
            var logs = new List<string>();
            service.Log += logs.Add;

            // Act
            var result = await service.DeployAsync(channel, Array.Empty<string>(), so);

            // Assert
            Assert.True(result.SoDeployed);
            Assert.True(result.RuntimeRestarted);
            Assert.Contains(channel.Commands, c => c.Contains("S97hmiruntime restart"));
            Assert.Contains(logs, l => l.Contains("兜底"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_RuntimeNeverComesBack_ReportsRestartFailed()
    {
        // Arrange：pgrep 始终失败 → 兜底后仍失败
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var so = MakeArmElf(dir, "libhmi_ui.so");
            var channel = new FakeChannel
            {
                Responder = cmd => cmd.Contains("pgrep") ? "HMI_RESTART_FAIL" : null
            };
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };
            var logs = new List<string>();
            service.Log += logs.Add;

            // Act
            var result = await service.DeployAsync(channel, Array.Empty<string>(), so);

            // Assert
            Assert.True(result.SoDeployed);
            Assert.False(result.RuntimeRestarted);
            Assert.Contains(logs, l => l.Contains("仍未运行"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_WithoutSo_OnlyUploadsSourcesAndAssets()
    {
        // Arrange
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var src = MakeTempFile(dir, "generated_hmi.c", "/* ui */");
            var channel = new FakeChannel();
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            // Act
            var result = await service.DeployAsync(channel, new[] { src });

            // Assert
            Assert.False(result.SoDeployed);
            Assert.False(result.RuntimeRestarted);
            Assert.Null(result.SharedLibraryPath);
            Assert.Null(result.SoDeployError);
            Assert.Contains((src, "/mnt/UDISK/hmi/src/generated_hmi.c"), channel.Transfers);
            Assert.DoesNotContain(channel.Commands, c => c.Contains("killall"));
            Assert.DoesNotContain(channel.Commands, c => c.Contains("/mnt/UDISK/hmi/libhmi_ui.so"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_InvalidElfSo_IsSkippedWithError()
    {
        // Arrange：内容不是 ARM ELF（如误选 x86 .so）
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var so = MakeTempFile(dir, "libhmi_ui.so", "not an elf at all");
            var channel = new FakeChannel();
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            // Act
            var result = await service.DeployAsync(channel, Array.Empty<string>(), so);

            // Assert
            Assert.False(result.SoDeployed);
            Assert.False(result.RuntimeRestarted);
            Assert.NotNull(result.SoDeployError);
            Assert.Contains("ARM ELF", result.SoDeployError);
            Assert.DoesNotContain(channel.Transfers, t => t.Remote == "/mnt/UDISK/hmi/libhmi_ui.so");
            Assert.DoesNotContain(channel.Commands, c => c.Contains("killall"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_MissingSoFile_SkipsSoDeployAndRestart()
    {
        // Arrange
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var src = MakeTempFile(dir, "generated_hmi.c", "/* ui */");
            var channel = new FakeChannel();
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            // Act：.so 路径指向不存在的文件
            var result = await service.DeployAsync(channel, new[] { src }, Path.Combine(dir, "libhmi_ui.so"));

            // Assert
            Assert.False(result.SoDeployed);
            Assert.False(result.RuntimeRestarted);
            Assert.DoesNotContain(channel.Commands, c => c.Contains("killall"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_InvalidAssetsAreSkipped()
    {
        // Arrange
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var src = MakeTempFile(dir, "generated_hmi.c", "/* ui */");
            var channel = new FakeChannel();
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            // Act：一个不存在的资源 + 一个空路径资源
            var result = await service.DeployAsync(
                channel,
                new[] { src },
                assets: new[]
                {
                    new HmiDeployAsset(Path.Combine(dir, "missing.png"), "missing.png"),
                    new HmiDeployAsset("", "empty.png")
                });

            // Assert
            Assert.Equal(0, result.AssetsUploaded);
            Assert.DoesNotContain(channel.Transfers, t => t.Remote.Contains("/assets/"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Theory]
    [InlineData(true, 0x7F, 'E', 'L', 'F', (byte)1, (byte)1, (byte)40)]   // ARM 小端 32 位
    [InlineData(false, 0x7F, 'E', 'L', 'F', (byte)1, (byte)1, (byte)62)]  // x86-64
    [InlineData(false, 0x7F, 'E', 'L', 'F', (byte)2, (byte)1, (byte)40)]  // 大端
    public void IsArmElf_ClassifiesHeaders(bool expected, byte b0, char c1, char c2, char c3, byte eiData, byte eiClass, byte machineLo)
    {
        var dir = TestTempDir.NewDir("elf");
        try
        {
            var path = Path.Combine(dir, "libhmi_ui.so");
            var bytes = new byte[64];
            bytes[0] = b0; bytes[1] = (byte)c1; bytes[2] = (byte)c2; bytes[3] = (byte)c3;
            bytes[4] = eiClass;
            bytes[5] = eiData;
            bytes[18] = machineLo;
            File.WriteAllBytes(path, bytes);

            Assert.Equal(expected, HmiDeployService.IsArmElf(path));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void IsArmElf_NonElfFile_ReturnsFalse()
    {
        var dir = TestTempDir.NewDir("elf");
        try
        {
            var path = Path.Combine(dir, "libhmi_ui.so");
            File.WriteAllText(path, "plain text, not elf");
            Assert.False(HmiDeployService.IsArmElf(path));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_RuntimeConfigText_UploadsCommConfig()
    {
        // 运行时连接配置（comm_config.ini）：IDE 通讯设置随下载下发，宿主启动读取
        var dir = TestTempDir.NewDir("deploy");
        try
        {
            var channel = new FakeChannel();
            var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            const string cfgText = "tcp.host=192.168.100.50\ntcp.port=502\n";
            var result = await service.DeployAsync(
                channel,
                Array.Empty<string>(),
                runtimeConfigText: cfgText);

            var transfer = Assert.Single(channel.Transfers);
            Assert.Equal("/mnt/UDISK/hmi/comm_config.ini", transfer.Remote);

            // 中转文件必须落在**可清理的应用临时根**下，不得落在系统临时目录：
            // 后者在本机只有 10 MB tmpfs，写满时 WriteAllTextAsync 抛
            // IOException(No space left on device) ⇒ 这条断言在旧实现下必红。
            // 等价的满盘实验见 docs/2026-09-15-app-paths-single-source.md §6.1
            // （填满 /tmp 后单跑：修复态绿、旧实现态抛 IOException）。
            Assert.True(IsUnder(transfer.Local, AppPaths.TempRoot),
                $"中转文件应落在应用临时根下：{transfer.Local}（临时根：{AppPaths.TempRoot}）");

            // 内容断言走**传输时刻的快照**，不再事后反读产品的中转文件 ——
            // 那个文件用完即删，且反读会把测试耦合到产品的中间落点。
            Assert.True(channel.Captured.ContainsKey(transfer.Local),
                $"传输时应已捕获本地文件内容：{transfer.Local}");
            Assert.Equal(cfgText, Encoding.UTF8.GetString(channel.Captured[transfer.Local]));

            // 中转文件用完即删：不清理会在临时根里无限累积（原实现更糟 —— 固定名，从不删）。
            Assert.False(File.Exists(transfer.Local),
                $"中转文件应在部署结束后被删除：{transfer.Local}");

            Assert.Contains(channel.Commands, c => c.Contains("mkdir -p /mnt/UDISK/hmi"));
            Assert.DoesNotContain(channel.Commands, c => c.Contains("rm -f /mnt/UDISK/hmi/comm_config.ini"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
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

    /// <summary>
    /// 中转文件必须是**唯一名**：两个部署并发（两个 IDE 实例、或同实例两次下载）时，
    /// 固定名会让后一次覆盖前一次正在传输的内容。
    ///
    /// <para>原实现是共享临时目录里的固定名 <c>zhishu_hmi_comm_config.ini</c>，
    /// 这条断言在那种实现下必红。</para>
    /// </summary>
    [Fact]
    public async Task DeployAsync_ConcurrentCommConfigTransfers_UseDistinctStagingFiles()
    {
        var dir = TestTempDir.NewDir("deploy-concurrent");
        try
        {
            const string cfgA = "tcp.host=192.168.100.50\ntcp.port=502\n";
            const string cfgB = "tcp.host=192.168.100.99\ntcp.port=503\n";

            var chA = new FakeChannel();
            var chB = new FakeChannel();
            var serviceA = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };
            var serviceB = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

            await Task.WhenAll(
                serviceA.DeployAsync(chA, Array.Empty<string>(), runtimeConfigText: cfgA),
                serviceB.DeployAsync(chB, Array.Empty<string>(), runtimeConfigText: cfgB));

            var a = Assert.Single(chA.Transfers);
            var b = Assert.Single(chB.Transfers);

            Assert.NotEqual(a.Local, b.Local);

            // 各自拿到自己的内容（固定名实现下这里会被对方覆盖）
            Assert.Equal(cfgA, Encoding.UTF8.GetString(chA.Captured[a.Local]));
            Assert.Equal(cfgB, Encoding.UTF8.GetString(chB.Captured[b.Local]));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public async Task DeployAsync_NoEnabledConnections_RemovesStaleCommConfig()
    {
        // 工程无启用连接 → 删除设备旧 comm_config.ini，避免残留配置继续生效
        var channel = new FakeChannel();
        var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

        var result = await service.DeployAsync(
            channel,
            Array.Empty<string>(),
            runtimeConfigText: "");

        Assert.Empty(channel.Transfers);
        Assert.Contains(channel.Commands, c => c.Contains("rm -f /mnt/UDISK/hmi/comm_config.ini"));
    }

    [Fact]
    public async Task DeployAsync_NullRuntimeConfig_DoesNothing()
    {
        // 兼容旧调用：不传 runtimeConfigText → 不触碰设备连接配置
        var channel = new FakeChannel();
        var service = new HmiDeployService { RestartProbeDelay = TimeSpan.Zero };

        await service.DeployAsync(channel, Array.Empty<string>());

        Assert.Empty(channel.Transfers);
        Assert.DoesNotContain(channel.Commands, c => c.Contains("comm_config.ini"));
    }
}
