namespace HmiEditor.Core.Services;

/// <summary>
/// **用户管理**失败诊断码 —— 单一来源，值**不带**键前缀（前缀由 App 侧
/// <c>ExternalText.UserIssueKey</c> 拼成 <c>user.issue.</c>）。
///
/// <para><b>为什么单开一族</b>（登记 <b>#61d</b>，阶段 97）：<c>create.user.failed</c> 那一站
/// 显示的是 <c>创建用户失败: {0}</c>，而 <c>{0}</c> 原来填的是 <c>ex.Message</c> ——
/// <c>AuthenticationService.CreateUser</c> / <c>SetUserPassword</c> 抛的
/// <c>ArgumentException</c> 里有**两档是本仓库自建的**（角色未注册 / 密码长度不足），
/// 它们是**用户真能撞到的运行条件**、语义明确 ⇒ 该给码。码表按**产出方**分家是本仓库
/// 既定约定（<c>package.issue.</c> / <c>system.issue.</c> / <c>svg.issue.</c> /
/// <c>csv.issue.</c> / <c>gen.issue.</c> / <c>expr.syntax.</c>），用户管理这一族此前没有，
/// 现在立 <c>user.issue.</c>。</para>
///
/// <para><b>⚠ 与 <c>SystemFailureCodes</c> 的边界（别把这一族当收容所）</b>：那里**刻意不码化**
/// <c>ArgumentException</c>，理由是"它是调用方的编程错误、且码化它会连带吞掉
/// <c>ArgumentNullException</c> / <c>ArgumentOutOfRangeException</c> 家族"。本条**不违反**那条边界 ——
/// 分界线是「**谁造成的**」：</para>
/// <list type="bullet">
///   <item><c>ArgumentException.ThrowIfNullOrWhiteSpace(username/password)</c>（<c>CreateUser</c> 155/156、
///     <c>SetUserPassword</c> 235）⇒ **调用方编程错误**（界面已前置校验）⇒ **保持原样**，
///     仍回落 BCL 原文；</item>
///   <item>角色未注册 / 密码长度不足 ⇒ **用户可撞到的运行条件**（配置写错、用户密码太短）
///     ⇒ 换成 <see cref="UserAdminException"/>，带码。</item>
/// </list>
/// </summary>
public static class UserAdminIssueCodes
{
    /// <summary>要建的用户角色在 <c>AuthenticationService</c> 里没有注册。回落原文含角色 Id 与修法。</summary>
    public const string ROLE_NOT_REGISTERED = "ROLE_NOT_REGISTERED";

    /// <summary>密码短于 <c>SecurityConfig.PasswordMinLength</c>。回落原文含"最小要求"与"当前"两个实参。</summary>
    public const string PASSWORD_TOO_SHORT = "PASSWORD_TOO_SHORT";
}

/// <summary>
/// 一条用户管理失败 —— 「**码 + 中文回落原文**（+ 可选实参）」三段式，与
/// <see cref="SvgParseError"/> / <c>CsvImportIssue</c> / <c>ScriptError</c> 同形。
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它同时是**排障信息的载体** ——
/// 例如 <see cref="UserAdminIssueCodes.ROLE_NOT_REGISTERED"/> 那一档带着"请先 RegisterRole
/// 或修正 SecurityConfig.DefaultRoleId"，界面上（走字典时）不显示它，但回落路径与日志里必须还在。
/// ⇒ 回落原文**保持原措辞**（作者口吻、含修法提示），**改口吻只改字典值**。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="UserAdminIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record UserAdminIssue(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>
    /// 角色未注册。<c>roleId</c> 走实参 ⇒ 字典文案里能带角色名，
    /// 而回落原文里那句"请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId"只留给排障。
    /// </summary>
    public static UserAdminIssue RoleNotRegistered(string roleId)
        => new(UserAdminIssueCodes.ROLE_NOT_REGISTERED,
            $"角色不存在: {roleId}（请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId）",
            new object[] { roleId });

    /// <summary>
    /// 密码长度不足。<paramref name="min"/> / <paramref name="actual"/> 是**两个**实参，
    /// 单位都是 <b>UTF-8 字节</b>（与 <c>SecurityConfig.PasswordMinLength</c> 的语义一致；
    /// 阶段 168 之前这里写「个字符」而实参是字节，对 CJK 用户是**说谎**：输入 1 个汉字却被告知"当前 3"）。
    /// </summary>
    public static UserAdminIssue PasswordTooShort(int min, int actual)
        => new(UserAdminIssueCodes.PASSWORD_TOO_SHORT,
            $"密码长度不足，最小要求 {min} 个字节（当前 {actual} 个字节，一个汉字算 3 个）",
            new object[] { min, actual });
}
