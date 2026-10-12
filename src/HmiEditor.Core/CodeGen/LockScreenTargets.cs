using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 「会话超时后跳到哪块屏」—— **生成期解析的唯一来源**。
///
/// <para><b>为什么需要解析这一步：</b><see cref="SecurityConfig.LockScreenId"/> 是**屏幕 Id**
/// （编辑器内部的稳定标识），而设备端 <c>hmi_screen_switch</c> 是按**屏幕名字**匹配的。
/// 两者的对应关系只存在于工程文件里，固件编译期不知道 —— 所以必须在**生成期**换成名字，
/// 让设备端拿到一个可以直接用的字符串。这与 <see cref="ScreenNavTargets"/> 是同一条原则：
/// 「生成期能算出来的，就不要留到运行时猜」。</para>
///
/// <para><b>为什么回退目标是登录屏、而不是"黑屏 + 时间"：</b>会话超时的语义是
/// 「这个人已经不被信任了」，而重新获得信任的唯一途径**就是登录**。所以把用户送回登录屏
/// 是唯一自洽的落点；它同时带来一个实际好处 —— **不配 <c>LockScreenId</c> 的工程也有正确行为**
/// （回退到登录屏候选名，与 <c>hmi_login</c> 用同一张表）。模型注释里那句"系统默认锁屏
/// （黑屏 + 时间显示）"要求固件实现一块并不存在的系统锁屏，本轮不做，故不按它回退。</para>
///
/// <para><b>解析不出来不是错误：</b><see cref="Resolution.ScreenName"/> 为 <c>null</c> 时，
/// 设备端**仍然清除会话**，只是不切屏。安全边界不受影响（下一次权限判定照样拒绝），
/// 丢的只是"屏幕自己跳走"这个视觉提示。<see cref="Resolution.Problem"/> 是这句话的可见化 ——
/// 它会被发进生成头当注释，免得"配了锁屏却不跳"变成一次现场排查。</para>
/// </summary>
public static class LockScreenTargets
{
    /// <summary>
    /// 锁屏目标解析结果。
    /// </summary>
    /// <param name="ScreenName">会话超时时切换到的屏名；<c>null</c> = 不切屏（会话仍被清除）。</param>
    /// <param name="FromConfiguredId">
    /// 是否来自 <see cref="SecurityConfig.LockScreenId"/> 指向的屏。
    /// <c>false</c> 表示走的是回退路径（未配置，或配置的 Id 不存在）。
    /// </param>
    /// <param name="Problem">配置有问题时的诊断文本（会被发进生成头的 C 注释）；无问题为 <c>null</c>。</param>
    public sealed record Resolution(string? ScreenName, bool FromConfiguredId, string? Problem);

    /// <summary>
    /// 解析锁屏目标屏名。
    ///
    /// <para><paramref name="screens"/> 允许为 <c>null</c> / 空（<see cref="SecurityConfigCodeGen.Generate"/>
    /// 的简化重载就是这种情况）：此时**任何**解析都做不了，返回 <c>null</c> 屏名 + 诊断，
    /// 而不是"猜一个名字"。宁可设备端不切屏，也不要让它切到一块不存在的屏上（那会是一次静默失败）。</para>
    /// </summary>
    public static Resolution Resolve(SecurityConfig? config, IReadOnlyList<HmiScreen>? screens)
    {
        var hasScreens = screens is { Count: > 0 };

        if (config is not null && !string.IsNullOrEmpty(config.LockScreenId))
        {
            if (!hasScreens)
            {
                return new Resolution(null, false,
                    $"SecurityConfig.LockScreenId = \"{config.LockScreenId}\" 无法解析"
                    + "（生成时未提供屏幕清单）⇒ 超时后不切屏（会话仍被清除）");
            }

            var hit = screens!.FirstOrDefault(s => s.Id == config.LockScreenId);
            if (hit is not null) return new Resolution(hit.Name, true, null);

            // 配了 Id 但工程里没有这块屏 —— 最常见的成因是删屏后没清配置。
            // 回退登录屏而不是"不切屏"：用户配这条的本意是"超时后别停在原画面"。
            var fallback = ScreenNavTargets.Resolve(screens, ScreenNavTargets.Login);
            return new Resolution(fallback, false,
                $"SecurityConfig.LockScreenId = \"{config.LockScreenId}\" 在工程里不存在，"
                + (fallback is null ? "且没有可回退的登录屏 ⇒ 超时后不切屏" : $"已回退登录屏 \"{fallback}\""));
        }

        if (!hasScreens)
        {
            return new Resolution(null, false,
                "生成时未提供屏幕清单，锁屏目标未解析 ⇒ 超时后不切屏（会话仍被清除）");
        }

        var login = ScreenNavTargets.Resolve(screens, ScreenNavTargets.Login);
        if (login is null)
        {
            return new Resolution(null, false,
                "未配置 LockScreenId，且工程里没有登录屏（候选名 "
                + ScreenNavTargets.Describe(ScreenNavTargets.Login)
                + "）⇒ 超时后不切屏（会话仍被清除）");
        }

        return new Resolution(login, false, null);
    }
}
