namespace HmiEditor.AI;

/// <summary>
/// AI 产出的**用户可见诊断码** —— 单一来源。
///
/// <para><b>它解决什么</b>：AI 程序集里的校验/建议文案（<c>AiValidationIssue.Message</c> /
/// <c>LayoutIssue.Message</c>）**会直接显示在界面上** —— <c>AiChatPanelView</c> 的聊天条目
/// <c>Detail</c>、<c>AiGenerateDialogBase</c> 的状态行、<c>PropertyPanelView.Ai</c> 的建议列表。
/// 在把它们接上字典之前，它们只是**字面量**：界面语言切成英文，这三处照旧显示中文
/// （旁边的前缀却是本地化的键 ⇒ 中英混排）。</para>
///
/// <para><b>为什么不直接用键而用码</b>：与 <c>LuaSyntaxValidator</c> 的 <c>LUA_SYNTAX_*</c>
/// 同一套约定 —— 程序集只声明**稳定的码**，键由 App 侧的
/// <see cref="HmiEditor.App.Services.ExternalText.AiIssueKey"/> 统一推导
/// （<c>ai.issue.&lt;码小写&gt;</c>）。码是"哪一类问题"，键是"在哪本字典里"，两者生命周期不同：
/// 改键前缀不该动到 AI 程序集。</para>
///
/// <para><b>为什么必须是 <c>public const string</c></b>：本地化守卫靠**反射枚举**
/// 这个类型来拿"声明了哪些码"，再与"真正产出的码"做**双向差集** ——
/// 声明了没人产出的是死码（它和它的字典键都永远不会被用到），
/// 产出了没声明的是漏登记（键不会进字典 ⇒ 界面回落中文）。
/// 反射要求 <c>IsLiteral &amp;&amp; FieldType == typeof(string)</c>，所以不能是
/// <c>static readonly</c>。</para>
///
/// <para><b>命名</b>：大写下划线，与 Lua 侧的 <c>LUA_SYNTAX_MISSING_END</c> 同形。
/// 前缀分组只体现在名字上（<c>LAYOUT_*</c> 来自布局分析器、其余来自输出校验/编排），
/// 字典键统一落在 <c>ai.issue.</c> 之下。</para>
/// </summary>
public static class AiIssueCodes
{
    // ────────────────────────── 屏幕校验 ──────────────────────────

    /// <summary>屏幕对象为 null。0 实参。</summary>
    public const string SCREEN_EMPTY = "SCREEN_EMPTY";

    /// <summary>屏幕宽或高小于 1。2 实参：宽、高。</summary>
    public const string SCREEN_SIZE_INVALID = "SCREEN_SIZE_INVALID";

    /// <summary>屏幕背景色不是 #RGB/#RRGGBB/#RRGGBBAA。1 实参：原值。</summary>
    public const string SCREEN_BGCOLOR_INVALID = "SCREEN_BGCOLOR_INVALID";

    /// <summary>屏幕没有任何控件（Warning 级 —— 用户可后续手动添加）。0 实参。</summary>
    public const string SCREEN_NO_CONTROLS = "SCREEN_NO_CONTROLS";

    /// <summary>控件数量超过 20 个。1 实参：实际个数。</summary>
    public const string SCREEN_TOO_MANY_CONTROLS = "SCREEN_TOO_MANY_CONTROLS";

    // ────────────────────────── 控件校验（屏幕 / 模板共用） ──────────────────────────

    /// <summary>控件类型不在 <c>HmiControlType</c> 白名单里。2 实参：控件名、类型。</summary>
    public const string CONTROL_TYPE_INVALID = "CONTROL_TYPE_INVALID";

    /// <summary>控件宽或高小于 1。3 实参：控件名、宽、高。</summary>
    public const string CONTROL_SIZE_INVALID = "CONTROL_SIZE_INVALID";

    /// <summary>控件坐标含负数。3 实参：控件名、X、Y。</summary>
    public const string CONTROL_POSITION_INVALID = "CONTROL_POSITION_INVALID";

    /// <summary>控件矩形超出屏幕边界（Warning 级）。1 实参：控件名。</summary>
    public const string CONTROL_OUT_OF_BOUNDS = "CONTROL_OUT_OF_BOUNDS";

    /// <summary>控件背景色格式非法。2 实参：控件名、原值。</summary>
    public const string CONTROL_BGCOLOR_INVALID = "CONTROL_BGCOLOR_INVALID";

    /// <summary>控件文字色格式非法。2 实参：控件名、原值。</summary>
    public const string CONTROL_TEXTCOLOR_INVALID = "CONTROL_TEXTCOLOR_INVALID";

    /// <summary>控件绑定的变量在工程里不存在（Warning 级 —— 用户可后续手动绑定）。2 实参：控件名、变量名。</summary>
    public const string CONTROL_VARIABLE_MISSING = "CONTROL_VARIABLE_MISSING";

    /// <summary>两个控件重叠面积超过较小者的 50%（Warning 级）。2 实参：两个控件名。</summary>
    public const string CONTROL_OVERLAP = "CONTROL_OVERLAP";

    // ────────────────────────── 模板校验 ──────────────────────────

    /// <summary>模板对象为 null。0 实参。</summary>
    public const string TEMPLATE_EMPTY = "TEMPLATE_EMPTY";

    /// <summary>模板名称为空。0 实参。</summary>
    public const string TEMPLATE_NAME_EMPTY = "TEMPLATE_NAME_EMPTY";

    /// <summary>模板不包含任何控件。0 实参。</summary>
    public const string TEMPLATE_NO_CONTROLS = "TEMPLATE_NO_CONTROLS";

    /// <summary>模板控件超过 15 个（Warning 级）。1 实参：实际个数。</summary>
    public const string TEMPLATE_TOO_MANY_CONTROLS = "TEMPLATE_TOO_MANY_CONTROLS";

    /// <summary>参数定义里有一个名字为空。0 实参。</summary>
    public const string PARAM_NAME_EMPTY = "PARAM_NAME_EMPTY";

    /// <summary>参数名在模板内重复。1 实参：参数名。</summary>
    public const string PARAM_NAME_DUPLICATE = "PARAM_NAME_DUPLICATE";

    /// <summary>控件引用了未声明的参数（Warning 级 —— 允许用户在模板编辑器补充）。
    /// 2 实参：控件名、参数名。</summary>
    public const string PARAM_UNDECLARED = "PARAM_UNDECLARED";

    // ────────────────────────── 脚本校验 ──────────────────────────

    /// <summary>脚本对象为 null。0 实参。</summary>
    public const string SCRIPT_EMPTY = "SCRIPT_EMPTY";

    /// <summary>脚本名称为空。0 实参。</summary>
    public const string SCRIPT_NAME_EMPTY = "SCRIPT_NAME_EMPTY";

    /// <summary>脚本代码为空。0 实参。</summary>
    public const string SCRIPT_CODE_EMPTY = "SCRIPT_CODE_EMPTY";

    /// <summary>脚本代码超过 8 KB（Warning 级）。0 实参。</summary>
    public const string SCRIPT_TOO_LONG = "SCRIPT_TOO_LONG";

    /// <summary>脚本里找不到 <c>function</c> 定义。0 实参。</summary>
    public const string SCRIPT_NO_FUNCTION = "SCRIPT_NO_FUNCTION";

    /// <summary>脚本含越出 HMI API 边界的敏感调用（LLM 安全防线）。1 实参：被禁用的 API。</summary>
    public const string SCRIPT_FORBIDDEN_API = "SCRIPT_FORBIDDEN_API";

    /// <summary>脚本分类不在已知集合里，已回退 OnClick（Warning 级）。1 实参：原分类名。</summary>
    public const string SCRIPT_UNKNOWN_CATEGORY = "SCRIPT_UNKNOWN_CATEGORY";

    // ────────────────────────── 编排（意图路由） ──────────────────────────

    /// <summary>识别出图片意图，但编排器没装图片生成器。0 实参。</summary>
    public const string IMAGE_GENERATOR_MISSING = "IMAGE_GENERATOR_MISSING";

    /// <summary>意图识别不出来，或识别出的意图当前版本不支持。1 实参：意图名。</summary>
    public const string INTENT_UNSUPPORTED = "INTENT_UNSUPPORTED";

    // ────────────────────────── 布局分析（LayoutAnalyzer） ──────────────────────────

    /// <summary>屏幕为空或控件过少，评分不适用。0 实参。</summary>
    public const string LAYOUT_SCREEN_TOO_SMALL = "LAYOUT_SCREEN_TOO_SMALL";

    /// <summary>控件离屏幕左边缘太近。3 实参：控件名、当前距离、安全边距。</summary>
    public const string LAYOUT_MARGIN_LEFT = "LAYOUT_MARGIN_LEFT";

    /// <summary>控件离屏幕上边缘太近。3 实参：控件名、当前距离、安全边距。</summary>
    public const string LAYOUT_MARGIN_TOP = "LAYOUT_MARGIN_TOP";

    /// <summary>控件离屏幕右边缘太近。3 实参：控件名、当前距离、安全边距。</summary>
    public const string LAYOUT_MARGIN_RIGHT = "LAYOUT_MARGIN_RIGHT";

    /// <summary>控件离屏幕下边缘太近。3 实参：控件名、当前距离、安全边距。</summary>
    public const string LAYOUT_MARGIN_BOTTOM = "LAYOUT_MARGIN_BOTTOM";

    /// <summary>控件与同组控件的顶部未对齐。3 实参：控件名、参照控件名、偏差。</summary>
    public const string LAYOUT_ALIGN_TOP = "LAYOUT_ALIGN_TOP";

    /// <summary>控件与同组控件的底部未对齐。3 实参：控件名、参照控件名、偏差。</summary>
    public const string LAYOUT_ALIGN_BOTTOM = "LAYOUT_ALIGN_BOTTOM";

    /// <summary>控件与同组控件的左侧未对齐。3 实参：控件名、参照控件名、偏差。</summary>
    public const string LAYOUT_ALIGN_LEFT = "LAYOUT_ALIGN_LEFT";

    /// <summary>控件与同组控件的右侧未对齐。3 实参：控件名、参照控件名、偏差。</summary>
    public const string LAYOUT_ALIGN_RIGHT = "LAYOUT_ALIGN_RIGHT";

    /// <summary>同行相邻控件的间距与平均间距偏差较大。4 实参：两控件名、当前间距、平均间距。</summary>
    public const string LAYOUT_SPACING_UNEVEN = "LAYOUT_SPACING_UNEVEN";

    /// <summary>最大控件占屏面积过小，缺少主视觉层次。2 实参：控件名、占比百分数。</summary>
    public const string LAYOUT_NO_HIERARCHY = "LAYOUT_NO_HIERARCHY";

    /// <summary>文字与背景对比度低于 WCAG AA。3 实参：控件名、实际对比度、阈值。</summary>
    public const string LAYOUT_LOW_CONTRAST = "LAYOUT_LOW_CONTRAST";
}
