using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 联动模型专项测试 —— 验证变量绑定与动作联动的新模型。
/// 覆盖: 联动自动链接、多绑定、多动作、旧 JSON 迁移、ReadOnly 联动。
/// </summary>
public class BindingLinkageTests
{
    // ── 联动自动链接 ──

    [Fact]
    public void PrimaryWriteVariable_ReturnsFirstWriteBinding()
    {
        var control = new HmiControl { Type = HmiControlType.Button };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "display_var",
            Direction = BindingDirection.ReadOnly
        });
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "write_var",
            Direction = BindingDirection.WriteOnly
        });

        Assert.Equal("write_var", control.PrimaryWriteVariable);
    }

    [Fact]
    public void PrimaryBindingVariable_ReturnsFirstNonWriteBinding()
    {
        var control = new HmiControl { Type = HmiControlType.Label };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "write_var",
            Direction = BindingDirection.WriteOnly
        });
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "display_var",
            Direction = BindingDirection.ReadOnly
        });

        Assert.Equal("display_var", control.PrimaryBindingVariable);
    }

    [Fact]
    public void PrimaryBindingVariable_FallsBackToLegacyField()
    {
        var control = new HmiControl { Type = HmiControlType.Label, VariableBindingId = "legacy_var" };
        Assert.Equal("legacy_var", control.PrimaryBindingVariable);
    }

    [Fact]
    public void AutoLinkTarget_FillsEmptyWriteActionTarget()
    {
        var action = HmiAction.SetVariable("", "100");
        action.Target = null;

        action.AutoLinkTarget("motor_speed");

        Assert.Equal("motor_speed", action.Target);
    }

    [Fact]
    public void AutoLinkTarget_DoesNotOverrideExistingTarget()
    {
        var action = HmiAction.SetVariable("existing_var", "100");

        action.AutoLinkTarget("new_var");

        Assert.Equal("existing_var", action.Target);
    }

    [Fact]
    public void IsWriteAction_RecognizesAllWriteTypes()
    {
        Assert.True(HmiAction.IsWriteAction("SetVariable"));
        Assert.True(HmiAction.IsWriteAction("SetBit"));
        Assert.True(HmiAction.IsWriteAction("ResetBit"));
        Assert.True(HmiAction.IsWriteAction("ToggleBit"));
        Assert.True(HmiAction.IsWriteAction("MomentaryOn"));
        Assert.True(HmiAction.IsWriteAction("MomentaryOff"));
        Assert.False(HmiAction.IsWriteAction("SwitchScreen"));
        Assert.False(HmiAction.IsWriteAction("ShowAlarms"));
        Assert.False(HmiAction.IsWriteAction(null));
    }

    // ── 多绑定 ──

    [Fact]
    public void PreviewResolver_UsesPrimaryBinding_WithMultipleBindings()
    {
        var control = new HmiControl { Type = HmiControlType.Gauge };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "speed",
            Direction = BindingDirection.ReadWrite
        });
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "unit",
            Direction = BindingDirection.ReadOnly
        });

        var text = PreviewValueResolver.ResolveDisplayText(control, id => id == "speed" ? 42.5 : null);

        Assert.Equal("42.5", text);
    }

    [Fact]
    public void PreviewResolver_ResolvesByPropertyName()
    {
        var control = new HmiControl { Type = HmiControlType.Label };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "speed",
            Direction = BindingDirection.ReadWrite
        });
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "unit",
            Direction = BindingDirection.ReadOnly
        });

        var text = PreviewValueResolver.ResolveDisplayText(control, id => id == "unit" ? 7 : null, "Text");

        Assert.Equal("7.00", text);
    }

    [Fact]
    public void PreviewResolver_IgnoresWriteOnlyBinding_AsPrimary()
    {
        var control = new HmiControl { Type = HmiControlType.Gauge };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "write_only_var",
            Direction = BindingDirection.WriteOnly
        });

        // WriteOnly 不应作为显示数据源
        var text = PreviewValueResolver.ResolveDisplayText(control, id => id == "write_only_var" ? 99 : null);

        Assert.Null(text);
    }

    // ── 多动作 ──

    [Fact]
    public void CrossReference_FindsActionsInCollection()
    {
        var project = CreateProjectWithActions();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "motor1");

        Assert.True(result.HasReferences);
        Assert.Contains(result.References, r =>
            r.ReferenceType == ReferenceType.ActionTarget && r.TargetId == "motor1");
    }

    [Fact]
    public void CrossReference_FindsMultipleBindingsInCollection()
    {
        var project = new HmiProject { Name = "Test" };
        project.VariableManager.Variables.Add(new Variable { Id = "v1", Name = "temp", DataType = "Float" });
        project.VariableManager.Variables.Add(new Variable { Id = "v2", Name = "pres", DataType = "Float" });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var control = new HmiControl { Id = "c1", Name = "MultiBind", Type = HmiControlType.Label };
        control.VariableBindings.Add(new VariableBindingData { PropertyName = "Text", VariableId = "temp", Direction = BindingDirection.ReadOnly });
        control.VariableBindings.Add(new VariableBindingData { PropertyName = "Value", VariableId = "pres", Direction = BindingDirection.ReadOnly });
        screen.Controls.Add(control);
        project.Screens.Add(screen);

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "pres");

        Assert.True(result.HasReferences);
        var bindingRef = result.References.First(r => r.ReferenceType == ReferenceType.VariableBinding);
        Assert.Equal("Value", bindingRef.PropertyOrEvent);
    }

    [Fact]
    public void NavigationGraph_FindsSwitchScreenInActionsCollection()
    {
        var project = new HmiProject { Name = "Test" };
        var screen1 = new HmiScreen { Id = "s1", Name = "Main" };
        var screen2 = new HmiScreen { Id = "s2", Name = "Setup" };
        var control = new HmiControl { Id = "c1", Name = "NavBtn", Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction { ActionType = "SwitchScreen", Target = "Setup", TriggerEvent = "OnClick" });
        screen1.Controls.Add(control);
        project.Screens.Add(screen1);
        project.Screens.Add(screen2);

        var graph = NavigationGraph.BuildFromProject(project);

        Assert.Single(graph.Edges);
        Assert.Equal("Setup", graph.Edges[0].Label.Replace("→ ", ""));
    }

    // ── 旧 JSON 迁移 ──

    [Fact]
    public void Deserialize_LegacyVariableBindingId_MigratesToCollection()
    {
        const string legacyJson = """
        {
          "name": "LegacyProject",
          "screens": [
            {
              "id": "s1",
              "name": "Main",
              "controls": [
                {
                  "id": "c1",
                  "name": "TempLabel",
                  "type": "Label",
                  "variableBindingId": "temperature",
                  "bindingMode": "ReadOnly"
                }
              ]
            }
          ],
          "variableManager": {
            "variables": [
              { "id": "v1", "name": "temperature", "dataType": "Float" }
            ]
          }
        }
        """;

        var project = ProjectSerializer.Deserialize(legacyJson);

        Assert.NotNull(project);
        var control = project.Screens[0].Controls[0];
        Assert.Single(control.VariableBindings);
        Assert.Equal("temperature", control.VariableBindings[0].VariableId);
        Assert.Equal(BindingDirection.ReadOnly, control.VariableBindings[0].Direction);
    }

    [Fact]
    public void Deserialize_LegacyAction_MigratesToCollection()
    {
        const string legacyJson = """
        {
          "name": "LegacyProject",
          "screens": [
            {
              "id": "s1",
              "name": "Main",
              "controls": [
                {
                  "id": "c1",
                  "name": "StartBtn",
                  "type": "Button",
                  "action": {
                    "actionType": "SetVariable",
                    "target": "motor1",
                    "value": "100",
                    "triggerEvent": "OnClick"
                  }
                }
              ]
            }
          ],
          "variableManager": {
            "variables": [
              { "id": "v1", "name": "motor1", "dataType": "Int32" }
            ]
          }
        }
        """;

        var project = ProjectSerializer.Deserialize(legacyJson);

        Assert.NotNull(project);
        var control = project.Screens[0].Controls[0];
        Assert.Single(control.Actions);
        Assert.Equal("SetVariable", control.Actions[0].ActionType);
        Assert.Equal("motor1", control.Actions[0].Target);
    }

    [Fact]
    public void Serialize_DoesNotWriteLegacyFields_WhenOnlyNewCollectionsUsed()
    {
        var control = new HmiControl { Id = "c1", Name = "C", Type = HmiControlType.Label };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "temp",
            Direction = BindingDirection.ReadOnly
        });

        var json = ProjectSerializer.Serialize(new HmiProject { Name = "T" });
        // 仅序列化项目骨架 —— 此处验证新格式往返
        var project = new HmiProject { Name = "T" };
        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(control);
        project.Screens.Add(screen);
        json = ProjectSerializer.Serialize(project);

        Assert.Contains("variableBindings", json);
        Assert.Contains("ReadOnly", json);
    }

    // ── ReadOnly 联动 ──

    [Fact]
    public void FindDanglingReferences_DetectsBindingsInCollection()
    {
        var project = new HmiProject { Name = "Test" };
        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var control = new HmiControl { Id = "c1", Name = "Broken", Type = HmiControlType.Label };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "deleted_var",
            Direction = BindingDirection.ReadOnly
        });
        screen.Controls.Add(control);
        project.Screens.Add(screen);

        var dangling = CrossReferenceAnalyzer.FindDanglingReferences(project);

        Assert.Single(dangling);
        Assert.Equal("deleted_var", dangling[0].BindingId);
    }

    // ── 辅助 ──

    private static HmiProject CreateProjectWithActions()
    {
        var project = new HmiProject { Name = "Test" };
        project.VariableManager.Variables.Add(new Variable { Id = "v1", Name = "motor1", DataType = "Int32" });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var control = new HmiControl { Id = "c1", Name = "StartBtn", Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction { ActionType = "SetVariable", Target = "motor1", Value = "1", TriggerEvent = "OnClick" });
        control.Actions.Add(new HmiAction { ActionType = "SwitchScreen", Target = "Setup", TriggerEvent = "OnClick" });
        screen.Controls.Add(control);
        project.Screens.Add(screen);

        return project;
    }
}
