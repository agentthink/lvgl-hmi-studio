# 宿主对话框内部按钮的行为判据（阶段 50）

> 登记表 **#39** 的「事件接线一列仍未覆盖」那一半。提交 `ddeb131`。
> **产品代码改动 0 行** —— 判据建起来就全绿。

---

## 0. 一句话

阶段 49 把主窗口的 62 个菜单项点了一遍，但那只到「打开一个对话框」为止；
**对话框打开之后**里面几十个按钮的接线仍然没人点过。这一层把
「宿主 → 宿主内部按钮 → 二级弹窗」也变成可判：**6 个宿主 / 57 个具名按钮全部分类 / 46 个真点**，
断言宿主自身 `Window.OwnedWindows` 的**增量与类型**。

---

## 1. 起因：两层网之间正好漏了一段

| 层 | 覆盖 | 缺口 |
|---|---|---|
| 阶段 48 窗口冒烟网 | 32 个窗口「打得开 / 布局得出来」 | 用的是**设计器构造路径**（无参构造），**不接 Click** |
| 阶段 49 菜单点击判据 | 点菜单 → 弹窗的**增量与类型** | 只到「宿主被弹出来」为止 |
| **本层（阶段 50）** | 宿主**内部**按钮点了会怎样 | 见 §6 |

`DeviceManagerView` 的教训说明这不是纸面问题：它的构造函数曾在 `InitializeComponent()`
期间抛 NRE ⇒ 「设备管理」菜单点了没反应。**「接线在源码里看着存在」与「点下去真的会发生什么」
是两件事** —— 而阶段 48/49 两层网都不覆盖后者。

---

## 2. 判据形状

```
主窗口菜单 RaiseEvent(MenuItem.ClickEvent)
  → 宿主出现在 window.OwnedWindows 里（阶段 49 已验证：同步、不阻塞）
  → 枚举宿主内的具名 Button（按名字去重 + 断言同名必同实例）
  → 按**表里声明的顺序**逐个 RaiseEvent(Button.ClickEvent)
  → 看宿主自身 OwnedWindows 的增量与类型
```

**为什么 owner 下沉一层就够了**：`dialog.ShowDialog(this)` 里的 `this` 是宿主视图；
走 `AppDialogs.*Async(this, …)` 的也一样，因为 `TopLevel.GetTopLevel(this)` 解析出宿主。
所以二级弹窗会精确地落在**宿主**的 `OwnedWindows` 里 —— 与阶段 49 用主窗口是同一手法。

**面守卫走双向差集**（表里有宿主没有 ⇒ 控件被删/改名；宿主有表里没有 ⇒ 新增按钮没人分类），
与阶段 49 的菜单表、阶段 48 的全集守卫同形。

---

## 3. 两处形状是**实测**出来的，不是设计出来的

### 3.1 点击顺序是被测语义，不是排版

首跑按**字母序**点，`AnimationEditorView` 那条红了：

```
AnimationEditorView.BtnDeleteAnimation：预期弹出 1 个 ConfirmDeleteDialog，实际增量 0
```

对照探针（声明序）里它是 `+1`。差别只有一个：**探针先点了 `BtnNewAnimation`**。
查源码确认：

```csharp
private void OnDeleteAnimation()
{
    if (_currentAnimation == null) return;   // ← 首行
```

⇒ 「删除动画」是否弹确认框**取决于之前点过什么**。修法不是把断言放宽成「弹或不弹都行」
（那会把这条唯一的覆盖丢掉），而是**把顺序钉进表里**：`BtnNewAnimation` 排在
`BtnDeleteAnimation` 之前，后者就成了一条精确断言「必须弹 1 个 `ConfirmDeleteDialog`」。

> 负控 **N4** 就是交换这两行 ⇒ 判据必须红。**数组顺序 = 被测语义**，别当排版调整。

### 3.2 `StyleEditorView` 只有 1 个具名控件，而且它是「关窗」

探针读数：`具名按钮 1 个 · BtnTitleClose（跳过）`。查 XAML 印证 —— 这个视图只有
`BtnTitleClose` 有 `x:Name`，**其余内容全部在 code-behind 里构建**。

于是它整张面都在跳过表里 ⇒ `clicked == 0`。两种错误处置是**把它悄悄移出清单**（少了一个宿主，
没人知道）或**把下界放宽**（判据恒真）。正确处置是**登记 + 写明理由**：

```csharp
private static readonly Dictionary<string, string> NoClickableHosts = new(StringComparer.Ordinal)
{
    ["StyleEditorView"] = "唯一具名控件是 BtnTitleClose（关宿主）；这个视图的内容全部在 code-behind
                           里构建、没有任何 x:Name ⇒ 这一层对它只能核对「面」，做不了行为判据",
};
```

并且**该宿主的按钮必须全部是 `Never`**，否则两处不一致会被表卫生判据抓到。

---

## 4. 三个桶

| 桶 | 数 | 断言 | 理由门槛 |
|---|---|---|---|
| `Popup` | **3** | 增量**恰好 1**，类型名精确相符 | 不需要（行为已被精确断言） |
| `Silent` | **43** | 增量**恰好 0**（针对本判据的初始状态 + 本表顺序） | 不需要 |
| `Never` | **11** | 不点 | **必填 ≥10 字符** |

按宿主：

| 宿主 | 菜单 | 具名按钮 | 真点 | 弹窗 |
|---|---|---|---|---|
| `RecipeEditorView` | `MenuRecipeEditor` | 13 | 12 | `BtnAddGroup` → `TextInputDialog` |
| `UserManagerView` | `MenuUserManager` | 9 | 8 | — |
| `StyleEditorView` | `MenuStyleEditor` | 1 | **0** | — |
| `ScriptEditorView` | `MenuScriptEditor` | 13 | 11 | `BtnAiGenerate` → `AiScriptDialog` |
| `DeviceManagerView` | `MenuDeviceManager` | 15 | 10 | — |
| `AnimationEditorView` | `MenuAnimationEditor` | 6 | 5 | `BtnDeleteAnimation` → `ConfirmDeleteDialog` |
| **合计** | | **57** | **46** | **3** |

`Silent` 的语义要说准：它**不是**「这个按钮永远不弹窗」，而是**「在本判据的初始状态 +
本表的点击顺序下，增量必须恰好为 0」**。状态一变就会红，届时有人来看是不是该改桶 ——
这正是它的价值。典型例子：`BtnResetPassword` 在无选中用户时 `return`，选中后会弹新口令输入框；
本判据**造不出那个状态**，所以只覆盖到「不弹」。

> **为什么不单列「条件桶」**：本层每个宿主都在**固定初始状态 + 固定点击顺序**下点击，
> 唯一沾条件的就是 `BtnDeleteAnimation`，而它已经被 §3.1 的顺序钉死成 `Popup`。
> 加一个没有人用的桶只会多一条恒真的守卫。

`Never` 11 个分三类，每一类都要写得出理由：

| 类 | 例 | 理由 |
|---|---|---|
| 关宿主 | `BtnTitleClose`（6 个宿主各一） | 点了会关掉宿主，后续按钮无从验证 |
| 不可逆外部动作 | `BtnUpdateHmi` / `BtnFlashFw` / `BtnFelFlash` | 向设备推文件 / 烧录固件 |
| 写盘 | `BtnGenerate` | 生成整套代码到输出目录（目录还能被 `BtnBrowseOutput` 改掉） |
| **平台挂死** | `BtnHelp` | 见 §4.1 |

### 4.1 `BtnHelp` 为什么进 `Never`：headless 下会挂死

点它会 `ShowDialog` 一个「Lua 帮助」窗 ⇒ **整个测试进程不再前进**。
`timeout` 自己触发（**`EXIT=124`** ⇒ 是 hang，不是 OOM 的 137）。阶梯剥离（每步单独实测）：

| 步 | 内容 | 结果 |
|---|---|---|
| S1 | 裸 `TextBlock` | ✅ 过 |
| S2 | ＋`TextWrapping.Wrap` | ✅ 过 |
| S3 | ＋`LineHeight` | ✅ 过 |
| S4 | **`ScrollViewer` 包住它** | ❌ **挂在 `ShowDialog` 内** |

排除了三条常见嫌疑：**不是**文本长度（正文 575 字符）、**不是**字体回退链
（`Consolas, Courier New, monospace`）、**不是** owner 类型（换成真实 `ChromeWindow`
（`DeviceManagerView`）当 owner **同样挂**）。

而阶段 49 打开过的对话框里 `ScrollViewer` 其实很常见（`SettingsDialog` 14 处、
`ProjectSettingsView` 12 处、`CommunicationConfigView` 8 处、`RecipeEditorView` 5 处……**全都没挂**）
⇒ **不是「`ScrollViewer` 一律挂」**。触发条件未定，已连同嫌疑机制登记为登记表 **#40**。

---

## 5. 负控 10/10

每条判据至少一条独立注入；`--apply` 才落盘，还原一律写回**字节快照**，
**注入后编译失败判为「负控不成立」**，`assert count == 1`。

| 注入 | 打的是哪条断言 | 期望 | 实测 |
|---|---|---|---|
| N1 摘掉 `BtnAddGroup` 的接线 | `Popup` 增量必须为 1 | 红，**面守卫仍绿** | ✅ 红 1 条；面守卫绿 |
| N2 把 `BtnAddGroup` 接到「弹一个裸 `Window`」 | 类型必须相符 | 红并点名实际类型 | ✅ 红 1 条 |
| N3 让 `BtnPlayPreview` 弹个窗 | `Silent` 增量必须为 0 | 红 | ✅ 红 1 条 |
| N4 **交换** `BtnNewAnimation` / `BtnDeleteAnimation` 顺序 | §3.1 的顺序语义 | 红 | ✅ 红 1 条 |
| N5 分类表里把一个按钮改名 | 面差集 missing 方向 | 红 | ✅ 红 1 条（**missing + extra 同时点名**） |
| N10 产品 XAML 新增一个具名按钮 | 面差集 extra 方向 | 红 | ✅ 红 1 条 |
| N6 **正控**：`clicked < 1` 下界抬到 99 | 恒真守卫 | 红 | ✅ 红 **5** 条（除 `StyleEditorView` 外全部宿主） |
| N7 **正控**：`NoClickableHosts` 理由改到 10 字符以下 | 理由门槛 | 红 | ✅ 红 2 条 |
| N9 `Never` 桶里 `BtnGenerate` 名字改错 | 防表腐烂（名字必须在某个 `.axaml` 里出现） | 红 | ✅ 红 2 条 |
| N8 只改一行注释 | — | **7/7 全绿** | ✅ 全绿 |

还原后 4 个文件 `sha256` **逐字节一致**。

**N1 与 N2 是最有价值的两条**（与阶段 49 的 N2/N3 同构）：
N1 摘掉接线后按钮**还在逻辑树上** ⇒ 面守卫完全看不见，只有真点才抓得到，证明两层互不替代；
N2 就是「接了**正确的**处理器」那条判据 —— 期望类型不符即红，**不需要任何源码解析**。

### 5.1 脚本自身两处缺陷（判据本身是对的）

| # | 现象 | 根因 | 修 |
|---|---|---|---|
| N5 首跑 | 「注入后编译失败 ⇒ 不成立」 | **注入设计错**：改 `.axaml` 的 `x:Name` 会打断 code-behind 对生成字段的引用 | 改成①表侧改名（missing+extra 双向）②产品侧**新增**一个具名按钮（多出的字段没人引用，编译合法） |
| N7 首跑 | 红了但没命中 needle | **needle 凭记忆写的**：实际消息是 `的「没有可点按钮」理由太短`，中间隔着字 | needle 改成 `理由太短` |

> **同一个教训第三次出现**（阶段 49 已经踩过两次）：**needle 必须照实际输出写，
> 不能凭记忆**。本轮顺手给脚本加了「逐条保存注入后的完整输出」到 `.workbuddy/tmp/nc-inner/<ID>.log`，
> 下次不用靠猜。**不看输出直接采纳「7/9」会把两个脚本缺陷当成两个判据缺陷。**

---

## 6. 不抓什么（写进类注释，避免重犯「高估覆盖」）

1. **「不弹窗」是静默的** —— 一个没接线的按钮与一个接了线但什么都不做的按钮，在这一层长得一样
   （接线本身要靠负控 N1/N2 证明）。
2. **动作型按钮的副作用不判** —— 烧录 / 生成 / 起进程只判「不抛异常 + 弹窗层数」。
3. **需要先选中某项、而本判据造不出那个状态的二级弹窗**（`BtnResetPassword` 之类）当前是 no-op
   ⇒ 只被「增量恰为 0」覆盖到。
4. **不做真实命中测试**（不做像素点击）⇒ 按钮可见性 / 是否被禁用不在面内。
5. **不判关窗后资源释放是否干净**（与登记表 #37 遗留② 同族）。

---

## 7. 读数

| 指标 | 值 |
|---|---|
| 新增用例 | **7**（6 条 `Theory` 各一个宿主 + 1 条 `Fact` 表卫生），整条 **2 秒** |
| 用例总数 | `App.UiTests` **311 → 318**；八程序集合计 **3004 → 3011**（净增 **7**，纯净增） |
| 面 | **57** 个具名按钮 / **46** 个真点 / 3 个弹窗 / 43 静默 / 11 不点 |
| 负控 | **10/10**（含 2 条正控、1 条不误报），还原 4 个文件 `sha256` 逐字节一致 |
| 构建 | **0 警告 / 0 错误**（门禁第 [3] 步就是 Release + 警告即失败） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**，日志 `.workbuddy/tmp/ci-gate-2026-09-20-dialoginner.log` mtime **`23:50:28`** ⇒ 晚于提交 `ddeb131`（**`23:49:06`**）**82 秒** |
| 产品修复 | **0 行** —— 判据建起来就全绿 |
| 提交 | `ddeb131`（2026-09-20 23:49:06） |

---

## 8. 仍未闭环

| # | 项 | 说明 |
|---|---|---|
| 1 | **阶段 49 遗留①（工具栏 / 面板内的 `Btn*`）仍未覆盖** | 那是**主窗口里停靠的面板**（`LayerPanelView` / `FaceplateLibraryView` / `PropertyPanelView`…），与本层的「宿主对话框内部」是**两个不同的面**。本层收窄了 #39 的「事件接线」缺口，**不等于**收掉了那一项 |
| 2 | 其余 26 个窗口的事件接线 | 本层只覆盖「能从菜单用生产路径打开、且内部有具名按钮」的 6 个宿主 |
| 3 | **造状态能力缺失** | 选中一行 / 填一个输入框再确认 ⇒ 现在只能覆盖到「不弹」，覆盖不到「弹什么」 |
| 4 | 登记表 **#40**（`ScrollViewer` 挂死） | 触发条件未定、真机未验、处置方向需产品决策 |
| 5 | 4 个 `Silent` 依赖 StorageProvider 桩的行为 | `BtnImportCsv` / `BtnExportCsv` / 设备管理器 4 个 `BtnBrowse*` 在 headless 下都因「选择器返回空」提前 return ⇒ 只覆盖了「不弹」，没覆盖「选了文件之后做什么」 |
