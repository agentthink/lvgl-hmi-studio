namespace HmiEditor.Core.Models.User;

/// <summary>
/// 安全审计日志条目 —— 记录高风险操作的审计轨迹。
/// 设计原则 (identity-and-secrets): append-only, tamper-evident.
/// </summary>
public class AuditLogEntry
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>用户 ID</summary>
    public string UserId { get; set; } = "";

    /// <summary>用户名</summary>
    public string Username { get; set; } = "";

    /// <summary>操作类型</summary>
    public AuditAction Action { get; set; }

    /// <summary>操作的详细描述</summary>
    public string Detail { get; set; } = "";

    /// <summary>操作结果（成功/失败）</summary>
    public bool Success { get; set; }

    /// <summary>来源 IP 或终端标识</summary>
    public string? Source { get; set; }

    /// <summary>关联的 Session ID</summary>
    public string? SessionId { get; set; }

    /// <summary>额外元数据（JSON）</summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// 需审计的高风险操作类型。
/// </summary>
public enum AuditAction
{
    /// <summary>用户登录</summary>
    Login,

    /// <summary>用户登出</summary>
    Logout,

    /// <summary>登录失败</summary>
    LoginFailed,

    /// <summary>创建用户</summary>
    UserCreated,

    /// <summary>删除用户</summary>
    UserDeleted,

    /// <summary>修改用户权限</summary>
    UserPermissionChanged,

    /// <summary>修改系统配置</summary>
    ConfigChanged,

    /// <summary>密码修改</summary>
    PasswordChanged,

    /// <summary>紧急访问 (break-glass)</summary>
    BreakGlassAccess,

    /// <summary>导出数据</summary>
    DataExported,

    /// <summary>搁置报警（阶段 278；契约 §7）—— 停止播报是"人做的决定"，必须留痕。</summary>
    AlarmShelved,

    /// <summary>解除搁置（含**到期自动解除** —— 否则事后看不出"报警为什么突然又开始响"）。</summary>
    AlarmUnshelved,

    /// <summary>抑制报警（阶段 280）—— 抑制期间该报警**不产生记录**，必须留痕。</summary>
    AlarmSuppressed,

    /// <summary>解除抑制。</summary>
    AlarmUnsuppressed,

    /// <summary>修改用户资料（显示名等，阶段 124）</summary>
    UserProfileChanged,
}
