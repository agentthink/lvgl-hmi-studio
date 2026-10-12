using System.Security.Cryptography;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class AuthenticationTests
{
    // ── Permission 枚举测试 ──

    [Fact]
    public void Permission_Flags_AreCorrect()
    {
        Assert.Equal(1, (int)Permission.ViewScreen);
        Assert.Equal(2, (int)Permission.OperateControl);
        Assert.Equal(4, (int)Permission.EditRecipe);
        Assert.Equal(8, (int)Permission.AcknowledgeAlarm);
        Assert.Equal(16, (int)Permission.ChangeConfig);
        Assert.Equal(32, (int)Permission.ManageUsers);
    }

    [Fact]
    public void Permission_None_HasZeroValue()
    {
        Assert.Equal(0, (int)Permission.None);
    }

    [Fact]
    public void Permission_All_IncludesAll()
    {
        // ⚠ 这个清单**必须**跟着枚举一起长：阶段 275 加 ShelveAlarm 时它当场红过一次
        // （忘了提 Permission.All 的位宽 ⇒ 管理员"全部权限"里没有搁置）。
        var all = Permission.ViewScreen | Permission.OperateControl | Permission.EditRecipe |
                  Permission.AcknowledgeAlarm | Permission.ChangeConfig | Permission.ManageUsers |
                  Permission.ViewAlarmHistory | Permission.ExportData | Permission.DebugMode |
                  Permission.ShelveAlarm;
        Assert.Equal(Permission.All, all);
    }

    // ── UserRole 模型测试 ──

    [Fact]
    public void BuiltInOperator_HasCorrectPermissions()
    {
        var role = UserRole.BuiltInOperator;
        Assert.Equal("role-operator", role.Id);
        Assert.Equal(3, role.Level);
        Assert.True(role.HasPermission(Permission.ViewScreen));
        Assert.True(role.HasPermission(Permission.OperateControl));
        Assert.False(role.HasPermission(Permission.ManageUsers));
        Assert.False(role.HasPermission(Permission.ChangeConfig));
    }

    [Fact]
    public void BuiltInAdministrator_HasAllPermissions()
    {
        var role = UserRole.BuiltInAdministrator;
        Assert.Equal(15, role.Level);
        Assert.True(role.HasPermission(Permission.ManageUsers));
        Assert.True(role.HasPermission(Permission.DebugMode));
    }

    [Fact]
    public void UserRole_CanCreateCustomRole()
    {
        var role = new UserRole
        {
            Name = "Supervisor",
            Level = 5,
            Permissions = Permission.ViewScreen | Permission.OperateControl | Permission.ViewAlarmHistory
        };
        Assert.Equal(5, role.Level);
        Assert.True(role.HasPermission(Permission.ViewScreen));
        Assert.False(role.HasPermission(Permission.ManageUsers));
    }

    // ── UserProfile 模型测试 ──

    [Fact]
    public void UserProfile_DefaultRole_IsOperator()
    {
        var user = new UserProfile();
        Assert.Equal("role-operator", user.RoleId);
        Assert.True(user.IsEnabled);
        Assert.False(user.IsLocked);
    }

    // ── 密码哈希测试 ──

    [Fact]
    public void HashPassword_ProducesValidFormat()
    {
        var hash = AuthenticationService.HashPassword("myPassword123");
        var parts = hash.Split(':');
        Assert.Equal(2, parts.Length);
        Assert.NotEmpty(parts[0]);
        Assert.NotEmpty(parts[1]);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = AuthenticationService.HashPassword("correct-password");
        Assert.True(AuthenticationService.VerifyPassword("correct-password", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = AuthenticationService.HashPassword("correct-password");
        Assert.False(AuthenticationService.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void VerifyPassword_DifferentSalts_ProducesDifferentHashes()
    {
        var hash1 = AuthenticationService.HashPassword("same-password");
        var hash2 = AuthenticationService.HashPassword("same-password");
        Assert.NotEqual(hash1, hash2);  // each call generates new salt
    }

    [Fact]
    public void HashPassword_EmptyString_ProducesHash()
    {
        var hash = AuthenticationService.HashPassword("");
        Assert.NotNull(hash);
        Assert.Contains(":", hash);
    }

    // ── AuthenticationService 集成测试 ──

    [Fact]
    public void AuthService_Constructor_CreatesDefaultAdmin()
    {
        using var auth = new AuthenticationService();
        var users = auth.GetAllUsers();
        Assert.Contains(users, u => u.Username == "admin" && u.RoleId == "role-admin");
    }

    [Fact]
    public void AuthService_DefaultRoles_Exist()
    {
        using var auth = new AuthenticationService();
        var roles = auth.GetAllRoles();
        Assert.Equal(3, roles.Count);
        Assert.Contains(roles, r => r.Name == "Operator");
        Assert.Contains(roles, r => r.Name == "Engineer");
        Assert.Contains(roles, r => r.Name == "Administrator");
    }

    [Fact]
    public void Login_ValidCredentials_ReturnsSessionId()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);
        Assert.NotEmpty(sessionId);
    }

    [Fact]
    public void Login_WrongPassword_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "wrong-password").SessionId;
        Assert.Null(sessionId);
    }

    [Fact]
    public void Login_NonexistentUser_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("nonexistent", "password").SessionId;
        Assert.Null(sessionId);
    }

    [Fact]
    public void Login_ExcessiveFailures_LocksAccount()
    {
        using var auth = new AuthenticationService();
        auth.MaxLoginAttempts = 3;

        // 3 failed attempts
        for (int i = 0; i < 3; i++)
            auth.Login("admin", "wrong");

        var user = auth.GetUserByUsername("admin");
        Assert.NotNull(user);
        Assert.True(user!.IsLocked);
    }

    [Fact]
    public void Login_WhenLocked_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        auth.MaxLoginAttempts = 2;

        for (int i = 0; i < 2; i++)
            auth.Login("admin", "wrong");

        var sessionId = auth.Login("admin", "admin").SessionId;  // even correct password
        Assert.Null(sessionId);
    }

    [Fact]
    public void Login_DisabledUser_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        auth.SetUserEnabled("user-admin", false);
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.Null(sessionId);
    }

    [Fact]
    public void CheckPermission_Admin_HasManageUsers()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.ManageUsers));
    }

    [Fact]
    public void CheckPermission_Admin_HasAllPermissions()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);

        // 枚举所有权限检查
        foreach (Permission p in Enum.GetValues<Permission>())
        {
            if (p == Permission.None || p == Permission.All) continue;
            Assert.True(auth.CheckPermission(sessionId, p), $"Admin should have {p}");
        }
    }

    [Fact]
    public void CheckPermission_NoSession_ReturnsFalse()
    {
        using var auth = new AuthenticationService();
        Assert.False(auth.CheckPermission(null, Permission.ViewScreen));
    }

    /// <summary>
    /// **判据（阶段 168，登记 `#83`）**：口令下限的**口径是 UTF-8 字节数** —— 与模型的注释、
    /// 编辑器 `ProjectSettingsView.OnSetRuntimeUserPassword`（`GetByteCount`）、设备端 `strlen` 四处一致。
    ///
    /// <para>旧实现用 `plainPassword.Length`（UTF-16 码元）⇒ 同一个口令在「用户管理 → 重置密码」
    /// 与「工程设置 → 运行时用户」两条产品路径上**结论相反**：下限 6 时 `"电机"`（2 字 = 6 字节）
    /// 这里拒绝（2 &lt; 6）、那里接受（6 ≥ 6）。判据就是把这句读出来。</para>
    /// </summary>
    [Fact]
    public void SetUserPassword_MinimumLength_IsCountedInUtf8Bytes()
    {
        using var auth = new AuthenticationService();
        auth.ApplySecurityConfig(new SecurityConfig { PasswordMinLength = 6 });

        const string cjk = "电机";
        // **前提自检**：这个口令确实是"字节够、码元不够"的形状（否则判据空转）
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(cjk) == 6,
            $"前提不成立：'{cjk}' 的 UTF-8 字节数应当 6，实测 {System.Text.Encoding.UTF8.GetByteCount(cjk)}");
        Assert.True(cjk.Length < 6, $"前提不成立：'{cjk}' 的 UTF-16 码元数应当 < 6，实测 {cjk.Length}");

        var user = new UserProfile { Username = "op-cjk", DisplayName = "Op", RoleId = "role-operator" };
        auth.SetUserPassword(user, cjk);      // 旧实现会在这里抛"密码长度不足"
        auth.RegisterUser(user);

        Assert.True(auth.Login("op-cjk", cjk).SessionId is { Length: > 0 },
            "按 UTF-8 字节数达标的 CJK 口令应当能设、也能登 —— 两侧口径不一致时用户会看到"
            + "「工程设置能设、用户管理说太短」");
    }

    /// <summary>
    /// **判据（阶段 168）**：真正的**短**口令仍然被拒，且拒的时候报出来的两个数都是**字节**
    /// （`Args` 里 min / actual）—— 这是"文案说字节"那半边在**产出方**的落点：
    /// 若哪天有人把判定换回码元，`actual` 会立刻变成 2 而不是 4。
    /// </summary>
    [Fact]
    public void SetUserPassword_TooShort_ReportsBothCountsInBytes()
    {
        using var auth = new AuthenticationService();
        auth.ApplySecurityConfig(new SecurityConfig { PasswordMinLength = 6 });

        const string tooShort = "电a";        // 1 个汉字(3) + 1 个 ASCII(1) = 4 字节 / 2 码元
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(tooShort) == 4,
            $"前提不成立：'{tooShort}' 的 UTF-8 字节数应当 4，实测 {System.Text.Encoding.UTF8.GetByteCount(tooShort)}");

        var user = new UserProfile { Username = "op-short", DisplayName = "Op", RoleId = "role-operator" };
        var ex = Assert.Throws<UserAdminException>(() => auth.SetUserPassword(user, tooShort));

        var issue = ex.Issue;
        Assert.Equal(UserAdminIssueCodes.PASSWORD_TOO_SHORT, issue.Code);
        Assert.NotNull(issue.Args);
        Assert.Equal(6, Assert.IsType<int>(issue.Args![0]));
        Assert.Equal(4, Assert.IsType<int>(issue.Args[1]));   // ← 字节数，不是码元数(2)
    }

    [Fact]
    public void CheckLevel_Admin_ReturnsTrueForLevel15()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.True(auth.CheckLevel(sessionId, 15));
        Assert.True(auth.CheckLevel(sessionId, 8));
        Assert.True(auth.CheckLevel(sessionId, 0));
    }

    [Fact]
    public void CheckLevel_Operator_ReturnsFalseForHighLevel()
    {
        using var auth = new AuthenticationService();
        var op = new UserProfile { Username = "op1", DisplayName = "Op1", RoleId = "role-operator" };
        auth.SetUserPassword(op, "pass");
        auth.RegisterUser(op);

        var sessionId = auth.Login("op1", "pass").SessionId;
        Assert.True(auth.CheckLevel(sessionId, 0));
        Assert.True(auth.CheckLevel(sessionId, 3));
        Assert.False(auth.CheckLevel(sessionId, 4));
    }

    [Fact]
    public void Logout_InvalidatesSession()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        Assert.NotNull(sessionId);
        auth.Logout(sessionId!);
        Assert.False(auth.CheckPermission(sessionId, Permission.ViewScreen));
    }

    [Fact]
    public void RegisterAndLogin_CustomUser_Succeeds()
    {
        using var auth = new AuthenticationService();
        var user = new UserProfile
        {
            Username = "engineer1",
            DisplayName = "Engineer 1",
            RoleId = "role-engineer"
        };
        auth.SetUserPassword(user, "secure-pass");
        auth.RegisterUser(user);

        var sessionId = auth.Login("engineer1", "secure-pass").SessionId;
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.ChangeConfig));
        Assert.False(auth.CheckPermission(sessionId, Permission.ManageUsers));
    }

    [Fact]
    public void DeleteUser_RemovesUser()
    {
        using var auth = new AuthenticationService();
        var user = new UserProfile { Username = "temp", RoleId = "role-operator" };
        auth.SetUserPassword(user, "pass");
        auth.RegisterUser(user);

        Assert.NotNull(auth.GetUserByUsername("temp"));
        auth.DeleteUser(user.Id);
        Assert.Null(auth.GetUserByUsername("temp"));
    }

    [Fact]
    public void ChangeUserRole_UpdatesPermissions()
    {
        using var auth = new AuthenticationService();
        var user = new UserProfile { Username = "upgrade", RoleId = "role-operator" };
        auth.SetUserPassword(user, "pass");
        auth.RegisterUser(user);

        var sessionId = auth.Login("upgrade", "pass").SessionId;
        Assert.NotNull(sessionId);
        Assert.False(auth.CheckPermission(sessionId, Permission.ChangeConfig));

        auth.ChangeUserRole(user.Id, "role-engineer");
        // Need to re-login for new role
        auth.Logout(sessionId!);
        sessionId = auth.Login("upgrade", "pass").SessionId;
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.ChangeConfig));
    }

    // ── 审计日志测试 ──

    [Fact]
    public void AuditLog_Login_RecordsEntry()
    {
        using var auth = new AuthenticationService();
        auth.Login("admin", "admin");

        var log = auth.GetAuditLog(10);
        Assert.Contains(log, e => e.Action == AuditAction.Login && e.Username == "admin");
    }

    [Fact]
    public void AuditLog_LoginFailed_RecordsEntry()
    {
        using var auth = new AuthenticationService();
        auth.Login("admin", "wrong");

        var log = auth.GetAuditLog(10);
        Assert.Contains(log, e => e.Action == AuditAction.LoginFailed);
    }

    [Fact]
    public void AuditLog_UserDeleted_RecordsEntry()
    {
        using var auth = new AuthenticationService();
        var user = new UserProfile { Username = "deleteme", RoleId = "role-operator" };
        auth.SetUserPassword(user, "pass");
        auth.RegisterUser(user);
        auth.DeleteUser(user.Id);

        var log = auth.GetAuditLog(10);
        Assert.Contains(log, e => e.Action == AuditAction.UserDeleted);
    }

    [Fact]
    public void AuditLog_OrderedByTimeDescending()
    {
        using var auth = new AuthenticationService();
        auth.Login("admin", "wrong");    // T1
        auth.Login("admin", "admin");    // T2

        var log = auth.GetAuditLog(10);
        Assert.Equal(2, log.Count);  // login failed + login success
        Assert.True(log[0].Timestamp >= log[1].Timestamp);
    }

    [Fact]
    public void AuditLog_FilterByTimeRange()
    {
        using var auth = new AuthenticationService();
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        var t1 = DateTime.UtcNow;

        auth.Login("admin", "wrong");
        Thread.Sleep(10);
        auth.Login("admin", "admin");

        // Should find 2+ entries in recent range
        var entries = auth.GetAuditLogRange(t0, t1.AddDays(1));
        Assert.NotEmpty(entries);
    }

    // ── 会话管理测试 ──

    [Fact]
    public void GetActiveSessions_ReturnsLoggedIn()
    {
        using var auth = new AuthenticationService();
        auth.Login("admin", "admin");

        var sessions = auth.GetActiveSessions();
        Assert.NotEmpty(sessions);
        Assert.Contains(sessions, s => s.Username == "admin");
    }

    [Fact]
    public void GetActiveSessions_AfterLogout_ReturnsEmpty()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        auth.Logout(sessionId!);

        var sessions = auth.GetActiveSessions();
        Assert.DoesNotContain(sessions, s => s.Username == "admin");
    }

    [Fact]
    public void GetSessionUser_ReturnsCorrectUser()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.Login("admin", "admin").SessionId;
        var user = auth.GetSessionUser(sessionId);
        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);
    }

    // ── Break-glass 测试 ──

    [Fact]
    public void BreakGlassLogin_WithReason_Succeeds()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.BreakGlassLogin("admin", "admin", "紧急维护");
        Assert.NotNull(sessionId);
        Assert.True(auth.CheckPermission(sessionId, Permission.ManageUsers));
    }

    [Fact]
    public void BreakGlassLogin_NoReason_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        var sessionId = auth.BreakGlassLogin("admin", "admin", "");
        Assert.Null(sessionId);
    }

    [Fact]
    public void BreakGlassLogin_NonAdmin_ReturnsNull()
    {
        using var auth = new AuthenticationService();
        var user = new UserProfile { Username = "op", RoleId = "role-operator" };
        auth.SetUserPassword(user, "pass");
        auth.RegisterUser(user);

        var sessionId = auth.BreakGlassLogin("op", "pass", "test");
        Assert.Null(sessionId);
    }

    [Fact]
    public void AuditLog_BreakGlass_RecordsSpecialEntry()
    {
        using var auth = new AuthenticationService();
        auth.BreakGlassLogin("admin", "admin", "紧急维护");

        var log = auth.GetAuditLog(10);
        Assert.Contains(log, e => e.Action == AuditAction.BreakGlassAccess);
    }

    // ── 事件测试 ──

    [Fact]
    public void AuditLoggedEvent_FiresOnAction()
    {
        var fired = false;
        using var auth = new AuthenticationService();
        auth.AuditLogged += (_, _) => fired = true;
        auth.Login("admin", "admin");
        Assert.True(fired);
    }

    [Fact]
    public void UserLoginChangedEvent_Fires()
    {
        var fired = false;
        using var auth = new AuthenticationService();
        auth.UserLoginChanged += (_, _) => fired = true;
        auth.Login("admin", "admin");
        Assert.True(fired);
    }

    // ── Dispose 清理测试 ──

    [Fact]
    public void Dispose_ClearsAllState()
    {
        var auth = new AuthenticationService();
        auth.Login("admin", "admin");
        auth.Dispose();

        Assert.Empty(auth.GetAuditLog());
        Assert.Empty(auth.GetActiveSessions());
    }
}
