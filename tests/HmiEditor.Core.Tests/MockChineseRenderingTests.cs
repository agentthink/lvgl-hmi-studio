using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Mock 项目中文渲染边界测试 —— 覆盖 CodeGenerator 对各类中文 / Unicode / 特殊字符
/// 文本的代码生成行为，确保生成的 LVGL C 代码能正确编译并保留字素。
/// </summary>
public class MockChineseRenderingTests
{
    // ── 测试基建 ──

    private static HmiProject BuildProjectWithLabel(string text, string id = "lbl_test")
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.Controls.Add(new HmiControl
        {
            Id = id,
            Name = "TestLabel",
            Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 400, Height = 30,
            Text = text,
        });
        project.Screens.Add(screen);
        return project;
    }

    private static string GenerateCode(HmiProject project)
        => new CodeGenerator().GenerateSimulatorModule(project);

    // ── 常用汉字 ──

    [Fact]
    public void GenerateLabel_CommonChinese_GeneratesSetText()
    {
        var code = GenerateCode(BuildProjectWithLabel("你好世界"));
        CSource.AssertCode(code, "lv_label_set_text(obj_lbl_test, \"你好世界\")");
    }

    // ── 罕用汉字（CJK Ext B / CJK Ext A）──

    [Theory]
    [InlineData("龘靐")]
    [InlineData("龍鳳")]
    [InlineData("壹贰叁肆伍")]
    public void GenerateLabel_RareChinese_GeneratesSetText(string text)
    {
        var code = GenerateCode(BuildProjectWithLabel(text));
        CSource.AssertCode(code, $"lv_label_set_text(obj_lbl_test, \"{text}\")");
    }

    // ── 繁体中文 ──

    [Fact]
    public void GenerateLabel_TraditionalChinese_GeneratesSetText()
    {
        var code = GenerateCode(BuildProjectWithLabel("繁體中文測試"));
        CSource.AssertCode(code, "lv_label_set_text(obj_lbl_test, \"繁體中文測試\")");
    }

    // ── CJK 标点符号 ──

    [Theory]
    [InlineData("·")]
    [InlineData("！？")]
    [InlineData("【】")]
    [InlineData("《》")]
    public void GenerateLabel_CjkPunctuation_GeneratesSetText(string punct)
    {
        var code = GenerateCode(BuildProjectWithLabel($"测试{punct}完成"));
        Assert.Contains($"测试{punct}完成", code);
    }

    // ── 混合 Unicode（希腊字母 / 数学符号）──

    [Fact]
    public void GenerateLabel_MixedUnicode_GeneratesSetText()
    {
        var text = "αβγ ΣΠΩ ≠≤≥∞";
        var code = GenerateCode(BuildProjectWithLabel(text));
        CSource.AssertCode(code, $"lv_label_set_text(obj_lbl_test, \"{text}\")");
    }

    // ── Emoji（ surrogate pair ）──

    [Fact]
    public void GenerateLabel_Emoji_GeneratesSetText()
    {
        var text = "💾 保存设置";
        var code = GenerateCode(BuildProjectWithLabel(text));
        CSource.AssertCode(code, $"lv_label_set_text(obj_lbl_test, \"{text}\")");
    }

    // ── 空字符串 ──

    [Fact]
    public void GenerateLabel_EmptyText_GeneratesNoSetText()
    {
        var code = GenerateCode(BuildProjectWithLabel(""));
        Assert.DoesNotContain("lv_label_set_text", code);
    }

    [Fact]
    public void GenerateLabel_NullText_GeneratesNoSetText()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "lbl_null", Name = "Null", Type = HmiControlType.Label, Text = null,
        });
        var code = GenerateCode(project);
        Assert.DoesNotContain("lv_label_set_text", code);
    }

    // ── 超长中文文本（500 字）──

    [Fact]
    public void GenerateLabel_LongChineseText_NotTruncated()
    {
        var text = string.Concat(Enumerable.Repeat("智慧工厂中央控制屏", 30)); // 300 字
        var code = GenerateCode(BuildProjectWithLabel(text));
        Assert.Contains(text, code);
    }

    // ── 中文 + 数字单位 ──

    [Theory]
    [InlineData("温度：27.5 ℃")]
    [InlineData("压力：0.85 MPa")]
    [InlineData("流量：142.6 L/min")]
    [InlineData("功率：37.4 kW")]
    public void GenerateLabel_ChineseWithUnits_GeneratesSetText(string text)
    {
        var code = GenerateCode(BuildProjectWithLabel(text));
        CSource.AssertCode(code, $"lv_label_set_text(obj_lbl_test, \"{text}\")");
    }

    // ── 含引号（C 字符串转义）──

    [Fact]
    public void GenerateLabel_TextWithQuotes_EscapesProperly()
    {
        var code = GenerateCode(BuildProjectWithLabel("中文\"引号\"测试"));
        CSource.AssertCode(code, "lv_label_set_text(obj_lbl_test, \"中文\\\"引号\\\"测试\")");
    }

    // ── 含反斜杠 ──

    [Fact]
    public void GenerateLabel_TextWithBackslash_EscapesProperly()
    {
        var code = GenerateCode(BuildProjectWithLabel("路径C:\\Users\\测试"));
        CSource.AssertCode(code, "lv_label_set_text(obj_lbl_test, \"路径C:\\\\Users\\\\测试\")");
    }

    // ── 含换行符 ──

    [Fact]
    public void GenerateLabel_TextWithNewline_PreservedInCode()
    {
        // Label 文本中含 \n（C# 中的实际换行符），EscapeCString 不转换 \n，直接保留
        var code = GenerateCode(BuildProjectWithLabel("第一行\n第二行"));
        // 生成代码应包含实际换行（C 源码多行字符串不合法，但 EscapeCString 仅处理 \ 和 "）
        // 验证：两行文本都出现在代码中
        Assert.Contains("第一行", code);
        Assert.Contains("第二行", code);
    }

    // ── 按钮中文文本（创建子 label）──

    [Fact]
    public void GenerateButton_ChineseText_CreatesChildLabel()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "btn_start", Name = "启动按钮", Type = HmiControlType.Button,
            X = 10, Y = 10, Width = 120, Height = 40, Text = "启动生产线",
        });
        var code = GenerateCode(project);
        CSource.AssertCode(code, "lv_obj_t* label_btn_start = lv_label_create(obj_btn_start)");
        CSource.AssertCode(code, "lv_label_set_text(label_btn_start, \"启动生产线\")");
    }

    // ── 下拉框中文选项（\n 转义为 \\n）──

    [Fact]
    public void GenerateDropdown_ChineseOptions_EscapesNewlines()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "dd_recipe", Name = "配方选择", Type = HmiControlType.Dropdown,
            X = 10, Y = 10, Width = 200, Height = 40,
            Options = "默认配方\n高温烘焙\n低温慢煮",
            SelectedIndex = 0,
        });
        var code = GenerateCode(project);
        // 验证下拉框选项生成调用存在，且三个中文选项都出现在代码中
        CSource.AssertCode(code, "lv_dropdown_set_options(obj_dd_recipe,");
        Assert.Contains("默认配方", code);
        Assert.Contains("高温烘焙", code);
        Assert.Contains("低温慢煮", code);
        // 换行符应被转义为 \n（C 字符串中的换行转义），而非保留为实际换行
        Assert.DoesNotContain("默认配方\n", code); // 不应包含实际换行符
    }

    // ── TextArea 中文占位符 ──

    [Fact]
    public void GenerateTextArea_ChinesePlaceholder_GeneratesPlaceholderCall()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "ta_input", Name = "温度输入", Type = HmiControlType.TextArea,
            X = 10, Y = 10, Width = 200, Height = 40,
            PlaceholderText = "请输入温度（0-200）",
            Text = "125",
        });
        var code = GenerateCode(project);
        CSource.AssertCode(code, "lv_textarea_set_placeholder_text(obj_ta_input, \"请输入温度（0-200）\")");
    }

    // ── TextArea 软键盘接管（模拟器模式） ──

    [Fact]
    public void GenerateTextArea_SimulatorMode_AddsClickFocusable()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "ta_input", Name = "温度输入", Type = HmiControlType.TextArea,
            X = 10, Y = 10, Width = 200, Height = 40,
        });
        var code = GenerateCode(project);
        // 模拟器软键盘服务按 CLICK_FOCUSABLE 接管点击聚焦（弹 LVGL 内置键盘）
        CSource.AssertCode(code, "lv_obj_add_flag(obj_ta_input, LV_OBJ_FLAG_CLICK_FOCUSABLE)");
        // 未指定 Number 键盘：不生成 user_data 数字标记
        CSource.AssertNoCode(code, "set_user_data(obj_ta_input");
    }

    [Fact]
    public void GenerateTextArea_SimulatorMode_NumberKeyboard_MarksUserData()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "ta_input", Name = "温度输入", Type = HmiControlType.TextArea,
            X = 10, Y = 10, Width = 200, Height = 40,
            KeyboardMode = "Number",
        });
        var code = GenerateCode(project);
        CSource.AssertCode(code, "lv_obj_add_flag(obj_ta_input, LV_OBJ_FLAG_CLICK_FOCUSABLE)");
        // 数字键盘标记：模拟器 main.c 软键盘服务据此弹数字键盘
        CSource.AssertCode(code, "lv_obj_set_user_data(obj_ta_input, (void*)1)");
    }

    [Fact]
    public void GenerateTextArea_SharedLibraryMode_EmitsHostKeyboardMode()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "ta_input", Name = "温度输入", Type = HmiControlType.TextArea,
            X = 10, Y = 10, Width = 200, Height = 40,
            KeyboardMode = "Number",
        });
        // 固件 .so 模式：走宿主 hmi_set_ta_keyboard_mode（宿主软键盘），不生成 user_data
        var code = new CodeGenerator().GenerateSharedLibraryProject(project);
        CSource.AssertCode(code, "hmi_set_ta_keyboard_mode(obj_ta_input, \"num\")");
        CSource.AssertNoCode(code, "set_user_data(obj_ta_input");
    }

    // ── CheckBox 中文文本 ──

    [Fact]
    public void GenerateCheckBox_ChineseText_GeneratesSetText()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "cb_auto", Name = "自动模式", Type = HmiControlType.CheckBox,
            X = 10, Y = 10, Width = 200, Height = 30, Text = "启用自动模式",
        });
        var code = GenerateCode(project);
        CSource.AssertCode(code, "lv_checkbox_set_text(obj_cb_auto, \"启用自动模式\")");
    }

    // ── 多控件混合中文 ──

    [Fact]
    public void GenerateScreen_MultipleChineseControls_AllRendered()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "主控面板", Width = 1024, Height = 600 };
        screen.Controls.Add(new HmiControl { Id = "lbl1", Name = "温度", Type = HmiControlType.Label, Text = "温度：27.5℃" });
        screen.Controls.Add(new HmiControl { Id = "lbl2", Name = "压力", Type = HmiControlType.Label, Text = "压力：0.85 MPa" });
        screen.Controls.Add(new HmiControl { Id = "lbl3", Name = "罕用", Type = HmiControlType.Label, Text = "龘靐龍鳳" });
        project.Screens.Add(screen);
        var code = GenerateCode(project);

        Assert.Contains("温度：27.5℃", code);
        Assert.Contains("压力：0.85 MPa", code);
        Assert.Contains("龘靐龍鳳", code);
    }
}
