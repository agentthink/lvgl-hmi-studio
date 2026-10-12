using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Security;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using Hmi.Hardware.Loader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.CodeGen;

namespace HmiEditor.App.Views;

/// <summary>
/// 工程设置对话框 —— 统一管理 HmiProject 所有工程级配置项。
///
/// 6 个分类:
///   - 基本信息（名称、作者、版本、硬件目标等元数据）
///   - 显示设置（屏幕默认尺寸、色深、方向、DPI 等）
///   - 通讯设置（扫描周期、超时、重试、写合并、断线恢复）
///   - 运行时设置（看门狗、心跳、崩溃转储、启动屏幕）
///   - 导出设置（目标语言、代码风格、构建系统）
///   - 安全设置（操作模式、会话超时、密码策略、防篡改）
///
/// 曾经的「编辑器设置」分类已移除 —— 编辑器偏好是**应用级**的，
/// 唯一权威是 <c>SettingsService</c>（命令栏「设置」按钮 → SettingsDialog）。
///
/// 打开方式: 工具 → 工程设置 (Ctrl+Alt+P)
/// </summary>
public partial class ProjectSettingsView : ChromeWindow
{
    private readonly HmiProject _project;

    /// <summary>已加载的固件型号（由 firmware/*/firmware.json 生成），用于设备兼容性校验。</summary>
    private readonly List<IHardwareTarget> _loadedTargets = new();

    /// <summary>用户是否对配置做了修改</summary>
    private bool _hasChanges;

    /// <summary>批量更新 UI 时抑制变更事件</summary>
    private bool _isUpdating;

    /// <summary>
    /// **运行时用户表的编辑草稿**。
    ///
    /// <para><b>为什么需要草稿：</b>本对话框的取消语义靠「模型只在 <see cref="SaveToProject"/> 里被写」
    /// 保证（其余控件都是"控件中介"）。用户表是集合，绑不到单个控件上 ⇒ 若直接改
    /// <c>_project.SecurityConfig.RuntimeUsers</c>，点「取消」时改动**已经生效**了 ——
    /// 那种"取消了却没取消掉"比不做还糟。所以这里维护一份深拷贝，
    /// <see cref="SaveToProject"/> 时**再拷一层**写回。两层拷贝都必要：少第一层则模型被就地改，
    /// 少第二层则模型与草稿共享引用、之后接着编辑仍会改到模型。</para>
    /// </summary>
    private List<RuntimeUser> _runtimeUsersDraft = new();

    /// <summary>重建用户列表 / 回填详情控件时抑制「已修改」标记与选择事件重入。</summary>
    private bool _isUpdatingRuntimeUsers;

    public event EventHandler? ProjectSettingsChanged;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public ProjectSettingsView()
    {
        _project = null!;
        InitializeComponent();
    }

    public ProjectSettingsView(HmiProject project) : this()
    {
        _project = project;
        DataContext = this;

        InitializeComboBoxes();
        InitializeRuntimeUserSection();
        InitializeCategoryList();
        BindToProject();
        WireButtons();
        MarkNotYetEffectiveFields();
    }

    // ── 「面板上有、其实不生效」的标注 ──

    /// <summary>
    /// 按受管清单 <see cref="ConfigFieldInventory"/> 给不生效的控件加标注（灰显 + 悬停原因）。
    /// <para>键不在清单里时静默跳过 —— 说明该字段已经接线、条目已被删掉。</para>
    /// </summary>
    private void MarkNotYetEffectiveFields()
    {
        var any = false;

        // ── 导出设置
        any |= MarkNotEffective<ExportConfig>(nameof(ExportConfig.CodeStyle), CmbCodeStyle);
        any |= MarkNotEffective<ExportConfig>(nameof(ExportConfig.ResourceNaming), CmbNaming);
        any |= MarkNotEffective<ExportConfig>(nameof(ExportConfig.MergeResources), ChkMergeResources);

        // ── 显示设置
        any |= MarkNotEffective<ScreenDefaultsConfig>(
            nameof(ScreenDefaultsConfig.DefaultOrientation), CmbOrientation);

        // ── 运行时设置
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.HistoryStoragePath), TxtHistoryPath);
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.WatchdogTimeoutMs), NumWatchdog);
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.HeartbeatIntervalMs), NumHeartbeat);
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.EnableCrashDump), ChkCrashDump);
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.CrashDumpSector), NumCrashSector);
        any |= MarkNotEffective<RuntimeConfig>(nameof(RuntimeConfig.MaxRuntimeTasks), NumMaxTasks);

        // ── 通讯设置
        any |= MarkNotEffective<VariableManager>(
            nameof(VariableManager.DefaultScanIntervalMs), NumCommScanInterval);
        any |= MarkNotEffective<VariableManager>(
            nameof(VariableManager.DefaultTimeoutMs), NumCommTimeout);
        any |= MarkNotEffective<VariableManager>(nameof(VariableManager.MaxRetryCount), NumRetryCount);
        any |= MarkNotEffective<VariableManager>(
            nameof(VariableManager.RetryBackoffMs), NumRetryBackoff);
        any |= MarkNotEffective<VariableManager>(
            nameof(VariableManager.EnableWriteCoalescing), ChkWriteCoalescing);
        any |= MarkNotEffective<VariableManager>(
            nameof(VariableManager.ConnectionRecoveryMode), CmbRecoveryMode);

        NotEffectiveLegend.IsVisible = any;
    }

    /// <summary>
    /// 给单个控件打标注：<typeparamref name="T"/> 的类型名 + <paramref name="field"/> 组成清单键。
    /// <para>返回是否真的标了（清单里没有这条 = 已接线 ⇒ 返回 false）。</para>
    /// </summary>
    private static bool MarkNotEffective<T>(string field, Control? control)
    {
        if (control is null) return false;
        var entry = ConfigFieldInventory.Find($"{typeof(T).Name}.{field}");
        if (entry is null || entry.Surface != ConfigFieldInventory.ProjectSurface) return false;
        NotYetEffective.Mark(control, entry.NoteKey);
        return true;
    }

    // ──────────────────────────────────────────
    //  ComboBox 数据源初始化
    // ──────────────────────────────────────────

    private void InitializeComboBoxes()
    {
        // 预设屏幕尺寸（宽×高），用于快速选择
        var screenPresets = new (int W, int H, string Name)[]
        {
            (480,  272,  "480 × 272"),
            (640,  360,  "640 × 360"),
            (800,  480,  "800 × 480"),
            (1024, 600,  "1024 × 600"),
            (1024, 768,  "1024 × 768"),
            (1280, 720,  "1280 × 720"),
            (1280, 800,  "1280 × 800"),
            (1920, 1080, "1920 × 1080"),
            (0,    0,    AppLocalization.Service[Lang.ScreenCustom]),
        };
        var presetItems = screenPresets.Select(p => new ComboBoxItem
        {
            Content = p.Name,
            Tag = p
        }).ToArray();
        CmbScreenPreset.ItemsSource = presetItems;
        CmbScreenPreset.SelectedIndex = 3; // 默认 1024×600（对齐固件型号 T113 7寸）

        CmbLvglVersion.ItemsSource = BuildLvglVersionItems();
        // 色深候选项来自 Hmi.Hardware.Contracts.ColorDepth 枚举本身。
        // 此前是硬编码的 new[] { "RGB565", "ARGB8888", "Indexed8" } —— 枚举增删成员时面板不跟着变，
        // 而且和 Hmi.Hardware.Loader.ConfigDrivenTarget.ParseColorDepth 是两份各自维护的清单。
        // 注意必须全限定：本文件同时 import 了 HmiEditor.Core.Services（那里也有一个 ColorDepth）。
        CmbColorDepth.ItemsSource = Enum.GetNames(typeof(Hmi.Hardware.Contracts.ColorDepth));
        CmbOrientation.ItemsSource = BuildEnumItems(
            (Lang.EnumOrientationLandscape, ScreenOrientation.Landscape),
            (Lang.EnumOrientationPortrait,  ScreenOrientation.Portrait));
        CmbAdaptiveMode.ItemsSource = BuildEnumItems(
            (Lang.EnumAdaptiveNone, ScreenAdaptiveMode.None),
            (Lang.EnumAdaptiveFit,  ScreenAdaptiveMode.Fit),
            (Lang.EnumAdaptiveFill, ScreenAdaptiveMode.Fill));
        CmbRecoveryMode.ItemsSource = BuildEnumItems(
            (Lang.EnumRecoveryAuto,     ConnectionRecoveryMode.AutoReconnect),
            (Lang.EnumRecoveryManual,   ConnectionRecoveryMode.ManualReconnect),
            (Lang.EnumRecoveryFailFast, ConnectionRecoveryMode.FailFast));
        CmbLogLevel.ItemsSource = BuildEnumItems(
            (Lang.EnumLogLevelDebug,   LogLevel.Debug),
            (Lang.EnumLogLevelInfo,    LogLevel.Info),
            (Lang.EnumLogLevelWarning, LogLevel.Warning),
            (Lang.EnumLogLevelError,   LogLevel.Error),
            (Lang.EnumLogLevelFatal,   LogLevel.Fatal));
        CmbTargetLanguage.ItemsSource = Enum.GetNames<TargetLanguage>();
        CmbBuildSystem.ItemsSource = Enum.GetNames<BuildSystem>();
        CmbCodeStyle.ItemsSource = Enum.GetNames<CodeStyle>();
        CmbNaming.ItemsSource = Enum.GetNames<ResourceNaming>();
        CmbOptimization.ItemsSource = BuildEnumItems(
            (Lang.EnumOptSize,        CodeGenOptimization.Size),
            (Lang.EnumOptSpeed,       CodeGenOptimization.Speed),
            (Lang.EnumOptReadability, CodeGenOptimization.Readability));
        CmbOperationMode.ItemsSource = BuildEnumItems(
            (Lang.EnumOpModeDesigner,    OperationMode.Designer),
            (Lang.EnumOpModeOperator,    OperationMode.Operator),
            (Lang.EnumOpModeMaintenance, OperationMode.Maintenance),
            (Lang.EnumOpModeReadonly,    OperationMode.ReadOnly));

        PopulateHardwareTargets();
    }

    /// <summary>
    /// 通用：根据 (本地化键, 枚举值) 配对列表构造 ComboBoxItem 列表。
    /// Content=本地化显示文本，Tag=枚举值（box 后存取）。
    /// </summary>
    private static List<ComboBoxItem> BuildEnumItems<T>(params (string LangKey, T Value)[] pairs)
        where T : struct, Enum
    {
        var list = new List<ComboBoxItem>(pairs.Length);
        foreach (var (key, value) in pairs)
            list.Add(new ComboBoxItem { Content = AppLocalization.Service[key], Tag = value });
        return list;
    }

    private void PopulateHardwareTargets()
    {
        // 型号由固件决定：扫描 firmware/*/firmware.json 生成固定型号列表。
        // 每套固件对应一个固定型号（型号与固件一一对应），
        // 不提供"通用"选项，也不加载任意 JSON 配置目标。
        var registry = new HardwareTargetRegistry();
        registry.LoadFirmwareTargets(Path.Combine(AppContext.BaseDirectory, "firmware"));

        // 固件目录缺失或为空时回退到内置 T113 型号，保证型号始终可用
        if (!registry.GetAllTargets().Any())
        {
            registry.Register(new AllwinnerT113Target());
        }

        _loadedTargets.Clear();
        _loadedTargets.AddRange(registry.GetAllTargets());

        var items = _loadedTargets
            .OrderBy(t => t.Metadata.Name, StringComparer.Ordinal)
            .Select(t => new ComboBoxItem
            {
                Content = $"{t.Metadata.Name} ({t.Metadata.Vendor}) v{t.Metadata.Version}",
                Tag = t.Metadata.Id
            })
            .ToList();
        CmbHardwareTarget.ItemsSource = items;
    }

    /// <summary>
    /// 解析当前选中的固件型号。无匹配（旧工程保存了失效 id）时返回 null。
    /// </summary>
    private IHardwareTarget? GetSelectedHardwareTarget()
    {
        var id = GetHardwareTargetId();
        if (string.IsNullOrEmpty(id)) return null;
        return _loadedTargets.FirstOrDefault(t => t.Metadata.Id == id);
    }

    /// <summary>
    /// 设备兼容性校验：调用选中硬件目标的 ValidateProject，
    /// 在设备下拉框下方显示兼容性结果（通过/警告/错误）。
    /// </summary>
    private void ValidateHardwareTarget()
    {
        var target = GetSelectedHardwareTarget();
        if (target == null)
        {
            TxtHardwareValidation.IsVisible = false;
            TxtHardwareValidation.Text = "";
            return;
        }

        var result = target.ValidateProject(_project);
        if (result.Issues.Count == 0)
        {
            TxtHardwareValidation.Text = AppLocalization.Service[Lang.HwTargetValidationOk];
            TxtHardwareValidation.Foreground = (IBrush)Application.Current!.Resources["HmiSuccessBrush"]!;
        }
        else
        {
            var errors = result.Issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warnings = result.Issues.Count - errors;
            var lines = result.Issues.Select(i => "• " + LocalizeIssue(i));
            TxtHardwareValidation.Text =
                AppLocalization.Service.Format("msg.findings", result.Issues.Count, errors, warnings) +
                "\n" + string.Join("\n", lines);
            TxtHardwareValidation.Foreground = errors > 0
                ? (IBrush)Application.Current!.Resources["HmiDangerBrush"]!
                : (IBrush)Application.Current!.Resources["HmiWarningBrush"]!;
        }
        TxtHardwareValidation.IsVisible = true;
    }

    /// <summary>
    /// 校验文案按 <c>Code</c> 查字典，查不到就回落插件自带的消息。
    ///
    /// <para>规则住在**插件程序集**（<c>Hmi.Hardware.Builtin</c>）里，那里拿不到 App 的字典，
    /// 所以文案由 App 侧按 <c>hw.issue.&lt;code 小写&gt;</c> 提供，插件原文只作回落。
    /// 模板需要具体数据（画面名/尺寸）时由 <see cref="ValidationIssue.Args"/> 填 ——
    /// 否则界面只能给一句不含细节的笼统提示。</para>
    /// </summary>
    private static string LocalizeIssue(ValidationIssue issue)
        // 查模板 → 填参数 → 查不到回落插件原文。这段逻辑收口在 ExternalText（**只有一份**），
        // Core 侧的脚本校验走的是同一段 —— 两份必然漂移，而漂移的表现正是
        // "守卫绿着、界面却是中文"。
        => ExternalText.Resolve(PluginTextKeys.IssueKey(issue.Code), issue.Message, issue.Args);

    // ──────────────────────────────────────────
    //  分类导航
    // ──────────────────────────────────────────

    private void InitializeCategoryList()
    {
        var categories = new[]
        {
            new { Key = "basic",    Name = Lang.ProjectSettingsCategoryBasic },
            new { Key = "display",  Name = Lang.ProjectSettingsCategoryDisplay },
            new { Key = "comm",     Name = Lang.ProjectSettingsCategoryComm },
            new { Key = "runtime",  Name = Lang.ProjectSettingsCategoryRuntime },
            new { Key = "export",   Name = Lang.ProjectSettingsCategoryExport },
            new { Key = "security", Name = Lang.ProjectSettingsCategorySecurity },
        };
        foreach (var c in categories)
            CategoryList.Items.Add(new ListBoxItem { Content = AppLocalization.Service[c.Name], Tag = c.Key });

        CategoryList.SelectionChanged += (_, _) =>
        {
            ShowPanel(CategoryList.SelectedIndex);
        };

        // 初始化显示第一个面板
        ShowPanel(0);
    }

    /// <summary>
    /// 把工程里存的型号 Id 解析成下拉项；**无匹配时返回 <c>null</c>（下拉保持"未选中"）**。
    ///
    /// <para><b>为什么这里刻意不回退到第一项（2026-09-17 修）：</b><c>HardwareTargetId</c> 是可空字段，
    /// <c>null</c> 有明确语义 —— 生成物写 <c>// Target: generic</c>、状态栏回落 <c>generic</c>、
    /// <see cref="ValidateHardwareTarget"/> 直接隐藏校验行。旧实现回退到第一项，于是
    /// <see cref="SaveToProject"/> 会把用户**从未选择过**的型号持久化进工程：打开工程设置点一下
    /// 「应用」，生成物的 <c>// Target:</c> 就从 <c>generic</c> 变成那块板子，工程 JSON 也多一行。
    /// 更糟的是当工程里存的是**失效 Id**（旧目标被删）时，回退会把它悄悄绑到**另一块板**上，
    /// 而不是诚实地说"未指定"。这是"往返保真"测试（<c>ProjectSettingsRoundTripUiTests</c>）抓出来的。</para>
    /// </summary>
    /// <summary>
    /// LVGL 版本候选 = 已知版本 +（必要时）**工程里存的那个表外版本**。
    ///
    /// <para><b>为什么要把表外版本也放进候选（2026-09-17 修）：</b><c>LvglVersion</c> 不是纯展示字段 ——
    /// <c>LvglApiCompatChecker</c> 按 9.x / 8.x **分支改写生成代码**，导出器还按它填 CMake 分支名与依赖表。
    /// 候选表里没有它时：装载让 <c>SelectedItem</c> 变成 null，保存又把 null 归一成 <c>"9.5"</c> ——
    /// 于是「打开工程设置点一下确定」就把一个 8.x 工程**悄悄升成 9.x**，生成代码的 API 兼容改写随之
    /// 改变语义（`docs/2026-09-17-open-items-register.md` §4 第 18 项）。放进候选后它原样往返，
    /// 只有用户**显式选了另一个版本**才会变更。</para>
    /// </summary>
    private string[] BuildLvglVersionItems()
    {
        // 清单的唯一实现在 LvglApiCompatChecker（懂版本兼容的那一层）；这里只负责"表外版本也进候选"。
        var known = LvglApiCompatChecker.SupportedLvglVersions.ToArray();

        var stored = _project.LvglVersion;
        if (string.IsNullOrWhiteSpace(stored) || known.Contains(stored, StringComparer.Ordinal))
            return known;

        return known.Append(stored).ToArray();
    }

    private ComboBoxItem? FindHardwareTargetItem(string? targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        foreach (var item in CmbHardwareTarget.Items.OfType<ComboBoxItem>())
            if (Equals(item.Tag as string, targetId)) return item;

        return null;
    }

    private string? GetHardwareTargetId()
    {
        if (CmbHardwareTarget.SelectedItem is ComboBoxItem item)
            return item.Tag as string;
        return null;
    }

    /// <summary>
    /// 根据分类索引显示对应的 Panel，隐藏其他 Panel。
    /// </summary>
    private void ShowPanel(int index)
    {
        var panels = new Panel?[]
        {
            PanelBasicInfo,
            PanelDisplaySettings,
            PanelCommSettings,
            PanelRuntimeSettings,
            PanelExportSettings,
            PanelSecuritySettings,
        };

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i]!.IsVisible = (i == index);
        }
    }

    // ──────────────────────────────────────────
    //  绑定：从 HmiProject 加载到 UI
    // ──────────────────────────────────────────

    private void BindToProject()
    {
        // ── 基本信息
        TxtDescription.Text = _project.Description ?? "";
        TxtAuthor.Text = _project.Author ?? "";
        TxtCompany.Text = _project.Company ?? "";
        TxtProjectVersion.Text = _project.ProjectVersion ?? "1.0.0";
        CmbLvglVersion.SelectedItem = string.IsNullOrEmpty(_project.LvglVersion) ? "9.5" : _project.LvglVersion;
        CmbHardwareTarget.SelectedItem = FindHardwareTargetItem(_project.HardwareTargetId);
        ValidateHardwareTarget();
        TxtCreatedAt.Text = _project.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        TxtModifiedAt.Text = _project.LastModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

        // ── 显示设置
        NumScreenWidth.Value = _project.ScreenDefaultsConfig.DefaultWidth;
        NumScreenHeight.Value = _project.ScreenDefaultsConfig.DefaultHeight;
        // 用 ResolveColorDepthItem 而不是直接赋值：存的值不在枚举里时（老工程手改过 JSON、
        // 或枚举成员被删）直接赋值会留下 SelectedItem=null —— 下拉显示空白、保存却仍会写一个值，
        // 那既是"界面在撒谎"，也让用户无从知道最终会落到哪个色深。该 helper 的文档写明了这个理由，
        // 但此前**从未被调用过**（死代码），于是它防的那个坑一直是活的。
        CmbColorDepth.SelectedItem = ResolveColorDepthItem(_project.ScreenDefaultsConfig.DefaultColorDepth);
        CmbOrientation.SelectedItem = FindEnumItem(CmbOrientation, _project.ScreenDefaultsConfig.DefaultOrientation);
        CmbAdaptiveMode.SelectedItem = FindEnumItem(CmbAdaptiveMode, _project.ScreenDefaultsConfig.DefaultAdaptiveMode);
        TxtDefaultBgColor.Text = _project.ScreenDefaultsConfig.DefaultBgColor ?? "";
        NumDpiScale.Value = _project.ScreenDefaultsConfig.DpiScale;

        // ── 通讯设置
        NumCommScanInterval.Value = _project.VariableManager.DefaultScanIntervalMs;
        NumCommTimeout.Value = _project.VariableManager.DefaultTimeoutMs;
        NumRetryCount.Value = _project.VariableManager.MaxRetryCount;
        NumRetryBackoff.Value = _project.VariableManager.RetryBackoffMs;
        ChkWriteCoalescing.IsChecked = _project.VariableManager.EnableWriteCoalescing;
        CmbRecoveryMode.SelectedItem = FindEnumItem(CmbRecoveryMode, _project.VariableManager.ConnectionRecoveryMode);

        // ── 运行时设置
        NumRuntimeScan.Value = _project.RuntimeConfig.ScanIntervalMs;
        CmbLogLevel.SelectedItem = FindEnumItem(CmbLogLevel, _project.RuntimeConfig.LogLevel);
        TxtHistoryPath.Text = _project.RuntimeConfig.HistoryStoragePath ?? "";
        NumWatchdog.Value = _project.RuntimeConfig.WatchdogTimeoutMs;
        NumHeartbeat.Value = _project.RuntimeConfig.HeartbeatIntervalMs;
        ChkCrashDump.IsChecked = _project.RuntimeConfig.EnableCrashDump;
        NumCrashSector.Value = _project.RuntimeConfig.CrashDumpSector;
        NumMaxTasks.Value = _project.RuntimeConfig.MaxRuntimeTasks;
        ChkEnableReboot.IsChecked = _project.RuntimeConfig.EnableHmiReboot;

        PopulateScreenCombo(CmbBootScreen, _project.RuntimeConfig.BootScreenId);

        // ── 导出设置
        CmbTargetLanguage.SelectedItem = _project.ExportConfig.TargetLanguage.ToString();
        CmbBuildSystem.SelectedItem = _project.ExportConfig.GenerateBuildSystem.ToString();
        TxtOutputDir.Text = _project.ExportConfig.OutputDirectory ?? "";
        CmbCodeStyle.SelectedItem = _project.ExportConfig.CodeStyle.ToString();
        CmbNaming.SelectedItem = _project.ExportConfig.ResourceNaming.ToString();
        CmbOptimization.SelectedItem = FindEnumItem(CmbOptimization, _project.ExportConfig.CodeGenOptimization);
        ChkDebugSymbols.IsChecked = _project.ExportConfig.GenerateDebugSymbols;
        ChkIncludeComments.IsChecked = _project.ExportConfig.IncludeComments;
        ChkMergeResources.IsChecked = _project.ExportConfig.MergeResources;
        ChkLicense.IsChecked = _project.ExportConfig.IncludeLgplDisclaimer;

        // ── 安全设置
        CmbOperationMode.SelectedItem = FindEnumItem(CmbOperationMode, _project.SecurityConfig.OperationMode);
        NumSessionTimeout.Value = _project.SecurityConfig.SessionTimeoutMin;
        ChkAuditLog.IsChecked = _project.SecurityConfig.EnableAuditLog;
        TxtAuditPath.Text = _project.SecurityConfig.AuditLogStoragePath ?? "";
        NumPwdMinLen.Value = _project.SecurityConfig.PasswordMinLength;
        NumMaxAttempts.Value = _project.SecurityConfig.MaxLoginAttempts;
        NumLockout.Value = _project.SecurityConfig.LockoutDurationMin;
        ChkAutoLogout.IsChecked = _project.SecurityConfig.AutoLogoutEnabled;
        ChkTamper.IsChecked = _project.SecurityConfig.EnableTamperDetection;
        TxtDefaultRoleId.Text = _project.SecurityConfig.DefaultRoleId ?? "";

        PopulateScreenCombo(CmbLockScreen, _project.SecurityConfig.LockScreenId);

        BindRuntimeUsers();

        // 刻意**没有**「编辑器设置」分组。
        // 编辑器偏好（网格、吸附、撤销栈、字体、实时预览、界面语言、控件拾取模式）是
        // **应用级**偏好，唯一权威是 SettingsService（命令栏「设置」按钮 → SettingsDialog）。
        // 这里曾经也有一份逐字段对应的副本，结果是两边都能配、两边都不生效 ——
        // 更精确地说：真正决定画布的是两个硬编码常量（SnapGrid 的 GridSize="10" 与
        // DesignCanvas.SnapThreshold=5）。工程级 EditorConfig 已降级为只读兼容载体。

        _hasChanges = false;
    }

    private void PopulateScreenCombo(ComboBox cmb, string? selectedId)
    {
        cmb.Items.Clear();
        cmb.Items.Add(AppLocalization.Service["project.settings.use.default"]);
        foreach (var s in _project.Screens)
            cmb.Items.Add(s.Name);

        if (string.IsNullOrEmpty(selectedId))
        {
            cmb.SelectedIndex = 0;
        }
        else
        {
            var match = _project.Screens.FirstOrDefault(s => s.Id == selectedId);
            cmb.SelectedItem = match?.Name ?? AppLocalization.Service["project.settings.use.default"];
        }
    }

    // ──────────────────────────────────────────
    //  显式动作：把「默认尺寸」应用到全部已存在屏幕
    // ──────────────────────────────────────────

    /// <summary>
    /// 「应用到全部屏幕」按钮 —— 唯一会改动工程内已存在屏幕尺寸的入口。
    /// <para>
    /// 写入的是本对话框的草稿副本（与 <see cref="SaveToProject"/> 同一份），
    /// 因此「取消」仍然能整体丢弃；点确定/应用后才真正落到工程上。
    /// </para>
    /// </summary>
    private void OnApplySizeToAllScreensClicked(object? sender, RoutedEventArgs e)
    {
        var width = (int)NumScreenWidth.Value;
        var height = (int)NumScreenHeight.Value;
        if (width <= 0 || height <= 0) return;

        foreach (var screen in _project.Screens)
        {
            screen.Width = width;
            screen.Height = height;
        }

        _hasChanges = true;
        BtnApplySizeToAll.Content = AppLocalization.Service.Format(
            "project.settings.display.apply.size.done", _project.Screens.Count);
    }

    // ──────────────────────────────────────────
    //  反向绑定：将 UI 值保存回 HmiProject
    // ──────────────────────────────────────────

    private void SaveToProject()
    {
        // ── 基本信息
        _project.Description = string.IsNullOrEmpty(TxtDescription.Text) ? null : TxtDescription.Text.Trim();
        _project.Author = string.IsNullOrEmpty(TxtAuthor.Text) ? null : TxtAuthor.Text.Trim();
        _project.Company = string.IsNullOrEmpty(TxtCompany.Text) ? null : TxtCompany.Text.Trim();
        _project.ProjectVersion = string.IsNullOrEmpty(TxtProjectVersion.Text) ? "1.0.0" : TxtProjectVersion.Text.Trim();
        _project.LvglVersion = string.IsNullOrEmpty(CmbLvglVersion.SelectedItem as string) ? "9.5" : (string)CmbLvglVersion.SelectedItem!;
        _project.HardwareTargetId = GetHardwareTargetId();

        // ── 显示设置
        _project.ScreenDefaultsConfig.DefaultWidth = (int)(NumScreenWidth.Value);
        _project.ScreenDefaultsConfig.DefaultHeight = (int)(NumScreenHeight.Value);
        _project.ScreenDefaultsConfig.DefaultColorDepth = NormalizeColorDepth((string?)CmbColorDepth.SelectedItem);
        _project.ScreenDefaultsConfig.DefaultOrientation = GetEnumValue(CmbOrientation, ScreenOrientation.Landscape);
        _project.ScreenDefaultsConfig.DefaultAdaptiveMode = GetEnumValue(CmbAdaptiveMode, ScreenAdaptiveMode.Fit);
        _project.ScreenDefaultsConfig.DefaultBgColor = string.IsNullOrEmpty(TxtDefaultBgColor.Text) ? null : TxtDefaultBgColor.Text.Trim();
        _project.ScreenDefaultsConfig.DpiScale = (double)(NumDpiScale.Value);

        // 刻意**不**在这里同步已存在屏幕的尺寸。
        // ScreenDefaultsConfig 的契约是「只影响后续新建的屏幕」；要改已存在屏幕，
        // 走「显示设置」分组里显式的「应用到全部屏幕」按钮（OnApplySizeToAllScreensClicked）。
        // 曾经的实现是无条件 foreach 覆盖，导致每次打开工程设置点确定都会静默重置
        // 用户逐屏手工调过的尺寸。

        // ── 通讯设置
        _project.VariableManager.DefaultScanIntervalMs = (int)(NumCommScanInterval.Value);
        _project.VariableManager.DefaultTimeoutMs = (int)(NumCommTimeout.Value);
        _project.VariableManager.MaxRetryCount = (int)(NumRetryCount.Value);
        _project.VariableManager.RetryBackoffMs = (int)(NumRetryBackoff.Value);
        _project.VariableManager.EnableWriteCoalescing = ChkWriteCoalescing.IsChecked ?? true;
        _project.VariableManager.ConnectionRecoveryMode = GetEnumValue(CmbRecoveryMode, ConnectionRecoveryMode.AutoReconnect);

        // ── 运行时设置
        _project.RuntimeConfig.ScanIntervalMs = (int)(NumRuntimeScan.Value);
        _project.RuntimeConfig.LogLevel = GetEnumValue(CmbLogLevel, LogLevel.Warning);
        _project.RuntimeConfig.HistoryStoragePath = string.IsNullOrEmpty(TxtHistoryPath.Text) ? null : TxtHistoryPath.Text.Trim();
        _project.RuntimeConfig.WatchdogTimeoutMs = (int)(NumWatchdog.Value);
        _project.RuntimeConfig.HeartbeatIntervalMs = (int)(NumHeartbeat.Value);
        _project.RuntimeConfig.EnableCrashDump = ChkCrashDump.IsChecked ?? true;
        _project.RuntimeConfig.CrashDumpSector = (uint)(NumCrashSector.Value);
        _project.RuntimeConfig.MaxRuntimeTasks = (int)(NumMaxTasks.Value);
        _project.RuntimeConfig.EnableHmiReboot = ChkEnableReboot.IsChecked ?? true;
        _project.RuntimeConfig.BootScreenId = ScreenComboToId(CmbBootScreen);

        // ── 导出设置
        _project.ExportConfig.TargetLanguage = ((string)(CmbTargetLanguage.SelectedItem ?? "C")).ParseOrDefault(TargetLanguage.C);
        _project.ExportConfig.GenerateBuildSystem = ((string)(CmbBuildSystem.SelectedItem ?? "CMake")).ParseOrDefault(BuildSystem.CMake);
        _project.ExportConfig.OutputDirectory = string.IsNullOrEmpty(TxtOutputDir.Text) ? "./export" : TxtOutputDir.Text.Trim();
        _project.ExportConfig.CodeStyle = ((string)(CmbCodeStyle.SelectedItem ?? "Gnu99")).ParseOrDefault(CodeStyle.Gnu99);
        _project.ExportConfig.ResourceNaming = ((string)(CmbNaming.SelectedItem ?? "SnakeCase")).ParseOrDefault(ResourceNaming.SnakeCase);
        _project.ExportConfig.CodeGenOptimization = GetEnumValue(CmbOptimization, CodeGenOptimization.Size);
        _project.ExportConfig.GenerateDebugSymbols = ChkDebugSymbols.IsChecked ?? false;
        _project.ExportConfig.IncludeComments = ChkIncludeComments.IsChecked ?? true;
        _project.ExportConfig.MergeResources = ChkMergeResources.IsChecked ?? true;
        _project.ExportConfig.IncludeLgplDisclaimer = ChkLicense.IsChecked ?? true;

        // ── 安全设置
        _project.SecurityConfig.OperationMode = GetEnumValue(CmbOperationMode, OperationMode.Designer);
        _project.SecurityConfig.SessionTimeoutMin = (int)(NumSessionTimeout.Value);
        _project.SecurityConfig.EnableAuditLog = ChkAuditLog.IsChecked ?? true;
        _project.SecurityConfig.AuditLogStoragePath = string.IsNullOrEmpty(TxtAuditPath.Text) ? null : TxtAuditPath.Text.Trim();
        _project.SecurityConfig.PasswordMinLength = (int)(NumPwdMinLen.Value);
        _project.SecurityConfig.MaxLoginAttempts = (int)(NumMaxAttempts.Value);
        _project.SecurityConfig.LockoutDurationMin = (int)(NumLockout.Value);
        _project.SecurityConfig.AutoLogoutEnabled = ChkAutoLogout.IsChecked ?? true;
        _project.SecurityConfig.LockScreenId = ScreenComboToId(CmbLockScreen);
        _project.SecurityConfig.EnableTamperDetection = ChkTamper.IsChecked ?? false;
        _project.SecurityConfig.DefaultRoleId = string.IsNullOrEmpty(TxtDefaultRoleId.Text) ? "role-operator" : TxtDefaultRoleId.Text.Trim();

        SaveRuntimeUsers();

        // 刻意**不**再写 EditorConfig —— 见 BindToProject 末尾的说明。
        // 它的字段没有任何消费点，写进去只会让 project.hmi 里多一段会撒谎的 JSON。
        // 保留类型与属性仅为让已有工程文件继续无损往返（见 EditorConfig 的类注释）。
    }

    private string? ScreenComboToId(ComboBox cmb)
    {
        var selected = cmb.SelectedItem as string;
        if (selected == null || selected == AppLocalization.Service["project.settings.use.default"]) return null;
        var screen = _project.Screens.FirstOrDefault(s => s.Name == selected);
        return screen?.Id;
    }

    /// <summary>
    /// 通用：在 ComboBox 的 Items 中查找 Tag 等于指定枚举值的 ComboBoxItem。
    /// 找不到时返回第一项（防御性回退，避免 SelectedItem=null）。
    /// </summary>
    private static ComboBoxItem? FindEnumItem<T>(ComboBox cmb, T value) where T : struct, Enum
    {
        foreach (var item in cmb.Items.OfType<ComboBoxItem>())
            if (item.Tag is T t && t.Equals(value)) return item;
        return cmb.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    /// <summary>
    /// 把工程里存的色深字符串解析成下拉项。
    /// <para>
    /// 存的值不在枚举里（老工程手改过 JSON、或枚举成员被删除）时回退到第一项，
    /// 而不是留 <c>SelectedItem = null</c> —— 后者会让下一次保存把无效值静默改写成默认值。
    /// </para>
    /// </summary>
    private string ResolveColorDepthItem(string? stored)
    {
        var items = CmbColorDepth.Items.OfType<string>().ToArray();
        if (items.Length == 0) return "";
        return stored is not null && items.Contains(stored, StringComparer.Ordinal)
            ? stored
            : items[0];
    }

    /// <summary>
    /// 把下拉选中的色深规范成 <c>Hmi.Hardware.Contracts.ColorDepth</c> 的成员名。
    /// 非法值（含 null）回退到 <c>RGB565</c> —— 与 <c>ConfigDrivenTarget.ParseColorDepth</c> 同一兜底。
    /// <para>
    /// 这里必须带 <see cref="Enum.IsDefined{T}(T)"/>：<c>Enum.TryParse</c> 对数字串也返回 true
    /// （例如 <c>"7"</c> 会变成一个未定义的枚举值），只靠 TryParse 会把 <c>"7"</c> 原样存进工程。
    /// </para>
    /// </summary>
    private static string NormalizeColorDepth(string? selected) =>
        Enum.TryParse<Hmi.Hardware.Contracts.ColorDepth>(selected, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed.ToString()
            : nameof(Hmi.Hardware.Contracts.ColorDepth.RGB565);

    /// <summary>
    /// 通用：从 ComboBox 的 SelectedItem 中读取 Tag 作为枚举值。
    /// 选中项不是 ComboBoxItem 或 Tag 类型不匹配时返回 fallback。
    /// </summary>
    private static T GetEnumValue<T>(ComboBox cmb, T fallback) where T : struct, Enum
    {
        if (cmb.SelectedItem is ComboBoxItem item && item.Tag is T t)
            return t;
        return fallback;
    }

    // ──────────────────────────────────────────
    //  按钮事件
    // ──────────────────────────────────────────

    private void WireButtons()
    {
        // 任何 UI 变更都标记为已修改
        AttachChangeHandler(CategoryList, (_, _) => _hasChanges = true);
        AttachChangeHandler(TxtDescription);
        AttachChangeHandler(TxtAuthor);
        AttachChangeHandler(TxtCompany);
        AttachChangeHandler(TxtProjectVersion);
        AttachChangeHandler(TxtDefaultBgColor);
        AttachChangeHandler(TxtHistoryPath);
        AttachChangeHandler(TxtOutputDir);
        AttachChangeHandler(TxtAuditPath);
        AttachChangeHandler(TxtDefaultRoleId);
        AttachChangeHandler(CmbLvglVersion);
        AttachChangeHandler(CmbHardwareTarget);
        CmbHardwareTarget.SelectionChanged += (_, _) => ValidateHardwareTarget();
        AttachChangeHandler(CmbColorDepth);
        AttachChangeHandler(CmbOrientation);
        AttachChangeHandler(CmbAdaptiveMode);
        AttachChangeHandler(CmbRecoveryMode);
        AttachChangeHandler(CmbLogLevel);
        AttachChangeHandler(CmbTargetLanguage);
        AttachChangeHandler(CmbBuildSystem);
        AttachChangeHandler(CmbCodeStyle);
        AttachChangeHandler(CmbNaming);
        AttachChangeHandler(CmbOptimization);
        AttachChangeHandler(CmbOperationMode);
        AttachChangeHandler(CmbBootScreen);
        AttachChangeHandler(CmbLockScreen);
        AttachChangeHandler(NumScreenWidth);
        AttachChangeHandler(NumScreenHeight);
        AttachChangeHandler(NumCommScanInterval);
        AttachChangeHandler(NumCommTimeout);
        AttachChangeHandler(NumRetryCount);
        AttachChangeHandler(NumRetryBackoff);
        AttachChangeHandler(NumRuntimeScan);
        AttachChangeHandler(NumWatchdog);
        AttachChangeHandler(NumHeartbeat);
        AttachChangeHandler(NumCrashSector);
        AttachChangeHandler(NumMaxTasks);
        AttachChangeHandler(NumDpiScale);
        AttachChangeHandler(NumSessionTimeout);
        AttachChangeHandler(NumPwdMinLen);
        // 密码下限同时约束"设置密码"那一栏的提示语：改了策略要立刻反映，而不是等重开对话框
        NumPwdMinLen.ValueChanged += (_, _) => UpdateRuntimeUserPasswordHint();
        AttachChangeHandler(NumMaxAttempts);
        AttachChangeHandler(NumLockout);
        AttachChangeHandler(ChkWriteCoalescing);
        AttachChangeHandler(ChkCrashDump);
        AttachChangeHandler(ChkEnableReboot);
        AttachChangeHandler(ChkDebugSymbols);
        AttachChangeHandler(ChkIncludeComments);
        AttachChangeHandler(ChkMergeResources);
        AttachChangeHandler(ChkLicense);
        AttachChangeHandler(ChkAuditLog);
        AttachChangeHandler(ChkAutoLogout);
        AttachChangeHandler(ChkTamper);

        // ── 运行时用户表：集合编辑，不走 AttachChangeHandler（那条路只标 _hasChanges，
        //    而这里每次编辑还要同步写回草稿 + 刷新列表/问题显示）
        RuntimeUserList.SelectionChanged += OnRuntimeUserSelectionChanged;
        TxtRuntimeUserName.TextChanged += OnRuntimeUserNameChanged;
        TxtRuntimeUserDisplayName.TextChanged += OnRuntimeUserDisplayNameChanged;
        CmbRuntimeUserRole.SelectionChanged += OnRuntimeUserRoleChanged;
        ChkRuntimeUserEnabled.IsCheckedChanged += OnRuntimeUserEnabledChanged;
        BtnAddRuntimeUser.Click += OnAddRuntimeUser;
        BtnRemoveRuntimeUser.Click += OnRemoveRuntimeUser;
        BtnSetRuntimeUserPassword.Click += OnSetRuntimeUserPassword;

        BtnOk.Click += OnOkClicked;
        BtnCancel.Click += (_, _) => Close();
        BtnApply.Click += OnApplyClicked;
        BtnApplySizeToAll.Click += OnApplySizeToAllScreensClicked;
    }

    /// <summary>
    /// 给控件挂"改了就标脏"（`_hasChanges = true`）。
    ///
    /// <para>⚠ <b>阶段 195 修 `#90`</b>：这里原先是
    /// <c>if (ctl is NumericUpDown nud)</c> 起头 —— 而本视图的**数字框 16 个全是
    /// <c>fa:FANumberBox</c>**（FluentAvalonia 自己的控件，**不继承** <c>NumericUpDown</c>：
    /// 它的基类是 <c>TemplatedControl</c>、<c>Value</c> 是 <c>double?</c>、事件是
    /// <c>TypedEventHandler&lt;FANumberBox, FANumberBoxValueChangedEventArgs&gt;</c> ——
    /// 阶段 195 用探针实测）。于是那 16 个框**一个都不标脏**：改数字后点「确定」被
    /// <c>OnOkClicked</c> 的 <c>if (_hasChanges)</c> 挡掉 ⇒ **静默丢弃**（点「应用」才保存，
    /// 因为那条路无条件 <c>ApplyInternal()</c>）。</para>
    /// </summary>
    private void AttachChangeHandler<T>(T ctl) where T : Control
    {
        if (ctl is NumericUpDown nud)
            nud.ValueChanged += (_, _) => _hasChanges = true;
        else if (ctl is FluentAvalonia.UI.Controls.FANumberBox fanumber)
            fanumber.ValueChanged += (_, _) => _hasChanges = true;
        else if (ctl is TextBox txt)
            txt.TextChanged += (_, _) => _hasChanges = true;
        else if (ctl is CheckBox chk)
            chk.IsCheckedChanged += (_, _) => _hasChanges = true;
        else if (ctl is ComboBox cmb)
            cmb.SelectionChanged += (_, _) => _hasChanges = true;
    }

    private void AttachChangeHandler<T>(T ctl, EventHandler<RoutedEventArgs> h) where T : Control
    {
        // no-op stub for category list —— doesn't need change tracking
    }

    private void CmbScreenPreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (CmbScreenPreset.SelectedItem is ComboBoxItem item && item.Tag is (int w, int h, _))
        {
            if (w > 0 && h > 0)
            {
                _isUpdating = true;
                NumScreenWidth.Value = w;
                NumScreenHeight.Value = h;
                _isUpdating = false;
                _hasChanges = true;
            }
        }
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        if (_hasChanges)
            ApplyInternal();
        Close();
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        ApplyInternal();
    }

    private void ApplyInternal()
    {
        SaveToProject();
        _hasChanges = false;
        ValidateHardwareTarget();
        ProjectSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ──────────────────────────────────────────
    //  运行时用户表
    //
    //  这一区与上面那些"一个控件对一个字段"的项不同：它是集合编辑，所以走
    //  「草稿 List<RuntimeUser> ↔ ListBox/详情控件」两条通道，再靠 SaveToProject 整体写回。
    //  编辑期一律改草稿，模型不动 —— 这是本对话框取消语义成立的前提。
    // ──────────────────────────────────────────

    /// <summary>
    /// 初始化用户表区：角色下拉 + 初始态（无选中）。
    ///
    /// <para>角色下拉的选项**只来自 <see cref="SecurityConfigCodeGen.BuiltInRoles"/>** ——
    /// 设备端角色表就那三个，让用户自由填 Id 只会得到「口令对了也登不上」
    /// （固件 <c>hmi_role_lookup</c> 返回 -1，<c>hmi_login_as_role</c> 直接拒绝）。</para>
    /// </summary>
    private void InitializeRuntimeUserSection()
    {
        CmbRuntimeUserRole.ItemsSource = SecurityConfigCodeGen.BuiltInRoles
            .Select(r => new ComboBoxItem { Content = RoleTextKeys.DisplayNameWithLevel(r), Tag = r.Id })
            .ToList();

        SetRuntimeUserDetailEnabled(false);
        UpdateRuntimeUserPasswordHint();
        RefreshRuntimeUserIssues();
    }

    /// <summary>模型 → 草稿（深拷贝，只在这里读模型一次）。</summary>
    private void BindRuntimeUsers()
    {
        _runtimeUsersDraft = (_project.SecurityConfig.RuntimeUsers ?? new List<RuntimeUser>())
            .Select(u => u.Clone())
            .ToList();

        RefreshRuntimeUserListItems();
        SelectRuntimeUser(null);

        // **必须重算提示**（2026-09-17 修）：本方法在构造期 `InitializeRuntimeUserSection()` **之后**才跑
        // （那时草稿还是空的，提示已被写成"用户表为空 —— 设备端不校验凭据"），
        // 而草稿正是在这里才被填上模型内容。少这一行 ⇒ 打开一个**已配账户**的工程时，
        // 提示区仍在说"表为空、设备不校验凭据" —— 两句话都与事实相反
        // （有人工验证漏掉它的原因：这条提示只在装载时错，随便动一下任一控件就会被后续调用纠正）。
        RefreshRuntimeUserIssues();
    }

    /// <summary>
    /// 草稿 → 模型。
    /// <para><b>必须再拷一层</b>：直接把草稿列表赋给模型，二者就共享同一批对象引用，
    /// 「确定」之后继续编辑会**继续改到模型**，而这个对话框的取消语义正是建立在
    /// 「模型只在保存那一刻被写」之上的。</para>
    /// </summary>
    private void SaveRuntimeUsers()
    {
        _project.SecurityConfig.RuntimeUsers = _runtimeUsersDraft.Select(u => u.Clone()).ToList();
    }

    /// <summary>当前选中的草稿条目（未选中为 null）。</summary>
    private RuntimeUser? SelectedRuntimeUser =>
        (RuntimeUserList.SelectedItem as ListBoxItem)?.Tag as RuntimeUser;

    /// <summary>
    /// 重建列表项。刻意**不碰详情控件** —— 文本类编辑（用户名）会在每次按键后调它，
    /// 若顺带回填详情，就会把用户正在输入的内容替换掉（表现为光标乱跳、打不进字）。
    /// 详情回填只由 <see cref="SelectRuntimeUser"/> / <see cref="ShowRuntimeUserDetail"/> 负责。
    /// </summary>
    private void RefreshRuntimeUserListItems()
    {
        var selected = SelectedRuntimeUser;

        _isUpdatingRuntimeUsers = true;
        try
        {
            RuntimeUserList.Items.Clear();
            foreach (var user in _runtimeUsersDraft)
            {
                var item = CreateRuntimeUserListItem(user);
                RuntimeUserList.Items.Add(item);
                if (ReferenceEquals(user, selected))
                    RuntimeUserList.SelectedItem = item;
            }
        }
        finally
        {
            _isUpdatingRuntimeUsers = false;
        }
    }

    /// <summary>选中某条草稿条目（null = 清空选中），详情回填由选择事件负责。</summary>
    private void SelectRuntimeUser(RuntimeUser? user)
    {
        var item = user is null
            ? null
            : RuntimeUserList.Items.OfType<ListBoxItem>()
                .FirstOrDefault(i => ReferenceEquals(i.Tag, user));

        RuntimeUserList.SelectedItem = item;

        // SelectedItem 没变化时 SelectionChanged 不触发（例如重新绑定后本来就是 null），
        // 这里显式补一次，免得详情区停留在上一条的内容上。
        if (user is null) SetRuntimeUserDetailEnabled(false);
    }

    private ListBoxItem CreateRuntimeUserListItem(RuntimeUser user)
    {
        var role = SecurityConfigCodeGen.BuiltInRoles
            .FirstOrDefault(r => string.Equals(r.Id, user.RoleId, StringComparison.Ordinal));

        var nameText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(user.Username)
                ? AppLocalization.Service["project.settings.security.users.unnamed"]
                : user.Username,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };

        var parts = new List<string>
        {
            // 表外角色原样显示 Id：配合下方问题行才能定位到是哪一条配错了
            role is null ? user.RoleId : RoleTextKeys.DisplayName(role),
            string.IsNullOrEmpty(user.PasswordHash)
                ? AppLocalization.Service["project.settings.security.users.password.none"]
                : AppLocalization.Service["project.settings.security.users.password.ok"],
        };
        if (!user.IsEnabled)
            parts.Add(AppLocalization.Service["project.settings.security.users.disabled"]);

        var infoText = new TextBlock
        {
            Text = string.Join(" · ", parts),
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
        };

        // 状态点口径 = 设备端「这条进不进得去」：非空用户名 + 角色在表内 + 已启用 + 有密码
        var usable = !string.IsNullOrWhiteSpace(user.Username)
                     && role is not null
                     && user.IsEnabled
                     && !string.IsNullOrEmpty(user.PasswordHash);

        var dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(usable ? UiPalette.ConnectedBright : UiPalette.Disconnected),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        return new ListBoxItem
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    dot,
                    new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Children = { nameText, infoText },
                    },
                },
            },
            Tag = user,
            Padding = new Thickness(8),
        };
    }

    /// <summary>回填详情控件（会清空密码输入框：明文一律不留在界面上）。</summary>
    private void ShowRuntimeUserDetail(RuntimeUser user)
    {
        SetRuntimeUserDetailEnabled(true);

        _isUpdatingRuntimeUsers = true;
        try
        {
            TxtRuntimeUserName.Text = user.Username;
            TxtRuntimeUserDisplayName.Text = user.DisplayName;
            CmbRuntimeUserRole.SelectedItem = CmbRuntimeUserRole.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => Equals(i.Tag as string, user.RoleId));
            ChkRuntimeUserEnabled.IsChecked = user.IsEnabled;
            TxtRuntimeUserPassword.Text = "";
            TxtRuntimeUserPasswordState.Text = string.IsNullOrEmpty(user.PasswordHash)
                ? AppLocalization.Service["project.settings.security.users.password.none"]
                : AppLocalization.Service["project.settings.security.users.password.ok"];
        }
        finally
        {
            _isUpdatingRuntimeUsers = false;
        }
    }

    private void SetRuntimeUserDetailEnabled(bool enabled)
    {
        TxtRuntimeUserName.IsEnabled = enabled;
        TxtRuntimeUserDisplayName.IsEnabled = enabled;
        CmbRuntimeUserRole.IsEnabled = enabled;
        ChkRuntimeUserEnabled.IsEnabled = enabled;
        TxtRuntimeUserPassword.IsEnabled = enabled;
        BtnSetRuntimeUserPassword.IsEnabled = enabled;
        BtnRemoveRuntimeUser.IsEnabled = enabled;

        if (enabled) return;

        _isUpdatingRuntimeUsers = true;
        try
        {
            TxtRuntimeUserName.Text = "";
            TxtRuntimeUserDisplayName.Text = "";
            CmbRuntimeUserRole.SelectedItem = null;
            ChkRuntimeUserEnabled.IsChecked = false;
            TxtRuntimeUserPassword.Text = "";
            TxtRuntimeUserPasswordState.Text =
                AppLocalization.Service["project.settings.security.users.none.selected"];
        }
        finally
        {
            _isUpdatingRuntimeUsers = false;
        }
    }

    private void UpdateRuntimeUserPasswordHint()
    {
        // 下限来自**当前面板里的密码策略值**（不是模型里的旧值）—— 用户可能刚改过它还没保存
        TxtRuntimeUserPassword.PlaceholderText = AppLocalization.Service.Format(
            "project.settings.security.users.password.hint", (int)NumPwdMinLen.Value);
    }

    /// <summary>
    /// 把校验结果渲染成提示文本。空表与"表非空但一条都进不去"要分开说。
    ///
    /// <para><b>为什么必须有第三个分支（2026-09-17 修）：</b>四条规则各自都不覆盖"账户被停用"，
    /// 所以一张**全部停用**的表在 <see cref="RuntimeUserValidator.Validate"/> 下返回**零问题**，
    /// 于是落进原来的"正常"分支、显示成 <c>共 0 个可用账户</c> ——
    /// 可这条形态的后果是**设备端谁都登不上、所有权限门永久拒绝**，且已被导出闸门判为 Error
    /// （阶段 12）。也就是说：面板把一个阻断级状态显示成了常规提示。
    /// 本方法原本的注释就写着"要分开说"，但实现里那个分支**不存在** —— 属"注释描述了未实现的行为"。</para>
    /// </summary>
    private void RefreshRuntimeUserIssues()
    {
        var issues = RuntimeUserValidator.Validate(_runtimeUsersDraft);
        var usable = RuntimeUserValidator.CountUsable(_runtimeUsersDraft);

        if (issues.Count == 0)
        {
            if (_runtimeUsersDraft.Count == 0)
            {
                SetRuntimeUserIssues(AppLocalization.Service["project.settings.security.users.table.empty"], isProblem: false);
                return;
            }

            if (usable == 0)
            {
                SetRuntimeUserIssues(
                    AppLocalization.Service.Format(
                        "project.settings.security.users.table.none.usable", _runtimeUsersDraft.Count),
                    isProblem: true);
                return;
            }

            SetRuntimeUserIssues(
                AppLocalization.Service.Format("project.settings.security.users.table.ok", usable),
                isProblem: false);
            return;
        }

        var lines = issues.Select(issue => AppLocalization.Service.Format(
            "project.settings.security.users.issue.line",
            issue.Index + 1,
            string.IsNullOrWhiteSpace(issue.Username)
                ? AppLocalization.Service["project.settings.security.users.unnamed"]
                : issue.Username,
            RuntimeUserIssueText(issue.Kind)));

        SetRuntimeUserIssues(string.Join("\n", lines), isProblem: true);
    }

    /// <summary>
    /// 写出提示并同步颜色：出问题用警示色，空表/正常用常规色。
    /// <para>颜色与文案必须一起变 —— 只改文案不改颜色，用户扫一眼仍会把阻断级问题当成普通说明。</para>
    /// </summary>
    private void SetRuntimeUserIssues(string text, bool isProblem)
    {
        TxtRuntimeUserIssues.Text = text;
        TxtRuntimeUserIssues.Foreground = isProblem
            ? HmiEditor.App.Theme.Warning
            : HmiEditor.App.Theme.TextPrimarySoft;
    }

    /// <summary>问题类型 → 本地化文案（Core 侧只给枚举，文案在这里唯一映射一次）。</summary>
    private static string RuntimeUserIssueText(RuntimeUserIssueKind kind) => kind switch
    {
        RuntimeUserIssueKind.EmptyUsername =>
            AppLocalization.Service["project.settings.security.users.issue.empty.username"],
        RuntimeUserIssueKind.DuplicateUsername =>
            AppLocalization.Service["project.settings.security.users.issue.duplicate.username"],
        RuntimeUserIssueKind.UnknownRole =>
            AppLocalization.Service["project.settings.security.users.issue.unknown.role"],
        RuntimeUserIssueKind.EnabledWithoutPassword =>
            AppLocalization.Service["project.settings.security.users.issue.enabled.no.password"],
        _ => kind.ToString(),
    };

    // ── 用户表：事件 ──

    private void OnRuntimeUserSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRuntimeUsers) return;

        if (SelectedRuntimeUser is { } user) ShowRuntimeUserDetail(user);
        else SetRuntimeUserDetailEnabled(false);
    }

    private void OnRuntimeUserNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingRuntimeUsers) return;
        if (SelectedRuntimeUser is not { } user) return;

        // Trim：设备端按 strcmp 精确比对，首尾空格会让"看起来一样的用户名"在设备上对不上
        var newName = (TxtRuntimeUserName.Text ?? "").Trim();
        if (string.Equals(user.Username, newName, StringComparison.Ordinal)) return;

        user.Username = newName;
        _hasChanges = true;

        // 只重建列表项（名字要在列表里跟着变），不回填详情 —— 否则正在输入的内容被替换掉
        RefreshRuntimeUserListItems();
        RefreshRuntimeUserIssues();
    }

    private void OnRuntimeUserDisplayNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingRuntimeUsers) return;
        if (SelectedRuntimeUser is not { } user) return;

        var newName = TxtRuntimeUserDisplayName.Text ?? "";
        if (string.Equals(user.DisplayName, newName, StringComparison.Ordinal)) return;

        // 显示名仅供编辑器辨认，不下发到设备 ⇒ 改了不影响列表项主标题（那是用户名）
        user.DisplayName = newName;
        _hasChanges = true;
    }

    private void OnRuntimeUserRoleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingRuntimeUsers) return;
        if (SelectedRuntimeUser is not { } user) return;

        if ((CmbRuntimeUserRole.SelectedItem as ComboBoxItem)?.Tag is not string roleId) return;
        if (string.Equals(user.RoleId, roleId, StringComparison.Ordinal)) return;

        user.RoleId = roleId;
        _hasChanges = true;
        RefreshRuntimeUserListItems();
        RefreshRuntimeUserIssues();
    }

    private void OnRuntimeUserEnabledChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingRuntimeUsers) return;
        if (SelectedRuntimeUser is not { } user) return;

        var enabled = ChkRuntimeUserEnabled.IsChecked ?? false;
        if (user.IsEnabled == enabled) return;

        user.IsEnabled = enabled;
        _hasChanges = true;
        RefreshRuntimeUserListItems();
        RefreshRuntimeUserIssues();
    }

    private void OnAddRuntimeUser(object? sender, RoutedEventArgs e)
    {
        var user = new RuntimeUser
        {
            Username = RuntimeUserValidator.NextDefaultUsername(_runtimeUsersDraft),
            RoleId = UserRole.BuiltInOperator.Id,
            // 刻意留空：新账户在有密码之前进不去，问题行会明说这一点。
            // 预填一个默认口令比"暂时登不上"危险得多（默认口令几乎不会被改）。
            PasswordHash = "",
            IsEnabled = true,
        };

        _runtimeUsersDraft.Add(user);

        RefreshRuntimeUserListItems();
        SelectRuntimeUser(user);
        RefreshRuntimeUserIssues();
        _hasChanges = true;
    }

    /// <summary>
    /// 删除选中条目。
    /// <para>不弹二次确认：整个面板是「草稿 + 确定/取消」语义，删错了按取消即可全量回滚 ——
    /// 这里再加一层确认框只会训练用户无脑点「是」。</para>
    /// </summary>
    private void OnRemoveRuntimeUser(object? sender, RoutedEventArgs e)
    {
        if (SelectedRuntimeUser is not { } user) return;

        _runtimeUsersDraft.Remove(user);

        RefreshRuntimeUserListItems();
        SelectRuntimeUser(null);
        RefreshRuntimeUserIssues();
        _hasChanges = true;
    }

    /// <summary>
    /// 设置选中条目的密码 —— 点下即算哈希，明文只在输入框里存在过这一瞬。
    /// <para>长度按 <b>UTF-8 字节数</b>判（<see cref="SecurityConfig.PasswordMinLength"/> 的模型语义，
    /// 与设备端 <c>strlen</c>、以及 <c>AuthenticationService.SetUserPassword</c> 同口径），
    /// 提示语里明说"至少 N 个字节（一个汉字算 3 个字节）"，不让用户去猜口径。</para>
    /// ⚠ 这句原本写的是「按**字符数**判」—— 与紧随其后的代码（<c>GetByteCount</c>）相反，
    /// 阶段 168 连同 `AuthenticationService` 那处漏网一起更正。</summary>
    private void OnSetRuntimeUserPassword(object? sender, RoutedEventArgs e)
    {
        if (SelectedRuntimeUser is not { } user) return;

        var password = TxtRuntimeUserPassword.Text ?? "";
        if (password.Length == 0)
        {
            TxtRuntimeUserIssues.Text =
                AppLocalization.Service["project.settings.security.users.need.password"];
            return;
        }

        var minLength = (int)NumPwdMinLen.Value;
        // 按 **UTF-8 字节数** 判，与设备端 `strlen` 同口径（见 SecurityConfig.PasswordMinLength 的注释）。
        // 旧实现用 password.Length（UTF-16 码元 ≈ 字符数）⇒ 同一个口令在两侧结论不同：
        // 例如下限 8 时，"电机启动" 4 个字（12 字节）设备端放行、编辑器却报"至少 8 位"。
        if (System.Text.Encoding.UTF8.GetByteCount(password) < minLength)
        {
            TxtRuntimeUserIssues.Text = AppLocalization.Service.Format(
                "project.settings.security.users.password.too.short", minLength);
            return;
        }

        user.PasswordHash = RuntimePasswordHasher.Hash(password);

        _hasChanges = true;
        RefreshRuntimeUserListItems();   // 状态点与"未设置密码"标记要跟着变
        ShowRuntimeUserDetail(user);     // 密码状态文案要变（顺带清空输入框）
        RefreshRuntimeUserIssues();      // "已启用但未设密码"那条要消失
    }
}

/// <summary>
/// Enum.Parse 的容错扩展：解析失败时返回 T 默认值，不抛异常。
/// </summary>
file static class EnumParseExtensions
{
    public static T ParseOrDefault<T>(this string value, T fallback = default) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, true, out var result))
            return result;
        return fallback;
    }
}
