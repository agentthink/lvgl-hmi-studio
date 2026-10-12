using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 「动作 → 所需权限」唯一来源（<see cref="ActionPermissionMap"/>）与动作回调**权限门**的发射契约。
///
/// <para><b>为什么两条都要测：</b>映射表是"哪些动作受管"的唯一答案，门是它的唯一消费点。
/// 只测映射表 ⇒ 发射器可以悄悄不发（权限体系形同虚设）；只测门 ⇒ 新增动作类型可以悄悄漏配
/// （新动作默认不受管，且没人会发现）。</para>
///
/// <para><b>默认零变化是硬契约：</b>设计模式（<see cref="OperationMode.Designer"/>）下**不得**
/// 出现任何权限门 —— 历史导出产物与既有对拍测试都建立在"没有门"之上。项目显式切到
/// Operator/Maintenance/ReadOnly 之后才开始门控。</para>
/// </summary>
public class ActionPermissionMapTests
{
    /// <summary>反射取 <see cref="ActionTypeMeta"/> 的公开常量 —— 新增动作会被守卫自动纳入。</summary>
    private static List<string> DeclaredActionTypes() =>
        typeof(ActionTypeMeta)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    // ══════════════════ 映射表本身 ══════════════════

    [Fact]
    public void Entries_CoverDeclaredActionTypes_BothDirections()
    {
        var declared = DeclaredActionTypes();
        var mapped = ActionPermissionMap.Entries.Select(e => e.ActionType).ToList();

        var missing = declared.Except(mapped).ToList();
        Assert.True(missing.Count == 0,
            "新增动作类型必须在 ActionPermissionMap.Entries 里登记权限（否则它在设备端不受任何管束）："
            + string.Join(", ", missing));

        var unknown = mapped.Except(declared).ToList();
        Assert.True(unknown.Count == 0,
            "权限表里出现了 ActionTypeMeta 未声明的动作类型（改名后没同步）：" + string.Join(", ", unknown));

        Assert.Equal(mapped.Count, mapped.Distinct().Count());
        Assert.Equal(declared.Count, ActionPermissionMap.Entries.Count);
    }

    /// <summary>
    /// 「哪个动作要哪一位权限」的**语义**必须与下面这张独立期望表一致。
    ///
    /// <para><b>为什么必须有这条：</b>其余关于映射的断言，两侧都取自
    /// <see cref="ActionPermissionMap.Entries"/>（表给期望、发射器读表），只能证明"表和门彼此一致"，
    /// 证明不了"表本身是对的"—— 把某条受管动作的权限改成 <c>null</c>，那些测试会把它从循环里
    /// **过滤掉**（<c>Where(e =&gt; e.Permission is not null)</c>）而不是报错。安全语义表必须有一份
    /// **独立写出来的**期望值，否则改错了是完全静默的。</para>
    /// </summary>
    [Fact]
    public void Entries_MatchIntendedPermissions_Exactly()
    {
        // 独立期望表：动语义必须先改这里 —— 改这里本身就是"有意变更"的显式动作
        var intended = new Dictionary<string, Permission?>
        {
            [ActionTypeMeta.ToggleBit] = Permission.OperateControl,
            [ActionTypeMeta.SetBit] = Permission.OperateControl,
            [ActionTypeMeta.ResetBit] = Permission.OperateControl,
            [ActionTypeMeta.MomentaryOn] = Permission.OperateControl,
            [ActionTypeMeta.MomentaryOff] = Permission.OperateControl,
            [ActionTypeMeta.SetVariable] = Permission.OperateControl,
            [ActionTypeMeta.ExecuteScript] = Permission.OperateControl,
            [ActionTypeMeta.SwitchScreen] = Permission.ViewScreen,
            [ActionTypeMeta.ShowTrends] = Permission.ViewScreen,
            [ActionTypeMeta.ShowAlarms] = Permission.ViewAlarmHistory,
            [ActionTypeMeta.Login] = null,
            [ActionTypeMeta.Logout] = null,
        };

        var actual = ActionPermissionMap.Entries.ToDictionary(e => e.ActionType, e => e.Permission);

        var diffs = new List<string>();
        foreach (var (actionType, expected) in intended)
        {
            if (!actual.TryGetValue(actionType, out var got))
                diffs.Add($"{actionType}: 映射表里没有这条（期望 {Describe(expected)}）");
            else if (got != expected)
                diffs.Add($"{actionType}: 实际 {Describe(got)}，期望 {Describe(expected)}");
        }

        Assert.True(diffs.Count == 0,
            "动作权限语义与期望表不符（漏配 = 该动作在设备端不受任何管束）："
            + Environment.NewLine + string.Join(Environment.NewLine, diffs));

        // 期望表自身也要双向完整 —— 否则"期望表漏登记"会让上面那条放过它
        Assert.Empty(intended.Keys.Except(actual.Keys));
        Assert.Equal(intended.Count, actual.Count);

        static string Describe(Permission? p) => p is null ? "不门控" : p.Value.ToString();
    }

    [Fact]
    public void LoginAndLogout_AreNeverGated()
    {
        // 给 Logout 加门会锁死"权限不够 ⇒ 登不出去"；给 Login 加门则等于用门自己锁自己。
        Assert.Null(ActionPermissionMap.Required(ActionTypeMeta.Login));
        Assert.Null(ActionPermissionMap.Required(ActionTypeMeta.Logout));
    }

    [Fact]
    public void UnknownAction_IsNotGated_AndDoesNotThrow()
    {
        Assert.Null(ActionPermissionMap.Required("NoSuchAction"));
        Assert.Null(ActionPermissionMap.Required(null));
        Assert.Null(ActionPermissionMap.Required(""));
    }

    // ══════════════════ 门的发射 ══════════════════

    /// <summary>每个受管动作在 Operator 模式下都必须发射与映射一致的门。</summary>
    [Fact]
    public void Gate_OperatorMode_EmitsMappedPermissionForEveryGatedAction()
    {
        var gated = ActionPermissionMap.Entries.Where(e => e.Permission is not null).ToList();
        Assert.NotEmpty(gated);

        var offenders = new List<string>();
        foreach (var (actionType, permission) in gated)
        {
            var code = GenerateWithAction(OperationMode.Operator, actionType);
            var expected = $"if (!hmi_require_permission({PermissionMacros.For(permission!.Value)})) return;";
            if (!code.Contains(expected, StringComparison.Ordinal))
                offenders.Add($"{actionType} 缺门（期望含 {expected}）");
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Login/Logout 与未知动作不发射门。</summary>
    [Theory]
    [InlineData(ActionTypeMeta.Login)]
    [InlineData(ActionTypeMeta.Logout)]
    public void Gate_SessionAndUnknownActions_AreNotGated(string actionType)
    {
        var code = GenerateWithAction(OperationMode.Operator, actionType);
        Assert.DoesNotContain("if (!hmi_require_permission", code);
    }

    /// <summary>
    /// 设计模式（<b>默认</b>）下不得出现任何门 —— 与历史产物逐字节一致。
    /// 遍历全部受管动作，而不是抽查一个。
    /// <para>判据取**门调用的完整形态**（<c>if (!hmi_require_permission</c>），因为生成代码里
    /// 本来就有一行 <c>extern int hmi_require_permission(...)</c> 前置声明 —— 按符号名判会误报。</para>
    /// </summary>
    [Fact]
    public void Gate_DesignerMode_IsNeverEmitted()
    {
        var offenders = new List<string>();
        foreach (var (actionType, _) in ActionPermissionMap.Entries)
        {
            var code = GenerateWithAction(OperationMode.Designer, actionType);
            if (code.Contains("if (!hmi_require_permission", StringComparison.Ordinal))
                offenders.Add(actionType);
        }

        Assert.True(offenders.Count == 0,
            "设计模式下不得发射权限门（默认工程的导出产物必须与历史一致）：" + string.Join(", ", offenders));

        // 新建工程默认就是设计模式 —— 上面那条对"没显式配置的工程"同样成立
        Assert.Equal(OperationMode.Designer, new HmiProject().SecurityConfig.OperationMode);
    }

    /// <summary>
    /// 门的实参必须是**宏名**，不得是数字字面量：权限位只有编辑器一处来源，
    /// 生成代码里写死 <c>0x04</c> 就等于把数值复制到了第二处。
    /// </summary>
    [Fact]
    public void Gate_AlwaysPassesMacroName_NeverNumericLiteral()
    {
        foreach (var (actionType, permission) in ActionPermissionMap.Entries.Where(e => e.Permission is not null))
        {
            var code = GenerateWithAction(OperationMode.Operator, actionType);
            foreach (var line in code.Split('\n'))
            {
                if (!line.Contains("hmi_require_permission(", StringComparison.Ordinal)) continue;
                if (line.Contains("extern", StringComparison.Ordinal)) continue;   // 前置声明
                var argument = line[(line.IndexOf("hmi_require_permission(", StringComparison.Ordinal)
                                     + "hmi_require_permission(".Length)..];
                Assert.StartsWith("HMI_PERM_", argument);
                Assert.Contains(PermissionMacros.For(permission!.Value), line);
            }
        }
    }

    // ══════════════════ 辅助 ══════════════════

    private static string GenerateWithAction(OperationMode mode, string actionType)
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        project.SecurityConfig.OperationMode = mode;

        var conn = new DeviceConnection { Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp" };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_motor", Name = "motor", Address = "40002",
            DataType = "Bool", ConnectionId = "conn1"
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "StartBtn", Type = HmiControlType.Button,
            VariableBindingId = "v_motor", BindingMode = "ReadWrite",
        });
        project.Screens.Add(screen);

        var btn = screen.Controls[0];
        btn.Actions.Add(BuildAction(actionType));

        return new CodeGenerator().GenerateFullProject(project);
    }

    /// <summary>按动作类型造一个动作实例。覆盖完整性由 <c>BuildAction_CoversEveryDeclaredType</c> 自检。</summary>
    private static HmiAction BuildAction(string actionType) => actionType switch
    {
        ActionTypeMeta.ToggleBit => HmiAction.ToggleBit("v_motor"),
        ActionTypeMeta.SetBit => HmiAction.SetBit("v_motor"),
        ActionTypeMeta.ResetBit => HmiAction.ResetBit("v_motor"),
        ActionTypeMeta.MomentaryOn => HmiAction.MomentaryOn("v_motor"),
        ActionTypeMeta.MomentaryOff => HmiAction.MomentaryOff("v_motor"),
        ActionTypeMeta.SetVariable => HmiAction.SetVariable("v_motor", "1"),
        ActionTypeMeta.SwitchScreen => HmiAction.SwitchScreen("Main"),
        ActionTypeMeta.ExecuteScript => HmiAction.ExecuteScript("script1"),
        ActionTypeMeta.ShowAlarms => HmiAction.ShowAlarms,
        ActionTypeMeta.ShowTrends => HmiAction.ShowTrends,
        ActionTypeMeta.Login => HmiAction.Login,
        ActionTypeMeta.Logout => HmiAction.Logout,
        _ => throw new InvalidOperationException($"测试助手未覆盖动作类型 '{actionType}'"),
    };

    /// <summary>测试助手自身的覆盖完整性 —— 不然上面几条会"漏测而不报错"。</summary>
    [Fact]
    public void BuildAction_CoversEveryDeclaredType()
    {
        foreach (var actionType in DeclaredActionTypes())
        {
            var action = BuildAction(actionType);
            Assert.Equal(actionType, action.ActionType);
        }
    }
}
