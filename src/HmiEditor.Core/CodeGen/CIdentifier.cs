namespace HmiEditor.Core.CodeGen;

/// <summary>
/// C 标识符净化 —— 全仓唯一实现。
///
/// <para><b>为什么值得单独成类：</b>资源类符号（<c>IMG_PATH_*</c> / <c>img_*</c> /
/// <c>font_*</c> / <c>FONT_PATH_*</c>）由**两条独立链路**共同引用：
/// <c>CodeGenerator.GenerateSimulatorModule</c> 产出 <c>generated_hmi.c</c>，
/// <c>SimulatorProjectExporter.Export</c> 产出 <c>main.c</c> / <c>generated_images.c</c> /
/// <c>generated_fonts.c</c>。两条链路必须给出**逐字符相同**的符号名，否则症状是
/// 「编译期找不到符号 / 链接期未定义」，而原因在另一条链路里，排查很绕。</para>
///
/// <para><b>历史（2026-09-14 统一）：</b>同一段净化逻辑此前有三份实现 ——
/// <c>CodeGenerator.SanitizeIdentifier</c>、<c>CodeGenerator.SanitizeAssetId</c>、
/// <c>SimulatorProjectExporter.SanitizeIdentifier</c>。实现体逐字相同，只有
/// <c>null</c>/空输入的兜底值不同（<c>"anim"</c> / <c>"unknown"</c>），
/// 跨模块符号契约**靠巧合维持** —— 谁把同名的两个函数"顺手对齐"，
/// <c>generated_hmi.c</c> 与 <c>generated_images.c</c> 里的宏名就会错位。
/// 现在两条链路都从这里取，契约由 <c>CodeGenAssetSymbolContractTests</c> 守卫。</para>
///
/// <para><b>第二类用途：控件类生成符号（2026-09-15 收口）。</b>
/// <c>obj_*</c>、<c>create_screen_*</c>、<c>_rec_*</c>、<c>_logbuf_*</c>、<c>label_*</c> …
/// 这一族符号同样由生成器**在一处声明、在另一处引用**（有的跨 partial、跨文件、
/// 甚至跨编译单元 —— <c>SimulatorProjectExporter</c> 也要拼 <c>create_screen_&lt;id&gt;</c>）。
/// 收口前全仓有 116 处直接写 <c>$"前缀_{x.Id}"</c>，等于把"声明与引用必须逐字符相同"
/// 这条不变量交给 116 个调用点各自维持。</para>
/// </summary>
public static class CIdentifier
{
    /// <summary>资源（图片 / 字体）id 的兜底值。</summary>
    public const string AssetFallback = "unknown";

    /// <summary>
    /// 控件 / 动画 / 动作 id 的兜底值。
    /// <para>注意 <c>"anim"</c> 是历史取值（最早只有动画 id 走这条路径）。对"控件 id 为空"
    /// 的输入给出 <c>"anim"</c> 并不贴切，但**改它会改变既有产物**，属于独立议题，
    /// 不在统一实现时顺手改。</para>
    /// </summary>
    public const string ControlSymbolFallback = "anim";

    /// <summary>
    /// 资源 id → C 符号片段（即 <c>IMG_PATH_<b>x</b></c> / <c>img_<b>x</b></c> /
    /// <c>font_<b>x</b></c> 里的 <c>x</c>）。空输入 → <see cref="AssetFallback"/>。
    /// </summary>
    public static string ForAsset(string? id) => Sanitize(id, AssetFallback);

    /// <summary>
    /// 控件 / 动画 / 动作 id → 生成符号片段（<c>obj_<b>x</b></c>、<c>_slider_<b>x</b>_val_cb</c>）。
    /// 空输入 → <see cref="ControlSymbolFallback"/>。
    /// </summary>
    public static string ForControlSymbol(string? id) => Sanitize(id, ControlSymbolFallback);

    /// <summary>
    /// 控件对象指针的**完整符号名**：<c>obj_<b>&lt;净化id&gt;</b></c>。
    ///
    /// <para><b>必须走这里，不要手写 <c>$"obj_{control.Id}"</c></b>：该符号由
    /// <c>CodeGenerator.Screen.cs</c> **声明**、由 <c>RefreshBindings</c> /
    /// <c>VisualStates</c> / <c>ControlParameters</c> 等**多处引用**，两条路径必须给出
    /// 逐字符相同的名字。历史上声明侧写裸 <c>control.Id</c>，而刷新绑定侧走
    /// <see cref="ForControlSymbol"/> —— 于是 id 里含 <c>-</c> / 空格时，声明是
    /// <c>obj_a-b</c>（非法 C），引用是 <c>obj_a_b</c>，符号直接对不上。
    /// 设计器生成的 id 是 8 位十六进制，所以这条一直没暴露（属**潜在**缺陷），
    /// 但"同一个符号名有两套构造方式"本身就是 <see cref="CIdentifier"/> 要消灭的东西。</para>
    /// </summary>
    public static string ControlObject(string? controlId) => Prefixed("obj_", controlId);

    // ───────────────────────── 带前缀的生成符号 ─────────────────────────
    //
    // 下面这组方法存在的唯一理由：**同一个符号名只能有一种构造方式**。
    //
    // 这些符号都由生成器在**一处声明**、在**另一处引用**（有的还跨文件、跨编译单元）。
    // 只要两处的拼接写法不同，出现非法 id 时就会一边产出 `foo_a-b`（非法 C）、
    // 另一边产出 `foo_a_b`，症状是"编译期找不到符号"而根因在别处。
    //
    // 纪律：**不要在生成器里再写 `$"label_{control.Id}"` 这类插值**。
    // 前缀字面量应当只出现在本文件里（函数内局部、且只在一个方法内出现的前缀，
    // 允许直接用 <see cref="Prefixed"/> 传前缀 —— 不存在"两处拼接"的风险）。

    /// <summary>
    /// 带前缀的生成符号：<c>prefix + ForControlSymbol(id)</c>。
    /// 适用于**函数内局部**符号（声明与引用同在一个发射块内）—— 前缀出现在调用点即可。
    /// </summary>
    public static string Prefixed(string prefix, string? id) => prefix + ForControlSymbol(id);

    // ── 屏幕 / 图层（跨文件：CodeGenerator ↔ SharedLibrary ↔ SimulatorProjectExporter） ──

    /// <summary>屏幕工厂函数名：<c>create_screen_&lt;id&gt;</c>。非 static，供模拟器 main.c 等外部编译单元链接。</summary>
    public static string ScreenFactory(string? screenId) => Prefixed("create_screen_", screenId);

    /// <summary>屏幕内部工厂（图层形态）：<c>create_screen_&lt;id&gt;_inner</c>。static，文件内共享。</summary>
    public static string ScreenFactoryInner(string? screenId) => ScreenFactory(screenId) + "_inner";

    /// <summary>自适应布局回调：<c>adaptive_layout_&lt;id&gt;</c>（声明与调用分处两个方法）。</summary>
    public static string AdaptiveLayoutFn(string? screenId) => Prefixed("adaptive_layout_", screenId);

    /// <summary>main() 里的屏幕对象局部变量名：<c>screen_&lt;id&gt;</c>。</summary>
    public static string ScreenObject(string? screenId) => Prefixed("screen_", screenId);

    /// <summary>main() 里的图层对象局部变量名：<c>layer_&lt;id&gt;</c>。</summary>
    public static string LayerObject(string? layerId) => Prefixed("layer_", layerId);

    // ── 报警（文件级 static + 回调函数，跨函数） ──

    /// <summary>报警运行时记录：<c>_rec_&lt;id&gt;</c>（文件级 static，跨函数读写）。</summary>
    public static string AlarmRecord(string? alarmId) => Prefixed("_rec_", alarmId);

    /// <summary>报警判定函数：<c>check_alarm_&lt;id&gt;</c>（先声明后定义，另一处调用）。</summary>
    public static string AlarmCheckFn(string? alarmId) => Prefixed("check_alarm_", alarmId);

    // ── 数据记录器（文件级 static + 接口函数） ──

    /// <summary>环形缓冲：<c>_logbuf_&lt;id&gt;</c>（文件级 static 数组）。</summary>
    public static string LogBuffer(string? loggerId) => Prefixed("_logbuf_", loggerId);

    /// <summary>写指针：<c>_logwr_&lt;id&gt;</c>。</summary>
    public static string LogWriteIndex(string? loggerId) => Prefixed("_logwr_", loggerId);

    /// <summary>有效样本数：<c>_logcnt_&lt;id&gt;</c>。</summary>
    public static string LogCount(string? loggerId) => Prefixed("_logcnt_", loggerId);

    /// <summary>缓冲已写满标志：<c>_logfull_&lt;id&gt;</c>。</summary>
    public static string LogFullFlag(string? loggerId) => Prefixed("_logfull_", loggerId);

    /// <summary>记录器初始化函数：<c>datalog_init_&lt;id&gt;</c>。</summary>
    public static string DataLogInitFn(string? loggerId) => Prefixed("datalog_init_", loggerId);

    /// <summary>采样函数：<c>datalog_sample_&lt;id&gt;</c>。</summary>
    public static string DataLogSampleFn(string? loggerId) => Prefixed("datalog_sample_", loggerId);

    /// <summary>CSV 导出函数：<c>datalog_export_csv_&lt;id&gt;</c>（定义在 Trend、分派在 SharedLibrary）。</summary>
    public static string DataLogExportCsvFn(string? loggerId) => Prefixed("datalog_export_csv_", loggerId);

    // ── 趋势图（文件级 static + 接口函数） ──

    /// <summary>图表对象缓存指针：<c>_chart_&lt;id&gt;</c>（文件级 static，屏幕销毁时要清空）。</summary>
    public static string ChartObject(string? chartId) => Prefixed("_chart_", chartId);

    /// <summary>曲线序列数组：<c>_series_&lt;id&gt;</c>。</summary>
    public static string ChartSeries(string? chartId) => Prefixed("_series_", chartId);

    /// <summary>图表初始化函数：<c>trend_init_&lt;id&gt;</c>。</summary>
    public static string TrendInitFn(string? chartId) => Prefixed("trend_init_", chartId);

    /// <summary>图表刷新函数：<c>trend_update_&lt;id&gt;</c>。</summary>
    public static string TrendUpdateFn(string? chartId) => Prefixed("trend_update_", chartId);

    /// <summary>
    /// 把非标识符字符替换为 <c>_</c>；<c>null</c> 或空串返回 <paramref name="fallback"/>。
    ///
    /// <para><b>行为约定（勿轻改）：</b>使用的是 <see cref="char.IsLetterOrDigit(char)"/>，
    /// 它把中文等 Unicode 字母判为"合法"，因此中文 id 会**原样保留**（如 <c>图片2</c>）。
    /// 严格按 C 标准这需要 UCN，但既有产物依赖该行为，改变会牵动全部生成结果。</para>
    /// </summary>
    public static string Sanitize(string? input, string fallback)
    {
        if (string.IsNullOrEmpty(input)) return fallback;

        var result = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            result[i] = (char.IsLetterOrDigit(c) || c == '_') ? c : '_';
        }
        return new string(result);
    }
}
