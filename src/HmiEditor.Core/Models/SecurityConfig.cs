using System.Collections.Generic;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.Models;

/// <summary>
/// HMI 运行模式 —— 决定运行时操作权限的基础级别。
/// 与 UserRole/Permission 配合形成完整的权限体系。
/// </summary>
public enum OperationMode
{
    /// <summary>全功能模式：工程师/管理员使用。</summary>
    Designer = 0,

    /// <summary>操作员模式：基础画面 + 控件操作 + 报警确认。</summary>
    Operator = 1,

    /// <summary>维护模式：操作员权限 + 配方编辑 + 数据导出。</summary>
    Maintenance = 2,

    /// <summary>只读模式：仅查看画面，禁止任何操作。</summary>
    ReadOnly = 3,
}

/// <summary>
/// 运行时安全策略配置 —— 操作模式、会话管理、密码策略、审计开关。
/// 与已有的 UserRole/Permission 枚举配合形成完整的权限体系。
/// </summary>
public class SecurityConfig
{
    /// <summary>
    /// HMI 运行模式。设计模式下所有操作可用，操作员/维护模式受权限约束。
    /// </summary>
    public OperationMode OperationMode { get; set; } = OperationMode.Designer;

    /// <summary>
    /// 用户会话超时（分钟）。超时无操作后自动登出并锁定屏幕。
    /// </summary>
    public int SessionTimeoutMin { get; set; } = 30;

    /// <summary>
    /// 是否启用操作审计日志。关键操作（登录、配方修改、确认报警）写入审计表。
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// 审计日志存储路径。空则写入环形缓冲区（内存），指定则写入 SD 卡文件。
    /// </summary>
    public string? AuditLogStoragePath { get; set; }

    /// <summary>
    /// 用户密码最小长度（**UTF-8 字节数**，不是字符数）。
    /// <para>同时下发为 <c>HMI_SEC_PASSWORD_MIN_LEN</c>：设备端登录屏提交时用 <c>strlen</c> 做下限校验，
    /// 免得"编辑器拦住了、设备上照样能设一个 1 位密码"。<c>strlen</c> 数的是字节，
    /// 所以编辑器侧（<c>ProjectSettingsView.OnSetRuntimeUserPassword</c>）也**必须**按
    /// <c>Encoding.UTF8.GetByteCount</c> 判 —— 两侧不同口径时，同一个口令会出现
    /// "编辑器说太短、设备却放行"（或反过来），而归因成本很高（2026-09-17 统一）。</para>
    /// <para><b>对中文口令的后果（刻意保留，未改设备端）：</b>一个汉字占 3 字节，
    /// 因此下限 8 时"电机启动"（4 字 = 12 字节）是合格的。也就是说这条下限在**字符**意义上
    /// 对 CJK 更宽松。之所以统一到字节而不是改设备端按字符数：设备端已有口令是按字节校验放行的，
    /// 收紧会让**现有凭据直接登不上**（等于把人锁在门外），而放宽编辑器只是停止拒绝设备本就接受的口令。</para>
    /// </summary>
    public int PasswordMinLength { get; set; } = 6;

    /// <summary>
    /// 最大连续登录失败次数。超过后账户临时锁定 LockoutDurationMin 分钟。
    /// <para>同时下发为 <c>HMI_SEC_MAX_LOGIN_ATTEMPTS</c>，由设备端 <c>hmi_login_submit</c> 执行。</para>
    /// </summary>
    public int MaxLoginAttempts { get; set; } = 5;

    /// <summary>
    /// 账户锁定持续时间（分钟）。
    /// <para>同时下发为 <c>HMI_SEC_LOCKOUT_MIN</c>。设备端按"用户名"独立计数，
    /// 与编辑器的按账户锁定语义一致。</para>
    /// </summary>
    public int LockoutDurationMin { get; set; } = 15;

    /// <summary>
    /// 是否启用自动登出。超时后显示锁屏界面。
    /// </summary>
    public bool AutoLogoutEnabled { get; set; } = true;

    /// <summary>
    /// 锁屏界面的屏幕 Id。空则使用系统默认锁屏（黑屏 + 时间显示）。
    /// </summary>
    public string? LockScreenId { get; set; }

    /// <summary>
    /// 是否启用防篡改检测。检测到 Flash 扇区 CRC 异常时触发保护模式。
    /// </summary>
    public bool EnableTamperDetection { get; set; }

    /// <summary>
    /// 新用户注册时的默认角色 Id（关联 UserRole.Id）。
    /// </summary>
    public string DefaultRoleId { get; set; } = "role-operator";

    /// <summary>
    /// **运行时用户表** —— 设备登录屏要校验的账户，随工程保存并编译进
    /// <c>generated_security.h</c>（<c>HMI_SEC_USERS_INIT</c>）下发。
    ///
    /// <para><b>空表 = 不下发用户表</b>：设备端退回"<c>hmi_login</c> 按默认角色直接授权"的
    /// 既有行为，产物与历史逐字节一致（既有工程零改动）。</para>
    ///
    /// <para><b>与编辑器自身登录账户无关</b>：后者是 <c>AuthenticationService</c> 的
    /// <c>UserProfile</c>，活在编辑器进程内、不落工程。两者**不得**互相赋值 ——
    /// 详见 <see cref="RuntimeUser"/> 的类注释（跨边界复用凭据是安全反模式）。</para>
    /// </summary>
    public List<RuntimeUser> RuntimeUsers { get; set; } = new();
}
