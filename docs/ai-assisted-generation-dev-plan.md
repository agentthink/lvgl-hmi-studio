# AI 辅助生成功能 — 详细开发计划

> **状态**: Phase 1 + Phase 2 + Phase 3 + Phase 4 全部完成 ✅ (2026-08-02)
> **日期**: 2026-08-02
> **依赖**: 调研报告 `ai-assisted-generation-research.md`
> **预计总工期**: 10-14 周 (4 个 Phase 可部分并行)
> **补充**: 端到端管线测试层已完成 (2026-08-02) —— `FakeLlmProvider` + `AiPipelineEndToEndTests`(13 例),
> 覆盖 4 条生成链路的 Prompt 构建 → LLM 调用 → 解析 → 映射 → 校验全流程。全量回归 1024 测试。

> **实测**: 真实 LLM (glm-5.2 @ api.supxh.xin 中转) 四链路全通 (2026-08-02)。
> 实测中发现并修复 2 个真实 bug:
> 1. `LlmHttpProviderBase.SendWithRetryAsync` 成功路径响应被 `using var` 提前释放 → 改为所有权转移给调用方
> 2. 中转服务不支持含 `array` 类型 JSON Schema → `OpenAIProvider` 增加自动降级(json_schema → json_object)
> 全量回归 1032 测试。

---

## 总体架构决策

### ADR-AI-001: NL→Model 架构分层

**决策**: 新增 `HmiEditor.AI` 项目作为 AI 推理层，位于 Core 和 App 之间。AI 层仅依赖 Core，不被 Core 反向依赖。

```
┌──────────────────────────────────────────────┐
│  HmiEditor.App                               │
│  + Views/AiChatPanelView.axaml               │
│  + Services/AiContextProvider.cs             │
├──────────────────────────────────────────────┤
│  HmiEditor.AI  (NEW)                         │
│  + AiOrchestrator.cs                         │
│  + Generators/NlToScreenGenerator.cs         │
│  + Generators/NlToTemplateGenerator.cs       │
│  + Generators/NlToScriptGenerator.cs         │
│  + Inference/LlmProviderAdapter.cs           │
│  + Inference/PromptTemplates/                │
│  + Validation/AiOutputValidator.cs           │
├──────────────────────────────────────────────┤
│  HmiEditor.Core (不变)                        │
│  HmiEditor.CodeGen (不变)                     │
│  HmiEditor.Scripting (不变)                   │
└──────────────────────────────────────────────┘
```

**理由**: 
- 保持 FF1 架构不变量：UI 不侵入 Core
- AI 层可独立测试、独立替换模型提供商
- 与现有 CodeGen 管线松耦合

### ADR-AI-002: LLM Provider 策略

**决策**: 通过 `ILlmProvider` 统一接口支持多 Provider，配置文件驱动选择。内置三种 Provider:

| Provider | API 协议 | 适用场景 |
|----------|---------|---------|
| **ClaudeProvider** | Anthropic Messages API | 首选,结构化 JSON 输出最强,代码生成质量最高 |
| **OpenAIProvider** | OpenAI Chat Completions API | 备选,支持自定义 endpoint (Azure OpenAI / 本地代理 / 兼容服务) |
| **OllamaProvider** (Phase 4) | Ollama API | 离线场景,完全本地运行 (Qwen 等模型) |

**OpenAI 自定义 Endpoint 支持**:
- `Endpoint` 配置项支持替换为任意 OpenAI 兼容 API 地址 (如 `https://api.openai.com/v1` / `https://your-proxy.com/v1` / Azure OpenAI endpoint)
- `ApiKey` / `Model` 完全可配置
- 请求格式遵循标准 OpenAI Chat Completions API,与绝大多数第三方兼容服务互通

**理由**:
- 国内用户可通过代理/中转服务调用,无需直接访问海外 API
- Azure OpenAI 企业合规场景直接可用
- OpenAI 兼容协议生态广泛 (vLLM/LiteLLM/One API 等),降低锁定风险
- 接口抽象允许后续热切换

### ADR-AI-003: 非破坏性生成工作流

**决策**: AI 生成采用 "预览→确认→应用" 三步非破坏性工作流。生成的 HmiScreen / ControlTemplate 先写入临时 `AiPreviewCache`，用户在 DesignCanvas 中预览确认后才写入 HmiProject 模型。

**理由**:
- 保护用户数据：AI 输出不可信，用户必须有最终审批权
- 支持一键回退：未确认的生成结果不进 Undo 栈
- 与现有 UndoRedoManager 兼容：确认后走标准 Command 模式

---

## Phase 1: 基础设施 + NL→Screen POC (2-3 周)

> **状态: ✅ 已完成 2026-08-02**
> 实际交付: `HmiEditor.AI` 项目(7 文件)+ `HmiEditor.AI.Tests`(43 测试)+ App UI 集成(AI Chat Panel + 预览工作流)。
> 全量回归: 919 测试通过, Dock 渲染冒烟验证通过。

### 目标
搭建 `HmiEditor.AI` 项目骨架，打通"用户输入自然语言 → AI 生成 HMI 屏幕 → 在 DesignCanvas 中预览 → 一键应用"的完整链路。

### 任务分解

#### T1.1 创建 HmiEditor.AI 项目 + ILlmProvider 接口 (0.5 天)

**文件**:
- `src/HmiEditor.AI/HmiEditor.AI.csproj` — 类库项目, `.NET 10`, 无 Avalonia 依赖
- `src/HmiEditor.AI/Inference/ILlmProvider.cs` — LLM 抽象接口
- `src/HmiEditor.AI/Inference/LlmProviderConfig.cs` — 统一配置模型
- `src/HmiEditor.AI/Inference/LlmProviderFactory.cs` — 工厂,从配置创建 Provider

**接口定义**:
```csharp
namespace HmiEditor.AI.Inference;

/// <summary>LLM Provider 统一接口。所有 Provider 必须实现此接口。</summary>
public interface ILlmProvider
{
    /// <summary>Provider 标识名,如 "claude" / "openai" / "ollama"</summary>
    string ProviderName { get; }
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);
}

public record LlmRequest
{
    public List<LlmMessage> Messages { get; init; } = new();
    public double Temperature { get; init; } = 0.3;
    public int MaxTokens { get; init; } = 4096;
    public string? JsonSchema { get; init; }  // Structured Output 约束
}

public record LlmResponse
{
    public string Content { get; init; } = "";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}

public record LlmMessage
{
    public string Role { get; init; } = "user";  // system | user | assistant
    public string Content { get; init; } = "";
}

/// <summary>LLM Provider 统一配置,支持 Claude / OpenAI / 自定义 endpoint。</summary>
public class LlmProviderConfig
{
    /// <summary>Provider 类型: "claude" | "openai" | "ollama"</summary>
    public string Provider { get; set; } = "claude";

    /// <summary>API Key (Claude: x-api-key / OpenAI: Bearer token)</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>模型名称 (如 claude-sonnet-4-20250514 / gpt-4o / qwen2.5)</summary>
    public string Model { get; set; } = "";

    /// <summary>自定义 API Endpoint (OpenAI 兼容协议时使用)</summary>
    public string? Endpoint { get; set; }

    /// <summary>请求超时秒数</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
```

**配置示例 (appsettings.json)**:
```json
{
  "AI": {
    "Provider": "openai",
    "Model": "gpt-4o",
    "Endpoint": "https://api.openai.com/v1",
    "ApiKey": "sk-xxx",
    "TimeoutSeconds": 60
  }
}
```

**自定义 OpenAI 兼容 Endpoint 示例** (国内中转/代理):
```json
{
  "AI": {
    "Provider": "openai",
    "Model": "gpt-4o",
    "Endpoint": "https://your-openai-proxy.com/v1",
    "ApiKey": "sk-your-key"
  }
}
```

**Azure OpenAI 示例**:
```json
{
  "AI": {
    "Provider": "openai",
    "Model": "gpt-4o",
    "Endpoint": "https://your-resource.openai.azure.com/openai/deployments/your-deployment",
    "ApiKey": "your-azure-key"
  }
}
```

**验收标准**: `HmiEditor.AI.csproj` 编译通过, 接口 + 配置模型无编译错误。

---

#### T1.2 实现 Claude + OpenAI Provider (1.5 天)

**文件**:
- `src/HmiEditor.AI/Inference/ClaudeProvider.cs` — Anthropic Messages API
- `src/HmiEditor.AI/Inference/OpenAIProvider.cs` — OpenAI Chat Completions API (支持自定义 endpoint)

---

**ClaudeProvider 实现细节**:
- HTTP Client → `https://api.anthropic.com/v1/messages`
- 认证: `x-api-key` header
- Structured Output: 使用 Anthropic `tool_use` 的 `input_schema` 参数约束 JSON Schema
- 重试策略: 网络错误重试 2 次, 429 限速指数退避
- Token 统计: 从响应 `usage` 字段提取

---

**OpenAIProvider 实现细节** (支持自定义 endpoint):
- HTTP Client → 由 `LlmProviderConfig.Endpoint` 指定 (默认 `https://api.openai.com/v1`)
- 请求路径: `{Endpoint}/chat/completions` (完全兼容 OpenAI API 规范)
- 认证: `Authorization: Bearer {ApiKey}` header
- Structured Output: 使用 OpenAI `response_format: { type: "json_schema", json_schema: ... }` 参数
- 自定义 Endpoint 兼容性:
  - 国内中转/代理: 替换 Endpoint 为代理地址即可,请求格式不变
  - Azure OpenAI: Endpoint 指向 Azure 资源, ApiKey 使用 Azure Key
  - One API / vLLM / LiteLLM: 标准 OpenAI 协议,直接兼容

**Provider 注册逻辑 (LlmProviderFactory)**:
```csharp
public static ILlmProvider Create(LlmProviderConfig config)
{
    return config.Provider.ToLowerInvariant() switch
    {
        "claude" => new ClaudeProvider(config),
        "openai" => new OpenAIProvider(config),
        _ => throw new NotSupportedException($"Provider '{config.Provider}' not supported")
    };
}
```

**验收标准**:
- ClaudeProvider 调用真实 API 返回合法 JSON
- OpenAIProvider 调用自定义 endpoint 返回合法 JSON
- 两个 Provider 对同一 Prompt 的输出均能通过 AiOutputValidator

---

#### T1.3 实现 HMI Screen 的 Prompt 模板 (1 天)

**文件**:
- `src/HmiEditor.AI/Inference/PromptTemplates/ScreenGenerationSystem.txt` — System Prompt
- `src/HmiEditor.AI/Inference/PromptTemplates/ScreenGenerationUser.txt` — User Prompt 模板

**System Prompt 核心内容**:
```
你是一个 LVGL v9 嵌入式 HMI 界面生成器。根据用户描述，输出结构化的屏幕定义 JSON。

## 支持的控件类型 (HmiControlType)
Button, Label, Slider, Switch, CheckBox, Image, Bar, Dropdown, TextArea,
Gauge, Knob, Keyboard, Led, Spinner, TabView, Line, Table, Svg

## 控件关键属性
- Id: 唯一标识 (8 位 hex, 自动生成)
- Name: 控件显示名
- Type: 控件类型 (见上)
- X, Y, Width, Height: 位置和尺寸 (整数)
- Text: 文本内容 (Label/Button)
- BgColor: 背景色 (HEX #RRGGBB)
- TextColor: 文字色 (HEX #RRGGBB)
- Radius: 圆角半径 (整数, 默认 4)
- VariableBindingId: 绑定的变量名 (如 "temperature_1")

## 布局规则
- 屏幕默认 800x480 (标准 7" HMI 分辨率)
- 顶部状态栏高度 40px，底部导航栏高度 50px
- 控件间距至少 8px
- 颜色使用工业风格：深色背景 (#1a1a2e) + 高对比文字 (#e0e0e0)

## 输出格式
严格按照 JSON Schema 输出，不要包含任何额外文字或 markdown 代码块。
```

**User Prompt 模板** (替换 `{user_input}`):
```
请根据以下描述创建 HMI 屏幕：
{user_input}

项目中已有的变量列表（供参考，用于 VariableBindingId）：
{variable_list}

当前项目主题信息：
{theme_info}
```

**JSON Schema** (作为 Structured Output 约束):
```json
{
  "type": "object",
  "properties": {
    "screen": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "width": { "type": "integer", "default": 800 },
        "height": { "type": "integer", "default": 480 },
        "bgColor": { "type": "string" },
        "controls": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "type": { "type": "string", "enum": ["Button","Label","Slider","Switch","CheckBox","Image","Bar","Dropdown","TextArea","Gauge","Knob","Keyboard","Led","Spinner","TabView","Line","Table","Svg"] },
              "name": { "type": "string" },
              "x": { "type": "integer" },
              "y": { "type": "integer" },
              "width": { "type": "integer" },
              "height": { "type": "integer" },
              "text": { "type": "string" },
              "bgColor": { "type": "string" },
              "textColor": { "type": "string" },
              "radius": { "type": "integer" },
              "variableBindingId": { "type": "string" }
            },
            "required": ["type", "name", "x", "y", "width", "height"]
          }
        }
      },
      "required": ["name", "controls"]
    }
  }
}
```

**验收标准**: 手动测试 — 输入 "创建一个温度监控界面,带顶部状态栏、中间的仪表盘区域、底部报警按钮",AI 返回符合 Schema 的有效 JSON。

---

#### T1.4 实现 NL→Screen 生成器 (1 天)

**文件**:
- `src/HmiEditor.AI/Generators/NlToScreenGenerator.cs`
- `src/HmiEditor.AI/Models/AiGeneratedScreen.cs` — 中间模型,对应 JSON Schema 输出

**核心逻辑**:
```csharp
public class NlToScreenGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public async Task<HmiScreen> GenerateAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default)
    {
        // 1. 构建上下文: 变量列表 + 主题信息
        var context = BuildContext(projectContext);

        // 2. 渲染 System + User Prompt
        var messages = _prompts.BuildMessages(userInput, context);

        // 3. 调用 LLM, 指定 JSON Schema 约束输出
        var response = await _llm.ChatAsync(new LlmRequest
        {
            Messages = messages,
            JsonSchema = ScreenGenerationSchema.Json,
            Temperature = 0.2
        }, ct);

        // 4. 反序列化 JSON → AiGeneratedScreen
        var generated = JsonSerializer.Deserialize<AiGeneratedScreen>(response.Content);

        // 5. AiGeneratedScreen → HmiScreen 映射
        //    为每个控件生成 Id, 设置默认样式属性
        return MapToHmiScreen(generated);
    }
}
```

**验收标准**: 单元测试 — 给定 mock LLM 响应, 正确生成 HmiScreen 对象, 所有控件属性正确映射。

---

#### T1.5 实现 AI 输出验证器 (0.5 天)

**文件**:
- `src/HmiEditor.AI/Validation/AiOutputValidator.cs`

**验证规则**:
1. **控件类型白名单**: 所有控件 Type 必须在 `HmiControlType` 枚举中
2. **坐标边界检查**: X/Y ≥ 0, X + Width ≤ ScreenWidth, Y + Height ≤ ScreenHeight
3. **最小尺寸**: Width/Height ≥ 1
4. **重叠警告**: 检查控件间是否有严重重叠（轻微重叠允许,但报警告）
5. **BG Color 格式**: 符合 `#RRGGBB` 或 `#RRGGBBAA` 格式
6. **VariableBindingId 存在性**: 引用的变量在 Project.VariableManager 中存在（仅警告,不阻止）

**验收标准**: 单元测试覆盖所有验证规则。

---

#### T1.6 实现 AI 编排器 (0.5 天)

**文件**:
- `src/HmiEditor.AI/AiOrchestrator.cs`

**核心逻辑**:
```csharp
public class AiOrchestrator
{
    private readonly Dictionary<string, IAiGenerator> _generators;

    public async Task<AiOrchestrationResult> ProcessAsync(
        string userInput,
        HmiProject projectContext,
        CancellationToken ct = default)
    {
        // 1. 意图分类 (simple keyword-based for Phase 1)
        var intent = ClassifyIntent(userInput);

        // 2. 路由到对应生成器
        var generator = _generators[intent];
        var result = await generator.GenerateAsync(userInput, projectContext, ct);

        // 3. 验证输出
        var validation = AiOutputValidator.Validate(result);

        // 4. 返回带验证结果的结构
        return new AiOrchestrationResult
        {
            GeneratedScreen = result,
            Validation = validation,
            InputTokens = ...,
            OutputTokens = ...
        };
    }
}
```

**验收标准**: 集成测试 — 端到端从用户输入到验证后的 HmiScreen。

---

#### T1.7 AI Chat Panel UI (2 天)

**文件**:
- `src/HmiEditor.App/Views/AiChatPanelView.axaml` + `.axaml.cs`
- `src/HmiEditor.App/Services/AiContextProvider.cs`

**UI 设计**:
```
┌─────────────────────────┐
│ 🤖 AI 助手              │
├─────────────────────────┤
│                         │
│  [对话消息区域]           │
│  User: 创建温度监控...    │
│  AI: 已生成屏幕 "温度监控", │
│  包含 3 个控件。           │
│  [预览] [应用] [取消]     │
│                         │
├─────────────────────────┤
│ [输入框____________] [发送]│
│ [当前上下文: Screen-1 ✓]   │
└─────────────────────────┘
```

**集成到 Dock 布局**:
- 在 `HmiDockFactory` 的 RightDock 中新增 `aiChatTool`
- 标题: "AI 助手"
- 注册到 `HmiDockFactory` 构造函数

**上下文感知**:
- `AiContextProvider` 提供当前选中屏幕/控件的上下文
- 对话消息附加上下文数据（变量列表、主题信息）
- 当前屏幕名称显示在输入框下方

**验收标准**: 启动应用 → AI 面板可见 → 输入自然语言 → 显示生成的屏幕预览 → "应用"按钮将屏幕加入项目。

---

#### T1.8 预览机制集成 (1 天)

**文件**:
- `src/HmiEditor.App/Services/AiPreviewManager.cs`

**逻辑**:
1. AI 生成 HmiScreen 后写入 `_previewCache`
2. DesignCanvas 接收 `OnPreviewScreenGenerated` 消息,切换显示预览屏幕
3. 预览屏幕使用特殊视觉标识（虚线边框 + "AI 生成预览"水印）
4. 用户点击"应用" → UndoRedoManager 执行 `AddScreenCommand` → 刷新画布
5. 用户点击"取消" → 清除预览缓存,恢复原画布

**验收标准**: 生成→预览→确认→应用 完整流程,Undo 可回退。

---

### Phase 1 验收标准汇总

| 检查项 | 标准 |
|--------|------|
| 项目结构 | `HmiEditor.AI` 项目创建,无编译错误 |
| LLM 调用 | ClaudeProvider 成功返回合法 JSON |
| NL→Screen | 输入简单描述,生成有效 HmiScreen(3+ 控件) |
| 验证器 | 拒绝非法控件类型/越界坐标 |
| UI 面板 | AI Chat Panel 在 Dock 中可见可用 |
| 端到端 | 自然语言 → 预览 → 应用 全流程通过 |
| 回归测试 | 现有 dotnet test 全绿 |

---

## Phase 2: NL→Faceplate 模板生成 (3-4 周)

> **状态: ✅ 已完成 2026-08-02**
> 实际交付: `NlToTemplateGenerator` + 模板 Prompt/Schema + `AiOutputValidator.ValidateTemplate` + AI Chat Panel 模板意图 + Faceplate 库 AI 按钮 + `AiTemplateDialog`。
> 全量回归: 943 测试通过。

### 目标
支持用户用自然语言描述"三相电机控制面板"等复用组件,AI 自动创建 Faceplate 模板(含参数接口定义)。

### 任务分解

#### T2.1 Faceplate Template Prompt 模板 (1 天)

**文件**:
- `src/HmiEditor.AI/Inference/PromptTemplates/TemplateGenerationSystem.txt`
- `src/HmiEditor.AI/Inference/PromptTemplates/TemplateGenerationUser.txt`

**核心 Prompt 设计思路**:
- System Prompt: 强调"参数化复用"概念,要求 AI 识别可变部分并声明为参数
- 参数类型: Text(标签文本)、Value(数值)、Color(颜色)、Toggle(布尔)、Binding(变量绑定)

**JSON Schema** (模板生成输出):
```json
{
  "type": "object",
  "properties": {
    "template": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "description": { "type": "string" },
        "category": { "type": "string", "enum": ["通用","电机控制","温控","阀门","泵","仪表","自定义"] },
        "controls": { "type": "array", "items": { "$ref": "#/definitions/control" } },
        "parameters": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "name": { "type": "string" },
              "displayName": { "type": "string" },
              "type": { "type": "string", "enum": ["Text","Number","Color","Boolean","Binding"] },
              "defaultValue": { "type": "string" }
            },
            "required": ["name","displayName","type","defaultValue"]
          }
        }
      },
      "required": ["name","controls","parameters"]
    }
  }
}
```

**验收标准**: 输入 "三相电机控制面板,带启停按钮和电流表",AI 输出包含 3+ 参数(电机名称、启动/停止绑定 ID)的有效 JSON。

---

#### T2.2 NL→Template 生成器 (1.5 天)

**文件**:
- `src/HmiEditor.AI/Generators/NlToTemplateGenerator.cs`
- `src/HmiEditor.AI/Models/AiGeneratedTemplate.cs`

**核心逻辑**:
1. 调用 LLM 获取模板定义 + 参数列表
2. 在控件属性中应用参数占位符: `"{{Parameters.motor_name}}"`
3. 创建 `ControlTemplate` 实例,`Parameters` 包含参数接口定义
4. 调用 `FaceplateExpander.ValidateTemplate()` 做基础校验

**关键挑战**: 控件布局合理性。AI 经常把控件堆在一起,需要后处理逻辑:
- 水平排列: Button 并排间距 12px
- 垂直排列: Label + Value 上下间距 4px
- 标签-数值组合: Label 在 Value 上方 24px

**验收标准**: 单元测试 — 模拟 LLM 响应, 生成包含正确参数占位符的 ControlTemplate。

---

#### T2.3 模板预览与调优 UI (2 天)

**文件**:
- `src/HmiEditor.App/Views/FaceplateTemplateEditorView.axaml` — 新增 AI 生成 Tab
- `src/HmiEditor.App/Services/AiTemplatePreviewService.cs`

**交互设计**:
- Faceplate 库面板新增 "🤖 AI 生成" 按钮
- 弹出对话框: 输入描述 → 生成 → 预览模板 → 编辑参数 → 确认入库
- 生成的模板可直接拖放到画布测试实例化效果

**验收标准**: 用户在 Faceplate 库中点击 AI 生成 → 描述"水泵控制面板" → 模板出现在库中 → 拖放到画布可正常使用。

---

#### T2.4 Phase 2 集成测试 (1 天)

**测试用例**:
1. 电机控制面板: 2 个 Button + 1 个 Gauge, 3 个参数
2. PID 温控器面板: Label + Slider + Led, 4 个参数
3. 阀门状态面板: Switch + Label + Led, 2 个参数
4. 异常输入: 空描述 → 返回友好错误
5. 变量绑定: 参数类型 Binding 自动关联项目变量

---

## Phase 3: 智能变量绑定 + AI 脚本辅助 (2-3 周)

> **状态: ✅ 已完成 2026-08-02**
> 实际交付: `VariableMatchService` 推荐引擎 + VariablePicker 推荐区 + `NlToScriptGenerator` + 脚本 Prompt/Schema + `AiOutputValidator.ValidateScript` + `LuaCompletionService` + ScriptEditorView「AI 生成」按钮 + `AiScriptDialog`。
> 全量回归: 984 测试通过。

### 目标
AI 辅助变量绑定推荐和 Lua 脚本生成/补全。

### 任务分解

#### T3.1 变量绑定推荐引擎 (2 天)

**文件**:
- `src/HmiEditor.AI/Services/VariableMatchService.cs`
- `src/HmiEditor.AI/Models/VariableMatchResult.cs`

**核心算法** (不需要 LLM,基于规则+相似度):

```
推荐流程:
1. 控件类型 → 变量类型映射:
   Gauge/Bar/Slider → 数值型变量
   Switch/CheckBox/Led → 布尔型变量
   Label → 任意类型(显示用)

2. 命名相似度匹配:
   控件 Name "温度显示" → 匹配变量名包含 "temperature" / "温度" / "temp"
   使用 Levenshtein 距离 + 关键词提取

3. 历史绑定模式:
   如果项目中 Label("压力值") 已绑定 "pressure_1",
   则新的 Label("压力上限") 推荐 "pressure_1" 或其他 pressure_* 变量

4. 批量推荐:
   选中一组控件 → 批量匹配最佳变量
```

**验收标准**: 选中 Gauge 控件 → 属性面板显示推荐的数值型变量 Top 3。

---

#### T3.2 变量绑定 UI 集成 (1 天)

**文件**:
- `src/HmiEditor.App/Views/Editors/VariablePicker.axaml` — 增强推荐区域

**UI 变化**:
- VariablePicker 顶部新增推荐区域: "AI 推荐绑定: [pressure_main ★95%] [pressure_aux ★72%]"
- 推荐理由 tooltip: "基于控件名称 '压力显示' 匹配"
- 一键应用推荐

---

#### T3.3 AI Lua 脚本生成器 (2 天)

**文件**:
- `src/HmiEditor.AI/Generators/NlToScriptGenerator.cs`
- `src/HmiEditor.AI/Inference/PromptTemplates/ScriptGenerationSystem.txt`

**System Prompt 关键内容**:
```
你是 HMI Lua 脚本生成器。输出标准的 LVGL HMI Lua 脚本。

## 可用 API
- hmi_read(variable_name) → number | boolean
- hmi_write(variable_name, value)
- hmi_set_visible(control_id, visible)
- hmi_set_text(control_id, text)
- hmi_set_color(control_id, color_hex)
- print(message)

## 脚本模板 (ScriptBinding):
- FunctionName: 函数名
- TriggerType: OnClick | OnValueChanged | OnLoad | OnTimer
- TriggerControlId: 触发控件 ID
- IntervalMs: 定时器间隔 (TriggerType=OnTimer 时)

## 输出规则
- 函数命名: on_{trigger}_{target} (如 on_click_start_button)
- 包含中文注释
- 变量引用使用项目中已存在的变量名
```

**验收标准**: 输入 "当温度超过 80 度时,LED 指示灯变红并弹出报警",输出有效 Lua 脚本 + ScriptBinding。

---

#### T3.4 Lua 脚本补全 (1.5 天)

**文件**:
- `src/HmiEditor.AI/Services/LuaCompletionService.cs`

**实现方式**:
- 基于 `LuaSnippets` 模板库 + 上下文感知
- 用户输入 `hmi_` → 补全 `hmi_read(` / `hmi_write(` / `hmi_set_visible(`
- 用户输入 `on_` → 补全事件函数模板
- 不是实时持续补全,而是用户按 Tab 或 Ctrl+Space 触发

**验收标准**: ScriptEditor 中输入 `hmi_` → 弹出补全列表 → 选择后插入完整函数调用模板。

---

## Phase 4: SVG 图标生成 + 布局优化 (3-4 周)

> **状态: ✅ 已完成 2026-08-02**
> 实际交付: `LayoutAnalyzer` 规则分析器(5 维度评分) + PropertyPanel「💡 AI 建议」区 + 一键修复 + `IconGenerator` + `BatchIconGenerator` + 图标 Prompt + AssetManager「✨ AI 图标」按钮 + `AiIconDialog`。
> 全量回归: 1011 测试通过。

### 目标
AI 生成 SVG 图标并走 SvgToLvglConverter 管线,AI 分析布局质量给出改进建议。

### 任务分解

#### T4.1 AI SVG 图标生成器 (2 天)

**文件**:
- `src/HmiEditor.AI/Generators/IconGenerator.cs`
- `src/HmiEditor.AI/Inference/PromptTemplates/IconGenerationSystem.txt`

**实现策略**:
- 优先使用 Seedream 图片生成 → 后处理转 SVG 简化版
- 备选: LLM 直接生成 SVG path (效率高但质量参差)

**SVG 后处理管线**:
```
Seedream/LLM → 原始 SVG
→ SvgParser.Parse() (复用 Core.SvgParser)
→ 路径简化 (减少控制点,适配嵌入式)
→ 尺寸规范化 (48x48 或 64x64)
→ SvgToLvglConverter.GenerateCanvasCode()
→ 导入 AssetLibrary
```

**验收标准**: 输入 "水泵图标" → 生成简化的 SVG → 显示在 AssetManager → 拖放到画布正常渲染。

---

#### T4.2 图标批量生成 (1 天)

**文件**:
- `src/HmiEditor.AI/Generators/BatchIconGenerator.cs`

**功能**:
- 输入: 图标描述列表 (CSV 或手动输入)
- 输出: 批量导入到 AssetManager
- 进度条 + 取消支持

**典型场景**: 用户一次性导入 "阀门、电机、泵、传感器、温度计、压力表、液位计、流量计" 8 个图标。

---

#### T4.3 AI 布局优化分析 (2 天)

**文件**:
- `src/HmiEditor.AI/Services/LayoutAnalyzer.cs`
- `src/HmiEditor.AI/Models/LayoutAnalysisResult.cs`

**分析维度** (不依赖 LLM,基于规则):
1. **对齐检测**: 控件是否有水平/垂直对齐偏差 (偏离对齐线 3px 以上)
2. **间距均匀性**: 同类控件间距是否一致
3. **安全边距**: 控件是否离屏幕边缘太近 (< 8px)
4. **视觉层次**: 主要控件是否足够大 (至少占屏幕 15%)
5. **颜色对比度**: 文字与背景对比度是否达到 WCAG AA

**输出**:
```json
{
  "score": 72,
  "issues": [
    { "severity": "warning", "message": "按钮 '启动' 和 '停止' 水平未对齐, 偏差 5px", "autoFix": true },
    { "severity": "info", "message": "建议为仪表盘控件增加变量绑定", "autoFix": false }
  ]
}
```

**验收标准**: 选中屏幕 → 点击 "分析布局" → 显示评分 + 问题列表 → "自动修复"按钮修正对齐。

---

#### T4.4 PropertyPanel 集成布局建议 (1 天)

**文件**:
- `src/HmiEditor.App/Views/PropertyPanelView.axaml` — 新增 "AI 建议" 区域

**UI**: PropertyPanel 新增可折叠的 "💡 AI 建议" 区域:
- 选中控件时显示针对该控件的建议
- 选中屏幕时显示整体布局分析结果
- "一键修复"按钮应用可自动修复的建议

---

## 完整项目结构变更

### 新增项目

```
src/HmiEditor.AI/
├── HmiEditor.AI.csproj
├── AiOrchestrator.cs                    # AI 编排器
├── Inference/
│   ├── ILlmProvider.cs                  # LLM 抽象接口
│   ├── LlmProviderConfig.cs             # 统一配置模型 (支持 Claude/OpenAI/自定义 endpoint)
│   ├── LlmProviderFactory.cs            # Provider 工厂
│   ├── ClaudeProvider.cs                # Anthropic Messages API 实现
│   ├── OpenAIProvider.cs               # OpenAI Chat Completions API (支持自定义 endpoint)
│   └── PromptTemplates/
│       ├── ScreenGenerationSystem.txt
│       ├── ScreenGenerationUser.txt
│       ├── TemplateGenerationSystem.txt
│       ├── TemplateGenerationUser.txt
│       ├── ScriptGenerationSystem.txt
│       ├── ScriptGenerationUser.txt
│       └── IconGenerationSystem.txt
├── Generators/
│   ├── IAiGenerator.cs                  # 生成器接口
│   ├── NlToScreenGenerator.cs
│   ├── NlToTemplateGenerator.cs
│   ├── NlToScriptGenerator.cs
│   ├── IconGenerator.cs
│   └── BatchIconGenerator.cs
├── Models/
│   ├── AiGeneratedScreen.cs
│   ├── AiGeneratedTemplate.cs
│   ├── AiGeneratedScript.cs
│   └── LayoutAnalysisResult.cs
├── Services/
│   ├── VariableMatchService.cs
│   ├── LuaCompletionService.cs
│   └── LayoutAnalyzer.cs
└── Validation/
    └── AiOutputValidator.cs
```

### 修改现有项目

```
src/HmiEditor.App/
├── Views/
│   └── AiChatPanelView.axaml           # NEW
├── Services/
│   ├── HmiDockFactory.cs               # MODIFY: 添加 AiChat 面板
│   ├── AiContextProvider.cs             # NEW
│   ├── AiPreviewManager.cs             # NEW
│   └── AiTemplatePreviewService.cs     # NEW
├── App.axaml.cs                        # MODIFY: DI 注册 AI 服务
└── Views/Editors/
    └── VariablePicker.axaml             # MODIFY: 推荐区域

src/HmiEditor.Core/
└── Models/
    └── HmiProject.cs                    # MODIFY: +AiGenerationHistory
```

---

## 测试策略

### 单元测试 (每个 Generator)

| 测试类型 | 内容 | 工具 |
|----------|------|------|
| LLM Mock 测试 | 固定 JSON 响应,验证生成器反序列化+映射逻辑 | xUnit + Moq |
| 验证器测试 | 覆盖所有验证规则(白名单/边界/冲突) | xUnit |
| Prompt 渲染测试 | 验证 Prompt 模板正确注入变量列表/主题 | xUnit |

### 集成测试

| 测试类型 | 内容 |
|----------|------|
| Claude API 真实调用 | 端到端测试 NL→Screen (需 API Key,标记 `[Integration]`) |
| 预览-应用流程 | AiPreviewManager + UndoRedoManager 协作 |
| Dock 布局完整性 | AI 面板不影响现有布局行为 |

### 回归测试

- 运行全部 `dotnet test` 确保现有 40+ 测试套件全绿
- 特别关注: `CodeGenTests`, `FaceplateIntegrationTests`, `UndoRedoTests`

---

## 风险缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| LLM API 不稳定/限流 | 中 | 高 | 本地模型降级(Ollama+Qwen), 客户端缓存常见生成请求 |
| 生成的 LVGL API 不存在 | 中 | 中 | AiOutputValidator 校验 API 白名单; 生成后走 CodeGen 管线做编译检验 |
| AI 面板挤占 Dock 空间 | 低 | 低 | 默认隐藏,用户主动打开; 可折叠/浮动面板 |
| 生成屏幕控件过多(50+) | 低 | 中 | Prompt 中限制最大控件数(20); 超过阈值警告用户 |
| 用户过度依赖 AI 跳过学习 | 中 | 低 | 生成结果附带"设计说明"解释控件选择和布局理由 |

---

## 里程碑与交付物

| 里程碑 | 时间 | 交付物 |
|--------|------|--------|
| M1: Phase 1 完成 | 第 3 周 | NL→Screen 端到端可用,AI Chat Panel 集成 |
| M2: Phase 2 完成 | 第 7 周 | NL→Template 可用,模板库中可 AI 生成 |
| M3: Phase 3 完成 | 第 10 周 | 变量推荐 + Lua 补全/生成 |
| M4: Phase 4 完成 | 第 14 周 | SVG 图标生成 + 布局优化分析 |

---

> **下一步**: 确认 Phase 1 任务分解和工期估算, 开始 T1.1 创建 `HmiEditor.AI` 项目骨架。
