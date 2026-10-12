using System.Linq;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：<see cref="VariableBindingInfo"/> —— 绑定 Id → 数据类型 / 单位。
///
/// 这两个查询原先写在 <c>MainWindow</c> 私有方法里，只能靠"打开编辑器看一眼预览文本"
/// 验证。挪到 Core 后可以逐条覆盖查找顺序：UDT 字段（含点号）→ 变量 Id → 变量按名回退
/// → 系统变量 Id；以及"单位只有用户变量/UDT 字段才有"这条细节。
/// </summary>
public class VariableBindingInfoTests
{
    private static HmiProject BuildProject()
    {
        var project = new HmiProject { Name = "Bindings", LvglVersion = "9.5" };
        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });

        vm.Variables.Add(new Variable
        {
            Id = "v_temp", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "℃",
        });
        // 旧工程常见形态：只有名字没有 Id 语义（按名回退）
        vm.Variables.Add(new Variable
        {
            Id = "v_motor_raw", Name = "motor_speed", Address = "40002", DataType = "Int16",
            ConnectionId = "c1",
        });

        var def = vm.RegisterUdt("Motor");
        def.AddField("speed", "Float32");
        def.AddField("run", "Bool");
        var instance = vm.CreateUdtInstance(def, "motor1", "group1");
        instance.Address = "DB1.DBD0";
        instance.ConnectionId = "c1";
        instance.Unit = "rpm";
        return project;
    }

    // ── 数据类型 ─────────────────────────────────────────────

    [Fact]
    public void ResolveDataType_ById_ReturnsVariableType()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal("Float", VariableBindingInfo.ResolveDataType(vm, "v_temp"));
    }

    [Fact]
    public void ResolveDataType_ByIdIsUnknown_FallsBackToName()
    {
        var vm = BuildProject().VariableManager;
        // Id 是 v_motor_raw，按 Name 绑定也能解析（旧工程兼容路径）
        Assert.Equal("Int16", VariableBindingInfo.ResolveDataType(vm, "motor_speed"));
    }

    [Fact]
    public void ResolveDataType_UdtField_ReturnsFieldType()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal("Float32", VariableBindingInfo.ResolveDataType(vm, "motor1.speed"));
        Assert.Equal("Bool", VariableBindingInfo.ResolveDataType(vm, "motor1.run"));
    }

    [Fact]
    public void ResolveDataType_UnknownUdtField_FallsBackToDefault()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal(VariableBindingInfo.DefaultDataType,
            VariableBindingInfo.ResolveDataType(vm, "motor1.nonexistent"));
        Assert.Equal(VariableBindingInfo.DefaultDataType,
            VariableBindingInfo.ResolveDataType(vm, "ghost.field"));
    }

    /// <summary>
    /// 回归：系统变量 Id 形如 <c>sys.time.cur</c>，**自带点号**。原实现用
    /// <c>Contains('.')</c> 判定 UDT 字段并直接 <c>return "Float"</c>，导致
    /// 系统变量永远拿不到真实类型（其分支是死代码）。
    /// </summary>
    [Fact]
    public void ResolveDataType_SystemVariable_ReturnsItsType_DespiteDottedId()
    {
        var vm = BuildProject().VariableManager;

        // 覆盖 "String" 与 "Int32" 等非 Float 类型，确保不是碰巧等于默认值
        foreach (var sys in vm.SystemVariables.Where(v => v.DataType != VariableBindingInfo.DefaultDataType))
        {
            Assert.True(sys.Id.Contains('.'), "系统变量 Id 预期带点号");
            Assert.Equal(sys.DataType, VariableBindingInfo.ResolveDataType(vm, sys.Id));
        }
    }

    [Fact]
    public void ResolveDataType_Unknown_ReturnsFloatDefault()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal("Float", VariableBindingInfo.ResolveDataType(vm, "no_such_var"));
        Assert.Equal("Float", VariableBindingInfo.ResolveDataType(vm, ""));
    }

    // ── 单位 ────────────────────────────────────────────────

    [Fact]
    public void ResolveUnit_ReturnsVariableUnit()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal("℃", VariableBindingInfo.ResolveUnit(vm, "v_temp"));
    }

    [Fact]
    public void ResolveUnit_ByLegacyName_AlsoResolves()
    {
        var vm = BuildProject().VariableManager;
        // motor_speed 没有单位 → null（而不是抛异常或返回空串）
        Assert.Null(VariableBindingInfo.ResolveUnit(vm, "motor_speed"));
    }

    [Fact]
    public void ResolveUnit_UdtField_InheritsInstanceUnit()
    {
        var vm = BuildProject().VariableManager;
        Assert.Equal("rpm", VariableBindingInfo.ResolveUnit(vm, "motor1.speed"));
    }

    [Fact]
    public void ResolveUnit_SystemVariable_IsNullBecauseNoUnitField()
    {
        var vm = BuildProject().VariableManager;
        var sys = vm.SystemVariables[0];

        Assert.Null(VariableBindingInfo.ResolveUnit(vm, sys.Id));
    }

    [Fact]
    public void ResolveUnit_Unknown_IsNull()
    {
        var vm = BuildProject().VariableManager;
        Assert.Null(VariableBindingInfo.ResolveUnit(vm, "nope"));
        Assert.Null(VariableBindingInfo.ResolveUnit(vm, ""));
    }
}
