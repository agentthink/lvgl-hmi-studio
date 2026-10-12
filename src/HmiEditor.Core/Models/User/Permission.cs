namespace HmiEditor.Core.Models.User;

/// <summary>
/// 权限枚举 —— 定义 HMI 系统中的所有可授权操作。
/// 设计原则 (identity-and-secrets): 最小权限原则，按操作粒度细分。
/// </summary>
[Flags]
public enum Permission
{
    /// <summary>无权限</summary>
    None = 0,

    /// <summary>查看画面</summary>
    ViewScreen = 1 << 0,

    /// <summary>操作控件（按钮/滑块/开关等交互）</summary>
    OperateControl = 1 << 1,

    /// <summary>编辑配方参数</summary>
    EditRecipe = 1 << 2,

    /// <summary>确认报警</summary>
    AcknowledgeAlarm = 1 << 3,

    /// <summary>修改系统配置</summary>
    ChangeConfig = 1 << 4,

    /// <summary>管理用户账户</summary>
    ManageUsers = 1 << 5,

    /// <summary>查看报警历史</summary>
    ViewAlarmHistory = 1 << 6,

    /// <summary>导出数据记录</summary>
    ExportData = 1 << 7,

    /// <summary>调试/诊断模式</summary>
    DebugMode = 1 << 8,

    /// <summary>
    /// **搁置报警**（阶段 275；`docs/2026-10-01-alarm-shelving-contract.md` 的权限点 `alarm.shelve`）。
    ///
    /// <para>与 <see cref="AcknowledgeAlarm"/> **分开**是刻意的：搁置会让报警**停止播报**，
    /// 是"把报警静音"的权力 —— 给它单独一位，现场才能按班次/角色收放，
    /// 而不是"能确认就能静音"。默认预置角色里它与确认同权（见 <see cref="UserRole"/>），
    /// 项目可以按需收紧。</para>
    /// </summary>
    ShelveAlarm = 1 << 9,

    /// <summary>
    /// 全部权限 = 已定义的位**全占**（阶段 275 加 <see cref="ShelveAlarm"/> 时把 `(1 << 9) - 1`
    /// 提到 `(1 << 10) - 1` —— 忘了提就会出现"新权限不在 All 里"：管理员看着有"全部权限"，
    /// 实际搁置被拒）。
    /// </summary>
    All = (1 << 10) - 1,
}
