using System.Linq;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：<see cref="CSource"/> —— 生成代码断言工具的**自测**。
///
/// 断言工具本身出错会让整批生成代码测试失去意义（例如把「不匹配」判成「匹配」），
/// 所以这里对解析与匹配的边界逐条验证：排版无关、实参差异必被发现、
/// 注释/字符串里的同名文本不算调用、括号嵌套与不配平的处理。
/// </summary>
public class CSourceHelperTests
{
    // ───────────────── 解析 ─────────────────

    [Fact]
    public void ParseCalls_FindsPlainCallWithArgs()
    {
        var calls = CSource.ParseCalls("lv_obj_set_pos(obj_c1, 10, 20);");
        var call = Assert.Single(calls);
        Assert.Equal("lv_obj_set_pos", call.Name);
        Assert.Equal(new[] { "obj_c1", "10", "20" }, call.Args);
    }

    [Fact]
    public void ParseCalls_FindsNestedCalls()
    {
        var calls = CSource.ParseCalls("lv_obj_set_style_bg_color(obj_sl1, lv_color_hex(0x2563eb), LV_PART_KNOB);");
        Assert.Contains(calls, c => c.Name == "lv_obj_set_style_bg_color");
        Assert.Contains(calls, c => c.Name == "lv_color_hex" && c.Args.SequenceEqual(new[] { "0x2563eb" }));
    }

    [Fact]
    public void ParseCalls_SkipsStringLiteralsAndComments()
    {
        var code = """
            // lv_label_set_text(obj_x, "在注释里")
            const char* s = "lv_label_set_text(obj_y, \"在字符串里\")";
            /* lv_obj_create(obj_z) */
            lv_label_set_text(obj_real, "真的");
            """;

        var calls = CSource.ParseCalls(code);
        var call = Assert.Single(calls);
        Assert.Equal("lv_label_set_text", call.Name);
        Assert.Equal("obj_real", call.Args[0]);
    }

    [Fact]
    public void ParseCalls_SkipsZeroArgCallWithEmptyParens()
    {
        var calls = CSource.ParseCalls("lv_init();");
        var call = Assert.Single(calls);
        Assert.Empty(call.Args);
    }

    [Fact]
    public void ParseCalls_KeepsCommasInsideNestedParens()
    {
        var calls = CSource.ParseCalls("hmi_resolve_font(\"cjk\", &font_a, (x, y));");
        var call = calls.First(c => c.Name == "hmi_resolve_font");
        Assert.Equal(3, call.Args.Count);
        Assert.Equal("(x, y)", call.Args[2]);
    }

    [Fact]
    public void ParseCalls_ReturnsWhatItCanWhenParensUnbalanced()
    {
        var calls = CSource.ParseCalls("lv_init();\nlv_broken(obj_a, obj_b");
        Assert.Single(calls); // 只保留配平的那个，不抛异常
        Assert.Equal("lv_init", calls[0].Name);
    }

    [Fact]
    public void ParseCalls_DoesNotTreatKeywordsAsCalls()
    {
        var calls = CSource.ParseCalls("if (v > 0) { lv_obj_add_flag(obj, LV_OBJ_FLAG_HIDDEN); }");
        var call = Assert.Single(calls);
        Assert.Equal("lv_obj_add_flag", call.Name);
    }

    // ───────────────── 匹配：排版无关 ─────────────────

    [Theory]
    [InlineData("lv_obj_set_pos(obj_c1,10,20);")]
    [InlineData("lv_obj_set_pos( obj_c1 , 10 , 20 );")]
    [InlineData("  lv_obj_set_pos(obj_c1, 10, 20)  ")]
    [InlineData("lv_obj_set_pos(\n    obj_c1,\n    10,\n    20\n);")]
    public void AssertCall_IsFormattingInsensitive(string actual)
    {
        CSource.AssertCall(actual, "lv_obj_set_pos(obj_c1, 10, 20)");
    }

    [Fact]
    public void AssertText_IsFormattingInsensitiveForDeclarations()
    {
        CSource.AssertText(
            "static uint8_t buf_svg1[LV_CANVAS_BUF_SIZE(200,  100, 32,\n    LV_DRAW_BUF_STRIDE_ALIGN)];",
            "static uint8_t buf_svg1[LV_CANVAS_BUF_SIZE(200, 100, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
    }

    // ───────────────── 匹配：必须能抓住真实差异 ─────────────────

    [Fact]
    public void AssertCall_DetectsWrongArgument()
    {
        var ex = Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("lv_obj_set_pos(obj_c1, 10, 21);", "lv_obj_set_pos(obj_c1, 10, 20)"));
        Assert.Contains("未找到调用", ex.Message);
        Assert.Contains("lv_obj_set_pos(obj_c1, 10, 21)", ex.Message); // 失败信息给出同名候选
    }

    [Fact]
    public void AssertCall_DetectsWrongArgumentCount()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("lv_obj_set_pos(obj_c1, 10);", "lv_obj_set_pos(obj_c1, 10, 20)"));
    }

    [Fact]
    public void AssertCall_DetectsWrongObject()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("lv_label_set_text(obj_other, \"OK\");", "lv_label_set_text(obj_c1, \"OK\")"));
    }

    [Fact]
    public void AssertCall_DetectsWrongStringLiteral()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("lv_label_set_text(obj_c1, \"NO\");", "lv_label_set_text(obj_c1, \"OK\")"));
    }

    [Fact]
    public void AssertCall_DoesNotMatchTextInsideCommentOrString()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("/* lv_label_set_text(obj_c1, \"OK\") */", "lv_label_set_text(obj_c1, \"OK\")"));
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCall("const char* s = \"lv_label_set_text(obj_c1, \\\"OK\\\")\";",
                "lv_label_set_text(obj_c1, \"OK\")"));
    }

    [Fact]
    public void AssertNoCall_PassesWhenOnlyCommentMentionsIt()
    {
        // 注释里出现不代表真的调用了 —— 这正是「字符串包含式断言」会误判的情形。
        CSource.AssertNoCall("/* lv_canvas_set_px_opa(obj, 1, 2, 3, 4) */\nlv_init();",
            "lv_canvas_set_px_opa(obj, 1, 2, 3, 4)");
    }

    [Fact]
    public void AssertNoCall_FailsOnRealCall()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertNoCall("lv_obj_set_hidden(obj_c1, true);", "lv_obj_set_hidden(obj_c1, true)"));
    }

    // ───────────────── 智能分派 ─────────────────

    [Fact]
    public void AssertCode_DispatchesToCallMatching()
    {
        // 调用：排版不同也应通过
        CSource.AssertCode("lv_label_set_text(obj_c1,\n    \"Hello HMI\");", "lv_label_set_text(obj_c1, \"Hello HMI\")");
        // 非调用（声明）：退化为排版无关的包含
        CSource.AssertCode("static void hmi_ui_apply_theme(lv_obj_t* root_scr)\n{", "static void hmi_ui_apply_theme(lv_obj_t* root_scr)");
        // 非调用且确实缺失：必须失败
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCode("static void other(void)\n{", "static void hmi_ui_apply_theme(lv_obj_t* root_scr)"));
    }

    [Fact]
    public void AssertNoCode_DispatchesBothWays()
    {
        CSource.AssertNoCode("lv_init();", "lv_obj_set_hidden(obj_c1, true)");
        CSource.AssertNoCode("lv_init();", "static void never_emitted(void)");
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertNoCode("static void never_emitted(void)\n{", "static void never_emitted(void)"));
    }

    [Fact]
    public void AssertCode_TreatsProseWithParenthesesAsText_NotAsCall()
    {
        // 生成器输出的是 C 注释；"Keyframes (reference)" 不是调用，
        // 断言必须走文本匹配路径（否则会去找一个名叫 Keyframes 的函数）。
        CSource.AssertCode("// Keyframes (reference)\n", "Keyframes (reference)");
        // 紧邻括号才可能是调用：说明性文本即使带括号也不会被当成调用去匹配
        Assert.ThrowsAny<System.Exception>(() =>
            CSource.AssertCode("// 没有这个调用\n", "lv_obj_create(obj_x)"));
    }

    [Fact]
    public void AssertText_ToleratesMissingTrailingSemicolon()
    {
        CSource.AssertText("set_tag_value(TAG_v_motor, 1.0f);", "set_tag_value(TAG_v_motor, 1.0f);");
        CSource.AssertText("set_tag_value(TAG_v_motor, 1.0f)", "set_tag_value(TAG_v_motor, 1.0f);");
    }

    [Fact]
    public void Normalize_PreservesStringLiteralContentVerbatim()
    {
        Assert.Equal("f(\"a  b\",c)", CSource.Normalize("f( \"a  b\" , c )"));
        // 转义序列原样保留（生成代码里的 C 级转义不能被吃掉）
        Assert.Equal("f(\"\\\"q\\\"\")", CSource.Normalize("f( \"\\\"q\\\"\" )"));
    }
}
