using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.Core.Services;

/// <summary>
/// 脚本引擎接口 —— 编辑器侧用于加载、校验和管理脚本。
/// 运行时侧使用 LVGL 的 lv_lua_binding 桥接，生成 C 代码中的初始化调用。
/// </summary>
public interface IScriptEngine
{
    /// <summary>加载脚本到引擎</summary>
    void LoadScript(HmiScript script);

    /// <summary>卸载脚本</summary>
    void UnloadScript(string scriptId);

    /// <summary>校验脚本语法</summary>
    ScriptValidationResult Validate(HmiScript script);

    /// <summary>获取已加载的脚本列表</summary>
    IReadOnlyList<HmiScript> GetLoadedScripts();

    /// <summary>引擎是否已初始化</summary>
    bool IsInitialized { get; }
}

/// <summary>
/// 脚本校验结果。
///
/// <para><b>两条通道，严重级别不同</b>：<see cref="Errors"/> 是阻断级（不修就不能用），
/// <see cref="Warnings"/> 是提示级（有风险，但**不能判红**）。<see cref="IsValid"/> 只看前者。</para>
/// </summary>
public class ScriptValidationResult
{
    /// <summary>是否存在**阻断级**问题 —— 等价于 <c>Errors.Count == 0</c>，**不受警告影响**。</summary>
    public bool IsValid { get; set; } = true;

    /// <summary>阻断级问题：语法错、缺函数定义、名字/内容为空。UI 只展示第一条。</summary>
    public List<ScriptError> Errors { get; set; } = new();

    /// <summary>
    /// **非阻断**提示：发现了风险，但**不许判红**。
    ///
    /// <para>加这条通道是为了「运行前检查」—— 脚本可以语法完全合法、却依赖设备上不存在的
    /// 库 / 字段（<c>bit32</c> / <c>table.pack</c> / <c>math.type</c> / <c>utf8</c> …）。
    /// 这类问题**编辑器的真解析在原理上看不见**（见 <see cref="LuaRejectStage.Runtime"/>：
    /// <c>bit32.band(1, 3)</c> 是合法 Lua 表达式，缺的是运行时的库），
    /// 只能靠单独扫描发现 —— 这就是 <see cref="LuaDialect.CheckRuntimeSymbols"/> 存在的理由。</para>
    ///
    /// <para><b>为什么不塞进 <see cref="Errors"/></b>：设备侧目标 Lua 版本**至今未定**
    /// （<see cref="LuaDialect.Target"/> = <see cref="LuaVersion.Unknown"/>，固件里根本没有 Lua）。
    /// 同一个 <c>bit32</c>，设备是 5.2 就完全正常、是 5.1 就必炸 —— 此时判红等于
    /// **把"待定"当成"错误"**：用户会看到满屏红色、而在设备上跑得起来的脚本，
    /// 然后学会整片忽略校验。分开两条通道，正是为了保住这个区分。</para>
    /// </summary>
    public List<ScriptError> Warnings { get; set; } = new();
}

/// <summary>
/// 一条校验信息 —— <see cref="ScriptValidationResult.Errors"/>（阻断）与
/// <see cref="ScriptValidationResult.Warnings"/>（提示）**共用**这个记录类型，
/// 严重级别由**所在列表**表达，不再另设字段（两个列表本身就是判据，多一个字段只会多一处走样）。
///
/// <para><b><see cref="Message"/> 与 <see cref="Code"/>/<see cref="Args"/> 的分工</b>
/// —— 与 <c>Hmi.Hardware.Contracts.ValidationIssue</c> **同一种形状**，全项目只此一种做法
/// （第二套形状必然与第一套漂移）：</para>
/// <list type="bullet">
///   <item><see cref="Message"/> 是 Core 侧的**中文回落原文**。它必须**始终是一句能独立读懂的话**
///   —— 字典缺键（新规则还没翻译）时界面就显示它，所以不许写成"同上""见前一条"之类。
///   本组测试直接断言它，保证回落这一层自洽；</item>
///   <item>界面实际显示的是按 <c>script.issue.&lt;Code 小写&gt;</c> 查到的**本地化模板**，
///   模板里的 <c>{0}</c>/<c>{1}</c> 由 <see cref="Args"/> 填。</item>
/// </list>
///
/// <para><b><see cref="Line"/> 不烘进 <see cref="Message"/></b>：它已经是结构化字段，
/// 由消费方（界面）统一渲染。一并写进文案会有两个来源，而且会和界面自己的前缀叠成两层
/// —— 这个坑已经踩过一次（见 <c>docs/2026-09-20-core-script-message-localization.md</c>）。</para>
/// </summary>
public class ScriptError
{
    public int Line { get; set; }
    public int Column { get; set; }

    /// <summary>诊断码 —— 键推导（<c>script.issue.&lt;code 小写&gt;</c>）与分派都靠它，**不是**给人看的。</summary>
    public string Code { get; set; } = "";

    /// <summary>中文回落原文（见类型注释）。**不含行号**。</summary>
    public string Message { get; set; } = "";

    /// <summary>本地化模板的占位符实参；<c>null</c> / 空 = 该模板不含占位符。</summary>
    public IReadOnlyList<object>? Args { get; set; }
}
