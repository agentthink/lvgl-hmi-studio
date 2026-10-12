namespace HmiEditor.Core.Models.User;

/// <summary>
/// 用户角色 —— 0-15 级安全模型，对标 Pro-face GP-Pro EX。
///
/// 三层预置角色：
///   Operator(0-3):  基础操作，查看画面 + 操作控件 + 确认报警
///   Engineer(4-7) :  工程配置，追加配置修改 + 配方编辑
///   Admin(8-15):    完全权限，含用户管理 + 调试
///
/// <para><b>角色名与描述都是界面文案，不在这里</b>：本类只存 <c>Id/Name/Level/Permissions</c>。
/// 显示名与描述走字典（<c>role.operator</c> / <c>role.operator.desc</c> 等，见 <c>RoleTextKeys</c>）——
/// 模型里放用户可见文案的后果是"界面语言被模型冻结"，阶段 66 正是修这个。</para>
/// </summary>
public class UserRole
{
    /// <summary>角色标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>角色名称</summary>
    public string Name { get; set; } = "";

    /// <summary>安全等级 (0-15)，Pro-face 兼容体系</summary>
    public int Level { get; set; } = 0;

    /// <summary>权限位掩码</summary>
    public Permission Permissions { get; set; } = Permission.None;

    /// <summary>角色描述</summary>
    public string? Description { get; set; }

    /// <summary>是否为预置角色（预置角色不可删除）</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// 检查是否拥有指定权限。
    /// </summary>
    public bool HasPermission(Permission permission) =>
        (Permissions & permission) == permission;

    /// <summary>
    /// 预置 Operator 角色。
    /// </summary>
    public static UserRole BuiltInOperator => new()
    {
        Id = "role-operator",
        Name = "Operator",
        Level = 3,
        Permissions = Permission.ViewScreen | Permission.OperateControl | Permission.AcknowledgeAlarm |
                      Permission.ShelveAlarm | Permission.ViewAlarmHistory,
        IsBuiltIn = true,
    };

    /// <summary>
    /// 预置 Engineer 角色。
    /// </summary>
    public static UserRole BuiltInEngineer => new()
    {
        Id = "role-engineer",
        Name = "Engineer",
        Level = 7,
        Permissions = Permission.ViewScreen | Permission.OperateControl |
                      Permission.AcknowledgeAlarm | Permission.ShelveAlarm | Permission.EditRecipe |
                      Permission.ChangeConfig | Permission.ViewAlarmHistory | Permission.ExportData,
        IsBuiltIn = true,
    };

    /// <summary>
    /// 预置 Administrator 角色（完全权限）。
    /// </summary>
    public static UserRole BuiltInAdministrator => new()
    {
        Id = "role-admin",
        Name = "Administrator",
        Level = 15,
        Permissions = Permission.All,
        IsBuiltIn = true,
    };

    /// <summary>
    /// **全部预置角色 —— 唯一清单。**
    ///
    /// <para>生成头 <c>HMI_SEC_ROLES_INIT</c> 的行序、导出的模拟器算「预览身份 → 权限掩码」、
    /// 以及编辑器「运行时用户」的角色下拉与校验，全部从这一份取
    /// （<c>SecurityConfigCodeGen.BuiltInRoles</c> 是对它的转发，不是第二份）。
    /// 每多写一遍这三个角色，就多一个静默不一致的机会 —— 典型症状是
    /// 「预览里能登录、设备上登不上」或「生成头的掩码与预览掩码对不上」。</para>
    ///
    /// <para><b>表达式体，不是静态字段</b> ⇒ 每次调用返回**新实例**：
    /// 调用方改了 <c>Name</c>/<c>Permissions</c> 不会污染别处，也不会污染下一次调用。</para>
    /// </summary>
    public static UserRole[] BuiltInAll =>
        [BuiltInOperator, BuiltInEngineer, BuiltInAdministrator];
}
