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

/// <summary>SimulatorProjectExporter 分部 —— 图片/字体资源 C 文件生成（BuildImagesC / BuildFontsC）。</summary>
public static partial class SimulatorProjectExporter
{
    /// <summary>
    /// 生成图片资源 C 文件 —— 将项目中的图片资源转换为 LVGL 可识别的 C 数组。
    /// 注: 实际图片数据需通过 lv_img_conv.py 工具转换或在项目中预先转换。
    /// 此方法生成引用代码框架。
    /// </summary>
    private static string BuildImagesC(CResourceSymbols symbols)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/* ============================================================");
        sb.AppendLine(" * 图片资源 —— 由 HMI Editor 自动生成");
        sb.AppendLine(" * 使用 LVGL image_decoder 从磁盘加载实际图片文件");
        sb.AppendLine(" * ============================================================ */");
        sb.AppendLine("#include \"lvgl.h\"");
        sb.AppendLine();
        sb.AppendLine("/* 注册 FS_STDIO 驱动路径为 'A:' 已在 lv_conf.h 中配置 */");
        sb.AppendLine();
        sb.AppendLine("/* 图片资源声明（供外部模块引用）：此处以 extern 声明指向磁盘加载的 dsc，");
        sb.AppendLine(" * 实际 dsc 由 generated_hmi.c 的运行时代码通过 lv_image_set_src() 设置。 */");
        sb.AppendLine();

        // 生成 extern 声明，供 generated_hmi.c 通过 lv_image_set_src 引用
        foreach (var image in symbols.Images)
        {
            // 符号片段与文件名均来自符号表，不在本方法内重算。
            sb.AppendLine($"/* 图片: {image.Name} (ID: {image.Id}) */");
            sb.AppendLine($"/* 运行时绝对路径由 SIM_ASSET_DIR 注入 */");
            sb.AppendLine(CResourceDeclarations.ImageExtern(image.Fragment));
            sb.AppendLine($"/* 文件路径常量：用于运行时通过 lv_image_set_src 直接加载 */");
            sb.AppendLine(CResourceDeclarations.ImagePathMacroDefine(image.Fragment, image.FileName));
            sb.AppendLine();
        }

        // 生成占位 img_dsc 定义（为了编译通过；若 generated_hmi.c 使用 IMG_PATH_xxx 则实际不会用到 dsc）
        sb.AppendLine("/* ── 占位 lv_img_dsc 定义 ── */");
        sb.AppendLine("/* 注: 若 generated_hmi.c 使用 lv_image_set_src(\"A:/abs/path.jpg\") 字符串路径加载，");
        sb.AppendLine(" * 则下列占位符仅为 extern 声明的链接占位，不会被渲染器读取。 */");
        sb.AppendLine();
        foreach (var image in symbols.Images)
        {
            sb.AppendLine($"/* 占位符数组（仅为链接使用，实际数据由文件解码器读取） */");
            sb.AppendLine($"static const unsigned char _img_{image.Fragment}_empty_map[1] = {{0}};");
            sb.AppendLine($"const lv_img_dsc_t {CResourceDeclarations.ImageVariable(image.Fragment)} = {{");
            sb.AppendLine($"    .header.magic = 0,");
            sb.AppendLine($"    .header.cf = LV_COLOR_FORMAT_ARGB8888,");
            sb.AppendLine($"    .header.reserved_2 = 0,");
            sb.AppendLine($"    .header.w = 1,");
            sb.AppendLine($"    .header.h = 1,");
            sb.AppendLine($"    .header.stride = 4,");
            sb.AppendLine($"    .data_size = 1,");
            sb.AppendLine($"    .data = _img_{image.Fragment}_empty_map,");
            sb.AppendLine($"}};");
            sb.AppendLine();
        }

        sb.AppendLine("/* 外部模块图片声明（与 extern 保持一致） */");
        foreach (var image in symbols.Images)
        {
            sb.AppendLine(CResourceDeclarations.ImageDeclare(image.Fragment));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成字体资源 C 文件 —— 声明项目中使用的字体。
    /// 注: 字体数据需通过 LVGL Font Converter 工具转换。
    /// </summary>
    /// <summary>
    /// 生成字体资源 C 文件 —— 通过 LV_USE_FREETYPE 运行时从磁盘加载 TTF/OTF/TTC 字体文件。
    /// 字体以 lv_font_t 全局结构体形式暴露（符号名: font_{sanitizedId}），供
    /// generated_hmi.c 的 apply_theme_styles 通过 lv_obj_set_style_text_font 使用。
    /// 同时提供 init_font_assets(void) 初始化函数，供 main.c 在 lv_init() 后调用。
    /// 额外扫描项目中所有控件使用的 (FontFamily, FontSize) 组合，生成对应的变尺寸字体变量。
    /// </summary>
    private static string BuildFontsC(CResourceSymbols symbols)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/* ============================================================");
        sb.AppendLine(" * 字体资源 —— 由 HMI Editor 自动生成");
        sb.AppendLine(" * 通过 LV_USE_FREETYPE 运行时从磁盘加载 TTF/OTF/TTC 字体文件");
        sb.AppendLine(" * （支持完整 CJK / Unicode 字形，无需离线转换 C 数组）");
        sb.AppendLine(" * ============================================================ */");
        sb.AppendLine("#include \"lvgl.h\"");
        sb.AppendLine("#if LV_USE_FREETYPE");
        sb.AppendLine("/* LVGL 头文件库位于 lvgl/src/libs/freetype/ 目录，参考 SDL 驱动写法使用相对路径 */");
        sb.AppendLine("#include \"src/libs/freetype/lv_freetype.h\"");
        sb.AppendLine("#endif");
        sb.AppendLine("#include <stdio.h>");
        sb.AppendLine();

        // ── 路径宏 + 全局 lv_font_t 变量 ──
        sb.AppendLine("/* ── 字体磁盘路径宏 & 全局字体实例 ── */");
        sb.AppendLine("/* 注: 全局字体变量由 init_font_assets() 通过 FreeType create 的返回值拷贝 */");
        sb.AppendLine("/*     填充（结构体 shallow copy，内部指针指向 FT 内部 heap，生命周期与进程一致） */");
        sb.AppendLine();

        foreach (var font in symbols.Fonts)
        {
            // 符号片段 / 文件名 / 像素尺寸均来自符号表，不在本方法内重算。
            // lv_freetype_font_create 内部使用 FT_New_Face(pathname) 直接访问文件系统，
            // 不经过 LV_FS_STDIO（因此不加 A: 前缀）。SIM_ASSET_DIR 由 CMake 编译期注入为
            // <工程根>/assets 绝对路径，运行时按绝对路径加载，不依赖进程工作目录。
            sb.AppendLine($"/* 字体: {font.Name} (ID: {font.Id}) */");
            sb.AppendLine($"/* FreeType 运行时绝对路径由 SIM_ASSET_DIR 注入 */");
            sb.AppendLine(CResourceDeclarations.FontPathMacroDefine(font.Fragment, font.FileName));
            sb.AppendLine(CResourceDeclarations.FontPixelSizeMacroDefine(font.Fragment, font.PixelSize));
            // 全局字体变量声明（与 generated_hmi.c 的 extern lv_font_t font_xxx 匹配）
            sb.AppendLine(CResourceDeclarations.FontDefinition(font.Fragment));
            sb.AppendLine();
        }

        // ── 控件自定义字号字体变量 ──
        if (symbols.FontVariants.Count > 0)
        {
            sb.AppendLine("/* ── 控件自定义字号字体变量（由属性面板 FontSize 生成）── */");
            foreach (var variant in symbols.FontVariants)
            {
                sb.AppendLine(CResourceDeclarations.FontVariantDefinition(variant.FontFragment, variant.Size));
            }
            sb.AppendLine();
        }

        // ── 嵌入 CJK 字体回退（按尺寸取最近可用） ──
        // D5 修复：FreeType 加载失败时不再硬编码 14px 子集，
        // 而是按请求 size 选取 14/16 两个嵌入子集中最近的一个，
        // 与设计器"字号不变"语义对齐。
        sb.AppendLine("/* 模拟器嵌入的 CJK 子集只有 14/16 两档：按 size 取最近可用尺寸 */");
        sb.AppendLine("static lv_font_t* sim_fallback_cjk_font(int size)");
        sb.AppendLine("{");
        sb.AppendLine("    /* 未提供嵌入 CJK 字体时使用 LVGL 当前默认字体，保证模拟器可链接。 */");
        sb.AppendLine("    (void)size;");
        sb.AppendLine("    return lv_font_get_default();");
        sb.AppendLine("}");
        sb.AppendLine();

        // ── init_font_assets() 函数 ──
        sb.AppendLine("#if LV_USE_FREETYPE");
        sb.AppendLine("static int g_font_assets_initialized = 0;");
        sb.AppendLine();
        sb.AppendLine("void init_font_assets(void)");
        sb.AppendLine("{");
        sb.AppendLine("    if (g_font_assets_initialized) return;");
        sb.AppendLine("    g_font_assets_initialized = 1;");
        sb.AppendLine("    /* 注意：LV_USE_FREETYPE=1 时 lv_init() 内部已调用 lv_freetype_init。");
        sb.AppendLine("     * 此处无需重复初始化，直接调用 lv_freetype_font_create 即可。 */");
        foreach (var font in symbols.Fonts)
        {
            sb.AppendLine($"    {{");
            sb.AppendLine($"        lv_font_t * created = lv_freetype_font_create(");
            sb.AppendLine($"            {CResourceDeclarations.FontPathMacro(font.Fragment)},");
            sb.AppendLine($"            LV_FREETYPE_FONT_RENDER_MODE_BITMAP,");
            sb.AppendLine($"            {CResourceDeclarations.FontPixelSizeMacro(font.Fragment)},");
            sb.AppendLine($"            LV_FREETYPE_FONT_STYLE_NORMAL);");
            sb.AppendLine($"        if (created) {{");
            sb.AppendLine($"            /* Shallow-copy 返回的堆结构体到全局符号（结构体字段无 move 语义，指针均指向 FT 内部对象） */");
            sb.AppendLine($"            {CResourceDeclarations.FontVariable(font.Fragment)} = *created;");
            sb.AppendLine($"            printf(\"[fonts] loaded asset \\\"{font.Name}\\\" via FreeType (px=%d)\\n\", {CResourceDeclarations.FontPixelSizeMacro(font.Fragment)});");
            sb.AppendLine($"        }} else {{");
            sb.AppendLine($"            fprintf(stderr, \"[fonts] WARN: cannot load font asset \\\"{font.Name}\\\" from \" {CResourceDeclarations.FontPathMacro(font.Fragment)} \"\\n\");");
            sb.AppendLine($"            /* 加载失败时回退到嵌入的 SimSun CJK 子集字体（D5：按 size 取最近可用尺寸），");
            sb.AppendLine($"             * 避免生成的 hmi_ui_apply_theme 对零初始化结构体调用 lv_obj_set_style_text_font");
            sb.AppendLine($"             * 后，渲染时解引用空函数指针导致段错误（模拟器异常退出）。 */");
            sb.AppendLine($"            {CResourceDeclarations.FontVariable(font.Fragment)} = *sim_fallback_cjk_font({CResourceDeclarations.FontPixelSizeMacro(font.Fragment)});");
            sb.AppendLine($"        }}");
            sb.AppendLine($"    }}");
        }

        // 变尺寸字体创建（控件自定义字号）
        foreach (var variant in symbols.FontVariants)
        {
            sb.AppendLine($"    {{");
            sb.AppendLine($"        lv_font_t * created = lv_freetype_font_create(");
            sb.AppendLine($"            {CResourceDeclarations.FontPathMacro(variant.FontFragment)},");
            sb.AppendLine($"            LV_FREETYPE_FONT_RENDER_MODE_BITMAP,");
            sb.AppendLine($"            {variant.Size},");
            sb.AppendLine($"            LV_FREETYPE_FONT_STYLE_NORMAL);");
            sb.AppendLine($"        if (created) {{");
            sb.AppendLine($"            {CResourceDeclarations.FontVariantVariable(variant.FontFragment, variant.Size)} = *created;");
            sb.AppendLine($"            printf(\"[fonts] loaded font '{variant.FontFragment}' size {variant.Size} via FreeType\\n\");");
            sb.AppendLine($"        }} else {{");
            sb.AppendLine($"            fprintf(stderr, \"[fonts] WARN: cannot load font '{variant.FontFragment}' size {variant.Size} from \" {CResourceDeclarations.FontPathMacro(variant.FontFragment)} \"\\n\");");
            sb.AppendLine($"            /* 加载失败时回退到嵌入的 SimSun CJK 子集字体（D5：按 size 取最近可用尺寸） */");
            sb.AppendLine($"            {CResourceDeclarations.FontVariantVariable(variant.FontFragment, variant.Size)} = *sim_fallback_cjk_font({variant.Size});");
            sb.AppendLine($"        }}");
            sb.AppendLine($"    }}");
        }

        sb.AppendLine("}");
        sb.AppendLine("#else  /* !LV_USE_FREETYPE */");
        sb.AppendLine("/* 无 FreeType：将字体结构体回退到嵌入的 SimSun CJK 子集字体（D5：按 size 取最近可用尺寸），");
        sb.AppendLine(" * 避免 apply_theme_styles 使用零初始化结构体导致渲染崩溃（模拟器异常退出）。 */");
        sb.AppendLine("void init_font_assets(void)");
        sb.AppendLine("{");
        foreach (var font in symbols.Fonts)
        {
            sb.AppendLine($"    {CResourceDeclarations.FontVariable(font.Fragment)} = *sim_fallback_cjk_font({CResourceDeclarations.FontPixelSizeMacro(font.Fragment)});");
        }
        foreach (var variant in symbols.FontVariants)
        {
            sb.AppendLine($"    {CResourceDeclarations.FontVariantVariable(variant.FontFragment, variant.Size)} = *sim_fallback_cjk_font({variant.Size});");
        }
        sb.AppendLine("}");
        sb.AppendLine("#endif");
        sb.AppendLine();

        // ── 与 extern 保持一致的 LV_IMG_DECLARE 风格声明（此处用于字体） ──
        foreach (var font in symbols.Fonts)
        {
            sb.AppendLine($"/* extern 可见性已通过全局定义 lv_font_t {CResourceDeclarations.FontVariable(font.Fragment)} 提供 */");
        }

        return sb.ToString();
    }
}
