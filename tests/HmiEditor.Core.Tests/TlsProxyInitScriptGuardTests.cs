using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **TLS 终止代理的 init 脚本守卫**（2026-10-11，阶段 303；契约 §4 四条要求）。
///
/// <para><b>为什么是文本级守卫</b>：契约 §6 自己给这条判据定的形状就是
/// 「读 init 脚本断言（与 <c>S97hmiruntime</c> 同款写法）」—— 脚本要在设备的
/// busybox/procd 环境里跑，主机上判不了它的**行为**；但它的**结构**（谁先谁后、
/// 有没有 respawn、失败那支干什么）恰恰是 §4 要守的东西，而这些都能从文本判死。</para>
///
/// <para><b>决策本身**不**在这里判</b>：<c>0/3/4</c> 退出码的语义由
/// <c>firmware/t113/tests/check_tls_proxy.sh</c> 用真 TLS 端到端判（19 条断言）。两边分工：
/// 那边判"代理说得对"，这边判"脚本接得对"。</para>
/// </summary>
public class TlsProxyInitScriptGuardTests
{
    private readonly ITestOutputHelper _o;

    public TlsProxyInitScriptGuardTests(ITestOutputHelper o) => _o = o;

    private const string ScriptRel = "firmware/t113/tina/package/hmi-runtime/files/S97hmiruntime";
    private const string ProxySrcRel = "firmware/t113/app/proxy/tls_proxy.c";

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static int IndexOfOrMinus1(string text, string needle) => text.IndexOf(needle, StringComparison.Ordinal);

    /// <summary>取 `procd_open_instance <name>` 到其后第一个 `procd_close_instance` 之间的块。</summary>
    private static string ProcdInstanceBlock(string text, string name)
    {
        var start = text.IndexOf($"procd_open_instance {name}", StringComparison.Ordinal);
        if (start < 0) return "";
        var end = text.IndexOf("procd_close_instance", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    [Fact]
    public void InitScript_MeetsTheFourContractRequirements()
    {
        var s = Read(ScriptRel);
        var proxy = Read(ProxySrcRel);
        var problems = new List<string>();

        // ① procd 托管 ＋ respawn
        var block = ProcdInstanceBlock(s, "hmi-tls-proxy");
        if (block.Length == 0) problems.Add("① 找不到 `procd_open_instance hmi-tls-proxy` 块");
        else if (!block.Contains("procd_set_param respawn", StringComparison.Ordinal))
            problems.Add("① 代理实例块里没有 `procd_set_param respawn`（崩了没人拉 ⇒ MQTT 静默断）");

        // ② 代理**先于** hmi_runtime 可用；且必须在 UDISK **挂好之后**（配置在 UDISK 上）
        var callAt = IndexOfOrMinus1(s, "\n    start_tls_proxy");
        var hostAt = IndexOfOrMinus1(s, "procd_set_param command /bin/sh -c \"exec $HMI_BIN");
        var udiskAt = IndexOfOrMinus1(s, "while [ $i -lt 30 ] && ! grep -q \" /mnt/UDISK \" /proc/mounts");
        if (callAt < 0) problems.Add("② 找不到 `start_tls_proxy` 的调用点");
        else
        {
            if (hostAt < 0) problems.Add("② 找不到 hmi_runtime 的 procd command（脚本结构变了？）");
            else if (callAt > hostAt) problems.Add("② 代理在 hmi_runtime **之后**才起（客户端会先白刷一屏连不上）");
            if (udiskAt < 0) problems.Add("② 找不到 UDISK 挂载等待块（脚本结构变了？）");
            else if (callAt < udiskAt) problems.Add("② 代理在 UDISK 挂载**之前**起 ⇒ 读不到 comm_config.ini，会静默退成'不需要代理'");
        }

        // ③ 明文监听只绑回环：脚本不许传入任何监听地址覆盖；C 侧也不许出现 0.0.0.0
        if (s.Contains("0.0.0.0", StringComparison.Ordinal))
            problems.Add("③ init 脚本里出现 0.0.0.0（契约 §4：绑 LAN 比不做 TLS 更危险）");
        if (proxy.Contains("0.0.0.0", StringComparison.Ordinal))
            problems.Add("③ 代理源码里出现 0.0.0.0（监听地址只许是 HMI_TLS_PROXY_LISTEN_HOST）");

        // ④ 失败**不改变** mqtt.tls 取值（不许自动降级）：脚本不许写配置文件、不许写 TLS 键
        foreach (var line in s.Split('\n'))
        {
            var code = Regex.Replace(line, @"#.*$", "");
            // ⚠ 剥引号只用于 `mqtt.tls=` 那条：它要躲开脚本自己的提示语 `echo "… mqtt.tls=off …"`。
            //   **写文件**那条必须用**原行** —— 剥了引号会把 `> "$COMM_CFG"` 里的变量也剥掉，
            //   负控实测过：那样写能连过两轮（先漏字面路径、再漏引号里的变量）。
            var codeNoStrings = Regex.Replace(code, "\"[^\"]*\"|'[^']*'", "\"\"");
            if (Regex.IsMatch(code,
                    @"(sed\s+-i|>>?\s*)[^|]*(\$COMM_CFG|\$\{COMM_CFG\}|comm_config\.ini)"))
                problems.Add($"④ init 脚本会写 comm_config.ini（自动降级的形状）：{line.Trim()}");
            if (codeNoStrings.Contains("mqtt.tls=", StringComparison.Ordinal))
                problems.Add($"④ init 脚本里出现 `mqtt.tls=` 赋值（取值只由编辑器下发，脚本不许改）：{line.Trim()}");
        }

        // 退出码分派必须在（0 起 / 3 跳过 / 其余拒绝），且三支都说得出话
        if (!s.Contains("--check", StringComparison.Ordinal))
            problems.Add("退出码分派缺失：脚本没有用 `--check` 问代理该不该起");
        if (!Regex.IsMatch(s, @"case \$\? in\s*\n\s*0\)", RegexOptions.Multiline))
            problems.Add("退出码分派缺失：没有 `case $? in` 的 `0)` 支");
        if (!Regex.IsMatch(s, @"\n\s*3\)\s+echo ""hmi-tls-proxy: mqtt\.tls=off", RegexOptions.Multiline))
            problems.Add("退出码分派缺失：`3)`（不需要代理）那支不见了");
        if (!Regex.IsMatch(s, @"\n\s*\*\)\s+echo ""hmi-tls-proxy: 配置非法", RegexOptions.Multiline))
            problems.Add("退出码分派缺失：`*)`（配置非法 ⇒ 不启动）那支不见了");

        // stop 必须把代理也停掉（只停宿主会让代理占着回环端口继续跑旧配置）
        if (!s.Contains("killall hmi-tls-proxy", StringComparison.Ordinal))
            problems.Add("stop_service 没有停代理（killall hmi-tls-proxy 缺失）");
        if (!s.Contains("procd_kill hmi-tls-proxy", StringComparison.Ordinal))
            problems.Add("stop_service 没有 procd_kill 代理实例");

        JudgeReadings.Write(_o, "TLS 代理 init 脚本四条（阶段 303）", new[]
        {
            "① 代理 procd 实例块含 respawn",
            "② start_tls_proxy 在 UDISK 挂载之后、hmi_runtime 实例之前",
            "③ 脚本与代理源码都不含 0.0.0.0（监听恒为回环）",
            "④ 脚本不写 comm_config.ini、不赋 mqtt.tls（不降级）；`--check` 的三支齐全；stop 也停代理",
        });

        Assert.True(problems.Count == 0,
            "init 脚本没满足契约 §4 的四条要求：" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }
}
