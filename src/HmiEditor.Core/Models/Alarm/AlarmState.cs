namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警状态机 —— Inactive → Active → Acknowledged → Returned
/// 或 Inactive → Active → Returned（无需确认时）；
/// 任一非 Returned 档都可进入 <see cref="Shelved"/>（搁置＝**停止播报**，见
/// `docs/2026-10-01-alarm-shelving-contract.md`）。
///
/// <para><b>取值与设备侧同号</b>：0..4 —— 生成器写的 `state 0/1/2/3` 不变，新增的这一档说 <c>4</c>；
/// 报警上行契约（M2）也把 <c>st=4</c> 留给它（`MqttAlarmContract.ShelvedState`）。</para>
/// </summary>
public enum AlarmState
{
    /// <summary>非激活：条件未满足，正常状态</summary>
    Inactive,

    /// <summary>激活中：条件已满足，等待确认</summary>
    Active,

    /// <summary>已确认：操作员确认了报警，条件可能仍满足</summary>
    Acknowledged,

    /// <summary>已恢复：条件已解除，报警生命周期结束</summary>
    Returned,

    /// <summary>
    /// 已搁置（阶段 274；ISA-18.2 shelving）：**停止播报**，但报警还在、条件仍在求值。
    /// 必须带到期时间（`[1 min, 8 h]`），到期自动解除；**搁置中不允许确认**。
    /// </summary>
    Shelved = 4,
}
