using System;
using System.Collections.Generic;
using System.Linq;

namespace HmiEditor.App.Services;

/// <summary>
/// 一个配置字段的生效状态。
/// </summary>
public enum ConfigFieldStatus
{
    /// <summary>有真实消费点：改它，行为真的会变。</summary>
    Wired = 0,

    /// <summary>
    /// 只会被写进工程文件 / 设置文件，**没有任何执行路径读它**。
    /// 表现是「存得住、看得见、什么也不发生」。
    /// </summary>
    StoredOnly = 1,

    /// <summary>
    /// 面板上有控件，但对应功能根本不存在（不是没接线，是没实现）。
    /// </summary>
    NotImplemented = 2,
}

/// <summary>
/// 受管清单里的一个条目。
/// </summary>
/// <param name="Type">声明这个字段的类型（用于反射校验字段确实存在）。</param>
/// <param name="Field">属性名，同时也是「消费点扫描」用的 token。</param>
/// <param name="Surface">
/// 归属哪块面板：<see cref="ConfigFieldInventory.AppSurface"/> = 应用设置对话框，
/// 其余值 = 工程设置对话框。
/// </param>
/// <param name="Status">生效状态。本清单只登记非 <see cref="ConfigFieldStatus.Wired"/> 的字段。</param>
/// <param name="Control">
/// 需要在面板上灰显的控件变量名（等于 XAML 里的 <c>x:Name</c>）。
/// <para>
/// 为 <c>null</c> 表示"面板上没有对应控件、无需标注" —— 例如
/// <c>Theme.AccentColor</c>：界面上的强调色输入框是**真的**生效的
/// （它写的是 <c>AppThemeService.CustomAccent</c>），只是这个键没人读。
/// 这种"键是死的、控件不死的"情形必须区分开，否则就会把能用的控件标成不能用。
/// </para>
/// </param>
public sealed record ConfigFieldEntry(
    Type Type,
    string Field,
    string Surface,
    ConfigFieldStatus Status,
    string? Control)
{
    /// <summary>清单键：<c>类型名.字段名</c>。面板标注与守卫都按它对齐。</summary>
    public string Key => $"{Type.Name}.{Field}";

    /// <summary>
    /// 悬停说明「为什么它不生效」的**本地化键**，由 <see cref="Key"/> 推导
    /// （<c>cfg.note.类型名.字段名</c>）。
    ///
    /// <para><b>为什么推导而不是手写</b>：这句说明是<b>用户可见</b>的（灰显控件的悬停提示），
    /// 必须入字典；但手写字符串或手写键都会多出一份"与 Type/Field 并行维护"的事实 ——
    /// 复制一条条目时最容易忘记改的就是它，改错了（说明与字段对不上）也没有任何东西会报。
    /// 推导之后，键只可能跟着 <see cref="Type"/> / <see cref="Field"/> 走。</para>
    ///
    /// <para><b>为什么返回键而不是解析好的文案</b>：本清单是静态字段，静态初始化只跑一次 ——
    /// 若在这里就 <c>AppLocalization.Service[…]</c> 解析成字符串，语言会被冻结在
    /// 首次访问那一刻，之后切换语言提示不会跟着变。解析放在显示时
    /// （<c>NotYetEffective.Mark</c>）。</para>
    /// </summary>
    public string NoteKey => $"cfg.note.{Key}";
}

/// <summary>
/// 配置字段受管清单 —— **登记「面板上有、其实不生效」的每一个配置项**。
///
/// <para><b>为什么需要这份清单：</b>这个编辑器里最贵的一类缺陷不是崩溃，是
/// <b>配置项静静地不生效</b>。用户在对话框里改完、点确定、值也被记住了，
/// 然后行为一点没变 —— 而他无从判断是自己理解错了还是软件坏了。
/// 逐个人肉排查这件事没有尽头（本轮一次性查出 30 项），所以把结论固化成数据，
/// 由三件事一起保证它不过期：</para>
///
/// <list type="number">
///   <item>两个设置对话框按 <see cref="ConfigFieldEntry.Surface"/> 给这些控件加「尚未生效」标注；</item>
///   <item>守卫断言清单里每个字段都真实存在于 <see cref="ConfigFieldEntry.Type"/> 上（表不能写歪）；</item>
///   <item>守卫断言这些字段**确实**没有被白名单之外的代码读过 ——
///         一旦有人给它接上真实消费点，守卫会红，逼他把这一条删掉（即升级为已接线）。</item>
/// </list>
///
/// <para><b>它刻意不登记已接线的字段。</b>全量登记 60 多个属性只会变成一份很快过期的抄写；
/// 而「未生效」这个集合是小的、稳定的、并且能机械校验的（见第 3 条）。</para>
///
/// <para><b>已知未登记、单独记在专题文档里的一类：</b>
/// 只被 AI 工具写入、没有任何执行路径读取的字段（<c>RuntimeConfig.EnableHmiReboot</c> 等）。
/// 它们需要的是「补执行路径」，不是「标未生效」，机械判据分不出来，故不入表。</para>
/// </summary>
public static class ConfigFieldInventory
{
    /// <summary>应用设置对话框（SettingsDialog）的 surface 值。</summary>
    public const string AppSurface = "app";

    /// <summary>工程设置对话框（ProjectSettingsView）的 surface 值。</summary>
    public const string ProjectSurface = "project";

    /// <summary>
    /// 所有「面板上有、其实不生效」的配置字段。
    /// <para>
    /// 维护方式：接入真实消费点后**删掉对应条目**（而不是把状态改成 Wired）。
    /// 删掉之后第 3 条守卫会自然放行，因为那时它确实被读了。
    /// </para>
    /// </summary>
    public static IReadOnlyList<ConfigFieldEntry> NotWired { get; } = new ConfigFieldEntry[]
    {
        // ══ 应用级（SettingsService）—— 面板：SettingsDialog ══
        // 左侧面板比例、状态栏、自动保存、网格/吸附、模拟器与真机目录、右键菜单开关
        // 都是**真的**接了线的，不在此列。

        new(typeof(SettingsService.LayoutSettings),
            nameof(SettingsService.LayoutSettings.ShowCommandBar), AppSurface,
            ConfigFieldStatus.StoredOnly, null),

        new(typeof(SettingsService.LayoutSettings),
            nameof(SettingsService.LayoutSettings.ShowMenu), AppSurface,
            ConfigFieldStatus.StoredOnly, null),

        new(typeof(SettingsService.LayoutSettings),
            nameof(SettingsService.LayoutSettings.AutoHidePanels), AppSurface,
            ConfigFieldStatus.NotImplemented, "ChkAutoHide"),

        new(typeof(SettingsService.ThemeSettings),
            nameof(SettingsService.ThemeSettings.AccentColor), AppSurface,
            ConfigFieldStatus.StoredOnly, null),

        new(typeof(SettingsService.InteractionSettings),
            nameof(SettingsService.InteractionSettings.EnableAnimations), AppSurface,
            ConfigFieldStatus.StoredOnly, "ChkAnimations"),

        new(typeof(SettingsService.InteractionSettings),
            nameof(SettingsService.InteractionSettings.EnableUndoRedo), AppSurface,
            ConfigFieldStatus.StoredOnly, null),

        new(typeof(SettingsService.InteractionSettings),
            nameof(SettingsService.InteractionSettings.UndoStackSize), AppSurface,
            ConfigFieldStatus.NotImplemented, "NumUndoStack"),

        new(typeof(SettingsService.InteractionSettings),
            nameof(SettingsService.InteractionSettings.ShowRulers), AppSurface,
            ConfigFieldStatus.NotImplemented, "ChkShowRulers"),

        new(typeof(SettingsService.InteractionSettings),
            nameof(SettingsService.InteractionSettings.EnableLivePreview), AppSurface,
            ConfigFieldStatus.NotImplemented, "ChkLivePreview"),

        new(typeof(SettingsService.DisplaySettings),
            nameof(SettingsService.DisplaySettings.DefaultFont), AppSurface,
            ConfigFieldStatus.StoredOnly, "CmbDefaultFont"),

        new(typeof(SettingsService.DisplaySettings),
            nameof(SettingsService.DisplaySettings.FontStep), AppSurface,
            ConfigFieldStatus.StoredOnly, "NumFontStep"),

        new(typeof(SettingsService.DisplaySettings),
            nameof(SettingsService.DisplaySettings.CanvasBackgroundColor), AppSurface,
            ConfigFieldStatus.NotImplemented, "TxtCanvasBgColor"),

        new(typeof(SettingsService.DisplaySettings),
            nameof(SettingsService.DisplaySettings.ShowToolTips), AppSurface,
            ConfigFieldStatus.StoredOnly, "ChkToolTips"),

        new(typeof(SettingsService.DisplaySettings),
            nameof(SettingsService.DisplaySettings.ShowPreviewOverlay), AppSurface,
            ConfigFieldStatus.NotImplemented, "ChkPreviewOverlay"),

        // ══ 工程级 —— 面板：ProjectSettingsView ══

        new(typeof(Core.Models.ExportConfig),
            nameof(Core.Models.ExportConfig.CodeStyle), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "CmbCodeStyle"),

        new(typeof(Core.Models.ExportConfig),
            nameof(Core.Models.ExportConfig.ResourceNaming), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "CmbNaming"),

        new(typeof(Core.Models.ExportConfig),
            nameof(Core.Models.ExportConfig.MergeResources), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "ChkMergeResources"),

        new(typeof(Core.Models.ScreenDefaultsConfig),
            nameof(Core.Models.ScreenDefaultsConfig.DefaultOrientation), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "CmbOrientation"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.HistoryStoragePath), ProjectSurface,
            ConfigFieldStatus.NotImplemented, "TxtHistoryPath"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.WatchdogTimeoutMs), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumWatchdog"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.HeartbeatIntervalMs), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumHeartbeat"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.EnableCrashDump), ProjectSurface,
            ConfigFieldStatus.NotImplemented, "ChkCrashDump"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.CrashDumpSector), ProjectSurface,
            ConfigFieldStatus.NotImplemented, "NumCrashSector"),

        new(typeof(Core.Models.RuntimeConfig),
            nameof(Core.Models.RuntimeConfig.MaxRuntimeTasks), ProjectSurface,
            ConfigFieldStatus.NotImplemented, "NumMaxTasks"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.DefaultScanIntervalMs), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumCommScanInterval"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.DefaultTimeoutMs), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumCommTimeout"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.MaxRetryCount), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumRetryCount"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.RetryBackoffMs), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "NumRetryBackoff"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.EnableWriteCoalescing), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "ChkWriteCoalescing"),

        new(typeof(Core.Models.VariableManager),
            nameof(Core.Models.VariableManager.ConnectionRecoveryMode), ProjectSurface,
            ConfigFieldStatus.StoredOnly, "CmbRecoveryMode"),
    };

    /// <summary>某个 surface 下的条目。</summary>
    public static IReadOnlyList<ConfigFieldEntry> ForSurface(string surface) =>
        NotWired.Where(e => e.Surface == surface).ToList();

    /// <summary>按清单键取条目；不存在返回 null。</summary>
    public static ConfigFieldEntry? Find(string key) =>
        NotWired.FirstOrDefault(e => e.Key == key);
}
