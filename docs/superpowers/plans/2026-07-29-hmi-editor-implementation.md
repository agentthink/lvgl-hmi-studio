# HMI 编辑器实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个基于 .NET 10 + Avalonia 11.3 的 LVGL HMI 可视化编辑器，支持控件拖放设计、工业通讯协议插件、变量管理与控件绑定、报警/趋势/配方运行时功能、C 代码生成导出、跨平台运行

**Architecture:** 分层架构——UI 层（Avalonia Desktop）、引擎层（控件映射/代码生成/序列化/变量管理/报警引擎）、输出层（C 代码生成）、硬件目标插件层（IHardwareTarget 契约 + 三层加载机制）、通讯协议插件层（ICommunicationDriver 契约 + 三层加载机制）。设计画布采用 **三层 Canvas 叠加模型**（SnapGrid + DesignCanvas + AdornerCanvas）；代码生成器采用 **Scriban 模板引擎六阶段管线**（模型提取→样式分析→布局翻译→模板渲染→后处理→文件输出）；状态管理采用 **Command模式+双栈** 撤销/重做；硬件目标通过 **IHardwareTarget 插件契约** 解耦；工业协议通过 **ICommunicationDriver 插件契约** 解耦，内置 Modbus RTU/TCP + MQTT；**Phase 3** 规划报警管理系统（5种触发条件+状态机）、趋势图/数据记录（LVGL Chart+环形缓冲区）、用户访问控制（0-15级安全模型）、Lua 脚本引擎、配方管理、离线仿真

**Tech Stack:** .NET 10 SDK · Avalonia 12.0.x · CommunityToolkit.Mvvm（WeakReferenceMessenger）· SkiaSharp · Scriban 5.10（模板引擎）· NJsonSchema（项目文件验证）· xUnit · PowerShell（构建脚本）

**深化架构规格：** 参见 [hmi-architecture-deep-dive.html](../hmi-architecture-deep-dive/hmi-architecture-deep-dive.html)

---

## 项目文件结构总览

```
d:\zhishuHMI\
├── src/
│   ├── HmiEditor.App/              # Avalonia 桌面应用主体
│   │   ├── HmiEditor.App.csproj
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── MainWindow.axaml / MainWindow.axaml.cs
│   │   ├── Views/                   # 视图
│   │   │   ├── DesignCanvasView.axaml
│   │   │   ├── ToolboxView.axaml
│   │   │   ├── PropertyPanelView.axaml
│   │   │   └── OutlinePanelView.axaml
│   │   └── Controls/                # 自定义控件
│   │       ├── DesignCanvas.cs
│   │       ├── AdornerCanvas.cs
│   │       └── SnapGrid.cs
│   ├── HmiEditor.Core/              # 引擎核心（.NET 10）
│   │   ├── HmiEditor.Core.csproj
│   │   ├── Models/                  # 数据模型
│   │   │   ├── HmiProject.cs
│   │   │   ├── HmiScreen.cs
│   │   │   └── HmiControl.cs
│   │   ├── Mapping/                 # Avalonia ↔ LVGL 控件映射
│   │   │   ├── ControlMappingRegistry.cs
│   │   │   └── PropertyMapping.cs
│   │   └── Serialization/           # 项目序列化
│   │       └── ProjectSerializer.cs
│   ├── HmiEditor.CodeGen/           # C 代码生成引擎
│   │   ├── HmiEditor.CodeGen.csproj
│   │   ├── Templates/               # Scriban 模板
│   │   │   ├── lvgl_v9_main.scriban
│   │   │   ├── lvgl_v9_screen.scriban
│   │   │   └── lvgl_v9_style.scriban
│   │   ├── CodeGenerator.cs
│   │   └── LvglApiWriter.cs
│   ├── HmiEditor.Renderer/          # 预览/截图渲染
│   │   ├── HmiEditor.Renderer.csproj
│   │   ├── DesignSurfaceRenderer.cs
│   │   └── BitmapExporter.cs
│   ├── Hmi.Hardware.Contracts/      # 硬件目标插件契约（零依赖）
│   │   ├── Hmi.Hardware.Contracts.csproj
│   │   ├── IHardwareTarget.cs        # 核心插件接口
│   │   ├── HardwareTargetMetadata.cs # record 数据模型
│   │   ├── DisplayConfig.cs
│   │   ├── ResourceConstraints.cs
│   │   ├── LvglConfig.cs
│   │   ├── ToolchainConfig.cs
│   │   └── ValidationResult.cs
│   ├── Hmi.Hardware.Loader/         # 插件加载器
│   │   ├── Hmi.Hardware.Loader.csproj
│   │   ├── HardwareTargetRegistry.cs # 注册中心
│   │   ├── PluginLoadContext.cs      # 可卸载加载上下文
│   │   └── ConfigDrivenTarget.cs     # JSON 配置适配器
│   └── Hmi.Hardware.Builtin/        # 内置芯片插件（T113/RK3506）
│       ├── Hmi.Hardware.Builtin.csproj
│       ├── AllwinnerT113Target.cs
│       └── RockchipRK3506Target.cs
├── tests/
│   ├── HmiEditor.Core.Tests/
│   ├── HmiEditor.CodeGen.Tests/
│   ├── HmiEditor.App.Tests/
│   └── Hmi.Hardware.Tests/          # 硬件插件测试
├── build/                           # 构建与清理脚本
│   ├── build.ps1
│   ├── clean.ps1
│   └── publish-linux-arm64.ps1
├── docs/
│   ├── superpowers/plans/
│   └── adr/                         # 架构决策记录
└── HmiEditor.sln
```

---

## Phase 0: 项目脚手架与原型验证（第 1-3 周）

> 目标：验证三条核心链路的技术可行性后，再进入完整开发。
> 所有 Phase 0 任务完成后召开 checkpoint 评审，决定是否进入 Phase 1。

### Task 0.1: 创建 .NET 解决方案与项目骨架

**Files:**
- Create: `d:\zhishuHMI\HmiEditor.sln`
- Create: `d:\zhishuHMI\src\HmiEditor.App\HmiEditor.App.csproj`
- Create: `d:\zhishuHMI\src\HmiEditor.Core\HmiEditor.Core.csproj`
- Create: `d:\zhishuHMI\src\HmiEditor.CodeGen\HmiEditor.CodeGen.csproj`
- Create: `d:\zhishuHMI\src\HmiEditor.Renderer\HmiEditor.Renderer.csproj`
- Create: `d:\zhishuHMI\src\Directory.Build.props`

- [ ] **Step 1: 安装 .NET 10 SDK 并验证版本**

Run: `dotnet --version`
Expected: `10.0.x` 输出

- [ ] **Step 2: 创建解决方案**

```powershell
cd d:\zhishuHMI
dotnet new sln -n HmiEditor
```

- [ ] **Step 3: 创建 Directory.Build.props（统一版本管理）**

```xml
<!-- d:\zhishuHMI\src\Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AvaloniaVersion>12.0.5</AvaloniaVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: 创建 Core 项目（.NET Standard 2.1，保证 Avalonia 兼容）**

```powershell
dotnet new classlib -n HmiEditor.Core -o src\HmiEditor.Core -f net10.0
```

修改 `HmiEditor.Core.csproj`，添加：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>HmiEditor.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: 创建 CodeGen 项目**

```powershell
dotnet new classlib -n HmiEditor.CodeGen -o src\HmiEditor.CodeGen -f net10.0
dotnet add src\HmiEditor.CodeGen\HmiEditor.CodeGen.csproj reference src\HmiEditor.Core\HmiEditor.Core.csproj
dotnet add src\HmiEditor.CodeGen package Scriban --version 5.10.0
```

- [ ] **Step 6: 创建 Renderer 项目**

```powershell
dotnet new classlib -n HmiEditor.Renderer -o src\HmiEditor.Renderer -f net10.0
dotnet add src\HmiEditor.Renderer\HmiEditor.Renderer.csproj reference src\HmiEditor.Core\HmiEditor.Core.csproj
```

- [ ] **Step 7: 创建 Avalonia 桌面应用项目**

```powershell
dotnet new avalonia.app -n HmiEditor.App -o src\HmiEditor.App
```

修改 `HmiEditor.App.csproj`，对齐版本号：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>HmiEditor.App</RootNamespace>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.0.5" />
    <PackageReference Include="Avalonia.Desktop" Version="12.0.5" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.5" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\HmiEditor.Core\HmiEditor.Core.csproj" />
    <ProjectReference Include="..\HmiEditor.CodeGen\HmiEditor.CodeGen.csproj" />
    <ProjectReference Include="..\HmiEditor.Renderer\HmiEditor.Renderer.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 8: 将所有项目加入解决方案**

```powershell
dotnet sln HmiEditor.sln add src\HmiEditor.App\HmiEditor.App.csproj
dotnet sln HmiEditor.sln add src\HmiEditor.Core\HmiEditor.Core.csproj
dotnet sln HmiEditor.sln add src\HmiEditor.CodeGen\HmiEditor.CodeGen.csproj
dotnet sln HmiEditor.sln add src\HmiEditor.Renderer\HmiEditor.Renderer.csproj
```

- [ ] **Step 9: 编译验证**

```powershell
dotnet build HmiEditor.sln
```
Expected: Build succeeded. 0 Error(s), 0 Warning(s).

- [ ] **Step 10: 运行空白窗口**

```powershell
dotnet run --project src\HmiEditor.App
```
Expected: 空白 Avalonia 窗口正常弹出，标题栏显示 "HmiEditor"。

- [ ] **Step 11: Commit**

```powershell
git add .
git commit -m "feat: scaffold .NET 10 + Avalonia 12 solution with Core/CodeGen/Renderer/App projects"
```

---

### Task 0.2: Canvas 拖放原型验证

**Files:**
- Create: `src\HmiEditor.App\Views\PrototypeCanvas.axaml`
- Create: `src\HmiEditor.App\Views\PrototypeCanvas.axaml.cs`

**目的**：验证 Avalonia Canvas 能否实现基本的控件拖放交互。不做架构设计，只验证可行性。

- [ ] **Step 1: 编写 PrototypeCanvas.axaml**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="HmiEditor.App.Views.PrototypeCanvas">
  <Grid>
    <!-- 工具箱：左侧固定 -->
    <Border Width="200" Background="#1e2d4a" HorizontalAlignment="Left">
      <StackPanel Margin="10" Spacing="8">
        <TextBlock Text="工具箱" Foreground="White" FontSize="18" FontWeight="Bold"/>
        <Button Content="Button" Padding="10,6" Background="#38bdf8"
                PointerPressed="OnToolboxItemPressed"/>
        <Button Content="Label" Padding="10,6" Background="#a78bfa"
                PointerPressed="OnToolboxItemPressed"/>
      </StackPanel>
    </Border>

    <!-- 设计画布：占据剩余空间 -->
    <Canvas x:Name="DesignSurface" Background="#131c31" Margin="200,0,0,0"
            PointerPressed="OnCanvasPointerPressed"
            PointerMoved="OnCanvasPointerMoved"
            PointerReleased="OnCanvasPointerReleased">
      <TextBlock Text="拖放控件到此处开始设计" Foreground="#7c8aa0"
                 FontSize="14" Canvas.Left="20" Canvas.Top="20"/>
    </Canvas>
  </Grid>
</UserControl>
```

- [ ] **Step 2: 编写 PrototypeCanvas.axaml.cs**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace HmiEditor.App.Views;

public partial class PrototypeCanvas : UserControl
{
    private string _pendingToolType = "";
    private Control? _draggingControl;
    private Point _dragOffset;

    public PrototypeCanvas()
    {
        InitializeComponent();
    }

    private void OnToolboxItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Button btn)
        {
            _pendingToolType = (btn.Content?.ToString() ?? "") switch
            {
                "Button" => "lv_btn",
                "Label" => "lv_label",
                _ => ""
            };

            var ctrl = _pendingToolType switch
            {
                "lv_btn" => new Border
                {
                    Width = 120, Height = 44,
                    Background = Avalonia.Media.Brushes.DodgerBlue,
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock
                    {
                        Text = "Button",
                        Foreground = Avalonia.Media.Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    }
                },
                "lv_label" => new TextBlock
                {
                    Text = "Label",
                    Foreground = Avalonia.Media.Brushes.White,
                    FontSize = 16
                },
                _ => null
            };

            if (ctrl != null)
            {
                Canvas.SetLeft(ctrl, 380);
                Canvas.SetTop(ctrl, 80);
                DesignSurface.Children.Add(ctrl);

                ctrl.PointerPressed += OnControlPointerPressed;
                ctrl.PointerMoved += OnControlPointerMoved;
                ctrl.PointerReleased += OnControlPointerReleased;
            }

            _pendingToolType = "";
        }
    }

    private void OnControlPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control ctrl)
        {
            _draggingControl = ctrl;
            _dragOffset = e.GetPosition(ctrl);
            ctrl.Opacity = 0.7;
            e.Handled = true;
        }
    }

    private void OnControlPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingControl == null) return;

        var pos = e.GetPosition(DesignSurface);
        Canvas.SetLeft(_draggingControl, pos.X - _dragOffset.X);
        Canvas.SetTop(_draggingControl, pos.Y - _dragOffset.Y);
        e.Handled = true;
    }

    private void OnControlPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingControl != null)
        {
            _draggingControl.Opacity = 1.0;
            _draggingControl = null;
        }
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 画布空白区域点击可取消选择（后续实现）
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e) { }
    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e) { }
}
```

- [ ] **Step 3: 修改 MainWindow 加载原型页面**

修改 `MainWindow.axaml` 和 `MainWindow.axaml.cs`：将 Content 替换为 `<views:PrototypeCanvas />`。

- [ ] **Step 4: 运行验证**

```powershell
dotnet run --project src\HmiEditor.App
```
Expected: 左侧显示工具箱（Button、Label），点击工具箱按钮后在画布上出现对应控件，可拖拽移动。

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "feat: Canvas drag-drop prototype — toolbox click creates controls, drag to reposition"
```

---

### Task 0.3: RenderTargetBitmap 截图验证

**Files:**
- Create: `src\HmiEditor.Renderer\BitmapExporter.cs`
- Modify: `src\HmiEditor.App\Views\PrototypeCanvas.axaml`（添加导出按钮）

**目的**：验证 Avalonia RenderTargetBitmap 能否正确截图，以及已知的文本模糊 bug 的修复方案是否有效。

- [ ] **Step 1: 编写 BitmapExporter.cs**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace HmiEditor.Renderer;

public static class BitmapExporter
{
    /// <summary>
    /// 将指定 Visual 渲染为 PNG 并保存到文件。
    /// 注意：需要临时设置不透明背景以规避 Avalonia RenderTargetBitmap 文本模糊问题。
    /// </summary>
    public static async Task ExportToFileAsync(
        Visual visual,
        Size renderSize,
        string outputPath,
        IBrush? backgroundColor = null)
    {
        var originalBg = (visual as Control)?.Background;
        var bgBrush = backgroundColor ?? Brushes.White;

        // 修复：强制设置不透明背景，规避文本模糊 issue
        if (visual is Control ctrl)
        {
            ctrl.Background = bgBrush;
        }

        var pixelSize = new PixelSize((int)renderSize.Width, (int)renderSize.Height);
        var dpi = new Vector(96, 96);
        var bitmap = new RenderTargetBitmap(pixelSize, dpi);

        await Task.Delay(50); // 等待渲染管线就绪
        bitmap.Render(visual);

        using var stream = File.OpenWrite(outputPath);
        bitmap.Save(stream, 100);

        // 恢复原始背景
        if (visual is Control ctrlRestore)
        {
            ctrlRestore.Background = originalBg;
        }
    }
}
```

- [ ] **Step 2: 编写单元测试验证截图产出**

```csharp
// tests\HmiEditor.Renderer.Tests\BitmapExporterTests.cs（先手动创建测试项目）
// 简化版测试：创建测试 TextBlock，截图保存后验证文件存在且非空

using Xunit;
using Avalonia.Controls;
using Avalonia.Media;

namespace HmiEditor.Renderer.Tests;

public class BitmapExporterTests
{
    [StaFact]
    public async Task ExportToFile_CreatesNonEmptyPng()
    {
        var textBlock = new TextBlock
        {
            Text = "LVGL HMI Test",
            Foreground = Brushes.Black,
            FontSize = 20,
            Width = 200,
            Height = 60
        };
        textBlock.Measure(new Avalonia.Size(200, 60));
        textBlock.Arrange(new Avalonia.Rect(0, 0, 200, 60));

        var outputPath = Path.GetTempFileName() + ".png";
        await BitmapExporter.ExportToFileAsync(
            textBlock,
            new Avalonia.Size(200, 60),
            outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 100);

        // 清理
        File.Delete(outputPath);
    }
}
```

- [ ] **Step 3: 编译并运行测试**

```powershell
dotnet test tests\HmiEditor.Renderer.Tests\HmiEditor.Renderer.Tests.csproj
```
Expected: 测试通过。

- [ ] **Step 4: Commit**

```powershell
git add .
git commit -m "feat: BitmapExporter with RenderTargetBitmap white-background workaround + test"
```

---

### Task 0.4: ARM64 NativeAOT 编译验证

**Files:**
- Create: `build\publish-linux-arm64.ps1`

**目的**：验证 .NET 10 + Avalonia 12 能否成功 AOT 编译为 linux-arm64 目标。

- [ ] **Step 1: 编写 publish-linux-arm64.ps1**

```powershell
# build\publish-linux-arm64.ps1
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish\linux-arm64"
)

$ErrorActionPreference = "Stop"

Write-Host "[*] Publishing HmiEditor.App for linux-arm64 (NativeAOT)..." -ForegroundColor Cyan

$projectPath = Join-Path $PSScriptRoot "..\src\HmiEditor.App\HmiEditor.App.csproj"
$outputPath = Join-Path $PSScriptRoot "..\$OutputDir"

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime linux-arm64 `
    --self-contained true `
    -p:PublishAot=true `
    -p:StripSymbols=true `
    --output $outputPath

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    $exePath = Join-Path $outputPath "HmiEditor.App"
    if (Test-Path $exePath) {
        $sizeInMB = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
        Write-Host "[OK] Publish succeeded. Binary: $exePath ($sizeInMB MB)" -ForegroundColor Green
    } else {
        Write-Host "[OK] Publish succeeded. Check output: $outputPath" -ForegroundColor Green
    }
    Get-ChildItem $outputPath | Select-Object Name, @{N='Size(MB)';E={[math]::Round($_.Length/1MB,2)}}
} else {
    Write-Host "[FAIL] Publish failed with exit code $exitCode" -ForegroundColor Red
    exit $exitCode
}
```

- [ ] **Step 2: 创建 AOT 兼容性配置文件**

```xml
<!-- src\HmiEditor.App\rd.xml -->
<Directives>
  <Application>
    <!-- 保留 Avalonia 核心类型（AOT 裁剪保护） -->
    <Assembly Name="Avalonia.Base" Dynamic="Required All" />
    <Assembly Name="Avalonia.Controls" Dynamic="Required All" />
    <Assembly Name="Avalonia.Desktop" Dynamic="Required All" />
    <!-- 保留 HmiEditor 自身类型 -->
    <Assembly Name="HmiEditor.App" Dynamic="Required All" />
    <Assembly Name="HmiEditor.Core" Dynamic="Required All" />
    <Assembly Name="HmiEditor.CodeGen" Dynamic="Required All" />
  </Application>
</Directives>
```

并在 `.csproj` 中添加：
```xml
<ItemGroup>
  <RdXmlFile Include="rd.xml" />
</ItemGroup>
```

- [ ] **Step 3: Windows 交叉编译 ARM64 AOT**

```powershell
.\build\publish-linux-arm64.ps1
```
Expected: `[OK] Publish succeeded. Binary size ~25-50 MB`

- [ ] **Step 4: 记录已知问题（如果编译失败）**

如果 NativeAOT 编译失败，记录具体错误信息到：
`docs\adr\0001-nativeaot-arm64-compatibility.md`

常见可能的失败原因：
- SkiaSharp 原生库不兼容 linux-arm64 AOT → 切换 `linux-musl-arm64` 或采用 `linux-arm64` + `PublishSingleFile` 非 AOT 作为备选
- 某个第三方包的反射使用与 AOT 裁剪冲突 → 在 `rd.xml` 中补充保留声明

- [ ] **Step 5: Commit**

```powershell
git add .
git commit -m "feat: ARM64 NativeAOT publish script + rd.xml AOT trimming guard"
```

---

### Task 0.5: Phase 0 Checkpoint — 技术验证评审

- [ ] **Step 1: 填写技术验证清单**

| 验证项 | 通过标准 | 实际结果 | 状态 |
|--------|---------|---------|------|
| 项目骨架可编译运行 | `dotnet build` 0 Error | | |
| Canvas 拖放交互 | 工具箱点击→画布出现控件→可拖拽移动 | | |
| RenderTargetBitmap 截图 | 生成 ≥100 字节的非模糊 PNG | | |
| ARM64 AOT 编译 | 产出 linux-arm64 可执行文件 | | |
| 截图修复方案有效 | 白底截图文本不模糊 | | |

- [ ] **Step 2: 做出 Go/No-Go 决策**

三条核心链路全部通过 → **Go：进入 Phase 1**
任一条未通过 → 评估备选方案或技术栈调整

- [ ] **Step 3: 更新 ADR 状态**

将 `ADR-001 (.NET 10 + Avalonia 12 选型)` 状态从 `Proposed` 更新为 `Accepted` 或 `Rejected`。

- [ ] **Step 4: Commit**

```powershell
git add docs/
git commit -m "docs: Phase 0 checkpoint — prototype verification results"
```

---

### Task 0.6: 硬件目标插件契约与内置实现

**Files:**
- Create: `src\Hmi.Hardware.Contracts\Hmi.Hardware.Contracts.csproj`
- Create: `src\Hmi.Hardware.Contracts\IHardwareTarget.cs`
- Create: `src\Hmi.Hardware.Contracts\HardwareTargetMetadata.cs`
- Create: `src\Hmi.Hardware.Contracts\DisplayConfig.cs`
- Create: `src\Hmi.Hardware.Contracts\ResourceConstraints.cs`
- Create: `src\Hmi.Hardware.Contracts\LvglConfig.cs`
- Create: `src\Hmi.Hardware.Contracts\ToolchainConfig.cs`
- Create: `src\Hmi.Hardware.Contracts\ValidationResult.cs`
- Create: `src\Hmi.Hardware.Loader\Hmi.Hardware.Loader.csproj`
- Create: `src\Hmi.Hardware.Loader\HardwareTargetRegistry.cs`
- Create: `src\Hmi.Hardware.Loader\PluginLoadContext.cs`
- Create: `src\Hmi.Hardware.Builtin\Hmi.Hardware.Builtin.csproj`
- Create: `src\Hmi.Hardware.Builtin\AllwinnerT113Target.cs`
- Create: `src\Hmi.Hardware.Builtin\RockchipRK3506Target.cs`
- Create: `tests\Hmi.Hardware.Tests\HardwareTargetTests.cs`

**目的**：创建硬件目标插件系统的契约接口、加载器和两个内置芯片实现。Phase 0 只完成 Tier 1（内置插件），Tier 2/3 在 Phase 2 实现。

- [ ] **Step 1: 创建 Hmi.Hardware.Contracts 项目（零依赖类库）**

```powershell
dotnet new classlib -n Hmi.Hardware.Contracts -o src\Hmi.Hardware.Contracts -f net10.0
dotnet add src\Hmi.Hardware.Contracts reference src\HmiEditor.Core\HmiEditor.Core.csproj
```

修改 `Hmi.Hardware.Contracts.csproj`：
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Hmi.Hardware.Contracts</RootNamespace>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <!-- 引用 Core 获取 HmiProject/HmiScreen 模型；无 NuGet 依赖 -->
  <ItemGroup>
    <ProjectReference Include="..\HmiEditor.Core\HmiEditor.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 编写 IHardwareTarget 接口**

```csharp
// src\Hmi.Hardware.Contracts\IHardwareTarget.cs
namespace Hmi.Hardware.Contracts;

/// <summary>
/// 硬件目标插件契约 —— 所有芯片插件必须实现此接口。
/// 编辑器通过此接口与硬件目标交互，不直接依赖具体实现。
/// </summary>
public interface IHardwareTarget
{
    HardwareTargetMetadata Metadata { get; }
    DisplayConfig Display { get; }
    ResourceConstraints Resources { get; }
    LvglConfig LvglConfig { get; }
    ToolchainConfig Toolchain { get; }

    ValidationResult ValidateProject(HmiProject project);
    IReadOnlyDictionary<string, object> GetTemplateParameters();
    string GenerateInitializationCode(HmiProject project);
}
```

- [ ] **Step 3: 编写 record 数据模型**

```csharp
// src\Hmi.Hardware.Contracts\HardwareTargetMetadata.cs
namespace Hmi.Hardware.Contracts;

public record HardwareTargetMetadata(
    string Id,
    string Name,
    string Vendor,
    string Architecture,
    string Version
);

// src\Hmi.Hardware.Contracts\DisplayConfig.cs
namespace Hmi.Hardware.Contracts;

public enum ColorDepth { RGB565, ARGB8888, Indexed8 }
public enum DisplayInterface { RGB666, RGB888, MIPI_DSI, SPI, I80 }

public record DisplayConfig(
    int DefaultWidth,
    int DefaultHeight,
    IReadOnlyList<ColorDepth> SupportedColorDepths,
    DisplayInterface Interface,
    int MaxRefreshRate
);

// src\Hmi.Hardware.Contracts\ResourceConstraints.cs
namespace Hmi.Hardware.Contracts;

public record ResourceConstraints(
    long FlashSizeBytes,
    long RamSizeBytes,
    long RecommendedLvglHeapBytes,
    int MaxTextureSize
);

// src\Hmi.Hardware.Contracts\LvglConfig.cs
namespace Hmi.Hardware.Contracts;

public record LvglConfig(
    int DefaultColorDepth,
    int DefaultMemorySize,
    bool UseDoubleBuffering,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyDictionary<string, string> CustomDefines
);

// src\Hmi.Hardware.Contracts\ToolchainConfig.cs
namespace Hmi.Hardware.Contracts;

public record ToolchainConfig(
    string CrossCompilerPrefix,
    string CompilerPath,
    IReadOnlyList<string> CompileFlags,
    string LinkerScriptPath,
    IReadOnlyList<string> LibraryDependencies
);

// src\Hmi.Hardware.Contracts\ValidationResult.cs
namespace Hmi.Hardware.Contracts;

public enum ValidationSeverity { Error, Warning }

public record ValidationIssue(ValidationSeverity Severity, string Code, string Message);
public record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
```

- [ ] **Step 4: 创建 Hmi.Hardware.Loader 项目**

```powershell
dotnet new classlib -n Hmi.Hardware.Loader -o src\Hmi.Hardware.Loader -f net10.0
dotnet add src\Hmi.Hardware.Loader reference src\Hmi.Hardware.Contracts\Hmi.Hardware.Contracts.csproj
```

- [ ] **Step 5: 编写 HardwareTargetRegistry**

```csharp
// src\Hmi.Hardware.Loader\HardwareTargetRegistry.cs
using Hmi.Hardware.Contracts;
using Microsoft.Extensions.Logging;

namespace Hmi.Hardware.Loader;

public class HardwareTargetRegistry
{
    private readonly Dictionary<string, IHardwareTarget> _targets = new();
    private readonly ILogger<HardwareTargetRegistry>? _logger;

    public HardwareTargetRegistry(ILogger<HardwareTargetRegistry>? logger = null)
    {
        _logger = logger;
    }

    public void LoadBuiltinTargets(IEnumerable<IHardwareTarget> builtinTargets)
    {
        foreach (var target in builtinTargets)
        {
            Register(target);
        }
        _logger?.LogInformation("已加载 {Count} 个内置硬件目标", _targets.Count);
    }

    public void Register(IHardwareTarget target)
    {
        _targets[target.Metadata.Id] = target;
    }

    public IHardwareTarget? GetTarget(string id) =>
        _targets.GetValueOrDefault(id);

    public IEnumerable<IHardwareTarget> GetAllTargets() => _targets.Values;
}
```

- [ ] **Step 6: 编写 PluginLoadContext（预留，Phase 2 启用）**

```csharp
// src\Hmi.Hardware.Loader\PluginLoadContext.cs
using System.Reflection;
using System.Runtime.Loader;

namespace Hmi.Hardware.Loader;

/// <summary>
/// 可卸载的插件加载上下文 —— 支持 DLL 插件的热更新。
/// Phase 2 启用，Phase 0 仅占位。
/// </summary>
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 契约程序集共享主上下文版本，确保接口类型一致
        if (assemblyName.Name == "Hmi.Hardware.Contracts")
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}
```

- [ ] **Step 7: 创建 Hmi.Hardware.Builtin 项目**

```powershell
dotnet new classlib -n Hmi.Hardware.Builtin -o src\Hmi.Hardware.Builtin -f net10.0
dotnet add src\Hmi.Hardware.Builtin reference src\Hmi.Hardware.Contracts\Hmi.Hardware.Contracts.csproj
```

- [ ] **Step 8: 编写 AllwinnerT113Target 实现**

```csharp
// src\Hmi.Hardware.Builtin\AllwinnerT113Target.cs
using Hmi.Hardware.Contracts;

namespace Hmi.Hardware.Builtin;

public sealed class AllwinnerT113Target : IHardwareTarget
{
    public HardwareTargetMetadata Metadata => new(
        Id: "allwinner-t113",
        Name: "Allwinner T113-S3",
        Vendor: "Allwinner",
        Architecture: "ARM Cortex-A7",
        Version: "1.0.0"
    );

    public DisplayConfig Display => new(
        DefaultWidth: 800,
        DefaultHeight: 480,
        SupportedColorDepths: new[] { ColorDepth.RGB565, ColorDepth.ARGB8888 },
        Interface: DisplayInterface.RGB666,
        MaxRefreshRate: 60
    );

    public ResourceConstraints Resources => new(
        FlashSizeBytes: 16 * 1024 * 1024,   // 16MB SPI Flash
        RamSizeBytes: 128 * 1024 * 1024,     // 128MB DDR3
        RecommendedLvglHeapBytes: 4 * 1024 * 1024, // 4MB
        MaxTextureSize: 1024
    );

    public LvglConfig LvglConfig => new(
        DefaultColorDepth: 32,
        DefaultMemorySize: 128,
        UseDoubleBuffering: true,
        EnabledFeatures: new[] { "LV_USE_FREETYPE", "LV_USE_PNG" },
        CustomDefines: new Dictionary<string, string>
        {
            ["LV_COLOR_DEPTH"] = "32",
            ["LV_MEM_SIZE"] = "0x400000"
        }
    );

    public ToolchainConfig Toolchain => new(
        CrossCompilerPrefix: "arm-linux-gnueabihf-",
        CompilerPath: "",
        CompileFlags: new[] { "-mcpu=cortex-a7", "-mfpu=neon-vfpv4", "-mfloat-abi=hard" },
        LinkerScriptPath: "t113-s3.ld",
        LibraryDependencies: new[] { "lvgl", "m", "dl" }
    );

    public ValidationResult ValidateProject(HmiProject project)
    {
        var issues = new List<ValidationIssue>();

        foreach (var screen in project.Screens)
        {
            if (screen.Width > Display.DefaultWidth || screen.Height > Display.DefaultHeight)
            {
                issues.Add(new(ValidationSeverity.Warning,
                    "RESOLUTION_EXCEEDS_DEFAULT",
                    $"Screen '{screen.Name}' ({screen.Width}x{screen.Height}) 超过默认分辨率"));
            }
        }

        return new(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public IReadOnlyDictionary<string, object> GetTemplateParameters() => new Dictionary<string, object>
    {
        ["target_id"] = Metadata.Id,
        ["color_depth"] = LvglConfig.DefaultColorDepth,
        ["lvgl_heap_size"] = Resources.RecommendedLvglHeapBytes,
        ["cross_compiler_prefix"] = Toolchain.CrossCompilerPrefix
    };

    public string GenerateInitializationCode(HmiProject project) =>
        "// T113-S3 display init\n" +
        "sunxi_display_init();\n" +
        "lv_init();\n" +
        "lv_display_create(800, 480);\n";
}
```

- [ ] **Step 9: 编写 RockchipRK3506Target 实现**

```csharp
// src\Hmi.Hardware.Builtin\RockchipRK3506Target.cs
using Hmi.Hardware.Contracts;

namespace Hmi.Hardware.Builtin;

public sealed class RockchipRK3506Target : IHardwareTarget
{
    public HardwareTargetMetadata Metadata => new(
        Id: "rockchip-rk3506",
        Name: "Rockchip RK3506",
        Vendor: "Rockchip",
        Architecture: "ARM Cortex-A7",
        Version: "1.0.0"
    );

    public DisplayConfig Display => new(
        DefaultWidth: 480,
        DefaultHeight: 272,
        SupportedColorDepths: new[] { ColorDepth.RGB565 },
        Interface: DisplayInterface.RGB666,
        MaxRefreshRate: 60
    );

    public ResourceConstraints Resources => new(
        FlashSizeBytes: 8 * 1024 * 1024,    // 8MB SPI Flash
        RamSizeBytes: 64 * 1024 * 1024,      // 64MB DDR2
        RecommendedLvglHeapBytes: 2 * 1024 * 1024, // 2MB
        MaxTextureSize: 512
    );

    public LvglConfig LvglConfig => new(
        DefaultColorDepth: 16,
        DefaultMemorySize: 64,
        UseDoubleBuffering: false,
        EnabledFeatures: new[] { "LV_USE_PNG" },
        CustomDefines: new Dictionary<string, string>
        {
            ["LV_COLOR_DEPTH"] = "16",
            ["LV_MEM_SIZE"] = "0x200000"
        }
    );

    public ToolchainConfig Toolchain => new(
        CrossCompilerPrefix: "arm-linux-gnueabihf-",
        CompilerPath: "",
        CompileFlags: new[] { "-mcpu=cortex-a7", "-mfpu=neon-vfpv4", "-mfloat-abi=hard" },
        LinkerScriptPath: "rk3506.lds",
        LibraryDependencies: new[] { "lvgl", "m" }
    );

    public ValidationResult ValidateProject(HmiProject project)
    {
        var issues = new List<ValidationIssue>();

        foreach (var screen in project.Screens)
        {
            if (screen.Width > Display.DefaultWidth || screen.Height > Display.DefaultHeight)
            {
                issues.Add(new(ValidationSeverity.Warning,
                    "RESOLUTION_EXCEEDS_DEFAULT",
                    $"Screen '{screen.Name}' 分辨率超过 RK3506 默认值"));
            }
        }

        return new(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public IReadOnlyDictionary<string, object> GetTemplateParameters() => new Dictionary<string, object>
    {
        ["target_id"] = Metadata.Id,
        ["color_depth"] = LvglConfig.DefaultColorDepth,
        ["lvgl_heap_size"] = Resources.RecommendedLvglHeapBytes,
        ["cross_compiler_prefix"] = Toolchain.CrossCompilerPrefix
    };

    public string GenerateInitializationCode(HmiProject project) =>
        "// RK3506 display init\n" +
        "rk_display_init();\n" +
        "lv_init();\n" +
        "lv_display_create(480, 272);\n";
}
```

- [ ] **Step 10: 将新项目加入解决方案并配置引用**

```powershell
dotnet sln HmiEditor.sln add src\Hmi.Hardware.Contracts\Hmi.Hardware.Contracts.csproj
dotnet sln HmiEditor.sln add src\Hmi.Hardware.Loader\Hmi.Hardware.Loader.csproj
dotnet sln HmiEditor.sln add src\Hmi.Hardware.Builtin\Hmi.Hardware.Builtin.csproj

# App 项目引用 Loader 和 Builtin
dotnet add src\HmiEditor.App reference src\Hmi.Hardware.Loader\Hmi.Hardware.Loader.csproj
dotnet add src\HmiEditor.App reference src\Hmi.Hardware.Builtin\Hmi.Hardware.Builtin.csproj

# CodeGen 引用 Contracts（用于获取模板参数）
dotnet add src\HmiEditor.CodeGen reference src\Hmi.Hardware.Contracts\Hmi.Hardware.Contracts.csproj
```

- [ ] **Step 11: 编写单元测试**

```csharp
// tests\Hmi.Hardware.Tests\HardwareTargetTests.cs
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using Hmi.Hardware.Loader;
using Xunit;

namespace Hmi.Hardware.Tests;

public class HardwareTargetTests
{
    [Fact]
    public void Registry_LoadBuiltin_ReturnsTwoTargets()
    {
        var registry = new HardwareTargetRegistry();
        registry.LoadBuiltinTargets(new IHardwareTarget[]
        {
            new AllwinnerT113Target(),
            new RockchipRK3506Target()
        });

        Assert.Equal(2, registry.GetAllTargets().Count());
    }

    [Fact]
    public void Registry_GetTarget_ReturnsCorrectTarget()
    {
        var registry = new HardwareTargetRegistry();
        registry.LoadBuiltinTargets(new IHardwareTarget[]
        {
            new AllwinnerT113Target(),
            new RockchipRK3506Target()
        });

        var t113 = registry.GetTarget("allwinner-t113");
        Assert.NotNull(t113);
        Assert.Equal("Allwinner T113-S3", t113!.Metadata.Name);
    }

    [Fact]
    public void T113_Display_DefaultResolution_800x480()
    {
        var target = new AllwinnerT113Target();
        Assert.Equal(800, target.Display.DefaultWidth);
        Assert.Equal(480, target.Display.DefaultHeight);
    }

    [Fact]
    public void RK3506_Display_DefaultResolution_480x272()
    {
        var target = new RockchipRK3506Target();
        Assert.Equal(480, target.Display.DefaultWidth);
        Assert.Equal(272, target.Display.DefaultHeight);
    }

    [Fact]
    public void T113_ValidateProject_HighResolution_ReturnsWarning()
    {
        var target = new AllwinnerT113Target();
        var project = new HmiProject();
        var screen = new HmiScreen { Name = "Big", Width = 1024, Height = 600 };
        project.Screens.Add(screen);

        var result = target.ValidateProject(project);
        Assert.True(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "RESOLUTION_EXCEEDS_DEFAULT");
    }

    [Fact]
    public void T113_GetTemplateParameters_ContainsColorDepth()
    {
        var target = new AllwinnerT113Target();
        var params_ = target.GetTemplateParameters();
        Assert.Equal(32, params_["color_depth"]);
    }
}
```

- [ ] **Step 12: 编译验证**

```powershell
dotnet build HmiEditor.sln
```
Expected: Build succeeded. 0 Error(s).

- [ ] **Step 13: 运行测试**

```powershell
dotnet test tests\Hmi.Hardware.Tests\Hmi.Hardware.Tests.csproj
```
Expected: 所有测试通过。

- [ ] **Step 14: Commit**

```powershell
git add .
git commit -m "feat: hardware target plugin system — IHardwareTarget contract + T113/RK3506 builtin implementations"
```

---

## Phase 1: Core Editor MVP（第 3-10 周）

> 前提：Phase 0 全部通过。目标：产出可用的基础编辑器，能拖放 8 个核心 LVGL 控件并导出可编译的 C 代码。

### Task 1.1: HMI 数据模型（Core 层）

**Files:**
- Create: `src\HmiEditor.Core\Models\HmiProject.cs`
- Create: `src\HmiEditor.Core\Models\HmiScreen.cs`
- Create: `src\HmiEditor.Core\Models\HmiControl.cs`
- Create: `src\HmiEditor.Core\Models\HmiControlType.cs`
- Create: `src\HmiEditor.Core\Models\HmiProperty.cs`

- [ ] **Step 1: 编写 HmiControlType 枚举**

```csharp
namespace HmiEditor.Core.Models;

public enum HmiControlType
{
    Screen,      // lv_obj (容器)
    Button,      // lv_button_create
    Label,       // lv_label_create
    Slider,      // lv_slider_create
    Switch,      // lv_switch_create
    CheckBox,    // lv_checkbox_create
    Image,       // lv_image_create
    Bar,         // lv_bar_create
    Dropdown,    // lv_dropdown_create
    TextArea     // lv_textarea_create
}
```

- [ ] **Step 2: 编写 HmiControl 模型**

```csharp
using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

public class HmiControl
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public HmiControlType Type { get; set; }
    public string Name { get; set; } = "";

    // 位置与大小（像素，对应 LVGL 坐标）
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 40;

    // 通用属性
    public string? Text { get; set; }
    public int Value { get; set; }
    public bool IsChecked { get; set; }

    // 样式属性（V1 最小集：背景色、文本色、圆角、边框）
    public string? BgColor { get; set; }
    public string? TextColor { get; set; }
    public int Radius { get; set; }
    public int BorderWidth { get; set; }
    public string? BorderColor { get; set; }

    // 层级关系
    public string? ParentId { get; set; }
    public ObservableCollection<HmiControl> Children { get; set; } = new();
}
```

- [ ] **Step 3: 编写 HmiScreen 模型**

```csharp
namespace HmiEditor.Core.Models;

public class HmiScreen
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Screen 1";
    public double Width { get; set; } = 800;
    public double Height { get; set; } = 480;
    public string? BgColor { get; set; }
    public ObservableCollection<HmiControl> Controls { get; set; } = new();
}
```

- [ ] **Step 4: 编写 HmiProject 模型**

```csharp
using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

public class HmiProject
{
    public string Name { get; set; } = "Untitled";
    public string LvglVersion { get; set; } = "9.2";
    public ObservableCollection<HmiScreen> Screens { get; set; } = new();
}
```

- [ ] **Step 5: 编写测试验证模型创建**

- [ ] **Step 6: Commit**

---

### Task 1.2: 项目序列化/反序列化

**Files:**
- Create: `src\HmiEditor.Core\Serialization\ProjectSerializer.cs`

- [ ] **Step 1: 编写 ProjectSerializer（System.Text.Json）**

```csharp
using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Serialization;

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(HmiProject project)
    {
        return JsonSerializer.Serialize(project, Options);
    }

    public static HmiProject? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<HmiProject>(json, Options);
    }
}
```

- [ ] **Step 2: 编写测试（序列化往返 + 边界值）**

- [ ] **Step 3: Commit**

---

### Task 1.3: Avalonia ↔ LVGL 控件映射注册表

**Files:**
- Create: `src\HmiEditor.Core\Mapping\ControlMappingRegistry.cs`
- Create: `src\HmiEditor.Core\Mapping\PropertyMapping.cs`

- [ ] **Step 1: 编写 PropertyMapping**

```csharp
namespace HmiEditor.Core.Mapping;

public record PropertyMapping(
    string AvaloniaPropertyName,
    string LvglPropertyName,
    string LvglSetterFormat  // 如 "lv_obj_set_{0}({1}, {2})"
);
```

- [ ] **Step 2: 编写 ControlMappingRegistry**

```csharp
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Mapping;

public static class ControlMappingRegistry
{
    public static readonly Dictionary<HmiControlType, string> LvglCreateFunction = new()
    {
        [HmiControlType.Screen]   = "lv_obj_create",
        [HmiControlType.Button]   = "lv_button_create",
        [HmiControlType.Label]    = "lv_label_create",
        [HmiControlType.Slider]   = "lv_slider_create",
        [HmiControlType.Switch]   = "lv_switch_create",
        [HmiControlType.CheckBox] = "lv_checkbox_create",
        [HmiControlType.Image]    = "lv_image_create",
        [HmiControlType.Bar]      = "lv_bar_create",
        [HmiControlType.Dropdown] = "lv_dropdown_create",
        [HmiControlType.TextArea] = "lv_textarea_create",
    };

    public static readonly List<PropertyMapping> CommonPropertyMappings = new()
    {
        new("X", "x", "lv_obj_set_x({0}, {1})"),
        new("Y", "y", "lv_obj_set_y({0}, {1})"),
        new("Width", "w", "lv_obj_set_width({0}, {1})"),
        new("Height", "h", "lv_obj_set_height({0}, {1})"),
    };
}
```

- [ ] **Step 3: 编写测试（枚举覆盖率：确保所有 HmiControlType 有对应 create 函数）**

- [ ] **Step 4: Commit**

---

### Task 1.4: C 代码生成引擎（V1）

**Files:**
- Create: `src\HmiEditor.CodeGen\CodeGenerator.cs`
- Create: `src\HmiEditor.CodeGen\LvglApiWriter.cs`
- Create: `src\HmiEditor.CodeGen\Templates\lvgl_v9_main.scriban`
- Create: `src\HmiEditor.CodeGen\Templates\lvgl_v9_screen.scriban`

- [ ] **Step 1: 编写 Scriban 模板 — lvgl_v9_screen.scriban**

```scriban
// Auto-generated by HmiEditor — DO NOT EDIT MANUALLY
// Screen: {{screen.name}}

static void create_screen_{{screen.id}}(lv_obj_t* parent) {
    lv_obj_t* screen = {{create_func}}(parent);
    lv_obj_set_size(screen, {{screen.width}}, {{screen.height}});
    {{~ if screen.bg_color ~}}
    lv_obj_set_style_bg_color(screen, {{screen.bg_color}}, 0);
    {{~ end ~}}

    {{~ for control in screen.controls ~}}
    // {{control.name}} ({{control.type}})
    {
        lv_obj_t* obj = {{control.create_func}}(screen);
        lv_obj_set_pos(obj, {{control.x}}, {{control.y}});
        lv_obj_set_size(obj, {{control.width}}, {{control.height}});
        {{~ if control.text ~}}
        {{~ if control.type == "Label" || control.type == "Button" ~}}
        lv_label_set_text(obj, "{{control.text}}");
        {{~ end ~}}
        {{~ end ~}}
        {{~ if control.bg_color ~}}
        lv_obj_set_style_bg_color(obj, {{control.bg_color}}, 0);
        {{~ end ~}}
    }
    {{~ end ~}}
}
```

- [ ] **Step 2: 编写 LvglApiWriter.cs**

负责将 `HmiControl` 对象的属性转换为 LVGL API 调用字符串。

- [ ] **Step 3: 编写 CodeGenerator.cs（整合入口）**

```csharp
using HmiEditor.Core.Models;
using Scriban;

namespace HmiEditor.CodeGen;

public class CodeGenerator
{
    public string GenerateMainC(HmiProject project)
    {
        var template = Template.Parse(File.ReadAllText("Templates/lvgl_v9_main.scriban"));
        return template.Render(new { project });
    }
}
```

- [ ] **Step 4: 编写测试（输入一个简单 Project→生成 C 代码→验证包含 lv_obj_create 等关键调用）**

- [ ] **Step 5: Commit**

---

### Task 1.5: 设计画布 + Adorner 层（App 层核心）

**Files:**
- Create: `src\HmiEditor.App\Controls\DesignCanvas.cs`
- Create: `src\HmiEditor.App\Controls\SelectionAdorner.cs`
- Create: `src\HmiEditor.App\Controls\SnapGrid.cs`
- Create: `src\HmiEditor.App\Views\DesignCanvasView.axaml`
- Create: `src\HmiEditor.App\Views\DesignCanvasView.axaml.cs`

**说明**：这是整个编辑器最核心的模块。基于 Task 0.2 的 prototype 重构为正式架构。

- [ ] **Step 1: 编写 SelectionAdorner（选择框 + 8 点缩放手柄）**

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HmiEditor.App.Controls;

public class SelectionAdorner : Control
{
    public Rect Bounds { get; set; }
    public bool IsVisible { get; set; }

    public double HandleSize { get; set; } = 8;

    public override void Render(DrawingContext context)
    {
        if (!IsVisible) return;

        // 选择框
        var pen = new Pen(Brushes.DodgerBlue, 1);
        context.DrawRectangle(null, pen, Bounds);

        // 8 个缩放手柄
        var handleBrush = Brushes.White;
        var handlePen = new Pen(Brushes.DodgerBlue, 1);
        var handles = new[]
        {
            Bounds.TopLeft, Bounds.TopRight, Bounds.BottomLeft, Bounds.BottomRight,
            new Point(Bounds.Center.X, Bounds.Top),
            new Point(Bounds.Center.X, Bounds.Bottom),
            new Point(Bounds.Left, Bounds.Center.Y),
            new Point(Bounds.Right, Bounds.Center.Y),
        };
        foreach (var h in handles)
        {
            var rect = new Rect(h.X - HandleSize / 2, h.Y - HandleSize / 2, HandleSize, HandleSize);
            context.DrawRectangle(handleBrush, handlePen, rect);
        }
    }
}
```

- [ ] **Step 2: 编写 SnapGrid（网格吸附背景）**

```csharp
using Avalonia;
using Avalonia.Media;

namespace HmiEditor.App.Controls;

public class SnapGrid : Control
{
    public double GridSize { get; set; } = 10;

    public override void Render(DrawingContext context)
    {
        var dotBrush = new SolidColorBrush(Color.FromRgb(60, 70, 90));
        var dotSize = 1.0;

        for (double x = 0; x < Bounds.Width; x += GridSize)
        for (double y = 0; y < Bounds.Height; y += GridSize)
        {
            context.DrawRectangle(dotBrush, null,
                new Rect(x - dotSize / 2, y - dotSize / 2, dotSize, dotSize));
        }
    }
}
```

- [ ] **Step 3: 编写 DesignCanvas 整合类**

关键逻辑：控件选中（单击高亮+显示选择框）、拖拽移动（支持 8 点缩放）、多选（Ctrl+点击）、对齐线。

- [ ] **Step 4: 编写 DesignCanvasView**

整合：SnapGrid（底层背景）+ DesignCanvas（控件层）+ SelectionAdorner（顶层装饰）。

- [ ] **Step 5: 编写交互测试（拖拽后坐标变化验证）**

- [ ] **Step 6: Commit**

---

### Task 1.6: 控件工具箱

**Files:**
- Create: `src\HmiEditor.App\Views\ToolboxView.axaml`
- Create: `src\HmiEditor.App\Views\ToolboxView.axaml.cs`

- [ ] **Step 1: 编写 ToolboxView（可折叠分组，每类控件拖拽图标+名称）**

分组结构：
- 容器：lv_obj (Screen)
- 输入控件：Button、Slider、Switch、CheckBox
- 显示控件：Label、Image、Bar
- 选择控件：Dropdown
- 输入控件：TextArea

- [ ] **Step 2: 实现拖放到画布功能**

从工具箱长按/拖拽图标到设计画布时，在画布上创建对应 HmiControl。

- [ ] **Step 3: Commit**

---

### Task 1.7: 属性编辑面板

**Files:**
- Create: `src\HmiEditor.App\Views\PropertyPanelView.axaml`
- Create: `src\HmiEditor.App\Views\PropertyPanelView.axaml.cs`

- [ ] **Step 1: 编写 PropertyPanelView**

当画布上选中控件时，右侧属性面板显示：位置（X/Y/Width/Height）、文本内容、样式（背景色/文本色/圆角/边框）、类型信息。

- [ ] **Step 2: 实现双向绑定**

修改属性面板的值 → 实时更新画布上控件的外观和模型数据。

- [ ] **Step 3: Commit**

---

### Task 1.8: 控件树面板

**Files:**
- Create: `src\HmiEditor.App\Views\OutlinePanelView.axaml`
- Create: `src\HmiEditor.App\Views\OutlinePanelView.axaml.cs`

- [ ] **Step 1: 编写 OutlinePanelView**

TreeView 展示当前 Screen 的控件层级。支持：点击选中、拖拽排序改变层级、右键菜单（复制/删除/上移/下移）。

- [ ] **Step 2: Commit**

---

### Task 1.9: 主界面整合 + 菜单与工具栏

**Files:**
- Modify: `src\HmiEditor.App\MainWindow.axaml`
- Modify: `src\HmiEditor.App\MainWindow.axaml.cs`

- [ ] **Step 1: 四面板 Dock 布局**

```
┌──────────┬──────────────────────┬──────────┐
│ Toolbox  │   Design Canvas      │ Property │
│ (200px)  │   (flex)             │ (280px)  │
│          │                      │          │
├──────────┴──────────────────────┴──────────┤
│            Outline Panel (bottom 150px)     │
└─────────────────────────────────────────────┘
```

- [ ] **Step 2: 菜单栏**

File: New / Open / Save / Export C Code / Exit
Edit: Undo / Redo / Delete / Select All

- [ ] **Step 3: 工具栏**

快捷按钮：New Screen / Export / Run Preview / Zoom In / Zoom Out

- [ ] **Step 4: 全局数据流**

使用 CommunityToolkit.Mvvm 的 Messenger 模式实现工具箱→画布、画布→属性面板、画布→控件树 的双向通信。

- [ ] **Step 5: Commit**

---

### Task 1.10: Phase 1 集成测试与 Demo 验收

- [ ] **Step 1: 编写端到端测试场景**

```
场景：创建完整 HMI 页面
1. 新建 Project → Screen 800x480
2. 从工具箱拖入 1 个 Button (120x44, 位置 340,400)
3. 从工具箱拖入 1 个 Label (200x30, 位置 300,100)，文本 "Hello LVGL"
4. 从工具箱拖入 1 个 Slider (300x20, 位置 250,200)
5. 选中 Label，修改文本为 "System Ready"
6. 导出 C 代码
7. 验证生成的 C 代码包含：
   - lv_obj_create() * 2 (screen + button)
   - lv_button_create() * 1
   - lv_label_create() * 1 (或 lv_obj_create + lv_label_set_text)
   - lv_slider_create() * 1
   - lv_obj_set_pos() × 4
   - lv_obj_set_size() × 4
```

- [ ] **Step 2: 运行验收测试**

```powershell
dotnet test
```

- [ ] **Step 3: 录制 Demo 视频（可选）**

- [ ] **Step 4: Phase 1 Checkpoint 评审**

- [ ] **Step 5: Commit + Tag**

```powershell
git add .
git commit -m "release: HmiEditor v0.1.0 — Phase 1 MVP complete"
git tag v0.1.0
```

---

## Phase 2: 增强功能（第 11-22 周）

> 目标：提升编辑器实用性到可公开发布水准。

### Task 2.1: 动画编辑器

**Files:**
- Create: `src\HmiEditor.App\Views\AnimationEditorView.axaml`
- Create: `src\HmiEditor.App\Views\AnimationEditorView.axaml.cs`
- Modify: `src\HmiEditor.CodeGen\LvglApiWriter.cs`（添加动画 API 生成）

实现：时间轴轨道 + 关键帧编辑，映射到 `lv_anim_set_*()` API。

### Task 2.2: 主题与样式编辑器

**Files:**
- Create: `src\HmiEditor.App\Views\StyleEditorView.axaml`

实现：全局样式管理（lv_style_t），为控件批量设置主题色、字体、圆角等。

### Task 2.3: 屏幕/页面管理器

**Files:**
- Create: `src\HmiEditor.App\Views\ScreenManagerView.axaml`

实现：多页面切换编辑，映射到 `lv_screen_load()` / `lv_screen_create()`。

### Task 2.4: 多语言/国际化支持

**Files:**
- Create: `src\HmiEditor.Core\Services\LocalizationService.cs`
- Create: `src\HmiEditor.App\Resources\Strings.zh-CN.resx`
- Create: `src\HmiEditor.App\Resources\Strings.en-US.resx`

### Task 2.5: Linux ARM64 完整测试

**Files:**
- Create: `build\test-linux-arm64.ps1`

建立 CI 流程：交叉编译 → 部署到 ARM64 设备 → 自动化 UI 测试。

### Task 2.6: 硬件目标 DLL 插件加载（Tier 2/3）

**Files:**
- Modify: `src\Hmi.Hardware.Loader\HardwareTargetRegistry.cs`（启用 LoadPluginTargets/LoadConfigTargets）
- Modify: `src\Hmi.Hardware.Loader\PluginLoadContext.cs`（从 internal 改为 public，启用 DLL 加载）
- Create: `src\Hmi.Hardware.Loader\ConfigDrivenTarget.cs`（JSON 配置适配器）
- Create: `targets\stm32f767.target.json`（示例 JSON 配置插件）
- Create: `tests\Hmi.Hardware.Tests\PluginLoadingTests.cs`

**目的**：实现 Phase 0 预留的 Tier 2（DLL 热插拔）和 Tier 3（JSON 配置）插件加载机制。

实现内容：
1. `LoadPluginTargets(pluginDirectory)` —— 扫描 `targets/*.dll`，使用 `PluginLoadContext` 反射加载，发现 `IHardwareTarget` 实现类并注册
2. `LoadConfigTargets(configDirectory)` —— 扫描 `targets/*.target.json`，反序列化为 `TargetConfigJson`，通过 `ConfigDrivenTarget` 适配器包装为 `IHardwareTarget`
3. `ConfigDrivenTarget` —— 适配器模式，将 JSON 配置映射到 `IHardwareTarget` 接口实现；`ValidateProject` 使用通用规则（分辨率/色深检查），不支持自定义初始化代码生成
4. 卸载测试 —— 验证 `PluginLoadContext.Unload()` 后 `GC.Collect()` 能回收程序集内存

### Task 2.7: Phase 2 发布

版本号：v1.0.0

---

## Phase 3: 工业运行时功能（第 23-38 周）

> 目标：从"UI 设计工具"进化为"完整 HMI 开发平台"，补充工厂现场必需的运行时能力。
> 前提：Phase 1/2 全部完成。Phase 3 的变量管理和通讯驱动基础设施已在 Phase 0 完成建模。

**Phase 3 背景**：对比 Siemens WinCC、Weintek EasyBuilder Pro、Rockwell FactoryTalk View、Pro-face GP-Pro EX、Beijer iX Developer 五大主流产品，发现 12 项功能缺口中 9 项完全缺失，其中报警/趋势/配方为三大致命缺口。

---

### Phase 3A: 安全与数据基础（第 23-30 周）

> 目标：实现三项工业 HMI 标配核心功能（报警、趋势、用户权限）。

---

### Task 3.1: 报警管理系统

**Files:**
- Create: `src\HmiEditor.Core\Models\Alarm\AlarmConfig.cs` — 报警配置模型
- Create: `src\HmiEditor.Core\Models\Alarm\AlarmCondition.cs` — 触发条件（阈值/位触发/状态变化）
- Create: `src\HmiEditor.Core\Models\Alarm\AlarmRecord.cs` — 报警记录（发生/确认/恢复时间戳）
- Create: `src\HmiEditor.Core\Models\Alarm\AlarmGroup.cs` — 报警分组
- Create: `src\HmiEditor.Core\Services\AlarmEngine.cs` — 报警引擎（条件评估/状态机）
- Create: `src\HmiEditor.App\Views\AlarmConfigView.axaml` — 报警配置面板
- Create: `src\HmiEditor.App\Views\AlarmListView.axaml` — 报警列表/汇总控件
- Modify: `src\HmiEditor.Core\Models\Variable.cs` — 添加报警配置属性
- Modify: `src\HmiEditor.CodeGen\Templates\lvgl_v9_alarm.scriban` — 报警 C 代码模板
- Create: `tests\HmiEditor.Core.Tests\AlarmEngineTests.cs`

**核心模型：**

```csharp
// 报警条件
public class AlarmCondition
{
    public string VariableId { get; set; } = "";
    public AlarmConditionType Type { get; set; }  // HighLimit, LowLimit, BitTrigger, StateChange, Deviation
    public double Threshold { get; set; }
    public double Deadband { get; set; }           // 死区，防抖动
    public int DelayMs { get; set; }               // 延迟触发
}

// 报警配置
public class AlarmConfig
{
    public string Name { get; set; } = "";
    public AlarmSeverity Severity { get; set; }    // Info, Warning, Critical, Emergency
    public string GroupId { get; set; } = "";
    public bool RequiresAck { get; set; } = true;   // 是否需要确认
    public AlarmCondition Condition { get; set; } = new();
}

// 报警记录（运行时）
public class AlarmRecord
{
    public string Id { get; set; }
    public AlarmState State { get; set; }           // Inactive, Active, Acknowledged, Returned
    public DateTime OccurredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
}
```

**设计要点：**
- 报警状态机：Inactive → Active → Acknowledged → Returned
- 五种触发条件：上限/下限/位触发/状态变化/偏差
- 死区防抖 + 延迟触发，对标 WinCC 的 Alarm Logging
- C 代码生成：每个报警生成一个 `alarm_check_*()` 函数
- LVGL 运行时：报警状态通过变量标志位传递（无动态内存分配）

---

### Task 3.2: 趋势图与数据记录

**Files:**
- Create: `src\HmiEditor.Core\Models\Trend\TrendConfig.cs` — 趋势图配置
- Create: `src\HmiEditor.Core\Models\Trend\TrendPen.cs` — 曲线笔（变量绑定/颜色/线宽）
- Create: `src\HmiEditor.Core\Models\Trend\DataLogConfig.cs` — 数据记录配置
- Create: `src\HmiEditor.Core\Services\DataLogger.cs` — 数据记录引擎
- Modify: `src\HmiEditor.Core\Models\Variable.cs` — 添加趋势配置属性
- Modify: `src\HmiEditor.CodeGen\Templates\lvgl_v9_trend.scriban` — 趋势图 C 代码模板
- Create: `tests\HmiEditor.Core.Tests\DataLoggerTests.cs`

**核心模型：**

```csharp
// 趋势图（一个控件实例，可包含多条曲线）
public class TrendConfig
{
    public string ControlId { get; set; } = "";
    public int TimeWindowMs { get; set; } = 60000;  // 默认显示 60 秒
    public IReadOnlyList<TrendPen> Pens { get; set; }
    public bool ShowLegend { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
}

public class TrendPen
{
    public string VariableId { get; set; } = "";
    public string Color { get; set; } = "#38bdf8";
    public int LineWidth { get; set; } = 2;
    public string? Label { get; set; }
}

// 数据记录（全局配置）
public class DataLogConfig
{
    public int SampleIntervalMs { get; set; } = 1000;
    public int MaxRecords { get; set; } = 86400;   // 24 小时 @ 1s
    public string? StoragePath { get; set; }        // SD 卡路径或 Flash 偏移
    public IReadOnlyList<string> VariableIds { get; set; }
}
```

**设计要点：**
- 趋势曲线：基于 LVGL Chart 控件（`lv_chart`），支持实时/历史模式
- 数据记录：环形缓冲区（Flash 友好），支持 CSV 导出
- 采样策略：定时采样 + 变化触发（Change-of-Value），对标 FactoryTalk View Data Log
- 存储：嵌入式 → 环形 Flash 扇区；PC 仿真 → SQLite

---

### Task 3.3: 用户访问控制

**Files:**
- Create: `src\HmiEditor.Core\Models\User\UserProfile.cs` — 用户模型
- Create: `src\HmiEditor.Core\Models\User\Role.cs` — 角色与权限枚举
- Create: `src\HmiEditor.Core\Services\AuthenticationService.cs` — 认证服务
- Create: `src\HmiEditor.App\Views\UserManagerView.axaml` — 用户管理面板
- Modify: `src\HmiEditor.CodeGen\Templates\lvgl_v9_user.scriban` — 用户 C 代码模板
- Create: `tests\HmiEditor.Core.Tests\AuthenticationTests.cs`

**核心模型：**

```csharp
public enum Permission
{
    ViewScreen,        // 查看画面
    OperateControl,    // 操作控件（按钮/滑块）
    EditRecipe,        // 编辑配方
    AcknowledgeAlarm,  // 确认报警
    ChangeConfig,      // 修改配置
    ManageUsers        // 管理用户
}

public class Role
{
    public string Name { get; set; } = "";
    public int Level { get; set; }   // 0-15，对标 Pro-face 模型
    public IReadOnlyList<Permission> Permissions { get; set; }
}

public class UserProfile
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string RoleName { get; set; } = "Operator";
    public DateTime? ExpiresAt { get; set; }
    public bool IsEnabled { get; set; } = true;
}
```

**设计要点：**
- 三层预置角色：Operator(0-3) → Engineer(4-7) → Administrator(8-15)
- 可自定义角色权限组合
- 密码哈希存储（SHA-256 + Salt），对标 Pro-face 15 级安全模型
- 页面级访问控制：每个 Screen 可设定最低访问等级
- C 代码生成：生成 `user_check_permission()` 函数

---

### Phase 3B: 灵活性与运维（第 31-38 周）

> 目标：实现脚本引擎、配方管理、离线仿真。

---

### Task 3.4: 宏/脚本引擎（Lua）

**Files:**
- Create: `src\HmiEditor.Scripting\HmiEditor.Scripting.csproj`
- Create: `src\HmiEditor.Scripting\LuaScriptEngine.cs` — Lua 解释器封装
- Create: `src\HmiEditor.Scripting\ScriptingApi.cs` — HMI 脚本 API 桥接层
- Create: `src\HmiEditor.Scripting\ScriptTemplateProvider.cs` — 脚本模板
- Create: `src\HmiEditor.App\Views\ScriptEditorView.axaml` — 脚本编辑器面板
- Modify: `src\HmiEditor.Core\Models\HmiControl.cs` — 添加事件脚本关联
- Create: `tests\HmiEditor.Scripting.Tests\LuaScriptingTests.cs`

**核心模型：**

```csharp
// 脚本引擎接口
public interface IScriptEngine
{
    void LoadScript(string scriptId, string code);
    object? ExecuteFunction(string functionName, Dictionary<string, object>? args = null);
    event EventHandler<ScriptLogEventArgs>? OnLog;
}

// 控件事件 → 脚本映射
public class ControlScriptBinding
{
    public string ControlId { get; set; } = "";
    public string EventName { get; set; } = "";   // "OnClick", "OnValueChanged", "OnPress"
    public string ScriptId { get; set; } = "";
    public string FunctionName { get; set; } = ""; // 入口函数名
}
```

**设计要点：**
- 选择 **Lua**（而非 C#/VBA/JavaScript）：理由 —— LVGL 已有 `lv_lua_binding` 桥接层，运行时体积小（~200KB），工业领域广泛使用
- 编辑器侧：使用 NLua（C# Lua 解释器）提供脚本编辑、语法检查、断点调试
- 运行时侧：生成 `lv_lua_init()` + 事件回调分发代码
- 预置脚本模板：按钮点击、值变化、定时器、报警联动 4 类
- 对标：Weintek EasyBuilder Pro 的宏指令（C-like） + Beijer iX Developer 的 C#开放性

---

### Task 3.5: 配方管理

**Files:**
- Create: `src\HmiEditor.Core\Models\Recipe\Recipe.cs` — 配方模型
- Create: `src\HmiEditor.Core\Models\Recipe\RecipeItem.cs` — 配方条目
- Create: `src\HmiEditor.App\Views\RecipeEditorView.axaml` — 配方编辑面板
- Modify: `src\HmiEditor.CodeGen\Templates\lvgl_v9_recipe.scriban` — 配方 C 代码模板
- Create: `tests\HmiEditor.Core.Tests\RecipeSerializerTests.cs`

**核心模型：**

```csharp
public class Recipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";      // "产品A-高温模式"
    public string GroupName { get; set; } = "";  // "温度控制"
    public int Version { get; set; } = 1;
    public IReadOnlyList<RecipeItem> Items { get; set; }
}

public class RecipeItem
{
    public string VariableId { get; set; } = "";  // 绑定的变量
    public string Label { get; set; } = "";        // "目标温度"
    public double DefaultValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
}
```

**设计要点：**
- 配方 ≡ 一组变量的预设值组合
- 配方 CRUD + CSV 导入导出（对标所有竞品标配）
- 配方上传/下载：通过通讯驱动批量读写寄存器
- 版本管理：每次修改递增版本号
- LVGL 运行时：配方数据存储在 Flash 固定扇区，切换时批量写入寄存器

---

### Task 3.6: 离线仿真

**Files:**
- Create: `src\HmiEditor.Simulator\HmiEditor.Simulator.csproj`
- Create: `src\HmiEditor.Simulator\LvglSimulatorHost.cs` — LVGL SDL2 模拟宿主
- Create: `src\HmiEditor.Simulator\VariableSimulator.cs` — 变量模拟值引擎
- Create: `src\HmiEditor.Simulator\CommunicationSimulator.cs` — 通讯协议模拟
- Create: `src\HmiEditor.App\Views\SimulatorView.axaml` — 仿真运行面板
- Create: `build\lvgl-simulator\` — LVGL SDL2 编译脚本

**设计要点：**
- SDL2 后端 LVGL PC 模拟器（对标 EasyBuilder Pro 的 EasySimulator）
- 变量模拟：正弦波/方波/递增/递减/随机/手动输入 6 种模式
- 通讯模拟：内置虚拟 Modbus 从站，回显写入值
- 仿真面板：变量值实时更新（高亮变化）、报警预览、趋势回放
- 与 Avalonia 编辑器分离进程，通过 IPC（Named Pipe）通信

---

### Task 3.7: Phase 3 集成测试与发布

- Phase 3A Checkpoint：报警 + 趋势 + 用户管理集成测试
- Phase 3B Checkpoint：脚本 + 配方 + 仿真集成测试
- Phase 3 发布：v2.0.0

---

## 开发规范

### 提交规范

采用 Conventional Commits：
- `feat:` — 新功能
- `fix:` — Bug 修复
- `docs:` — 文档
- `test:` — 测试
- `refactor:` — 重构
- `build:` — 构建脚本

### 质量门禁

每个 Task 完成后必须通过：
1. `dotnet build HmiEditor.sln` — 0 Error
2. `dotnet test` — 100% 通过
3. 代码审查（自查或被审查）

### 分支策略

- `main` — 稳定分支
- `feature/phase0-*` — Phase 0 特性分支
- `feature/phase1-*` — Phase 1 特性分支
- `release/v*` — 发布分支

### 资源清理规范

遵循用户规则：所有构建/运行脚本必须包含进程终止、端口释放、IPC 回收逻辑。
- 构建脚本 `build.ps1` / `clean.ps1` 需在退出时对比进程快照，清理僵尸进程
- Avalonia 应用通过 `App.axaml.cs` 的 `OnExit` 事件注册清理钩子
