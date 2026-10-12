# 配置诚实性收口（二）：导出前拦截「谁都登不上」的用户表 + AI 工具不再广告死字段

> 承接 `docs/2026-09-16-config-honesty-and-editor-preferences.md`（阶段 10「UI 在撒谎」五类根因）
> 与 `docs/2026-09-16-runtime-user-editor-ui.md` §7.3、`docs/2026-09-16-runtime-config-sinking.md` §9。
> 日期：2026-09-17

---

## 0. 一句话结论

阶段 10 把「面板上有控件、其实不生效」的 30 个字段登记成了数据并加了灰显，但**同一类缺陷还有两个
没被覆盖的入口**：① 用户表的**导出前拦截**缺失 —— 校验器写好了、只在面板里飘红字，产物照样能导出；
② **AI 工具描述**在广告两个没有执行路径的参数 —— 模型改了会回「已更新」，设备上什么都不变。

两者都不是"功能没做"，而是**"防线/说明与实际能力不一致"**：前者是防线只装在一半路上，
后者是撒谎的界面从设置面板换成了 AI 工具描述。

---

## 1. 缺陷一：运行时用户表的校验没接进导出闸门

### 1.1 症状与归因

`RuntimeUserValidator`（阶段 7）早就把四条规则写好了，但它的**唯一消费点**是
`ProjectSettingsView`（面板里显示提示）。导出路径完全不知道它的存在：

```
grep -rn "RuntimeUserValidator" src/
  src/HmiEditor.App/Views/ProjectSettingsView.axaml.cs:996   ← 唯一消费点（面板提示）
  src/HmiEditor.Core/Security/RuntimeUserValidator.cs:64     ← 定义
  src/HmiEditor.Core/Services/SimulatorProjectExporter.cs:493 ← 只是跳过空用户名
```

而 `MainWindow` 的导出/模拟器导出**已经有**一个 `GeneratedCodeValidator` + `HasErrors` 闸门
（结构 / Faceplate / 表达式 / 悬空引用 / 已知 LVGL API / 生成完整性）。用户表**没被纳入**。

### 1.2 为什么后果是"设备不可用"而不是"少个账户"

设备端 `firmware/t113/app/runtime/hmi_actions.c` 的 `hmi_login()`：

```c
#if HMI_SEC_USER_COUNT == 0
    if (hmi_login_as_role("", NULL) != 0) { ... }   /* 没配表 ⇒ 按默认角色授权 */
#endif
    if (try_switch(candidates, ...) != 0) {
#if HMI_SEC_USER_COUNT > 0
        HMI_ACT_LOG("hmi_login: 工程配了运行时用户表但没有登录屏 —— 无凭据提交入口，会话不建立");
```

即 **`HMI_SEC_USER_COUNT > 0` 时刻意不授权**（否则"点一下登录"就以默认角色进入会话，
凭据校验被完全绕过）。于是：

> **表里有条目、但没有一条可用** ⇒ 任何人都不可能建立会话 ⇒ 所有带权限门的动作**永久拒绝**。

产物烧到设备上的表现是「HMI 点了没反应」，而归因成本极高 —— 面板上那几行红字在导出之后
就被忘了。这正是本项目的判据：**能在导出前机械判定的形态，不该留给现场。**

刻意**不是**"表非空就报"：表为空是"没配运行时用户表"的既有形态（设备端按默认角色授权），
报错会拦下所有老工程。

### 1.3 定级：逐条对应设备端真实后果

| `RuntimeUserIssueKind` | 设备端后果 | 定级 |
|---|---|---|
| `UnknownRole` | `hmi_role_lookup` 返回 -1 ⇒ **口令正确也登不上** | **Error（拦导出）** |
| `EnabledWithoutPassword` | `hmi_verify_password` 解析失败即拒绝 ⇒ **该账户无法登录** | **Error（拦导出）** |
| `EmptyUsername` | 设备端 `strcmp("", "") == 0` ⇒ 空输入反而**能命中**它（仍需口令）；属配置错误 | Warning |
| `DuplicateUsername` | 线性查表**首个胜出** ⇒ 后面的同名条目永远匹配不到（改它没反应） | Warning |
| **整表**：`CountUsable == 0` 且表非空 | 见 §1.2 —— **谁都进不去，权限门永久拒绝** | **Error（拦导出）** |

**规则一条都没重写**：`ValidateRuntimeUsers` 直接调 `RuntimeUserValidator.Validate` +
`CountUsable`。重写一份的结局是"面板说没问题、导出说有问题"这类分叉。

**新增 `FindingCategory.RuntimeUser`**（＋ zh-CN / en-US 各 1 个类别标签），
`ValidationResultView` 显式映射它 —— 不映射会静默落进 `_ => 代码生成`，用户看到类别是错的。

---

## 2. 缺陷二：AI 工具在广告没有执行路径的参数

### 2.1 症状

`AgentToolFactory.UpdateRuntimeTool` 的描述与 schema 里有 5 个参数，其中两个是死的：

```
"可选参数: scan_interval_ms(扫描周期), watchdog_timeout_ms(看门狗超时),
          boot_screen_id(启动屏幕), enable_hmi_reboot(允许重启), log_level(...)"
```

`RuntimeConfig.WatchdogTimeoutMs` / `EnableHmiReboot` 的**唯一落点**是 AI 工具写入面与设置面板
（写值），**没有任何读取方**：

```
WatchdogTimeoutMs ← AgentToolFactory(写) / ConfigFieldInventory(清单) / ProjectSettingsView(面板)
EnableHmiReboot   ← ConfigFieldInventory(注释) / ProjectSettingsView(面板)
```

于是模型"照着描述改一个死键"，回给用户的是 **`已更新运行时配置: watchdog_timeout_ms`**。
这与阶段 9/10 收口的「UI 在撒谎」是同族，只是**撒谎的界面换成了 AI 工具描述**。

### 2.2 处置：把参数拿掉，而不是加一句"不生效"

两条路（专题文档原文给的选项）是「拿掉」或「至少把"当前不生效"写进描述」。
选**拿掉**：

- 参数是 AI 工具的**接口**（不是持久化数据），拿掉**不破坏老工程 JSON 兼容** ——
  `RuntimeConfig` 的两个字段原样保留，面板上也仍然可配（它们本来就是"等设备端有执行路径再接线"）；
- 保留参数只会让模型继续去改死键，"描述里写不生效"要靠模型自觉不选它 —— 不可靠；
- 描述里**用中文说明这两项不可改**（解释"为什么这个请求不能满足"是有价值的），
  但**不出现 snake_case 参数名** —— 后者会继续诱导模型。

### 2.3 守卫：判据从"真实消费"推导，不抄清单

`AgentToolSchemaContractTests.UpdateRuntimeSchema_AdvertisesOnlyRuntimeConfigFieldsThatAreActuallyRead`：

1. 取 `update_runtime` 渲染后的 schema 属性（**渲染后的结果**，不是源码里的注入表达式）；
2. 对 `RuntimeConfig` 的**每个**属性，在产品源码里扫 `.字段名` 的出现；
   **白名单**＝ AI 工具与设置面板（写入面）、清单、声明类型文件 ⇒ 白名单外的命中才算"有读取方"；
3. 断言：广告的参数 ∩ 无读取方的字段 ＝ ∅；
4. 描述里不得出现任何无读取方字段的 snake_case 名。

**刻意不依赖 `ConfigFieldInventory.NotWired`**：那份清单**有意**不登记"只被 AI 工具写入"这一类
（见其类注释 —— 它们要的是补执行路径，不是标未生效）。拿它当判据，本守卫**永远抓不到
`enable_hmi_reboot`** —— 这个坑是实施时真踩到的（见 §3.1）。

自检两条：① 探测器必须真的算出 `watchdog_timeout_ms` 与 `enable_hmi_reboot` 是死的
（探测器失效会让守卫空转恒绿）；② `ToSnakeCase` 有独立的正反例 theory
（名字映射写错会让"死字段集合"整体错位）。

---

## 3. 覆盖（新增 18 项测试）

| 文件 | 项数 | 覆盖 |
|---|---|---|
| `GeneratedCodeValidatorRuntimeUserTests` | 12 | 未配表/健康表零发现；两条 Error 级单条规则**且真的阻断导出**；停用无密码不算问题；两条 Warning 级且不阻断；重名指向**后面**那条；整表零可用 ⇒ Error（含"每条看着都配好了"的形态）；单条规则与整表规则**不互相吞掉**；每种问题类型必须显式定级 |
| `AgentToolSchemaContractTests`（+6） | 6 | 工具不广告无读取方的字段；描述里不出现死参数名；`ToSnakeCase` 正反例 5 条 |

**"每条问题类型必须显式定级"**是一条刻意设计的守卫：`GeneratedCodeValidator` 的定级
`switch` 有 `_ => Warning` 兜底 —— 新增一种 `RuntimeUserIssueKind` 时它会**静默变成 Warning**
（不拦导出）。守卫用一张显式的"期望定级表"把这件事逼成**必须做的决定**，并反向检查表里没有
已删除的类型（否则守卫在描述一个不存在的世界）。

---

## 4. 负向验证（7 组）

注入器一律 `copyfile` + 写回，判据是**期望用例名落在失败行上**（只判退出码会被"别的用例碰巧红了"骗过去）。

| NV | 注入 | 结果 |
|---|---|---|
| A | 导出路径不再调用 `ValidateRuntimeUsers` | ✅ `TableWithNoUsableAccount_IsError_…` |
| B | 去掉整表 `CountUsable == 0` 体检 | ✅ 同上 |
| C | `UnknownRole` 降级为 Warning | ✅ `UnknownRole_IsError_AndBlocksExport` |
| D | `EnabledWithoutPassword` 降级为 Warning | ✅ `SeverityMapping_MatchesActualBehaviour_ForEveryKind` |
| E | 把 `watchdog_timeout_ms` 加回 schema（复现本轮修掉的缺陷） | ✅ `UpdateRuntimeSchema_AdvertisesOnly…` |
| F | 描述里写回死参数名 | ✅ 同上 |
| G | 破坏 `ToSnakeCase` 映射 | ✅ `ToSnakeCase_MapsRealRuntimeConfigFields` |

### 4.1 一处自纠：首版守卫**依赖了错误的来源**

首版守卫用 `ConfigFieldInventory.NotWired` 反推"死字段"，并在自检里断言
`enable_hmi_reboot` 在里面 —— **测试立刻红了**，而且红得有价值：`EnableHmiReboot`
**不在**那份清单里，因为清单类注释写明**有意不登记这一类**
（"只被 AI 工具写入、没有任何执行路径读取的字段…需要的是补执行路径，不是标未生效，
机械判据分不出来，故不入表"）。

也就是说：**首版守卫即使实现正确，也抓不到本轮修掉的缺陷的一半**。
改为"从真实消费推导"后，两个字段都在集合里，且**将来任何新的死字段会被自动纳入**。
—— 又一次印证本项目的纪律：**判据的来源比判据本身更重要**；
拿一份"有意排除某类"的清单当全量来源，会得到一个看起来在工作、实际有盲区的守卫。

---

## 5. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2595 通过 / 0 失败**（Core **1912** / AI 313 / App.UiTests **153** / Device 76 / Communication 75 / Simulator 44 / Hardware 22；本轮基线 2577 ⇒ **+18**） |
| 端到端验收（真实 LVGL） | `verify.sh` **退出码 0**：Golden 2 screens / 36 controls → CMake 全量编译 → `hmi_sim` → selftest **Passed** → CTest **1/1 passed**（本轮改动为纯 C#，未触及生成链，属回归确认） |
| 负向验证 | **7/7 命中**（§4） |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |
| 本地化 | zh-CN / en-US 各 +1 键（`finding.category.runtime.user`） |

---

## 6. 范围边界（本轮**不做**，已登记）

1. **`EnableHmiReboot` 的面板控件仍未标注"不生效"**。它有控件（`ChkEnableReboot`）却不在
   `ConfigFieldInventory.NotWired` 里 —— 这是**清单的既有设计决定**（见其类注释：这一类要的是
   补执行路径、不是标未生效），不是本轮遗漏。本轮改的是**更危险的那一面**（模型去改死键）。
   若要给面板也加标注，应当先改那份清单的分类口径，属独立议题。
2. **`WatchdogTimeoutMs` / `EnableHmiReboot` 的字段本身没删**（删会破坏老工程 JSON 兼容），
   补执行路径仍是未开工的**新功能**（T113 是 Linux 用户态，"MCU 喂狗"没有对应物）。
3. **`RuntimeConfig.ScanIntervalMs` 与 `VariableManager.DefaultScanIntervalMs` 语义重复**
   （两者都默认 100、都叫"扫描周期"）—— 合并要先定"哪个面板是权威"，属产品决策。
4. **用户表校验未覆盖"口令强度"与"改了 `PasswordMinLength` 之后旧口令是否仍达标"**：
   只能看到哈希、看不到长度，判不了。这条限制写在 `RuntimeUserValidator` 的类注释里，
   不由本轮的导出闸门伪装成已校验。
