# AI 辅助生成功能调研报告

> 调研日期: 2026-08-02
> 项目背景: HMI Editor — 嵌入式 HMI 人机界面设计工具,基于 Avalonia/WPF,目标平台 LVGL v9

---

## 1. 行业趋势全景

### 1.1 三种 AI 编程范式

| 范式 | 定义 | 典型场景 | 成熟度 |
|------|------|---------|--------|
| **Vibe Coding** | 自然语言描述需求,AI 直接生成代码,开发者复制/粘贴/调试 | 快速原型、简单页面、个人小工具 | ★★★★ (2025年已普及) |
| **Spec Coding** | 先写规格说明/PRD,再让 AI 按规格生成代码,质量可控 | 企业级功能模块、团队协作开发 | ★★★ (2026年快速增长) |
| **Agentic Coding** | AI Agent 自主完成端到端开发任务:规划→探索→编码→验证 | 复杂项目开发、全栈应用、持续集成 | ★★★ (2026年爆发元年) |

### 1.2 2026 年关键趋势

- **从"AI 辅助"到"AI 干活"**: AI Agent 不再只提供代码片段,而是自主完成完整任务链
- **完整代码生成取代碎片化**: 从单行补全演进到文件级、模块级、项目级生成
- **多Agent协作**: 规划Agent + 编码Agent + 审查Agent 协同工作
- **设计→代码无缝流转**: Figma/Sketch → 框架代码的自动化链路日趋成熟

### 1.3 主流 AI 编程工具对比 (2026年7月)

| 工具 | 市场份额 | 核心模式 | 关键特性 |
|------|---------|---------|---------|
| **Cursor** | 31% | Agent + 内联补全 | Composer 2.5 多文件编辑,上下文感知 |
| **GitHub Copilot** | 24% | Agent + 补全 | Copilot Workspace,PR 描述生成 |
| **Claude Code** | 增速+10% | Terminal Agent | 自主规划执行,工具调用链,长上下文 |
| **Trae** | 快速增长 | IDE + SOLO | Designer/Planner/Builder 多角色,设计画布 |
| **Windsurf** | 8% | IDE Agent | Cascade 流式推理,实时预览 |

---

## 2. Trae IDE 核心能力分析

### 2.1 双模式架构

**IDE 模式** (人机协作):
- 智能代码补全 (Inline completion)
- 上下文问答 (Chat with codebase)
- 代码解释/重构/文档生成
- 用户对开发过程有完全掌控

**SOLO 模式** (AI 自主开发):
- 多角色 Agent: Designer → Planner → Builder → Reviewer
- 自主工作流: 需求理解 → 计划制定 → 分支探索 → 编码实现 → 质量验证
- Design Canvas: 所见即所得的 UI 设计画布
- 设计→代码双向同步

### 2.2 Design Canvas (与 HMI Editor 最相关的特性)

- 自然语言描述 → 页面/界面原型自动生成
- 可视化设计画布,支持拖拽编辑
- 设计系统 (Design Library) 约束生成风格
- HTML/CSS 页面生成的完整链路
- 图片/图形资产生成 (Seedream 模型)

### 2.3 插件生态

| 插件 | 功能 | 对 HMI Editor 的启发 |
|------|------|---------------------|
| **Seedance** | AI 视频生成 | 操作演示视频、产品宣传 |
| **Seedream** | AI 图片生成 | 图标库生成、面盘背景图 |
| **Frontend Design** | 前端 UI 生成 | 设计→代码转换参考 |
| **Stark** | 跨平台 UI/UX 设计 | 嵌入式 UI 设计模式参考 |

---

## 3. HMI Editor 现有能力盘点

### 3.1 已有代码生成能力

| 模块 | 能力 | 技术实现 |
|------|------|---------|
| **CodeGenerator** | 项目 → LVGL C 代码 | Scriban 模板引擎,完整项目生成 |
| **SvgToLvglConverter** | SVG 矢量图 → lv_canvas 绘制代码 | 路径展平 + 像素缓冲生成 |
| **FaceplateExpander** | 面盘模板展开为具体控件 | 模板参数替换 + 递归展开 |
| **RuntimeTagsCodeGen** | 变量绑定 → 运行时标签表 C 代码 | 绑定引擎扫描全局变量引用 |
| **LvglApiWriter** | 控件属性 → LVGL API 调用 | 控件类型映射 + 属性序列化 |
| **ScriptSandboxExecutor** | Lua 脚本沙箱执行 | 嵌入式脚本引擎集成 |

### 3.2 已支持的生成模板

```
Templates/
├── lvgl_v9_main.scriban       # main() 入口
├── lvgl_v9_screen.scriban     # 屏幕创建函数
├── lvgl_v9_alarm.scriban      # 报警配置
├── lvgl_v9_trend.scriban      # 趋势图配置
├── lvgl_v9_recipe.scriban     # 配方管理
├── lvgl_v9_script.scriban     # 脚本绑定
└── lvgl_v9_user.scriban       # 用户管理
```

### 3.3 已有的设计能力

- **DesignCanvas** — 可视化面盘编辑器,支持拖拽/对齐/吸附
- **StyleEditor / StylePreset** — 主题样式编辑器与预设管理
- **PropertyPanel** — 属性面板,实时编辑控件属性
- **AssetManager** — 资源管理器 (SVG/图片)
- **PrototypeCanvas** — 原型画布,交互流程预览
- **Simulator** — 嵌入式运行时模拟器

---

## 4. AI 辅助生成机会点分析

### 4.1 【高价值】自然语言生成 HMI 屏幕

**场景**: 用户说 "创建一个温度监控界面,包含顶部状态栏、中间仪表盘区域、底部报警列表"

**现有基础**: CodeGenerator 已有完整的模型→代码管线
**需要新增**:
- NL → HmiScreen 模型 的 AI 推理层
- 控件类型/布局参数的结构化输出解析
- 变量绑定的智能推断 (温度标签→温度传感器变量)

**技术路线**:
- 方案 A: 调用 LLM API 直接输出 HmiScreen JSON → 反序列化到模型 → 走现有代码管线
- 方案 B: LLM 生成 Scriban 模板描述 → 解析为中间表示 → 注入到模型

### 4.2 【高价值】智能 Faceplate 面盘模板生成

**场景**: 用户描述一个复用组件 (如 "三相电机控制面板,带启停按钮和电流表"),AI 自动创建 Faceplate 模板

**现有基础**: FaceplateExpander + TemplateParameter 模型
**需要新增**:
- 自然语言 → TemplateParameter 列表的映射
- 控件组合布局的智能推荐
- 参数占位符的自动识别 (电流值、启停状态等)

### 4.3 【中价值】AI 辅助脚本生成

**场景**: 用户在 ScriptEditor 中编写 Lua 脚本时获得智能补全和生成

**现有基础**: LuaScriptValidator + LuaSnippets
**需要新增**:
- Lua 代码智能补全 (上下文感知)
- 自然语言 → Lua 脚本转换
- 脚本模板推荐 (基于当前绑定上下文)

### 4.4 【中价值】智能变量绑定建议

**场景**: 用户拖放控件后,AI 自动推荐合适的变量绑定

**现有基础**: RuntimeBindingEngine + ExpressionBindingEvaluator
**需要新增**:
- 基于控件类型的变量匹配算法
- 历史绑定模式学习
- 批量绑定推荐 (如一组温度控件自动匹配温度变量组)

### 4.5 【中价值】AI 驱动的 SVG 图标生成

**场景**: 用自然语言描述图标需求,生成 SVG 后走 SvgToLvglConverter

**现有基础**: SvgToLvglConverter 完整管线
**需要新增**:
- 集成 AI 图像生成 (Seedream 或类似服务)
- SVG 规范化/简化后处理 (适配嵌入式资源约束)
- 图标库批量生成能力

### 4.6 【低价值/长线】AI 布局优化建议

**场景**: 分析当前屏幕布局,给出可读性/可用性改进建议

**现有基础**: AdaptiveLayoutCalculator
**需要新增**:
- 布局质量评分模型
- HMI 设计最佳实践知识库
- 一键应用建议能力

### 4.7 【基础设施】内嵌 AI Chat Panel

**场景**: HMI Editor 内集成 AI 对话面板,覆盖所有辅助生成场景的统一入口

**实现方式**:
- 在 Dock 布局中新增 AI Chat 面板
- 支持上下文感知 (当前选中控件、当前屏幕)
- 统一调度上述所有 AI 辅助能力

---

## 5. 技术实现建议

### 5.1 架构分层

```
┌─────────────────────────────────────────┐
│          AI Chat Panel (UI)              │
│  自然语言输入 + 上下文预览 + 结果确认     │
├─────────────────────────────────────────┤
│       AI Orchestration Service           │
│  意图识别 → 能力路由 → 结果后处理         │
├─────────────────────────────────────────┤
│  NL→Screen  │ NL→Template │ NL→Script   │
│  Generator  │ Generator   │ Generator   │
├─────────────────────────────────────────┤
│          LLM Provider Adapter            │
│  支持 OpenAI / Claude / 本地模型 等       │
├─────────────────────────────────────────┤
│       Existing CodeGen Pipeline          │
│  CodeGenerator / FaceplateExpander / ... │
└─────────────────────────────────────────┘
```

### 5.2 推荐技术栈

- **AI 模型**: Claude API (推荐,代码生成能力最强) 或 OpenAI GPT-4o
- **Prompt 工程**: Spec Coding 模式,结构化输出 (JSON Schema)
- **后处理**: C# 层做输出校验 + 反序列化到现有模型类
- **部署**: 云 API 调用 + 本地 LLM 可选 (Ollama/Qwen 等)

### 5.3 分阶段落地路线

| 阶段 | 周期 | 目标 | 交付物 |
|------|------|------|--------|
| **Phase 1** | 2-3周 | AI Chat Panel 基础框架 + NL→Screen POC | 可对话生成简单单屏幕 |
| **Phase 2** | 3-4周 | NL→Faceplate Template 生成 | 自然语言创建复用组件 |
| **Phase 3** | 2-3周 | 智能变量绑定 + AI 脚本辅助 | Lua 补全、变量推荐 |
| **Phase 4** | 3-4周 | SVG 图标生成 + 布局优化 | 图标库 + 布局建议 |

---

## 6. 竞品参考

| 产品 | AI 辅助能力 | 与本项目的差异 |
|------|-----------|---------------|
| **LVGL SquareLine Studio** | 无原生 AI 能力 | 纯手动设计,正好是差异化切入点 |
| **Qt Design Studio** | 无原生 AI 能力 | 泛用桌面/移动 UI,不专注嵌入式 HMI |
| **Figma + 插件** | AI 设计变体/自动布局 | 通用设计工具,不生成嵌入式代码 |
| **Trae Design Canvas** | NL→页面生成 | 面向 Web,但方法论可借鉴 |

**结论**: HMI Editor 在 AI 辅助嵌入式 HMI 代码生成这一细分领域有明显的先发优势——现有代码管线完整,只需增加 NL→Model 这一层即可实现差异化竞争力。

---

## 7. 风险与注意事项

1. **生成代码质量**: 嵌入式环境资源受限,AI 生成的代码必须经过严格的资源估算(ResourceEstimator)和验证(GeneratedCodeValidator)
2. **LLM 幻觉**: 可能生成不存在的 LVGL API,需要后处理校验层
3. **成本控制**: API 调用成本,考虑本地模型作为降级方案
4. **用户信任**: 提供"预览→确认→生成"的非破坏性工作流,允许随时回退
5. **模板与 AI 的平衡**: 现有 Scriban 模板体系成熟可靠,AI 生成应与模板体系互补而非替代

---

> **调研结论**: AI 辅助生成是 HMI Editor 最值得投入的下一个重大功能方向。现有代码生成管线已成熟,只需在顶部增加 NL→结构化模型 的 AI 推理层,即可实现"说句话出 HMI 项目"的核心体验,在嵌入式 HMI 工具领域建立差异化竞争优势。
