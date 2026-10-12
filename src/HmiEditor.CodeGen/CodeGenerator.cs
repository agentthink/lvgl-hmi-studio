using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// C 代码生成引擎 —— 将 HMI 项目模型生成 LVGL C 源代码。
/// 包含主题样式初始化 + 屏幕 + 控件生成。
/// </summary>
public partial class CodeGenerator
{
    private readonly string _templatesPath;

    /// <summary>是否在生成代码中包含标注性注释（控件名/绑定/动作描述）。由 ExportConfig.IncludeComments 控制。</summary>
    private bool _includeComments = true;

    /// <summary>
    /// SharedLibrary（.so）模式：字体引用走宿主 hmi_resolve_font 服务（项目无关热更新），
    /// TextArea 生成软键盘模式标记；Executable/模拟器模式保持 extern + generated_fonts.c。
    /// </summary>
    private bool _soMode = false;

    /// <summary>
    /// 生成器追溯戳（**只有版本号**），写入生成代码头部。
    ///
    /// <para>⚠ 2026-10-11（阶段 317，用户拍板 ④=1）：**去掉了原来的 `(git &lt;短哈希&gt;)`**。
    /// 原因：设备侧产物 <c>firmware/t113/generated/</c> 是**入库**的，而带 git sha 的戳会让
    /// **每次提交都改产物** ⇒ "可再导出"没法用逐字节相等来判、diff 永远有噪声。
    /// 溯源改由提交信息 / 发布清单承担；版本号仍能定位代码世代。</para>
    /// </summary>
    internal static string GeneratorStamp { get; } = BuildGeneratorStamp();

    private static string BuildGeneratorStamp()
    {
        var asm = typeof(CodeGenerator).Assembly;
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            version = asm.GetName().Version?.ToString() ?? "0.0.0";
        // SDK 会把 commit 拼进 InformationalVersion（形如 `1.0.0+<sha>`）⇒ 这一截也得摘掉，
        // 否则"只留版本号"只做到一半（它同样是每次提交都变）。
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];
        return $"HmiEditor.CodeGen {version}";
    }

    public CodeGenerator(string? templatesPath = null)
    {
        _templatesPath = templatesPath ?? Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    /// <summary>
    /// 生成完整的 LVGL C 项目代码（含 main()，供固件直接编译）。
    /// </summary>
    public string GenerateFullProject(HmiProject project, AssetLibrary? assetLibrary = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GenerateSimulatorModule(project, assetLibrary));
        sb.AppendLine(BuildFullProjectMain(project));
        return sb.ToString();
    }

    /// <summary>
    /// 生成完整工程模式的 main()：初始化 LVGL、应用主题、创建独立屏幕与共享图层、加载启动屏幕并进入主循环。
    /// </summary>
    private string BuildFullProjectMain(HmiProject project)
    {
        var sb = new StringBuilder();

        // 图层引用：被引用的公共页面不再作为独立屏幕创建
        var referencedLayerIds = ScreenLayerRefs.CollectReferencedLayerIds(project);

        sb.AppendLine("int main(void)");
        sb.AppendLine("{");
        sb.AppendLine("    lv_init();");
        sb.AppendLine();

        // 应用主题样式
        sb.AppendLine("    apply_theme_styles();");
        sb.AppendLine();

        // 独立屏幕（非图层）
        foreach (var screen in project.Screens.Where(s => !referencedLayerIds.Contains(s.Id)))
        {
            sb.AppendLine($"    lv_obj_t* {CIdentifier.ScreenObject(screen.Id)} = {CIdentifier.ScreenFactory(screen.Id)}();");
        }

        // 共享图层对象（公共页面作为背景图层）：每个图层仅创建一份 detached 实例，
        // 初始隐藏，挂载到启动屏幕后随屏幕切换转移（需运行时按 HMI_LAYER_REFS 处理）。
        var layers = project.Screens.Where(s => referencedLayerIds.Contains(s.Id)).ToList();
        if (layers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    /* 共享图层对象（公共页面作为背景，detached + 初始隐藏） */");
            foreach (var layer in layers)
            {
                sb.AppendLine($"    lv_obj_t* {CIdentifier.LayerObject(layer.Id)} = {CIdentifier.ScreenFactoryInner(layer.Id)}(NULL);");
                sb.AppendLine($"    lv_obj_add_flag({CIdentifier.LayerObject(layer.Id)}, LV_OBJ_FLAG_HIDDEN);");
            }
        }

        sb.AppendLine();
        // RuntimeConfig.BootScreenId —— 开机默认加载屏幕；空或无效则回退首个非图层屏幕
        var bootScreen = project.Screens
            .Where(s => !referencedLayerIds.Contains(s.Id))
            .FirstOrDefault(s => s.Id == project.RuntimeConfig.BootScreenId)
            ?? project.Screens.FirstOrDefault(s => !referencedLayerIds.Contains(s.Id));
        if (bootScreen != null)
        {
            sb.AppendLine($"    lv_screen_load({CIdentifier.ScreenObject(bootScreen.Id)});");

            // 挂载启动屏幕引用的图层（置于背景层）
            foreach (var layerId in bootScreen.ReferencedLayerIds)
            {
                if (!referencedLayerIds.Contains(layerId)) continue;
                sb.AppendLine($"    lv_obj_set_parent({CIdentifier.LayerObject(layerId)}, {CIdentifier.ScreenObject(bootScreen.Id)});");
                sb.AppendLine($"    lv_obj_remove_flag({CIdentifier.LayerObject(layerId)}, LV_OBJ_FLAG_HIDDEN);");
                sb.AppendLine($"    lv_obj_move_background({CIdentifier.LayerObject(layerId)});");
            }
        }
        sb.AppendLine();

        // 启动所有 OnLoad 动画
        if (project.AnimationConfigs.Count > 0)
        {
            sb.AppendLine("    apply_animations();");
            sb.AppendLine();
        }

        sb.AppendLine("    while (1) {");
        sb.AppendLine("        lv_task_handler();");
        sb.AppendLine("        lv_tick_inc(5);");
        sb.AppendLine("    }");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");

        // LVGL 兼容性检查：替换 9.x 废弃 API，检测结果以注释头插入生成代码
        return LvglApiCompatChecker.Process(sb.ToString(), project.LvglVersion);
    }
}
