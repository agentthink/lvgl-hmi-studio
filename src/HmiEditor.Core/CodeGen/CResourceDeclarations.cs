namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 资源类 C 符号的<b>命名与发射</b> —— 全仓唯一实现。
///
/// <para><b>为什么需要它：</b>符号名（<c>img_*</c> / <c>font_*</c> / <c>IMG_PATH_*</c> /
/// <c>FONT_PATH_*</c> / <c>FONT_PXSIZE_*</c>）此前以字面量形式散落在两条链路的
/// <b>约 25 处</b>字符串插值里。前缀只要有一处被改名（<c>img_</c> → <c>image_</c>），
/// 就会变成「声明与定义不同名」——症状是编译期找不到符号或链接期未定义引用，
/// 而报错位置与改动位置往往不在同一个文件。</para>
///
/// <para>同理，<c>extern const lv_img_dsc_t img_X;</c> 这一句在
/// <c>CodeGenerator</c>（2 处）与 <c>SimulatorProjectExporter</c>（1 处）各自手写；
/// 导出侧另有一处写成 <c>LV_IMG_DECLARE(img_X)</c>（展开即
/// <c>extern const lv_image_dsc_t img_X;</c>，靠 LVGL v9 兼容头的
/// <c>typedef lv_image_dsc_t lv_img_dsc_t;</c> 等价）。两种写法类型兼容、都能编译，
/// 但**混用**意味着将来 LVGL 收紧兼容层时只有一半代码会先坏。统一到本类后，
/// 「声明形态」只有一个来源。</para>
///
/// <para><b>覆盖范围</b>：符号名、声明语句、定义语句、路径/尺寸宏定义。
/// <b>不覆盖</b>：<c>_img_&lt;fragment&gt;_empty_map</c>（仅导出侧单点使用，无重复可言）、
/// 具体的 <c>lv_image_set_src(...)</c> 调用语句（含宿主 API 语义，不属于符号命名）。</para>
/// </summary>
public static class CResourceDeclarations
{
    // ══════════════════ 符号名（跨链路必须逐字符一致） ══════════════════

    /// <summary>图片描述符变量名：<c>img_&lt;fragment&gt;</c>。</summary>
    public static string ImageVariable(string fragment) => $"img_{fragment}";

    /// <summary>字体句柄变量名：<c>font_&lt;fragment&gt;</c>。</summary>
    public static string FontVariable(string fragment) => $"font_{fragment}";

    /// <summary>变尺寸字体句柄变量名：<c>font_&lt;fontFragment&gt;_&lt;size&gt;</c>。</summary>
    public static string FontVariantVariable(string fontFragment, int size) => $"font_{fontFragment}_{size}";

    /// <summary>图片路径宏名：<c>IMG_PATH_&lt;fragment&gt;</c>。</summary>
    public static string ImagePathMacro(string fragment) => $"IMG_PATH_{fragment}";

    /// <summary>字体路径宏名：<c>FONT_PATH_&lt;fragment&gt;</c>。</summary>
    public static string FontPathMacro(string fragment) => $"FONT_PATH_{fragment}";

    /// <summary>字体像素尺寸宏名：<c>FONT_PXSIZE_&lt;fragment&gt;</c>。</summary>
    public static string FontPixelSizeMacro(string fragment) => $"FONT_PXSIZE_{fragment}";

    // ══════════════════ 声明语句 ══════════════════

    /// <summary><c>extern const lv_img_dsc_t img_X;</c> —— 图片描述符外部声明。</summary>
    public static string ImageExtern(string fragment)
        => $"extern const lv_img_dsc_t {ImageVariable(fragment)};";

    /// <summary>
    /// <c>LV_IMG_DECLARE(img_X);</c> —— LVGL 风格声明，等价于 <see cref="ImageExtern"/>。
    /// 与 <see cref="ImageExtern"/> 同时存在于导出产物中（一处显式 extern、一处 DECLARE），
    /// 是既有产物形态，保持一致。
    /// </summary>
    public static string ImageDeclare(string fragment)
        => $"LV_IMG_DECLARE({ImageVariable(fragment)});";

    /// <summary><c>extern lv_font_t font_X;</c> —— 字体句柄外部声明。</summary>
    public static string FontExtern(string fragment)
        => $"extern lv_font_t {FontVariable(fragment)};";

    /// <summary><c>extern lv_font_t font_X_NN;</c> —— 变尺寸字体句柄外部声明。</summary>
    public static string FontVariantExtern(string fontFragment, int size)
        => $"extern lv_font_t {FontVariantVariable(fontFragment, size)};";

    // ══════════════════ 定义语句 ══════════════════

    /// <summary><c>lv_font_t font_X;</c> —— 模拟器模式下字体句柄的全局定义。</summary>
    public static string FontDefinition(string fragment)
        => $"lv_font_t {FontVariable(fragment)};";

    /// <summary><c>lv_font_t font_X_NN;</c> —— 变尺寸字体句柄的全局定义。</summary>
    public static string FontVariantDefinition(string fontFragment, int size)
        => $"lv_font_t {FontVariantVariable(fontFragment, size)};";

    /// <summary>
    /// <c>static const lv_font_t* font_X = NULL;</c> —— 共享库（<c>.so</c>）模式下的定义。
    /// 该模式下字体是项目无关的运行时解析结果，故为文件级 <c>static</c> 指针。
    /// </summary>
    public static string FontPointerDefinition(string fragment)
        => $"static const lv_font_t* {FontVariable(fragment)} = NULL;";

    /// <summary><c>static const lv_font_t* font_X_NN = NULL;</c> —— 共享库模式的变尺寸定义。</summary>
    public static string FontPointerVariantDefinition(string fontFragment, int size)
        => $"static const lv_font_t* {FontVariantVariable(fontFragment, size)} = NULL;";

    // ══════════════════ 路径 / 尺寸宏定义 ══════════════════

    /// <summary>
    /// <c>#define IMG_PATH_X "A:" SIM_ASSET_DIR "/file"</c>。
    /// <c>SIM_ASSET_DIR</c> 由构建系统编译期注入为 assets 目录绝对路径：
    /// 既不依赖进程工作目录，也不固化开发者本机路径。
    /// </summary>
    public static string ImagePathMacroDefine(string fragment, string fileName)
        => $"#define {ImagePathMacro(fragment)} \"A:\" SIM_ASSET_DIR \"/{fileName}\"";

    /// <summary>
    /// <c>#define FONT_PATH_X SIM_ASSET_DIR "/file"</c>。
    /// <b>不</b>加 <c>"A:"</c> 前缀 —— <c>lv_freetype_font_create</c> 内部用
    /// <c>FT_New_Face</c> 直接访问文件系统，不经过 LVGL 的 <c>LV_FS_STDIO</c>。
    /// </summary>
    public static string FontPathMacroDefine(string fragment, string fileName)
        => $"#define {FontPathMacro(fragment)} SIM_ASSET_DIR \"/{fileName}\"";

    /// <summary><c>#define FONT_PXSIZE_X NN</c> —— FreeType 初始化像素尺寸。</summary>
    public static string FontPixelSizeMacroDefine(string fragment, int pixelSize)
        => $"#define {FontPixelSizeMacro(fragment)} {pixelSize}";
}
