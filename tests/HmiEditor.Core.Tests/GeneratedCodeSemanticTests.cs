using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 生成代码的**结构化/语义断言** —— 替代「生成串包含某子串」式断言。
///
/// 这类断言直接问「生成结果是否合法」而不是「是否出现某个字符串」：
///   - 所有 <c>lv_*</c> 调用是否都在已知 API 白名单内（拼写/版本漂移会被抓住）；
///   - 是否存在悬空引用等 Error 级问题；
/// 字符串层面怎么重构都不影响这些断言，行为回归却会被抓住。
/// </summary>
public class GeneratedCodeSemanticTests
{
    /// <summary>覆盖面较广的工程：多种控件 + 绑定 + 单位 + 表格单元格 + 报警 + 趋势。</summary>
    private static HmiProject MakeBroadProject()
    {
        var project = new HmiProject { Name = "Semantic", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "主屏", Width = 1024, Height = 600 };
        project.Screens.Add(scr);

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "%",
        });

        void Bind(HmiControl c)
        {
            c.VariableBindings.Add(new VariableBindingData
            {
                VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value",
            });
            scr.Controls.Add(c);
        }

        // 覆盖各控件族的参数生成分支
        Bind(new HmiControl { Id = "lbl", Type = HmiControlType.Label, X = 0, Y = 0, Width = 120, Height = 30, Text = "温度" });
        Bind(new HmiControl { Id = "bar", Type = HmiControlType.Bar, X = 0, Y = 40, Width = 200, Height = 20, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "sld", Type = HmiControlType.Slider, X = 0, Y = 70, Width = 200, Height = 20, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "arc", Type = HmiControlType.Arc, X = 220, Y = 0, Width = 60, Height = 60, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "led", Type = HmiControlType.Led, X = 300, Y = 0, Width = 20, Height = 20 });
        Bind(new HmiControl { Id = "sw", Type = HmiControlType.Switch, X = 330, Y = 0, Width = 60, Height = 30 });
        Bind(new HmiControl { Id = "cb", Type = HmiControlType.CheckBox, X = 400, Y = 0, Width = 80, Height = 30 });
        scr.Controls.Add(new HmiControl { Id = "dd", Type = HmiControlType.Dropdown, X = 500, Y = 0, Width = 120, Height = 30, Options = "A\nB" });
        scr.Controls.Add(new HmiControl { Id = "ta", Type = HmiControlType.TextArea, X = 500, Y = 40, Width = 160, Height = 40, PlaceholderText = "输入", KeyboardMode = "Number" });
        scr.Controls.Add(new HmiControl { Id = "gauge", Type = HmiControlType.Gauge, X = 640, Y = 0, Width = 120, Height = 120, MinValue = 0, MaxValue = 100, Value = 42, MajorTickCount = 6 });
        scr.Controls.Add(new HmiControl { Id = "tv", Type = HmiControlType.TabView, X = 0, Y = 120, Width = 300, Height = 160 });
        scr.Controls.Add(new HmiControl { Id = "line", Type = HmiControlType.Line, X = 320, Y = 120, Width = 120, Height = 60 });
        scr.Controls.Add(new HmiControl { Id = "spinner", Type = HmiControlType.Spinner, X = 460, Y = 120, Width = 40, Height = 40 });
        scr.Controls.Add(new HmiControl
        {
            Id = "tbl", Type = HmiControlType.Table, X = 0, Y = 300, Width = 300, Height = 120,
            RowCount = 2, ColumnCount = 2,
            Cells = new List<TableCellData>
            {
                new TableCellData { Row = 0, Col = 0, VariableId = "v1", DecimalPlaces = 1 },
                new TableCellData { Row = 0, Col = 1, Text = "静态" },
            },
        });
        // canvas 族（SVG / 过程控件 / 椭圆）
        scr.Controls.Add(new HmiControl
        {
            Id = "svg", Type = HmiControlType.Svg, X = 340, Y = 300, Width = 100, Height = 80,
            SvgViewBoxWidth = 100, SvgViewBoxHeight = 100,
            SvgShapes = new List<SvgShape>
            {
                new SvgShape { Kind = SvgShapeKind.Path, PathData = "M0 0 L50 0 L25 50 Z", Fill = "#123456" },
            },
        });
        scr.Controls.Add(new HmiControl { Id = "tank", Type = HmiControlType.Tank, X = 460, Y = 300, Width = 80, Height = 120, Value = 50 });
        scr.Controls.Add(new HmiControl { Id = "pipe", Type = HmiControlType.Pipe, X = 560, Y = 320, Width = 120, Height = 30, PipeFlowSpeed = 500 });
        scr.Controls.Add(new HmiControl { Id = "valve", Type = HmiControlType.Valve, X = 700, Y = 300, Width = 60, Height = 60, ValveType = "Globe" });
        scr.Controls.Add(new HmiControl { Id = "ell", Type = HmiControlType.Ellipse, X = 780, Y = 300, Width = 100, Height = 60, BgColor = "#334155" });

        project.AlarmGroups.Add(new AlarmGroup
        {
            Id = "g1", Name = "报警",
            Alarms =
            {
                new AlarmConfig
                {
                    Id = "a1", Name = "高温", IsEnabled = true,
                    Condition = new AlarmCondition { VariableId = "v1", Type = AlarmConditionType.HighLimit, Threshold = 80 },
                },
            },
        });
        project.TrendConfigs.Add(new TrendConfig
        {
            Id = "t1", Name = "趋势", IsEnabled = true, MaxPoints = 100,
            Pens = { new TrendPen { Label = "温度", Color = "#ef4444", VariableId = "v1", IsEnabled = true } },
        });
        return project;
    }

    [Theory]
    [InlineData(false)]   // PC 模拟器路径
    [InlineData(true)]    // 设备 .so 路径
    public void GeneratedCode_HasNoUnknownLvglApis_AndNoErrors(bool deviceMode)
    {
        var project = MakeBroadProject();
        var generator = new CodeGenerator();
        var code = deviceMode
            ? generator.GenerateSharedLibraryProject(project)
            : generator.GenerateSimulatorModule(project, null);

        var findings = GeneratedCodeValidator.Validate(project, code);

        var unknownApis = findings
            .Where(f => f.Category == FindingCategory.LvglApi)
            .Select(f => f.Message)
            .Distinct()
            .ToList();
        Assert.True(unknownApis.Count == 0,
            "生成代码出现未知 LVGL API（拼写/版本漂移）：" + string.Join(" | ", unknownApis));

        var errors = findings
            .Where(f => f.Severity == FindingSeverity.Error)
            .Select(f => $"{f.Category}: {f.Message}")
            .Distinct()
            .ToList();
        Assert.True(errors.Count == 0,
            "生成代码存在 Error 级问题：" + string.Join(" | ", errors));
    }

    [Fact]
    public void GeneratedCode_ContainsNoLvgl8OnlyApis()
    {
        // 语义断言：9.x 已移除的 API 不得出现在任一生成路径（此前用字符串包含来查）
        var removed = new[]
        {
            "lv_canvas_set_px_opa", "lv_draw_polygon", "lv_obj_set_hidden",
            "lv_obj_set_click_focusable", "lv_table_set_col_cnt", "lv_table_set_row_cnt",
        };

        var project = MakeBroadProject();
        var generator = new CodeGenerator();
        var sim = generator.GenerateSimulatorModule(project, null);
        var device = generator.GenerateSharedLibraryProject(project);

        foreach (var api in removed)
        {
            Assert.DoesNotContain(api, sim);
            Assert.DoesNotContain(api, device);
        }
    }
}
