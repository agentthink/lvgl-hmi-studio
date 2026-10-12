# ADR-R2: Phase R2 架构决策

**Status**: Accepted
**Date**: 2026-07-31
**Supersedes**: None
**Responsibility**: User + local checks (dotnet build + dotnet test)

## Context / Forces

Phase R2 引入 5 个新功能模块，涉及对 Core 模型的修改和新 View 的创建。

| Force | Rationale |
|-------|----------|
| 向后兼容 | 旧 `.hmi.json` 必须可加载（FF2/FF7 跨阶段不变量） |
| 依赖方向 | UI 不可被 Core 引用（FF1 架构不变量） |
| 增量迁移 | 壳层优先，View 内部逐步迁移，保持 code-behind 兼容期 |
| 模块化优先 | 新功能作为独立模块加入，不拆分服务 |

## Decision

### R2-T1 层管理器：在 HmiControl 新增 IsLocked/IsVisible/LayerIndex

新增字段使用 C# 默认值（false/true/0），System.Text.Json 反序列化时缺失字段自动填默认值，保证旧项目兼容。

Z 序由 `LayerIndex` + 列表位置共同决定：排序时先按 LayerIndex，再按列表索引。不破坏现有 `Controls` 列表的追加语义。

### R2-T2 设计时预览：DesignCanvas 预览模式

DesignCanvas 新增 `IsPreviewMode` 状态。预览模式下：
- 禁用选中/拖拽/缩放手柄
- 绑定变量的控件渲染模拟值
- 复用 VariableSimulator 获取数据

预览模式是画布的视觉状态切换，不修改模型数据。

### R2-T3/R2-T4 资源管理器 + 图片优化

新增 `AssetLibrary` Core 模型和 `ImageOptimizer` Core 服务。AssetManagerView 作为 Dock 面板集成。图片优化为无副作用纯函数（输入字节→输出字节+元数据）。

### R2-T5 导航流设计器

新增 `NavigationGraph` Core 模型（节点=屏幕，边=跳转）。独立 JSON 文件存储，不侵入 `.hmi.json`。从控件的 SwitchScreen 动作自动提取边。

## Alternatives Considered

| Alternative | Rejection Reason |
|-------------|-----------------|
| 将 LayerIndex 存储为单独的 JSON 文件 | 增加文件管理复杂度；破坏单文件项目语义 |
| 预览模式创建独立窗口 | 偏离设计时上下文；用户需切换窗口 |
| 资源管理器内嵌到 .hmi.json | 导致项目文件膨胀；资源应可独立管理 |
| 微服务拆分新功能 | 单机编辑器无分布式需求；模块化足够 |

## Consequences

| Positive | Negative |
|----------|----------|
| 新字段默认值保证旧项目兼容 | HmiControl 模型持续膨胀（已 15+ 字段） |
| 模块化边界清晰，可独立测试 | Dock 面板数量增加，需管理布局空间 |
| 纯函数验证器/优化器无副作用 | 图片优化依赖平台图像库（需评估） |

## Reversibility

**Cost to undo**: 低 — 新字段为可选，移除不影响现有功能。新 View 可从 Dock 移除。
**Reconsideration trigger**: 如果 HmiControl 字段数超过 30，考虑引入属性字典或分组子模型。

## Fitness Functions

| Property | Metric | Threshold | Source | Cadence |
|----------|--------|-----------|--------|---------|
| 依赖方向 | UI→Core 引用数 | 0 | dotnet build | 每次提交 |
| 向后兼容 | 旧 .hmi.json 加载 | 100% 成功 | ProjectSerializerTests | 每次提交 |
| 层管理正确性 | 锁定控件不可选 | 100% 拦截 | LayerManagerTests | 每次提交 |
| 预览模式隔离 | 预览不修改模型 | 0 写操作 | PreviewModeTests | 每次提交 |
