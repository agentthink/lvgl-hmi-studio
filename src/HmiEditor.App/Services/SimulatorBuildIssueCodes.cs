namespace HmiEditor.App.Services;

/// <summary>
/// **模拟器编译 / 启动**失败诊断码 —— 单一来源（阶段 103 新增，登记 <b>#61c</b> 更正后的重归位）。
///
/// <para><b>为什么单开一族</b>：<c>BuildRunDialog</c> 的「编译并运行」主流程只有**一个**
/// <c>catch (Exception ex)</c>，而它有两个露出点 —— <c>AppendLog(Format(Lang.BuildrunLogError,
/// ex.Message))</c>（在「异常原文直通本地化模板」那张站点面上）与
/// <c>ShowErrors(new[] { ex.Message })</c>（同一个原因、第二条露出点）。改动前那个 <c>catch</c>
/// 接到的中文原文全部来自 <c>InvalidOperationException</c>，而它**没有码** ——
/// <c>SystemFailureCodes</c> 那张表是「探针**实测过才加**」的（阶段 85），
/// <c>InvalidOperationException</c> 不在里面 ⇒ <see cref="ExternalText.FailureReason"/>
/// 的第 ④ 支回落原文 ⇒ **英文界面下这一半照旧中文**。本族把码补上，第 ②‴ 支才第一次接得住它。</para>
///
/// <para><b>⚠ 与 <c>system.issue.</c>（<see cref="SystemFailureCodes"/>）的形状差异，别把两族混起来</b>：
/// 那一族**没有产出方** —— 异常是 BCL 自己抛的，「类型 → 码」的表只能维护在 App 侧，那是全仓库
/// 唯一一处「码不是由产出方声明」的地方；本族**有产出方**（<c>SimulatorBuildService</c> 的两条前置校验、
/// <c>BuildRunDialog</c> 的进程启动），码由产出方随异常一起给出。两者唯一的共同点是
/// **码表与消费点同住 <c>HmiEditor.App</c>** —— 本族是到现在为止唯一一条这样的族。</para>
///
/// <para><b>⚠ 边界：<c>BuildAsync</c> 开头那三行 <c>ArgumentNullException.ThrowIfNull</c>
/// 刻意不码化</b>（<c>project</c> / <c>workDir</c> / <c>lvglDir</c> 的参数校验）—— 那是**调用方
/// 编程错误**，不是用户能撞到的运行条件。与 <see cref="SystemFailureCodes"/> 刻意不码化
/// <c>ArgumentException</c> 家族、<c>HmiEditor.Core.Services.UserAdminIssueCodes</c> 保留
/// <c>ThrowIfNullOrWhiteSpace</c> 是同一条边界。</para>
///
/// <para><b>为什么是 <c>const string</c> 而不是枚举</b>：与另外十余套码
/// （<c>AiIssueCodes</c> / <c>HmiPackageIssueCodes</c> / <c>LoginIssueCodes</c> …）同一形状 ——
/// 守卫靠**反射枚举**这个类型拿「声明了哪些码」，再与「真实产得出的码 ∪ 明确写进
/// <c>NotProducible</c> 的码」做**三态闭包**。反射要求
/// <c>IsLiteral &amp;&amp; FieldType == typeof(string)</c>；⚠ 这个类型里**只许放码** ——
/// 回落原文若也写成常量（像 <c>SimulatorNotConnectedException.Fallback</c> 那样）会被反射当成
/// 一条**假码**，机制面虚高 1。</para>
///
/// <para><b>命名</b>：大写下划线。字典键统一落在 <c>sim.build.issue.</c> 之下，由
/// <see cref="ExternalText.SimulatorBuildIssueKey"/> 推导（**不各写一份** —— 推导规则分家过的亏
/// 本项目吃过，见 <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
/// </summary>
internal static class SimulatorBuildIssueCodes
{
    /// <summary>
    /// LVGL 源码目录无效（<c>lvgl.h</c> 或 <c>CMakeLists.txt</c> 缺失）。
    /// 实参 = 那个目录 —— 「是哪个目录」只有产出方知道，而它**必须**让用户看到
    /// （用户多半是配置里填错了路径）。
    /// </summary>
    internal const string LVGL_SOURCE_INVALID = "LVGL_SOURCE_INVALID";

    /// <summary>
    /// <c>PATH</c>（与 <c>CMAKE_EXECUTABLE</c>）上都找不到 cmake。无实参 ——
    /// 用户需要知道的是「装一个 CMake」，不需要知道我们去哪些目录找过。
    /// </summary>
    internal const string CMAKE_NOT_FOUND = "CMAKE_NOT_FOUND";

    /// <summary>
    /// <c>Process.Start</c> 返回了 <c>null</c>（模拟器进程没起来）。无实参 ——
    /// 此前的原文只有「无法启动模拟器进程」六个字，连可执行文件都没带上
    /// （它就在 <c>SimulatorBuildResult.ExecutablePath</c> 里，属**另一条**恢复路径，
    /// 见 <c>docs/2026-09-23-exception-passthrough-triage.md</c> §103）。
    /// </summary>
    internal const string PROCESS_START_FAILED = "PROCESS_START_FAILED";
}
