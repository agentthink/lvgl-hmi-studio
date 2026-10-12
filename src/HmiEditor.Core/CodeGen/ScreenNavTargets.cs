using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 「系统动作 → 目标屏」的**候选名表** —— 唯一来源。
///
/// <para><b>为什么需要它：</b>设备端 <c>hmi_actions.c</c> 是手写 C，编译期不知道工程里有哪些屏，
/// 所以用一组候选屏名做模糊匹配（<c>try_switch</c>）。而**导出的模拟器 <c>main.c</c> 是生成物，
/// 生成期就知道工程的全部屏名** —— 那就没必要在运行时再猜一次：直接解析出目标屏、发射确定的调用。
/// 但"哪几个名字算报警屏"这条约定必须是**一份**，否则两边会挑到不同的屏（预览与设备不一致）。</para>
///
/// <para><b>权威在哪：</b>本类是权威；固件里那三份数组是它的孪生副本，由
/// <c>ScreenNavTargetsTests</c> 逐项比对钉住（改名/加名字而不同步就会红）。</para>
/// </summary>
public static class ScreenNavTargets
{
    /// <summary>报警屏候选名（对应固件 <c>hmi_show_alarms</c> 里的 candidates 数组，顺序敏感）。</summary>
    public static readonly IReadOnlyList<string> Alarm =
        ["alarm", "alarms", "alarm_list", "报警", "报警列表", "AlarmList"];

    /// <summary>趋势屏候选名（对应固件 <c>hmi_show_trends</c>，顺序敏感）。</summary>
    public static readonly IReadOnlyList<string> Trends =
        ["trend", "trends", "trend_view", "趋势", "趋势图", "TrendView"];

    /// <summary>登录屏候选名（对应固件 <c>hmi_login</c>，顺序敏感）。</summary>
    public static readonly IReadOnlyList<string> Login =
        ["login", "login_screen", "登录", "登录屏", "Login"];

    /// <summary>本类定义的候选表全集，顺序与固件文件里的出现顺序一致（供孪生守卫遍历）。</summary>
    public static readonly IReadOnlyList<IReadOnlyList<string>> All = [Login, Alarm, Trends];

    /// <summary>
    /// 按候选顺序在工程里找**第一块**匹配的屏，返回它的名字；都不匹配则 <c>null</c>。
    ///
    /// <para>顺序刻意按**候选表**而不是按屏幕顺序 —— 与固件的 <c>try_switch</c> 一致
    /// （它也是先遍历候选、再逐个去比对屏名），这样"同时存在 alarm 与 报警列表 两块屏"时
    /// 两边挑到的是同一块。</para>
    /// </summary>
    public static string? Resolve(HmiProject project, IReadOnlyList<string> candidates) =>
        Resolve(project.Screens, candidates);

    /// <summary>
    /// 重载：直接给屏幕清单。会话超时后的锁屏目标解析要用它 ——
    /// <see cref="LockScreenTargets"/> 手里只有清单（它不该拿到整个工程，见那边的注释）。
    ///
    /// <para><paramref name="screens"/> 允许为 <c>null</c>：那是"生成期没有屏幕信息"这条合法输入
    /// （<see cref="SecurityConfigCodeGen.Generate"/> 的简化重载），返回 <c>null</c> 表示解析不出来。
    /// 让调用方去 <c>screens!</c> 断言反而更糟 —— 它把"没有清单"这件正常的事表达成了"编译器你相信我"。</para>
    /// </summary>
    public static string? Resolve(IReadOnlyList<HmiScreen>? screens, IReadOnlyList<string> candidates)
    {
        if (screens is null) return null;
        foreach (var candidate in candidates)
        {
            if (screens.Any(s => s.Name == candidate)) return candidate;
        }
        return null;
    }

    /// <summary>供生成代码写的候选表说明（如 <c>alarm / alarms / alarm_list</c>）。</summary>
    public static string Describe(IReadOnlyList<string> candidates) => string.Join(" / ", candidates);
}
