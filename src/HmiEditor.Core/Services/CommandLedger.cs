using System;
using System.Collections.Generic;
using System.Linq;

namespace HmiEditor.Core.Services;

/// <summary>幂等台账（判据与设备侧将来的去重表同一语义）。</summary>
public interface ICommandLedger
{
    /// <summary>收下这个 id；已在台账里（且未过期）⇒ false（重复命令）。</summary>
    bool TryAccept(string id, long nowMs, long ttlMs);
}

/// <summary>
/// **有界幂等台账**（阶段 285）—— 键是命令 id，过期条目在每次写入时淘汰。
///
/// <para>为什么有界：远程命令是**不受信输入**，台账不能无限涨（那是内存耗尽攻击面）。
/// 容量上限 + 过期淘汰两条一起，才既判得住"重复不执行"，也不会被刷爆。</para>
/// </summary>
public sealed class CommandLedger : ICommandLedger
{
    private readonly Dictionary<string, long> _seen = new(StringComparer.Ordinal);
    private readonly int _capacity;

    public CommandLedger(int capacity = 1024)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    /// <summary>当前台账条目数（判据用）。</summary>
    public int Count => _seen.Count;

    public bool TryAccept(string id, long nowMs, long ttlMs)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Evict(nowMs);

        if (_seen.ContainsKey(id)) return false;

        // 满了就丢**最先到期**的那条（不是"最先来的"—— 过期的先扔，语义更稳）
        if (_seen.Count >= _capacity)
        {
            var victim = _seen.OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
            _seen.Remove(victim.Key);
        }

        _seen[id] = nowMs + ttlMs;      // 到期时刻：过了它，同一个 id 可以再次使用
        return true;
    }

    private void Evict(long nowMs) =>
        _seen.Where(kv => nowMs > kv.Value).Select(kv => kv.Key).ToList()
             .ForEach(k => _seen.Remove(k));
}
