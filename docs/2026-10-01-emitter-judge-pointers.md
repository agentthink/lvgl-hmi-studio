# 控件发射器 → **判据点**（阶段 204）
## —— 把"这些发射路径有判据看着"从**印象**变成**可核对的指针**

> 承阶段 203（`docs/2026-10-01-control-field-consumption.md`）：那一阶段试过两条机械代理，**两条都不可靠**，
> 结论是"必须先看已有的面"。这一阶段换一层做账：**在发射器粒度上指名判据**，
> 并且要求那条判据里**逐字存在**一段断言原文 —— 于是"只有一行注释的发射器"（`#93` 的 Keyboard）
> **不可能**通过。

---

## 1. 为什么不能停在"印象"上

阶段 203 的两条代理各自翻过车：

| 代理 | 翻车方式 |
|---|---|
| 按**字段名** grep 生成器 | **假阴**：`Rotation`/`RotationDirection` 经 `HmiControl.EffectiveRotation()` 间接消费，字段名根本不出现 |
| 按 **LVGL API 名** grep 测试 | **口径错**：`lv_canvas_draw_text` 没人断言，但 Tank/Pipe/Valve 的绘制**有**判据（断言的是 `lv_draw_rect`/`lv_draw_line`/`lv_draw_arc` 与具体坐标） |

⇒ "有判据看着"如果只停留在印象/代理上，就永远说不清。**做法：把指针写下来，并让机器验证指针是真的。**

## 2. 三道核对

判据在 `tests/HmiEditor.Core.Tests/ControlEmitterJudgePointersTests.cs`（4 条）：

1. **表 ↔ 代码双向闭合**：`CodeGenerator.ControlParameters.cs` 里**实际定义**的每个
   `Emit*Parameters` 方法都必须在表里，表里的每个也必须在代码里存在
   （新加一个发射器却不登记判据点 ⇒ 红；改名/删除后表没同步 ⇒ 红）。
2. **指针必须逐字命中**：每条登记给出 `判据文件 + 断言片段（Snippet）`，
   要求那段原文**逐字存在**于该文件（且长度 ≥ 12 字符，免得"指着个空"）。
3. **面板字段的消费点必须落表**：面板写回的 **58 个** `HmiControl` 字段，
   逐个在 `HmiEditor.CodeGen` 里找**第一个真实读取点**（跳过注释行），它所在的**方法**必须落在
   `Emitters` ∪ `SharedConsumers` ∪ `IndirectFields` 里 —— 新冒出一个消费点就红。
4. **共用消费点/间接消费登记自身要真**：`SharedConsumers` 的方法要在指名文件里找得到定义/调用、
   判据文件要存在且含 Snippet；`IndirectFields`（`Rotation`/`RotationDirection`）要满足
   ① 是面板写回的字段 ② 判据里有那句断言 ③ **生成器里确实没有**对该字段的直接读取
   （一旦有了 ⇒ 必须挪进 `Emitters` 表）。

## 3. 账的内容（阶段 204 实测）

- **19 条发射器指针**：15 个 `Emit*Parameters`（Slider/Bar/Dropdown/TextArea/Gauge/Led/Spinner/Table/
  TabView/Label/Keyboard/Image/Line/Polyline/Arc）＋ 4 个共用发射器
  （`WriteStyleCalls` / `WriteTextCall` / `AppendTransform` / `GenerateVisualStates`）。
- **11 条共用消费点**：`TableColumnWidthExpr` · `GetControlFontExpression` · `GenerateScreenCode` ·
  `ProcessControlCodeGen` 的 Tank/Pipe/Valve · `SliderBarIndicators` 的 PointerDeclarations/AppendSliderTicks/
  SliderTickCount · `GenerateControlParameters` 的公共尾（不透明度/隐藏/指示器色）· `GenerateActionCallbacks`。
- **2 条间接消费**：`Rotation` / `RotationDirection`（经 `EffectiveRotation()`）。

### 🩸 建账第一天就抓到一个**我不知道的消费点**

核对 ③ 第一次跑就报：

```
这些面板字段的**第一个读取点**落在一个没登记的发射器/消费点里：
  Name → GenerateActionCallbacks
```

`HmiControl.Name` 在 `GenerateActionCallbacks` 里被读（动作回调的注释 `/* {Name}: … */`）——
**这是"靠印象"绝不会知道的一条**。处置：把它登记进 `SharedConsumers`（判据 ⇒ `ActionCallbacksCodeGenTests.cs`）。
同理被这条核对**逐个量出来**的还有 `Value`/`Points`/`Cells`/`IsVisible` 这类"面板不写、发射器也读"的字段，
它们进了 `NonPanelFields` 名单（表里可以提到它们，但不算面板表面）。

## 4. 负控（6 支，`.workbuddy/tmp/s204-nc.py`）

| 支 | 注入 | 期望红 | 读数（失败正文） |
|---|---|---|---|
| nc1 | 删掉一条发射器登记（`EmitBarParameters`） | 双向闭合 | `这些发射器没有登记判据点（新加的发射器必须点名一条真判据）：EmitBarParameters` |
| nc2 | 一条 Snippet 指向不存在的断言 | 指针核对 | `EmitBarParameters：ControlEmitterCoverageTests.cs 里找不到断言片段 \`lv_bar_set_range(obj_b1, 999, 999);\`` |
| nc3 | 删掉共用消费点 `GenerateTankCode` | 面板字段消费点核对 | `TankShowScale → GenerateTankCode` / `TankTickCount → GenerateTankCode` |
| nc4 | 删掉间接消费登记里的 `Rotation` | 面板字段消费点核对 | `Rotation → (CodeGen 里没有直接读取)` |
| nc5 | 往间接登记塞 `MirrorX`（其实有直接读取） | 间接登记自检 | `MirrorX：生成器里**已经有**直接读取了（应挪进 Emitters 表，而不是留在间接登记里）` |
| nc6 | **删掉判据文件里那句断言**（`lv_bar_set_range`） | 指针核对 | `EmitBarParameters：… 里找不到断言片段 \`lv_bar_set_range(obj_b1, 0, 100);\`` |

> nc6 是这一批里最要紧的一支：它证明指针**绑的是真断言**，不是"文件里随便有个名字"——
> 判据被掏空时，指针当场失效。

6 支逐支独立日志（`.workbuddy/tmp/s204-nc{1..6}.log`）、每腿限时、开跑前扫残留注入，
还原后 2 个文件的 SHA256 逐字节相同。

## 5. 读数与仍未做

**读数**：`GATE_EXIT=0` / **7 步全绿** / 0 警告 0 错误；日志 **980 行**（非空 **944**）；脚本 **29**；
路径卫生 **700**；步 5 逐程序集 `44 / 44 / 4 / 78 / 96 / 2280 / 315 / 798 / 8`（合计 **3667**）
＝ **3667 通过 / 0 失败**（阶段 203 为 3663，**＋4**）；`HmiEditor.Core.Tests` **2284 → 2288**；
`App.UiTests` **798 / 0**（连跑 41 / 34 s）；`UnusedBaseline` **不变 1109**；**产品代码 0 行改动**。

**仍未做**：

1. **颗粒度是"发射器/消费点"，不是"每个字段一条判据"**：一张 58 行 × 判据名的**逐字段**表仍未建
   （本阶段的核对照样能保证"每个字段的消费点都有人看着"，但说不出"这个字段由哪条断言管")。
2. **模拟器侧**（`SimulatorProjectExporter`）的消费点不在本表里 —— 同一批字段在模拟器工程里的消费
   仍只有"印象"。
3. **`#92` 的剩余部分**照旧（运行时回调 / 面板标明）。
