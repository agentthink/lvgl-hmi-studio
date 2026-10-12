namespace HmiEditor.Core.Models.User;

/// <summary>
/// HMI **运行时**用户 —— 随工程保存、并编译进 <c>generated_security.h</c> 下发到设备/模拟器的登录账户。
///
/// <para><b>为什么必须与 <see cref="UserProfile"/> 分开（这是安全边界，不是洁癖）：</b>
/// <see cref="UserProfile"/> 服务的是**编辑器自身**的访问控制 —— <c>AuthenticationService</c>
/// 在 App 启动时 new 出来交给 <c>LoginDialog</c>，用户表活在编辑器进程内、**不随工程落盘**
/// （只有审计日志能落盘），预置账户 admin/admin。本类型服务的是**设备登录屏**。
/// 两者共用一份凭据等于跨边界复用：编辑器管理员口令下发到设备后，任何拿到固件 dump 的人
/// 都能离线爆破出编辑器口令；反过来设备的弱口令也成了编辑器的入口。
/// 所以本类型只承载运行时凭据，与 <c>AuthenticationService</c> 之间**没有任何数据通路**。</para>
///
/// <para><b>哈希格式：</b><see cref="PasswordHash"/> = <c>"Base64(salt):Base64(hash)"</c>，
/// 算法与迭代次数见 <c>RuntimePasswordHasher</c> —— 那是编辑器 C# 与固件 C 两侧
/// **必须逐位一致**的跨语言契约，由固定向量测试钉住（向量由独立实现算出，不是自证）。</para>
/// </summary>
public class RuntimeUser
{
    /// <summary>登录用户名（设备端按它查表；**大小写敏感**，与固件 <c>strcmp</c> 一致）。</summary>
    public string Username { get; set; } = "";

    /// <summary>显示名称（仅供编辑器 UI 显示，不下发到设备）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// 角色 Id（关联 <see cref="UserRole.Id"/>）。
    /// <para>只能引用 <c>SecurityConfigCodeGen</c> 会发射的那三个预置角色
    /// （<c>role-operator</c> / <c>role-engineer</c> / <c>role-admin</c>）：设备端角色表就是那三个，
    /// 填了表外的 Id 会让该账户在设备上**登录失败**。生成器对此发警告注释，不静默回退。</para>
    /// </summary>
    public string RoleId { get; set; } = "role-operator";

    /// <summary>
    /// 密码哈希（<c>"Base64Salt:Base64Hash"</c>）。
    /// <para>空串 = 该账户**无法登录**（不是"免密放行"）—— 设备端解析失败即拒绝。</para>
    /// </summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>账户是否启用。禁用账户在设备端被直接拒绝。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 字段级拷贝。
    ///
    /// <para><b>用途是「编辑草稿」</b>（工程设置对话框：编辑期改副本，点确定才写回，
    /// 点取消即丢弃），不是领域意义上的克隆。</para>
    ///
    /// <para><b>为什么放在这里而不是让调用方手写字段拷贝：</b>手写的拷贝方法在本类
    /// 新增字段后**仍然编译通过**，只是静默漏拷一个字段 —— 那种 bug 表现为
    /// "填了没生效"或"取消了又回来了"，排查成本远高于在此处维护一行。</para>
    /// </summary>
    public RuntimeUser Clone() => new()
    {
        Username = Username,
        DisplayName = DisplayName,
        RoleId = RoleId,
        PasswordHash = PasswordHash,
        IsEnabled = IsEnabled,
    };
}
