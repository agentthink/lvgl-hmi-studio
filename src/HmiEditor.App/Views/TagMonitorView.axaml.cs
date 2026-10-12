using Avalonia.Controls;
using Avalonia.Media;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Views;

/// <summary>
/// 标签监视器 —— 展示运行时绑定引擎构建的标签表、实时值、手动写入。
/// 对标 EasyBuilder 的 Tag 监视器 / WinCC 变量状态视图。
/// </summary>
public partial class TagMonitorView : ChromeWindow
{
    private readonly RuntimeBindingEngine _bindingEngine;
    private readonly Func<string, double?>? _valueProvider;
    private List<TagRowModel> _rows = new();

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public TagMonitorView()
    {
        _bindingEngine = null!;
        InitializeComponent();
    }

    /// <summary>
    /// 创建标签监视器。
    /// </summary>
    /// <param name="bindingEngine">运行时绑定引擎（需已 BuildRuntimeTags）。</param>
    /// <param name="valueProvider">实时值提供器（预览缓存；可为空，此时显示引擎读值）。</param>
    public TagMonitorView(RuntimeBindingEngine bindingEngine, Func<string, double?>? valueProvider = null) : this()
    {
        _bindingEngine = bindingEngine ?? throw new ArgumentNullException(nameof(bindingEngine));
        _valueProvider = valueProvider;

        BtnRefresh.Click += (_, _) => RefreshRows();
        BtnRebuild.Click += (_, _) =>
        {
            _bindingEngine.BuildRuntimeTags();
            RefreshRows();
        };
        BtnWrite.Click += (_, _) => TryWriteValue();
        CmbDirectionFilter.SelectionChanged += (_, _) => RenderRows();
    }

    /// <summary>刷新标签行（重新读取所有值）。</summary>
    public void RefreshRows()
    {
        _rows.Clear();
        foreach (var tag in _bindingEngine.Tags)
        {
            // 当前值: 优先外部提供器(预览缓存), 回退引擎读值(通讯源)
            double? value = _valueProvider?.Invoke(tag.VariableId)
                ?? _bindingEngine.ReadValue(tag.VariableId);

            var status = value.HasValue
                ? AppLocalization.Service[Lang.TagMonitorConnected]
                : (tag.Direction == BindingDirection.ReadOnly && _bindingEngine.ValueSource == null
                    ? AppLocalization.Service[Lang.TagMonitorNoSource]
                    : AppLocalization.Service[Lang.TagMonitorNoValue]);

            _rows.Add(new TagRowModel
            {
                VariableId = tag.VariableId,
                Address = tag.Address,
                ConnectionName = string.IsNullOrEmpty(tag.ConnectionName) ? AppLocalization.Service[Lang.TagMonitorNoConnection] : tag.ConnectionName,
                DirectionLabel = tag.Direction switch
                {
                    BindingDirection.ReadOnly => AppLocalization.Service["tag.monitor.read.only"],
                    BindingDirection.WriteOnly => AppLocalization.Service[Lang.TagMonitorWriteOnly],
                    _ => AppLocalization.Service["tag.monitor.read.write"]
                },
                DirectionColor = tag.Direction switch
                {
                    BindingDirection.ReadOnly => "#94a3b8",
                    BindingDirection.WriteOnly => "#f59e0b",
                    _ => "#4ade80"
                },
                ControlRef = $"{tag.ControlName}·{tag.PropertyName}",
                CurrentValueText = value?.ToString("0.###") ?? "—",
                StatusText = status,
                StatusColor = value.HasValue ? "#4ade80" : "#ef4444"
            });
        }

        RenderRows();
        SummaryText.Text = AppLocalization.Service.Format(Lang.TagMonitorSummary, _bindingEngine.TagCount);
        PopulateWriteTargets();
    }

    /// <summary>按方向筛选渲染行。</summary>
    private void RenderRows()
    {
        var filter = CmbDirectionFilter.SelectedIndex;
        var filtered = filter switch
        {
            1 => _rows.Where(r => r.DirectionLabel == AppLocalization.Service["tag.monitor.read.only"]),
            2 => _rows.Where(r => r.DirectionLabel != AppLocalization.Service["tag.monitor.read.only"]),
            _ => _rows
        };
        TagList.ItemsSource = filtered.ToList();
    }

    /// <summary>填充手动写入目标（仅可写标签）。</summary>
    private void PopulateWriteTargets()
    {
        CmbWriteTarget.Items.Clear();
        foreach (var tag in _bindingEngine.Tags)
        {
            if (tag.Direction == BindingDirection.ReadOnly) continue;
            CmbWriteTarget.Items.Add($"{tag.VariableId} ({tag.Address})");
        }
        if (CmbWriteTarget.Items.Count > 0)
            CmbWriteTarget.SelectedIndex = 0;
    }

    /// <summary>手动写入选中标签。</summary>
    private void TryWriteValue()
    {
        if (CmbWriteTarget.SelectedItem is not string selection) return;
        if (!double.TryParse(TxtWriteValue.Text, out var engineeringValue)) return;

        var variableId = selection.Split(' ')[0];
        var ok = _bindingEngine.WriteValue(variableId, engineeringValue);
        SummaryText.Text = ok
            ? AppLocalization.Service.Format("tag.monitor.write.ok", variableId, engineeringValue.ToString("0.###"))
            : AppLocalization.Service.Format("tag.monitor.write.fail", variableId);

        if (ok) RefreshRows();
    }
}

/// <summary>标签行展示模型。</summary>
public class TagRowModel
{
    public string VariableId { get; set; } = "";
    public string Address { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public string DirectionLabel { get; set; } = "";
    public string DirectionColor { get; set; } = "#94a3b8";
    public string ControlRef { get; set; } = "";
    public string CurrentValueText { get; set; } = "—";
    public string StatusText { get; set; } = "";
    public string StatusColor { get; set; } = "#ef4444";
}
