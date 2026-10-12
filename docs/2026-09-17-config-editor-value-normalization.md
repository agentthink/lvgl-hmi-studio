# 配置编辑器与判据同口径：表外 LVGL 版本 / 死代码回退 / 口令字节数

> 按顺序推进 `docs/2026-09-17-open-items-register.md` §4「软件侧可验证」里仍开着的
> **第 18、19、20 项**（15/16/17 已在前几轮闭环）。
> 日期：2026-09-17

---

## 0. 一句话结论

三项看起来互不相干，根因是同一条：**编辑器的判断与实际判据（下游消费者 / 设备端）不同口径**，
而它们全都表现为"界面看着正常、结果不对"：

| # | 症状 | 实际判据在哪 |
|---|---|---|
| 18 | 打开设置点一下确定，**8.x 工程被悄悄升成 9.x** | `LvglApiCompatChecker` 按版本分支改写生成代码 |
| 19 | 色深下拉**显示空白**，保存却仍写入一个色深 | `NormalizeColorDepth(null)` 仍会产出一个值 |
| 20 | 中文口令**设备端放行、编辑器却报"至少 N 位"** | 固件 `strlen`（UTF-8 字节） |

---

## 1. 第 18 项：表外 LVGL 版本被静默归一

### 1.1 为什么这不是"补个默认值"

`LvglVersion` **不是纯展示字段**：

```
src/HmiEditor.CodeGen/CodeGenerator*.cs     → LvglApiCompatChecker.Process(code, project.LvglVersion)
                                              （注释：仅当 9.x 时生效；8.x 下那些旧 API 属正常用法）
src/HmiEditor.Core/Services/SimulatorProjectExporter.cs → CMake 分支名 / 依赖表跟随它
```

也就是：**改了版本 = 改了生成代码的语义**（API 兼容改写是否生效）。

候选表是固定的 `8.3 / 9.0 … 9.5`。工程里存了表外版本（更老工程写的 `8.2`、或手改过 JSON）时：

- 装载 `CmbLvglVersion.SelectedItem = "8.2"` → 不在候选里 ⇒ `SelectedItem` 变 **null**；
- 保存 `... = (string)CmbLvglVersion.SelectedItem!` 为空 ⇒ 归一成 **`"9.5"`**。

⇒ **打开工程设置点一下「应用/确定」，8.x 工程就变成了 9.x**，且没有任何提示。

### 1.2 修法：把表外版本放进候选

```csharp
private string[] BuildLvglVersionItems()
{
    var known = new[] { "8.3", "9.0", "9.1", "9.2", "9.3", "9.4", "9.5" };
    var stored = _project.LvglVersion;
    if (string.IsNullOrWhiteSpace(stored) || known.Contains(stored, StringComparer.Ordinal)) return known;
    return known.Append(stored).ToArray();   // 表外版本也进候选 ⇒ 原样往返
}
```

后果：下拉**显示工程里那个版本**（不再空白），保存原样写回；只有用户**显式选了别的版本**才会变更。
与上一轮"失效硬件型号 → 未指定而不绑到另一块板"是同一把尺子：**不静默改用户/历史留下的值**。

---

## 2. 第 19 项：`ResolveColorDepthItem` 是死代码，而它防的坑是活的

该 helper 的文档写得很清楚：

> 存的值不在枚举里（老工程手改过 JSON、或枚举成员被删除）时回退到第一项，
> **而不是留 `SelectedItem = null`** —— 后者会让下一次保存把无效值静默改写成默认值。

但它**从未被调用过**（`grep ResolveColorDepthItem` 只有定义那一行）：装载走的是裸赋值
`CmbColorDepth.SelectedItem = _project.ScreenDefaultsConfig.DefaultColorDepth;`。

于是实际行为是它想防的那个：存了无效色深时，**下拉显示空白**，而保存仍会经
`NormalizeColorDepth(null)` 写入一个值（`RGB565`）。界面与结果不一致 —— 用户看到"没选"，
实际落盘"选了 RGB565"。

### 判据：先证明"有没有可观测缺陷"，再决定动不动手

`ResolveColorDepthItem` 回退到 `items[0]`，`NormalizeColorDepth(null)` 返回 `RGB565`；
`ColorDepth` 的首成员恰好就是 `RGB565`，所以**两者最终落盘的值相同**。
⇒ 这不是"数值被改错"的缺陷，而是**"界面在撒谎"** + **死代码**。

修法选**把 helper 接上**（而不是删掉）：它的意图是对的（界面必须显示一个具体候选），
接上之后死代码变成活代码、文档声明被兑现，且下拉不再空白。若将来枚举重排，
两条路径会分叉，那时"按候选表选一个"比"按硬编码常量兜底"更自洽。

---

## 3. 第 20 项：口令下限的"字符数 vs UTF-8 字节数"

### 3.1 取证

| 侧 | 判据 | 出处 |
|---|---|---|
| 编辑器 UI | `password.Length`（UTF-16 码元 ≈ 字符数） | `ProjectSettingsView.OnSetRuntimeUserPassword` |
| 模型注释 | 「用户密码最小长度（**字符数**）」 | `SecurityConfig.PasswordMinLength` |
| 固件（最终执行者） | `strlen(password)`（**UTF-8 字节**） | `hmi_actions.c`：`if (strlen(password) < HMI_SEC_PASSWORD_MIN_LEN)` |

⇒ 下限 8 时，"电机启动"4 个字 = **12 字节**：**设备端放行，编辑器报"至少 8 位"**。

### 3.2 往哪边统一：**往字节**（与登记表建议一致），并说明为什么不改设备端

两条路都能"统一"，但代价不对称：

| 方案 | 后果 |
|---|---|
| **编辑器改判字节**（本轮采用） | 无设备改动、无凭据迁移：只是**停止拒绝设备本就接受的口令**。代价：这条下限在**字符**意义上对 CJK 更宽松（一个汉字 3 字节） |
| 固件改判字符（需要 C 侧 UTF-8 码点计数） | 策略在字符意义上不被削弱，但**会让现有凭据直接登不上** —— 已按字节放行的 CJK 口令（如 3 字 = 9 字节 ≥ 8）在固件更新后会被拒，等于把人锁在门外 |

在"弱一点但文档说清楚"与"可能锁住现有用户"之间选前者；这一条同时是登记表 §4 第 20 项自己给出的方向
（*"真要统一应统一到字节数（固件是判据的最终执行者）"*）。

### 3.3 落地（三处一起改，缺一不可）

1. **判据**：`System.Text.Encoding.UTF8.GetByteCount(password) < minLength`。
2. **模型注释**：`（字符数）` → `（**UTF-8 字节数**，不是字符数）`，并写明 CJK 后果与"为什么不改设备端"。
3. **用户可见文案**：`至少 {0} 位` → `至少 {0} 个字节（一个汉字算 3 字节）`（zh-CN / en-US 各 2 条）。
   只改判据不改文案，用户仍会按"位"理解，反而更容易配错。

---

## 4. 覆盖（新增 4 项）

| 用例 | 钉住的性质 |
|---|---|
| `OutOfListLvglVersion_IsPreserved_NotSilentlyUpgraded` | 表外版本：下拉显示它 + 保存原样（不被升成 9.5） |
| `KnownLvglVersion_RoundTripsExactly` | 互补：表内版本照旧原样往返 |
| `InvalidStoredColorDepth_ShowsConcreteSelection_AndSavesValidEnumMember` | 无效色深：下拉**不空白**，落盘是枚举内成员 |
| `PasswordMinimum_IsMeasuredInUtf8Bytes_MatchingDevice` | 12 字节的 4 字中文通过；6 字节的 2 字中文仍被拒（证明判的是字节，不是"含汉字就放行"） |

---

## 5. 负向验证（3 组，全部命中）

| NV | 注入 | 期望红的用例 |
|---|---|---|
| A | `BuildLvglVersionItems` 不再追加表外版本（复现 #18） | `OutOfListLvglVersion_IsPreserved_NotSilentlyUpgraded` |
| B | 色深退回裸赋值（复现 #19：下拉空白仍落盘一个值） | `InvalidStoredColorDepth_ShowsConcreteSelection_AndSavesValidEnumMember` |
| C | 口令长度退回 `password.Length`（复现 #20） | `PasswordMinimum_IsMeasuredInUtf8Bytes_MatchingDevice` |

---

## 6. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2628 通过 / 0 失败**（App.UiTests 182 → **186**；本轮基线 2624 ⇒ **+4**） |
| 负向验证 | **3/3 命中** |
| 本地化 | zh-CN / en-US 各改 2 条（占位符数量不变） |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 7. 范围边界（本轮不做，已登记）

1. **第 20 项的另一半：设备端按字符数判**。若团队要把策略在**字符**意义上收紧，
   需要 C 侧 UTF-8 码点计数 + **现有凭据迁移方案**（否则锁住已用 CJK 口令的现场用户），
   属产品决策而非补缺口。
2. **第 21 项（用户表校验不覆盖口令强度）**：只能看到哈希、看不到长度，判不了 —— 刻意限制，
   不伪装成已校验。
3. 登记表 §4 其余仍开着项按顺序在后续轮次处理（`TimeoutSeconds` 默认值、测试判据绝对余量、
   POSIX 路径耦合、firmware `const` 警告、Dock 真实拖拽、运行时动态文案等）。
