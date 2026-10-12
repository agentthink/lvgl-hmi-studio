using HmiEditor.AI.Services;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// VariableMatchService 测试 —— 变量绑定推荐引擎。
/// </summary>
public class VariableMatchServiceTests
{
    private static VariableManager CreateManager(params (string Name, string DataType)[] variables)
    {
        var vm = new VariableManager();
        foreach (var (name, dataType) in variables)
        {
            vm.Variables.Add(new Variable { Name = name, DataType = dataType });
        }
        return vm;
    }

    [Fact]
    public void Recommend_TypeCompatibleAndNameMatch_TopResult()
    {
        var vm = CreateManager(
            ("temperature_1", "Float"),
            ("motor_speed", "Int16"),
            ("alarm_text", "String"));

        var control = new HmiControl { Type = HmiControlType.Gauge, Name = "温度显示" };
        var results = VariableMatchService.Recommend(control, vm);

        Assert.NotEmpty(results);
        Assert.Equal("temperature_1", results[0].Variable.Name);
        Assert.True(results[0].Score >= 60);
    }

    [Fact]
    public void Recommend_BoolControl_PrefersBoolVariables()
    {
        var vm = CreateManager(
            ("motor_state", "Bool"),
            ("temperature_1", "Float"));

        var control = new HmiControl { Type = HmiControlType.Switch, Name = "电机开关" };
        var results = VariableMatchService.Recommend(control, vm);

        Assert.NotEmpty(results);
        Assert.Equal("motor_state", results[0].Variable.Name);
    }

    [Fact]
    public void Recommend_NoNameMatch_ReturnsEmpty()
    {
        var vm = CreateManager(("pressure_1", "Float"));

        var control = new HmiControl { Type = HmiControlType.Label, Name = "日期显示" };
        var results = VariableMatchService.Recommend(control, vm);

        Assert.Empty(results);
    }

    [Fact]
    public void Recommend_NoVariables_ReturnsEmpty()
    {
        var control = new HmiControl { Type = HmiControlType.Button, Name = "启动" };
        var results = VariableMatchService.Recommend(control, new VariableManager());
        Assert.Empty(results);
    }

    [Fact]
    public void Recommend_MaxResults_Respected()
    {
        var vm = new VariableManager();
        for (var i = 0; i < 10; i++)
        {
            vm.Variables.Add(new Variable { Name = $"temperature_{i}", DataType = "Float" });
        }

        var control = new HmiControl { Type = HmiControlType.Label, Name = "温度显示" };
        var results = VariableMatchService.Recommend(control, vm, maxResults: 3);
        Assert.Equal(3, results.Count);
    }

    [Theory]
    [InlineData(HmiControlType.Gauge, "Float", true)]
    [InlineData(HmiControlType.Switch, "Bool", true)]
    [InlineData(HmiControlType.Switch, "Float", false)]
    [InlineData(HmiControlType.Table, "UDT:motor", true)]
    [InlineData(HmiControlType.Gauge, "UDT:motor", false)]
    [InlineData(HmiControlType.Label, "", true)]
    public void IsDataTypeCompatible_VariousCases(HmiControlType controlType, string dataType, bool expected)
    {
        Assert.Equal(expected, VariableMatchService.IsDataTypeCompatible(controlType, dataType));
    }

    [Fact]
    public void Recommend_EnglishSemanticAlias_Matches()
    {
        var vm = CreateManager(("temp_main", "Float"));

        var control = new HmiControl { Type = HmiControlType.Bar, Name = "温度显示" };
        var results = VariableMatchService.Recommend(control, vm);

        Assert.NotEmpty(results);
        Assert.Equal("temp_main", results[0].Variable.Name);
    }
}
