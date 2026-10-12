# 让配置项停止撒谎 —— 五类「UI 在撒谎」的系统性收口

> 记录日期：2026-09-16（阶段 10）
> 范围：A 描述错 / A′ 注释与实现相反 / B 同一操作多套语义 / C 重复建模 / D 下发无消费 / E 色深多处建模
> 结论：**五类全部收口**，新增 30 个受管清单条目、4 个结构守卫、1 条持久化回归网。

## 0. 症状

这一轮针对的不是崩溃，而是**配置项静静地不生效**。用户路径是：
在对话框里改一个值 → 点确定 → 值被记住了 → 行为一点没变 → 用户无法判断
是自己理解错了，还是软件坏了。

三个真实病例：

| 病例 | 症状 | 真正的决定者 |
|---|---|---|
| 改「网格大小」 | 两个设置界面各有一个输入框，**改哪个都没用** | `DesignCanvasView.axaml` 里写死的 `GridSize="10"` |
| 改「吸附步长」（当时不存在这个项） | —— | `DesignCanvas.SnapThreshold = 5`（硬编码常量） |
| 改任何应用级偏好（面板比例、最近工程、缩放） | 当时**当前会话内有效**，重启全回默认 | `SettingsService` 的 JSON 反序列化缺陷（见 §3） |

## 1. 五类根因与处置

| 类 | 根因 | 处置 |
|---|---|---|
| **A** 描述本身错 | `ExportConfig.GenerateDebugSymbols` 的文档写「J-Link/ST-Link 调试」，而 T113 是 ARM Linux（openixSuit 烧录 / ADB 热更新） | 重写文档；同时把这个开关真正接上（给 CMake/Makefile/platformio 三条构建配置加 `-g`） |
| **A′** 注释与实现相反 | `ScreenDefaultsConfig` 类注释写「只影响后续新建的屏幕」，而 `ProjectSettingsView.SaveToProject` 无条件 `foreach` 覆盖所有已存在屏幕 | 删除那段 `foreach`；新增显式的「应用到全部屏幕」按钮承载原本的意图 |
| **B** 同一操作多套语义 | 6 条新建屏幕路径，尺寸来源 3 种（工程配置 / 1024×600 / **800×480**） | 抽出唯一工厂 `Core/Services/ScreenFactory.cs`，优先级契约「显式参数 > 工程默认」；`int Width = 800` 改为 `int?`，用 `null` 表示「用下游默认」 |
| **C** 重复建模，两套都死 | 工程级 `EditorConfig`（12 字段）与应用级 `SettingsService.Interaction/Display` 逐字段对应，默认值逐字相同 | 应用级收口为唯一权威；`EditorConfig` 降级为只读兼容载体（见 §4）；网格/吸附/显示网格**真正接线**（见 §5） |
| **D** 下发了但无消费 | 工程配置里 30 个字段只进 JSON | 建立受管清单 `ConfigFieldInventory` + 面板灰显标注 + 三向守卫（见 §6） |
| **E** 同一语义多处建模 | 「色深」在 4 处各有建模：`ScreenDefaultsConfig.DefaultColorDepth`(string) / `ImageOptimizer.ColorDepth`(enum) / `Hmi.Hardware.Contracts.ColorDepth`(enum) / `LvglConfig.DefaultColorDepth`(int) / `AssetItem.ColorDepth`(int) | 候选项来源统一到 `Hmi.Hardware.Contracts.ColorDepth` 枚举；`ImageOptimizer.ColorDepth` 改名 `ImageColorDepth` 消除 CS0104；`ConfigDrivenTarget` 三个手写 switch 改 `Enum.TryParse + Enum.IsDefined` |

### 1.1 「null 表示没给」这条契约

B 类的根因值得单独记：`AiGeneratedScreen.AiScreen.Width` 原本是
`public int Width { get; set; } = 800;` —— **「参数没给」与「参数给了 800」不可区分**。
模型输出里没写尺寸时，AI 屏就静默变成 800×480（而工程目标是 1024×600）。

现在改为 `int?`，`null` = 用下游默认（`ScreenFactory` 会回退到工程配置，再回退 1024×600）。
**新配置字段的默认值是「危险默认」时，一律用可空类型 + `null` 语义，不要填常量。**

## 2. 应用级设置成了唯一权威

- `ProjectSettingsView` 的「编辑器设置」分类**整体移除**（侧栏 7 → 6 项）。
  理由：那 12 个控件要么已迁到应用设置（`SettingsDialog`），要么对应功能根本不存在；
  留一个"去别处改"的空标签比没有更糟。
- `EditorConfig` 保留**类型与属性**，但：
  - 类注释声明「已废弃的只读兼容载体」，每个字段标注真实来源；
  - 保留的唯一目的是让已有 `project.hmi` **无损往返**（读进来再存回去，值原样保留），
    而不是静默丢掉用户文件里的一段；
  - 结构守卫 `EditorPreferenceWiringTests.ProductCode_NoLongerReferencesEditorConfig`
    断言「除模型自己与 `HmiProject` 的持有属性外，产品代码零引用」。
- `SettingsDialog` 新增「吸附步长」项（新键 `SnapStep`，默认 5）。

## 3. `SettingsService` 曾经重启即回默认（本轮最隐蔽的缺陷）

`UserSettings` 是 `Dictionary<string, object?>`，而 `System.Text.Json` 反序列化
`object` 时给出的是 **`JsonElement`**；`Get<T>` 用 `value is T` 判等 ——
两者不相等 ⇒ **磁盘上每一个值都被当成"类型不匹配"退回默认值**。

后果链条：
1. 面板比例、最近工程列表、缩放级别每次启动都回默认；
2. 打开 `SettingsDialog` 时 `PopulateXxx` 把默认值灌回控件 → 触发 `ValueChanged` →
   把这些默认值**写回文件** ⇒ 用户原本存的值被抹掉。

修法：
- `Load()` 走 `Dictionary<string, JsonElement>` + `Materialize()` 折算成普通 CLR 值；
- `Get<T>` 加一层 `Coerce`（`1.0` 被序列化成 `1`、读回来是 `int` 这类情形）；
- **`MaterializeNumber` 必须写成早期返回**：`int : long : double` 三元素三元链的公共类型是
  `double`，会把每个整数都装箱成 `double`（这个坑在本轮真的踩到并被测试抓住）。

顺带加了设置文件的结构版本（`SchemaVersion`）与一次性迁移：
v1 的 `GridSize` 默认值 8 **从未被任何消费点读取**（画布恒用硬编码 10），
所以文件里的 8 不代表用户意图，迁移时对齐到 10。迁移结果只在内存，靠下一次 `Set` 落盘 ⇒ 幂等。

## 4. 编辑器偏好真正接线（默认行为逐像素不变）

| 偏好 | 应用级键 | 接到 | 出厂默认（= 接线前的硬编码） |
|---|---|---|---|
| 网格点间距 | `Interaction.GridSize` | `SnapGrid.GridSize` | 10 |
| 显示网格 | `Interaction.ShowGrid` | `SnapGrid.IsVisible` | true |
| 吸附步长 | `Interaction.SnapStep`（新） | `DesignCanvas.SnapStep` | 5 |
| 吸附开关 | `Interaction.EnableSnapToGrid` | `DesignCanvas.SnapEnabled` | true |

### 4.1 网格点间距与吸附步长是两个数，不是一个

接线前的实际手感是「点阵 10px、吸附 5px」。把两者合成一个数会**连带改掉拖拽精度**，
所以保留两个键。它们不相等本身不是缺陷：5px 是 10px 点阵的二分格。

### 4.2 「默认行为不变」靠什么保证

- `SettingsService` 出厂值 = 10 / 5 / true / true，与旧硬编码逐值相等；
- `DesignCanvas.SnapEnabled` 关闭时 `SnapToGrid` 原样返回（旧行为恒开 ⇒ 默认不变）；
- `SnapGrid.GridSizeProperty` 的默认值也是 10，与偏好出厂值一致（有测试钉住）；
- `DesignCanvasView.axaml` **不再写** `GridSize="10"`，点距的唯一来源是应用级设置；
- 改设置**即时生效**：`SettingsService.SettingChanged` → `DesignCanvasView` 重算，
  不必重启编辑器。

## 5. 受管清单：D 类的机械判据

`src/HmiEditor.App/Services/ConfigFieldInventory.cs` 登记**每一个「面板上有、其实不生效」的字段**
（本轮 30 条：应用级 14 + 工程级 16），每条含 `Type / Field / Surface / Status / Control / Note`。

- `Status`：`StoredOnly`（只进文件、没人读）或 `NotImplemented`（功能根本不存在）。
- `Control`：需要灰显的控件名；为 `null` 表示"面板上没有对应控件、无需标注" ——
  **`Theme.AccentColor` 就是这种情况**：界面上的强调色输入框是真生效的
  （它写的是 `AppThemeService.CustomAccent`），只是这个键没人读。
  这种"键是死的、控件不死的"必须区分开，否则会把能用的控件灰掉。
- 两个对话框按 `Surface` 给对应控件加标注（灰显 + 悬停原因），底部显示图例；
  清单里删掉条目 ⇒ 面板自动不再标注（**接线只需改一处**）。

### 5.1 三向守卫

| 守卫 | 断言 |
|---|---|
| `InventoryEntries_ExistOnDeclaredType` | 清单里的字段在声明类型上真实存在（表不能写歪） |
| `NotWiredFields_AreNotConsumedOutsideWhitelist` | 这些字段的**所有**命中都在白名单内 ⇒ 一旦有人接上真实消费点就红，逼他删条目 |
| `PanelMarkings_MatchInventoryAndExistInXaml` | 清单 ↔ 面板标注的**键与控件名**双向一致，且控件名真的在该 XAML 里 |
| `EntriesWithoutControl_AreNotMarked` | 反向：没有控件的条目绝不能被标注 |

白名单里除了声明文件与两个面板，还有一个**只写不读**的落点：
`AgentToolFactory.cs`（AI 工具的配置写入面）。它把 AI 传来的值写进工程配置，
但没有任何执行路径读回来 —— 「写入」与「消费」是两件事。

### 5.2 已知未登记的一类

只被 AI 工具**写入**、没有任何执行路径读取的字段（`RuntimeConfig.EnableHmiReboot` 等）。
它们需要的是「补执行路径」，不是「标未生效」，机械判据分不出来，
所以不入表，单独记在这里。**下一轮若要收口，方向是补执行路径，不是加清单条目。**

## 6. 涉及文件

**新增**

| 文件 | 作用 |
|---|---|
| `src/HmiEditor.Core/Services/ScreenFactory.cs` | 新建屏幕的唯一工厂 |
| `src/HmiEditor.App/Services/ConfigFieldInventory.cs` | 受管清单 |
| `src/HmiEditor.App/Controls/NotYetEffective.cs` | 「尚未生效」标注（灰显 + 悬停） |
| `tests/HmiEditor.TestSupport/SourceScanner.cs` | 结构守卫共用扫描器（剥注释用单遍状态机） |
| `tests/.../ScreenFactoryTests.cs`、`ColorDepthSourceOfTruthTests.cs`、`SettingsServiceTests.cs`、`EditorPreferenceWiringTests.cs`、`ConfigFieldInventoryTests.cs` | 行为 + 守卫 |

**主要修改**

`SettingsService.cs`（持久化 + 迁移 + `SnapStep` + `SettingChanged`）、
`ProjectSettingsView.axaml(.cs)`（移除编辑器分类 + 剩余字段标注）、
`SettingsDialog.axaml(.cs)`（吸附步长 + 标注）、
`DesignCanvas.cs` / `DesignCanvas.Input.cs` / `SnapGrid.cs` / `DesignCanvasView.axaml(.cs)`（接线）、
`EditorConfig.cs`（降级为兼容载体）、`ExportConfig.cs` / `ScreenDefaultsConfig.cs`（文案与契约）、
`CodeGenerator.BuildSystem.cs`（`-g`）、`ImageOptimizer.cs`（`ImageColorDepth`）、
`Hmi.Hardware.Loader/ConfigDrivenTarget.cs`（`Enum.TryParse + IsDefined`）、
`AiGeneratedScreen.cs` / `AgentOutput.cs`（可空尺寸读取）。

## 7. 本文件没做的事

1. `UndoStackSize` 只是被标成「未实现」—— 真要生效得给 `UndoRedoManager` 加容量裁剪
   （现在是 `Stack`，"丢最旧的"需要换数据结构）。
2. `ShowRulers` / `EnableLivePreview` / `ShowPreviewOverlay` / `AutoHidePanels` 同理：
   标了「未实现」，功能本身没做。
3. `Display.CanvasBackgroundColor` 与主题资源 `HmiBgWindow`、`PaletteDefaults.Background`
   是三处同值建模 —— 本轮只标注「此处不生效」，没做收敛。
4. 受管清单的 `Note` 文案是中文直写，未走本地化资源（30 条 × 2 语言成本高于收益）。
