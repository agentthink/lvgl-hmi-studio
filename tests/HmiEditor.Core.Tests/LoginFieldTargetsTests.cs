using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 登录屏字段识别（<see cref="LoginFieldTargets"/>）的行为测试。
///
/// <para>守三条判据，每条对应一种「生成得出来但一定用不了」的产物形态 ——
/// 它们的共同点是：**C 编译全绿、单元测试若不专门盯就全绿，只有到设备上才发现登不上**。</para>
/// <list type="number">
///   <item>把非 TextArea 当输入框 ⇒ <c>lv_textarea_get_text</c> 收到按钮/标签，运行期行为未定义。</item>
///   <item>用户名与密码解析成**同一个控件** ⇒ 发射 <c>hmi_login_submit(get_text(x), get_text(x))</c>，
///         口令永远等于用户名，永远校验失败且看不出原因。</item>
///   <item>多个候选框时选谁不确定 ⇒ 同一份工程每次导出得到不同代码（产物不可复现）。</item>
/// </list>
/// </summary>
public class LoginFieldTargetsTests
{
    // ── ① 只认 TextArea ──

    [Fact]
    public void FindPasswordField_IgnoresNonTextAreaControls_EvenIfNameLooksLikePassword()
    {
        var controls = new[] { Btn("b1", "pass"), Ta("t1", "note", passwordMode: true) };

        Assert.Equal("t1", LoginFieldTargets.FindPasswordField(controls)!.Id);
    }

    [Fact]
    public void FindPasswordField_ReturnsNull_WhenOnlyNonTextAreaMatches()
    {
        var controls = new[] { Btn("b1", "pass"), Ta("t1", "note") };

        Assert.Null(LoginFieldTargets.FindPasswordField(controls));
    }

    // ── ② 用户名框绝不等于密码框 ──

    [Fact]
    public void FindUsernameField_NeverReturnsThePasswordField()
    {
        // t1 勾了密码模式（⇒ 密码框），但名字 "user" 同时命中了用户名候选。
        // 它既已被选作密码框，就不能再当用户框 —— 否则两个实参是同一个框。
        var t1 = Ta("t1", "user", passwordMode: true);
        var t2 = Ta("t2", "userbox");
        var controls = new[] { t1, t2 };

        var password = LoginFieldTargets.FindPasswordField(controls);
        var username = LoginFieldTargets.FindUsernameField(controls, password);

        Assert.Equal("t1", password!.Id);
        Assert.Equal("t2", username!.Id);
    }

    [Fact]
    public void FindUsernameField_ReturnsNull_WhenTheOnlyUserLikeBoxIsThePasswordField()
    {
        var only = Ta("t1", "user", passwordMode: true);

        var password = LoginFieldTargets.FindPasswordField(new[] { only });
        var username = LoginFieldTargets.FindUsernameField(new[] { only }, password);

        Assert.Equal("t1", password!.Id);
        Assert.Null(username);   // 宁可退回旧语义，也不把同一个框用两次
    }

    // ── ③ 顺序由输入顺序决定（产物可复现）──

    [Fact]
    public void FindPasswordField_PicksTheFirstCandidate_InDeclarationOrder()
    {
        // 两个框都勾了密码模式（如"密码"+"确认密码"）⇒ 取声明在前的那个。
        // 判据必须是**输入顺序**，不能依赖哈希/字典枚举 —— 那会让每次导出得到不同代码。
        var controls = new[] { Ta("a1", "note", passwordMode: true), Ta("b1", "note2", passwordMode: true) };

        Assert.Equal("a1", LoginFieldTargets.FindPasswordField(controls)!.Id);
    }

    [Fact]
    public void FindUsernameField_PicksTheFirstCandidate_InDeclarationOrder()
    {
        var controls = new[] { Ta("u2", "userbox"), Ta("u1", "user") };

        Assert.Equal("u2", LoginFieldTargets.FindUsernameField(controls, null)!.Id);
    }

    // ── 名字匹配的边界（候选表本身 + 严宽）──

    [Theory]
    [InlineData("user")]
    [InlineData("User")]
    [InlineData("USER")]
    [InlineData("username")]
    [InlineData("userBox")]
    [InlineData("用户名")]
    [InlineData("账号")]
    public void Matches_AcceptsEveryUsernameCandidate_CaseInsensitively(string name)
        => Assert.True(LoginFieldTargets.Matches(name, LoginFieldTargets.Username));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("user ")]       // 前后空格**不**宽容：名字是标识符，多打了空格是输入问题
    [InlineData(" user")]
    [InlineData("userbox2")]
    [InlineData("StartBtn")]
    public void Matches_RejectsAnythingElse(string? name)
        => Assert.False(LoginFieldTargets.Matches(name, LoginFieldTargets.Username));

    /// <summary>候选表自身不得为空、不得含空串 —— 否则 <see cref="LoginFieldTargets.Matches"/> 会命中一切。</summary>
    [Fact]
    public void CandidateTables_AreNonEmptyAndFreeOfBlanks()
    {
        foreach (var table in new[] { LoginFieldTargets.Username, LoginFieldTargets.Password })
        {
            Assert.NotEmpty(table);
            Assert.DoesNotContain(table, string.IsNullOrWhiteSpace);
        }
    }

    // ── 辅助 ──

    private static HmiControl Ta(string id, string name, bool passwordMode = false)
        => new() { Id = id, Name = name, Type = HmiControlType.TextArea, PasswordMode = passwordMode };

    private static HmiControl Btn(string id, string name)
        => new() { Id = id, Name = name, Type = HmiControlType.Button };
}
