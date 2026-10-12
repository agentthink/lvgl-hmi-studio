using System.Linq;
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using Hmi.Hardware.Loader;
using HmiEditor.Core.Models;
using Xunit;

namespace Hmi.Hardware.Tests;

public class HardwareTargetTests
{
    [Fact]
    public void Registry_LoadBuiltin_ReturnsTwoTargets()
    {
        var registry = new HardwareTargetRegistry();
        registry.LoadBuiltinTargets(new IHardwareTarget[]
        {
            new AllwinnerT113Target(),
            new RockchipRK3506Target()
        });

        Assert.Equal(2, registry.GetAllTargets().Count());
    }

    [Fact]
    public void Registry_GetTarget_ReturnsCorrectTarget()
    {
        var registry = new HardwareTargetRegistry();
        registry.LoadBuiltinTargets(new IHardwareTarget[]
        {
            new AllwinnerT113Target(),
            new RockchipRK3506Target()
        });

        var t113 = registry.GetTarget("allwinner-t113");
        Assert.NotNull(t113);
        Assert.Equal("Allwinner T113-S3", t113!.Metadata.Name);
    }

    [Fact]
    public void T113_Display_DefaultResolution_1024x600()
    {
        // 硬件规格 02-schematic-spec：7寸 1024x600 RGB666 IPS 屏
        var target = new AllwinnerT113Target();
        Assert.Equal(1024, target.Display.DefaultWidth);
        Assert.Equal(600, target.Display.DefaultHeight);
    }

    [Fact]
    public void RK3506_Display_DefaultResolution_480x272()
    {
        var target = new RockchipRK3506Target();
        Assert.Equal(480, target.Display.DefaultWidth);
        Assert.Equal(272, target.Display.DefaultHeight);
    }

    [Fact]
    public void T113_ValidateProject_HighResolution_ReturnsWarning()
    {
        var target = new AllwinnerT113Target();
        var project = new HmiProject();
        var screen = new HmiScreen { Name = "Big", Width = 1280, Height = 800 };
        project.Screens.Add(screen);

        var result = target.ValidateProject(project);
        Assert.True(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "RESOLUTION_EXCEEDS_DEFAULT");
    }

    [Fact]
    public void T113_GetTemplateParameters_ContainsColorDepth()
    {
        var target = new AllwinnerT113Target();
        var params_ = target.GetTemplateParameters();
        Assert.Equal(32, params_["color_depth"]);
    }

    [Fact]
    public void RK3506_LvglConfig_DefaultColorDepth_16()
    {
        var target = new RockchipRK3506Target();
        Assert.Equal(16, target.LvglConfig.DefaultColorDepth);
    }

    [Fact]
    public void T113_GenerateInitializationCode_ContainsDisplayCreate()
    {
        var target = new AllwinnerT113Target();
        var code = target.GenerateInitializationCode(new HmiProject());
        Assert.Contains("lv_display_create", code);
        Assert.Contains("1024, 600", code);
    }

    [Fact]
    public void Registry_GetTarget_UnknownId_ReturnsNull()
    {
        var registry = new HardwareTargetRegistry();
        Assert.Null(registry.GetTarget("non-existent"));
    }

    [Fact]
    public void T113_RecommendedInterfaces_ContainsSerialAndEthernet()
    {
        var target = new AllwinnerT113Target();
        Assert.Contains(CommunicationInterface.Serial, target.RecommendedInterfaces);
        Assert.Contains(CommunicationInterface.Ethernet, target.RecommendedInterfaces);
    }

    [Fact]
    public void T113_SupportedCommunicationDrivers_ContainsModbusAndMqtt()
    {
        var target = new AllwinnerT113Target();
        Assert.Contains("modbus-rtu", target.SupportedCommunicationDrivers);
        Assert.Contains("modbus-tcp", target.SupportedCommunicationDrivers);
        Assert.Contains("mqtt", target.SupportedCommunicationDrivers);
    }

    [Fact]
    public void RK3506_RecommendedInterfaces_ContainsSerialOnly()
    {
        var target = new RockchipRK3506Target();
        Assert.Contains(CommunicationInterface.Serial, target.RecommendedInterfaces);
        Assert.DoesNotContain(CommunicationInterface.Ethernet, target.RecommendedInterfaces);
    }

    [Fact]
    public void RK3506_SupportedCommunicationDrivers_OnlyModbusRtu()
    {
        var target = new RockchipRK3506Target();
        Assert.Single(target.SupportedCommunicationDrivers);
        Assert.Contains("modbus-rtu", target.SupportedCommunicationDrivers);
    }
}
