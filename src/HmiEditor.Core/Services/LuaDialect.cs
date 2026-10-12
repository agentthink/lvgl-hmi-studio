using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HmiEditor.Core.Services;

/// <summary>Lua 语言版本。</summary>
public enum LuaVersion
{
    /// <summary>尚未确定（**当前设备侧的真实状态**）。</summary>
    Unknown = 0,
    Lua51,
    Lua52,
    Lua53,
    Lua54,
}

/// <summary>一个写法在某版本上不可用时，会在**哪个阶段**暴露 —— 这决定编辑器能不能发现它。</summary>
public enum LuaRejectStage
{
    /// <summary>词法 / 语法层就报错 ⇒ 编辑器的真解析**看得见**（报成语法错误）。</summary>
    Parse,

    /// <summary>语法完全合法，只是那个库 / 字段不存在 ⇒ 编辑器的真解析**看不见**，
    /// 只在设备上运行时炸（<c>attempt to index global 'bit32' (a nil value)</c>）。</summary>
    Runtime,
}

/// <summary>
/// 一条「写法 × 版本」的事实：该写法最早在哪个 Lua 版本可用、以及它在哪个阶段被卡住。
/// </summary>
/// <param name="Name">人类可读的名字（文档与测试用它点名）。</param>
/// <param name="Example">最小示例源码 —— **必须是独立合法的 Lua 语句**，
/// 且**逐字**出现在 <c>tools/probe-lua-dialect.lua</c> 里（有判据守着两者不许漂移）。</param>
/// <param name="Minimum">最早可用的 Lua 版本；<see langword="null"/> = **不随版本单调**，
/// 无法用"最小版本"表达（见 <paramref name="Note"/>）。</param>
/// <param name="Stage">被拒的阶段，见 <see cref="LuaRejectStage"/>。</param>
/// <param name="Note">一句话说明依据 / 例外。</param>
public sealed record LuaFeature(
    string Name,
    string Example,
    LuaVersion? Minimum,
    LuaRejectStage Stage,
    string Note);

/// <summary>
/// 一个**版本敏感的运行时符号** —— 代码里引用了它，而某个 Lua 版本上它并不存在。
///
/// <para><b>它是干什么的</b>：<see cref="LuaFeature"/> 里 <see cref="LuaRejectStage.Runtime"/> 的那几条，
/// 语法完全合法 ⇒ 编辑器的真解析**永远看不见**。这个表就是给它们做的**独立检查通道**
/// （运行前扫描，见 <see cref="LuaDialect.CheckRuntimeSymbols"/>）——
/// 靠"把解析器换成别的版本"是治不了这一类假阴性的。</para>
///
/// <para><b>这不是"白名单"（穷举设备侧允许的全局名）</b>，而是**差异表**。原因：严格白名单要求
/// 静态判定"哪些标识符是全局引用"，而 Lua 是动态语言（局部变量、<c>_ENV</c> 改写、
/// 设备侧注入的 <c>hmi_read</c> / <c>hmi_write</c> 都不在标准库里）—— 误报会多到用户直接忽略警告，
/// 等于没有。差异表只报"确实差的那些"，噪声可控；代价是**完备性靠
/// <see cref="LuaDialect.Features"/> 承接**（有判据守着两表不许漂移）。</para>
///
/// <para><b>已知不覆盖</b>：「某版本起**没有了**」这种反向约束（典型是 5.1 的全局 <c>unpack</c>
/// 在 5.2 被移进 <c>table</c>）用 <see cref="Minimum"/> 表达不了，**本表不收** ——
/// 硬塞进来会造一个说谎的模型。登记在
/// <c>docs/2026-09-20-lua-dialect-alignment.md</c> 的「仍未闭环」里。</para>
/// </summary>
/// <param name="Name">与 <see cref="LuaFeature.Name"/> **逐字一致**（有判据守着），文档与测试都用它点名。</param>
/// <param name="Symbol">待扫的符号：不含 <c>.</c> = 库 / 全局名（<c>bit32</c>）；
/// 含 <c>.</c> = 库字段（<c>table.pack</c>，点两侧允许空白）。</param>
/// <param name="Minimum">最早可用的 Lua 版本。</param>
/// <param name="Note">一句话依据。</param>
public sealed record LuaRuntimeSymbol(
    string Name,
    string Symbol,
    LuaVersion Minimum,
    string Note);

/// <summary>运行前检查对某个符号引用给出的判定。</summary>
public enum LuaSymbolStatus
{
    /// <summary>设备侧目标版本**未定** ⇒ 该符号可能有、也可能没有，**不能下结论**。</summary>
    /// <remarks>这不是"没问题"、也不是"有问题"，是"待定" —— 判成红就成了误报，
    /// 判成绿就成了假阴性。所以它只能是一条提示（见 <see cref="ScriptValidationResult.Warnings"/>）。</remarks>
    Undetermined,

    /// <summary>目标版本**已定**，且该符号在目标上确实不存在 ⇒ 到设备上必然运行时报错。</summary>
    Unavailable,
}

/// <summary>一处符号引用（位置 + 判定）。</summary>
/// <param name="Symbol">命中的符号。</param>
/// <param name="Line">行号（1 起）。</param>
/// <param name="Column">列号（1 起）—— 只是提示，UI 不用。</param>
/// <param name="Status">判定，见 <see cref="LuaSymbolStatus"/>。</param>
public sealed record LuaRuntimeSymbolHit(
    LuaRuntimeSymbol Symbol,
    int Line,
    int Column,
    LuaSymbolStatus Status);

/// <summary>
/// 一条**方言差异改写规则**：解析器报错里出现 <paramref name="Marker"/> ⇒ 这不是普通语法错，
/// 而是"编辑器基准与设备目标可能不一致"。
///
/// <para><b>为什么是 record 而不是元组</b>：它有 4 个字段，其中两个是字符串
/// （<paramref name="Marker"/> 与 <paramref name="Code"/>），元组写法一旦调换顺序，
/// 编译器**不会**报错，只会在运行时静默失配。</para>
/// </summary>
/// <param name="Marker">解析器报错里的片段（<c>near '/'</c>），匹配用。</param>
/// <param name="Code">命中后的诊断 Code —— 界面按 <c>script.issue.&lt;code 小写&gt;</c> 查字典。</param>
/// <param name="Version">该写法属于哪个 Lua 版本。渲染时只把**标签**当参数传出去，
/// 模板里不写死版本号（与 <see cref="LuaDialect.ParserBaselineLabel"/> 同一条纪律）。</param>
/// <param name="Fallback">中文回落原文（含"若目标设备版本更高…"那句尾巴，见
/// <see cref="LuaDialect.Hints"/>）。字典模板渲染后必须与它**逐字一致**，有判据守着。</param>
public sealed record LuaDialectHint(
    string Marker,
    string Code,
    LuaVersion Version,
    string Fallback);

/// <summary>
/// Lua 方言对齐的**单一事实源**。
///
/// <para><b>为什么要它</b>：编辑器能提供的解析能力（MoonSharp 2.0.0 = <b>Lua 5.2</b>）与设备上真正跑的
/// Lua 版本**未必相同**，两者不一致时会出现两个方向的偏差 —— 而这两个方向的危害完全不对称：</para>
/// <list type="bullet">
///   <item><b>假阳性</b>（编辑器报错、设备能跑）：用户会来抱怨，但设备不会出事；</item>
///   <item><b>假阴性</b>（编辑器放行、设备炸）：**用户在编辑器里看到一片绿，到设备上才发现**。
///   这是危险的方向，也是 <see cref="Features"/> 存在的理由。</item>
/// </list>
///
/// <para><b>设备侧目标版本当前不可判</b>（<see cref="Target"/> = <see cref="LuaVersion.Unknown"/>）：
/// 固件里**根本没有 Lua** —— <c>LV_USE_LUA</c> 不是 LVGL 的配置项（LVGL 全树无此符号），
/// 模板引用的 <c>lv_lua_bridge.h</c> / <c>lv_binding_lua</c> 也不在树里；而 Tina SDK 自带的是
/// <b>Lua 5.1.5</b>（<c>firmware/Tina-SDK/package/utils/lua</c>）。两个候选（5.1.5 / 新版 binding）
/// 差一个数量级，不能靠猜。完整取证见 <c>docs/2026-09-20-lua-dialect-alignment.md</c>。</para>
///
/// <para><b>对齐的触发条件只有一处</b>：把 <see cref="Target"/> 从 <see cref="LuaVersion.Unknown"/>
/// 改成真实版本，然后 —— <c>LuaDialectTests.WhenTargetIsDecided_AlignmentMustBeDone</c> 会红，
/// 逼你把新出现的假阴性逐条处置。**不要只改这一个字段就收工。**</para>
/// </summary>
public static class LuaDialect
{
    // ── 两个版本：解析器基准（确定的）与设备目标（未定的）────────────────

    /// <summary>
    /// 解析器基准 —— **编辑器能提供的解析能力上限**，由 MoonSharp 2.0.0 决定（它实现 Lua 5.2）。
    ///
    /// <para>这不是"产品选择"，是**库的硬边界**：换成 5.3/5.4 就得换解析器。
    /// 实测依据（25 个用例）见 <c>docs/2026-09-20-lua-syntax-validation.md</c> §3。</para>
    /// </summary>
    public static readonly LuaVersion ParserBaseline = LuaVersion.Lua52;

    /// <summary>
    /// 设备侧目标版本。**当前 <see cref="LuaVersion.Unknown"/>**（固件没有 Lua，无从对齐）。
    /// 这是"对齐"唯一的开关 —— 但改它只是**开始**对齐，不是完成（见类型注释）。
    /// </summary>
    public static readonly LuaVersion Target = LuaVersion.Unknown;

    /// <summary>
    /// 已知候选目标及其证据。**区分"候选"与"已定"** —— 列在这里不代表选了它。
    /// </summary>
    public static readonly IReadOnlyList<(LuaVersion Version, string Evidence)> Candidates = new[]
    {
        (LuaVersion.Lua51,
         "Tina SDK 自带 Lua 5.1.5：firmware/Tina-SDK/package/utils/lua（源码里 lua.h 的 LUA_RELEASE）"),
    };

    // ── 版本标签 ─────────────────────────────────────────────────────

    /// <summary>解析器基准的可读标签（**所有面向用户的方言文案都必须由它派生**）。</summary>
    public static string ParserBaselineLabel => LabelOf(ParserBaseline);

    /// <summary>把版本渲染成可读标签；<see cref="LuaVersion.Unknown"/> ⇒ 「未确定」。</summary>
    public static string LabelOf(LuaVersion v) => v switch
    {
        LuaVersion.Lua51 => "Lua 5.1",
        LuaVersion.Lua52 => "Lua 5.2",
        LuaVersion.Lua53 => "Lua 5.3",
        LuaVersion.Lua54 => "Lua 5.4",
        _ => "未确定",
    };

    // ── 事实表 ───────────────────────────────────────────────────────

    /// <summary>
    /// 「写法 × 版本」事实表。
    ///
    /// <para><b>数据来源（两条独立证据，都不是推测）：</b></para>
    /// <list type="number">
    ///   <item>MoonSharp 2.0.0 实测（25 用例探针，见 lua-syntax-validation 专题 §3）；</item>
    ///   <item>Tina SDK 自带的 Lua 5.1.5 **源码 + host 二进制**真跑
    ///   （<c>tools/probe-lua-dialect.lua</c>，命令见方言对齐专题）。</item>
    /// </list>
    ///
    /// <para><b>为什么 <c>Stage</c> 要单独记</b>：<see cref="LuaRejectStage.Runtime"/> 那一类
    /// 语法完全合法，编辑器的真解析**永远看不见** —— 无论基准怎么调都看不见。
    /// 也就是说这类假阴性**不可能靠"对齐版本"解决**，只能靠别的手段（库白名单 / 运行前检查）。
    /// 把 Stage 记下来，是为了不让这件事被误认为"等固件定了就好了"。</para>
    /// </summary>
    public static readonly IReadOnlyList<LuaFeature> Features = new[]
    {
        // 5.2 新增，5.1 没有 —— 而 SDK 自带的正是 5.1.5
        new LuaFeature("goto / 标签", "goto done ::done::", LuaVersion.Lua52, LuaRejectStage.Parse,
            "5.1 关键字表里没有 goto（llex.c 的 luaX_tokens），5.1 上实测 '=' expected near 'lbl'"),
        new LuaFeature("bit32 库", "bit32.band(1, 3)", LuaVersion.Lua52, LuaRejectStage.Runtime,
            "5.1.5 源码全文无 bit32；语法合法故编辑器看不见，运行时 attempt to index global 'bit32'"),
        new LuaFeature("table.pack", "table.pack(1, 2)", LuaVersion.Lua52, LuaRejectStage.Runtime,
            "5.1.5 源码无 table.pack（同版本的对应物是全局 unpack）"),
        new LuaFeature("table.unpack", "table.unpack({1, 2})", LuaVersion.Lua52, LuaRejectStage.Runtime,
            "5.1.5 里 unpack 是全局函数（lbaselib.c 的 {\"unpack\", luaB_unpack}），没有 table.unpack"),

        // 5.3
        new LuaFeature("整除运算符 //", "local a = 7 // 2", LuaVersion.Lua53, LuaRejectStage.Parse,
            "MoonSharp 与 5.1.5 都报 unexpected symbol near '/'"),
        new LuaFeature("按位运算符 & | ~ << >>", "local a = 1 & 3", LuaVersion.Lua53, LuaRejectStage.Parse,
            "同上；这组是**假阳性**（编辑器报错、设备能跑）的典型"),
        new LuaFeature("math.type", "math.type(1)", LuaVersion.Lua53, LuaRejectStage.Runtime,
            "5.3 新增函数；语法合法 ⇒ 编辑器看不见"),
        new LuaFeature("utf8 库", "utf8.len(\"x\")", LuaVersion.Lua53, LuaRejectStage.Runtime,
            "5.3 新增库；语法合法 ⇒ 编辑器看不见"),

        // 5.4
        new LuaFeature("<const> / <close> 属性", "local x <const> = 1", LuaVersion.Lua54, LuaRejectStage.Parse,
            "5.4 属性语法；两个解析器都报 unexpected symbol near '<'"),

        // 非版本单调 —— 不许给它编一个"最小版本"充数
        new LuaFeature("中文标识符", "local 变量 = 1", null, LuaRejectStage.Parse,
            "**不是版本特性**：MoonSharp 接受（未按 C locale 限制），SDK 的 5.1.5 词法层就拒"
            + "（llex.c 用裸 isalpha 按字节判、全仓无 setlocale）⇒ 属 locale / 构建配置差异，"
            + "在 5.2 / 5.3 / 5.4 上的行为**未实测**，因此不给最小版本"),
    };

    /// <summary>不随版本单调的项（<see cref="LuaFeature.Minimum"/> 为 <see langword="null"/>）。</summary>
    public static IReadOnlyList<LuaFeature> NonMonotone
        => Features.Where(f => f.Minimum is null).ToList();

    // ── 偏差计算 ─────────────────────────────────────────────────────

    /// <summary>
    /// **假阴性**：编辑器**会放行**、而 <paramref name="target"/> 上**不可用**的写法。
    /// 危险方向 —— 用户在编辑器里看到全绿，到设备上才炸。
    ///
    /// <para>「编辑器放行」的判据：对 <see cref="LuaRejectStage.Parse"/> 项 = 基准能解析
    /// （<c>Minimum &lt;= 基准</c>）；对 <see cref="LuaRejectStage.Runtime"/> 项 = **恒为真**
    /// （语法永远合法，解析器永远不报）。</para>
    /// </summary>
    public static IReadOnlyList<LuaFeature> FalseNegatives(LuaVersion target) => Gaps(target, falseNegatives: true);

    /// <summary>
    /// **假阳性**：编辑器**会拒**、而 <paramref name="target"/> 上能跑的写法（已知误报，用户可见）。
    /// 危害小于假阴性，但会让用户不信任校验。
    /// </summary>
    public static IReadOnlyList<LuaFeature> FalsePositives(LuaVersion target) => Gaps(target, falseNegatives: false);

    private static IReadOnlyList<LuaFeature> Gaps(LuaVersion target, bool falseNegatives)
    {
        if (target == LuaVersion.Unknown || target == ParserBaseline) return Array.Empty<LuaFeature>();

        return Features.Where(f =>
        {
            if (f.Minimum is not { } min) return false;                 // 非单调项不进这套算法
            bool baselineAccepts = f.Stage == LuaRejectStage.Runtime    // 语法合法 ⇒ 解析器照收
                                   || min <= ParserBaseline;
            bool targetAccepts = min <= target;
            return falseNegatives ? baselineAccepts && !targetAccepts
                                  : !baselineAccepts && targetAccepts;
        }).ToList();
    }

    // ── 运行前检查：设备侧运行时符号 ──────────────────────────────────
    //
    //  这一节是**独立机制**，不是"对齐版本"的一部分。理由就写在 LuaRuntimeSymbol 的类型注释里：
    //  Runtime 那几条假阴性**语法完全合法**，解析器在原理上看不见 ——
    //  换解析器、调 Minimum、改基准，全都救不了它，只能单独扫。

    /// <summary>
    /// 版本敏感的运行时符号表。与 <see cref="Features"/> 里 <see cref="LuaRejectStage.Runtime"/>
    /// 的条目**一一对应** —— <see cref="LuaRuntimeSymbol.Name"/> 逐字一致、
    /// <see cref="LuaRuntimeSymbol.Minimum"/> 也一致，两表不许漂移（有判据守着）。
    /// </summary>
    public static readonly IReadOnlyList<LuaRuntimeSymbol> RuntimeSymbols = new[]
    {
        new LuaRuntimeSymbol("bit32 库", "bit32", LuaVersion.Lua52,
            "5.2 新增库；SDK 自带的 5.1.5 源码全文无 bit32"),
        new LuaRuntimeSymbol("table.pack", "table.pack", LuaVersion.Lua52,
            "5.2 新增；5.1 的对应物是全局 unpack"),
        new LuaRuntimeSymbol("table.unpack", "table.unpack", LuaVersion.Lua52,
            "5.2 起才有；5.1 里 unpack 是全局函数（lbaselib.c 的 luaB_unpack）"),
        new LuaRuntimeSymbol("math.type", "math.type", LuaVersion.Lua53,
            "5.3 新增函数 —— math 库本身 5.1 就有，所以只能按「库.字段」判，不能把 math 整个算进去"),
        new LuaRuntimeSymbol("utf8 库", "utf8", LuaVersion.Lua53,
            "5.3 新增库"),
    };

    // ── 运行前检查的 Code 族 ──
    //
    //  Code 分**细**到两态，不是"两个 Code 显得整齐"：这两态的话术方向相反
    //  （一个说"确定没有"、一个说"暂时不知道"），必须各自有一条本地化模板。
    //  合用一个 Code 就只能把区分塞进 Args，而 Args 是**填模板用的**，不是给程序分派用的。

    /// <summary>运行前检查 Code 族的**前缀** —— 消费方据此判断"这是不是运行前提示"，
    /// 见 <see cref="IsRuntimeSymbolWarning"/>。</summary>
    public const string RuntimeSymbolWarningCodePrefix = "LUA_RUNTIME_SYMBOL_";

    /// <summary>目标**已定**、且该符号在目标上确实不存在 ⇒ 到设备上必然运行时报错。</summary>
    public const string RuntimeSymbolUnavailableCode = RuntimeSymbolWarningCodePrefix + "UNAVAILABLE";

    /// <summary>目标**未定** ⇒ 无法判定（**当前设备的真实状态**，见 <see cref="Target"/>）。</summary>
    public const string RuntimeSymbolUndeterminedCode = RuntimeSymbolWarningCodePrefix + "UNDETERMINED";

    /// <summary>该 Code 是否属于运行前检查族（断言用 <c>==</c> 会随 Code 细分而失效，判族才稳）。</summary>
    public static bool IsRuntimeSymbolWarning(string? code)
        => code is not null && code.StartsWith(RuntimeSymbolWarningCodePrefix, StringComparison.Ordinal);

    private static readonly IReadOnlyDictionary<string, Regex> SymbolPatterns = BuildSymbolPatterns();

    private static IReadOnlyDictionary<string, Regex> BuildSymbolPatterns()
    {
        var map = new Dictionary<string, Regex>(StringComparer.Ordinal);
        foreach (var s in RuntimeSymbols)
            map[s.Symbol] = new Regex(BuildSymbolPattern(s.Symbol));
        return map;
    }

    /// <summary>
    /// 一个符号的扫描模式。
    /// <list type="bullet">
    ///   <item>点两侧允许空白（<c>table . pack</c> 也是合法 Lua）；</item>
    ///   <item>前面不能是词字符 / <c>.</c> / <c>:</c> —— 挡住 <c>x.bit32</c>、<c>x:bit32()</c>、<c>_bit32</c>；</item>
    ///   <item>后面用 <c>\b</c> 挡住 <c>bit32x</c>。</item>
    /// </list>
    /// </summary>
    private static string BuildSymbolPattern(string symbol)
    {
        string body = string.Join(@"\s*\.\s*", symbol.Split('.').Select(Regex.Escape));
        return $@"(?<![\w.:]){body}\b";
    }

    /// <summary>
    /// **运行前检查** —— 扫出源码里对 <see cref="RuntimeSymbols"/> 的引用。
    ///
    /// <para><b>判定规则</b>（<paramref name="target"/> = 设备侧目标版本）：</para>
    /// <list type="bullet">
    ///   <item>目标已定、且符号可用（<c>Minimum &lt;= target</c>）⇒ **不报**；</item>
    ///   <item>目标已定、且符号不可用 ⇒ <see cref="LuaSymbolStatus.Unavailable"/>（到设备上必然炸）；</item>
    ///   <item>目标**未定** ⇒ <see cref="LuaSymbolStatus.Undetermined"/>，**照报**。</item>
    /// </list>
    ///
    /// <para><b>为什么未定时要照报</b>：不报 ＝ 把"不知道"当成"没问题"，而那正是本轮要治的假阴性；
    /// 也不能判红（那是误报）。所以它只产出**提示**，由调用方放进
    /// <see cref="ScriptValidationResult.Warnings"/>。目标一旦确定，这一层会**自动收敛**成
    /// "只报真的不可用的"（有判据钉着这个收敛行为）。</para>
    ///
    /// <para><b>扫描前先做两件事</b>：剥注释（复用 <see cref="LuaSyntaxValidator.StripComments"/>，
    /// 行号不漂移）、遮蔽字符串字面量 —— <c>local s = "bit32.band"</c> 里的 <c>bit32</c>
    /// **不是引用**，报它纯属噪声。</para>
    ///
    /// <para><b>已知假阳性</b>（因为是提示，不会把脚本判红）：<c>local bit32 = {}</c> 这类局部
    /// 变量遮蔽照报、<c>{ bit32 = 1 }</c> 的表键照报。取舍是**宁可多报不可漏报** ——
    /// 漏报正是这个机制要治的病。</para>
    /// </summary>
    public static IReadOnlyList<LuaRuntimeSymbolHit> CheckRuntimeSymbols(string? code, LuaVersion target)
    {
        if (string.IsNullOrWhiteSpace(code)) return Array.Empty<LuaRuntimeSymbolHit>();

        string scannable = MaskStringLiterals(LuaSyntaxValidator.StripComments(code));
        if (scannable.Length == 0) return Array.Empty<LuaRuntimeSymbolHit>();

        var hits = new List<LuaRuntimeSymbolHit>();

        foreach (var symbol in RuntimeSymbols)
        {
            if (target != LuaVersion.Unknown && symbol.Minimum <= target) continue;   // 可用 ⇒ 不报

            var status = target == LuaVersion.Unknown
                ? LuaSymbolStatus.Undetermined
                : LuaSymbolStatus.Unavailable;

            foreach (Match m in SymbolPatterns[symbol.Symbol].Matches(scannable))
            {
                var (line, column) = PositionOf(scannable, m.Index);
                hits.Add(new LuaRuntimeSymbolHit(symbol, line, column, status));
            }
        }

        return hits.OrderBy(h => h.Line).ThenBy(h => h.Column).ToList();
    }

    /// <summary>
    /// 把一处命中拆成「诊断 Code + 模板实参」—— 界面据此查
    /// <c>script.issue.&lt;code 小写&gt;</c> 并填 <c>{0}</c>/<c>{1}</c>。
    ///
    /// <para><b>版本标签一律派生后当参数传</b>，模板与回落文案里都不写死
    /// （与 <see cref="Hints"/> 同一条纪律）—— 而且标签本身是**语言无关**的
    /// （"Lua 5.2" 译成别的语言时也不变），所以它天生适合走参数而不是进模板。</para>
    /// </summary>
    public static (string Code, object[] Args) DescribeHit(LuaRuntimeSymbolHit hit, LuaVersion target)
        => hit.Status == LuaSymbolStatus.Unavailable
            ? (RuntimeSymbolUnavailableCode, new object[] { hit.Symbol.Name, LabelOf(target) })
            : (RuntimeSymbolUndeterminedCode, new object[] { hit.Symbol.Name, LabelOf(hit.Symbol.Minimum) });

    /// <summary>
    /// 把一处命中渲染成给用户看的**中文回落**文本。版本标签一律由 <see cref="LabelOf"/> 派生，
    /// **不许在这写死版本号**（与 <see cref="Hints"/> 同一条纪律）。
    ///
    /// <para><b>刻意写短</b>：它会被塞进脚本编辑器那一行校验状态栏（首条 + 计数），
    /// 长了就看不全。所以这里只给"哪个符号 + 什么结论"，判据依据留在
    /// <see cref="LuaRuntimeSymbol.Note"/> 与专题文档里。</para>
    ///
    /// <para><b>界面显示的不是它</b>：界面走 <see cref="DescribeHit"/> 查字典模板，字典缺键才落到这里。
    /// 所以这里的文本必须与字典模板（占位符换成实参后）**逐字一致** ——
    /// 有判据守着（<c>ScriptIssueLocalizationGuardTests</c>），改一边必须改另一边。</para>
    ///
    /// <para><b>行号刻意不在这里</b>：<see cref="LuaRuntimeSymbolHit.Line"/> 是结构化字段，
    /// 由消费方统一渲染。写进文案会与界面自己的前缀叠成两层，而且行号就有了两个来源。</para>
    /// </summary>
    public static string ExplainHit(LuaRuntimeSymbolHit hit, LuaVersion target)
    {
        var (code, args) = DescribeHit(hit, target);
        string symbol = (string)args[0];
        string version = (string)args[1];

        return code == RuntimeSymbolUnavailableCode
            // 目标已定且该符号确实没有 ⇒ 这是**确定**会炸，措辞不含糊。
            ? $"{symbol} 在设备目标 {version} 上不存在，到设备上会运行时报错"
            // 未定 ⇒ **不能下结论**。两个方向的误读都要堵上：既不能说"一定有错"，
            // 也不能让用户以为"一定没事"，所以可忽略的条件一并给出。
            : $"{symbol} 需要 {version} 或更高，设备目标未定、暂无法判定"
              + $"（设备若为 {version} 或更高则无此问题）";
    }

    /// <summary>
    /// 把字符串字面量的**内容**换成空格（引号本身与换行保留）——
    /// 长度与行号都不漂移，但 <c>"bit32.band"</c> 里的符号不再被当成引用。
    ///
    /// <para>输入**必须是已剥注释的源码**（<see cref="LuaSyntaxValidator.StripComments"/>）——
    /// 那一层已经正确跳过了字符串里的 <c>--</c>，这里只管引号。</para>
    ///
    /// <para><b>已知不覆盖</b>：Lua 的长字符串 <c>[[bit32.band]]</c> 里的符号仍会被报
    /// （<see cref="LuaSyntaxValidator.StripComments"/> 也不处理长字符串）。指向"宁可多报"那一侧。</para>
    /// </summary>
    private static string MaskStringLiterals(string code)
    {
        var chars = code.ToCharArray();
        int i = 0, n = chars.Length;

        while (i < n)
        {
            if (chars[i] is not ('"' or '\'')) { i++; continue; }

            char quote = chars[i];
            i++;
            while (i < n)
            {
                if (chars[i] == '\\' && i + 1 < n)
                {
                    chars[i] = ' ';
                    // 转义续行（反斜杠后直接换行）时必须保留那个换行，否则行号会漂
                    if (chars[i + 1] != '\n') chars[i + 1] = ' ';
                    i += 2;
                    continue;
                }
                if (chars[i] == quote) { i++; break; }
                if (chars[i] != '\n') chars[i] = ' ';
                i++;
            }
        }

        return new string(chars);
    }

    /// <summary>把字符下标换算成 1 起的（行, 列）。</summary>
    private static (int Line, int Column) PositionOf(string text, int index)
    {
        int line = 1, lineStart = 0;
        int stop = Math.Min(index, text.Length);

        for (int i = 0; i < stop; i++)
        {
            if (text[i] == '\n') { line++; lineStart = i + 1; }
        }

        return (line, index - lineStart + 1);
    }

    // ── 面向用户的方言文案 ────────────────────────────────────────────

    /// <summary>
    /// 方言差异改写表：<c>Marker</c> 是解析器报错里的片段，<c>Fallback</c> 是给用户看的中文回落，
    /// <c>Code</c> 是界面查字典用的诊断码。
    ///
    /// <para><b>文案里的基准版本必须由 <see cref="ParserBaselineLabel"/> 派生</b>，
    /// 不许写死 —— 否则"基准换了、文案没换"会静默（有判据守着，见 <c>LuaDialectTests</c>）。</para>
    ///
    /// <para><b>文案里一律要说"若目标设备版本更高，此处应视为误报"</b>：因为目标未定，
    /// 编辑器只能对**自己这一侧**的判定负责，不能替设备下结论（假阴性的方向更危险，
    /// 由 <see cref="FalseNegatives"/> 单独承接，不靠这句安慰话）。</para>
    /// </summary>
    public static IReadOnlyList<LuaDialectHint> Hints { get; } = BuildHints();

    /// <summary>
    /// 方言提示统一带的尾巴。
    ///
    /// <para><b>为什么收成一个常量</b>：6 条提示都要带它，散在 6 处必然有一处漏、而漏了没人发现
    /// —— 而这句话是"编辑器不替设备下结论"的**唯一表达**，漏掉就等于把误报说成了真错。
    /// 它同时也是字典模板的组成部分（两边内容必须一致，有判据守着）。</para>
    /// </summary>
    public const string HintTail = "；若目标设备的 Lua 版本更高，此处应视为误报";

    private static LuaDialectHint[] BuildHints()
    {
        string v = ParserBaselineLabel;
        string v53 = LabelOf(LuaVersion.Lua53);
        string v54 = LabelOf(LuaVersion.Lua54);

        return new[]
        {
            new LuaDialectHint("near '/'", LuaSyntaxValidator.SyntaxOpIntDivCode, LuaVersion.Lua53,
                $"此写法属于 {v53} 的整除运算符 //（编辑器校验基于 {v}）{HintTail}"),
            new LuaDialectHint("near '&'", LuaSyntaxValidator.SyntaxOpBitAndCode, LuaVersion.Lua53,
                $"此写法属于 {v53} 的按位与 &（编辑器校验基于 {v}）{HintTail}"),
            new LuaDialectHint("near '|'", LuaSyntaxValidator.SyntaxOpBitOrCode, LuaVersion.Lua53,
                $"此写法属于 {v53} 的按位或 |（编辑器校验基于 {v}）{HintTail}"),
            new LuaDialectHint("near '~'", LuaSyntaxValidator.SyntaxOpBitNotCode, LuaVersion.Lua53,
                $"此写法属于 {v53} 的按位取反 / 异或 ~（编辑器校验基于 {v}）{HintTail}"),
            new LuaDialectHint("near '<'", LuaSyntaxValidator.SyntaxAngleAttrOrShiftLeftCode, LuaVersion.Lua54,
                $"此写法可能是 {v54} 的属性（如 local x <const> = 1）或左移 <<（编辑器校验基于 {v}）{HintTail}"),
            new LuaDialectHint("near '>'", LuaSyntaxValidator.SyntaxOpShiftRightCode, LuaVersion.Lua53,
                $"此写法属于 {v53} 的右移 >>（编辑器校验基于 {v}）{HintTail}"),
        };
    }
}
