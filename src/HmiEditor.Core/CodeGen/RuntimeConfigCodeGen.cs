using System.Text;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 生成设备端运行时参数头 <c>generated_runtime.h</c> —— <see cref="RuntimeConfig"/> 里
/// **固件已有接收位点**的那部分参数的唯一下沉产物。
///
/// <para><b>为什么需要它：</b><see cref="RuntimeConfig"/> 的十余个字段在编辑器里有完整 UI
/// （工程设置 → 运行时）、其中一部分还能被 AI 工具改，但**此前整条链路上只有
/// <see cref="RuntimeConfig.BootScreenId"/> 一个走到了设备**（它在生成代码里决定
/// <c>lv_screen_load</c> 的目标）。其余字段改完只写进工程 JSON，设备行为纹丝不动 ——
/// 用户看到的是"配了没用"。本生成器把其中固件已有接收位点的两个下沉成宏。</para>
///
/// <para><b>固件的消费方式是 <c>__has_include</c></b>（与 <c>generated_security.h</c> /
/// <c>generated_comm.h</c> 同一套路）：头缺失时固件回退到 <c>runtime_config.h</c> /
/// <c>hmi_runtime.h</c> 里的历史默认值 —— 于是没带这份头的导出工程仍能编译、行为与历史一致，
/// 带上就自动生效。</para>
///
/// <para><b>本轮只下沉两个字段</b>，判据是"固件端已有接收位点"：
/// <list type="bullet">
///   <item><see cref="RuntimeConfig.ScanIntervalMs"/> → <c>HMI_POLL_INTERVAL_MS</c>：
///         固件 <c>comm_core.c</c> 拿它做采集循环休眠，此前硬编码在 <c>runtime_config.h</c>。</item>
///   <item><see cref="RuntimeConfig.LogLevel"/> → <c>HMI_LOG_LEVEL</c>：固件日志宏族
///         此前没有门槛，全部无条件输出。</item>
/// </list>
/// 其余字段（看门狗 / 心跳 / 崩溃转储 / 任务数 / 历史存储路径）在固件侧**没有对应机制**，
/// 属于"要先立功能"而非"补一条下发" —— 见 <see cref="RuntimeConfig"/> 里逐字段的状态标注。</para>
///
/// <para><b>参数合法性</b>：配置模型是公开可写的（UI / AI 工具 / 手改 JSON 都能写），
/// 所以生成期必须挡住非法值，否则产物里会出现 <c>#define HMI_POLL_INTERVAL_MS 0</c>
/// 这种"编译得过、跑起来忙等烧 CPU"的东西。非法值一律回退到固件历史默认值，
/// 并在产物里留一行诊断注释说明原因（不回退成"不发射"——那会让缺省回退分支失去意义）。</para>
///
/// <para><b>两个可配值都用 <c>#ifndef</c> 包住</b>：既允许临时 <c>-D</c> 覆盖
/// （现场调试、host 测试要跑出不同门槛），也避免与固件侧的缺省回退重定义冲突。
/// 级别常量（<c>HMI_LOG_LVL_*</c>）**不包** —— 它们是枚举镜像而不是配置项，不该被改。</para>
/// </summary>
public static class RuntimeConfigCodeGen
{
    /// <summary><see cref="RuntimeConfig.ScanIntervalMs"/> 的合法区间下界（毫秒）。</summary>
    public const int ScanIntervalMinMs = 1;

    /// <summary><see cref="RuntimeConfig.ScanIntervalMs"/> 的合法区间上界（毫秒）。</summary>
    public const int ScanIntervalMaxMs = 60_000;

    /// <summary>
    /// 轮询周期非法时的回退值 —— **等于固件 <c>runtime_config.h</c> 的历史默认值**，
    /// 这样回退不会引入任何行为差异。
    /// </summary>
    public const int FallbackScanIntervalMs = 100;

    /// <summary>
    /// 日志级别非法时的回退值 —— 等于 <see cref="RuntimeConfig.LogLevel"/> 的模型默认值，
    /// 同样保证回退零行为差异。
    /// </summary>
    public const LogLevel FallbackLogLevel = LogLevel.Warning;

    /// <summary>
    /// 生成运行时参数头文本。空 <paramref name="config"/> 按默认 <see cref="RuntimeConfig"/> 处理。
    /// </summary>
    public static string Generate(RuntimeConfig? config)
    {
        config ??= new RuntimeConfig();

        var sb = new StringBuilder();
        sb.AppendLine("/* ============================================================");
        sb.AppendLine(" * 设备端运行时参数 —— 由 HMI Editor 生成，请勿手改");
        sb.AppendLine(" *   单一来源：HmiProject.RuntimeConfig");
        sb.AppendLine(" *   消费方：firmware runtime/runtime_config.h（变量表轮询周期）");
        sb.AppendLine(" *           与 runtime/hmi_log.h（日志门槛）—— 两处都用");
        sb.AppendLine(" *           __has_include 优先引用本头，缺省回退历史默认值。");
        sb.AppendLine(" * ============================================================ */");
        sb.AppendLine("#ifndef HMI_GENERATED_RUNTIME_H");
        sb.AppendLine("#define HMI_GENERATED_RUNTIME_H");
        sb.AppendLine();

        AppendScanInterval(sb, config);
        AppendLogLevel(sb, config);

        sb.AppendLine("#endif /* HMI_GENERATED_RUNTIME_H */");
        return sb.ToString();
    }

    /// <summary>变量表轮询周期 —— 固件采集循环的休眠时长。</summary>
    private static void AppendScanInterval(StringBuilder sb, RuntimeConfig config)
    {
        var value = config.ScanIntervalMs;
        var valid = value >= ScanIntervalMinMs && value <= ScanIntervalMaxMs;

        sb.AppendLine("/* ── 变量表轮询周期（毫秒）。RuntimeConfig.ScanIntervalMs ──");
        sb.AppendLine(" * 固件 comm_core.c 的采集循环按此周期休眠（variable_table.c 在周期内合并块读）。");
        sb.AppendLine(" * 0 或负值会让采集线程忙等烧 CPU ⇒ 生成期挡在区间的外面。 */");
        if (!valid)
            sb.AppendLine($"/* ! 配置值 {value} 超出合法区间 [{ScanIntervalMinMs}, {ScanIntervalMaxMs}]，"
                + $"已回退 {FallbackScanIntervalMs} */");
        sb.AppendLine("#ifndef HMI_POLL_INTERVAL_MS");
        sb.AppendLine($"#define HMI_POLL_INTERVAL_MS {(valid ? value : FallbackScanIntervalMs)}");
        sb.AppendLine("#endif");
        sb.AppendLine();
    }

    /// <summary>
    /// 日志门槛 + 级别常量。门槛语义：**输出「严重度 &gt;= 本值」的日志**
    /// （与 <see cref="LogLevel"/> 枚举同向 —— 数字越大越严重）。
    /// </summary>
    private static void AppendLogLevel(StringBuilder sb, RuntimeConfig config)
    {
        var level = config.LogLevel;
        var valid = Enum.IsDefined(level);

        sb.AppendLine("/* ── 日志门槛。RuntimeConfig.LogLevel —— 输出「严重度 >= 本值」的日志 ──");
        sb.AppendLine(" * 固件 runtime/hmi_log.h 的分级宏族（HMI_LOG_DEBUG/INFO/WARN/ERR）按它裁剪；");
        sb.AppendLine(" * HMI_LOG(...) 定位为**无条件关键信息**，不受本门槛约束。");
        sb.AppendLine(" * 级别常量由 LogLevel 枚举推导 —— 设备端不得另写一份数值。 */");
        if (!valid)
            sb.AppendLine($"/* ! 配置值 {(int)level} 不是合法级别，已回退 {(int)FallbackLogLevel}（{FallbackLogLevel}） */");

        var width = Enum.GetValues<LogLevel>().Max(l => LogLevelMacro(l).Length);
        foreach (var l in Enum.GetValues<LogLevel>())
            sb.AppendLine($"#define {LogLevelMacro(l).PadRight(width)} {(int)l}");
        sb.AppendLine("#ifndef HMI_LOG_LEVEL");
        sb.AppendLine($"#define HMI_LOG_LEVEL {(valid ? (int)level : (int)FallbackLogLevel)}");
        sb.AppendLine("#endif");
        sb.AppendLine();
    }

    /// <summary>
    /// <see cref="LogLevel"/> → C 宏名（如 <c>HMI_LOG_LVL_WARNING</c>）。
    ///
    /// <para>名字取 <c>HMI_LOG_LVL_*</c> 而不是 <c>HMI_LOG_LEVEL_*</c>：后者会被读成
    /// "门槛等于这个级别"，与 <c>HMI_LOG_LEVEL</c>（门槛本身）混淆。
    /// 这是**枚举 → C 名字**的唯一实现，设备端不得自己拼。</para>
    /// </summary>
    public static string LogLevelMacro(LogLevel level) => $"HMI_LOG_LVL_{level.ToString().ToUpperInvariant()}";
}
