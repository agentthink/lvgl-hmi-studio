# CodeGenerator ↔ SimulatorProjectExporter 符号契约统一（P2 阶段 1）

日期：2026-09-14
范围：`HmiEditor.CodeGen` / `HmiEditor.Core` / 新增 `HmiEditor.Core/CodeGen`
状态：阶段 1 已完成并提交；阶段 2、3 待做

---

## 1. 背景：为什么这两条链路必须逐字符一致

同一批资源（图片 / 字体）的 C 符号，由**两条独立链路**各自产出：

| 链路 | 入口 | 产物 |
|---|---|---|
| 生成链 | `CodeGenerator.GenerateSimulatorModule` | `generated_hmi.c` |
| 导出链 | `SimulatorProjectExporter.Export` | `main.c`、`generated_images.c`、`generated_fonts.c` |

涉及的共享符号族：

- `IMG_PATH_<id>` —— 图片路径宏（`"A:" SIM_ASSET_DIR "/<file>"`）
- `img_<id>` —— 图片描述符（`lv_img_dsc_t`）
- `font_<id>` —— 字体句柄
- `font_<id>_<size>` —— 控件自定义字号变体
- `FONT_PATH_<id>` / `FONT_PXSIZE_<id>` —— 字体路径/字号宏（仅导出侧定义）

**失败模式的隐蔽性**：两条链路分别编译/链接，符号名不一致时症状是「编译期找不到符号」或「链接期未定义引用」，而报错文件是 A、根因在 B。排查时要跨两个模块、两种产物形态比对，非常绕。

## 2. 归因：三份净化实现 + 调用点分散

### 2.1 直接根因 —— 同一段净化逻辑有三份实现

统一前：

| 位置 | 函数 | 空值兜底 |
|---|---|---|
| `CodeGenerator` | `SanitizeAssetId` | `"unknown"` |
| `CodeGenerator` | `SanitizeIdentifier` | `"anim"` |
| `SimulatorProjectExporter` | `SanitizeIdentifier` | `"unknown"` |

实现体逐字相同（`char.IsLetterOrDigit(c) \|\| c == '_'`，否则 `'_'`），**只有兜底值漂移**。

更糟的是调用语义错配：`CodeGenerator` 里资产走 `SanitizeAssetId`（→`"unknown"`）、控件走 `SanitizeIdentifier`（→`"anim"`），而 `SimulatorProjectExporter` 把**资产 id** 交给 `SanitizeIdentifier`。

> 当时两条链路的资产符号一致，**纯属巧合** —— 因为资产 id 通常非空，兜底分支根本没被走到。谁哪天把同名两函数「顺手对齐」，`generated_hmi.c` 与 `generated_images.c` 的宏名立刻错位。

### 2.2 更深根因 —— 同一个符号被多处独立重算

统一实现**并不能**解决全部问题。`SimulatorProjectExporter` 内部对同一批字体符号在 **8 处**各自重新调用一次净化：

| 行（统一后） | 用途 |
|---|---|
| 218 | `main.c` 的 `extern lv_font_t font_*;` |
| 236 | `main.c` 的 `g_loaded_font = &font_*;` |
| 1143 | `generated_fonts.c` 的 `FONT_PATH_*` / `FONT_PXSIZE_*` / `font_*` 定义 |
| 1197 | `generated_fonts.c` 的 `lv_freetype_font_create(FONT_PATH_*, ...)` |
| 1246 | `generated_fonts.c` 的 fallback 分支 |
| 1260 | `generated_fonts.c` 的声明注释块 |
| 1274 | `CollectControlFontSizes` 的默认字体 |
| 1285 | `CollectControlFontSizes` 的 `control.FontFamily` |

图片侧同样是 3 处独立重算（1060 / 1081 / 1100）。

只要这些调用点之间出现分叉（分支条件写歪、fallback 字面量不一致、`_soMode` 处理不同），**同一份产物内部就会自相矛盾**：`generated_fonts.c` 里定义 `font_X`，而 `main.c` 里 `extern font_Y`。这类矛盾**不需要跨模块**就能复现，比 2.1 更隐蔽。

## 3. 方案与落地

### 3.1 依赖方向决定落点

`HmiEditor.CodeGen` → `HmiEditor.Core`（单向），`HmiEditor.Core` 无项目引用。共享代码只能放 `Core`。

新建 `src/HmiEditor.Core/CodeGen/CIdentifier.cs`：

```csharp
public static class CIdentifier
{
    public const string AssetFallback = "unknown";
    public const string ControlSymbolFallback = "anim";

    public static string ForAsset(string? id) => Sanitize(id, AssetFallback);
    public static string ForControlSymbol(string? id) => Sanitize(id, ControlSymbolFallback);

    public static string Sanitize(string? input, string fallback) { /* 唯一实现体 */ }
}
```

**两个入口而不是一个**：`ForAsset` / `ForControlSymbol` 把「资产符号」与「控件符号」的语义差异显式化，同时保留各自的历史兜底值。这消掉了「同名两函数被误对齐」的可能性 —— 语义不同，名字就不同。

### 3.2 行为约定（**勿轻改**）

`Sanitize` 用的是 `char.IsLetterOrDigit`，它把中文等 Unicode 字母判为「合法」，因此**中文 id 会原样保留**（如 `图片2` 生成 `font_图片2`）。

严格按 C 标准这需要 UCN 转义，但既有产物依赖当前行为 —— 改动会牵动全部生成结果，属于独立议题。

`ControlSymbolFallback = "anim"` 同样是历史取值（最早只有动画 id 走这条路径），对「控件 id 为空」并不贴切，但改它会改变既有产物，**统一时不得顺手修正**。

### 3.3 改动清单

| 文件 | 改动 | 行数 |
|---|---|---|
| `src/HmiEditor.Core/CodeGen/CIdentifier.cs` | **新建** | 65 |
| `src/HmiEditor.CodeGen/CodeGenerator.cs` | 删 2 个方法（28 行），32 处调用点改向，+1 `using` | −60 / +33（净 3411 行） |
| `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` | 删 1 个方法（17 行），11 处调用点改向，+1 `using` | −28 / +12（净 1296 行） |

**保留未动**：`RuntimeTagsCodeGen.SanitizeIdentifierPublic` —— 产物是 `TAG_` 常量，不参与资源符号契约。（它是事实上的第四份实现，但无空值兜底、行为等价，收敛它属独立议题。）

## 4. 测试网

### 4.1 `CodeGenAssetSymbolContractTests`（5 项）

跨链路符号契约守卫。**同一次生成**里分别对两条链路的产物抓符号集合：

| 测试 | 断言 |
|---|---|
| `ImagePathMacros_AreIdenticalInBothLinkChains` | `IMG_PATH_*` 集合**完全相等** |
| `ImageDescriptors_AreIdenticalInBothLinkChains` | `img_*` 集合**完全相等** |
| `FontHandles_AreIdenticalInBothLinkChains` | `font_*` **子集关系**（module ⊆ exporter） |
| `FontPathMacros_ExistOnlyOnTheExporterSideAndCoverEveryFont` | `FONT_PATH_*` 仅导出侧定义且覆盖每个字体 |
| `SanitizedAssetIdentifiers_FollowOneRuleAcrossBothChains` | 净化规则跨链路一致 |

集合比对用 `HashSet.SymmetricExceptWith`，相等即对称差为空。

测试数据刻意覆盖三条分支：需要净化的 id（`img-1`）、中文 id（`图片2`，验证原样保留）、**空 id**（走兜底分支）；另有一个 `FontSize = 22 ≠ 主题 14` 的控件，产生 `font_font_1_22` 变体符号。

### 4.2 `CIdentifierTests`（12 项）

净化规则单测：8 组 `[Theory]` 非法字符替换表、中文保留、幂等性、以及「`ForAsset` 与 `ForControlSymbol` 对每个非空输入结果一致」（唯一差别只应在空输入兜底值）。

## 5. 负向验证

> 纪律：「测试全绿」≠「测试有效」。逐条破坏产品代码，确认**恰好**对应项变红，再还原并核对 `git diff` 为空。

| # | 破坏动作 | 结果 | 判定 |
|---|---|---|---|
| 1 | 导出侧图片净化漂移为 `Sanitize(image.Id, "BREAK")`（仅 1060 一处） | **恰好 3 项红**（`ImagePathMacros` / `ImageDescriptors` / `SanitizedAssetIdentifiers`），报出「IMG_PATH_* 不一致（差异 2 个）」 | 守卫有效 |
| 2 | `AssetFallback` 常量从 `"unknown"` 改为 `"NEGATIVE_VERIFY"` | 12/12 全过 | **设计取舍，非缺陷**：测试断的是 `ForAsset(null) == AssetFallback`，两边同源、改常量两边同动。测试价值在断言「该常量确实被用作兜底」，不在断言字面量 |
| 3 | 导出侧**字体**净化漂移，仅 1143 一处 | 5/5 全过 | ⚠️ **暴露盲区**，见下 |
| 4 | 导出侧**字体**净化 8 处**全部**漂移 | **恰好 1 项红**（`FontHandles_AreIdenticalInBothLinkChains`），报出 `font_unknown` 在导出侧缺失；产物集合里同时出现 `font_FBREAK` 与 `font_FONTBREAK` | 守卫有效，但敏感度不足 |

### 5.1 验证 3 的结论（重要）

**局部漂移不红，是全量漂移才红。** 原因：

- 图片侧断言是**严格相等**，且 `IMG_PATH_*` 在生成链只有 1 处产出 → 导出侧单点漂移立刻让对称差非空 → 敏感。
- 字体侧断言是**子集关系**，而 `font_*` 在导出侧由 8 处**重复产出同一个符号名** → 只漂 1 处时，其余 7 处仍输出正确名字，**并集被补齐**，子集断言依然成立 → 迟钝。

验证 4 的失败信息还把 2.2 的隐患**实证**出来了：导出侧产物内部同时存在 `font_FBREAK` 与 `font_FONTBREAK`（因为我在不同调用点用了不同 fallback 字面量）—— 这正是「同一文件内部自相矛盾」的形态。

**结论**：`CIdentifier` 只解决了「实现唯一」，没解决「调用点分散」。要真正消除风险，必须让导出侧**把符号表算一次**，后续所有代码段从表里取。

## 6. 阶段划分

| 阶段 | 内容 | 状态 |
|---|---|---|
| **1** | 统一净化实现（`CIdentifier`）+ 跨链路契约测试网 + 负向验证 | ✅ 完成 |
| **2A** | 导出侧引入**资源符号表**（`CResourceSymbols`）—— 消除 2.2 的多点重算 | ✅ 完成，见 `docs/2026-09-14-codegen-exporter-symbol-table.md` |
| **2D** | 字体断言从「子集」改为「严格相等 + 表为单一来源」，收紧 5.1 的迟钝 | ✅ 完成，同上 |
| **2B** | 统一 `extern lv_font_t font_<id>;` / `extern const lv_img_dsc_t img_<id>;` 声明发射 | 待做 |
| **2C** | 统一字号收集算法（`CodeGenerator.CollectUsedFontSizes` 与 `CResourceSymbols.CollectFontVariants` 逐字重复，14 行） | 待做 |
| **3** | `CodeGenerator.cs`（3411 行、零 partial）按域拆分 | 待做 |

**已决议不做**：把 `IMG_PATH_<id> "A:" SIM_ASSET_DIR "/<file>"` 抽成宏 —— 该串在 3 处逐字重复，但抽宏会削减生成产物的可读性（生成的 C 代码需要人能直接看懂路径来源），收益不抵成本。

> 注：不抽 `IMG_PATH_` 宏，但「路径串的构造」仍应统一到 2A 的符号表里 —— 重复的是**构造逻辑**而非**结果文本**。

## 7. 验证基线

`dotnet test HmiEditor.slnx -c Release`：

| 项目 | 项数 |
|---|---|
| HmiEditor.Core.Tests | 1347（统一前 1330，+5 契约 +12 净化规则） |
| HmiEditor.AI.Tests | 237 |
| HmiEditor.App.UiTests | 91 |
| Hmi.Communication.Tests | 75 |
| HmiEditor.Device.Tests | 63 |
| HmiEditor.Simulator.Tests | 41 |
| Hmi.Hardware.Tests | 22 |
| **合计** | **1876，0 失败** |

Release 构建 **0 警告 0 错误**。

### 环境坑（会影响测试结果判读）

本机 `/tmp` 是 **10M tmpfs**。多个测试项目并行写临时文件时会被打满，症状是随机的 `No space left on device` / `Failed to create '...' because the disk was full`，报错落在**任意**一个写临时文件的测试上（本次观察到 `MockImageLoadingTests`、`HmiPackageEndToEndTests`、`DataLoggerTests` 各中一次）。

**规避方式**：`env TMPDIR=<磁盘目录> dotnet test ...`。不带 `TMPDIR` 时若出现零星失败，先怀疑磁盘而非代码 —— 单独复跑该测试类可立刻分辨。
