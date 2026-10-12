using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **主题 / payload 契约 v1**（阶段 264；登记 `§2-#12` 的 M1）—— 解析侧 +
/// **跨语言同源样例守卫**（设备侧 C 的 <c>k_contract_samples[]</c> 必须与这里的表**逐字段相同**）。
///
/// <para>两侧各自的判据：C 侧 <c>firmware/t113/tests/hmi_comm_test.c</c> 的
/// <c>test_mqtt_contract()</c> 拿同一张表逐字节断言 C 格式化器；本文件断言 C# 侧
/// **格式化 + 解析**，并**直接读那个 C 文件**比对样例表（漂移即红 —— 这是"两端同源"唯一
/// 不靠人记的做法）。</para>
/// </summary>
public class MqttValueContractTests
{
    private readonly ITestOutputHelper _out;

    public MqttValueContractTests(ITestOutputHelper o) => _out = o;

    private const string DeviceTestFile = "firmware/t113/tests/hmi_comm_test.c";

    /// <summary>同源样例（必须与设备侧 C 表一致；由 <see cref="Samples_MatchTheDeviceHostTestTable"/> 钉住）。</summary>
    private static readonly Sample[] Samples =
    {
        new("hmi", "temp",     12.5,       1000L,         MqttTimeBase.Boot, true,  "hmi/val/temp",     "v=12.5;t=1000;tb=boot;q=1"),
        new("hmi", "pressure", -3.25,      250L,          MqttTimeBase.Utc,  false, "hmi/val/pressure", "v=-3.25;t=250;tb=utc;q=0"),
        new("hmi", "count",    1000000.0,  1750000000L,   MqttTimeBase.Utc,  true,  "hmi/val/count",    "v=1e+06;t=1750000000;tb=utc;q=1"),
        new("hmi", "ratio",    0.125,      0L,            MqttTimeBase.Boot, true,  "hmi/val/ratio",    "v=0.125;t=0;tb=boot;q=1"),
        new("a/b", "nested",   42.0,       7L,            MqttTimeBase.Boot, true,  "a/b/val/nested",   "v=42;t=7;tb=boot;q=1"),
    };

    private sealed record Sample(
        string Prefix, string Name, double Value, long TimestampMs, MqttTimeBase TimeBase,
        bool Quality, string Topic, string Payload);

    [Fact]
    public void FormatAndParse_RoundTripForEverySample()
    {
        foreach (var s in Samples)
        {
            Assert.Equal(s.Topic, MqttValueContract.FormatValueTopic(s.Prefix, s.Name));
            Assert.Equal(s.Payload, MqttValueContract.FormatValuePayload(s.Value, s.TimestampMs, s.TimeBase, s.Quality));

            Assert.True(MqttValueContract.TryParseValueTopic(s.Topic, out var prefix, out var name));
            Assert.Equal(s.Prefix, prefix);
            Assert.Equal(s.Name, name);

            Assert.True(MqttValueContract.TryParseValuePayload(s.Payload, out var message));
            Assert.False(message.IsLegacy);
            Assert.Equal(s.Value, message.Value);
            Assert.Equal(s.TimestampMs, message.TimestampMs);
            Assert.Equal(s.TimeBase, message.TimeBase);
            Assert.Equal(s.Quality, message.Quality);
        }
    }

    [Theory]
    [InlineData("", "temp")]          // 空前缀
    [InlineData("hmi", "")]           // 空变量名
    [InlineData("hmi", "a/b")]        // 变量名含 `/` ⇒ 会改写主题层级
    public void FormatValueTopic_RejectsIllegalSegments(string prefix, string name) =>
        Assert.Throws<ArgumentException>(() => MqttValueContract.FormatValueTopic(prefix, name));

    [Theory]
    [InlineData("hmi/val/temp", true, "hmi", "temp")]
    [InlineData("a/b/val/nested", true, "a/b", "nested")]
    [InlineData("hmi/val/a/b", false, "", "")]      // 变量名两段 ⇒ 拒
    [InlineData("hmi/val/", false, "", "")]         // 空变量名 ⇒ 拒
    [InlineData("hmi/temp", false, "", "")]         // 没有 `/val/` ⇒ 拒
    [InlineData("/val/temp", false, "", "")]        // 空前缀 ⇒ 拒
    [InlineData("", false, "", "")]
    public void TryParseValueTopic_Boundaries(string topic, bool ok, string prefix, string name)
    {
        Assert.Equal(ok, MqttValueContract.TryParseValueTopic(topic, out var p, out var n));
        if (ok)
        {
            Assert.Equal(prefix, p);
            Assert.Equal(name, n);
        }
    }

    [Fact]
    public void TryParseValuePayload_IgnoresUnknownKeys_AndAcceptsTheLegacyBareNumber()
    {
        // 未知键忽略（向前兼容：以后加 `u=`/`n=` 不会打挂老远程端）
        Assert.True(MqttValueContract.TryParseValuePayload("v=1.5;t=9;tb=boot;q=1;u=℃;n=温度", out var msg));
        Assert.Equal(1.5, msg.Value);
        Assert.Equal(9, msg.TimestampMs);
        Assert.Equal(MqttTimeBase.Boot, msg.TimeBase);

        // 改造前的裸数值（`%g`）：接受，但必须标成 legacy ⇒ 远程端知道"这条没有时间戳"
        Assert.True(MqttValueContract.TryParseValuePayload("12.5", out var legacy));
        Assert.True(legacy.IsLegacy);
        Assert.Equal(12.5, legacy.Value);
        Assert.Equal(0, legacy.TimestampMs);
        Assert.Equal(MqttTimeBase.Unknown, legacy.TimeBase);
    }

    [Theory]
    [InlineData("t=1;tb=boot;q=1")]      // 缺 v
    [InlineData("v=abc;t=1;tb=boot;q=1")]// v 不是数
    [InlineData("v=1;t=x;tb=boot;q=1")]  // t 不是整数
    [InlineData("v=1;t=1;tb=gps;q=1")]   // tb 只许 boot/utc
    [InlineData("v=1;t=1;tb=boot;q=2")]  // q 只许 0/1
    [InlineData("")]
    public void TryParseValuePayload_RejectsMalformed(string payload) =>
        Assert.False(MqttValueContract.TryParseValuePayload(payload, out _));

    [Fact]
    public void FormatNumber_MatchesCsPercentSixG()
    {
        Assert.Equal("12.5", MqttValueContract.FormatNumber(12.5));
        Assert.Equal("-3.25", MqttValueContract.FormatNumber(-3.25));
        Assert.Equal("42", MqttValueContract.FormatNumber(42.0));
        Assert.Equal("1e+06", MqttValueContract.FormatNumber(1_000_000.0));   // C 是 `1e+06`，C# 的 G6 是 `1E+06`
        Assert.Equal("1e-05", MqttValueContract.FormatNumber(0.00001));
        Assert.Throws<ArgumentException>(() => MqttValueContract.FormatNumber(double.NaN));
    }

    /// <summary>
    /// **跨语言同源守卫**：直接读设备侧 C 测试文件里的 <c>k_contract_samples[]</c>，
    /// 与本文件的 <see cref="Samples"/> **逐字段**比对。⇒ 任何一侧偷偷改表都会红。
    /// </summary>
    [Fact]
    public void Samples_MatchTheDeviceHostTestTable()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, DeviceTestFile);
        Assert.True(File.Exists(path), $"找不到设备侧测试文件：{path}");
        var text = File.ReadAllText(path);

        var block = Regex.Match(text,
            @"static const contract_sample_t k_contract_samples\[\] = \{(?<body>.*?)\n\};",
            RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_contract_samples[] 区块没找到 —— 表被改名/改形了吗？");

        var rowPattern = new Regex(
            @"\{\s*""(?<prefix>[^""]*)""\s*,\s*""(?<name>[^""]*)""\s*,\s*(?<value>[-0-9.eE+]+)\s*,\s*"
            + @"(?<t>\d+)ULL\s*,\s*(?<tb>[01])\s*,\s*(?<q>[01])\s*,\s*""(?<topic>[^""]*)""\s*,\s*""(?<payload>[^""]*)""\s*\}",
            RegexOptions.Compiled);

        var deviceRows = rowPattern.Matches(block.Groups["body"].Value)
            .Select(m => new Sample(
                m.Groups["prefix"].Value,
                m.Groups["name"].Value,
                double.Parse(m.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(m.Groups["t"].Value, System.Globalization.CultureInfo.InvariantCulture),
                m.Groups["tb"].Value == "1" ? MqttTimeBase.Utc : MqttTimeBase.Boot,
                m.Groups["q"].Value == "1",
                m.Groups["topic"].Value,
                m.Groups["payload"].Value))
            .ToList();

        Assert.True(deviceRows.Count >= 5,
            $"只从 C 侧解析出 {deviceRows.Count} 条样例（要求 ≥5）—— 解析器与表格式脱节了");

        var readings = new List<string>
        {
            $"    设备侧样例 {deviceRows.Count} 条 / C# 侧样例 {Samples.Length} 条（逐字段比对）",
        };
        readings.AddRange(Samples.Select(s => $"        · {s.Topic} ⇒ {s.Payload}"));

        // 逐字段相等（顺序也相等 ⇒ 两条表必须同序，改表时两端一起改）
        Assert.Equal(Samples.Length, deviceRows.Count);
        for (var i = 0; i < Samples.Length; i++)
            Assert.Equal(Samples[i], deviceRows[i]);

        // 顺带钉住"两侧格式化器产出同一串"（C 侧那份由 C 判据保证，这里保证 C# 侧那份）
        foreach (var s in Samples)
        {
            Assert.Equal(s.Topic, MqttValueContract.FormatValueTopic(s.Prefix, s.Name));
            Assert.Equal(s.Payload, MqttValueContract.FormatValuePayload(s.Value, s.TimestampMs, s.TimeBase, s.Quality));
        }

        JudgeReadings.Write(_out, "主题/payload 契约 v1 · 跨语言同源样例（§2-#12 的 M1）", readings);
    }
}
