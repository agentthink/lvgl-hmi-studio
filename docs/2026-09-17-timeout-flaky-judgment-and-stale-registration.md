# 登记表 §4 第 22～25 项：一条默认值、一条负载敏感判据、一条**不成立**的登记、一条警告清理

> 按顺序推进 `docs/2026-09-17-open-items-register.md` §4「软件侧可验证」里仍开着的第 22～25 项
> （第 21 项「用户表校验不覆盖口令强度」是**刻意限制**，跳过）。
> 日期：2026-09-17

---

## 0. 一句话结论

这一批里**只有两项需要改代码**，而且性质完全不同；另两项一个是我自己上一轮写错的登记、
一个是真警告：

| # | 性质 | 处置 |
|---|---|---|
| 22 | **真缺陷**（默认值覆盖不住长生成） | 默认超时 60 → **240 s**，并用**算式**钉住 |
| 23 | **判据脆弱**（绝对毫秒上界） | 改成"两次不同窗口的**差值**"判据 |
| 24 | **登记有误**（不是缺陷） | **撤销该条**并写清为什么它不可能失败 |
| 25 | 真警告（4 条 `-Wdiscarded-qualifiers`） | 局部修掉 + 写清为什么不做全仓 const 化 |

---

## 1. 第 22 项：默认超时覆盖不住最长的单次生成

### 1.1 取证

`LlmProviderConfig.TimeoutSeconds`（默认 **60**）被直接用作 `HttpClient.Timeout` ——
即**整个响应读完为止**的上限（含流式读完整条 SSE 流）：

```csharp
_http = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
```

而 Agent 的单次请求是 `MaxTokens = 8192`（`AgentEngine`）。按慢速中转 / 本地模型
**~45 token/s** 估算：

```
8192 token ÷ 45 token/s ≈ 182 s   >   60 s
```

⇒ 生成会在**一半左右被掐断**，用户看到"AI 生成失败"。这与模型质量无关，
**换一个慢一点的模型或中转就会复发** —— 而本字段**没有设置界面**（`LlmConfigStore`
不持久化它、AI 面板也没有输入框），默认值是唯一杠杆。

### 1.2 修法：240 s，并由算式钉住

取 **240 s**（≈182 s 留约 30% 余量）。放大超时的代价可控：长任务期间界面有流式增量与停止按钮
（阶段 11），用户随时可取消，不必等超时。

判据不是"默认值等于 240"（那种测试只会让人改两处），而是**从算式推出下界**：

```csharp
const int AgentMaxTokens = 8192;
const double SlowRelayTokensPerSecond = 45.0;
var neededSeconds = AgentMaxTokens / SlowRelayTokensPerSecond;   // ≈182
Assert.True(new LlmProviderConfig().TimeoutSeconds >= neededSeconds, ...);
```

于是"有人把它改回 60"会红，并在失败信息里直接给出算式与失效模式；
而"有人把 8192 调大"时，这条判据仍然成立（它读的是同一组常量）。

---

## 2. 第 23 项：一条**靠绝对毫秒余量**的判据

### 2.1 问题

```csharp
Assert.InRange(sw.ElapsedMilliseconds, 150, 2000);   // firstByteMs = 200
```

上界 2000 ms 是**绝对余量**。本沙箱存在全局串行的资源竞争，定时器续体可被推迟到秒级
（`docs/2026-09-17-sandbox-newpath-serialization.md`），同族判据已被撞穿过好几条
（阶段 6 / 7 / 8 各一条）。同一文件里相邻用例只断言**下界**（`>= 400`）——
下界对负载天然安全（负载只会让耗时更大），上界不是。

### 2.2 修法：差值判据（对负载不敏感）

```csharp
var fast = await MeasureNoDataAsync(firstByteMs: 200);
var slow = await MeasureNoDataAsync(firstByteMs: 1200);
Assert.InRange(slow - fast, 600, 8000);
```

两次测量**承受同一份负载**，共同项在差值里抵消；而"等的是首字节窗口"与"等的是一个固定值"
在差值上截然不同：

| 实现 | `slow - fast` |
|---|---|
| 按首字节窗口返回（正确） | ≈ **1000 ms**（窗口差） |
| 空等一个固定超时（要防的行为） | ≈ **0 ms** |

所以这条判据**不需要**在负载与灵敏度之间折中：下界 600 ms 已能区分两者，
上界 8000 ms 只排除"完全无界"。这是本项目"把 flaky 转成可构造输入"的又一例。

---

## 3. 第 24 项：撤销一条**不成立**的登记

登记原文：*"`ConfigCodeGenConsumptionTests` 用 POSIX 绝对路径字面量（`/home/user/proj`）
⇒ 在 Windows 上断言会挂（平台耦合）"*。**复核后不成立。**

```csharp
// 测试
Assert.Equal(System.IO.Path.Combine("/home/user/proj", "./export"), resolved);

// SUT
return Path.IsPathRooted(OutputDirectory) ? OutputDirectory
                                          : Path.Combine(projectDirectory, OutputDirectory);
```

测试的期望值是**同一个 `Path.Combine` 调用**（同样的入参）算出来的，与 SUT 分支里的那次
逐字相同 ⇒ 在**任何**平台上两侧恒等，不可能因平台而异。绝对路径那条也一样：
`Path.IsPathRooted("/opt/output")` 在 Windows 上同样为 `true`，直接原样返回。

=> 该条**撤销**。残留的只是一个**覆盖**层面的 nuance：夹具只用了 POSIX 形状的字面量，
形如 `C:foo` 的"盘符相对路径"没被覆盖 —— 但那不是一个失败风险，也不值一条测试。
（这正是复核纪律该有的样子：登记表是判断的产物，**判断本身也会写错**，
所以"按登记表动手"之前必须先用代码证伪一次。）

---

## 4. 第 25 项：`host_main.c` 的 4 条 `-Wdiscarded-qualifiers`

### 4.1 复现（先复现再动手）

`host_main.c` 不被 `run_host_tests.sh` 编译，要按它自己的 Makefile 编：

```bash
cd firmware/t113/host_test && rm -f objs/host/host_main.o && make objs/host/host_main.o
```

得到 **4 条**警告（`host_main.c:83-86`）：

```
warning: assignment discards 'const' qualifier from pointer target type
    g_font_cn_12 = cn_font_create(12);  if (!g_font_cn_12) g_font_cn_12 = &lv_font_source_han_sans_sc_14_cjk;
```

### 4.2 根因

`LV_FONT_DECLARE(x)` 展开为 **`extern const lv_font_t x;`**，即 LVGL 内置字体是 **const**；
而 `g_font_cn_*` 的类型是**非 const** 的 `lv_font_t*`。

**这不是本文件的疏忽**：这四个全局同时被**设备侧** `app/main.c` 定义，并被共享头
`sys_settings.h` 与 `settings_ui.h` 的 `set_ta_font(lv_obj_t*, lv_font_t*)` 使用 ——
设备上它们装的是 FreeType 动态创建的可变字体对象。把整条链改成 const 要动
**7 个固件文件（含设备运行时）** 去换 4 条 **host 工具**警告，明显不成比例，
而且任何一处漏改都会**编不过设备端**。

### 4.3 修法：局部显式转换 + 写清"为什么安全"

只在这 4 处回退赋值上加 `(lv_font_t*)`，并把理由写进注释：这些指针**只被喂给 LVGL 的只读字体 API**
（`lv_obj_set_style_text_font` / 判空与比较），从不经它们写入 —— 所以去 const 不改变任何行为，
纯粹是让类型匹配。验证：同一命令重跑，**0 警告**、产物正常生成（22832 B）。

> 不这么做的另一条路（全仓 const 化）记在 §7，留待真正需要时再做。

---

## 5. 负向验证（2 组，全部命中）

第 24 项没有可注入的行为（它本来就是"不需要改"），第 25 项是编译期警告，**两者都不产生 NV 组**。

| NV | 注入 | 期望红的用例 |
|---|---|---|
| A | `TimeoutSeconds` 默认值退回 `60`（**复现 #22 的缺陷**） | `DefaultTimeout_CoversLargestSingleRequest` |
| B | `CommandFrameCollector` 的首字节窗口不再跟随参数（改成固定 200ms，**复现 #23 要防的行为**） | `NoData_ReturnsEmptyAfterFirstByteWindow_NotAfterCommandTimeout` |

---

## 6. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| `host_main.c` 单文件重编（Makefile 原参数） | **0 警告**（修前 4 条），产物 `objs/host/host_main.o` 22832 B |
| 全量测试（7 程序集） | **2630 通过 / 0 失败**（AI 313 → **315**；本轮基线 2628 ⇒ **+2**） |
| 负向验证 | **2/2 命中** |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 7. 范围边界（本轮不做，已登记）

1. **`g_font_cn_*` 的全仓 const 正确性**：把设备侧定义、`sys_settings.h`、`soft_keyboard.c`、
   `screen_manager.c`、`settings_ui.h` 的 `set_ta_font` 一起改成 `const lv_font_t*`。
   收益是类型正确；成本是 7 个固件文件 + 设备编译风险。**本轮用局部转换替代**，
   理由与安全论证见 §4.3。若要动，应作为独立的固件重构，并在真机回归里覆盖。
2. **`TimeoutSeconds` 仍无可配置入口**：本轮只调默认值。给出设置界面属产品决策
   （放 AI 面板还是应用设置、是否随工程保存）。
3. 第 21 项（用户表校验**不覆盖口令强度**：只能看到哈希、看不到长度）**刻意限制**，跳过。
4. §4 其余仍开着项按顺序在后续轮次处理（Dock 真实拖拽 / `ViewLocator` 旧 Parent、
   运行时动态文案入字典、en-US 校对、实机相关项）。
