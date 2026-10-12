using System.Linq;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：<see cref="VariableManager.ResolveBinding"/> 在"点号"上的语义。
///
/// <para>点号**不是** UDT 字段的独占标记：PLC/CSV 导入的变量名本身就常带点号
/// （<c>Motor.Speed</c>、<c>DB1.DBD0</c>、<c>Line1.Temp</c>）。此前的实现在点号分支里
/// 一旦 UDT 解析失败就直接 <c>return null</c>，这类绑定会静默解析不到变量 ——
/// 既不进运行时标签表，预览里也永远是空值。</para>
/// </summary>
public class VariableBindingResolutionTests
{
    private static HmiProject BuildProject()
    {
        var project = new HmiProject { Name = "Dotted", LvglVersion = "9.5" };
        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });

        // PLC 导入的符号名（自带点号）
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "Motor.Speed", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "rpm",
        });
        vm.Variables.Add(new Variable
        {
            Id = "DB1.DBD0", Name = "DB1.DBD0", Address = "40002", DataType = "Int16",
            ConnectionId = "c1",
        });
        // 前缀恰是 UDT 实例名、但后缀不是该实例的字段 —— 点号分支最容易在此处错误地
        // 直接 return null（回归点）。
        vm.Variables.Add(new Variable
        {
            Id = "v_torque", Name = "Motor.Torque", Address = "40003", DataType = "Float",
            ConnectionId = "c1", Unit = "Nm",
        });

        // 与上面同名的 UDT 实例（"Motor" 实例 + speed 字段）—— 用于验证优先级
        var def = vm.RegisterUdt("MotorType");
        def.AddField("Speed", "Float32");
        var instance = vm.CreateUdtInstance(def, "Motor", "g1");
        instance.Address = "DB2.DBD0";
        instance.ConnectionId = "c1";
        return project;
    }

    [Fact]
    public void UdtField_TakesPrecedenceOverLiteralVariable()
    {
        var vm = BuildProject().VariableManager;

        // "Motor" 是 UDT 实例且 "Speed" 是它的字段（FindField 大小写敏感）
        // → UDT 字段优先，不受"同名变量也存在"影响。
        var field = Assert.IsType<UdtFieldVariable>(vm.ResolveBinding("Motor.Speed"));
        Assert.Equal("Motor.Speed", field.CompoundId);
    }

    [Fact]
    public void DottedPrefixIsUdtInstance_ButFieldMissing_FallsBackToLiteralVariable()
    {
        var vm = BuildProject().VariableManager;

        // 回归点：前缀 "Motor" 命中 UDT 实例，但 "Torque" 不是它的字段；
        // 旧实现在这里 return null，导致字面变量 "Motor.Torque" 永远解析不到。
        var variable = Assert.IsType<Variable>(vm.ResolveBinding("Motor.Torque"));
        Assert.Equal("40003", variable.Address);
        Assert.Equal("Float", variable.DataType);
    }

    [Fact]
    public void DottedName_WithNoMatchingUdtInstance_ResolvesLiteralVariable()
    {
        var vm = BuildProject().VariableManager;

        var variable = Assert.IsType<Variable>(vm.ResolveBinding("DB1.DBD0"));
        Assert.Equal("DB1.DBD0", variable.Name);
        Assert.Equal("Int16", variable.DataType);
    }

    [Fact]
    public void UnknownDottedId_ReturnsNull()
    {
        var vm = BuildProject().VariableManager;
        Assert.Null(vm.ResolveBinding("ghost.field"));
        Assert.Null(vm.ResolveBinding(".leading"));
        Assert.Null(vm.ResolveBinding("trailing."));
        Assert.Null(vm.ResolveBinding(""));
    }

    // ── 下游收益：类型解析与运行时标签表 ──────────────────────

    [Fact]
    public void VariableBindingInfo_ResolvesDottedLiteralVariable()
    {
        var vm = BuildProject().VariableManager;

        Assert.Equal("Int16", VariableBindingInfo.ResolveDataType(vm, "DB1.DBD0"));
        Assert.Null(VariableBindingInfo.ResolveUnit(vm, "DB1.DBD0")); // 该变量无单位
    }

    [Fact]
    public void RuntimeTagTable_IncludesBindingsToDottedLiteralVariables()
    {
        var project = BuildProject();
        var scr = new HmiScreen { Id = "s1", Name = "主屏", Width = 800, Height = 480 };
        project.Screens.Add(scr);
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl", Type = HmiControlType.Label, Name = "DB1 值",
            VariableBindings =
            {
                new VariableBindingData
                {
                    VariableId = "DB1.DBD0", Direction = BindingDirection.ReadOnly,
                    PropertyName = "Value",
                },
            },
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        // 修复前这里会是 0：点号绑定解析不到变量 → 没有标签 → 预览/监视器全空
        Assert.Equal(1, engine.TagCount);
        Assert.Equal("DB1.DBD0", engine.Tags[0].VariableId);
        Assert.Equal("40002", engine.Tags[0].Address);
    }
}
