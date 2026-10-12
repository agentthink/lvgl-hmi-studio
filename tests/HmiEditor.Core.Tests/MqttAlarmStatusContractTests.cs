using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警/状态上行契约（M2）**（阶段 272；登记 `§2-#12` 的 M2）——
/// 解析/格式化 ＋ **两张跨语言同源样例表守卫**（直接读设备侧 C 测试文件）。
/// </summary>
public class MqttAlarmStatusContractTests
{
    private readonly ITestOutputHelper _out;

    public MqttAlarmStatusContractTests(ITestOutputHelper o) => _out = o;

    private const string DeviceTestFile = "firmware/t113/tests/hmi_comm_test.c";

    private sealed record AlarmSample(string Prefix, string AlarmId, int State, int Severity,
                                      long TimestampMs, MqttTimeBase TimeBase, string Topic, string Payload);

    private sealed record StatusSample(string Prefix, bool Online, long TimestampMs,
                                       MqttTimeBase TimeBase, string Topic, string Payload);

    private static string DeviceSource()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, DeviceTestFile);
        Assert.True(File.Exists(path), $"找不到设备侧测试文件：{path}");
        return File.ReadAllText(path);
    }

    private static string Block(string source, string tableName)
    {
        var m = Regex.Match(source, tableName + @"\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(m.Success, $"C 侧 {tableName} 区块没找到 —— 表被改名/改形了吗？");
        return m.Groups["body"].Value;
    }

    private static List<AlarmSample> DeviceAlarmSamples()
    {
        var row = new Regex(
            @"\{\s*""(?<prefix>[^""]*)""\s*,\s*""(?<id>[^""]*)""\s*,\s*(?<state>\d+)\s*,\s*(?<sev>\d+)\s*,\s*"
            + @"(?<t>\d+)ULL\s*,\s*(?<tb>[01])\s*,\s*""(?<topic>[^""]*)""\s*,\s*""(?<payload>[^""]*)""\s*\}",
            RegexOptions.Compiled);
        return row.Matches(Block(DeviceSource(), "k_alarm_samples"))
            .Select(m => new AlarmSample(
                m.Groups["prefix"].Value, m.Groups["id"].Value,
                int.Parse(m.Groups["state"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["sev"].Value, CultureInfo.InvariantCulture),
                long.Parse(m.Groups["t"].Value, CultureInfo.InvariantCulture),
                m.Groups["tb"].Value == "1" ? MqttTimeBase.Utc : MqttTimeBase.Boot,
                m.Groups["topic"].Value, m.Groups["payload"].Value))
            .ToList();
    }

    private static List<StatusSample> DeviceStatusSamples()
    {
        var row = new Regex(
            @"\{\s*""(?<prefix>[^""]*)""\s*,\s*(?<online>[01])\s*,\s*(?<t>\d+)ULL\s*,\s*(?<tb>[01])\s*,\s*"
            + @"""(?<topic>[^""]*)""\s*,\s*""(?<payload>[^""]*)""\s*\}",
            RegexOptions.Compiled);
        return row.Matches(Block(DeviceSource(), "k_status_samples"))
            .Select(m => new StatusSample(
                m.Groups["prefix"].Value, m.Groups["online"].Value == "1",
                long.Parse(m.Groups["t"].Value, CultureInfo.InvariantCulture),
                m.Groups["tb"].Value == "1" ? MqttTimeBase.Utc : MqttTimeBase.Boot,
                m.Groups["topic"].Value, m.Groups["payload"].Value))
            .ToList();
    }

    [Fact]
    public void AlarmSamples_MatchTheDeviceHostTestTable_AndRoundTrip()
    {
        var samples = DeviceAlarmSamples();
        Assert.True(samples.Count >= 5, $"只从 C 侧解析出 {samples.Count} 条报警样例（要求 ≥5）");

        foreach (var s in samples)
        {
            // ① 两侧格式化逐字节一致
            Assert.Equal(s.Topic, MqttAlarmContract.FormatTopic(s.Prefix, s.AlarmId));
            Assert.Equal(s.Payload, MqttAlarmContract.FormatPayload(s.State, s.Severity, s.TimestampMs, s.TimeBase));

            // ② 主题解析
            Assert.True(MqttAlarmContract.TryParseTopic(s.Topic, out var prefix, out var id));
            Assert.Equal(s.Prefix, prefix);
            Assert.Equal(s.AlarmId, id);

            // ③ payload 解析往返
            Assert.True(MqttAlarmContract.TryParsePayload(s.Payload, out var msg));
            Assert.Equal(s.State, msg.State);
            Assert.Equal(s.Severity, msg.Severity);
            Assert.Equal(s.TimestampMs, msg.TimestampMs);
            Assert.Equal(s.TimeBase, msg.TimeBase);
        }

        JudgeReadings.Write(_out, "报警上行契约 M2 · 跨语言同源样例（§2-#12 的 M2 · 阶段 272）", new[]
        {
            $"设备侧报警样例 {samples.Count} 条（逐字段比对 ＋ 往返）",
        }.Concat(samples.Select(s => $"        · {s.Topic} ⇒ {s.Payload}")).ToList());
    }

    [Fact]
    public void StatusSamples_MatchTheDeviceHostTestTable_AndTheLwtFormIsParsed()
    {
        var samples = DeviceStatusSamples();
        Assert.True(samples.Count >= 3, $"只从 C 侧解析出 {samples.Count} 条状态样例（要求 ≥3）");

        foreach (var s in samples)
        {
            Assert.Equal(s.Topic, MqttStatusContract.FormatTopic(s.Prefix));
            Assert.Equal(s.Payload, MqttStatusContract.FormatPayload(s.Online, s.TimestampMs, s.TimeBase));

            Assert.True(MqttStatusContract.TryParseTopic(s.Topic, out var prefix));
            Assert.Equal(s.Prefix, prefix);

            Assert.True(MqttStatusContract.TryParsePayload(s.Payload, out var msg));
            Assert.True(msg.Online);
            Assert.Equal(s.TimestampMs, msg.TimestampMs);
            Assert.Equal(s.TimeBase, msg.TimeBase);
            Assert.True(msg.HasTimestamp);
        }

        // 遗嘱档：与常量逐字相同；解析成"离线 + 无时间戳"（不是失败）
        Assert.Equal("on=0", MqttStatusContract.LwtPayload);
        Assert.True(MqttStatusContract.TryParsePayload(MqttStatusContract.LwtPayload, out var lwt));
        Assert.False(lwt.Online);
        Assert.False(lwt.HasTimestamp);
        Assert.Equal(MqttTimeBase.Unknown, lwt.TimeBase);

        // 两类"同一个常量"：C# 的格式化在 offline 时不看时间戳参数（与设备侧一致）
        Assert.Equal(MqttStatusContract.LwtPayload,
            MqttStatusContract.FormatPayload(false, 12345, MqttTimeBase.Utc));
    }

    [Theory]
    [InlineData(0, 1)]      // Inactive 不是可发布的迁移
    [InlineData(5, 1)]      // 越界
    [InlineData(1, 4)]      // severity 越界
    [InlineData(1, -1)]
    public void AlarmPayload_RejectsOutOfRangeStateOrSeverity(int state, int severity) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            MqttAlarmContract.FormatPayload(state, severity, 0, MqttTimeBase.Boot));

    [Fact]
    public void ShelvedState_IsReservedAndAlreadyFormattable()
    {
        // A1=② 已决定"搁置设备侧也做"：契约先留位（4），实现另立阶段 ⇒ 现在格式化/解析都要能过。
        Assert.Equal(4, MqttAlarmContract.ShelvedState);
        var payload = MqttAlarmContract.FormatPayload(MqttAlarmContract.ShelvedState, 2, 1, MqttTimeBase.Boot);
        Assert.Equal("st=4;sev=2;t=1;tb=boot", payload);
        Assert.True(MqttAlarmContract.TryParsePayload(payload, out var msg));
        Assert.Equal(MqttAlarmContract.ShelvedState, msg.State);
    }

    [Theory]
    [InlineData("st=1;sev=2;t=1")]           // 缺 tb
    [InlineData("sev=2;t=1;tb=boot")]        // 缺 st
    [InlineData("st=1;t=1;tb=boot")]         // 缺 sev
    [InlineData("st=1;sev=2;t=1;tb=gps")]    // tb 只许 boot/utc
    [InlineData("st=0;sev=2;t=1;tb=boot")]   // 状态 0（Inactive）不可发布
    [InlineData("st=5;sev=2;t=1;tb=boot")]   // 状态越界
    [InlineData("st=1;sev=4;t=1;tb=boot")]   // severity 越界
    [InlineData("1")]                        // 裸数值（值契约才接受）
    [InlineData("")]
    public void AlarmPayload_RejectsMalformed(string payload) =>
        Assert.False(MqttAlarmContract.TryParsePayload(payload, out _));

    [Fact]
    public void UnknownKeys_AreIgnored_OnBothContracts()
    {
        // 向前兼容：以后加 u=/n= 这类字段不会打挂老远程端
        Assert.True(MqttAlarmContract.TryParsePayload("st=1;sev=2;t=1;tb=boot;u=℃;n=温度高", out var alarm));
        Assert.Equal(1, alarm.State);
        Assert.True(MqttStatusContract.TryParsePayload("on=1;t=1;tb=utc;site=plant1", out var status));
        Assert.True(status.Online);
    }

    [Theory]
    [InlineData("hmi/alarm/a/b", false)]     // 报警 ID 两段
    [InlineData("hmi/alarm/", false)]        // 空 ID
    [InlineData("/alarm/a1", false)]         // 空前缀
    [InlineData("hmi/status", false)]        // 不是报警主题
    public void AlarmTopic_Boundaries(string topic, bool ok) =>
        Assert.Equal(ok, MqttAlarmContract.TryParseTopic(topic, out _, out _));

    [Theory]
    [InlineData("hmi/status", true, "hmi")]
    [InlineData("a/b/status", true, "a/b")]
    [InlineData("hmi/status/x", false, "")]
    [InlineData("hmi/alarm/a1", false, "")]
    public void StatusTopic_Boundaries(string topic, bool ok, string prefix)
    {
        Assert.Equal(ok, MqttStatusContract.TryParseTopic(topic, out var p));
        if (ok) Assert.Equal(prefix, p);
    }
}
