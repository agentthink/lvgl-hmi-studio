using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

/// <summary>
/// Faceplate 模板编辑器对话框。
/// 支持新建模板（从选中控件）和编辑现有模板的元信息、参数接口。
/// </summary>
public partial class FaceplateTemplateEditorView : ChromeWindow
{
    /// <summary>编辑后的模板（Save 后有效）。</summary>
    public ControlTemplate? Result { get; private set; }

    private readonly ControlTemplate _template;
    private readonly bool _isNew;
    private readonly List<HmiControl> _sourceControls;
    private readonly ObservableCollection<TemplateParameter> _params;
    private int _selectedParamIndex = -1;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public FaceplateTemplateEditorView()
    {
        _template = null!;
        _sourceControls = null!;
        _params = null!;
        InitializeComponent();
    }

    /// <summary>
    /// 编辑现有模板。
    /// </summary>
    public FaceplateTemplateEditorView(ControlTemplate template)
    {
        InitializeComponent();
        _template = template;
        _isNew = false;
        _sourceControls = template.Controls.ToList();
        _params = new ObservableCollection<TemplateParameter>(
            template.Parameters.Select(p => CloneParameter(p)));

        LoadMetadata();
        RefreshParamList();
        RefreshControlsSummary();
    }

    /// <summary>
    /// 从选中控件创建新模板。
    /// </summary>
    public FaceplateTemplateEditorView(IEnumerable<HmiControl> selectedControls)
    {
        InitializeComponent();

        var controlList = selectedControls.ToList();
        if (controlList.Count == 0)
            throw new ArgumentException(AppLocalization.Service[Lang.FaceplateEditorMinControls]);

        var minX = controlList.Min(c => c.X);
        var minY = controlList.Min(c => c.Y);

        _template = new ControlTemplate
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = AppLocalization.Service[Lang.FaceplateEditorNewTemplate],
            Description = "",
            Category = AppLocalization.Service[Lang.FaceplateEditorCategoryGeneral],
            Version = 1
        };

        // 复制控件为相对坐标
        foreach (var c in controlList)
        {
            var cloned = new HmiControl
            {
                Type = c.Type,
                Name = c.Name,
                X = c.X - minX,
                Y = c.Y - minY,
                Width = c.Width,
                Height = c.Height,
                Text = c.Text,
                BgColor = c.BgColor,
                TextColor = c.TextColor,
                Radius = c.Radius,
                BorderWidth = c.BorderWidth,
                BorderColor = c.BorderColor,
                Value = c.Value,
                IsChecked = c.IsChecked,
                VariableBindingId = c.VariableBindingId
            };

            // 联动模型：克隆 VariableBindings 和 Actions 集合
            foreach (var b in c.VariableBindings)
                cloned.VariableBindings.Add(new VariableBindingData
                {
                    PropertyName = b.PropertyName,
                    VariableId = b.VariableId,
                    Direction = b.Direction
                });
            foreach (var a in c.Actions)
                cloned.Actions.Add(new HmiAction
                {
                    ActionType = a.ActionType,
                    Target = a.Target,
                    Value = a.Value,
                    TriggerEvent = a.TriggerEvent
                });

            _template.Controls.Add(cloned);
        }

        _isNew = true;
        _sourceControls = _template.Controls.ToList();
        _params = new ObservableCollection<TemplateParameter>();

        LoadMetadata();
        RefreshParamList();
        RefreshControlsSummary();
    }

    // ── 元信息 ──

    /// <summary>
    /// 把模型写进三个元信息输入框。**这里必须显式回写模型**，不能指望
    /// <c>TextChanged</c> 顺带完成：Avalonia 12 中程序化赋值 <c>TextBox.Text</c>
    /// 不会触发 <c>TextChanged</c>（只有用户输入才触发），依赖它会让"默认值归一化"
    /// （空分类 → 通用）时有时无 —— 取决于用户有没有碰过输入框。
    /// </summary>
    private void LoadMetadata()
    {
        _isLoadingMetadata = true;
        try
        {
            TxtName.Text = _template.Name;
            TxtCategory.Text = _template.Category;
            TxtDescription.Text = _template.Description;
        }
        finally
        {
            _isLoadingMetadata = false;
        }

        // 归一化（空分类 → 通用、空名字 → 新模板默认名）在装载时立即生效，与事件语义无关。
        ApplyMetadataToTemplate();

        TxtInfo.Text = _isNew
            ? AppLocalization.Service.Format(Lang.FaceplateEditorTitleNew, _template.Id, _sourceControls.Count)
            : AppLocalization.Service.Format(Lang.FaceplateEditorTitleEdit, _template.Id, _template.Version);
    }

    /// <summary>装载元信息输入框期间为 true —— 防止刷新动作被当成用户编辑。</summary>
    private bool _isLoadingMetadata;

    private void OnMetadataChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingMetadata) return;
        ApplyMetadataToTemplate();
    }

    /// <summary>把输入框内容写回模板元信息（含空值归一化）。</summary>
    private void ApplyMetadataToTemplate()
    {
        _template.Name = string.IsNullOrWhiteSpace(TxtName.Text)
            ? AppLocalization.Service[Lang.FaceplateEditorNewTemplate]
            : TxtName.Text.Trim();
        _template.Category = string.IsNullOrWhiteSpace(TxtCategory.Text)
            ? AppLocalization.Service[Lang.FaceplateEditorCategoryGeneral]
            : TxtCategory.Text.Trim();
        _template.Description = TxtDescription.Text ?? "";
    }

    // ── 参数编辑 ──

    private void RefreshParamList()
    {
        ParamListPanel.Children.Clear();

        if (_params.Count == 0)
        {
            ParamListPanel.Children.Add(new TextBlock
            {
                Text = AppLocalization.Service[Lang.FaceplateEditorNoParams],
                Foreground = HmiEditor.App.Theme.TextMuted,
                FontSize = 12,
                Margin = new Thickness(4, 8, 0, 0)
            });
            return;
        }

        for (int i = 0; i < _params.Count; i++)
        {
            var param = _params[i];
            var card = BuildParamCard(param, i);
            ParamListPanel.Children.Add(card);
        }
    }

    /// <summary>构建单个参数编辑卡片。</summary>
    private Border BuildParamCard(TemplateParameter param, int index)
    {
        var isSelected = index == _selectedParamIndex;

        var card = new Border
        {
            Background = isSelected ? HmiEditor.App.Theme.BgSelected : HmiEditor.App.Theme.BgDeep,
            CornerRadius = new CornerRadius(4),
            BorderBrush = isSelected ? HmiEditor.App.Theme.Link : HmiEditor.App.Theme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 6),
            Tag = index
        };

        // 点击选中
        card.PointerPressed += (_, _) =>
        {
            _selectedParamIndex = index;
            RefreshParamList();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("70,*,70,*"),
            RowSpacing = 3,
            ColumnSpacing = 6
        };

        // 第 0 行：参数名 + 显示名
        grid.Children.Add(MakeLabel(AppLocalization.Service[Lang.FaceplateEditorParamName], 0, 0));
        var txtParamName = new TextBox
        {
            Text = param.Name,
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(4, 2),
            [Grid.RowProperty] = 0,
            [Grid.ColumnProperty] = 1
        };
        txtParamName.TextChanged += (_, _) =>
        {
            param.Name = txtParamName.Text ?? "";
        };
        grid.Children.Add(txtParamName);

        grid.Children.Add(MakeLabel(AppLocalization.Service[Lang.FaceplateEditorParamDisplay], 0, 2));
        var txtDisplayName = new TextBox
        {
            Text = param.DisplayName,
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(4, 2),
            [Grid.RowProperty] = 0,
            [Grid.ColumnProperty] = 3
        };
        txtDisplayName.TextChanged += (_, _) =>
        {
            param.DisplayName = txtDisplayName.Text ?? "";
        };
        grid.Children.Add(txtDisplayName);

        // 第 1 行：方向 + 类型
        grid.Children.Add(MakeLabel(AppLocalization.Service[Lang.FaceplateEditorParamDirection], 1, 0));
        var cboDirection = new ComboBox
        {
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 1
        };
        cboDirection.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateEditorParamDirInput], Tag = ParameterDirection.Input });
        cboDirection.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateEditorParamDirOutput], Tag = ParameterDirection.Output });
        cboDirection.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateEditorParamDirInOut], Tag = ParameterDirection.InOut });
        cboDirection.SelectedIndex = (int)param.Direction;
        cboDirection.SelectionChanged += (_, _) =>
        {
            if (cboDirection.SelectedItem is ComboBoxItem cbi && cbi.Tag is ParameterDirection dir)
                param.Direction = dir;
        };
        grid.Children.Add(cboDirection);

        grid.Children.Add(MakeLabel(AppLocalization.Service[Lang.FaceplateEditorParamType], 1, 2));
        var cboType = new ComboBox
        {
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            [Grid.RowProperty] = 1,
            [Grid.ColumnProperty] = 3
        };
        cboType.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateTypeString], Tag = ParameterType.String });
        cboType.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateTypeNumber], Tag = ParameterType.Number });
        cboType.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateTypeBoolean], Tag = ParameterType.Boolean });
        cboType.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateTypeColor], Tag = ParameterType.Color });
        cboType.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.FaceplateTypeVariableId], Tag = ParameterType.VariableId });
        cboType.SelectedIndex = (int)param.Type;
        cboType.SelectionChanged += (_, _) =>
        {
            if (cboType.SelectedItem is ComboBoxItem cbi && cbi.Tag is ParameterType type)
            {
                param.Type = type;
                RefreshParamList(); // 类型变化可能需要重建编辑器
            }
        };
        grid.Children.Add(cboType);

        // 第 2 行：默认值
        grid.Children.Add(MakeLabel(AppLocalization.Service[Lang.FaceplateEditorParamDefault], 2, 0));
        var txtDefault = new TextBox
        {
            Text = param.DefaultValue ?? "",
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(4, 2),
            PlaceholderText = AppLocalization.Service[Lang.FaceplateEditorParamOptional],
            [Grid.RowProperty] = 2,
            [Grid.ColumnProperty] = 1
        };
        txtDefault.TextChanged += (_, _) =>
        {
            param.DefaultValue = string.IsNullOrEmpty(txtDefault.Text) ? null : txtDefault.Text;
        };
        grid.Children.Add(txtDefault);

        // 第 3 行：数值约束（仅 Number 类型显示）
        if (param.Type == ParameterType.Number)
        {
            grid.Children.Add(MakeLabel(AppLocalization.Service["recipe.param.min"], 3, 0));
            var txtMin = new TextBox
            {
                Text = param.MinValue?.ToString() ?? "",
                Background = HmiEditor.App.Theme.BgPanel,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                FontSize = 12,
                Padding = new Thickness(4, 2),
                PlaceholderText = AppLocalization.Service[Lang.FaceplateEditorParamUnlimited],
                [Grid.RowProperty] = 3,
                [Grid.ColumnProperty] = 1
            };
            txtMin.TextChanged += (_, _) =>
            {
                param.MinValue = double.TryParse(txtMin.Text, out var v) ? v : null;
            };
            grid.Children.Add(txtMin);

            grid.Children.Add(MakeLabel(AppLocalization.Service["recipe.param.max"], 3, 2));
            var txtMax = new TextBox
            {
                Text = param.MaxValue?.ToString() ?? "",
                Background = HmiEditor.App.Theme.BgPanel,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                FontSize = 12,
                Padding = new Thickness(4, 2),
                PlaceholderText = AppLocalization.Service[Lang.FaceplateEditorParamUnlimited],
                [Grid.RowProperty] = 3,
                [Grid.ColumnProperty] = 3
            };
            txtMax.TextChanged += (_, _) =>
            {
                param.MaxValue = double.TryParse(txtMax.Text, out var v) ? v : null;
            };
            grid.Children.Add(txtMax);
        }

        // 第 4 行：描述
        grid.Children.Add(MakeLabel(AppLocalization.Service["recipe.desc"], 4, 0));
        var txtDesc = new TextBox
        {
            Text = param.Description,
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(4, 2),
            PlaceholderText = AppLocalization.Service[Lang.FaceplateEditorParamDescOptional],
            [Grid.RowProperty] = 4,
            [Grid.ColumnProperty] = 1
        };
        txtDesc.TextChanged += (_, _) =>
        {
            param.Description = txtDesc.Text ?? "";
        };
        grid.Children.Add(txtDesc);

        // 删除按钮
        var btnDelete = new Button
        {
            Content = "✕",
            Background = HmiEditor.App.Theme.Danger,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Width = 24, Height = 24,
            FontSize = 11,
            Padding = new Thickness(0),
            [Grid.RowProperty] = 4,
            [Grid.ColumnProperty] = 3,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnDelete.Click += (_, _) =>
        {
            _params.RemoveAt(index);
            _selectedParamIndex = -1;
            RefreshParamList();
        };
        grid.Children.Add(btnDelete);

        card.Child = grid;
        return card;
    }

    private static TextBlock MakeLabel(string text, int row, int col)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            [Grid.RowProperty] = row,
            [Grid.ColumnProperty] = col
        };
    }

    private void OnAddParamClicked(object? sender, RoutedEventArgs e)
    {
        var param = new TemplateParameter
        {
            Name = $"Param{_params.Count + 1}",
            DisplayName = AppLocalization.Service.Format(Lang.FaceplateEditorParamDefaultName, _params.Count + 1),
            Direction = ParameterDirection.Input,
            Type = ParameterType.String
        };
        _params.Add(param);
        _selectedParamIndex = _params.Count - 1;
        RefreshParamList();
    }

    private void OnRemoveParamClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedParamIndex >= 0 && _selectedParamIndex < _params.Count)
        {
            _params.RemoveAt(_selectedParamIndex);
            _selectedParamIndex = -1;
            RefreshParamList();
        }
    }

    // ── 控件摘要 ──

    private void RefreshControlsSummary()
    {
        var count = _sourceControls.Count;
        // 类型走单一来源（`ControlTypeTextKeys`）：改动前直显英文枚举名（`Button` / `TextArea`），
        // 中文界面下也是英文。见登记 #61o 的剩余面（阶段 160 收口）。
        var types = _sourceControls.Select(c => Services.ControlTypeTextKeys.Describe(c.Type))
            .Distinct().ToList();
        TxtControlsSummary.Text = count > 0
            ? AppLocalization.Service.Format(Lang.FaceplateEditorSummary, count, string.Join(", ", types))
            : AppLocalization.Service.Format("faceplate.contains.controls.count", 0);
    }

    // ── 保存/取消 ──

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        // 验证参数名唯一性
        var paramNames = _params.Select(p => p.Name).ToList();
        var duplicates = paramNames.GroupBy(n => n).Where(g => g.Count() > 1).ToList();
        if (duplicates.Count > 0)
        {
            var dupNames = string.Join(", ", duplicates.Select(g => g.Key));
            TxtInfo.Text = AppLocalization.Service.Format(Lang.FaceplateEditorParamDup, dupNames);
            TxtInfo.Foreground = HmiEditor.App.Theme.Danger;
            return;
        }

        // 同步参数到模板
        _template.Parameters.Clear();
        foreach (var p in _params)
            _template.Parameters.Add(p);

        // 版本递增（编辑时）
        if (!_isNew)
            _template.Version++;

        Result = _template;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    // ── 辅助 ──

    private static TemplateParameter CloneParameter(TemplateParameter src)
    {
        return new TemplateParameter
        {
            Name = src.Name,
            DisplayName = src.DisplayName,
            Direction = src.Direction,
            Type = src.Type,
            DefaultValue = src.DefaultValue,
            Description = src.Description,
            MinValue = src.MinValue,
            MaxValue = src.MaxValue
        };
    }
}
