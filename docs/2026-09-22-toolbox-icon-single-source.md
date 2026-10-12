# 工具箱图标 ↔ 图标字典：同一个控件的两套几何（阶段 72，2026-09-22）

> 一句话：**同一个控件类型，工具箱走 `ToolboxView.ControlIcon` 里一张 C# switch 表，
> 主窗口绘图工具栏走 `Assets/Icons.axaml` 的资源键** —— 两套命名空间、两套扫描面，
> 中间没有任何判据。实测 19 个类型在两边同名同义，其中 **4 条当时就已经分叉**
> （两处画得不一样，且编译期零警告、运行期不抛）。

---

## 1 起点：从阶段 71 的「顺带发现」往下挖一层

阶段 71（`docs/2026-09-22-xaml-resource-key-reverse-closure.md`）修掉的是
「**同一份几何**在 `Icons.axaml` 与 `MainWindow.axaml` 各存一份、**逐字相同**」——
那种形状只会在**下一次**改动时才分叉。

它的 §28.7 第 2 条记了一个反向形态：`IconResetTransform` 与 `IconReloadPanels`
**两个字面不同的键指向同一份几何**。顺着「同一份几何还有别的存放方式吗」这条线
往下找，就找到了本阶段：**几何的第二个存放点是 C# 代码里的字符串字面量**，
而且它**当时就已经分叉了**。

## 2 读数

两个口径，别混（问的是不同问题）：

| 口径 | 判法 | 结果 |
|---|---|---|
| **镜像键**（本阶段判据用） | 枚举名 → `IconDraw<T>` / `IconQuick<T>` 是否存在 | **19 有镜像**（`IconQuick` 14 / `IconDraw` 5）+ **7 无镜像**；19 里 **15 逐字相同 + 4 已分叉** |
| 几何串（首轮探针） | 工具箱几何串在字典里存不存在 | 16 逐字相同 / 4 已分叉 / 6 只在工具箱 |

两个口径的差：`Ellipse` 的圆恰好**逐字等于** `IconQuickLed` 的圆 ⇒ 被几何口径
误收进「逐字相同」（几何全等、语义不同）。所以 `16 + 4 ≠ 19` **不是笔误**。

已分叉的 4 条全是 `IconDraw` 族（`IconDraw` 那 5 个里只有 `IconDrawLine` 没分叉）：

| 类型 | 工具箱（修前） | 字典 | 关系 |
|---|---|---|---|
| `Rectangle` | `M5,5 L19,5 L19,19 L5,19 Z` | `M4,4 L20,4 L20,20 L4,20 Z` | 收缩一格 |
| `Ellipse` | `M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 Z` | `M12,4 A8,8 0 1 0 12,20 A8,8 0 1 0 12,4 Z` | 半径 7 vs 8 |
| `Polyline` | `M5,17 L10,9 L14,13 L19,5` | `M4,17 L9,9 L13,13 L20,5` | 收缩一格 |
| `Arc` | `M5,12 A7,7 0 1 0 12,5 M12,5 L17,5` | `M5,13 A7,7 0 1 0 12,6 M12,6 L17,6` | 上移/右移一格 |

反方向（字典侧）：镜像族键共 **21** 个（`IconDraw` 7 + `IconQuick` 14），
其中 **2** 个后缀不是 `HmiControlType` 成员（`IconDrawSelect` / `IconDrawText`，
都是绘图工具栏工具，不是可拖入的画布控件）。

## 3 为什么这比阶段 71 那批更危险

| | 阶段 71（21 条几何双份） | 阶段 72（4 条几何分叉） |
|---|---|---|
| 当下可见？ | 否 —— 逐字相同，**将来**改一处才会分叉 | **是** —— 两处画得不一样，用户能同时看到 |
| 症状 | 未来才会出现 | 已经存在（同一个「矩形」两种大小） |
| 静默？ | 编译期零警告、运行期不抛 | 同 |

「同一个**界面**里同时能看到两者」是关键：主窗口绘图工具栏的「矩形」按钮与
工具箱「基础绘图图形」分类的「矩形」项，在同一个屏幕上。

## 4 修法：4 条几何对齐

`ToolboxView.axaml.cs` 的 4 条几何改成字典侧的写法（`git diff` 逐字为证）：

```
-  HmiControlType.Rectangle  => "M5,5 L19,5 L19,19 L5,19 Z",
-  HmiControlType.Ellipse    => "M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 Z",
-  HmiControlType.Polyline   => "M5,17 L10,9 L14,13 L19,5",
-  HmiControlType.Arc        => "M5,12 A7,7 0 1 0 12,5 M12,5 L17,5",
+  HmiControlType.Rectangle  => "M4,4 L20,4 L20,20 L4,20 Z",
+  HmiControlType.Ellipse    => "M12,4 A8,8 0 1 0 12,20 A8,8 0 1 0 12,4 Z",
+  HmiControlType.Polyline   => "M4,17 L9,9 L13,13 L20,5",
+  HmiControlType.Arc        => "M5,13 A7,7 0 1 0 12,6 M12,6 L17,6",
```

选择「以字典为准」而不是反向：字典是**共享**的那一份（主窗口 65 条键，
工具箱只是 26 条的一个子集），改动面更小、也更符合「单一来源」的方向。
`ControlIcon` 由 `private static` 改 `internal static`，**只为判据可调用**
（`HmiEditor.App.csproj` 早已 `InternalsVisibleTo("HmiEditor.App.UiTests")`），
不是给产品代码用的构造 API —— 这一点已写进当地注释。

## 5 判据（4 条）+ 两张差异表

`tests/HmiEditor.App.UiTests/ToolboxIconDictionaryTests.cs`

| # | 判据 | 守什么 |
|---|---|---|
| 一 | `EveryToolboxIcon_MatchesItsDictionaryIcon` | 19 个镜像键逐个与工具箱几何**逐字一致**；失败消息直接打出两边几何 + `Bounds` |
| 二 | `ToolboxTypesWithoutDictionaryMirror_AreStillTheKnownSet` | 枚举有、字典没有的那 7 个 —— **双向**：有人补了镜像键却不接工具箱 ⇒ 红 |
| 三 | `MirrorKeyDerivation_IsUnambiguous` | 同一类型不许同时有 `IconDraw<T>` 与 `IconQuick<T>`（推导会静默取前者） |
| 四 | `DictionaryMirrorKeys_WithoutAControlType_AreStillTheKnownSet` | **反方向**：字典镜像族键的后缀必须是枚举成员，否则红（`IconDrawFoo` 这种名字会让人以为存在对应控件） |

判据一的三条**面自检**，顺序＝「更上游的输入面」在前：
字典几何条目 ≥60（实测 65）→ `textMissing` 为空 → 镜像数 ≥19。
这个顺序不是排版洁癖：负控 case 7 把抽取正则写坏时，三条都会命 ——
**先命中的必须是最上游那条**，否则下游断言永远见不到光（那就是死断言）。

两张差异表都不是豁免表，每条必须写**为什么**：

* `NoDictionaryMirror`（7 条）：`Screen` 是「容器对象」vs `IconNewScreen` 是「新建动作」，
  语义不同、刻意不共用；其余 6 个（`Spinner`/`TabView`/`Svg`/`Tank`/`Pipe`/`Valve`）只在工具箱出现。
* `MirrorKeyWithoutControlType`（2 条）：`IconDrawSelect` / `IconDrawText` 是绘图工具栏工具。

## 6 机制：按**命名约定**推导，不写硬表

镜像键一律是 `IconDraw<枚举名>` / `IconQuick<枚举名>`。表会过期、约定不会。
代价写在差异表里：**名字对不上时推导就看不见**，所以差异表必须显式列出来
（判据四就是这个方向的另一半）。

## 7 🩸 两个坑（都留在了类注释里）

### 7.1 `resources[key]` 索引器不查 `MergedDictionaries`

首版用 `TryGetResource` 判命中、回头却用索引器取对象。两者**不一个口径**：
`Icons.axaml` 是靠 `ResourceInclude` **合并**进来的，`TryGetResource` 会查合并字典
（⇒ 命中），而索引器**不查**（⇒ 返回 `null`）⇒ 对 `null` 取成员直接
`NullReferenceException`。首跑 3 条判据一起红、栈都指向同一行。

**规矩：只认 `TryGetResource` 的 `out` 值。**

### 7.2 🩸 `StreamGeometry.ToString()` 返回的是**类型名**，不是路径数据

首版拿 `fromToolbox.ToString()` 与 `fromDict.ToString()` 比较当作「几何文本」——
**那一维是恒真的假绿**：两边都是字符串 `"Avalonia.Media.StreamGeometry"`。
是**读数**暴露的：19 行对照表的最后一列全是同一个类型名。

这解释了阶段 71 那条判据为什么只能用 `Path.Data` 的**引用相等**：
Avalonia **没有**公开的「几何 → 路径文本」API（实测 `PathGeometry.CreateFromGeometry`
与 `Geometry.GetFlattenedGeometry` 在 `Avalonia.Base` 里**都不存在**），
而内容相等在这里根本比不了。

**修法**：字典侧的几何文本改从 `Assets/Icons.axaml` 的**源文件**读（正则抽
`<StreamGeometry x:Key="K">几何</StreamGeometry>`，实测 65 条 —— 这是本判据**唯一**
偏离「只取运行期」的地方，理由写在文档里、也写在类注释里）。两个维度：

| 维 | 内容 | 证明什么 |
|---|---|---|
| **文本维**（主） | `ControlIcon(type)` vs 字典里**写的**字符串，`Ordinal` 逐字 | 缺陷的定义就是「两处**写法**不一致」 |
| **运行期维**（辅） | `StreamGeometry.Parse(ControlIcon(type)).Bounds` vs 资源对象的 `Bounds` | 这个键在运行期**真的有值、且是 Geometry**（走 `TryGetResource`） |

运行期维**只到 `Bounds`、不是内容相等** —— 这一点必须说清楚，别让下一个人以为
「两维都过了 = 逐点相同」。

## 8 端到端负控 9/9

`.workbuddy/tmp/neg72.py`（注入产品代码 / 字典 / **判据自己的正则**），
判红谓词匹配测试**自己的汇总行**（`失败: N`，N>0）**且**取到 `错误消息:`。

| # | 注入 | 期望命中 | 实测 |
|---|---|---|---|
| 1 | 工具箱 `Rectangle` 改回收缩版 | 判据一（`Bounds` 维） | 红 ✓（失败 1） |
| 2 | 字典补 `IconQuickTank` | 判据二差异表 stale 分支 | 红 ✓ |
| 3 | 字典删 `IconQuickButton` | 判据一面自检 | 红 ✓（失败 2） |
| 4 | 改 `IconDrawRectangle` 几何 | 判据一 | 红 ✓ |
| 5 | 字典补 `IconDrawButton` | 判据三（歧义） | 红 ✓ |
| 6 | ★ 工具箱 `Rectangle` 换成**同矩形不同起点/绕向** | 判据一（**文本维**） | 红 ✓ |
| 7 | 把判据自己的抽取正则写坏 | 判据一（面自检，最上游那条） | 红 ✓ |
| 8 | 字典 `IconDrawSelect` → `IconDrawZzz` | 判据四 undocumented 分支 | 红 ✓ |
| 9 | 差异表塞一个**后缀已对上**枚举成员的键 | 判据四 stale 分支 | 红 ✓ |

**case 6 是本轮分量最重的一条**：注入后两处几何的 `Bounds` **都是 `4, 4, 16, 16`**
（完全相同），只有写法不同。首版（`ToString()` 那一维）会**放过**它；
现在它判红，失败消息里两行 `Bounds` 一字不差 —— 这才证明文本维是活的，
而不是又一个假绿。

还原一律**写回备份原文**（**禁 `shutil.copy2`**：它连 mtime 一起复制 ⇒ MSBuild 判定
无需重建 ⇒ 正控跑的是负控的二进制），收尾后三个文件与备份**逐字节 `cmp` 一致**、
注入痕迹 grep **0**。

## 9 诚实边界

1. **字典侧几何的文本取自源文件**，不是运行期对象（Avalonia 无「几何 → 文本」API）。
   运行期那一维只到 `Bounds`。⇒ 若 `Icons.axaml` 的几何文本被别处**覆盖**成不同值，
   文本维会绿而运行期维才可能红 —— 这类覆盖由阶段 71 的 `XamlResourceKeyClosureTests`
   （定义侧重复）兜，不由本判据兜。
2. **只覆盖「按命名约定对得上」的那 19 个**。`ControlIcon` 的 `_ =>` 兜底返回一个矩形
   ⇒ 枚举新增成员却漏了 `switch` case **不会让工具箱自己报错**，只会由判据一（有镜像键时）
   或判据二（无镜像键时）间接兜住。
3. **判据四只保证「有人改动时会红」**，不保证 `IconDrawSelect` / `IconDrawText`
   这两个键**是活的**（它们是否真被引用由阶段 71 的定义侧反向判据管）。
4. **像素级比对仍未做**（沿用阶段 71 边界 #4）—— 但范围缩小了：这 19 条**文本相同**
   ⇒ 走同一个 `StreamGeometry.Parse` ⇒ 渲染必然相同；阶段 71 那 21 条 `Path`
   仍只有「引用相等 + `Stroke`/`Stretch` 非空」。

## 10 验证

| 项 | 结果 |
|---|---|
| 构建 | **0 警告 0 错误** |
| `ToolboxIconDictionaryTests` | **4/4 通过**；读数：`types=26 withMirror=19 withoutMirror=7 ambiguous=0 字典几何条目=65 textMissing=0`、镜像族键 21 / 无对应类型 2 |
| 端到端负控 | **9/9 判红**并命中期望分支；还原逐字节一致、注入痕迹 0 |
| 门禁（提交前 / 提交后） | 见 `docs/project-status.md` 阶段 72 |

专题相关：`docs/2026-09-22-xaml-resource-key-reverse-closure.md`（阶段 71，定义侧反向闭合）。
