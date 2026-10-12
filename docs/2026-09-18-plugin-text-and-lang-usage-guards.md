# 插件文案扫描面 + 键覆盖（2026-09-18，P0-5）

## 1. 背景

登记表 §4 #27d 的**后半**一直开着：**插件程序集里的用户可见文案不在本地化守卫的扫描面内**
（守卫只扫 `src/HmiEditor.App`）。而插件文案确实会显示在界面上 —— App 侧对插件文本统一走
"**按约定推导键 → 查字典 → 查不到就回落插件原文**"：

| 显示位置 | 键推导（`PluginTextKeys`） | 回落 |
|---|---|---|
| 通讯驱动显示名 | `driver.<id，连字符→点>` | 驱动 Id |
| 通讯驱动**描述** | `driver.<id>.description` | 插件中文描述 |
| 配置字段标签 | `param.<字段 Key，驼峰→点分小写>` | 插件中文标签 |
| 硬件校验结果 | `hw.issue.<Code 小写>` | 插件中文消息 |

**回落就是缺陷的藏身处**：算出来的键在字典里没有，英文界面就显示插件里的中文，
而**没有任何东西会提醒你**（`LangKeyCoverageTests` 只查"常量→字典"，插件根本没常量）。

## 2. 先量事实 —— 找到 5 处**活的**回落缺陷

| # | 缺什么 | 后果（实测） |
|---|---|---|
| 1 | `param.baud` 缺失：字典里是 `param.baud.rate`，而字段 Key 是 **`baud`** | 英文界面显示中文标签「波特率」 |
| 2 | `param.timeout.ms` 缺失 | 英文界面显示「超时(ms)」 |
| 3 | `param.host` 缺失 | 英文界面显示「IP 地址」 |
| 4 | **驱动描述无键路径**：App 直接把 `Metadata.Description` 贴到界面（两处） | 英文界面显示整段中文描述 |
| 5 | `hw.issue.resolution_exceeds_default` 缺失 | 英文界面显示中文校验消息 |

**#1 的根因值得单记**：`Lang.ParamBaudRate` + `param.baud.rate` 是**按陈旧的名字**加的 ——
常量没人读、字典键没人查、界面实际推导 `param.baud`。既有三道守卫全绿也照样漏（见 §5）。

## 3. 修法

1. **键推导收成单一来源**：新增 `src/HmiEditor.App/Services/PluginTextKeys.cs`
   （`DriverNameKey` / `DriverDescriptionKey` / `FieldLabelKey` / `IssueKey` / `CamelToDotted`），
   `CommunicationConfigView`（3 处）与 `ProjectSettingsView`（1 处）改为调用它，
   **删掉各自的本地副本** —— 推导写两份必然漂移，而漂移的表现正是"守卫绿着、界面却是中文"。
2. **补驱动描述键路径**：App 侧按 `driver.<id>.description` 查字典（查不到回落插件描述），
   消费点两处（驱动下拉、驱动详情）。
3. **校验模板带数据**：`ValidationIssue` 增加可选 `Args`（additive，既有构造点不受影响），
   两块板把画面名/宽/高传进去；App 侧有 `Args` 时走 `Format`，模板里用 `{0}/{1}/{2}`。
   **不这么做就只能写一句不含细节的笼统提示**（信息量下降），而这属于 UX 降级。
4. **字典补 8 键 ×2 语言**：`param.baud` / `param.host` / `param.timeout.ms` /
   `driver.{modbus.rtu,modbus.tcp,mqtt,opc.ua}.description` / `hw.issue.resolution_exceeds_default`。
   锚点插入、**不重排**文件（重排会产出几千行假 diff）；插完立即用 JSON 解析验证转义。
   **只增不删**：陈旧键（`param.baud.rate` 等 5 个）保留 —— 字段 Key 来自插件，
   第三方驱动仍可能用 `baudRate` 这类键名，删掉会打断它们。

## 4. 两道新守卫（都在 `HmiEditor.App.UiTests`）

### 4.1 键覆盖（零容忍）—— `PluginTextLocalizationGuardTests`

用**真实插件对象**推导（不是抄源码正则）：

- `BuiltinDriverCatalog.CreateAll()` → 驱动名/描述/每个 `ConfigField.Key` 的标签键；
- 两块内置板**真的把校验规则触发出来**（超分辨率 + LVGL 主版本不符）再收 `Code`
  —— 不是抄测试里的规则名，规则改了判据自动跟上；
- 逐键断言在**两本字典**里都能解析。

另有两条：`KeyDerivation_SeesTheRealPluginSurface`（推导面自检 + `CamelToDotted` 语义钉子）、
`IssueTemplates_PlaceholderCountsMatchPluginArgs`（**显式决定表**：带数据的规则登记占位符个数；
不等会让用户看到 `{1}` 字面量 —— `Format` 在个数不符时**不抛异常**）。

### 4.2 插件文本基线（只减不增）—— 同文件

按**语句**判定（不只按行 —— 多行 `throw new` 的文案在下一行，登记表 §12.1 踩过），
排除异常消息 / `Debug·Console·Trace` / `ILogger` 运维日志 / 正则模式；
`Log?.Invoke`（运行时驱动日志，可能进设备日志面板）与 kind 标签**故意多报**、锁进基线。

**基线 49 条 / 9 个文件，逐条审过**（见测试内注释）：驱动元数据（描述/标签 ~40）、
校验回落文案（2）、运维 kind 标签（2）。它们**删不掉**（契约要求
`ConfigField.Label`/`Metadata.Description`/`ValidationIssue.Message` 非空），
显示路径已由键接管 —— 基线的作用是**再新增插件文案即红**，而不是假装清干净了。

### 4.3 Lang 常量使用面（只减不增）—— `LangConstantUsageGuardTests`

P0-5 找到的缺陷形状是"**加了没人读的常量**（对着陈旧字段名）"。既有守卫全看不见：

| 守卫 | 查什么 | 对本次缺陷 |
|---|---|---|
| `LangKeyCoverageTests` | 常量 → 两本字典都解析得到 | ✅ 绿（键和常量都在） |
| `HardcodedLocalizationGuardTests` | App 层有没有硬编码中文 | ✅ 绿（没写死中文） |
| **本守卫** | 常量在 `src/`+`tests/` 里**有没有静态引用** | ❌ 红 |

**实测基线 1267 / 1912 个常量无静态引用**（约 2/3）。诚实边界写在测试注释里：
这是**代理指标**（`param.*` 家族的键是运行时动态推导的，常量确实没人读、但键是活的），
基线里的名字**不能直接批量删除**，逐个查证属独立议题 —— 已登记为 §4 #35。

## 5. 负向验证（4/4 命中）

| NV | 注入 | 结果 |
|---|---|---|
| A | 删字典键 `param.baud` | ✅ 键覆盖红：`[param] param.baud  缺 zh-CN` |
| B | 给 `MqttDriver` 加一条**可编译**的中文字面量（先确认构建 0 错 0 警） | ✅ 基线红：`MqttDriver.cs: 实测 12 ≠ 基线 11` |
| C | 英文模板 `{0}({1}x{2})` 删掉两个占位符 | ✅ 占位符红：`0 ≠ 登记值 3（不等会让用户看到 {0} 字面量）` |
| D | `Lang` 里加一个没人引用的常量 | ✅ 使用面红：`1268 ≠ 基线 1267` |

> **NV-B 首轮不够严谨并已重做**：第一次注入的是不存在的 record 属性（编译失败），
> 测试仍然变红（扫描器读源码、不读编译产物）—— 但"编译失败下变红"不能证明守卫有牙齿。
> 改成可编译的注入（构建 0 错 0 警）后复现，才作数。

## 6. 明确**没做**的：长度比机检（量过，不可判）

计划里写的"长度比"预检实测**无法作为判据**：

| 阈值 | 命中 |
|---|---|
| `en/zh > 3` 且 `en > 60` | 45 条 |
| `en/zh > 4` 且 `en > 80` | **0 条** |

比例高的全部是 2~4 字中文标签对 12~27 字符英文（`prop.radius` 2→13、
`win.comm.config` 4→27）—— 这是**中英文紧凑度差异**，不是缺陷信号。
⇒ **不发布**这种要么噪音、要么空转的守卫；`en-US 逐字校对`仍是**母语级判断**项，
留在登记表（机器只能查结构：键覆盖、占位符、键集一致，三项均已绿）。

## 7. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release --no-incremental` | **0 警告 0 错误** |
| 全量测试（7 程序集） | 见提交读数（基线 2728 ⇒ 本轮 +7） |
| 负向验证 | **4/4 命中**（NV-B 重做后作数） |
| 字典 | 2250 → **2258** 键 ×2 语言，JSON 解析 + 键集一致 |

## 8. 登记表

- §4 #27d（插件程序集不在扫描面内）：**后半闭环** —— 老规则补键（`hw.issue.resolution_exceeds_default`）
  + 扫描面扩到插件程序集 + 驱动描述键路径 + 键覆盖零容忍判据。
- §4 #27（en-US 校对）：**结构可判项全绿**（键集一致 / 占位符一致 / 插件键覆盖）；
  **逐字校对仍开着**（母语级判断），长度比机检经实测判定为不可判（§6）。
- 新增 §4 #35：**1267 个无静态引用的 Lang 常量**（代理指标，逐个查证属独立议题）。
