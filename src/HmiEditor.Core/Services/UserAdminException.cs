namespace HmiEditor.Core.Services;

/// <summary>
/// 「用户管理操作被拒」—— 把 <see cref="UserAdminIssue"/> **包成异常**交给上层
/// （登记 <b>#61d</b>，阶段 97）。
///
/// <para><b>为什么必须有这个类型</b>：<c>UserManagerView</c> 的创建用户站点写的是
/// <c>Format(Lang.UserMgrCreateFailed, ex.Message)</c>，而 <c>ex.Message</c> 来自
/// <c>AuthenticationService</c> 自建的 <c>ArgumentException</c> ⇒ 中文界面下是**作者口吻**
/// （「请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId」），英文界面下**照旧中文**
/// —— <c>ArgumentException</c> 没有码，<c>ExternalText.FailureReason</c> 的四支分叉一支都接不住它，
/// 只能落第 ④ 支回落原文。把裸 <c>ArgumentException</c> 换成带码异常，
/// <b>码这条路才第一次通到界面</b>；否则"把 <c>ex.Message</c> 换成 <c>FailureReason(ex)</c>"
/// 就只是阶段 83 钉死的**改名式假闭环**。</para>
///
/// <para><b>为什么继承 <see cref="Exception"/> 而不是 <c>ArgumentException</c></b>：
/// ① 语义上它不是"调用方编程错误"（那两档是 <c>ThrowIfNullOrWhiteSpace</c>，仍保持
/// <c>ArgumentException</c>，见 <see cref="UserAdminIssueCodes"/> 的边界说明）；
/// ② 继承 <c>ArgumentException</c> 会让 <c>catch (ArgumentException)</c> 无意中继续命中
/// ⇒ 站点"收口了"这件事在**编译面**上看不出来（本项目登记过的失效模式）。
/// ⇒ 换类型的代价是**编译器当场点出全部消费点**，这正是要的：实测 2 个 catch 站点
/// ＋ 5 处 <c>Assert.Throws&lt;ArgumentException&gt;</c>。</para>
///
/// <para><b><see cref="Exception.Message"/> 保留中文回落</b>（= <see cref="UserAdminIssue.Message"/>）：
/// 它是 <c>FailureReason</c> 走不到码化分支时的显示内容，也是任何日志/兜底打印里的可读信息。</para>
/// </summary>
public sealed class UserAdminException : Exception
{
    /// <summary>用一条失败构造。异常消息 = 该失败的中文回落原文。</summary>
    /// <param name="issue">见 <see cref="UserAdminIssue"/>。不可以为 <c>null</c>。</param>
    public UserAdminException(UserAdminIssue issue)
        : base(issue.Message)
        => Issue = issue;

    /// <summary>本异常的**码 + 实参 + 中文回落** —— App 侧渲染的唯一依据。</summary>
    public UserAdminIssue Issue { get; }
}
