using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **远程写命令契约**（阶段 285；`§2-#12` M5 第一块）—— 编辑器侧对偶 ＋ 跨语言同源守卫。
/// 三条硬规则：**幂等 id 形状** · **TTL 上下界** · **过期即丢（含"来自未来"）**。
/// </summary>
public class MqttCommandContractTests
{
    private readonly ITestOutputHelper _out;

    public MqttCommandContractTests(ITestOutputHelper o) => _out = o;

    private static string DeviceSource() =>
        File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "firmware/t113/tests/hmi_comm_test.c"));

    [Fact]
    public void Samples_MatchTheDeviceHostTestTable()
    {
        var block = Regex.Match(DeviceSource(), @"k_cmd_fresh_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_cmd_fresh_samples[] 没找到");

        var row = new Regex(
            @"\{\s*""(?<id>[^""]*)""\s*,\s*(?<ts>\d+)\s*,\s*(?<ttl>\d+)\s*,\s*(?<now>\d+)\s*,\s*(?<expect>-?\d+)\s*\}",
            RegexOptions.Compiled);

        var samples = row.Matches(block.Groups["body"].Value).Select(m => (
            Id: m.Groups["id"].Value,
            Ts: long.Parse(m.Groups["ts"].Value, CultureInfo.InvariantCulture),
            Ttl: long.Parse(m.Groups["ttl"].Value, CultureInfo.InvariantCulture),
            Now: long.Parse(m.Groups["now"].Value, CultureInfo.InvariantCulture),
            Expect: int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture))).ToList();

        Assert.True(samples.Count >= 7, $"只解析出 {samples.Count} 条新鲜度样例（要求 ≥7）");

        foreach (var s in samples)
            Assert.Equal(s.Expect == 0, MqttCommandContract.IsFresh(s.Ts, s.Ttl, s.Now));

        JudgeReadings.Write(_out, "远程命令信封 · 跨语言同源样例（阶段 285）", new[]
        {
            $"设备侧新鲜度样例 {samples.Count} 条（逐条比对 IsFresh）",
            "恰好 = TTL 收（闭区间）· 超 1ms 即过期 · now 早于 ts 远超偏斜 ⇒ 拒 · TTL 越界 ⇒ 拒",
        }.Concat(samples.Select(s => $"        · {s.Id}: ts={s.Ts} ttl={s.Ttl} now={s.Now} ⇒ {(s.Expect == 0 ? "收" : "拒")}")).ToList());
    }

    [Fact]
    public void Samples_ForIdShape_MatchTheDeviceHostTestTable()
    {
        // 设备侧是逐条 CHECK，这里把同一批语料再走一遍（形状守卫：`[A-Za-z0-9_-]`、≤64）
        Assert.True(MqttCommandContract.IsIdValid("cmd-001"));
        Assert.True(MqttCommandContract.IsIdValid("A_z-9"));
        Assert.True(MqttCommandContract.IsIdValid(new string('a', MqttCommandContract.IdMaxLength)));
        Assert.False(MqttCommandContract.IsIdValid(""));
        Assert.False(MqttCommandContract.IsIdValid(null));
        Assert.False(MqttCommandContract.IsIdValid("has space"));
        Assert.False(MqttCommandContract.IsIdValid("has;semi"));
        Assert.False(MqttCommandContract.IsIdValid("has=eq"));
        Assert.False(MqttCommandContract.IsIdValid(new string('a', MqttCommandContract.IdMaxLength + 1)));
    }

    [Fact]
    public void Payload_RoundTrips_AndMatchesTheDeviceOrder()
    {
        var command = new MqttCommand("cmd-001", 1_000_000, 60_000, "alice", "v1", "42");
        var payload = MqttCommandContract.FormatPayload(command);

        // 与设备侧逐字节同序（跨语言：`id;ts;ttl;by;target;value`）
        Assert.Equal("id=cmd-001;ts=1000000;ttl=60000;by=alice;target=v1;value=42", payload);

        var parsed = MqttCommandContract.TryParsePayload(payload);
        Assert.NotNull(parsed);
        Assert.Equal(command, parsed);

        // by 为空 ⇒ 不出该段（设备侧同款）
        var noBy = MqttCommandContract.FormatPayload(command with { By = null });
        Assert.Equal("id=cmd-001;ts=1000000;ttl=60000;target=v1;value=42", noBy);
        Assert.Equal(command with { By = null }, MqttCommandContract.TryParsePayload(noBy));

        Assert.Equal("site1/cmd/v1", MqttCommandContract.FormatTopic("site1", "v1"));
        Assert.Throws<ArgumentException>(() => MqttCommandContract.FormatTopic("", "v1"));
        Assert.Throws<ArgumentException>(() => MqttCommandContract.FormatPayload(command with { Id = "bad id" }));
        Assert.Throws<ArgumentException>(() => MqttCommandContract.FormatPayload(command with { TtlMs = 0 }));
    }

    [Theory]
    [InlineData("id=c;ts=1;ttl=60000;target=v", true)]                 // 缺 by/value 也合法
    [InlineData("id=c;ts=1;ttl=60000", false)]                        // 缺 target
    [InlineData("ts=1;ttl=60000;target=v", false)]                    // 缺 id
    [InlineData("id=bad id;ts=1;ttl=60000;target=v", false)]          // id 形状
    [InlineData("id=c;ts=abc;ttl=60000;target=v", false)]             // ts 非数字
    [InlineData("id=c;ts=-1;ttl=60000;target=v", false)]              // ts 负（NumberStyles.None）
    [InlineData("id=c;ts=1;ttl=0;target=v", false)]                   // TTL 非法
    [InlineData("id=c;ts=1;ttl=60000;target=;", false)]               // 空 target
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParsePayload_IsStrict(string? payload, bool ok)
    {
        var parsed = MqttCommandContract.TryParsePayload(payload);
        Assert.Equal(ok, parsed is not null);
    }

    [Fact]
    public void Accept_RejectsExpiredFutureAndDuplicate()
    {
        var ledger = new CommandLedger();
        const long now = 1_000_000;

        // 收：新鲜
        var good = MqttCommandContract.FormatPayload(new MqttCommand("c1", now - 1000, 60_000, "alice", "v1", "1"));
        Assert.Null(MqttCommandContract.Accept(good, now, ledger));

        // 重复：同一 id 再来一次 ⇒ duplicate
        Assert.Equal(MqttCommandIssue.Duplicate, MqttCommandContract.Accept(good, now, ledger));

        // 过期
        var old = MqttCommandContract.FormatPayload(new MqttCommand("c2", now - 120_000, 60_000, "alice", "v1", "1"));
        Assert.Equal(MqttCommandIssue.Expired, MqttCommandContract.Accept(old, now, ledger));

        // 来自未来
        var future = MqttCommandContract.FormatPayload(new MqttCommand("c3", now + 60_000, 60_000, "alice", "v1", "1"));
        Assert.Equal(MqttCommandIssue.FromFuture, MqttCommandContract.Accept(future, now, ledger));

        // 形状坏
        Assert.Equal(MqttCommandIssue.MissingField, MqttCommandContract.Accept("garbage", now, ledger));

        // 被拒的命令**不进台账**（否则一次伪造就能把合法 id 永久占掉）
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void Ledger_EvictsExpired_AndIsBounded()
    {
        var ledger = new CommandLedger(capacity: 2);

        Assert.True(ledger.TryAccept("a", 0, 1_000));
        Assert.True(ledger.TryAccept("b", 0, 1_000));
        Assert.False(ledger.TryAccept("a", 500, 1_000));       // 未过期 ⇒ 重复
        Assert.True(ledger.TryAccept("c", 2_000, 1_000));      // a/b 都已过期 ⇒ 淘汰后收下
        Assert.Equal(1, ledger.Count);

        // 有界：容量 2，收 3 条未过期的 ⇒ 只能留住 2 条，且**丢掉最先到期的**
        var bounded = new CommandLedger(capacity: 2);
        Assert.True(bounded.TryAccept("short", 0, 1_000));
        Assert.True(bounded.TryAccept("long", 0, 300_000));
        Assert.True(bounded.TryAccept("third", 0, 300_000));
        Assert.Equal(2, bounded.Count);
        Assert.False(bounded.TryAccept("long", 0, 300_000));   // 还在
        Assert.True(bounded.TryAccept("short", 0, 1_000));     // 被丢的是最先到期的那个

        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandLedger(0));
    }

    [Fact]
    public void Constants_MatchTheDeviceHeader()
    {
        var header = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "firmware/t113/app/comm/mqtt_cmd_contract.h"));

        Assert.Contains($"#define HMI_MQTT_CMD_ID_MAX          {MqttCommandContract.IdMaxLength}", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_MQTT_CMD_MIN_TTL_MS      {MqttCommandContract.MinTtlMs}ull", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_MQTT_CMD_MAX_TTL_MS      {MqttCommandContract.MaxTtlMs}ull", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_MQTT_CMD_CLOCK_SKEW_MS   {MqttCommandContract.ClockSkewMs}ull", header, StringComparison.Ordinal);
    }
}
