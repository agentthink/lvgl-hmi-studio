namespace HmiEditor.Core.Models;

/// <summary>
/// 日志级别 —— 运行时日志输出等级。
/// 门槛语义：输出「严重度 &gt;= 配置值」的日志，所以数字越大越安静。
/// </summary>
public enum LogLevel
{
    /// <summary>开发调试，仅开发版固件启用。</summary>
    Debug = 0,

    /// <summary>常规运行信息。</summary>
    Info = 1,

    /// <summary>异常但可恢复。</summary>
    Warning = 2,

    /// <summary>严重错误。</summary>
    Error = 3,

    /// <summary>致命错误，立即触发保护。</summary>
    Fatal = 4,
}

/// <summary>
/// 嵌入式运行时行为配置 —— 决定扫描周期、日志级别、存储策略等运行参数。
///
/// <para><b>字段下发状态（2026-09-16 逐项核对源码得出）</b> —— "配了不通设备"是本模型的
/// 头号风险：十个字段在工程设置面板里都有 UI、其中一部分还能被 AI 工具改，但**只有三个
/// 真的走到了设备**。逐个标出来，改的人才知道手上这个字段是活的还是死的：
/// <list type="bullet">
///   <item><b>已闭环（3）</b>：<see cref="BootScreenId"/>（生成代码里决定开机加载哪张屏）、
///         <see cref="ScanIntervalMs"/> 与 <see cref="LogLevel"/>
///         （→ <c>generated_runtime.h</c>，固件经 <c>__has_include</c> 消费）。</item>
///   <item><b>未下发（5）</b>：<see cref="HistoryStoragePath"/>、<see cref="HeartbeatIntervalMs"/>、
///         <see cref="MaxRuntimeTasks"/>、<see cref="EnableCrashDump"/>、<see cref="CrashDumpSector"/>
///         —— 只写进工程 JSON，生成器不发射、固件无接收位点，点它不会改变设备行为。</item>
///   <item><b>描述本身就是错的（2）</b>：<see cref="WatchdogTimeoutMs"/>
///         （T113 上是 Linux 用户态，没有"MCU 喂狗"这回事）、<see cref="EnableHmiReboot"/>
///         （旧注释指向系统变量 <c>$SYS.Reboot</c>，而那个变量**在编辑器里根本不存在**）。</item>
/// </list>
/// 这几条状态是核对出来的、不是猜的：判定方法是"把字段名铺到生成器与固件源码里找消费点"。
/// 教训与 <c>SecurityConfig</c> 那轮一致 —— <b>模型注释里不许写没验证过的行为描述</b>，
/// 后来人会把它当事实用（阶段 7、阶段 8 各因同一原因修过一处同类注释）。</para>
/// </summary>
public class RuntimeConfig
{
    /// <summary>
    /// 全局变量扫描周期（毫秒）。所有变量的读取/写入按此节拍轮询。
    ///
    /// <para><b>已闭环</b>：生成期发射 <c>HMI_POLL_INTERVAL_MS</c>（<c>generated_runtime.h</c>），
    /// 固件 <c>runtime/runtime_config.h</c> 经 <c>__has_include</c> 取它做采集循环休眠。
    /// 生成期会把 [1, 60000] 之外的值挡回默认 100 —— 0 或负值会让采集线程忙等烧 CPU。</para>
    ///
    /// <para><b>与 <see cref="VariableManager.DefaultScanIntervalMs"/> 语义重复</b>：那个也默认 100、
    /// 也叫"扫描周期"、UI 在**通讯设置**面板，而且同样零消费。于是一个工程里摆着两个"扫描周期"
    /// 输入框，用户看到的也是两个。合并属于产品决策，本轮只如实标注、不擅自合并。</para>
    /// </summary>
    public int ScanIntervalMs { get; set; } = 100;

    /// <summary>
    /// 历史数据存储路径。空表示使用 Flash 扇区，非空则指向外部存储挂载路径。
    ///
    /// <para><b>未下发</b>：只在工程 JSON 与设置面板里存在，生成器不发射、固件无接收位点。</para>
    /// </summary>
    public string? HistoryStoragePath { get; set; }

    /// <summary>
    /// 运行时日志级别。
    ///
    /// <para><b>已闭环</b>：生成期发射 <c>HMI_LOG_LEVEL</c> 与级别常量
    /// （<c>generated_runtime.h</c>），固件 <c>runtime/hmi_log.h</c> 的分级宏族按它裁剪。</para>
    ///
    /// <para><b>接上之后带来一次有意的行为变更</b>：固件里 19 处原先硬编码的
    /// <c>HMI_LOG("DBG: ...")</c> 已改用 <c>HMI_LOG_DEBUG</c>，默认级别 Warning 下它们
    /// **不再输出**（此前是无条件输出）。这是"默认值终于生效"的必然结果，不是回归 ——
    /// 要看这些调试输出，把这里调到 <see cref="LogLevel.Debug"/> 后重新导出。
    /// 无条件的 <c>HMI_LOG(...)</c>（启动横幅 / FATAL / 操作审计）不受门槛约束。</para>
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// 看门狗超时（毫秒）。
    ///
    /// <para><b>未下发，且旧描述本身就是错的。</b>旧注释写的是"超过此时间未喂狗则 MCU 复位" ——
    /// T113-S3 上跑的是 Linux 用户态进程，**没有 MCU、也没有喂狗这回事**，固件全树不存在任何
    /// watchdog 逻辑。这个字段从建模起就没有对应物。</para>
    ///
    /// <para>要让它有意义得先重定义语义并立功能，例如「应用心跳超时 ⇒ 进程自重启（procd respawn）」
    /// —— 那是新功能，不是补一条下发。</para>
    /// </summary>
    public int WatchdogTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 心跳上报间隔（毫秒）。HMI 向 PLC/上位机发送心跳帧的周期。
    ///
    /// <para><b>未下发</b>：固件侧没有统一的"心跳帧"机制 —— 保活由各协议自己带
    /// （如 MQTT 的 <c>HMI_MQTT_KEEPALIVE</c>，硬编码在 <c>runtime_config.h</c>）。
    /// 要让这个字段生效，得先在固件里立出"统一心跳"这个机制。</para>
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 开机默认加载的屏幕 Id。空则加载 Screens[0]。
    ///
    /// <para><b>已闭环</b>：两处消费 —— 生成代码里决定 <c>lv_screen_load</c> 的目标
    /// （<c>CodeGenerator</c>），以及导出工程时把该屏重排到 <c>screenFunctions[0]</c>
    /// （<c>SimulatorProjectExporter</c>）。回退顺序：指定屏 → 首个非图层屏 → 首个屏。</para>
    /// </summary>
    public string? BootScreenId { get; set; }

    /// <summary>
    /// 是否启用崩溃转储。崩溃时将现场寄存器/堆栈写入指定扇区。
    ///
    /// <para><b>未下发</b>：崩溃转储机制在固件侧不存在。要闭环得先定三件事：抓什么现场、
    /// 写到哪里（<c>/mnt/UDISK</c> 是唯一持久可写分区，<c>/usr</c> 是 tmpfs 重启即丢）、
    /// 下次开机怎么取出来。</para>
    /// </summary>
    public bool EnableCrashDump { get; set; } = true;

    /// <summary>
    /// 崩溃转储的目标扇区偏移地址（仅 <see cref="EnableCrashDump"/> = true 时有效）。
    ///
    /// <para><b>未下发</b>：见 <see cref="EnableCrashDump"/>。</para>
    /// </summary>
    public uint CrashDumpSector { get; set; }

    /// <summary>
    /// 运行时最大并发任务数（脚本 + 通讯 + 显示刷新的任务总数上限）。
    ///
    /// <para><b>未下发</b>：固件没有任务池这种东西 —— 结构是 LVGL 主循环 + 少量固定线程，
    /// 没有可配的上限。要闭环得先有任务池这个结构。</para>
    /// </summary>
    public int MaxRuntimeTasks { get; set; } = 32;

    /// <summary>
    /// 是否允许触发软重启。
    ///
    /// <para><b>未下发，且旧描述指向一个不存在的对象。</b>旧注释写的是"是否允许通过系统变量
    /// <c>$SYS.Reboot</c> 触发软重启" —— 而 <see cref="SystemVariable"/> 里预置的系统变量用的是
    /// <c>$SYS$XXX</c> 形式（<c>$SYS$TIME</c> / <c>$SYS$USER</c> / <c>$SYS$SCR</c> …），
    /// <b>没有任何 Reboot 变量</b>，设备端也没有对应处理分支。也就是说这个开关从来没有可以
    /// 作用的对象，勾不勾都一样。</para>
    ///
    /// <para>要闭环需要两步：① 在系统变量表里真的加一个重启变量、并接上设备端处理；
    /// ② 再让本开关门控它。</para>
    /// </summary>
    public bool EnableHmiReboot { get; set; } = true;
}
