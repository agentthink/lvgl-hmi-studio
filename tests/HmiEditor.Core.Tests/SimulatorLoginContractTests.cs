using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 模拟器登录契约 —— 生成的 <c>main.c</c> 与生成的 <c>generated_security.h</c> 必须讲**同一个故事**。
///
/// <para><b>为什么单开一个类：</b><see cref="SimulatorLinkContractTests"/> 只管"符号有没有定义"，
/// 管不了"值对不对"。模拟器这一侧的登录有一条最难发现的失效形态 ——
/// <b>预览用的权限掩码与设备端不一致</b>：两边都从同一份用户/角色配置派生，但只要
/// 中间多了一次"自己再推一遍角色表"，就会出现"预览里点得动、设备上点不动"，
/// 而两边的编译、单元测试全绿。</para>
///
/// <para>所以这里的判据不是把期望掩码抄进测试，而是**从生成头的角色表重新推一遍**，
/// 再与 main.c 里的预览表比对 —— 抄期望值挡不住"两处一起改错"。</para>
/// </summary>
public class SimulatorLoginContractTests
{
    // ── 语料 ──

    /// <summary>三个用户：op（启用）、eng（启用）、dis（**停用**，用于验证预览表也遵守「停用不能登录」）。</summary>
    private static HmiProject BuildProjectWithUsers()
    {
        var project = new HmiProject { Name = "SimLogin", LvglVersion = "9.2" };
        project.SecurityConfig.OperationMode = OperationMode.Operator;

        project.SecurityConfig.RuntimeUsers.Add(new RuntimeUser
        {
            Username = "op", RoleId = UserRole.BuiltInOperator.Id,
            PasswordHash = RuntimePasswordHasher.HashWithSalt("op1234", RuntimeCredentialVectors.SaltA),
            IsEnabled = true,
        });
        project.SecurityConfig.RuntimeUsers.Add(new RuntimeUser
        {
            Username = "eng", RoleId = UserRole.BuiltInEngineer.Id,
            PasswordHash = RuntimePasswordHasher.HashWithSalt("eng1234", RuntimeCredentialVectors.SaltB),
            IsEnabled = true,
        });
        project.SecurityConfig.RuntimeUsers.Add(new RuntimeUser
        {
            Username = "dis", RoleId = UserRole.BuiltInOperator.Id,
            PasswordHash = RuntimePasswordHasher.HashWithSalt("op1234", RuntimeCredentialVectors.SaltA),
            IsEnabled = false,   // 停用：设备端与模拟器都必须登不进
        });

        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(main);

        var login = new HmiScreen { Id = "s2", Name = "登录屏", Width = 800, Height = 480 };
        login.Controls.Add(new HmiControl { Id = "u1", Name = "user", Type = HmiControlType.TextArea });
        login.Controls.Add(new HmiControl
        {
            Id = "p1", Name = "pass", Type = HmiControlType.TextArea, PasswordMode = true,
        });
        login.Controls.Add(new HmiControl { Id = "btn", Name = "LoginBtn", Type = HmiControlType.Button });
        project.Screens.Add(login);
        return project;
    }

    /// <summary>导出模拟器工程，返回 (main.c, generated_security.h) 的文本。</summary>
    private static (string MainC, string SecurityHeader) ExportSimulator(HmiProject project)
    {
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var dir = TestTempDir.NewDir("sim-login-contract");
        try
        {
            SimulatorProjectExporter.Export(project, code, dir);
            return (File.ReadAllText(Path.Combine(dir, "main.c")),
                    File.ReadAllText(Path.Combine(dir, "generated_security.h")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 解析（判据取自生成产物本身，不是抄期望值）──

    /// <summary>
    /// 从生成头解析：角色 Id → 权限掩码字面量（如 <c>0x04B</c>）。
    /// 掩码带 unsigned 后缀（<c>0x04Bu</c>）—— 比较时归一化掉，让判据只比数值。
    /// </summary>
    private static Dictionary<string, string> ParseRoleMasks(string securityHeader)
    {
        var rx = new Regex(@"\{\s*""(?<id>[^""]*)""\s*,\s*""[^""]*""\s*,\s*\d+\s*,\s*(?<mask>0x[0-9A-Fa-f]+)[uU]?\s*\}");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in rx.Matches(securityHeader))
            result[m.Groups["id"].Value] = m.Groups["mask"].Value;
        return result;
    }

    /// <summary>从生成头解析：用户名 → (角色 Id, 是否启用)。</summary>
    private static Dictionary<string, (string RoleId, bool Enabled)> ParseUsers(string securityHeader)
    {
        var rx = new Regex(@"\{\s*""(?<name>[^""]*)""\s*,\s*""(?<role>[^""]*)""\s*,\s*""[^""]*""\s*,\s*(?<en>[01])\s*\}");
        var result = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
        foreach (Match m in rx.Matches(securityHeader))
            result[m.Groups["name"].Value] = (m.Groups["role"].Value, m.Groups["en"].Value == "1");
        return result;
    }

    /// <summary>从 main.c 的预览身份表解析：用户名 → 掩码字面量。</summary>
    private static Dictionary<string, string> ParsePreviewLogins(string mainC)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var at = mainC.IndexOf("s_sim_preview_logins", StringComparison.Ordinal);
        if (at < 0) return result;
        var eq = mainC.IndexOf("] = {", at, StringComparison.Ordinal);
        if (eq < 0) return result;
        var open = eq + "] = {".Length - 1;                       // 指向初始化列表的 '{'
        var close = mainC.IndexOf("};", open, StringComparison.Ordinal);
        var block = close < 0 ? mainC[open..] : mainC[open..close];

        var rx = new Regex(@"\{\s*""(?<user>[^""]*)""\s*,\s*(?<mask>0x[0-9A-Fa-f]+)[uU]?\s*\}");
        foreach (Match m in rx.Matches(block))
            result[m.Groups["user"].Value] = m.Groups["mask"].Value;
        return result;
    }

    /// <summary>取函数体文本（按花括号配对；C 的 <c>#if</c> 不影响配对）。</summary>
    private static string FunctionBody(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"生成产物里找不到函数签名：{signature}");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[(open + 1)..i];
        }
        Assert.Fail($"函数体花括号不平衡：{signature}");
        return "";
    }

    // ── 判据 ──

    /// <summary>
    /// 预览身份表的掩码必须**由生成头的角色表重新推得**。
    /// 这是"预览能点、设备点不动"的直接守卫。
    /// </summary>
    [Fact]
    public void PreviewLoginMasks_MatchRoleTableInGeneratedHeader()
    {
        var project = BuildProjectWithUsers();
        var (mainC, header) = ExportSimulator(project);

        var roleMasks = ParseRoleMasks(header);
        var users = ParseUsers(header);
        var preview = ParsePreviewLogins(mainC);

        // 扫描面自检：两侧都真的解析出东西，否则"空 == 空"会假绿
        Assert.True(roleMasks.Count >= 3, $"生成头里只解析出 {roleMasks.Count} 个角色掩码 —— 语料或正则失效");
        Assert.True(users.Count >= 3, $"生成头里只解析出 {users.Count} 个用户 —— 语料或正则失效");
        Assert.Equal(2, preview.Count);   // op + eng（dis 停用，不应出现）

        var checkedEnabled = 0;
        foreach (var (name, (roleId, enabled)) in users)
        {
            if (!enabled) continue;   // 停用账户由 PreviewLogins_ExcludeDisabledAccounts 单独守
            checkedEnabled++;
            Assert.True(roleMasks.ContainsKey(roleId), $"角色表里没有 {roleId}");
            Assert.True(preview.TryGetValue(name, out var mask), $"预览身份表里应有启用用户 {name}");
            Assert.Equal(roleMasks[roleId], mask);   // 掩码必须与生成头的角色表一致
        }

        Assert.Equal(2, checkedEnabled);   // 语料自检：启用用户恰好 2 个（op/eng）
    }

    /// <summary>
    /// 停用账户**不得**出现在预览身份表里 —— 设备端 `!enabled` 直接拒绝，
    /// 若模拟器把停用账户也放进去，预览就成了"停用还能进"的假象。
    /// </summary>
    [Fact]
    public void PreviewLogins_ExcludeDisabledAccounts()
    {
        var project = BuildProjectWithUsers();
        var (mainC, _) = ExportSimulator(project);

        var preview = ParsePreviewLogins(mainC);

        Assert.DoesNotContain("dis", preview.Keys);   // 停用
        Assert.Contains("op", preview.Keys);
        Assert.Contains("eng", preview.Keys);
    }

    /// <summary>角色不在预置角色表内的用户同样进不了预览表（设备端 <c>hmi_role_lookup</c> 也返回 -1）。</summary>
    [Fact]
    public void PreviewLogins_ExcludeUsersWithUnknownRole()
    {
        var project = BuildProjectWithUsers();
        project.SecurityConfig.RuntimeUsers.Add(new RuntimeUser
        {
            Username = "ghost", RoleId = "role-does-not-exist",
            PasswordHash = RuntimePasswordHasher.HashWithSalt("ghost123", RuntimeCredentialVectors.SaltA),
            IsEnabled = true,
        });

        var (mainC, _) = ExportSimulator(project);

        Assert.DoesNotContain("ghost", ParsePreviewLogins(mainC).Keys);
    }

    /// <summary>
    /// 有运行时用户表时，<c>hmi_login</c> 里的"按默认角色自动授权"必须落在
    /// <c>#if HMI_SEC_USER_COUNT == 0</c> 分支内。
    ///
    /// <para>这一条守的是**最危险的回归**：自动授权一旦跑到分支外，
    /// 登录屏上点一下就以默认角色进入会话，凭据校验被完全绕过 ——
    /// 而产物照样编译、测试照样绿。判据钉的是"它被条件包住"这件结构事实。</para>
    /// </summary>
    [Fact]
    public void HmiLogin_AutoGrant_IsConditionalOnEmptyUserTable()
    {
        var project = BuildProjectWithUsers();
        var (mainC, _) = ExportSimulator(project);

        var body = FunctionBody(mainC, "void hmi_login(void)");
        var grant = body.IndexOf("s_sim_logged_in = 1;", StringComparison.Ordinal);
        Assert.True(grant > 0, "hmi_login 里找不到授权语句 —— 语料或实现已变");

        // 授权语句之前最近的预处理分支指令必须正是「用户表为空」
        var before = body[..grant];
        var lastIf = before.LastIndexOf("#if ", StringComparison.Ordinal);
        var lastElse = before.LastIndexOf("#else", StringComparison.Ordinal);
        Assert.True(lastIf >= 0, "授权语句没有被任何 #if 包住 —— 无条件自动授权（凭据校验被绕过）");
        Assert.True(lastIf > lastElse, "授权语句落在 #else 分支里 —— 条件反了");
        Assert.Contains("HMI_SEC_USER_COUNT == 0", before[lastIf..]);
    }

    /// <summary>
    /// 模拟器工程必须给 <c>hmi_login_submit</c> 提供**定义**（生成的登录按钮回调会调它）。
    /// 判据从生成模块的 extern 声明反推 —— 与 <see cref="SimulatorLinkContractTests"/> 同一套路，
    /// 这里额外钉住"定义在 main.c 里"，因为模拟器的宿主就是它自己。
    /// </summary>
    [Fact]
    public void MainC_DefinesCredentialSubmitEntry_ReferencedByGeneratedModule()
    {
        var project = BuildProjectWithUsers();
        var module = new CodeGenerator().GenerateSimulatorModule(project);
        var (mainC, _) = ExportSimulator(project);

        Assert.Contains("hmi_login_submit", module);   // 生成模块确实引用了它
        Assert.Matches(
            new Regex(@"int\s+hmi_login_submit\s*\([^;{}]*\)\s*\{"),
            mainC);                                     // main.c 里是定义，不是又一条声明
    }

    /// <summary>
    /// 没有运行时用户表的工程：预览表不发射、计数为 0，
    /// <c>hmi_login</c> 仍是"切登录屏 + 按默认角色授权"（**既有行为，产物不变**）。
    /// </summary>
    [Fact]
    public void WithoutUserTable_PreviewTableIsEmpty_AndHmiLoginKeepsLegacyBehaviour()
    {
        var project = new HmiProject { Name = "NoUsers", LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });

        var (mainC, _) = ExportSimulator(project);

        Assert.Contains("#define SIM_PREVIEW_LOGIN_COUNT 0", mainC);
        // 空表不发射**数组声明**（函数体里对 s_sim_preview_logins[i] 的引用仍在，
        // 但被 #if SIM_PREVIEW_LOGIN_COUNT > 0 编译掉 —— 所以判据看的是声明）
        Assert.DoesNotContain("s_sim_preview_logins[SIM_PREVIEW_LOGIN_COUNT] = {", mainC);

        var body = FunctionBody(mainC, "void hmi_login(void)");
        Assert.Contains("s_sim_logged_in = 1;", body);           // 旧行为仍在（但只在 USER_COUNT==0 分支里）
        Assert.Contains("HMI_SEC_DEFAULT_ROLE_MASK", mainC);
    }

    /// <summary>
    /// 预览身份只认「当前工程里真实存在且启用」的用户：传一个不存在的名字，
    /// 模拟器必须**不建立会话**（而不是宽容放行）。
    ///
    /// <para>判据：授权语句必须排在查表失败的提示语**之前** —— 若它落在后面（或压根不在
    /// 查表成功分支里），说明"没找到用户"这条路径也会建立会话，即 <c>--login 随便什么</c> 都能进。</para>
    /// </summary>
    [Fact]
    public void PreviewLogin_UnknownName_PathDoesNotGrantSession()
    {
        var project = BuildProjectWithUsers();
        var (mainC, _) = ExportSimulator(project);

        var body = FunctionBody(mainC, "static void sim_preview_login(void)");
        var grant = body.IndexOf("s_sim_logged_in = 1;", StringComparison.Ordinal);
        var notFound = body.IndexOf("用户表里没有这个用户", StringComparison.Ordinal);

        Assert.True(grant > 0, "sim_preview_login 里找不到授权语句 —— 语料或实现已变");
        Assert.True(notFound > 0, "找不到「没找到用户」的提示语 —— 语料或实现已变");
        Assert.True(grant < notFound,
            "授权语句排在「没找到用户」提示之后 —— 未知用户名也会建立会话（--login 随便填都能进）");
    }
}
