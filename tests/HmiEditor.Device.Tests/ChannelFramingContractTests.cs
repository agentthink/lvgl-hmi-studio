using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// 收帧判据"只有一份实现"的守卫。
/// <para><b>为什么需要</b>：TCP 与串口两个通道原来各持有一份**逐字相同**的收帧代码
/// （连注释都一样），于是一个缺陷长在两处、改一处漏一处。抽成
/// <see cref="HmiEditor.Device.Channels.CommandFrameCollector"/> 之后必须守住"不再长出第二份"，
/// 否则将来给新通道补一份就又静默重复。</para>
/// <para><b>守卫成对</b>：一条证"确实委托了共享实现"，一条证"本地没有第二份"。
/// 只写前者的话，把实现复制回来时不会红。</para>
/// </summary>
public class ChannelFramingContractTests
{
    private const string SharedImplementationFile = "CommandFrameCollector.cs";

    /// <summary>判据里禁止出现的"自己算收帧"特征（历史写法）。</summary>
    private static readonly string[] LocalFramingMarkers = ["idleSince", "_dataIncoming", "_rxBuffer"];

    private static string ChannelsDir => Path.Combine(FindRepoRoot(), "src", "HmiEditor.Device", "Channels");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("未找到仓库根（应由 HmiEditor.slnx 标记）");
    }

    [Theory]
    [InlineData("TcpDeviceChannel.cs")]
    [InlineData("SerialDeviceChannel.cs")]
    public void Channel_DelegatesFramingToSharedCollector(string fileName)
    {
        var src = File.ReadAllText(Path.Combine(ChannelsDir, fileName));

        // 扫描面自检：读到的是真文件，而不是空串或半截内容 —— 否则下面几条断言会恒绿
        Assert.True(src.Length > 1500, $"{fileName} 内容异常，仅 {src.Length} 字符");
        Assert.Contains("class ", src);

        Assert.Contains("CommandFrameCollector _frames = new()", src);
        Assert.Contains("_frames.SendAsync(", src);
        Assert.Contains("_frames.Append(", src);
        Assert.Contains("CommandOutput.Sanitize(", src);
    }

    [Fact]
    public void ChannelsDirectory_HasExactlyOneFramingImplementation()
    {
        // 目录级穷举：将来新增的通道文件天然被纳入检查，无需维护文件清单
        var files = Directory.GetFiles(ChannelsDir, "*.cs");
        Assert.True(files.Length >= 5, $"Channels 目录文件数异常：{files.Length}");

        var offenders = new List<string>();
        var scanned = 0;
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == SharedImplementationFile) continue;
            var src = File.ReadAllText(file);
            scanned++;
            foreach (var marker in LocalFramingMarkers)
                if (src.Contains(marker, StringComparison.Ordinal))
                    offenders.Add($"{Path.GetFileName(file)} 含 {marker}");
        }

        Assert.True(scanned >= 4, $"实际只扫到 {scanned} 个文件，判据可能空转");
        Assert.True(offenders.Count == 0,
            "收帧判据只允许一份实现 " + SharedImplementationFile + "：" + string.Join("；", offenders));
    }

    [Fact]
    public void FramingMarkers_WouldBeCaught_IfTheyCameBack()
    {
        // 判据自检（正反例）：把历史写法喂进来必须命中；
        // 否则上面那条 DoesNotContain 可能因为特征名写错而退化成恒绿。
        const string historical = """
            var idleSince = Environment.TickCount64;
            while (true) { if (_dataIncoming) { idleSince = 0; } }
            lock (_sync) { _rxBuffer.Append("x"); }
            """;

        foreach (var marker in LocalFramingMarkers)
            Assert.Contains(marker, historical, StringComparison.Ordinal);
    }
}
