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
/// **远程写命令的值校验**（阶段 287）—— 编辑器侧对偶 ＋ 跨语言同源守卫 ＋
/// **设备侧幂等台账语义**（C 实现必须与 C# <see cref="CommandLedger"/> 同语义）。
/// </summary>
public class MqttWriteGuardTests
{
    private readonly ITestOutputHelper _out;

    public MqttWriteGuardTests(ITestOutputHelper o) => _out = o;

    private static string DeviceSource() =>
        File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "firmware/t113/tests/hmi_comm_test.c"));

    [Fact]
    public void Samples_MatchTheDeviceHostTestTable()
    {
        var block = Regex.Match(DeviceSource(), @"k_write_guard_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_write_guard_samples[] 没找到");

        var row = new Regex(
            @"\{\s*(?<wr>[01])\s*,\s*(?<min>-?[\d.]+)\s*,\s*(?<max>-?[\d.]+)\s*,\s*""(?<value>[^""]*)""\s*,\s*(?<expect>-?\d+)\s*,\s*""(?<token>[^""]*)""\s*\}",
            RegexOptions.Compiled);

        static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

        var samples = row.Matches(block.Groups["body"].Value).Select(m => (
            Writable: m.Groups["wr"].Value == "1",
            Min: D(m.Groups["min"].Value),
            Max: D(m.Groups["max"].Value),
            Value: m.Groups["value"].Value,
            Expect: (MqttWriteResult)int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture),
            Token: m.Groups["token"].Value)).ToList();

        Assert.True(samples.Count >= 17, $"只解析出 {samples.Count} 条写校验样例（要求 ≥17）");

        foreach (var s in samples)
        {
            var result = MqttWriteGuard.Check(s.Writable, s.Min, s.Max, s.Value, out var value);
            Assert.Equal(s.Expect, result);
            if (result == MqttWriteResult.Ok)
            {
                Assert.Null(MqttWriteGuard.Token(result));
                Assert.Equal(D(s.Value), value, 6);                 // OK 时必须回填解析值
            }
            else
            {
                Assert.Equal(s.Token, MqttWriteGuard.Token(result));
            }
        }

        JudgeReadings.Write(_out, "远程写命令值校验 · 跨语言同源样例（阶段 287）", new[]
        {
            $"设备侧样例 {samples.Count} 条（逐条比对结果码 ＋ OK 时回填值 ＋ 拒绝时 token）",
            "顺序是契约：只读 ⇒ read-only（值再合法也拒）；min>max ⇒ bad-target（配置错）",
        }.Concat(samples.Select(s => $"        · wr={s.Writable} [{s.Min},{s.Max}] {s.Value} ⇒ {s.Expect} {s.Token}")).ToList());
    }

    [Fact]
    public void StrictParse_RejectsAnythingButAWholeNumber()
    {
        Assert.True(MqttWriteGuard.TryParseStrict("42", out var v) && v == 42);
        Assert.True(MqttWriteGuard.TryParseStrict("-0.5", out var neg) && neg == -0.5);
        Assert.True(MqttWriteGuard.TryParseStrict("1e3", out var exp) && exp == 1000);   // 科学计数法合法

        foreach (var bad in new[] { "", " ", "42 ", " 42", "42kg", "nan", "inf", "-inf", "0x10", "1,5" })
            Assert.False(MqttWriteGuard.TryParseStrict(bad, out _), $"应拒：{bad}");

        Assert.False(MqttWriteGuard.TryParseStrict(null, out _));
    }

    [Fact]
    public void LedgerSamples_MatchTheDeviceSide()
    {
        // C 侧 k_cmd_ledger_samples[] 的语义必须与 C# CommandLedger **逐条一致**（跨语言）
        var block = Regex.Match(DeviceSource(), @"k_cmd_ledger_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_cmd_ledger_samples[] 没找到");

        var row = new Regex(
            @"\{\s*""(?<id>[^""]*)""\s*,\s*(?<now>\d+)\s*,\s*(?<ttl>\d+)\s*,\s*(?<expect>-?\d+)\s*,\s*(?<count>\d+)\s*\}",
            RegexOptions.Compiled);

        var ledger = new CommandLedger();
        var samples = 0;
        foreach (Match m in row.Matches(block.Groups["body"].Value))
        {
            samples++;
            var id = m.Groups["id"].Value;
            var now = long.Parse(m.Groups["now"].Value, CultureInfo.InvariantCulture);
            var ttl = long.Parse(m.Groups["ttl"].Value, CultureInfo.InvariantCulture);
            var expect = int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture) == 1;
            var countAfter = int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture);

            Assert.Equal(expect, ledger.TryAccept(id, now, ttl));
            Assert.Equal(countAfter, ledger.Count);
        }
        Assert.True(samples >= 5, $"只解析出 {samples} 条台账样例");
    }

    [Fact]
    public void Ledger_IsBounded_AndDropsTheEarliestExpiry()
    {
        // 与设备侧同一语义：满了丢**最先到期**的（不是最先来的）
        var ledger = new CommandLedger(capacity: 3);
        Assert.True(ledger.TryAccept("short", 0, MqttCommandContract.MinTtlMs));
        Assert.True(ledger.TryAccept("mid", 0, 60_000));
        Assert.True(ledger.TryAccept("long", 0, MqttCommandContract.MaxTtlMs));
        Assert.True(ledger.TryAccept("new", 0, MqttCommandContract.MaxTtlMs));   // 满 ⇒ 丢 short

        Assert.True(ledger.TryAccept("short", 0, MqttCommandContract.MinTtlMs)); // 被丢 ⇒ 可再收
        Assert.False(ledger.TryAccept("long", 0, MqttCommandContract.MaxTtlMs)); // 还在
    }
}
