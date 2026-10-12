using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R3-T1: UDT 结构化标签单元测试。
/// 覆盖 UDT 定义、字段操作、实例化、绑定解析、向后兼容。
/// </summary>
public class UdtTests
{
    // ── UdtDefinition 基础操作 ──

    [Fact]
    public void Definition_Default_HasEmptyFields()
    {
        var udt = new UdtDefinition();
        Assert.Empty(udt.Fields);
        Assert.NotEmpty(udt.Id);
        Assert.Equal("", udt.Name);
    }

    [Fact]
    public void Definition_AddField_IncreasesFieldCount()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");

        Assert.Equal(2, udt.Fields.Count);
        Assert.Equal("speed", udt.Fields[0].Name);
        Assert.Equal("Float", udt.Fields[0].DataType);
    }

    [Fact]
    public void Definition_AddField_WithDefault_SetsValue()
    {
        var udt = new UdtDefinition { Name = "Valve" };
        var field = udt.AddField("state", "Bool", "false");

        Assert.Equal("false", field.DefaultValue);
    }

    [Fact]
    public void Definition_RemoveField_RemovesCorrectField()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        var f1 = udt.AddField("speed", "Float");
        udt.AddField("current", "Float");

        var result = udt.RemoveField(f1);

        Assert.True(result);
        Assert.Single(udt.Fields);
        Assert.Equal("current", udt.Fields[0].Name);
    }

    [Fact]
    public void Definition_FindField_ReturnsCorrectField()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");

        var found = udt.FindField("speed");

        Assert.NotNull(found);
        Assert.Equal("speed", found!.Name);
    }

    [Fact]
    public void Definition_FindField_NotFound_ReturnsNull()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        udt.AddField("speed", "Float");

        var found = udt.FindField("nonexistent");

        Assert.Null(found);
    }

    // ── 实例化 ──

    [Fact]
    public void CreateInstance_SetsUdtTypeId()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        udt.AddField("speed", "Float");

        var instance = udt.CreateInstance("motor1");

        Assert.Equal("motor1", instance.Name);
        Assert.Equal(udt.Id, instance.UdtTypeId);
        Assert.True(instance.IsUdtInstance);
        Assert.Equal("UDT:Motor", instance.DataType);
    }

    [Fact]
    public void ExpandInstance_ReturnsAllFields()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");
        udt.AddField("fault", "Bool");
        udt.AddField("run", "Bool");

        var instance = udt.CreateInstance("motor1", "group1");
        instance.Address = "DB1.0";

        var fields = udt.ExpandInstance(instance);

        Assert.Equal(4, fields.Count);
        Assert.Equal("motor1.speed", fields[0].CompoundId);
        Assert.Equal("motor1.current", fields[1].CompoundId);
        Assert.Equal("DB1.0.speed", fields[0].Address);
        Assert.Equal("group1", fields[0].GroupId);
    }

    [Fact]
    public void ExpandInstance_NullInstance_Throws()
    {
        var udt = new UdtDefinition { Name = "Motor" };
        Assert.Throws<ArgumentNullException>(() => udt.ExpandInstance(null!));
    }

    // ── Variable UDT 标记 ──

    [Fact]
    public void Variable_Default_IsNotUdtInstance()
    {
        var variable = new Variable { Name = "temp" };
        Assert.False(variable.IsUdtInstance);
        Assert.Equal("", variable.UdtTypeId);
    }

    [Fact]
    public void Variable_WithUdtType_IsUdtInstance()
    {
        var variable = new Variable
        {
            Name = "motor1",
            UdtTypeId = "abc123"
        };
        Assert.True(variable.IsUdtInstance);
    }

    // ── VariableManager UDT 注册 ──

    [Fact]
    public void Manager_RegisterUdt_AddsToDefinitions()
    {
        var mgr = new VariableManager();

        var udt = mgr.RegisterUdt("Motor", "Motor type");

        Assert.Single(mgr.UdtDefinitions);
        Assert.Equal("Motor", udt.Name);
        Assert.Equal("Motor type", udt.Description);
    }

    [Fact]
    public void Manager_FindUdtDefinition_ReturnsCorrectDefinition()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");

        var found = mgr.FindUdtDefinition(udt.Id);

        Assert.NotNull(found);
        Assert.Equal("Motor", found!.Name);
    }

    [Fact]
    public void Manager_FindUdtDefinition_NotFound_ReturnsNull()
    {
        var mgr = new VariableManager();
        var found = mgr.FindUdtDefinition("nonexistent");
        Assert.Null(found);
    }

    [Fact]
    public void Manager_FindUdtDefinition_EmptyId_ReturnsNull()
    {
        var mgr = new VariableManager();
        var found = mgr.FindUdtDefinition("");
        Assert.Null(found);
    }

    [Fact]
    public void Manager_FindUdtByName_ReturnsCorrectDefinition()
    {
        var mgr = new VariableManager();
        mgr.RegisterUdt("Motor");

        var found = mgr.FindUdtByName("Motor");

        Assert.NotNull(found);
        Assert.Equal("Motor", found!.Name);
    }

    [Fact]
    public void Manager_RemoveUdtDefinition_RemovesWhenNoInstances()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");

        var result = mgr.RemoveUdtDefinition(udt.Id);

        Assert.True(result);
        Assert.Empty(mgr.UdtDefinitions);
    }

    [Fact]
    public void Manager_RemoveUdtDefinition_FailsWhenInstancesExist()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        mgr.CreateUdtInstance(udt, "motor1");

        var result = mgr.RemoveUdtDefinition(udt.Id);

        Assert.False(result);
        Assert.Single(mgr.UdtDefinitions);
    }

    // ── 实例创建 ──

    [Fact]
    public void Manager_CreateUdtInstance_AddsToVariables()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");

        var instance = mgr.CreateUdtInstance(udt, "motor1", "group1");

        Assert.Single(mgr.Variables);
        Assert.Equal("motor1", instance.Name);
        Assert.Equal("group1", instance.GroupId);
        Assert.Equal(udt.Id, instance.UdtTypeId);
    }

    // ── 绑定解析 ──

    [Fact]
    public void ResolveBinding_PlainVariable_ByFindsById()
    {
        var mgr = new VariableManager();
        var variable = new Variable { Name = "temperature", DataType = "Float" };
        mgr.Variables.Add(variable);

        var resolved = mgr.ResolveBinding(variable.Id);

        Assert.NotNull(resolved);
        Assert.IsType<Variable>(resolved);
        Assert.Equal("temperature", ((Variable)resolved!).Name);
    }

    [Fact]
    public void ResolveBinding_PlainVariable_ByFindsByName()
    {
        var mgr = new VariableManager();
        var variable = new Variable { Name = "temperature", DataType = "Float" };
        mgr.Variables.Add(variable);

        var resolved = mgr.ResolveBinding("temperature");

        Assert.NotNull(resolved);
        Assert.IsType<Variable>(resolved);
    }

    [Fact]
    public void ResolveBinding_UdtField_ReturnsFieldVariable()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");
        mgr.CreateUdtInstance(udt, "motor1");

        var resolved = mgr.ResolveBinding("motor1.speed");

        Assert.NotNull(resolved);
        Assert.IsType<UdtFieldVariable>(resolved);
        var fieldVar = (UdtFieldVariable)resolved!;
        Assert.Equal("motor1.speed", fieldVar.CompoundId);
        Assert.Equal("speed", fieldVar.FieldName);
        Assert.Equal("Float", fieldVar.DataType);
    }

    [Fact]
    public void ResolveBinding_UdtField_NonExistentField_ReturnsNull()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        mgr.CreateUdtInstance(udt, "motor1");

        var resolved = mgr.ResolveBinding("motor1.nonexistent");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveBinding_UdtField_NonExistentInstance_ReturnsNull()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");

        var resolved = mgr.ResolveBinding("nonexistent.speed");

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveBinding_EmptyString_ReturnsNull()
    {
        var mgr = new VariableManager();
        var resolved = mgr.ResolveBinding("");
        Assert.Null(resolved);
    }

    // ── 可绑定项列表 ──

    [Fact]
    public void GetAllBindableItems_IncludesPlainVariables()
    {
        var mgr = new VariableManager();
        mgr.Variables.Add(new Variable { Name = "temp", DataType = "Float" });
        mgr.Variables.Add(new Variable { Name = "pressure", DataType = "Int16" });

        var items = mgr.GetAllBindableItems();

        Assert.Equal(2, items.Count);
        Assert.Equal("temp", items[0].DisplayName);
        Assert.False(items[0].IsUdtInstance);
    }

    [Fact]
    public void GetAllBindableItems_IncludesUdtInstancesWithChildren()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");
        mgr.CreateUdtInstance(udt, "motor1");

        var items = mgr.GetAllBindableItems();

        Assert.Single(items);
        Assert.True(items[0].IsUdtInstance);
        Assert.Equal("motor1", items[0].DisplayName);
        Assert.Equal(2, items[0].Children!.Count);
        Assert.Equal("motor1.speed", items[0].Children![0].BindingId);
        Assert.True(items[0].Children![0].IsUdtField);
    }

    [Fact]
    public void GetAllBindableItems_MixedPlainAndUdt()
    {
        var mgr = new VariableManager();
        mgr.Variables.Add(new Variable { Name = "temp", DataType = "Float" });

        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        mgr.CreateUdtInstance(udt, "motor1");

        var items = mgr.GetAllBindableItems();

        Assert.Equal(2, items.Count);
        Assert.False(items[0].IsUdtInstance); // temp
        Assert.True(items[1].IsUdtInstance);   // motor1
    }

    // ── 向后兼容 ──

    [Fact]
    public void Variable_DefaultUdtTypeId_IsEmpty()
    {
        // 旧项目反序列化时不会包含 UdtTypeId 字段
        // C# 默认值应为空字符串（非 null），IsUdtInstance 应为 false
        var variable = new Variable();
        Assert.Equal("", variable.UdtTypeId);
        Assert.False(variable.IsUdtInstance);
    }

    [Fact]
    public void Manager_DefaultUdtDefinitions_IsEmpty()
    {
        var mgr = new VariableManager();
        Assert.Empty(mgr.UdtDefinitions);
    }

    [Fact]
    public void Manager_WithoutUdt_GetAllBindableItems_WorksNormally()
    {
        var mgr = new VariableManager();
        mgr.Variables.Add(new Variable { Name = "temp", DataType = "Float" });

        var items = mgr.GetAllBindableItems();

        Assert.Single(items);
        Assert.Equal("temp", items[0].DisplayName);
        Assert.Null(items[0].Children);
    }

    // ── 多 UDT 实例 ──

    [Fact]
    public void MultipleInstances_OfSameUdt_AllResolveCorrectly()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");
        mgr.CreateUdtInstance(udt, "motor1");
        mgr.CreateUdtInstance(udt, "motor2");

        var r1 = mgr.ResolveBinding("motor1.speed");
        var r2 = mgr.ResolveBinding("motor2.current");

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.Equal("motor1.speed", ((UdtFieldVariable)r1!).CompoundId);
        Assert.Equal("motor2.current", ((UdtFieldVariable)r2!).CompoundId);
    }

    [Fact]
    public void ExpandInstance_MultipleInstances_HaveSameStructure()
    {
        var mgr = new VariableManager();
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        udt.AddField("current", "Float");
        var motor1 = mgr.CreateUdtInstance(udt, "motor1");
        var motor2 = mgr.CreateUdtInstance(udt, "motor2");

        var fields1 = udt.ExpandInstance(motor1);
        var fields2 = udt.ExpandInstance(motor2);

        Assert.Equal(fields1.Count, fields2.Count);
        Assert.Equal("motor1.speed", fields1[0].CompoundId);
        Assert.Equal("motor2.speed", fields2[0].CompoundId);
    }

    // ── UDT 与普通变量共存 ──

    [Fact]
    public void MixedBindingResolution_BothTypesResolve()
    {
        var mgr = new VariableManager();

        // 普通变量
        var tempVar = new Variable { Name = "temperature", DataType = "Float" };
        mgr.Variables.Add(tempVar);

        // UDT
        var udt = mgr.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        mgr.CreateUdtInstance(udt, "motor1");

        // 解析普通变量
        var plainResult = mgr.ResolveBinding(tempVar.Id);
        Assert.NotNull(plainResult);
        Assert.IsType<Variable>(plainResult);

        // 解析 UDT 字段
        var udtResult = mgr.ResolveBinding("motor1.speed");
        Assert.NotNull(udtResult);
        Assert.IsType<UdtFieldVariable>(udtResult);
    }
}
