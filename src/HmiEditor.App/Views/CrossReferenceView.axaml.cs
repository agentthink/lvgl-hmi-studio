using Avalonia.Controls;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;

namespace HmiEditor.App.Views;

/// <summary>
/// I2: 交叉引用面板 —— 按变量 / 屏幕 / 脚本查找项目中所有引用位置，
/// 用于删除前影响分析与引用追踪。内核复用 CrossReferenceAnalyzer。
/// </summary>
public partial class CrossReferenceView : UserControl
{
    private HmiProject? _project;

    /// <summary>
    /// 结果是 <see cref="ReferenceRow"/> 而不是 <see cref="ReferenceLocation"/>（阶段 94，登记 #61m）：
    /// Core 的 <c>Description</c> 是**回落原文**，动作行的正常渲染在 App 侧（见 <see cref="ReferenceRow"/>）。
    /// </summary>
    private readonly ObservableCollection<ReferenceRow> _results = new();

    public CrossReferenceView()
    {
        InitializeComponent();
        ResultList.ItemsSource = _results;

        FindBtn.Click += (_, _) => Search();
        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                Search();
            }
        };
        TargetTypeCombo.SelectionChanged += (_, _) => Search();
    }

    /// <summary>加载项目（切换项目时调用）。</summary>
    public void LoadProject(HmiProject? project)
    {
        _project = project;
        _results.Clear();
    }

    /// <summary>按当前目标类型与关键词执行交叉引用搜索。</summary>
    public void Search()
    {
        _results.Clear();
        if (_project == null) return;

        var target = SearchBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(target)) return;

        var tag = TargetTypeCombo.SelectedItem is ComboBoxItem cbi
            ? cbi.Tag?.ToString()
            : "Variable";

        CrossReferenceResult? result = tag switch
        {
            "Screen" => CrossReferenceAnalyzer.FindScreenReferences(_project, target),
            "Script" => CrossReferenceAnalyzer.FindScriptReferences(_project, target),
            _ => CrossReferenceAnalyzer.FindVariableReferences(_project, target)
        };

        foreach (var r in result.References)
            _results.Add(new ReferenceRow(r));
    }
}
