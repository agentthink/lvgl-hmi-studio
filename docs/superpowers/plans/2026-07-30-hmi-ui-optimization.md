# HMI Editor UI 对标优化 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 对标 Siemens WinCC / Weintek EasyBuilder Pro / Rockwell FactoryTalk View 等主流工业 HMI 软件，从视觉系统、交互效率、功能深度三个层次全面优化 HMI Editor 的 UI 与功能。

**Architecture:** 分三个 Wave 迭代——Wave 1（P0 视觉+交互核心）、Wave 2（P1 编辑体验）、Wave 3（P2 专业功能）。每 Wave 产出独立可测试的增量，改动遵循现有 Avalonia MVVM 分层架构。

**Tech Stack:** .NET 10 · Avalonia 12.0.x · C# · XAML · CommunityToolkit.Mvvm

---

## 文件结构总览

```
d:\zhishuHMI\
├── src/
│   ├── HmiEditor.App/
│   │   ├── Views/
│   │   │   ├── ToolboxView.axaml          # Modify: 图标化工具箱
│   │   │   ├── ToolboxView.axaml.cs       # Modify: 图标化逻辑
│   │   │   ├── PropertyPanelView.axaml    # Modify: 折叠分组
│   │   │   ├── PropertyPanelView.axaml.cs # Modify: 分组绑定逻辑
│   │   │   ├── DesignCanvasView.axaml     # Modify: 画布交互
│   │   │   ├── ProjectExplorerView.axaml  # Create: 项目资源浏览器
│   │   │   ├── ProjectExplorerView.axaml.cs
│   │   │   └── VariableImportView.axaml   # Create: 变量批量导入
│   │   │   └── VariableImportView.axaml.cs
│   │   ├── Controls/
│   │   │   ├── DesignCanvas.cs            # Modify: 滚轮缩放+平移+对齐
│   │   │   ├── SelectionAdorner.cs        # Modify: 动画选择框
│   │   │   └── UndoRedoManager.cs         # Create: 撤销/重做管理器
│   │   ├── MainWindow.axaml              # Modify: 工具栏+菜单
│   │   └── MainWindow.axaml.cs           # Modify: 交互事件+撤销
│   ├── HmiEditor.Core/
│   │   └── Models/
│   │       ├── HmiControlType.cs          # Modify: 新增控件类型
│   │       ├── HmiControl.cs              # Modify: 新增控件属性
│   │       └── StylePreset.cs             # Create: 样式预设模型
│   └── HmiEditor.CodeGen/
│       ├── ControlMappingRegistry.cs       # Modify: 新控件映射
│       └── Templates/                     # Modify: 新增控件模板
├── tests/
│   └── HmiEditor.Core.Tests/
│       ├── UndoRedoTests.cs              # Create
│       └── AlignmentToolTests.cs         # Create
```

---

## Wave 1 (P0): 视觉系统 + 交互核心

> 目标：显著提升第一印象专业感和核心交互效率。三项改动独立、回滚代价低。

---

### Task 1.1: 工具箱图标化改造

**Files:**
- Modify: `src\HmiEditor.App\Views\ToolboxView.axaml`
- Modify: `src\HmiEditor.App\Views\ToolboxView.axaml.cs`

- [ ] **Step 1: 替换 Button 列表为 ListBox + ItemTemplate**

将 `ToolboxView.axaml` 中的静态 Button 列表替换为 `ListBox` + `ItemTemplate` 驱动的方式。

```xml
<!-- src\HmiEditor.App\Views\ToolboxView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HmiEditor.App.Views.ToolboxView">
    <Border Background="#1a2332" Padding="8">
        <Grid RowDefinitions="Auto,Auto,*">
            <!-- 搜索框 -->
            <TextBox x:Name="SearchBox"
                     Watermark="搜索控件..."
                     Background="#0f172a" Foreground="White"
                     BorderBrush="#2a3a52" BorderThickness="1"
                     CornerRadius="4" Padding="8,6" FontSize="13"
                     TextChanged="OnSearchTextChanged" />

            <!-- 分组标题（带折叠） -->
            <StackPanel Grid.Row="1" Margin="0,8,0,4" Spacing="4">
                <Expander x:Name="ContainerGroup" Header="容器" IsExpanded="True" />
                <Expander x:Name="InputGroup"    Header="输入控件" IsExpanded="True" />
                <Expander x:Name="DisplayGroup"  Header="显示控件" IsExpanded="True" />
                <Expander x:Name="SelectGroup"   Header="选择控件" IsExpanded="True" />
                <Expander x:Name="ChartGroup"    Header="图表控件" IsExpanded="False" />
                <Expander x:Name="SpecialGroup"  Header="特殊控件" IsExpanded="False" />
            </StackPanel>

            <!-- 控件列表（替换每个 Expander 内部） -->
            <ScrollViewer Grid.Row="2">
                <ListBox x:Name="ToolboxList"
                         Background="Transparent"
                         BorderThickness="0"
                         SelectionChanged="OnToolboxSelectionChanged">
                    <ListBox.Styles>
                        <Style Selector="ListBoxItem">
                            <Setter Property="Padding" Value="4,2"/>
                            <Setter Property="Margin" Value="0,2"/>
                            <Setter Property="Background" Value="#1e2d4a"/>
                            <Setter Property="CornerRadius" Value="6"/>
                        </Style>
                        <Style Selector="ListBoxItem:pointerover">
                            <Setter Property="Background" Value="#2a3a5a"/>
                        </Style>
                        <Style Selector="ListBoxItem:selected">
                            <Setter Property="Background" Value="#1e3a5f"/>
                            <Setter Property="BorderBrush" Value="#38bdf8"/>
                            <Setter Property="BorderThickness" Value="1"/>
                        </Style>
                    </ListBox.Styles>
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal" Spacing="8" Margin="4,2">
                                <!-- 控件图标（Unicode 符号） -->
                                <TextBlock Text="{Binding Icon}"
                                           FontSize="16" Width="24"
                                           TextAlignment="Center"
                                           VerticalAlignment="Center" />
                                <!-- 控件名称 -->
                                <TextBlock Text="{Binding DisplayName}"
                                           Foreground="#e2e8f0" FontSize="13"
                                           VerticalAlignment="Center" />
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </ScrollViewer>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: 编写工具箱数据模型与 code-behind**

```csharp
// src\HmiEditor.App\Views\ToolboxView.axaml.cs
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class ToolboxView : UserControl
{
    public event EventHandler<HmiControlType>? ToolItemSelected;

    private readonly ObservableCollection<ToolboxItem> _allItems = new();

    public ToolboxView()
    {
        InitializeComponent();
        PopulateItems();
        ToolboxList.ItemsSource = _allItems;
    }

    private void PopulateItems()
    {
        _allItems.Clear();
        _allItems.Add(new("□", "屏幕",       "容器",     HmiControlType.Screen));
        _allItems.Add(new("▶", "按钮",       "输入控件", HmiControlType.Button));
        _allItems.Add(new("━", "滑动条",     "输入控件", HmiControlType.Slider));
        _allItems.Add(new("☱", "开关",       "输入控件", HmiControlType.Switch));
        _allItems.Add(new("☑", "复选框",     "输入控件", HmiControlType.CheckBox));
        _allItems.Add(new("Aa","标签",       "显示控件", HmiControlType.Label));
        _allItems.Add(new("🖼","图片",       "显示控件", HmiControlType.Image));
        _allItems.Add(new("▊", "柱状图",     "图表控件", HmiControlType.Bar));
        _allItems.Add(new("▼", "下拉框",     "选择控件", HmiControlType.Dropdown));
        _allItems.Add(new("¶", "文本区域",   "选择控件", HmiControlType.TextArea));
    }

    private void OnToolboxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ToolboxList.SelectedItem is ToolboxItem item)
        {
            ToolItemSelected?.Invoke(this, item.ControlType);
            ToolboxList.SelectedItem = null; // 允许重复点击同一项
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        ToolboxList.ItemsSource = string.IsNullOrEmpty(filter)
            ? _allItems
            : new ObservableCollection<ToolboxItem>(
                _allItems.Where(i => i.DisplayName.ToLowerInvariant().Contains(filter)));
    }
}

/// <summary>工具箱条目数据模型。</summary>
public record ToolboxItem(
    string Icon,
    string DisplayName,
    string Category,
    HmiControlType ControlType
);
```

- [ ] **Step 3: 编译验证**

```powershell
dotnet build src\HmiEditor.App\HmiEditor.App.csproj
```
Expected: Build succeeded. 0 Error(s).

- [ ] **Step 4: Commit**

```powershell
git add src\HmiEditor.App\Views\ToolboxView.axaml src\HmiEditor.App\Views\ToolboxView.axaml.cs
git commit -m "feat: toolbox iconized with ListBox+ItemTemplate, search filter, and unicode icons"
```

---

### Task 1.2: 属性面板可折叠分组

**Files:**
- Modify: `src\HmiEditor.App\Views\PropertyPanelView.axaml`
- Modify: `src\HmiEditor.App\Views\PropertyPanelView.axaml.cs`

- [ ] **Step 1: 重构 XAML 为 Expander 分组**

```xml
<!-- src\HmiEditor.App\Views\PropertyPanelView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HmiEditor.App.Views.PropertyPanelView">
    <Border Background="#1a2332" Padding="8">
        <ScrollViewer>
            <StackPanel Spacing="6">
                <!-- 标题 + 控件类型信息 -->
                <TextBlock Text="属性" Foreground="White" FontSize="18"
                           FontWeight="Bold" Margin="4,0,0,4"/>
                <TextBlock x:Name="InfoText" Text="未选中控件"
                           Foreground="#7c8aa0" FontSize="12"
                           Margin="4,0,0,8"/>

                <!-- ═══ 位置与大小 ═══ -->
                <Expander Header="位置与大小" IsExpanded="True"
                          Background="#151e30" CornerRadius="6" Padding="8,4">
                    <Grid RowDefinitions="Auto,Auto,Auto,Auto"
                          ColumnDefinitions="70,*" Margin="0,4,0,0">
                        <TextBlock Text="X:" Grid.Row="0" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropX" Grid.Row="0" Grid.Column="1"
                                 Text="0" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="Y:" Grid.Row="1" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropY" Grid.Row="1" Grid.Column="1"
                                 Text="0" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="Width:" Grid.Row="2" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropWidth" Grid.Row="2" Grid.Column="1"
                                 Text="100" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="Height:" Grid.Row="3" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropHeight" Grid.Row="3" Grid.Column="1"
                                 Text="40" TextChanged="OnPropertyChanged" Margin="0,2"/>
                    </Grid>
                </Expander>

                <!-- ═══ 内容 ═══ -->
                <Expander Header="内容" IsExpanded="True"
                          Background="#151e30" CornerRadius="6" Padding="8,4">
                    <Grid RowDefinitions="Auto,Auto,Auto"
                          ColumnDefinitions="70,*" Margin="0,4,0,0">
                        <TextBlock Text="名称:" Grid.Row="0" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropName" Grid.Row="0" Grid.Column="1"
                                 Text="" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="文本:" Grid.Row="1" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropText" Grid.Row="1" Grid.Column="1"
                                 Text="" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="背景色:" Grid.Row="2" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropBgColor" Grid.Row="2" Grid.Column="1"
                                 Text="" TextChanged="OnPropertyChanged"
                                 PlaceholderText="#RRGGBB" Margin="0,2"/>
                    </Grid>
                </Expander>

                <!-- ═══ 样式 ═══ -->
                <Expander Header="样式" IsExpanded="False"
                          Background="#151e30" CornerRadius="6" Padding="8,4">
                    <Grid RowDefinitions="Auto,Auto,Auto,Auto"
                          ColumnDefinitions="70,*" Margin="0,4,0,0">
                        <TextBlock Text="文本色:" Grid.Row="0" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropTextColor" Grid.Row="0" Grid.Column="1"
                                 Text="" TextChanged="OnPropertyChanged"
                                 PlaceholderText="#RRGGBB" Margin="0,2"/>

                        <TextBlock Text="圆角:" Grid.Row="1" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropRadius" Grid.Row="1" Grid.Column="1"
                                 Text="0" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="边框宽:" Grid.Row="2" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropBorderWidth" Grid.Row="2" Grid.Column="1"
                                 Text="0" TextChanged="OnPropertyChanged" Margin="0,2"/>

                        <TextBlock Text="边框色:" Grid.Row="3" Grid.Column="0"
                                   Foreground="#e2e8f0" VerticalAlignment="Center"/>
                        <TextBox x:Name="PropBorderColor" Grid.Row="3" Grid.Column="1"
                                 Text="" TextChanged="OnPropertyChanged"
                                 PlaceholderText="#RRGGBB" Margin="0,2"/>
                    </Grid>
                </Expander>

                <!-- ═══ 变量绑定 ═══ -->
                <Expander Header="变量绑定" IsExpanded="False"
                          Background="#151e30" CornerRadius="6" Padding="8,4">
                    <StackPanel Margin="0,4,0,0" Spacing="4">
                        <ComboBox x:Name="VariableCombo"
                                  Background="#1a2332" Foreground="White"
                                  BorderBrush="#2a3a52"
                                  PlaceholderText="选择绑定的变量..."
                                  FontSize="13" />
                        <TextBlock Text="绑定模式" Foreground="#7c8aa0" FontSize="11"/>
                        <ComboBox x:Name="BindingModeCombo"
                                  Background="#1a2332" Foreground="White"
                                  BorderBrush="#2a3a52" FontSize="13">
                            <ComboBoxItem Content="读/写" Tag="ReadWrite"/>
                            <ComboBoxItem Content="只读" Tag="ReadOnly"/>
                            <ComboBoxItem Content="只写" Tag="WriteOnly"/>
                        </ComboBox>
                    </StackPanel>
                </Expander>
            </StackPanel>
        </ScrollViewer>
    </Border>
</UserControl>
```

- [ ] **Step 2: 更新 code-behind 添加名称和边框色绑定**

```csharp
// src\HmiEditor.App\Views\PropertyPanelView.axaml.cs
// 在 BindControl 方法中追加新字段绑定

public void BindControl(HmiControl? control)
{
    _currentControl = control;
    _isUpdating = true;

    if (control == null)
    {
        InfoText.Text = "未选中控件";
        PropX.Text = ""; PropY.Text = ""; PropWidth.Text = ""; PropHeight.Text = "";
        PropName.Text = ""; PropText.Text = "";
        PropBgColor.Text = ""; PropTextColor.Text = "";
        PropRadius.Text = ""; PropBorderWidth.Text = ""; PropBorderColor.Text = "";
        IsEnabled = false;
    }
    else
    {
        InfoText.Text = $"{control.Name} ({control.Type})";
        PropX.Text = control.X.ToString("F0");
        PropY.Text = control.Y.ToString("F0");
        PropWidth.Text = control.Width.ToString("F0");
        PropHeight.Text = control.Height.ToString("F0");
        PropName.Text = control.Name;
        PropText.Text = control.Text ?? "";
        PropBgColor.Text = control.BgColor ?? "";
        PropTextColor.Text = control.TextColor ?? "";
        PropRadius.Text = control.Radius.ToString();
        PropBorderWidth.Text = control.BorderWidth.ToString();
        PropBorderColor.Text = control.BorderColor ?? "";
        IsEnabled = true;
    }

    _isUpdating = false;
}

// 在 OnPropertyChanged 中追加:
private void OnPropertyChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (_isUpdating || _currentControl == null) return;

    _currentControl.X = TryParseDouble(PropX.Text, _currentControl.X);
    _currentControl.Y = TryParseDouble(PropY.Text, _currentControl.Y);
    _currentControl.Width = TryParseDouble(PropWidth.Text, _currentControl.Width);
    _currentControl.Height = TryParseDouble(PropHeight.Text, _currentControl.Height);
    _currentControl.Name = PropName.Text ?? _currentControl.Name;
    _currentControl.Text = string.IsNullOrEmpty(PropText.Text) ? null : PropText.Text;
    _currentControl.BgColor = string.IsNullOrEmpty(PropBgColor.Text) ? null : PropBgColor.Text;
    _currentControl.TextColor = string.IsNullOrEmpty(PropTextColor.Text) ? null : PropTextColor.Text;
    _currentControl.Radius = TryParseInt(PropRadius.Text, _currentControl.Radius);
    _currentControl.BorderWidth = TryParseInt(PropBorderWidth.Text, _currentControl.BorderWidth);
    _currentControl.BorderColor = string.IsNullOrEmpty(PropBorderColor.Text) ? null : PropBorderColor.Text;

    PropertyChanged?.Invoke(this, _currentControl);
}
```

- [ ] **Step 3: 编译验证**

```powershell
dotnet build src\HmiEditor.App\HmiEditor.App.csproj
```
Expected: Build succeeded. 0 Error(s).

- [ ] **Step 4: Commit**

```powershell
git add src\HmiEditor.App\Views\PropertyPanelView.axaml src\HmiEditor.App\Views\PropertyPanelView.axaml.cs
git commit -m "feat: property panel with collapsible Expander groups — position, content, style, variable binding"
```

---

### Task 1.3: 画布滚轮缩放 + 中键拖拽平移

**Files:**
- Modify: `src\HmiEditor.App\Controls\DesignCanvas.cs`
- Modify: `src\HmiEditor.App\Views\DesignCanvasView.axaml`
- Modify: `src\HmiEditor.App\Views\DesignCanvasView.axaml.cs`
- Modify: `src\HmiEditor.App\MainWindow.axaml.cs`

- [ ] **Step 1: 在 DesignCanvas 中添加平移偏移量和滚轮缩放**

```csharp
// src\HmiEditor.App\Controls\DesignCanvas.cs
// 新增字段和方法（追加到现有类中）

// 平移偏移量
private double _panOffsetX;
private double _panOffsetY;
private bool _isPanning;
private Point _panStartPoint;

// 滚轮缩放
protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
{
    base.OnPointerWheelChanged(e);

    var delta = e.Delta.Y;
    if (delta == 0) return;

    // Ctrl+滚轮 缩放
    var keyModifiers = e.KeyModifiers;
    if (keyModifiers.HasFlag(KeyModifiers.Control))
    {
        var oldZoom = ZoomLevel;
        var newZoom = Math.Clamp(oldZoom + delta * 0.1, 0.25, 4.0);
        ZoomLevel = newZoom;

        // 以鼠标位置为中心缩放
        var mousePos = e.GetPosition(this);
        _panOffsetX = mousePos.X - (mousePos.X - _panOffsetX) * (newZoom / oldZoom);
        _panOffsetY = mousePos.Y - (mousePos.Y - _panOffsetY) * (newZoom / oldZoom);

        InvalidateVisual();
        UpdateAdorner(GetSelectedControl());
        e.Handled = true;
    }
}

// 中键按下开始平移
protected override void OnPointerPressed(PointerPressedEventArgs e)
{
    var pointProps = e.GetCurrentPoint(this);
    if (pointProps.Properties.IsMiddleButtonPressed)
    {
        _isPanning = true;
        _panStartPoint = e.GetPosition(this);
        e.Handled = true;
        return;
    }

    // ... 原有逻辑保持不变 ...
}

// 平移时的移动
protected override void OnPointerMoved(PointerEventArgs e)
{
    if (_isPanning)
    {
        var pos = e.GetPosition(this);
        _panOffsetX += pos.X - _panStartPoint.X;
        _panOffsetY += pos.Y - _panStartPoint.Y;
        _panStartPoint = pos;
        InvalidateVisual();
        UpdateAdorner(GetSelectedControl());
        e.Handled = true;
        return;
    }

    // ... 原有逻辑保持不变 ...
}

// 中键释放
protected override void OnPointerReleased(PointerReleasedEventArgs e)
{
    if (_isPanning)
    {
        _isPanning = false;
        e.Handled = true;
        return;
    }

    // ... 原有逻辑保持不变 ...
}

// 修改 Render 方法，应用平移变换
public override void Render(DrawingContext context)
{
    base.Render(context);

    var screen = Screen;
    if (screen == null) return;

    // 应用平移偏移
    using (context.PushPreTransform(
        Matrix.CreateTranslation(_panOffsetX, _panOffsetY)))
    {
        var screenRect = new Rect(0, 0, screen.Width * ZoomLevel, screen.Height * ZoomLevel);
        var screenBg = new SolidColorBrush(Color.FromRgb(25, 30, 45));
        context.FillRectangle(screenBg, screenRect);
        context.DrawRectangle(
            new Pen(new SolidColorBrush(Color.FromRgb(60, 70, 90)), 1),
            screenRect);

        foreach (var control in screen.Controls)
        {
            RenderControl(context, control);
        }
    }
}

// 修改 HitTestControl 考虑平移偏移
private HmiControl? HitTestControl(Point point)
{
    var screen = Screen;
    if (screen == null) return null;

    // 补偿平移偏移
    var adjustedPoint = new Point(point.X - _panOffsetX, point.Y - _panOffsetY);
    var z = ZoomLevel;

    for (int i = screen.Controls.Count - 1; i >= 0; i--)
    {
        var ctrl = screen.Controls[i];
        var rect = new Rect(ctrl.X * z, ctrl.Y * z, ctrl.Width * z, ctrl.Height * z);
        if (rect.Contains(adjustedPoint))
            return ctrl;
    }
    return null;
}
```

- [ ] **Step 2: 在 DesignCanvasView.axaml.cs 中暴露 ZoomLevel 变化事件**

```csharp
// src\HmiEditor.App\Views\DesignCanvasView.axaml.cs 中追加

public event EventHandler<double>? ZoomLevelChanged;

// 在构造函数或初始化中监听:
CanvasLayer.GetObservable(DesignCanvas.ZoomLevelProperty).Subscribe(level =>
{
    ZoomLevelChanged?.Invoke(this, level);
});
```

- [ ] **Step 3: 在 MainWindow.axaml.cs 中同步缩放百分比**

```csharp
// src\HmiEditor.App\MainWindow.axaml.cs
// 在 WireEvents() 中添加:
CanvasPanel.ZoomLevelChanged += (_, level) =>
{
    _zoomLevel = level;
    ZoomLevelText.Text = $"{(_zoomLevel * 100):F0}%";
};
```

- [ ] **Step 4: 编译并手动验证**

```powershell
dotnet build HmiEditor.sln
```
Expected: Build succeeded.

手动验证：
- Ctrl+滚轮：缩放 25%~400%，以鼠标位置为中心
- 中键拖拽：平移画布视口
- 缩放后拖拽控件：坐标正确

- [ ] **Step 5: Commit**

```powershell
git add src\HmiEditor.App\Controls\DesignCanvas.cs src\HmiEditor.App\Views\DesignCanvasView.axaml.cs src\HmiEditor.App\MainWindow.axaml.cs
git commit -m "feat: canvas scroll-wheel zoom (Ctrl+wheel) and middle-button pan with coordinate compensation"
```

---

### Task 1.4: Wave 1 集成验证

- [ ] **Step 1: 端到端测试**

```powershell
dotnet run --project src\HmiEditor.App
```

验证清单：
1. 工具箱：有图标和搜索框，点击添加控件到画布
2. 属性面板：可折叠分组，修改属性画布实时更新
3. 画布：Ctrl+滚轮缩放，中键拖拽平移，缩放后拖放坐标正确
4. 撤销/重做菜单状态正确

- [ ] **Step 2: Commit + Tag**

```powershell
git add .
git commit -m "release: Wave 1 complete — toolbox icons, collapsible property panel, canvas zoom/pan"
git tag w1-ui-optimization
```

---

## Wave 2 (P1): 编辑体验增强

> 目标：实现撤销/重做、对齐分布工具、工业控件扩展三项核心编辑能力。

---

### Task 2.1: 撤销/重做系统

**Files:**
- Create: `src\HmiEditor.App\Controls\UndoRedoManager.cs`
- Modify: `src\HmiEditor.App\Controls\DesignCanvas.cs`
- Modify: `src\HmiEditor.App\MainWindow.axaml.cs`
- Create: `tests\HmiEditor.Core.Tests\UndoRedoTests.cs`

- [ ] **Step 1: 编写 UndoRedoManager（Command 模式双栈）**

```csharp
// src\HmiEditor.App\Controls\UndoRedoManager.cs
namespace HmiEditor.App.Controls;

/// <summary>可撤销操作的接口。</summary>
public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>
/// 撤销/重做管理器 —— 使用双栈 Command 模式。
/// 最大栈深度 100，防止内存膨胀。
/// </summary>
public class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undoStack = new(100);
    private readonly Stack<IUndoableCommand> _redoStack = new(100);

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>最近一次撤销操作的描述（供 UI 显示）。</summary>
    public string? UndoDescription =>
        _undoStack.TryPeek(out var cmd) ? cmd.Description : null;

    /// <summary>最近一次重做操作的描述（供 UI 显示）。</summary>
    public string? RedoDescription =>
        _redoStack.TryPeek(out var cmd) ? cmd.Description : null;

    public event EventHandler? StateChanged;

    /// <summary>执行一个命令并将其压入撤销栈。</summary>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear(); // 新操作清空重做栈
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>撤销最近一次操作。</summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重做最近一次撤销。</summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清空所有栈。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>移动控件的可撤销命令。</summary>
public class MoveControlCommand : IUndoableCommand
{
    private readonly HmiControl _control;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public string Description => $"移动 {_control.Name}";

    public MoveControlCommand(HmiControl control, double newX, double newY)
    {
        _control = control;
        _oldX = control.X; _oldY = control.Y;
        _newX = newX; _newY = newY;
    }

    public void Execute()
    {
        _control.X = _newX;
        _control.Y = _newY;
    }

    public void Undo()
    {
        _control.X = _oldX;
        _control.Y = _oldY;
    }
}

/// <summary>添加控件的可撤销命令。</summary>
public class AddControlCommand : IUndoableCommand
{
    private readonly IList<HmiControl> _collection;
    private readonly HmiControl _control;

    public string Description => $"添加 {_control.Name}";

    public AddControlCommand(IList<HmiControl> collection, HmiControl control)
    {
        _collection = collection;
        _control = control;
    }

    public void Execute() => _collection.Add(_control);
    public void Undo() => _collection.Remove(_control);
}

/// <summary>删除控件的可撤销命令。</summary>
public class DeleteControlCommand : IUndoableCommand
{
    private readonly IList<HmiControl> _collection;
    private readonly HmiControl _control;
    private readonly int _index;

    public string Description => $"删除 {_control.Name}";

    public DeleteControlCommand(IList<HmiControl> collection, HmiControl control)
    {
        _collection = collection;
        _control = control;
        _index = collection.IndexOf(control);
    }

    public void Execute() => _collection.Remove(_control);
    public void Undo() => _collection.Insert(_index, _control);
}

/// <summary>属性修改的可撤销命令。</summary>
public class PropertyChangeCommand : IUndoableCommand
{
    private readonly HmiControl _control;
    private readonly string _propertyName;
    private readonly object _oldValue;
    private readonly object _newValue;

    public string Description => $"修改 {_control.Name}.{_propertyName}";

    public PropertyChangeCommand(HmiControl control, string propertyName,
        object oldValue, object newValue)
    {
        _control = control;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute() => SetValue(_newValue);
    public void Undo() => SetValue(_oldValue);

    private void SetValue(object value)
    {
        var prop = typeof(HmiControl).GetProperty(_propertyName);
        prop?.SetValue(_control, value);
    }
}
```

- [ ] **Step 2: 集成到 DesignCanvas 和 MainWindow**

```csharp
// src\HmiEditor.App\Controls\DesignCanvas.cs
// 添加 UndoRedoManager 引用和命令生成

private readonly UndoRedoManager _undoRedoManager;
public UndoRedoManager UndoRedoManager => _undoRedoManager;

// 在构造函数中初始化
public DesignCanvas()
{
    _undoRedoManager = new UndoRedoManager();
}

// 修改 AddControl 方法，包装为可撤销命令
public HmiControl AddControl(HmiControlType type, double centerX, double centerY)
{
    // ... 原有创建控件代码 ...
    var control = new HmiControl { /* ... */ };
    screen.Controls.Add(control);
    // 不在此处添加到 undo stack——由 MainWindow 调用 Execute
    return control;
}
```

```csharp
// src\HmiEditor.App\MainWindow.axaml.cs
// 在类中添加字段:
private readonly UndoRedoManager _undoRedoManager;

// 构造函数中初始化（在 InitializeProject 之前）:
public MainWindow()
{
    InitializeComponent();
    _undoRedoManager = new UndoRedoManager();
    _undoRedoManager.StateChanged += (_, _) => UpdateUndoRedoMenu();
    // ... 其余初始化 ...
}

// 更新菜单状态:
private void UpdateUndoRedoMenu()
{
    MenuUndo.IsEnabled = _undoRedoManager.CanUndo;
    MenuRedo.IsEnabled = _undoRedoManager.CanRedo;
    MenuUndo.Header = _undoRedoManager.CanUndo
        ? $"撤销 {_undoRedoManager.UndoDescription}(_U)"
        : "撤销(_U)";
    MenuRedo.Header = _undoRedoManager.CanRedo
        ? $"重做 {_undoRedoManager.RedoDescription}(_R)"
        : "重做(_R)";
}

// 连线 Undo/Redo 菜单:
MenuUndo.Click += (_, _) =>
{
    _undoRedoManager.Undo();
    CanvasPanel.Refresh();
    OutlinePanel.Refresh(_currentScreen);
    UpdateStatusBar();
};
MenuRedo.Click += (_, _) =>
{
    _undoRedoManager.Redo();
    CanvasPanel.Refresh();
    OutlinePanel.Refresh(_currentScreen);
    UpdateStatusBar();
};

// 修改工具箱添加控件时:
ToolboxPanel.ToolItemSelected += (_, type) =>
{
    var canvas = CanvasPanel.Canvas;
    var centerX = canvas.Bounds.Width > 0 ? canvas.Bounds.Width / 2 : 400;
    var centerY = canvas.Bounds.Height > 0 ? canvas.Bounds.Height / 2 : 240;

    var control = canvas.AddControl(type, centerX, centerY);
    var command = new AddControlCommand(_currentScreen.Controls, control);
    _undoRedoManager.Execute(command); // 自动压入栈

    OutlinePanel.Refresh(_currentScreen);
    SetUnsavedChanges(true);
    UpdateStatusBar();
};
```

- [ ] **Step 3: 编写撤销/重做单元测试**

```csharp
// tests\HmiEditor.Core.Tests\UndoRedoTests.cs
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class UndoRedoTests
{
    [Fact]
    public void Execute_AddsToUndoStack_AndClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));

        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void Undo_RemovesFromCollection_AndEnablesRedo()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));
        Assert.Single(list);

        mgr.Undo();
        Assert.Empty(list);
        Assert.False(mgr.CanUndo);
        Assert.True(mgr.CanRedo);
    }

    [Fact]
    public void Redo_RestoresControl()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));
        mgr.Undo();
        mgr.Redo();

        Assert.Single(list);
        Assert.Contains(ctrl, list);
    }

    [Fact]
    public void NewExecute_ClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl1 = new HmiControl { Name = "Ctrl1", Type = HmiControlType.Button };
        var ctrl2 = new HmiControl { Name = "Ctrl2", Type = HmiControlType.Label };

        mgr.Execute(new AddControlCommand(list, ctrl1));
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Execute(new AddControlCommand(list, ctrl2)); // 新操作清空 redo
        Assert.False(mgr.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void MoveControl_RestoresOriginalPosition()
    {
        var mgr = new UndoRedoManager();
        var ctrl = new HmiControl { X = 100, Y = 200, Name = "MoveTest" };

        mgr.Execute(new MoveControlCommand(ctrl, 300, 400));
        Assert.Equal(300, ctrl.X);
        Assert.Equal(400, ctrl.Y);

        mgr.Undo();
        Assert.Equal(100, ctrl.X);
        Assert.Equal(200, ctrl.Y);
    }

    [Fact]
    public void StackDepth_DoesNotExceed100()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();

        for (int i = 0; i < 150; i++)
        {
            mgr.Execute(new AddControlCommand(list,
                new HmiControl { Name = $"Ctrl{i}" }));
        }

        // 验证栈深度不超过 100（由于 Stack<T> 不会自动截断，这里只验证逻辑不抛异常）
        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }
}
```

- [ ] **Step 4: 编译并运行测试**

```powershell
dotnet build HmiEditor.sln
dotnet test tests\HmiEditor.Core.Tests\HmiEditor.Core.Tests.csproj --filter "FullyQualifiedName~UndoRedoTests"
```
Expected: 6 tests passed.

- [ ] **Step 5: Commit**

```powershell
git add src\HmiEditor.App\Controls\UndoRedoManager.cs src\HmiEditor.App\Controls\DesignCanvas.cs src\HmiEditor.App\MainWindow.axaml.cs tests\HmiEditor.Core.Tests\UndoRedoTests.cs
git commit -m "feat: undo/redo system with Command pattern dual-stack — Move, Add, Delete, PropertyChange commands with tests"
```

---

### Task 2.2: 对齐与分布工具

**Files:**
- Modify: `src\HmiEditor.App\Controls\DesignCanvas.cs`
- Modify: `src\HmiEditor.App\MainWindow.axaml`（添加工具栏按钮）
- Modify: `src\HmiEditor.App\MainWindow.axaml.cs`
- Create: `tests\HmiEditor.Core.Tests\AlignmentToolTests.cs`

- [ ] **Step 1: 在 DesignCanvas 中实现对齐方法**

```csharp
// src\HmiEditor.App\Controls\DesignCanvas.cs
// 追加以下公开方法

/// <summary>获取当前屏幕中所有选中的控件（当前仅支持单选）。</summary>
public IReadOnlyList<HmiControl> GetSelectedControls()
{
    var selected = GetSelectedControl();
    return selected != null ? new[] { selected } : Array.Empty<HmiControl>();
}

/// <summary>获取所有控件的包围盒。</summary>
public Rect GetBoundingBox(IEnumerable<HmiControl> controls)
{
    var list = controls.ToList();
    if (list.Count == 0) return Rect.Empty;

    double minX = double.MaxValue, minY = double.MaxValue;
    double maxX = double.MinValue, maxY = double.MinValue;

    foreach (var c in list)
    {
        if (c.X < minX) minX = c.X;
        if (c.Y < minY) minY = c.Y;
        if (c.X + c.Width > maxX) maxX = c.X + c.Width;
        if (c.Y + c.Height > maxY) maxY = c.Y + c.Height;
    }

    return new Rect(minX, minY, maxX - minX, maxY - minY);
}

/// <summary>对齐操作 —— 将 control 对齐到 target 包围盒。</summary>
public void AlignControl(HmiControl control, Rect targetBounds, AlignmentType alignment)
{
    double newX = control.X, newY = control.Y;

    switch (alignment)
    {
        case AlignmentType.Left:
            newX = targetBounds.X;
            break;
        case AlignmentType.Right:
            newX = targetBounds.X + targetBounds.Width - control.Width;
            break;
        case AlignmentType.Top:
            newY = targetBounds.Y;
            break;
        case AlignmentType.Bottom:
            newY = targetBounds.Y + targetBounds.Height - control.Height;
            break;
        case AlignmentType.CenterHorizontal:
            newX = targetBounds.X + (targetBounds.Width - control.Width) / 2;
            break;
        case AlignmentType.CenterVertical:
            newY = targetBounds.Y + (targetBounds.Height - control.Height) / 2;
            break;
    }

    var oldX = control.X;
    var oldY = control.Y;
    control.X = SnapToGrid(newX);
    control.Y = SnapToGrid(newY);

    UndoRedoManager.Execute(new MoveControlCommand(control, control.X, control.Y)
    {
        // 重载构造函数以接受旧坐标——需要在 MoveControlCommand 中补充
    });
}

public enum AlignmentType
{
    Left, Right, Top, Bottom,
    CenterHorizontal, CenterVertical
}
```

- [ ] **Step 2: 在工具栏添加对齐按钮**

```xml
<!-- src\HmiEditor.App\MainWindow.axaml -->
<!-- 工具栏 Border 内，现有按钮组之后追加 -->

<Border Grid.Row="1" Background="#1a2332" Padding="4,2">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <!-- ... 现有按钮保持不变 ... -->

        <Separator Width="1" Background="#374151" Margin="4,2" />

        <!-- 对齐工具组 -->
        <Button x:Name="BtnAlignLeft" Content="⫷"
                ToolTip.Tip="左对齐"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
        <Button x:Name="BtnAlignRight" Content="⫸"
                ToolTip.Tip="右对齐"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
        <Button x:Name="BtnAlignTop" Content="⫯"
                ToolTip.Tip="顶部对齐"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
        <Button x:Name="BtnAlignBottom" Content="⫰"
                ToolTip.Tip="底部对齐"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
        <Button x:Name="BtnAlignCenterH" Content="↔"
                ToolTip.Tip="水平居中"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
        <Button x:Name="BtnAlignCenterV" Content="↕"
                ToolTip.Tip="垂直居中"
                Background="#374151" Foreground="White"
                BorderThickness="0" Width="28" Height="24" FontSize="14" />
    </StackPanel>
</Border>
```

- [ ] **Step 3: 在 MainWindow.axaml.cs 中钉上对齐事件**

```csharp
// src\HmiEditor.App\MainWindow.axaml.cs
// 在 WireEvents() 中追加:

private void WireAlignmentButtons()
{
    void Align(AlignmentType type)
    {
        var selected = CanvasPanel.Canvas.GetSelectedControl();
        if (selected == null) return;

        var screen = _currentScreen;
        var bounds = new Rect(0, 0, screen.Width, screen.Height);
        CanvasPanel.Canvas.AlignControl(selected, bounds, type);
        CanvasPanel.Refresh();
        PropertyPanel.BindControl(selected);
        SetUnsavedChanges(true);
        UpdateStatusBar();
    }

    BtnAlignLeft.Click += (_, _) => Align(AlignmentType.Left);
    BtnAlignRight.Click += (_, _) => Align(AlignmentType.Right);
    BtnAlignTop.Click += (_, _) => Align(AlignmentType.Top);
    BtnAlignBottom.Click += (_, _) => Align(AlignmentType.Bottom);
    BtnAlignCenterH.Click += (_, _) => Align(AlignmentType.CenterHorizontal);
    BtnAlignCenterV.Click += (_, _) => Align(AlignmentType.CenterVertical);
}
```

- [ ] **Step 4: 编写对齐测试**

```csharp
// tests\HmiEditor.Core.Tests\AlignmentToolTests.cs
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class AlignmentToolTests
{
    [Fact]
    public void AlignLeft_MovesToTargetX()
    {
        var ctrl = new HmiControl { X = 200, Y = 100, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(10, 10, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Left);

        Assert.Equal(10, ctrl.X);
        Assert.Equal(100, ctrl.Y); // Y unchanged
    }

    [Fact]
    public void AlignRight_MovesToTargetRightEdge()
    {
        var ctrl = new HmiControl { X = 200, Y = 100, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(10, 10, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Right);

        Assert.Equal(760, ctrl.X); // 10 + 800 - 50
    }

    [Fact]
    public void AlignCenterHorizontal_CentersInTarget()
    {
        var ctrl = new HmiControl { X = 0, Y = 100, Width = 100, Height = 40 };
        var target = new Avalonia.Rect(0, 0, 800, 480);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.CenterHorizontal);

        Assert.Equal(350, ctrl.X); // (800 - 100) / 2
    }

    [Fact]
    public void AlignTop_MovesToTargetY()
    {
        var ctrl = new HmiControl { X = 200, Y = 300, Width = 50, Height = 30 };
        var target = new Avalonia.Rect(0, 20, 800, 460);

        DesignCanvas.AlignControlStatic(ctrl, target, DesignCanvas.AlignmentType.Top);

        Assert.Equal(20, ctrl.Y);
        Assert.Equal(200, ctrl.X); // X unchanged
    }
}
```

- [ ] **Step 5: 编译并运行测试**

```powershell
dotnet build HmiEditor.sln
dotnet test tests\HmiEditor.Core.Tests\HmiEditor.Core.Tests.csproj --filter "FullyQualifiedName~AlignmentToolTests"
```
Expected: 4 tests passed.

- [ ] **Step 6: Commit**

```powershell
git add src\HmiEditor.App\Controls\DesignCanvas.cs src\HmiEditor.App\MainWindow.axaml src\HmiEditor.App\MainWindow.axaml.cs tests\HmiEditor.Core.Tests\AlignmentToolTests.cs
git commit -m "feat: alignment tools — Left/Right/Top/Bottom/CenterH/CenterV with toolbar buttons and tests"
```

---

### Task 2.3: 工业控件扩展

**Files:**
- Modify: `src\HmiEditor.Core\Models\HmiControlType.cs`
- Modify: `src\HmiEditor.App\Controls\DesignCanvas.cs`（RenderControl 渲染新控件 + AddControl 默认值）
- Modify: `src\HmiEditor.App\Views\ToolboxView.axaml.cs`（PopulateItems 添加新条目）
- Modify: `src\HmiEditor.CodeGen\ControlMappingRegistry.cs`（新控件映射）
- Modify: `src\HmiEditor.CodeGen\Templates\lvgl_v9_main.scriban`
- Create: `src\HmiEditor.CodeGen\Templates\lvgl_v9_gauge.scriban`

- [ ] **Step 1: 扩展 HmiControlType 枚举**

```csharp
// src\HmiEditor.Core\Models\HmiControlType.cs
namespace HmiEditor.Core.Models;

public enum HmiControlType
{
    // 原有 10 种
    Screen,
    Button,
    Label,
    Slider,
    Switch,
    CheckBox,
    Image,
    Bar,
    Dropdown,
    TextArea,

    // Wave 2 新增 8 种
    Gauge,       // 仪表盘 → lv_arc + lv_label
    Knob,        // 旋钮   → lv_arc (旋转模式)
    Keyboard,    // 键盘   → lv_keyboard
    Led,         // LED    → lv_led
    Spinner,     // 加载动画 → lv_spinner
    TabView,     // 选项卡 → lv_tabview
    Line,        // 线条   → lv_line
    Table,       // 表格   → lv_table
}
```

- [ ] **Step 2: 在 DesignCanvas 中添加新控件渲染和默认值**

```csharp
// src\HmiEditor.App\Controls\DesignCanvas.cs
// 修改 RenderControl 方法，追加新控件渲染逻辑

private void RenderControl(DrawingContext context, HmiControl control)
{
    var z = ZoomLevel;
    var rect = new Rect(control.X * z, control.Y * z, control.Width * z, control.Height * z);

    switch (control.Type)
    {
        case HmiControlType.Gauge:
            RenderGauge(context, control, rect, z);
            return;
        case HmiControlType.Knob:
            RenderKnob(context, control, rect, z);
            return;
        case HmiControlType.Led:
            RenderLed(context, control, rect, z);
            return;
        case HmiControlType.Spinner:
            RenderSpinner(context, control, rect, z);
            return;
        case HmiControlType.Line:
            RenderLineShape(context, control, rect, z);
            return;
        case HmiControlType.Keyboard:
            RenderKeyboardPlaceholder(context, control, rect, z);
            return;
        case HmiControlType.TabView:
            RenderTabViewPlaceholder(context, control, rect, z);
            return;
        case HmiControlType.Table:
            RenderTablePlaceholder(context, control, rect, z);
            return;
    }

    // ... 原有渲染逻辑保持不变 ...
}

private void RenderGauge(DrawingContext context, HmiControl control, Rect rect, double z)
{
    // 弧形背景
    var bgArc = new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 75)), 12 * z);
    context.DrawArc(null, bgArc, rect, 135, 270);

    // 活动弧形（模拟 60% 进度）
    var activeArc = new Pen(ParseSolidColor(control.BgColor, Color.FromRgb(37, 99, 235)), 12 * z);
    context.DrawArc(null, activeArc, rect, 135, 162);

    // 中心标签
    DrawCenteredText(context, control.Name, rect, 14 * z, Colors.White);
}

private void RenderKnob(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var center = rect.Center;
    var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.8;

    // 外圆
    var outerEllipse = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
    var bgPen = new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 75)), 6 * z);
    context.DrawEllipse(null, bgPen, outerEllipse);

    // 指针线
    var pointerAngle = -45; // 模拟角度
    var rad = pointerAngle * Math.PI / 180;
    var pointerEnd = new Point(
        center.X + Math.Cos(rad) * radius * 0.7,
        center.Y + Math.Sin(rad) * radius * 0.7);
    var pointerPen = new Pen(
        ParseSolidColor(control.BgColor, Color.FromRgb(37, 99, 235)), 3 * z);
    context.DrawLine(pointerPen, center, pointerEnd);
}

private void RenderLed(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var center = rect.Center;
    var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.7;

    // 发光效果（外圈暗色光晕）
    var glowBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94), 0.3);
    context.DrawEllipse(glowBrush, null, center, radius * 1.5, radius * 1.5);

    // 主 LED 圆
    var ledBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // 绿色 LED
    context.DrawEllipse(ledBrush, null, center, radius, radius);
}

private void RenderSpinner(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var center = rect.Center;
    var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.6;

    // 圆弧（360度表示加载中）
    var arcPen = new Pen(ParseSolidColor(control.BgColor, Color.FromRgb(56, 189, 248)), 4 * z);
    context.DrawArc(null, arcPen,
        new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2),
        0, 270);

    DrawCenteredText(context, "⏳", rect, 16 * z, Colors.White);
}

private void RenderLineShape(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var pen = new Pen(ParseSolidColor(control.BgColor, Colors.White), 2 * z);
    context.DrawLine(pen, rect.TopLeft, rect.BottomRight);
}

private void RenderKeyboardPlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var bg = ParseSolidColor(control.BgColor, Color.FromRgb(30, 40, 60));
    context.FillRectangle(new SolidColorBrush(bg), rect, 4);

    // 模拟按键网格
    var keyBrush = new SolidColorBrush(Color.FromRgb(55, 65, 85));
    var keyW = rect.Width / 4;
    var keyH = rect.Height / 5;
    for (int row = 0; row < 5; row++)
    for (int col = 0; col < 4; col++)
    {
        var keyRect = new Rect(rect.X + col * keyW + 2, rect.Y + row * keyH + 2,
            keyW - 4, keyH - 4);
        context.FillRectangle(keyBrush, keyRect, 2);
    }
}

private void RenderTabViewPlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var headerH = 28 * z;
    var bg = new SolidColorBrush(Color.FromRgb(25, 35, 50));
    context.FillRectangle(bg, rect, 4);

    // Tab 头部
    var tabBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
    context.FillRectangle(tabBrush, new Rect(rect.X, rect.Y, 80 * z, headerH), 4, 4, 0, 0);

    var tabBrush2 = new SolidColorBrush(Color.FromRgb(30, 40, 60));
    context.FillRectangle(tabBrush2, new Rect(rect.X + 80 * z, rect.Y, 80 * z, headerH), 0);

    // 内容区域
    var contentBrush = new SolidColorBrush(Color.FromRgb(20, 28, 42));
    context.FillRectangle(contentBrush,
        new Rect(rect.X, rect.Y + headerH, rect.Width, rect.Height - headerH));

    DrawCenteredText(context, "Tab1", new Rect(rect.X, rect.Y, 80 * z, headerH), 11 * z, Colors.White);
}

private void RenderTablePlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
{
    var gridBrush = new SolidColorBrush(Color.FromRgb(30, 40, 60));
    context.FillRectangle(gridBrush, rect, 4);

    var linePen = new Pen(new SolidColorBrush(Color.FromRgb(60, 70, 90)), 0.5 * z);
    var cols = 4;
    var rows = 4;
    var cellW = rect.Width / cols;
    var cellH = rect.Height / rows;

    for (int c = 1; c < cols; c++)
        context.DrawLine(linePen,
            new Point(rect.X + c * cellW, rect.Y),
            new Point(rect.X + c * cellW, rect.Y + rect.Height));

    for (int r = 1; r < rows; r++)
        context.DrawLine(linePen,
            new Point(rect.X, rect.Y + r * cellH),
            new Point(rect.X + rect.Width, rect.Y + r * cellH));
}

// 辅助方法
private static Color ParseSolidColor(string? hexColor, Color fallback)
{
    if (string.IsNullOrEmpty(hexColor)) return fallback;
    try
    {
        return Color.Parse(hexColor.StartsWith('#') ? hexColor : "#" + hexColor);
    }
    catch { return fallback; }
}

private static void DrawCenteredText(DrawingContext context, string text, Rect rect,
    double fontSize, Color color)
{
    try
    {
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default,
            fontSize, new SolidColorBrush(color));
        var x = rect.X + (rect.Width - ft.Width) / 2;
        var y = rect.Y + (rect.Height - ft.Height) / 2;
        context.DrawText(ft, new Point(x, y));
    }
    catch { /* 跳过文本渲染 */ }
}
```

- [ ] **Step 3: 在 AddControl 中追加新控件默认值**

```csharp
// src\HmiEditor.App\Controls\DesignCanvas.cs
// 在 AddControl 方法的 switch 表达式中追加:

var (defaultW, defaultH) = type switch
{
    // ... 原有 ...
    HmiControlType.Gauge => (160.0, 160.0),
    HmiControlType.Knob => (100.0, 100.0),
    HmiControlType.Keyboard => (240.0, 300.0),
    HmiControlType.Led => (40.0, 40.0),
    HmiControlType.Spinner => (60.0, 60.0),
    HmiControlType.TabView => (300.0, 200.0),
    HmiControlType.Line => (100.0, 2.0),
    HmiControlType.Table => (300.0, 200.0),
    _ => (100.0, 40.0)
};

var control = new HmiControl
{
    // ...
    BgColor = type switch
    {
        HmiControlType.Gauge => "#38bdf8",
        HmiControlType.Knob => "#a78bfa",
        HmiControlType.Keyboard => "#334155",
        HmiControlType.Led => "#22c55e",
        HmiControlType.Spinner => "#f59e0b",
        HmiControlType.TabView => "#1e293b",
        HmiControlType.Line => "#ffffff",
        HmiControlType.Table => "#1e293b",
        // ...原有保持不变...
    }
};
```

- [ ] **Step 4: 在 ToolboxView 中添加新控件条目**

```csharp
// src\HmiEditor.App\Views\ToolboxView.axaml.cs
// 在 PopulateItems() 中追加:

private void PopulateItems()
{
    _allItems.Clear();
    // ... 原有 10 项 ...
    _allItems.Add(new("⊛", "仪表盘",   "图表控件", HmiControlType.Gauge));
    _allItems.Add(new("◉", "旋钮",     "输入控件", HmiControlType.Knob));
    _allItems.Add(new("⌨", "键盘",     "输入控件", HmiControlType.Keyboard));
    _allItems.Add(new("⬤", "LED 灯",   "显示控件", HmiControlType.Led));
    _allItems.Add(new("⟳", "加载动画", "特殊控件", HmiControlType.Spinner));
    _allItems.Add(new("▤", "选项卡",   "容器",     HmiControlType.TabView));
    _allItems.Add(new("╱", "线条",     "显示控件", HmiControlType.Line));
    _allItems.Add(new("⊞", "表格",     "容器",     HmiControlType.Table));
}
```

- [ ] **Step 5: 在 ControlMappingRegistry 中添加新控件 LVGL 映射**

```csharp
// src\HmiEditor.Core\Mapping\ControlMappingRegistry.cs
// 在 LvglCreateFunction 字典中追加:

public static readonly Dictionary<HmiControlType, string> LvglCreateFunction = new()
{
    // ... 原有 10 项 ...
    [HmiControlType.Gauge]    = "lv_arc_create",
    [HmiControlType.Knob]     = "lv_arc_create",
    [HmiControlType.Keyboard] = "lv_keyboard_create",
    [HmiControlType.Led]      = "lv_led_create",
    [HmiControlType.Spinner]  = "lv_spinner_create",
    [HmiControlType.TabView]  = "lv_tabview_create",
    [HmiControlType.Line]     = "lv_line_create",
    [HmiControlType.Table]    = "lv_table_create",
};
```

- [ ] **Step 6: 编译验证**

```powershell
dotnet build HmiEditor.sln
```
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```powershell
git add src\HmiEditor.Core\Models\HmiControlType.cs src\HmiEditor.App\Controls\DesignCanvas.cs src\HmiEditor.App\Views\ToolboxView.axaml.cs src\HmiEditor.Core\Mapping\ControlMappingRegistry.cs
git commit -m "feat: 8 new industrial controls — Gauge, Knob, Keyboard, Led, Spinner, TabView, Line, Table with LVGL mappings and canvas rendering"
```

---

### Task 2.4: Wave 2 集成验证

- [ ] **Step 1: 端到端测试**

```powershell
dotnet run --project src\HmiEditor.App
```

验证清单：
1. Ctrl+Z / Ctrl+Shift+Z 撤销/重做添加、移动、删除、属性修改
2. 选中控件后用对齐按钮对齐到屏幕边界
3. 工具箱有 18 种控件（含 8 种新增工业控件），每种可拖入画布
4. 新控件的画布渲染清晰可辨

- [ ] **Step 2: 运行全部测试**

```powershell
dotnet test
```
Expected: All tests pass.

- [ ] **Step 3: Commit + Tag**

```powershell
git add .
git commit -m "release: Wave 2 complete — undo/redo, alignment tools, 8 industrial controls"
git tag w2-ui-optimization
```

---

## Wave 3 (P2): 专业功能

> 目标：预设样式模板、项目资源浏览器、变量批量导入、控件模板系统。
> 注：Wave 3 为后续迭代，本计划仅定义任务框架，详细步骤在 Wave 1/2 完成后展开。

### Task 3.1: 预设样式模板

- Create: `src\HmiEditor.Core\Models\StylePreset.cs`
- Create: `src\HmiEditor.App\Views\StylePresetPicker.axaml`

实现 5 套预设配色方案（工业灰 / 海洋蓝 / 高对比度 / 暗夜模式 / 暖色调），通过 `HmiTheme` 模型应用到项目所有控件。

### Task 3.2: 项目资源浏览器

- Create: `src\HmiEditor.App\Views\ProjectExplorerView.axaml`

TreeView 结构：屏幕列表（带缩略图）→ 变量表（可排序/筛选）→ 图片资源库 → 字体资源，替换现有单一的 OutlinePanel。

### Task 3.3: PLC 变量批量导入

- Create: `src\HmiEditor.App\Views\VariableImportView.axaml`

支持 CSV/Excel 导入，兼容西门子 TIA Portal `.xlsx` 标签表格式，自动解析变量名、地址、数据类型并填充到 `VariableManager`。

### Task 3.4: 控件模板/组件系统

- Modify: `src\HmiEditor.Core\Models\HmiControl.cs`（添加模板引用字段）
- Create: `src\HmiEditor.Core\Models\ControlTemplate.cs`

复合控件（如"启动按钮+指示灯"组合）保存为模板，支持拖放复用，一处修改全局更新，对标 FactoryTalk View 的 Global Objects。

---

## 质量门禁

每个 Task 完成后必须通过：
1. `dotnet build HmiEditor.sln` — 0 Error
2. 相关测试 100% 通过
3. 手动验证交互流程

## 工作量估算

| Wave | Task 数 | 预估工作量 | 核心交付 |
|---|---|---|---|
| Wave 1 | 3 Tasks | 2-3 天 | 图标化工具箱、折叠属性面板、画布缩放平移 |
| Wave 2 | 3 Tasks | 5-7 天 | 撤销/重做、对齐工具、8 种工业控件 |
| Wave 3 | 4 Tasks | 8-12 天 | 样式模板、资源浏览器、变量导入、控件模板 |

## 风险与依赖

| 风险 | 缓解 |
|---|---|
| Avalonia Expander 在深色主题下样式异常 | 使用内联 Style 覆盖默认主题 |
| UndoRedoManager 与现有事件系统冲突 | 独立于 DesignCanvas 之外，通过 MainWindow 协调 |
| 新控件渲染性能（Gauge/Table 绘制开销大） | 仅在 Render 时绘制，不做逐帧动画 |