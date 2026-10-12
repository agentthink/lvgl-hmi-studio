using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Lua 真语法校验的判据（「编辑器侧 Lua 校验做强」）。
/// 见 <c>docs/2026-09-20-lua-syntax-validation.md</c>。
///
/// <para>本组里有三类特殊用例，动它们之前先读文档：</para>
/// <list type="bullet">
/// <item><see cref="BuiltinTemplates_AllPassSyntaxCheck"/> —— <b>元判据</b>：内置模板本身不能有语法错，
/// 否则用户点一下「插入模板」就看到红字；</item>
/// <item><c>Dialect_*</c> 三条 —— <b>如实钉住基准（Lua 5.2）的方言行为</b>：5.3/5.4 的运算符必须被判错、
/// 且报错必须带方言说明。这是<b>已知误报的显式登记</b>，不是待修缺陷。
/// **注意 <c>Dialect_BaselineAccepts_*</c> 那条的语义**：「基准放行」**不等于**「设备上能用」——
/// 反例见 <c>LuaDialect.FalseNegatives</c>（<c>goto</c> / <c>bit32</c> / <c>table.pack</c> 基准放行、
/// 而 SDK 自带的 Lua 5.1.5 上不可用）。完整论证见
/// <c>docs/2026-09-20-lua-dialect-alignment.md</c>；</item>
/// <item><see cref="NegativeControl_RemovingEndFromTemplate_IsCaught"/> —— 负控，含正控对照。</item>
/// </list>
/// </summary>
public class LuaSyntaxValidatorTests
{
    // ── 真语法解析 ──

    [Fact]
    public void ValidScript_IsValid()
    {
        var r = LuaSyntaxValidator.Validate("function on_click()\n  local v = hmi_read(\"x\")\nend");

        Assert.True(r.IsValid);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void MissingEnd_ReportsLineNumberAndChineseFallback()
    {
        var code = "function f()\n  if true then\n    local a = 1\n  end\n";
        var r = LuaSyntaxValidator.Validate(code);

        Assert.False(r.IsValid);
        var e = Assert.Single(r.Errors);
        Assert.Equal(LuaSyntaxValidator.SyntaxMissingEndCode, e.Code);
        // 行号必须真的被填出来 —— 上一版拿 ex.Message 去找坐标（坐标只在 DecoratedMessage 里），
        // 行号恒为 0 且无人发现。
        Assert.True(e.Line > 0, $"行号必须是正数，实际 {e.Line}");
        Assert.Contains("缺少 end", e.Message);

        // ① 行号**不进** Message：它在 Line 字段里，由界面统一渲染（ExternalText.Describe）。
        //    两处都有就必然有一处先过期 —— 这条断言是"行号只有一个来源"的守卫。
        Assert.DoesNotContain($"第 {e.Line} 行", e.Message);

        // ② 无占位符的诊断不许带 Args：带了就说明模板里没有 {0}，
        //    而 Format 在实参多于占位符时**不抛异常**（参数被静默丢弃），只有钉住才发现。
        Assert.Null(e.Args);
    }

    [Fact]
    public void UnfinishedString_ReportsChineseMessage()
    {
        var r = LuaSyntaxValidator.Validate("function f()\n  local s = \"abc\nend");

        Assert.False(r.IsValid);
        Assert.Contains("字符串没有闭合", Assert.Single(r.Errors).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void EmptyCode_IsValid_BecauseCallerOwnsThatCheck(string? code)
        => Assert.True(LuaSyntaxValidator.Validate(code).IsValid);

    // ── 方言边界（已知误报，显式钉住；版本标签一律从 LuaDialect 取，不写死）──

    [Theory]
    [InlineData("function f() local a = 7 // 2 end", "整除")]
    [InlineData("function f() local a = 1 & 3 end", "按位与")]
    [InlineData("function f() local a = 1 | 3 end", "按位或")]
    [InlineData("function f() local a = 8 >> 2 end", "右移")]
    public void Dialect_Lua53Operators_AreRejected_WithDialectHint(string code, string what)
    {
        var r = LuaSyntaxValidator.Validate(code);

        Assert.False(r.IsValid);                 // 基准解析器确实不认
        var msg = Assert.Single(r.Errors).Message;
        Assert.Contains(LuaDialect.LabelOf(LuaVersion.Lua53), msg);      // 但必须说清这是方言差异
        Assert.Contains(LuaDialect.ParserBaselineLabel, msg);
        Assert.Contains(what, msg);
    }

    [Fact]
    public void Dialect_Lua54Attribute_IsRejected_WithDialectHint()
    {
        var r = LuaSyntaxValidator.Validate("function f() local x <const> = 1 end");

        Assert.False(r.IsValid);
        var msg = Assert.Single(r.Errors).Message;
        Assert.Contains(LuaDialect.LabelOf(LuaVersion.Lua54), msg);
        Assert.Contains(LuaDialect.ParserBaselineLabel, msg);
    }

    /// <summary>
    /// **「基准放行」不是「设备可用」** —— 本测试只证明解析器收下这些写法，
    /// 不证明设备上跑得起来（<c>goto</c> / <c>bit32</c> / <c>table.pack</c> 在 SDK 自带的
    /// Lua 5.1.5 上全部不可用，见 <see cref="LuaDialect.FalseNegatives"/>）。
    /// 名字刻意写长就是为了让"这是基准判据"这件事在测试列表里一眼可见。
    /// </summary>
    [Theory]
    [InlineData("function f() goto done ::done:: end")]         // 5.2 有 goto（5.1 没有）
    [InlineData("function f() local a = bit32.band(1, 3) end")] // 5.2 有 bit32（5.1 没有）
    [InlineData("function f() local t = table.pack(1, 2) end")] // 5.2 有 table.pack（5.1 没有）
    [InlineData("function f() local a = 0x1p4 end")]            // 十六进制浮点
    [InlineData("function f(...) local a, b = ... end")]        // 可变参数
    public void Dialect_BaselineAccepts_These_WhichSaysNothingAboutTheDevice(string code)
        => Assert.True(LuaSyntaxValidator.Validate(code).IsValid, code);

    // ── 剥注释 ──

    [Fact]
    public void StripComments_RemovesLineComment_AndKeepsLineCount()
    {
        var code = "function f()  -- 注释\n  local a = 1  -- 又一个\nend";
        var stripped = LuaSyntaxValidator.StripComments(code);

        Assert.DoesNotContain("注释", stripped);
        Assert.Contains("function f()", stripped);
        Assert.Equal(code.Count(c => c == '\n'), stripped.Count(c => c == '\n'));
    }

    [Fact]
    public void StripComments_RemovesBlockComment_BothForms_KeepsLineCount()
    {
        var code = "function f()\n--[[ 多行\n注释 ]]\n  local a = 1\n--[==[ 等号形式\n也剥掉 ]==]\nend";
        var stripped = LuaSyntaxValidator.StripComments(code);

        Assert.DoesNotContain("多行", stripped);
        Assert.DoesNotContain("等号形式", stripped);
        Assert.Contains("local a = 1", stripped);
        Assert.Equal(code.Count(c => c == '\n'), stripped.Count(c => c == '\n'));
    }

    [Fact]
    public void StripComments_KeepsStringLiterals_ThatContainDoubleDash()
    {
        var stripped = LuaSyntaxValidator.StripComments("local s = \"a -- b\"\nlocal a = 1");

        Assert.Contains("a -- b", stripped);
        Assert.Contains("local a = 1", stripped);
    }

    // ── 函数定义判据 ──

    [Theory]
    [InlineData("function on_click() end")]
    [InlineData("local f = function() end")]
    [InlineData("local f = function(a) return a end")]
    public void HasFunctionDefinition_RealDefinitions_True(string code)
        => Assert.True(LuaSyntaxValidator.HasFunctionDefinition(code));

    [Theory]
    [InlineData("-- function ghost()\nlocal a = 1")]        // 行注释里的假函数
    [InlineData("--[[ function ghost() ]]\nlocal a = 1")]   // 块注释里的假函数
    [InlineData("local a = 1")]
    [InlineData("")]
    [InlineData(null)]
    public void HasFunctionDefinition_NoRealDefinition_False(string? code)
        => Assert.False(LuaSyntaxValidator.HasFunctionDefinition(code));

    // ── ScriptEngineService 集成 ──

    /// <summary>元判据：4 个内置模板必须全部通过真语法校验。</summary>
    [Fact]
    public void BuiltinTemplates_AllPassSyntaxCheck()
    {
        var engine = new ScriptEngineService();

        foreach (var template in ScriptTemplate.Defaults)
        {
            var result = engine.Validate(new HmiScript { Name = template.Name, Code = template.Code });
            Assert.True(result.IsValid,
                $"内置模板「{template.Name}」必须有合法语法（用户点一下就插入），实际错误：" +
                string.Join(" / ", result.Errors.Select(e => e.Message)));
        }
    }

    [Fact]
    public void Validate_SyntaxError_IsReported()
    {
        var engine = new ScriptEngineService();
        var r = engine.Validate(new HmiScript { Name = "S", Code = "function f() if true then end" });

        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => LuaSyntaxValidator.IsSyntaxError(e.Code));
    }

    [Fact]
    public void Validate_CommentOnlyFunction_ReportsNoFunction()
    {
        var engine = new ScriptEngineService();
        var r = engine.Validate(new HmiScript
        {
            Name = "S",
            Code = "local a = 1\n-- function on_click()\n-- end",
        });

        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Code == "NO_FUNCTION");
    }

    // ── 负控 ──

    [Fact]
    public void NegativeControl_RemovingEndFromTemplate_IsCaught()
    {
        var code = ScriptTemplate.OnClick.Code;
        int lastEnd = code.LastIndexOf("end", StringComparison.Ordinal);
        Assert.True(lastEnd > 0, "模板里应当有 end");
        var corrupted = code.Remove(lastEnd, 3);

        var engine = new ScriptEngineService();
        var broken = engine.Validate(new HmiScript { Name = "S", Code = corrupted });

        Assert.False(broken.IsValid);
        Assert.Contains(broken.Errors, e => LuaSyntaxValidator.IsSyntaxError(e.Code));

        // 正控必须同场：同一条判据下，未破坏的模板要过 ——
        // 否则"会红"这件事没有信息量（可能是判据恒红）。
        Assert.True(engine.Validate(new HmiScript { Name = "S", Code = code }).IsValid);
    }
}
