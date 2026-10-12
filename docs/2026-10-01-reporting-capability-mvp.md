# 报表能力：现状、选型与**最小可行面**（阶段 171 定义 → **阶段 172 已落地第一片**）

> **决策（2026-10-01）**：产品方向选了 **A 报表能力**（登记册 §2 第 9 项："仅 CSV 导出，无报表模板/打印"）。
> 本文件是**动手前的定义**：现状读数 → 选型的硬约束 → MVP 边界 → 判据计划。
> 结论一句话：**不引入任何新依赖**——用「CSV ＋ 自包含 HTML（浏览器打印）」两件套，
> 先在**审计日志**这一条数据源上打通。

---

## 1. 现状读数（阶段 171 实测，不是转述）

| 面 | 现状 | 位置 |
|---|---|---|
| **配方 CSV** | ✅ **唯一的 CSV 导出**：`ExportCsv()` 输出 `RecipeName,RecipeVersion,GroupName,<变量…>`，有转义 `EscapeCsv`，可**往返**导入（`ImportCsv` 解析同一份） | `src/HmiEditor.Core/Services/RecipeManager.cs:198` |
| **审计日志** | ❌ **导不出**：面板只有"刷新 / 过滤"两个入口 | `src/HmiEditor.App/Views/AuditLogView.axaml.cs:26-27` |
| **报警记录** | ❌ 无导出（面板是报警**配置**与运行期列表） | `src/HmiEditor.App/Views/AlarmListView.*` |
| **历史数据（趋势）** | ❌ 编辑器侧无导出；`DataLogger` 有采样（测试里当数据源用），设备侧 `DataLogConfig` 只落 SD 卡 | `src/HmiEditor.Core/Models/Trend/DataLogConfig.cs` |
| **设备侧** | ❌ 固件里**没有** CSV/报表代码（`grep -rn "csv" firmware/…/runtime/*.c` = 0 命中） | `firmware/t113/app/runtime/` |
| **菜单** | 只有"导出 C 代码""导出模拟器工程"两条 | `src/HmiEditor.App/MainWindow.axaml:24,29` |
| **打印 / PDF** | ❌ 没有任何打印栈、没有 PDF 依赖 | —— |

---

## 2. 选型的硬约束（本仓自己的纪律，逐条都影响结论）

1. **中央包版本管理**：`Directory.Packages.props` 统一钉版本 ⇒ 加依赖要动它；
2. **离线可用**：设备/现场环境不能假设有网络或浏览器**插件**；
3. **可判**：本仓要求每个行为面有判据（headless 能跑）⇒ 选型必须让**产物内容**可逐字节断言；
4. **许可**：仓库是随产品交付的 ⇒ 依赖许可必须干净；
5. **产物可复现**：与"同一工程两次导出逐字节一致"同一族纪律 ⇒ 报表产物也应当**确定性**（时间戳之类的
   非确定字段要能被钉住或显式排除）。

---

## 3. 技术选型（对比 ＋ 结论）

| 方案 | 许可 | 能打印？ | 离线？ | 判据友好？ | 结论 |
|---|---|---|---|---|---|
| **A. CSV ＋ 自包含 HTML（零依赖）** | —— | HTML 走**浏览器打印**（系统对话框） | ✅ | ✅ 产物是文本，可逐字节断言 | ✅ **选它** |
| B. QuestPDF（PDF） | 社区版有营收门槛 | ✅ | ✅ | 中（二进制 PDF 需解析或只断言结构） | 暂不（许可门槛 ＋ 体积） |
| C. EPPlus / ClosedXML（xlsx） | EPPlus 商用；ClosedXML MIT | 需 Excel | ✅ | 中 | 暂不（本仓不需要 xlsx 的交互能力） |
| D. Scriban / Razor（模板引擎） | MIT / Apache | 取决于产物 | ✅ | ✅ | 暂不（先把模板形状定下来；占位符替换 20 行足够） |
| E. 设备侧打印/报表 | —— | ❌（T113 无打印栈） | ✅ | 弱 | **明确不做** |

**结论**：**A**。报表 = **CSV（数据）＋ 自包含 HTML（人看/打印）**，模板用**极小的占位符替换**
（`{title}` / `{generated_at}` / `{columns}` / `{rows}`），HTML 内联样式、**不引用任何外部资源**
（离线也能打开与打印）。

---

## 4. 最小可行面（MVP）

**第一条数据源：审计日志**（它已经在内存里、字段稳定、也无导出入口 —— 收益最直接）。

1. **Core 侧纯函数**（可判、无 UI）：`ReportExporter`（暂定 `src/HmiEditor.Core/Reporting/`）
   - `ToCsv(ReportTable table)`：RFC4180 转义（引号/逗号/换行）、`\r\n` 行尾、**UTF-8 BOM**（Excel 友好，
     且与配方导出的既有选择保持一致或明确写出差异）；
   - `ToHtml(ReportTable table, string title, DateTimeOffset generatedAt, CultureInfo? culture)`：
     自包含 HTML（内联 CSS、`<meta charset="utf-8">`、无外链）；
   - `ReportTable` = 标题 ＋ 列（键 + 显示名）＋ 行（字符串值）—— **数据与呈现分离**，
     数据源只负责把模型翻译成 `ReportTable`。
2. **审计数据源**：`AuditLogReport.From(IReadOnlyList<AuditLogEntry>, ILocalizer)` —— 列顺序固定
   （时间 / 用户 / 角色 / 动作 / 结果 / 详情），时间按**可注入的格式**（默认 ISO-8601）。
3. **UI**：审计面板加一个"导出报表"按钮 → 文件选择器（`.csv` / `.html` 两个过滤器）→ 走
   `UiOperationGuard`（与既有导出同一条收口）→ 状态栏回报（入字典）。
4. **本地化**：标题、列名、"生成时间"标签、按钮/状态文案**全部入字典**（zh/en 各若干键）。

**明确不做**（写进登记，避免"顺手扩大"）：PDF/xlsx 引擎 · 定时/自动报表 · 邮件/上传 ·
设备侧打印 · 报表设计器（用户自定义布局）。

---

## 5. 判据计划（每条都能在 headless 里跑）

| 判据 | 判什么 |
|---|---|
| `ReportCsv_EscapesRfc4180` | 值含 `,` / `"` / 换行 / 前后空格时的转义；**正控**：不含特殊字符时**不**加引号（否则"全都加引号"也能过） |
| `ReportCsv_IsDeterministic` | 同一 `ReportTable` 两次序列化**逐字节相同**（可比性；时间字段由调用方传入 ⇒ 判据可钉） |
| `ReportHtml_IsSelfContained` | 产物里**没有** `<script src=` / `<link href=` / 外链 `http(s)://`（离线可打开）；**正控**：内联 `<style>` 必须在 |
| `AuditLogReport_ColumnOrderAndLocalization` | 列顺序固定、列名来自字典（切 zh/en 各渲染一遍逐字比对，沿用本仓既有的"双语对拍"手法） |
| `ReportExport_ProducesByteIdenticalFiles_ForSameInput` | 端到端（临时目录）：两次导出逐字节相同 |
| 源码契约 | UI 那一跳必须走 `UiOperationGuard`（与既有导出出口同形） |

**负控**（逐支独立日志 ＋ 还原 SHA256 逐字节相同）：把转义去掉 · 让 HTML 引入一个外链 ·
把时间戳直接取 `DateTimeOffset.Now`（破坏确定性）· 列名写死中文（绕过字典）。

---

## 6. 诚实边界（先写下来，省得以后假装做到了）

1. 报表**不是**"设备现场的班次报表"——设备侧不参与生成；产物来自**编辑器**持有的数据
   （审计/项目模型；将来接运行期日志时另说）。
2. **打印** = 打开 HTML 后由**浏览器/系统**打印，本产品不做排版保证（分页、页眉页脚交给浏览器）。
3. 本 MVP **不做** xlsx（公式/多 sheet）与 PDF（签名/归档规范）。
4. 时间与时区：产物里的时间由调用方传入并显式格式化；**不做**跨时区换算承诺。

---

## 7. 实施（**阶段 172 已完成第一片**）

实施读数与判据见 §8；本节的四步顺序已完成 1–4。

## 8. 已实施（阶段 172）：第一片 = 审计日志报表

| 步骤 | 落地 |
|---|---|
| ① `ReportTable` ＋ `ReportExporter` | `src/HmiEditor.Core/Reporting/ReportTable.cs` / `ReportExporter.cs`：`ToCsv`（RFC4180 转义、**CRLF**、`
` 行尾）、`ToCsvBytes`（UTF-8 **BOM** ＋ 正文）、`ToHtml`（**自包含**：内联 `<style>`、零外链）、`FormatUtc`（UTC ＋ 显式 `Z`，不随区域） |
| ② 审计数据源 | `src/HmiEditor.Core/Reporting/AuditLogReport.cs`：列序固定；**Core 不认识语言**（列名/标题/结果全走 `localize`）；App 侧 `Services/AuditReportText.cs` 做「Core 键 → `Lang` 常量」映射 |
| ③ UI | 审计面板加「导出报表」按钮（`AuditLogView.axaml`）＋ 处理函数走 `UiOperationGuard.Run(Lang.UiOpExportAuditReport, …)`；按所选扩展名判 CSV / HTML；**导出"当前列表里显示的那些"**（与筛选语义一致）；生成时间在调用点取一次 |
| ④ 判据 ＋ 负控 | Core 9 条（转义含正控 / CRLF / BOM / 确定性 / 自包含含正控 / HTML 转义 / 列序 / 行内容 / 确定性）＋ App 4 条（键可达含回落即红 / 列名一致 / **接线契约** / 端到端逐字节）＋ 负控 **5 支** |

### 8.1 两处对定义稿的更正（实测说了算）

1. **列集合**：定义稿写"时间/用户/**角色**/动作/结果/详情"，而 `AuditLogEntry` **没有角色字段**
   （实际字段：`UserId/Username/Action/Detail/Success/Source/SessionId/Metadata`）⇒ 按真实字段定列：
   **时间 / 用户 / 操作 / 结果 / 来源 / 详情**（不硬凑一个不存在的列）。
2. **列名与结果词＝复用，不新造**：列名复用 `user.mgr.audit.*`（用户管理器审计页那一组词）、
   结果复用 `gen.success` / `gen.failed` ⇒ 新增字典键只有 **9 个**（标题/生成时间/按钮/另存标题/
   两个文件类型/成功/空/操作名）。⚠ **副作用（正面）**：这次复用让 **8 个此前"没有代码引用"的
   `Lang` 常量变活** ⇒ `LangConstantUsageGuardTests.UnusedBaseline` 按纪律**下调 1104 → 1097**
   （不是"顺手删死常量"，注释里逐个点名了那 8 个）。

### 8.2 判据与负控清单

| 面 | 判据 |
|---|---|
| CSV | `ToCsv_EscapesRfc4180`（**正控**：不含特殊字符时**不**加引号）· `ToCsv_UsesCrlfAndEndsEveryRow` · `ToCsvBytes_PrependsUtf8Bom` · `ToCsv_IsDeterministic` |
| HTML | `ToHtml_IsSelfContained`（无 `script/src/href/http(s)`，**正控**：内联 `<style>` 在）· `ToHtml_EscapesCellText` |
| 审计表 | `ColumnOrderAndNames_ComeFromKeys` · `RowCells_FollowTheSameColumnOrder_WithUtcTimeAndLocalizedResult` · `From_IsDeterministic` |
| App | `EveryCoreKey_ResolvesToLocalizedText_InBothDictionaries`（两本字典都在 ＋ 映射**不许回落成键名**）· `TableColumns_FollowCoreOrderAndAppText` · `ExportPath_GoesThroughUiOperationGuard_AndIsWired`（源码级三处）· `Export_IsByteIdentical_ForSameInput`（含真落盘一次到 `TestTempDir`） |

**负控 5 支**（`.workbuddy/tmp/s172-nc.py`，逐支还原 SHA256 逐字节相同）：去掉 CSV 转义 ·
HTML 塞外链 · 时间退回本地 `DateTime.Now` · App 映射回落成键名 · 面板绕过 `UiOperationGuard`
⇒ 逐支红在期望那条腿。

### 8.3 仍未做（第二片候选）

报警记录报表 · 历史数据（趋势采样）报表 · 报表范围/列选择对话框 · 定时/自动导出 ·
PDF/xlsx（仍在"明确不做"里，除非产品另有要求）。

## 9. 下一步（原计划，已由 §8 完成 1–4）

1. `ReportTable` ＋ `ReportExporter`（CSV/HTML）＋ 上述前 3 条判据（Core.Tests）；
2. `AuditLogReport` 数据源 ＋ 列名入字典 ＋ 双语对拍判据；
3. 审计面板按钮 ＋ `UiOperationGuard` 收口 ＋ 源码契约判据；
4. 端到端（临时目录）判据 ＋ 负控 4 支；门禁 ＋ 文档 ＋ 分笔提交。

（登记册条目：`#86`，见 §87。）


---

## 9. 阶段 173：PDF / xlsx（产品拍板"要做"）

### 9.1 两条路线各自的证据

| | xlsx | pdf |
|---|---|---|
| 选型 | **手写 SpreadsheetML**（BCL 的 `ZipArchive` ＋ 手拼 XML），零依赖 | **SkiaSharp 的 PDF 后端**（MIT；`HmiEditor.App/project.assets.json` 里**已有** `SkiaSharp/3.119.4`，Avalonia 的 Skia 栈带的） |
| 为什么不是库 | 报表只要"一张表 ＋ 表头格式化"；ClosedXML/EPPlus 还牵 DocumentFormat.OpenXml / SixLabors 许可 | 手写 PDF 要自己实现 TrueType 子集化 ＋ CID 字体 ＋ ToUnicode，代价远大于用已在场的 Skia |
| 确定性 | ✅ 逐字节（zip 条目时间戳写死 ＋ 顺序固定） | ✅ 固定 creation 时逐字节（实测两次导出相同） |
| 中文 | 不涉及字体（文本进 XML） | ✅ 嵌入字体（含 `/ToUnicode`）；字体找不到 ⇒ **明确提示**，不生成豆腐块 |

探针读数（阶段 173）：`typeface=Noto Sans CJK JP`、`审/计/报/表` 字形 id 非 0、`%PDF-1.4`、
含 `/ToUnicode`、固定 creation 时两次导出**逐字节相同**（11.5 KB / 一行文本）。

### 9.2 两处**不能按直觉断言**的地方（都写进了判据注释）

1. Skia 把字体字典/字体流放进**压缩对象流** ⇒ raw 扫描**看不到** `/FontFile2`；
   "字体真的嵌进去了"改由**体积下限**钉（纯拉丁 14.0 KB、4 个汉字 18.1 KB、60 个汉字 19.5 KB
   —— 小文本却有十几 KB 只能是字体载荷）。刻意**不**钉"子集随字形数线性增长"（实测 4→60 个汉字只 +7%）。
2. 只比"两次调用字节相同"**抓不到**"实现里改读时钟"（同一秒内字节相同，负控 nc2/nc5 第一版就是这么漏的）
   ⇒ 两个写出器都补了**结构断言**：xlsx 的 zip 条目时间戳不许是"现在"、PDF 的 `/CreationDate`
   必须是**传进去的那个值**。

### 9.3 UI 与判据

导出格式 **2 → 4 种**（`.csv`/`.xlsx`/`.pdf`/`.html`，默认 CSV），文件类型选择器给四种过滤名；
PDF 找不到可嵌入字体时给明确提示（新增 3 个字典键）。
判据：Core `XlsxReportWriterTests` 6 条 ＋ App `PdfReportWriterTests` 7 条 ＋ 四格式契约 1 条；
**负控 5 支**全红（xlsx 不转义 / xlsx 时间戳改成现在 / pdf 不画文本 / pdf 去掉分页 / pdf creation 读时钟）。

### 9.4 顺带修掉一处**环境把门禁判红**的坑

加显式 `PackageReference Include="SkiaSharp"` 后构建日志出现 **42 条 NU1900**（NuGet 审计取不到漏洞数据：
本机 HTTP 缓存在工作区外、只读）。根因**不是**那条引用 —— 而是 **restore 的警告会写进
`project.assets.json` 并在**之后的每次构建**重放**；把 `NUGET_HTTP_CACHE_PATH` 指到工作区内后
`dotnet restore --force` 重生成 assets ⇒ 构建日志 0 警告 ✓。
`scripts/ci-gate.sh` 的 restore 前已把它指到 `obj/nuget-http-cache`（.gitignore 覆盖）：
**不关审计、不改工程配置**，只给受限环境一个可写的缓存位置。


---

## 10. 阶段 174：第二片 = 报警记录报表 ＋ 导出公共出口

### 10.1 为什么先做"公共出口"而不是直接抄一份

`AuditLogView` 里原先内联着"扩展名 → 写出器"的 switch；报警面板再写一份就是**两份实现**，
而本仓的教训清单里正有一条"同一实现两份必然漂移"（登记 §12.7）。⇒ 提成
`App/Services/ReportExport.cs`：`Render`（纯函数、可判）＋ `SaveAsync`（选择器 ＋ 写盘 ＋ 回报）＋
`FileTypes`。顺带把**格式词**从 `audit.*` 提到 `report.*`（它们不是审计的词）。

### 10.2 报警报表

列序 **时间 / 报警名称 / 级别 / 状态 / 触发值 / 确认人**；级别与状态**走键**
（`alarm.level.*` / `alarm.state.*`，新增三个状态词；`Inactive` 复用既有）；
触发值**原始字符串优先**（带单位），没有才退不变文化数值；导出的是面板**筛选后**的那一份
（`FilteredRecords()` 被抽成方法，"显示"与"导出"共用同一口径）。

### 10.3 判据与负控

Core 5 条（含"行里不许出现 `Critical`/`Active`"这条反向断言、四个级别/四个状态键**互不相同**）＋
App 5 条（键两本字典都在且不回落 · 表列名 == 映射 · **公共出口四格式 ＋ 未知回落 CSV ＋ PDF 缺字体提示** ·
**两面板都走公共出口且面板里没有写出器** · 按钮接线）＋ 负控 4 支（级别直显枚举 / 列名回落 /
PDF 缺字体硬写 / 未知扩展名不回落）。

### 10.4 撞上的两处守卫（都按守卫说的改）

① 按钮效果普查表要求给新按钮声明效果 ⇒ `Never`（点开文件选择器，headless 无对话框），
理由写明"接线由源码契约判据覆盖"；
② 断言风格守卫禁止带括号的调用文本断言 ⇒ 改片段断言（`CSource` 结构化断言只在 Core.Tests 工程）。

### 10.5 仍未做

历史数据（趋势采样）报表 · 报表范围/列选择对话框 · 定时/自动导出。


---

## 11. 阶段 175：第三片 = 历史数据（趋势采样）报表

### 11.1 与前两片不同的地方：列是动态的

列 = **时间 ＋ 每个变量一列**，而"变量与值的对齐"只有 `DataLogger.Config.VariableIds` 说得准
（用户中途改画笔**不会**改已采样值的对齐）⇒ 报表的列名必须取**采样时**的那一份；用当前画笔会让
列与值**错位，而且不报错**。为此把"画笔 → 变量 Id 顺序"抽成 `EnabledVariableIds()`，
开记录与导报表共用一处。

### 11.2 三条刻意的边界

1. **变量列名不翻译**（用 Id 原文）—— 数据标识符，与登记 `#27c` 同一条边界；
2. **缺值留空，不补 0** —— 0 是"确实采到 0"，两者必须可区分（判据里有反向断言：整张表不出现 `"0"`）；
3. **行数封顶**（默认最近 5000 条）：环形缓冲可能几万条，导全量会产出几十 MB 并卡住 UI；
   截断**不留痕**（多一行说明会把列数搞乱），需要全量的走设备侧 SD 卡 CSV。

### 11.3 判据与负控

Core 6 条（列序/对齐/留空/不变文化/**截断保留最近**＋不封顶正控/无变量只剩时间列/确定性）＋
App 3 条（键两本字典在且不回落 · 公共出口能渲染它 · 接线契约：走公共出口、面板无写出器、
列名取 logger 配置、按钮接线）；负控 4 支全红。

### 11.4 与设备侧 CSV 的关系（新登记 `#86`）

`DataLogger.ExportCsv()`（`Core/Services/DataLogger.cs:190`）**产品侧零调用**；设备侧的 CSV 由
**生成的固件**（`DataLogExportCsvFn` ＋ `DataLogConfig.CsvDelimiter`）另写一份 —— 两者是**不同产物**：
设备侧要可配分隔符、ISO 时间、F3 值（SD 卡上给别的工具读），编辑器报表要 BOM/CRLF/四格式/确定性。
本阶段**不合并**，只登记（三种归宿待选：删掉 / 让报表走它 / 留作设备侧镜像）。


---

## 12. 阶段 176：把 `DataLogger.ExportCsv()` 接活（用户选「接」）

### 12.1 先量后接：两件决定性的事

| 量 | 读数 | 结论 |
|---|---|---|
| 设备 CSV 的时间基 | `s->timestamp_ms = lv_tick_get();`（`CodeGenerator.Trend.cs:92`）＝**开机时长 ms** | 编辑器只有墙钟 ⇒ **做不出**逐字相同的镜像 ⇒ 表头写 `timestamp_utc_ms`（写明口径、不冒充 `timestamp_ms`），**列序**与设备一致 |
| `DataLogConfig.CsvDelimiter` 谁在用 | **只有那个死方法**；生成的设备 CSV 把它**硬编码成逗号**（实测：配置 `;` 也照样写 `,`） | 这才是真正「该接」的线：字段文档写「CSV 导出分隔符」，而设备 CSV 是设备侧唯一的 CSV 出口 ⇒ **硬编码让这个设置完全无效**（顺手修掉的真缺陷） |

### 12.2 接法

1. **设备 CSV 用配置分隔符**（表头/变量列/`%.3f` 行都取 `logger.CsvDelimiter`；空则回落逗号）；
2. **`ExportCsv()` 重写成设备列序**（`timestamp_utc_ms` ＋ 变量 Id、`F3`、行尾 `\n`、空分隔符回落逗号）
   ＋ 新增 `ExportCsvBytes()`（UTF-8 BOM；**空数据仍是空数组**）；
3. **进产品**：`ReportTable.CsvOverride`（这条数据源在 CSV 上有更合适的形态时带上）＋
   `ReportExport` 的 CSV 分支**优先用它**（其余三种格式仍走报表列）＋ 趋势面板传
   `_logger?.ExportCsvBytes()` —— 这就是「**让编辑器报表走它**」。

### 12.3 判据与负控

Core `DataLoggerCsvTests` 5 条（表头时间基/分隔符、空分隔符回落、BOM 与空数据、**UTC epoch 毫秒用固定时刻核对**、
确定性）＋ CodeGen 3 档（`,;|` 三档 ＋ 非逗号时反向断言 ＋ 前提自检）＋ App 1 条接缝（带覆盖 == 覆盖；
**正控**：不带覆盖仍是通用 CSV；xlsx 不受覆盖影响）＋ 接线契约；**负控 4 支**全红。

### 12.4 门禁抓到的两处（我只跑了 Debug 的过滤子集，没跑全量 —— 教训）

1. **既有测试断言旧格式**：`DataLoggerTests.Logger_ExportCsv_ProducesValidFormat` 断言表头含 `Timestamp`
   ⇒ 已按新契约改为 `timestamp_utc_ms`（并注明为什么改）；
2. **断言风格守卫**：新判据里的"精确调用文本 ＋ 字符串包含"被禁 ⇒ 改用 `CSource.AssertText/AssertNoText`
   （Core.Tests 里够得着那套结构化断言）。
   🩸 教训：**改了 Core 就得跑全量 Core**（Debug 下的过滤子集把这两条藏在门禁里）。


---

## 13. 阶段 177：第四片 = 报表列选择（导出前勾列）

### 13.1 落地

- **Core `ReportTable.WithColumns(keys)`**：列按调用方顺序保留、行跟着投影；**未知列键忽略**（旧配置的列名
  不该把用户挡在门外）；重复键只取一次；**一个都不剩 ⇒ 抛**（界面上不允许取消掉所有列）；
  `CsvOverride` **原样带过**（列选择对整表 CSV 形态不生效）。
- **App**：纯接缝 `NeedsColumnChoice`（只有一列就不弹）与 `ApplyColumnSelection`；
  对话框 `ReportColumnDialog`（默认全选、全选/全不选、**零勾选时确定不可点**、结果只回列键且顺序沿用原表）；
  三个面板导出前都过 `ChooseColumnsAsync`，**取消即中止导出**。

### 13.2 新窗口自动进两道普查面（守卫教的）

1. `WindowViewSmokeUiTests`（按类型关系自动收面）报：外壳不符 `ChromeWindow` 契约（要求 Content 是
   Border、其 Child 是 Grid），实测 DockPanel —— 并说明这是**静默**缺陷（`InjectResizeGrips` 直接 return，
   症状是窗口拖不动边）。⇒ 按 `FontSubsetView` 的外壳重写。
2. `WindowProductionPathUiTests.EveryConstructibleWindow_IsOnTheProductionPathTable` ⇒ 表里加一条。
3. 门禁又抓 `CS0108`：`ChromeWindow` **本身就有** `OnTitleBarPointerPressed` / `OnTitleCloseClicked`
   ⇒ 删掉我重复声明的两个处理器（XAML 绑到基类那两个即可）。
4. `UnusedBaseline` **1099 → 1102**（对话框三个常量只出现在 axaml 花括号里，同一形状第四次）。

### 13.3 判据与负控

Core 5 条（顺序/投影、未知键忽略＋去重、一个不剩抛、`CsvOverride` 带过、确定性）＋ App 5 条
（`NeedsColumnChoice` 只在多列时为真、接缝投影、对话框文案两本字典都在、**三面板接线契约**、
对话框控件齐全）＋ 负控 4 支（列序被忽略 / 接缝不投影 / 报警面板漏跳 / 恒弹）。

### 13.4 🩸 负控脚本自身的三条坑（本阶段真踩到，写进脚本头注释）

① 基线快照必须**开跑前取一次**（每支现取会把上一支的注入当基线 ⇒ 越跑越脏，工作树真留下过注入）；
② 每支要 `try/finally` 还原（脚本自己抛 `KeyError` 时还原那行走不到）；
③ **不要接 `head`**（SIGPIPE 会把它杀在还原之前）。另：注入必须**保持可编译**（删两行会让
`table = chosen;` 悬空 ⇒ 构建失败 ⇒ 负控无效）。


---

## 14. 阶段 178：第五片 = 时间范围（快捷档）

### 14.1 口径

- **`ReportTimeRange`**：两个可空端点的**闭区间**（`from <= t <= to`，端点算在内）；一律 **UTC**；
  `from > to` **抛** —— 静默给一张空表会让用户以为"这段时间真没数据"。
- **快捷档**（`ReportRangeOptions.Compute(choice, now)`，纯映射、`All ⇒ null`）：全部 / 最近 1 小时 /
  24 小时 / 7 天 / 30 天。**自定义起止日期本阶段不做**（诚实边界，留作后续候选）。
- **数据源各自过滤**：审计按 `Timestamp`、报警按 `OccurredAt`、历史按 `Timestamp`；
  **历史必须"先过滤再封顶"**（顺序反了会静默少给数据）。

### 14.2 🩸 判据太弱被负控抓出来（本片最值得记的）

nc3（"先封顶再过滤"注入）第一次**没红**：我挑的场景是"范围只覆盖**最早**那几条"，
两种顺序结果完全相同 ⇒ 判据恒绿。换成"范围覆盖**超出前 maxRows 条之外**的数据"
（10 条取最近 5 条、`maxRows=3` ⇒ 先封顶会得到**空**）立刻可区分。
⇒ 规矩：**判据的场景必须让两种实现产出不同读数**。

### 14.3 守卫逼出的更好设计

`WindowProductionPathUiTests` 要求"生产构造与设计器构造的字段形状可观测地不同" —— 我第一版把档位按钮
放进 `static` 字典（两个实例共享状态，本身就是坏味道）⇒ 改成**实例字段**（0 → 5 条）。
另：`WindowViewSmokeUiTests` 又一次要求 `ChromeWindow` 外壳（Content 是 Border、其 Child 是 Grid）。

### 14.4 判据与负控

Core 5 条（闭区间/UTC/单侧不设限 · `from>to` 抛 · 审计过滤 · 报警过滤 · 历史先过滤再封顶＋正控）＋
App 4 条（档位跨度映射 · 键双语 · 三面板接线含"范围真的传进数据源" · 对话框控件）＋ 负控 4 支。

### 14.5 仍未做

自定义起止日期 · 定时/自动导出 · 报表模板（多份报表打包）· 报表设计器。


---

## 15. 阶段 179：第六片 = 报表包（一个数据源 × 四格式 ＋ `index.txt`）

### 15.1 分工

「导出报表」= 精细挑一份（先问**范围/列**，再选**一种**格式）；「导出报表包」= 留档/交付
（**全列、全时段、四种格式**一次出全）＋ 一份 `index.txt` 记下"哪些文件、各多少行、哪些格式没出、为什么"。

### 15.2 三处口径

1. **文件名确定**：`<kind>-<yyyyMMdd-HHmmss><ext>`，时间由调用方给；非法字符/空白清成 `-`、全非法回落 `report`、
   **中文保留**（它只是文件名，不是路径分隔）；
2. **渲染不出来的格式记进索引**（缺字体 ⇒ PDF 跳过并写明原因），**不静默丢** —— 判据里还有一条
   反向断言"缺字体时目录里**没有** PDF 文件"（防"跳过"其实是"写了豆腐块"）；
3. **两次打包逐字节相同**（文件名、四份内容、索引都比）。

### 15.3 🩸 工具教训（本阶段最贵的）

我用 heredoc 脚本按"缩进字符串"改 C#，把三处 `OnExportReport` **劈成两半**（一次还留下重复的方法头），
而由于 `dotnet build ... -v q` 只看尾行，我一度以为"构建干净" ⇒ 后面才被编译错误抓住。
**规矩**：改 C# 先 `read` 看清上下文，再用 `edit` 做精确替换（不靠缩进猜锚点）；
**改完立刻看编译/测试退出码**，不要只看输出尾行。

### 15.4 仍未做

**定时/自动导出**（要设置项 ＋ "到期只跑一次"的时钟注入，天生易 flaky ⇒ 单独设计）· 自定义起止日期 ·
报表设计器。


---

## 16. 阶段 180：第五片的补齐 = **自定义起止日期**

阶段 178 的快捷档只覆盖"最近 N"，用户要"上个月那一段"就只能全导。本片把 `Custom` 那一档补齐。

### 16.1 四处口径

1. **右端是"整天"**：用户挑"从 9-28 到 10-01"指的是**两个整日**，所以左端 = 起始日 00:00、
   右端 = 结束日**最后一刻**（= 次日 00:00 减 1 tick）。若把右端当成 10-01 的 00:00，
   用户会得到一张**空表**（而且不报错）—— 这是最容易写错、也最难被用户理解的一种错。
2. **偏移必须留住**：`DatePicker.SelectedDate` 是 `DateTimeOffset?`，带的是**用户当地偏移**。
   按各自偏移转 UTC 后，东八区的"10-01 00:00"是 `09-30T16:00Z`，而 UTC 的"10-01 00:00"是 `10-01T00:00Z`
   —— **同一个日期、差 8 小时**。图省事按 `DateTime.Date` 当 UTC 用，判据里专门有一条把它钉死。
3. **两端可空 = 该侧不设限**（"从某天起" / "到某天为止"），但**两端都空**是调用方的编程错误：
   Core 抛（静默按"全部"会让用户以为设了范围），UI 侧确定键在这种输入下**不可点**（双保险）。
4. **旧的 `Compute(choice, now)` 遇到 `Custom` 也抛**：这个重载拿不到两个日期，回落成 `null`
   ＝"按全部导出"正是第 3 条要防的最坏形状 ⇒ 让它**编译期之外**立刻失败，逼调用方换重载。

### 16.2 一个刻意的"不读时钟"

对话框**不预填**"最近 7 天"这类默认日期：预填就要在这里读一次 `Now`，于是"当前时刻"有了**第二个来源**
（阶段 178 定的口径是"范围只由调用方用自己的 now 算"）。代价是"自定义 + 两端都空"成了**可达输入** ——
处置就是上面第 3 条的按钮禁用，而不是偷偷替用户选两天。

### 16.3 `ReportRangeText`：顺手把五个死常量救活

阶段 178 的对话框直接 `AppLocalization.Service[LabelKey(choice)]`（键在运行时拼），于是
`Lang.ReportRangeAll` / `LastHour` / `Last24h` / `Last7d` / `Last30d` 这五个常量**没有任何代码引用点** ——
只能靠 `LangConstantUsageGuardTests` 的基线（1110）兜着（那正是类注释说的"常量是键空间镜像"）。
本片把"Core 给键、App 推键"这一既有形状（同 `AuditReportText`）补上 ⇒ 五个常量变成**活引用**，
`UnusedBaseline` **1110 → 1105**（本集**第一次降**）。标题/提示仍留在 `{res:Loc}`（那里要的是**语言切换能刷新**）。

### 16.4 判据与负控

- Core `ReportRangeOptionsTests` **8 条**：快捷档跨度 · 自定义**整天**口径 · **偏移留住**（同一日期两种偏移差 8 小时）·
  单侧不设限 · 两端都空抛 · 起点晚于终点抛 · 档位重载**拒绝** `Custom` · 界面上每档都可算。
- App 新增 **2 条**：`ReportRangeText` 与 Core 的键**逐档相等**（两份抄本不许漂移）＋ 对话框**生产路径**
  走一遍四条腿（初始"全部" → 切"自定义"面板出现且确定被挡 → 填日期能把带偏移的日期带出 → 切回"全部"日期不再带出）。
- **负控 6 支**（`.workbuddy/tmp/s180-nc.py`，逐支独立日志 ＋ 还原 SHA256 逐字节相同）：
  右端丢整天 · 左端丢偏移 · 档位重载不拒绝 `Custom` · App 映射串档 · 确定键不挡空自定义 · `Selected()` 不带出日期。

### 16.5 仍未做

**定时/自动导出**（已登记为 `#87`：要设置项 ＋ 可注入时钟 ＋"到期只跑一次"的幂等判据，天生易 flaky ⇒ 单独设计）·
报表模板（多份报表打包）· **报表设计器** ⇒ **阶段 181 起立项**，见本文 §17 与
`docs/2026-10-01-report-designer.md`。



---

## 17. 阶段 181：**报表设计器**（定义批 ＋ 模板内核）

从本阶段起，报表设计器有**自己的专题**：`docs/2026-10-01-report-designer.md`
（现状读数 / 三个候选的取舍 / "模板＝覆盖层"的口径 / 分片计划 / 诚实边界）。

一句话结论：缺口是**顺序 / 列名 / 隐藏 / 持久化**四件；本阶段落地前三件的 Core 内核
（`ReportTemplate` ＋ `ReportTable.ApplyTemplate`），并定下一条关键口径 ——
**模板是覆盖层，不是白名单**（否则"用户以后新加一支画笔"会永远静默消失在报表里）。
App 侧的**存储（待拍板：应用级设置 vs 工程文件）**、设计器窗口与三面板套用点留下两片。
