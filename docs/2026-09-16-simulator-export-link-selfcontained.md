# 导出模拟器工程自洽（链接契约）+ 判定策略下沉（2026-09-16）

> 承接 `docs/2026-09-16-permission-model-sinking.md` §8 遗留 **2**：
> 「模拟器侧 4 个动作 + `hmi_require_permission` 只有 `extern` 没有定义 ⇒ 导出工程用了这些动作/开权限门就**链接失败**」。
>
> 开工后**发现缺口比登记的更大**（4 → 7 个符号），并顺带扯出两个同族缺陷。
> 本轮 = 三件事：**A** 链接自洽、**B** 非法浮点字面量、**C** 消灭判定策略的第二份实现。

## 1. 缺陷形态：单元测试全绿，`cmake --build` 到链接期才炸

生成代码对宿主的依赖写成了 `extern` 声明。这类声明的语义是"**宿主必须提供**"，而
"宿主到底提供了没有"这件事，**C# 侧的单元测试根本看不到**：

```
[100%] 链接 C 可执行文件 sim_demo
main.c.o: in function `on_click_obj_c8':
generated_hmi.c:412: undefined reference to `hmi_show_alarms'
```

导出工程能编译（每个 TU 都编得过），只有**真链接**才会暴露。而 `SimulatorProjectExporter`
的测试只断言"文件生成了 / 里面有某段文本"，所以从第一版起这个洞一直在。

### 1.1 缺的到底有几个

《遗留》里写的是 4 个动作 + 1 个权限门 = 5 个。**穷举判据（§2）实测是 7 个** ——
多出来的是 `var_raw_read` / `var_raw_write`：生成代码在 `on_click` 回调里**确实调用**它们
（走 `set_tag_value` 的写路径），但导出工程里从没有人实现过。

| # | 宿主符号 | 谁在调 | 登记时是否已知 |
|---|---|---|---|
| 1 | `hmi_show_alarms` | `ShowAlarms` 动作回调 | ✅ |
| 2 | `hmi_show_trends` | `ShowTrends` 动作回调 | ✅ |
| 3 | `hmi_login` | `Login` 动作回调 | ✅ |
| 4 | `hmi_logout` | `Logout` 动作回调 | ✅ |
| 5 | `hmi_require_permission` | 动作回调的权限门（Operator 及以上模式） | ✅ |
| 6 | `var_raw_read` | 绑定刷新路径 | ❌ **本轮新发现** |
| 7 | `var_raw_write` | `on_click` 写路径 | ❌ **本轮新发现** |

## 2. 判据：穷举"生成代码要求宿主提供什么"

**不逐个列名字**。逐个写断言等于把清单抄进测试 —— 以后新增宿主 API 时，测试不会自己长出来。
改成从生成代码**反推**：扫出全部 `extern` **函数**声明，再要求导出工程里每一个都有**定义**。

`tests/HmiEditor.Core.Tests/SimulatorLinkContractTests.cs`：

```csharp
/// 生成代码里的 extern **函数**声明名
private static List<string> DeclaredExternFunctions(string code)
{
    var rx = new Regex(@"^[ \t]*extern\b[^;{}()]*?\b([A-Za-z_]\w*)[ \t]*\(", RegexOptions.Multiline);
    return rx.Matches(code).Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
}

/// 该源码里是否存在 name 的**定义**（带函数体，而非又一条声明）
private static bool Defines(string source, string name) =>
    new Regex(@"^[ \t]*(?:[A-Za-z_]\w*[ \t]+|\*[ \t]*)+" + Regex.Escape(name) + @"[ \t]*\([^;{}]*\)[ \t\r\n]*\{",
        RegexOptions.Multiline).IsMatch(source);
```

判据必须**自证不空转**，所以那条测试里有两处自检：

- **扫描面自检**：`declared.Count >= 6` —— 语料或正则失效时先红，而不是"没缺定义"的假绿；
- **判据自检**：`hmi_screen_switch`（已知有定义）必须判为 true，`no_such_symbol_zzz` 必须判为 false。

### 2.1 判据的第二层：`nm` 符号级证据

C# 判据是**文本级**的（"有没有这段定义"），可能比现实宽。要落到"真链接会不会炸"，
用**只编译不链接 + `nm` 比对**。

> 全文提到的探针脚本（`link-probe.sh` / `strip-host-symbols.py` / `float-literal-neg.py` / `nvrun.py`）
> 都是**一次性**的，写在 `.refactor-tmp/`（仓库内、不入版本控制）里并随本轮清理。
> 下表给的是完整的判据与构造法，照它可原样复现。

```bash
nm --defined-only "$OBJ"/*.o | awk 'NF>=3{print $NF}' | sort -u > defined.txt
nm -u           "$OBJ"/*.o | awk 'NF>=2{print $NF}' | sort -u > undef.txt
comm -23 undef.txt defined.txt > unresolved.txt
# 减掉 libc/libm 的符号表（比维护一份名单可靠），再剔掉 lv_*/SDL_*
```

三点踩坑记录：

1. **`nm -D` 的符号名带 `@@GLIBC_x.y` 后缀** ⇒ 比对前必须 `sed 's/@.*//'`，否则一个都减不掉；
2. **`libm` 要单独减**：`sinf` 在 glibc 里是 `libm.so.6` 的成员，不在 `libc.so.6` 里。
   漏了它，"宿主符号"段里就永远杵着一个 `sinf` —— 那是**假阳性**，会让人开始怀疑判据而不是代码；
3. **为什么不用真链接**：真链接要全量编 LVGL（几分钟），而且本机上游 LVGL **没有 Tina 定制的
   SimSun 内置字体**，会把环境差异混进判据。只编译 + `nm` 同样给符号级结论。

### 2.2 探针自身的正反例（判据必须有反例，否则退化后永远绿）

| 控制 | 构造 | 实测输出 |
|---|---|---|
| **正例**（当前代码） | 直接探 `linkprobe4/` | 未定义 68 个，扣掉 libc/libm/lv_*/SDL_* 后**为空** ⇒ 导出工程自洽 |
| **负例**（修复前的状态） | 从 `main.c` 删掉那 7 个定义（保留调用点） | 未定义 74 个，扣完剩下：`hmi_login` `hmi_logout` `hmi_require_permission` `hmi_show_alarms` `hmi_show_trends` `var_raw_read` `var_raw_write` —— **恰好 7 个，一个不多一个不少** |

负例不是"随手改坏"，它构造的就是**本轮之前导出工程的真实状态**：

```
--- 扣掉 libc 与 lv_*/SDL_* 之后的「宿主符号」= 真链接会 undefined reference 的 ---
    hmi_login
    hmi_logout
    hmi_require_permission
    hmi_show_alarms
    hmi_show_trends
    var_raw_read
    var_raw_write
    ↑ 这 7 个符号真链接时会 undefined reference
```

写负例时自己踩了一个坑：第一版剥离正则带了「可选的前置注释组 + `DOTALL`」，
结果光标被推到很远的地方，把别的内容一起删了（`var_raw_read` 直接"找不到"）。
**删块类正则不要跨行吞注释** —— 只在签名行定位，然后靠花括号配平收尾。

## 3. 修法（A）：在 `main.c` 里补一个"PC 模拟器宿主运行时"段

写清分工，免得后人把这段当成"重复实现"删掉：

```c
/* ── PC 模拟器宿主运行时（对应固件 app/runtime/ 那几个文件）────────────────
 * 生成代码只**声明**这些符号（extern），实现必须由宿主提供。导出工程因此必须是自洽的：
 * 少一个定义，cmake 到链接期就是 undefined reference —— 而那一步单元测试覆盖不到。
 * 守卫：SimulatorLinkContractTests 穷举「生成代码的 extern 函数 − 工程内已定义」。 */

int hmi_screen_switch(const char* name);          /* 已有，补上了段落归属 */
float var_raw_read(int index);                    /* 新：一块 RAM 表顶上 variable_table.c */
void  var_raw_write(int index, float raw);
int   hmi_require_permission(unsigned int bits);
void  hmi_show_alarms(void);  void hmi_show_trends(void);
void  hmi_login(void);        void hmi_logout(void);
```

标签存储的长度**与生成代码同源**（都由导出器手里那份 `tags` 算出）：

```c
#define SIM_TAG_COUNT 3          /* = generated_hmi.c 的 TAG_COUNT */
static float s_tag_raw[SIM_TAG_COUNT];
```

`SIM_TAG_COUNT` 必须**相等**而不是"不小于"：宿主偏小 ⇒ 越界读被静默吞掉
（`var_raw_read` 直接返回 `0.0f`，表现为"变量值永远是 0"）；偏大只是浪费内存。
`tags` 为空时兜底为 `1` —— 长度 0 的数组在 C 里不合法。两条各有断言（§5）。

`#include "generated_security.h"` 是**无条件**加的：这份头是本次导出的产物、必然存在，
而权限门与 `hmi_perm_allows` 都要用它（§4）。

## 4. 修法（C）：判定策略下沉，消灭第二份实现

### 4.1 症状

`hmi_has_permission`（固件）里有一条**语义决定**：

```c
const unsigned int mask = s_logged_in ? s_perm_mask : (unsigned int)HMI_PERM_VIEW_SCREEN;
```

"未登录 = 能看画面、不能操作控件"。模拟器要复刻权限门，就必须把这条公式再写一遍。
两处迟早分叉，而分叉的表现是**「预览里点得动、设备上点不动」**——归因成本极高。
本轮既然要动模拟器的门，就顺手把公式本身下沉进生成头：

```c
/* ── 权限判定（唯一实现）──
 * 设备端 hmi_has_permission 与模拟器 hmi_require_permission 都调它，
 * 谁也不许再自己写「未登录折算」或 (mask & bits) == bits。 */
#if HMI_SEC_ENFORCE
static inline int hmi_perm_allows(int logged_in, unsigned int perm_mask, unsigned int bits)
{
    if (bits == 0u) return 1;
    /* 未登录：只折算「查看画面」（能浏览/换屏，不能操作控件或改配置）—— 这条语义只在这里 */
    const unsigned int mask = logged_in ? perm_mask : (unsigned int)HMI_PERM_VIEW_SCREEN;
    return (mask & bits) == bits;
}
#endif /* HMI_SEC_ENFORCE */
```

两个消费方都只剩转发：

| 消费方 | 修后 |
|---|---|
| 固件 `hmi_has_permission` | `return hmi_perm_allows(s_logged_in, s_perm_mask, bits);` |
| 模拟器 `hmi_require_permission` | `if (!hmi_perm_allows(s_sim_logged_in, s_sim_perm_mask, bits)) { 日志; return 0; }` |

### 4.2 两个刻意的选择

**① `static inline`** —— 每个 TU 各一份，**不产生外部符号、不引入链接依赖**
（本轮的主题是链接自洽，策略本身不能反过来变成新的宿主符号），也不会因未使用而告警。

**② 包在 `#if HMI_SEC_ENFORCE` 里** —— 头缺失时的回退分支**刻意不定义任何 `HMI_PERM_*`**，
所以"用了权限位的代码必须被编译掉"这条**具有传递性**：不只是 `hmi_perm_allows` 自己，
**调用侧同样如此**。固件与模拟器两侧的门都包在 `#if HMI_SEC_ENFORCE` 里，与回退语义一致。

### 4.3 `HMI_SEC_DEFAULT_ROLE_MASK`：模拟器没有角色表

固件按角色 Id 线性查表拿掩码；模拟器**没有角色表**（也不该复制一份表示形式）。
所以生成器把"默认角色的掩码"直接算成一个宏：

```c
#define HMI_SEC_DEFAULT_ROLE_INDEX 0      /* role-operator */
#define HMI_SEC_DEFAULT_ROLE_MASK 0x04Bu  /* Operator */
```

`hmi_login` 在模拟器侧就是 `s_sim_perm_mask = HMI_SEC_DEFAULT_ROLE_MASK;`。
**不变量**：它必须等于角色表里 `HMI_SEC_DEFAULT_ROLE_INDEX` 那一行的掩码 ——
有断言逐角色对拍（含"未知 Id 回退首行 ⇒ 掩码跟着回退"这一例）。

### 4.4 导航候选屏名：`ScreenNavTargets` 单一来源

模拟器知道工程的**全部屏名**（生成期），不必像设备端那样运行时模糊匹配 ——
但它"哪几个名字算报警屏"这条**约定**必须与固件同一份：

| 位置 | 形态 |
|---|---|
| **权威** `Core/CodeGen/ScreenNavTargets.cs` | `Alarm` / `Trends` / `Login` 三份候选表，`All` 的**顺序 = 固件文件里的出现顺序** |
| 固件 `hmi_actions.c` | 三份 `const char* candidates[]` —— **孪生副本**，有逐项 + 顺序守卫 |
| 模拟器 `main.c` | 生成期 `Resolve(project, candidates)` 出结果，**直呼屏名** |

**解析顺序 = 候选顺序**，不是屏幕顺序（与固件 `try_switch`「先遍历候选、再比对屏名」一致）：
否则同一块工程里同时存在 `alarm` 与 `报警列表` 时，两边会挑到不同的屏。
匹配是**精确**的（与固件 `strcmp` 一致），不做大小写/包含匹配。
一个都不匹配 ⇒ 记日志、不切屏（与固件 `try_switch` 失败后的行为一致）。

## 5. 修法（B）：非法浮点字面量（同族缺陷，顺手修）

这是探针编译时撞出来的，**不是本轮设计目标**，但比 A/C 更严重：

```
generated_hmi.c:196:31: error: 整数常量的 ‘f’ 后缀无效
  196 |     set_tag_value(TAG_v_temp, 1f);
```

C 里 `1f` **不是**合法浮点常量（`f` 后缀要求字面量本身带小数点或指数）。
根因在 `NormalizeActionValue` 用了 `"0.########"` —— 对整数值恰好产出无小数点的 `1`，
拼上 `f` 就是 `1f`。**任何带「设置变量」动作的工程都会在编译期失败**，而单元测试全绿
（既有测试把 `100f` 写进了期望值，等于把 bug 钉成了规格）。

```csharp
if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
    && double.IsFinite(number))                        // double.TryParse 认 NaN/Infinity/1e999
    return number.ToString("0.0########", CultureInfo.InvariantCulture) + "f";   // 强制一位小数
```

`double.IsFinite` 这一层不能省：`double.TryParse` 会把 `NaN` / `Infinity` 当合法输入，
直接格式化就产出 `NaNf` / `Infinityf`，同样是编译期炸。

编译器级反例（把导出产物改回 `1f`，见 §2.1 提示）已留档，证据就是上面那行 gcc 报错。

## 6. 测试与守卫

| 关注 | 断言 |
|---|---|
| **链接契约穷举** | 生成代码的 `extern` 函数 **每一个** 都能在导出工程里找到**定义**；含扫描面自检 + 判据正反例 |
| 默认零变化 | `OperationMode.Designer` 下**不得**出现 `hmi_require_permission` 的调用点 |
| 标签存储同源 | `SIM_TAG_COUNT` == `generated_hmi.c` 的 `TAG_COUNT`（解析 `tag_index_t` 成员数）；两个访问函数都按它设界 |
| 标签存储不为 0 长度 | 无绑定变量时 `#define SIM_TAG_COUNT 1`，且生成侧确实不发 `tag_index_t` |
| 策略是唯一实现 | `hmi_perm_allows` 的公式、未登录折算走**权限位宏**、**不含裸 `0x`**、被 `#if HMI_SEC_ENFORCE` 包住 |
| **公式没有第二份** | 扫固件 `runtime/*.c` + 导出产物：`& bits` / `bits &` 只允许出现在生成头 |
| 固件是薄调用 | `hmi_has_permission` 体内有转发调用、**不含** `& bits`、**不含** `HMI_PERM_` |
| 模拟器是薄调用 | `hmi_require_permission` 同样；且 `main.c` 自己 `#include "generated_security.h"` |
| 默认角色掩码 | 4 个 `[Theory]`（三个预置角色 + 未知 Id 回退）逐项等于角色表对应行 |
| 模拟器不复制角色表 | `main.c` 里不得出现 `HMI_SEC_ROLES` |
| **候选表孪生** | 固件三份 `candidates[]` 与 `ScreenNavTargets.All` 逐项 + **顺序**比对；解析器另有非空自检 |
| 解析语义 | 候选顺序优先于屏幕顺序、精确匹配、全不中 ⇒ `null`；生成侧直呼解析结果 / 无落点只记日志 |
| float 字面量 | 穷举扫 `(?<![\w.])\d+f(?![\w.])`；8 例 `[Theory]` 值映射；非有限值回退 `0.0f` |

同时**改了一条既有期望值**：`RuntimeTagsCodeGenTests` 原期望 `set_tag_value(TAG_v_temp, 100f)`
钉的正是那个非法产物，已改为 `100.0f`（并在注释里写明为什么）。

## 7. 负向验证（6 组，全部命中）

判据：每条注入都必须让**指定**测试变红、且失败块里出现**期望文本** —— 不红就说明守卫是空的，
红在别的用例上则说明判据没瞄准。每条注入前 `assert count == 1` 守住唯一性，还原放在 `finally`
并回写原文、跑完立即校验内容一致（脚本一次性，见 §2.1 提示）。

| # | 注入 | 期望命中 | 实测 |
|---|---|---|---|
| a | `main.c` 里 `hmi_show_trends` 的定义改名（调用点不动） | `GeneratedModule_ExternFunctions_…` 且点名 `hmi_show_trends` | ✅ |
| b | 固件候选表 `"alarm_list"` → `"alarm-list"` | `FirmwareCandidateTables_MatchSingleSource_ItemByItem` | ✅ |
| c | `hmi_has_permission` 回退成"自己算一份" | `Firmware_HasPermission_DelegatesToGeneratedPolicy` **+** `Policy_FormulaExistsOnlyInGeneratedHeader` | ✅ 两条同时红 |
| d | 生成头去掉"未登录折算查看画面" | `Header_PolicySinking_IsTheOnlyImplementation`（点名 `HMI_PERM_VIEW_SCREEN`） | ✅ |
| e | 格式串退回 `"0.########"` | `GeneratedCode_NeverEmitsFloatLiteralWithoutDecimalPoint`（点名"非法 C 浮点常量"） | ✅ |
| f | `SIM_TAG_COUNT` 从 `tags.Count` 漂到 `tags.Count + 1` | `SimulatorTagStore_SizeMatchesGeneratedTagCount` | ✅ |

c 一次注入**同时**红两条，正是"两条守卫互补"的证据：一条证明"转发没错"，一条证明"没有第二份"。
只写前者的话，有人加回第二份实现时测试不会红。

## 8. 验证与证据

| 项 | 结果 |
|---|---|
| 全量测试（7 个程序集） | **2320 通过 / 0 失败**（Core 1714、AI 272、App.UiTests 129、Communication 75、Device 64、Simulator 44、Hardware 22） |
| `scripts/ci-gate.sh` | 5 步全过（脚本语法 → 路径卫生 → 构建 → 全量测试 → **固件 host 测试 34/34**） |
| 符号级链接探针（正例） | 未定义符号段扣完 libc/libm/lv_*/SDL_* 后**为空** ⇒ 导出工程自洽 |
| 符号级链接探针（负例） | 剥掉 7 个定义后**恰好**点出那 7 个 ⇒ 判据本身有效 |
| 编译器级负例 | `1f` ⇒ `error: 整数常量的 'f' 后缀无效` |
| 权限语义未变 | 固件 C host 测试 34/34（含「未登录：允许查看画面」+「未登录：拒绝操作控件」两侧）——
  这一条现在**真的走**生成的 `hmi_perm_allows`，等于对下沉做了一次端到端回归 |

## 9. 遗留

1. **真实凭据校验仍未实现**（P2-2 后半）：固件与模拟器的 `hmi_login` 都按默认角色直接授权，
   两侧代码里都写了"落地后应改为等登录屏提交凭据"。模拟器侧现在至少与固件**同语义**。
2. **`DeviceChannelTests.ConcurrentCommands_AreSerialized_NoCrossTalk` 是全量负载下的 flaky**（本轮新发现）：
   门禁第一次跑时红（`Expected: ["ok:AAA","ok:BBB"] Actual: ["","ok:AAA\nok:BBB"]`），
   单独复跑 3/3 绿、门禁第二次跑也绿。根因指向 `TcpDeviceChannel` 的**静默期（300ms）**收帧启发式：
   全量并行负载下服务端回包被拖过 300ms ⇒ 第一条命令收到空串、迟到的两行落进第二条的窗口。
   `DeviceChannelTests` 与 `LineServer` 都不在本轮改动范围内，未修。
3. `HmiEditor.Core.Tests` 的 `TempRoot` 稳定名目录内容累积无上限（既有登记项，未动）。
