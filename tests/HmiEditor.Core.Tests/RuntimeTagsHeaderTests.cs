using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>工程标签契约头（libhmi_ui.so 编译用）生成测试。</summary>
public class RuntimeTagsHeaderTests
{
    private static IReadOnlyList<RuntimeTag> BuildTags(params string[] variableIds)
    {
        var engine = new RuntimeBindingEngine(new HmiProject());
        // 直接构造 RuntimeTag 列表（绑定引擎结果等价）
        return variableIds.Select((v, i) => new RuntimeTag
        {
            VariableId = v,
            Address = $"4000{i + 1}",
            Direction = BindingDirection.ReadWrite,
            Scale = 1.0,
            Offset = 0.0
        }).ToList();
    }

    [Fact]
    public void GenerateHeader_WithTags_ContainsEnumExternsAndGuard()
    {
        var tags = BuildTags("PV", "SV", "Run-State");

        var header = new RuntimeTagsCodeGen().GenerateHeader(tags);

        Assert.Contains("#ifndef HMI_GENERATED_TAGS_H", header);
        Assert.Contains("#define HMI_GENERATED_TAGS_H", header);
        Assert.Contains("TAG_PV = 0", header);
        Assert.Contains("TAG_SV = 1", header);
        Assert.Contains("TAG_Run_State = 2", header);
        Assert.Contains("TAG_COUNT", header);
        // ABI V3：标签表 .so 自包含（内嵌 static const），不再 extern 宿主表
        Assert.Contains("static const tag_meta_t HMI_UI_TAG_META", header);
        Assert.Contains("static const tag_binding_t HMI_UI_TAG_BINDINGS", header);
        Assert.DoesNotContain("extern const tag_meta_t    TAG_TABLE", header);
        Assert.DoesNotContain("extern const tag_binding_t TAG_BINDINGS", header);
        Assert.Contains("tag_meta_t", header);
        Assert.Contains("tag_binding_t", header);
        Assert.Contains("var_raw_read", header);
        Assert.Contains("get_tag_value", header);
    }

    [Fact]
    public void GenerateHeader_NoTags_EmitsEmptyContract()
    {
        var header = new RuntimeTagsCodeGen().GenerateHeader(Array.Empty<RuntimeTag>());

        Assert.Contains("TAG_COUNT = 0", header);
        Assert.Contains("HMI_UI_TAG_META[TAG_COUNT > 0 ? TAG_COUNT : 1]", header);
    }

    [Fact]
    public void GenerateHeader_EmbedsSelfContainedTables()
    {
        // ABI V3：.so 内嵌自己的标签元数据/绑定 —— 换工程热更新不再依赖宿主符号
        var tags = BuildTags("PV");

        var header = new RuntimeTagsCodeGen().GenerateHeader(tags);

        Assert.DoesNotContain("static const tag_meta_t TAG_TABLE", header);
        Assert.DoesNotContain("static const tag_binding_t TAG_BINDINGS", header);
        Assert.DoesNotContain("extern const tag_meta_t", header);
        Assert.DoesNotContain("extern const tag_binding_t", header);
        Assert.Contains("HMI_UI_TAG_META[TAG_COUNT > 0 ? TAG_COUNT : 1]", header);
        Assert.Contains("\"PV\"", header);
        Assert.Contains("&HMI_UI_TAG_META[idx]", header);
    }

    [Fact]
    public void GenerateHeader_SubDeviceTag_UsesRawAddressAndSlaveUnit()
    {
        // 回归：从站路由必须走 binding.unit，addr 必须为纯地址（"1:40001" 会被固件
        // hmi_tag_parse_addr 解析成 fn=01(线圈)/off=40001 —— 站号被当功能码、完整地址
        // 被当偏移，轮询组请求错误导致 Modbus 无读写数据）。
        var tags = new[]
        {
            new RuntimeTag
            {
                VariableId = "PV",
                Address = "1:40001",      // 显示/诊断用（带站号前缀）
                RawAddress = "40001",     // 固件轮询用（纯地址）
                SlaveAddress = 3,         // 从站号 → binding.unit
                DriverId = "modbus-tcp",
                DataType = "U16",
                Direction = BindingDirection.ReadWrite,
                Scale = 1.0,
                Offset = 0.0
            }
        };

        var header = new RuntimeTagsCodeGen().GenerateHeader(tags);

        Assert.Contains("\"modbus-tcp\", \"40001\", 3, \"U16\", 100", header);
        Assert.DoesNotContain("\"1:40001\", 1,", header);
    }

    [Fact]
    public void GenerateHeader_NoSubDeviceTag_FallsBackToUnit1AndRawAddress()
    {
        var tags = new[]
        {
            new RuntimeTag
            {
                VariableId = "SV",
                Address = "40002",
                SlaveAddress = 0,          // 无子设备
                DriverId = "modbus-rtu",
                DataType = "Int16",
                Direction = BindingDirection.ReadWrite,
                Scale = 1.0,
                Offset = 0.0
            }
        };

        var header = new RuntimeTagsCodeGen().GenerateHeader(tags);

        Assert.Contains("\"modbus-rtu\", \"40002\", 1, \"U16\", 100", header);
    }
}
