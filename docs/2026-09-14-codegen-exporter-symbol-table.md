# 导出侧资源符号表（P2 阶段 2A / 2D）

日期：2026-09-14
范围：`HmiEditor.Core/CodeGen/CResourceSymbols.cs`（新建）、`SimulatorProjectExporter.cs`
状态：2A、2D 已完成；2B、2C 待做
前置：`docs/2026-09-14-codegen-exporter-unification.md`（阶段 1：统一 `CIdentifier`）

---

## 1. 为什么阶段 1 不够

阶段 1 把三份重复的净化实现收成了一个 `CIdentifier`，但它只保证**规则相同**，不保证**调用点不漂移**。

负向验证暴露的事实：`SimulatorProjectExporter` 内部对**同一批字体符号**在 **9 处**各自重算净化名：

| 行（改造前） | 用途 |
|---|---|
| 218 | `main.c` 的 `extern lv_font_t font_*;` |
| 236 | `main.c` 的 `g_loaded_font = &font_*;` |
| 1143 | `generated_fonts.c` 的 `FONT_PATH_*` / `FONT_PXSIZE_*` / `font_*` 定义 |
| 1197 | `generated_fonts.c` 的 `lv_freetype_font_create(FONT_PATH_*, ...)` |
| 1246 | `generated_fonts.c` 的 `!LV_USE_FREETYPE` 回退分支 |
| 1260 | `generated_fonts.c` 的声明注释块 |
| 1274 | `CollectControlFontSizes` 的默认字体 |
| 1285 | `CollectControlFontSizes` 的 `control.FontFamily` |

图片侧同理 3 处（1060 / 1081 / 1100）。

**任一处漂移，产物内部就自相矛盾**：`generated_fonts.c` 定义 `font_X`、`main.c` 却 `extern font_Y`。两处代码各自看都「没问题」，症状是链接期未定义引用。这是**不需要跨模块**就能复现的矛盾，比阶段 1 处理的那类更隐蔽。

阶段 1 的负向验证实测数据：

| 破坏范围 | 跨链路契约测试结果 |
|---|---|
| 字体净化**单点**漂移 | 5/5 全过 —— **假绿** |
| 字体净化 **8 处全部**漂移 | 恰好 1 项红 |

单点不红的原因见 §3。

## 2. 方案：符号表算一次

新建 `CResourceSymbols`（`HmiEditor.Core/CodeGen/`），把「一批资源 → 一批符号」的计算收口：

```csharp
public sealed record ImageSymbol(string Id, string Name, string Fragment, string FileName);
public sealed record FontSymbol(string Id, string Name, string Fragment, string FileName, int PixelSize);
public sealed record FontVariantSymbol(string FontFragment, int Size);

public sealed class CResourceSymbols
{
    public IReadOnlyList<ImageSymbol> Images { get; }
    public IReadOnlyList<FontSymbol> Fonts { get; }
    public IReadOnlyList<FontVariantSymbol> FontVariants { get; }
    public string? DefaultFontFragment { get; }

    public static CResourceSymbols Create(
        IReadOnlyList<AssetItem> imageAssets,
        IReadOnlyList<AssetItem> fontAssets,
        HmiProject project);
}
```

`Export` 入口只调一次，而后三个发射方法改签名从「接收原始资源」变成「查表」：

| 方法 | 改造前 | 改造后 |
|---|---|---|
| `BuildMainC` | `…, IReadOnlyList<AssetItem> fontAssets, …` | `…, CResourceSymbols symbols, …` |
| `BuildImagesC` | `(IReadOnlyList<AssetItem> imageAssets)` | `(CResourceSymbols symbols)` |
| `BuildFontsC` | `(IReadOnlyList<AssetItem> fontAssets, HmiProject project)` | `(CResourceSymbols symbols)` |

`CollectControlFontSizes` 整体搬进 `CResourceSymbols.CollectFontVariants`（`project` 参数随之进入 `Create`）。

### 关键不变量

> **`SimulatorProjectExporter` 内不得再出现 `CIdentifier` 调用。**

改造后实测：`grep -c CIdentifier src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` → **0**。

资源符号的净化只能发生在 `CResourceSymbols.Create` 里一处。发射代码拿到的是**成品字符串**，没有重算的机会 —— 这比「约定大家调用同一个函数」强，因为后者依赖人的纪律，前者是类型层面没有原料。

> 注：`CResourceSymbols` 内部仍调用 `CIdentifier.ForAsset` —— 那是唯一的计算点，符合预期。

## 3. 测试策略：从「子集」改为「严格相等 + 表为单一来源」

阶段 1 的跨链路字体断言用的是**子集关系**（`generated_hmi.c` 的 `font_*` ⊆ 导出侧）。而导出侧由 8 处重复产出同名，**只漂 1 处时其余 7 处把并集补齐**，子集断言仍成立 → 假绿。

新增 `ExportResourceSymbolTests`（9 项），改为**同文件内部严格相等** + **收口到符号表**：

| 断言 | 关系 |
|---|---|
| `FONT_PATH_*` ↔ `FONT_PXSIZE_*` | 严格相等 |
| `lv_freetype_font_create` 的路径宏实参 ⊆ `FONT_PATH_*` | 子集（实参必须已定义） |
| `FONT_PATH_*` / `FONT_PXSIZE_*` / 句柄定义 各自无重复 | 唯一性 |
| `main.c` 的 `extern font_*` ↔ `FONT_PATH_*` | **严格相等** |
| `&font_X` 的 X ∈ main.c extern 集合 ∩ generated_fonts.c 定义集合 | 引用完整性 |
| `IMG_PATH_*` ↔ extern ↔ 定义 ↔ `LV_IMG_DECLARE` ↔ 占位数组名 | 五路严格相等 |
| `generated_images.c` 三类声明各自无重复 | 唯一性 |
| **产物全部片段 ↔ `CResourceSymbols.Create` 算出的片段** | **严格相等（收口）** |
| 变体的基片段 ⊆ `FONT_PATH_*` | 子集 |

另新增 `CResourceSymbolsTests`（14 项）钉住表本身：净化（含中文原样保留、空值兜底）、文件名取末段（反斜杠归一）、像素尺寸兜底、默认字体、变体收集（跳过主题字号 / 用默认字体 / 保留库外 family / 去重排序）、空输入、null 参数、顺序保持。

### 踩的坑：基/变体正则无法从文本区分

`lv_font_t font_<name>;` 里，基字体 id 若以 `_数字` 结尾（如 `font-1` → `font_1`），文本上与「基片段 `font` + 字号 `1`」**完全同形**。用 `font_(\w+)_(\d+)` 拆分会把 `font_1` 误判成变体。

**对策**：一律按**原始句柄名**比对（`Grab(fontsC, @"^lv_font_t font_(\w+);")`），不拆基/变体；期望集合由符号表给出（`Fonts` 片段 ∪ `FontVariants` 的 `frag_size`）。

### 顺带修掉的盲区：`\w` vs `[A-Za-z0-9_]`

跨链路契约测试用 `[A-Za-z0-9_]`，**抓不到中文片段**（`font_中文字体` 里 `font_` 后接非 ASCII，正则不匹配）→ 中文 id 的两个符号集都是空，相等断言平凡成立。

本类改用 `\w`（.NET 下含 Unicode 字母），中文片段也被纳入比对。

## 4. 负向验证

| # | 破坏动作 | 结果 |
|---|---|---|
| NV-A | `BuildFontsC` 只让**句柄定义**一处漂移（`CIdentifier.Sanitize(id, "DRIFT")`） | **恰好 1 项红**（`ArtifactFragments_EqualTheSymbolTableExactly`，报 `DRIFT` vs `unknown`）；**跨链路契约测试 5 项全绿** |
| NV-B | `BuildFontsC` 只让**路径宏**一处漂移 | **恰好 4 项红**（`FontsC_PathMacros_And_PxSizeMacros` / `FontsC_CreateCall_OnlyUsesMacrosThatExist` / `MainC_FontExterns_AreExactlyTheBaseFonts` / 收口）；契约测试仍全绿 |
| NV-C | `BuildImagesC` 只让 **`LV_IMG_DECLARE`** 一处漂移 | 2 项红（本类 1 + 契约测试 1 —— 图片侧契约断的是严格相等，单点漂移本来就能抓） |
| NV-D | `CollectFontVariants` 去掉「跳过主题字号」 | **恰好 1 项红**（`CollectFontVariants_SkipsThemeSize_AndTakesNonThemeOnes`） |

**NV-A 是这一轮最关键的证据**：同一处漂移，阶段 1 的守卫报绿、阶段 2 的守卫报红且定位准确。四处破坏均已还原，`grep NEGATIVE-VERIFY` 无残留。

## 5. 产物逐字节等价校验

2A 是纯重构，产物应当**一字不差**。用固定 fixture（3 图 + 3 字体，含需净化 id / 中文 id / 空 id / 非主题字号控件），分别用 `19696f4`（阶段 1 后）与改造后代码导出 `full`、`no-assets` 两组工程，比对全部 16 个文件：

```
diff -r eq-old eq-new        → 无差异
md5sum 逐文件比对            → 16/16 一致
```

这比测试更硬：**逐字节零差异**证明重构没有改变任何输出，包括不参与断言的注释、宏顺序、空行。

## 6. 验证基线

`env TMPDIR=<磁盘目录> dotnet test HmiEditor.slnx -c Release`：

| 项目 | 项数 |
|---|---|
| HmiEditor.Core.Tests | **1370**（阶段 1 后 1347，+14 符号表单测 +9 产物自洽守卫） |
| HmiEditor.AI.Tests | 237 |
| HmiEditor.App.UiTests | 91 |
| Hmi.Communication.Tests | 75 |
| HmiEditor.Device.Tests | 63 |
| HmiEditor.Simulator.Tests | 41 |
| Hmi.Hardware.Tests | 22 |
| **合计** | **1899，0 失败** |

Release 构建 0 警告 0 错误。

> 观察到的 flaky：`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`、`AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay` —— 两者都依赖真实定时器，并行跑全量时偶发；单独复跑 45/45 全绿。与本改动无关。

## 7. 后续

| 阶段 | 内容 | 状态 |
|---|---|---|
| 2A | 导出侧符号表（消除 9 处多点重算） | ✅ 完成 |
| 2D | 字体断言收紧为严格相等 + 表为单一来源 | ✅ 完成 |
| **2B** | 统一资源符号的**命名与声明发射**（`CResourceDeclarations`）—— 此前 `img_`/`font_`/`IMG_PATH_`/`FONT_PATH_`/`FONT_PXSIZE_` 前缀以字面量散落约 25 处 | ✅ 完成，见 `docs/2026-09-14-codegen-resource-declarations.md` |
| **2C** | `CodeGenerator` 接入符号表，删除逐字重复的 `CollectUsedFontSizes` | ✅ 完成，同上 |
| 3 | `CodeGenerator.cs`（3380 行、零 partial）按域拆分 | 待做 |

### 更正：2C 没有「过滤差异」陷阱

本文件初版写过：

> 2C 的注意点：`CodeGenerator.CollectUsedFontSizes` 与导出侧版本有一处**实质差异** —— 生成器侧会用 `availableFontIds` 做存在性过滤，导出侧**不过滤**，统一时必须显式建模（例如加 `bool filterToKnownFonts`）。

**这个判断是错的**（2026-09-14 实施 2C 时发现）。存在性过滤在另一个函数 `GetControlFontExpression` 里，不在 `CollectUsedFontSizes`：

```csharp
if (availableFontIds != null && !availableFontIds.Contains(sanitizedId))
    return null;   // 资源不在库中 → 回退主题默认字体
```

`CollectUsedFontSizes` 与 `CResourceSymbols.CollectFontVariants` **没有任何差异**，是干净的同体重复，直接合并即可、不需要开关参数。当时把两个函数的行为混在了一起。

`GetControlFontExpression` 的过滤逻辑本轮**未改动** —— 它决定「控件用哪个字体表达式」，不是「要生成哪些字体符号」，职责不同。

> 教训：判定"两处实现是否有实质差异"时，必须逐行读完**目标函数本身**，不要拿"同一文件里另一个相关函数的行为"当依据。尤其当两者名字相似（`CollectUsedFontSizes` vs `GetControlFontExpression`）时更容易串。

**决议不做**：把 `IMG_PATH_<id> "A:" SIM_ASSET_DIR "/<file>"` 抽成宏 —— 该串在 3 处逐字重复，但抽宏会削减生成产物的可读性。不过路径串的**构造**现已进符号表（`ImageSymbol.FileName` + 发射处拼接），重复的是结果文本而非构造逻辑。
