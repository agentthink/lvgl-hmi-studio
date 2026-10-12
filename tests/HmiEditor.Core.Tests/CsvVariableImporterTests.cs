using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class CsvVariableImporterTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsErrors()
    {
        var result = CsvVariableImporter.Parse("");
        Assert.True(result.HasErrors);
        Assert.Empty(result.Variables);
    }

    [Fact]
    public void Parse_HeaderOnly_ReturnsEmpty()
    {
        var csv = "Name,DataType,Address\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Empty(result.Variables);
        Assert.Equal(0, result.SuccessRows);
    }

    [Fact]
    public void Parse_StandardFormat_ParsesCorrectly()
    {
        var csv = "Name,DataType,Address,Unit\nMotorTemp,Int16,MW100,°C\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal("MotorTemp", result.Variables[0].Name);
        Assert.Equal("Int16", result.Variables[0].DataType);
        Assert.Equal("MW100", result.Variables[0].Address);
        Assert.Equal("°C", result.Variables[0].Unit);
    }

    [Fact]
    public void Parse_ChineseHeaders_ParsesCorrectly()
    {
        var csv = "变量名,数据类型,地址\n温度,Real,DB1.DBD0\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal("温度", result.Variables[0].Name);
        Assert.Equal("Real", result.Variables[0].DataType);
        Assert.Equal("DB1.DBD0", result.Variables[0].Address);
    }

    [Fact]
    public void Parse_TiaPortalFormat_ParsesCorrectly()
    {
        var csv = "Name,DataType,Address,Comment\n\"Motor_Speed\",\"Int\",\"MW200\",\"Motor speed value\"\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal("Motor_Speed", result.Variables[0].Name);
        Assert.Equal("Int", result.Variables[0].DataType);
        Assert.Equal("MW200", result.Variables[0].Address);
        Assert.Equal("Motor speed value", result.Variables[0].Unit); // Comment mapped to Unit
    }

    [Fact]
    public void Parse_QuotedFields_HandlesCommasInside()
    {
        var csv = "Name,DataType,Address,Unit\n\"Temp,Sensor\",Real,\"DB1,DBD4\",°C\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal("Temp,Sensor", result.Variables[0].Name);
        Assert.Equal("Real", result.Variables[0].DataType);
        Assert.Equal("DB1,DBD4", result.Variables[0].Address);
    }

    [Fact]
    public void Parse_EscapedQuotes_HandlesDoubleQuotes()
    {
        var csv = "Name,DataType,Address\n\"He said \"\"hello\"\"\",Int,MW1\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Contains("hello", result.Variables[0].Name);
    }

    [Fact]
    public void Parse_MultipleRows_ParsesAll()
    {
        var csv = "Name,DataType,Address\nV1,Int,MW10\nV2,Bool,I0.0\nV3,Real,MD20\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Equal(3, result.Variables.Count);
        Assert.Equal(3, result.SuccessRows);
    }

    [Fact]
    public void Parse_MissingName_SkipsRow()
    {
        var csv = "Name,DataType,Address\n,Int,MW10\nV2,Bool,I0.0\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal("V2", result.Variables[0].Name);
        // 阶段 88（登记 #61f）：错误不再是裸中文串，而是「码 + 行号 + 中文回落」。
        // 断码与行号（表头第 1 行 ⇒ 坏行第 2 行）才是这条用例的本意 ——
        // 只断"消息里有某几个字"会被措辞改动骗过，也测不到"行号是结构化字段"这件事。
        Assert.Equal(CsvImportIssueCodes.ROW_EMPTY_NAME, result.Errors[0].Code);
        Assert.Equal(2, result.Errors[0].Line);
        Assert.Contains("Name 为空", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ScaleAndOffset_ParsesCorrectly()
    {
        var csv = "Name,DataType,Address,Scale,Offset,PollRateMs\nMotor,Int,MW100,2.5,1.0,1000\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Single(result.Variables);
        Assert.Equal(2.5, result.Variables[0].Scale);
        Assert.Equal(1.0, result.Variables[0].Offset);
        Assert.Equal(1000, result.Variables[0].PollRateMs);
    }

    [Fact]
    public void Parse_WindowsLineEndings_HandlesCorrectly()
    {
        var csv = "Name,DataType,Address\r\nV1,Int,MW10\r\nV2,Bool,I0.0\r\n";
        var result = CsvVariableImporter.Parse(csv);
        Assert.Equal(2, result.Variables.Count);
    }

    [Fact]
    public void Parse_WithConnectionAndSlave_AssignsOwnership()
    {
        var csv = "Name,DataType,Address\nV1,Int,40001\nV2,Real,40002\n";
        var result = CsvVariableImporter.Parse(csv, "conn-1", "slave-2");
        Assert.Equal(2, result.Variables.Count);
        Assert.All(result.Variables, v =>
        {
            Assert.Equal("conn-1", v.ConnectionId);
            Assert.Equal("slave-2", v.SlaveId);
        });
    }

    [Fact]
    public void Export_RoundTrips_BackToSameVariables()
    {
        var vars = new List<HmiEditor.Core.Models.Variable>
        {
            new() { Name = "Temp1", DataType = "Int16", Address = "40001", Unit = "°C", Scale = 1.5, Offset = 2.0, PollRateMs = 500 },
            new() { Name = "Press", DataType = "Float32", Address = "40002" },
            new() { Name = "Name,With,Comma", DataType = "Bool", Address = "00001" },
        };

        var csv = CsvVariableImporter.Export(vars);
        var result = CsvVariableImporter.Parse(csv);

        Assert.Equal(3, result.Variables.Count);
        Assert.Equal("Temp1", result.Variables[0].Name);
        Assert.Equal("40001", result.Variables[0].Address);
        Assert.Equal("Int16", result.Variables[0].DataType);
        Assert.Equal("°C", result.Variables[0].Unit);
        Assert.Equal(1.5, result.Variables[0].Scale);
        Assert.Equal(2.0, result.Variables[0].Offset);
        Assert.Equal(500, result.Variables[0].PollRateMs);
        // 含逗号的名称应被转义并正确回读
        Assert.Equal("Name,With,Comma", result.Variables[2].Name);
    }
}
