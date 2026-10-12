# 设计-代码单一事实源收尾（2026-09-18，P0-4）

## 1. 背景

09-14 审查把「设计-代码单一事实源缺失」列为 **D7 / P2-3**：
`design-previews/` 里躺着设计稿（`.design` JSON + `colors_and_type.css` 设计 token），
而**编辑器从不消费它们** —— 设计稿与已实现主题（`PaletteDefaults`）无任何共享模型，
两边各写各的，没有任何机制会发现它们已经漂移。这是 09-14 P2 里**唯一仍开着**的
架构级软件侧项。

## 2. 先量事实：设计稿与实现主题**本来就不同**

动手前先量清楚，避免"让设计稿驱动实现"这种会**改变已发布外观**的动作：

| | 设计稿 `colors_and_type.css`（R2 预览） | 已实现 `PaletteDefaults` |
|---|---|---|
| 主色 | `#3B71F5`（靛蓝） | `#2563eb`（蓝） |
| 背景 | `#0D0E12`（近黑） | `#131c31`（深蓝） |
| 文本 | `#E4E6EB` | `#e2e8f0` |
| 边框 | `#2A2D38` | `#334155` |

**结论**：设计稿是**提案**，不是已实现主题 —— 两边是有意分歧。
因此"单向流入"**不能**实现成"把 PaletteDefaults 的值改成 CSS 的值"
（那会改掉已发布界面外观，不是零行为风险）；只能实现成
**机器可检的对应关系**：设计稿改了 → 守卫变红 → 人**有意识地**决定
采纳（更新目录/主题）还是记录分歧。

## 3. 交付物

### 3.1 `DesignTokens`（`src/HmiEditor.Core/Design/DesignTokens.cs`）

把 CSS 的 `--hmi-*` 色板 token 结构化为**代码侧镜像目录**：
每条 = 语义角色 → CSS 变量 → 预览值 → 对应的 `PaletteDefaults` 常量（可空）→ 注释。

- `PaletteConstant` 为 **null** 表示"设计稿有这个角色、已实现主题没有"
  （如 Popover / Input / Ring）—— 这是**记录**，不是缺陷；
- 值不同时注释写明"有意分歧"——分歧从"靠人记住"变成"写在目录里、由测试看守"。

### 3.2 `DesignTokenCatalogTests`（8 条）

| 用例 | 判据 |
|---|---|
| `Catalog_EveryTokenExistsInCss_WithMatchingValue` | 目录每条 token 的 CSS 变量**必须存在于 CSS 且记录值与 CSS 一致** —— 这是"单向流入"的机制本身 |
| `CssParser_FindsAllColorTokens` | 自检：解析器必须扫到 ≥10 个色板 token、目录 ≥10 条（防正则/目录退化后空跑） |
| `Catalog_MappedPaletteConstants_AllExist` | 目录登记的 `PaletteDefaults` 常量必须真实存在（反射） |
| `Css_IsNotSilentlyConsumedByEditor_ExplicitCorrespondenceOnly` | **把"设计稿与实现是两套"变成显式可审的对应关系**：两边色值重叠 ≤2，否则说明设计稿色静默进了实现 |
| `DesignFile_HasStructuredModel`（×2） | 两个 `.design` 是合法 JSON 且具备 `data[]`（含 id/type/version）+ `config` 结构 |
| `DesignLayer_NoBareHex_OutsideTheTwoTokenSources` | **设计层无裸 hex**：`Core/Design/*.cs` 除 `PaletteDefaults.cs`/`DesignTokens.cs` 外不得散写 `"#rrggbb"` |
| `NoBareHexGuard_ItselfDetectsHex` | 自检：判据真能识别裸 hex，且允许文件里确实有 hex（扫描面非空转） |

## 4. 负向验证（3/3 命中）

| NV | 注入 | 结果 |
|---|---|---|
| A | 把 CSS 的 `--hmi-primary` 改成 `#FF0000`（**模拟设计师改稿**） | ✅ `Catalog_EveryTokenExistsInCss_WithMatchingValue` 红并列出 `--hmi-primary` 目录值与 CSS 值；还原 → 绿 |
| B | 往 `ArcLayout.cs`（设计层非 token 文件）追加一行裸 hex | ✅ `DesignLayer_NoBareHex_OutsideTheTwoTokenSources` 红并点名文件；还原 → 绿 |
| C | 把目录里 `nameof(PaletteDefaults.Primary)` 换成抄错的字面量 `"PrimaryXyz"` | ✅ `Catalog_MappedPaletteConstants_AllExist` 红并点名；还原 → 绿 |

> **NV-C 的设计细节**：目录用 `nameof(PaletteDefaults.X)`，抄错常量名**编译不过** ——
> 编译器已经比反射检查更强。所以这条守卫真正防的是"有人把 `nameof` 换成字面量"
> （那才让抄错变成运行期才暴露），NV-C 注入的正是这个形态。

## 5. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release --no-incremental` | **0 警告 0 错误** |
| 全量测试（7 程序集） | 基线 2720 ⇒ **+8**（本轮新增 8 条守卫） |
| 负向验证 | **3/3 命中**，每次还原后复查 |

## 6. 范围边界（如实说明）

- **没有**把设计稿色值改成实现主题色值 —— 那会改已发布外观，不是零行为风险；
  本轮交付的是**对应关系与守卫**，值的有意分歧被写进目录注释。
- **没有**让编辑器运行时消费 `.design` 文件（那要改加载路径与模型，属独立议题）；
  本轮只把 `.design` 变成**结构受检**（JSON 形状 + 必需字段）。
- 若要真正"设计稿驱动实现"，需三步一起改（测试断言 + 目录映射 + 两侧色值），
  这条路径已写在 `Css_IsNotSilentlyConsumedByEditor_…` 的注释里，避免后人只改一处。

## 7. 登记表

`docs/2026-09-17-open-items-register.md` §4（09-14 P2-3）随之闭环；
登记表新增 P0-4 一行并写明范围边界。
