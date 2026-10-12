using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 运行时用户表的编辑器侧校验 —— 四条规则，**每条都要有正反例**。
///
/// <para><b>为什么断言枚举而不是文案：</b>文案会改，规则不该跟着红。枚举增删/改名时，
/// 这里与 App 层的映射会被编译期一起拦住。</para>
///
/// <para><b>为什么专门覆盖大小写与"同义写法"：</b>固件用 <c>strcmp</c>（Ordinal，大小写敏感），
/// <c>Op</c> 与 <c>op</c> 在设备上是两个不同用户。判据若用 <c>OrdinalIgnoreCase</c>，
/// 就会把"设备上其实互不冲突的两条"误报成重名 —— 判据比现实宽或窄都是错的，两种都要有反例钉住。</para>
///
/// <para><b>为什么"停用且无密码"不算问题：</b>停用账户在设备端直接拒绝，无密码与否不影响可达性。
/// 把它算成问题会让"我特意停用留着以后启用"的账户一直飘着一条红字。</para>
/// </summary>
public class RuntimeUserValidatorTests
{
    /// <summary>内容无关的占位哈希 —— 校验只判"非空"，不解析格式（格式由 <c>RuntimePasswordHasher</c> 负责）。</summary>
    private const string SomeHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    private static RuntimeUser User(
        string username,
        string roleId = "role-operator",
        string passwordHash = SomeHash,
        bool enabled = true) => new()
        {
            Username = username,
            RoleId = roleId,
            PasswordHash = passwordHash,
            IsEnabled = enabled,
        };

    // ══════════════════ 空表 / 合法表 ══════════════════

    [Fact]
    public void EmptyTable_NoIssues()
    {
        Assert.Empty(RuntimeUserValidator.Validate(new List<RuntimeUser>()));
    }

    [Fact]
    public void NullTable_NoIssues_NoThrow()
    {
        // 旧工程反序列化后 RuntimeUsers 可能是 null（属性被显式置空），不该崩在校验上
        Assert.Empty(RuntimeUserValidator.Validate(null));
    }

    [Fact]
    public void HealthyTable_NoIssues()
    {
        var users = new List<RuntimeUser>
        {
            User("op", "role-operator"),
            User("eng", "role-engineer"),
            User("admin", "role-admin"),
            User("retired", "role-operator", enabled: false),
        };

        Assert.Empty(RuntimeUserValidator.Validate(users));
    }

    // ══════════════════ 空用户名 ══════════════════

    [Theory]
    [InlineData("")]
    [InlineData("   ")]      // 纯空白：与空串同义写法
    [InlineData("\t")]
    public void BlankUsername_Reported(string username)
    {
        var issues = RuntimeUserValidator.Validate(new List<RuntimeUser> { User(username) });

        var issue = Assert.Single(issues);
        Assert.Equal(RuntimeUserIssueKind.EmptyUsername, issue.Kind);
    }

    // ══════════════════ 重名 ══════════════════

    [Fact]
    public void DuplicateUsername_OnlyLaterEntryReported()
    {
        // 设备端 hmi_user_lookup 线性查表 + 首个匹配胜出 ⇒ 首条是"生效的那条"，不报；
        // 后面两条改了也毫无效果（最隐蔽的一类"改了没反应"），各报一条。
        var users = new List<RuntimeUser> { User("op"), User("op"), User("op") };

        var issues = RuntimeUserValidator.Validate(users);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(RuntimeUserIssueKind.DuplicateUsername, i.Kind));
        Assert.Equal(new[] { 1, 2 }, issues.Select(i => i.Index).ToArray());
    }

    [Fact]
    public void UsernameCaseDifference_IsNotDuplicate()
    {
        // 反例：strcmp 大小写敏感 ⇒ 这两个在设备上是两个不同用户，不该报重名
        var users = new List<RuntimeUser> { User("op"), User("Op") };

        Assert.Empty(RuntimeUserValidator.Validate(users));
    }

    [Fact]
    public void BlankUsers_DoNotReportDuplicatesAgainstEachOther()
    {
        // 多个空条目若各报一条"重复"，噪音会盖住真问题（空用户名本身已各报一条）
        var users = new List<RuntimeUser> { User(""), User("") };

        var issues = RuntimeUserValidator.Validate(users);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(RuntimeUserIssueKind.EmptyUsername, i.Kind));
    }

    // ══════════════════ 角色表外 ══════════════════

    [Theory]
    [InlineData("role-guest")]
    [InlineData("")]                 // 空角色 Id：hmi_role_lookup 返回 -1
    [InlineData("ROLE-OPERATOR")]    // 反例的另一种形态：大小写不同 ⇒ strcmp 判不等
    public void UnknownRole_Reported(string roleId)
    {
        var issues = RuntimeUserValidator.Validate(new List<RuntimeUser> { User("op", roleId) });

        var issue = Assert.Single(issues);
        Assert.Equal(RuntimeUserIssueKind.UnknownRole, issue.Kind);
    }

    [Theory]
    [InlineData("role-operator")]
    [InlineData("role-engineer")]
    [InlineData("role-admin")]
    public void KnownRoles_NotReported(string roleId)
    {
        Assert.Empty(RuntimeUserValidator.Validate(new List<RuntimeUser> { User("op", roleId) }));
    }

    [Fact]
    public void IsKnownRoleId_PositiveAndNegative()
    {
        // 判据自身要有正反例 —— 否则它退化成恒 false 时上面的 Theory 会以另一种方式掩盖问题
        Assert.True(RuntimeUserValidator.IsKnownRoleId("role-operator"));
        Assert.False(RuntimeUserValidator.IsKnownRoleId("role-guest"));
        Assert.False(RuntimeUserValidator.IsKnownRoleId("ROLE-OPERATOR"));
        Assert.False(RuntimeUserValidator.IsKnownRoleId(null));
        Assert.False(RuntimeUserValidator.IsKnownRoleId(""));
    }

    // ══════════════════ 启用但无密码 ══════════════════

    [Fact]
    public void EnabledWithoutPassword_Reported()
    {
        var issues = RuntimeUserValidator.Validate(
            new List<RuntimeUser> { User("op", passwordHash: "") });

        var issue = Assert.Single(issues);
        Assert.Equal(RuntimeUserIssueKind.EnabledWithoutPassword, issue.Kind);
    }

    [Fact]
    public void DisabledWithoutPassword_NotReported()
    {
        // 停用账户在设备端直接拒绝 ⇒ 无密码不是问题（否则会一直飘一条红字）
        var users = new List<RuntimeUser> { User("op", passwordHash: "", enabled: false) };

        Assert.Empty(RuntimeUserValidator.Validate(users));
    }

    // ══════════════════ 一条条目可同时命中多条规则 ══════════════════

    [Fact]
    public void IssuesCarryIndexAndUsername_AndStackPerEntry()
    {
        var users = new List<RuntimeUser>
        {
            User("op"),                              // 0: 合法
            User("eng", "role-guest", ""),           // 1: 角色表外 + 启用无密码
        };

        var issues = RuntimeUserValidator.Validate(users);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(1, i.Index));
        Assert.All(issues, i => Assert.Equal("eng", i.Username));
        Assert.Contains(issues, i => i.Kind == RuntimeUserIssueKind.UnknownRole);
        Assert.Contains(issues, i => i.Kind == RuntimeUserIssueKind.EnabledWithoutPassword);
    }

    // ══════════════════ 查重辅助（UI 即时反馈用） ══════════════════

    [Fact]
    public void IsUsernameTaken_HonoursExceptIndex()
    {
        var users = new List<RuntimeUser> { User("op"), User("eng") };

        Assert.True(RuntimeUserValidator.IsUsernameTaken(users, "op"));
        Assert.False(RuntimeUserValidator.IsUsernameTaken(users, "op", exceptIndex: 0));  // 正在编辑自己
        Assert.False(RuntimeUserValidator.IsUsernameTaken(users, "other"));
        Assert.False(RuntimeUserValidator.IsUsernameTaken(users, ""));       // 空名不参与查重
        Assert.False(RuntimeUserValidator.IsUsernameTaken(users, null));
        Assert.False(RuntimeUserValidator.IsUsernameTaken(null, "op"));
    }

    [Fact]
    public void CountUsable_CountsOnlyReachableEntries()
    {
        var users = new List<RuntimeUser>
        {
            User("op"),                                  // 可用
            User("eng", "role-engineer"),                // 可用
            User("guest", "role-guest"),                 // 角色表外 ⇒ 进不去
            User("nopass", passwordHash: ""),            // 无密码 ⇒ 进不去
            User("off", enabled: false),                 // 停用 ⇒ 进不去
            User(""),                                    // 空用户名 ⇒ 不计数
        };

        Assert.Equal(2, RuntimeUserValidator.CountUsable(users));
        Assert.Equal(0, RuntimeUserValidator.CountUsable(null));
    }

    [Fact]
    public void NextDefaultUsername_SkipsTaken()
    {
        Assert.Equal("user1", RuntimeUserValidator.NextDefaultUsername(new List<RuntimeUser>()));

        var users = new List<RuntimeUser> { User("user1"), User("user2") };
        Assert.Equal("user3", RuntimeUserValidator.NextDefaultUsername(users));

        // 大小写不同不算占用（与查重同源判据）：USER1 ≠ user1 ⇒ 仍从 user1 起
        Assert.Equal("user1", RuntimeUserValidator.NextDefaultUsername(new List<RuntimeUser> { User("USER1") }));
    }
}
