using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 权限模型**下沉产物** <c>generated_security.h</c> 的生成契约，以及"设备端不得另写一份权限位"的守卫。
///
/// <para><b>为什么这些断言是"对拍"而不是"快照"：</b>头里的每个数值都必须能从
/// <see cref="Permission"/> / <see cref="UserRole"/> / <see cref="SecurityConfig"/> 推出来 ——
/// 所以测试不是比对一段写死的文本，而是**现算一遍再比**。这样编辑器改枚举值/角色权限后，
/// 头不跟着变就会红（快照测试只会让人顺手更新快照）。</para>
/// </summary>
public class SecurityConfigCodeGenTests
{
    // ══════════════════ 权限位 / 运行模式 ══════════════════

    [Fact]
    public void Header_EmitsEveryPermissionBit_MatchingEnum()
    {
        var defines = ParseDefines(SecurityConfigCodeGen.Generate(new SecurityConfig()));

        var expected = Enum.GetValues<Permission>().ToDictionary(
            p => PermissionMacros.For(p),
            p => PermissionMacros.Mask(p));

        foreach (var (macro, mask) in expected)
        {
            Assert.True(defines.ContainsKey(macro), $"权限头缺宏 {macro}");
            Assert.Equal(mask, ParseHex(defines[macro]));
        }

        // 不多发：HMI_PERM_ 前缀的宏数 == 枚举成员数
        Assert.Equal(expected.Count, defines.Keys.Count(k => k.StartsWith("HMI_PERM_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Header_EmitsEveryOperationMode_MatchingEnum()
    {
        var defines = ParseDefines(SecurityConfigCodeGen.Generate(new SecurityConfig()));

        foreach (var mode in Enum.GetValues<OperationMode>())
        {
            var macro = PermissionMacros.For(mode);
            Assert.True(defines.ContainsKey(macro), $"权限头缺运行模式宏 {macro}");
            Assert.Equal(((int)mode).ToString(), defines[macro]);
        }
    }

    [Theory]
    [InlineData(OperationMode.Designer, "0")]
    [InlineData(OperationMode.Operator, "1")]
    [InlineData(OperationMode.Maintenance, "1")]
    [InlineData(OperationMode.ReadOnly, "1")]
    public void Header_EnforceFlag_FollowsOperationMode(OperationMode mode, string expectedEnforce)
    {
        var config = new SecurityConfig { OperationMode = mode };
        var header = SecurityConfigCodeGen.Generate(config);
        var defines = ParseDefines(header);

        Assert.Equal(expectedEnforce, defines["HMI_SEC_ENFORCE"]);
        Assert.Equal(PermissionMacros.For(mode), defines["HMI_SEC_OPERATION_MODE"]);

        // 允许命令行覆盖（调试用）：必须包在 #ifndef 里
        Assert.Contains("#ifndef HMI_SEC_ENFORCE", header);
    }

    // ══════════════════ 角色表 ══════════════════

    [Fact]
    public void Header_RoleTable_MatchesBuiltInRoles_AndCountMatchesEntries()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig());
        var defines = ParseDefines(header);

        var expected = new[] { UserRole.BuiltInOperator, UserRole.BuiltInEngineer, UserRole.BuiltInAdministrator };
        Assert.Equal(expected.Length.ToString(), defines["HMI_SEC_ROLE_COUNT"]);

        var entries = ParseRoles(header);
        Assert.Equal(expected.Length, entries.Count);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Id, entries[i].Id);
            Assert.Equal(expected[i].Name, entries[i].Name);
            Assert.Equal(expected[i].Level, entries[i].Level);
            Assert.Equal(PermissionMacros.Mask(expected[i].Permissions), entries[i].Mask);
        }

        // 掩码必须是权限位的**或**，且非空 —— 角色表与权限位两处各自维护时最容易在这里分叉
        Assert.All(entries, e => Assert.NotEqual(0u, e.Mask));
    }

    /// <summary>
    /// <c>SecurityConfigCodeGen.BuiltInRoles</c> 必须是 <c>UserRole.BuiltInAll</c> 的**纯转发**。
    ///
    /// <para><b>为什么需要它：</b>预置角色清单现在有三个消费方 —— 生成头 <c>HMI_SEC_ROLES_INIT</c>、
    /// 导出模拟器算「预览身份 → 权限掩码」、编辑器「运行时用户」的角色下拉。
    /// 任何一处"顺手再写一遍这三个角色"都会长出静默不一致，而症状正是
    /// 「预览能登录、设备登不上」这类最费排查时间的问题。</para>
    /// </summary>
    [Fact]
    public void BuiltInRoles_IsPureForwardOfUserRoleBuiltInAll()
    {
        var forwarded = SecurityConfigCodeGen.BuiltInRoles;
        var source = UserRole.BuiltInAll;

        Assert.Equal(source.Length, forwarded.Length);
        for (var i = 0; i < source.Length; i++)
        {
            Assert.Equal(source[i].Id, forwarded[i].Id);
            Assert.Equal(source[i].Name, forwarded[i].Name);
            Assert.Equal(source[i].Level, forwarded[i].Level);
            Assert.Equal(source[i].Permissions, forwarded[i].Permissions);
        }

        // 表达式体 ⇒ 每次调用返回新实例：调用方改了返回值不该污染下一次调用。
        // （这份清单原先在各处各写一遍时天然如此；收敛成共享静态字段就会静默变质）
        var mutated = SecurityConfigCodeGen.BuiltInRoles;
        mutated[0].Name = "被改脏了";
        Assert.NotEqual("被改脏了", SecurityConfigCodeGen.BuiltInRoles[0].Name);
    }

    [Theory]
    [InlineData("role-operator", "0")]
    [InlineData("role-engineer", "1")]
    [InlineData("role-admin", "2")]
    public void Header_DefaultRoleIndex_FollowsSecurityConfig(string roleId, string expectedIndex)
    {
        var config = new SecurityConfig { DefaultRoleId = roleId };
        var defines = ParseDefines(SecurityConfigCodeGen.Generate(config));
        Assert.Equal(expectedIndex, defines["HMI_SEC_DEFAULT_ROLE_INDEX"]);
    }

    [Fact]
    public void Header_UnknownDefaultRole_FallsBackToFirst_WithVisibleWarning()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig { DefaultRoleId = "role-does-not-exist" });
        var defines = ParseDefines(header);

        Assert.Equal("0", defines["HMI_SEC_DEFAULT_ROLE_INDEX"]);
        // 静默回退会让人以为"配的角色生效了"，必须留痕
        Assert.Contains("role-does-not-exist", header);
        Assert.Contains("回退索引 0", header);
    }

    /// <summary>
    /// <c>HMI_SEC_DEFAULT_ROLE_MASK</c> 必须等于角色表里 <c>HMI_SEC_DEFAULT_ROLE_INDEX</c> 那一行的掩码。
    ///
    /// <para><b>为什么需要它：</b>固件有角色表（按 Id 线性查表拿掩码），而模拟器没有 ——
    /// 它只认这一个宏。两者一旦分叉，表现是"同一个角色：预览里点得动、设备上点不动"（或反之）。
    /// 生成器两处同源，但只有断言才能把"同源"变成"现在就是同源"。</para>
    /// </summary>
    [Theory]
    [InlineData("role-operator", 0)]
    [InlineData("role-engineer", 1)]
    [InlineData("role-admin", 2)]
    [InlineData("role-does-not-exist", 0)]   // 未知 Id 回退首行 ⇒ 掩码必须跟着回退，不能停在配置里那个
    public void Header_DefaultRoleMask_MatchesThatRoleTableRow(string roleId, int expectedIndex)
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig { DefaultRoleId = roleId });
        var defines = ParseDefines(header);
        var roles = ParseRoles(header);

        var index = int.Parse(defines["HMI_SEC_DEFAULT_ROLE_INDEX"]);
        Assert.Equal(expectedIndex, index);
        Assert.Equal(roles[index].Mask, ParseHex(defines["HMI_SEC_DEFAULT_ROLE_MASK"]));
    }

    // ══════════════════ 判定策略（唯一实现下沉） ══════════════════

    /// <summary>
    /// 判定公式 —— 含"未登录折算成查看画面"这条**语义决定** —— 只有一份，就在生成头里。
    ///
    /// <para>两个消费方（固件 <c>hmi_has_permission</c>、模拟器 <c>hmi_require_permission</c>）
    /// 都只是转发调用。各自写一份的后果是分叉，而分叉的表现是
    /// "预览里点得动、设备上点不动"，归因成本极高。</para>
    /// </summary>
    [Fact]
    public void Header_PolicySinking_IsTheOnlyImplementation()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig());
        var body = ExtractCFunctionBody(header,
            "hmi_perm_allows(int logged_in, unsigned int perm_mask, unsigned int bits)");

        CSource.AssertCode(body, "if (bits == 0u) return 1;");
        CSource.AssertCode(body, "(mask & bits) == bits");
        // 未登录折算走**权限位宏**（不是裸 0x…）：否则权限位重排后这里会静默错位
        CSource.AssertCode(body, PermissionMacros.For(Permission.ViewScreen));
        CSource.AssertNoCode(body, "0x");

        // 只在门控模式下存在：回退分支刻意不提供任何权限位，所以用它（含本函数）的代码必须被编译掉
        var guardStart = header.IndexOf("#if HMI_SEC_ENFORCE", StringComparison.Ordinal);
        var policyStart = header.IndexOf("static inline int hmi_perm_allows", StringComparison.Ordinal);
        var guardEnd = header.IndexOf("#endif /* HMI_SEC_ENFORCE */", StringComparison.Ordinal);
        Assert.True(guardStart >= 0 && policyStart > guardStart && guardEnd > policyStart,
            "hmi_perm_allows 必须被 #if HMI_SEC_ENFORCE 包住 —— 否则回退分支会引用未定义的 HMI_PERM_*");
    }

    /// <summary>
    /// 判定公式**不得**在生成头之外再出现一次：设备端与模拟器的产物、固件源码全部扫一遍。
    /// <para>这条与 <c>Header_PolicySinking_…</c> 互补：那条证明"唯一实现存在且对"，
    /// 这条证明"没有第二份"。只看前者的话，有人加回第二份实现测试也不会红。</para>
    /// </summary>
    [Fact]
    public void Policy_FormulaExistsOnlyInGeneratedHeader()
    {
        var offenders = new List<string>();
        var sentinels = new[] { "& bits", "bits &" };
        static bool HasFormula(string text, string[] sentinels)
            => sentinels.Any(s => text.Contains(s, StringComparison.Ordinal));

        var runtime = Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime");
        Assert.True(Directory.Exists(runtime), $"未找到固件运行时目录：{runtime}");
        var firmwareSources = Directory.GetFiles(runtime, "*.c").ToList();
        Assert.NotEmpty(firmwareSources);   // 扫描面自检
        foreach (var file in firmwareSources)
            if (HasFormula(File.ReadAllText(file), sentinels)) offenders.Add($"firmware/{Path.GetFileName(file)}");

        var dir = TestTempDir.NewDir("policy-single-source");
        try
        {
            var project = BuildProjectWithAction(OperationMode.Operator);
            var code = new CodeGenerator().GenerateFullProject(project);
            SimulatorProjectExporter.Export(project, code, dir);

            var exported = Directory.GetFiles(dir, "*.c").ToList();
            Assert.NotEmpty(exported);   // 扫描面自检
            foreach (var file in exported)
                if (HasFormula(File.ReadAllText(file), sentinels)) offenders.Add(Path.GetFileName(file));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }

        Assert.True(offenders.Count == 0,
            "判定公式只允许出现在生成的 generated_security.h（hmi_perm_allows）里，"
            + "以下产物/源码又写了一份（分叉风险）：" + string.Join(", ", offenders));
    }

    /// <summary>
    /// 设备端是**薄调用**：只把会话状态转发给生成的策略，不再自己算，也不再自己挑"折算哪一位"。
    /// </summary>
    [Fact]
    public void Firmware_HasPermission_DelegatesToGeneratedPolicy()
    {
        var path = Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime/hmi_actions.c");
        Assert.True(File.Exists(path), $"未找到 {path}");
        var body = ExtractCFunctionBody(File.ReadAllText(path), "int hmi_has_permission(unsigned int bits)");

        CSource.AssertCode(body, "hmi_perm_allows(s_logged_in, s_perm_mask, bits)");   // 转发，不自己算
        CSource.AssertNoCode(body, "& bits");
        CSource.AssertNoCode(body, "HMI_PERM_");   // "折算成哪一位"是策略的事，调用侧不许再决定
    }

    /// <summary>模拟器侧的权限门同样只做转发 —— 两边调的是同一个函数。</summary>
    [Fact]
    public void ExportedSimulator_PermissionGate_DelegatesToGeneratedPolicy()
    {
        var dir = TestTempDir.NewDir("policy-sim");
        try
        {
            var project = BuildProjectWithAction(OperationMode.Operator);
            var code = new CodeGenerator().GenerateFullProject(project);
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            var body = ExtractCFunctionBody(mainC, "int hmi_require_permission(unsigned int bits)");
            CSource.AssertCode(body, "hmi_perm_allows(s_sim_logged_in, s_sim_perm_mask, bits)");
            CSource.AssertNoCode(body, "& bits");

            // 策略来自生成头 ⇒ main.c 必须自己 include 它（不能指望别的 TU 传进来）
            CSource.AssertCode(mainC, "#include \"generated_security.h\"");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>
    /// 模拟器的 <c>hmi_login</c> 用生成器算好的默认角色掩码 —— 它没有角色表，不能自己再推一遍。
    /// </summary>
    [Fact]
    public void ExportedSimulator_Login_UsesGeneratedDefaultRoleMask()
    {
        var dir = TestTempDir.NewDir("policy-sim-login");
        try
        {
            var project = BuildProjectWithAction(OperationMode.Operator);
            var code = new CodeGenerator().GenerateFullProject(project);
            SimulatorProjectExporter.Export(project, code, dir);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));

            var body = ExtractCFunctionBody(mainC, "void hmi_login(void)");
            CSource.AssertCode(body, "s_sim_perm_mask = HMI_SEC_DEFAULT_ROLE_MASK;");
            CSource.AssertNoCode(mainC, "HMI_SEC_ROLES");   // 角色表是固件侧的表示，模拟器不复制
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════ 动作表（与生成门同源） ══════════════════

    [Fact]
    public void Header_ActionTable_MentionsEveryMappedAction()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig());

        foreach (var (actionType, permission) in ActionPermissionMap.Entries)
        {
            Assert.Contains(actionType, header);
            if (permission is { } p)
                Assert.Contains(PermissionMacros.For(p), header);
        }
    }

    // ══════════════════ 导出产物 ══════════════════

    /// <summary>
    /// 两个产物必须**互相配套**：头给出权限定义、生成代码引用它。只导出其中之一，
    /// 工程一编译就是未定义标识符（这正是本轮之前模拟器侧真实存在的缺口形态）。
    /// </summary>
    [Fact]
    public void Export_WritesSecurityHeader_AndGeneratedCodeUsesIt()
    {
        var dir = TestTempDir.NewDir("hmi-sec-export");
        try
        {
            var project = BuildProjectWithAction(OperationMode.Operator);
            var code = new CodeGenerator().GenerateFullProject(project);

            var result = SimulatorProjectExporter.Export(project, code, dir);

            Assert.Contains("generated_security.h", result.Files);
            var header = File.ReadAllText(Path.Combine(dir, "generated_security.h"));
            // 必须**同样传 screens**：导出器传了（锁屏目标要在生成期把 Id 解析成屏名），
            // 少传一个参数比的就是两个不同输入 —— 而这条用例的全部价值正是
            // "导出器与直接调用走的是同一个生成器、同一组参数"。
            Assert.Equal(SecurityConfigCodeGen.Generate(project.SecurityConfig, project.Screens), header);

            var generated = File.ReadAllText(Path.Combine(dir, "generated_hmi.c"));
            Assert.Contains("hmi_require_permission(HMI_PERM_OPERATE_CONTROL)", generated);
            Assert.Contains("HMI_PERM_OPERATE_CONTROL", header);   // 门引用的宏确实由头提供
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════ 守卫：设备端不得另写一份权限位 ══════════════════

    /// <summary>
    /// 固件里**不得**出现手写的 <c>#define HMI_PERM_…</c>：权限位只有编辑器一处来源。
    /// <para>唯一的允许对象是**生成物本身**（文件名 <c>generated_security.h</c>：设备工程导出目录、
    /// 以及 host 测试的 fixture —— 后者由 <c>HostTestFixture_MatchesGeneratorOutput</c> 逐字节钉住）。
    /// 扫描面排除 vendored SDK（<c>Tina-Linux/</c>、<c>Tina-SDK/</c>，含断链符号链接）。</para>
    /// <para>判据用 <see cref="PermissionMacros.LooksLikeHandWrittenDefinition"/>（容忍 <c>#  define</c>
    /// 缩进风格）—— 见 <c>HandWrittenPermissionBit_Detection_…</c>：这里曾只认 <c>"#define "</c>，
    /// 而设备端回退块自己的风格就是 <c>#  define</c>，照抄该风格手写一位权限位可完全绕过本守卫。</para>
    /// </summary>
    [Fact]
    public void Firmware_DoesNotHandWritePermissionBits()
    {
        const string GeneratedHeaderName = "generated_security.h";

        var firmwareRoot = Path.Combine(FindRepoRoot(), "firmware");
        Assert.True(Directory.Exists(firmwareRoot), $"未找到 firmware 目录：{firmwareRoot}");

        var offenders = new List<string>();
        var generatedHeaders = 0;
        foreach (var file in EnumerateFirmwareSources(firmwareRoot))
        {
            var rel = Path.GetRelativePath(firmwareRoot, file).Replace('\\', '/');
            if (rel.StartsWith("Tina-Linux/", StringComparison.Ordinal)) continue;   // vendored SDK + 产物
            if (rel.StartsWith("Tina-SDK/", StringComparison.Ordinal)) continue;     // vendored SDK（含断链）

            var isGenerated = Path.GetFileName(file) == GeneratedHeaderName;
            if (isGenerated) generatedHeaders++;

            var lineNo = 0;
            foreach (var line in ReadLinesSafely(file))
            {
                lineNo++;
                if (!PermissionMacros.LooksLikeHandWrittenDefinition(line)) continue;
                if (isGenerated) continue;   // 生成物：权限位本来就该定义在这里
                offenders.Add($"{rel}:{lineNo}");
            }
        }

        Assert.True(offenders.Count == 0,
            $"设备端不得手写权限位（唯一来源是编辑器生成的 {GeneratedHeaderName}）："
            + string.Join(", ", offenders));
        Assert.True(generatedHeaders > 0,
            $"没扫到任何 {GeneratedHeaderName} —— 守卫的扫描面可能已失效（例如 fixture 被移走）");
    }

    /// <summary>
    /// 守卫的**判据本身**要被钉住 —— 否则判据退化成"什么也匹配不到"时，上面那条守卫会永远绿。
    /// 正例覆盖各种预处理指令缩进写法（含设备端回退块在用的 <c>#  define</c>）。
    /// </summary>
    [Theory]
    [InlineData("#define HMI_PERM_VIEW_SCREEN 0x001u")]
    [InlineData("#  define HMI_PERM_OPERATE_CONTROL 0x002u")]        // 设备端回退块的风格
    [InlineData("# define HMI_PERM_NONE 0u")]
    [InlineData("#\tdefine HMI_PERM_ALL 0x1FFu")]
    [InlineData("    #  define   HMI_PERM_DEBUG_MODE 0x100u")]        // 缩进 + 多余空白
    public void HandWrittenPermissionBit_Detection_CatchesEveryIndentStyle(string line)
    {
        Assert.True(PermissionMacros.LooksLikeHandWrittenDefinition(line),
            $"这条手写权限位没被守卫判据识别：{line}");
    }

    /// <summary>反例：不该被误判的（引用宏、生成头的正常内容、注释）—— 判据宽松不等于乱抓。</summary>
    [Theory]
    [InlineData("if (!hmi_require_permission(HMI_PERM_OPERATE_CONTROL)) return;")]
    [InlineData("HMI_SEC_ROLES_INIT \\")]
    [InlineData("/* 设备端不得另写 HMI_PERM_ 数值 */")]
    [InlineData("#define HMI_SEC_ENFORCE 0")]
    [InlineData("#  define HMI_SEC_ROLE_COUNT 0")]
    [InlineData("#define HMI_SEC_DEFAULT_ROLE_INDEX (-1)")]
    public void HandWrittenPermissionBit_Detection_DoesNotOverreach(string line)
    {
        Assert.False(PermissionMacros.LooksLikeHandWrittenDefinition(line),
            $"这条不该被判成手写权限位：{line}");
    }

    /// <summary>递归枚举固件源文件；<b>跳过符号链接</b>（vendored SDK 里有断链，递归会抛）。</summary>
    private static IEnumerable<string> EnumerateFirmwareSources(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                var info = new FileInfo(entry);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;   // 符号链接：不跟随
                if (Directory.Exists(entry)) { stack.Push(entry); continue; }
                if (entry.EndsWith(".c", StringComparison.Ordinal) || entry.EndsWith(".h", StringComparison.Ordinal))
                    yield return entry;
            }
        }
    }

    /// <summary>读文件；读不动（断链/权限）就当作"没有内容"，不让守卫因为环境问题红。</summary>
    private static IEnumerable<string> ReadLinesSafely(string path)
    {
        try { return File.ReadLines(path).ToList(); }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    // ══════════════════ 运行时用户表（凭据下沉） ══════════════════

    [Fact]
    public void Header_UserTable_EmitsEveryRuntimeUser_InOrder()
    {
        var config = new SecurityConfig
        {
            RuntimeUsers =
            {
                new RuntimeUser { Username = "u1", RoleId = "role-operator", PasswordHash = "c2FsdA==:aGFzaA==" },
                new RuntimeUser
                {
                    Username = "u2", RoleId = "role-engineer", PasswordHash = "c2FsdA==:aGFzaA==", IsEnabled = false,
                },
            },
        };

        var header = SecurityConfigCodeGen.Generate(config);
        var defines = ParseDefines(header);
        var users = ParseUsers(header);

        Assert.Equal("2", defines["HMI_SEC_USER_COUNT"]);
        Assert.Equal(2, users.Count);
        Assert.Equal("u1", users[0].Username);
        Assert.Equal("role-operator", users[0].RoleId);
        Assert.True(users[0].Enabled);
        Assert.Equal("u2", users[1].Username);
        Assert.Equal("role-engineer", users[1].RoleId);
        Assert.False(users[1].Enabled);   // IsEnabled=false ⇒ 0，不能被"顺手写成 1"
    }

    /// <summary>
    /// 凭据策略常量必须与 <c>RuntimePasswordHasher</c> 的常量、<see cref="SecurityConfig"/>
    /// 的策略字段**同源**。各自维护一份的后果是"编辑器按 1000 轮算、设备按另一轮数验"——
    /// 表现是所有账户都登不上，且从日志上看不出是谁的错。
    /// </summary>
    [Fact]
    public void Header_CredentialPolicy_MatchesHasherAndConfig()
    {
        var defines = ParseDefines(SecurityConfigCodeGen.Generate(new SecurityConfig
        {
            PasswordMinLength = 9,
            MaxLoginAttempts = 4,
            LockoutDurationMin = 25,
        }));

        Assert.Equal(RuntimePasswordHasher.HmiHashIterations.ToString(), defines["HMI_SEC_HASH_ITERATIONS"]);
        Assert.Equal(RuntimePasswordHasher.HmiSaltBytes.ToString(), defines["HMI_SEC_SALT_BYTES"]);
        Assert.Equal("9", defines["HMI_SEC_PASSWORD_MIN_LEN"]);
        Assert.Equal("4", defines["HMI_SEC_MAX_LOGIN_ATTEMPTS"]);
        Assert.Equal("25", defines["HMI_SEC_LOCKOUT_MIN"]);
        Assert.Equal("1000", defines["HMI_SEC_HASH_ITERATIONS"]);   // 与实现常量一致（防"改了实现忘了头"）
    }

    /// <summary>
    /// 空表也必须发射 <c>HMI_SEC_USER_COUNT 0</c>：固件用 <c>#if HMI_SEC_USER_COUNT &gt; 0</c>
    /// 决定是否编译校验分支 —— 缺了这个宏，"头在、表空"这条路径直接编译失败
    /// （而"头不在"那条路径由 <c>hmi_actions.h</c> 回退块给出同名 0）。
    /// <para>同时**不得**发射空的 <c>HMI_SEC_USERS_INIT</c>（那会产出非法的 C 初始化器）。</para>
    /// </summary>
    [Fact]
    public void Header_EmptyUserTable_EmitsCountZero_AndNoInitializer()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig());

        Assert.Equal("0", ParseDefines(header)["HMI_SEC_USER_COUNT"]);
        Assert.DoesNotContain("HMI_SEC_USERS_INIT", header);
        Assert.Empty(ParseUsers(header));
    }

    /// <summary>
    /// 角色 Id 不在预置角色表内必须在头里**留痕**：设备端角色表只有那三个，
    /// 表外的 Id 会让该账户在设备上登录失败（<c>hmi_role_lookup</c> 返回 -1）。
    /// 静默略过 = 一次现场排查。
    /// </summary>
    [Fact]
    public void Header_UserWithUnknownRole_IsFlaggedVisibly()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig
        {
            RuntimeUsers =
            {
                new RuntimeUser { Username = "u1", RoleId = "role-typo", PasswordHash = "c2FsdA==:aGFzaA==" },
            },
        });

        Assert.Contains("role-typo", header);
        Assert.Contains("不在预置角色表内", header);
        Assert.Contains("u1", header);
    }

    /// <summary>空哈希 = 该账户**无法登录**（不是免密）—— 这条语义必须在头里写明，不该被读成"没设密码所以放行"。</summary>
    [Fact]
    public void Header_UserWithEmptyPasswordHash_IsFlaggedVisibly()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig
        {
            RuntimeUsers =
            {
                new RuntimeUser { Username = "u1", RoleId = "role-operator", PasswordHash = "" },
            },
        });

        Assert.Contains("无法登录", header);
        Assert.Contains("不是免密放行", header);
    }

    /// <summary>
    /// 用户可控文本进 C **注释**时必须掐掉 <c>*/</c>：否则一个用户名里带 <c>*/</c> 的工程
    /// 能把块注释提前闭合，其后的文字直接变成代码（产物编译失败，甚至编译进别的东西）。
    /// <para>这条同时说明"注释"与"字符串字面量"是两条不同的出路：注释路径要拆散，
    /// 字面量路径要保持原样（转义由 Quote 负责），不许混用。</para>
    /// </summary>
    [Fact]
    public void Header_UserNameContainingCommentTerminator_DoesNotCloseTheCommentEarly()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig
        {
            RuntimeUsers =
            {
                new RuntimeUser
                {
                    Username = "evil*/boom/*",
                    RoleId = "role-typo",
                    PasswordHash = "c2FsdA==:aGFzaA==",
                },
            },
        });

        // 注释路径：注释体内的 */ 已拆成 "* /"（用户名里唯一那处终止符必须被拆开）
        Assert.Contains("evil* /boom/*", header);
        // 字面量路径：用户名本身保持原样（否则 C 读回的名字就变了）
        Assert.Contains("evil*/boom/*", header);
        // 反向：不得出现"注释里带着未拆的终止符"导致提前闭合
        Assert.DoesNotContain("/* ⚠ 用户 \"evil*/", header);
    }

    /// <summary>解析用户表 INIT 条目（把解析器放在测试节里，与它服务的那组断言挨着）。</summary>
    private static readonly Regex UserEntryRegex = new(
        @"\{\s*""(?<name>[^""]*)""\s*,\s*""(?<role>[^""]*)""\s*,\s*""(?<hash>[^""]*)""\s*,\s*(?<enabled>[01])\s*\}",
        RegexOptions.Compiled);

    private static List<(string Username, string RoleId, string PasswordHash, bool Enabled)> ParseUsers(string header)
    {
        var result = new List<(string, string, string, bool)>();
        foreach (Match m in UserEntryRegex.Matches(header))
        {
            result.Add((
                m.Groups["name"].Value,
                m.Groups["role"].Value,
                m.Groups["hash"].Value,
                m.Groups["enabled"].Value == "1"));
        }
        return result;
    }

    /// <summary>
    /// host 测试用的 fixture 头必须是**生成器的真实产物** —— 否则那两个 C 测试
    /// （权限 / 凭据）测的是手抄的表，「与编辑器一致」这条就成摆设。
    /// 生成器输出一变本用例就红 ⇒ 用 <see cref="BuildFixtureConfig"/> 重算后写回 fixture。
    /// </summary>
    [Fact]
    public void HostTestFixture_MatchesGeneratorOutput()
    {
        var path = Path.Combine(FindRepoRoot(), "firmware/t113/tests/fixtures/generated_security.h");
        Assert.True(File.Exists(path), $"未找到 host 测试 fixture：{path}");

        // 带 screens：会话超时的锁屏目标要在生成期解析（LockScreenId 是 Id，固件按名字切屏）。
        // 不传 screens 的话 fixture 里 HMI_SEC_LOCK_SCREEN_NAME 恒为空串 ⇒ 固件 host 测试
        // 只能测到"超时清会话"、测不到"超时跳屏"。
        Assert.Equal(
            SecurityConfigCodeGen.Generate(BuildFixtureConfig(), BuildFixtureScreens()),
            File.ReadAllText(path));
    }

    // ══════════════════ 会话超时 / 审计策略 ══════════════════

    /// <summary>
    /// 会话超时三个宏必须来自 <see cref="SecurityConfig"/>。
    ///
    /// <para><b>这三个宏此前根本不存在</b>（模型有字段、UI 能配、生成器不发、固件不读）——
    /// 于是"配了 30 分钟自动登出、设备上永远不登出"，是一处**在用户脸上的假功能**。
    /// 本用例钉住它们真的随配置变化。</para>
    /// </summary>
    [Fact]
    public void SessionPolicy_EmitsTimeoutAutoLogoutAndLockScreenName()
    {
        var project = new HmiProject { Name = "SessionPolicy" };
        project.Screens.Add(new HmiScreen { Id = "s-lock", Name = "我的锁屏" });
        project.SecurityConfig.SessionTimeoutMin = 45;
        project.SecurityConfig.AutoLogoutEnabled = false;
        project.SecurityConfig.LockScreenId = "s-lock";

        var header = SecurityConfigCodeGen.Generate(project.SecurityConfig, project.Screens);

        Assert.Contains("#define HMI_SEC_SESSION_TIMEOUT_MIN 45", header);
        Assert.Contains("#define HMI_SEC_AUTO_LOGOUT 0", header);
        // Id → **名字**：设备端 hmi_screen_switch 按名字匹配，拿到 Id 是匹配不上的
        Assert.Contains("#define HMI_SEC_LOCK_SCREEN_NAME \"我的锁屏\"", header);
        Assert.DoesNotContain("\"s-lock\"", header);
    }

    /// <summary>
    /// 派生块 <c>HMI_SEC_SESSION_ENABLED</c> 必须存在、条件恰好由那三个宏组成、两条分支都发射、
    /// 且**只有一处**推导。
    ///
    /// <para><b>为什么不在这里断言"取值为几"：</b>那是**预处理器**的行为。文本里 <c>#if</c>
    /// 之后永远是字面的 <c>1</c>（<c>0</c> 在 <c>#else</c> 分支里），在 C# 里断言它等于几是
    /// **恒真判据**（第一版就这么错过一次）。取值正确性由固件 host 测试覆盖 ——
    /// 它真的走 <c>gcc</c> 预处理并断言 <c>HMI_SEC_SESSION_ENABLED == 1</c> + 超时行为真的发生。
    /// 两层分工：这里守"形态"，那边守"取值"。</para>
    /// </summary>
    [Fact]
    public void SessionEnabled_DerivationBlock_IsPresentWellFormedAndSingle()
    {
        var header = SecurityConfigCodeGen.Generate(new SecurityConfig())
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains(
            "#if HMI_SEC_ENFORCE && HMI_SEC_AUTO_LOGOUT && (HMI_SEC_SESSION_TIMEOUT_MIN > 0)\n"
            + "#define HMI_SEC_SESSION_ENABLED 1\n"
            + "#else\n"
            + "#define HMI_SEC_SESSION_ENABLED 0\n"
            + "#endif",
            header);

        // 只许一处推导：两处各推一遍 = 迟早漂移，而漂移的表现是"预览会跳锁屏、设备不会"
        Assert.Single(Regex.Matches(header, @"#if HMI_SEC_ENFORCE && HMI_SEC_AUTO_LOGOUT"));
    }

    /// <summary>
    /// 审计的两个字段**刻意不下发**（见 <c>AppendAuditPolicy</c> 的说明）：
    /// ① <c>EnableAuditLog</c> 是编辑器侧的操作日志开关，而编辑器的语义是
    /// 「认证事件始终记录、不受它影响」，设备端只有认证类审计 ⇒ 下发一个不生效的宏是误导；
    /// ② <c>AuditLogStoragePath</c> 是编辑器**本机**路径，发给设备就是跨机路径（必然静默写不进去）。
    ///
    /// <para>本用例守的是"将来别好心把它们补上"—— 同时反向守"字段本身没被删掉"。</para>
    /// </summary>
    [Fact]
    public void AuditPolicy_IsDeliberatelyNotEmitted_ButExplained()
    {
        var config = new SecurityConfig
        {
            EnableAuditLog = true,
            AuditLogStoragePath = @"D:\logs\audit.log",
        };

        var header = SecurityConfigCodeGen.Generate(config);

        Assert.DoesNotContain("#define HMI_SEC_AUDIT_LOG", header);
        Assert.DoesNotContain("#define HMI_SEC_AUDIT_PATH", header);
        // 反向自检：不是靠"字段被删了"过的 —— 说明块里必须提到它们、以及设备端的真实落点
        Assert.True(config.EnableAuditLog);
        Assert.Contains("AuditLogStoragePath", header);
        Assert.Contains("/mnt/UDISK/hmi/audit.log", header);
    }

    /// <summary>
    /// 锁屏目标解析不出来时，生成头里**必须留下可见的诊断注释** ——
    /// 否则"配了锁屏却不跳"会变成一次现场排查（静默失败），而那正是本轮在修的问题类型。
    /// </summary>
    [Fact]
    public void UnresolvableLockScreen_LeavesVisibleDiagnosis_AndEmptyName()
    {
        var config = new SecurityConfig { LockScreenId = "screen-gone" };
        var screens = new List<HmiScreen> { new() { Id = "s1", Name = "Main" } };

        var header = SecurityConfigCodeGen.Generate(config, screens);

        Assert.Contains("/* ⚠ SecurityConfig.LockScreenId", header);
        Assert.Contains("screen-gone", header);
        // 解析不出来 ⇒ 空串 ⇒ 设备端不切屏（但**仍然清会话**，安全边界不受影响）
        Assert.Contains("#define HMI_SEC_LOCK_SCREEN_NAME \"\"", header);
    }

    /// <summary>
    /// host 测试 fixture 的工程配置 —— **两个 host 测试（权限 + 凭据）共用的输入**。
    ///
    /// <para>用户表的哈希用 <see cref="RuntimePasswordHasher.HashWithSalt"/> 现算而不是写字面量：
    /// 于是"fixture 里的哈希"与"生成器会产出的哈希"由构造保证一致；而哈希算法本身的正确性
    /// 由 <c>RuntimePasswordHasherTests</c> 向 Python 独立真值收敛。算法一改，fixture 立刻红
    /// ⇒ 提醒重新生成，而不是让设备端静默登不上。</para>
    ///
    /// <para>口令与 salt 取自 <see cref="RuntimeCredentialVectors"/> —— 固件侧
    /// <c>hmi_actions_credential_test.c</c> 用的是**同一组**。</para>
    /// </summary>
    private static SecurityConfig BuildFixtureConfig() => new()
    {
        OperationMode = OperationMode.Operator,
        DefaultRoleId = "role-operator",
        RuntimeUsers =
        {
            new RuntimeUser
            {
                Username = RuntimeCredentialVectors.UserA,
                DisplayName = "Operator",
                RoleId = "role-operator",
                PasswordHash = RuntimePasswordHasher.HashWithSalt(
                    RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.SaltA),
            },
            new RuntimeUser
            {
                Username = RuntimeCredentialVectors.UserB,
                DisplayName = "Engineer",
                RoleId = "role-engineer",
                PasswordHash = RuntimePasswordHasher.HashWithSalt(
                    RuntimeCredentialVectors.PasswordB, RuntimeCredentialVectors.SaltB),
            },
            new RuntimeUser
            {
                Username = RuntimeCredentialVectors.UserDisabled,
                DisplayName = "Disabled",
                RoleId = "role-operator",
                // 与 UserA 同一份凭据（同 salt、同口令）—— 见 RuntimeCredentialVectors 的说明：
                // 这样"停用被拒"用例提交的是**正确**口令，纯粹测 enabled 语义
                PasswordHash = RuntimePasswordHasher.HashWithSalt(
                    RuntimeCredentialVectors.PasswordDisabled, RuntimeCredentialVectors.SaltA),
                IsEnabled = false,
            },
        },
        // 锁屏屏：会话超时后跳到这里。**必须给** —— 不给的话 fixture 里
        // HMI_SEC_LOCK_SCREEN_NAME 恒为空串，固件 host 测试就测不到"超时会跳屏"这条路径。
        LockScreenId = "screen-lock",
    };

    /// <summary>
    /// fixture 的屏幕清单 —— 供**生成期解析锁屏目标**（<see cref="LockScreenTargets"/>）用。
    ///
    /// <para>刻意让 Id 与屏名**不同名**（Id <c>screen-lock</c> / 名字 <c>锁屏</c>）：
    /// 若两者同名，"拿到了 Id 却当成名字用"这个 bug 会被静默放过 —— 而这正是这条链最容易出的错
    /// （生成期解析的全部价值就在"Id ≠ 名字"）。</para>
    ///
    /// <para>另外带上 <c>login</c> 屏：它让"未配 LockScreenId 时回退登录屏"这条分支
    /// 在 host 测试里是可构造的（见 <c>hmi_actions_session_test.c</c>）。</para>
    /// </summary>
    private static List<HmiScreen> BuildFixtureScreens() =>
    [
        new HmiScreen { Id = "screen-main", Name = "Main" },
        new HmiScreen { Id = "screen-lock", Name = "锁屏" },
        new HmiScreen { Id = "screen-login", Name = "login" },
    ];

    /// <summary>
    /// fixture 下发的哈希必须真的能被校验器用**同一组口令**验过 ——
    /// 这条把"生成器下发什么"与"C 侧拿什么去验"接上，否则两边可以各自自洽却互不兼容。
    /// </summary>
    [Fact]
    public void FixtureConfig_HashesVerifyWithTheirPlainPasswords()
    {
        var config = BuildFixtureConfig();

        Assert.True(RuntimePasswordHasher.Verify(
            RuntimeCredentialVectors.PasswordA, config.RuntimeUsers[0].PasswordHash));
        Assert.True(RuntimePasswordHasher.Verify(
            RuntimeCredentialVectors.PasswordB, config.RuntimeUsers[1].PasswordHash));
        Assert.False(RuntimePasswordHasher.Verify("not-the-password", config.RuntimeUsers[0].PasswordHash));
    }

    /// <summary>
    /// 固件必须**消费**生成头（__has_include 优先 + 缺省回退），否则上面的守卫只是空转。
    /// <para>引用与回退只允许出现在 <c>hmi_actions.h</c> 一处 —— 否则"缺省语义"会长出第二份。</para>
    /// </summary>
    [Fact]
    public void Firmware_ConsumesGeneratedHeader_WithFallback()
    {
        var runtime = Path.Combine(FindRepoRoot(), "firmware/t113/app/runtime");
        var headerPath = Path.Combine(runtime, "hmi_actions.h");
        Assert.True(File.Exists(headerPath), $"未找到 {headerPath}");
        var header = File.ReadAllText(headerPath);

        Assert.Contains("__has_include(\"generated_security.h\")", header);
        Assert.Contains("include \"generated_security.h\"", header);   // 缩进形式不限（# 后可有空格）

        // 缺省回退必须是"设计模式：不门控"，且**不定义任何权限位**
        var fallback = header[header.IndexOf("#ifndef HMI_SEC_ENFORCE", StringComparison.Ordinal)..];
        Assert.Contains("define HMI_SEC_ENFORCE 0", fallback);
        Assert.DoesNotContain("#define HMI_PERM_", fallback);

        // 实现文件不得再写一份（避免"两个地方各自回退"）
        var impl = File.ReadAllText(Path.Combine(runtime, "hmi_actions.c"));
        Assert.DoesNotContain("__has_include", impl);
        Assert.DoesNotContain("#ifndef HMI_SEC_ENFORCE", impl);
    }

    // ══════════════════ 辅助 ══════════════════

    /// <summary>
    /// 取某个 C 函数的**函数体**（从签名后的第一个 <c>{</c> 到配平的 <c>}</c>）。
    /// <para>判据必须落在函数体内：整份文件都在射程里的话，"别的地方恰好有一行相似的调用"
    /// 就能让断言绿 —— 那就不是在测这个函数了。</para>
    /// </summary>
    private static string ExtractCFunctionBody(string source, string signatureFragment)
    {
        var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
        Assert.True(start >= 0, $"没找到函数签名：{signatureFragment}");
        var open = source.IndexOf('{', start);
        Assert.True(open > start, $"签名后没有函数体：{signatureFragment}");

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return source[open..(i + 1)];
            }
        }
        throw new InvalidOperationException($"{signatureFragment} 的花括号不配平");
    }

    private static HmiProject BuildProjectWithAction(OperationMode mode)
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        project.SecurityConfig.OperationMode = mode;

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var button = new HmiControl { Id = "c1", Name = "StartBtn", Type = HmiControlType.Button };
        button.Actions.Add(HmiAction.ToggleBit("v_motor"));
        screen.Controls.Add(button);
        project.Screens.Add(screen);
        return project;
    }

    /// <summary>解析 <c>#define NAME VALUE</c>（只取单行形式；续行宏的条目另有解析器）。</summary>
    private static Dictionary<string, string> ParseDefines(string header)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in header.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("#define ", StringComparison.Ordinal)) continue;
            var rest = line["#define ".Length..];
            rest = StripComment(rest);
            var space = rest.IndexOf(' ');
            if (space <= 0) continue;
            map[rest[..space]] = rest[(space + 1)..].Trim();
        }
        return map;
    }

    /// <summary>去掉行尾注释（生成头会给数值追加 <c>/* 说明 */</c>，解析时必须剥掉）。</summary>
    private static string StripComment(string text)
    {
        var block = text.IndexOf("/*", StringComparison.Ordinal);
        if (block >= 0) text = text[..block];
        var line = text.IndexOf("//", StringComparison.Ordinal);
        if (line >= 0) text = text[..line];
        return text.TrimEnd();
    }

    private static readonly Regex RoleEntryRegex = new(
        @"\{\s*""(?<id>[^""]+)""\s*,\s*""(?<name>[^""]+)""\s*,\s*(?<level>-?\d+)\s*,\s*(?<mask>0x[0-9A-Fa-f]+)u\s*\}",
        RegexOptions.Compiled);

    /// <summary>解析角色表 INIT 条目（按出现顺序）。</summary>
    private static List<(string Id, string Name, int Level, uint Mask)> ParseRoles(string header)
    {
        var result = new List<(string, string, int, uint)>();
        foreach (Match m in RoleEntryRegex.Matches(header))
        {
            result.Add((
                m.Groups["id"].Value,
                m.Groups["name"].Value,
                int.Parse(m.Groups["level"].Value),
                ParseHex(m.Groups["mask"].Value)));
        }
        return result;
    }

    private static uint ParseHex(string literal)
    {
        var text = literal.Trim().TrimEnd('u', 'U');
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(text[2..], 16)
            : uint.Parse(text);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"向上未找到仓库根（含 HmiEditor.slnx）。起点：{AppContext.BaseDirectory}");
    }
}
