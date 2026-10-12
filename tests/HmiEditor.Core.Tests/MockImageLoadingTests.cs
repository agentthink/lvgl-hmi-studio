using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Mock 项目图片加载边界测试 —— 覆盖 SimulatorProjectExporter 的 generated_images.c
/// 生成、CodeGenerator 的 lv_image_set_src 调用、资产 ID 清理等行为。
/// </summary>
public class MockImageLoadingTests
{
    // ── 测试基建 ──

    private static string NewTempDir() => TestTempDir.NewDir("hmi-img-test");

    private static AssetItem MakeImageAsset(string id, string filePath, int w = 100, int h = 100)
        => new()
        {
            Id = id,
            Name = $"图片_{id}",
            Type = AssetType.Image,
            FilePath = filePath,
            SizeBytes = 1024,
            Width = w,
            Height = h,
            ColorDepth = 32,
        };

    private static (HmiProject project, AssetLibrary assets, string code) BuildWithImageControl(string assetId)
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "img_1",
            Name = "测试图片",
            Type = HmiControlType.Image,
            X = 0, Y = 0, Width = 200, Height = 150,
            ImageAssetId = assetId,
            StretchMode = "Fill",
        });
        project.Screens.Add(screen);

        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset(assetId, "/tmp/test.bmp"));
        assets.IncrementReference(assetId);

        var code = new CodeGenerator().GenerateSimulatorModule(project, assets);
        return (project, assets, code);
    }

    // ── 导出器：有图片资源 → 生成 generated_images.c ──

    [Fact]
    public void Export_WithImageAssets_GeneratesImagesCFile()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_a", "/tmp/a.bmp"));

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir, assets);
            Assert.Contains("generated_images.c", result.Files);
            Assert.True(File.Exists(Path.Combine(dir, "generated_images.c")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 导出器：无图片资源 → 不生成 generated_images.c ──

    [Fact]
    public void Export_WithoutImageAssets_NoImagesCFile()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null);
            Assert.DoesNotContain("generated_images.c", result.Files);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_images.c 内容：IMG_PATH 宏 ──

    [Fact]
    public void GeneratedImagesC_ContainsImgPathMacro()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_banner", "/home/user/banner.bmp"));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var imagesC = File.ReadAllText(Path.Combine(dir, "generated_images.c"));
            // 路径宏格式：A: SIM_ASSET_DIR/<文件名>（SIM_ASSET_DIR 由 CMake 编译期注入为 assets 绝对路径）
            Assert.Contains("#define IMG_PATH_img_banner", imagesC);
            Assert.Contains("\"A:\" SIM_ASSET_DIR \"/banner.bmp\"", imagesC);
            // 不应硬编码开发者本机绝对路径（工程迁移后失效）
            Assert.DoesNotContain("/home/user/banner.bmp", imagesC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── generated_images.c 内容：extern 声明 ──

    [Fact]
    public void GeneratedImagesC_ContainsExternImgDsc()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_logo", "/tmp/logo.bmp"));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var imagesC = File.ReadAllText(Path.Combine(dir, "generated_images.c"));
            Assert.Contains("extern const lv_img_dsc_t img_img_logo", imagesC);
            CSource.AssertCode(imagesC, "LV_IMG_DECLARE(img_img_logo)");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── Windows 路径反斜杠 → 正斜杠 ──

    [Fact]
    public void GeneratedImagesC_WindowsPath_ConvertedToUnix()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_win", "C:\\Users\\test\\image.bmp"));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var imagesC = File.ReadAllText(Path.Combine(dir, "generated_images.c"));
            // IMG_PATH 宏只取文件名（反斜杠路径天然被消除），由 SIM_ASSET_DIR 注入 assets 绝对路径
            Assert.Contains("#define IMG_PATH_img_win \"A:\" SIM_ASSET_DIR \"/image.bmp\"", imagesC);
            // 不应在路径宏定义行中包含反斜杠（注释中的原始路径可能保留，仅检查宏定义行）
            var macroLine = imagesC.Split('\n').First(l => l.Contains("#define IMG_PATH_img_win"));
            Assert.DoesNotContain("\\", macroLine.Replace("\\\"", ""));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── CodeGen：Image 控件生成 lv_image_set_src ──

    [Fact]
    public void CodeGen_ImageControl_GeneratesSetImageSrc()
    {
        var (_, _, code) = BuildWithImageControl("asset_img_001");
        CSource.AssertCode(code, "lv_image_set_src(obj_img_1, IMG_PATH_asset_img_001)");
    }

    // ── CodeGen：Image 控件拉伸模式 → 真实 inner_align 代码 ──

    [Fact]
    public void CodeGen_ImageControlWithStretchMode_GeneratesInnerAlign()
    {
        var (_, _, code) = BuildWithImageControl("asset_stretch");
        // Fill → LV_IMAGE_ALIGN_STRETCH（真实代码，非注释占位）
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img_1, LV_IMAGE_ALIGN_STRETCH)");
    }

    // ── CodeGen：屏幕背景图 → 真实 lv_image_create 代码 ──

    [Fact]
    public void CodeGen_ScreenBgImage_GeneratesRealImageCode()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.BgImageAssetId = "asset_bg";
        screen.BgImageMode = ScreenBgImageMode.Stretch;
        screen.BgImageOpacity = 80;
        project.Screens.Add(screen);

        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("asset_bg", "/tmp/bg.bmp", 1024, 600));

        var code = new CodeGenerator().GenerateSimulatorModule(project, assets);
        // 背景图资源在 AssetLibrary 中 → 生成真实创建代码（80% → 204/255）
        Assert.Contains("lv_image_create", code);
        CSource.AssertCode(code, "lv_image_set_src(bg_img, IMG_PATH_asset_bg)");
        CSource.AssertCode(code, "lv_image_set_inner_align(bg_img, LV_IMAGE_ALIGN_STRETCH)");
        CSource.AssertCode(code, "lv_obj_set_style_opa(bg_img, 204, 0)");
        CSource.AssertCode(code, "lv_obj_move_background(bg_img)");
    }

    // ── CodeGen：状态化图片（Pressed/Checked 态）──

    [Fact]
    public void CodeGen_StateImageAsset_GeneratesSetImageSrc()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 };
        var ctrl = new HmiControl
        {
            Id = "btn_state",
            Name = "状态按钮",
            Type = HmiControlType.Button,
            X = 10, Y = 10, Width = 100, Height = 40,
            ImageAssetId = "asset_normal",
        };
        ctrl.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Pressed,
            ImageAssetId = "asset_pressed",
        });
        screen.Controls.Add(ctrl);
        project.Screens.Add(screen);

        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("asset_normal", "/tmp/normal.bmp"));
        assets.AddAsset(MakeImageAsset("asset_pressed", "/tmp/pressed.bmp"));

        var code = new CodeGenerator().GenerateSimulatorModule(project, assets);
        CSource.AssertCode(code, "lv_image_set_src(obj_btn_state, IMG_PATH_asset_normal)");
        CSource.AssertCode(code, "lv_image_set_src(obj_btn_state, IMG_PATH_asset_pressed)");
    }

    // ── CMakeLists 包含 generated_images.c ──

    [Fact]
    public void Export_CmakeLists_IncludesImagesSource()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_a", "/tmp/a.bmp"));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var cmake = File.ReadAllText(Path.Combine(dir, "CMakeLists.txt"));
            Assert.Contains("generated_images.c", cmake);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 多图片资产全部生成声明 ──

    [Fact]
    public void GeneratedImagesC_MultipleAssets_AllDeclared()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 100, Height = 100 });
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img_a", "/tmp/a.bmp"));
        assets.AddAsset(MakeImageAsset("img_b", "/tmp/b.bmp"));
        assets.AddAsset(MakeImageAsset("img_c", "/tmp/c.bmp"));

        var dir = NewTempDir();
        try
        {
            SimulatorProjectExporter.Export(project, code, dir, assets);
            var imagesC = File.ReadAllText(Path.Combine(dir, "generated_images.c"));
            Assert.Contains("IMG_PATH_img_a", imagesC);
            Assert.Contains("IMG_PATH_img_b", imagesC);
            Assert.Contains("IMG_PATH_img_c", imagesC);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 资产 ID 含特殊字符 → 清理为合法 C 标识符 ──

    [Theory]
    [InlineData("asset-banner", "asset_banner")]
    [InlineData("asset.logo", "asset_logo")]
    [InlineData("asset logo", "asset_logo")]
    [InlineData("asset/logo", "asset_logo")]
    [InlineData("asset@logo", "asset_logo")]
    public void CodeGen_ImageAssetIdWithSpecialChars_Sanitized(string rawId, string expectedSanitized)
    {
        var (_, _, code) = BuildWithImageControl(rawId);
        Assert.Contains($"IMG_PATH_{expectedSanitized}", code);
        CSource.AssertCode(code, $"lv_image_set_src(obj_img_1, IMG_PATH_{expectedSanitized})");
    }

    // ── 空图片资产 ID → 使用 "unknown" 占位 ──

    [Fact]
    public void CodeGen_EmptyImageAssetId_UsesUnknownPlaceholder()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 200, Height = 100 };
        screen.Controls.Add(new HmiControl
        {
            Id = "img_empty",
            Name = "空ID图片",
            Type = HmiControlType.Image,
            X = 0, Y = 0, Width = 100, Height = 100,
            ImageAssetId = "",
        });
        project.Screens.Add(screen);

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        // 空 ImageAssetId 不应生成 lv_image_set_src（string.IsNullOrEmpty 判断）
        Assert.DoesNotContain("lv_image_set_src", code);
    }

    // ── 图片资产引用计数 ──

    [Fact]
    public void AssetLibrary_IncrementDecrementReference_WorksCorrectly()
    {
        var lib = new AssetLibrary();
        lib.AddAsset(MakeImageAsset("ref_test", "/tmp/ref.bmp"));
        Assert.Equal(0, lib.Assets[0].ReferenceCount);

        lib.IncrementReference("ref_test");
        lib.IncrementReference("ref_test");
        Assert.Equal(2, lib.Assets[0].ReferenceCount);

        lib.DecrementReference("ref_test");
        Assert.Equal(1, lib.Assets[0].ReferenceCount);

        lib.DecrementReference("ref_test");
        Assert.Equal(0, lib.Assets[0].ReferenceCount);

        // 不会变成负数
        lib.DecrementReference("ref_test");
        Assert.Equal(0, lib.Assets[0].ReferenceCount);
    }
}
