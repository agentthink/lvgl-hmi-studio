namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警运行时记录 —— 每次报警触发生成一条记录。
/// 遵循状态机：Inactive → Active → Acknowledged → Returned
/// </summary>
public class AlarmRecord
{
    /// <summary>记录唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>关联的报警配置 ID</summary>
    public string AlarmConfigId { get; set; } = "";

    /// <summary>报警名称（冗余存储，防止配置被删除后丢失上下文）</summary>
    public string AlarmName { get; set; } = "";

    /// <summary>严重级别</summary>
    public AlarmSeverity Severity { get; set; }

    /// <summary>当前状态</summary>
    public AlarmState State { get; set; } = AlarmState.Inactive;

    /// <summary>触发时的变量值</summary>
    public double TriggerValue { get; set; }

    /// <summary>触发时的变量值（原始字符串表示）</summary>
    public string? RawValue { get; set; }

    /// <summary>触发时间</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>确认时间</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>确认人</summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>恢复时间</summary>
    public DateTime? ReturnedAt { get; set; }

    /// <summary>备注</summary>
    public string? Comment { get; set; }

    // ── 搁置审计（阶段 274；契约 §7）——
    // 搁置是"人做的决定"，必须留痕：谁、什么时候、到什么时候、为什么；
    // 解除同样留痕，且**到期自动解除也要写**（否则事后看不出"报警为什么突然又开始响"）。

    /// <summary>搁置人。</summary>
    public string? ShelvedBy { get; set; }

    /// <summary>搁置时间（UTC）。</summary>
    public DateTime? ShelvedAt { get; set; }

    /// <summary>搁置到期时间（UTC）—— 必须有值（不存在"无限搁置"）。</summary>
    public DateTime? ShelvedUntil { get; set; }

    /// <summary>搁置原因（可空，但面板应引导填写）。</summary>
    public string? ShelveReason { get; set; }

    /// <summary>解除人（到期自动解除时为 null）。</summary>
    public string? UnshelvedBy { get; set; }

    /// <summary>解除时间（UTC）。</summary>
    public DateTime? UnshelvedAt { get; set; }

    /// <summary>解除原因：<c>Expired</c>（到期）或 <c>Manual</c>（手动）。</summary>
    public string? UnshelveReason { get; set; }

    /// <summary>是否处于搁置中（<see cref="AlarmState.Shelved"/>）。</summary>
    public bool IsShelved => State == AlarmState.Shelved;
}
