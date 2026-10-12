using HmiEditor.CodeGen;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>LVGL 兼容性检查器测试 —— 验证废弃 API 自动替换与提示逻辑。</summary>
public class LvglApiCompatCheckerTests
{
    [Fact]
    public void Process_ReplacesLegacyApis_ForV9Project()
    {
        var code = "    lv_table_set_col_cnt(t, 3);\n"
                 + "    lv_slider_set_left_value(s, 5, LV_ANIM_OFF);\n";

        var result = LvglApiCompatChecker.Process(code, "9.5");

        CSource.AssertCall(result, "lv_table_set_column_count(t, 3)");
        CSource.AssertCall(result, "lv_slider_set_start_value(s, 5, LV_ANIM_OFF)");
        // 原调用行已被替换（提示头注释中仍会记录旧 API 名，属预期）
        CSource.AssertNoCall(result, "lv_table_set_col_cnt(t, 3)");
        // 替换记录会写入提示头
        Assert.Contains("LVGL 兼容性检查", result);
    }

    [Fact]
    public void Process_ReportsRemovedApis_AsComments()
    {
        var code = "    lv_checkbox_set_checked(cb, true);\n";

        var result = LvglApiCompatChecker.Process(code, "9.5");

        Assert.Contains("lv_checkbox_set_checked", result);
        Assert.Contains("LV_STATE_CHECKED", result);
    }

    [Fact]
    public void Process_SkipsCheck_ForV8Project()
    {
        var code = "    lv_img_create(parent);\n";

        var result = LvglApiCompatChecker.Process(code, "8.3");

        // 8.x 下旧 API 属正常用法，不做替换也不加提示头
        Assert.Equal(code, result);
    }

    [Fact]
    public void Process_NoIssues_ReturnsUnchanged()
    {
        var code = "    lv_slider_set_range(s, 0, 100);\n";

        var result = LvglApiCompatChecker.Process(code, "9.5");

        Assert.Equal(code, result);
    }
}
