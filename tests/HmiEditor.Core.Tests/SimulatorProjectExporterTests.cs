using System;
using System.Collections.Generic;
using System.IO;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="SimulatorProjectExporter"/> 的行为测试 —— 专门补齐既有测试
/// （<c>SimulatorLoopTests</c> / <c>MockFontLoadingTests</c> / <c>MockImageLoadingTests</c> /
/// <c>MockProjectEndToEndTests</c>）**没有覆盖**的那部分契约。
///
/// <para>既有测试已经覆盖了各产物的"有没有某段文本"，本类补的是它们漏掉的行为面：</para>
/// <list type="bullet">
///   <item><b>启动屏选择与重排</b>：<c>Export</c> 里唯一的业务分支 —— 四级回退
///     （<c>BootScreenId</c> → 首个非图层屏 → 首个屏 → 兜底 1024×600）与
///     <c>screenFunctions</c> 重排，此前**零覆盖**。</item>
///   <item><b><c>generated_comm.h</c> 的内容</b>：既有测试只断言了文件存在。</item>
///   <item><b><c>tags</c> 路径</b>：绑定刷新钩子 <c>sim_refresh_bindings</c> 的有/无分支。</item>
///   <item><b>参数校验与产物清单契约</b>：Null 参数、错误消息、<c>Files</c> 的精确顺序。</item>
///   <item><b>失败不阻断与幂等</b>：资产文件缺失、重复导出到同一目录。</item>
/// </list>
///
/// <para>这些是抽 IR 统一发射器（<c>docs/2026-09-14-optimization-directions.md</c> 的 P2）
/// 之前必须先钉住的行为契约 —— 与 <c>DesignCanvas</c> 拆分类同一纪律：先有行为网，再动刀。</para>
/// </summary>
public class SimulatorProjectExporterTests
{
    // ══════════════════════ 测试基建 ══════════════════════

    private static HmiProject BuildProject(params (string Id, string Name, int Width, int Height)[] screens)
    {
        var project = new HmiProject();
        foreach (var (id, name, width, height) in screens)
            project.Screens.Add(new HmiScreen { Id = id, Name = name, Width = width, Height = height });
        return project;
    }

    private static string Generate(HmiProject project, AssetLibrary? assets = null)
        => new CodeGenerator().GenerateSimulatorModule(project, assets);

    /// <summary>导出到独立临时目录、跑断言、随后清理 —— 保证每个用例不留残留。</summary>
    private static void WithExport(
        HmiProject project,
        Action<SimulatorExportResult, string> assert,
        string? code = null,
        AssetLibrary? assets = null,
        IReadOnlyList<RuntimeTag>? tags = null,
        IReadOnlyList<DeviceConnection>? connections = null)
    {
        var dir = TestTempDir.NewDir("sim-exporter");
        try
        {
            var result = SimulatorProjectExporter.Export(
                project, code ?? Generate(project, assets), dir, assets, tags, connections);
            assert(result, dir);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    private static string ReadFile(string dir, string name)
        => File.ReadAllText(Path.Combine(dir, name));

    private static AssetItem MakeImageAsset(string id, string filePath = "/nonexistent/placeholder.bmp")
        => new()
        {
            Id = id,
            Name = $"图片_{id}",
            Type = AssetType.Image,
            FilePath = filePath,
            Width = 100,
            Height = 100,
            ColorDepth = 32,
        };

    private static AssetItem MakeFontAsset(string id, string filePath = "/nonexistent/placeholder.ttf")
        => new()
        {
            Id = id,
            Name = $"字体_{id}",
            Type = AssetType.Font,
            FilePath = filePath,
            Height = 20,
        };

    // ══════════════════════ A. ExtractScreenFunctions ══════════════════════

    [Fact]
    public void ExtractScreenFunctions_CodeWithoutFactory_ReturnsEmpty()
    {
        Assert.Empty(SimulatorProjectExporter.ExtractScreenFunctions(string.Empty));
        Assert.Empty(SimulatorProjectExporter.ExtractScreenFunctions("int main(void) { return 0; }"));
    }

    [Fact]
    public void ExtractScreenFunctions_DeduplicatesPrototypeAndDefinition()
    {
        // 生成代码里工厂既有前置声明又有定义：注册表只能出现一次，否则 screen_factories[]
        // 会重复登记，SCREEN_COUNT 与实际屏幕数不符（切屏下标错位）。
        const string code = """
            static lv_obj_t* create_screen_a(void);
            lv_obj_t* create_screen_a(void) { return NULL; }
            static lv_obj_t* create_screen_b(void) { return NULL; }
            """;

        var functions = SimulatorProjectExporter.ExtractScreenFunctions(code);

        Assert.Equal(new[] { "create_screen_a", "create_screen_b" }, functions);
    }

    [Fact]
    public void ExtractScreenFunctions_DeclarationWithParameters_IsNotAFactory()
    {
        // 工厂签名固定为 (void)：带参数的声明不是屏幕工厂，收进注册表会让生成的
        // screen_factories[] 调用一个签名不符的函数（C 编译期报错）。
        const string code = "lv_obj_t* create_screen_x(int parent) { return NULL; }";

        Assert.Empty(SimulatorProjectExporter.ExtractScreenFunctions(code));
    }

    // ══════════════════════ B. 启动屏选择与重排 ══════════════════════

    [Fact]
    public void Export_BootScreenId_DrivesSimConfigResolution()
    {
        var project = BuildProject(("s1", "主画面", 800, 480), ("s2", "设置页", 1024, 600));
        project.RuntimeConfig.BootScreenId = "s2";

        WithExport(project, (result, dir) =>
        {
            var config = ReadFile(dir, "sim_config.h");
            CSource.AssertText(config, "SIM_WINDOW_WIDTH 1024");
            CSource.AssertText(config, "SIM_WINDOW_HEIGHT 600");
        });
    }

    [Fact]
    public void Export_BootScreenId_MovesItsFactoryToTheFront()
    {
        var project = BuildProject(("s1", "主画面", 800, 480), ("s2", "设置页", 1024, 600));
        project.RuntimeConfig.BootScreenId = "s2";

        WithExport(project, (result, dir) =>
        {
            // screens[0] 即启动屏（main.c 用 lv_screen_load(screens[0]) 加载它）
            Assert.Equal("create_screen_s2", result.ScreenFunctions[0]);
            Assert.Equal(new[] { "create_screen_s2", "create_screen_s1" }, result.ScreenFunctions);
            CSource.AssertCode(ReadFile(dir, "main.c"), "lv_screen_load(screens[0])");
        });
    }

    [Fact]
    public void Export_BootScreenId_KeepsFactoryAndNameArraysInTheSameOrder()
    {
        var project = BuildProject(("s1", "主画面", 800, 480), ("s2", "设置页", 1024, 600));
        project.RuntimeConfig.BootScreenId = "s2";

        WithExport(project, (result, dir) =>
        {
            var mainC = ReadFile(dir, "main.c");

            var bootFactory = mainC.IndexOf("create_screen_s2,", StringComparison.Ordinal);
            var otherFactory = mainC.IndexOf("create_screen_s1,", StringComparison.Ordinal);
            Assert.True(bootFactory >= 0 && otherFactory > bootFactory,
                "启动屏工厂应排在 screen_factories[] 首位");

            // screen_names[] 与 screen_factories[] 同序，且用屏幕 Name（不是 Id）——
            // 运行时 hmi_screen_switch 是**按名字**查表的，两个数组错序就会切错屏。
            var bootName = mainC.IndexOf("\"设置页\",", StringComparison.Ordinal);
            var otherName = mainC.IndexOf("\"主画面\",", StringComparison.Ordinal);
            Assert.True(bootName >= 0 && otherName > bootName,
                "屏幕名应跟随工厂顺序，启动屏在首位");
        });
    }

    [Fact]
    public void Export_WithoutBootScreenId_SkipsScreensReferencedAsLayers()
    {
        // L 被 s1 引用为背景图层 → 不作为独立屏幕。启动屏应落到 s2（首个非图层屏），
        // 而不是项目里的第一个屏幕 L。尺寸取得互相不同，断言才有区分度。
        var project = BuildProject(
            ("L", "公共底板", 1111, 222),
            ("s2", "设置页", 1024, 600),
            ("s1", "主画面", 800, 480));
        project.Screens[0].IsPublicScreen = true;
        project.Screens[2].ReferencedLayerIds.Add("L");

        WithExport(project, (result, dir) =>
        {
            CSource.AssertText(ReadFile(dir, "sim_config.h"), "SIM_WINDOW_WIDTH 1024");
            Assert.Equal("create_screen_s2", result.ScreenFunctions[0]);
        });
    }

    [Fact]
    public void Export_AllScreensReferencedAsLayers_FallsBackToTheFirstScreen()
    {
        // 互相引用 → 每个屏幕都是别人的图层 → 第二级（首个非图层屏）落空，落到第三级 Screens[0]。
        // 生成代码用手写而非 CodeGenerator：真实生成链在这种情况下不会产出任何工厂
        // （图层屏不注册），会在更早一步就抛错；这里要单独验证的是 Export 自身的回退顺序。
        var project = BuildProject(("s1", "主画面", 800, 480), ("s2", "设置页", 1024, 600));
        project.Screens[0].ReferencedLayerIds.Add("s2");
        project.Screens[1].ReferencedLayerIds.Add("s1");
        const string code = """
            lv_obj_t* create_screen_s1(void) { return NULL; }
            lv_obj_t* create_screen_s2(void) { return NULL; }
            """;

        WithExport(project, (result, dir) =>
            CSource.AssertText(ReadFile(dir, "sim_config.h"), "SIM_WINDOW_WIDTH 800"), code: code);
    }

    [Fact]
    public void Export_CodeWithoutAnyProjectScreen_FallsBackToDefaultResolution()
    {
        // 项目里没有屏幕、生成代码里却有工厂（导入/手写代码场景）：第四级兜底 1024×600。
        // 这条同时钉住「兜底屏幕的 Id 与工厂名不匹配时不做重排」的现状。
        var project = new HmiProject();
        const string code = "lv_obj_t* create_screen_ghost(void) { return NULL; }";

        WithExport(project, (result, dir) =>
        {
            var config = ReadFile(dir, "sim_config.h");
            CSource.AssertText(config, "SIM_WINDOW_WIDTH 1024");
            CSource.AssertText(config, "SIM_WINDOW_HEIGHT 600");
            Assert.Equal(new[] { "create_screen_ghost" }, result.ScreenFunctions);
        }, code: code);
    }

    // ══════════════════════ C. 参数校验与错误路径 ══════════════════════

    [Fact]
    public void Export_NullArguments_ThrowArgumentNullException()
    {
        var dir = TestTempDir.NewDir("sim-exporter-null");
        try
        {
            const string code = "lv_obj_t* create_screen_a(void) { return NULL; }";

            Assert.Throws<ArgumentNullException>(() =>
                SimulatorProjectExporter.Export(null!, code, dir));
            Assert.Throws<ArgumentNullException>(() =>
                SimulatorProjectExporter.Export(new HmiProject(), null!, dir));
            Assert.Throws<ArgumentNullException>(() =>
                SimulatorProjectExporter.Export(new HmiProject(), code, null!));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void Export_NoScreenFactory_ThrowsInvalidOperationWithActionableMessage()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));
        var dir = TestTempDir.NewDir("sim-exporter-nofactory");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                SimulatorProjectExporter.Export(project, "int main(void) { return 0; }", dir));

            // 报错要能直接指向原因（生成代码没产出工厂函数），而不是一句裸异常
            Assert.Contains("create_screen", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════════ D. 产物清单契约 ══════════════════════

    [Fact]
    public void Export_MinimalProject_ProducesTheExactFileSetInOrder()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));

        WithExport(project, (result, dir) =>
        {
            // 顺序是调用方（SimulatorBuildService / UI 列表）依赖的契约
            // generated_security.h 必须与生成代码同批产出：动作门引用的 HMI_PERM_* 来自它
            // generated_runtime.h 同理：设备端 runtime_config.h/hmi_runtime.h 引用它的宏
            Assert.Equal(
                new[]
                {
                    "main.c", "sim_config.h", "CMakeLists.txt", "lv_conf.h",
                    "generated_hmi.c", "generated_comm.h", "generated_security.h",
                    "generated_runtime.h", "README.md",
                },
                result.Files);

            foreach (var file in result.Files)
                Assert.True(File.Exists(Path.Combine(dir, file)), $"报告了但未落盘的产物: {file}");
        });
    }

    [Fact]
    public void Export_WithImageAndFontAssets_AddsBothGeneratedSourcesInOrder()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img1"));
        assets.AddAsset(MakeFontAsset("fnt1"));

        WithExport(project, (result, dir) =>
        {
            Assert.Equal(
                new[]
                {
                    "main.c", "generated_images.c", "generated_fonts.c", "sim_config.h",
                    "CMakeLists.txt", "lv_conf.h", "generated_hmi.c", "generated_comm.h",
                    "generated_security.h", "generated_runtime.h", "README.md",
                },
                result.Files);

            foreach (var file in result.Files)
                Assert.True(File.Exists(Path.Combine(dir, file)), $"报告了但未落盘的产物: {file}");
        }, assets: assets);
    }

    [Fact]
    public void Export_GeneratedHmiC_IsByteIdenticalToTheGeneratedCode()
    {
        // 导出器不得"顺手美化"生成代码：设备端与模拟器必须编译同一份 generated_hmi.c
        var project = BuildProject(("s1", "主画面", 800, 480));
        var code = Generate(project);

        WithExport(project, (result, dir) =>
            Assert.Equal(code, ReadFile(dir, "generated_hmi.c")), code: code);
    }

    // ══════════════════════ E. generated_comm.h 内容 ══════════════════════

    [Fact]
    public void Export_WithoutConnections_StillWritesASelfContainedCommHeader()
    {
        // 设备端 comm_core.c 用 __has_include 引用该头：无连接时也必须存在且自洽
        // （guard 成对、无任何协议宏），否则设备端构建会因缺头或不完整的条件编译而断。
        var project = BuildProject(("s1", "主画面", 800, 480));

        WithExport(project, (result, dir) =>
        {
            var comm = ReadFile(dir, "generated_comm.h");
            CSource.AssertText(comm, "#ifndef HMI_GENERATED_COMM_H");
            CSource.AssertText(comm, "#endif /* HMI_GENERATED_COMM_H */");
            CSource.AssertNoText(comm, "HMI_COMM_RTU_DEV");
            CSource.AssertNoText(comm, "HMI_COMM_TCP_HOST");
            CSource.AssertNoText(comm, "HMI_COMM_MQTT_HOST");
        });
    }

    [Fact]
    public void Export_WithEnabledRtuConnection_CommHeaderCarriesItsMacros()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));
        var rtu = new DeviceConnection { DriverId = "modbus-rtu" };
        rtu.Config["port"] = "COM3";
        rtu.Config["baud"] = "9600";
        rtu.Config["parity"] = "None";
        rtu.Config["timeoutMs"] = "300";

        WithExport(project, (result, dir) =>
        {
            var comm = ReadFile(dir, "generated_comm.h");
            CSource.AssertText(comm, "#define HMI_COMM_RTU_DEV \"/dev/ttyS3\"");
            CSource.AssertText(comm, "#define HMI_COMM_RTU_BAUD 9600");
            CSource.AssertText(comm, "#define HMI_COMM_RTU_PARITY 'N'");
            CSource.AssertText(comm, "#define HMI_COMM_RTU_TMO_MS 300");
        }, connections: new[] { rtu });
    }

    // ══════════════════════ F. tags 路径（绑定模拟刷新） ══════════════════════

    [Fact]
    public void Export_WithRuntimeTags_DeclaresAndPollsBindingRefresh()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));
        var tags = new List<RuntimeTag>
        {
            new() { VariableId = "v1", Address = "40001", RawAddress = "40001", Scale = 1, Offset = 0 },
        };

        WithExport(project, (result, dir) =>
        {
            var mainC = ReadFile(dir, "main.c");
            // 声明（否则主循环里的调用是隐式声明，C99 之后是错误）
            CSource.AssertText(mainC, "extern void sim_refresh_bindings(uint32_t tick);");
            // 主循环每帧轮询
            CSource.AssertCode(mainC, "sim_refresh_bindings(sim_tick)");
        }, tags: tags);
    }

    [Fact]
    public void Export_WithoutRuntimeTags_OmitsBindingRefresh()
    {
        // 无标签表时不得残留 extern 声明或调用：generated_hmi.c 里没有该函数实现，
        // 链接期会报未定义符号。
        var project = BuildProject(("s1", "主画面", 800, 480));

        WithExport(project, (result, dir) =>
            CSource.AssertNoText(ReadFile(dir, "main.c"), "sim_refresh_bindings"));
    }

    // ══════════════════════ G. 重复导出（幂等） ══════════════════════

    [Fact]
    public void Export_TwiceIntoSameDirectory_OverwritesWithoutError()
    {
        // UI 的「重新生成并导出」会复用同一目录：第二次导出必须能覆盖，
        // 且结果与首次一致（不能追加、不能因文件已存在而失败）。
        var project = BuildProject(("s1", "主画面", 800, 480));
        var code = Generate(project);
        var dir = TestTempDir.NewDir("sim-exporter-twice");
        try
        {
            var first = SimulatorProjectExporter.Export(project, code, dir);
            var second = SimulatorProjectExporter.Export(project, code, dir);

            Assert.Equal(first.Files, second.Files);
            Assert.Equal(code, File.ReadAllText(Path.Combine(dir, "generated_hmi.c")));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    // ══════════════════════ H. 资产复制：失败不阻断 ══════════════════════

    [Fact]
    public void Export_MissingAssetFile_DoesNotBlockExport()
    {
        // 资产文件可能已被用户移动/删除，而 assetLibrary 里仍留有记录。
        // 导出必须照常产出 generated_images.c（用路径宏引用），不能整体失败。
        var project = BuildProject(("s1", "主画面", 800, 480));
        var assets = new AssetLibrary();
        assets.AddAsset(MakeImageAsset("img1"));

        WithExport(project, (result, dir) =>
        {
            Assert.Contains("generated_images.c", result.Files);
            Assert.True(File.Exists(Path.Combine(dir, "generated_images.c")));
        }, assets: assets);
    }

    [Fact]
    public void Export_ExistingAssetFile_IsCopiedIntoAssetsFolder()
    {
        var project = BuildProject(("s1", "主画面", 800, 480));
        var sourceDir = TestTempDir.NewDir("sim-exporter-src");
        try
        {
            var sourceFile = Path.Combine(sourceDir, "logo.bmp");
            var content = new byte[] { 0x42, 0x4D, 0x01, 0x02 };
            File.WriteAllBytes(sourceFile, content);

            var assets = new AssetLibrary();
            assets.AddAsset(MakeImageAsset("img1", sourceFile));

            WithExport(project, (result, dir) =>
            {
                var copied = Path.Combine(dir, "assets", "logo.bmp");
                Assert.True(File.Exists(copied), "资源文件应复制到导出目录的 assets/ 下");
                Assert.Equal(content, File.ReadAllBytes(copied));
            }, assets: assets);
        }
        finally
        {
            TestTempDir.DeleteDir(sourceDir);
        }
    }
}
