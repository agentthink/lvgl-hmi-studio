using HmiEditor.Core.CodeGen;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="CIdentifier"/> 的规则测试 —— 这份实现由两条生成链路（<c>CodeGenerator</c> →
/// <c>generated_hmi.c</c>，<c>SimulatorProjectExporter</c> → <c>main.c</c>/<c>generated_*.c</c>）共用，
/// 规则本身是跨模块符号契约的一部分，所以单独钉一遍。
/// </summary>
public class CIdentifierTests
{
    [Fact]
    public void NullOrEmpty_FallsBackPerKind()
    {
        // 资源符号：空 id 会出现在 IMG_PATH_*/img_*/font_* 里，兜底值必须两条链路一致
        Assert.Equal(CIdentifier.AssetFallback, CIdentifier.ForAsset(null));
        Assert.Equal(CIdentifier.AssetFallback, CIdentifier.ForAsset(string.Empty));

        // 控件/动画符号
        Assert.Equal(CIdentifier.ControlSymbolFallback, CIdentifier.ForControlSymbol(null));
        Assert.Equal(CIdentifier.ControlSymbolFallback, CIdentifier.ForControlSymbol(string.Empty));
    }

    [Theory]
    [InlineData("img-1", "img_1")]
    [InlineData("a.b.c", "a_b_c")]
    [InlineData("font 1", "font_1")]
    [InlineData("x/y", "x_y")]
    [InlineData("ALREADY_OK", "ALREADY_OK")]
    [InlineData("_lead", "_lead")]
    [InlineData("trail_", "trail_")]
    [InlineData("with1digit", "with1digit")]
    public void IllegalCharacters_BecomeUnderscore(string input, string expected)
    {
        Assert.Equal(expected, CIdentifier.ForAsset(input));
        Assert.Equal(expected, CIdentifier.ForControlSymbol(input));
    }

    [Fact]
    public void ChineseCharacters_ArePreserved_WhichIsTheExistingBehaviour()
    {
        // 刻意钉住：char.IsLetterOrDigit 把中文判为字母，所以中文 id 原样保留。
        // 严格按 C 标准这需要 UCN，但既有产物（已有工程文件）依赖该行为，
        // 统一实现时不得顺手"修正"，否则全部生成结果都会变。要改属于独立议题。
        Assert.Equal("图片2", CIdentifier.ForAsset("图片2"));
    }

    [Fact]
    public void BothKinds_AgreeForEveryNonEmptyInput()
    {
        // 唯一的差别只应体现在空输入的兜底值上：非空输入必须给出同一个符号，
        // 否则"资源 id"被误用为"控件 id"（或反之）时符号会悄悄漂移。
        foreach (var sample in new[] { "img-1", "图片2", "_x", "a b", "f.t" })
        {
            Assert.Equal(CIdentifier.ForAsset(sample), CIdentifier.ForControlSymbol(sample));
        }
    }

    [Fact]
    public void Sanitize_IsIdempotent()
    {
        // 二次净化的输入（例如已被净化过的 id 再次进入）不应继续变化
        foreach (var sample in new[] { "img-1", "图片2", "a_b", "f.t" })
        {
            var once = CIdentifier.ForAsset(sample);
            Assert.Equal(once, CIdentifier.ForAsset(once));
        }
    }

    // ───────────────────── obj_* 的唯一构造器 ─────────────────────

    /// <summary>
    /// <see cref="CIdentifier.ControlObject"/> 是 <c>obj_*</c> 的**唯一**构造器。
    /// 声明侧（<c>Screen.cs</c>）与引用侧（绑定刷新 / 视觉状态 / 参数）都必须走它，
    /// 否则 id 含需净化字符时两边符号名会对不上（见
    /// <see cref="ControlObjectSymbolContractTests"/>）。
    /// </summary>
    [Theory]
    [InlineData("lbl", "obj_lbl")]
    [InlineData("tbl_main", "obj_tbl_main")]
    [InlineData("a8f3c1d2", "obj_a8f3c1d2")]      // 设计器自动生成的 8 位十六进制 → 恒等
    [InlineData("lbl-1", "obj_lbl_1")]             // 非标识符字符 → 净化
    [InlineData("bar 2", "obj_bar_2")]
    [InlineData("a.b", "obj_a_b")]
    [InlineData("温度", "obj_温度")]                 // 中文按既有行为保留
    public void ControlObject_PrefixesAndSanitizes(string id, string expected)
        => Assert.Equal(expected, CIdentifier.ControlObject(id));

    /// <summary>空 id 落到 <see cref="CIdentifier.ControlSymbolFallback"/> —— 与绑定刷新侧历史行为一致。</summary>
    [Fact]
    public void ControlObject_EmptyInput_UsesControlSymbolFallback()
    {
        Assert.Equal($"obj_{CIdentifier.ControlSymbolFallback}", CIdentifier.ControlObject(null));
        Assert.Equal($"obj_{CIdentifier.ControlSymbolFallback}", CIdentifier.ControlObject(string.Empty));
    }

    /// <summary>合法标识符必须是**恒等**映射 —— 这保证了统一构造器不改变既有工程产物。</summary>
    [Fact]
    public void ControlObject_IsIdentityForLegalIdentifiers()
    {
        foreach (var id in new[] { "lbl", "a8f3c1d2", "_lead", "trail_", "with1digit", "图片2" })
        {
            Assert.Equal("obj_" + id, CIdentifier.ControlObject(id));
        }
    }

    // ───────────────────── 带前缀的生成符号（2026-09-15 收口） ─────────────────────

    /// <summary>
    /// <c>Prefixed</c> 是控件类符号族（<c>label_</c> / <c>_rec_</c> / <c>_logbuf_</c> …）的唯一构造入口：
    /// 前缀原样保留，id 部分必须净化。
    /// </summary>
    [Theory]
    [InlineData("label_", "lbl", "label_lbl")]
    [InlineData("label_", "btn 2", "label_btn_2")]
    [InlineData("_rec_", "al-1", "_rec_al_1")]
    [InlineData("_logbuf_", "lg.1", "_logbuf_lg_1")]
    [InlineData("_pts_", "图片2", "_pts_图片2")]      // 中文按既有行为保留
    public void Prefixed_KeepsPrefixAndSanitizesId(string prefix, string id, string expected)
        => Assert.Equal(expected, CIdentifier.Prefixed(prefix, id));

    /// <summary>空 id 同样落到兜底值（否则会产出 <c>label_</c> 这种悬空前缀）。</summary>
    [Fact]
    public void Prefixed_EmptyInput_UsesControlSymbolFallback()
    {
        Assert.Equal("label_" + CIdentifier.ControlSymbolFallback, CIdentifier.Prefixed("label_", null));
        Assert.Equal("label_" + CIdentifier.ControlSymbolFallback, CIdentifier.Prefixed("label_", string.Empty));
    }

    /// <summary>
    /// 屏幕 / 图层族：<c>create_screen_*</c> 跨文件（生成器 ↔ 导出器），前缀字面量必须钉死。
    /// </summary>
    [Theory]
    [InlineData("scr1", "create_screen_scr1", "create_screen_scr1_inner", "adaptive_layout_scr1", "screen_scr1")]
    [InlineData("scr-1", "create_screen_scr_1", "create_screen_scr_1_inner", "adaptive_layout_scr_1", "screen_scr_1")]
    [InlineData("屏 幕", "create_screen_屏_幕", "create_screen_屏_幕_inner", "adaptive_layout_屏_幕", "screen_屏_幕")]
    public void ScreenFamily_ProducesExpectedSymbols(string id, string factory, string inner, string adaptive, string screenObj)
    {
        Assert.Equal(factory, CIdentifier.ScreenFactory(id));
        Assert.Equal(inner, CIdentifier.ScreenFactoryInner(id));
        Assert.Equal(adaptive, CIdentifier.AdaptiveLayoutFn(id));
        Assert.Equal(screenObj, CIdentifier.ScreenObject(id));
        Assert.Equal("layer_" + CIdentifier.ForControlSymbol(id), CIdentifier.LayerObject(id));
    }

    /// <summary>报警族（文件级 static 记录 + 判定函数，跨函数）。</summary>
    [Theory]
    [InlineData("al1", "_rec_al1", "check_alarm_al1")]
    [InlineData("al-1", "_rec_al_1", "check_alarm_al_1")]
    [InlineData("var_v 1", "_rec_var_v_1", "check_alarm_var_v_1")]
    public void AlarmFamily_ProducesExpectedSymbols(string id, string record, string checkFn)
    {
        Assert.Equal(record, CIdentifier.AlarmRecord(id));
        Assert.Equal(checkFn, CIdentifier.AlarmCheckFn(id));
    }

    /// <summary>数据记录器族（文件级 static 环形缓冲 + 接口函数）。</summary>
    [Theory]
    [InlineData("lg1", "_logbuf_lg1", "_logwr_lg1", "_logcnt_lg1", "_logfull_lg1",
        "datalog_init_lg1", "datalog_sample_lg1", "datalog_export_csv_lg1")]
    [InlineData("lg 1", "_logbuf_lg_1", "_logwr_lg_1", "_logcnt_lg_1", "_logfull_lg_1",
        "datalog_init_lg_1", "datalog_sample_lg_1", "datalog_export_csv_lg_1")]
    public void DataLogFamily_ProducesExpectedSymbols(
        string id, string buf, string wr, string cnt, string full, string initFn, string sampleFn, string csvFn)
    {
        Assert.Equal(buf, CIdentifier.LogBuffer(id));
        Assert.Equal(wr, CIdentifier.LogWriteIndex(id));
        Assert.Equal(cnt, CIdentifier.LogCount(id));
        Assert.Equal(full, CIdentifier.LogFullFlag(id));
        Assert.Equal(initFn, CIdentifier.DataLogInitFn(id));
        Assert.Equal(sampleFn, CIdentifier.DataLogSampleFn(id));
        Assert.Equal(csvFn, CIdentifier.DataLogExportCsvFn(id));
    }

    /// <summary>趋势图族（文件级 static 图表/序列指针 + 初始化/刷新函数）。</summary>
    [Theory]
    [InlineData("tr1", "_chart_tr1", "_series_tr1", "trend_init_tr1", "trend_update_tr1")]
    [InlineData("tr.1", "_chart_tr_1", "_series_tr_1", "trend_init_tr_1", "trend_update_tr_1")]
    public void TrendFamily_ProducesExpectedSymbols(string id, string chart, string series, string initFn, string updateFn)
    {
        Assert.Equal(chart, CIdentifier.ChartObject(id));
        Assert.Equal(series, CIdentifier.ChartSeries(id));
        Assert.Equal(initFn, CIdentifier.TrendInitFn(id));
        Assert.Equal(updateFn, CIdentifier.TrendUpdateFn(id));
    }

    /// <summary>
    /// 全部具名构造器对**合法标识符**都是恒等 —— 这是"收口不改变任何既有产物"的根据
    /// （逐字节等价校验见 docs/2026-09-15-local-symbol-families.md）。
    /// </summary>
    [Fact]
    public void NamedConstructors_AreIdentityForLegalIdentifiers()
    {
        const string id = "a8f3c1d2";
        foreach (var actual in new[]
                 {
                     CIdentifier.ScreenFactory(id), CIdentifier.AdaptiveLayoutFn(id),
                     CIdentifier.ScreenObject(id), CIdentifier.LayerObject(id),
                     CIdentifier.AlarmRecord(id), CIdentifier.AlarmCheckFn(id),
                     CIdentifier.LogBuffer(id), CIdentifier.LogWriteIndex(id), CIdentifier.LogCount(id),
                     CIdentifier.LogFullFlag(id), CIdentifier.DataLogInitFn(id), CIdentifier.DataLogSampleFn(id),
                     CIdentifier.DataLogExportCsvFn(id), CIdentifier.ChartObject(id), CIdentifier.ChartSeries(id),
                     CIdentifier.TrendInitFn(id), CIdentifier.TrendUpdateFn(id),
                 })
        {
            Assert.EndsWith("_" + id, actual);
        }

        // _inner 后缀在 id 之后，单独断言（它同样必须是恒等，只是不以 id 结尾）
        Assert.Equal($"create_screen_{id}_inner", CIdentifier.ScreenFactoryInner(id));
    }
}
