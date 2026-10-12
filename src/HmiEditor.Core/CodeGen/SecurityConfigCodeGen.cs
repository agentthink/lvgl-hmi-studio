using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 生成设备端安全/权限头 <c>generated_security.h</c> —— 权限模型的**唯一下沉产物**。
///
/// <para><b>为什么是"生成物"而不是固件里手写一组宏：</b>权限位与角色表在编辑器侧已经有唯一来源
/// （<see cref="Permission"/> / <see cref="UserRole"/> 预置角色 / <see cref="SecurityConfig"/>）。
/// 固件手写一份，等于把"哪些位有效、每个角色有哪些位"变成两处维护 —— 编辑器加了权限位，
/// 设备端会静默不认识它（拒不拒绝都错）。所以设备端只允许 include 这份头。</para>
///
/// <para><b>固件的消费方式是 <c>__has_include</c></b>（与 <c>generated_comm.h</c> 同一套路，
/// 见 <c>comm_core.c</c>）：头缺失时固件按"设计模式：不门控"回退 —— 于是导出工程没带这份头
/// 也仍然能编译、行为与历史一致，而一旦带上就自动启用门控。</para>
///
/// <para><b>与动作门的同源关系：</b>末尾的动作→权限注释表与生成门都读
/// <see cref="ActionPermissionMap"/>；门里的宏名由 <see cref="PermissionMacros"/> 给出，
/// 因此不可能出现"头里叫 HMI_PERM_A、门里引用 HMI_PERM_B"。</para>
/// </summary>
public static class SecurityConfigCodeGen
{
    /// <summary>
    /// 预置角色清单 —— **对 <c>UserRole.BuiltInAll</c> 的转发**（唯一清单在 Models 层，
    /// 因为 Security 层与 App 层也要读它；这里若再写一遍就成了第二份）。
    /// 生成头 <c>HMI_SEC_ROLES_INIT</c> 的行序即此顺序，导出的模拟器也读它
    /// 算「预览身份 → 权限掩码」（<c>--login</c>）。
    ///
    /// <para>表达式体 ⇒ 每次调用返回**新实例**：调用方改了 <c>Name</c>/<c>Permissions</c> 不会污染别处
    /// （先前各处各写一遍 <c>new[] { BuiltInOperator, … }</c> 时天然如此，收敛后必须保持）。
    /// 两处各写一份清单的后果是「生成头的角色掩码」与「模拟器预览掩码」静默不一致 ——
    /// 那正是"预览能点、设备点不动"。</para>
    /// </summary>
    public static UserRole[] BuiltInRoles => UserRole.BuiltInAll;

    /// <summary>角色 → 权限掩码的 C 十六进制字面量（如 <c>0x04B</c>）。</summary>
    public static string MaskHex(UserRole role) => PermissionMacros.Hex(PermissionMacros.Mask(role.Permissions));

    /// <summary>
    /// 生成权限头文本。空 <paramref name="config"/> 按默认 <see cref="SecurityConfig"/> 处理。
    ///
    /// <para><paramref name="screens"/> 只服务于**会话超时的锁屏目标解析**
    /// （<see cref="LockScreenTargets"/>）：<see cref="SecurityConfig.LockScreenId"/> 是屏幕 **Id**，
    /// 而设备端 <c>hmi_screen_switch</c> 按屏幕 **名字**匹配，所以要在生成期把 Id 换成名字。
    /// 传 <c>null</c>（简化重载）时锁屏目标解析不出来 —— 设备端会**清除会话但不切屏**，
    /// 安全边界不受影响，产物里多一段诊断注释说明原因。</para>
    /// </summary>
    public static string Generate(SecurityConfig? config, IReadOnlyList<HmiScreen>? screens = null)
    {
        config ??= new SecurityConfig();

        var sb = new StringBuilder();
        sb.AppendLine("/* ============================================================");
        sb.AppendLine(" * 设备端安全 / 权限模型 —— 由 HMI Editor 生成，请勿手改");
        sb.AppendLine(" *   单一来源：Permission 枚举 + UserRole 预置角色 + HmiProject.SecurityConfig");
        sb.AppendLine(" *   消费方：firmware runtime/hmi_actions.c（__has_include 优先引用，");
        sb.AppendLine(" *           缺省回退「设计模式：不门控」）+ 生成的动作回调门 + 导出的模拟器 main.c");
        sb.AppendLine(" *           —— 三者的权限判定都调这里的 hmi_perm_allows（唯一实现）。");
        sb.AppendLine(" * ============================================================ */");
        sb.AppendLine("#ifndef HMI_GENERATED_SECURITY_H");
        sb.AppendLine("#define HMI_GENERATED_SECURITY_H");
        sb.AppendLine();

        AppendOperationMode(sb, config);
        AppendPermissions(sb);
        AppendPolicy(sb);
        AppendRoles(sb, config);
        AppendUsers(sb, config);
        AppendSessionPolicy(sb, config, screens);
        AppendAuditPolicy(sb);
        AppendActionTable(sb);

        sb.AppendLine("#endif /* HMI_GENERATED_SECURITY_H */");
        return sb.ToString();
    }

    /// <summary>运行模式 + 门控开关。</summary>
    private static void AppendOperationMode(StringBuilder sb, SecurityConfig config)
    {
        sb.AppendLine("/* ── 运行模式（OperationMode）。设计模式下所有操作可用，其余模式受权限约束 ── */");
        foreach (var mode in Enum.GetValues<OperationMode>())
            sb.AppendLine($"#define {PermissionMacros.For(mode),-26} {(int)mode}");
        sb.AppendLine();
        sb.AppendLine($"#define HMI_SEC_OPERATION_MODE {PermissionMacros.For(config.OperationMode)}  /* {config.OperationMode} */");
        sb.AppendLine();
        sb.AppendLine("/* 是否执行权限门：Designer(设计模式) 下不门控，导出产物与历史逐字节一致。");
        sb.AppendLine(" * 其余模式一律门控；调试时可用 -DHMI_SEC_ENFORCE=0 从命令行临时关掉。 */");
        sb.AppendLine("#ifndef HMI_SEC_ENFORCE");
        sb.AppendLine($"#define HMI_SEC_ENFORCE {(config.OperationMode == OperationMode.Designer ? 0 : 1)}");
        sb.AppendLine("#endif");
        sb.AppendLine();
    }

    /// <summary>权限位 —— 逐成员发射（含 None/All），值由枚举推导。</summary>
    private static void AppendPermissions(StringBuilder sb)
    {
        sb.AppendLine("/* ── 权限位（Permission 枚举）。设备端**不得**另写一份数值 ── */");
        var width = Enum.GetValues<Permission>().Max(p => PermissionMacros.For(p).Length);
        foreach (var permission in Enum.GetValues<Permission>())
            sb.AppendLine($"#define {PermissionMacros.For(permission).PadRight(width)} {PermissionMacros.Hex(PermissionMacros.Mask(permission))}");
        sb.AppendLine();
    }

    /// <summary>
    /// 权限判定 —— **唯一实现**（设备端与模拟器都调它）。
    ///
    /// <para>为什么不放在两个消费方各写一遍：这条公式含一条**语义决定**（未登录折算成「能看画面」）。
    /// 一旦模拟器侧再写一遍，两处迟早分叉，而分叉的表现是「预览里点得动、设备上点不动」——
    /// 归因成本极高。所以判定逻辑随权限位一起下沉到生成头，两个消费方都只剩薄薄一层。</para>
    ///
    /// <para><b>放在 <c>#if HMI_SEC_ENFORCE</c> 里</b>：回退分支（头缺失）刻意不提供任何权限位数值，
    /// 所以任何使用 <c>HMI_PERM_*</c> 的代码都必须被 <c>HMI_SEC_ENFORCE</c> 编译掉 —— 调用侧同样如此。</para>
    ///
    /// <para><c>static inline</c> ⇒ 每个 TU 各一份，不产生外部符号、不引入链接依赖，也不会因未使用而告警。</para>
    /// </summary>
    private static void AppendPolicy(StringBuilder sb)
    {
        sb.AppendLine("/* ── 权限判定（唯一实现）──");
        sb.AppendLine(" * 设备端 hmi_has_permission 与模拟器 hmi_require_permission 都调它，");
        sb.AppendLine(" * 谁也不许再自己写「未登录折算」或 (mask & bits) == bits。 */");
        sb.AppendLine("#if HMI_SEC_ENFORCE");
        sb.AppendLine("static inline int hmi_perm_allows(int logged_in, unsigned int perm_mask, unsigned int bits)");
        sb.AppendLine("{");
        sb.AppendLine("    if (bits == 0u) return 1;");
        sb.AppendLine("    /* 未登录：只折算「查看画面」（能浏览/换屏，不能操作控件或改配置）—— 这条语义只在这里 */");
        sb.AppendLine($"    const unsigned int mask = logged_in ? perm_mask : (unsigned int){PermissionMacros.For(Permission.ViewScreen)};");
        sb.AppendLine("    return (mask & bits) == bits;");
        sb.AppendLine("}");
        sb.AppendLine("#endif /* HMI_SEC_ENFORCE */");
        sb.AppendLine();
    }

    /// <summary>角色表（预置角色）+ 默认角色索引。</summary>
    private static void AppendRoles(StringBuilder sb, SecurityConfig config)
    {
        var roles = BuiltInRoles;

        sb.AppendLine("/* ── 角色表（UserRole 预置角色），掩码为上面权限位的或 ──");
        sb.AppendLine(" * 固件用它初始化角色数组并按 HMI_SEC_ROLE_COUNT 线性查表。 */");
        sb.AppendLine($"#define HMI_SEC_ROLE_COUNT {roles.Length}");
        sb.AppendLine("#define HMI_SEC_ROLES_INIT \\");
        for (var i = 0; i < roles.Length; i++)
        {
            var role = roles[i];
            var isLast = i == roles.Length - 1;
            // 列宽在**字段连逗号**上补齐（不是在引号内），产出的表才能既对齐又是合法 C
            var idField = $"{{ {Quote(role.Id)},".PadRight(26);
            var nameField = $"{Quote(role.Name)},".PadRight(18);
            var initializer = $"    {idField} {nameField} {role.Level,2}, {MaskHex(role)} }}";
            sb.AppendLine(isLast ? initializer : initializer + ", \\");   // 条目间逗号 + 续行符
        }
        sb.AppendLine();

        // 默认角色：按 SecurityConfig.DefaultRoleId 查表；查不到回退索引 0 —— 但要写明，不静默取默认值
        var defaultIndex = Array.FindIndex(roles, r => r.Id == config.DefaultRoleId);
        sb.AppendLine("/* 默认角色：凭据校验落地前，hmi_login 用它激活会话（见 hmi_actions.c 注释） */");
        if (defaultIndex < 0)
        {
            defaultIndex = 0;
            sb.AppendLine($"/* ⚠ SecurityConfig.DefaultRoleId = {Quote(config.DefaultRoleId)} 不在预置角色表内，已回退索引 0 */");
        }
        sb.AppendLine($"#define HMI_SEC_DEFAULT_ROLE_INDEX {defaultIndex}  /* {roles[defaultIndex].Id} */");
        // 默认角色的权限掩码 —— 模拟器没有角色表（它不查角色 Id），但 hmi_login 必须按**同一个角色**
        // 授权，否则"预览能点、设备点不动"。由生成器算好，避免模拟器侧再推一遍角色表。
        sb.AppendLine($"#define HMI_SEC_DEFAULT_ROLE_MASK {MaskHex(roles[defaultIndex])}  /* {roles[defaultIndex].Name} */");
        sb.AppendLine();
    }

    /// <summary>
    /// 运行时用户表 + 凭据策略 —— 设备登录屏 <c>hmi_login_submit</c> 的数据来源。
    ///
    /// <para><b>为什么表空也要发射 <c>HMI_SEC_USER_COUNT 0</c>：</b>固件用
    /// <c>#if HMI_SEC_USER_COUNT &gt; 0</c> 决定是否编译校验分支。若空表时不发射这个宏，
    /// 「头在、表空」这条路径下固件会引用未定义标识符（编译失败）—— 而「头不在」那条路径
    /// 由 <c>hmi_actions.h</c> 的回退块给出同名 0，两条路径语义才一致。</para>
    ///
    /// <para><b>凭据算法是跨语言契约：</b>格式 <c>"Base64Salt:Base64Hash"</c>，算法为
    /// <c>h0 = SHA256(salt ‖ utf8(pwd))</c> 后再迭代到 <c>HMI_SEC_HASH_ITERATIONS</c>。
    /// 编辑器侧唯一实现是 <c>Core/Security/RuntimePasswordHasher.cs</c>，固件侧是
    /// <c>app/runtime/hmi_crypto.c</c>；两边的同一性由**同一组固定向量**各测一遍钉住
    /// （向量取自独立实现，不是「自己算自己」）。改这里任何常量都必须同步固件与向量。</para>
    ///
    /// <para>用户可控文本会进 C 注释，因此一律走 <see cref="SafeComment"/> 掐掉 <c>*&#47;</c> ——
    /// 否则一个 App 名叫 <c>a*/b</c> 的工程能把块注释提前闭合，后面的文字直接变成代码。</para>
    /// </summary>
    private static void AppendUsers(StringBuilder sb, SecurityConfig config)
    {
        var users = config.RuntimeUsers ?? new List<RuntimeUser>();

        sb.AppendLine("/* ── 运行时用户表（SecurityConfig.RuntimeUsers）—— 设备登录屏的凭据来源 ──");
        sb.AppendLine(" * 凭据格式 \"Base64Salt:Base64Hash\"；算法 = SHA256 迭代 HMI_SEC_HASH_ITERATIONS 次，");
        sb.AppendLine(" * 起始输入为 salt ‖ utf8(password)。两侧唯一实现：");
        sb.AppendLine(" *   编辑器 Core/Security/RuntimePasswordHasher.cs");
        sb.AppendLine(" *   固件   app/runtime/hmi_crypto.c");
        sb.AppendLine(" * 同一性由**同一组固定向量**两侧各测一遍钉住，改算法必须同时改两侧与向量。");
        sb.AppendLine(" * 表为空（USER_COUNT == 0）⇒ hmi_login_submit 一律拒绝，");
        sb.AppendLine(" * hmi_login 退回「按默认角色授权」（既有行为，语义不变）。 */");
        sb.AppendLine($"#define HMI_SEC_USER_COUNT {users.Count}");
        sb.AppendLine($"#define HMI_SEC_HASH_ITERATIONS {RuntimePasswordHasher.HmiHashIterations}");
        sb.AppendLine($"#define HMI_SEC_SALT_BYTES {RuntimePasswordHasher.HmiSaltBytes}");
        sb.AppendLine($"#define HMI_SEC_PASSWORD_MIN_LEN {config.PasswordMinLength}");
        sb.AppendLine($"#define HMI_SEC_MAX_LOGIN_ATTEMPTS {config.MaxLoginAttempts}");
        sb.AppendLine($"#define HMI_SEC_LOCKOUT_MIN {config.LockoutDurationMin}");

        if (users.Count == 0)
        {
            sb.AppendLine();
            return;
        }

        // 可见的诊断注释 —— 静默略过会让「配了用户却登不上」变成一次现场排查
        var knownRoleIds = new HashSet<string>(StringComparer.Ordinal)
        {
            UserRole.BuiltInOperator.Id,
            UserRole.BuiltInEngineer.Id,
            UserRole.BuiltInAdministrator.Id,
        };
        foreach (var user in users)
        {
            var label = string.IsNullOrWhiteSpace(user.Username) ? "(空用户名)" : user.Username;
            if (string.IsNullOrWhiteSpace(user.Username))
                sb.AppendLine("/* ⚠ 存在用户名为空的条目 —— 设备端登录屏提交空用户名时 strcmp 判空串相等，会被命中 */");
            if (!knownRoleIds.Contains(user.RoleId))
                sb.AppendLine($"/* ⚠ 用户 {SafeComment(Quote(label))} 的角色 {SafeComment(Quote(user.RoleId))} 不在预置角色表内 —— 设备端登录会失败（hmi_role_lookup 返回 -1） */");
            if (string.IsNullOrEmpty(user.PasswordHash))
                sb.AppendLine($"/* ⚠ 用户 {SafeComment(Quote(label))} 的 PasswordHash 为空 —— 设备端**无法登录**该账户（不是免密放行） */");
        }

        // 刻意不做列宽对齐（角色表那种 PadRight 在这里没意义：hash 定长且很长，
        // 对齐只会让"改一个用户就要重算全表空格"变成一次无谓的 diff）
        sb.AppendLine("#define HMI_SEC_USERS_INIT \\");
        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            var isLast = i == users.Count - 1;
            var initializer =
                $"    {{ {Quote(user.Username)}, {Quote(user.RoleId)}, {Quote(user.PasswordHash)}, {(user.IsEnabled ? 1 : 0)} }}";
            sb.AppendLine(isLast ? initializer : initializer + ", \\");   // 条目间逗号 + 续行符
        }
        sb.AppendLine();
    }

    /// <summary>
    /// 会话超时策略 —— 设备端 <c>hmi_actions.c</c> 的惰性超时评估读这三个宏。
    ///
    /// <para><b>与编辑器 <c>AuthenticationService.CheckPermission</c> 逐条同构</b>（这是本段的全部价值，
    /// 三条都不能"顺手改"）：① 「有人在问权限」本身就算活动 —— 两边都**没有**输入事件钩子，
    /// 刷新活动时间的动作就发生在权限判定里（这不是为省事的妥协，而是编辑器侧既有的权威语义）；
    /// ② 判据是**严格大于**（<c>elapsed &gt; timeout</c>），设备端按毫秒整数比较，
    /// 与 <c>TotalMinutes &gt; TimeoutMinutes</c> 同义；③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定** ——
    /// 顺序不能反，先放行再登出等于把这次操作送出去了。</para>
    /// </summary>
    private static void AppendSessionPolicy(StringBuilder sb, SecurityConfig config, IReadOnlyList<HmiScreen>? screens)
    {
        var lockScreen = LockScreenTargets.Resolve(config, screens);

        sb.AppendLine("/* ── 会话超时（SecurityConfig.SessionTimeoutMin / AutoLogoutEnabled / LockScreenId）──");
        sb.AppendLine(" * 设备端 hmi_has_permission 入口做**惰性评估**，另有 hmi_session_poll() 供主循环周期评估");
        sb.AppendLine(" * （这样\"没人动\"才能在自己跳到锁屏屏上体现，而不是等下一次有人来操作才被发现）。");
        sb.AppendLine(" * 与编辑器 AuthenticationService.CheckPermission **逐条同构**，三条都不能改：");
        sb.AppendLine(" *   ① 「有人在问权限」本身就算活动（两边都没有输入事件钩子）");
        sb.AppendLine(" *   ② 判据是**严格大于** elapsed > timeout（毫秒整数比较，与 TotalMinutes > TimeoutMinutes 同义）");
        sb.AppendLine(" *   ③ 超时 ⇒ 清会话 + 跳锁屏 + **拒绝本次判定**（顺序不能反） */");
        sb.AppendLine($"#define HMI_SEC_SESSION_TIMEOUT_MIN {config.SessionTimeoutMin}  /* <= 0 表示不超时（编辑器 Minimum=1，这是手改 JSON 的兜底） */");
        sb.AppendLine($"#define HMI_SEC_AUTO_LOGOUT {(config.AutoLogoutEnabled ? 1 : 0)}  /* 0 = 关掉自动登出（编辑器 AutoLogoutEnabled） */");
        sb.AppendLine($"#define HMI_SEC_LOCK_SCREEN_NAME {Quote(lockScreen.ScreenName ?? "")}  /* {SafeComment(DescribeLockScreen(lockScreen))} */");
        if (lockScreen.Problem is { } problem)
            sb.AppendLine($"/* ⚠ {SafeComment(problem)} */");
        sb.AppendLine();
        sb.AppendLine("/* 派生开关：会话超时是否启用。**两个消费方（固件 hmi_actions.c / 导出模拟器 main.c）都读它** ——");
        sb.AppendLine(" * 各自再按 ENFORCE / AUTO_LOGOUT / TIMEOUT_MIN 算一遍必然漂移，而漂移的表现是");
        sb.AppendLine(" * 「预览会跳锁屏、设备不会」（或反之），归因成本极高。");
        sb.AppendLine(" * 放在这里而不是头文件开头：HMI_SEC_ENFORCE 可被 -DHMI_SEC_ENFORCE=0 覆盖，");
        sb.AppendLine(" * 只有在本行求值才能反映最终的 ENFORCE。 */");
        sb.AppendLine("#if HMI_SEC_ENFORCE && HMI_SEC_AUTO_LOGOUT && (HMI_SEC_SESSION_TIMEOUT_MIN > 0)");
        sb.AppendLine("#define HMI_SEC_SESSION_ENABLED 1");
        sb.AppendLine("#else");
        sb.AppendLine("#define HMI_SEC_SESSION_ENABLED 0");
        sb.AppendLine("#endif");
        sb.AppendLine();
    }

    /// <summary>锁屏宏行尾注释的说明文本（为什么是这个名字）。</summary>
    private static string DescribeLockScreen(LockScreenTargets.Resolution resolution) =>
        resolution.ScreenName is null
            ? "空 = 超时后不切屏（会话仍被清除）"
            : resolution.FromConfiguredId
                ? "由 SecurityConfig.LockScreenId 解析而来"
                : "LockScreenId 未配置 ⇒ 回退登录屏（与 hmi_login 同一张候选表）";

    /// <summary>
    /// 审计日志 —— **刻意不下发任何宏**，只留一段说明。
    ///
    /// <para><b>为什么两个字段都不下发</b>：① <see cref="SecurityConfig.EnableAuditLog"/> 是
    /// **编辑器侧的操作日志开关**（<c>AuthenticationService.RecordAudit</c>），而编辑器的语义是
    /// 「**认证事件始终记录，不受它影响**」（安全合规要求，见 <c>AuthenticationService</c> 的注释）。
    /// 设备端只有认证类审计 ⇒ 它在设备端**没有可作用的对象**，下发一个不生效的宏是误导。
    /// ② <see cref="SecurityConfig.AuditLogStoragePath"/> 是**编辑器本机**的路径
    /// （<c>AuthenticationService.AuditLogFilePath</c> 直接拿它 fopen）。把它发给设备就是**跨机路径**：
    /// 用户填 <c>D:\logs\audit.log</c> 在设备上必然写不进去，而且失败是静默的。</para>
    ///
    /// <para>设备端审计落点因此固定为 <c>/mnt/UDISK/hmi/audit.log</c>（唯一持久可写分区，与 assets /
    /// fonts / comm_config.ini 同级），由 <c>app/runtime/hmi_actions.c</c> 的 <c>HMI_SEC_AUDIT_PATH</c>
    /// 定义、可用 <c>-D</c> 覆盖（主机侧测试就是这么指到临时目录的）；
    /// 导出模拟器写自己输出目录下的 <c>audit.log</c>（模拟器不落 UDISK）。</para>
    /// </summary>
    private static void AppendAuditPolicy(StringBuilder sb)
    {
        sb.AppendLine("/* ── 审计日志（SecurityConfig.EnableAuditLog / AuditLogStoragePath）—— 刻意不下发 ──");
        sb.AppendLine(" *   1) EnableAuditLog 是**编辑器侧**的操作日志开关（AuthenticationService.RecordAudit），");
        sb.AppendLine(" *      而编辑器的语义是「**认证事件始终记录，不受它影响**」（安全合规要求）。");
        sb.AppendLine(" *      设备端只有认证类审计 ⇒ 它在设备端没有可作用的对象，下发一个不生效的宏是误导。");
        sb.AppendLine(" *   2) AuditLogStoragePath 是**编辑器本机**的路径（直接 fopen）。发给设备就是跨机路径：");
        sb.AppendLine(" *      用户填 D:\\logs\\audit.log 在设备上必然静默写不进去。");
        sb.AppendLine(" *   ⇒ 设备端落点固定 /mnt/UDISK/hmi/audit.log（见 app/runtime/hmi_actions.c 的");
        sb.AppendLine(" *      HMI_SEC_AUDIT_PATH，可用 -D 覆盖）；导出模拟器写自己输出目录下的 audit.log。 */");
        sb.AppendLine();
    }

    /// <summary>动作 → 所需权限（与生成的动作门同源）。</summary>
    private static void AppendActionTable(StringBuilder sb)
    {
        sb.AppendLine("/* ── 动作 → 所需权限（与生成的动作回调门同源：ActionPermissionMap） ──");
        sb.AppendLine(" * 未列出的动作（含 Login/Logout 与未知类型）不门控。");
        var width = ActionPermissionMap.Entries.Max(e => e.ActionType.Length);
        foreach (var (actionType, permission) in ActionPermissionMap.Entries)
        {
            // ⚠ 不要在这里写 /* … */ 注释 —— 整块本身就在块注释里，嵌套的 */ 会提前闭合它、
            // 后面几行就变成了代码（实测过一次：gcc 报 "expected identifier before '['"）。
            var perm = permission is { } p ? $"{PermissionMacros.For(p)}  ({p})" : "-  (不门控)";
            sb.AppendLine($" *   {actionType.PadRight(width)}  {perm}   [{ActionTypeMeta.DisplayName(actionType)}]");
        }
        sb.AppendLine(" */");
    }

    /// <summary>C 字符串字面量（含双引号）。</summary>
    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// 进 C **注释**的用户可控文本 —— 先把 <c>*/</c> 拆开。
    /// <para>块注释里出现一个 <c>*/</c> 会提前闭合注释，紧随其后的文字就变成了代码。
    /// 用户可控文本一律过这里；它与进字符串字面量的 <see cref="Quote"/> 是两条不同的出路，
    /// 不能混用（Quote 加了引号反而更糟：注释里出现引号不闭合，但 <c>*/</c> 照样闭合）。</para>
    /// </summary>
    private static string SafeComment(string s) => s.Replace("*/", "* /");
}
