using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// 用户界面偏好设置服务 —— 持久化布局、交互、显示等个性化配置。
/// 所有设置项都有合理的默认值，首次使用无需初始化。
///
/// <para><b>它是编辑器偏好的唯一权威。</b>工程级 <c>EditorConfig</c> 已降级为"只读兼容载体"，
/// 不再有任何消费点 —— 编辑器行为偏好一律走这里。</para>
/// </summary>
public sealed class SettingsService : IDisposable
{
    /// <summary>
    /// 设置文件的结构版本。
    /// <list type="bullet">
    ///   <item>v1 —— 初始版本。其中 <c>GridSize</c> 默认值 8 **从未被任何消费点读取**（画布恒用硬编码 10）。</item>
    ///   <item>v2 —— 编辑器偏好真正接线：<c>GridSize</c> 默认对齐画布真实值 10，新增 <c>SnapStep</c>（默认 5）。</item>
    /// </list>
    /// </summary>
    private const int CurrentSchemaVersion = 2;

    /// <summary>v1 时代写进文件的"假的"网格默认值 —— 无消费点，不代表用户意图。</summary>
    private const int LegacyDefaultGridSize = 8;

    private readonly string _persistPath;
    private readonly object _lock = new();
    private UserSettings _settings;

    /// <summary>
    /// 某个设置项发生变化（参数为键名）。
    /// 供画布等消费点做实时响应 —— 否则改完设置得重启才生效，等于没接线。
    /// </summary>
    public event EventHandler<string>? SettingChanged;

    public SettingsService(string persistPath)
    {
        _persistPath = persistPath;
        _settings = Load();
    }

    /// <summary>当前所有设置项。</summary>
    public UserSettings Current => _settings;

    /// <summary>
    /// 获取设置项。磁盘上的值会先折算到调用方期望的类型（见 <see cref="Coerce"/>）。
    /// </summary>
    public T Get<T>(string key, T defaultValue)
    {
        lock (_lock)
        {
            if (!_settings.TryGetValue(key, out var value) || value is null)
                return defaultValue;
            if (value is T typed)
                return typed;
            return Coerce(value, defaultValue);
        }
    }

    /// <summary>
    /// 设置设置项。
    /// </summary>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _settings[key] = value;
            Save();
        }
        SettingChanged?.Invoke(this, key);
    }

    /// <summary>
    /// 获取布局设置。
    /// </summary>
    public LayoutSettings Layout => new(this);

    /// <summary>
    /// 获取交互设置。
    /// </summary>
    public InteractionSettings Interaction => new(this);

    /// <summary>
    /// 获取显示设置。
    /// </summary>
    public DisplaySettings Display => new(this);

    /// <summary>
    /// 获取主题设置。
    /// </summary>
    public ThemeSettings Theme => new(this);

    /// <summary>
    /// 获取项目浏览器设置（右键增强菜单开关，管理员可配置）。
    /// </summary>
    public ExplorerSettings Explorer => new(this);

    /// <summary>
    /// 获取最近打开的工程列表。
    /// </summary>
    public RecentProjectsSettings RecentProjects => new(this);

    /// <summary>
    /// 获取 PC 模拟器构建设置。
    /// </summary>
    public SimulatorSettings Simulator => new(this);

    /// <summary>
    /// 获取 T113 真机部署（.so 交叉编译）设置。
    /// </summary>
    public DeviceSettings Device => new(this);

    /// <summary>
    /// 获取**报表模板**（阶段 182：每个数据源一份列布局模板）。
    /// </summary>
    public ReportTemplateSettings ReportTemplates => new(this);

    /// <summary>
    /// 获取**定时/自动导出**设置（阶段 186：开关 ＋ 间隔 ＋ 目标目录）。
    /// </summary>
    public AutoExportSettings AutoExport => new(this);

    private UserSettings Load()
    {
        UserSettings settings;
        try
        {
            if (!File.Exists(_persistPath))
            {
                settings = new UserSettings();
            }
            else
            {
                var json = File.ReadAllText(_persistPath);
                settings = new UserSettings();
                var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (raw != null)
                {
                    foreach (var (key, element) in raw)
                        settings[key] = Materialize(element);
                }
            }
        }
        catch
        {
            settings = new UserSettings();
        }

        Migrate(settings);
        return settings;
    }

    /// <summary>
    /// 把 JSON 元素折算成普通 CLR 值（bool / int / long / double / string / string[]）。
    ///
    /// <para><b>为什么必须做这一步：</b><c>UserSettings</c> 是
    /// <c>Dictionary&lt;string, object?&gt;</c>，<c>System.Text.Json</c> 反序列化 <c>object</c>
    /// 时给出的是 <see cref="JsonElement"/>，而 <see cref="Get{T}"/> 用 <c>is T</c> 判等 ——
    /// 两者不相等 ⇒ 磁盘上**每一个**值都被静默当成"类型不匹配"退回默认值。
    /// 表现就是"设置存得进去、重启全回默认"。</para>
    /// </summary>
    private static object? Materialize(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => MaterializeNumber(element),
        JsonValueKind.Array => MaterializeArray(element),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        // 嵌套对象/其它形态原样保留（JsonElement 本身可序列化）
        _ => element.Clone(),
    };

    /// <summary>
    /// 数字按"能放下就用最窄的整数类型"还原。
    /// <para><b>必须写成早期返回，不能写成三元链：</b><c>int : long : double</c> 三个分支的
    /// 公共类型是 <c>double</c>，三元表达式会把每个整数都装箱成 <c>double</c> ——
    /// 于是整数偏好的类型不再是 <c>int</c>，迁移判据、<c>is int</c> 判断全部静默失效。</para>
    /// </summary>
    private static object MaterializeNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var i)) return i;
        if (element.TryGetInt64(out var l)) return l;
        return element.GetDouble();
    }

    private static object MaterializeArray(JsonElement element)
    {
        var items = new List<object?>();
        foreach (var item in element.EnumerateArray())
            items.Add(Materialize(item));

        // 全字符串数组（如 RecentProjects）还原为 string[]
        return items.All(x => x is string)
            ? items.Cast<string>().ToArray()
            : items.ToArray();
    }

    /// <summary>
    /// 把已存的值折算到调用方期望的类型。
    /// <para><c>JsonSerializer</c> 会把 <c>1.0</c> 写成 <c>1</c>，读回来就成了 <c>int</c>，
    /// 于是 <c>Get&lt;double&gt;</c> 会拿到一个 <c>int</c>；没有这一层折算，缩放级别这类
    /// "整数形式的浮点偏好"每次重启都会回默认。</para>
    ///
    /// <para><b>结构化的偏好</b>（阶段 182）：<see cref="Materialize"/> 对嵌套对象刻意保留
    /// <see cref="JsonElement"/>，而上面几支只认基元 ⇒ 报表模板这种"字典/列表"形状的值
    /// 以前会在读回时静默退回默认值（＝"存得进去、重启就没了"的第二种形态，与
    /// <see cref="Materialize"/> 注释里记的那次同族）。这里按**目标类型**反序列化一次；
    /// 形状不匹配就抛，由下面的 catch 统一退回默认值 —— 绝不 `InvalidCastException` 打穿启动。</para>
    /// </summary>
    private static T Coerce<T>(object raw, T fallback)
    {
        try
        {
            var target = typeof(T);
            if (target == typeof(double)) return (T)(object)Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (target == typeof(int)) return (T)(object)Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            if (target == typeof(long)) return (T)(object)Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            if (target == typeof(bool)) return (T)(object)Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            if (target == typeof(string)) return (T)(object)(Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty);

            if (raw is JsonElement element)
            {
                var deserialized = JsonSerializer.Deserialize<T>(element.GetRawText());
                if (deserialized is not null) return deserialized;
            }
        }
        catch
        {
            // 折算失败按默认值处理
        }
        return fallback;
    }

    /// <summary>
    /// 老版本设置文件的一次性迁移。幂等：迁移结果只在内存里，靠下一次 <see cref="Set"/> 落盘。
    /// </summary>
    private static void Migrate(UserSettings settings)
    {
        var version = settings.TryGetValue("SchemaVersion", out var raw) && raw is int v
            ? v
            : 1;
        if (version >= CurrentSchemaVersion) return;

        // v1 的 GridSize 默认值 8 没有任何消费点（画布恒用硬编码 10），
        // 所以文件里的 8 只是那句空话的残留，直接对齐到画布真实值。
        if (settings.TryGetValue("GridSize", out var grid) &&
            grid is int g && g == LegacyDefaultGridSize)
        {
            settings["GridSize"] = 10;
        }

        if (!settings.ContainsKey("SnapStep"))
            settings["SnapStep"] = 5;

        settings["SchemaVersion"] = CurrentSchemaVersion;
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });
            File.WriteAllText(_persistPath, json);
        }
        catch { /* 持久化失败不应影响功能 */ }
    }

    public void Dispose()
    {
        try
        {
            Save();
        }
        catch { }
    }

    // ── 设置项分组 ──

    public sealed class LayoutSettings
    {
        private readonly SettingsService _parent;
        internal LayoutSettings(SettingsService parent) => _parent = parent;

        public double LeftPanelProportion => _parent.Get(nameof(LeftPanelProportion), 0.17);
        public void SetLeftPanelProportion(double value) => _parent.Set(nameof(LeftPanelProportion), value);

        public double RightPanelProportion => _parent.Get(nameof(RightPanelProportion), 0.17);
        public void SetRightPanelProportion(double value) => _parent.Set(nameof(RightPanelProportion), value);

        public double BottomPanelProportion => _parent.Get(nameof(BottomPanelProportion), 0.3);
        public void SetBottomPanelProportion(double value) => _parent.Set(nameof(BottomPanelProportion), value);

        public bool ShowStatusBar => _parent.Get(nameof(ShowStatusBar), true);
        public void SetShowStatusBar(bool value) => _parent.Set(nameof(ShowStatusBar), value);

        public bool ShowCommandBar => _parent.Get(nameof(ShowCommandBar), true);
        public void SetShowCommandBar(bool value) => _parent.Set(nameof(ShowCommandBar), value);

        public bool ShowMenu => _parent.Get(nameof(ShowMenu), true);
        public void SetShowMenu(bool value) => _parent.Set(nameof(ShowMenu), value);

        public bool AutoHidePanels => _parent.Get(nameof(AutoHidePanels), false);
        public void SetAutoHidePanels(bool value) => _parent.Set(nameof(AutoHidePanels), value);
    }

    public sealed class InteractionSettings
    {
        private readonly SettingsService _parent;
        internal InteractionSettings(SettingsService parent) => _parent = parent;

        public bool EnableAnimations => _parent.Get(nameof(EnableAnimations), true);
        public void SetEnableAnimations(bool value) => _parent.Set(nameof(EnableAnimations), value);

        public bool EnableSnapToGrid => _parent.Get(nameof(EnableSnapToGrid), true);
        public void SetEnableSnapToGrid(bool value) => _parent.Set(nameof(EnableSnapToGrid), value);

        /// <summary>
        /// 画布网格点的间距（像素）—— 只影响<b>看到的点阵</b>。
        /// <para>默认 10，与 <c>SnapGrid.GridSizeProperty</c> 的声明默认值一致；
        /// 两者都由 <c>App.UiTests</c> 的默认值对齐守卫钉住。</para>
        /// </summary>
        public int GridSize => _parent.Get(nameof(GridSize), 10);
        public void SetGridSize(int value) => _parent.Set(nameof(GridSize), value);

        /// <summary>
        /// 拖拽/缩放时的吸附步长（像素）—— 只影响<b>控件落在哪</b>。
        /// <para>与 <see cref="GridSize"/> 分开：网格点是 10、吸附是 5 是本项目的实际手感，
        /// 合并成一个数会连带改变拖拽精度。默认 5，等于接线前 <c>DesignCanvas.SnapThreshold</c> 的硬编码值。</para>
        /// </summary>
        public int SnapStep => _parent.Get(nameof(SnapStep), 5);
        public void SetSnapStep(int value) => _parent.Set(nameof(SnapStep), value);

        public bool ShowGrid => _parent.Get(nameof(ShowGrid), true);
        public void SetShowGrid(bool value) => _parent.Set(nameof(ShowGrid), value);

        /// <summary>
        /// 画布标尺开关。**尚未实现** —— 标尺功能不存在，改它没有任何效果。
        /// 保留键值只为不改动已有设置文件；批次 5 的受管清单会把它标成"未实现"。
        /// </summary>
        public bool ShowRulers => _parent.Get(nameof(ShowRulers), true);
        public void SetShowRulers(bool value) => _parent.Set(nameof(ShowRulers), value);

        public bool EnableUndoRedo => _parent.Get(nameof(EnableUndoRedo), true);
        public void SetEnableUndoRedo(bool value) => _parent.Set(nameof(EnableUndoRedo), value);

        /// <summary>
        /// 撤销栈深度。**尚未实现** —— <c>UndoRedoManager</c> 目前无容量上限
        /// （<c>new Stack&lt;IUndoableCommand&gt;(100)</c> 里的 100 是初始容量，不是上限）。
        /// </summary>
        public int UndoStackSize => _parent.Get(nameof(UndoStackSize), 100);
        public void SetUndoStackSize(int value) => _parent.Set(nameof(UndoStackSize), value);

        public bool EnableLivePreview => _parent.Get(nameof(EnableLivePreview), true);
        public void SetEnableLivePreview(bool value) => _parent.Set(nameof(EnableLivePreview), value);

        public bool EnableAutoSave => _parent.Get(nameof(EnableAutoSave), true);
        public void SetEnableAutoSave(bool value) => _parent.Set(nameof(EnableAutoSave), value);

        public int AutoSaveInterval => _parent.Get(nameof(AutoSaveInterval), 60);
        public void SetAutoSaveInterval(int value) => _parent.Set(nameof(AutoSaveInterval), value);
    }

    public sealed class DisplaySettings
    {
        private readonly SettingsService _parent;
        internal DisplaySettings(SettingsService parent) => _parent = parent;

        public double ZoomLevel => _parent.Get(nameof(ZoomLevel), 1.0);
        public void SetZoomLevel(double value) => _parent.Set(nameof(ZoomLevel), value);

        public string DefaultFont => _parent.Get(nameof(DefaultFont), "Noto Sans CJK SC");
        public void SetDefaultFont(string value) => _parent.Set(nameof(DefaultFont), value);

        public int FontStep => _parent.Get(nameof(FontStep), 2);
        public void SetFontStep(int value) => _parent.Set(nameof(FontStep), value);

        public string CanvasBackgroundColor => _parent.Get(nameof(CanvasBackgroundColor), "#131c31");
        public void SetCanvasBackgroundColor(string value) => _parent.Set(nameof(CanvasBackgroundColor), value);

        public bool ShowToolTips => _parent.Get(nameof(ShowToolTips), true);
        public void SetShowToolTips(bool value) => _parent.Set(nameof(ShowToolTips), value);

        public bool ShowPreviewOverlay => _parent.Get(nameof(ShowPreviewOverlay), true);
        public void SetShowPreviewOverlay(bool value) => _parent.Set(nameof(ShowPreviewOverlay), value);

        public string PreviewResolution => _parent.Get(nameof(PreviewResolution), "");
        public void SetPreviewResolution(string value) => _parent.Set(nameof(PreviewResolution), value);
    }

    public sealed class ThemeSettings
    {
        private readonly SettingsService _parent;
        internal ThemeSettings(SettingsService parent) => _parent = parent;

        public string AccentColor => _parent.Get(nameof(AccentColor), "");
        public void SetAccentColor(string value) => _parent.Set(nameof(AccentColor), value);
    }

    /// <summary>
    /// 最近打开的工程列表 —— 持久化最近 10 个 .hmi 文件路径，供引导页面快速访问。
    /// </summary>
    public sealed class RecentProjectsSettings
    {
        private readonly SettingsService _parent;
        private const int MaxItems = 10;
        internal RecentProjectsSettings(SettingsService parent) => _parent = parent;

        /// <summary>获取最近打开的工程路径列表（按最近优先排列）。</summary>
        /// <remarks>
        /// 用 <c>OfType&lt;string&gt;</c> 而非 <c>Cast&lt;string&gt;</c>：设置文件是用户可手改的，
        /// 里面混进一个非字符串元素就让启动页面抛异常，代价远大于忽略那一项。
        /// </remarks>
        public IReadOnlyList<string> List =>
            _parent.Get(nameof(RecentProjects), Array.Empty<string>()).OfType<string>().ToList();

        /// <summary>记录一个工程路径到最近列表（去重并移到最前）。</summary>
        public void Record(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var current = List.ToList();
            current.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            current.Insert(0, path);
            if (current.Count > MaxItems)
                current.RemoveRange(MaxItems, current.Count - MaxItems);
            _parent.Set(nameof(RecentProjects), current.ToArray());
        }

        /// <summary>清空最近工程列表。</summary>
        public void Clear() => _parent.Set(nameof(RecentProjects), Array.Empty<string>());
    }

    /// <summary>
    /// PC 模拟器构建设置 —— LVGL 源码目录与模拟器工作目录。
    /// LVGL 目录留空时自动探测（环境变量 LVGL_DIR / 工作目录上级 / 常用开发路径）。
    /// </summary>
    public sealed class SimulatorSettings
    {
        private readonly SettingsService _parent;
        internal SimulatorSettings(SettingsService parent) => _parent = parent;

        /// <summary>LVGL 源码目录（含 lvgl.h 与 CMakeLists.txt）。空 = 自动探测。</summary>
        public string LvglDir => _parent.Get(nameof(LvglDir), "");
        public void SetLvglDir(string value) => _parent.Set(nameof(LvglDir), value);

        /// <summary>模拟器工程工作目录。空 = 系统临时目录。</summary>
        public string WorkDir => _parent.Get(nameof(WorkDir), "");
        public void SetWorkDir(string value) => _parent.Set(nameof(WorkDir), value);
    }

    /// <summary>
    /// T113 真机部署设置 —— libhmi_ui.so 交叉编译。
    /// 目录留空时自动探测（环境变量 ZHSHU_HMI_SDK / 仓库内 firmware/Tina-SDK、Tina-Linux）。
    /// </summary>
    public sealed class DeviceSettings
    {
        private readonly SettingsService _parent;
        internal DeviceSettings(SettingsService parent) => _parent = parent;

        /// <summary>Tina SDK 根目录（含 package/gui/lvgl 与 out/）。空 = 自动探测。</summary>
        public string T113SdkRoot => _parent.Get(nameof(T113SdkRoot), "");
        public void SetT113SdkRoot(string value) => _parent.Set(nameof(T113SdkRoot), value);

        /// <summary>交叉编译器完整路径（arm-openwrt-linux-*-gcc）。空 = SDK 内自动查找。</summary>
        public string T113ToolchainGcc => _parent.Get(nameof(T113ToolchainGcc), "");
        public void SetT113ToolchainGcc(string value) => _parent.Set(nameof(T113ToolchainGcc), value);
    }

    /// <summary>
    /// **报表模板**（阶段 182）：每个数据源（<see cref="ReportSourceKeys"/>）一份**列布局模板** ——
    /// 列的顺序、列名覆盖、显式隐藏（口径见 <see cref="ReportTemplate"/>）。
    ///
    /// <para><b>为什么存应用级而不是工程文件</b>（用户拍板 · 选项 A）：与阶段 165 的**面板布局**同族 ——
    /// 它是**操作者偏好**（"我导出时习惯看哪几列"），换工程后仍应生效；而模板描述的是
    /// **运行数据**（审计/报警/采样都不在工程里）的视图。附带好处：三面板已能静态取到
    /// <c>App.SettingsService</c> ⇒ 零接线改动。</para>
    ///
    /// <para><b>存储形状</b>：一个"数据源键 → 条目数组"的字典。它是**结构化**的值，
    /// 走的是 <c>Coerce&lt;T&gt;</c> 里那条"复杂类型按目标类型反序列化"的路（阶段 182 新加）——
    /// 没有它，这个值会在读回时静默退回默认（"存得进去、重启就没了"）。</para>
    ///
    /// <para>⚠ <b>空模板不落盘</b>："用户把模板删干净了"与"从没设过"在行为上等价（都是恒等），
    /// 所以两种都存成"没有这一项" —— 免得磁盘上留一堆空壳，也免得判据要区分两种空。</para>
    /// </summary>
    public sealed class ReportTemplateSettings
    {
        /// <summary>偏好文件里的键名（**不要**改名：它是磁盘契约）。</summary>
        private const string StorageKey = "ReportTemplates";

        private readonly SettingsService _parent;
        internal ReportTemplateSettings(SettingsService parent) => _parent = parent;

        /// <summary>某个数据源的模板；没设过（或已删空）⇒ <c>null</c>。</summary>
        public ReportTemplate? Get(string source)
        {
            if (!ReportSourceKeys.IsKnown(source)) return null;
            var all = Load();
            return all.TryGetValue(source, out var entries) && entries.Length > 0
                ? new ReportTemplate(entries)
                : null;
        }

        /// <summary>写模板（空模板 ⇒ 等同 <see cref="Clear"/>）。</summary>
        public void Set(string source, ReportTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (!ReportSourceKeys.IsKnown(source)) return;

            if (template.IsEmpty)
            {
                Clear(source);
                return;
            }

            var all = new Dictionary<string, ReportTemplateEntry[]>(Load(), StringComparer.Ordinal)
            {
                [source] = template.Entries.ToArray(),
            };
            _parent.Set(StorageKey, all);
        }

        /// <summary>删掉某个数据源的模板。</summary>
        public void Clear(string source)
        {
            if (!ReportSourceKeys.IsKnown(source)) return;

            var all = new Dictionary<string, ReportTemplateEntry[]>(Load(), StringComparer.Ordinal);
            if (!all.Remove(source)) return;

            _parent.Set(StorageKey, all);
        }

        /// <summary>设了模板的数据源（顺序同 <see cref="ReportSourceKeys.All"/>）。</summary>
        public IReadOnlyList<string> Configured
        {
            get
            {
                var all = Load();
                var result = new List<string>();
                foreach (var source in ReportSourceKeys.All)
                    if (all.TryGetValue(source, out var entries) && entries.Length > 0) result.Add(source);

                return result;
            }
        }

        private Dictionary<string, ReportTemplateEntry[]> Load() =>
            _parent.Get(StorageKey, new Dictionary<string, ReportTemplateEntry[]>(StringComparer.Ordinal));
    }

    /// <summary>
    /// **定时/自动导出设置**（阶段 186，登记 `#87`）：开关 ＋ 间隔（分钟）＋ 目标目录。
    ///
    /// <para>三处默认值都刻意"不干活"：<c>Enabled = false</c> ⇒ 装完不会突然开始往磁盘写东西；
    /// 目录留空 ⇒ 即便开了也只会报"没配目录"而**不弹选择器**（后台节拍里弹窗是最坏的行为）。</para>
    ///
    /// <para>⚠ 间隔下限取 1 分钟、上限 24 小时：更密没有意义（数据源是分钟级的运维数据），
    /// 而上限之外用户多半想要的是"每天一次"这种外部计划任务（见专题 §2 的候选表）。</para>
    /// </summary>
    public sealed class AutoExportSettings
    {
        private readonly SettingsService _parent;
        internal AutoExportSettings(SettingsService parent) => _parent = parent;

        /// <summary>功能开关（默认关）。</summary>
        public bool Enabled => _parent.Get(nameof(Enabled), false);
        public void SetEnabled(bool value) => _parent.Set(nameof(Enabled), value);

        /// <summary>间隔（分钟，默认 30）。</summary>
        public int IntervalMinutes => _parent.Get(nameof(IntervalMinutes), 30);
        public void SetIntervalMinutes(int value) => _parent.Set(nameof(IntervalMinutes), value);

        /// <summary>目标目录（默认空 ＝ 没配置）。</summary>
        public string Directory => _parent.Get(nameof(Directory), "");
        public void SetDirectory(string value) => _parent.Set(nameof(Directory), value ?? "");

        /// <summary>服务要的那一份（纯数据）。</summary>
        public AutoExportOptions Options() => new(Enabled, IntervalMinutes, Directory);
    }

    /// <summary>
    /// 项目浏览器设置 —— 右键增强菜单（管理员可自定义菜单项开关）。
    /// </summary>
    public sealed class ExplorerSettings
    {
        private readonly SettingsService _parent;
        internal ExplorerSettings(SettingsService parent) => _parent = parent;

        /// <summary>是否启用右键增强菜单。关闭后保留浏览器默认行为。</summary>
        public bool EnableContextMenu => _parent.Get(nameof(EnableContextMenu), true);
        public void SetEnableContextMenu(bool value) => _parent.Set(nameof(EnableContextMenu), value);

        /// <summary>屏幕节点：打开（切换为当前屏幕）。</summary>
        public bool ShowOpenScreen => _parent.Get(nameof(ShowOpenScreen), true);
        public void SetShowOpenScreen(bool value) => _parent.Set(nameof(ShowOpenScreen), value);

        /// <summary>分组节点：新建资源。</summary>
        public bool ShowCreate => _parent.Get(nameof(ShowCreate), true);
        public void SetShowCreate(bool value) => _parent.Set(nameof(ShowCreate), value);

        /// <summary>屏幕节点：设为启动屏幕。</summary>
        public bool ShowStartupScreen => _parent.Get(nameof(ShowStartupScreen), true);
        public void SetShowStartupScreen(bool value) => _parent.Set(nameof(ShowStartupScreen), value);

        /// <summary>屏幕节点：设为/取消公共页面。</summary>
        public bool ShowPublicPage => _parent.Get(nameof(ShowPublicPage), true);
        public void SetShowPublicPage(bool value) => _parent.Set(nameof(ShowPublicPage), value);

        /// <summary>重命名菜单项。</summary>
        public bool ShowRename => _parent.Get(nameof(ShowRename), true);
        public void SetShowRename(bool value) => _parent.Set(nameof(ShowRename), value);

        /// <summary>复制（屏幕/控件）菜单项。</summary>
        public bool ShowDuplicate => _parent.Get(nameof(ShowDuplicate), true);
        public void SetShowDuplicate(bool value) => _parent.Set(nameof(ShowDuplicate), value);

        /// <summary>删除菜单项。</summary>
        public bool ShowDelete => _parent.Get(nameof(ShowDelete), true);
        public void SetShowDelete(bool value) => _parent.Set(nameof(ShowDelete), value);

        /// <summary>控件：复制/粘贴。</summary>
        public bool ShowCopyPaste => _parent.Get(nameof(ShowCopyPaste), true);
        public void SetShowCopyPaste(bool value) => _parent.Set(nameof(ShowCopyPaste), value);

        /// <summary>控件：上移/下移图层顺序。</summary>
        public bool ShowMoveLayer => _parent.Get(nameof(ShowMoveLayer), true);
        public void SetShowMoveLayer(bool value) => _parent.Set(nameof(ShowMoveLayer), value);
    }
}

/// <summary>
/// 用户界面偏好设置 —— 所有设置项都有合理的默认值。
/// </summary>
public sealed class UserSettings : Dictionary<string, object?>
{
    public UserSettings()
    {
        // 布局
        this["LeftPanelProportion"] = 0.17;
        this["RightPanelProportion"] = 0.17;
        this["BottomPanelProportion"] = 0.3;
        this["ShowStatusBar"] = true;
        this["ShowCommandBar"] = true;
        this["ShowMenu"] = true;
        this["AutoHidePanels"] = false;

        // 交互
        this["EnableAnimations"] = true;
        this["EnableSnapToGrid"] = true;
        this["GridSize"] = 10;
        this["SnapStep"] = 5;
        this["ShowGrid"] = true;
        this["ShowRulers"] = true;
        this["EnableUndoRedo"] = true;
        this["UndoStackSize"] = 100;
        this["EnableLivePreview"] = true;
        this["EnableAutoSave"] = true;
        this["AutoSaveInterval"] = 60;

        // 显示
        this["ZoomLevel"] = 1.0;
        this["DefaultFont"] = "Noto Sans CJK SC";
        this["FontStep"] = 2;
        this["CanvasBackgroundColor"] = "#131c31";
        this["ShowToolTips"] = true;
        this["ShowPreviewOverlay"] = true;
        this["PreviewResolution"] = "";

        // 主题
        this["AccentColor"] = "";

        // 最近工程
        this["RecentProjects"] = Array.Empty<string>();

        // 项目浏览器右键菜单
        this["EnableContextMenu"] = true;
        this["ShowOpenScreen"] = true;
        this["ShowCreate"] = true;
        this["ShowStartupScreen"] = true;
        this["ShowPublicPage"] = true;
        this["ShowRename"] = true;
        this["ShowDuplicate"] = true;
        this["ShowDelete"] = true;
        this["ShowCopyPaste"] = true;
        this["ShowMoveLayer"] = true;
    }
}