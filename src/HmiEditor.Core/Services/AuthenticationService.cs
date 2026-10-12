using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.Services;

/// <summary>
/// 用户认证与访问控制服务。
///
/// 设计原则 (identity-and-secrets):
///   - 密码仅存储 SHA-256 + Salt 哈希，不存明文
///   - 最小权限: 每次权限检查独立进行
///   - 会话管理: 超时自动过期 + 最后活动时间跟踪
///   - 审计日志: 高风险操作追加记录 (append-only)
///   - 防暴力破解: 连续失败锁定策略
///   - Break-glass: 紧急访问路径（仅管理员可用）
/// </summary>
public class AuthenticationService : IDisposable
{
    private readonly Dictionary<string, UserProfile> _users = new();
    private readonly Dictionary<string, UserRole> _roles = new();
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();

    // Append-only audit log
    private readonly List<AuditLogEntry> _auditLog = new();
    private readonly object _auditLock = new();
    private readonly object _userLock = new();

    // ── 默认配置 ──

    public int MaxLoginAttempts { get; set; } = 5;
    public int DefaultSessionTimeoutMinutes { get; set; } = 480;

    /// <summary>当前工程的安全配置（由 ApplySecurityConfig 设置）。null = 未应用工程配置，使用默认值。</summary>
    private SecurityConfig? _securityConfig;

    /// <summary>
    /// 应用工程安全配置（来自 HmiProject.SecurityConfig）。更新登录尝试上限、会话超时、审计路径等。
    /// 应在加载/创建工程后、用户操作前调用。密码长度/自动登出/锁定时长等策略在后续操作中读取。
    /// </summary>
    public void ApplySecurityConfig(SecurityConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _securityConfig = config;
        MaxLoginAttempts = config.MaxLoginAttempts;
        DefaultSessionTimeoutMinutes = config.SessionTimeoutMin;
        if (!string.IsNullOrWhiteSpace(config.AuditLogStoragePath))
            AuditLogFilePath = config.AuditLogStoragePath;
        Log?.Invoke($"[AuthService] 应用安全配置: MaxAttempts={MaxLoginAttempts}, SessionTimeout={DefaultSessionTimeoutMinutes}min, PwdMinLen={config.PasswordMinLength}, AutoLogout={config.AutoLogoutEnabled}, AuditLog={config.EnableAuditLog}");
    }

    /// <summary>
    /// 审计日志文件路径（JSONL）。设置后所有审计记录同时追加到文件（append-only）。
    /// 未设置时仅保留内存日志。
    /// </summary>
    public string? AuditLogFilePath { get; set; }

    /// <summary>审计日志变更事件（用于 UI 刷新）</summary>
    public event EventHandler<AuditLogEntry>? AuditLogged;

    /// <summary>用户登录/登出事件</summary>
    public event EventHandler<string>? UserLoginChanged;

    /// <summary>
    /// 会话因超时被锁定时触发。事件参数为 <see cref="SecurityConfig.LockScreenId"/>
    ///（null 表示使用系统默认锁屏：黑屏 + 时间显示）。UI 层据此切换到锁屏界面。
    /// 仅在 <see cref="SecurityConfig.AutoLogoutEnabled"/> 未显式关闭时触发。
    /// </summary>
    public event EventHandler<string?>? SessionLocked;

    /// <summary>日志输出</summary>
    public Action<string>? Log { get; set; }

    // ── 初始化 ──

    public AuthenticationService()
    {
        InitializeDefaultRoles();
        InitializeDefaultUsers();
    }

    private void InitializeDefaultRoles()
    {
        RegisterRole(UserRole.BuiltInOperator);
        RegisterRole(UserRole.BuiltInEngineer);
        RegisterRole(UserRole.BuiltInAdministrator);
    }

    private void InitializeDefaultUsers()
    {
        // 预置管理员账户（首次启动）
        var admin = new UserProfile
        {
            Id = "user-admin",
            Username = "admin",
            DisplayName = "Administrator",
            RoleId = "role-admin",
        };
        SetUserPassword(admin, "admin");
        RegisterUser(admin);
    }

    // ── 角色管理 ──

    public void RegisterRole(UserRole role)
    {
        lock (_userLock)
        {
            _roles[role.Id] = role;
        }
        Log?.Invoke($"[AuthService] 注册角色: {role.Name} (Level={role.Level})");
    }

    public UserRole? GetRole(string roleId)
    {
        lock (_userLock)
        {
            return _roles.GetValueOrDefault(roleId);
        }
    }

    public IReadOnlyList<UserRole> GetAllRoles()
    {
        lock (_userLock)
        {
            return _roles.Values.ToList();
        }
    }

    // ── 用户管理 ──

    public void RegisterUser(UserProfile user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_userLock)
        {
            _users[user.Username.ToLowerInvariant()] = user;
        }
        Log?.Invoke($"[AuthService] 注册用户: {user.Username} (Role={user.RoleId})");
    }

    /// <summary>
    /// 创建并注册新用户。当未指定 roleId 时使用 <see cref="SecurityConfig.DefaultRoleId"/>
    /// 作为默认角色（关联 UserRole.Id）。密码经 SetUserPassword 哈希存储。
    /// </summary>
    /// <param name="username">登录用户名</param>
    /// <param name="displayName">显示名称（null 则用 username）</param>
    /// <param name="password">明文密码（经 PasswordMinLength 校验）</param>
    /// <param name="roleId">角色 Id（null 则使用 SecurityConfig.DefaultRoleId）</param>
    /// <returns>已注册的 UserProfile</returns>
    public UserProfile CreateUser(string username, string displayName, string password, string? roleId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // 未指定角色时使用 SecurityConfig.DefaultRoleId（默认 "role-operator"）
        var effectiveRoleId = !string.IsNullOrWhiteSpace(roleId)
            ? roleId!
            : (_securityConfig?.DefaultRoleId ?? "role-operator");

        // 校验角色已注册（与 ChangeUserRole 行为对齐）。
        // DefaultRoleId 配置错误或显式传入未注册角色时抛出**带码异常**，
        // 避免静默创建 RoleId 指向不存在角色的用户（此类用户后续权限检查会全部失败却无任何提示）。
        // ⚠ 阶段 97（#61d）把这里从裸 ArgumentException 换成 UserAdminException：
        //    它是**用户可撞到的运行条件**（工程配置写错），不是调用方编程错误
        //    ⇒ 必须带码，否则界面只能回落中文原文（英文界面下照旧中文）。
        if (GetRole(effectiveRoleId) == null)
            throw new UserAdminException(UserAdminIssue.RoleNotRegistered(effectiveRoleId));

        var user = new UserProfile
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            RoleId = effectiveRoleId,
            IsEnabled = true,
        };

        SetUserPassword(user, password);
        RegisterUser(user);

        AppendAudit(AuditAction.UserCreated, user.Id, username,
            $"创建用户: {username} (Role={effectiveRoleId})", true);
        Log?.Invoke($"[AuthService] 创建用户: {username} (Role={effectiveRoleId})");
        return user;
    }

    public UserProfile? GetUser(string userId)
    {
        lock (_userLock)
        {
            return _users.Values.FirstOrDefault(u => u.Id == userId);
        }
    }

    public UserProfile? GetUserByUsername(string username)
    {
        lock (_userLock)
        {
            return _users.GetValueOrDefault(username.ToLowerInvariant());
        }
    }

    public IReadOnlyList<UserProfile> GetAllUsers()
    {
        lock (_userLock)
        {
            return _users.Values.ToList();
        }
    }

    public bool DeleteUser(string userId)
    {
        UserProfile? user;
        lock (_userLock)
        {
            user = _users.Values.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;
            _users.Remove(user.Username.ToLowerInvariant());
        }

        TerminateUserSessions(userId);
        AppendAudit(AuditAction.UserDeleted, user.Id, user.Username,
            $"删除用户: {user.Username}", true);
        Log?.Invoke($"[AuthService] 删除用户: {user.Username}");
        return true;
    }

    /// <summary>
    /// 设置用户密码（生成 SHA-256 + Salt 哈希）。
    /// </summary>
    public void SetUserPassword(UserProfile user, string plainPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainPassword);
        // SecurityConfig.PasswordMinLength 校验（未应用工程配置时不校验，保持向后兼容预置账户）。
        // ⚠ 口径必须是 **UTF-8 字节数**（登记 #20 的语义、模型的注释、编辑器
        // `ProjectSettingsView.OnSetRuntimeUserPassword`、设备端 `strlen` 四处一致）：
        // 旧实现用 `plainPassword.Length`（UTF-16 码元 ≈ 字符数）⇒ 同一个口令在「用户管理」这条路上
        // 与「工程设置 → 运行时用户」那条路上结论相反（"电机" 6 字节 2 码元：这里拒、那里收；
        // 登记 #83，阶段 168）。文案也随之按字节说（`user.issue.password_too_short`）。
        if (_securityConfig is { } cfg)
        {
            var byteCount = Encoding.UTF8.GetByteCount(plainPassword);
            if (byteCount < cfg.PasswordMinLength)
                throw new UserAdminException(UserAdminIssue.PasswordTooShort(cfg.PasswordMinLength, byteCount));
        }
        user.PasswordHash = HashPassword(plainPassword);
    }

    /// <summary>
    /// 启用/禁用用户账户。
    /// </summary>
    public bool SetUserEnabled(string userId, bool enabled)
    {
        var user = GetUser(userId);
        if (user == null) return false;

        user.IsEnabled = enabled;
        if (!enabled)
            TerminateUserSessions(userId);

        AppendAudit(AuditAction.UserPermissionChanged, userId, user.Username,
            $"{(enabled ? "启用" : "禁用")}账户: {user.Username}", true);
        return true;
    }

    /// <summary>
    /// 更换用户角色。
    /// </summary>
    public bool ChangeUserRole(string userId, string newRoleId)
    {
        var user = GetUser(userId);
        var role = GetRole(newRoleId);
        if (user == null || role == null) return false;

        var oldRole = user.RoleId;
        user.RoleId = newRoleId;
        AppendAudit(AuditAction.UserPermissionChanged, userId, user.Username,
            $"变更角色: {oldRole} → {newRoleId}", true);
        Log?.Invoke($"[AuthService] {user.Username} 角色变更: {oldRole} → {newRoleId}");
        return true;
    }

    /// <summary>
    /// 更新用户显示名（阶段 124，#66②）。
    ///
    /// <para>与 <see cref="SetUserEnabled"/> / <see cref="ChangeUserRole"/> 同一条纪律：
    /// **改模型 + 写审计**，调用方（用户管理面板）只需接线。显示名不是权限，
    /// 所以用独立的 <see cref="AuditAction.UserProfileChanged"/>，不要混进
    /// <c>UserPermissionChanged</c>（那会让审计里"权限变更"名不副实）。</para>
    ///
    /// <para>值没变时**不写审计**（否则每次失焦都留一条空记录）。</para>
    /// </summary>
    public bool UpdateUserDisplayName(string userId, string? displayName)
    {
        var user = GetUser(userId);
        if (user == null) return false;

        var next = displayName?.Trim() ?? "";
        if (string.Equals(user.DisplayName, next, StringComparison.Ordinal)) return true;

        var old = user.DisplayName;
        user.DisplayName = next;
        AppendAudit(AuditAction.UserProfileChanged, userId, user.Username,
            $"变更显示名: {old} → {next}", true);
        Log?.Invoke($"[AuthService] {user.Username} 显示名变更: {old} → {next}");
        return true;
    }

    // ── 认证 ──

    /// <summary>
    /// 用户登录。成功给会话 ID，失败给**带码的原因**（<see cref="LoginResult"/>）。
    /// 连续失败超过 <see cref="MaxLoginAttempts"/> 则锁定账户。
    ///
    /// <para><b>为什么返回记录体而不是 <c>string?</c></b>（登记 <b>#61v</b>，阶段 99）：
    /// 原先失败一律 <c>null</c> ⇒ 五种完全不同的失败（用户不存在 / 锁定 / 禁用 / 过期 /
    /// 密码错误）**压成同一个 <c>null</c>**，界面只能说「用户名或密码错误」。
    /// 原因本来就在审计日志里，只是没有通道送回界面。</para>
    /// </summary>
    public LoginResult Login(string username, string password, string? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = GetUserByUsername(username);

        // 用户不存在
        if (user == null)
        {
            AppendAudit(AuditAction.LoginFailed, "", username, $"用户不存在: {username}", false);
            Log?.Invoke($"[AuthService] ⚠ 登录失败(用户不存在): {username}");
            // ⚠ 与「密码错误」共用码：分开会让界面变成用户名枚举预言机（见 LoginIssueCodes）。
            return LoginResult.Failed(LoginIssue.BadCredentials());
        }

        // 账户被锁定 —— 检查锁定是否到期自动解锁（SecurityConfig.LockoutDurationMin）
        if (user.IsLocked)
        {
            if (user.LockedUntilUtc.HasValue && DateTime.UtcNow >= user.LockedUntilUtc.Value)
            {
                user.IsLocked = false;
                user.FailedLoginAttempts = 0;
                user.LockedUntilUtc = null;
                Log?.Invoke($"[AuthService] 账户自动解锁: {username}");
            }
            else
            {
                AppendAudit(AuditAction.LoginFailed, user.Id, username, "账户已锁定", false);
                Log?.Invoke($"[AuthService] ⚠ 登录失败(已锁定): {username}");
                return LoginResult.Failed(LoginIssue.Locked(RemainingLockMinutes(user)));
            }
        }

        // 账户已禁用
        if (!user.IsEnabled)
        {
            AppendAudit(AuditAction.LoginFailed, user.Id, username, "账户已禁用", false);
            Log?.Invoke($"[AuthService] ⚠ 登录失败(已禁用): {username}");
            return LoginResult.Failed(LoginIssue.Disabled());
        }

        // 账户已过期
        if (user.ExpiresAt.HasValue && DateTime.UtcNow > user.ExpiresAt.Value)
        {
            AppendAudit(AuditAction.LoginFailed, user.Id, username, "账户已过期", false);
            Log?.Invoke($"[AuthService] ⚠ 登录失败(已过期): {username}");
            return LoginResult.Failed(LoginIssue.Expired());
        }

        // 验证密码
        if (!VerifyPassword(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            // ⚠ lockoutMin 提到 if 外面：返回值要带上它（本刀之前它只进日志）。
            var lockoutMin = _securityConfig?.LockoutDurationMin ?? 30;
            var justLocked = user.FailedLoginAttempts >= MaxLoginAttempts;
            if (justLocked)
            {
                user.IsLocked = true;
                user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(lockoutMin);
                Log?.Invoke($"[AuthService] ⚠ 账户锁定: {username} ({MaxLoginAttempts}次失败, {lockoutMin}分钟)");
            }

            AppendAudit(AuditAction.LoginFailed, user.Id, username,
                $"密码错误 ({user.FailedLoginAttempts}/{MaxLoginAttempts})", false);
            Log?.Invoke($"[AuthService] ⚠ 登录失败(密码错误): {username}");

            // 「本次把账户顶到锁定」与「普通密码错」是两件事：前者要告诉用户等多久，
            // 后者只是重输。改动前两者都返回 null ⇒ 界面同样无差别。
            return LoginResult.Failed(justLocked
                ? LoginIssue.LockedNow(lockoutMin)
                : LoginIssue.BadCredentials());
        }

        // 登录成功
        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;

        var session = new UserSession
        {
            UserId = user.Id,
            Username = user.Username,
            RoleId = user.RoleId,
            TimeoutMinutes = DefaultSessionTimeoutMinutes,
        };
        _sessions[session.Id] = session;

        AppendAudit(AuditAction.Login, user.Id, username, $"用户登录 (Session={session.Id[..8]}...)", true, source, session.Id);
        Log?.Invoke($"[AuthService] ✓ 登录成功: {username} (Session={session.Id[..8]}...)");
        return LoginResult.Ok(session.Id);
    }

    /// <summary>
    /// 账户锁定剩余分钟数（向上取整、下限 1）。
    ///
    /// <para><b>下限为什么是 1</b>：<c>LockedUntilUtc</c> 为 <c>null</c>（永久锁定）在本仓库
    /// **不可达** —— <c>IsLocked = true</c> 的唯一写点在本类内、且与 <c>LockedUntilUtc</c>
    /// **同时赋值**；管理界面的解锁（<c>UserManagerView</c>）只写 <c>false</c>。
    /// 但真出现时若返回 0，文案会变成「请 0 分钟后再试」⇒ 取 1 兜住。</para>
    ///
    /// <para><b>为什么向上取整</b>：剩余 0.4 分钟时截断成 0 会说「请 0 分钟后再试」，
    /// 而账户其实还锁着。</para>
    /// </summary>
    private static int RemainingLockMinutes(UserProfile user)
    {
        if (!user.LockedUntilUtc.HasValue) return 1;

        var left = (user.LockedUntilUtc.Value - DateTime.UtcNow).TotalMinutes;
        return left <= 0 ? 1 : (int)Math.Ceiling(left);
    }

    /// <summary>
    /// 登出。
    /// </summary>
    public void Logout(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.IsExpired = true;
            AppendAudit(AuditAction.Logout, session.UserId, session.Username, "用户登出", true, sessionId: sessionId);
            UserLoginChanged?.Invoke(this, session.Username);
            Log?.Invoke($"[AuthService] 登出: {session.Username}");
        }
    }

    // ── 授权检查 ──

    /// <summary>
    /// 检查当前会话用户是否拥有指定权限。
    /// </summary>
    public bool CheckPermission(string? sessionId, Permission requiredPermission)
    {
        // 未登录
        if (string.IsNullOrEmpty(sessionId))
            return requiredPermission == Permission.None;

        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        // 会话过期
        if (session.IsExpired)
            return false;

        // 会话超时（SecurityConfig.AutoLogoutEnabled=false 时跳过自动登出）
        if (_securityConfig?.AutoLogoutEnabled != false &&
            (DateTime.UtcNow - session.LastActivityAt).TotalMinutes > session.TimeoutMinutes)
        {
            session.IsExpired = true;
            _sessions.TryRemove(sessionId, out _);
            // 通知 UI 层切换到锁屏界面（SecurityConfig.LockScreenId）
            var lockScreenId = _securityConfig?.LockScreenId;
            SessionLocked?.Invoke(this, lockScreenId);
            Log?.Invoke($"[AuthService] 会话超时锁定: {session.Username} (LockScreen={lockScreenId ?? "默认"})");
            return false;
        }

        // 更新最后活动时间
        session.LastActivityAt = DateTime.UtcNow;

        var role = GetRole(session.RoleId);
        if (role == null) return false;

        return role.HasPermission(requiredPermission);
    }

    /// <summary>
    /// 检查用户等级是否满足要求（页面级访问控制）。
    /// </summary>
    public bool CheckLevel(string? sessionId, int requiredLevel)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        if (!_sessions.TryGetValue(sessionId, out var session)) return false;

        var role = GetRole(session.RoleId);
        return role != null && role.Level >= requiredLevel;
    }

    /// <summary>
    /// 获取当前会话的用户信息。
    /// </summary>
    public UserProfile? GetSessionUser(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        if (!_sessions.TryGetValue(sessionId, out var session)) return null;
        return GetUser(session.UserId);
    }

    /// <summary>
    /// 获取当前活动的会话列表。
    /// </summary>
    public IReadOnlyList<UserSession> GetActiveSessions()
    {
        var now = DateTime.UtcNow;
        // 清理过期会话
        var expiredIds = _sessions.Values
            .Where(s => s.IsExpired || (now - s.LastActivityAt).TotalMinutes > s.TimeoutMinutes)
            .Select(s => s.Id)
            .ToList();
        foreach (var id in expiredIds)
        {
            if (_sessions.TryGetValue(id, out var s))
            {
                // 仅对"新检测到的超时"（尚未 IsExpired）触发锁屏事件，避免重复通知。
                // 与 CheckPermission 超时分支行为一致：移除会话 + 触发 SessionLocked。
                var wasExpired = s.IsExpired;
                s.IsExpired = true;
                if (!wasExpired && _securityConfig?.AutoLogoutEnabled != false)
                {
                    _sessions.TryRemove(id, out _);
                    var lockScreenId = _securityConfig?.LockScreenId;
                    SessionLocked?.Invoke(this, lockScreenId);
                    Log?.Invoke($"[AuthService] 会话超时锁定(清理): {s.Username} (LockScreen={lockScreenId ?? "默认"})");
                }
            }
        }

        return _sessions.Values.Where(s => !s.IsExpired).ToList();
    }

    // ── Break-glass 紧急访问 ──

    /// <summary>
    /// 紧急访问（break-glass）—— 管理员在审计下获得临时权限。
    /// 需要管理员账户和理由说明。
    /// </summary>
    public string? BreakGlassLogin(string adminUsername, string adminPassword,
        string reason, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        // ⚠ `Login` 阶段 99 改成**返回值带码**（`LoginResult`，登记 #61v）⇒ 这里只取会话 id。
        //    本方法自己的 `string?` 契约与三档失败口径**刻意不动**：它的失败原因
        //    （理由为空 / 不是管理员 / 密码错）不是登录原因，而且它**没有界面消费方**
        //    （全仓只有测试在读它）⇒ 码化它今天**没有用户可见收益**（与 #61k 同类「判过的不做」）。
        //    ⚠ 本方法正是本刀「量的那一遍」漏掉的那个消费点 —— 数外部调用方时
        //    `grep '\.Login('` **看不见同类型内部的 unqualified 调用**，是编译器把它点出来的。
        var result = Login(adminUsername, adminPassword, source);
        if (result.SessionId is not { } sessionId) return null;

        if (!_sessions.TryGetValue(sessionId, out var session)) return null;
        var role = GetRole(session.RoleId);
        if (role == null || role.Level < 8)
        {
            Logout(sessionId);
            return null;
        }

        AppendAudit(AuditAction.BreakGlassAccess, session.UserId, session.Username,
            $"紧急访问: {reason}", true);
        Log?.Invoke($"[AuthService] 🔔 Break-glass: {adminUsername}: {reason}");
        return sessionId;
    }

    // ── 审计日志 ──

    /// <summary>
    /// 记录一条审计日志（UI 动作 / 非认证操作共用入口，如导出代码、模板保存）。
    /// 记录写入内存并触发 <see cref="AuditLogged"/>；若设置了
    /// <see cref="AuditLogFilePath"/>，同时追加到文件。
    ///
    /// SecurityConfig.EnableAuditLog=false 时跳过非认证操作的审计记录
    ///（导出、模板保存等），但认证事件（登录/登出/锁定等）由 AppendAudit
    /// 直接处理，始终记录以满足安全合规要求。
    /// </summary>
    public void RecordAudit(AuditAction action, string detail, bool success, string? source = null)
    {
        // EnableAuditLog=false 时仅抑制 UI/非认证操作日志，认证日志不受影响
        if (_securityConfig?.EnableAuditLog == false)
        {
            Log?.Invoke($"[AuthService] 审计日志已禁用，跳过操作记录: {action}");
            return;
        }
        AppendAudit(action, "", "", detail, success, source, null);
    }

    /// <summary>
    /// 从 JSONL 文件加载历史审计记录到内存（按 Id 去重，避免重复加载）。
    /// 用于应用启动时恢复跨会话审计轨迹。
    /// </summary>
    public int LoadAuditLogFromFile(string filePath)
    {
        var loaded = AuditLogStore.LoadAll(filePath);
        lock (_auditLock)
        {
            var existing = new HashSet<string>(_auditLog.Select(e => e.Id));
            var added = 0;
            foreach (var entry in loaded)
            {
                if (existing.Add(entry.Id))
                {
                    _auditLog.Add(entry);
                    added++;
                }
            }
            return added;
        }
    }

    /// <summary>
    /// 获取审计日志（按时间倒序）。
    /// </summary>
    public IReadOnlyList<AuditLogEntry> GetAuditLog(int? maxEntries = null)
    {
        lock (_auditLock)
        {
            var entries = _auditLog.OrderByDescending(e => e.Timestamp).ToList();
            return maxEntries.HasValue ? entries.Take(maxEntries.Value).ToList() : entries;
        }
    }

    /// <summary>
    /// 获取指定时间范围内的审计日志。
    /// </summary>
    public IReadOnlyList<AuditLogEntry> GetAuditLogRange(DateTime from, DateTime to)
    {
        lock (_auditLock)
        {
            return _auditLog
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }
    }

    // ── 密码工具 ──

    /// <summary>
    /// 验证明文密码是否匹配哈希。
    /// </summary>
    public static bool VerifyPassword(string plainPassword, string passwordHash)
    {
        var parts = passwordHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        using var sha256 = SHA256.Create();
        var combined = new byte[salt.Length + Encoding.UTF8.GetBytes(plainPassword).Length];
        Array.Copy(salt, 0, combined, 0, salt.Length);
        Array.Copy(Encoding.UTF8.GetBytes(plainPassword), 0, combined, salt.Length, combined.Length - salt.Length);

        var actualHash = sha256.ComputeHash(combined);
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    /// <summary>
    /// 生成密码哈希（格式: "Base64Salt:Base64Hash"）。
    /// </summary>
    public static string HashPassword(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var sha256 = SHA256.Create();
        var combined = new byte[salt.Length + Encoding.UTF8.GetBytes(plainPassword).Length];
        Array.Copy(salt, 0, combined, 0, salt.Length);
        Array.Copy(Encoding.UTF8.GetBytes(plainPassword), 0, combined, salt.Length, combined.Length - salt.Length);

        var hash = sha256.ComputeHash(combined);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    // ── 清理 ──

    public void Dispose()
    {
        _sessions.Clear();
        lock (_auditLock)
        {
            _auditLog.Clear();
        }
        // 清理事件订阅，避免 Dispose 后外部委托引用导致内存泄漏
        SessionLocked = null;
        AuditLogged = null;
        UserLoginChanged = null;
        Log?.Invoke("[AuthService] 已释放");
    }

    // ── 内部方法 ──

    /// <summary>
    /// 写一条审计（**唯一的写入口** —— 内存表 ＋ append-only 文件 ＋ <see cref="AuditLogged"/> 事件）。
    /// 阶段 278 起对 App 层公开：报警搁置/解除走 <c>AlarmAuditWiring</c> 也走这里，避免第二份落盘逻辑。
    /// </summary>
    public void AppendAudit(AuditAction action, string userId, string username,
        string detail, bool success, string? source = null, string? sessionId = null)
    {
        var entry = new AuditLogEntry
        {
            UserId = userId,
            Username = username,
            Action = action,
            Detail = detail,
            Success = success,
            Timestamp = DateTime.UtcNow,
            Source = source,
            SessionId = sessionId,
        };

        lock (_auditLock)
        {
            _auditLog.Add(entry);
        }

        // 持久化到文件（append-only）。磁盘失败仅记录，不阻断认证流程。
        if (!string.IsNullOrEmpty(AuditLogFilePath) && !AuditLogStore.Append(AuditLogFilePath, entry))
        {
            Log?.Invoke($"[AuthService] ⚠ 审计日志写入失败: {AuditLogFilePath}");
        }

        AuditLogged?.Invoke(this, entry);

        if (action == AuditAction.Login || action == AuditAction.LoginFailed)
            UserLoginChanged?.Invoke(this, username);
    }

    private void TerminateUserSessions(string userId)
    {
        var expiredIds = _sessions.Values
            .Where(s => s.UserId == userId)
            .Select(s => s.Id)
            .ToList();
        foreach (var id in expiredIds)
        {
            if (_sessions.TryGetValue(id, out var s))
                s.IsExpired = true;
        }
    }
}
