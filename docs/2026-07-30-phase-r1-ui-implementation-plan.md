# Phase R1 UI 设计工程实施方案

> **状态**: PROPOSED  
> **阶段**: Development (Phase R1 启动)  
> **责任**: 用户 + `dotnet build HmiEditor.sln`  
> **日期**: 2026-07-30  
> **适用专家**: architecture-decisions  
> **前置条件**: ADR-001 (Dock + Ribbon 选型) ✅ / 附录 D (库评估) ✅

---

## 一、实施决策

### 决策问题

如何在最小化回归风险的前提下,将 `MainWindow.axaml` 从固定四栏 + emoji 工具栏重构为 Dock 可停靠布局 + CommandBar 工具栏,同时保持现有 18 个 View 和 6 个 Control 的功能不变?

### 驱动力

| 驱动力 | 理由 |
|---|---|
| 回归风险 | 现有 18 个 View 均使用 code-behind,直接全量 MVVM 重构风险极高 |
| 渐进式改造 | Dock 壳层先上线,内部 View 逐步迁移,每步可验证 |
| 依赖隔离 | Dock.Avalonia 和 FluentAvalonia 仅影响 UI 壳层,不触及 Core/CodeGen |
| 用户感知 | Phase R1 结束时用户应看到专业级界面,而非半成品 |

### 决策

采用**壳层优先 + 渐进迁移**策略:

1. **Step 0**: 添加 NuGet 依赖,验证编译
2. **Step 1**: 构建 Dock 壳层 + CommandBar 工具栏,现有 View 作为 Dockable 内容嵌入
3. **Step 2**: 工具箱拖放支持
4. **Step 3**: 属性面板类型化编辑器
5. **Step 4**: 状态栏资源仪表盘

每步独立可验证,失败可回退。

### 被拒替代

| 方案 | 拒绝原因 |
|---|---|
| 全量 MVVM 重构后上线 | 18 个 View 同时改造,回归风险不可控 |
| 仅换样式不改结构 | 无法解决功能密度和布局灵活性问题 |
| 新建项目从头写 | 丢弃现有投资,周期过长 |

---

## 二、依赖设置

### 2.1 NuGet 包新增

在 `HmiEditor.App.csproj` 的 `<ItemGroup>` 中添加:

```xml
<PackageReference Include="Dock.Avalonia" Version="12.0.0.2" />
<PackageReference Include="FluentAvalonia" Version="3.0.2" />
```

### 2.2 兼容性验证清单

| 验证项 | 方法 | 预期结果 |
|---|---|---|
| Avalonia 12.1 + Dock.Avalonia 12.0.0.2 | `dotnet build` | 编译通过,无版本冲突警告 |
| FluentAvalonia 3.0.2 主题加载 | 运行时检查 | 应用启动无异常,CommandBar 可渲染 |
| Dock 布局序列化 | 运行时测试 | `DockSerializer` 可保存/加载布局 |
| 深色主题融合 | 视觉检查 | FluentAvalonia 主题色与现有 `#1a2332` 深色体系协调 |

### 2.3 风险点

`Dock.Avalonia` v12.0.0.2 标注兼容 Avalonia 12.0,在 12.1 上可能存在 API 差异。**Step 0 必须先通过编译验证**,若失败则回退到固定布局 + TabControl 降级方案(ADR-001 备选)。

---

## 三、Step 1: Dock 壳层 + CommandBar 工具栏

### 3.1 目标布局

```
┌─────────────────────────────────────────────────────────┐
│  Menu (保留现有菜单栏)                                    │
├─────────────────────────────────────────────────────────┤
│  CommandBar: [文件] [编辑] [视图] [插入] [格式*] [工具]   │
│  (* 选中控件时显示)                                       │
├──────────┬──────────────────────────┬───────────────────┤
│ Dock:    │ Dock: 设计画布             │ Dock: 属性面板     │
│ 工具箱   │ (DesignCanvasView)        │ (PropertyPanelView)│
│ (可停靠) │                           │                   │
│          │                           │                   │
│ Dock:    ├──────────────────────────┤ Dock: 项目浏览器   │
│ 模板库   │ Dock: 输出窗口             │ (ProjectExplorer)  │
│          │                           │                   │
├──────────┴──────────────────────────┴───────────────────┤
│  状态栏: [就绪信息] [控件数] [Flash: ████░ 62%] [RAM: ██░ 41%] │
└─────────────────────────────────────────────────────────┘
```

### 3.2 CommandBar 分组设计

| 分组 | 按钮 | 对应现有控件 | 图标策略 |
|---|---|---|---|
| 文件 | 新建 / 打开 / 保存 / 导出 | MenuNewProject / MenuOpenProject / MenuSaveProject / MenuExportCode | 使用 FluentAvalonia 内置图标或 PathIcon |
| 编辑 | 撤销 / 重做 / 删除 | MenuUndo / MenuRedo / BtnDeleteControl | 同上 |
| 视图 | 放大 / 缩小 / 重置 / 屏幕管理器 | MenuZoomIn / MenuZoomOut / MenuZoomReset / MenuScreenManager | 同上 |
| 插入 | 新建屏幕 | BtnNewScreen | 同上 |
| 格式(上下文) | 左对齐 / 右对齐 / 顶对齐 / 底对齐 / 水平居中 / 垂直居中 | BtnAlignLeft ~ BtnAlignCenterV | 同上,选中控件时显示 |
| 工具 | 用户管理 / 报警 / 趋势 / 脚本 / 配方 / 变量管理 / 仿真 | BtnUserManager ~ BtnSimulator | 同上 |

### 3.3 关键文件改动

#### 3.3.1 `MainWindow.axaml` — 替换主体布局

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="using:HmiEditor.App.Views"
        xmlns:fa="using:FluentAvalonia.UI.Controls"
        xmlns:dock="using:Dock"
        xmlns:dockctrl="using:Dock.Controls"
        x:Class="HmiEditor.App.MainWindow"
        Title="HMI Editor"
        Width="1400" Height="900"
        WindowState="Maximized">

  <Grid RowDefinitions="Auto,Auto,*,Auto">

    <!-- 菜单栏(保持不变) -->
    <Menu Grid.Row="0" Background="#111827">
      <!-- ... 现有菜单项 ... -->
    </Menu>

    <!-- CommandBar 工具栏(替代扁平按钮栏) -->
    <fa:CommandBar Grid.Row="1"
                   Background="#1a2332"
                   DefaultLabelPosition="Right"
                   IsOpen="False">
      <!-- 文件组 -->
      <fa:CommandBarButton x:Name="CmdNewProject"
                           Text="新建"
                           Click="OnNewProject"/>
      <fa:CommandBarButton x:Name="CmdOpenProject"
                           Text="打开"
                           Click="OnOpenProject"/>
      <fa:CommandBarButton x:Name="CmdSaveProject"
                           Text="保存"
                           Click="OnSaveProject"/>
      <fa:CommandBarSeparator/>
      <!-- 编辑组 -->
      <fa:CommandBarButton x:Name="CmdUndo" Text="撤销" IsEnabled="False"/>
      <fa:CommandBarButton x:Name="CmdRedo" Text="重做" IsEnabled="False"/>
      <fa:CommandBarSeparator/>
      <!-- 视图组 -->
      <fa:CommandBarButton x:Name="CmdZoomIn" Text="放大"/>
      <fa:CommandBarButton x:Name="CmdZoomOut" Text="缩小"/>
      <fa:TextBlock Text="{Binding ZoomText}" VerticalAlignment="Center"/>
      <fa:CommandBarSeparator/>
      <!-- 格式组(上下文) -->
      <fa:CommandBarButton x:Name="CmdAlignLeft" Text="左对齐"
                           IsVisible="False"/>
      <fa:CommandBarButton x:Name="CmdAlignRight" Text="右对齐"
                           IsVisible="False"/>
      <!-- ... 其他对齐按钮 ... -->
      <fa:CommandBarSeparator/>
      <!-- 工具组 -->
      <fa:CommandBarButton x:Name="CmdExportCode" Text="导出"/>
      <fa:CommandBarButton x:Name="CmdUserManager" Text="用户"/>
      <fa:CommandBarButton x:Name="CmdSimulator" Text="仿真"/>
    </fa:CommandBar>

    <!-- Dock 布局区(替代固定 Grid) -->
    <dock:DockControl Grid.Row="2">
      <dockctrl:DockArea x:Name="RootDockArea">
        <!-- 布局在 code-behind 或 ViewModel 中初始化 -->
      </dockctrl:DockArea>
    </dock:DockControl>

    <!-- 状态栏(Step 4 扩展资源仪表盘) -->
    <Border Grid.Row="3" Background="#111827" Padding="8,4">
      <Grid ColumnDefinitions="*,Auto,Auto,Auto">
        <TextBlock x:Name="StatusText" Text="就绪"
                   Foreground="#9ca3af" FontSize="12"/>
        <TextBlock x:Name="ControlCountText" Text="控件: 0"
                   Grid.Column="1" Foreground="#9ca3af" FontSize="12"/>
        <!-- Step 4 在此添加资源仪表盘 -->
      </Grid>
    </Border>

  </Grid>
</Window>
```

#### 3.3.2 Dock 面板封装策略

现有 View 包装为 Dockable 的最小改动方案 —— 在 `MainWindow.axaml.cs` 中初始化:

```csharp
// 将现有 View 包装为 Dockable
private void InitializeDockLayout()
{
    // 工具箱 — 左侧停靠
    var toolboxDock = CreateDockable("工具箱", ToolboxPanel, DockOperation.DockLeft);

    // 设计画布 — 中心
    var canvasDock = CreateDockable("设计画布", CanvasPanel, DockOperation.DockCenter);

    // 属性面板 — 右侧停靠
    var propertyDock = CreateDockable("属性", PropertyPanel, DockOperation.DockRight);

    // 项目浏览器 — 底部
    var explorerDock = CreateDockable("项目资源", ProjectExplorerPanel, DockOperation.DockBottom);

    RootDockArea.Layout = new DockLayout(toolboxDock, canvasDock, propertyDock, explorerDock);
}
```

> **注意**: 上述 API 是 `Dock.Avalonia` 的概念性用法。实际 API 需在 Step 0 编译验证后根据 `Dock.Avalonia` v12.0.0.2 的真实 API 调整。核心思路是:现有 UserControl 直接作为 Dockable 内容,无需修改 View 内部代码。

#### 3.3.3 命令迁移策略

现有菜单/按钮事件通过 code-behind 绑定。CommandBar 按钮复用相同的事件处理器:

```csharp
// CommandBar 按钮复用现有事件处理器
CmdNewProject.Click += (_, _) => NewProject();
CmdOpenProject.Click += (_, _) => OpenProject();
CmdSaveProject.Click += (_, _) => SaveProject();
CmdExportCode.Click += (_, _) => ExportCode();

CmdUndo.Click += (_, _) => { _undoRedoManager.Undo(); /* ... */ };
CmdRedo.Click += (_, _) => { _undoRedoManager.Redo(); /* ... */ };

// 上下文"格式"组:选中控件时显示
CanvasPanel.Canvas.SelectionChanged += (_, controls) =>
{
    var hasSelection = controls.Count > 0;
    CmdAlignLeft.IsVisible = hasSelection;
    CmdAlignRight.IsVisible = hasSelection;
    // ... 其他对齐按钮
};
```

### 3.4 验证标准

| 验证项 | 方法 | 通过条件 |
|---|---|---|
| 编译通过 | `dotnet build HmiEditor.sln` | 0 error, 0 warning(新增) |
| 应用启动 | `dotnet run --project HmiEditor.App` | 窗口正常显示,无崩溃 |
| 菜单功能 | 逐项点击菜单 | 与重构前行为一致 |
| CommandBar 功能 | 逐项点击工具栏按钮 | 与重构前行为一致 |
| Dock 面板 | 拖拽工具箱面板到不同位置 | 面板可停靠/浮动/自动隐藏 |
| 布局保存 | 关闭后重新打开 | 面板布局恢复(若 Dock.Avalonia 支持) |

---

## 四、Step 2: 工具箱拖放支持

### 4.1 当前状态

`ToolboxView.axaml.cs` 使用 `ListBox.SelectionChanged` 事件,点击后在画布中心创建控件。无拖放。

### 4.2 改动方案

#### 4.2.1 `ToolboxView.axaml.cs` — 添加拖放源

```csharp
// 在 ToolboxView 构造函数中添加
ToolboxList.PointerPressed += OnToolboxPointerPressed;

private void OnToolboxPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (ToolboxList.SelectedItem is ToolboxItem item)
    {
        var data = new DataObject();
        data.Set("HmiControlType", item.ControlType);

        // 启动拖放,显示半透明预览
        DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }
}
```

#### 4.2.2 `DesignCanvas.cs` — 添加拖放目标

```csharp
// 在 DesignCanvas 静态构造函数中注册 DragOver 和 Drop 事件
AddHandler(DragDrop.DragOverEvent, OnDragOver);
AddHandler(DragDrop.DropEvent, OnDrop);

private void OnDragOver(object? sender, DragEventArgs e)
{
    if (e.Data.Contains("HmiControlType"))
    {
        e.DragEffects = DragDropEffects.Copy;
    }
    else
    {
        e.DragEffects = DragDropEffects.None;
    }
}

private void OnDrop(object? sender, DragEventArgs e)
{
    if (!e.Data.Contains("HmiControlType")) return;

    var controlType = (HmiControlType)e.Data.Get("HmiControlType")!;

    // 将屏幕坐标转换为画布坐标(补偿缩放和平移)
    var dropPoint = e.GetPosition(this);
    var canvasX = (dropPoint.X - _panOffsetX) / ZoomLevel;
    var canvasY = (dropPoint.Y - _panOffsetY) / ZoomLevel;

    // 在落点创建控件
    AddControl(controlType, canvasX, canvasY);

    e.DragEffects = DragDropEffects.Copy;
}
```

#### 4.2.3 保留点击备选

现有 `ToolItemSelected` 事件保持不变,用户仍可点击工具箱在画布中心创建控件。拖放为增强,不替换。

### 4.3 坐标变换校验

拖放落点精度是关键风险(FF5)。坐标补偿公式:

```
canvasX = (screenX - panOffsetX) / zoomLevel
canvasY = (screenY - panOffsetY) / zoomLevel
```

需覆盖以下场景的测试:

| 缩放 | 平移 | 预期 |
|---|---|---|
| 1.0x | (0, 0) | 落点 = 鼠标坐标 |
| 2.0x | (0, 0) | 落点 = 鼠标坐标 / 2 |
| 0.5x | (0, 0) | 落点 = 鼠标坐标 * 2 |
| 1.0x | (100, 50) | 落点 = 鼠标坐标 - (100, 50) |
| 2.0x | (100, 50) | 落点 = (鼠标坐标 - (100, 50)) / 2 |

---

## 五、Step 3: 属性面板类型化编辑器

### 5.1 当前状态

`PropertyPanelView.axaml` 所有属性用 `TextBox` 输入。颜色有 `ColorPickerPopup` 色块弹窗基础。

### 5.2 改动方案

#### 5.2.1 编辑器映射

| 属性 | 当前控件 | 目标控件 | 备注 |
|---|---|---|---|
| X / Y / Width / Height | TextBox | `NumericUpDown` | 含范围/步进,整数 |
| 名称 / 文本 | TextBox | `TextBox` | 保持不变 |
| 背景色 / 文本色 / 边框色 | TextBox + 色块 | `ColorPickerButton` | 复用 `ColorPickerPopup.cs` |
| 圆角 / 边框宽 | TextBox | `NumericUpDown` | 范围 0-100 |
| 变量绑定 | ComboBox | `AutoCompleteBox` | 支持搜索过滤 |
| 绑定模式 | ComboBox | `ComboBox` | 保持不变 |
| 动作类型 | ComboBox | `ComboBox` | 保持不变 |

#### 5.2.2 按控件类型分类显示

```csharp
// PropertyPanelView.axaml.cs 中根据选中控件类型切换可见属性组
private void UpdatePropertyGroups(HmiControl? control)
{
    if (control == null)
    {
        PositionGroup.IsVisible = false;
        ContentGroup.IsVisible = false;
        StyleGroup.IsVisible = false;
        VariableGroup.IsVisible = false;
        ActionGroup.IsVisible = false;
        return;
    }

    // 所有控件都有位置与大小
    PositionGroup.IsVisible = true;

    // 按类型显示不同属性组
    switch (control.Type)
    {
        case HmiControlType.Button:
        case HmiControlType.Label:
            ContentGroup.IsVisible = true;  // 有文本
            StyleGroup.IsVisible = true;
            ActionGroup.IsVisible = true;   // 按钮有动作
            break;

        case HmiControlType.Gauge:
        case HmiControlType.Bar:
            ContentGroup.IsVisible = false; // 无文本
            StyleGroup.IsVisible = true;
            VariableGroup.IsVisible = true; // 显示类控件绑定变量
            break;

        case HmiControlType.Led:
            ContentGroup.IsVisible = false;
            StyleGroup.IsVisible = true;
            VariableGroup.IsVisible = true;
            break;

        // ... 其他类型
    }
}
```

#### 5.2.3 NumericUpDown 替换示例

```xml
<!-- 替换前 -->
<TextBox x:Name="PropX" Text="0" TextChanged="OnPropertyChanged" Margin="0,2"/>

<!-- 替换后 -->
<numeric:NumericUpDown x:Name="PropX"
                       Value="0"
                       Minimum="0"
                       Maximum="4096"
                       Increment="1"
                       FormatString="0"
                       ValueChanged="OnPropertyChanged"
                       Margin="0,2"/>
```

> Avalonia 12.1 内置 `NumericUpDown`,无需额外 NuGet 包。

### 5.3 验证标准

| 验证项 | 方法 | 通过条件 |
|---|---|---|
| 颜色选择器 | 点击色块,选择颜色 | 控件颜色立即更新,画布重绘 |
| 数值输入范围 | 输入超范围值 | 被 NumericUpDown 拦截,不写入模型 |
| 变量搜索 | 在 AutoCompleteBox 输入部分名称 | 过滤匹配变量 |
| 控件类型切换 | 选中 Gauge 后选中 Button | 属性组正确切换 |
| 向后兼容 | 加载旧项目文件 | 属性面板正确显示现有属性 |

---

## 六、Step 4: 状态栏资源仪表盘

### 6.1 当前状态

状态栏仅显示文本状态和控件计数。

### 6.2 改动方案

#### 6.2.1 资源估算接口

```csharp
// HmiEditor.Core/Services/ResourceEstimator.cs
public class ResourceEstimator
{
    /// <summary>估算项目 Flash 占用(字节)。</summary>
    public long EstimateFlashUsage(HmiProject project)
    {
        long total = 0;
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                total += EstimateControlFlash(control);
            }
        }
        return total;
    }

    /// <summary>估算项目 RAM 占用(字节)。</summary>
    public long EstimateRamUsage(HmiProject project)
    {
        // 每个控件运行时对象 + 画面缓冲
        long total = project.Screens.Count * 1024; // 每屏基础开销
        foreach (var screen in project.Screens)
        {
            total += screen.Controls.Count * 128; // 每控件约 128B
        }
        return total;
    }

    private static long EstimateControlFlash(HmiControl control)
    {
        // 按控件类型估算 C 代码体积(字节)
        return control.Type switch
        {
            HmiControlType.Button => 120,
            HmiControlType.Label => 80,
            HmiControlType.Gauge => 350,
            HmiControlType.Bar => 200,
            HmiControlType.Image => 64 + EstimateImageSize(control),
            HmiControlType.Table => 500,
            HmiControlType.Keyboard => 800,
            _ => 100,
        };
    }

    private static long EstimateImageSize(HmiControl control)
    {
        // 粗略估算:宽 * 高 * 2 字节(RGB565)
        return (long)control.Width * (long)control.Height * 2;
    }
}
```

#### 6.2.2 状态栏 UI 扩展

```xml
<!-- MainWindow.axaml 状态栏扩展 -->
<Border Grid.Row="3" Background="#111827" Padding="8,4">
  <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto,Auto">
    <TextBlock x:Name="StatusText" Text="就绪"
               Foreground="#9ca3af" FontSize="12"/>
    <TextBlock x:Name="ControlCountText" Text="控件: 0"
               Grid.Column="1" Foreground="#9ca3af" FontSize="12"
               Margin="0,0,16,0"/>

    <!-- Flash 仪表盘 -->
    <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="6"
                VerticalAlignment="Center">
      <TextBlock Text="Flash:" Foreground="#7c8aa0" FontSize="11"/>
      <ProgressBar x:Name="FlashBar" Width="80" Height="10"
                   Minimum="0" Maximum="100" Value="0"
                   CornerRadius="2"/>
      <TextBlock x:Name="FlashText" Text="0%"
                 Foreground="#9ca3af" FontSize="11"/>
    </StackPanel>

    <!-- RAM 仪表盘 -->
    <StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="6"
                VerticalAlignment="Center" Margin="16,0,0,0">
      <TextBlock Text="RAM:" Foreground="#7c8aa0" FontSize="11"/>
      <ProgressBar x:Name="RamBar" Width="80" Height="10"
                   Minimum="0" Maximum="100" Value="0"
                   CornerRadius="2"/>
      <TextBlock x:Name="RamText" Text="0%"
                 Foreground="#9ca3af" FontSize="11"/>
    </StackPanel>

    <!-- 目标硬件 -->
    <TextBlock x:Name="TargetText" Text="目标: generic"
               Grid.Column="4" Foreground="#64748b" FontSize="11"
               Margin="16,0,0,0"/>
  </Grid>
</Border>
```

#### 6.2.3 资源仪表盘更新逻辑

```csharp
// MainWindow.axaml.cs 中
private void UpdateResourceDashboard()
{
    var estimator = new ResourceEstimator();
    var flashUsage = estimator.EstimateFlashUsage(_currentProject);
    var ramUsage = estimator.EstimateRamUsage(_currentProject);

    // 从 HardwareTarget 获取资源约束
    var constraints = HardwareLoader.GetConstraints(_currentProject.HardwareTargetId);
    var flashTotal = constraints?.FlashSize ?? 8 * 1024 * 1024;  // 默认 8MB
    var ramTotal = constraints?.RamSize ?? 64 * 1024 * 1024;    // 默认 64MB

    var flashPercent = Math.Min(100, (double)flashUsage / flashTotal * 100);
    var ramPercent = Math.Min(100, (double)ramUsage / ramTotal * 100);

    FlashBar.Value = flashPercent;
    FlashText.Text = $"{flashPercent:F1}% ({flashUsage / 1024:F0}KB / {flashTotal / 1024 / 1024:F0}MB)";
    RamBar.Value = ramPercent;
    RamText.Text = $"{ramPercent:F1}% ({ramUsage / 1024:F0}KB / {ramTotal / 1024 / 1024:F0}MB)";

    // 超限警告
    if (flashPercent > 90)
    {
        FlashBar.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // 红色
    }
    else if (flashPercent > 70)
    {
        FlashBar.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // 橙色
    }
    else
    {
        FlashBar.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // 绿色
    }
}
```

---

## 七、任务依赖排序

```
Step 0: 依赖安装 + 编译验证
  │
  ├──→ Step 1: Dock 壳层 + CommandBar (可独立验证)
  │      │
  │      ├──→ Step 2: 工具箱拖放 (依赖 Step 1 的画布面板)
  │      │
  │      ├──→ Step 3: 属性面板编辑器 (依赖 Step 1 的属性面板位置)
  │      │
  │      └──→ Step 4: 资源仪表盘 (依赖 Step 1 的状态栏位置)
  │
  └──→ Step 0 失败 → 降级方案: 固定布局 + TabControl
```

Step 2/3/4 可并行开发,但都依赖 Step 1 完成。

---

## 八、Fitness Functions(Phase R1 专项)

| # | 属性 | 指标 | 阈值/规则 | 测量源 | 评估频率 | 失败响应 | 本地检查路径 |
|---|---|---|---|---|---|---|---|
| R1-FF1 | 编译通过 | build error 数 | 0 | `dotnet build` | 每次提交 | 阻止合并 | `dotnet build HmiEditor.sln` |
| R1-FF2 | 功能等价 | 菜单/工具栏功能 | 所有重构前功能可用 | 手动测试 | Step 1 完成时 | 修复后重测 | `dotnet run` + 手动 |
| R1-FF3 | 拖放精度 | 落点偏差 | < 5px(缩放补偿后) | 手动测试 | Step 2 完成时 | 修复坐标变换 | 手动验证 |
| R1-FF4 | 属性类型安全 | 非法值拦截 | 100% 非法值被编辑器拦截 | 手动测试 | Step 3 完成时 | 修复编辑器 | 手动验证 |
| R1-FF5 | 资源估算 | 仪表盘显示 | Flash/RAM 百分比正确显示 | 手动测试 | Step 4 完成时 | 修复估算逻辑 | 手动验证 |
| R1-FF6 | 性能 | 100 控件渲染帧率 | > 30 FPS | Avalonia RenderTimer | Step 1 完成时 | 优化渲染路径 | `dotnet run` + 手动 |
| R1-FF7 | 向后兼容 | 旧项目加载 | `.hmi.json` 可正常加载 | 手动测试 | 每次提交 | 阻止合并 | 加载旧项目文件 |

---

## 九、风险登记

| # | 风险 | 可能性 | 影响 | 缓解 | 触发条件 |
|---|---|---|---|---|---|
| R1-R1 | Dock.Avalonia 12.0.0.2 在 Avalonia 12.1 上不兼容 | 中 | 高 | Step 0 先验证编译;准备降级方案(固定布局 + TabControl) | `dotnet build` 失败或运行时崩溃 |
| R1-R2 | FluentAvalonia 主题与深色工业主题冲突 | 中 | 中 | 仅使用 CommandBar 控件,覆盖其主题刷色为现有 `#1a2332` 体系 | 视觉检查发现颜色不一致 |
| R1-R3 | CommandBar 在小屏幕上拥挤 | 中 | 低 | 设置 `DefaultLabelPosition="Right"`,按钮过多时使用溢出菜单 | 1366x768 分辨率下测试 |
| R1-R4 | 拖放坐标变换在缩放/平移后不正确 | 高 | 中 | 单元测试覆盖 0.25x/1x/2x/4x 缩放 + 平移场景 | FF R1-FF3 验证 |
| R1-R5 | Dock 布局序列化与项目文件冲突 | 低 | 中 | 布局独立存储为 `.layout.json`,不侵入 `.hmi.json` | 项目文件格式验证 |
| R1-R6 | 现有事件绑定迁移到 CommandBar 遗漏功能 | 中 | 中 | 逐项对照现有按钮/菜单,建立迁移检查清单 | 功能等价测试 R1-FF2 |

---

## 十、迁移检查清单

确保 CommandBar 覆盖所有现有工具栏按钮的功能:

| 现有按钮 | 现有事件 | CommandBar 目标 | 状态 |
|---|---|---|---|
| BtnScreenManager | Click → 屏幕管理器 | CmdScreenManager | ☐ |
| BtnStyleEditor | Click → 主题编辑器 | CmdStyleEditor | ☐ |
| BtnNewScreen | Click → 新建屏幕 | CmdNewScreen | ☐ |
| BtnExportCode | Click → 导出C代码 | CmdExportCode | ☐ |
| BtnUserManager | Click → 用户管理 | CmdUserManager | ☐ |
| BtnAlarmConfig | Click → 报警配置 | CmdAlarmConfig | ☐ |
| BtnTrendConfig | Click → 趋势配置 | CmdTrendConfig | ☐ |
| BtnScriptEditor | Click → 脚本编辑器 | CmdScriptEditor | ☐ |
| BtnRecipeEditor | Click → 配方编辑器 | CmdRecipeEditor | ☐ |
| BtnVariableImport | Click → 变量导入 | CmdVariableImport | ☐ |
| BtnVariableManager | Click → 变量管理 | CmdVariableManager | ☐ |
| BtnSimulator | Click → 仿真 | CmdSimulator | ☐ |
| BtnZoomIn | Click → 放大 | CmdZoomIn | ☐ |
| BtnZoomOut | Click → 缩小 | CmdZoomOut | ☐ |
| BtnDeleteControl | Click → 删除控件 | CmdDeleteControl | ☐ |
| BtnAlignLeft | Click → 左对齐 | CmdAlignLeft | ☐ |
| BtnAlignRight | Click → 右对齐 | CmdAlignRight | ☐ |
| BtnAlignTop | Click → 顶部对齐 | CmdAlignTop | ☐ |
| BtnAlignBottom | Click → 底部对齐 | CmdAlignBottom | ☐ |
| BtnAlignCenterH | Click → 水平居中 | CmdAlignCenterH | ☐ |
| BtnAlignCenterV | Click → 垂直居中 | CmdAlignCenterV | ☐ |

---

## 十一、后续检查

1. **Step 0 完成后**: 确认 Dock.Avalonia + FluentAvalonia 在 Avalonia 12.1 上编译通过(影响全部后续步骤)
2. **Step 1 完成后**: 确认所有现有功能在 Dock 壳层中正常工作(影响 Step 2-4 启动)
3. **Phase R1 完成后**: 准备 Phase R2(Faceplate 系统)的实施计划(影响 ADR-002)
