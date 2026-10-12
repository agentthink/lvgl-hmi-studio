using System.Collections.ObjectModel;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Faceplate 端到端集成测试 —— 验证 模板创建 → 实例化 → 代码生成 完整链路。
/// 确保数据流同步：project.Templates 是唯一数据源，CodeGenerator 读取同一集合。
/// </summary>
public class FaceplateIntegrationTests
{
    /// <summary>
    /// 验证完整的 Faceplate 管线：
    /// 1. 创建参数化模板并添加到项目
    /// 2. 创建 Faceplate 实例并放到屏幕上
    /// 3. 生成 C 代码，验证实例被正确展开
    /// </summary>
    [Fact]
    public void FullPipeline_TemplateInstantiate_CodeGen_ExpandsCorrectly()
    {
        // ── 1. 创建参数化模板 ──
        var template = new ControlTemplate
        {
            Id = "tpl_test_motor",
            Name = "测试电机面板",
            Category = "通用",
            Version = 1,
            Controls = new ObservableCollection<HmiControl>
            {
                new()
                {
                    Type = HmiControlType.Label, Name = "Title",
                    X = 0, Y = 0, Width = 120, Height = 24,
                    Text = "{{Parameters.Title}}"
                },
                new()
                {
                    Type = HmiControlType.Led, Name = "MotorLED",
                    X = 0, Y = 30, Width = 20, Height = 20,
                    VariableBindingId = "{{Parameters.StatusVar}}"
                }
            },
            Parameters = new ObservableCollection<TemplateParameter>
            {
                new()
                {
                    Name = "Title",
                    DisplayName = "标题",
                    Direction = ParameterDirection.Input,
                    Type = ParameterType.String,
                    DefaultValue = "Motor"
                },
                new()
                {
                    Name = "StatusVar",
                    DisplayName = "状态变量",
                    Direction = ParameterDirection.Input,
                    Type = ParameterType.VariableId
                }
            }
        };

        // ── 2. 设置项目（模板是项目的一部分） ──
        var project = new HmiProject { Name = "TestProject", LvglVersion = "9.2" };
        project.Templates.Add(template);

        var screen = new HmiScreen
        {
            Id = "main_screen",
            Name = "MainScreen",
            Width = 800,
            Height = 480
        };
        project.Screens.Add(screen);

        // ── 3. 创建 Faceplate 实例 ──
        var instance = new HmiControl
        {
            Id = "fp_inst_01",
            Type = HmiControlType.Screen, // 占位类型
            Name = "MotorPanel #001",
            X = 100,
            Y = 50,
            Width = 120,
            Height = 50,
            TemplateId = "tpl_test_motor",
            TemplateVersion = 1,
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "Title", Value = "主电机" },
                new() { ParameterName = "StatusVar", Value = "motor_speed_01", VariableId = "motor_speed_01" }
            }
        };
        screen.Controls.Add(instance);

        // ── 4. 生成代码 ──
        var generator = new CodeGenerator();
        var code = generator.GenerateFullProject(project);

        // ── 5. 验证实例被展开 ──
        // 模板内 Title 控件的 Text 占位符应被替换为 "主电机"
        Assert.Contains("主电机", code);

        // 模板内 LED 控件的 VariableBindingId 应被替换为 "motor_speed_01"
        Assert.Contains("motor_speed_01", code);

        // 不应残留未替换的占位符
        Assert.DoesNotContain("{{Parameters.Title}}", code);
        Assert.DoesNotContain("{{Parameters.StatusVar}}", code);
    }

    /// <summary>
    /// 验证多个 Faceplate 实例使用同一模板但不同参数。
    /// </summary>
    [Fact]
    public void MultipleInstances_SameTemplate_DifferentParams_AllExpanded()
    {
        var template = new ControlTemplate
        {
            Id = "tpl_valve",
            Name = "阀门",
            Version = 1,
            Controls = new ObservableCollection<HmiControl>
            {
                new()
                {
                    Type = HmiControlType.Label, Name = "ValveName",
                    X = 0, Y = 0, Width = 100, Height = 20,
                    Text = "{{Parameters.ValveName}}"
                }
            },
            Parameters = new ObservableCollection<TemplateParameter>
            {
                new()
                {
                    Name = "ValveName",
                    Direction = ParameterDirection.Input,
                    Type = ParameterType.String,
                    DefaultValue = "Valve"
                }
            }
        };

        var project = new HmiProject { Name = "MultiValve", LvglVersion = "9.2" };
        project.Templates.Add(template);

        var screen = new HmiScreen
        {
            Id = "valve_screen",
            Name = "ValveScreen",
            Width = 800, Height = 480
        };
        project.Screens.Add(screen);

        // 三个不同参数的实例
        screen.Controls.Add(new HmiControl
        {
            Id = "v1", Type = HmiControlType.Screen,
            X = 0, Y = 0, Width = 100, Height = 20,
            TemplateId = "tpl_valve",
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "ValveName", Value = "进料阀" }
            }
        });

        screen.Controls.Add(new HmiControl
        {
            Id = "v2", Type = HmiControlType.Screen,
            X = 200, Y = 0, Width = 100, Height = 20,
            TemplateId = "tpl_valve",
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "ValveName", Value = "出料阀" }
            }
        });

        screen.Controls.Add(new HmiControl
        {
            Id = "v3", Type = HmiControlType.Screen,
            X = 400, Y = 0, Width = 100, Height = 20,
            TemplateId = "tpl_valve",
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "ValveName", Value = "回流阀" }
            }
        });

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("进料阀", code);
        Assert.Contains("出料阀", code);
        Assert.Contains("回流阀", code);
        Assert.DoesNotContain("{{Parameters.ValveName}}", code);
    }

    /// <summary>
    /// 验证序列化往返：模板和 Faceplate 实例保存后加载保持一致。
    /// </summary>
    [Fact]
    public void SerializationRoundTrip_TemplatesAndInstances_Preserved()
    {
        var project = new HmiProject { Name = "SerializeTest", LvglVersion = "9.2" };

        var template = new ControlTemplate
        {
            Id = "tpl_roundtrip",
            Name = "往返测试模板",
            Category = "测试",
            Version = 3,
            Controls = new ObservableCollection<HmiControl>
            {
                new() { Type = HmiControlType.Label, Name = "Lbl", X = 0, Y = 0, Width = 80, Height = 20, Text = "{{Parameters.Name}}" }
            },
            Parameters = new ObservableCollection<TemplateParameter>
            {
                new() { Name = "Name", DisplayName = "名称", Direction = ParameterDirection.Input, Type = ParameterType.String, DefaultValue = "Default" }
            }
        };
        project.Templates.Add(template);

        var screen = new HmiScreen { Id = "s1", Name = "Screen1", Width = 800, Height = 480 };
        project.Screens.Add(screen);

        screen.Controls.Add(new HmiControl
        {
            Id = "fp1", Type = HmiControlType.Screen,
            X = 50, Y = 50, Width = 80, Height = 20,
            TemplateId = "tpl_roundtrip",
            TemplateVersion = 3,
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "Name", Value = "测试值" }
            }
        });

        // 序列化
        var json = ProjectSerializer.Serialize(project);
        Assert.NotEmpty(json);

        // 反序列化
        var loaded = ProjectSerializer.Deserialize(json);
        Assert.NotNull(loaded);

        // 验证模板
        Assert.Single(loaded!.Templates);
        var loadedTemplate = loaded.Templates[0];
        Assert.Equal("tpl_roundtrip", loadedTemplate.Id);
        Assert.Equal("往返测试模板", loadedTemplate.Name);
        Assert.Equal(3, loadedTemplate.Version);
        Assert.Single(loadedTemplate.Parameters);
        Assert.Equal("Name", loadedTemplate.Parameters[0].Name);

        // 验证 Faceplate 实例
        Assert.Single(loaded.Screens);
        var loadedScreen = loaded.Screens[0];
        Assert.Single(loadedScreen.Controls);
        var loadedInstance = loadedScreen.Controls[0];
        Assert.Equal("tpl_roundtrip", loadedInstance.TemplateId);
        Assert.Equal(3, loadedInstance.TemplateVersion);
        Assert.Single(loadedInstance.ParameterBindings);
        Assert.Equal("测试值", loadedInstance.ParameterBindings[0].Value);
    }

    /// <summary>
    /// 验证加载的项目可以正确生成代码 —— 端到端序列化+生成。
    /// </summary>
    [Fact]
    public void SerializeThenCodeGen_ExpandedCorrectly()
    {
        var project = new HmiProject { Name = "E2E", LvglVersion = "9.2" };

        var template = new ControlTemplate
        {
            Id = "tpl_e2e",
            Name = "E2E模板",
            Version = 1,
            Controls = new ObservableCollection<HmiControl>
            {
                new() { Type = HmiControlType.Label, Name = "T", X = 0, Y = 0, Width = 60, Height = 16, Text = "{{Parameters.Label}}" }
            },
            Parameters = new ObservableCollection<TemplateParameter>
            {
                new() { Name = "Label", Direction = ParameterDirection.Input, Type = ParameterType.String, DefaultValue = "N/A" }
            }
        };
        project.Templates.Add(template);

        var screen = new HmiScreen { Id = "e2e_screen", Name = "E2EScreen", Width = 320, Height = 240 };
        project.Screens.Add(screen);
        screen.Controls.Add(new HmiControl
        {
            Id = "e2e_inst", Type = HmiControlType.Screen,
            X = 10, Y = 10, Width = 60, Height = 16,
            TemplateId = "tpl_e2e",
            ParameterBindings = new ObservableCollection<ParameterBinding>
            {
                new() { ParameterName = "Label", Value = "温度" }
            }
        });

        // 序列化 → 反序列化 → 代码生成
        var json = ProjectSerializer.Serialize(project);
        var loaded = ProjectSerializer.Deserialize(json);
        Assert.NotNull(loaded);

        var code = new CodeGenerator().GenerateFullProject(loaded!);

        Assert.Contains("温度", code);
        Assert.DoesNotContain("{{Parameters.Label}}", code);
    }
}
