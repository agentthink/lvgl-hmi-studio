using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// 脚本编辑器对话框 —— 创建、编辑和管理 Lua 脚本。
/// 左右分栏设计：左侧脚本列表 + 右侧代码编辑区域。
/// </summary>
public partial class ScriptEditorView : ChromeWindow
{
    private readonly ObservableCollection<HmiScript> _scripts;
    private readonly ObservableCollection<ScriptBinding> _bindings;
    private readonly HmiProject _project;
    private readonly ScriptEngineService _engine;
    private readonly Func<string, double?>? _externalValueSource;
    private readonly Services.AiPreviewManager? _aiManager;
    private HmiScript? _currentScript;
    private bool _isUpdating;

    /// <summary>
    /// 列表**重建窗口**标记（阶段 192）：<c>RefreshScriptList</c> 里 <c>Items.Clear()</c> 引发的
    /// <c>SelectionChanged</c> 不是用户意图，不能让它把 <c>_currentScript</c> 清掉
    /// （见 <see cref="RefreshScriptList"/> 的注释）。
    /// </summary>
    private bool _rebuildingScriptList;
    private readonly DispatcherTimer _debounceTimer;

    /// <summary>
    /// 脚本列表发生变化时触发（通知外部保存）。
    /// </summary>
    public event EventHandler? ScriptListChanged;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public ScriptEditorView()
    {
        _scripts = null!;
        _bindings = null!;
        _project = null!;
        _engine = null!;
        _externalValueSource = null;
        _debounceTimer = null!;
        InitializeComponent();
    }

    public ScriptEditorView(ObservableCollection<HmiScript> scripts,
                            ObservableCollection<ScriptBinding> bindings,
                            HmiProject project,
                            Func<string, double?>? debugValueProvider = null,
                            Services.AiPreviewManager? aiManager = null) : this()
    {
        _scripts = scripts;
        _bindings = bindings;
        _project = project;
        _externalValueSource = debugValueProvider;
        _engine = new ScriptEngineService();
        _aiManager = aiManager;

        // 未传入 AI 管理器时隐藏 AI 生成按钮
        if (_aiManager is null && BtnAiGenerate is not null)
        {
            BtnAiGenerate.IsVisible = false;
        }

        // 将已有脚本加载到引擎
        foreach (var s in scripts)
            _engine.LoadScript(s);

        // 初始化去抖定时器（500ms 延迟校验）
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += OnDebounceTimerTick;

        InitializeComboBoxes();
        RefreshScriptList();
        UpdateRightPanel();

        WireEvents();
    }

    // ──────────────────────────────────────────────
    //  ComboBox 初始化
    // ──────────────────────────────────────────────

    private void InitializeComboBoxes()
    {
        // 筛选 ComboBox
        CmbCategoryFilter.Items.Clear();
        CmbCategoryFilter.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.ScriptAll], Tag = "" });
        foreach (var cat in new[] { "OnClick", "OnValueChanged", "Timer", "Alarm" })
        {
            CmbCategoryFilter.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });
        }
        CmbCategoryFilter.SelectedIndex = 0;

        // 分类 ComboBox
        CboCategory.Items.Clear();
        foreach (var cat in new[] { "OnClick", "OnValueChanged", "Timer", "Alarm" })
        {
            CboCategory.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });
        }
        CboCategory.SelectedIndex = 0;
    }

    // ──────────────────────────────────────────────
    //  事件绑定
    // ──────────────────────────────────────────────

    private void WireEvents()
    {
        // 左侧按钮
        BtnNewScript.Click += (_, _) => OnNewScript();
        BtnDeleteScript.Click += (_, _) => OnDeleteScript();
        ScriptList.SelectionChanged += OnScriptSelectionChanged;
        CmbCategoryFilter.SelectionChanged += OnFilterChanged;

        // 编辑面板字段变更
        TxtScriptName.TextChanged += OnScriptFieldChanged;
        TxtDescription.TextChanged += OnScriptFieldChanged;
        ChkEnabled.IsCheckedChanged += OnScriptFieldChanged;
        CboCategory.SelectionChanged += OnCategoryChanged;

        // 代码编辑器 —— 文本变化触发去抖校验
        TxtCodeEditor.TextChanged += OnCodeTextChanged;

        // 模板按钮
        BtnTemplateOnClick.Click += (_, _) => InsertTemplate(ScriptTemplate.OnClick);
        BtnTemplateOnValueChanged.Click += (_, _) => InsertTemplate(ScriptTemplate.OnValueChanged);
        BtnTemplateTimer.Click += (_, _) => InsertTemplate(ScriptTemplate.Timer);
        BtnTemplateAlarm.Click += (_, _) => InsertTemplate(ScriptTemplate.Alarm);
        BtnAiGenerate.Click += (_, _) => OnAiGenerateScript();

        // 底部操作按钮
        BtnValidate.Click += (_, _) => ValidateCurrentScript();
        BtnInsertTemplate.Click += (_, _) => ShowTemplatePicker();
        BtnDebugRun.Click += (_, _) => RunCurrentScript();
        BtnClearOutput.Click += (_, _) => TxtRunOutput.Text = "";
        BtnHelp.Click += (_, _) => ShowLuaHelp();

        // 键盘快捷键
        KeyDown += OnEditorKeyDown;
    }

    // ──────────────────────────────────────────────
    //  左侧：脚本列表管理
    // ──────────────────────────────────────────────

    private void RefreshScriptList(string? filterCategory = null)
    {
        // ⚠ **重建期间必须挡住 `OnScriptSelectionChanged`**（阶段 192 修 `#89` 时一并修的）：
        //   `Items.Clear()` 会引发一次 `SelectionChanged`（选中 → null），而它的 `else` 分支会把
        //   `_currentScript` 置 null、并 `UpdateRightPanel()` 收起编辑面板。这条路径在
        //   **阶段 192 之前根本走不到** —— 三个字段处理器被 `Parent == null` 整个挡掉了，
        //   所以"改一个字 ⇒ 面板收起、当前脚本丢失、后面每个字都不落模型"这个症状一直没人看见
        //   （会话历史里另有两处同形：`AlarmConfigView` 的重建、`AnimationEditorView` 的关键帧列表）。
        //   重建期间的选择变化**不是用户意图**，所以下面"按 Id 找回正在编辑的那一条"能成立。
        _rebuildingScriptList = true;
        try
        {
            ScriptList.Items.Clear();

            var filtered = string.IsNullOrEmpty(filterCategory)
                ? _scripts
                : new ObservableCollection<HmiScript>(_scripts.Where(s => s.Category == filterCategory));

            foreach (var script in filtered)
            {
                var item = CreateScriptListItem(script);
                ScriptList.Items.Add(item);
            }

            ListStatusText.Text = AppLocalization.Service.Format(Lang.ScriptCount, filtered.Count);
            EmptyHint.IsVisible = ScriptList.Items.Count == 0;
        }
        finally
        {
            _rebuildingScriptList = false;
        }

        // 恢复选中：找回**正在编辑的那一条**（`Items.Clear()` 已经把选中清掉了，所以按 Id 重选）。
        // ⚠ 这里读的 `_currentScript` 之所以还活着，靠的就是上面那道重建窗口 —— 少了它，
        //   下面这个循环永远找不到东西、`restored` 恒 false（症状：改一个字就丢当前脚本）。
        var restored = false;
        if (_currentScript != null)
        {
            foreach (var item in ScriptList.Items)
            {
                if (item is ListBoxItem lbi && lbi.Tag is HmiScript s && s.Id == _currentScript.Id)
                {
                    ScriptList.SelectedItem = lbi;
                    restored = true;
                    break;
                }
            }
        }

        // 它被筛掉了（或本来就没有当前脚本）⇒ 如实清空并收起面板 ——
        // 这正是「筛选到别的分类 ⇒ 编辑面板隐藏」那条既有行为的出处。
        if (!restored)
        {
            _currentScript = null;
            UpdateRightPanel();
        }
    }

    private ListBoxItem CreateScriptListItem(HmiScript script)
    {
        var categoryColor = GetCategoryColor(script.Category);
        var isDisabled = !script.IsEnabled;

        // 脚本名称
        var nameText = new TextBlock
        {
            Text = script.Name,
            Foreground = isDisabled
                ? new SolidColorBrush(Color.FromRgb(100, 110, 130))
                : HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        // 分类标签（彩色徽章）
        var categoryBadge = new Border
        {
            Background = new SolidColorBrush(categoryColor),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = script.Category,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 10
            }
        };

        // 启用状态
        var statusText = new TextBlock
        {
            Text = script.IsEnabled
                ? AppLocalization.Service[Lang.ScriptEnabled]
                : AppLocalization.Service[Lang.ScriptDisabled],
            Foreground = script.IsEnabled
                ? new SolidColorBrush(UiPalette.ValidationOk)
                : new SolidColorBrush(UiPalette.ValidationIdle),
            FontSize = 10,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var topRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children = { nameText, categoryBadge, statusText }
        };

        // 描述（截断）
        var descText = new TextBlock
        {
            Text = !string.IsNullOrEmpty(script.Description)
                ? (script.Description.Length > 30 ? script.Description[..30] + "..." : script.Description)
                : AppLocalization.Service[Lang.ScriptNoDesc],
            Foreground = isDisabled
                ? new SolidColorBrush(Color.FromRgb(80, 88, 105))
                : HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var infoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { topRow, descText }
        };

        return new ListBoxItem
        {
            Content = infoPanel,
            Tag = script,
            Padding = new Thickness(10, 6)
        };
    }

    private Color GetCategoryColor(string category)
    {
        return category switch
        {
            "OnClick" => UiPalette.EventOnClick,       // 蓝色
            "OnValueChanged" => UiPalette.EventOnValueChanged, // 绿色
            "Timer" => UiPalette.EventTimer,          // 橙色
            "Alarm" => UiPalette.EventAlarm,         // 红色
            _ => UiPalette.EventDefault                // 灰色
        };
    }

    private void OnNewScript()
    {
        var script = new HmiScript
        {
            Name = AppLocalization.Service.Format(Lang.ScriptNewDefault, _scripts.Count + 1),
            Category = "OnClick",
            Code = AppLocalization.Service["script.lua.template"],
            Description = ""
        };

        _scripts.Add(script);
        _engine.LoadScript(script);

        RefreshScriptList(GetActiveFilter());

        // 选中新脚本
        SelectScript(script);
        ScriptListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteScript()
    {
        if (ScriptList.SelectedItem is not ListBoxItem selectedItem ||
            selectedItem.Tag is not HmiScript script)
            return;

        if (_scripts.Count <= 1)
        {
            ListStatusText.Text = AppLocalization.Service[Lang.ScriptKeepOne];
            return;
        }

        _scripts.Remove(script);
        _engine.UnloadScript(script.Id);

        // 移除相关绑定
        var relatedBindings = _bindings.Where(b => b.ScriptId == script.Id).ToList();
        foreach (var b in relatedBindings)
            _bindings.Remove(b);

        if (_currentScript?.Id == script.Id)
            _currentScript = null;

        RefreshScriptList(GetActiveFilter());

        // 选中第一个可用脚本
        if (ScriptList.Items.Count > 0 && ScriptList.SelectedItem == null)
        {
            if (ScriptList.Items[0] is ListBoxItem firstItem && firstItem.Tag is HmiScript firstScript)
                SelectScript(firstScript);
        }
        else
        {
            UpdateRightPanel();
        }

        ListStatusText.Text = AppLocalization.Service.Format(Lang.ScriptDeleted, script.Name);
        ScriptListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnScriptSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 重建列表期间的选中变化不是用户意图（`Items.Clear()` 会造出一次"选中 → null"）——
        // 让它走 else 分支会把 `_currentScript` 清掉、面板收起（症状：改一个字就丢当前脚本）。
        if (_rebuildingScriptList) return;

        if (ScriptList.SelectedItem is ListBoxItem item && item.Tag is HmiScript script)
        {
            SelectScript(script);
        }
        else if (ScriptList.Items.Count > 0)
        {
            // 没有选中时，尝试选中第一个
            ScriptList.SelectedIndex = 0;
        }
        else
        {
            _currentScript = null;
            UpdateRightPanel();
        }
    }

    private void SelectScript(HmiScript script)
    {
        _currentScript = script;
        PopulateEditPanel(script);
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshScriptList(GetActiveFilter());
    }

    private string? GetActiveFilter()
    {
        if (CmbCategoryFilter.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrEmpty(tag))
            return tag;
        return null;
    }

    // ──────────────────────────────────────────────
    //  右侧：编辑面板
    // ──────────────────────────────────────────────

    private void UpdateRightPanel()
    {
        var hasScript = _currentScript != null;
        EmptyHint.IsVisible = !hasScript;
        EditPanel.IsVisible = hasScript;
        CodeEditorPanel.IsVisible = hasScript;
        BottomPanel.IsVisible = hasScript;
    }

    private void PopulateEditPanel(HmiScript script)
    {
        _isUpdating = true;

        TxtScriptName.Text = script.Name;
        TxtDescription.Text = script.Description ?? "";
        ChkEnabled.IsChecked = script.IsEnabled;

        // ⚠ 代码区这一句**必须在 `_isUpdating` 窗口内**（阶段 192 修 `#89`）：它原先是
        //   `_isUpdating = false` **之后**才写的，当时唯一挡住它触发 `OnCodeTextChanged` 的
        //   就是那句 `Parent == null` 守卫 —— 守卫一去掉，这里就会把刚读出的代码再写一遍
        //   并点起 500ms 去抖定时器（"一打开对话框就校验一次"）。收进窗口内两种框架行为下都对：
        //   程序化赋值触发 `TextChanged` 时被 `_isUpdating` 挡掉，不触发时本来也无事。
        TxtCodeEditor.Text = script.Code;

        // 分类选中
        foreach (var item in CboCategory.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string cat && cat == script.Category)
            {
                CboCategory.SelectedItem = item;
                break;
            }
        }

        _isUpdating = false;

        // 更新校验状态
        UpdateValidationStatus(script);

        UpdateRightPanel();
    }

    // ──────────────────────────────────────────────
    //  字段变更
    // ──────────────────────────────────────────────

    /// <summary>
    /// 三个字段处理器（本方法 / <see cref="OnCategoryChanged"/> / <see cref="OnCodeTextChanged"/>）。
    ///
    /// <para>⚠ <b>阶段 192 修 `#89`：这里原先是 <c>if (_isUpdating || _currentScript == null || Parent == null) return;</c></b>
    /// —— 而本视图是 <c>ChromeWindow : Window</c>，生产里走 <c>ShowDialog(this)</c>（`MainWindow` /
    /// `Wiring.Panels` 三处），<c>Owner</c> 有、<c>Parent</c> <b>恒为 null</b> ⇒ 处理器首行就返回，
    /// 用户打的字一个都不落模型（改名 / 改描述 / 改代码全丢，`ScriptListChanged` 也不触发）。
    /// 阶段 191 按生产形状把这条量了出来（专题 §8.3），本阶段修掉：**`Parent == null` 这个条件
    /// 去掉**，防重入继续由 <c>_isUpdating</c> ＋ <c>_currentScript</c> 负责 —— 程序化回填
    /// （<see cref="PopulateEditPanel"/> / <see cref="InsertTemplate"/> / AI 生成后的刷新）
    /// 全部收进了 <c>_isUpdating</c> 窗口，所以这两个条件已经够用。</para>
    /// </summary>
    private void OnScriptFieldChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _currentScript == null) return;

        _isUpdating = true;

        _currentScript.Name = TxtScriptName.Text ?? "";
        _currentScript.Description = TxtDescription.Text;
        _currentScript.IsEnabled = ChkEnabled.IsChecked ?? true;

        // 刷新脚本列表项
        RefreshScriptList(GetActiveFilter());

        ScriptListChanged?.Invoke(this, EventArgs.Empty);

        _isUpdating = false;
    }

    private void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentScript == null) return;

        if (CboCategory.SelectedItem is ComboBoxItem item && item.Tag is string category)
        {
            _currentScript.Category = category;
            RefreshScriptList(GetActiveFilter());
            ScriptListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ──────────────────────────────────────────────
    //  代码编辑 & 去抖校验
    // ──────────────────────────────────────────────

    private void OnCodeTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _currentScript == null) return;

        _currentScript.Code = TxtCodeEditor.Text ?? "";
        _currentScript.ModifiedAt = DateTime.UtcNow;

        // 重启去抖定时器
        _debounceTimer.Stop();
        _debounceTimer.Start();

        ScriptListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        ValidateCurrentScript();
    }

    // ──────────────────────────────────────────────
    //  语法校验
    // ──────────────────────────────────────────────

    private void ValidateCurrentScript()
    {
        if (_currentScript == null) return;

        UpdateValidationStatus(_currentScript);
    }

    private void UpdateValidationStatus(HmiScript script)
    {
        var result = _engine.Validate(script);

        if (!result.IsValid)
        {
            ValidationDot.Fill = new SolidColorBrush(UiPalette.ValidationError); // 红色
            // 文案走 ExternalText.Describe：按 script.issue.<code 小写> 查字典 + 填 Args，
            // 字典缺键才回落 Core 的中文原文；**行号由它加**（Message 里不含行号）。
            var errorMsg = result.Errors.Count > 0
                ? AppLocalization.Service.Format(Lang.ScriptErrorPrefix, ExternalText.Describe(result.Errors[0]))
                : AppLocalization.Service[Lang.ScriptSyntaxError];
            TxtValidationStatus.Text = errorMsg;
            TxtValidationStatus.Foreground = new SolidColorBrush(UiPalette.ValidationError);
            ToolTip.SetTip(TxtValidationStatus, string.Join("\n", result.Errors.Select(ExternalText.Describe)));
        }
        else if (result.Warnings.Count > 0)
        {
            // 语法没问题，但「运行前检查」发现了要知情的风险：代码引用了设备上**可能不存在**的
            // 库 / 字段（bit32 / table.pack / math.type / utf8 …）。这类问题真解析在原理上
            // 看不见（见 LuaRejectStage.Runtime），只能靠单独扫描。
            //
            // 这是**非阻断**提示，所以用黄色而不是红色 —— 设备侧目标 Lua 版本至今未定
            // （LuaDialect.Target = Unknown），同一个 bit32 在 5.2 上正常、在 5.1 上必炸。
            // 判红等于把"待定"当成"错误"。
            ValidationDot.Fill = new SolidColorBrush(UiPalette.ValidationWarn); // 黄色
            var warnMsg = AppLocalization.Service.Format(Lang.ScriptWarnPrefix, ExternalText.Describe(result.Warnings[0]));
            if (result.Warnings.Count > 1)
                warnMsg += AppLocalization.Service.Format(Lang.ScriptWarnMore, result.Warnings.Count - 1);
            TxtValidationStatus.Text = warnMsg;
            TxtValidationStatus.Foreground = new SolidColorBrush(UiPalette.ValidationWarn);
            // 状态栏一行只放得下首条，全部提示挂 ToolTip（走同一套渲染，与首条同源）
            ToolTip.SetTip(TxtValidationStatus, string.Join("\n", result.Warnings.Select(ExternalText.Describe)));
        }
        else
        {
            ValidationDot.Fill = new SolidColorBrush(UiPalette.ValidationOk); // 绿色
            TxtValidationStatus.Text = AppLocalization.Service[Lang.ScriptSyntaxOk];
            TxtValidationStatus.Foreground = new SolidColorBrush(UiPalette.ValidationOk);
            ToolTip.SetTip(TxtValidationStatus, null);
        }
    }

    // ──────────────────────────────────────────────
    //  模板插入
    // ──────────────────────────────────────────────

    /// <summary>
    /// 将模板代码插入到光标位置。
    /// </summary>
    private void InsertTemplate(ScriptTemplate template)
    {
        if (_currentScript == null) return;

        var cursorPos = TxtCodeEditor.CaretIndex;
        var text = TxtCodeEditor.Text ?? "";

        // 在光标位置插入模板代码
        var newText = text.Insert(cursorPos, template.Code);

        // ⚠ 程序化写 UI 收进 `_isUpdating`（阶段 192 修 `#89` 同批）：处理器不再被 `Parent` 挡掉之后，
        //   这一句若漏在窗口外，就会经 `OnCodeTextChanged` 把**同一份**文本再写一遍模型
        //   并点起 500ms 去抖定时器。模型仍由下面那行**显式**写（这条路径不依赖事件）。
        _isUpdating = true;
        TxtCodeEditor.Text = newText;
        TxtCodeEditor.CaretIndex = cursorPos + template.Code.Length;
        _isUpdating = false;

        _currentScript.Code = newText;

        TxtValidationStatus.Text = AppLocalization.Service.Format(Lang.ScriptTemplateInserted, template.Name);
        TxtValidationStatus.Foreground = new SolidColorBrush(UiPalette.ValidationIdle);
        ScriptListChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// AI 生成脚本 → 替换当前代码。失败由 UiOperationGuard 兜底
    /// （回落到主窗口状态栏 + 落盘日志），对话框异常不再静默消失。
    /// </summary>
    private void OnAiGenerateScript() =>
        UiOperationGuard.Run(Lang.UiOpAiScript, GenerateScriptCoreAsync);

    private async Task GenerateScriptCoreAsync()
    {
        if (_aiManager is null) return;
        if (_currentScript is null)
        {
            OnNewScript(); // 无当前脚本时先新建
            if (_currentScript is null) return;
        }

        var dialog = new AiScriptDialog(_aiManager)
        {
            CurrentProject = _project
        };
        var script = await dialog.ShowDialog<HmiScript?>(this);
        if (script is null) return;

        // 替换当前脚本内容
        _currentScript.Name = script.Name;
        _currentScript.Description = script.Description;
        _currentScript.Category = script.Category;
        _currentScript.Code = script.Code;
        _currentScript.Version++;

        // 刷新 UI（⚠ 同 `InsertTemplate`：程序化写收进 `_isUpdating`，模型已由上面四行显式写好）
        _isUpdating = true;
        TxtScriptName.Text = _currentScript.Name;
        TxtDescription.Text = _currentScript.Description;
        TxtCodeEditor.Text = script.Code;
        foreach (var item in CboCategory.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string cat && cat == _currentScript.Category)
            {
                CboCategory.SelectedIndex = CboCategory.Items.IndexOf(item);
                break;
            }
        }
        _isUpdating = false;

        RefreshScriptList();

        TxtValidationStatus.Text = AppLocalization.Service.Format(Lang.ScriptGenerated, script.Name, script.Code.Length);
        TxtValidationStatus.Foreground = new SolidColorBrush(UiPalette.ValidationOk);
        ScriptListChanged?.Invoke(this, EventArgs.Empty);
        ValidateCurrentScript();
    }

    /// <summary>
    /// 弹出模板选择菜单（按钮点击时的备选方案）。
    /// </summary>
    private void ShowTemplatePicker()
    {
        var templates = ScriptEngineService.GetTemplates();
        var flyout = new MenuFlyout();

        foreach (var t in templates)
        {
            var item = new MenuItem
            {
                Header = $"{t.Name} ({t.Category})",
                Tag = t
            };
            var captured = t;
            item.Click += (_, _) => InsertTemplate(captured);
            flyout.Items.Add(item);
        }

        flyout.ShowAt(BtnInsertTemplate);
    }

    // ──────────────────────────────────────────────
    //  I5: 调试运行（沙箱 + 项目/仿真变量）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 在沙箱中运行当前脚本。Lua 回调体先经 PrepareLuaForSandbox 翻译，
    /// 变量源为项目变量种子 + 外部值源（预览仿真器数据）。
    /// </summary>
    private void RunCurrentScript()
    {
        if (_currentScript == null) return;

        var sandbox = new ScriptSandboxExecutor
        {
            ExternalValueSource = _externalValueSource,
            TimeoutMs = 2000
        };
        sandbox.SeedProjectVariables(_project);

        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(_currentScript.Code ?? "");
        if (prepared == null)
        {
            // 无 function 定义 → 按原始代码执行
            RunOutputPanel.IsVisible = true;
            TxtRunOutput.Text = AppLocalization.Service.Format(Lang.ScriptNoFunction, _currentScript.Code ?? "");
            var rawResult = sandbox.Execute(_currentScript.Code ?? "");
            AppendResult(rawResult);
            return;
        }

        var result = sandbox.Execute(prepared);
        RunOutputPanel.IsVisible = true;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(AppLocalization.Service[Lang.ScriptTranslatedCode]);
        sb.AppendLine(prepared);
        sb.AppendLine(AppLocalization.Service[Lang.ScriptExecResult]);
        TxtRunOutput.Text = sb.ToString();
        AppendResult(result);
    }

    private void AppendResult(ScriptExecutionResult result)
    {
        var sb = new System.Text.StringBuilder(TxtRunOutput.Text ?? "");
        if (!result.Success)
        {
            sb.AppendLine(AppLocalization.Service.Format(Lang.ScriptExecFail, result.Error ?? ""));
        }
        else
        {
            sb.AppendLine(AppLocalization.Service.Format(Lang.ScriptExecOk, result.Outputs.Count));
        }

        if (result.Outputs.Count > 0)
        {
            sb.AppendLine(AppLocalization.Service[Lang.ScriptOutputVars]);
            foreach (var (name, value) in result.Outputs)
            {
                sb.AppendLine($"  {name} = {value}");
            }
        }

        if (result.Log.Count > 0)
        {
            sb.AppendLine(AppLocalization.Service[Lang.ScriptExecLog]);
            foreach (var line in result.Log)
                sb.AppendLine("  " + line);
        }

        TxtRunOutput.Text = sb.ToString();
    }

    // ──────────────────────────────────────────────
    //  Lua 帮助
    // ──────────────────────────────────────────────

    /// <summary>
    /// 构造「Lua 帮助」窗（**不显示**）。
    ///
    /// <para><b>为什么把构造与显示分开：</b>这个窗在 <b>Avalonia.Headless</b> 下
    /// <c>Show()</c> 会让布局**不收敛**、把整个测试进程挂住（`EXIT=124`，不是断言失败也不是 OOM），
    /// 触发条件是「垂直可滚动的 `ScrollViewer` + `TextWrapping.Wrap` 的多行 `TextBlock`」在
    /// **首次布局**里定不下来 —— 取证与两侧对照见
    /// `docs/2026-09-22-headless-scrollviewer-wrap-hang.md`。真平台（X11/Skia）下不挂，
    /// 所以这是 headless 的限制、**不是产品缺陷**，产品代码不该为了它改渲染方式
    /// （实测：`Wrap` 改 `NoWrap` 会让 en-US 文案的最长行 665px 超宽 ⇒ 用户要横向滚动）。</para>
    ///
    /// <para>分开之后，判据 <c>LuaHelpDialogUiTests</c> 可以直接拿这个窗口对象核对
    /// **标题 / 正文 / 关闭按钮文案 / 关闭按钮的接线**（这块面此前是 0 覆盖），
    /// 而不触发那条会挂死的布局路径。</para>
    ///
    /// <para><b>「关闭按钮的接线」也测得到（阶段 75 更正）：</b>阶段 73 曾判定它「要真显示窗才观察得到」，
    /// 实测**不成立** —— 从未 <c>Show()</c> 的 <c>Window.Close()</c> 不抛且会触发 <c>Closed</c>，
    /// 而未 attach 的按钮上 <c>RaiseEvent(Button.ClickEvent)</c> 会触发直接挂上的 handler
    /// ⇒ 判据能观察到「点那个按钮到底有没有调 <c>Close()</c>」。故此处的接线写成了
    /// **对按钮实例的直接引用**（见方法体内注释），不再走 <c>Children[1]</c> 这种位置下标。</para>
    ///
    /// <para>⚠ 别把 <c>ShowDialog</c> 挪进这个方法里 —— <see cref="ShowLuaHelp"/> 才是显示入口，
    /// 而 UI 测试里点 <c>BtnHelp</c> 就会挂死，所以 <c>DialogInnerInvocationUiTests</c>
    /// 把它登记在 <c>Never</c> 桶。</para>
    /// </summary>
    internal static Window CreateLuaHelpDialog()
    {
        var helpText = AppLocalization.Service[Lang.ScriptLuaHelpText];

        // 关闭按钮先建出来并**留住引用** —— 接线时直接用它，不回到 Children 里按下标找。
        // 按下标找（原先的 `grid.Children[1]`）有一个**静默失效面**：往那个 Grid 里插一个控件，
        // 下标就不再指向它，`is Button` 一失配整段 if 被跳过 ⇒ 关闭按钮**不抛不报、就是点不动**；
        // 若插进去的恰好也是 Button，`Close()` 还会接到**错的按钮**上（阶段 75 两种形态都实测复现）。
        // 守这件事的判据：`LuaHelpDialogUiTests.HelpDialog_CloseButtonIsActuallyWiredToClose`。
        var closeBtn = new Button
        {
            Content = AppLocalization.Service[Lang.GenClose],
            Background = HmiEditor.App.Theme.AccentHover,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(20, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            [Grid.RowProperty] = 1
        };

        var helpDialog = new Window
        {
            Title = AppLocalization.Service[Lang.ScriptHelpTitle],
            Width = 480,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = HmiEditor.App.Theme.BgWindow,
            Content = new Border
            {
                Background = HmiEditor.App.Theme.BgPanel,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("*,Auto"),
                    Children =
                    {
                        new ScrollViewer
                        {
                            Content = new TextBlock
                            {
                                Text = helpText,
                                Foreground = HmiEditor.App.Theme.TextPrimary,
                                FontSize = 13,
                                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                                TextWrapping = TextWrapping.Wrap,
                                LineHeight = 20
                            }
                        },
                        closeBtn
                    }
                }
            }
        };

        // 关闭按钮事件：**直接引用**上面那个按钮实例 —— 不按 `Content`/`Child`/`Children[i]` 逐层找
        closeBtn.Click += (_, _) => helpDialog.Close();

        return helpDialog;
    }

    private void ShowLuaHelp() => CreateLuaHelpDialog().ShowDialog(this);

    // ──────────────────────────────────────────────
    //  键盘快捷键
    // ──────────────────────────────────────────────

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+S 快速保存
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            QuickSave();
            return;
        }

        // F5 语法校验
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            ValidateCurrentScript();
            return;
        }

        // F6 调试运行
        if (e.Key == Key.F6)
        {
            e.Handled = true;
            RunCurrentScript();
        }
    }

    private void QuickSave()
    {
        if (_currentScript == null) return;

        ValidateCurrentScript();

        // 「已保存」**追加**在末尾，而不是覆盖：直接覆盖会把这一轮刚发现的「运行前检查」提示
        // 抹掉 —— 用户会读成"保存成功 = 没问题"。颜色沿用校验结论（ValidateCurrentScript
        // 已经设好），所以这里**不要**再改回绿色。
        TxtValidationStatus.Text = $"{TxtValidationStatus.Text}  {AppLocalization.Service[Lang.ScriptSaved]}";
        ScriptListChanged?.Invoke(this, EventArgs.Empty);
    }
}
