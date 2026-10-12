using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Mock 项目字体加载边界测试 —— 覆盖 FreeType 运行时字体加载的代码生成：
/// generated_fonts.c、lv_conf.h、main.c、CMakeLists.txt 的字体相关配置。
/// </summary>
public class MockFontLoadingTests
{
    // ── 测试基建 ──

    private static string NewTempDir() => TestTempDir.NewDir("hmi-font-test-");

    private static AssetItem MakeFontAsset(string id, string filePath, int pxSize = 20)
        => new()
        {
            Id = id,
            Name = $"字体_{id}",
            Type = AssetType.Font,
            FilePath = filePath,
            SizeBytes = 1024 * 1024,
            Width = 0,
            Height = pxSize,
            ColorDepth = 0,
        };

    private static (HmiProject project, AssetLibrary assets, string code) BuildWithFontAsset(string fontId, string fontPath)
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "lbl_test", Name = "测试", Type = HmiControlType.Label, Text = "中文测试",
        });

        var assets = new AssetLibrary();
        assets.AddAsset(MakeFontAsset(fontId, fontPath));
        assets.IncrementReference(fontId);

        var code = new CodeGenerator().GenerateSimulatorModule(project, assets);
        return (project, assets, code);
    }

    // ── 导出器：有字体资源 → 生成 generated_fonts.c ──

    [Fact]
    public void Export_WithFontAssets_GeneratesFontsCFile()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/NotoSansCJK.ttc");

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir, assets);
            Assert.Contains("generated_fonts.c", result.Files);
            Assert.True(File.Exists(Path.Combine(dir, "generated_fonts.c")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 导出器：无字体资源 → 不生成 generated_fonts.c ──

    [Fact]
    public void Export_WithoutFontAssets_NoFontsCFile()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null);
            Assert.DoesNotContain("generated_fonts.c", result.Files);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_fonts.c 包含 lv_freetype_font_create ──

    [Fact]
    public void GeneratedFontsC_ContainsFreeTypeCreate()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/NotoSansCJK.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains("lv_freetype_font_create", fontsC);
            Assert.Contains("LV_FREETYPE_FONT_RENDER_MODE_BITMAP", fontsC);
            Assert.Contains("LV_FREETYPE_FONT_STYLE_NORMAL", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_fonts.c 使用真实 OS 路径（无 "A:" 前缀）──

    [Fact]
    public void GeneratedFontsC_UsesRealOsPath_NoALetterPrefix()
    {
        var fontPath = "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc";
        var (project, assets, code) = BuildWithFontAsset("font_cjk", fontPath);

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            // FONT_PATH 宏只取文件名，由 SIM_ASSET_DIR 注入 assets 绝对路径（无 A: 前缀，FreeType 直接访问文件系统）
            Assert.Contains("#define FONT_PATH_font_cjk SIM_ASSET_DIR \"/NotoSansCJK-Regular.ttc\"", fontsC);
            // 不应包含 "A:/usr"（字体不走 LV_FS_STDIO，无 A: 盘符前缀）
            Assert.DoesNotContain("A:/usr", fontsC);
            // 不应硬编码开发者本机绝对路径（工程迁移后失效）
            Assert.DoesNotContain("/usr/share/fonts", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_fonts.c 包含 init_font_assets 函数 ──

    [Fact]
    public void GeneratedFontsC_ContainsInitFunction()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            CSource.AssertCode(fontsC, "void init_font_assets(void)");
            Assert.Contains("g_font_assets_initialized", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_fonts.c 包含全局 lv_font_t 变量 ──

    [Fact]
    public void GeneratedFontsC_ContainsGlobalFontVariable()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains("lv_font_t font_font_cjk;", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── lv_conf.h 有字体资源 → 启用 LV_USE_FREETYPE ──

    [Fact]
    public void LvConf_WithFontAssets_EnablesFreeType()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var conf = File.ReadAllText(Path.Combine(dir, "lv_conf.h"));
            Assert.Contains("#define LV_USE_FREETYPE 1", conf);
            Assert.Contains("#define LV_USE_TEXT_UTF8 1", conf);
            Assert.Contains("LV_FREETYPE_CACHE_SIZE", conf);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── lv_conf.h 无字体资源 → 使用 SimSun CJK fallback ──

    [Fact]
    public void LvConf_WithoutFontAssets_UsesSimSunCjkFallback()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null);
            var conf = File.ReadAllText(Path.Combine(dir, "lv_conf.h"));
            Assert.Contains("LV_FONT_SIMSUN_16_CJK", conf);
            Assert.Contains("LV_FONT_DEFAULT", conf);
            Assert.Contains("#define LV_USE_TEXT_UTF8 1", conf);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── main.c 有字体资源 → 调用 init_font_assets ──

    [Fact]
    public void MainC_WithFontAssets_CallsInitFontAssets()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));
            Assert.Contains("init_font_assets", mainC);
            Assert.Contains("load_project_font", mainC);
            Assert.Contains("extern lv_font_t font_font_cjk", mainC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── main.c 无字体资源 → 无字体加载调用 ──

    [Fact]
    public void MainC_WithoutFontAssets_NoFontLoadingCode()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null);
            var mainC = File.ReadAllText(Path.Combine(dir, "main.c"));
            Assert.DoesNotContain("init_font_assets", mainC);
            Assert.DoesNotContain("load_project_font", mainC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── CMakeLists 有字体资源 → 链接 FreeType ──

    [Fact]
    public void CmakeLists_WithFontAssets_LinksFreeType()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var cmake = File.ReadAllText(Path.Combine(dir, "CMakeLists.txt"));
            Assert.Contains("freetype2", cmake);
            Assert.Contains("FREETYPE", cmake);
            Assert.Contains("generated_fonts.c", cmake);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── CodeGen 主题代码：有字体资源 → 设置 style_text_font ──

    [Fact]
    public void CodeGen_ThemeWithFontAsset_SetsStyleTextFont()
    {
        var (project, assets, code) = BuildWithFontAsset("font_cjk", "/tmp/font.ttc");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(scr, &font_font_cjk, 0)");
    }

    // ── CodeGen 主题代码：无字体资源 → 回退嵌入 CJK 子集（而非 LV_FONT_DEFAULT=Montserrat，无中文字形）──

    [Fact]
    public void CodeGen_ThemeWithoutFontAsset_FallsBackToEmbeddedCjk()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        // 模拟器模式：无字体资产回退 SimSun CJK 子集（保证中文不显示方框）
        CSource.AssertCode(code, "lv_obj_set_style_text_font(scr, &lv_font_simsun_14_cjk, 0)");
    }

    // ── 字体资产 ID 含特殊字符 → 清理 ──

    [Theory]
    [InlineData("font-cjk", "font_cjk")]
    [InlineData("font.cjk", "font_cjk")]
    [InlineData("font cjk", "font_cjk")]
    public void GeneratedFontsC_SpecialCharId_Sanitized(string rawId, string expected)
    {
        var (project, assets, code) = BuildWithFontAsset(rawId, "/tmp/font.ttc");

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains($"FONT_PATH_{expected}", fontsC);
            Assert.Contains($"lv_font_t font_{expected};", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 字体像素大小从 AssetItem.Height 读取 ──

    [Theory]
    [InlineData(14)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void GeneratedFontsC_PixelSize_ReadsFromAssetHeight(int pxSize)
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var assets = new AssetLibrary();
        assets.AddAsset(MakeFontAsset("font_sz", "/tmp/font.ttc", pxSize));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains($"#define FONT_PXSIZE_font_sz {pxSize}", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 字体 Height 为 0 → 默认 20px ──

    [Fact]
    public void GeneratedFontsC_ZeroHeight_DefaultsTo20px()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var assets = new AssetLibrary();
        assets.AddAsset(new AssetItem
        {
            Id = "font_default",
            Name = "默认字号字体",
            Type = AssetType.Font,
            FilePath = "/tmp/font.ttc",
            Height = 0,
        });

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains("#define FONT_PXSIZE_font_default 20", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 多字体资产全部生成 ──

    [Fact]
    public void GeneratedFontsC_MultipleFonts_AllGenerated()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var assets = new AssetLibrary();
        assets.AddAsset(MakeFontAsset("font_regular", "/tmp/regular.ttc", 16));
        assets.AddAsset(MakeFontAsset("font_bold", "/tmp/bold.ttc", 24));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var fontsC = File.ReadAllText(Path.Combine(dir, "generated_fonts.c"));
            Assert.Contains("FONT_PATH_font_regular", fontsC);
            Assert.Contains("FONT_PATH_font_bold", fontsC);
            Assert.Contains("lv_font_t font_font_regular;", fontsC);
            Assert.Contains("lv_font_t font_font_bold;", fontsC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }
}
