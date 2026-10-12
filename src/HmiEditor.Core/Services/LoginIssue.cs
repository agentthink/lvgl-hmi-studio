namespace HmiEditor.Core.Services;

/// <summary>
/// 编辑器登录失败的**码表**（对应文案键族 <c>login.issue.*</c> 的键尾）。
///
/// <para><b>为什么要有它</b>（登记 <b>#61v</b>，阶段 99）：<see cref="AuthenticationService.Login"/>
/// 原先返回 <c>string?</c> —— 成功给会话 id、**失败给 <c>null</c>**，五种完全不同的失败
/// （用户不存在 / 账户锁定 / 账户禁用 / 账户过期 / 密码错误）**压成同一个 <c>null</c>**
/// ⇒ 界面只能说一句通用的「用户名或密码错误」，用户**说不出是哪一条**。而这五条的处置
/// 完全不同：密码错 ⇒ 重输；锁定 ⇒ 等一会儿；禁用 / 过期 ⇒ 找管理员。
/// 失败原因其实**早就记下来了**（<c>AppendAudit(AuditAction.LoginFailed, …)</c> 里就是中文），
/// 只是没有任何通道把它送回界面。</para>
///
/// <para><b>⚠ 「用户不存在」与「密码错误」刻意共用一个码</b>（<c>BAD_CREDENTIALS</c>）：
/// 分开会让界面变成**用户名枚举**预言机（"这个用户名存在吗"），而且改动前两者本来
/// 就显示同一句 <c>login.err</c> ⇒ 分开属于**范围外的行为变更**。#61v 点名的缺口是
/// 锁定 / 过期那几条，所以这里只收它们。区分仍然保留在**审计日志**里
/// （<c>用户不存在: X</c> 与 <c>密码错误 (n/m)</c> 两串不同）。</para>
///
/// <para><b>码是英文常量、面向下一步</b>：Core 没有字典服务（依赖方向 App → Core），
/// 所以这里只给「码 + 实参 + **中文回落原文**」；查字典与填参在 App 侧的唯一出口
/// <c>ExternalText.Describe(LoginIssue)</c>。三段式与 <c>SvgParseError</c> /
/// <c>CsvImportIssue</c> / <c>RecipeCloneIssue</c> 同形。</para>
/// </summary>
public static class LoginIssueCodes
{
    /// <summary>用户名不存在**或**密码错误 —— 刻意不区分（见类型注释）。</summary>
    public const string BAD_CREDENTIALS = "BAD_CREDENTIALS";

    /// <summary>账户处于锁定状态（且锁定尚未到期）。实参：剩余分钟数。</summary>
    public const string LOCKED = "LOCKED";

    /// <summary>**本次**密码错误把失败次数顶到上限、账户刚被锁定。实参：锁定时长分钟数。</summary>
    public const string LOCKED_NOW = "LOCKED_NOW";

    /// <summary>账户已被禁用（<c>UserProfile.IsEnabled == false</c>，管理员可恢复）。</summary>
    public const string DISABLED = "DISABLED";

    /// <summary>账户已过期（<c>UserProfile.ExpiresAt</c> 早于当前时间）。</summary>
    public const string EXPIRED = "EXPIRED";
}

/// <summary>
/// 一条登录失败：<b>码 + 中文回落原文 + 实参</b>（三段式，与 <c>RecipeCloneIssue</c> 同形）。
///
/// <para><see cref="Message"/> 是**字典缺键时才用**的回落，读者是真人、必须是完整可用的中文
/// （它也因此必然含数字：<c>账户已锁定，请 N 分钟后再试</c>）。占位符个数与
/// <see cref="Args"/> 个数的对应由判据钉住 —— <c>LocalizationService.Format</c> 在两者
/// 不符时**不抛异常**、只原样返回，用户会看到字面的 <c>{0}</c>。</para>
///
/// <para><b>工厂方法而不是公开构造</b>：码与实参的个数必须**绑死**（<c>LOCKED</c> 恒 1 个实参、
/// <c>DISABLED</c> 恒 0 个）。让调用方手写 <c>new LoginIssue("LOCKED", msg, null)</c>
/// 会在编译期通过、运行期显示 <c>{0}</c> —— 那正是判据要拦的形状。</para>
/// </summary>
public sealed record LoginIssue(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>用户名不存在**或**密码错误（合并档，无实参）。</summary>
    public static LoginIssue BadCredentials() => new(LoginIssueCodes.BAD_CREDENTIALS,
        "用户名或密码错误", null);

    /// <summary>账户锁定中，<paramref name="remainingMinutes"/> 分钟后可再试。</summary>
    public static LoginIssue Locked(int remainingMinutes) => new(LoginIssueCodes.LOCKED,
        $"账户已锁定，请 {remainingMinutes} 分钟后再试", new object[] { remainingMinutes });

    /// <summary>本次失败触发锁定，锁定时长 <paramref name="lockoutMinutes"/> 分钟。</summary>
    public static LoginIssue LockedNow(int lockoutMinutes) => new(LoginIssueCodes.LOCKED_NOW,
        $"密码错误次数已达上限，账户已被锁定 {lockoutMinutes} 分钟", new object[] { lockoutMinutes });

    /// <summary>账户被禁用。</summary>
    public static LoginIssue Disabled() => new(LoginIssueCodes.DISABLED,
        "账户已被禁用，请联系管理员", null);

    /// <summary>账户已过期。</summary>
    public static LoginIssue Expired() => new(LoginIssueCodes.EXPIRED,
        "账户已过期，请联系管理员", null);
}

/// <summary>
/// 登录结果：<b>会话 id 与失败原因恰好一个非空</b>（两工厂保证）。
///
/// <para><b>为什么要换成返回类型而不是多一个 <c>out</c> 参数</b>：<c>out</c> 参数
/// **可以被忽略**（<c>auth.Login(u, p);</c> 照样编译通过），于是"忘填原因"这条老路
/// 换个写法又活了。换成返回类型之后，**编译器会当场点名每一个拿返回值当字符串用的
/// 消费点** —— 这在本项目是既定的取证手段（阶段 98 对 <c>CloneRecipe</c> 做过同一件事）。
/// 改动前产品侧只有 1 个消费点（<c>LoginDialog</c>），另 57 处是测试。</para>
/// </summary>
public sealed record LoginResult(string? SessionId, LoginIssue? Issue)
{
    /// <summary>登录成功。</summary>
    public static LoginResult Ok(string sessionId) => new(sessionId, null);

    /// <summary>登录失败（带码原因）。</summary>
    public static LoginResult Failed(LoginIssue issue) => new(null, issue);
}
