using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hmi.Hardware.Contracts;
using Hmi.Hardware.Loader;
using Xunit;

namespace Hmi.Hardware.Tests;

public class PluginLoadingTests
{
    // ── ConfigDrivenTarget（Tier 3）──

    [Fact]
    public void ConfigDrivenTarget_ParseValidJson_CreatesTarget()
    {
        var json = @"
        {
            ""id"": ""test-mcu"",
            ""name"": ""Test MCU"",
            ""vendor"": ""TestVendor"",
            ""architecture"": ""ARM Cortex-M4"",
            ""version"": ""1.0.0"",
            ""display"": {
                ""defaultWidth"": 480,
                ""defaultHeight"": 272,
                ""colorDepths"": [""RGB565""],
                ""interface"": ""SPI"",
                ""maxRefreshRate"": 60
            },
            ""resources"": {
                ""flashSizeBytes"": 1048576,
                ""ramSizeBytes"": 262144,
                ""recommendedLvglHeapBytes"": 65536,
                ""maxTextureSize"": 256
            },
            ""lvgl"": {
                ""defaultColorDepth"": 16,
                ""defaultMemorySize"": 32,
                ""useDoubleBuffering"": false
            },
            ""toolchain"": {
                ""crossCompilerPrefix"": ""arm-none-eabi-"",
                ""compileFlags"": [""-mcpu=cortex-m4""]
            },
            ""communicationInterfaces"": [""Serial"", ""CanBus""],
            ""communicationDrivers"": [""modbus-rtu""]
        }";

        var config = JsonSerializer.Deserialize<TargetConfigJson>(json);
        Assert.NotNull(config);
        Assert.Equal("test-mcu", config!.Id);
        Assert.Equal("Test MCU", config.Name);

        var target = new ConfigDrivenTarget(config);
        Assert.Equal("test-mcu", target.Metadata.Id);
        Assert.Equal(480, target.Display.DefaultWidth);
        Assert.Equal(272, target.Display.DefaultHeight);
        Assert.Equal(1_048_576, target.Resources.FlashSizeBytes);
        Assert.Equal(16, target.LvglConfig.DefaultColorDepth);
        Assert.Contains("modbus-rtu", target.SupportedCommunicationDrivers);
    }

    [Fact]
    public void ConfigDrivenTarget_ValidateProject_ResolutionWarning()
    {
        var config = new TargetConfigJson
        {
            Id = "test",
            Name = "Test",
            Display = new DisplayConfigJson { DefaultWidth = 320, DefaultHeight = 240 }
        };
        var target = new ConfigDrivenTarget(config);
        var project = new HmiEditor.Core.Models.HmiProject();
        project.Screens.Add(new HmiEditor.Core.Models.HmiScreen
        {
            Name = "BigScreen",
            Width = 800,
            Height = 480
        });

        var result = target.ValidateProject(project);
        Assert.True(result.IsValid); // 仅警告，不阻止
        Assert.Contains(result.Issues, i => i.Code == "RESOLUTION_EXCEEDS_DEFAULT");
    }

    [Fact]
    public void ConfigDrivenTarget_GetTemplateParameters_ContainsAllKeys()
    {
        var target = new ConfigDrivenTarget(new TargetConfigJson
        {
            Id = "stm32f4",
            Name = "STM32F407",
            Lvgl = new LvglConfigJson { DefaultColorDepth = 16 },
            Resources = new ResourceConstraintsJson { RecommendedLvglHeapBytes = 65536 },
            Toolchain = new ToolchainConfigJson { CrossCompilerPrefix = "arm-none-eabi-" }
        });

        var parameters = target.GetTemplateParameters();
        Assert.Equal("stm32f4", parameters["target_id"]);
        Assert.Equal(16, parameters["color_depth"]);
        Assert.Equal(65536L, parameters["lvgl_heap_size"]);
        Assert.Equal("arm-none-eabi-", parameters["cross_compiler_prefix"]);
    }

    [Fact]
    public void ConfigDrivenTarget_GenerateInitializationCode_ReturnsPlaceholder()
    {
        var target = new ConfigDrivenTarget(new TargetConfigJson
        {
            Id = "test",
            Name = "TestDevice",
            Display = new DisplayConfigJson { DefaultWidth = 800, DefaultHeight = 480 }
        });

        var code = target.GenerateInitializationCode(new HmiEditor.Core.Models.HmiProject());
        Assert.Contains("lv_display_create(800, 480)", code);
    }

    [Fact]
    public void ConfigDrivenTarget_CommunicationInterfaces_ParsedCorrectly()
    {
        var target = new ConfigDrivenTarget(new TargetConfigJson
        {
            Id = "test",
            Name = "Test",
            CommunicationInterfaces = new() { "Serial", "Ethernet", "WiFi" }
        });

        Assert.Contains(CommunicationInterface.Serial, target.RecommendedInterfaces);
        Assert.Contains(CommunicationInterface.Ethernet, target.RecommendedInterfaces);
        Assert.Contains(CommunicationInterface.WiFi, target.RecommendedInterfaces);
        Assert.Equal(3, target.RecommendedInterfaces.Count);
    }

    // ── 注册中心 ──

    [Fact]
    public void Registry_LoadConfigTargets_LoadsFromDirectory()
    {
        // 创建临时目录和 JSON 配置文件
        var tempDir = TestTempDir.NewDir("hmi-targets-test");

        try
        {
            var jsonPath = Path.Combine(tempDir, "test-mcu.target.json");
            File.WriteAllText(jsonPath, @"{
                ""id"": ""test-mcu"",
                ""name"": ""Test MCU"",
                ""vendor"": ""Test"",
                ""architecture"": ""ARM"",
                ""display"": { ""defaultWidth"": 320, ""defaultHeight"": 240 },
                ""resources"": { ""flashSizeBytes"": 1048576, ""ramSizeBytes"": 262144 },
                ""lvgl"": { ""defaultColorDepth"": 16 },
                ""toolchain"": { ""crossCompilerPrefix"": ""arm-none-eabi-"" },
                ""communicationInterfaces"": [""Serial""],
                ""communicationDrivers"": [""modbus-rtu""]
            }");

            var registry = new HardwareTargetRegistry();
            var count = registry.LoadConfigTargets(tempDir);

            Assert.Equal(1, count);
            var target = registry.GetTarget("test-mcu");
            Assert.NotNull(target);
            Assert.Equal("Test MCU", target!.Metadata.Name);
        }
        finally
        {
            TestTempDir.DeleteDir(tempDir);
        }
    }

    [Fact]
    public void Registry_LoadConfigTargets_NonexistentDirectory_ReturnsZero()
    {
        var registry = new HardwareTargetRegistry();
        var count = registry.LoadConfigTargets("Z:\\nonexistent\\path");
        Assert.Equal(0, count);
    }

    [Fact]
    public void Registry_LoadConfigTargets_InvalidJson_SkipsFile()
    {
        var tempDir = TestTempDir.NewDir("hmi-targets-invalid");

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "bad.target.json"), "not valid json");

            var registry = new HardwareTargetRegistry();
            var count = registry.LoadConfigTargets(tempDir);
            Assert.Equal(0, count);
        }
        finally
        {
            TestTempDir.DeleteDir(tempDir);
        }
    }

    // ── 卸载 ──

    [Fact]
    public void Registry_UnloadPluginContexts_NoContexts_ReturnsZero()
    {
        var registry = new HardwareTargetRegistry();
        var count = registry.UnloadPluginContexts();
        Assert.Equal(0, count);
    }
}
