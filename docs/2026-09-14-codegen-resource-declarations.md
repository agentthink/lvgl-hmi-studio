# 资源符号命名与声明发射统一（P2 阶段 2B / 2C）

日期：2026-09-14
范围：`HmiEditor.Core/CodeGen/CResourceDeclarations.cs`（新建）、`CodeGenerator.cs`、`SimulatorProjectExporter.cs`
状态：2B、2C 已完成
前置：
- 阶段 1：`docs/2026-09-14-codegen-exporter-unification.md`（统一 `CIdentifier`）
- 阶段 2A/2D：`docs/2026-09-14-codegen-exporter-symbol-table.md`（导出侧符号表）

---

## 1. 归因

阶段 2A 把**净化规则**和**计算点**收敛了，但符号名的**前缀字面量**和**声明语句形态**仍然散着。

### 1.1 前缀字面量散落约 25 处

`img_` / `font_` / `IMG_PATH_` / `FONT_PATH_` / `FONT_PXSIZE_` 以字符串插值形式出现在两条链路里：

| 符号族 | CodeGenerator | SimulatorProjectExporter |
|---|---|---|
| `img_<f>` | 2 处 | 4 处 |
| `font_<f>` | 7 处 | 9 处 |
| `font_<f>_<n>` | 4 处 | 4 处 |
| `IMG_PATH_<f>` | 5 处 | 1 处 |
| `FONT_PATH_<f>` | — | 3 处 |
| `FONT_PXSIZE_<f>` | — | 5 处 |

**这些前缀是跨链路契约**：改一处不改另一处，就是「声明与定义不同名」——编译期找不到符号或链接期未定义引用，而报错位置与改动位置往往不在同一个文件。阶段 1、2A 都没有消除这个面。

### 1.2 声明形态三种写法混用

| 语句 | 出现位置 | 次数 |
|---|---|---|
| `extern const lv_img_dsc_t img_X;` | CodeGen ×2、Exporter ×1 | 3 |
| `LV_IMG_DECLARE(img_X);` | Exporter ×1 | 1 |
| `extern lv_font_t font_X;` | CodeGen ×1、Exporter ×1 | 2 |
| `lv_font_t font_X;`（定义） | Exporter ×1 | 1 |
| `static const lv_font_t* font_X = NULL;`（so 模式定义） | CodeGen ×2 | 2 |

`LV_IMG_DECLARE` 在 LVGL v9 的 `src/lv_api_map_v8.h:318` 展开为
`extern const lv_image_dsc_t var_name;`，而兼容头 `src/lv_api_map_v8.h:51` 有
`typedef lv_image_dsc_t lv_img_dsc_t;` —— **两种写法类型兼容、都能编译**。

所以 1.2 不是潜伏 bug，是**一致性债**：将来 LVGL 收紧 v8 兼容层时，只有一半代码会先坏；而且同一份产物里出现两种声明风格，读代码的人会以为它们语义不同。

## 2. 2B 落地：`CResourceDeclarations`

新建 `src/HmiEditor.Core/CodeGen/CResourceDeclarations.cs`（110 行），三类成员：

```csharp
// 符号名
ImageVariable(fragment)                       => img_<f>
FontVariable(fragment)                        => font_<f>
FontVariantVariable(fontFragment, size)       => font_<f>_<n>
ImagePathMacro(fragment)                      => IMG_PATH_<f>
FontPathMacro(fragment)                       => FONT_PATH_<f>
FontPixelSizeMacro(fragment)                  => FONT_PXSIZE_<f>

// 声明语句
ImageExtern(fragment)                         => extern const lv_img_dsc_t img_<f>;
ImageDeclare(fragment)                        => LV_IMG_DECLARE(img_<f>);
FontExtern(fragment)                          => extern lv_font_t font_<f>;
FontVariantExtern(fontFragment, size)         => extern lv_font_t font_<f>_<n>;

// 定义语句
FontDefinition(fragment)                      => lv_font_t font_<f>;
FontVariantDefinition(fontFragment, size)     => lv_font_t font_<f>_<n>;
FontPointerDefinition(fragment)               => static const lv_font_t* font_<f> = NULL;
FontPointerVariantDefinition(fontFragment, size)

// 路径 / 尺寸宏定义
ImagePathMacroDefine(fragment, fileName)      => #define IMG_PATH_<f> "A:" SIM_ASSET_DIR "/<file>"
FontPathMacroDefine(fragment, fileName)       => #define FONT_PATH_<f> SIM_ASSET_DIR "/<file>"
FontPixelSizeMacroDefine(fragment, pixelSize) => #define FONT_PXSIZE_<f> <n>
```

`FontPathMacroDefine` **不加** `"A:"` 前缀 —— `lv_freetype_font_create` 内部走 `FT_New_Face` 直接访问文件系统，不经过 LVGL 的 `LV_FS_STDIO`。这处差异此前只体现在注释里，现在由方法名与文档注释承担。

### 覆盖范围（有意排除）

- **不覆盖** `_img_<fragment>_empty_map`：仅导出侧两处（定义 + 引用），无重复可言。
- **不覆盖** `lv_image_set_src(...)` 调用语句：含宿主 API 语义，不属于符号命名。
- 但 `lv_image_set_src(obj, IMG_PATH_x)` 里的**宏名**改走 `ImagePathMacro(fragment)`。

## 3. 2C 落地：CodeGenerator 接入符号表

`CodeGenerator.CollectUsedFontSizes`（14 行算法）与 `CResourceSymbols.CollectFontVariants` **逐字重复**。CodeGenerator 的两个生成入口现在都构造符号表：

```csharp
var symbols = CResourceSymbols.Create(imageAssets, fontAssets, project);
var availableImageIds = symbols.Images.Select(i => i.Fragment).ToHashSet();
var availableFontIds  = symbols.Fonts.Select(f => f.Fragment).ToHashSet();
```

带来的收敛：

| 项 | 改造前 | 改造后 |
|---|---|---|
| `CollectUsedFontSizes` | 14 行算法 + 3 处调用 | **删除**，用 `symbols.FontVariants` |
| `availableImageIds` 构造 | 2 处手写净化 | 从表派生 |
| `availableFontIds` 构造 | 2 处手写净化，且**每个 screen 重算一次** | 从表派生，**提升到循环外只算一次** |
| 变尺寸字体的磁盘文件名 | `fontAssets.Where(f => CIdentifier.ForAsset(f.Id) == fontId).Select(取文件名).FirstOrDefault()` | 查表 `symbols.Fonts.FirstOrDefault(f => f.Fragment == ...)?.FileName` |

`CodeGenerator.cs` 3411 → **3380** 行。

### ⚠️ 更正上一轮文档的一处判断

阶段 2A 的文档（§7）与本项目记忆里写过：

> 2C 的注意点：生成器侧会用 `availableFontIds` 做存在性过滤，导出侧**不过滤**，统一时必须显式建模该差异（`bool filterToKnownFonts`）。

**这个判断是错的。** 存在性过滤不在 `CollectUsedFontSizes` 里，而在另一个函数 `GetControlFontExpression`：

```csharp
if (availableFontIds != null && !availableFontIds.Contains(sanitizedId))
    return null;   // 资源不在库中 → 回退主题默认字体
```

`CollectUsedFontSizes` 与 `CollectFontVariants` **没有任何差异**，是干净的同体重复，直接合并即可，不需要开关参数。当时把两个函数的行为混在了一起。

`GetControlFontExpression` 的过滤逻辑本轮**未改动**（它决定的是「控件用哪个字体表达式」，不是「要生成哪些字体符号」，职责不同）。

## 4. 负向验证

| # | 破坏动作 | 结果 |
|---|---|---|
| NV-A | 把单一来源的前缀 `font_` 改成 `fnt_`（`FontVariable`） | **恰好 4 项红**（`CodeGenTests.GenerateSharedLibraryProject_ThemeUsesProjectFontAsset` + `ExportResourceSymbolTests` 3 项）；**跨链路契约测试 `CodeGenAssetSymbolContractTests` 全绿** |
| NV-B | 导出侧**只让「句柄定义」一处**绕过构造器手写 `fnt_{fragment}` | **恰好 2 项红**（`ArtifactFragments_EqualTheSymbolTableExactly` + `MainC_DefaultFontPointer_RefersToAnExternedFont`）；契约测试仍绿 |

**NV-A 是 2B 价值的直接证据**：漂移在结构上已不可能发生 —— 两条链路取的是同一个来源，所以改前缀会**一起改**，跨链路契约自然成立。而「有人偷偷改了公共前缀」这件事仍被 pin 住（so 模式守卫 + 产物自洽守卫报红）。

**NV-B 证明守卫仍能抓局部绕过**：有人绕开构造器手写时，产物内部立刻矛盾并被捕获。

两处破坏均已还原，`grep NEGATIVE-VERIFY` 与 `grep fnt_` 均无残留。

## 5. 产物逐字节等价校验

2B/2C 是纯重构（搬移 + 换构造器），产物应一字不差。这次的 fixture 比阶段 2A 更厚，覆盖 **3 条生成路径**：

| 产物 | 覆盖点 |
|---|---|
| `generated_hmi.c`（`GenerateSimulatorModule`） | 模拟器模式的 extern 声明、IMG_PATH 宏、变尺寸 extern |
| `shared_library.c`（`GenerateSharedLibraryProject`） | **so 模式**的 `static const lv_font_t*` 指针定义、`hmi_resolve_font` 初始化 |
| `theme.c`（`GenerateThemeCode`） | `fontToUse` 的 `_soMode` 三元表达式 |
| `export/`（9 个文件，`SimulatorProjectExporter.Export`） | main.c / generated_images.c / generated_fonts.c / CMakeLists / lv_conf / sim_config / comm / README |

fixture 含 3 图 + 3 字体（需净化 id `img-1`/`font-1`、中文 id `图片2`/`中文字体`、空 id），以及两个非主题字号控件（`FontSize` 22 / 30）产生变尺寸符号；另有带背景图的屏。

`git stash -u -- src/` 切到 `67f524b` 与新代码各导出一次：

```
diff -r eq-old eq-new   → 无差异（22 个文件）
```

**逐字节零差异**，含注释、宏顺序、空行、文件顺序。

## 6. 验证基线

`env TMPDIR=<磁盘目录> dotnet test HmiEditor.slnx -c Release`：

| 项目 | 项数 |
|---|---|
| HmiEditor.Core.Tests | 1370 |
| HmiEditor.AI.Tests | 237 |
| HmiEditor.App.UiTests | 91 |
| Hmi.Communication.Tests | 75 |
| HmiEditor.Device.Tests | 63 |
| HmiEditor.Simulator.Tests | 41 |
| Hmi.Hardware.Tests | 22 |
| **合计** | **1899，0 失败** |

Release 构建 0 警告 0 错误。

> flaky 观察（与本改动无关，依赖真实定时器）：`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`、`AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay` —— 并行跑全量时偶发；单独复跑 `AlarmEngineTests|DataLoggerTests` 45/45 全绿。

## 7. 后续

| 阶段 | 内容 | 状态 |
|---|---|---|
| 2A / 2D | 导出侧资源符号表 + 断言收紧 | ✅ 完成 |
| 2B / 2C | `CResourceDeclarations` 统一命名与发射 + CodeGenerator 接入符号表 | ✅ 完成 |
| **3** | `CodeGenerator.cs`（3380 行、零 partial）按域拆分 | 待做 |

阶段 2 全部收口。**P2 的另一条主线仍未开工**：权限/凭据闭环（`hmi_actions.c` 里报警/趋势/登录有实现，权限与凭据校验是 TODO）、设计-代码单一事实源（design-previews 无共享模型）、Dock 拖拽空白收尾、AI 流式。

**阶段 3 的前置纪律**（沿用本项目惯例）：拆 `CodeGenerator` 前先盘现有覆盖，确认 3 条生成路径都有行为网 —— 本轮已确认 `CodeGenTests` 覆盖 so 模式、`CodeGenAssetSymbolContractTests` 覆盖模拟器↔导出器契约、`ExportResourceSymbolTests` 覆盖产物内部自洽；但**生成器自身的按屏/按控件发射逻辑覆盖情况尚未盘点**，这是阶段 3 的第一步。
