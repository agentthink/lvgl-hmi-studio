using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;

namespace HmiEditor.Core.Services;

/// <summary>模拟器工程导出结果。</summary>
public record SimulatorExportResult(
    string DirectoryPath,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ScreenFunctions);

/// <summary>
/// I11: LVGL PC 模拟器工程导出器 —— "设计→生成→板级验证"闭环。
///
/// 将生成代码一键打包为可编译的 LVGL v9 + SDL2 PC 工程:
///   main.c              — SDL 主循环 + 屏幕工厂注册 + 加载
///   CMakeLists.txt      — 查找 SDL2、add_subdirectory(LVGL)
///   lv_conf.h           — 基础配置(32bpp / SDL 后端 / 日志)
///   generated_hmi.c     — CodeGen 输出
///   generated_security.h — 权限模型下沉产物（动作门引用的 HMI_PERM_* 来自它）
///   generated_runtime.h — 运行时参数下沉产物（HMI_POLL_INTERVAL_MS / HMI_LOG_LEVEL）
///   README.md           — 依赖与构建说明
///
/// 用户在本机安装 SDL2 并克隆 lvgl v9.2 后即可编译运行,
/// 在 PC 上预览实际生成的 HMI 画面,形成验证闭环。
/// </summary>
public static partial class SimulatorProjectExporter
{
    private static readonly Regex ScreenFuncRegex =
        new(@"(?:static\s+)?lv_obj_t\*\s+(create_screen_[A-Za-z0-9_]+)\s*\(void\)", RegexOptions.Compiled);

    /// <summary>从生成代码提取 create_screen_* 函数名(按出现顺序)。</summary>
    public static IReadOnlyList<string> ExtractScreenFunctions(string generatedCode)
    {
        var result = new List<string>();
        foreach (Match m in ScreenFuncRegex.Matches(generatedCode))
        {
            if (!result.Contains(m.Groups[1].Value))
                result.Add(m.Groups[1].Value);
        }
        return result;
    }

    /// <summary>
    /// 导出模拟器工程到 outputDirectory。
    /// 屏幕数量为 0 时抛出 InvalidOperationException。
    /// </summary>
    /// <param name="tags">运行时标签表（绑定变量）。非空时模拟器主循环会模拟刷新绑定显示控件。</param>
    /// <param name="connections">通讯连接配置。非空且含启用连接时额外生成 generated_comm.h（设备端 comm_core 优先使用）。</param>
    public static SimulatorExportResult Export(
        HmiProject project, string generatedCode, string outputDirectory, AssetLibrary? assetLibrary = null,
        IReadOnlyList<RuntimeTag>? tags = null,
        IReadOnlyList<DeviceConnection>? connections = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(generatedCode);
        ArgumentNullException.ThrowIfNull(outputDirectory);

        var screenFunctions = ExtractScreenFunctions(generatedCode);
        if (screenFunctions.Count == 0)
            throw new InvalidOperationException("生成代码中未找到 create_screen 函数,无法导出模拟器工程");

        Directory.CreateDirectory(outputDirectory);

        // 启动屏幕：优先 RuntimeConfig.BootScreenId，回退为第一个非图层屏幕，最后兜底首个屏幕。
        // 四条都落空（工程一个屏幕都没有）时用 ScreenFactory 造一个占位屏幕 ——
        // 此处只用到它的 Id（下面的名字比对），尺寸取自工程默认模板而非写死的 1024×600。
        var referencedLayerIds = ScreenLayerRefs.CollectReferencedLayerIds(project);
        var bootScreen = project.Screens.FirstOrDefault(s => s.Id == project.RuntimeConfig.BootScreenId)
            ?? project.Screens.FirstOrDefault(s => !referencedLayerIds.Contains(s.Id))
            ?? project.Screens.FirstOrDefault()
            ?? ScreenFactory.Create(project);

        // 按 BootScreenId 重排 screenFunctions，确保 screens[0] 为启动屏幕。
        // 名字必须与生成侧同一个构造器 —— screenFunctions 是从生成代码里**正则提取**的
        // （已经是净化后的名字），这里若裸拼 `create_screen_{Id}`，id 含 `-`/空格时就对不上，
        // 表现为"启动屏重排静默失效"（screens[0] 不是启动屏）。
        var bootFuncName = CIdentifier.ScreenFactory(bootScreen.Id);
        var orderedFns = new List<string>(screenFunctions.Count);
        if (screenFunctions.Contains(bootFuncName))
        {
            orderedFns.Add(bootFuncName);
            orderedFns.AddRange(screenFunctions.Where(f => f != bootFuncName));
        }
        else
        {
            orderedFns.AddRange(screenFunctions);
        }
        screenFunctions = orderedFns;
        var files = new List<string>();

        // 检查资产库中是否有字体/图片资源
        var fontAssets = assetLibrary?.Assets.Where(a => a.Type == AssetType.Font).ToList() ?? new List<AssetItem>();
        var imageAssets = assetLibrary?.Assets.Where(a => a.Type == AssetType.Image).ToList() ?? new List<AssetItem>();

        // ── 资源符号表：整批资源只在这里算一次，各产物代码段一律查表 ──
        // 同一符号若在多处独立重算净化名，任一处漂移都会让产物内部自相矛盾
        // （generated_fonts.c 定义 font_X、main.c extern font_Y）。详见 CResourceSymbols 文档注释。
        var symbols = CResourceSymbols.Create(imageAssets, fontAssets, project);

        // 将资源文件复制到输出目录 assets/ 下。生成代码通过编译期注入的 SIM_ASSET_DIR 宏
        // （<工程根>/assets 绝对路径，由 CMakeLists.txt target_compile_definitions 提供）加载资源，
        // 既不依赖进程工作目录，也不固化开发者本机绝对路径（工程迁移后重新 cmake 自动更新）。
        CopyAssetFiles(outputDirectory, imageAssets, fontAssets);

        // 生成 main.c（包含字体加载逻辑）
        var mainC = Path.Combine(outputDirectory, "main.c");
        File.WriteAllText(mainC, BuildMainC(project, screenFunctions, bootScreen, symbols, tags), new UTF8Encoding(false));
        files.Add("main.c");

        // 生成图片资源 C 文件（如果有图片资源）
        if (imageAssets.Count > 0)
        {
            var imagesC = Path.Combine(outputDirectory, "generated_images.c");
            File.WriteAllText(imagesC, BuildImagesC(symbols), new UTF8Encoding(false));
            files.Add("generated_images.c");
        }

        // 生成字体加载代码（如果有字体资源）
        if (fontAssets.Count > 0)
        {
            var fontsC = Path.Combine(outputDirectory, "generated_fonts.c");
            File.WriteAllText(fontsC, BuildFontsC(symbols), new UTF8Encoding(false));
            files.Add("generated_fonts.c");
        }

        var simConfig = Path.Combine(outputDirectory, "sim_config.h");
        File.WriteAllText(simConfig, BuildSimConfig(bootScreen), new UTF8Encoding(false));
        files.Add("sim_config.h");

        var cmake = Path.Combine(outputDirectory, "CMakeLists.txt");
        File.WriteAllText(cmake, BuildCmakeLists(imageAssets.Count > 0, fontAssets.Count > 0, project.LvglVersion), new UTF8Encoding(false));
        files.Add("CMakeLists.txt");

        var lvConf = Path.Combine(outputDirectory, "lv_conf.h");
        File.WriteAllText(lvConf, BuildLvConf(fontAssets.Count > 0), new UTF8Encoding(false));
        files.Add("lv_conf.h");

        var genC = Path.Combine(outputDirectory, "generated_hmi.c");
        File.WriteAllText(genC, generatedCode, new UTF8Encoding(false));
        files.Add("generated_hmi.c");

        // 通讯连接配置 → generated_comm.h（设备端 comm_core.c 用 __has_include 优先引用；
        // 无连接时仍生成空头文件保证设备端 include 不破坏构建）
        var commH = Path.Combine(outputDirectory, "generated_comm.h");
        File.WriteAllText(commH, CommConfigCodeGen.Generate(connections), new UTF8Encoding(false));
        files.Add("generated_comm.h");

        // 安全/权限模型 → generated_security.h。设备端 runtime/hmi_actions.c 用 __has_include
        // 优先引用它（缺省回退"设计模式：不门控"），而**动作回调门里的 HMI_PERM_* 也来自它** ——
        // 所以它必须与生成代码同批产出，否则导出的工程一编译就是未定义标识符。
        // 传 screens：会话超时的锁屏目标要在这里**生成期解析**（LockScreenId 是 Id，固件按名字切屏）。
        var securityH = Path.Combine(outputDirectory, "generated_security.h");
        File.WriteAllText(securityH, SecurityConfigCodeGen.Generate(project.SecurityConfig, project.Screens), new UTF8Encoding(false));
        files.Add("generated_security.h");

        // 运行时参数 → generated_runtime.h。设备端 runtime/runtime_config.h（轮询周期）与
        // runtime/hmi_runtime.h（日志门槛）用 __has_include 优先引用它，缺省回退各自的硬编码默认值。
        // 这两个值此前只能靠手改固件源码调整 —— 工程设置里的「扫描周期 / 日志级别」配了到不了设备。
        var runtimeH = Path.Combine(outputDirectory, "generated_runtime.h");
        File.WriteAllText(runtimeH, RuntimeConfigCodeGen.Generate(project.RuntimeConfig), new UTF8Encoding(false));
        files.Add("generated_runtime.h");

        var readme = Path.Combine(outputDirectory, "README.md");
        File.WriteAllText(readme, BuildReadme(project, screenFunctions), new UTF8Encoding(false));
        files.Add("README.md");

        return new SimulatorExportResult(outputDirectory, files, screenFunctions);
    }

    // ── 文件内容 ──

    /// <summary>
    /// 将图片/字体资源文件复制到输出目录的 assets/ 子目录，供生成代码以相对路径加载。
    /// 复制失败不阻断导出（仅记录到 stderr 调试输出），保证无资源时仍可构建。
    /// </summary>
    private static void CopyAssetFiles(string outputDirectory, IReadOnlyList<AssetItem> imageAssets, IReadOnlyList<AssetItem> fontAssets)
    {
        var assetsDir = Path.Combine(outputDirectory, "assets");
        try
        {
            Directory.CreateDirectory(assetsDir);
            foreach (var asset in imageAssets.Concat(fontAssets))
            {
                var src = asset.FilePath;
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
                try
                {
                    var dest = Path.Combine(assetsDir, Path.GetFileName(src));
                    File.Copy(src, dest, overwrite: true);
                }
                catch { /* 单个资源复制失败不影响其余资源与构建 */ }
            }
        }
        catch { /* assets 目录创建失败时跳过复制 */ }
    }
}
