# HMI Editor v2.1.0 — Phase 2 发布摘要

> 发布日期：2026-07-30
> 版本标识：v2.1.0
> 基础 SDK：.NET 10 + Avalonia 12.1.0

---

## 概述

从 v0.1.0（Phase 1 MVP）到 v1.0.0 的增强版本，将 HMI 编辑器从基础原型提升至可公开发布水准。新增动画编辑器、主题编辑、Faceplate 参数化复合组件系统、多屏幕管理、国际化、变量与控件动作系统、硬件目标插件系统、Linux ARM64 部署支持、以及工程化的构建/测试基础设施，并完成 Avalonia 12.1 迁移与 UI 重构。

---

## 新增功能

### 2.1 动画编辑器
- `AnimationConfig` / `AnimationKeyframe` / `AnimationPathType` 模型，对应 LVGL v9 动画系统（`lv_anim_init` → `lv_anim_start`）
- 7 种缓动函数：线性 / 缓入缓出 / 缓入 / 缓出 / 弹性回弹 / 过冲 / 步进
- 可动画属性：X / Y / Width / Height / Opacity / Radius / BorderWidth / Value
- 触发方式：OnLoad / OnClick / OnValueChange / OnPress / OnRelease
- 关键帧列表支持（为空时使用起止值线性过渡）
- 循环次数（0=不重复，-1=无限循环，N=重复 N 次）
- `CodeGenerator.GenerateAnimationCode` 生成 LVGL C 代码，控制变量提升为文件作用域静态量
- 动画配置支持工程序列化（`ProjectSerializer`）
- 可视化时间轴交互：
  - 拖拽关键帧圆点调整时间位置（实时更新 + 重叠检测 + 0-100% 钳制）
  - 双击时间轴轨道添加关键帧（自动插值计算属性值）
  - 播放/停止预览按钮（DispatcherTimer 驱动播放头 + 实时显示插值值）
  - 关键帧间值变化曲线（虚线 Polyline 可视化）

### 2.2 主题与样式编辑器
- 完整的 10 色调色板编辑（主色/次色/面板/画布/文本/边框/状态色）
- 全局尺寸设置（基础字号、圆角、边框宽度）
- 实时预览 + 恢复默认 + 应用保存
- 所有主题数据通过 `HmiTheme` 模型统一管理

### 2.3 屏幕/页面管理器
- 多屏幕创建/删除/切换
- 屏幕命名与分辨率配置
- 屏幕间独立控件集管理
- 映射至 `lv_screen_load()` / `lv_screen_create()` LVGL API

### 2.4 多语言/国际化支持
- `LocalizationService` — 运行时语言切换 + 事件通知
- JSON 资源文件（zh-CN / en-US），支持外部文件热加载
- fallback 机制：当前语言缺失 → en-US → key 本身
- 所有 UI 文本通过 `LocService["key"]` 统一访问

### 2.5 变量与控件动作系统
- 变量管理对话框：CRUD 变量与分组，类型/地址/单位/缩放编辑
- 17 个系统内置变量（时间、用户、通讯、HMI 状态），`$SYS$` 寻址
- 控件动作系统：SwitchScreen / SetVariable / ExecuteScript / Login / Logout
- 控件类型专属动作：按钮的 SetBit / ResetBit / ToggleBit / MomentaryOn / MomentaryOff
- 动态动作列表（按控件类型），变量绑定下拉框从工程变量填充
- 属性面板集成 12 色预设调色板 + 十六进制输入取色器

### 2.6 Faceplate 参数化复合组件系统（Phase R2）
- 模板化复合组件，支持参数化展开（`FaceplateExpander`）
- `TemplateManager` 绑定 `HmiProject.Templates`（单一数据源）
- 循环依赖检测与跳过
- Faceplate 编辑器视图与 MainWindow 集成

### 2.7 Linux ARM64 部署
- 交叉编译脚本 `publish-linux-arm64.ps1`（NativeAOT）
- AOT 裁剪保护配置文件 `rd.xml`
- 自动化测试脚本 `test-linux-arm64.ps1`
- WSL2 交叉编译工作流文档

### 2.8 硬件目标 DLL 插件加载（Tier 2/3）
- **Tier 2 — DLL 热插拔**：`PluginLoadContext`（AssemblyLoadContext, isCollectible=true），支持程序集卸载回收
- **Tier 3 — JSON 配置**：`ConfigDrivenTarget` 适配器，将 JSON 配置映射为 `IHardwareTarget` 接口
- 示例配置：`targets/stm32f767.target.json`（STM32F767 Cortex-M7）
- 22 个硬件插件单元测试覆盖

### 2.9 UI 重构与 Avalonia 12.1 迁移（Phase R1）
- 拖放操作迁移至 Avalonia 12.1 DataTransfer API
- 采用 Dock.Avalonia v12.1.0 + FluentAvaloniaUI v3.0.2
- 暗色工业 IDE 风格界面

### 2.10 工程化改进
- 版本信息集中管理（`VersionInfo.cs`）
- 构建脚本 `build.ps1` 包含进程快照隔离 + 残余清理
- 清理脚本 `clean.ps1` — 缓存写保护 + IPC 资源释放
- 0 警告策略强制执行（修复 CS8618 / CS8600 / CS8602 / CS0414 / AVLN3001）

---

## 未纳入本次发布

无待实现功能。后续版本将根据用户反馈持续增强。

---

## 质量指标

| 指标 | 数值 |
|------|------|
| 单元测试总数 | 310 |
| 测试通过率 | 100% |
| 编译警告 | 0 |
| 项目数 | 16（含 4 个测试项目） |
| 代码工程 | 12 个 src + 4 个 tests + 1 个 slnx |

### 测试分布

| 测试项目 | 用例数 | 覆盖领域 |
|----------|--------|---------|
| HmiEditor.Core.Tests | 249 | 数据模型、序列化、映射注册、本地化、代码生成、动画、Faceplate、CSV 导入、样式预设、模板管理 |
| Hmi.Communication.Tests | 15 | Modbus RTU/TCP、MQTT、驱动注册 |
| Hmi.Hardware.Tests | 22 | 内置插件、插件加载、ConfigDrivenTarget |
| HmiEditor.Simulator.Tests | 24 | 模拟器行为 |

---

## 构建与部署

```powershell
# 完整构建
.\build\build.ps1 -RunTests

# 发布 Linux ARM64（需要在 WSL2 环境中执行）
.\build\publish-linux-arm64.ps1

# 清理
.\build\clean.ps1
```

---

## 已知问题

1. **Linux ARM64 NativeAOT 交叉编译**：Windows → Linux 直接交叉编译受 .NET NativeAOT 限制，需使用 WSL2 环境执行发布脚本
