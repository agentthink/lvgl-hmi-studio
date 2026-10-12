using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="CResourceSymbols"/> 的单元测试 —— 导出侧资源符号表。
///
/// <para>这张表的意义是「<b>每批资源只算一次</b>」：产物里出现的每个资源符号片段都必须
/// 来自它，代码段不得再自行净化。本类钉住表本身的语义（净化、文件名、字号、
/// 默认字体、变体收集），产物层的一致性守卫见 <c>ExportResourceSymbolTests</c>。</para>
/// </summary>
public class CResourceSymbolsTests
{
    // ══════════════════════ 测试基建 ══════════════════════

    private static AssetItem Image(string id, string filePath = "a/logo.bmp", int height = 100)
        => new() { Id = id, Name = $"图_{id}", Type = AssetType.Image, FilePath = filePath, Height = height };

    private static AssetItem Font(string id, string filePath = "a/cjk.ttf", int height = 0)
        => new() { Id = id, Name = $"字_{id}", Type = AssetType.Font, FilePath = filePath, Height = height };

    private static HmiProject Project(int themeFontSize = 14, params HmiControl[] controls)
    {
        var project = new HmiProject();
        if (themeFontSize > 0) project.Theme = new HmiTheme { FontSize = themeFontSize };
        var screen = new HmiScreen { Id = "s1", Name = "主画面", Width = 800, Height = 480 };
        foreach (var c in controls) screen.Controls.Add(c);
        project.Screens.Add(screen);
        return project;
    }

    private static HmiControl Label(string fontFamily = "", int fontSize = 0, string id = "lbl")
        => new()
        {
            Id = id, Name = id, Type = HmiControlType.Label, Text = "t",
            X = 0, Y = 0, Width = 10, Height = 10,
            FontFamily = fontFamily, FontSize = fontSize,
        };

    // ══════════════════════ A. 净化 ══════════════════════

    [Fact]
    public void Create_SanitizesIds_SoFragmentsAreValidCSymbolBodies()
    {
        var table = CResourceSymbols.Create(
            new List<AssetItem> { Image("img-1"), Image("a.b.c") },
            new List<AssetItem> { Font("font-1"), Font("x/y") },
            Project());

        Assert.Equal(new[] { "img_1", "a_b_c" }, table.Images.Select(i => i.Fragment));
        Assert.Equal(new[] { "font_1", "x_y" }, table.Fonts.Select(f => f.Fragment));
    }

    [Fact]
    public void Create_PreservesChineseIds_WhichIsTheExistingBehaviour()
    {
        // char.IsLetterOrDigit 把中文判为合法 → 片段原样保留。
        // 严格 C 标准需 UCN，但既有产物依赖该行为，统一时不得顺手修正。
        var table = CResourceSymbols.Create(
            new List<AssetItem> { Image("图片2") },
            new List<AssetItem> { Font("中文字体") },
            Project());

        Assert.Equal("图片2", table.Images[0].Fragment);
        Assert.Equal("中文字体", table.Fonts[0].Fragment);
    }

    [Fact]
    public void Create_EmptyId_FallsBackToAssetFallback()
    {
        var table = CResourceSymbols.Create(
            new List<AssetItem> { Image(string.Empty) },
            new List<AssetItem> { Font(null!) },
            Project());

        Assert.Equal(CIdentifier.AssetFallback, table.Images[0].Fragment);
        Assert.Equal(CIdentifier.AssetFallback, table.Fonts[0].Fragment);
    }

    // ══════════════════════ B. 派生信息 ══════════════════════

    [Fact]
    public void Create_FileName_IsLastSegmentWithBackslashesNormalized()
    {
        // 资产路径可能是 Windows 录入的；导出时统一转正斜杠再取末段，
        // 否则 SIM_ASSET_DIR "/C:\a\logo.bmp" 会拼出非法路径。
        var table = CResourceSymbols.Create(
            new List<AssetItem> { Image("i1", @"C:\proj\assets\logo.bmp") },
            new List<AssetItem> { Font("f1", @"D:\fonts\cjk.ttf") },
            Project());

        Assert.Equal("logo.bmp", table.Images[0].FileName);
        Assert.Equal("cjk.ttf", table.Fonts[0].FileName);
    }

    [Fact]
    public void Create_FontPixelSize_UsesHeightWhenPositive_ElseDefault()
    {
        var table = CResourceSymbols.Create(
            new List<AssetItem>(),
            new List<AssetItem> { Font("f_zero", height: 0), Font("f_24", height: 24), Font("f_neg", height: -3) },
            Project());

        Assert.Equal(CResourceSymbols.DefaultFontPixelSize, table.Fonts[0].PixelSize);
        Assert.Equal(24, table.Fonts[1].PixelSize);
        // 负数走同一兜底：Height > 0 才采用
        Assert.Equal(CResourceSymbols.DefaultFontPixelSize, table.Fonts[2].PixelSize);
    }

    [Fact]
    public void Create_DefaultFontFragment_IsFirstFont_AndNullWhenNoFonts()
    {
        var withFonts = CResourceSymbols.Create(
            new List<AssetItem>(), new List<AssetItem> { Font("first"), Font("second") }, Project());
        Assert.Equal("first", withFonts.DefaultFontFragment);

        var noFonts = CResourceSymbols.Create(new List<AssetItem>(), new List<AssetItem>(), Project());
        Assert.Null(noFonts.DefaultFontFragment);
    }

    [Fact]
    public void Create_EmptyInputs_YieldEmptyTable()
    {
        var table = CResourceSymbols.Create(new List<AssetItem>(), new List<AssetItem>(), Project());

        Assert.Empty(table.Images);
        Assert.Empty(table.Fonts);
        Assert.Empty(table.FontVariants);
        Assert.Null(table.DefaultFontFragment);
    }

    [Fact]
    public void Create_NullArguments_Throw()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            CResourceSymbols.Create(null!, new List<AssetItem>(), Project()));
        Assert.Throws<System.ArgumentNullException>(() =>
            CResourceSymbols.Create(new List<AssetItem>(), null!, Project()));
        Assert.Throws<System.ArgumentNullException>(() =>
            CResourceSymbols.Create(new List<AssetItem>(), new List<AssetItem>(), null!));
    }

    // ══════════════════════ C. 变尺寸字体收集 ══════════════════════

    [Fact]
    public void CollectFontVariants_SkipsThemeSize_AndTakesNonThemeOnes()
    {
        // 主题 14：字号 14 的控件由基础字体变量覆盖，不产生变体；
        // 字号 22 的控件产生 font_<family>_22。
        var project = Project(14, Label("font-1", 14, "a"), Label("font-1", 22, "b"), Label("font-1", 0, "c"));
        var table = CResourceSymbols.Create(
            new List<AssetItem>(), new List<AssetItem> { Font("font-1") }, project);

        // 字号 0 → 走主题 14 → 跳过。故只剩一条变体。
        var variant = Assert.Single(table.FontVariants);
        Assert.Equal("font_1", variant.FontFragment);
        Assert.Equal(22, variant.Size);
    }

    [Fact]
    public void CollectFontVariants_UsesDefaultFont_WhenControlFamilyIsEmpty()
    {
        var project = Project(14, Label("", 22, "a"));
        var table = CResourceSymbols.Create(
            new List<AssetItem>(), new List<AssetItem> { Font("font-1") }, project);

        var variant = Assert.Single(table.FontVariants);
        Assert.Equal("font_1", variant.FontFragment);
    }

    [Fact]
    public void CollectFontVariants_KeepsUnknownFamily_BecauseFilteringWouldChangeExistingOutput()
    {
        // 行为保持：FontFamily 不在字体资源库内也照样生成变体符号（既有产物如此）。
        // 若在此处做存在性过滤，会删掉产物里的符号 → 属于行为变更，不在统一范围内。
        var project = Project(14, Label("not-in-library", 22, "a"));
        var table = CResourceSymbols.Create(
            new List<AssetItem>(), new List<AssetItem> { Font("font-1") }, project);

        var variant = Assert.Single(table.FontVariants);
        Assert.Equal("not_in_library", variant.FontFragment);
    }

    [Fact]
    public void CollectFontVariants_DeduplicatesAndSortsByFragmentThenSize()
    {
        var project = Project(14,
            Label("b-font", 22, "1"),
            Label("a-font", 30, "2"),
            Label("b-font", 22, "3"),   // 与 "1" 重复
            Label("b-font", 18, "4"),
            Label("a-font", 30, "5"));  // 与 "2" 重复
        var table = CResourceSymbols.Create(new List<AssetItem>(), new List<AssetItem>(), project);

        Assert.Equal(
            new[] { ("a_font", 30), ("b_font", 18), ("b_font", 22) },
            table.FontVariants.Select(v => (v.FontFragment, v.Size)).ToArray());
    }

    [Fact]
    public void CollectFontVariants_NoFontAssetsAndNoFamily_YieldNothing()
    {
        // 无字体资源 + 控件未指定 FontFamily → 默认字体片段为 null → 无变体可生成
        var project = Project(14, Label("", 22, "a"));
        var table = CResourceSymbols.Create(new List<AssetItem>(), new List<AssetItem>(), project);

        Assert.Empty(table.FontVariants);
    }

    [Fact]
    public void Create_PreservesAssetOrder_InImagesAndFonts()
    {
        // 顺序即产物顺序：Export 依赖 Fonts[0] 作为默认字体，
        // 也依赖 Images 顺序稳定，否则每次导出产物 diff 抖动。
        var table = CResourceSymbols.Create(
            new List<AssetItem> { Image("i_c"), Image("i_a"), Image("i_b") },
            new List<AssetItem> { Font("f_z"), Font("f_a") },
            Project());

        Assert.Equal(new[] { "i_c", "i_a", "i_b" }, table.Images.Select(i => i.Fragment));
        Assert.Equal(new[] { "f_z", "f_a" }, table.Fonts.Select(f => f.Fragment));
    }
}
