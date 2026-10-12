using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I4: UDT 全链路单元测试 —— 定义 → 实例 → 点号寻址解析 →
/// 变量浏览器可绑定项 → 运行时标签表 → CodeGen 展开 → 删除保护。
/// </summary>
public class UdtPipelineTests
{
    private static (HmiProject Project, UdtDefinition Def, Variable Instance) BuildMotorProject()
    {
        var project = new HmiProject { LvglVersion = "9.2" };
        project.VariableManager.Connections.Add(new DeviceConnection
        {
            Id = "c1", Name = "PLC1", DriverId = "modbus-tcp"
        });

        var def = project.VariableManager.RegisterUdt("Motor");
        def.AddField("speed", "Float32");
        def.AddField("current", "Float32");
        def.AddField("run", "Bool");

        var instance = project.VariableManager.CreateUdtInstance(def, "motor1", "group1");
        instance.Address = "DB1.DBD0";
        instance.ConnectionId = "c1";
        return (project, def, instance);
    }

    // ── 定义 → 实例 ──

    [Fact]
    public void CreateInstance_UdtTypeIdAndDataType_Set()
    {
        var (_, def, instance) = BuildMotorProject();

        Assert.True(instance.IsUdtInstance);
        Assert.Equal(def.Id, instance.UdtTypeId);
        Assert.Equal("UDT:Motor", instance.DataType);
        Assert.Equal("motor1", instance.Name);
    }

    [Fact]
    public void RemoveDefinition_WithInstance_IsRejected()
    {
        var (project, def, _) = BuildMotorProject();

        Assert.False(project.VariableManager.RemoveUdtDefinition(def.Id));
        Assert.Contains(def, project.VariableManager.UdtDefinitions);
    }

    [Fact]
    public void RemoveDefinition_AfterInstanceRemoved_Succeeds()
    {
        var (project, def, instance) = BuildMotorProject();
        project.VariableManager.Variables.Remove(instance);

        Assert.True(project.VariableManager.RemoveUdtDefinition(def.Id));
        Assert.DoesNotContain(def, project.VariableManager.UdtDefinitions);
    }

    // ── 点号寻址解析 ──

    [Fact]
    public void ResolveBinding_CompoundId_ReturnsFieldVariable()
    {
        var (project, _, _) = BuildMotorProject();

        var resolved = project.VariableManager.ResolveBinding("motor1.speed");

        Assert.IsType<UdtFieldVariable>(resolved);
        var field = (UdtFieldVariable)resolved!;
        Assert.Equal("motor1", field.InstanceName);
        Assert.Equal("speed", field.FieldName);
        Assert.Equal("motor1.speed", field.CompoundId);
        Assert.Equal("Float32", field.DataType);
        Assert.Equal("DB1.DBD0.speed", field.Address);
    }

    [Fact]
    public void ResolveBinding_UnknownInstance_ReturnsNull()
    {
        var (project, _, _) = BuildMotorProject();
        Assert.Null(project.VariableManager.ResolveBinding("motorX.speed"));
        Assert.Null(project.VariableManager.ResolveBinding("motor1.unknown"));
    }

    // ── 变量浏览器可绑定项 ──

    [Fact]
    public void GetAllBindableItems_ExpandsUdtInstanceWithFields()
    {
        var (project, _, _) = BuildMotorProject();

        var items = project.VariableManager.GetAllBindableItems();

        var instanceItem = items.First(i => i.IsUdtInstance && i.BindingId == "motor1");
        Assert.Equal("UDT:Motor", instanceItem.DataType);
        Assert.NotNull(instanceItem.Children);
        Assert.Equal(3, instanceItem.Children!.Count);

        var speed = instanceItem.Children!.First(c => c.BindingId == "motor1.speed");
        Assert.True(speed.IsUdtField);
        Assert.Equal("Float32", speed.DataType);
    }

    // ── 运行时标签表 ──

    [Fact]
    public void RuntimeBindingEngine_RegistersUdtFieldTag()
    {
        var (project, _, _) = BuildMotorProject();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    VariableId = "motor1.speed",
                    Direction = BindingDirection.ReadOnly
                }
            }
        });
        project.Screens.Add(screen);

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var tag = engine.FindTag("motor1.speed");
        Assert.NotNull(tag);
        Assert.Equal("Float32", tag!.DataType);
        Assert.Equal("DB1.DBD0.speed", tag.Address);
        Assert.Equal("PLC1", tag.ConnectionName);
        Assert.Equal("modbus-tcp", tag.DriverId);
    }

    // ── CodeGen 展开 ──

    [Fact]
    public void GenerateFullProject_UdtFieldTag_EmitsTagTable()
    {
        var (project, _, _) = BuildMotorProject();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Type = HmiControlType.Label, Name = "SpeedLabel",
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    VariableId = "motor1.speed",
                    Direction = BindingDirection.ReadOnly
                }
            }
        });
        project.Screens.Add(screen);

        var generator = new CodeGenerator();
        var code = generator.GenerateFullProject(project);

        // 标签表应包含 UDT 字段（compound id 清洗为 C 标识符 + 原始地址）
        Assert.Contains("TAG_motor1_speed", code);
        Assert.Contains("\"motor1.speed\"", code);
        Assert.Contains("DB1.DBD0.speed", code);
    }

    [Fact]
    public void Serialization_UdtRoundTrip_PreservesDefinitionAndInstance()
    {
        var (project, _, _) = BuildMotorProject();
        var json = HmiEditor.Core.Serialization.ProjectSerializer.Serialize(project);
        var back = HmiEditor.Core.Serialization.ProjectSerializer.Deserialize(json);

        Assert.NotNull(back);
        var def = back!.VariableManager.UdtDefinitions.FirstOrDefault(d => d.Name == "Motor");
        Assert.NotNull(def);
        Assert.Equal(3, def!.Fields.Count);

        var inst = back.VariableManager.Variables.FirstOrDefault(v => v.Name == "motor1");
        Assert.NotNull(inst);
        Assert.True(inst!.IsUdtInstance);
        Assert.Equal(def.Id, inst.UdtTypeId);

        var resolved = back.VariableManager.ResolveBinding("motor1.speed");
        Assert.IsType<UdtFieldVariable>(resolved);
    }
}
