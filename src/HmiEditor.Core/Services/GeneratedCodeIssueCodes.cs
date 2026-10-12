namespace HmiEditor.Core.Services;

/// <summary>
/// 「生成代码验证结果」对话框里**给用户看的文案**的**诊断码**表 ——
/// 「**码 + 中文回落原文**（+ 实参）」三段式的码那一半（阶段 89，登记 <b>#61g</b>）。
/// 与 <c>HmiPackageIssueCodes</c> / <c>SvgParseIssueCodes</c> / <c>CsvImportIssueCodes</c> /
/// <c>AiIssueCodes</c> / <c>ScriptError.Code</c> 同一套约定。
///
/// <para><b>为什么要有这张表</b>：<see cref="GeneratedCodeValidator.Validate"/> 以前把每条
/// finding 的文案**在 Core 里直接拼成中文**，而唯一的文本出口
/// （<c>App/Views/ValidationResultView</c> 的 <c>Text = finding.Message</c> /
/// <c>Text = finding.Location</c>）**原样直显** ⇒ 界面切成英文时**整个对话框都是中文**
/// （不只是"原因那一半"）。码化之后 App 层才能按用户语言渲染
/// （出口 <c>ExternalText.Describe(GeneratedCodeFinding)</c> /
/// <c>ExternalText.DescribeLocation(GeneratedCodeFinding)</c>）。</para>
///
/// <para><b>它们都不是异常</b>：<see cref="GeneratedCodeValidator"/> 是**纯函数**
/// （<c>Validate</c> 返回 findings 列表）。⇒ 本族走「**返回值带码**」那条搬运方式，
/// 与 SVG 解析失败（阶段 87）/ CSV 导入问题（阶段 88）同形，**不是**带码异常。</para>
///
/// <para><b>与 <see cref="RuntimeUserValidator"/> 的关系</b>：那条支线（下面 5 个
/// <c>RUNTIME_USER_*</c>）以前在 Core 里把 <c>RuntimeUserIssueKind</c> 映射成中文长句 ——
/// 而 <c>RuntimeUserValidator</c> 的类注释**明文写着**「刻意只给枚举、不带文案：Core 侧的
/// 测试断言枚举（稳定），文案由 App 层映射」。本表把那次映射的产物改成**码**，
/// 于是 Core 不再产文案、约定也回到一致（映射本身留着，只是映射到码而不是中文）。</para>
///
/// <para><b>键由 App 侧统一推导</b>：<c>gen.issue.&lt;码小写&gt;</c> / 定位走
/// <c>gen.loc.&lt;码小写&gt;</c>。Core 拿不到字典服务（依赖方向 App → Core）
/// ⇒ 这里只声明稳定的码，查表在 App 层（<c>ExternalText.GenIssueKey</c>）。</para>
/// </summary>
public static class GeneratedCodeIssueCodes
{
    // ── 结构（Structure）──

    /// <summary>项目一条屏幕都没有 ⇒ 生成不出任何东西。0 实参。</summary>
    public const string NO_SCREENS = "NO_SCREENS";

    /// <summary>屏幕重名（同名的屏幕会生成同名前缀的函数）。2 实参 = 屏幕名、重复个数。</summary>
    public const string DUPLICATE_SCREEN_NAME = "DUPLICATE_SCREEN_NAME";

    /// <summary>同一屏内控件 id 重复 ⇒ 生成代码会产生同名静态变量。2 实参 = 控件 id、重复个数。</summary>
    public const string DUPLICATE_CONTROL_ID = "DUPLICATE_CONTROL_ID";

    // ── Faceplate（未解析参数）──

    /// <summary>控件文本里还剩 Faceplate 占位符（<c>{{…}}</c>）。1 实参 = 该文本（已截断）。</summary>
    public const string FACEPLATE_TEXT_UNRESOLVED = "FACEPLATE_TEXT_UNRESOLVED";

    /// <summary>绑定模型的属性值里还剩 Faceplate 占位符。2 实参 = 属性名、该值（已截断）。</summary>
    public const string FACEPLATE_BINDING_UNRESOLVED = "FACEPLATE_BINDING_UNRESOLVED";

    /// <summary>旧字段 <c>VariableBindingId</c> 里还剩 Faceplate 占位符。1 实参 = 该值（已截断）。</summary>
    public const string FACEPLATE_BINDING_LEGACY_UNRESOLVED = "FACEPLATE_BINDING_LEGACY_UNRESOLVED";

    // ── 表达式 / 悬空引用 ──

    /// <summary>
    /// 表达式绑定编译不过。2 实参 = 表达式（已截断）、**<c>ExpressionSyntaxError</c> 记录体**
    /// （阶段 91 / #61i 改口径：以前装的是 <c>TryCompile</c> 的 <c>out string</c> 错误串，
    /// 而那串是 Core 的中文回落原文 ⇒ 英文界面下中英混排。
    /// 记录体由 <c>ExternalText.Describe(GeneratedCodeFinding)</c> 填参前渲染成用户语言）。
    ///
    /// <para>⚠ 第二个实参是 <c>FormatException.Message</c> —— 它来自 <c>ExpressionEngine</c> 的
    /// <b>中文</b>抛错（<c>"三元运算缺少 ':'"</c> / <c>"缺少右括号 ')'"</c> /
    /// <c>$"意外的符号: {tok.Value} (位置 {tok.Pos})"</c>）⇒ 英文界面下这一半**仍是中文**。
    /// 这是本族的**诚实边界**，已登记 <b>#61i</b>（要给那三条建码）—— 本表不吞掉它。</para>
    /// </summary>
    public const string EXPRESSION_SYNTAX = "EXPRESSION_SYNTAX";

    /// <summary>绑定指向的变量已不存在。1 实参 = 变量 id。</summary>
    public const string DANGLING_REFERENCE = "DANGLING_REFERENCE";

    // ── 运行时用户表（RuntimeUser）──
    // 5 条：4 个 Kind + 1 条兜底。文案在 Core 里不再出现（见类注释）。

    /// <summary>用户名空/纯空白。1 实参 = 条目的表内下标。等级 Warning。</summary>
    public const string RUNTIME_USER_EMPTY_USERNAME = "RUNTIME_USER_EMPTY_USERNAME";

    /// <summary>用户名与前面某条重复（首个胜出 ⇒ 这条永远匹配不到）。2 实参 = 下标、用户名。Warning。</summary>
    public const string RUNTIME_USER_DUPLICATE_USERNAME = "RUNTIME_USER_DUPLICATE_USERNAME";

    /// <summary>角色 id 不在预置角色表内（设备端 <c>hmi_role_lookup</c> 查不到）。2 实参 = 下标、用户名。Error。</summary>
    public const string RUNTIME_USER_UNKNOWN_ROLE = "RUNTIME_USER_UNKNOWN_ROLE";

    /// <summary>已启用但没有密码哈希（设备端口令校验解析失败即拒绝）。2 实参 = 下标、用户名。Error。</summary>
    public const string RUNTIME_USER_ENABLED_WITHOUT_PASSWORD = "RUNTIME_USER_ENABLED_WITHOUT_PASSWORD";

    /// <summary>
    /// **兜底码**：<c>RuntimeUserIssueKind</c> 以后新增了取值、而这里的 switch 还没跟上的那一档。
    ///
    /// <para>⚠ 它是**结构上到不了**的（<c>RuntimeUserIssueKind</c> 目前恰好 4 个取值，
    /// <c>RuntimeUserValidator</c> 只会构造这 4 个）⇒ 进
    /// <c>GenFindingLocalizationGuardTests</c> 的 <c>NotProducible</c> 表（**双向**：
    /// 一旦真能产出 ⇒ 判据红，逼人把它挪进可产出集）。**不删码**（防线要留着）、
    /// 也**不塞进可产出集**（那是判据造假）—— 这是阶段 88 立下的三态闭包。</para>
    /// </summary>
    public const string RUNTIME_USER_UNKNOWN = "RUNTIME_USER_UNKNOWN";

    /// <summary>整表体检：表非空但**一条可用账户都没有** ⇒ 谁都登不上（表现为「点了没反应」）。1 实参 = 表内条数。Error。</summary>
    public const string RUNTIME_USER_TABLE_UNUSABLE = "RUNTIME_USER_TABLE_UNUSABLE";

    // ── 生成代码（CodeGen / LvglApi）──

    /// <summary>生成代码为空串。0 实参。</summary>
    public const string EMPTY_GENERATED_CODE = "EMPTY_GENERATED_CODE";

    /// <summary>生成代码里找不到 <c>create_screen_</c> 函数。0 实参。</summary>
    public const string MISSING_CREATE_SCREEN = "MISSING_CREATE_SCREEN";

    /// <summary>
    /// 生成代码里的一条 <c>// WARNING:</c> 注释被**刮回来**当 finding —— 消息体就是那条注释的原文。
    ///
    /// <para>⚠ **本码刻意没有字典键**：这条消息的内容是**产物（<c>generated_ui.c</c>）里的注释原文**，
    /// 是给读 C 代码的人看的，不是编辑器界面文案 ⇒ 翻译它等于改写产物（属「不迁」，与 #61c 同类的
    /// 判过的"不做"）。它是**回显**，所以「界面语言」与「它的语言」无关 —— 而 CodeGen 目前
    /// **自己就中英不一致**（<c>CodeGenerator.Screen.cs</c> 3 条英文 + 1 条中文），已登记 <b>#61k</b>。</para>
    /// </summary>
    public const string GENERATOR_WARNING_COMMENT = "GENERATOR_WARNING_COMMENT";

    /// <summary>调到白名单外的 <c>lv_*</c> 调用（拼写错误或版本差异）。1 实参 = API 名。</summary>
    public const string UNKNOWN_LVGL_API = "UNKNOWN_LVGL_API";
}

/// <summary>
/// finding 的**定位串**里「面向用户的文案」那一半的码（阶段 89，登记 <b>#61g</b>）。
///
/// <para><b>为什么与消息码分两张表</b>：定位与消息的**生命周期不同** —— 消息是一句话，
/// 定位是「这条 finding 落在哪」，它自己还会被复制/筛选。分成两张表之后，
/// 「定位要不要翻」这个问题可以单独判（见下）。</para>
///
/// <para><b>⚠ 只有 5 种定位需要码</b>：<c>屏幕 {0}</c> / <c>屏幕 {0} / 控件 {1}</c> /
/// <c>屏幕 {0} / 控件 {1} / {2}</c> / <c>屏幕 {0} / 控件 {1} / 动作</c> / <c>生成代码</c>。
/// 另外 4 种定位**与语言无关**、
/// 刻意**不建码**（进字典是伪造精度）：<c>-</c>（没有定位）、屏幕 id 列表、
/// <c>SecurityConfig.RuntimeUsers[{0}]</c>、<c>SecurityConfig.RuntimeUsers</c>
/// —— 它们是**模型路径**，翻了反而对不上代码。<c>LocationCode == ""</c> 就是"直出"的表示，
/// 并由判据的**白名单**守住（往这一档塞一条中文 ⇒ 判据红）。</para>
///
/// <para><b><c>SCREEN_CONTROL_ACTION</c> 为什么单立一档（阶段 93，闭环登记 #61l）</b>：
/// 悬空引用有**两种载体** —— 控件绑定（定位到<em>属性</em>）与动作目标
/// （<c>HmiAction.Target</c>，定位到<em>动作</em>）。第三格装的是**位置名**，
/// 而"动作"这个词自己是要翻译的 ⇒ 它必须落在**键的固定词**里，
/// 不能当第 3 个实参塞进 <c>SCREEN_CONTROL_PROPERTY</c>（实参是数据，不是查找键；
/// 那样还得引入嵌套实参去翻它）。</para>
/// </summary>
public static class FindingLocationCodes
{
    /// <summary>只到屏幕一级。1 实参 = 屏幕名。</summary>
    public const string SCREEN = "SCREEN";

    /// <summary>到屏幕 + 控件。2 实参 = 屏幕名、控件名。</summary>
    public const string SCREEN_CONTROL = "SCREEN_CONTROL";

    /// <summary>到屏幕 + 控件 + 绑定属性。3 实参 = 屏幕名、控件名、属性名。</summary>
    public const string SCREEN_CONTROL_PROPERTY = "SCREEN_CONTROL_PROPERTY";

    /// <summary>到屏幕 + 控件 + 动作（动作目标悬空）。2 实参 = 屏幕名、控件名；
    /// 「动作」是<b>键自身的固定词</b>，不是实参。</summary>
    public const string SCREEN_CONTROL_ACTION = "SCREEN_CONTROL_ACTION";

    /// <summary>问题在生成代码里，不在某个控件上。0 实参。</summary>
    public const string GENERATED_CODE = "GENERATED_CODE";
}
