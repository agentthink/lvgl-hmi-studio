namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 搁置/确认被拒的**码**（阶段 274；契约 §4）。与 `LoginIssue` / `UserAdminIssue` 同款：
/// 界面要能**按码**分派文案，而不是统一一句"操作失败"。
/// </summary>
public static class AlarmShelveIssue
{
    /// <summary>目标记录不在"可搁置"的状态（`Inactive`）。</summary>
    public const string NotActive = "not-active";

    /// <summary>已恢复（`Returned`）—— 它本来就不播报了，搁置没有意义。</summary>
    public const string Returned = "returned";

    /// <summary>已经确认过，不能再确认。</summary>
    public const string AlreadyAcked = "already-acked";

    /// <summary>**搁置中不允许确认**（契约里最要紧的一条）。</summary>
    public const string Shelved = "shelved";

    /// <summary>已经搁置中，不能重复搁置（要延期就先解除）。</summary>
    public const string AlreadyShelved = "already-shelved";

    /// <summary>搁置时长越界（`[1 min, 8 h]`）。</summary>
    public const string DurationOutOfRange = "duration-out-of-range";

    /// <summary>没有 `alarm.shelve` 权限。</summary>
    public const string Forbidden = "forbidden";

    /// <summary>找不到记录。</summary>
    public const string NotFound = "not-found";

    /// <summary>码 → 字典键（`alarm.shelve.issue.*`）。未登记的码回落 <see cref="Forbidden"/> 的键。</summary>
    public static string Key(string? code) => code switch
    {
        NotActive => "alarm.shelve.issue.not_active",
        Returned => "alarm.shelve.issue.returned",
        AlreadyAcked => "alarm.shelve.issue.already_acked",
        Shelved => "alarm.shelve.issue.shelved",
        AlreadyShelved => "alarm.shelve.issue.already_shelved",
        DurationOutOfRange => "alarm.shelve.issue.duration_out_of_range",
        NotFound => "alarm.shelve.issue.not_found",
        _ => "alarm.shelve.issue.forbidden",
    };
}

/// <summary>搁置/解除/确认的结果（成功或带码失败）。</summary>
/// <param name="Ok">是否成功了。</param>
/// <param name="IssueCode">失败时的码（见 <see cref="AlarmShelveIssue"/>）；成功为 null。</param>
/// <param name="Record">涉及的记录（找不到时为 null）。</param>
public readonly record struct AlarmShelveResult(bool Ok, string? IssueCode, AlarmRecord? Record)
{
    public static AlarmShelveResult Success(AlarmRecord record) => new(true, null, record);
    public static AlarmShelveResult Fail(string code, AlarmRecord? record = null) => new(false, code, record);
}

/// <summary>搁置审计事件（阶段 278；契约 §7）—— 引擎只**发出事件**，落盘由宿主决定（同源一处写）。</summary>
/// <param name="AlarmConfigId">报警配置 ID。</param>
/// <param name="AlarmName">报警名（冗余，配置删了也不丢上下文）。</param>
/// <param name="Shelved">true=搁置事件，false=解除事件。</param>
/// <param name="Granted">成功/被拒（被拒也要留痕 —— 契约 §5）。</param>
/// <param name="IssueCode">被拒时的码（见 <see cref="AlarmShelveIssue"/>）。</param>
/// <param name="By">操作人（到期自动解除时为 null）。</param>
/// <param name="Until">搁置到期时间（解除事件为 null）。</param>
/// <param name="Reason">搁置原因 / 解除原因（<c>Manual</c>／<c>Expired</c>）／被拒时为空。</param>
public readonly record struct AlarmShelveAudit(
    string AlarmConfigId,
    string AlarmName,
    bool Shelved,
    bool Granted,
    string? IssueCode,
    string? By,
    DateTime? Until,
    string? Reason);

/// <summary>
/// **抑制审计事件**（阶段 280；契约 §2 S1 的对照面）—— 抑制＝**不产生记录**（不是产生后隐藏），
/// 所以它没有状态、没有到期时间，只有"从什么时候起、由谁、按哪个报警"。
/// </summary>
/// <param name="AlarmConfigId">报警配置 ID。</param>
/// <param name="AlarmName">报警名。</param>
/// <param name="Suppressed">true=开始抑制，false=解除抑制。</param>
/// <param name="Granted">成功/被拒（被拒也留痕）。</param>
/// <param name="IssueCode">被拒时的码。</param>
/// <param name="By">操作人。</param>
/// <param name="Reason">原因（开始抑制时填写；解除时可为空）。</param>
public readonly record struct AlarmSuppressAudit(
    string AlarmConfigId,
    string AlarmName,
    bool Suppressed,
    bool Granted,
    string? IssueCode,
    string? By,
    string? Reason);
