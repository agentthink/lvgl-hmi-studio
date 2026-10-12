using System.Collections.Concurrent;
using HmiEditor.Core.Models.Alarm;

namespace HmiEditor.Core.Services;

/// <summary>
/// 报警引擎 —— 条件评估与状态机管理。
///
/// 状态机:
///   Inactive → (条件满足) → Active → (操作员确认) → Acknowledged → (条件解除) → Returned
///   Inactive → (条件满足) → Active → (条件解除, 无需确认) → Returned
///   Inactive → (条件满足) → Active → (条件解除, 需要确认) → Acknowledged → Returned
///
/// 五种触发条件: HighLimit, LowLimit, BitTrigger, StateChange, Deviation
/// 死区防抖 + 延迟触发机制
/// </summary>
public class AlarmEngine : IDisposable
{
    private readonly Dictionary<string, AlarmConfig> _alarmConfigs = new();
    private readonly ConcurrentDictionary<string, AlarmRecord> _activeRecords = new();
    private readonly Dictionary<string, double> _lastVariableValues = new();
    private readonly Dictionary<string, DateTime> _conditionMetSince = new();
    /// <summary>被抑制的报警配置 ID → 原因（阶段 280）。抑制期间**不求值、不产生记录**。</summary>
    private readonly Dictionary<string, string?> _suppressed = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    private readonly Timer? _evaluationTimer;
    private int _evaluationIntervalMs = 500;
    private bool _disposed;

    /// <summary>报警触发事件</summary>
    public event EventHandler<AlarmRecord>? AlarmTriggered;

    /// <summary>报警确认事件</summary>
    public event EventHandler<AlarmRecord>? AlarmAcknowledged;

    /// <summary>报警恢复事件</summary>
    public event EventHandler<AlarmRecord>? AlarmReturned;

    /// <summary>报警被搁置（阶段 274）—— <c>Reason</c> 传的是搁置原因。</summary>
    public event EventHandler<AlarmRecord>? AlarmShelved;

    /// <summary>搁置被解除 —— <c>Reason</c> 传 <c>Expired</c>（到期）或 <c>Manual</c>（手动）。</summary>
    public event EventHandler<AlarmRecord>? AlarmUnshelved;

    /// <summary>
    /// **搁置审计**（阶段 278；契约 §7）—— 成功/被拒/解除（含到期）都发；
    /// 引擎只发事件，落盘由宿主（`AlarmAuditWiring`）负责 —— 保证"审计只有一处写"。
    /// </summary>
    public event EventHandler<AlarmShelveAudit>? ShelveAudited;

    /// <summary>**抑制审计**（阶段 280）：开始/解除/被拒都发（落盘同样由宿主负责）。</summary>
    public event EventHandler<AlarmSuppressAudit>? SuppressionAudited;

    /// <summary>日志输出（注入用，为空则静默）</summary>
    public Action<string>? Log { get; set; }

    public AlarmEngine()
    {
        _evaluationTimer = new Timer(EvaluateAll, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 设置轮询评估间隔。默认 500ms。
    /// </summary>
    public int EvaluationIntervalMs
    {
        get => _evaluationIntervalMs;
        set
        {
            _evaluationIntervalMs = Math.Max(50, value);
            if (IsRunning)
                Start();
        }
    }

    /// <summary>引擎是否正在运行</summary>
    public bool IsRunning { get; private set; }

    // ── 配置管理 ──

    /// <summary>
    /// 注册一个报警配置。
    /// </summary>
    public void RegisterAlarm(AlarmConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_lock)
        {
            _alarmConfigs[config.Id] = config;
        }
        Log?.Invoke($"[AlarmEngine] 注册报警: {config.Name} (ID={config.Id})");
    }

    /// <summary>
    /// 批量注册报警配置。
    /// </summary>
    public void RegisterAlarms(IEnumerable<AlarmConfig> configs)
    {
        foreach (var config in configs)
            RegisterAlarm(config);
    }

    /// <summary>
    /// 移除报警配置。
    /// </summary>
    public void UnregisterAlarm(string alarmId)
    {
        lock (_lock)
        {
            _alarmConfigs.Remove(alarmId);
            _conditionMetSince.Remove(alarmId);
        }
        Log?.Invoke($"[AlarmEngine] 移除报警: {alarmId}");
    }

    /// <summary>
    /// 获取所有已注册的报警配置。
    /// </summary>
    public IReadOnlyList<AlarmConfig> GetAlarmConfigs()
    {
        lock (_lock)
        {
            return _alarmConfigs.Values.ToList();
        }
    }

    // ── 运行控制 ──

    /// <summary>
    /// 启动报警引擎，开始周期性条件评估。
    /// </summary>
    public void Start()
    {
        IsRunning = true;
        _evaluationTimer?.Change(0, _evaluationIntervalMs);
        Log?.Invoke($"[AlarmEngine] 启动 (间隔={_evaluationIntervalMs}ms)");
    }

    /// <summary>
    /// 停止报警引擎。
    /// </summary>
    public void Stop()
    {
        IsRunning = false;
        _evaluationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Log?.Invoke("[AlarmEngine] 停止");
    }

    // ── 变量值更新 ──

    /// <summary>
    /// 更新变量的当前值（引擎会重新评估关联的报警条件）。
    /// </summary>
    public void UpdateVariableValue(string variableId, double value)
    {
        lock (_lock)
        {
            _lastVariableValues[variableId] = value;
        }
    }

    /// <summary>
    /// 批量更新变量值。
    /// </summary>
    public void UpdateVariableValues(Dictionary<string, double> values)
    {
        lock (_lock)
        {
            foreach (var kv in values)
                _lastVariableValues[kv.Key] = kv.Value;
        }
    }

    // ── 操作 ──

    /// <summary>
    /// 确认指定报警记录（布尔版，保留原签名）。
    /// ⚠ **搁置中的记录会被拒绝**（见 <see cref="TryAcknowledgeAlarm"/>）—— 搁置期间"确认"会让操作员
    /// 以为"我已处理"，而实际报警被静音了（契约 §4 注 6）。
    /// </summary>
    public bool AcknowledgeAlarm(string recordId, string? acknowledgedBy = null) =>
        TryAcknowledgeAlarm(recordId, acknowledgedBy).Ok;

    /// <summary>确认（带码）：合法迁移只有 <see cref="AlarmState.Active"/> ⇒ <see cref="AlarmState.Acknowledged"/>。</summary>
    public AlarmShelveResult TryAcknowledgeAlarm(string recordId, string? acknowledgedBy = null)
    {
        if (!_activeRecords.TryGetValue(recordId, out var record))
            return AlarmShelveResult.Fail(AlarmShelveIssue.NotFound);

        if (!AlarmShelvePolicy.CanAcknowledge(record.State, out var issue))
        {
            Log?.Invoke($"[AlarmEngine] 确认被拒({issue}): {record.AlarmName} (state={record.State})");
            return AlarmShelveResult.Fail(issue!, record);
        }

        record.State = AlarmState.Acknowledged;
        record.AcknowledgedAt = DateTime.UtcNow;
        record.AcknowledgedBy = acknowledgedBy;
        AlarmAcknowledged?.Invoke(this, record);
        Log?.Invoke($"[AlarmEngine] 确认报警: {record.AlarmName}");
        return AlarmShelveResult.Success(record);
    }

    // ── 搁置（阶段 274；契约 docs/2026-10-01-alarm-shelving-contract.md）──

    /// <summary>
    /// 搁置一条报警（**停止播报**）。必须给 <c>[1 min, 8 h]</c> 内的时长；
    /// <paramref name="permitted"/> = false ⇒ 拒 `forbidden`（权限判定在调用方，但**引擎仍要拒**，
    /// 免得上层忘判）。
    /// </summary>
    public AlarmShelveResult Shelve(string recordId, TimeSpan duration,
                                    string? shelvedBy = null, string? reason = null,
                                    bool permitted = true)
    {
        if (!_activeRecords.TryGetValue(recordId, out var record))
            return AlarmShelveResult.Fail(AlarmShelveIssue.NotFound);

        if (!permitted)
        {
            Log?.Invoke($"[AlarmEngine] 搁置被拒(forbidden): {record.AlarmName} by={shelvedBy ?? "?"}");
            RaiseShelveAudit(record, shelved: true, granted: false, AlarmShelveIssue.Forbidden, shelvedBy, until: null, reason: null);
            return AlarmShelveResult.Fail(AlarmShelveIssue.Forbidden, record);
        }

        if (!AlarmShelvePolicy.CanShelve(record.State, duration, out var issue))
        {
            Log?.Invoke($"[AlarmEngine] 搁置被拒({issue}): {record.AlarmName} (state={record.State}, {duration})");
            RaiseShelveAudit(record, shelved: true, granted: false, issue, shelvedBy, until: null, reason: null);
            return AlarmShelveResult.Fail(issue!, record);
        }

        var now = DateTime.UtcNow;
        record.State = AlarmState.Shelved;
        record.ShelvedBy = shelvedBy;
        record.ShelvedAt = now;
        record.ShelvedUntil = now + duration;
        record.ShelveReason = reason;

        AlarmShelved?.Invoke(this, record);
        RaiseShelveAudit(record, shelved: true, granted: true, issueCode: null, shelvedBy,
            until: record.ShelvedUntil, reason: reason);
        Log?.Invoke($"[AlarmEngine] 搁置报警: {record.AlarmName} 至 {record.ShelvedUntil:O}（{reason ?? "未填原因"}）");
        return AlarmShelveResult.Success(record);
    }

    /// <summary>手动解除搁置。解除时**重新求值一次**决定回到哪个状态（契约 §4 注 8）。</summary>
    public AlarmShelveResult Unshelve(string recordId, string? unshelvedBy = null) =>
        ReleaseShelve(recordId, "Manual", unshelvedBy);

    /// <summary>
    /// **到期扫描**：把已过 <see cref="AlarmRecord.ShelvedUntil"/> 的搁置自动解除。
    /// 由 <see cref="EvaluateAll"/> 每轮调用；判据也可以直接传时间点**确定性地**驱动它。
    /// </summary>
    public IReadOnlyList<AlarmRecord> ExpireShelves(DateTime nowUtc)
    {
        var expired = _activeRecords.Values
            .Where(r => r.State == AlarmState.Shelved && r.ShelvedUntil is { } until && until <= nowUtc)
            .ToList();

        foreach (var record in expired)
            ReleaseShelve(record.Id, "Expired", unshelvedBy: null, nowUtc: nowUtc);

        return expired;
    }

    /// <summary>解除搁置的公共实现（手动/到期共用 —— 两处的审计与状态恢复必须一模一样）。</summary>
    private AlarmShelveResult ReleaseShelve(string recordId, string reason,
                                            string? unshelvedBy, DateTime? nowUtc = null)
    {
        if (!_activeRecords.TryGetValue(recordId, out var record))
            return AlarmShelveResult.Fail(AlarmShelveIssue.NotFound);

        if (!AlarmShelvePolicy.CanUnshelve(record.State, out var issue))
            return AlarmShelveResult.Fail(issue!, record);

        var now = nowUtc ?? DateTime.UtcNow;
        record.UnshelvedBy = unshelvedBy;
        record.UnshelvedAt = now;
        record.UnshelveReason = reason;

        // 契约 §4 注 8：恢复"如果没搁置，现在应该是什么状态" —— 引擎就地读当前变量值重新求值一次。
        RestoreUnderlyingState(record, now);

        AlarmUnshelved?.Invoke(this, record);
        RaiseShelveAudit(record, shelved: false, granted: true, issueCode: null, releasedBy: unshelvedBy, reason: reason);
        Log?.Invoke($"[AlarmEngine] 解除搁置({reason}): {record.AlarmName} ⇒ {record.State}");
        return AlarmShelveResult.Success(record);
    }

    /// <summary>
    /// 解除搁置时重算底层状态：条件仍满足 ⇒ 回到 <see cref="AlarmState.Active"/>（保留原触发时间）；
    /// 条件已解除 ⇒ <see cref="AlarmState.Returned"/> ＋ 记恢复时间。
    /// ⚠ 变量还没初值 ⇒ 保守地回到 <see cref="AlarmState.Active"/>（宁可再响一次，不可静默吞掉）。
    /// </summary>
    private void RestoreUnderlyingState(AlarmRecord record, DateTime now)
    {
        AlarmConfig? config;
        double currentValue;
        lock (_lock)
        {
            config = _alarmConfigs.GetValueOrDefault(record.AlarmConfigId);
            var hasValue = _lastVariableValues.TryGetValue(config?.Condition.VariableId ?? "", out currentValue);
            if (config is null || !hasValue)
            {
                record.State = AlarmState.Active;
                return;
            }
        }

        if (IsConditionMet(config.Condition, currentValue))
        {
            record.State = AlarmState.Active;
            return;
        }

        record.State = AlarmState.Returned;
        record.ReturnedAt = now;
        AlarmReturned?.Invoke(this, record);
    }

    /// <summary>发一条搁置审计（成功/被拒/解除都走这里 —— 免得三处各拼一遍字段）。</summary>
    private void RaiseShelveAudit(AlarmRecord record, bool shelved, bool granted, string? issueCode,
                                  string? shelvedBy = null, string? releasedBy = null,
                                  DateTime? until = null, string? reason = null)
        => ShelveAudited?.Invoke(this, new AlarmShelveAudit(
            record.AlarmConfigId, record.AlarmName, shelved, granted, issueCode,
            shelved ? shelvedBy : releasedBy, until, reason));

    // ── 抑制（阶段 280；与搁置的分工见契约 §2）──

    /// <summary>
    /// **抑制**一条报警：抑制期间它**不产生记录**（求值整条跳过）。
    /// 与搁置的区别：搁置是"已经报了的先别吵"（有状态、有到期），抑制是"压根不要报"（无状态、无到期）。
    /// </summary>
    public AlarmShelveResult Suppress(string alarmConfigId, string? by = null, string? reason = null,
                                      bool permitted = true)
    {
        if (string.IsNullOrEmpty(alarmConfigId)) return AlarmShelveResult.Fail(AlarmShelveIssue.NotFound);

        AlarmConfig? config;
        lock (_lock) config = _alarmConfigs.GetValueOrDefault(alarmConfigId);
        var name = config?.Name ?? alarmConfigId;

        if (!permitted)
        {
            Log?.Invoke($"[AlarmEngine] 抑制被拒(forbidden): {name} by={by ?? "?"}");
            RaiseSuppressAudit(alarmConfigId, name, suppressed: true, granted: false,
                AlarmShelveIssue.Forbidden, by, reason);
            return AlarmShelveResult.Fail(AlarmShelveIssue.Forbidden);
        }

        lock (_lock)
        {
            if (_suppressed.ContainsKey(alarmConfigId))
            {
                Log?.Invoke($"[AlarmEngine] 抑制被拒(already): {name}");
                // 重复抑制也留痕：审计的价值一半在"谁试过"（契约 §5 的"不静默失败"）
                RaiseSuppressAudit(alarmConfigId, name, suppressed: true, granted: false,
                    AlarmShelveIssue.AlreadyShelved, by, reason);
                return AlarmShelveResult.Fail(AlarmShelveIssue.AlreadyShelved);
            }

            _suppressed[alarmConfigId] = reason;
        }

        Log?.Invoke($"[AlarmEngine] 抑制报警: {name}（{reason ?? "未填原因"}）");
        RaiseSuppressAudit(alarmConfigId, name, suppressed: true, granted: true, issueCode: null, by, reason);
        return AlarmShelveResult.Success(CreateAuditStub(alarmConfigId, name));
    }

    /// <summary>解除抑制（下次求值即恢复；抑制期间没产生过记录，所以没有"补报"）。</summary>
    public AlarmShelveResult Unsuppress(string alarmConfigId, string? by = null)
    {
        string? reason;
        lock (_lock)
        {
            if (!_suppressed.TryGetValue(alarmConfigId, out reason))
            {
                Log?.Invoke($"[AlarmEngine] 解除抑制被拒(not suppressed): {alarmConfigId}");
                RaiseSuppressAudit(alarmConfigId, alarmConfigId, suppressed: false, granted: false,
                    AlarmShelveIssue.NotActive, by, reason: null);
                return AlarmShelveResult.Fail(AlarmShelveIssue.NotActive);
            }
            _suppressed.Remove(alarmConfigId);
        }

        AlarmConfig? config;
        lock (_lock) config = _alarmConfigs.GetValueOrDefault(alarmConfigId);
        var name = config?.Name ?? alarmConfigId;

        Log?.Invoke($"[AlarmEngine] 解除抑制: {name}");
        RaiseSuppressAudit(alarmConfigId, name, suppressed: false, granted: true, issueCode: null, by, reason);
        return AlarmShelveResult.Success(CreateAuditStub(alarmConfigId, name));
    }

    /// <summary>该报警当前是否被抑制。</summary>
    public bool IsSuppressed(string alarmConfigId)
    {
        lock (_lock) return _suppressed.ContainsKey(alarmConfigId);
    }

    /// <summary>当前被抑制的报警配置 ID（按 ID 序，便于判据与界面稳定显示）。</summary>
    public IReadOnlyList<string> GetSuppressedAlarmIds()
    {
        lock (_lock)
            return _suppressed.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    private void RaiseSuppressAudit(string alarmConfigId, string name, bool suppressed, bool granted,
                                    string? issueCode, string? by, string? reason)
        => SuppressionAudited?.Invoke(this, new AlarmSuppressAudit(alarmConfigId, name, suppressed, granted, issueCode, by, reason));

    /// <summary>给"抑制"这类**没有记录**的操作一个返回载体（<see cref="AlarmShelveResult.Record"/> 允许为空壳）。</summary>
    private static AlarmRecord CreateAuditStub(string alarmConfigId, string name) =>
        new() { AlarmConfigId = alarmConfigId, AlarmName = name };

    /// <summary>当前处于搁置中的记录（**不参与播报** —— 远程端/报警条按它过滤）。</summary>
    public IReadOnlyList<AlarmRecord> GetShelvedAlarms() =>
        _activeRecords.Values
            .Where(r => r.State == AlarmState.Shelved)
            .OrderByDescending(r => r.OccurredAt)
            .ToList();

    /// <summary>
    /// 获取当前处于激活/待确认状态的报警记录。
    /// </summary>
    public IReadOnlyList<AlarmRecord> GetActiveAlarms()
    {
        // 搁置中的记录**不在播报面**（那正是搁置的意义）—— 要它们请走 GetShelvedAlarms()。
        return _activeRecords.Values
            .Where(r => r.State == AlarmState.Active || r.State == AlarmState.Acknowledged)
            .OrderByDescending(r => r.OccurredAt)
            .ToList();
    }

    /// <summary>
    /// 获取所有报警记录（含已恢复的）。
    /// </summary>
    public IReadOnlyList<AlarmRecord> GetAllRecords()
    {
        return _activeRecords.Values
            .OrderByDescending(r => r.OccurredAt)
            .ToList();
    }

    /// <summary>
    /// 清除已恢复的报警记录。
    /// </summary>
    public void ClearReturnedRecords()
    {
        var returnedIds = _activeRecords.Values
            .Where(r => r.State == AlarmState.Returned)
            .Select(r => r.Id)
            .ToList();

        foreach (var id in returnedIds)
            _activeRecords.TryRemove(id, out _);
    }

    /// <summary>
    /// 清空所有报警记录。
    /// </summary>
    public void ClearAllRecords()
    {
        _activeRecords.Clear();
    }

    // ── 条件评估（定时回调）──

    /// <summary>
    /// **手工评估一次**（阶段 274）：判据需要一个确定性入口 —— 依赖定时器会让用例又慢又飘。
    /// ⚠ 与定时入口的门槛**不同**：手工入口**不要求** <see cref="IsRunning"/>（它就是给"现在评估一次"用的），
    /// 定时回调仍要求引擎在跑。
    /// </summary>
    public void EvaluateOnce()
    {
        if (_disposed) return;
        EvaluateCore();
    }

    private void EvaluateAll(object? state)
    {
        if (_disposed || !IsRunning) return;
        EvaluateCore();
    }

    private void EvaluateCore()
    {
        List<AlarmConfig> configs;

        lock (_lock)
        {
            configs = _alarmConfigs.Values.Where(c => c.IsEnabled).ToList();
        }

        var now = DateTime.UtcNow;

        // 到期搁置先解除（契约 §6：不允许"到期了还挂着"）
        ExpireShelves(now);

        foreach (var config in configs)
        {
            // 抑制＝**不产生记录**（契约 §2 S1 的对照面）：整条求值跳过 —— 既不新建记录，
            // 也不推进既有记录（"先产生再隐藏"不是抑制，那会让报警历史里凭空多出记录）。
            lock (_lock)
            {
                if (_suppressed.ContainsKey(config.Id)) continue;
            }

            try
            {
                EvaluateCondition(config, now);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[AlarmEngine] 评估异常: {config.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 评估单个报警。
    ///
    /// 🩸 <b>变量值必须在这**就地**读，不得由调用方传一份「整轮开始时的一次性快照」</b>（缺陷 #70）：
    /// 定时器回调排在 <c>ThreadPool</c> 上、可被抢占任意时长，于是那一轮的快照可能**在值已被改回
    /// 「不满足条件」之后**才被用于判定 ⇒ <c>isTriggered</c> 为真、而该配置此刻已无「非 Returned」
    /// 记录 ⇒ 会在一条早已 Returned 的记录之外**新建一条 Active 记录**（同一报警在界面上出现两条）。
    /// 实测复现率：`Core.Tests` 整程序集并行跑 **12 次红 1 次**；确定性取证见阶段 117 的探针。
    /// </summary>
    private void EvaluateCondition(AlarmConfig config, DateTime now)
    {
        // 获取变量当前值 —— 就地读；见上方 xmldoc（#70）
        double currentValue;
        lock (_lock)
        {
            if (!_lastVariableValues.TryGetValue(config.Condition.VariableId, out currentValue))
                return; // 变量尚未初始化
        }

        // 检查条件是否满足
        var isTriggered = IsConditionMet(config.Condition, currentValue);

        // 获取该报警当前的活动记录
        var activeRecord = _activeRecords.Values
            .FirstOrDefault(r => r.AlarmConfigId == config.Id &&
                                 r.State != AlarmState.Returned);

        if (isTriggered)
        {
            // 延迟触发逻辑
            if (config.Condition.DelayMs > 0)
            {
                var sinceKey = config.Id;
                lock (_lock)
                {
                    if (!_conditionMetSince.ContainsKey(sinceKey))
                        _conditionMetSince[sinceKey] = now;
                }

                DateTime metSince;
                lock (_lock)
                {
                    metSince = _conditionMetSince.GetValueOrDefault(sinceKey, now);
                }

                if ((now - metSince).TotalMilliseconds < config.Condition.DelayMs)
                    return; // 延迟时间未到
            }

            if (activeRecord == null)
            {
                // 首次触发 — 创建新记录。
                // ⚠ **创建要带二次确认**（阶段 274 实测踩到）：`Start()` 用 `Change(0, …)` 起表 ⇒
                // 定时回调**立即**在 ThreadPool 上跑，可能与手工评估/上一轮交错，两边都看到
                // `activeRecord == null` ⇒ **同一报警出现两条记录**（与 #70 同一类竞态的另一个出口）。
                // 修法：在锁内重查一次；事件在锁外发（处理器可能回调引擎，锁内发会死锁）。
                AlarmRecord? created = null;
                lock (_lock)
                {
                    var stillNone = !_activeRecords.Values.Any(r =>
                        r.AlarmConfigId == config.Id && r.State != AlarmState.Returned);
                    if (stillNone)
                    {
                        created = new AlarmRecord
                        {
                            AlarmConfigId = config.Id,
                            AlarmName = config.Name,
                            Severity = config.Severity,
                            State = AlarmState.Active,
                            TriggerValue = currentValue,
                            OccurredAt = now,
                        };
                        _activeRecords[created.Id] = created;
                    }
                }

                if (created is null) return;   // 别的线程已经建好了 ⇒ 本轮什么都不做

                AlarmTriggered?.Invoke(this, created);
                Log?.Invoke($"[AlarmEngine] ⚠ 报警触发: {config.Name} (值={currentValue}, 阈值={config.Condition.Threshold})");
            }
            else if (activeRecord.State == AlarmState.Returned)
            {
                // 已恢复后再次触发
                activeRecord.State = AlarmState.Active;
                activeRecord.TriggerValue = currentValue;
                activeRecord.OccurredAt = now;
                AlarmTriggered?.Invoke(this, activeRecord);
            }
        }
        else
        {
            // 条件不满足 — 清除延迟标记
            lock (_lock)
            {
                _conditionMetSince.Remove(config.Id);
            }

            if (activeRecord != null)
            {
                if (activeRecord.State == AlarmState.Active)
                {
                    if (config.RequiresAck)
                    {
                        // 需要确认：即使条件解除也保持 Acknowledged，不触发 Returned
                        activeRecord.State = AlarmState.Acknowledged;
                        activeRecord.AcknowledgedAt ??= now;
                    }
                    else
                    {
                        // 无需确认：条件解除 → 直接 Returned
                        activeRecord.State = AlarmState.Returned;
                        activeRecord.ReturnedAt = now;
                        AlarmReturned?.Invoke(this, activeRecord);
                        Log?.Invoke($"[AlarmEngine] ✓ 报警恢复: {config.Name}");
                    }
                }
                else if (activeRecord.State == AlarmState.Acknowledged)
                {
                    // 已确认且条件解除 → Returned
                    activeRecord.State = AlarmState.Returned;
                    activeRecord.ReturnedAt = now;
                    AlarmReturned?.Invoke(this, activeRecord);
                    Log?.Invoke($"[AlarmEngine] ✓ 报警恢复(已确认): {config.Name}");
                }
            }
        }
    }

    // ── 条件判断 ──

    private static bool IsConditionMet(AlarmCondition condition, double currentValue)
    {
        return condition.Type switch
        {
            AlarmConditionType.HighLimit => currentValue >= condition.Threshold,
            AlarmConditionType.LowLimit => currentValue <= condition.Threshold,
            AlarmConditionType.HighHighLimit => currentValue >= condition.Threshold,
            AlarmConditionType.LowLowLimit => currentValue <= condition.Threshold,
            // ⚠ C# 的位移量按 64 取模：offset=64 会静默测 bit 0、负数会回绕 ⇒ 位报警盯错位。
            // 合法域是 0..63，越界一律判"条件不成立"（阶段 121 修复）。
            AlarmConditionType.BitTrigger => condition.BitOffset is >= 0 and <= 63
                                             && ((long)currentValue & (1L << condition.BitOffset)) != 0,
            AlarmConditionType.StateChange => Math.Abs(currentValue - condition.ExpectedValue) > condition.Threshold,
            AlarmConditionType.Deviation => Math.Abs(currentValue - condition.ExpectedValue) > condition.Threshold,
            _ => false,
        };
    }

    // ── 一次性条件检查（用于单元测试） ──

    /// <summary>
    /// 对给定值执行一次性条件检查（不涉及状态机、不产生记录）。
    /// </summary>
    public static bool CheckConditionOnce(AlarmCondition condition, double currentValue)
    {
        return IsConditionMet(condition, currentValue);
    }

    // ── IDisposable ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _evaluationTimer?.Dispose();
        ClearAllRecords();
        Log?.Invoke("[AlarmEngine] 已释放");
    }
}
