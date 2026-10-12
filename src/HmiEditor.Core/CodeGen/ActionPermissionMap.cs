using HmiEditor.Core.Design;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 控件动作 → 所需权限的**唯一来源**。
///
/// <para><b>消费方两处，必须同源：</b>① 生成的动作回调门（<c>CodeGenerator.AppendActionBody</c>
/// 发射 <c>if (!hmi_require_permission(...)) return;</c>）；② 生成的设备头里的注释表
/// （<see cref="SecurityConfigCodeGen"/> 让人一眼看到"哪些动作受哪一位管"）。</para>
///
/// <para><b>两条语义决定（写下来免得后人"顺手统一"）：</b></para>
/// <list type="number">
/// <item><b>Login/Logout 恒不门控</b>（<c>null</c>）：会话动作必须永远可用。给 Logout 加权限门
/// 会产生"权限不够 ⇒ 登不出去"的死锁，而登录门本身就是权限体系的大门，不能拿它自己锁自己。</item>
/// <item><b>未知动作不门控</b>：动作类型是字符串（可能来自手改的 JSON），未知类型由发射器
/// 落成 <c>/* Unknown action */</c> 注释、**不产生任何调用** ⇒ 门控它没有意义。
/// 已知类型的覆盖完整性由测试穷举 <see cref="ActionTypeMeta"/> 的常量守卫，
/// 所以"漏配"不会被这条兜底吞掉。</item>
/// </list>
/// </summary>
public static class ActionPermissionMap
{
    /// <summary>
    /// 动作 → 所需权限的完整清单（<c>null</c> = 不门控）。
    /// <b>双向完整</b>：既不得漏掉 <see cref="ActionTypeMeta"/> 的任何一个动作，也不得出现
    /// 未定义的动作类型 —— 由 <c>ActionPermissionMapTests</c> 两个方向各断言一次。
    /// </summary>
    public static readonly IReadOnlyList<(string ActionType, Permission? Permission)> Entries =
    [
        // 写量/触发脚本 —— 会改变设备状态，操作员及以上
        (ActionTypeMeta.ToggleBit,      Permission.OperateControl),
        (ActionTypeMeta.SetBit,         Permission.OperateControl),
        (ActionTypeMeta.ResetBit,       Permission.OperateControl),
        (ActionTypeMeta.MomentaryOn,    Permission.OperateControl),
        (ActionTypeMeta.MomentaryOff,   Permission.OperateControl),
        (ActionTypeMeta.SetVariable,    Permission.OperateControl),
        (ActionTypeMeta.ExecuteScript,  Permission.OperateControl),

        // 引导类 —— 只是换屏/看趋势，最低的"能看画面"即可
        (ActionTypeMeta.SwitchScreen,   Permission.ViewScreen),
        (ActionTypeMeta.ShowTrends,     Permission.ViewScreen),

        // 报警列表含历史记录，归到"查看报警历史"
        (ActionTypeMeta.ShowAlarms,     Permission.ViewAlarmHistory),

        // 会话动作：永远可用（见类注释第 1 条）
        (ActionTypeMeta.Login,          null),
        (ActionTypeMeta.Logout,         null),
    ];

    /// <summary>
    /// 该动作所需的权限；<c>null</c> 表示不门控（Login/Logout 或未知动作）。
    /// </summary>
    public static Permission? Required(string? actionType)
    {
        foreach (var (type, permission) in Entries)
        {
            if (type == actionType) return permission;
        }
        return null;
    }
}
