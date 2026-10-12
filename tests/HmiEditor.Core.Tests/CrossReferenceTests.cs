using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R3-T4: 交叉引用分析器单元测试。
/// 覆盖变量引用、屏幕引用、脚本引用、安全删除检查。
/// </summary>
public class CrossReferenceTests
{
    /// <summary>创建测试用的项目结构。</summary>
    private static HmiProject CreateTestProject()
    {
        var project = new HmiProject { Name = "Test" };

        // 变量
        var tempVar = new Variable { Id = "v_temp", Name = "temperature", DataType = "Float" };
        var pressureVar = new Variable { Id = "v_press", Name = "pressure", DataType = "Float" };
        project.VariableManager.Variables.Add(tempVar);
        project.VariableManager.Variables.Add(pressureVar);

        // 屏幕 1
        var screen1 = new HmiScreen { Id = "s1", Name = "Main" };
        screen1.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "TempLabel",
            Type = HmiControlType.Label,
            VariableBindingId = "temperature"
        });
        screen1.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "PressureGauge",
            Type = HmiControlType.Gauge,
            VariableBindingId = "pressure"
        });
        screen1.Controls.Add(new HmiControl
        {
            Id = "c3",
            Name = "GoToSettings",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Settings")
        });
        screen1.Controls.Add(new HmiControl
        {
            Id = "c4",
            Name = "SetTempBtn",
            Type = HmiControlType.Button,
            Action = HmiAction.SetVariable("temperature", "100")
        });
        project.Screens.Add(screen1);

        // 屏幕 2
        var screen2 = new HmiScreen { Id = "s2", Name = "Settings" };
        screen2.Controls.Add(new HmiControl
        {
            Id = "c5",
            Name = "BackButton",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Main")
        });
        project.Screens.Add(screen2);

        return project;
    }

    // ── 变量引用 ──

    [Fact]
    public void FindVariableReferences_BindingMatch_ReturnsReference()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "temperature");

        Assert.True(result.HasReferences);
        Assert.Equal(2, result.Count); // binding + SetVariable action
    }

    [Fact]
    public void FindVariableReferences_ByVariableId()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "v_temp");

        Assert.True(result.HasReferences);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindVariableReferences_NoReferences_ReturnsEmpty()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "nonexistent");

        Assert.False(result.HasReferences);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void FindVariableReferences_IncludesBindingLocations()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "pressure");

        Assert.True(result.HasReferences);
        Assert.Contains(result.References, r =>
            r.ReferenceType == ReferenceType.VariableBinding &&
            r.ControlName == "PressureGauge");
    }

    [Fact]
    public void FindVariableReferences_IncludesActionLocations()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "temperature");

        Assert.Contains(result.References, r =>
            r.ReferenceType == ReferenceType.ActionTarget &&
            r.ControlName == "SetTempBtn");
    }

    // ── 屏幕引用 ──

    [Fact]
    public void FindScreenReferences_ReturnsSwitchScreenActions()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindScreenReferences(project, "Settings");

        Assert.True(result.HasReferences);
        Assert.Equal(1, result.Count);
        Assert.Equal("GoToSettings", result.References[0].ControlName);
    }

    [Fact]
    public void FindScreenReferences_BothDirections()
    {
        var project = CreateTestProject();

        var mainRefs = CrossReferenceAnalyzer.FindScreenReferences(project, "Main");
        var settingsRefs = CrossReferenceAnalyzer.FindScreenReferences(project, "Settings");

        Assert.Equal(1, mainRefs.Count);   // BackButton → Main
        Assert.Equal(1, settingsRefs.Count); // GoToSettings → Settings
    }

    [Fact]
    public void FindScreenReferences_NoReferences_ReturnsEmpty()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindScreenReferences(project, "Nonexistent");

        Assert.False(result.HasReferences);
    }

    // ── 脚本引用 ──

    [Fact]
    public void FindScriptReferences_ByExecuteScriptAction()
    {
        var project = CreateTestProject();
        project.Screens[0].Controls[0].Action = HmiAction.ExecuteScript("script1");

        var result = CrossReferenceAnalyzer.FindScriptReferences(project, "script1");

        Assert.True(result.HasReferences);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void FindScriptReferences_ByScriptBinding()
    {
        var project = CreateTestProject();
        project.ScriptBindings.Add(new ScriptBinding
        {
            ScriptId = "script1",
            ControlId = "c1",
            EventName = "OnClick",
            FunctionName = "on_click"
        });

        var result = CrossReferenceAnalyzer.FindScriptReferences(project, "script1");

        Assert.True(result.HasReferences);
        Assert.Contains(result.References, r =>
            r.ReferenceType == ReferenceType.ScriptBinding);
    }

    [Fact]
    public void FindScriptReferences_NoReferences_ReturnsEmpty()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindScriptReferences(project, "nonexistent");

        Assert.False(result.HasReferences);
    }

    // ── 安全删除检查 ──

    [Fact]
    public void CanSafelyDeleteVariable_WithReferences_ReturnsFalse()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.CanSafelyDeleteVariable(project, "temperature");

        Assert.False(result);
    }

    [Fact]
    public void CanSafelyDeleteVariable_WithoutReferences_ReturnsTrue()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.CanSafelyDeleteVariable(project, "nonexistent");

        Assert.True(result);
    }

    [Fact]
    public void CanSafelyDeleteScreen_WithReferences_ReturnsFalse()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.CanSafelyDeleteScreen(project, "Settings");

        Assert.False(result);
    }

    [Fact]
    public void CanSafelyDeleteScreen_WithoutReferences_ReturnsTrue()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.CanSafelyDeleteScreen(project, "Nonexistent");

        Assert.True(result);
    }

    // ── 所有引用变量集合 ──

    [Fact]
    public void GetAllReferencedVariableIds_ReturnsAllBindings()
    {
        var project = CreateTestProject();

        var refs = CrossReferenceAnalyzer.GetAllReferencedVariableIds(project);

        Assert.Contains("temperature", refs);
        Assert.Contains("pressure", refs);
        Assert.True(refs.Count >= 2);
    }

    [Fact]
    public void GetAllReferencedVariableIds_IncludesActionTargets()
    {
        var project = CreateTestProject();

        var refs = CrossReferenceAnalyzer.GetAllReferencedVariableIds(project);

        // SetVariable action targets "temperature"
        Assert.Contains("temperature", refs);
    }

    [Fact]
    public void GetAllReferencedVariableIds_EmptyProject_ReturnsEmpty()
    {
        var project = new HmiProject();

        var refs = CrossReferenceAnalyzer.GetAllReferencedVariableIds(project);

        Assert.Empty(refs);
    }

    // ── UDT 字段引用 ──

    [Fact]
    public void FindVariableReferences_UdtField_ByCompoundId()
    {
        var project = new HmiProject();

        // UDT 定义 + 实例
        var udt = project.VariableManager.RegisterUdt("Motor");
        udt.AddField("speed", "Float");
        var motor1 = project.VariableManager.CreateUdtInstance(udt, "motor1");
        motor1.Id = "v_motor1";

        // 控件绑定到 UDT 字段
        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "SpeedGauge",
            Type = HmiControlType.Gauge,
            VariableBindingId = "motor1.speed"
        });
        project.Screens.Add(screen);

        // 按实例名查找
        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "motor1");

        Assert.True(result.HasReferences);
        Assert.Equal(1, result.Count);
    }

    // ── 引用位置信息完整性 ──

    [Fact]
    public void ReferenceLocation_ContainsScreenAndControlInfo()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "temperature");

        var bindingRef = result.References.FirstOrDefault(r =>
            r.ReferenceType == ReferenceType.VariableBinding);

        Assert.NotNull(bindingRef);
        Assert.Equal("Main", bindingRef!.ScreenName);
        Assert.Equal("s1", bindingRef.ScreenId);
        Assert.Equal("TempLabel", bindingRef.ControlName);
        Assert.Equal("c1", bindingRef.ControlId);
        Assert.Equal(HmiControlType.Label, bindingRef.ControlType);
    }

    [Fact]
    public void ReferenceLocation_ActionContainsDescription()
    {
        var project = CreateTestProject();

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "temperature");

        var actionRef = result.References.FirstOrDefault(r =>
            r.ReferenceType == ReferenceType.ActionTarget);

        Assert.NotNull(actionRef);
        Assert.NotEmpty(actionRef!.Description);
    }

    // ── 多引用场景 ──

    [Fact]
    public void MultipleControlsSameVariable_AllFound()
    {
        var project = new HmiProject();
        var variable = new Variable { Id = "v1", Name = "temp" };
        project.VariableManager.Variables.Add(variable);

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        for (var i = 0; i < 5; i++)
        {
            screen.Controls.Add(new HmiControl
            {
                Id = $"c{i}",
                Name = $"Control{i}",
                Type = HmiControlType.Label,
                VariableBindingId = "temp"
            });
        }
        project.Screens.Add(screen);

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "temp");

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void CrossScreenReferences_AllFound()
    {
        var project = new HmiProject();
        var variable = new Variable { Id = "v1", Name = "shared" };
        project.VariableManager.Variables.Add(variable);

        // 两个屏幕都引用同一个变量
        for (var s = 0; s < 2; s++)
        {
            var screen = new HmiScreen { Id = $"s{s}", Name = $"Screen{s}" };
            screen.Controls.Add(new HmiControl
            {
                Id = $"c{s}",
                Name = $"Control{s}",
                Type = HmiControlType.Label,
                VariableBindingId = "shared"
            });
            project.Screens.Add(screen);
        }

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "shared");

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.References.Select(r => r.ScreenId).Distinct().Count());
    }
}
