using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 动作回调代码生成单元测试 —— 验证 6 类动作（SwitchScreen/ExecuteScript/
/// ShowAlarms/ShowTrends/Login/Logout）从 TODO 注释升级为真实运行时 API 调用。
/// </summary>
public class ActionCallbacksCodeGenTests
{
    // ── SwitchScreen ──

    [Fact]
    public void SwitchScreen_EmitsHmiScreenSwitch()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SwitchScreen("Main"));

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_screen_switch(\"Main\");", code);
        Assert.DoesNotContain("TODO: lv_screen_load", code);
    }

    [Fact]
    public void SwitchScreen_DanglingTarget_KeepsTodo()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SwitchScreen("NonExistent"));

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("not in screen list", code);
        Assert.DoesNotContain("hmi_screen_switch(\"NonExistent\")", code);
    }

    // ── ExecuteScript ──

    [Fact]
    public void ExecuteScript_EmitsHmiRunScript()
    {
        var project = CreateProject();
        project.Scripts.Add(new HmiScript { Id = "abc12345", Name = "启动脚本" });
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ExecuteScript("abc12345"));

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_run_script(\"abc12345\");", code);
        Assert.DoesNotContain("TODO: Call script", code);
    }

    [Fact]
    public void ExecuteScript_DanglingTarget_KeepsTodo()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ExecuteScript("nonexistent"));

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("not in script list", code);
        Assert.DoesNotContain("hmi_run_script(\"nonexistent\")", code);
    }

    // ── 系统动作 ──

    [Fact]
    public void ShowAlarms_EmitsHmiShowAlarms()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ShowAlarms);

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_show_alarms();", code);
        Assert.DoesNotContain("TODO: Load alarm screen", code);
    }

    [Fact]
    public void ShowTrends_EmitsHmiShowTrends()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ShowTrends);

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_show_trends();", code);
        Assert.DoesNotContain("TODO: Load trend screen", code);
    }

    [Fact]
    public void Login_EmitsHmiLogin()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_login();", code);
        Assert.DoesNotContain("TODO: Show login dialog", code);
        // 工程里没有登录屏 ⇒ 不可能发射凭据提交（证明这条调用是**条件**发射的）
        Assert.DoesNotContain("lv_textarea_get_text", code);
    }

    // ── Login：两种语义的分流（提交凭据 / 导航到登录屏）──

    /// <summary>
    /// 登录屏上认得出用户名框与密码框 ⇒ 发射**凭据提交**，而不是"切到登录屏"。
    ///
    /// <para><b>为什么这条必须有：</b>此前两种语义混在一个动作里 —— <c>hmi_login()</c> 第一步
    /// 就是切登录屏，于是"在登录屏上按登录按钮"等于自己切自己，两个输入框从来没被读过
    /// （生成代码里一次都没出现过 <c>lv_textarea_get_text</c>）。回归表现是：
    /// 登录屏填什么都能进 —— 因为压根没校验。</para>
    ///
    /// <para><b>参数顺序也是契约：</b>反了的话 C 编译毫无提示，运行期却永远"口令错"
    /// （用户名被当成口令去哈希）。所以顺序自检单独留一条。</para>
    /// </summary>
    [Fact]
    public void Login_OnLoginScreenWithBothFields_SubmitsCredentials()
    {
        var project = CreateProjectWithLoginScreen("login", UserBox(), PassBox());
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");

        // 顺序自检（不依赖 AssertCall 的实参比较实现）：用户名取值必须排在密码取值之前
        var submit = code.IndexOf("hmi_login_submit(", StringComparison.Ordinal);
        var user = code.IndexOf("lv_textarea_get_text(obj_u1)", StringComparison.Ordinal);
        var pass = code.IndexOf("lv_textarea_get_text(obj_p1)", StringComparison.Ordinal);
        Assert.True(user > submit && pass > user,
            $"凭据提交的实参顺序必须是 (用户名, 密码)；实测 submit@{submit} user@{user} pass@{pass}");
    }

    /// <summary>按钮挂在**别的**屏上 ⇒ 仍是导航语义（切到登录屏去让人输凭据）。</summary>
    [Fact]
    public void Login_OnNonLoginScreen_NavigatesInsteadOfSubmitting()
    {
        var project = CreateProjectWithLoginScreen("login", UserBox(), PassBox());
        // CreateProject 的主屏（Main）上的按钮 —— 主屏不是登录屏
        project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button).Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code, "hmi_login()");
        CSource.AssertNoCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");
    }

    /// <summary>
    /// 登录屏上没有任何输入框 ⇒ 退回导航语义（**不报错**，产物与从前一致）。
    /// 登录屏是用户自己画的，形状不合约定不该让整个工程编译不过。
    /// </summary>
    [Fact]
    public void Login_OnLoginScreenWithoutTextAreas_FallsBackToNavigation()
    {
        var project = CreateProjectWithLoginScreen("login");
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code, "hmi_login()");
        Assert.DoesNotContain("lv_textarea_get_text", code);
    }

    /// <summary>
    /// 只找到密码框、找不到用户名框 ⇒ 同样退回导航语义：**不硬猜**哪个框是账号。
    /// 硬猜会把屏上任意一个无关输入框当成用户名 —— 那比"没有凭据校验"更糟：
    /// 用户以为自己配好了，实际永远登不上，且看不出原因。
    /// </summary>
    [Fact]
    public void Login_LoginScreenWithOnlyPasswordField_DoesNotGuessUsername()
    {
        var project = CreateProjectWithLoginScreen("login", PassBox());
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code, "hmi_login()");
        Assert.DoesNotContain("lv_textarea_get_text", code);
    }

    /// <summary>密码框靠 <c>PasswordMode</c> 属性识别 —— 名字起得再不规范也认得出。</summary>
    [Fact]
    public void Login_PasswordFieldRecognizedByPasswordMode_NotOnlyByName()
    {
        var project = CreateProjectWithLoginScreen("login", UserBox(), PassBox(name: "secret2"));
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");
    }

    /// <summary>
    /// <c>PasswordMode</c> 与名字约定冲突时**属性优先**：属性是用户在界面上显式勾的，
    /// 名字只是约定 —— 显式声明比命名惯例可信。
    /// </summary>
    [Fact]
    public void Login_PasswordModeWinsOverNameConvention()
    {
        // a1 名字像密码框但没勾密码模式；p1 勾了但名字无关 ⇒ 应选 p1
        var project = CreateProjectWithLoginScreen("login",
            UserBox(), PassBox(id: "a1", name: "pass", passwordMode: false), PassBox(name: "note"));
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");
    }

    /// <summary>用户名框的名字匹配忽略大小写（<c>UserName</c> 也算命中）。</summary>
    [Fact]
    public void Login_UsernameMatchIsCaseInsensitive()
    {
        var project = CreateProjectWithLoginScreen("login", UserBox(name: "UserName"), PassBox());
        LoginButton(project, "login").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");
    }

    /// <summary>
    /// 「当前屏是不是登录屏」必须走 <see cref="ScreenNavTargets.Login"/> 那份候选表 ——
    /// 否则会长出"<c>hmi_login</c> 认为登录屏是 A、发射侧认为当前屏才是登录屏"的分叉。
    /// 中文屏名一样要命中。
    /// </summary>
    [Fact]
    public void Login_LoginScreenNameMatchedThroughCandidateTable()
    {
        var project = CreateProjectWithLoginScreen("登录屏", UserBox(), PassBox());
        LoginButton(project, "登录屏").Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code,
            "hmi_login_submit(lv_textarea_get_text(obj_u1), lv_textarea_get_text(obj_p1))");
    }

    [Fact]
    public void Logout_EmitsHmiLogout()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.Logout);

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("hmi_logout();", code);
        Assert.DoesNotContain("TODO: Clear user session", code);
    }

    // ── 脚本分发表 ──

    [Fact]
    public void ScriptDispatchTable_GeneratesEntries()
    {
        var project = CreateProject();
        project.Scripts.Add(new HmiScript { Id = "abc12345", Name = "启动脚本" });
        project.Scripts.Add(new HmiScript { Id = "def67890", Name = "停止脚本" });

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("Script Dispatch Table", code);
        Assert.Contains("HMI_SCRIPTS[]", code);
        Assert.Contains("HMI_SCRIPT_COUNT", code);
        Assert.Contains("int hmi_run_script(const char* script_id)", code);
        Assert.Contains("HMI_WEAK void script_abc12345(void)", code);
        Assert.Contains("HMI_WEAK void script_def67890(void)", code);
        Assert.Contains("\"abc12345\", script_abc12345", code);
        Assert.Contains("\"def67890\", script_def67890", code);
    }

    [Fact]
    public void ScriptDispatchTable_Empty_StillDefinesHmiRunScript()
    {
        var project = CreateProject();

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("int hmi_run_script(const char* script_id)", code);
        Assert.Contains("return -1;", code);
        Assert.DoesNotContain("HMI_SCRIPTS[]", code);
    }

    // ── 多动作事件注册一致性 ──

    /// <summary>
    /// 单控件挂多个动作时，回调函数定义名（带 _<ActionType> 后缀）必须与
    /// lv_obj_add_event_cb 注册名一致，否则生成代码链接失败（未定义符号）。
    /// </summary>
    [Fact]
    public void MultiAction_EventRegistrationMatchesCallbackDefinition()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ShowAlarms);
        btn.Actions.Add(HmiAction.Login);

        var code = new CodeGenerator().GenerateFullProject(project);

        // 定义带后缀
        CSource.AssertCall(code, "on_click_obj_c2_ShowAlarms(lv_event_t* e)");
        CSource.AssertCall(code, "on_click_obj_c2_Login(lv_event_t* e)");
        // 注册也带后缀（与定义一致）
        CSource.AssertCall(code, "lv_obj_add_event_cb(obj_c2, on_click_obj_c2_ShowAlarms, LV_EVENT_CLICKED, NULL)");
        CSource.AssertCall(code, "lv_obj_add_event_cb(obj_c2, on_click_obj_c2_Login, LV_EVENT_CLICKED, NULL)");
        // 不应出现无后缀的悬空注册名
        Assert.DoesNotContain("on_click_obj_c2, LV_EVENT_CLICKED", code);
    }

    // ── 动作值 → C 浮点字面量 ──

    /// <summary>
    /// 穷举扫描：生成代码里**不得**出现「数字紧邻 <c>f</c> 后缀、中间没有小数点」的浮点常量
    /// （<c>1f</c> / <c>100f</c> 在 C 里都非法：<c>f</c> 后缀要求字面量本身带小数点或指数）。
    ///
    /// <para><b>为什么用扫描而不是逐个动作钉期望：</b>这个缺陷的形态是"某处又拼了个字面量"，
    /// 逐个动作写期望值等于把清单抄进测试 —— 新写法不会自动长出来。扫描对**任何**发射点都成立。</para>
    ///
    /// <para><b>🩸 输入口径（2026-10-01，阶段 240）</b>：扫的是 <see cref="CSource.WithoutTrivia(string)"/>
    /// 之后的代码。生成物头部那行**追溯戳**（`HmiEditor.CodeGen &lt;版本&gt; (git &lt;短哈希&gt;)`）是注释，
    /// 但短哈希末段恰好是「数字 + f」形状时（实测 `580599f`）会被旧写法读成非法字面量 ⇒
    /// **判据按提交哈希 flaky**。同一口径：注释不算代码。</para>
    /// </summary>
    [Fact]
    public void GeneratedCode_NeverEmitsFloatLiteralWithoutDecimalPoint()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        // 语料要覆盖会发射字面量的各类动作，否则扫了个寂寞（下方有非空自检）
        foreach (var v in new[] { "1", "-2", "100", "0" })
            btn.Actions.Add(HmiAction.SetVariable("v_motor", v));
        btn.Actions.Add(HmiAction.SetBit("v_motor"));
        btn.Actions.Add(HmiAction.ResetBit("v_motor"));
        btn.Actions.Add(HmiAction.ToggleBit("v_motor"));
        btn.Actions.Add(HmiAction.MomentaryOn("v_motor"));
        btn.Actions.Add(HmiAction.MomentaryOff("v_motor"));

        var code = new CodeGenerator().GenerateFullProject(project);

        // 🩸 扫描 **去 trivia** 后的代码（2026-10-01，阶段 240）：生成物头部那行追溯戳
        // `HmiEditor.CodeGen <版本> (git <短哈希>)` 是**注释**，但当哈希末段恰好是「数字 + f」
        // （实测 `580599f`）时，下面那条正则把它读成非法浮点常量 ⇒ 判据**按提交哈希 flaky**。
        // 与 `CSource.ParseCalls` 同一口径：注释不算代码。
        var scan = CSource.WithoutTrivia(code);

        // 判据自检：合法的「数字.数字f」必须扫得到，否则说明语料/正则失效
        var legal = System.Text.RegularExpressions.Regex.Matches(scan, @"\d+\.\d+f").Count;
        Assert.True(legal >= 8, $"只扫到 {legal} 个合法浮点字面量 —— 语料或正则失效");

        var bad = System.Text.RegularExpressions.Regex
            .Matches(scan, @"(?<![\w.])\d+f(?![\w.])")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        Assert.True(bad.Count == 0,
            "生成代码里出现了非法 C 浮点常量（整数必须补小数点，如 1.0f）：" + string.Join(", ", bad));
    }

    /// <summary>
    /// 「设置变量」的值必须按 C 语法落成浮点字面量。回归：
    /// 格式串 <c>"0.########"</c> 对整数值产出 <c>1</c>，拼上 <c>f</c> 就是 <c>1f</c> ——
    /// 任何带该动作的工程都会在**编译期**失败（「整数常量的 'f' 后缀无效」），而单元测试全绿。
    /// </summary>
    [Theory]
    [InlineData("1", "1.0f")]
    [InlineData("0", "0.0f")]
    [InlineData("-2", "-2.0f")]
    [InlineData("100", "100.0f")]
    [InlineData("1.5", "1.5f")]
    [InlineData("-0.25", "-0.25f")]
    [InlineData("true", "1.0f")]
    [InlineData("false", "0.0f")]
    public void SetVariable_Value_IsAlwaysAValidCFloatLiteral(string value, string expected)
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SetVariable("v_motor", value));

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code, $"set_tag_value(TAG_v_motor, {expected})");
    }

    /// <summary>
    /// 非有限值必须挡在门外：<c>double.TryParse</c> 认 <c>NaN</c>/<c>Infinity</c>，
    /// 直接格式化会产出 <c>NaNf</c>（同样不是合法 C 常量）。
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    public void SetVariable_NonFiniteValue_FallsBackToZero(string value)
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SetVariable("v_motor", value));

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCall(code, "set_tag_value(TAG_v_motor, 0.0f)");
        // 注释里回显用户原文是允许的（且已 EscapeComment）；要挡的是**字面量**变成 NaNf/Infinityf
        Assert.DoesNotContain("NaNf", code);
        Assert.DoesNotContain("Infinityf", code);
    }

    // ── 辅助 ──

    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        var conn = new DeviceConnection { Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp" };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_motor", Name = "motor", Address = "40002",
            DataType = "Bool", ConnectionId = "conn1"
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c2", Name = "StartBtn", Type = HmiControlType.Button,
            VariableBindingId = "v_motor", BindingMode = "ReadWrite"
        });
        project.Screens.Add(screen);
        return project;
    }

    /// <summary>
    /// 在 <see cref="CreateProject"/> 之上再加一块登录屏（屏名可指定，用于验证候选表命中）。
    /// 屏上始终放一个按钮（<c>lbtn</c>）供挂 Login 动作；输入框由调用方按用例给。
    /// </summary>
    private static HmiProject CreateProjectWithLoginScreen(string loginScreenName, params HmiControl[] loginControls)
    {
        var project = CreateProject();

        var login = new HmiScreen { Id = "s2", Name = loginScreenName, Width = 800, Height = 480 };
        foreach (var c in loginControls) login.Controls.Add(c);
        login.Controls.Add(new HmiControl { Id = "lbtn", Name = "LoginBtn", Type = HmiControlType.Button });
        project.Screens.Add(login);
        return project;
    }

    private static HmiControl LoginButton(HmiProject project, string screenName)
        => project.Screens.First(s => s.Name == screenName)
            .Controls.First(c => c.Type == HmiControlType.Button);

    /// <summary>用户名输入框：id 固定 <c>u1</c> ⇒ 生成符号 <c>obj_u1</c>（断言里直接用）。</summary>
    private static HmiControl UserBox(string id = "u1", string name = "user")
        => new() { Id = id, Name = name, Type = HmiControlType.TextArea };

    /// <summary>密码输入框：id 固定 <c>p1</c> ⇒ 生成符号 <c>obj_p1</c>。默认勾密码模式。</summary>
    private static HmiControl PassBox(string id = "p1", string name = "pass", bool passwordMode = true)
        => new() { Id = id, Name = name, Type = HmiControlType.TextArea, PasswordMode = passwordMode };
}
