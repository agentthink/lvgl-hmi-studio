namespace HmiEditor.Core.Models.User;

/// <summary>
/// 用户模型 —— 单个操作员账户。
///
/// 安全设计 (identity-and-secrets):
///   - PasswordHash: SHA-256 + Salt 哈希存储，绝不存明文
///   - IsEnabled: 禁用账户实现立即吊销
///   - ExpiresAt: 可选过期时间（临时账户）
/// </summary>
public class UserProfile
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>登录用户名</summary>
    public string Username { get; set; } = "";

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>密码哈希（SHA-256 + Salt 格式: "Base64Salt:Base64Hash"）</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>角色 ID</summary>
    public string RoleId { get; set; } = "role-operator";

    /// <summary>账户是否启用（禁用 = 立即吊销）</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>账户过期时间（null = 永不过期）</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>上次登录时间</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>连续登录失败次数（防暴力破解）</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>账户是否锁定（超过最大失败尝试次数）</summary>
    public bool IsLocked { get; set; }

    /// <summary>锁定时长（分钟）</summary>
    public int LockoutMinutes { get; set; } = 30;

    /// <summary>账户锁定截止时间（UTC）。null = 未锁定或无自动解锁。用于判断锁定是否到期自动解锁。</summary>
    public DateTime? LockedUntilUtc { get; set; }
}
