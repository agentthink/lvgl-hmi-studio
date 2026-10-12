namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警配置 —— 定义单个报警的所有属性。
/// </summary>
public class AlarmConfig
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>报警名称（如"电机过载"）</summary>
    public string Name { get; set; } = "";

    /// <summary>报警描述（详细说明）</summary>
    public string? Description { get; set; }

    /// <summary>严重级别</summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>所属分组 ID</summary>
    public string GroupId { get; set; } = "";

    /// <summary>是否需要确认</summary>
    public bool RequiresAck { get; set; } = true;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>触发条件</summary>
    public AlarmCondition Condition { get; set; } = new();

    /// <summary>关联的脚本 ID（可选，触发时执行）</summary>
    public string? ScriptId { get; set; }

    /// <summary>关联的控件 ID（可选，触发时闪烁/弹窗）</summary>
    public string? LinkedControlId { get; set; }

    // ── ISA-18.2 职责字段（阶段 266；登记 §2-#11 切片㊀）──
    // ISA-18.2 要求每条报警在**主报警表（alarm philosophy）**里写清三件事：
    //   原因（为什么会报）/ 响应（操作员该做什么）/ 后果（不处理会怎样）。
    // 三件都是**可空**：本仓要兼容老工程（缺字段 ⇒ 空），且是否必填是产品口径（方案 §4 待拍板）。
    // 消费点：`AlarmPhilosophyReport`（主报警表导出）与 `AlarmConfigView` 编辑面板。

    /// <summary>原因（ISA-18.2 cause）—— 为什么会报警。</summary>
    public string? Cause { get; set; }

    /// <summary>响应（ISA-18.2 response）—— 操作员应当做什么。</summary>
    public string? Response { get; set; }

    /// <summary>后果（ISA-18.2 consequence）—— 不处理会导致什么。</summary>
    public string? Consequence { get; set; }

    /// <summary>
    /// 允许响应时间（ms，ISA-18.2 切片㊁；可空 ⇒ 用 <see cref="AlarmPriority.DefaultAllowedResponseMs"/>）。
    ///
    /// <para>它是**优先级推导的输入**（`优先级 = f(严重度, 允许响应时间)`），不是自由填的优先级数字 ——
    /// 存优先级会陈旧，推导不会。消费点：<see cref="AlarmPriority"/>" +
    /// " 与 `AlarmPhilosophyReport` 的优先级列。</para>
    /// </summary>
    public int? AllowedResponseMs { get; set; }
}
