using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Mock 项目端到端集成测试 —— 模拟 MockHmiGen 的完整流程：
/// 构造含中文 + 图片 + 字体的 HmiProject → 生成 C 代码 → 导出模拟器工程 →
/// 打包/解包 .hmi → 断言各数据域完整性。
/// </summary>
public class MockProjectEndToEndTests
{
    // ── 测试基建 ──

    private static string NewTempDir() => TestTempDir.NewDir("hmi-e2e-mock-");

    /// <summary>构造与 MockHmiGen 等价的完整 mock 工程（2 屏幕 + 中文控件 + 图片 + 字体）。</summary>
    private static (HmiProject project, AssetLibrary assets) BuildMockProject(string assetDir)
    {
        Directory.CreateDirectory(assetDir);

        // 生成占位 BMP 文件（真实可读 1x1 像素）
        var bmpPath = Path.Combine(assetDir, "test_banner.bmp");
        File.WriteAllBytes(bmpPath, new byte[] { 0x42, 0x4D, 0x36, 0, 0, 0, 0, 0, 0, 0, 0x36, 0, 0, 0 });

        var fontPath = Path.Combine(assetDir, "test_font.ttc");
        File.WriteAllBytes(fontPath, new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 });

        var project = new HmiProject
        {
            Name = "Mock中文显示测试工程",
            LvglVersion = "9.5",
            Description = "用于验证模拟器中文与复杂图片显示修复效果的 mock 工程",
        };
        project.ScreenDefaultsConfig.DefaultWidth = 1024;
        project.ScreenDefaultsConfig.DefaultHeight = 600;

        // 主屏幕
        var screenMain = new HmiScreen
        {
            Id = "screen001",
            Name = "主控面板 · Main",
            Width = 1024, Height = 600,
            BgColor = "#0f172a",
        };

        // 各类中文控件
        screenMain.Controls.Add(new HmiControl
        {
            Id = "lbl_title", Name = "主标题", Type = HmiControlType.Label,
            X = 40, Y = 20, Width = 600, Height = 60,
            Text = "智慧工厂中央控制屏", TextColor = "#ffffff",
        });
        screenMain.Controls.Add(new HmiControl
        {
            Id = "lbl_data", Name = "数据指示", Type = HmiControlType.Label,
            X = 40, Y = 150, Width = 460, Height = 30,
            Text = "温度：27.5 ℃ · 压力：0.85 MPa", TextColor = "#f97316",
        });
        screenMain.Controls.Add(new HmiControl
        {
            Id = "lbl_rare", Name = "罕用字测试", Type = HmiControlType.Label,
            X = 40, Y = 470, Width = 460, Height = 30,
            Text = "CJK字形测试：壹贰叁肆伍 · 龍鳳龘靐 · 你好世界！", TextColor = "#facc15",
        });
        screenMain.Controls.Add(new HmiControl
        {
            Id = "btn_start", Name = "启动按钮", Type = HmiControlType.Button,
            X = 40, Y = 530, Width = 150, Height = 50,
            Text = "启动生产线", BgColor = "#16a34a", TextColor = "#ffffff",
        });
        screenMain.Controls.Add(new HmiControl
        {
            Id = "img_banner", Name = "顶部Banner图", Type = HmiControlType.Image,
            X = 0, Y = 0, Width = 1024, Height = 120,
            ImageAssetId = "asset_banner", StretchMode = "Fill",
        });

        project.Screens.Add(screenMain);

        // 第二屏（繁体 + 符号）
        var screen2 = new HmiScreen
        {
            Id = "screen002",
            Name = "参数设置 · 第二屏",
            Width = 1024, Height = 600,
            BgColor = "#1e293b",
        };
        screen2.Controls.Add(new HmiControl
        {
            Id = "lbl_s2_title", Name = "S2标题", Type = HmiControlType.Label,
            X = 40, Y = 40, Width = 900, Height = 50,
            Text = "参数设置页 · 繁體中文測試 · αβγ · ΣΠΩ · ≠≤≥∞",
            TextColor = "#fde68a",
        });
        project.Screens.Add(screen2);

        // 资产库
        var assets = new AssetLibrary();
        assets.AddAsset(new AssetItem
        {
            Id = "asset_banner",
            Name = "顶部Banner(中文渐变)",
            Type = AssetType.Image,
            FilePath = bmpPath,
            SizeBytes = new FileInfo(bmpPath).Length,
            Width = 800, Height = 200, ColorDepth = 32,
        });
        assets.IncrementReference("asset_banner");

        assets.AddAsset(new AssetItem
        {
            Id = "asset_font_cjk",
            Name = "Noto Sans CJK 全字形中文字体",
            Type = AssetType.Font,
            FilePath = fontPath,
            SizeBytes = new FileInfo(fontPath).Length,
            Height = 20,
        });
        assets.IncrementReference("asset_font_cjk");

        return (project, assets);
    }

    // ── 完整循环：生成代码 + 导出工程 ──

    [Fact]
    public void MockProject_FullLoop_GeneratesAndExports()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            // 生成 C 代码
            var code = new CodeGenerator().GenerateSimulatorModule(project, assets);
            Assert.Contains("智慧工厂中央控制屏", code);
            Assert.Contains("龘靐", code);
            Assert.Contains("繁體中文測試", code);

            // 导出模拟器工程
            var exportDir = Path.Combine(tempRoot, "export");
            var result = SimulatorProjectExporter.Export(project, code, exportDir, assets);

            // 验证所有文件生成
            Assert.Contains("main.c", result.Files);
            Assert.Contains("generated_hmi.c", result.Files);
            Assert.Contains("generated_images.c", result.Files);
            Assert.Contains("generated_fonts.c", result.Files);
            Assert.Contains("lv_conf.h", result.Files);
            Assert.Contains("CMakeLists.txt", result.Files);
            Assert.Equal(2, result.ScreenFunctions.Count);

            // 验证 main.c 软键盘服务完整生成（点击输入框弹内置键盘）
            var mainC = File.ReadAllText(Path.Combine(exportDir, "main.c"));
            CSource.AssertCode(mainC, "sim_kb_attach(screens[i])");
            CSource.AssertCode(mainC, "lv_keyboard_create(lv_layer_top())");
            Assert.Contains("LV_EVENT_FOCUSED", mainC);
            Assert.Contains("LV_EVENT_DEFOCUSED", mainC);
            // LVGL 9.x 无 lv_obj_set_click_focusable，改用 LV_OBJ_FLAG_CLICK_FOCUSABLE 标志
            CSource.AssertCode(mainC, "lv_obj_add_flag(obj, LV_OBJ_FLAG_CLICK_FOCUSABLE)");
            Assert.DoesNotContain("lv_obj_set_click_focusable", mainC);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 打包/解包往返：中文文本保持完整 ──

    [Fact]
    public void MockProject_PackUnpack_PreservesChineseText()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            var hmiPath = Path.Combine(tempRoot, "mock.hmi");
            using (var fs = File.Create(hmiPath))
            {
                HmiPackageService.Pack(fs, new HmiPackageContent(
                    project, assets, null,
                    item => File.Exists(item.FilePath) ? item.FilePath : null));
            }

            var unpackDir = Path.Combine(tempRoot, "unpack");
            HmiPackageResult result;
            using (var fs = File.OpenRead(hmiPath))
            {
                result = HmiPackageService.Unpack(fs, unpackDir);
            }

            // 中文文本完整
            var lbl = result.Project.Screens[0].Controls.First(c => c.Id == "lbl_title");
            Assert.Equal("智慧工厂中央控制屏", lbl.Text);

            var rare = result.Project.Screens[0].Controls.First(c => c.Id == "lbl_rare");
            Assert.Equal("CJK字形测试：壹贰叁肆伍 · 龍鳳龘靐 · 你好世界！", rare.Text);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 打包/解包往返：繁体字 + Unicode 符号保持完整 ──

    [Fact]
    public void MockProject_PackUnpack_PreservesTraditionalAndUnicode()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            var hmiPath = Path.Combine(tempRoot, "mock.hmi");
            using (var fs = File.Create(hmiPath))
            {
                HmiPackageService.Pack(fs, new HmiPackageContent(
                    project, assets, null,
                    item => File.Exists(item.FilePath) ? item.FilePath : null));
            }

            var unpackDir = Path.Combine(tempRoot, "unpack");
            HmiPackageResult result;
            using (var fs = File.OpenRead(hmiPath))
            {
                result = HmiPackageService.Unpack(fs, unpackDir);
            }

            var s2Title = result.Project.Screens[1].Controls.First(c => c.Id == "lbl_s2_title");
            Assert.Equal("参数设置页 · 繁體中文測試 · αβγ · ΣΠΩ · ≠≤≥∞", s2Title.Text);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 打包/解包往返：图片资产路径保持完整 ──

    [Fact]
    public void MockProject_PackUnpack_PreservesImageAssets()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            var hmiPath = Path.Combine(tempRoot, "mock.hmi");
            using (var fs = File.Create(hmiPath))
            {
                HmiPackageService.Pack(fs, new HmiPackageContent(
                    project, assets, null,
                    item => File.Exists(item.FilePath) ? item.FilePath : null));
            }

            var unpackDir = Path.Combine(tempRoot, "unpack");
            HmiPackageResult result;
            using (var fs = File.OpenRead(hmiPath))
            {
                result = HmiPackageService.Unpack(fs, unpackDir);
            }

            var imgAsset = result.AssetLibrary.Assets.FirstOrDefault(a => a.Type == AssetType.Image);
            Assert.NotNull(imgAsset);
            Assert.Equal("asset_banner", imgAsset.Id);
            Assert.True(File.Exists(imgAsset.FilePath), "图片文件应解包落盘");
            Assert.Equal(800, imgAsset.Width);
            Assert.Equal(200, imgAsset.Height);

            // 控件绑定保持
            var imgCtrl = result.Project.Screens[0].Controls.First(c => c.Id == "img_banner");
            Assert.Equal("asset_banner", imgCtrl.ImageAssetId);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 打包/解包往返：字体资产保持完整 ──

    [Fact]
    public void MockProject_PackUnpack_PreservesFontAsset()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            var hmiPath = Path.Combine(tempRoot, "mock.hmi");
            using (var fs = File.Create(hmiPath))
            {
                HmiPackageService.Pack(fs, new HmiPackageContent(
                    project, assets, null,
                    item => File.Exists(item.FilePath) ? item.FilePath : null));
            }

            var unpackDir = Path.Combine(tempRoot, "unpack");
            HmiPackageResult result;
            using (var fs = File.OpenRead(hmiPath))
            {
                result = HmiPackageService.Unpack(fs, unpackDir);
            }

            var fontAsset = result.AssetLibrary.Assets.FirstOrDefault(a => a.Type == AssetType.Font);
            Assert.NotNull(fontAsset);
            Assert.Equal("asset_font_cjk", fontAsset.Id);
            Assert.True(File.Exists(fontAsset.FilePath), "字体文件应解包落盘");
            Assert.Equal(20, fontAsset.Height);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 多屏全部导出 ──

    [Fact]
    public void MockProject_MultipleScreens_AllExported()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);
            var code = new CodeGenerator().GenerateSimulatorModule(project, assets);

            var exportDir = Path.Combine(tempRoot, "export");
            var result = SimulatorProjectExporter.Export(project, code, exportDir, assets);

            Assert.Contains("create_screen_screen001", result.ScreenFunctions);
            Assert.Contains("create_screen_screen002", result.ScreenFunctions);
            Assert.Equal(2, result.ScreenFunctions.Count);
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }

    // ── 空控件列表不崩溃 ──

    [Fact]
    public void MockProject_EmptyControls_NoCrash()
    {
        var project = new HmiProject { LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "空屏", Width = 100, Height = 100 });

        var code = new CodeGenerator().GenerateSimulatorModule(project);
        Assert.Contains("create_screen_s1", code);

        var dir = NewTempDir();
        try
        {
            var result = SimulatorProjectExporter.Export(project, code, dir, assetLibrary: null);
            Assert.Single(result.ScreenFunctions);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ── 导出后再次生成代码验证一致性 ──

    [Fact]
    public void MockProject_ExportThenRegenerate_CodeConsistent()
    {
        var tempRoot = NewTempDir();
        try
        {
            var assetDir = Path.Combine(tempRoot, "assets");
            var (project, assets) = BuildMockProject(assetDir);

            var code1 = new CodeGenerator().GenerateSimulatorModule(project, assets);

            var exportDir = Path.Combine(tempRoot, "export");
            SimulatorProjectExporter.Export(project, code1, exportDir, assets);

            // 从导出目录读取 generated_hmi.c 并验证关键内容
            var generatedHmi = File.ReadAllText(Path.Combine(exportDir, "generated_hmi.c"));
            Assert.Contains("智慧工厂中央控制屏", generatedHmi);
            Assert.Contains("龘靐", generatedHmi);
            CSource.AssertCode(generatedHmi, "lv_image_set_src(obj_img_banner, IMG_PATH_asset_banner)");
            CSource.AssertCode(generatedHmi, "lv_obj_set_style_text_font(scr, &font_asset_font_cjk, 0)");
        }
        finally
        {
            TestTempDir.DeleteDir(tempRoot);
        }
    }
}
