# Lua 方言对齐：设备侧没有 Lua，但"设备上跑不了什么"已经测得出来

承接 `docs/2026-09-20-lua-syntax-validation.md`（阶段 39）的「仍未闭环 ①：方言误报」。
登记表 **#36「仍开着」②**。相关：`docs/2026-09-20-scripting-assembly-triage.md`（阶段 38）。

## 1. 结论摘要（先说要紧的）

四条，按重要性排：

1. **「等固件 Lua 版本确定后再对齐」这个说法本身要改。** 固件侧**根本没有 Lua**，而且不是
   "开关没打开" —— `LV_USE_LUA` **不是 LVGL 的配置项**（全 firmware 树零命中），
   模板里那个 `#ifdef LV_USE_LUA` 是本项目**自造**的宏。⇒ 不是一个宏的事，
   而是**整条桥接（`lv_lua_bridge.h` / `lv_binding_lua`）从未存在过**。
2. **但"设备上跑不了什么"已经测得出来了。** Tina SDK 自带 **Lua 5.1.5**（含源码与 host 二进制），
   把它当候选目标真跑一遍，得到 6 条**假阴性** —— 编辑器**放行**、设备**不可用**。
3. **这 6 条里有 5 条编辑器永远看不见**（语法完全合法，只是库里没有）。
   ⇒ **这部分不是"对齐版本"能解决的**，必须靠别的机制。这一条最容易被漏掉，
   因为"等版本定了再说"听起来像是万能解。
4. **那个"别的机制"已经落地了**（2026-09-19，见 §12）：`LuaDialect.RuntimeSymbols` + 运行前检查，
   扫出"语法合法、设备上却没这个库"的引用，走**警告**通道 —— **不判红**，
   因为目标未定，判红就是把"待定"当成"错误"。

## 2. 证据链（三条独立证据，都不是推断）

| # | 事实 | 证据 |
|---|---|---|
| ① | **`LV_USE_LUA` 不是 LVGL 的配置项** | `rg --no-ignore -c 'LV_USE_LUA' firmware/` ⇒ **零命中**（含 `firmware/t113/host_test/lvgl` 这份 LVGL 源码）。同一个搜索里 `lv_lua_bridge` / `lv_binding_lua` 同样**零命中**；两份 `lv_conf.h`（SDK 的 `package/gui/lvgl/` 与本项目的 `t113/config/`）里 `lua` 也**零命中** |
| ② | **Tina SDK 自带 Lua 5.1.5** | `firmware/Tina-SDK/package/utils/lua`；构建产物里源码的 `lua.h` 写着 `LUA_VERSION  "Lua 5.1"` / `LUA_RELEASE "Lua 5.1.5"` / `LUA_VERSION_NUM 501` |
| ③ | **有一个 x86-64 的 host 二进制可以真跑** | `firmware/Tina-SDK/out/t113-mq_r/compile_dir/target/host/lua-5.1.5/src/lua`（`ELF 64-bit LSB pie executable, x86-64`）⇒ 探针能拿到**实测**而不是推断 |

⇒ 于是"设备侧到底会发生什么"**不必等固件开工**：只要有候选版本，就能测。
本轮就这么做的。

## 3. 探针：编译期与运行期必须分开报

`tools/probe-lua-dialect.lua`（+ `tools/probe-lua-dialect.sh`）。**这个区分是整件事的关键**：

| 列 | 怎么做 | 谁看得到 |
|---|---|---|
| `compile` | `loadstring` 只做词法 / 语法分析 | **编辑器的真解析等价于这一列** |
| `run` | `pcall` 真执行 | 库 / 字段不存在**只在这一列**暴露 |

⇒ **`compile=OK` 而 `run=ERR` 的写法，编辑器无论怎么对齐版本都看不见。**

探针刻意**不进门禁**：它需要一个"设备侧 Lua"解释器，而 CI 里没有（SDK 的 `out/` 是构建产物）。
让它进门槛只会逼出一个跳过分支，那种判据等于没有。⇒ 手工探针，但**命令可复现**：
`bash tools/probe-lua-dialect.sh`（找不到解释器时**非零退出**，不会静默当成"全通过"）；
探针自带两条**正控**，正控不过就 `exit 2` 报 `PROBE_BROKEN`。

## 4. 实测结果：Lua 5.1.5（SDK 自带）vs 编辑器基准（MoonSharp = 5.2）

`bash tools/probe-lua-dialect.sh` 的真实输出（12 用例）：

| 写法 | 5.1.5 `compile` | 5.1.5 `run` | 基准（5.2） | 性质 |
|---|---|---|---|---|
| `goto done ::done::` | ❌ `'=' expected near 'done'` | — | ✅ 通过 | **假阴性**（解析层） |
| `bit32.band(1, 3)` | ✅ | ❌ `attempt to index global 'bit32'` | ✅ 通过 | **假阴性（编辑器看不见）** |
| `table.pack(1, 2)` | ✅ | ❌ `attempt to call field 'pack'` | ✅ 通过 | 同上 |
| `table.unpack({1, 2})` | ✅ | ❌ `attempt to call field 'unpack'` | ✅ 通过 | 同上（5.1 里 `unpack` 是**全局函数**，`lbaselib.c` 的 `{"unpack", luaB_unpack}`） |
| `math.type(1)` | ✅ | ❌ `attempt to call field 'type'` | ✅ 通过 | 同上 |
| `utf8.len("x")` | ✅ | ❌ `attempt to index global 'utf8'` | ✅ 通过 | 同上 |
| `local a = 7 // 2` | ❌ `unexpected symbol near '/'` | — | ❌ 报错 | 一致（都拒）⇒ 不是差异 |
| `local a = 1 & 3` | ❌ `unexpected symbol near '&'` | — | ❌ 报错 | 一致 |
| `local x <const> = 1` | ❌ `unexpected symbol near '<'` | — | ❌ 报错 | 一致 |
| `local 变量 = 1` | ❌ `'<name>' expected` | — | ✅ 通过 | **假阴性，但成因不是版本**（见 §5） |
| 正控 `local a = 2^3` / `for i=1,3` | ✅ | ✅ | ✅ | 探针自检 |

**汇总**：`cases=12 compile_ok=7 compile_err=5 run_ok=2 run_err=5`。

### 两个方向，危害不对称

| 方向 | 定义 | 实例 | 后果 |
|---|---|---|---|
| **假阳性** | 编辑器拒、设备能跑 | 5.3+ 的 `//` `&` `\|` `>>`、5.4 的 `<const>` | 用户会来抱怨；**设备不会出事**。`LuaDialect.Hints` 已把报错改写成可读方言说明 |
| **假阴性** | 编辑器放行、设备炸 | `goto` / `bit32` / `table.pack` / `table.unpack` / `math.type` / `utf8` | **用户在编辑器里看到一片绿，到设备上才发现** |

⇒ 之前只登记了假阳性（"误报"），**漏了整个危险方向**。这是本轮最实质的收获。

## 5. `中文标识符`：实测是假阴性，但**不许**塞进版本模型

`local 变量 = 1`：编辑器通过、5.1.5 在**词法层**就拒（`'<name>' expected`）。

依据（读的是 SDK 那份 5.1.5 的源码，不是猜）：

- `llex.c` 读标识符用的是**裸 `isalpha`**（`else if (isalpha(ls->current) || ls->current == '_')`），
  按**字节**判，UTF-8 的中文字节不满足；
- 全仓**没有 `setlocale`**（只有 `os.setlocale` 这个**暴露给脚本**的函数，`lua.c` 自己不调）
  ⇒ 进程停在 `"C"` locale。

⇒ 它**不是版本特性**（成因是 locale / 构建配置），不能写成"最小版本 5.2"——
那样一来是假陈述，二来会把它混进"版本对齐"的清单里，让人以为改个版本号就解决了。

**在 `LuaDialect.Features` 里它的 `Minimum` 是 `null`**，并有判据
（`NonMonotone_StaysOutOfTheVersionModel_SoTheSetIsOnlyALowerBound`）守着：
不许给它编一个版本充数，也因此**它不在自动算出的假阴性集合里**。

> ⇒ **`LuaDialect.FalseNegatives(t)` 给出的是下界，不是完备清单。** 这句话写在代码注释和测试里，
> 不只写在这里。

## 6. 做了什么（本轮交付）

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `src/HmiEditor.Core/Services/LuaDialect.cs`（新） | **单一事实源**：`ParserBaseline`（= 5.2，MoonSharp 硬边界）/ `Target`（= Unknown）/ `LabelOf` / `Features`（10 条事实）/ `FalseNegatives` / `FalsePositives` / `Hints` |
| 2 | `LuaSyntaxValidator` 改造 | 6 条方言文案**从基准派生**（原来 6 处都写死「Lua 5.2」）；类注释补上两个方向的不对称 |
| 3 | `tools/probe-lua-dialect.lua` + `.sh`（新） | 探针入库 + 可复现命令；**示例与事实表逐字一致**（有判据） |
| 4 | `tests/…/LuaDialectTests.cs`（新，**8 条**） | 见 §7 |
| 5 | `LuaSyntaxValidatorTests` 调整 | 版本标签改为从 `LuaDialect` 取；`Dialect_Lua52Features_AreAccepted` **改名**为 `Dialect_BaselineAccepts_These_WhichSaysNothingAboutTheDevice`（原名把"基准放行"读成了"设备可用"） |

### 关键设计：把"待对齐"做成一条会红的触发线

`Target` 当前是 `LuaVersion.Unknown`，有一条判据
（`Target_IsStillUnknown_AndDecidingItTriggersRealWork`）钉着这个事实。
**它红了不代表有 bug，代表有人确定了目标版本** —— 那时要做的不是把它改绿，而是：

1. 用真实目标重跑 `tools/probe-lua-dialect.lua`；
2. 按实测更新 `LuaDialect.Features` 的 `Minimum` / `Stage`；
3. 处置新出现的假阴性（三条路径：见 §8）；
4. 改 `Dialect_*` 钉桩与本专题。

> 理由很直白：**写在文档里的待办没人读，红色的测试有人修。**

## 7. 判据与负向验证

`LuaDialectTests` **8 条**：

| 判据 | 守什么 |
|---|---|
| `ParserBaseline_IsLua52_BecauseThatIsWhereMoonSharpStops` | **本组唯一**允许写死版本字面量的地方（换解析器才改这里） |
| `Hints_AllDeriveFromBaselineLabel` | 文案不许有第二处写死版本号（本轮之前的反例） |
| `FeatureExamples_AllAppearVerbatimInTheProbe` | **事实表与探针不许漂移**（改表不改探针 = 表里出现从没跑过的断言） |
| `FalseNegatives_ForSdkCandidate_AreExactlyTheSixMeasuredOnes` | 6 条与探针输出逐条对应；其中 **5 条**是 `Runtime` 阶段（编辑器看不见） |
| `FalsePositives_GrowWithTargetVersion_AsMeasured` | 反方向：5.3 ⇒ 2 条、5.4 ⇒ 3 条 |
| `Gaps_AreEmpty_WhenTargetIsUnknownOrEqualsBaseline` | **正控 + 边界**：挡住"函数恒返回全表"这种坏法 |
| `NonMonotone_StaysOutOfTheVersionModel_…` | 非单调项不许编版本充数；集合是**下界** |
| `Target_IsStillUnknown_AndDecidingItTriggersRealWork` | **触发线**（见 §6） |

**负向验证 4/4**（注入 → 立刻还原，同场正控）：

| # | 注入 | 结果 |
|---|---|---|
| 1 | `Target` 改成 `Lua51` | ✅ 触发线**红** |
| 2 | 把一条 hint 的 `{v}` 写死成「Lua 5.1」 | ✅ 派生判据**红** |
| 3 | 把探针里 `local a = 7 // 2` 改成 `local a = 7 / 2` | ✅ 漂移判据**红** |
| 4 | 往 `src/` 已有文件追加一个没人引用的类型 | ✅ 死类型守卫**红并点名**（见 §9） |

**还原**：注入 1/2/3 覆盖的是同一份 `LuaDialect.cs`，用 `sha256sum` 逐字节核对
（`9a8c0f5c…beffc57`，两侧一致）；注入 4 同法。**同场正控**：还原后复跑三类
**43/43 绿**、`Core.Tests` 全量 **2084 通过**。

## 8. 为什么不能就地"对齐"：三条路径，取决于目标是什么

| 若目标是 | 编辑器该有的行为 | 需要什么 |
|---|---|---|
| **5.1（SDK 自带那条）** | 把 6 条假阴性**报出来**（"设备侧不支持"），而不是放行 | 设备版本信息 + UI 支持多条消息（现在只显示 `Errors[0]`） |
| **≥ 5.2** | 把方言说明从"可能是误报"改成"与本编辑器一致" | 只改 `LuaDialect` 与文案 |
| **任意版本** | `Runtime` 那 5 条（库 / 字段缺失）**语法校验永远无能为力** | **独立机制**：版本敏感符号扫描 —— 这是新功能，不是对齐。**（2026-09-19 已落地，见 §12）** |

**第三行是本轮最该记住的一条**：它说明"对齐版本"**不是**这个问题的完整答案。

> **后续复核（2026-09-19，阶段 41）**：第三行已落地，实现方式是**版本敏感符号扫描**
> 而不是严格白名单（取舍见 §12.3），并为此给 `ScriptValidationResult` 加了 **Warnings 通道**。
> 上面第 1 行说的"UI 支持多条消息"只解决了一半：警告侧现在显示首条 + 计数 + `ToolTip`，
> `Errors` 侧仍只显示首条。

## 9. 顺带闭环：两个死类型真的删掉了

阶段 37 留下的 `HmiProperty`(9 行) / `ResourceLibraryVersionInfo`(22 行)，本轮**确认后删除**。

**确认方式（三层，不只 grep 一次）**：

| 层 | 做法 | 结果 |
|---|---|---|
| 1 | 成员级：两个类型的所有成员是否被用到 | 只有 `class` 声明那一行；无 `new X` / `List<X>` / `X[]` |
| 2 | 字符串层：反射 / 序列化按名查找？ | 全仓**零**字符串命中 |
| 3 | **`--no-ignore` 全仓（含 `bin/` `obj/`）** | 只有自身声明 + 守卫注释 ⇒ 连构建产物里都没有 |

⇒ `git rm` 删除两个文件，`DeadTypeGuardTests.ExpectedDeadTypes` 从 2 项**清空为 0 项**。

### 空基线为什么不是恒绿（这一步最容易想歪）

基线清空后判据反而**更强**：语义变成「**`src/` 里不允许存在无人引用的类型**」。
它不恒绿的依据是三条**元判据**在守"扫描面有没有塌"：

- 语料规模下限（`.cs > 300` / `.axaml > 20`）—— 实测 **601 / 47**；
- 声明两条路都要出结果（`MainWindow` / `AnimationEditorView`）；
- 活类型与三个误报源类型（`StringIfEmpty` / `EnumParseExtensions` / `LvglMacros`…）**必须判活**。

**负控 4 直接证明了这件事**：往 `src/` 追加一个 `ZzzNegCtlDeadType` ⇒ 红，且输出**点名**：

```
新增（代码里有、基线没有）= [ZzzNegCtlDeadType]
    ZzzNegCtlDeadType   被声明=True  出现次数=1  判定=死
  语料：.cs 601 个、.axaml 47 个、声明到的类型 614 个
```

## 10. 读数

- `dotnet build HmiEditor.slnx -c Release -warnaserror` ⇒ **0 警告 / 0 错误**；
- `HmiEditor.Core.Tests` ⇒ **2076 → 2084**（**+8** = 新增的 `LuaDialectTests`，纯净增）；
- 三类测试（方言 / 校验 / 死类型）⇒ **35 → 43**；
- 门禁 **7 步全绿、`GATE_EXIT=0`**，构建步 **0 警告 / 0 错误**，**八程序集合计 2908**
  （阶段 39 的 2900 **+8**，差额恰好是新测试类）。日志
  `.workbuddy/tmp/ci-gate-2026-09-20-luadialect.log`，**mtime `17:33:09`，晚于提交 `4720c0c` 的 `17:32:20`**；
- 探针 `bash tools/probe-lua-dialect.sh` ⇒ `cases=12 compile_ok=7 compile_err=5 run_ok=2 run_err=5`，`PROBE_EXIT=0`。

## 11. 仍未闭环

| # | 项 | 状态 |
|---|---|---|
| ① | **目标版本未定** | 由 `Target = Unknown` + 触发线承接（§6）。**固件要真上 Lua，得先把 `lv_binding_lua` 之类的东西弄进树里** —— 现在 `LV_USE_LUA` 连 LVGL 的配置项都不是 |
| ② | **`Runtime` 类假阴性** | 语法校验**原理上**看不见库缺失（§8 第三行）⇒ 独立机制。**✅ 已落地（2026-09-19，见 §12）**；残余：符号表是**差异表、不是穷举白名单**，完备性靠 `Features` + 判据承接（§12.3） |
| ③ | **中文标识符** | 成因是 locale / 构建配置，只登记了事实，**未实测 5.2/5.3/5.4 上的行为**（`Minimum` 记 `null`） |
| ④ | **编辑器只显示 `Errors[0]`** | **部分解决（2026-09-19）**：警告侧显示首条 + 计数、全部挂 `ToolTip`（§12.1）；`Errors` 侧仍只显示首条 ⇒ 与 ① 仍是同一个前置条件 |
| ⑤ | 覆盖率读数 | `HmiProperty` / `ResourceLibraryVersionInfo` 从分母消失（22+9=31 行）；覆盖率**不是门禁**，本轮未重跑（同 lua-syntax-validation §9 的口径） |

## 12. 后续复核与落地：运行前检查 — 2026-09-19（阶段 41）

> §1~§11 是 2026-09-20 当天的快照，**保留原文**；本节是后续推进的记录。

### 12.1 做了什么

§8 第三行说的那个"独立机制"落地了。它**不是**严格白名单（穷举设备侧允许的全局名），
而是**版本敏感符号扫描** —— 取舍见 12.3。

| # | 交付物 | 说明 |
|---|---|---|
| 1 | `LuaDialect.RuntimeSymbols`（**5 条**） | `bit32` / `table.pack` / `table.unpack` / `math.type` / `utf8`，每条带 `Minimum`。**与 `Features` 里 `Stage == Runtime` 的条目一一对应**（名字 + `Minimum` 都一致，有判据守漂移） |
| 2 | `LuaDialect.CheckRuntimeSymbols(code, target)` | 剥注释（复用 `StripComments`，行号不漂移）→ **遮蔽字符串字面量** → 逐符号正则扫描；返回 `(符号, 行, 列, 判定)`，按行排序 |
| 3 | `LuaSymbolStatus` | `Undetermined`（目标未定，**不能下结论**）/ `Unavailable`（目标已定且确实没有） |
| 4 | `LuaDialect.ExplainHit(hit, target)` | 中文提示，版本标签一律由 `LabelOf` 派生（与 `Hints` 同一条纪律）；**刻意写短**（要塞进状态栏一行） |
| 5 | `ScriptValidationResult.Warnings`（**新通道**） | 前提条件，见 12.2 |
| 6 | `ScriptEngineService.Validate` 第三段 | 只在前两关都过时跑；语法没过时不跑（那时用户该看的是语法错） |
| 7 | `ScriptEditorView` | 警告用**黄色**（新增 `UiPalette.ValidationWarn`）显示首条 + 计数，全部挂 `ToolTip`；`QuickSave` 不再把校验结论覆盖成绿色 |
| 8 | `tests/…/LuaRuntimeCheckTests.cs`（**25 条**） | 见 12.4 |

### 12.2 前提：先得有"警告"这条通道

`ScriptValidationResult` 原来只有 `IsValid` + `Errors`，**没有 Warnings**。这不是小事：

设备侧目标版本**至今未定**（`Target = Unknown`）。同一个 `bit32`，设备是 5.2 就完全正常、
是 5.1 就必炸 —— 把这种情况写进 `Errors`，等于**把"待定"当成"错误"**：
用户会看到满屏红色、却在设备上跑得起来的脚本，然后学会整片忽略校验。
那不是"更保险的做法"，那是把判据变成噪声。

⇒ 加了独立通道，并且 `IsValid` **只看 `Errors`**，警告永远不影响它。

### 12.3 为什么不是严格白名单

"白名单"的严格形式是「枚举设备侧存在的全局名，代码里凡引用不在表内的就报」。**没这么做**：

| 做法 | 问题 |
|---|---|
| 穷举 5.1 全局表 + 静态判定"哪些标识符是全局引用" | Lua 是动态语言：局部变量、`_ENV` 改写、设备侧注入的 `hmi_read` / `hmi_write` / `hmi_set_control_property` **都不在标准库里**，`function on_click()` 里的名字也不在。误报会多到用户直接无视警告 ⇒ **等于没有** |
| **差异表**（实际采用） | 只报"确实差的那些"，噪声可控 |

代价要明说：**差异表是下界，不是完备清单** —— 它只覆盖 `Features` 里登记过的项。
这个缺口由判据接住（`RuntimeSymbols_MatchRuntimeFeatures_BothWays` 双向断言），
而 `Features` 本身的完备性由探针 `tools/probe-lua-dialect.lua` 承接 ——
与 §5 里 `中文标识符` 那条"集合是下界"是同一件事。

### 12.4 判据（25 条）与负向验证

关键几条：

| 判据 | 守什么 |
|---|---|
| `RuntimeSymbols_MatchRuntimeFeatures_BothWays` | **两表不许漂移** —— 漏登记 = 静默漏报，正是本机制要治的病 |
| `SymbolShape_IsIdentifierOrDottedIdentifier` | `Symbol` 是**被扫描的字面符号**，不是正则；写成 `bit32.*` 相当于把正则注进扫描器 |
| `PlainCode_ProducesNoHits` | 正控：`table.insert` / `math.floor` / `string.len` **都不报** —— `table` / `math` 这两个库**本身** 5.1 就有，敏感的只是新增的**字段** |
| `StringLiteral_IsNotAReference` / `Comments_AreNotReferences` / `FieldOrLongerIdentifier_IsNotAGlobalReference` | 反假阳性：`"bit32.band"`、注释里、`x.bit32` / `x:bit32()` / `bit32x` / `_bit32` 都不算引用 |
| `TargetHighEnough_ReportsNothing` | 目标已定且够高 ⇒ 零命中（挡住"永远全报"的坏实现） |
| `TargetLua51_ReportsEverySymbol_AsUnavailable` | 目标 = 5.1 ⇒ 全判 `Unavailable`、消息点名**真实目标**、且**不含"未定"** |
| `UnknownTarget_Message_DoesNotClaimTheSymbolIsMissing` | 反向：未定的文案**不许**说"不存在" |
| `EngineValidate_RuntimeSymbol_IsWarningNotError` | **本组最要紧**：`IsValid == true`、`Errors` 空、`Warnings` 有一条 |
| `EngineValidate_SyntaxError_SkipsRuntimeCheck` | 语法没过时不跑运行前检查 |
| `BuiltinTemplates_HaveNoRuntimeWarnings` | 元判据：4 个内置模板不许带出黄字（用户点一下就插入） |

**负控 4/4**（注入 → 只红预期的那几条 → 立刻还原）：

| # | 注入 | 结果 |
|---|---|---|
| 1 | `ScriptEngineService` 里 `result.Warnings.Add` 改成 `result.Errors.Add` | ✅ 红 **2/25**，恰好是 `EngineValidate_RuntimeSymbol_IsWarningNotError` + `NegativeControl_InjectingBit32IntoATemplate_IsCaught` |
| 2 | 去掉字符串遮蔽（调用 `MaskStringLiterals` 那一步） | ✅ 红 **1/25**：`StringLiteral_IsNotAReference`（且无"未使用方法"警告） |
| 3 | 去掉可用性收敛（`if (target != Unknown && Minimum <= target) continue`） | ✅ 红 **1/25**：`TargetHighEnough_ReportsNothing` |
| 4 | 往 `RuntimeSymbols` 塞一条 `Features` 里没有的符号 | ✅ 红 **1/25**：`RuntimeSymbols_MatchRuntimeFeatures_BothWays` |

### 12.5 目标一旦确定，这一层自动收敛

`CheckRuntimeSymbols` 的行为**由 `Target` 一个字段决定**：

| `Target` | 行为 |
|---|---|
| `Unknown`（当前） | 每处引用都报，判定 `Undetermined`，文案说清"若设备版本 ≥ N 则无此问题" |
| 已定、且符号可用 | **不报** |
| 已定、且符号不可用 | 报，判定 `Unavailable`，文案点名目标版本 |

⇒ 与 §6 的触发线（`Target_IsStillUnknown_AndDecidingItTriggersRealWork`）是**同一处开关**，
不需要第二处去改。那条触发线红了时，本机制会自动变准；要补的是"重跑探针 + 更新 `Features`"。

### 12.6 读数

- `HmiEditor.Core.Tests` 的 Lua 相关：**94 → 119**（**+25** = `LuaRuntimeCheckTests`）；
  该组 **119 通过 / 0 失败 / 0 警告**；
- 提交 **`93db3d9`**（10 files changed，**+648 / −13**，`2026-09-20T17:48:26+08:00`）；
- 门禁 **7 步全绿 / `GATE_EXIT=0`**，构建步 **0 警告 / 0 错误**；
  **八程序集合计 2933** —— Core 大队 **2084 → 2101**（**+25**，另有 8 条"跑 `git` 的用例"单独一趟），
  其余六个程序集（AI 315 / App.UiTests 255 / Device 87 / Communication 75 / Hardware 44 /
  Simulator 44 / RenderTests 4）**逐条与阶段 40 一致** ⇒ 差额恰好是新测试类，**纯净增**；
- 日志 `.workbuddy/tmp/ci-gate-2026-09-19-luaruntime.log`，mtime **`17:49:27`**，
  **晚于**提交 `93db3d9` 的 **`17:48:26`**（差 61 秒）。

### 12.7 仍未闭环（本机制之后）

| # | 项 | 状态 |
|---|---|---|
| ① | 「某版本起**没有了**」这种反向约束 —— 典型是 5.1 的全局 `unpack` 在 5.2 被移进 `table` | **模型表达不了**（`Minimum` 只表达"从某版本起才有"）⇒ 本表**刻意不收**，硬塞进来会造一个说谎的模型 |
| ② | 局部变量遮蔽（`local bit32 = {}`）与表键（`{ bit32 = 1 }`） | 已知**假阳性**；因为是提示、不判红，取舍是"宁可多报不可漏报" |
| ③ | 长字符串 `[[bit32.band]]` | 未遮蔽（`StripComments` 本身也不处理长字符串）⇒ 多报，同上 |
| ④ | `CheckSuspiciousApis` 未搬 | 见 §11 ④ 与登记表 #36 ④ |
| ⑤ | **中文校验消息未进本地化字典** | ~~`ExplainHit` 的文案是 Core 侧中文字面量，不随 UI 语言切换（与既有那条同源）~~ ⇒ ✅ **已闭环（2026-09-20，阶段 42）**：`ExplainHit` 保留为**中文回落**，界面改走 `LuaDialect.DescribeHit` 给出的 `(Code, Args)` 查字典；`ScriptError` 加 `Args`（对齐 `ValidationIssue`），Code 细分到 **19 个**，行号从 `Message` 剥离。**注意这条在阶段 41 之后被放大了** —— 新增的警告文案同样直接显示给用户。专题 `docs/2026-09-20-core-script-message-localization.md` |

> **阶段 42 回补（2026-09-20）**：本表是**阶段 41 当时**的快照，上表 ⑤ 已闭环（原文划掉保留）。
> 另外 §12.6 的读数在阶段 42 之后是：八程序集 **2940**（**+7**，全部落在 `App.UiTests` 255 → 262 ⇒ 纯净增）；
> 门禁日志 `.workbuddy/tmp/ci-gate-2026-09-20-scriptmsg.log`，mtime **`18:50:27`**，晚于提交 `1eec98a` 的 **`18:49:26`**（差 61 秒）。
