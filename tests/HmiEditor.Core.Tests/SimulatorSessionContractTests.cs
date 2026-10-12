using System;
using System.IO;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 导出模拟器的**会话超时契约**。
///
/// <para><b>为什么必须有这些守卫：</b>设备端那份实现（<c>firmware/t113/app/runtime/hmi_actions.c</c>）
/// <b>不在导出工程里</b> —— 模拟器的 <c>main.c</c> 要重新实现全部宿主符号
/// （<c>hmi_screen_switch</c> / <c>hmi_login_submit</c> / <c>var_raw_*</c> …，见
/// <see cref="SimulatorLinkContractTests"/> 的类注释）。会话超时同理：少了它，表现是
/// <b>"预览里永远不超时、设备上会"</b> —— 于是这个功能在预览里根本验证不了，用户只能上设备试。
/// 把"两边行为一致"变成编译期能查的事，就是本类的作用。</para>
/// </summary>
public class SimulatorSessionContractTests
{
    private static HmiProject BuildProject()
    {
        var project = new HmiProject { Name = "SimSession", LvglVersion = "9.2" };
        project.SecurityConfig.OperationMode = OperationMode.Operator;
        project.SecurityConfig.SessionTimeoutMin = 20;
        project.SecurityConfig.AutoLogoutEnabled = true;
        // 锁屏屏：**Id 与名字刻意不同** —— 若同名，"拿到 Id 却当名字用"这个 bug 会被静默放过
        project.SecurityConfig.LockScreenId = "screen-lock";
        project.SecurityConfig.RuntimeUsers.Add(new RuntimeUser
        {
            Username = "op",
            DisplayName = "Operator",
            RoleId = "role-operator",
            PasswordHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=",
            IsEnabled = true,
        });

        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "screen-lock", Name = "锁屏页", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "login", Width = 800, Height = 480 });
        return project;
    }

    private static string ExportMainC(HmiProject project)
    {
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("sim-session");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            return File.ReadAllText(Path.Combine(dir, "main.c"));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>截出 <paramref name="signature"/> 那一整个函数体（到顶格的 <c>}</c> 为止）。</summary>
    private static string FunctionBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"生成代码里找不到 {signature}");
        var end = source.IndexOf("\n}", start, StringComparison.Ordinal);
        Assert.True(end > start, $"找不到 {signature} 的函数体结尾");
        return source[start..end];
    }

    /// <summary>
    /// <c>hmi_session_poll</c> 必须被**定义**（不是又一条声明）且在主循环里**被调用**。
    /// 只定义不调用 ⇒ 屏幕永远不会自己跳锁屏，而"看到锁屏"正是操作员判断自己需要重新登录的线索。
    /// </summary>
    [Fact]
    public void MainC_DefinesSessionPoll_AndCallsItFromTheLoop()
    {
        var mainC = ExportMainC(BuildProject());

        Assert.Contains("int hmi_session_poll(void)", mainC);
        Assert.Contains("hmi_session_poll();", mainC);
        // 扫描面自检：主循环确实在我们认为的那个函数里。
        // 用 AssertText 而不是 Assert.Contains —— 这个字面量含 `(` 与 `,`，
        // 正是 TestAssertionStyleGuardTests 禁止的形态（它守的是"别把断言绑在排版上"）。
        CSource.AssertText(mainC, "int main(int argc, char** argv)");
    }

    /// <summary>
    /// 权限门里三行的**先后顺序**（与固件 hmi_has_permission 同构）：超时评估 → 活动打点 → 权限判定。
    ///
    /// <para>两条顺序各自对应一个真实缺陷，别把它们当成一条：
    /// ① <c>评估</c> 挪到 <c>判定</c> 之后 ⇒ 超时那一刻的操作先被放行、再登出，等于把这次操作送出去；
    /// ② <c>打点</c> 挪到 <c>评估</c> 之前 ⇒ <c>touch</c> 先把 deadline 推到 now+阈值，
    /// 紧接着的评估必然算「未超时」⇒ <b>超时永不触发</b>（空闲到超时之后的那次操作会被放行）。
    /// 只守 ① 会漏掉 ②，而 ② 的表现比 ① 更隐蔽：它不是"某一次操作漏过去了"，是"这一整条防线不存在"。</para>
    /// </summary>
    [Fact]
    public void RequirePermission_EvaluatesTimeoutThenTouchesThenDecides()
    {
        var gate = FunctionBody(ExportMainC(BuildProject()), "int hmi_require_permission(unsigned int bits)");

        var expireAt = gate.IndexOf("sim_session_expire()", StringComparison.Ordinal);
        var touchAt = gate.IndexOf("sim_session_touch()", StringComparison.Ordinal);
        var decideAt = gate.IndexOf("hmi_perm_allows(", StringComparison.Ordinal);

        Assert.True(expireAt >= 0, "权限门里没有超时评估 ⇒ 会话永不超时");
        Assert.True(touchAt >= 0, "判定入口没有活动打点 ⇒ 一直在操作也会被踢");
        Assert.True(decideAt >= 0, "权限门里没有判定调用（语料或实现变了）");
        Assert.True(expireAt < decideAt, "超时评估必须在权限判定之前");
        Assert.True(expireAt < touchAt,
            "超时评估必须在活动打点之前 —— 先打点等于每次判定都先续命（deadline 被推到 now+阈值），"
            + "紧接着的评估必然算未超时，超时因此永不触发");
    }

    /// <summary>
    /// 会话超时切的是**生成期解析出的屏名**，不是 <see cref="SecurityConfig.LockScreenId"/> 那个 Id
    /// —— 模拟器的 <c>hmi_screen_switch</c> 与设备端一样按名字匹配。
    /// </summary>
    [Fact]
    public void SessionExpiry_SwitchesToResolvedScreenName_NotTheId()
    {
        var expire = FunctionBody(ExportMainC(BuildProject()), "static int sim_session_expire(void)");

        Assert.Contains("\"锁屏页\"", expire);
        Assert.DoesNotContain("screen-lock", expire);
    }

    /// <summary>登录即活动：两条建立会话的路径都要给会话计时起头，否则"刚登录就被判超时"。</summary>
    [Fact]
    public void BothLoginPaths_StartTheSessionClock()
    {
        var mainC = ExportMainC(BuildProject());

        Assert.Contains("sim_session_touch()", FunctionBody(mainC, "static void sim_preview_login(void)"));
        Assert.Contains("sim_session_touch()", FunctionBody(mainC, "void hmi_login(void)"));
    }

    /// <summary><c>hmi_logout</c> 顺手清计时 —— 否则登出后残留的 deadline 会让下一次会话莫名被踢。</summary>
    [Fact]
    public void Logout_ClearsTheSessionClock()
    {
        var logout = FunctionBody(ExportMainC(BuildProject()), "void hmi_logout(void)");

        Assert.Contains("s_sim_session_deadline_ms = 0u;", logout);
    }

    /// <summary>
    /// 设计模式：符号仍然存在（<c>hmi_session_poll</c> 被主循环调用，链接期必须有定义），
    /// 但 <c>hmi_require_permission</c> 的整个 <c>#if HMI_SEC_ENFORCE</c> 块被编译掉 ⇒ 它不可能生效。
    /// 与"设计模式产物行为不变"这条硬契约一致。
    /// </summary>
    [Fact]
    public void DesignerProject_PollExistsButGateBlockIsCompiledOut()
    {
        var project = BuildProject();
        project.SecurityConfig.OperationMode = OperationMode.Designer;

        var mainC = ExportMainC(project);

        Assert.Contains("int hmi_session_poll(void)", mainC);
        Assert.Contains("#if HMI_SEC_ENFORCE", FunctionBody(mainC, "int hmi_require_permission(unsigned int bits)"));
    }
}
