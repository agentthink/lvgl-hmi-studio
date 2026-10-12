using HmiEditor.Core.Models;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 「登录屏上的用户名框 / 密码框」的识别约定 —— 生成期唯一来源。
///
/// <para><b>为什么需要一条约定：</b>设备端 <c>hmi_login()</c> 是**无参**的（历史语义 = "跳去登录屏"），
/// 拿不到登录屏输入框里的文字。要让「登录」按钮真的把凭据交出去，生成器必须在**生成期**
/// 认出哪两个控件是凭据输入框，才能发射
/// <c>hmi_login_submit(lv_textarea_get_text(obj_user), lv_textarea_get_text(obj_pass))</c>。</para>
///
/// <para><b>判据优先级：</b>密码框先看 <see cref="HmiControl.PasswordMode"/> —— 那是编辑器里
/// 显式勾出来的属性，比名字可靠；勾了密码模式的文本框就是密码框，不再看名字。
/// 一个都没勾时才退回按名字候选匹配。用户名框只在**排除掉已选中的密码框之后**的文本框里按名字找。</para>
///
/// <para><b>名字匹配忽略大小写</b>：与 <see cref="ScreenNavTargets"/> 的屏名候选（精确 <c>strcmp</c>）
/// 不同 —— 屏名是设备端**运行时**模糊匹配，歧义会变成"挑错屏"；控件名是生成期解析并发射**确定**调用，
/// 宽容一点不会产生运行期歧义，而"用户起名 UserBox 还是 userbox"不该让凭据提交静默失效。</para>
///
/// <para><b>认不出来不是错误：</b>调用方（<c>CodeGenerator.AppendLoginAction</c>）会退回
/// <c>hmi_login()</c> 的旧语义，产物与历史一致。所以这里只做"能认则认"，不做校验也不抛错。</para>
/// </summary>
public static class LoginFieldTargets
{
    /// <summary>用户名输入框的候选控件名（忽略大小写）。</summary>
    public static readonly IReadOnlyList<string> Username =
        ["user", "username", "userbox", "user_name", "用户名", "账号"];

    /// <summary>密码输入框的候选控件名（忽略大小写）；<c>PasswordMode</c> 优先于此表。</summary>
    public static readonly IReadOnlyList<string> Password =
        ["pass", "password", "passbox", "pass_word", "密码", "口令"];

    /// <summary>
    /// 找密码框：优先 <see cref="HmiControl.PasswordMode"/>，其次名字候选。找不到返回 <c>null</c>。
    /// </summary>
    public static HmiControl? FindPasswordField(IReadOnlyList<HmiControl> controls)
    {
        var textAreas = controls.Where(c => c.Type == HmiControlType.TextArea).ToList();
        return textAreas.FirstOrDefault(c => c.PasswordMode)
            ?? textAreas.FirstOrDefault(c => Matches(c.Name, Password));
    }

    /// <summary>
    /// 找用户名框：只在**排除 <paramref name="passwordField"/>** 之后的文本框里按名字候选找。
    /// 排除是必须的 —— 否则一个既勾了密码模式、名字又恰好命中用户名的框会被同时当作两个字段。
    /// </summary>
    public static HmiControl? FindUsernameField(IReadOnlyList<HmiControl> controls, HmiControl? passwordField)
    {
        return controls
            .Where(c => c.Type == HmiControlType.TextArea && !ReferenceEquals(c, passwordField))
            .FirstOrDefault(c => Matches(c.Name, Username));
    }

    /// <summary>控件名是否命中候选表（忽略大小写；不 trim —— 名字里的空格是有意义的）。</summary>
    public static bool Matches(string? controlName, IReadOnlyList<string> candidates) =>
        !string.IsNullOrEmpty(controlName)
        && candidates.Any(c => string.Equals(c, controlName, StringComparison.OrdinalIgnoreCase));
}
