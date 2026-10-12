using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Controls;

namespace HmiEditor.App.Views;

/// <summary>
/// 配方编辑器对话框 —— 管理配方组、配方及配方参数。
/// 三栏设计：左侧分组列表 / 中间配方列表 / 右侧编辑面板。
/// </summary>
public partial class RecipeEditorView : ChromeWindow
{
    private readonly RecipeManager _recipeManager;
    private readonly VariableManager? _variableManager;
    private RecipeGroup? _selectedGroup;
    private Recipe? _selectedRecipe;
    private RecipeItem? _selectedParam;
    private bool _isUpdating;
    private int _paramCounter;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public RecipeEditorView()
    {
        _recipeManager = null!;
        InitializeComponent();
    }

    public RecipeEditorView(RecipeManager recipeManager, VariableManager? variableManager = null) : this()
    {
        _recipeManager = recipeManager;
        _variableManager = variableManager;

        Initialize();
    }

    // ═══════════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════════

    private void Initialize()
    {
        // 初始化 ComboBox 数据源
        CmbParamDataType.ItemsSource = new[] { "Float", "Int16", "Int32", "String" };
        CmbParamDataType.SelectedIndex = 0;

        // 绑定事件
        BtnAddGroup.Click += (_, _) => OnAddGroup();
        BtnDeleteGroup.Click += (_, _) => OnDeleteGroup();
        GroupList.SelectionChanged += OnGroupSelectionChanged;

        BtnNewRecipe.Click += (_, _) => OnNewRecipe();
        BtnCloneRecipe.Click += (_, _) => OnCloneRecipe();
        BtnDeleteRecipe.Click += (_, _) => OnDeleteRecipe();
        RecipeList.SelectionChanged += OnRecipeSelectionChanged;

        BtnAddParam.Click += (_, _) => OnAddParam();
        BtnDeleteParam.Click += (_, _) => OnDeleteParam();
        BtnMoveUp.Click += (_, _) => OnMoveParam(-1);
        BtnMoveDown.Click += (_, _) => OnMoveParam(1);
        ParamList.SelectionChanged += OnParamSelectionChanged;

        BtnImportCsv.Click += async (_, _) => await OnImportCsv();
        BtnExportCsv.Click += async (_, _) => await OnExportCsv();
        BtnSaveRecipe.Click += (_, _) => OnSaveRecipe();

        // 参数编辑字段变更自动保存到当前选中参数
        TxtParamLabel.TextChanged += (_, _) => SyncParamEdit();
        TxtParamDefault.TextChanged += (_, _) => SyncParamEdit();
        TxtParamMin.TextChanged += (_, _) => SyncParamEdit();
        TxtParamMax.TextChanged += (_, _) => SyncParamEdit();
        TxtParamUnit.TextChanged += (_, _) => SyncParamEdit();
        TxtParamSortOrder.TextChanged += (_, _) => SyncParamEdit();
        CmbParamVariable.SelectionChanged += (_, _) => SyncParamEdit();
        CmbParamDataType.SelectionChanged += (_, _) => SyncParamEdit();

        RefreshGroups();
    }

    // ═══════════════════════════════════════════════
    //  分组管理
    // ═══════════════════════════════════════════════

    private void RefreshGroups()
    {
        GroupList.Items.Clear();
        var groups = _recipeManager.GetAllGroups();

        foreach (var group in groups)
        {
            // 填充分组内的配方列表
            var recipes = _recipeManager.GetRecipesByGroup(group.Name);
            group.Recipes.Clear();
            foreach (var r in recipes)
                group.Recipes.Add(r);

            GroupList.Items.Add(CreateGroupItem(group));
        }

        EmptyGroupHint.IsVisible = groups.Count == 0;
        BtnDeleteGroup.IsEnabled = groups.Count > 0;
    }

    private ListBoxItem CreateGroupItem(RecipeGroup group)
    {
        var nameText = new TextBlock
        {
            Text = group.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var countText = new TextBlock
        {
            Text = AppLocalization.Service.Format(Lang.RecipeCount, group.Recipes.Count),
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { nameText, countText }
        };

        return new ListBoxItem
        {
            Content = infoPanel,
            Tag = group,
            Padding = new Thickness(10, 8)
        };
    }

    private void OnGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is ListBoxItem item && item.Tag is RecipeGroup group)
        {
            _selectedGroup = group;
            RefreshRecipes();
        }
    }

    private void OnAddGroup()
    {
        var dialog = new TextInputDialog
        {
            Title = AppLocalization.Service[Lang.RecipeAddGroup],
            Prompt = AppLocalization.Service[Lang.RecipePromptGroupName],
            Watermark = AppLocalization.Service[Lang.RecipeWatermarkGroupName]
        };

        dialog.Closed += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(dialog.ResultText))
            {
                _recipeManager.AddGroup(dialog.ResultText.Trim());
                RefreshGroups();
            }
        };
        dialog.ShowDialog(this);
    }

    private void OnDeleteGroup()
    {
        if (_selectedGroup == null) return;

        var confirm = new ConfirmDialog
        {
            Title = AppLocalization.Service["gen.confirm.delete"],
            Message = AppLocalization.Service.Format(Lang.RecipeConfirmDeleteGroup, _selectedGroup.Name),
        };

        confirm.Closed += (_, _) =>
        {
            if (confirm.Confirmed)
            {
                // 删除分组下的所有配方
                var recipes = _recipeManager.GetRecipesByGroup(_selectedGroup.Name);
                foreach (var r in recipes.ToList())
                    _recipeManager.DeleteRecipe(r.Id);

                RefreshGroups();
                _selectedGroup = null;
                RecipeList.Items.Clear();
                ClearEditPanel();
                EmptyRecipeHint.IsVisible = true;
            }
        };
        confirm.ShowDialog(this);
    }

    // ═══════════════════════════════════════════════
    //  配方管理
    // ═══════════════════════════════════════════════

    private void RefreshRecipes()
    {
        RecipeList.Items.Clear();
        if (_selectedGroup == null)
        {
            EmptyRecipeHint.IsVisible = true;
            return;
        }

        var recipes = _recipeManager.GetRecipesByGroup(_selectedGroup.Name);
        foreach (var recipe in recipes)
        {
            RecipeList.Items.Add(CreateRecipeItem(recipe));
        }

        EmptyRecipeHint.IsVisible = recipes.Count == 0;
        ClearEditPanel();
    }

    private ListBoxItem CreateRecipeItem(Recipe recipe)
    {
        // 启用状态指示圆点
        var statusDot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = recipe.IsEnabled
                ? new SolidColorBrush(UiPalette.ConnectedBright)
                : new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var nameText = new TextBlock
        {
            Text = recipe.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var versionText = new TextBlock
        {
            Text = $"v{recipe.Version}",
            Foreground = HmiEditor.App.Theme.Link,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };

        var topRowChildren = new List<Control> { statusDot, nameText, versionText };

        if (recipe.IsBuiltIn)
        {
            topRowChildren.Add(new Border
            {
                Background = HmiEditor.App.Theme.BgSelected,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = AppLocalization.Service[Lang.RecipeBuiltin],
                    Foreground = HmiEditor.App.Theme.TextSecondary,
                    FontSize = 10
                }
            });
        }

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        foreach (var child in topRowChildren)
            topRow.Children.Add(child);

        var descText = new TextBlock
        {
            Text = string.IsNullOrEmpty(recipe.Description) ? AppLocalization.Service[Lang.RecipeNoDesc] : recipe.Description,
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Margin = new Thickness(16, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { topRow, descText }
        };

        return new ListBoxItem
        {
            Content = infoPanel,
            Tag = recipe,
            Padding = new Thickness(10, 8)
        };
    }

    private void OnRecipeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RecipeList.SelectedItem is ListBoxItem item && item.Tag is Recipe recipe)
        {
            _selectedRecipe = recipe;
            LoadRecipeToEditPanel(recipe);
        }
    }

    private void OnNewRecipe()
    {
        if (_selectedGroup == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSelectGroupFirst]);
            return;
        }

        var dialog = new TextInputDialog
        {
            Title = AppLocalization.Service[Lang.RecipeNew],
            Prompt = AppLocalization.Service[Lang.RecipePromptName],
            Watermark = AppLocalization.Service[Lang.RecipeWatermarkName]
        };

        dialog.Closed += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(dialog.ResultText))
            {
                var recipe = new Recipe
                {
                    Name = dialog.ResultText.Trim(),
                    GroupName = _selectedGroup.Name,
                    Items = new List<RecipeItem>()
                };
                _recipeManager.AddRecipe(recipe);
                RefreshGroups();
                RefreshRecipes();

                // 选中新创建的配方
                foreach (var listItem in RecipeList.Items.OfType<ListBoxItem>())
                {
                    if (listItem.Tag is Recipe r && r.Id == recipe.Id)
                    {
                        RecipeList.SelectedItem = listItem;
                        break;
                    }
                }
            }
        };
        dialog.ShowDialog(this);
    }

    private void OnCloneRecipe()
    {
        if (_selectedRecipe == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSelectRecipeFirst]);
            return;
        }

        var dialog = new TextInputDialog
        {
            Title = AppLocalization.Service[Lang.RecipeClone],
            Prompt = AppLocalization.Service[Lang.RecipePromptCloneName],
            Watermark = AppLocalization.Service.Format(Lang.RecipeCopySuffix, _selectedRecipe.Name),
            DefaultText = AppLocalization.Service.Format(Lang.RecipeCopySuffix, _selectedRecipe.Name)
        };

        dialog.Closed += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(dialog.ResultText))
            {
                try
                {
                    var result = _recipeManager.CloneRecipe(_selectedRecipe.Id, dialog.ResultText.Trim());
                    if (result.Recipe is { } clone)
                    {
                        RefreshGroups();
                        RefreshRecipes();
                        ShowStatus(AppLocalization.Service.Format(Lang.RecipeCloned, clone.Name));
                    }
                    else if (result.Issue is { } issue)
                    {
                        // 失败档（登记 #61t，阶段 98）：**改动前这里什么都没有** ——
                        // CloneRecipe 的返回类型声明成非空、消费点连 else 都没写 ⇒
                        // 「源配方已过期」与「入库被拒」两种情况都是**点了按钮没反应**。
                        // 现在走码化出口（码 → 键 → 模板填实参），与 SvgParse / CsvImport 族同形。
                        ShowStatus(ExternalText.Describe(issue));
                    }
                }
                catch (Exception ex)
                {
                    // 兜底 —— 它包住的是 RefreshGroups / RefreshRecipes 这两行 **UI 刷新**
                    // （CloneRecipe 本身不抛；失败已由上面的码化分支接走）。
                    // ⚠ 刻意**不**把 ex.Message 换成 FailureReason(ex)：这条 catch 接的是
                    // UI 层的意外异常，产出方给不出码 ⇒ 换出口是「**改名式假闭环**」
                    // （用户所见一字不变）。基线因此**保留这一行**，理由见
                    // ExceptionMessagePassthroughFaceTests 的阶段 98 段。
                    ShowStatus(AppLocalization.Service.Format(Lang.RecipeCloneFailed, ex.Message));
                }
            }
        };
        dialog.ShowDialog(this);
    }

    private void OnDeleteRecipe()
    {
        if (_selectedRecipe == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSelectRecipeFirst]);
            return;
        }

        if (_selectedRecipe.IsBuiltIn)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeBuiltinNoDelete]);
            return;
        }

        var confirm = new ConfirmDialog
        {
            Title = AppLocalization.Service["gen.confirm.delete"],
            Message = AppLocalization.Service.Format(Lang.RecipeConfirmDelete, _selectedRecipe.Name),
        };

        confirm.Closed += (_, _) =>
        {
            if (confirm.Confirmed)
            {
                _recipeManager.DeleteRecipe(_selectedRecipe.Id);
                _selectedRecipe = null;
                RefreshGroups();
                RefreshRecipes();
                ClearEditPanel();
                ShowStatus(AppLocalization.Service[Lang.RecipeDeleted]);
            }
        };
        confirm.ShowDialog(this);
    }

    // ═══════════════════════════════════════════════
    //  配方编辑面板
    // ═══════════════════════════════════════════════

    private void LoadRecipeToEditPanel(Recipe recipe)
    {
        _isUpdating = true;

        TxtRecipeName.Text = recipe.Name;
        TxtDescription.Text = recipe.Description ?? "";
        ChkEnabled.IsChecked = recipe.IsEnabled;
        TxtVersion.Text = $"v{recipe.Version}";
        TxtCreatedAt.Text = recipe.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        TxtModifiedAt.Text = recipe.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        // 分组 ComboBox
        var groupNames = _recipeManager.GetAllGroups().Select(g => g.Name).ToList();
        CmbRecipeGroup.ItemsSource = groupNames;
        var groupIdx = groupNames.IndexOf(recipe.GroupName);
        CmbRecipeGroup.SelectedIndex = groupIdx >= 0 ? groupIdx : 0;

        // 刷新参数列表
        RefreshParamList(recipe);

        _isUpdating = false;
    }

    private void ClearEditPanel()
    {
        _isUpdating = true;
        TxtRecipeName.Text = "";
        TxtDescription.Text = "";
        ChkEnabled.IsChecked = false;
        TxtVersion.Text = "";
        TxtCreatedAt.Text = "";
        TxtModifiedAt.Text = "";
        CmbRecipeGroup.ItemsSource = null;
        ParamList.Items.Clear();
        ParamEditPanel.IsVisible = false;
        _selectedParam = null;
        _isUpdating = false;
    }

    private void OnSaveRecipe()
    {
        if (_selectedRecipe == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeNoSelected]);
            return;
        }

        // 收集编辑面板数据
        var name = TxtRecipeName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeNameEmpty]);
            return;
        }

        // 验证参数
        var errors = new List<string>();
        foreach (var item in _selectedRecipe.Items)
        {
            if (item.DefaultValue < item.MinValue)
                errors.Add(AppLocalization.Service.Format(Lang.RecipeValidationDefaultBelow, item.Label, item.DefaultValue, item.MinValue));
            if (item.DefaultValue > item.MaxValue)
                errors.Add(AppLocalization.Service.Format(Lang.RecipeValidationDefaultAbove, item.Label, item.DefaultValue, item.MaxValue));
        }

        if (errors.Count > 0)
        {
            var errorMsg = AppLocalization.Service[Lang.RecipeValidationFailed] + string.Join("\n", errors.Take(5));
            if (errors.Count > 5)
                errorMsg += AppLocalization.Service.Format(Lang.RecipeValidationMore, errors.Count - 5);
            ShowStatus(errorMsg);
            return;
        }

        _selectedRecipe.Name = name;
        _selectedRecipe.Description = TxtDescription.Text?.Trim();
        _selectedRecipe.IsEnabled = ChkEnabled.IsChecked ?? true;

        if (CmbRecipeGroup.SelectedItem is string groupName)
        {
            _selectedRecipe.GroupName = groupName;
        }

        var success = _recipeManager.UpdateRecipe(_selectedRecipe);
        if (success)
        {
            RefreshGroups();
            RefreshRecipes();
            // 重新选中当前配方
            foreach (var item in RecipeList.Items.OfType<ListBoxItem>())
            {
                if (item.Tag is Recipe r && r.Id == _selectedRecipe.Id)
                {
                    RecipeList.SelectedItem = item;
                    LoadRecipeToEditPanel(_selectedRecipe);
                    break;
                }
            }
            ShowStatus(AppLocalization.Service.Format(Lang.RecipeSavedFormat, name, _selectedRecipe.Version));
        }
        else
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSaveFailed]);
        }
    }

    // ═══════════════════════════════════════════════
    //  参数管理
    // ═══════════════════════════════════════════════

    private void RefreshParamList(Recipe recipe)
    {
        ParamList.Items.Clear();
        foreach (var item in recipe.Items.OrderBy(i => i.SortOrder))
        {
            ParamList.Items.Add(CreateParamItem(item));
        }
    }

    /// <summary>
    /// 配方参数行。编辑后要就地刷新的那个标签在**构造时**捕获，
    /// 不再按 <c>grid.Children[1]</c> 这类**下标**反向定位（结构一变即静默失效）。
    /// <see cref="Control.Tag"/> 仍是 <see cref="RecipeItem"/>。
    /// </summary>
    private sealed class RecipeParamRow : ListBoxItem
    {
        public RecipeParamRow(RecipeItem item, TextBlock labelText)
        {
            Param = item;
            LabelText = labelText;
            Tag = item;
        }

        public RecipeItem Param { get; }

        public TextBlock LabelText { get; }
    }

    private ListBoxItem CreateParamItem(RecipeItem item)
    {
        var sortOrderText = new TextBlock
        {
            Text = item.SortOrder.ToString(),
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 24
        };

        var labelText = new TextBlock
        {
            Text = item.Label,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var varIdText = new TextBlock
        {
            Text = item.VariableId,
            Foreground = HmiEditor.App.Theme.Link,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };

        var defaultText = new TextBlock
        {
            Text = item.DefaultValue.ToString("F2"),
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 50
        };

        var unitText = new TextBlock
        {
            Text = item.Unit ?? "",
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 36
        };

        var minText = new TextBlock
        {
            Text = item.MinValue > double.MinValue / 2 ? item.MinValue.ToString("F1") : "—",
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 36
        };

        var maxText = new TextBlock
        {
            Text = item.MaxValue < double.MaxValue / 2 ? item.MaxValue.ToString("F1") : "—",
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 36
        };

        var grid = new Grid
        {
            // 与表头列宽保持完全一致，避免序号/默认值/单位/最小/最大列错位。
            // 表头定义见 RecipeEditorView.axaml 的 28,*,*,56,40,40,40。
            ColumnDefinitions = new ColumnDefinitions("28,*,*,56,40,40,40"),
            ColumnSpacing = 2
        };
        grid.Children.Add(sortOrderText);
        grid.Children.Add(labelText);
        Grid.SetColumn(labelText, 1);
        grid.Children.Add(varIdText);
        Grid.SetColumn(varIdText, 2);
        grid.Children.Add(defaultText);
        Grid.SetColumn(defaultText, 3);
        grid.Children.Add(unitText);
        Grid.SetColumn(unitText, 4);
        grid.Children.Add(minText);
        Grid.SetColumn(minText, 5);
        grid.Children.Add(maxText);
        Grid.SetColumn(maxText, 6);

        return new RecipeParamRow(item, labelText)
        {
            Content = grid,
            Padding = new Thickness(4, 3)
        };
    }

    private void OnAddParam()
    {
        if (_selectedRecipe == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSelectRecipeFirst]);
            return;
        }

        _paramCounter++;
        var maxOrder = _selectedRecipe.Items.Count > 0
            ? _selectedRecipe.Items.Max(i => i.SortOrder)
            : 0;

        var item = new RecipeItem
        {
            VariableId = "",
            Label = AppLocalization.Service.Format(Lang.RecipeParamDefaultName, _paramCounter),
            DefaultValue = 0,
            MinValue = double.MinValue,
            MaxValue = double.MaxValue,
            DataType = "Float",
            SortOrder = maxOrder + 1
        };

        _selectedRecipe.Items.Add(item);
        ParamList.Items.Add(CreateParamItem(item));

        // 选中新添加的参数
        if (ParamList.Items.Count > 0)
        {
            ParamList.SelectedIndex = ParamList.Items.Count - 1;
        }

        ShowStatus(AppLocalization.Service.Format(Lang.RecipeParamAdded, item.Label));
    }

    private void OnDeleteParam()
    {
        if (_selectedRecipe == null || _selectedParam == null)
        {
            ShowStatus(AppLocalization.Service[Lang.RecipeSelectParamFirst]);
            return;
        }

        _selectedRecipe.Items.Remove(_selectedParam);
        RefreshParamList(_selectedRecipe);
        ParamEditPanel.IsVisible = false;
        _selectedParam = null;
        ShowStatus(AppLocalization.Service[Lang.RecipeParamDeleted]);
    }

    private void OnMoveParam(int direction)
    {
        if (_selectedRecipe == null || _selectedParam == null) return;

        var sorted = _selectedRecipe.Items.OrderBy(i => i.SortOrder).ToList();
        var idx = sorted.IndexOf(_selectedParam);
        var newIdx = idx + direction;

        if (newIdx < 0 || newIdx >= sorted.Count) return;

        // 交换 SortOrder
        (sorted[idx].SortOrder, sorted[newIdx].SortOrder) = (sorted[newIdx].SortOrder, sorted[idx].SortOrder);
        _selectedParam = sorted[newIdx];

        RefreshParamList(_selectedRecipe);

        // 选中移动后的参数
        foreach (var item in ParamList.Items.OfType<ListBoxItem>())
        {
            if (item.Tag == _selectedParam)
            {
                ParamList.SelectedItem = item;
                break;
            }
        }
    }

    private void OnParamSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ParamList.SelectedItem is ListBoxItem item && item.Tag is RecipeItem param)
        {
            _selectedParam = param;
            LoadParamToEditPanel(param);
        }
        else
        {
            ParamEditPanel.IsVisible = false;
            _selectedParam = null;
        }
    }

    // ═══════════════════════════════════════════════
    //  参数编辑面板
    // ═══════════════════════════════════════════════

    private void LoadParamToEditPanel(RecipeItem item)
    {
        _isUpdating = true;

        ParamEditPanel.IsVisible = true;

        TxtParamLabel.Text = item.Label;
        TxtParamDefault.Text = item.DefaultValue.ToString(CultureInfo.InvariantCulture);
        TxtParamMin.Text = item.MinValue > double.MinValue / 2
            ? item.MinValue.ToString(CultureInfo.InvariantCulture)
            : "";
        TxtParamMax.Text = item.MaxValue < double.MaxValue / 2
            ? item.MaxValue.ToString(CultureInfo.InvariantCulture)
            : "";
        TxtParamUnit.Text = item.Unit ?? "";
        TxtParamSortOrder.Text = item.SortOrder.ToString();

        // 变量选择 ComboBox
        if (_variableManager != null)
        {
            var varItems = _variableManager.Variables
                .Select(v => new { v.Id, Display = $"{v.Name} ({v.Id})" })
                .ToList();
            CmbParamVariable.ItemsSource = varItems;
            var varIdx = varItems.FindIndex(v => v.Id == item.VariableId);
            CmbParamVariable.SelectedIndex = varIdx >= 0 ? varIdx : -1;
        }

        // 数据类型 ComboBox
        var dataTypes = new[] { "Float", "Int16", "Int32", "String" };
        var dtIdx = Array.IndexOf(dataTypes, item.DataType);
        CmbParamDataType.SelectedIndex = dtIdx >= 0 ? dtIdx : 0;

        _isUpdating = false;
    }

    private void SyncParamEdit()
    {
        if (_isUpdating || _selectedParam == null) return;

        _selectedParam.Label = TxtParamLabel.Text ?? "";

        if (double.TryParse(TxtParamDefault.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
            _selectedParam.DefaultValue = dv;

        if (double.TryParse(TxtParamMin.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minV))
            _selectedParam.MinValue = minV;
        else
            _selectedParam.MinValue = double.MinValue;

        if (double.TryParse(TxtParamMax.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxV))
            _selectedParam.MaxValue = maxV;
        else
            _selectedParam.MaxValue = double.MaxValue;

        _selectedParam.Unit = TxtParamUnit.Text;

        if (int.TryParse(TxtParamSortOrder.Text, out var sort))
            _selectedParam.SortOrder = sort;

        if (CmbParamDataType.SelectedItem is string dataType)
            _selectedParam.DataType = dataType;

        if (CmbParamVariable.SelectedItem is { } selectedVar)
        {
            // 提取变量 ID
            var idProp = selectedVar.GetType().GetProperty("Id");
            if (idProp != null)
            {
                _selectedParam.VariableId = idProp.GetValue(selectedVar) as string ?? "";
            }
        }

        // 刷新参数列表显示
        foreach (var row in ParamList.Items.OfType<RecipeParamRow>())
        {
            if (row.Param != _selectedParam) continue;

            row.LabelText.Text = _selectedParam.Label;
            break;
        }
    }

    // ═══════════════════════════════════════════════
    //  CSV 导入/导出
    // ═══════════════════════════════════════════════

    private async System.Threading.Tasks.Task OnImportCsv()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.RecipeCsvImportTitle],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.RecipeCsvFileFilter]) { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count == 0) return;

        try
        {
            using var stream = await files[0].OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var result = _recipeManager.ImportCsv(content);

            // ⚠ 明细先摆出来：状态行只放条数，**哪一行、为什么**在这里。
            //   改动前这一路的返回值只是个 `int` ⇒ 坏行 / 非数字的值 / 入库被拒全被静默吞掉，
            //   用户只看到「成功导入 N 个配方」（登记 #61f，阶段 100）。
            ShowCsvIssues(result.Issues);

            if (result.Imported > 0 || result.HasIssues)
            {
                RefreshGroups();
                RefreshRecipes();

                var status = AppLocalization.Service.Format(Lang.RecipeCsvImportOk, result.Imported);
                if (result.HasIssues)
                {
                    status += AppLocalization.Service.Format(
                        Lang.RecipeCsvImportWarnings, result.Issues.Count);
                }

                ShowStatus(status);
            }
            else
            {
                ShowStatus(AppLocalization.Service[Lang.RecipeCsvImportInvalid]);
            }
        }
        catch (Exception ex)
        {
            // 上一次的明细要清掉：否则旧问题会挂在新错误下面，看着像这次导入的。
            ShowCsvIssues(Array.Empty<RecipeImportIssue>());
            ShowStatus(UiOperationGuard.FailureDetail(Lang.UiOpImportRecipe, files[0].Path.LocalPath, ex));
        }
    }

    private async System.Threading.Tasks.Task OnExportCsv()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.RecipeCsvExportTitle],
            DefaultExtension = ".csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.RecipeCsvFileFilter]) { Patterns = new[] { "*.csv" } }
            }
        });

        if (file == null) return;

        try
        {
            var content = _recipeManager.ExportCsv();
            if (string.IsNullOrEmpty(content))
            {
                ShowStatus(AppLocalization.Service[Lang.RecipeCsvExportEmpty]);
                return;
            }

            using var stream = await file.OpenWriteAsync();
            using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(content);

            ShowStatus(AppLocalization.Service[Lang.RecipeCsvExportOk]);
        }
        catch (Exception ex)
        {
            ShowStatus(UiOperationGuard.FailureDetail(Lang.UiOpExportRecipe, file.Path.LocalPath, ex));
        }
    }

    // ═══════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════

    private void ShowStatus(string message)
    {
        // 更新标题栏提示（简短反馈）
        Title = $"{AppLocalization.Service["win.recipe.editor"]} — {message}";
    }

    /// <summary>状态条上最多摆几条 —— 超出的在 ToolTip 里（见下）。</summary>
    private const int MaxInlineCsvIssues = 5;

    /// <summary>
    /// 把一次 CSV 导入的问题**摆到界面上**（登记 <b>#61f</b>，阶段 100）。
    ///
    /// <para><b>为什么这个方法非有不可</b>：本项缺陷不是"少翻一个词"，是**失败原因到不了界面**
    /// —— 改动前 <c>ImportCsv</c> 只返回一个 <c>int</c>，站点用 <c>if (count &gt; 0)</c> 把它
    /// 盖成「成功导入 N 个配方」；CSV 里某一格写成 <c>abc</c>，那个配方项就**静默少一个**。
    /// 只加渲染出口而不接线，用户所见**一字不变**。</para>
    ///
    /// <para><b>为什么不换行截断也不静默截断</b>：可见文本取前 <see cref="MaxInlineCsvIssues"/>
    /// 条，超出时补一个显式省略号（用户知道还有），而 <b>ToolTip 里是全部</b> ——
    /// 条数另有状态行（<c>recipe.csv.import.warnings</c>）兜着。三个渠道加起来没有一处"悄悄丢"。</para>
    /// </summary>
    private void ShowCsvIssues(IReadOnlyList<RecipeImportIssue> issues)
    {
        if (issues.Count == 0)
        {
            TxtCsvIssues.Text = string.Empty;
            ToolTip.SetTip(TxtCsvIssues, null);
            CsvIssueBar.IsVisible = false;
            return;
        }

        // 唯一的渲染出口（码 → 字典模板 → 填实参 → 查不到才回落中文原文）。
        var lines = issues.Select(ExternalText.Describe).ToList();

        TxtCsvIssues.Text = string.Join(" · ", lines.Take(MaxInlineCsvIssues))
            + (lines.Count > MaxInlineCsvIssues ? " · …" : string.Empty);
        ToolTip.SetTip(TxtCsvIssues, string.Join("\n", lines));
        CsvIssueBar.IsVisible = true;
    }

    /// <summary>
    /// 文本输入对话框 —— 用于获取分组名/配方名/克隆名。
    /// </summary>
    private class TextInputDialog : Window
    {
        private readonly TextBox _inputBox;
        public string? ResultText { get; private set; }

        public string Prompt
        {
            set => _promptText.Text = value;
        }

        public string Watermark
        {
            set => _inputBox.PlaceholderText = value;
        }

        public string DefaultText
        {
            set => _inputBox.Text = value;
        }

        private readonly TextBlock _promptText;

        public TextInputDialog()
        {
            Width = 380;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = HmiEditor.App.Theme.BgWindow;
            WindowDecorations = WindowDecorations.BorderOnly;

            _promptText = new TextBlock
            {
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 13,
                Margin = new Thickness(16, 16, 16, 8)
            };

            _inputBox = new TextBox
            {
                Background = HmiEditor.App.Theme.BgDeep,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6),
                FontSize = 13,
                Margin = new Thickness(16, 0, 16, 12)
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var cancelBtn = new Button
            {
                Content = AppLocalization.Service["gen.cancel"],
                Background = HmiEditor.App.Theme.BgElevated,
                Foreground = HmiEditor.App.Theme.TextSecondary,
                BorderThickness = new Thickness(1),
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            var okBtn = new Button
            {
                Content = AppLocalization.Service[Lang.GenOk],
                Background = HmiEditor.App.Theme.AccentHover,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            cancelBtn.Click += (_, _) => Close();
            okBtn.Click += (_, _) =>
            {
                ResultText = _inputBox.Text;
                Close();
            };

            _inputBox.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    ResultText = _inputBox.Text;
                    Close();
                }
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            var root = new Border
            {
                Background = HmiEditor.App.Theme.BgWindow,
                Padding = new Thickness(0),
                Child = new DockPanel
                {
                    Children =
                    {
                        new Border
                        {
                            Background = HmiEditor.App.Theme.BgPanel,
                            Height = 32,
                            Child = new TextBlock
                            {
                                Text = AppLocalization.Service[Lang.RecipeDialogInputTitle],
                                Foreground = HmiEditor.App.Theme.TextPrimary,
                                FontSize = 12,
                                FontWeight = FontWeight.SemiBold,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(12, 0, 0, 0)
                            }
                        },
                        new StackPanel
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Children = { _promptText, _inputBox, btnPanel }
                        }
                    }
                }
            };

            Content = root;
        }
    }

    /// <summary>
    /// 确认对话框 —— 用于删除确认。
    /// </summary>
    private class ConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }
        public string Message { get; set; } = "";
        public new string Title { get; set; } = AppLocalization.Service[Lang.GenConfirm];

        public ConfirmDialog()
        {
            Width = 380;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = HmiEditor.App.Theme.BgWindow;
            WindowDecorations = WindowDecorations.BorderOnly;

            var messageText = new TextBlock
            {
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 13,
                Margin = new Thickness(16, 16, 16, 12),
                TextWrapping = TextWrapping.Wrap
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var cancelBtn = new Button
            {
                Content = AppLocalization.Service["gen.cancel"],
                Background = HmiEditor.App.Theme.BgElevated,
                Foreground = HmiEditor.App.Theme.TextSecondary,
                BorderThickness = new Thickness(1),
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            var okBtn = new Button
            {
                Content = AppLocalization.Service["gen.confirm.delete"],
                Background = HmiEditor.App.Theme.Danger,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            cancelBtn.Click += (_, _) => Close();
            okBtn.Click += (_, _) =>
            {
                Confirmed = true;
                Close();
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            // 更新 Title 绑定
            var titleBar = new Border
            {
                Background = HmiEditor.App.Theme.BgPanel,
                Height = 32,
                Child = new TextBlock
                {
                    Text = Title,
                    Foreground = HmiEditor.App.Theme.TextPrimary,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                }
            };

            var contentPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { messageText, btnPanel }
            };

            var root = new Border
            {
                Background = HmiEditor.App.Theme.BgWindow,
                Padding = new Thickness(0),
                Child = new DockPanel
                {
                    Children = { titleBar, contentPanel }
                }
            };

            Content = root;

            // 使用 Loaded 事件设置消息
            Loaded += (_, _) =>
            {
                messageText.Text = Message;
                if (titleBar.Child is TextBlock tb2)
                    tb2.Text = Title;
            };
        }
    }
}
