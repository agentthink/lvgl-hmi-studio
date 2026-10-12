using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Views.Editors;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class PropertyPanelView : UserControl
{
    // ── Faceplate 参数 ──

    /// <summary>
    /// 把存储的 BindingId 解析为友好名称“变量名 (类型)”。
    /// 若 target 不是已知的变量/系统变量 ID，则原样返回（兼容屏幕名、手写值等）。
    /// </summary>
    private string? ResolveTargetDisplayName(string? target)
    {
        if (string.IsNullOrEmpty(target) || _variableManager == null)
            return target;

        // 优先匹配普通变量（BindingId == Variable.Id）
        foreach (var item in _variableManager.GetAllBindableItems())
        {
            if (item.BindingId == target)
                return $"{item.DisplayName}  ({item.DataType})";
        }

        // 再匹配系统变量（Tag 存的是 Id，如 sys.time.cur）
        foreach (var sv in _variableManager.SystemVariables)
        {
            if (sv.Id == target)
                return $"{sv.Name}  ({sv.DataType})";
        }

        // 解析不到说明 target 是屏幕名/脚本 ID/手写值，保持原样
        return target;
    }

    /// <summary>
    /// 加载 Faceplate 参数面板 —— 当选中控件为 Faceplate 实例时显示。
    /// 根据 TemplateId 查找模板，为每个参数动态生成编辑器。
    /// </summary>
    private void LoadFaceplateParams(HmiControl control)
    {
        // 非 Faceplate 实例 → 隐藏面板
        if (string.IsNullOrEmpty(control.TemplateId) || _templateManager == null)
        {
            FaceplateParamExpander.IsVisible = false;
            return;
        }

        var template = _templateManager.FindById(control.TemplateId);
        if (template == null)
        {
            FaceplateParamExpander.IsVisible = false;
            return;
        }

        FaceplateParamExpander.IsVisible = true;
        FaceplateTemplateName.Text = AppLocalization.Service.Format(Lang.PropFaceplateTemplate, template.Name, template.Version);
        FaceplateTemplateDesc.Text = template.Description;

        // 清除旧编辑器 —— **只清动态区自己的容器**。
        // 🩸 原先写的是 `while (FaceplateParamPanel.Children.Count > 2) RemoveAt(2)`：
        //    那个字面 2 把「前两个子元素是 XAML 里的固定表头」编码成了**位置契约** ⇒
        //    往 XAML 里加第 3 个静态元素（分隔线之类），它会在**每次刷新**时被静默删掉
        //    （与「按下标找控件」同族，只是从「读错」变成「删错」）。
        //    改成持引用的容器后，静态区与动态区各管各的，静态区怎么长都不会被误删。
        FaceplateParamHost.Children.Clear();

        // 为每个参数构建编辑器
        var summary = _templateManager.GetParameterSummary(template, control);
        foreach (var (param, binding) in summary)
        {
            var editor = BuildParameterEditor(param, binding, control);
            FaceplateParamHost.Children.Add(editor);
        }

        // 无参数时显示提示
        if (template.Parameters.Count == 0)
        {
            FaceplateParamHost.Children.Add(new TextBlock
            {
                Text = AppLocalization.Service[Lang.PropFaceplateNoParams],
                Foreground = HmiEditor.App.Theme.TextMuted,
                FontSize = 11,
                Margin = new Thickness(4, 4, 0, 0)
            });
        }
    }

    /// <summary>
    /// 为单个模板参数构建编辑器控件。
    /// 根据 ParameterType 选择不同的编辑器：String→TextBox, Number→FANumberBox,
    /// Boolean→CheckBox, Color→TextBox+色块, VariableId→ComboBox。
    /// </summary>
    private Control BuildParameterEditor(
        TemplateParameter param, ParameterBinding? binding, HmiControl control)
    {
        var currentValue = binding?.Value ?? param.DefaultValue ?? "";
        var dirLabel = param.Direction switch
        {
            ParameterDirection.Input => "IN",
            ParameterDirection.Output => "OUT",
            ParameterDirection.InOut => "I/O",
            _ => "IN"
        };

        var container = new StackPanel { Spacing = 3 };

        // 参数标签行：显示名 + 方向标签
        var labelRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        labelRow.Children.Add(new TextBlock
        {
            Text = param.DisplayName,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        labelRow.Children.Add(new Border
        {
            [Grid.ColumnProperty] = 1,
            Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(42, 45, 56)),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1),
            Child = new TextBlock
            {
                Text = dirLabel,
                FontSize = 9,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.FromRgb(155, 161, 174)),
                FontFamily = new FontFamily("Consolas, monospace")
            }
        });
        container.Children.Add(labelRow);

        // 根据参数类型选择编辑器
        Control editor = param.Type switch
        {
            ParameterType.Number => BuildNumberEditor(param, currentValue, control),
            ParameterType.Boolean => BuildBooleanEditor(param, currentValue, control),
            ParameterType.VariableId => BuildVariableEditor(param, currentValue, control),
            ParameterType.Color => BuildColorEditor(param, currentValue, control),
            _ => BuildStringEditor(param, currentValue, control)
        };
        container.Children.Add(editor);

        // 参数描述（如有）
        if (!string.IsNullOrEmpty(param.Description))
        {
            container.Children.Add(new TextBlock
            {
                Text = param.Description,
                Foreground = HmiEditor.App.Theme.TextMuted,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return container;
    }

    private TextBox BuildStringEditor(TemplateParameter param, string currentValue, HmiControl control)
    {
        var tb = new TextBox
        {
            Text = currentValue,
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(6, 3)
        };
        tb.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;
            _templateManager?.UpdateParameterBinding(control, param.Name, tb.Text);
            PropertyChanged?.Invoke(this, control);
        };
        return tb;
    }

    private FluentAvalonia.UI.Controls.FANumberBox BuildNumberEditor(
        TemplateParameter param, string currentValue, HmiControl control)
    {
        var nb = new FluentAvalonia.UI.Controls.FANumberBox
        {
            Value = double.TryParse(currentValue, out var n) ? n : 0,
            Minimum = param.MinValue ?? double.MinValue,
            Maximum = param.MaxValue ?? double.MaxValue,
            SmallChange = 1,
            LargeChange = 10,
            FontSize = 12
        };
        nb.ValueChanged += (_, _) =>
        {
            if (_isUpdating) return;
            _templateManager?.UpdateParameterBinding(control, param.Name, nb.Value.ToString());
            PropertyChanged?.Invoke(this, control);
        };
        return nb;
    }

    private CheckBox BuildBooleanEditor(TemplateParameter param, string currentValue, HmiControl control)
    {
        var cb = new CheckBox
        {
            IsChecked = bool.TryParse(currentValue, out var b) && b,
            Content = AppLocalization.Service["trend.enabled"]
        };
        cb.IsCheckedChanged += (_, _) =>
        {
            if (_isUpdating) return;
            _templateManager?.UpdateParameterBinding(control, param.Name, cb.IsChecked.ToString());
            PropertyChanged?.Invoke(this, control);
        };
        return cb;
    }

    private ComboBox BuildVariableEditor(TemplateParameter param, string currentValue, HmiControl control)
    {
        var combo = new ComboBox
        {
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            IsEditable = true,
            PlaceholderText = AppLocalization.Service[Lang.PropVarPlaceholder]
        };

        // 填充变量列表
        if (_variableManager != null)
        {
            foreach (var v in _variableManager.Variables)
            {
                combo.Items.Add(new ComboBoxItem
                {
                    Content = $"{v.Name} ({v.DataType})",
                    Tag = v.Id  // M1: 使用 Id 作为绑定标识
                });
            }
        }
        combo.Text = currentValue;

        combo.SelectionChanged += (_, _) =>
        {
            if (_isUpdating) return;
            var val = combo.SelectedItem is ComboBoxItem cbi
                ? cbi.Tag?.ToString()
                : combo.Text?.Trim();
            _templateManager?.UpdateParameterBinding(control, param.Name, val, val);
            PropertyChanged?.Invoke(this, control);
        };

        return combo;
    }

    private Grid BuildColorEditor(TemplateParameter param, string currentValue, HmiControl control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,24")
        };

        var tb = new TextBox
        {
            Text = currentValue,
            Background = HmiEditor.App.Theme.BgPanel,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            FontSize = 12,
            Padding = new Thickness(6, 3),
            PlaceholderText = AppLocalization.Service["prop.color.hint"]
        };

        var swatch = new Border
        {
            [Grid.ColumnProperty] = 1,
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(3),
            Background = HmiEditor.App.Theme.AccentHover,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(4, 0, 0, 0)
        };

        // 更新色块
        void UpdateSwatch()
        {
            try
            {
                if (!string.IsNullOrEmpty(tb.Text))
                {
                    var hex = tb.Text.StartsWith('#') ? tb.Text : "#" + tb.Text;
                    swatch.Background = new SolidColorBrush(Avalonia.Media.Color.Parse(hex));
                }
            }
            catch { /* 忽略无效颜色 */ }
        }
        UpdateSwatch();

        tb.TextChanged += (_, _) =>
        {
            if (_isUpdating) return;
            _templateManager?.UpdateParameterBinding(control, param.Name, tb.Text);
            UpdateSwatch();
            PropertyChanged?.Invoke(this, control);
        };

        // 点击色块 → 颜色选择器
        swatch.PointerPressed += (_, _) =>
        {
            _activeColorField = null; // 用 null 标记 Faceplate 颜色参数
            _colorPicker.ColorSelected -= OnFaceplateColorSelected;
            _colorPicker.ColorSelected += OnFaceplateColorSelected;
            _colorPicker.Open(swatch, tb.Text ?? "");

            // 存储对 TextBox 的引用
            _faceplateColorTarget = tb;
            _faceplateColorControl = control;
            _faceplateColorParam = param;
        };

        grid.Children.Add(tb);
        grid.Children.Add(swatch);
        return grid;
    }

    // Faceplate 颜色选择的临时引用
    private TextBox? _faceplateColorTarget;
    private HmiControl? _faceplateColorControl;
    private TemplateParameter? _faceplateColorParam;

    private void OnFaceplateColorSelected(object? sender, string hex)
    {
        if (_faceplateColorTarget != null && _faceplateColorControl != null && _faceplateColorParam != null)
        {
            _faceplateColorTarget.Text = hex;
            _templateManager?.UpdateParameterBinding(
                _faceplateColorControl, _faceplateColorParam.Name, hex);
            PropertyChanged?.Invoke(this, _faceplateColorControl);
        }

        _faceplateColorTarget = null;
        _faceplateColorControl = null;
        _faceplateColorParam = null;
        _colorPicker.ColorSelected -= OnFaceplateColorSelected;
    }
}
