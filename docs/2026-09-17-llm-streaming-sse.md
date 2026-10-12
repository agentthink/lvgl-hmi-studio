# LLM 流式响应（SSE）：从「Provider 只会等」到「正文逐段可见」

> 对应规划文档 `docs/2026-09-14-optimization-directions.md` 的 **P2-5 / D9**
> （原文：「**AI 无流式响应** —— `src/HmiEditor.AI` grep `stream` **0 命中**」；
> 完成标准：*长任务过程中 UI 有逐步轨迹；取消可中断*）。
> 关联：`docs/2026-09-15-ai-cancellation-contract.md`（取消链路，本轮之前已闭环）。
> 日期：2026-09-17

---

## 0. 一句话结论

`ILlmProvider` 只有 `ChatAsync`（请求—完整响应），于是**一次生成从点下按钮到出结果之间，
界面是死的**：Agent 多步任务至少还有工具步骤在动，而「生成屏幕/模板/脚本/图片」是**单次长调用**
（复杂画面 30~120 s），全程只有一条静止的「正在生成, 请稍候…」。

本轮把 SSE 增量打通到 UI：三个新类型（`SseReader` / `IStreamingLlmProvider` / `LlmChat`）、
两个 Provider 的真流式实现、引擎与生成器接线、面板实时预览。**默认行为逐字不变**
（不传观察者就不走 SSE），流式是**能力**而非契约。

---

## 1. 归因：为什么之前一行 `stream` 都没有

不是"忘了写"，而是**接口形状决定了写不出来**：

```csharp
public interface ILlmProvider
{
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);
}
```

返回的是**完整响应**，中途没有任何出口。要流式，就得往这条链路上开一个"增量回调"的口子，
而口子开在哪一层、用什么形状，是这个改动真正的设计内容。

### 1.1 三处必须一起动（只动一处就是半成品）

| 层 | 缺口 |
|---|---|
| 协议 | 没有 SSE 切帧；OpenAI / Claude 两套流式事件形状无人解析 |
| 调用 | 生成器与 Agent 引擎只认 `ChatAsync`，拿到回调也没地方转 |
| 呈现 | 面板只有"结果气泡"，没有"运行中正文"的位置 |

---

## 2. 做法

### 2.1 能力接口，而不是给主接口加成员

```csharp
/// 可选的流式能力接口 —— 支持 SSE 增量输出的 Provider 实现它。
public interface IStreamingLlmProvider : ILlmProvider
{
    Task<LlmResponse> ChatStreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken ct = default);
}
```

**为什么不加进 `ILlmProvider`：** 流式是能力不是契约。加进主接口意味着每个实现
（含测试替身、将来的本地模型 / Ollama）都被迫提供一份"假装流式"的桩，
而调用方**无法区分真流式与桩**。独立接口让能力可探测：`provider is IStreamingLlmProvider`。

### 2.2 切帧与语义分离：`SseReader`

SSE 的行语法（`data:` / `event:` / 空行分帧 / 注释 / 多行 data 拼接）是**协议层**的事，
与"哪个字段是正文、哪个是 token 用量"无关。两个 Provider 各写一份切帧代码的必然结局是分叉
—— 项目里已有多处"同一规则两份实现后漂移"的历史（阶段 6 的收帧判据、阶段 9 的回退数值）。
`SseReader` 只切帧，语义留给调用方。

刻意容忍三种现实：① 部分中转不补帧尾空行（流结束时把残留 data 也交出去）；
② 心跳/注释行；③ 无冒号的坏字段行（按空值处理，**一行坏数据不该让整次生成失败**）。

### 2.3 单一调用入口：`LlmChat.WithProgressAsync`

```csharp
if (textProgress is not null && provider is IStreamingLlmProvider streaming)
    return await streaming.ChatStreamAsync(request, d => textProgress.Report(d), ct);

var response = await provider.ChatAsync(request, ct);
if (textProgress is not null && response.Content.Length > 0)
    textProgress.Report(response.Content);   // ← 不变量：非流式也必须回调一次
```

**不变量：只要传了观察者，无论 Provider 支不支持流式，都至少收到一次回调。**
没有这条，调用方就得为"真流式/假流式"写两套 UI 逻辑；有了它，UI 只认一种事件。

**为什么必须收口成一处：** 「支不支持流式 → 走哪条路 → 非流式补一次回调」是三条独立判据。
任何调用方自己写一遍，都会在有人新增 Provider 时出现"Agent 走流式、画布生成却不走"这类分叉
—— 而分叉的表现是**界面时灵时不灵**，不是编译错误。

### 2.4 两个 Provider 的流式实现

| Provider | 请求 | 正文来源 | 用量来源 |
|---|---|---|---|
| OpenAI | `stream: true` + `stream_options.include_usage` | `choices[0].delta.content` | 末帧 `usage` |
| Claude | `stream: true` | `text_delta`；**结构化输出时取 `input_json_delta`** | `message_start` / `message_delta` |

**Claude 那处是最容易写错的**：结构化输出走 `tool_use`，此时模型几乎不输出 text block，
正文全在 `input_json_delta` 的分片里。只取 text block 会让**生成结果恒为空**
（且不报错）。测试专门钉住这条。

**`stream_options.include_usage` 是刻意加的**：非流式响应总带 usage，流式默认不带 ⇒
不加会让 UI 的 token 消耗读数在切到流式后凭空消失。

### 2.5 降级：三重现实与一条边界

OpenAI 侧沿用既有的三层结构模式降级（`json_schema → json_object → 无约束`），
因为中转对 `response_format` 支持不一；流式同样需要。**边界**：

- **降级只可能发生在响应体尚未读取时** —— HTTP 状态先于正文到达，
  `SendWithRetryAsync` 在非 2xx 时直接抛出，此时一个增量都没回调过 ⇒ 重试不会重复吐字。
- **流中途失败（200 之后）带不上 HTTP 状态码** ⇒ 天然不被降级捕获 ⇒ 只上抛。
  这是刻意的：换形态重试会把已经显示出去的正文**再吐一遍**。
- 中转过在流中途发 `{"error":…}` 帧（OpenAI 系）或 `event: error`（Claude）时，
  显式抛 `LlmProviderException` 并**保留已回调的增量**，不重试。
- 所有结构模式都被拒 ⇒ 判定该通道不接受流式 ⇒ 退回非流式并把完整内容作为**单次增量**回调。
- **鉴权错误（401/403）不触发回退**：换请求形态必然再失败，徒增一次请求（有测试钉住）。

### 2.6 界面：运行中气泡的实时预览

`ChatEntry` 新增 `LivePreview` / `StreamingStatus` / `IsStreaming`。设计要点：

1. **预览只显示尾部 600 字**：一轮可能吐几十 KB（整屏 JSON），全文塞进一个
   `SelectableTextBlock` 只会拖慢布局。
2. **节流重绘 120 ms**：`ChatEntry` 无 INPC，刷新靠重挂 `ItemsSource`
   （`ForceRefreshMessages`）—— 逐字调用会把 UI 拖垮。
3. **工具步骤到达时撤掉预览**：那意味着该轮输出已解析完，原始 JSON 继续挂着会与步骤卡片重复。
4. **`finally` 兜底清空**：最终正文进 `Content`，预览只是"还在动"的证据。

---

## 3. 覆盖（新增 42 项测试）

| 文件 | 项数 | 覆盖 |
|---|---|---|
| `SseReaderTests` | 14 | 分帧、多行 data、注释、`[DONE]`、CRLF、缺尾空行、坏字段行、UTF-8，**以及"不必等流结束"的流式性证明** |
| `ProviderStreamingTests` | 14 | OpenAI/Claude 增量顺序与拼接、用量、请求带 `stream`、降级不重复、401 不重试、坏帧跳过、空 delta 跳过、流中途报错不重试、取消传播 |
| `LlmChatTests` | 5 | 四条不变量（流式转发 / 非流式补一次 / 无观察者不走 SSE / 空正文不回调 / null 参数） |
| `AgentEngineStreamingTests` | 4 | 引擎走流式、非流式 Provider 仍回调、无观察者走非流式、流式失败不静默吞 |
| `AiStreamingWiringTests` | 5 | App 层接线静态守卫（4 条生成分支 + Agent 调用 + 转发不断链）+ `ChatEntry` 可见性语义 |

**流式性证明**（`SseReaderTests.ReadAsync_YieldsFirstFrameBeforeStreamEnds`）：
喂一个"吐完第一帧就挂起"的流，拿到第一帧即退出。若实现是 `ReadToEnd` 式写法，
第一次迭代会等到超时 —— 这是"首字节延迟"这一体验指标在单元测试里唯一的确定性判据。

**App 层接线守卫**（静态扫源码）：`textProgress` 与 `CancellationToken` 同样是**可选参数**，
漏传照常编译、照常通过行为测试，后果是"界面全静止"的静默失效。
守卫断言 4 条意图分支与 Agent 调用都必须把观察者接上，且 `AiPreviewManager` 不得半路丢弃它。

---

## 4. 负向验证（6 组，含 1 处自纠）

注入器一律 `copyfile` + 写回，断言**期望用例名落在失败行上**（只判退出码会被"别的用例碰巧红了"骗过去）。

| NV | 注入 | 结果 |
|---|---|---|
| A | `AiPreviewManager` 丢掉一处 `textProgress` | ✅ 命中：`AiPreviewManager_ForwardsTextProgressToOrchestrator` |
| B | Claude 正文改回只取 text block（忽略 `input_json_delta`） | ✅ 命中：`Claude_ChatStreamAsync_ToolUseInputJsonDeltas_BecomeContent` |
| C | `LlmChat` 恒走 `ChatAsync`（引擎不走流式） | ✅ 命中：`RunAsync_StreamingProvider_UsesStreaming_AndReportsDeltasInOrder` |
| D | OpenAI 吞掉流中途 `error` 帧 | ✅ 命中：`OpenAi_ChatStreamAsync_MidStreamErrorFrame_ThrowsAndIsNotRetried` |
| E | Claude 忽略 `error` 事件 | ✅ 命中：`Claude_ChatStreamAsync_MidStreamErrorEvent_ThrowsAndIsNotRetried` |
| F | `SseReader` 不再按空行分帧 | ✅ 命中：`ReadAsync_TwoFramesSeparatedByBlankLine_YieldsBoth` |

### 4.1 一处自纠：负向验证暴露出**我自己写的死代码**

首轮 NV 注入「去掉 OpenAI 降级时的 `emitted` 检查」，**没能让任何用例变红**。
追下去发现那不是"测试不够"，而是**那个分支按构造不可达**：

```csharp
catch (LlmProviderException ex) when (IsRequestShapeError(ex) && !emitted)
```

`StreamOnceAsync` 在响应体开始读取之后**不可能**再抛 `LlmProviderException`
（`SendWithRetryAsync` 在非 2xx 时、即正文之前抛出；正文读取期的失败是
`IOException`/`OperationCanceledException`，不带状态码）。所以 `emitted` 恒为 false。

我最初的类注释把它写成了"防止正文重复"的**主动机制** —— 那是一句**没被验证过的行为声明**，
正是本项目反复吃过亏的那一类（阶段 6 的错误注释、阶段 8 的"已下发未消费"）。
处置：**删掉不可达分支**（与 `ViewLocator` 那次死工厂分支同一把尺子），
并把"流中途失败"改成**真实可达**的用户可见行为（`error` 帧 / `error` 事件 → 抛错、保留已吐正文、不重试），
用 NV-D / NV-E 钉住。注释同步改写为**真实机制**：「状态码先于正文 ⇒ 降级天然发生在增量之前」。

---

## 5. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2577 通过 / 0 失败**（Core 1900 / AI **313** / App.UiTests **147** / Device 76 / Communication 75 / Simulator 44 / Hardware 22；基线 2535 ⇒ **+42**） |
| 负向验证 | **6/6 命中**（见 §4） |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |
| 本地化 | zh-CN / en-US 各 +2 键（`ai.streaming.hint` / `ai.streaming.status`），占位符数量一致 |

---

## 6. 范围边界（本轮**不做**，已登记）

1. **并行 tool_call**（规划文档 P2-5 的后半）。本轮只做 SSE 正文流式。Agent 的工具调用仍是
   串行（`AgentEngine` 每轮解析一个 `tool_call`）。要并行需要改协议（一次输出多个调用）+
   引擎调度 + 结果回填顺序，属**独立的架构改动**，与流式无耦合 —— 把它塞进来只会让
   "流式"这一件事的回归半径翻倍。
2. **非 Agent 路径的"逐字打字机"排版**。预览是**尾部窗口**而非正式正文；
   正式正文仍是整段替换（避免 JSON 半截被当成结果展示）。
3. **`IconGenerator` / 批量图标路径未接观察者**。`AiIconDialog` 与 `BatchIconGenerator`
   仍是"点了等结果"（图标生成通常数秒级，收益低于画布生成）。生成器本身**已支持**
   `textProgress` 参数，接线属一行的机械改动。
4. **无真实网关联调**。所有流式测试都用注入的 `HttpMessageHandler` / 自造 SSE 流，
   未对真实 OpenAI / Anthropic 端点发过请求（无密钥、也不该在测试里发）。
   真实网关的边界（分片粒度、心跳间隔、代理缓冲）只能靠现场验证。
