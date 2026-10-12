using System.Linq;
using System.Text;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.AI.Inference;

/// <summary>
/// Prompt 模板引擎 —— 从嵌入资源加载模板并渲染用户上下文。
/// 负责:
///   - 构建 System/User 消息
///   - 注入项目上下文(变量列表/主题)供模型参考
///   - 用户输入清洗: 长度上限 + 控制字符剥离 (LLM 安全: 不可信输入处理)
/// </summary>
public sealed class PromptTemplateEngine
{
    private const int MaxUserInputLength = 2000;

    /// <summary>
    /// 可用控件类型名（排除 <see cref="HmiControlType.Screen"/> 容器控件）。
    /// **定义已上移到 <see cref="AiSchemaEnums.GeneratableControlTypes"/>** —— 这里只做转发，
    /// 让 System 提示的 <c>{control_types}</c>、两份 Schema 的 <c>{control_type_enum}</c>
    /// 与 Agent 工具 schema / 解析白名单共用同一个数组实例（而不是"各处都读枚举、但各自算一遍"）。
    ///
    /// <para>保留这个属性是为了既有契约与测试（<c>PromptTemplateContractTests</c>）不动。</para>
    /// </summary>
    internal static IReadOnlyList<string> ControlTypeNames => AiSchemaEnums.GeneratableControlTypes;

    /// <summary>
    /// 模板参数值类型名 —— 同 <see cref="ControlTypeNames"/> 的道理，**唯一来源是
    /// <see cref="ParameterType"/> 枚举**：TemplateGenerationSchema 的
    /// <c>parameters[].type</c> enum 曾手写这 5 个字面量，与枚举无任何关联。
    /// </summary>
    internal static IReadOnlyList<string> ParameterTypeNames { get; } =
        Enum.GetNames<ParameterType>();

    /// <summary>
    /// 模板占位符全集（不含花括号），供守卫测试比对模板文件里实际出现的占位符。
    /// 必要性：<c>Replace</c> 对不存在的占位符是**静默无操作**，模板文件缺失时
    /// <c>LoadTemplate</c> 也只是返回空串 —— 所以占位符拼错不会有任何报错，
    /// 只会把 {xxx} 原文原样泄漏给模型。
    /// </summary>
    internal static IReadOnlySet<string> KnownPlaceholders { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "user_input", "variable_list", "theme_info", "screen_size",
            "control_types", "control_type_enum", "parameter_type_enum", "script_category_enum",
            "category", "variant_spec", "width", "height"
        };

    private readonly string _templatesPath;

    public PromptTemplateEngine(string? templatesPath = null)
    {
        _templatesPath = templatesPath ?? Path.Combine(AppContext.BaseDirectory, "Inference", "PromptTemplates");
    }

    /// <summary>读取模板文件内容,文件不存在时返回默认占位文案。</summary>
    private string LoadTemplate(string fileName)
    {
        var path = Path.Combine(_templatesPath, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>
    /// 构建屏幕生成对话消息。
    /// </summary>
    public List<LlmMessage> BuildScreenMessages(string userInput, HmiProject project)
    {
        var system = LoadTemplate("ScreenGenerationSystem.txt")
            .Replace("{control_types}", BuildControlTypes())
            .Replace("{screen_size}", BuildScreenSize(project));
        var userTemplate = LoadTemplate("ScreenGenerationUser.txt");

        var cleaned = SanitizeUserInput(userInput);

        var userContent = userTemplate
            .Replace("{user_input}", cleaned)
            .Replace("{variable_list}", BuildVariableList(project))
            .Replace("{theme_info}", BuildThemeInfo(project.Theme));

        var messages = new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = system },
            new() { Role = LlmRoles.User, Content = userContent }
        };

        return messages;
    }

    /// <summary>获取屏幕生成的 JSON Schema 内容（控件类型 enum 由枚举注入，见 <see cref="RenderSchema"/>）。</summary>
    public string GetScreenSchema() => RenderSchema("ScreenGenerationSchema.json");

    /// <summary>
    /// 构建 Faceplate 模板生成对话消息。
    /// </summary>
    public List<LlmMessage> BuildTemplateMessages(string userInput, HmiProject project)
    {
        var system = LoadTemplate("TemplateGenerationSystem.txt")
            .Replace("{control_types}", BuildControlTypes());
        var userTemplate = LoadTemplate("TemplateGenerationUser.txt");

        var cleaned = SanitizeUserInput(userInput);

        var userContent = userTemplate
            .Replace("{user_input}", cleaned)
            .Replace("{variable_list}", BuildVariableList(project))
            .Replace("{theme_info}", BuildThemeInfo(project.Theme));

        return new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = system },
            new() { Role = LlmRoles.User, Content = userContent }
        };
    }

    /// <summary>获取模板生成的 JSON Schema 内容（控件类型 enum 由枚举注入，见 <see cref="RenderSchema"/>）。</summary>
    public string GetTemplateSchema() => RenderSchema("TemplateGenerationSchema.json");

    /// <summary>
    /// 构建 Lua 脚本生成对话消息。
    /// </summary>
    public List<LlmMessage> BuildScriptMessages(string userInput, HmiProject project)
    {
        var system = LoadTemplate("ScriptGenerationSystem.txt");
        var userTemplate = LoadTemplate("ScriptGenerationUser.txt");

        var cleaned = SanitizeUserInput(userInput);

        var userContent = userTemplate
            .Replace("{user_input}", cleaned)
            .Replace("{variable_list}", BuildVariableList(project));

        return new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = system },
            new() { Role = LlmRoles.User, Content = userContent }
        };
    }

    /// <summary>获取脚本生成的 JSON Schema 内容。</summary>
    public string GetScriptSchema() => RenderSchema("ScriptGenerationSchema.json");

    /// <summary>
    /// 构建 SVG 图标生成对话消息。无项目上下文需求, 仅用户描述。
    /// 默认使用 General 分类 + Linear 变体(向后兼容)。
    /// </summary>
    public List<LlmMessage> BuildIconMessages(string userInput)
        => BuildIconMessages(userInput, Generators.IconCategory.General, Generators.IconStyleVariant.Linear);

    /// <summary>
    /// 构建 SVG 图标生成对话消息 —— 注入分类与视觉风格变体规范。
    /// 分类与变体由 <see cref="Generators.IconCategoryClassifier"/> / <see cref="Generators.IconVariantSelector"/>
    /// 决定, 使同一语义图标在不同分类/变体下视觉表现多样化。
    /// </summary>
    public List<LlmMessage> BuildIconMessages(
        string userInput,
        Generators.IconCategory category,
        Generators.IconStyleVariant variant)
    {
        var system = LoadTemplate("IconGenerationSystem.txt");
        var userTemplate = LoadTemplate("IconGenerationUser.txt");

        var cleaned = SanitizeUserInput(userInput);
        var userContent = userTemplate
            .Replace("{user_input}", cleaned)
            .Replace("{category}", GetCategoryHint(category))
            .Replace("{variant_spec}", Generators.IconVariantCatalog.GetSpecText(variant));

        return new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = system },
            new() { Role = LlmRoles.User, Content = userContent }
        };
    }

    /// <summary>
    /// 构建 SVG 图片生成对话消息 —— 支持自定义尺寸的彩色矢量图。
    /// </summary>
    public List<LlmMessage> BuildImageMessages(string userInput, int width, int height)
    {
        // System 也注入实际宽高：模板里的 SVG 示例曾写作 viewBox="0 0 {W} {H}" 这种
        // 未被替换的花括号示意，模型有照抄成非法 SVG 的风险；给实际数值最不易错。
        var system = LoadTemplate("ImageGenerationSystem.txt")
            .Replace("{width}", width.ToString())
            .Replace("{height}", height.ToString());
        var userTemplate = LoadTemplate("ImageGenerationUser.txt");

        var cleaned = SanitizeUserInput(userInput);
        var userContent = userTemplate
            .Replace("{user_input}", cleaned)
            .Replace("{width}", width.ToString())
            .Replace("{height}", height.ToString());

        return new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = system },
            new() { Role = LlmRoles.User, Content = userContent }
        };
    }

    /// <summary>生成分类提示语 —— 简述该类图标的核心语义, 供 LLM 构图参考。</summary>
    private static string GetCategoryHint(Generators.IconCategory category) => category switch
    {
        Generators.IconCategory.Device =>
            "类别: 设备(Device)。此类图标表达实物设备(泵/电机/阀门/压缩机/传感器等), 构图应突出设备外形与核心机构(叶片/阀芯/线圈), 辨识度高。",
        Generators.IconCategory.Action =>
            "类别: 动作(Action)。此类图标表达操作动作(启动/停止/复位/切换/确认等), 构图应直观表达动作含义, 常用方向箭头、状态切换、勾叉等符号语言。",
        Generators.IconCategory.Status =>
            "类别: 状态(Status)。此类图标表达运行状态(运行/报警/待机/故障/电量等), 构图应突出状态指示感(圆点/闪电/对勾/感叹号), 可读性强。",
        Generators.IconCategory.Navigation =>
            "类别: 导航(Navigation)。此类图标表达界面导航(返回/首页/菜单/设置/方向箭头), 构图简洁明确, 符合通用导航符号认知。",
        Generators.IconCategory.Process =>
            "类别: 过程图形(Process)。此类图标表达过程元素(管道/液位/流量/压力/罐体/仪表), 构图应体现工程语义与连接关系, 结构清晰。",
        Generators.IconCategory.Communication =>
            "类别: 通信(Communication)。此类图标表达通信元素(网络/无线/串口/蓝牙/信号), 构图常用弧线、波形、连接点表达传输与互联。",
        Generators.IconCategory.Safety =>
            "类别: 安全(Safety)。此类图标表达安全元素(急停/锁定/防护/警告), 构图应传达防护与警示语义(盾形/锁/停止符号), 视觉醒目。",
        Generators.IconCategory.Environment =>
            "类别: 环境(Environment)。此类图标表达环境参数(温度/湿度/光照/空气质量), 构图常用水滴、太阳、叶片、温度计等自然元素隐喻。",
        _ =>
            "类别: 通用(General)。请根据图标名称自行判断核心语义, 采用最直观的通用表达方式。"
    };

    /// <summary>
    /// 用户输入清洗 —— 不可信输入处理:
    /// 1. 截断超长输入 (防 token 滥用/denial of wallet)
    /// 2. 剥离控制字符与零宽字符 (防注入)
    /// 3. 不在输入中携带 prompt 注入指令隔离标记(由 System Prompt 约束,双重防御)
    /// </summary>
    internal static string SanitizeUserInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            // 保留可打印字符 + 常用中文 + 换行; 剥离控制字符/零宽字符
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                continue;
            if (c == '\u200b' || c == '\u200c' || c == '\u200d' || c == '\ufeff')
                continue;
            sb.Append(c);
        }

        var result = sb.ToString().Trim();
        return result.Length > MaxUserInputLength
            ? result[..MaxUserInputLength]
            : result;
    }

    /// <summary>
    /// 渲染 JSON Schema —— 把 <c>{control_type_enum}</c> / <c>{parameter_type_enum}</c> /
    /// <c>{script_category_enum}</c> 注入为对应枚举（或产品侧清单）的 JSON 数组。
    ///
    /// <para><b>Schema 文件里不得写好枚举字面量</b>：写了就必然漂移
    /// （曾出现 ScreenGenerationSchema 缺 Tank/Pipe/Valve、TemplateGenerationSchema
    /// 还额外缺 Rectangle/Ellipse/Polyline/Arc，而 System 提示已宣告支持它们）。</para>
    ///
    /// <para><b>新增带占位符的 Schema 必须接到这里</b>：<c>GetScriptSchema()</c> 曾经直接
    /// <c>LoadTemplate</c>，一旦给它加了占位符，那个 <c>{xxx}</c> 就会原样发给模型
    /// （<c>Replace</c> 对不存在的占位符是静默无操作）。守卫见
    /// <c>AiSchemaEnumContractTests</c> 与 <c>PromptTemplateContractTests</c>。</para>
    /// </summary>
    private string RenderSchema(string fileName)
        => LoadTemplate(fileName)
            .Replace("{control_type_enum}", ToJsonEnum(ControlTypeNames))
            .Replace("{parameter_type_enum}", ToJsonEnum(ParameterTypeNames))
            .Replace("{script_category_enum}", ToJsonEnum(ScriptTemplate.Categories));

    /// <summary>从 HmiControlType 枚举生成可用控件类型清单(排除 Screen 根容器)，避免与代码枚举漂移。</summary>
    private static string BuildControlTypes()
        => string.Join(", ", ControlTypeNames);

    /// <summary>枚举名清单 → JSON 字符串数组字面量（供 JSON Schema 的 enum 使用）。</summary>
    private static string ToJsonEnum(IReadOnlyList<string> names)
        => "[" + string.Join(", ", names.Select(n => $"\"{n}\"")) + "]";

    /// <summary>项目屏幕尺寸上下文: 首屏尺寸，无屏幕时回退 1024x600。</summary>
    private static string BuildScreenSize(HmiProject project)
    {
        var s = project.Screens.FirstOrDefault();
        return s is null ? "1024x600" : $"{s.Width}x{s.Height}";
    }

    /// <summary>构建变量列表上下文: "变量名 (数据类型) [单位]"。</summary>
    private static string BuildVariableList(HmiProject project)
    {
        var vars = project.VariableManager?.Variables;
        if (vars is null || vars.Count == 0)
            return "(项目暂无变量)";

        var sb = new StringBuilder();
        foreach (var v in vars)
        {
            sb.Append("- ").Append(v.Name)
              .Append(" (").Append(v.DataType).Append(')');
            if (!string.IsNullOrEmpty(v.Unit))
                sb.Append(" [").Append(v.Unit).Append(']');
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>构建主题上下文: 主色调/背景色/文字色。</summary>
    private static string BuildThemeInfo(HmiTheme theme)
    {
        if (theme is null) return "(使用默认工业风格)";
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(theme.PrimaryColor))
            sb.Append("主色调: ").Append(theme.PrimaryColor).Append("; ");
        if (!string.IsNullOrEmpty(theme.BackgroundColor))
            sb.Append("背景色: ").Append(theme.BackgroundColor).Append("; ");
        if (!string.IsNullOrEmpty(theme.TextColor))
            sb.Append("文字色: ").Append(theme.TextColor).Append(';');
        return sb.Length == 0 ? "(使用默认工业风格)" : sb.ToString().TrimEnd();
    }
}
