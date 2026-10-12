using System.Reflection;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// FaceplateExpander 单元测试 —— 验证模板展开、坐标平移、参数替换、
/// 嵌套展开、循环依赖检测、深度限制及 CodeGenerator 集成。
/// </summary>
public class FaceplateExpanderTests
{
    /// <summary>构造一个"电机面板"模板：含标题 Label + 状态 Label，带两个参数。</summary>
    private static ControlTemplate CreateMotorTemplate()
    {
        var template = new ControlTemplate
        {
            Id = "tpl_motor",
            Name = "Motor Panel",
            Version = 1
        };

        // 标题标签 —— 引用 {{Parameters.Title}}
        template.Controls.Add(new HmiControl
        {
            Id = "t_title",
            Type = HmiControlType.Label,
            Name = "Title",
            X = 0, Y = 0, Width = 120, Height = 24,
            Text = "{{Parameters.Title}}"
        });

        // 状态标签 —— 引用 {{Parameters.StatusVar}}
        template.Controls.Add(new HmiControl
        {
            Id = "t_status",
            Type = HmiControlType.Label,
            Name = "Status",
            X = 0, Y = 30, Width = 120, Height = 24,
            Text = "Idle",
            VariableBindingId = "{{Parameters.StatusVar}}"
        });

        // 启动按钮 —— 联动模型: 绑定变量 + 动作 Target 均引用 {{Parameters.StatusVar}}
        var startButton = new HmiControl
        {
            Id = "t_start",
            Type = HmiControlType.Button,
            Name = "Start",
            X = 0, Y = 60, Width = 120, Height = 40,
            Text = "启动"
        };
        startButton.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "{{Parameters.StatusVar}}",
            Direction = BindingDirection.ReadWrite
        });
        startButton.Actions.Add(new HmiAction
        {
            ActionType = "SetVariable",
            Target = "{{Parameters.StatusVar}}",
            Value = "{{Parameters.Value}}",
            TriggerEvent = "OnClick"
        });
        template.Controls.Add(startButton);

        // 参数定义
        template.Parameters.Add(new TemplateParameter
        {
            Name = "Title",
            DisplayName = "标题",
            Type = ParameterType.String,
            DefaultValue = "Motor"
        });
        template.Parameters.Add(new TemplateParameter
        {
            Name = "StatusVar",
            DisplayName = "状态变量",
            Type = ParameterType.VariableId
        });
        template.Parameters.Add(new TemplateParameter
        {
            Name = "Value",
            DisplayName = "设定值",
            Type = ParameterType.String,
            DefaultValue = "1"
        });

        return template;
    }

    [Fact]
    public void ExpandScreen_NonFaceplateControl_PreservedAsIs()
    {
        var expander = new FaceplateExpander(new List<ControlTemplate>());
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var ctrl = new HmiControl { Id = "c1", Type = HmiControlType.Label, Name = "Standalone", Text = "Hello" };
        screen.Controls.Add(ctrl);

        var result = expander.ExpandScreen(screen);

        Assert.Single(result);
        Assert.Same(ctrl, result[0]);
    }

    [Fact]
    public void ExpandScreen_FaceplateInstance_ReplacedWithTemplateControls()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        // 画布上的 Faceplate 实例
        var instance = new HmiControl
        {
            Id = "inst1",
            Type = HmiControlType.Screen, // 占位类型
            Name = "Motor #1",
            TemplateId = "tpl_motor",
            X = 100, Y = 50
        };
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        // 应展开为 3 个控件（标题 + 状态 + 启动按钮），原 Faceplate 实例不再存在
        Assert.Equal(3, result.Count);
        Assert.All(result, c => Assert.NotEqual("inst1", c.Id));
        Assert.All(result, c => Assert.True(string.IsNullOrEmpty(c.TemplateId)));
    }

    [Fact]
    public void ExpandScreen_CoordinateTranslation_AppliedToClonedControls()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 200, Y = 100
        };
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        // 标题控件模板坐标 (0,0) + 实例 (200,100) = (200,100)
        var title = result.First(c => c.Name == "Title");
        Assert.Equal(200, title.X);
        Assert.Equal(100, title.Y);

        // 状态控件模板坐标 (0,30) + 实例 (200,100) = (200,130)
        var status = result.First(c => c.Name == "Status");
        Assert.Equal(200, status.X);
        Assert.Equal(130, status.Y);
    }

    [Fact]
    public void ExpandScreen_ParameterSubstitution_ReplacesPlaceholders()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 0, Y = 0
        };
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Title",
            Value = "Pump A"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "StatusVar",
            VariableId = "var_pump_a_status"
        });
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        var title = result.First(c => c.Name == "Title");
        Assert.Equal("Pump A", title.Text);
        Assert.DoesNotContain("{{Parameters.", title.Text);

        var status = result.First(c => c.Name == "Status");
        Assert.Equal("var_pump_a_status", status.VariableBindingId);
    }

    [Fact]
    public void ExpandScreen_ActionsAndBindings_PlaceholdersReplaced()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 0, Y = 0
        };
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Title",
            Value = "Pump A"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "StatusVar",
            VariableId = "var_pump_a_status"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Value",
            Value = "50"
        });
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        // 联动模型: 按钮的 VariableBindings.VariableId 被替换
        var start = result.First(c => c.Name == "Start");
        Assert.Equal("var_pump_a_status", start.VariableBindings[0].VariableId);

        // 联动模型: Actions 集合的 Target/Value 被替换
        Assert.Single(start.Actions);
        Assert.Equal("var_pump_a_status", start.Actions[0].Target);
        Assert.Equal("50", start.Actions[0].Value);
        Assert.DoesNotContain("{{Parameters.", start.Actions[0].Target);
        Assert.DoesNotContain("{{Parameters.", start.Actions[0].Value);
    }

    [Fact]
    public void ExpandScreen_DefaultValue_UsedWhenNoBinding()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        // 实例不提供 Title 绑定 —— 应使用模板默认值 "Motor"
        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 0, Y = 0
        };
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        var title = result.First(c => c.Name == "Title");
        Assert.Equal("Motor", title.Text);
    }

    [Fact]
    public void ExpandScreen_NestedFaceplate_ExpandedRecursively()
    {
        // 内层模板：单个 Label
        var innerTemplate = new ControlTemplate
        {
            Id = "tpl_inner",
            Name = "Inner",
            Controls =
            {
                new HmiControl
                {
                    Id = "inner_lbl",
                    Type = HmiControlType.Label,
                    Name = "InnerLabel",
                    X = 5, Y = 5, Width = 50, Height = 20,
                    Text = "Inner"
                }
            }
        };

        // 外层模板：含一个嵌套 Faceplate 实例
        var outerTemplate = new ControlTemplate
        {
            Id = "tpl_outer",
            Name = "Outer",
            Controls =
            {
                new HmiControl
                {
                    Id = "outer_lbl",
                    Type = HmiControlType.Label,
                    Name = "OuterLabel",
                    X = 0, Y = 0, Width = 100, Height = 20,
                    Text = "Outer"
                },
                new HmiControl
                {
                    Id = "nested_inst",
                    Type = HmiControlType.Screen,
                    Name = "Nested",
                    TemplateId = "tpl_inner",
                    X = 10, Y = 30
                }
            }
        };

        var expander = new FaceplateExpander(new[] { innerTemplate, outerTemplate });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        // 画布上的外层 Faceplate 实例
        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_outer",
            X = 100, Y = 200
        };
        screen.Controls.Add(instance);

        var result = expander.ExpandScreen(screen);

        // 外层 1 个 Label + 内层展开 1 个 Label = 2 个原子控件
        Assert.Equal(2, result.Count);

        // 内层 Label 坐标：模板内 (5,5) + 外层嵌套实例 (10,30) + 外层实例 (100,200) = (115,235)
        var innerLabel = result.First(c => c.Name == "InnerLabel");
        Assert.Equal(115, innerLabel.X);
        Assert.Equal(235, innerLabel.Y);
    }

    [Fact]
    public void ExpandScreen_CircularDependency_DetectedAndSkipped()
    {
        // 模板 A 引用模板 B，模板 B 引用模板 A —— 循环
        var templateA = new ControlTemplate
        {
            Id = "tpl_a",
            Name = "A",
            Controls =
            {
                new HmiControl
                {
                    Id = "a_nested",
                    TemplateId = "tpl_b",
                    X = 0, Y = 0
                }
            }
        };
        var templateB = new ControlTemplate
        {
            Id = "tpl_b",
            Name = "B",
            Controls =
            {
                new HmiControl
                {
                    Id = "b_nested",
                    TemplateId = "tpl_a",
                    X = 0, Y = 0
                }
            }
        };

        var expander = new FaceplateExpander(new[] { templateA, templateB });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_a",
            X = 0, Y = 0
        });

        // 不应抛异常，也不应无限递归 —— 循环部分被跳过，返回空或有限结果
        var result = expander.ExpandScreen(screen);
        Assert.NotNull(result);
    }

    [Fact]
    public void ExpandScreen_TemplateNotFound_ReturnsEmptyForInstance()
    {
        var expander = new FaceplateExpander(new List<ControlTemplate>());
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "inst1",
            TemplateId = "nonexistent",
            X = 0, Y = 0
        });

        var result = expander.ExpandScreen(screen);

        // 模板不存在时，该实例展开为空
        Assert.Empty(result);
    }

    [Fact]
    public void GenerateScreenCode_WithExpander_ExpandsFaceplateIntoCode()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var generator = new CodeGenerator();

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 50, Y = 60
        };
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Title",
            Value = "Valve X"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "StatusVar",
            VariableId = "var_valve_status"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Value",
            Value = "1"
        });
        screen.Controls.Add(instance);

        var code = generator.GenerateScreenCode(screen, null, expander);

        // 展开后的控件应出现在生成的 C 代码中
        Assert.Contains("lv_label_create", code);
        Assert.Contains("Valve X", code);
        // 原始占位符不应出现在最终代码中
        Assert.DoesNotContain("{{Parameters.", code);
    }

    [Fact]
    public void GenerateFullProject_WithTemplates_ExpandsFaceplates()
    {
        var template = CreateMotorTemplate();
        var generator = new CodeGenerator();

        var project = new HmiProject { LvglVersion = "9.2" };
        project.Templates.Add(template);

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var instance = new HmiControl
        {
            Id = "inst1",
            TemplateId = "tpl_motor",
            X = 10, Y = 20
        };
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Title",
            Value = "Conveyor"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "StatusVar",
            VariableId = "var_conveyor_status"
        });
        instance.ParameterBindings.Add(new ParameterBinding
        {
            ParameterName = "Value",
            Value = "1"
        });
        screen.Controls.Add(instance);
        project.Screens.Add(screen);

        var code = generator.GenerateFullProject(project);

        Assert.Contains("Conveyor", code);
        Assert.DoesNotContain("{{Parameters.", code);
    }

    // ───────────────────── 克隆 id 的确定性（导出可复现的前提） ─────────────────────

    /// <summary>
    /// 展开出的克隆控件 id 必须是**确定性**的。
    ///
    /// <para><b>为什么守这个</b>：克隆 id 直接进 C 符号名（<c>CodeGenerator.Screen.cs</c> 的
    /// <c>static lv_obj_t* obj_{control.Id};</c>，以及 SVG / 过程图形的 <c>_shapebuf_{Id}</c>），
    /// 而 <see cref="FaceplateExpander.ExpandScreen"/> 在 <c>GenerateSimulatorModule</c>
    /// （<c>Simulator.cs</c>）与 <c>GenerateSharedLibraryProject</c>（<c>SharedLibrary.cs</c>）
    /// 里都会被调用 —— 于是只要项目里有 Faceplate 实例，导出产物就**不可复现**：
    /// 原实现用 <c>Guid.NewGuid().ToString("N")[..8]</c>，实测同一份源码在两个独立进程里
    /// 19/34 个产物不同，产物里是 <c>obj_f1b2b995</c> 对 <c>obj_235079a3</c>。</para>
    ///
    /// <para><c>Guid</c> 是**每次调用**都随机（这点与 <c>string.GetHashCode()</c> 的"每进程加盐"
    /// 不同），所以同进程展开两次不一致就足以抓住它 —— 本测试因此不需要跨进程。</para>
    /// </summary>
    [Fact]
    public void ExpandScreen_ClonedControlIds_AreDeterministic()
    {
        var template = CreateMotorTemplate();
        var screen = ScreenWithInstance("inst1");

        var first = new FaceplateExpander(new[] { template }).ExpandScreen(screen)
            .Select(c => c.Id).ToList();
        var second = new FaceplateExpander(new[] { template }).ExpandScreen(screen)
            .Select(c => c.Id).ToList();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// id 由「实例 id + 模板内序号」确定性派生 —— 黄金值钉死规则本身，
    /// 顺带把产物里的符号名固定下来（改规则必须是有意为之并同步本表）。
    /// </summary>
    [Fact]
    public void ExpandScreen_ClonedControlIds_FollowInstanceIdAndTemplateIndex()
    {
        var template = CreateMotorTemplate();   // 3 个控件：t_title / t_status / t_start

        var ids = new FaceplateExpander(new[] { template })
            .ExpandScreen(ScreenWithInstance("a8f3c1d2"))
            .Select(c => c.Id).ToList();

        Assert.Equal(new[] { "a8f3c1d2_0", "a8f3c1d2_1", "a8f3c1d2_2" }, ids);
    }

    /// <summary>
    /// 实例 id 里的非标识符字符先净化再派生 —— 克隆 id 会原样进
    /// <c>obj_{Id}</c>，留着 <c>-</c> / 空格会生成非法 C（而其它引用点走的是
    /// <c>CIdentifier.ForControlSymbol</c>，届时两边符号名还会对不上）。
    /// </summary>
    [Fact]
    public void ExpandScreen_InstanceIdNeedingSanitization_IsSanitizedBeforeDerivation()
    {
        var template = CreateMotorTemplate();

        var ids = new FaceplateExpander(new[] { template })
            .ExpandScreen(ScreenWithInstance("a-b c"))
            .Select(c => c.Id).ToList();

        Assert.Equal(new[] { "a_b_c_0", "a_b_c_1", "a_b_c_2" }, ids);
    }

    /// <summary>
    /// 同一模板的两个实例必须产出**互不相交**的 id 集合。
    /// 发射器把 <c>obj_*</c> 声明为文件作用域静态且不带屏幕前缀，重名即重复符号。
    /// </summary>
    [Fact]
    public void ExpandScreen_TwoInstances_ProduceDisjointIds()
    {
        var template = CreateMotorTemplate();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(Instance("instA"));
        screen.Controls.Add(Instance("instB"));

        var ids = new FaceplateExpander(new[] { template }).ExpandScreen(screen)
            .Select(c => c.Id).ToList();

        Assert.Equal(6, ids.Count);
        Assert.Equal(6, ids.Distinct().Count());
    }

    /// <summary>子控件的 id 接在父克隆 id 之后，形成「实例 → 模板控件 → 子控件」的可追溯路径。</summary>
    [Fact]
    public void ExpandScreen_ClonedChildren_GetPathDerivedIds()
    {
        var template = new ControlTemplate { Id = "tpl_grp", Name = "分组", Version = 1 };
        var group = new HmiControl
        {
            Id = "t_grp", Type = HmiControlType.Rectangle, Width = 100, Height = 100,
        };
        group.Children.Add(new HmiControl { Id = "t_c0", Type = HmiControlType.Label, Width = 40, Height = 20 });
        group.Children.Add(new HmiControl { Id = "t_c1", Type = HmiControlType.Label, Width = 40, Height = 20 });
        template.Controls.Add(group);

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var instance = new HmiControl { Id = "instG", TemplateId = "tpl_grp", X = 0, Y = 0 };
        screen.Controls.Add(instance);

        var expanded = new FaceplateExpander(new[] { template }).ExpandScreen(screen);

        var ids = expanded
            .SelectMany(c => new[] { c.Id }.Concat(c.Children.Select(ch => ch.Id)))
            .ToList();

        Assert.Equal(new[] { "instG_0", "instG_0_0", "instG_0_1" }, ids);
    }

    /// <summary>嵌套展开时，内层克隆重用外层克隆 id 作前缀 —— 仍是确定性路径。</summary>
    [Fact]
    public void ExpandScreen_NestedFaceplate_IdsRemainDeterministicPrefixChain()
    {
        // 内层模板：一个 Label
        var inner = new ControlTemplate { Id = "tpl_inner", Name = "内层", Version = 1 };
        inner.Controls.Add(new HmiControl { Id = "i_lbl", Type = HmiControlType.Label, Width = 40, Height = 20 });

        // 外层模板：一个指向内层模板的嵌套实例
        var outer = new ControlTemplate { Id = "tpl_outer", Name = "外层", Version = 1 };
        outer.Controls.Add(new HmiControl
        {
            Id = "o_slot", Type = HmiControlType.Rectangle, Width = 60, Height = 60,
            TemplateId = "tpl_inner", TemplateVersion = 1,
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "instOuter", TemplateId = "tpl_outer", X = 0, Y = 0,
        });

        var ids = new FaceplateExpander(new[] { inner, outer }).ExpandScreen(screen)
            .Select(c => c.Id).ToList();

        // 外层第 0 个控件 → instOuter_0（它自身是嵌套实例，被内层展开替代）
        // 内层第 0 个控件 → instOuter_0_0
        Assert.Equal(new[] { "instOuter_0_0" }, ids);
    }

    /// <summary>
    /// 端到端：带 Faceplate 实例的项目，模拟器模块必须**逐字节可复现**，
    /// 且产物里出现派生出的符号名 —— 把「确定性 id → 生成产物」串起来。
    /// </summary>
    [Fact]
    public void GenerateSimulatorModule_WithFaceplate_IsReproducibleAndUsesDerivedSymbols()
    {
        var first = new CodeGenerator().GenerateSimulatorModule(ProjectWithInstance("inst1"), null);
        var second = new CodeGenerator().GenerateSimulatorModule(ProjectWithInstance("inst1"), null);

        Assert.Equal(first, second);
        CSource.AssertText(first, "static lv_obj_t* obj_inst1_0;");
        CSource.AssertText(first, "static lv_obj_t* obj_inst1_1;");
        CSource.AssertText(first, "static lv_obj_t* obj_inst1_2;");
    }

    /// <summary>
    /// **属性保真**守卫：展开后的克隆控件必须保留模板控件的**每一个**属性
    /// （除 id / 坐标 / 父子关系这些"展开语义"必然改动的字段）。
    ///
    /// <para>为什么用反射而不是逐条断言：<c>HmiControl</c> 有 80+ 属性，逐条列举**必然随新增字段漂移** ——
    /// 旧 <c>CloneControl</c> 手工赋了 18 个属性，漏掉 ShowPointer / ShowTicks / MinValue / MaxValue /
    /// StepValue / IndicatorColor / Rotation / ImageAssetId / TextAlign / FontSize / Points / Cells /
    /// SvgShapes / VisualStates 等约 50 项，于是 Faceplate 里的 SVG / 表格 / 滑块范围 / 折线 / 图片
    /// 展开后**全部退化成默认值**（设计器里配好、产物里全无）。反射版让"将来新增的属性"自动被覆盖。</para>
    /// </summary>
    [Fact]
    public void ExpandScreen_Clone_PreservesEveryTemplateProperty()
    {
        var template = new ControlTemplate { Id = "tpl_dense", Name = "密集配置", Version = 1 };
        var source = CreateDenseControl();
        template.Controls.Add(source);

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl { Id = "inst_dense", TemplateId = "tpl_dense", X = 0, Y = 0 });

        var clone = Assert.Single(new FaceplateExpander(new[] { template }).ExpandScreen(screen));

        // 展开语义必然改动的字段：id 派生、坐标平移、父子关系由 Children 重建
        var excluded = new HashSet<string>
        {
            nameof(HmiControl.Id), nameof(HmiControl.X), nameof(HmiControl.Y),
            nameof(HmiControl.ParentId), nameof(HmiControl.Children),
        };

        var mismatched = new List<string>();
        foreach (var p in typeof(HmiControl).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0 || excluded.Contains(p.Name)) continue;
            var expected = p.GetValue(source);
            var actual = p.GetValue(clone);
            if (!ValueEquals(expected, actual))
                mismatched.Add($"{p.Name}: 模板=[{Describe(expected)}] 克隆=[{Describe(actual)}]");
        }

        Assert.True(mismatched.Count == 0,
            "Faceplate 展开丢了模板控件的属性（克隆体退化成默认值 → 样式/数值范围/图形/Cells 全失效）："
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", mismatched));
    }

    /// <summary>
    /// **集合不共享**守卫：同一模板的两个实例绑不同实参时，各自展开出的克隆必须**互不影响**。
    ///
    /// <para>克隆是浅拷贝（<c>HmiControl.ShallowCopy</c>）实现的，若忘了把 <c>VariableBindings</c> /
    /// <c>Actions</c> 这类集合换成新实例，副本就与**模板本身**共享同一个集合 ——
    /// 第一个实例的参数替换会把占位符直接写进模板，于是第二个实例拿到的是第一个实例的值，
    /// 并且源工程被静默改脏（与"展开产物可复现"直接冲突）。</para>
    /// </summary>
    [Fact]
    public void ExpandScreen_TwoInstances_WithDifferentParameterValues_AreIndependent()
    {
        var template = CreateMotorTemplate();
        var expander = new FaceplateExpander(new[] { template });
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(InstanceWithStatusVar("instA", "var_A"));
        screen.Controls.Add(InstanceWithStatusVar("instB", "var_B"));

        var result = expander.ExpandScreen(screen);

        // 占位符在**集合元素**里（startButton.VariableBindings[0].VariableId）—— 只有集合被共享才会串味
        var startA = result.First(c => c.Name == "Start" && c.Id.StartsWith("instA"));
        var startB = result.First(c => c.Name == "Start" && c.Id.StartsWith("instB"));
        Assert.Equal("var_A", startA.VariableBindings[0].VariableId);
        Assert.Equal("var_B", startB.VariableBindings[0].VariableId);

        // 展开过程不得改动模板本身：占位符应原样留着，供下一个实例继续替换
        var templateStart = template.Controls.First(c => c.Name == "Start");
        Assert.Equal("{{Parameters.StatusVar}}", templateStart.VariableBindings[0].VariableId);
    }

    // ───────────────────── fixture ─────────────────────

    /// <summary>造一个"每个可写属性都设成非默认值"的控件 —— 属性保真守卫的语料。</summary>
    private static HmiControl CreateDenseControl()
    {
        var c = new HmiControl
        {
            Id = "t_dense",
            Type = HmiControlType.Table,
            Name = "密集",
            X = 11, Y = 22, Width = 33, Height = 44,
            Rotation = 45, RotationDirection = "ccw", MirrorX = true, MirrorY = true,
            IsLocked = true, IsVisible = false, LayerIndex = 3,
            Text = "文本", Value = 42, IsChecked = true,
            BgColor = "#112233", TextColor = "#445566", Radius = 7, BorderWidth = 2, BorderColor = "#778899",
            ImageAssetId = "img_1",
            TextAlign = TextAlignMode.Right, TextVAlign = TextVAlignMode.Bottom,
            FontFamily = "fangsong", FontSize = 22,
            ValueDisplayType = ValueDisplayType.Float, ValueScaleFactor = 2.5, ValueDecimalPlaces = 4,
            ValuePrefix = "P:", ValueSuffix = ":S",
            MinValue = 5, MaxValue = 250, StepValue = 7, ShowTicks = true, ShowPointer = true,
            SelectedIndex = 2, Options = "a\nb\nc", PlaceholderText = "请选择", MaxLength = 12, PasswordMode = true,
            MajorTickCount = 9, RowCount = 4, ColumnCount = 5, HeaderTexts = "h1|h2", RowHeight = 21,
            ColumnWidths = "30|70", TabTitles = "t1|t2", StretchMode = "Contain",
            Brightness = 200, SpinSpeed = 500, LineCap = "Round",
            KeyboardMode = "Number", TankShowScale = false, TankTickCount = 8,
            PipeDirection = "vertical", PipeFlowDirection = "reverse", PipeFlowSpeed = 900,
            ValveType = "butterfly", IndicatorColor = "#aabbcc",
            LabelLongMode = "wrap", ValueMode = "raw", Opacity = 66,
            GroupId = "g1", BindingMode = "ReadOnly", VariableBindingId = "var_legacy",
            TemplateVersion = 3, SvgViewBoxWidth = 55, SvgViewBoxHeight = 66,
            Cells = new List<TableCellData>
            {
                new() { Row = 1, Col = 2, Text = "cell", VariableId = "v1", Prefix = "p", Suffix = "s", DecimalPlaces = 3 },
            },
            Points = new List<PointData> { new(1, 2), new(3, 4) },
            SvgShapes = new List<SvgShape>
            {
                new()
                {
                    Kind = SvgShapeKind.Circle, Points = new double[] { 1, 2, 3 },
                    PathData = "M0 0", Fill = "#fff", Stroke = "#000", StrokeWidth = 3,
                },
            },
        };
        c.Action = new HmiAction { ActionType = "SetVariable", Target = "v1", Value = "1", TriggerEvent = "OnClick" };
        c.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value", VariableId = "v1", Direction = BindingDirection.ReadWrite,
            Kind = BindingSourceKind.Variable, Expression = "x+1",
        });
        c.Actions.Add(new HmiAction { ActionType = "SwitchScreen", Target = "s2", Value = "v2", TriggerEvent = "OnPress" });
        c.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Pressed, BgColor = "#010203", TextColor = "#040506", BorderColor = "#070809",
            ImageAssetId = "img_2", Text = "按下", TextAlign = TextAlignMode.Left, TextVAlign = TextVAlignMode.Top,
        });
        c.ParameterBindings.Add(new ParameterBinding { ParameterName = "P1", Value = "pv", VariableId = "v2" });
        return c;
    }

    /// <summary>
    /// 值/序列/对象比较 —— 序列按元素递归比较，普通对象按公共属性比较。
    /// 模型类是纯数据类且**未重写 Equals**，直接用 Equals 会退化成引用比较（假红）。
    /// </summary>
    private static bool ValueEquals(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.GetType() != b.GetType()) return false;
        if (a is string || a.GetType().IsValueType) return a.Equals(b);
        if (a is System.Collections.IEnumerable ea && b is System.Collections.IEnumerable eb)
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            return la.Count == lb.Count && la.Zip(lb).All(p => ValueEquals(p.First, p.Second));
        }
        return ElementEquals(a, b);
    }

    /// <summary>按公共属性逐个比较（用于未重写 Equals 的模型类，如 HmiAction / ParameterBinding / SvgShape）。</summary>
    private static bool ElementEquals(object a, object b)
    {
        foreach (var p in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            if (!ValueEquals(p.GetValue(a), p.GetValue(b))) return false;
        }
        return true;
    }

    private static string Describe(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return s;
        if (value is System.Collections.IEnumerable e)
            return "[" + string.Join("|", e.Cast<object?>().Select(Describe)) + "]";
        if (value.GetType().IsValueType) return value.ToString() ?? "?";
        var props = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => $"{p.Name}={Describe(p.GetValue(value))}");
        return "{" + string.Join(",", props) + "}";
    }

    private static HmiControl InstanceWithStatusVar(string id, string variableId)
    {
        var instance = Instance(id);
        instance.ParameterBindings.Add(new ParameterBinding { ParameterName = "StatusVar", VariableId = variableId });
        return instance;
    }

    private static HmiControl Instance(string id) => new()
    {
        Id = id,
        TemplateId = "tpl_motor",
        TemplateVersion = 1,
        X = 10,
        Y = 20,
    };

    private static HmiScreen ScreenWithInstance(string instanceId)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(Instance(instanceId));
        return screen;
    }

    private static HmiProject ProjectWithInstance(string instanceId)
    {
        var project = new HmiProject { Name = "FaceplateRepro", LvglVersion = "9.5" };
        project.Templates.Add(CreateMotorTemplate());
        project.Screens.Add(ScreenWithInstance(instanceId));
        return project;
    }
}
