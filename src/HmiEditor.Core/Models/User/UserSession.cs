namespace HmiEditor.Core.Models.User;

/// <summary>
/// 会话记录 —— 用户登录后的活动会话。
/// 身份验证事后分析所需。
/// </summary>
public class UserSession
{
    /// <summary>会话 ID</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>用户 ID</summary>
    public string UserId { get; set; } = "";

    /// <summary>用户名（冗余存储，便于审计）</summary>
    public string Username { get; set; } = "";

    /// <summary>角色 ID（登录时快照）</summary>
    public string RoleId { get; set; } = "";

    /// <summary>登录时间</summary>
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后活动时间</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>会话是否已过期</summary>
    public bool IsExpired { get; set; }

    /// <summary>会话超时（分钟），默认 480 分钟（8 小时）</summary>
    public int TimeoutMinutes { get; set; } = 480;
}
