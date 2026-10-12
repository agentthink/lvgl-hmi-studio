using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HmiEditor.Core.Services;

/// <summary>命令信封被拒的原因（稳定码 —— 界面/审计按码分派文案）。</summary>
public static class MqttCommandIssue
{
    public const string InvalidId = "invalid-id";
    public const string InvalidTtl = "invalid-ttl";
    public const string Expired = "expired";
    public const string FromFuture = "from-future";
    public const string Duplicate = "duplicate";
    public const string MissingField = "missing-field";
}

/// <summary>一条远程写命令（信封 ＋ 载荷）。</summary>
public sealed record MqttCommand(
    string Id, long IssuedMs, long TtlMs, string? By, string Target, string Value);

/// <summary>
/// **远程写命令契约**（阶段 285；`§2-#12` M5 第一块）—— 设备侧唯一实现是
/// <c>firmware/t113/app/comm/mqtt_cmd_contract.c</c>；本类是它对偶。
///
/// <para>主题 <c>&lt;prefix&gt;/cmd/&lt;target&gt;</c>，载荷 <c>key=value;</c>（与 M1/M2 同一套分段语义）。
/// 三条硬规则：**幂等 id 形状** · **TTL 上下界** · **过期即丢（含"来自未来"）**。</para>
/// </summary>
public static class MqttCommandContract
{
    public const int IdMaxLength = 64;
    public const long MinTtlMs = 1000;
    public const long MaxTtlMs = 300_000;
    public const long ClockSkewMs = 5_000;

    /// <summary><c>&lt;prefix&gt;/cmd/&lt;target&gt;</c>。</summary>
    public static string FormatTopic(string prefix, string target)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空", nameof(prefix));
        if (string.IsNullOrEmpty(target)) throw new ArgumentException("目标不能为空", nameof(target));
        return $"{prefix}/cmd/{target}";
    }

    /// <summary>幂等 id 形状：非空、≤64、只许 <c>[A-Za-z0-9_-]</c>（分号/等号会破坏载荷，一律拒）。</summary>
    public static bool IsIdValid(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > IdMaxLength) return false;
        foreach (var c in id)
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-'))
                return false;
        return true;
    }

    /// <summary>TTL 上下界（<c>0</c> 是"永久有效"，必须拒）。</summary>
    public static bool IsTtlValid(long ttlMs) => ttlMs is >= MinTtlMs and <= MaxTtlMs;

    /// <summary>
    /// 新鲜度：<c>issued</c> 在未来超过 <see cref="ClockSkewMs"/> ⇒ 拒（伪造/时钟快）；
    /// <c>now - issued &gt; ttl</c> ⇒ 拒（**超过**才算过期 —— 恰好 = TTL 收，闭区间）。
    /// </summary>
    public static bool IsFresh(long issuedMs, long ttlMs, long nowMs)
    {
        if (!IsTtlValid(ttlMs)) return false;
        if (issuedMs > nowMs && issuedMs - nowMs > ClockSkewMs) return false;
        if (nowMs > issuedMs && nowMs - issuedMs > ttlMs) return false;
        return true;
    }

    /// <summary>拼载荷（与设备侧同序：<c>id;ts;ttl;[by;]target;value</c>）。</summary>
    public static string FormatPayload(MqttCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsIdValid(command.Id)) throw new ArgumentException("幂等 id 不合法", nameof(command));
        if (!IsTtlValid(command.TtlMs)) throw new ArgumentException("TTL 越界", nameof(command));
        if (string.IsNullOrEmpty(command.Target)) throw new ArgumentException("目标不能为空", nameof(command));

        var sb = new StringBuilder();
        sb.Append("id=").Append(command.Id)
          .Append(";ts=").Append(command.IssuedMs.ToString(CultureInfo.InvariantCulture))
          .Append(";ttl=").Append(command.TtlMs.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(command.By)) sb.Append(";by=").Append(command.By);
        sb.Append(";target=").Append(command.Target)
          .Append(";value=").Append(command.Value ?? "");
        return sb.ToString();
    }

    /// <summary>解析载荷；缺字段/形状不合规 ⇒ <c>null</c>（**不**"尽力而为"地收一半）。</summary>
    public static MqttCommand? TryParsePayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var segment in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;                     // 不是 k=v 的段直接跳过（与 MqttPayloadFields 同规矩）
            fields[segment[..eq]] = segment[(eq + 1)..];
        }

        if (!fields.TryGetValue("id", out var id) || !IsIdValid(id)) return null;
        if (!fields.TryGetValue("ts", out var tsText) ||
            !long.TryParse(tsText, NumberStyles.None, CultureInfo.InvariantCulture, out var ts)) return null;
        if (!fields.TryGetValue("ttl", out var ttlText) ||
            !long.TryParse(ttlText, NumberStyles.None, CultureInfo.InvariantCulture, out var ttl) ||
            !IsTtlValid(ttl)) return null;
        if (!fields.TryGetValue("target", out var target) || string.IsNullOrEmpty(target)) return null;

        fields.TryGetValue("by", out var by);
        fields.TryGetValue("value", out var value);
        return new MqttCommand(id, ts, ttl, by, target, value ?? "");
    }

    /// <summary>端到端验收（解析 ＋ 新鲜度 ＋ 幂等去重），返回稳定码或 <c>null</c>=收。</summary>
    public static string? Accept(string? payload, long nowMs, ICommandLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var command = TryParsePayload(payload);
        if (command is null) return MqttCommandIssue.MissingField;

        if (command.IssuedMs > nowMs && command.IssuedMs - nowMs > ClockSkewMs)
            return MqttCommandIssue.FromFuture;
        if (nowMs > command.IssuedMs && nowMs - command.IssuedMs > command.TtlMs)
            return MqttCommandIssue.Expired;

        return ledger.TryAccept(command.Id, nowMs, command.TtlMs) ? null : MqttCommandIssue.Duplicate;
    }
}
