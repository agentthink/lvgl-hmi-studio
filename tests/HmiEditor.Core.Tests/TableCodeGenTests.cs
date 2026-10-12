using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 表格控件增强（行高/列宽/单元格绑定）代码生成与布局解析单元测试。
/// </summary>
public class TableCodeGenTests
{
    private static HmiControl MakeTable(Action<HmiControl>? config = null)
    {
        var t = new HmiControl
        {
            Id = "t1", Name = "Table1", Type = HmiControlType.Table,
            X = 0, Y = 0, Width = 800, Height = 200,
            RowCount = 3, ColumnCount = 3
        };
        config?.Invoke(t);
        return t;
    }

    private static string GenScreen(HmiControl control)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(control);
        return new CodeGenerator().GenerateScreenCode(screen);
    }

    private static string GenSimulator(HmiControl control, Variable? variable = null)
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        if (variable != null) project.VariableManager.Variables.Add(variable);
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(control);
        project.Screens.Add(screen);
        return new CodeGenerator().GenerateSimulatorModule(project);
    }

    [Fact]
    public void ColumnWidths_Empty_EvenlyFillControlWidth()
    {
        var code = GenScreen(MakeTable());   // 800 / 3 ≈ 266
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 0, 266);");
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 1, 266);");
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 2, 266);");
    }

    [Fact]
    public void ColumnWidths_SpecifiedAndAuto_MapCorrectly()
    {
        var code = GenScreen(MakeTable(t => t.ColumnWidths = "100,auto,200"));
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 0, 100);");
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 1, LV_COORD_MAX);");
        CSource.AssertCode(code, "lv_table_set_column_width(obj_t1, 2, 200);");
    }

    [Fact]
    public void RowHeight_Positive_FixesRowHeightViaPartItems()
    {
        var code = GenScreen(MakeTable(t => t.RowHeight = 40));
        CSource.AssertCode(code, "lv_obj_set_style_min_height(obj_t1, 40, LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_max_height(obj_t1, 40, LV_PART_ITEMS);");
    }

    [Fact]
    public void RowHeight_Zero_NoFixedHeight()
    {
        var code = GenScreen(MakeTable(t => t.RowHeight = 0));
        // 不生成固定行高（min/max_height）；LV_PART_ITEMS 仍用于网格线边框
        CSource.AssertNoCode(code, "lv_obj_set_style_min_height(obj_t1,");
        CSource.AssertNoCode(code, "lv_obj_set_style_max_height(obj_t1,");
    }

    [Fact]
    public void Cells_StaticText_FillsCellValue()
    {
        var code = GenScreen(MakeTable(t =>
            t.Cells = new() { new TableCellData { Row = 1, Col = 0, Text = "启动" } }));
        CSource.AssertCode(code, "lv_table_set_cell_value(obj_t1, 1, 0, \"启动\");");
    }

    [Fact]
    public void Cells_BoundVariable_RefreshesInSimRefresh()
    {
        var code = GenSimulator(
            MakeTable(t => t.Cells = new()
            {
                new TableCellData { Row = 1, Col = 1, VariableId = "v_temp", Suffix = "℃", DecimalPlaces = 1 }
            }),
            new Variable { Id = "v_temp", Name = "温度", Unit = "℃", DataType = "Float" });

        Assert.Contains("void sim_refresh_bindings", code);
        CSource.AssertCode(code, "lv_table_set_cell_value_fmt(obj_t1, 1, 1, \"%.1f℃\", (double)v);");
    }

    [Fact]
    public void Cells_BoundVariable_NotEmittedAsStaticValue()
    {
        var code = GenScreen(MakeTable(t => t.Cells = new()
            { new TableCellData { Row = 2, Col = 2, VariableId = "v_pres" } }));
        // 绑定单元格不应生成静态 set_cell_value（在 create_screen 阶段）
        CSource.AssertNoCode(code, "lv_table_set_cell_value(obj_t1, 2, 2,");
    }

    [Fact]
    public void Table_GridLines_DefaultColor_OnCellPart()
    {
        // 网格线：LVGL 无主题时表格默认无单元格边框，需显式设置 LV_PART_ITEMS 边框
        var code = GenScreen(MakeTable());
        CSource.AssertCode(code, "lv_obj_set_style_border_width(obj_t1, 1, LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_border_color(obj_t1, lv_color_hex(0x334155), LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_border_side(obj_t1, LV_BORDER_SIDE_FULL, LV_PART_ITEMS);");
    }

    [Fact]
    public void Table_GridLines_UseUserBorderColor()
    {
        var code = GenScreen(MakeTable(t => t.BorderColor = "#ff0000"));
        CSource.AssertCode(code, "lv_obj_set_style_border_color(obj_t1, lv_color_hex(0xff0000), LV_PART_ITEMS);");
    }

    [Fact]
    public void Table_CellBackground_UsesBgColor()
    {
        // 回归防护：LVGL 无主题时 LV_PART_ITEMS（单元格）默认渲染白色背景，盖住主体背景色，
        // 必须显式设置单元格背景 = BgColor，保证模拟器与设计器背景一致。
        var code = GenScreen(MakeTable(t => t.BgColor = "#64748b"));
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_t1, lv_color_hex(0x64748b), LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_t1, LV_OPA_COVER, LV_PART_ITEMS);");
    }

    [Fact]
    public void Table_NoBgColor_UsesDesignerFallbackColor()
    {
        // 无 BgColor 时单元格背景用 #1e293b（= 设计器兜底色 30,41,59），避免模拟器显示 LVGL 默认白色
        var code = GenScreen(MakeTable());
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_t1, lv_color_hex(0x1e293b), LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_t1, LV_OPA_COVER, LV_PART_ITEMS);");
    }

    [Fact]
    public void ParseColumnWidths_Empty_AllFill()
    {
        var w = TableLayoutHelper.ParseColumnWidths(null, 3);
        Assert.Equal(3, w.Count);
        Assert.All(w, v => Assert.Equal(TableLayoutHelper.Fill, v));
    }

    [Fact]
    public void ParseColumnWidths_AutoAndNumber_Parsed()
    {
        var w = TableLayoutHelper.ParseColumnWidths("100,auto,", 3);
        Assert.Equal(100, w[0]);
        Assert.Equal(TableLayoutHelper.Auto, w[1]);
        Assert.Equal(TableLayoutHelper.Fill, w[2]);
    }
}
