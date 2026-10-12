using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 运行时标签表代码生成单元测试 —— 验证绑定引擎 → C 变量表 → 动作回调集成。
/// </summary>
public class RuntimeTagsCodeGenTests
{
    // ── 标签表生成 ──

    [Fact]
    public void Generate_EmptyTags_ReturnsNoTagsMarker()
    {
        var code = new RuntimeTagsCodeGen().Generate(Array.Empty<RuntimeTag>());
        Assert.Contains("No bound variables", code);
        Assert.DoesNotContain("TAG_COUNT", code);
    }

    [Fact]
    public void Generate_Tags_ProducesEnumAndTable()
    {
        var tags = CreateTags();
        var code = new RuntimeTagsCodeGen().Generate(tags);

        Assert.Contains("typedef enum", code);
        Assert.Contains("TAG_v_temp = 0", code);
        Assert.Contains("TAG_v_pres = 1", code);
        Assert.Contains("TAG_COUNT", code);
        Assert.Contains("static const tag_meta_t TAG_TABLE[TAG_COUNT]", code);
        // v_temp: 可写(0), scale=2, offset=5
        Assert.Contains("\"v_temp\", \"40001\", 0, 2", code);
        Assert.Contains("5", code); // offset
        // v_pres: 只读(1)
        Assert.Contains("\"v_pres\", \"40002\", 1", code);
    }

    [Fact]
    public void Generate_IncludesReadWriteHelpers()
    {
        var code = new RuntimeTagsCodeGen().Generate(CreateTags());

        CSource.AssertCode(code, "extern float var_raw_read(int index)");
        CSource.AssertCode(code, "extern void var_raw_write(int index, float raw)");
        Assert.Contains("static inline float get_tag_value", code);
        Assert.Contains("static inline uint8_t set_tag_value", code);
        Assert.Contains("read_only", code); // 只读保护
        Assert.Contains("static inline int tag_index_by_name", code);
        Assert.Contains("strcmp", code);
    }

    [Fact]
    public void Generate_ReadOnlyTag_MarksReadOnly()
    {
        var tags = new[]
        {
            new RuntimeTag { VariableId = "v_ro", Address = "1", Direction = BindingDirection.ReadOnly }
        };
        var code = new RuntimeTagsCodeGen().Generate(tags);

        Assert.Contains("\"v_ro\", \"1\", 1,", code);
    }

    [Fact]
    public void Generate_WritableTag_NotReadOnly()
    {
        var tags = new[]
        {
            new RuntimeTag { VariableId = "v_rw", Address = "2", Direction = BindingDirection.ReadWrite }
        };
        var code = new RuntimeTagsCodeGen().Generate(tags);

        Assert.Contains("\"v_rw\", \"2\", 0,", code);
    }

    // ── 全项目集成 ──

    [Fact]
    public void GenerateFullProject_WithBindings_EmitsTagTable()
    {
        var project = CreateProject();
        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("Runtime Tag Table", code);
        Assert.Contains("TAG_v_temp", code);
        Assert.Contains("get_tag_value", code);
        Assert.Contains("#include <string.h>", code);
    }

    [Fact]
    public void GenerateFullProject_SetVariableAction_EmitsSetTagValue()
    {
        var project = CreateProject();
        // 给按钮添加 SetVariable 动作
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SetVariable("v_temp", "100"));

        var code = new CodeGenerator().GenerateFullProject(project);

        // 100.0f 而不是 100f：C 里 `100f` 不是合法浮点常量（整数常量不能带 f 后缀），
        // 旧期望钉的正是那个非法产物 —— 见 ActionCallbacksCodeGenTests.GeneratedCode_NeverEmitsFloatLiteralWithoutDecimalPoint
        CSource.AssertCode(code, "set_tag_value(TAG_v_temp, 100.0f);");
        Assert.DoesNotContain("TODO: Write value", code);
    }

    [Fact]
    public void GenerateFullProject_ToggleBitAction_EmitsReadToggle()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.ToggleBit("v_motor"));

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCode(code, "get_tag_value(TAG_v_motor)");
        CSource.AssertCode(code, "set_tag_value(TAG_v_motor, _v > 0.5f ? 0.0f : 1.0f)");
    }

    [Fact]
    public void GenerateFullProject_MomentaryOn_EmitsPressRelease()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.MomentaryOn("v_motor"));

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCode(code, "set_tag_value(TAG_v_motor, 1.0f);"); // press
        CSource.AssertCode(code, "set_tag_value(TAG_v_motor, 0.0f);"); // release
        Assert.Contains("LV_EVENT_PRESSED", code);
        Assert.Contains("LV_EVENT_RELEASED", code);
    }

    [Fact]
    public void GenerateFullProject_DanglingAction_KeepsTodo()
    {
        var project = CreateProject();
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SetVariable("nonexistent", "1"));

        var code = new CodeGenerator().GenerateFullProject(project);

        Assert.Contains("not in tag table", code);
    }

    [Fact]
    public void GenerateFullProject_ReadOnlyWriteAction_StillEmitsButDeviceProtects()
    {
        var project = CreateProject();
        // v_pres 绑定为 ReadOnly, 写动作仍生成调用, C 侧 set_tag_value 拒绝
        var btn = project.Screens[0].Controls.First(c => c.Type == HmiControlType.Button);
        btn.Actions.Add(HmiAction.SetBit("v_pres"));

        var code = new CodeGenerator().GenerateFullProject(project);

        CSource.AssertCode(code, "set_tag_value(TAG_v_pres, 1.0f);");
        // set_tag_value 内部有 read_only 保护
        CSource.AssertCode(code, "if (t->read_only) return 0;");
    }

    // ── 辅助 ──

    private static List<RuntimeTag> CreateTags() => new()
    {
        new RuntimeTag
        {
            VariableId = "v_temp", Address = "40001",
            Direction = BindingDirection.ReadWrite, Scale = 2.0, Offset = 5.0
        },
        new RuntimeTag
        {
            VariableId = "v_pres", Address = "40002",
            Direction = BindingDirection.ReadOnly
        }
    };

    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };
        var conn = new DeviceConnection { Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp" };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_temp", Name = "temperature", Address = "40001",
            DataType = "Float", ConnectionId = "conn1", Scale = 2.0, Offset = 5.0
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_motor", Name = "motor", Address = "40002",
            DataType = "Bool", ConnectionId = "conn1"
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_pres", Name = "pressure", Address = "40003",
            DataType = "Int16", ConnectionId = "conn1"
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "TempLabel", Type = HmiControlType.Label,
            VariableBindingId = "v_temp", BindingMode = "ReadOnly"
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "c2", Name = "StartBtn", Type = HmiControlType.Button,
            VariableBindingId = "v_motor", BindingMode = "ReadWrite"
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "c3", Name = "PresGauge", Type = HmiControlType.Gauge,
            VariableBindingId = "v_pres", BindingMode = "ReadOnly"
        });
        project.Screens.Add(screen);
        return project;
    }

    // ── 阶段 288：值域口径（默认 = 数据类型可表示范围，原始值；Float 无界）──

    private static RuntimeTag Tag(string id, string dtype) => new()
    {
        VariableId = id, Address = "40001", RawAddress = "1", DataType = dtype,
    };

    [Theory]
    [InlineData("Bool", 1, "0.0", "1.0")]
    [InlineData("Int16", 1, "-32768.0", "32767.0")]
    [InlineData("UInt16", 1, "0.0", "65535.0")]
    [InlineData("Int32", 1, "-2147483648.0", "2147483647.0")]
    [InlineData("UInt32", 1, "0.0", "4294967295.0")]
    [InlineData("Float", 0, "0.0", "0.0")]           // 无界 ⇒ has_range=0
    [InlineData("Weird", 0, "0.0", "0.0")]           // 未知类型宁松勿误拒
    public void TagMeta_CarriesTheDataTypeRange(string dtype, int hasRange, string min, string max)
    {
        var code = new RuntimeTagsCodeGen().Generate(new[] { Tag("v_x", dtype) });

        // 结构体尾部三个字段（宿主存指针不拷贝 ⇒ 尾部追加安全）
        CSource.AssertText(code, "uint8_t has_range;");
        CSource.AssertText(code, "float min_value;");
        CSource.AssertText(code, "float max_value;");

        // 行初始值：..., {hasRange}, {min}f, {max}f
        CSource.AssertText(code, $"1.0f, 0.0f, {hasRange}, {min}f, {max}f");
    }

    [Fact]
    public void TagMetaHeader_CarriesTheSameRangeFields()
    {
        // 两个产物（运行时表 / .so 头）都必须带同一组字段，否则宿主采纳时会结构体不一致
        var header = new RuntimeTagsCodeGen().GenerateHeader(new[] { Tag("v_x", "Int16") });

        CSource.AssertText(header, "uint8_t has_range;");
        CSource.AssertText(header, "float min_value;");
        CSource.AssertText(header, "float max_value;");
        CSource.AssertText(header, "1, -32768.0f, 32767.0f");
    }
}
