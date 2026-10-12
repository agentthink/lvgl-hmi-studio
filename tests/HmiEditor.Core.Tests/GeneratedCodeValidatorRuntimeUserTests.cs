using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 导出前的**运行时用户表**拦截 —— 补齐「校验器存在、但导出不拦」这条防线缺口。
///
/// <para><b>为什么这不是"再多一条提示"：</b>设备端 <c>hmi_login()</c> 在
/// <c>HMI_SEC_USER_COUNT &gt; 0</c> 时**刻意不授权**（会话只能由登录屏经
/// <c>hmi_login_submit</c> 建立）。于是表里一条可用账户都没有时，任何人都不可能建立会话，
/// 所有带权限门的动作<b>永久拒绝</b> —— 产物烧到设备上表现为「HMI 点了没反应」。
/// 面板里的红字在导出后就被忘了，所以必须由导出前的 <see cref="GeneratedCodeValidator"/> 拦。</para>
///
/// <para><b>断言类别 + 严重级别，不断言文案：</b>文案会改，定级与类别才是契约。</para>
/// </summary>
public class GeneratedCodeValidatorRuntimeUserTests
{
    private const string SomeHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    /// <summary>内容无关的最小合法代码：含 create_screen_、无未知 lv_ 调用、无 WARNING 残留。</summary>
    private const string CleanCode = "void create_screen_main(void) { }";

    private static RuntimeUser User(
        string username,
        string roleId = "role-operator",
        string passwordHash = SomeHash,
        bool enabled = true) => new()
        {
            Username = username,
            RoleId = roleId,
            PasswordHash = passwordHash,
            IsEnabled = enabled,
        };

    private static HmiProject Project(params RuntimeUser[] users)
    {
        var project = new HmiProject();
        // 结构校验要求至少有一个屏幕；不加屏幕会让 ValidateStructure 报 Error，
        // 从而把下面所有 HasErrors 断言变成"因为没屏幕而红"，与本轮规则无关。
        // 注意 ScreenFactory.Create 只**造**屏幕、不加入工程（调用方负责），别漏 Add。
        project.Screens.Add(ScreenFactory.Create(project, name: "main"));
        project.SecurityConfig.RuntimeUsers = users.ToList();
        return project;
    }

    private static IReadOnlyList<GeneratedCodeFinding> RuntimeUserFindings(HmiProject project) =>
        GeneratedCodeValidator.Validate(project, CleanCode)
            .Where(f => f.Category == FindingCategory.RuntimeUser)
            .ToList();

    // ══════════════════ 不回归：未配用户表 / 健康表 ══════════════════

    [Fact]
    public void NoRuntimeUsers_ProducesNoRuntimeUserFindings()
    {
        // 表为空 = "没配运行时用户表"的既有形态，设备端按默认角色授权。
        // 这里若报错会拦下所有老工程 —— 行为必须与本次改动前一致。
        Assert.Empty(RuntimeUserFindings(new HmiProject()));
    }

    [Fact]
    public void HealthyTable_ProducesNoRuntimeUserFindings()
    {
        var project = Project(User("op", "role-operator"), User("eng", "role-engineer", enabled: true));

        Assert.Empty(RuntimeUserFindings(project));
        // 顺带证明测试夹具本身是"干净"的（否则下面的拦截断言可能被无关 Error 带成恒真）
        Assert.Empty(GeneratedCodeValidator.Validate(project, CleanCode));
    }

    // ══════════════════ 单条账户：永远登不上 ⇒ Error（导出拦下） ══════════════════

    [Fact]
    public void UnknownRole_IsError_AndBlocksExport()
    {
        // 配一条可用账户，避免整表规则同时触发 —— 本用例只验单条定级
        var project = Project(User("ghost", "role-nonexistent"), User("op"));
        var findings = RuntimeUserFindings(project);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal("SecurityConfig.RuntimeUsers[0]", finding.Location);
        Assert.True(GeneratedCodeValidator.HasErrors(GeneratedCodeValidator.Validate(project, CleanCode)),
            "角色表外 ⇒ 口令对了也登不上，必须阻断导出");
    }

    [Fact]
    public void EnabledWithoutPassword_IsError_AndBlocksExport()
    {
        var project = Project(User("nopass", passwordHash: ""), User("op"));
        var findings = RuntimeUserFindings(project);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal("SecurityConfig.RuntimeUsers[0]", finding.Location);
        Assert.True(GeneratedCodeValidator.HasErrors(GeneratedCodeValidator.Validate(project, CleanCode)));
    }

    [Fact]
    public void DisabledWithoutPassword_IsNotReported()
    {
        // 停用账户在设备端直接拒绝，无密码与否不影响可达性（与 RuntimeUserValidator 同口径）
        var project = Project(User("op"), User("retired", passwordHash: "", enabled: false));

        Assert.Empty(RuntimeUserFindings(project));
    }

    // ══════════════════ 单条账户：不会让谁都进不去 ⇒ Warning ══════════════════

    [Fact]
    public void EmptyUsername_IsWarning_DoesNotBlockExport()
    {
        var project = Project(User(""), User("op"));
        var findings = RuntimeUserFindings(project);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.False(GeneratedCodeValidator.HasErrors(GeneratedCodeValidator.Validate(project, CleanCode)));
    }

    [Fact]
    public void DuplicateUsername_IsWarning_AndPointsAtTheLaterEntry()
    {
        // 设备端首个匹配胜出 ⇒ 只有后面那条是无效的，位置必须点在 [1] 而不是 [0]
        var project = Project(User("op"), User("op"));
        var findings = RuntimeUserFindings(project);

        var finding = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("SecurityConfig.RuntimeUsers[1]", finding.Location);
    }

    // ══════════════════ 整表体检：一条可用都没有 ⇒ Error（本项的核心） ══════════════════

    [Fact]
    public void TableWithNoUsableAccount_IsError_EvenWhenEachEntryLooksConfigured()
    {
        // 每条都"有用户名有密码"，但全部停用 ⇒ 目标平台上谁都进不去。
        // 这是单条规则**看不见**的形态：四条规则各自都不报（停用不算问题）。
        var project = Project(
            User("op", enabled: false),
            User("eng", enabled: false));

        var findings = RuntimeUserFindings(project);

        var aggregate = Assert.Single(findings);
        Assert.Equal(FindingSeverity.Error, aggregate.Severity);
        Assert.Equal("SecurityConfig.RuntimeUsers", aggregate.Location);
        Assert.Contains("没有一条可用", aggregate.Message);
        Assert.True(GeneratedCodeValidator.HasErrors(GeneratedCodeValidator.Validate(project, CleanCode)));
    }

    [Fact]
    public void TableWithNoUsableAccount_AllUnknownRoles_ReportsBothPerEntryAndAggregate()
    {
        // 两条都角色表外 ⇒ 既有两条单条 Error，也有一条整表 Error（信息不互相吞掉）
        var project = Project(User("a", "role-x"), User("b", "role-y"));
        var findings = RuntimeUserFindings(project);

        Assert.Equal(3, findings.Count);
        Assert.All(findings, f => Assert.Equal(FindingSeverity.Error, f.Severity));
        Assert.Contains(findings, f => f.Location == "SecurityConfig.RuntimeUsers");
    }

    [Fact]
    public void OneUsableAccountAmongBroken_IsNotAggregateError()
    {
        // 只要有一条真能进，就不是"谁都进不去" ⇒ 不报整表 Error
        // （单条的 UnknownRole 仍要报，用户需要知道那条是死的）
        var project = Project(User("op"), User("dead", "role-x"));
        var findings = RuntimeUserFindings(project);

        var finding = Assert.Single(findings);
        Assert.Equal("SecurityConfig.RuntimeUsers[1]", finding.Location);
        Assert.DoesNotContain("没有一条可用", finding.Message);
    }

    // ══════════════════ 单一来源守卫：新增问题类型必须显式定级 ══════════════════

    /// <summary>
    /// 每种 <see cref="RuntimeUserIssueKind"/> 的期望定级。
    ///
    /// <para><b>这条守卫的意义：</b><see cref="GeneratedCodeValidator"/> 的定级 switch 有
    /// <c>_ =&gt; Warning</c> 兜底 —— 新增一种问题类型时它会**静默变成 Warning**（不拦导出）。
    /// 本表是显式的"必须做的决定"：类型增删就编译/断言不过，
    /// 逼接手人回答"这条要不要拦导出"，而不是让它悄悄降级。</para>
    /// </summary>
    private static readonly Dictionary<RuntimeUserIssueKind, FindingSeverity> ExpectedSeverity = new()
    {
        [RuntimeUserIssueKind.EmptyUsername] = FindingSeverity.Warning,
        [RuntimeUserIssueKind.DuplicateUsername] = FindingSeverity.Warning,
        [RuntimeUserIssueKind.UnknownRole] = FindingSeverity.Error,
        [RuntimeUserIssueKind.EnabledWithoutPassword] = FindingSeverity.Error,
    };

    [Fact]
    public void EveryIssueKind_HasAnExplicitlyDecidedSeverity()
    {
        var allKinds = Enum.GetValues<RuntimeUserIssueKind>();

        var undecided = allKinds.Where(k => !ExpectedSeverity.ContainsKey(k)).ToList();
        Assert.True(undecided.Count == 0,
            "新增了 RuntimeUserIssueKind 但没有定级（会静默落进 _ => Warning，不拦导出）：" +
            string.Join(", ", undecided));

        // 反向：表里不能有已删除的类型（否则守卫本身在描述一个不存在的世界）
        var stale = ExpectedSeverity.Keys.Where(k => !allKinds.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "期望表里有已不存在的类型：" + string.Join(", ", stale));
    }

    [Fact]
    public void SeverityMapping_MatchesActualBehaviour_ForEveryKind()
    {
        // 把每种问题类型构造成真实用户表，逐一核对实际定级与期望表一致。
        var projects = new Dictionary<RuntimeUserIssueKind, HmiProject>
        {
            [RuntimeUserIssueKind.EmptyUsername] = Project(User(""), User("op")),
            [RuntimeUserIssueKind.DuplicateUsername] = Project(User("op"), User("op")),
            [RuntimeUserIssueKind.UnknownRole] = Project(User("ghost", "role-x"), User("op")),
            [RuntimeUserIssueKind.EnabledWithoutPassword] = Project(User("nopass", passwordHash: ""), User("op")),
        };

        Assert.Equal(ExpectedSeverity.Count, projects.Count);

        foreach (var (kind, project) in projects)
        {
            // 断言"该类型被检出且定级正确"：先确认 RuntimeUserValidator 真的产出了这种问题
            var issues = RuntimeUserValidator.Validate(project.SecurityConfig.RuntimeUsers);
            Assert.Contains(issues, i => i.Kind == kind);

            var severity = RuntimeUserFindings(project)
                .Where(f => f.Location != "SecurityConfig.RuntimeUsers")
                .Select(f => f.Severity)
                .ToList();

            Assert.Contains(ExpectedSeverity[kind], severity);
        }
    }
}
