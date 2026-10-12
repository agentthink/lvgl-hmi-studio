using System;
using System.Collections.Generic;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.Security;

/// <summary>
/// 运行时用户表的**问题类型**。
///
/// <para>刻意只给枚举、不带文案：Core 侧的测试断言枚举（稳定），文案由 App 层映射
/// （可翻译、可改）。若这里直接产中文串，改一次措辞就会红一片测试，
/// 而那些测试本来想证明的是"这条规则被检出了"。</para>
/// </summary>
public enum RuntimeUserIssueKind
{
    /// <summary>
    /// 用户名为空（或纯空白）。
    /// <para><b>注意这不是"死条目"</b>：设备端 <c>hmi_user_lookup</c> 用 <c>strcmp</c> 比对，
    /// 登录屏输入空用户名时 <c>strcmp("", "") == 0</c> ⇒ **空输入即可命中该条目**。
    /// 也就是说它反而比正常条目更容易被命中，只是仍需通过口令校验。
    /// 用户名为空属配置错误（用户名是登录标识）。</para>
    /// </summary>
    EmptyUsername,

    /// <summary>
    /// 用户名重复。设备端 <c>hmi_user_lookup</c> 是**线性查表 + 首个匹配胜出** ⇒
    /// 排在后面的同名条目永远匹配不到（改它也毫无效果，是很隐蔽的"改了没反应"）。
    /// </summary>
    DuplicateUsername,

    /// <summary>
    /// 角色 Id 不在预置角色表内。设备端 <c>hmi_role_lookup</c> 用 <c>strcmp</c> 线性查表，
    /// 查不到返回 -1 ⇒ 即使口令正确，<c>hmi_login_as_role</c> 也直接拒绝（登录失败）。
    /// </summary>
    UnknownRole,

    /// <summary>
    /// 已启用但没有密码哈希。设备端 <c>hmi_verify_password</c> 解析失败即拒绝 ⇒
    /// 该账户**无法登录**（不是"免密放行"）。
    /// </summary>
    EnabledWithoutPassword,
}

/// <summary>运行时用户表的一条问题：<paramref name="Index"/> 指向 <c>SecurityConfig.RuntimeUsers</c> 中的下标。</summary>
public sealed record RuntimeUserIssue(RuntimeUserIssueKind Kind, int Index, string Username);

/// <summary>
/// 运行时用户表的**编辑器侧校验** —— 把"会让设备端登不上/改了没反应"的配置形态提前指出来。
///
/// <para><b>为什么要有这个类：</b>生成器对同样的形态只是发一条 C 注释警告（产物里），
/// 用户要看到它得先导出、再翻 <c>generated_security.h</c>。这四条规则在编辑器里就能判断，
/// 而它们的共同后果是"设备行为与用户预期不一致"：改用户名没反应（重名）、
/// 口令对了也登不进（角色表外）、配了账户却登不上（无密码）。</para>
///
/// <para><b>判据必须与固件同源：</b>用户名与角色 Id 的比对一律 <see cref="StringComparison.Ordinal"/>
/// —— 固件侧是 <c>strcmp</c>，大小写敏感。若这里用 <c>OrdinalIgnoreCase</c>，
/// 编辑器会放过 <c>Op</c>/<c>op</c> 这对"在设备上其实是两个不同用户"的条目。
/// 同理"首个匹配胜出"也是固件 <c>hmi_user_lookup</c> 的实际行为（不是"整表扫描"）。</para>
///
/// <para>本类**不做**的事：不校验口令强度（只能看到哈希，看不到长度）、
/// 不判断"改小了 PasswordMinLength 之后旧口令是否仍达标"（同上，哈希不可逆）。
/// 那两条只能在 UI 上以提示语交代，不能伪装成校验。</para>
/// </summary>
public static class RuntimeUserValidator
{
    /// <summary>校验整张表，返回按表序排列的问题列表（空表 ⇒ 空列表）。</summary>
    public static IReadOnlyList<RuntimeUserIssue> Validate(IReadOnlyList<RuntimeUser>? users)
    {
        var issues = new List<RuntimeUserIssue>();
        if (users is null) return issues;

        // 用户名 → 首次出现的下标。首个胜出，所以**只有后面的**才是无效条目。
        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            var name = user.Username ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                // 空用户名不参与查重：多个空条目若各报一条重复，噪音会盖住真问题
                issues.Add(new RuntimeUserIssue(RuntimeUserIssueKind.EmptyUsername, i, name));
                continue;
            }

            if (firstSeen.ContainsKey(name))
                issues.Add(new RuntimeUserIssue(RuntimeUserIssueKind.DuplicateUsername, i, name));
            else
                firstSeen[name] = i;

            if (!IsKnownRoleId(user.RoleId))
                issues.Add(new RuntimeUserIssue(RuntimeUserIssueKind.UnknownRole, i, name));

            if (user.IsEnabled && string.IsNullOrEmpty(user.PasswordHash))
                issues.Add(new RuntimeUserIssue(RuntimeUserIssueKind.EnabledWithoutPassword, i, name));
        }

        return issues;
    }

    /// <summary>
    /// 角色 Id 是否在预置角色表内（固件 <c>hmi_role_lookup</c> 的等价判据：<c>strcmp</c>，大小写敏感）。
    /// </summary>
    public static bool IsKnownRoleId(string? roleId) =>
        !string.IsNullOrEmpty(roleId)
        && Array.Exists(UserRole.BuiltInAll,
                        r => string.Equals(r.Id, roleId, StringComparison.Ordinal));

    /// <summary>
    /// 用户名是否已被占用（<paramref name="exceptIndex"/> 用于排除"正在编辑的那一条"自身）。
    /// <para>供 UI 做即时反馈；整表体检请用 <see cref="Validate"/>。</para>
    /// </summary>
    public static bool IsUsernameTaken(IReadOnlyList<RuntimeUser>? users, string? username, int exceptIndex = -1)
    {
        if (users is null || string.IsNullOrWhiteSpace(username)) return false;

        for (var i = 0; i < users.Count; i++)
        {
            if (i == exceptIndex) continue;
            if (string.Equals(users[i].Username, username, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// 表中的**有效条目数** —— 口径与设备端一致：非空用户名 + 角色在表内 + 已启用 + 有密码哈希。
    /// <para>用于 UI 判断"配了表但一条都进不去"（比"表非空"更有信息量）。</para>
    /// </summary>
    public static int CountUsable(IReadOnlyList<RuntimeUser>? users)
    {
        if (users is null) return 0;

        var count = 0;
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.Username)) continue;
            if (!IsKnownRoleId(user.RoleId)) continue;
            if (!user.IsEnabled) continue;
            if (string.IsNullOrEmpty(user.PasswordHash)) continue;
            count++;
        }
        return count;
    }

    /// <summary>
    /// 生成一个不与表中既有条目冲突的默认用户名（新增用户时的起点）。
    /// <para>空表返回 <c>user1</c>；否则取第一个未被占用的 <c>userN</c>。</para>
    /// </summary>
    public static string NextDefaultUsername(IReadOnlyList<RuntimeUser>? users)
    {
        for (var n = 1; n < 1000; n++)
        {
            var candidate = $"user{n}";
            if (!IsUsernameTaken(users, candidate)) return candidate;
        }
        return "user";   // 表里已有 999 个 userN —— 交给查重提示去挡
    }
}
