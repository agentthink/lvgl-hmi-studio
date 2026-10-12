# 控件**类型**显示名的面收口 —— 26 个枚举名不再漏进界面（阶段 160，补 `#61o` 的剩余面）

> **一句话**：`HmiControlType` 的**显示名**早在阶段 96（登记 #61o）就收敛成唯一来源
> `ControlTypeTextKeys`，但当时只**枚举到三个露出点**（工具箱条目 / 画布默认名 / 交叉引用类型列）；
> **其余露出点**一直在直显英文枚举名（`Button` / `TextArea`），中文界面下也是英文。
> 本阶段把剩下的 5 处补上，并把这条面改成**逐文件计数**的守卫。

---

## 1. 补上的 5 处

| 位置 | 改动前显示 | 改动后（zh） |
|---|---|---|
| 属性面板标题行（已命名 / 未命名两档） | `按钮1 (Button)` | `按钮1 (按钮)` |
| 工程树控件节点 | `按钮1 (Button)` | `按钮1 (按钮)` |
| 动画「目标控件」下拉 | `[主画面] 按钮1 (Button)` | `[主画面] 按钮1 (按钮)` |
| 画布 `Text` 为空时的回落标签（`DesignCanvas.cs` ＋ `Renderers.cs` 两处） | `Button` | `按钮` |
| 面单编辑器「控件摘要」的类型列表 | `类型: Button, Label` | `类型: 按钮, 标签` |

⚠ 交叉引用那一列（`ReferenceRow.ControlType`）阶段 96 就已经走单一来源了，本阶段只是把它一起
纳入计数守卫。

---

## 2. 🩸 同时补上**阶段 159 漏掉的一处**（多选/单选状态栏）

`MainWindow` 的两条状态栏用的是 `first.Name` / `first.Type`：

```
已选 {0} 个控件 | 首项: {1} ({2})        // 多选
已选中: {1} ({2}) — X:… Y:… W:… H:…      // 单选
```

⇒ 中文界面下显示的是 **`Button1 (Button)`** —— 名字用了**落盘的中性名**（阶段 159 之后不再是
界面名），类型是**英文枚举名**。改成 `ControlNameDefaults.Display(first)` ＋
`ControlTypeTextKeys.Describe(first.Type)` 后是 `按钮1 (按钮)`。

**为什么它没在 159 被抓住**：那一版的显示面守卫只查「文件里**有没有**一次正确调用」——
`MainWindow.axaml.cs` 里已经有一条正确的（撤销描述），于是**同一个文件里的另外两处**照样绿灯。
⇒ 本阶段把两个显示面守卫都改成**逐文件计数**：挪动/新增/删除任何一处都要**重数**这张表，
否则当场红（与 `OutsideFaceCensus` 同规矩）。负控 **nc2** 就是"把这两处改回去"⇒ 计数守卫红。

---

## 3. 判据（`ControlTypeDisplayTests`，4 条；另 1 条并在 `ControlDefaultNameTests`）

| # | 判据 | 判的是哪一跳 |
|---|---|---|
| J1 | `Describe_IsLocalizedForEveryType_AndNeverLeaksTheEnumNameInChinese` | 26 个类型：两本字典**两两不同**（`zh != en` 全部成立，实测同名 0 个）且**中文名不等于英文枚举名** |
| J2 | `TypeDisplaySurface_RoutesThroughControlTypeTextKeys` | 类型显示面**逐文件计数**（10 个文件，含工具箱那 26 处） |
| J3 | `PropertyPanel_Header_ShowsLocalizedType` | 行为：zh `按钮1 (按钮)` / en `Button1 (Button)` |
| J4 | `ProjectExplorer_NodeHeader_ShowsLocalizedType` | 行为：树节点标题按语言重建；中文界面下**不许**出现英文枚举名 |
| J5 | `RenameWritePoints_GoThroughToPersisted` | 两个改名写回点（属性面板 / 资源管理器）都过 `ToPersisted` |

计数守卫的两张表（**动面就重数**）：

- **名字显示**（10 文件 / 24 处）：`MainWindow.axaml.cs` **3**（撤销描述 ＋ 两条状态栏）·
  `UndoRedoManager.cs` 4 · `DesignCanvas.Renderers.cs` 4 · `DesignCanvas.IndustrialRenderers.cs` 3 ·
  `ProjectExplorerView.axaml.cs` 3 · `Wiring.DockAggregate.cs` 2 · `PropertyPanelView.axaml.cs` 2 ·
  `DesignCanvas.cs` 1 · `AnimationEditorView.axaml.cs` 1 · `ReferenceRow.cs` 1；
- **类型显示**（10 文件 / 36 处）：`ToolboxView.axaml.cs` **26**（原始消费者）·
  `PropertyPanelView.axaml.cs` 2 · `MainWindow.axaml.cs` 2 · `DesignCanvas.Editing.cs` 1（新建控件的
  **默认显示文案**）· `DesignCanvas.cs` 1 · `DesignCanvas.Renderers.cs` 1 ·
  `ProjectExplorerView.axaml.cs` 1 · `AnimationEditorView.axaml.cs` 1 ·
  `FaceplateTemplateEditorView.axaml.cs` 1 · `ReferenceRow.cs` 1。

---

## 4. 负控 5 支（`.workbuddy/tmp/s160-nc.py`，还原 SHA256 逐字节相同）

| 控制 | 注入 | 实测 |
|---|---|---|
| nc1 | 属性面板标题行的类型改回英文枚举名 | 计数守卫红（2 → 0）＋ J3 红 |
| nc2 | **状态栏两条改回 `first.Name` / `first.Type`** | 名字计数守卫红（3 → 1）—— 阶段 159 漏掉的那一处 |
| nc3 | 面单摘要的类型列表改回 `Type.ToString()` | 类型计数守卫红（1 → 0） |
| nc4 | `Describe` 直接返回枚举名 | J1 红 ＋ J3 红 |
| nc5 | 资源管理器改名写回绕过 `ToPersisted` | J5 红 |

---

## 5. 诚实边界

1. 两个显示面守卫都是**清单式**的：新加一个文件里的显示点**不会**被自动发现
   （源码上看不出某个 `.Name` / `.Type` 是"给人看的"还是"当标识符用的"）。
   计数能保证的是**已列文件里的处数变化当场红**。
2. `ControlTypeTextKeys.Describe` **没有默认支**（新增枚举成员不接文案 ⇒ 编译期 CS8509 红）；
   因此这些显示点遇到**非法枚举值**会抛 —— 与它既有的三条调用路径同一前提（都来自真实控件）。
3. 未纳入本面的：AI 工具/诊断文本里的类型名（那是**数据**不是界面文案）、
   Core 层台账里的英文类型名（Core 是库、不引 `Lang`）。
