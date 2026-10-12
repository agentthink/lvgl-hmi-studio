# RuntimeConfig 字段下沉：10 个字段里只有 1 个真的走到了设备

> 2026-09-16（第 21 轮）。上一轮用「盘点到设备端落地情况」的手法找出了会话超时这个洞，
> 本轮把同一手法扩大到**全部工程配置**，选中 `RuntimeConfig`（名字即"运行时"、10 个字段、
> 与已闭环的 `SecurityConfig` 同族）。

## 1. 症状：一个名字叫"运行时"的配置页，改它不影响运行时

工程设置面板里 `RuntimeConfig` 的 **10 个字段全部有完整 UI**（读写两条路径都在
`ProjectSettingsView.axaml.cs:359-367` / `473-482`），其中 5 个还能被 AI 工具 `update_runtime` 改
（`AgentToolFactory.cs:894`）。但把字段名铺到生成器与固件源码里找消费点，结果是：

| # | 字段 | 面板 UI | `update_runtime` | 下发路径 | 设备端消费 | 结论 |
|---|---|---|---|---|---|---|
| 1 | `ScanIntervalMs` | 数字 | ✅ | ❌ → ✅ `HMI_POLL_INTERVAL_MS` | ✅ 采集循环休眠 | **本轮闭环** |
| 2 | `LogLevel` | 下拉 | ✅ | ❌ → ✅ `HMI_LOG_LEVEL` | ✅ 分级宏裁剪 | **本轮闭环** |
| 3 | `BootScreenId` | 屏下拉 | ✅ | ✅ 生成代码 `lv_screen_load` | ✅ | 有效（更早轮次） |
| 4 | `HistoryStoragePath` | 文本框 | ❌ | ❌ | ❌ | 未下发 |
| 5 | `WatchdogTimeoutMs` | 数字 | ✅ | ❌ | ❌ | 未下发 + **描述本身错** |
| 6 | `HeartbeatIntervalMs` | 数字 | ❌ | ❌ | ❌ | 未下发 |
| 7 | `EnableCrashDump` | 勾选 | ❌ | ❌ | ❌ | 未下发 |
| 8 | `CrashDumpSector` | 数字 | ❌ | ❌ | ❌ | 未下发 |
| 9 | `MaxRuntimeTasks` | 数字 | ❌ | ❌ | ❌ | 未下发 |
| 10 | `EnableHmiReboot` | 勾选 | ✅ | ❌ | ❌ | 未下发 + **旧描述指向不存在的对象** |

**本轮开始前的真实状态：10 个字段里只有 `BootScreenId` 一个真的生效。**

两个值得单独拎出来的观察：

- **AI 工具的参数有 2/5 是空转的**。`update_runtime` 暴露的 `watchdog_timeout_ms` 与
  `enable_hmi_reboot` **既能写进工程、又不会有任何后果** —— 比面板更糟，因为面板上至少还看得见
  旁边那些同样没用的字段，而 AI 是"照着工具描述改"的，它不知道自己改了个死键。
- **`CodeGenerator.BuildSystem.cs` 只下发两个宏**（`HMI_OPERATION_MODE` /
  `HMI_TAMPER_DETECTION`），**都来自 `SecurityConfig`，没有一个来自 `RuntimeConfig`**。
  固件的 `runtime_config.h` 是编译期硬编码宏 —— 与 `RuntimeConfig` 只是**名字撞车**，
  两边从来没有过通道。

## 2. 三类归因：为什么不能"一次全接上"

| 类 | 字段 | 为什么不能直接接 |
|---|---|---|
| **A. 文案/语义本身错** | `WatchdogTimeoutMs`、`EnableHmiReboot` | 描述里承诺的行为在设备上**没有对应物**。接不上不是"缺一条下发"，是**没有可下发的东西** —— 得先立功能、或先删描述 |
| **B. 有现成固件位点** | `ScanIntervalMs`、`LogLevel` | 固件里已经有 `HMI_POLL_INTERVAL_MS` 与一套日志宏，只是**硬编码**。改成"优先取生成头、缺头回退"即可真闭环 |
| **C. 需先立功能** | `HistoryStoragePath`、`HeartbeatIntervalMs`、`MaxRuntimeTasks`、`EnableCrashDump`、`CrashDumpSector` | 设备端连"这个机制的骨架"都不存在：没有统一心跳帧、没有任务池、没有崩溃转储、`/rom` 是只读 squashfs。下发一个无接收位点的宏是**制造误导** |

本轮只做 B 类（用户选定的切法：**低成本真闭环**），A 类修文案，C 类按状态标注 + 守卫锁住。

## 3. 决定一：`__has_include` + `#ifndef` 缺省回退

生成头 `generated_runtime.h` 走既有下沉通道（`RuntimeConfigCodeGen` → `SimulatorProjectExporter`），
固件两个消费点都用「**有就取、没有就用历史默认**」：

```c
#if defined(__has_include)
#  if __has_include("generated_runtime.h")
#    include "generated_runtime.h"
#  endif
#endif
#ifndef HMI_POLL_INTERVAL_MS
#  define HMI_POLL_INTERVAL_MS 100         /* 无生成头时的默认周期 */
#endif
```

**两个可配值都用 `#ifndef` 包住，不是为了好看**：host 测试靠 `-DHMI_LOG_LEVEL=n` 跑出三种门槛，
不包就只能测一种；而"只测一种门槛"等于没测门槛。

## 4. 决定二：`回退分支提不提供数值` 是语义决定，两边**刻意不同**

| 值 | 缺生成头时 | 为什么 |
|---|---|---|
| `HMI_POLL_INTERVAL_MS` | **回退到 100** | 采集循环**无条件要用**这个参数。没有数字就编不过 —— 回退不是妥协，是这个值的唯一合理语义 |
| `HMI_LOG_LVL_*`（级别常量） | **不提供** | 门槛功能关闭时 `HMI_LOG_AT` 走的是**丢弃 `lvl` 实参**的 `#else` 分支 ⇒ `HMI_LOG_LVL_*` 一次都不会被展开。此时提供数值就是**在设备端另立一份级别定义**，正是本轮要消灭的东西 |

第二行靠的是 C 标准的一条细节：`HMI_LOG_AT` 的 `#else` 分支替换列表里**没有 `lvl`**，
所以该实参连宏展开都不会发生 ⇒ `HMI_LOG_LVL_DEBUG` 未定义也不报错。这一点写在了
`hmi_log.h` 的头注释里，并写成 host 测试的**场景 4**（不带生成头编译，断言"不提供级别常量"）。

> 后来人接第三个字段时**不要照抄**：先问"这个值设备端是不是无条件要用"。
> 是 ⇒ 给回退数值；不是（功能关闭时它整段被编译掉）⇒ 不给。

## 5. 决定三：日志宏必须**独立成文件**（`hmi_log.h`）

原来的日志宏挂在聚合头 `hmi_runtime.h` 里，而聚合头会拉进 `lvgl.h` —— **host 测试没有 LVGL**。
于是「日志门槛按配置裁剪」这条链路在主机上**根本编不过** ⇒ 闭环只能退化成"注释声称生效"。

拆出 `firmware/t113/app/runtime/hmi_log.h`（**刻意不 include lvgl.h**，只 `<stdio.h>`）后，
`hmi_log_test.c` 能真编译、真跑、真数"哪几行出来了"。

**两档用法，别混**（头注释也写明了）：

| 宏 | 语义 |
|---|---|
| `HMI_LOG(...)` | **无条件**关键信息（启动横幅 / FATAL / 操作审计）。不受门槛约束 —— 让日志级别能把它静音 = 关掉现场排查能力 |
| `HMI_LOG_DEBUG/INFO/WARN/ERR(...)` | 分级日志，按 `HMI_LOG_LEVEL` 裁剪。门槛语义：输出「严重度 **>=** 配置值」 |

## 6. 有意的行为变更：19 处 DBG 日志默认静音

`LogLevel` 之前的默认值**是纸面上的**。接上之后默认 Warning(2) 立刻生效，于是固件里 19 处
原先硬编码的 `HMI_LOG("DBG: ...")`（`main.c` 12 处 + `drivers/lcd_sunxifb.c` 7 处）改用
`HMI_LOG_DEBUG`，**默认不再输出**。

这不是回归，是"默认值终于生效"的必然结果；要看这些输出，把工程设置 → 运行时 → 日志级别
调到 `Debug` 后重新导出即可。

## 7. A 类：两处**描述本身就是错的**（本轮修正）

| 字段 | 旧描述 | 事实 |
|---|---|---|
| `WatchdogTimeoutMs` | "超过此时间未喂狗则 **MCU 复位**" | T113-S3 上跑的是 Linux 用户态进程，**没有 MCU、也没有喂狗这回事**；固件全树不存在任何 watchdog 逻辑。这个字段从建模起就没有对应物 |
| `EnableHmiReboot` | "是否允许通过系统变量 **`$SYS.Reboot`** 触发软重启" | `SystemVariable` 里预置的系统变量用的是 **`$SYS$XXX`** 形式（`$SYS$TIME`/`$SYS$USER`/`$SYS$SCR` …），**没有任何 Reboot 变量**，设备端也没有对应处理分支。这个开关从来没有可作用的对象，勾不勾都一样 |

两条都不是"记错了某个事实"，是**写下了一个从未实现的行为承诺**。这是本项目第 3、第 4 例同类问题
（阶段 7、阶段 8 各修过一处），所以本轮把状态标注**直接写进模型注释**（`RuntimeConfig` 类注释
给出「已闭环 3 / 未下发 5 / 描述错 2」三张清单），让下一个改字段的人先看到它。

顺带标注了一条**不改**的重复：`RuntimeConfig.ScanIntervalMs` 与
`VariableManager.DefaultScanIntervalMs` **语义重复**（都默认 100、都叫"扫描周期"、都零消费，
UI 还分别在两个面板里）⇒ 一个工程里摆着两个"扫描周期"输入框。合并属于**产品决策**，
本轮只如实标注、不擅自合并。

## 8. 验证

| 项 | 结果 |
|---|---|
| `RuntimeConfigCodeGenTests`（定向） | **31/31 全绿** |
| 固件 host 测试（`run_host_tests.sh`，**7 步 → 8 步**） | 全绿；新增第 8 步 4 场景 × 8 判据 |
| 负向验证（8 条注入：7 组 C# + 1 组 C） | **8/8 命中**，还原后两侧复跑全绿。**但走了两轮**：首轮 1/8、次轮 7/8 —— 两次都是**扫描器判据自己有洞**（见 §8.1 教训二），不是守卫无效 |
| 全量 `ci-gate.sh` | **5 步全过**：脚本语法 21 / 路径卫生（579 文件）/ 构建 **0 警告 0 错误** / 全量 **2469/2469**（Core **1851**）/ 固件 host **8 步全过** |

新增测试：

| 文件 | 内容 |
|---|---|
| `tests/HmiEditor.Core.Tests/RuntimeConfigCodeGenTests.cs` | 31 项：A 值→宏 / B 日志门槛从枚举推 / C 产物宏集合精确相等 / D 字段下发期望表（**双向完整**）/ E 导出落盘 / F host fixture 逐字节守卫 / G 固件消费侧 |
| `firmware/t113/tests/hmi_log_test.c` | 4 场景 × 8 判据（周期取值 / 级别常量存在性 / 四个级别输出与否 / 无条件 `HMI_LOG` 必出 / `[hmi]` 前缀） |
| `firmware/t113/tests/fixtures/generated_runtime.h` | 生成器的**真实产物**（`ScanIntervalMs=250, LogLevel=Warning`），由 C# 侧逐字节钉住 |

### 8.1 两条通用教训

**教训一：受管清单守卫必须写成"独立期望表 + 双向比对"。**
D 类那条期望表如果写成"遍历属性、断言不下发"，把字段改名或改成 `null` 只会让它**从循环里消失**，
守卫照样绿。必须显式写出期望表，再比对（多一个、少一个字段都红）。

**教训二（本轮抓到）：负向验证的「扫描器」自己会坏，且坏法与"守卫无效"完全无法区分。**
首轮 8 条注入跑出 **1/8 命中**，7 条报"未命中"。看输出是「用例已红；needle 在失败块内：False」
—— 目标用例**确实红了**，判据却说 needle 不在块里。

根因不在守卫、也不在被测代码，在**解析失败块的函数** —— 而且**改了两轮才对**：

| 轮次 | 结果 | 真因 |
|---|---|---|
| 第 1 轮 | **1/8 命中**，7 条报"未命中"（明细是「用例已红；needle 在失败块内：False」—— 用例**确实红了**，判据却说 needle 不在块里） | 块头判据写成 `用例名 in 行 and '[FAIL]' in 行` ⇒ 命中的是**最底下 xUnit 的汇总行**（那行有 `[FAIL]` 也有用例名，但**没有错误消息、没有堆栈**）。从那行往后找不到「堆栈跟踪」，块就只有它自己 |
| 第 2 轮 | **7/8**；只剩 case 2 报"解析不出失败块" | 块头判据改成 `失败\s+(\S+)\s*\[` 后**Theory 的块头匹配不上**：名字后面跟的是 `(bad: 2147483647)`，**参数里带空格**，而「非空白串后紧跟 `[`」这条要求把它卡死。`[Fact]` 的名字里没空格 ⇒ 侥幸通过 —— **只测 `[Fact]` 的扫描器会一直是绿的** |

真实的失败块长这样（两种块头都在，注意第 1 轮误抓的是最后那行）：

```
  失败 HmiEditor.Core.Tests.…LogLevel_ThresholdAndConstants_AreDerivedFromTheEnum [8 ms]      ← Fact 块头
  错误消息:
   Assert.Contains() Failure: Sub-string not found
Not found: "#define HMI_LOG_LEVEL 0"                                                           ← needle 在这里
  堆栈跟踪:
…
  失败 HmiEditor.Core.Tests.…ScanIntervalMs_…WithDiagnostic(bad: 2147483647) [8 ms]            ← Theory 块头（参数含空格）
  错误消息:
Not found: "#define HMI_POLL_INTERVAL_MS 100"                                                  ← needle 在这里
  堆栈跟踪:
…
失败!  - 失败:     4，通过:     0，已跳过:     0，总计:     4，…                                 ← 运行汇总行（不是块的一部分）
[xUnit.net 00:00:00.44]     HmiEditor.Core.Tests.…LogLevel_…DerivedFromTheEnum [FAIL]          ← 第 1 轮误抓的就是这种行
```

修法三条，缺一不可：

1. 块头判据改成「**行首是 `失败 `/`Failed `**」这一条（名字与 `[` 之间不管有什么）；
   块边界改成「到下一个块头 / 到运行汇总行（`失败!  - 失败: …`）或 `[xUnit.net ` 行之前」
   —— 最后一块若按「到文件结尾」算会把整段尾巴吞进去，判据反而变松；
2. **给扫描器加双向自检**（这步是必需的）：喂四种合成日志 —— 全绿日志**不得**判出失败块、
   只带 xUnit 汇总行的日志**不得**判出失败块、真的失败块**必须**被判出且 needle 在里面、
   **Theory 块头（`(bad: 2147483647)`，参数含空格）必须被识别成块且不被截断**。
   最后这条就是第 2 轮那个 bug 的**回归钉子**。判据自己坏掉时，它报的"未命中"与"守卫无效"
   外观一模一样，**没有这层自检就只能靠肉眼去怀疑脚本**；
3. **needle 的松紧也要复查**：C 侧那条注入原本拿 `FAIL` 当 needle —— **任何**一条判据失败都会
   输出 `FAIL`，判据退化成恒真。改成该注入真正打破的那条判据名
   （`DEBUG 级输出与否符合门槛`）。

**为什么之前几轮没暴露第 1 个问题**：阶段 7 那轮的判据是「用例名与 `[FAIL]` 同行」
（`project-status.md` 验证记录里写着"判据比上一轮更硬…行尾 `[FAIL]`"）—— 而**满足这条的恰好就是
xUnit 末尾那行汇总行**。它确实能证明"红的是对的那个用例"（比只判退出码强，能挡住"别的用例碰巧红了"），
所以那几轮的结论没问题；但它**撑不住**本轮加硬的「期望文本必须落在**该用例自己的失败块**里」
—— 汇总行里没有错误消息，硬判据一上，抓错行这件事才现形。**判据每一次加硬都要重新检查它的实现。**

## 9. 遗留

- **`ScanIntervalMs` 与 `VariableManager.DefaultScanIntervalMs` 语义重复**：见 §7 末。合并要先定
  "哪个面板是权威、另一个是删是留"，属产品决策。
- **5 个字段仍未下发**（`HistoryStoragePath` / `HeartbeatIntervalMs` / `MaxRuntimeTasks` /
  `EnableCrashDump` / `CrashDumpSector`）：都属 §2 的 C 类，要闭环得先立功能（统一心跳帧 /
  任务池 / 崩溃转储机制）。已在 `RuntimeConfig` 类注释里逐条标注「未下发 + 要闭环得先做什么」。
- **`update_runtime` 的两个空转参数**（`watchdog_timeout_ms` / `enable_hmi_reboot`）**本轮未动**：
  删参数是破坏性变更（AI 提示词与调用方都要跟），改语义要先有 §7 的功能。**建议下一步处理**：
  要么从工具描述里拿掉，要么至少把"当前不生效"写进工具描述 —— 现在 AI 照着描述改一个死键，
  用户看到的却是"已更新运行时配置"。
- **`WatchdogTimeoutMs` / `EnableHmiReboot` 仍是模型里的死字段**：本轮做的是**把错误的描述改对**
  并标注未下发，**没有**删字段（删会破坏老工程 JSON 的兼容）。
