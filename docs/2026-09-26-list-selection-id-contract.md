# 阶段 108 —— 修登记 #64：列表选中按 `Id` 比（`IIdentifiedModel` 契约）

> 2026-09-26。范围：**产品代码**修一个真缺陷 + 把判据侧的表按**实测**改到与修复一致。
> 起因：阶段 107 新建的「按钮可观测效果」判据（第五层动作面）在**首跑里当场点出**了它
> —— 判据自己抓出来的产品缺陷，不是人肉 review 出来的。
> 判据 ⇒ `tests/HmiEditor.App.UiTests/HostButtonEffectUiTests.cs`；读数 ⇒ `project-status.md` §108。

## 108.1 缺陷：一句话与它为什么静默

`VariableManagerView.SelectListItemByTag(ListBox, string?)` 的判据是

```csharp
if (item is ListBoxItem lbi && lbi.Tag?.ToString() == tag)   // ✗ 缺陷形状
```

而 `CreateTaggedListItem` 把**模型对象**整个塞进 `Tag`
（`Variable` / `VariableGroup` / `VariableAlarmConfig` / `UdtDefinition`
**都没有覆写 `ToString()`**）⇒ `ToString()` 恒定返回**类型全名**、与 `Id` **永不相等**
⇒ **6 处调用全部是空操作**（`VariableManagerView.axaml.cs:374,422,482,495,545,569`）。

后果：`AddVariable` 里那句注释「自动选中新添加的变量，提升反馈感」**声明的行为不存在**
—— 新建变量之后 `_selectedVariable` 仍是 `null`，紧跟着点「添加报警」只提示「请先选择变量」
（阶段 107 首跑实测：点了 `BtnAddVariable` 再点 `BtnAddAlarm`，报警列表 **0 → 0**）。

⚠ **不是全仓**：`SelectComboBoxByTag` 那一族是对的（它的 `Tag` 是**字符串**，`Int16` 之类）。
坏的只是**列表**这一支 —— 因为列表的 `Tag` 装的是**对象**。

## 108.2 口径：三条路，只选一条（登记原文写的就是「先要定口径」）

| 路 | 做法 | 判 |
|---|---|---|
| **①（选）** | 改 **helper 的比对一端**：`Tag` 是模型 ⇒ 按 **`Id`** 比；给模型加**显式契约接口** | ✅ 无类型分支、无静默回落 |
| ② | 改 6 个调用点：传对象、按**引用同一性**比 | ❌ `RefreshGroupList` 给「未分组」**每次刷新都新建**一个 `VariableGroup { Id = "" }` ⇒ **新实例 ≠ 旧实例**，按同一性反而漏（而且这个「未分组」根本就是靠 Id 认的） |
| ③ | 按类型 `switch` / 反射反推 `Id` | ❌ 新增一种列表 Tag 类型时**静默退回 `ToString()`** —— 正好复现本缺陷的形状。这是「**分派依据不能是已有的那一个枚举**」的又一例 |

选 ①。为了让 ① 不变成「另一种口头约定」，把「有 `Id`」升格为**类型契约**：

```csharp
// src/HmiEditor.Core/Models/IIdentifiedModel.cs（新）
public interface IIdentifiedModel { string Id { get; } }
```

`Variable` / `VariableGroup` / `VariableAlarmConfig` / `UdtDefinition` 各加一行 `: IIdentifiedModel`。

## 108.3 改动（7 files，＋90 / −27）

| 文件 | 改了什么 |
|---|---|
| `src/HmiEditor.Core/Models/IIdentifiedModel.cs`（新，23 行） | 契约接口；注释里写明「别退化成 `switch` / 反射」 |
| `Variable.cs` / `VariableGroup.cs` / `Alarm/VariableAlarmConfig.cs` / `VariableUdt.cs` | 各 **1 行** |
| `src/HmiEditor.App/Views/VariableManagerView.axaml.cs`（＋7 / −2） | 判据改 `item is ListBoxItem { Tag: IIdentifiedModel model } && model.Id == tag`；`<remarks>` 记下两条**别这么改**（退回 `ToString()` / 改成引用同一性） |
| `tests/…/HostButtonEffectUiTests.cs`（＋56 / −21） | 表按实测改（4 条 `Inert`→`Moves`，3 条 `Why` 更正），新加 3 个「选中项」分量 |

⚠ `SelectComboBoxByTag` 与 `CreateTaggedListItem` **一行没动**。

## 108.4 判据侧：先跑、让它点名，再改表（不猜）

改完产品代码后**先跑判据**，它当场点名了要改哪几条（这就是「判据自带信息」的用处）：

```text
VariableManagerView.BtnAddAlarm：**未声明**的分量「报警列表」从 0 变成了 1 ……
VariableManagerView.BtnDeleteAlarm：**未声明**的分量「报警列表」从 1 变成了 0 ……
VariableManagerView.BtnDuplicateVariable：**未声明**的分量「变量」从 3 变成了 4 ……
VariableManagerView.BtnDeleteVariable：**未声明**的分量「变量」从 4 变成了 3 ……
（第二次跑又点名：BtnDeleteUdt：**未声明**的分量「UDT选中」从 1 变成了 0）
```

「修好就红」在设计时（阶段 107）就写进了那几条 `Inert` 的 `Why` 里 ⇒ 这是一次**按预案**的转档。

| 按钮 | 阶段 107 | 阶段 108 |
|---|---|---|
| `BtnAddAlarm` / `BtnDeleteAlarm` / `BtnDuplicateVariable` / `BtnDeleteVariable` | `Inert` | **`Moves`** |
| `BtnSaveVar` / `BtnSaveAlarm` / `ChkAlarmEnabled` | `Inert`（`Why` 归因 #64） | `Inert`（**`Why` 更正**） |

### 新增 3 个「选中项」分量，把这次修复**直接**钉住

```csharp
private static int SelectedTagIs<T>(Window window, string listName)
{
    var list = Control<ListBox>(window, listName);
    return list.SelectedItem is ListBoxItem item && item.Tag is T ? 1 : 0;
}
```

`变量选中` / `报警选中` / `UDT选中`，并在 `BtnAddVariable` / `BtnAddAlarm` / `BtnAddUdt` 上声明为 `1`。
为什么非要它不可：**`UdtList` 那条选择路径原先零覆盖** —— `BtnAddUdt` 是**直接**给
`_selectedUdt` 赋值的（`_selectedUdt = def; RefreshUdtList();`），绕过了列表选中
⇒ 4 条转 `Moves` 的行**盯不到**它。有了 `UDT选中` 才盯得住。

## 108.5 顺带更正一：阶段 107 的「`Inert` 合计 14」是**手抄错的**

逐宿主是 **14 + 3 + 0 = 17**；阶段 107 把 `VariableManagerView` **那一行**的数当成了总和
（`Moves 5 / Inert 14 / Never 1` 里的 14）。错的数字传到了 **4 处**
（状态表 §107.3 / §107.5、本专题族的 107.5、登记 #39），本批一并改成 **17**。

🩸 **教训**：分档数字**只许从判据的输出行抄，不许手算**。判据其实一直在印：

```text
=== 宿主按钮效果/表 ===
宿主 3 个；按钮 31 条（Moves 15 / Inert 13 / Never 3）
```

⚠ **但这行只在失败时才落进日志** —— `ITestOutputHelper` 的输出，绿跑默认不印。要抄读数得显式加
`--logger "console;verbosity=detailed"`。本批的 15/13/3 就是这么取的。
这与「**归位行的数字必上判据**」（#61w）是同一族问题：**手抄的数字没有判据盯着**。

## 108.6 顺带更正二：`Inert` 的 `Why` 是**免证的**

阶段 107 有 **3 条**（`BtnSaveVar` / `BtnSaveAlarm` / `ChkAlarmEnabled`）把成因**错记成** #64 ——
它们的空操作其实是**点击顺序**造成的：

- `BtnSaveVar`：上一步 `DeleteVariable` 把 `_selectedVariable` **显式置空**了（`axaml.cs:509`）；
- `BtnSaveAlarm`：第 4 步 `DeleteSelectedAlarm` 把 `_selectedAlarm` 置空（`axaml.cs:439`）；
- `ChkAlarmEnabled`：**根本没有接线**（XAML 里既无 `Click=` 也无 `IsCheckedChanged=`）。

为什么错了一年（一天）都没响？**因为没有任何判据会去打 `Inert` 的 `Why`** ——
表卫生只校验「理由长度 ≥ 10 字符」。

🩸 **所以**：`Moves` 的 `Why` 会被真值校验间接逼着写对，`Inert` 的 `Why` **不会**
⇒ 写 `Inert` 的理由时必须**真的把处理器读一遍**，别顺手归因到最近的一个登记号上。

## 108.7 负控 3/3（判红 ＋ 点名，缺一不可）

| # | 注入（都能编译） | 期望子串 | 实测 |
|---|---|---|---|
| NC1 | 比对**退回缺陷形状**（`Tag.ToString()` 比 `Id`） | `分量「报警列表」期望 1、实际 0` | **红 ✓** |
| NC2 | 摘掉契约（`Variable` 不再实现 `IIdentifiedModel`） | `分量「变量选中」期望 1、实际 0` | **红 ✓** |
| NC3 | 把声明值**写反**（「变量选中」1 → 0） | `分量「变量选中」期望 0、实际 1` | **红 ✓** |

NC1 是这段最要紧的一条：**证明 #64 再回归会被抓住**（阶段 107 时这一族对它**全绿**）。
NC2 证明新加的「选中项」分量**是承重的**（盯的是直接行为，不是下游后果的影子）。
NC3 证明断言的**第二向**（不只抓「该动没动」，也抓「声明值写反」）。

逐支一个日志（`s108-nc1..3.log`）、逐支**写回原文**并断言 SHA256 与注入前逐字节相同
（**不用** `shutil.copy2`：连 mtime 一起复制 ⇒ MSBuild 判定无需重建 ⇒ 实际跑的是负控那次的二进制）。

## 108.8 门禁

| 时刻（birth / mtime） | 日志 | 批次 / 提交 | 结果 |
|---|---|---|---|
| 16:49:46 | — | 内容批（产品代码 ＋ 表）`0fa8855` | 7 files，**＋90 / −27** |
| **16:49:49 / 16:50:56** | `s108-gate1.log` | 门禁（7 步） | `GATE_EXIT=0` / **7 步全绿**、0 警告 0 错误；44 / 44 / 75 / 87 / 4 / 315 / **2122** / **584** ＋ 8 ＋ 2；路径卫生 **668** / 407 个 `.cs` |

提交 `0fa8855` 时刻 **16:49:46** ⇒ 门禁 birth **16:49:49.587**（**晚 3.6 秒**，清晰）。
⚠ 路径卫生 667 → **668** 文件、406 → **407** 个 `.cs` 都能归因到本批新增的 `IIdentifiedModel.cs`。

## 108.9 诚实边界 / 未闭环

1. 契约只挂**这 4 个**模型（`VariableManagerView` 列表用到的全部 Tag 类型）。
   `UdtField` 没有 `Id`（它的键是 `Name`）、且没被 `SelectListItemByTag` 用过 ⇒ 本次不挂。
2. `BtnSaveVar` / `BtnSaveAlarm` 声明 `Inert` 的理由是「**未被观测**」而**不是**「没动作」：
   本层分量里没有「报警启用位 / 需确认位」⇒ 即使它们跑到了也读不出差别。
3. 表里其余 `Inert` 的 `Why` 本批**顺带读过一遍**（§108.6 的应对），但复核是**人工的、不是判据**
   ⇒ 它们各自仍然**免证**。
4. **#64 闭环，但软件侧仍未清空**：**#39**（残余 146 个具名按钮不在动作面覆盖面里）、
   **#45**（Dock 面板与大队同进程僵死、根因未定）仍开着。
