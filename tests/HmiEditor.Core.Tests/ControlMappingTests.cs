using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class ControlMappingTests
{
    [Fact]
    public void Mapping_AllControlTypes_HaveCreateFunction()
    {
        var enumValues = Enum.GetValues<HmiControlType>();
        foreach (var type in enumValues)
        {
            var func = ControlMappingRegistry.GetCreateFunction(type);
            Assert.False(string.IsNullOrEmpty(func));
            Assert.StartsWith("lv_", func);
        }
    }

    [Fact]
    public void Mapping_GetCreateFunction_ReturnsCorrectFunction()
    {
        Assert.Equal("lv_button_create", ControlMappingRegistry.GetCreateFunction(HmiControlType.Button));
        Assert.Equal("lv_label_create", ControlMappingRegistry.GetCreateFunction(HmiControlType.Label));
        Assert.Equal("lv_slider_create", ControlMappingRegistry.GetCreateFunction(HmiControlType.Slider));
        Assert.Equal("lv_switch_create", ControlMappingRegistry.GetCreateFunction(HmiControlType.Switch));
        Assert.Equal("lv_checkbox_create", ControlMappingRegistry.GetCreateFunction(HmiControlType.CheckBox));
    }

    [Fact]
    public void Mapping_CommonPropertyMappings_ContainsCoreProperties()
    {
        var names = ControlMappingRegistry.CommonPropertyMappings
            .Select(m => m.AvaloniaPropertyName).ToList();

        Assert.Contains("X", names);
        Assert.Contains("Y", names);
        Assert.Contains("Width", names);
        Assert.Contains("Height", names);
        Assert.Contains("BgColor", names);
    }

    [Fact]
    public void Mapping_GetMappingsForType_ReturnsMappings()
    {
        var mappings = ControlMappingRegistry.GetMappingsForType(HmiControlType.Button);
        Assert.NotEmpty(mappings);
    }

    [Fact]
    public void Mapping_AllLvglCreateFunctions_AreValid()
    {
        foreach (var kv in ControlMappingRegistry.LvglCreateFunction)
        {
            var func = kv.Value;
            Assert.Matches(@"^lv_\w+_create$", func);
        }
    }
}
