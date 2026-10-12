using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **MQTT TLS 配置契约**（阶段 282；`C4=②` 的 M4 第一块）—— 编辑器侧对偶 ＋
/// **跨语言同源样例守卫**（直接读设备侧 C 测试文件里的 <c>k_tls_samples[]</c>）。
///
/// <para>红线（评估 §5）：**绝不静默降级为明文** —— 未知模式拒绝、缺件拒绝。</para>
/// </summary>
public class MqttTlsContractTests
{
    private readonly ITestOutputHelper _out;

    public MqttTlsContractTests(ITestOutputHelper o) => _out = o;

    private const string DeviceTestFile = "firmware/t113/tests/hmi_comm_test.c";

    private static string DeviceSource()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, DeviceTestFile);
        Assert.True(File.Exists(path), $"找不到设备侧测试文件：{path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Samples_MatchTheDeviceHostTestTable()
    {
        var block = Regex.Match(DeviceSource(), @"k_tls_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_tls_samples[] 区块没找到 —— 表被改名/改形了吗？");

        var row = new Regex(
            @"\{\s*""(?<mode>[^""]*)""\s*,\s*(?<id>NULL|""[^""]*"")\s*,\s*(?<file>NULL|""[^""]*"")\s*,\s*(?<ca>NULL|""[^""]*"")\s*,\s*(?<expect>-?\d+)\s*\}",
            RegexOptions.Compiled);

        static string? Text(string raw) => raw == "NULL" ? null : raw.Trim('"');

        var samples = row.Matches(block.Groups["body"].Value)
            .Select(m => (
                Mode: m.Groups["mode"].Value,
                PskId: Text(m.Groups["id"].Value),
                PskFile: Text(m.Groups["file"].Value),
                CaPath: Text(m.Groups["ca"].Value),
                Expect: int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture)))
            .ToList();

        Assert.True(samples.Count >= 7, $"只从 C 侧解析出 {samples.Count} 条 TLS 样例（要求 ≥7）");

        foreach (var s in samples)
        {
            Assert.True(MqttTlsContract.TryParseMode(s.Mode, out var mode), s.Mode);
            Assert.Equal(s.Mode, MqttTlsContract.ModeToken(mode));
            var ok = MqttTlsContract.Validate(mode, s.PskId, s.PskFile, s.CaPath);
            Assert.Equal(s.Expect == 0, ok);
        }

        JudgeReadings.Write(_out, "MQTT TLS 配置契约 · 跨语言同源样例（阶段 282）", new[]
        {
            $"设备侧样例 {samples.Count} 条（逐条：模式解析 + token 往返 + 必需项校验）",
            "红线：未知模式拒绝（不当 off）· psk 缺身份/缺密钥文件拒绝 · ca 缺 CA 拒绝",
        }.Concat(samples.Select(s => $"        · {s.Mode}/{s.PskId ?? "-"}/{s.PskFile ?? "-"}/{s.CaPath ?? "-"} ⇒ {(s.Expect == 0 ? "通过" : "拒")}")).ToList());
    }

    [Fact]
    public void EndpointSamples_MatchTheDeviceHostTestTable()
    {
        // **跨语言同源**：直接解析设备侧 `k_tls_endpoint_samples[]`（改形/改名都红）
        var block = Regex.Match(DeviceSource(), @"k_tls_endpoint_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_tls_endpoint_samples[] 区块没找到");

        var row = new Regex(
            @"\{\s*""(?<mode>[^""]*)""\s*,\s*""(?<broker>[^""]*)""\s*,\s*(?<bport>\d+)\s*,\s*(?<local>NULL|""[^""]*"")\s*,\s*""(?<want>[^""]*)""\s*,\s*(?<wport>\d+)\s*,\s*(?<expect>-?\d+)\s*\}",
            RegexOptions.Compiled);

        static string? Text(string raw) => raw == "NULL" ? null : raw.Trim('"');

        var samples = row.Matches(block.Groups["body"].Value).Select(m => (
            Mode: m.Groups["mode"].Value,
            Broker: m.Groups["broker"].Value,
            BrokerPort: int.Parse(m.Groups["bport"].Value, CultureInfo.InvariantCulture),
            LocalPort: Text(m.Groups["local"].Value),
            WantHost: m.Groups["want"].Value,
            WantPort: int.Parse(m.Groups["wport"].Value, CultureInfo.InvariantCulture),
            Expect: int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture))).ToList();

        Assert.True(samples.Count >= 9, $"只解析出 {samples.Count} 条端点样例（要求 ≥9）");

        foreach (var s in samples)
        {
            Assert.True(MqttTlsContract.TryParseMode(s.Mode, out var mode), s.Mode);
            var ok = MqttTlsContract.TryResolveEndpoint(mode, s.Broker, s.BrokerPort, s.LocalPort, out var host, out var port);
            Assert.Equal(s.Expect == 0, ok);
            if (s.Expect == 0)
            {
                Assert.Equal(s.WantHost, host);
                Assert.Equal(s.WantPort, port);
            }
        }

        JudgeReadings.Write(_out, "MQTT TLS 端点解析 · 跨语言同源样例（阶段 284）", new[]
        {
            $"设备侧端点样例 {samples.Count} 条（逐条：模式解析 + 端点解析 + host/port 比对）",
            "off ⇒ 直连 broker；psk/ca ⇒ 连 127.0.0.1（TLS 开着**绝不**把 broker 交给客户端）",
        }.Concat(samples.Select(s => $"        · {s.Mode}/{s.Broker}:{s.BrokerPort}/local={s.LocalPort ?? "缺省"} ⇒ {(s.Expect == 0 ? $"{s.WantHost}:{s.WantPort}" : "拒")}")).ToList());
    }

    [Fact]
    public void NoBypass_TlsOnMeansClientMustTalkToLoopbackOnly()
    {
        Assert.True(MqttTlsContract.AssertNoBypass(MqttTlsMode.Off, "10.0.0.5"));       // 显式明文 ⇒ 直连合法
        Assert.True(MqttTlsContract.AssertNoBypass(MqttTlsMode.Psk, "127.0.0.1"));
        Assert.True(MqttTlsContract.AssertNoBypass(MqttTlsMode.Ca, "localhost"));
        Assert.True(MqttTlsContract.AssertNoBypass(MqttTlsMode.Ca, "::1"));
        Assert.False(MqttTlsContract.AssertNoBypass(MqttTlsMode.Psk, "10.0.0.5"));     // 绕过代理 ⇒ 不许
        Assert.False(MqttTlsContract.AssertNoBypass(MqttTlsMode.Ca, null));
        Assert.False(MqttTlsContract.AssertNoBypass((MqttTlsMode)9, "127.0.0.1"));
    }

    [Theory]
    [InlineData(null, 1883, true, 1883)]
    [InlineData("", 1883, true, 1883)]
    [InlineData("11883", 1883, true, 11883)]
    [InlineData("65535", 1, true, 65535)]
    [InlineData("0", 1, false, 0)]
    [InlineData("65536", 1, false, 0)]
    [InlineData("-1", 1, false, 0)]
    [InlineData("70k", 1, false, 0)]
    [InlineData(" 1883", 1, false, 0)]
    public void TryParsePort_MatchesTheDeviceSide(string? text, int def, bool ok, int expected)
    {
        Assert.Equal(ok, MqttTlsContract.TryParsePort(text, def, out var port));
        if (ok) Assert.Equal(expected, port);
    }

    [Theory]
    [InlineData("off", MqttTlsMode.Off)]
    [InlineData("psk", MqttTlsMode.Psk)]
    [InlineData("ca", MqttTlsMode.Ca)]
    public void TryParseMode_AcceptsOnlyTheThreeLowercaseTokens(string text, MqttTlsMode expected)
    {
        Assert.True(MqttTlsContract.TryParseMode(text, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("OFF")]      // 大小写敏感：只留一种写法，免得两侧归一化规则分叉
    [InlineData("tls")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseMode_RejectsUnknown_AndNeverFallsBackToOff(string? text)
    {
        Assert.False(MqttTlsContract.TryParseMode(text, out var mode));
        Assert.Equal(MqttTlsMode.Off, mode);   // 出参是 default，但调用方**必须**看返回值
    }

    [Fact]
    public void Validate_NeverDegradesToPlaintext()
    {
        Assert.True(MqttTlsContract.Validate(MqttTlsMode.Off, "ignored", "ignored", "ignored"));
        Assert.False(MqttTlsContract.Validate(MqttTlsMode.Psk, "", "/k", null));      // 缺身份
        Assert.False(MqttTlsContract.Validate(MqttTlsMode.Psk, "id", "", null));      // 缺密钥文件
        Assert.False(MqttTlsContract.Validate(MqttTlsMode.Ca, null, null, ""));       // 缺 CA
        Assert.True(MqttTlsContract.Validate(MqttTlsMode.Psk, "id", "/k", null));
        Assert.True(MqttTlsContract.Validate(MqttTlsMode.Ca, null, null, "/ca.pem"));
    }

    [Fact]
    public void TryParse_MissingKeyMeansOff_ButBadValueFails()
    {
        // 老工程：ini 里**没有** mqtt.tls ⇒ off（向后兼容）
        Assert.True(MqttTlsContract.TryParse(new Dictionary<string, string>(), out var mode, out var issue));
        Assert.Equal(MqttTlsMode.Off, mode);
        Assert.Null(issue);

        // 键存在但取值非法 ⇒ **失败**（不是当 off）
        Assert.False(MqttTlsContract.TryParse(
            new Dictionary<string, string> { [MqttTlsContract.ModeKey] = "ssl" }, out _, out var bad));
        Assert.Equal("unknown-mode", bad);

        // psk 缺件 ⇒ 失败，且给出具体 issue（界面按它分派文案）
        Assert.False(MqttTlsContract.TryParse(
            new Dictionary<string, string> { [MqttTlsContract.ModeKey] = "psk", [MqttTlsContract.PskIdKey] = "id" },
            out _, out var incomplete));
        Assert.Equal("psk-incomplete", incomplete);

        // ca 缺件 ⇒ 失败
        Assert.False(MqttTlsContract.TryParse(
            new Dictionary<string, string> { [MqttTlsContract.ModeKey] = "ca" }, out _, out var ca));
        Assert.Equal("ca-missing", ca);

        // 完整配置 ⇒ 通过
        Assert.True(MqttTlsContract.TryParse(new Dictionary<string, string>
        {
            [MqttTlsContract.ModeKey] = "psk",
            [MqttTlsContract.PskIdKey] = "hmi-01",
            [MqttTlsContract.PskFileKey] = "/mnt/UDISK/hmi/psk.key",
        }, out var psk, out _));
        Assert.Equal(MqttTlsMode.Psk, psk);
    }

    [Fact]
    public void IniCodegen_EmitsTlsKeysOnlyWhenConfigured()
    {
        static DeviceConnection Mqtt(Dictionary<string, string> cfg) => new()
        {
            Id = "c1", Name = "MQTT", DriverId = "mqtt", IsEnabled = true,
            Config = cfg,
        };

        // ① 老工程（不带任何 TLS 键）⇒ ini **逐字节不变**：一个 tls 键都不出现
        var legacy = CommConfigCodeGen.GenerateRuntimeConfigText(new[]
        {
            Mqtt(new Dictionary<string, string> { ["broker"] = "10.0.0.5" }),
        });
        Assert.DoesNotContain(MqttTlsContract.ModeKey, legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("mqtt.tls.", legacy, StringComparison.Ordinal);
        Assert.Contains("mqtt.host=10.0.0.5", legacy, StringComparison.Ordinal);   // 正控：明文直连 broker

        // ③ 显式 off 同理（**显式**才允许直连 —— 红线只禁"自动降级"）
        var explicitOff = CommConfigCodeGen.GenerateRuntimeConfigText(new[]
        {
            Mqtt(new Dictionary<string, string> { ["broker"] = "10.0.0.5", ["tls"] = "off" }),
        });
        Assert.Contains("mqtt.host=10.0.0.5", explicitOff, StringComparison.Ordinal);
        Assert.Contains("mqtt.tls=off", explicitOff, StringComparison.Ordinal);
        Assert.DoesNotContain("upstream_host", explicitOff, StringComparison.Ordinal);

        // ② 配了 psk ⇒ 模式 + 身份 + 密钥文件三行都在
        //    ⚠ **键名分两层**：连接参数是短名（tls/…），落到 ini 才是 mqtt.tls.*（阶段 282 曾把两层混为一谈）
        var psk = CommConfigCodeGen.GenerateRuntimeConfigText(new[]
        {
            Mqtt(new Dictionary<string, string>
            {
                ["broker"] = "10.0.0.5",
                ["tls"] = "psk",
                ["tlsPskId"] = "hmi-01",
                ["tlsPskFile"] = "/mnt/UDISK/hmi/psk.key",
            }),
        });
        Assert.Contains("mqtt.tls=psk", psk, StringComparison.Ordinal);
        // ⚠ **不许绕过代理**在生成期的落点：TLS 开着时客户端必须连 loopback，
        //   broker 地址只以 upstream_* 出现（给代理读）
        Assert.Contains("mqtt.host=127.0.0.1", psk, StringComparison.Ordinal);
        Assert.DoesNotContain("mqtt.host=10.0.0.5", psk, StringComparison.Ordinal);
        Assert.Contains("mqtt.tls.upstream_host=10.0.0.5", psk, StringComparison.Ordinal);
        Assert.Contains("mqtt.tls.upstream_port=1883", psk, StringComparison.Ordinal);
        Assert.Contains("mqtt.tls.psk_id=hmi-01", psk, StringComparison.Ordinal);
        Assert.Contains("mqtt.tls.psk_file=/mnt/UDISK/hmi/psk.key", psk, StringComparison.Ordinal);
        Assert.DoesNotContain("mqtt.tls.ca_path", psk, StringComparison.Ordinal);   // 没配的不发

        // ③ 解析回来的参数与 ini 一致（生成 → 解析闭环）
        var resolved = CommConfigCodeGen.ResolveMqtt(Mqtt(new Dictionary<string, string>
        {
            ["tls"] = "psk",
            ["tlsPskId"] = "hmi-01",
            ["tlsPskFile"] = "/mnt/UDISK/hmi/psk.key",
        }));
        Assert.NotNull(resolved);
        Assert.Equal("psk", resolved!.Tls);
        Assert.Equal("hmi-01", resolved.TlsPskId);
        Assert.Equal("/mnt/UDISK/hmi/psk.key", resolved.TlsPskFile);
    }

    [Fact]
    public void ModeToken_RejectsUnknownEnumValues()
    {
        Assert.Equal("off", MqttTlsContract.ModeToken(MqttTlsMode.Off));
        Assert.Equal("psk", MqttTlsContract.ModeToken(MqttTlsMode.Psk));
        Assert.Equal("ca", MqttTlsContract.ModeToken(MqttTlsMode.Ca));
        Assert.Throws<ArgumentOutOfRangeException>(() => MqttTlsContract.ModeToken((MqttTlsMode)9));
    }
}
