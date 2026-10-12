# XAML 资源键的「定义侧反向闭合」—— 顺带修掉 21 条几何的双份

> 阶段 71（2026-09-22）。对应登记表 #38 遗留③（原文：「『定义但从未被引用』**50** 个键
> 只做了反向对照，未见得是死键，未处理」）。
> 相关：`docs/2026-09-20-xaml-resource-key-closure.md`（正向那半）、阶段 69（`AVLN2000` / `InvalidCastException` 定性）。

## 1 一句话结论

**那句「50 个」是准的，但它不是 50 个死键，而是「21 条几何写了两份」+「引用面漏了两种调用形状」+「3 个消费者在包外」。**
修完之后孤儿数 **50 → 3**，且这 3 个全是合法例外 —— **真死资源 0 个**。

| 族 | 个数 | 真实消费路径 | 为什么旧引用面看不见 | 处置 |
|---|---|---|---|---|
| **绘图/快捷图标** | **21** | **无 —— 几何被逐字内联进 `MainWindow.axaml`** | 内联几何不是 `{StaticResource}` | **修（本阶段主缺陷）** |
| 主题资源（Menu/ContextMenu/SystemControl/Dock 边框背景） | 19 | `AppThemeService` 的 `SetOrUpdate*` | 调用形状是 `helper(字典, "键", 值)` | 补引用形状 ③ |
| 分类色板 `HmiCat*Brush` | 5 | `Theme.cs:62-66` 的 `Brush("HmiCatXxxBrush")` | `Brush("K")` 不属旧三类形状 | 补引用形状 ④ |
| 主题变体字典键 | 2 | Avalonia 按 `RequestedThemeVariant` 选 | **原理上**不可静态枚举 | 进差异表（写明理由） |
| Dock 图标覆盖 | 1 | Dock 主题包内 | 消费者在包外 | 进差异表（写明理由） |
| （合计） | 50 | | | 21 修 + 26 补形状 + 3 例外 |

## 2 缺口是怎么量出来的

先按既定纪律**验真登记表的断言**（本项目已多次发现登记表里的数字/定性过期）。探针
（`.workbuddy/tmp/probe-orphan-xaml-keys.py`，镜像 `XamlResourceKeyClosureTests` 的三个正则，
不另抄一套规则）跑出来：App 根下定义 **187** 个 `x:Key`（`App.axaml` 128 + `Assets/Icons.axaml` 65，
`Icons.axaml` 里 64 条不同几何），静态面零引用的 **正好 50 个**。

🔑 **第一个关键分辨**：这 50 个不是一个同质集合。粗看至少三类，处置相反（一类要修、一类要补扫描面、
一类要承认看不见）。把 50 个键逐族拿代码证据分开，是本阶段真正的起点。

🔑 **第二个关键分辨**：`grep -rn "IconDraw\|IconQuick" src/ tests/ | grep -v Assets/Icons.axaml`
⇒ **0 命中**。23 个图标键（实为 21）在整个仓里**只出现在定义处**。

## 3 主缺陷：21 条几何在两个文件里各存一份

### 3.1 事实

`MainWindow.axaml` 里绘图工具栏那一段有 **21** 处 `<Path Data="几何字面量"/>`，
逐条与该文件里 `Icons.axaml` 的键**几何全等**（用探针按几何全等建映射，不手抄）：

```
IconDrawSelect     Icons.axaml:77  ==  MainWindow.axaml:472
IconDrawRectangle  Icons.axaml:78  ==  MainWindow.axaml:476
IconDrawEllipse    Icons.axaml:79  ==  MainWindow.axaml:480
IconDrawPolyline   Icons.axaml:80  ==  MainWindow.axaml:484
IconDrawArc        Icons.axaml:81  ==  MainWindow.axaml:488
IconDrawLine       Icons.axaml:82  ==  MainWindow.axaml:492
IconDrawText       Icons.axaml:83  ==  MainWindow.axaml:496
IconQuickButton    Icons.axaml:85  ==  MainWindow.axaml:508
IconQuickLabel     Icons.axaml:86  ==  MainWindow.axaml:512
IconQuickSlider    Icons.axaml:87  ==  MainWindow.axaml:516
IconQuickSwitch    Icons.axaml:88  ==  MainWindow.axaml:520
IconQuickCheckBox  Icons.axaml:89  ==  MainWindow.axaml:524
IconQuickLed       Icons.axaml:90  ==  MainWindow.axaml:528
IconQuickGauge     Icons.axaml:91  ==  MainWindow.axaml:532
IconQuickBar       Icons.axaml:92  ==  MainWindow.axaml:536
IconQuickDropdown  Icons.axaml:93  ==  MainWindow.axaml:540
IconQuickTable     Icons.axaml:94  ==  MainWindow.axaml:544
IconQuickImage     Icons.axaml:95  ==  MainWindow.axaml:548
IconQuickKnob      Icons.axaml:96  ==  MainWindow.axaml:552
IconQuickTextArea  Icons.axaml:97  ==  MainWindow.axaml:556
IconQuickKeyboard  Icons.axaml:98  ==  MainWindow.axaml:560
```

### 3.2 成因（`MainWindow.axaml:440` 那段注释自己写着）

> 绘图工具栏图标：`PathIcon` 模板用 `Fill` 填充，会把描边线条型图标渲染成实心色块
> （矩形/椭圆/按钮框）或空白（直线/折线/文本），故改用 `Path` 控件 + `Stroke` 描边渲染

诊断是**对的** —— `PathIcon` 的模板确实用 `Fill`。但结论跳错了一步：
**「换成 `Path` 控件」与「必须把几何内联」是两件事**。`{StaticResource}` 取出来的是
`StreamGeometry` 对象，`Path.Data` 的类型就是 `Geometry` ⇒
`<Path Data="{StaticResource IconDrawSelect}"/>` 在当初**同样是可行的**。
内联的代价是：同一份几何两处存放，**改一处不改另一处会静默分叉** ——
编译期零警告、运行期不抛，只是图标画得不一样。

### 3.3 为什么全仓 3000+ 条判据没有一条看得见它

`XamlResourceKeyClosureTests`（阶段 47 建）只走**一个方向**：引用 ⇒ 定义
（「每个被引用的键都能解析」）。反向「定义 ⇒ 有人引用」从建类起**一直是空的**。
这 21 条几何在**两个方向**上都看不见：正向——它们不是引用；反向——那条判据不存在。

## 4 修法与关键验证

21 处 `Data="几何"` → `Data="{StaticResource IconXxx}"`（脚本
`.workbuddy/tmp/fix-drawing-icons.py`：从两个文件**程序化取几何**并按全等建映射，不手抄；
默认干跑、`--write` 才落盘、每处 `assert count == 1`、落盘后回读校验）。
**零视觉变化** —— 同一份几何、同一控件类型、同一套 `Selector="Path"` 描边样式。

### 4.1 必须实测的那一件事

「改回 `{StaticResource}` 会不会重演当初的 Fill 问题」是**本修法的唯一技术风险**，不能靠推理结论。
行为判据 `MainWindowConstructionTests.DrawingToolbarIcons_AreTheIconDictionaryInstances`
（走生产构造路径：`new MainWindow(settings)` → `Show()` → `Measure`/`Arrange`）在运行期断言 21 个
`Path`：

- `path.Data` 与 `Application.Current.Resources.TryGetResource(key)` 取到的对象**引用相等**
  （`ReferenceEqualityComparer`）—— 这是「真的走到了字典」的最强陈述；
  写成「几何字符串相等」会放过「又内联了一份一模一样的」，而那正是要防的回归形状；
- `path.Stroke` 非空 **且** `path.Stretch == Uniform` —— 钉住「仍然是 `Path` 控件 + 描边样式」，
  把「日后换回 `PathIcon`」这条回头路堵住（换回去几何引用可能还在，但渲染退化成色块/空白）。

实测读数：**工具栏 `Path` 21 个 / XAML 里 21 个 `{StaticResource}` / 字典解析出 21 个实例，0 条不符**。

### 4.2 判据不写「名字 → 图标键」的表

那张表是**把规格抄一份**、会过期。取面改用**结构**：`<Path>` 挂在绘图 `ToggleButton` 上，
或挂在带 `drw-qbtn` 类的 `Button` 上；键从 XAML 现场读。

## 5 引用面：3 类 → 5 类

新增的两类都是**字符串字面量键**，只是调用形状不在旧正则里：

| # | 形状 | 命中 | 语义（**必须写清它证明什么**） |
|---|---|---|---|
| ① | `.axaml` 的 `{StaticResource K}` / `{DynamicResource K}` | 2810 | 框架按名取 —— 最强 |
| ② | `.cs` 的 `Resources["K"]` / `FindResource("K")` / `TryFindResource("K")` | 4 | 代码按名取 |
| ③ | `.cs` 的 `SetOrUpdateBrush\|SetOrUpdateColor(resources, "K", …)` | 22 | ⚠ **只证明「本应用在按名字管理这个键」，不证明有人读它** —— 读的消费者在**包外**（FluentTheme 的 `MenuBase`/`ContextMenu`/`Popup` 模板、Dock 主题） |
| ④ | `.cs` 的 `Brush("K")` / `Color("K")`（`Theme.cs` 的取值口径） | 26 | 本项目自己的一套 API，调用点只写键名 |
| ⑤ | `ResourceDictionary.ThemeDictionaries` 的 `x:Key`（`Light`/`Dark`） | 0（进差异表） | ⚠ **原理上**不可静态枚举 —— 由框架按 `RequestedThemeVariant` **选**，永不显式引用 |

④ 的形状刻意收紧到「**唯一实参且是标识符形状的字面量**」+ 负向环视排除
`RenderResourceCache.Brush(...)` 这类同名但收颜色值的工厂（实测该重载的实参全是变量或
`Color.Parse(...)`，不会把资源键名传进去）。

## 6 判据（新增 4 条）

`XamlResourceKeyClosureTests`：

| 判据 | 断言 |
|---|---|
| `EveryDefinedResourceKey_IsReferencedOrExplicitlyExempt` | 定义的每个 `x:Key` ∈ 引用面 ∪ 差异表；失败消息**把两种可能拆开写**（真死资源 / 引用面又漏了形状），并明说「**补形状，不要往差异表里塞**」 |
| `UnreferencedExemptions_AreStillUnreferencedAndStillDefined` | 差异表**双向过期检查**：键一旦可达（理由作废）或从定义里消失 ⇒ 红 |
| `ReverseClosureFace_IsNotDegenerate` | 面自检：定义面 ≥150、定义∩引用 ≥150、**五类引用形状各设下界**（`xaml`≥1000 / `cs`≥1 / `managed`≥10 / `theme`≥10） |
| `MainWindowConstructionTests.DrawingToolbarIcons_AreTheIconDictionaryInstances` | 见 §4.1 |

**差异表只有 3 条**（每条写明消费者是谁 / 为什么取不到）：

| 键 | 理由 |
|---|---|
| `Light` | 主题变体字典键 —— 由框架按 `RequestedThemeVariant` 选，永不显式引用 |
| `Dark` | 同上 |
| `DockToolChromeIconBrush` | 消费者在包外 —— `Dock.Avalonia.Themes.Fluent.dll` 里有该键名的字面量；`App.axaml` 这里是有意覆盖它的默认值 |

> `DockToolChromeIconBrush` 的取证值得记一笔：`grep -rl` 在 `~/.nuget/packages/` 下**零命中**
> （Dock 主题是编译进 dll 的），`strings`（ASCII）也零命中 —— 但 **`strings -el`（UTF-16）
> 找到了** `DockToolChromeIconBrush` 与 `DockToolChromeDividerThickness` / `DockToolChromeHeaderMargin`
> 等同族键。⇒ 查「某键名是否被某 .NET 程序集引用」，`strings` 要带 `-el`。

🩸 **反向判据的价值恰在于「误报孤儿」比「空跑」更危险** —— 五类形状一旦有一类正则写坏、
一条都不命中，判据就会开始**报出根本不存在的孤儿**，而那会被当成"发现了缺陷"。
`ReverseClosureFace_IsNotDegenerate` 就是为此而设（nc5 专门注入它）。

## 7 负控（端到端，5/5 PASS）

`.workbuddy/tmp/nc71.py` 注入**产品代码 / 测试代码**（不是注入判据的输入），
注入名一律用不存在的名字（`PROBE71_*`），避免 #48（判据里的字面量反过来把自己喂饱）：

| # | 注入 | 期望 | 实测 |
|---|---|---|---|
| nc1 | 把一处 `{StaticResource IconDrawSelect}` 改回**内联几何** | 红 | 红 **2** 条（点名 `IconDrawSelect` + 「又有人把几何内联回去了」） |
| nc2 | 差异表塞一个**已可达**的键 | 红 | 红 **1** 条（「不再适用」） |
| nc3 | 差异表塞一个**不存在**的键 | 红 | 红 **1** 条（同上分支） |
| nc4 | 删掉一处真实引用（`IconNewProject` → `PROBE71_Missing`） | 红 | 红 **2** 条（「定义了却没有任何引用面」） |
| nc5 | 把 ④ `theme` 形状的正则写坏 | 红 | 红 **2** 条（「退化成了空集」+ 5 个 `HmiCat` 孤儿） |

判红谓词匹配测试**自己的汇总行**（`失败: N`，N>0）**且**取到 `错误消息:`；
不写成「stdout 里没有『已通过!』」—— 那会把**编译失败**也算成红。
每个场景还原后**写回原文**（**禁 `shutil.copy2`**：它连 mtime 一起复制 ⇒ MSBuild 判定无需重建
⇒ 正控跑的是负控的二进制），并断言 sha 与原文一致、还原后编译通过。收尾后注入痕迹 grep **0**。

## 8 顺带发现（如实记录，未擅自改）

1. 🩸 **类注释里有一条已被推翻的断言**。`XamlResourceKeyClosureTests` 的「本类没做的事（有意）」
   写着「`{StaticResource X}` 取到一个 `Color` 而属性要 `IBrush`，那类不匹配**同样是静默的**」
   —— 该说法**已于阶段 69 被实测推翻**（`ColorToBrushConverter` 就是为它写的；类型不匹配在
   **构造期**抛 `InvalidCastException` 且带行号）。登记表改了、类注释没跟着改 ⇒ 本轮一并修正：
   那条不是「本类没做的事」，而是「**本来就不需要在这里守**」。
   **教训**：定性被推翻时，要连**它散落过的每一处**一起改，否则下一个读注释的人会照着旧前提做决定。
2. `IconResetTransform`（`Icons.axaml:41`）与 `IconReloadPanels`（`:48`）**共用同一份几何**
   —— 两个不同动作（重置变换 / 重新加载面板）当前显示同一个字形
   （`MainWindow.axaml:230`、`:277`）。**未改** —— 给它俩画两个不同字形是**视觉/产品决策**，
   不是这一轮该顺手做的。⚠ 但这说明「同一份几何」还有**另一种**合法形态：两个字面不同的键
   指向同一字形（与 §3 的「同一个键的几何抄两份」方向相反）。
   探针脚本因此做成「几何 → 键列表」，只在内联几何**有歧义**时才报错。
3. `App.axaml` 里两个 `Color` 形态的 `SystemControlMenuBackground` / `SystemControlPopupBackground`
   （`:148-149`）也由 `AppThemeService.SetOrUpdateColor` **按名写入**（`:357-358`）—— 补了形状 ③ 之后
   它们不再是孤儿。⚠ 但「**有人写**」与「**有人读**」是两件事：这两个 `Color` 键的读取方在
   FluentTheme 模板里，**未去包外取证**（与 §5 ③ 同一条边界）。

## 9 诚实边界

1. **只说「引用面取不到」，不说「没人读」**。形状 ③ 的语义是**被管理的资源**，不是被消费的键；
   差异表 3 条里 2 条（`Light`/`Dark`）是「框架按名选」、1 条是「消费者在包外」——
   **这三条都不是"我们证明了没人用"**，而是「本判据看不见它，且能说清为什么」。
2. **反向判据的面只到 `src/HmiEditor.App` 的 `.axaml`**（`AppSourceScanner.ScanAppXaml`）。
   `tests/` 零 XAML、`src/` 其余 11 个项目零 XAML ⇒ 这个面当前是完整的，
   但**「当前完整」是靠事实，不是靠机制**（新项目带 `.axaml` 时不会自动进面）。
3. **`Path.Data` 的引用相等只在主窗口的 21 个工具栏图标上被断言**。其余 44 个图标键
   （`CommandBar` 那批）走的是 `PathIcon`，由正向判据（能不能解析）覆盖，
   **没有**「取到的是不是字典里那一个实例」这类行为断言 —— 它们不像工具栏那样有过内联史。
4. **没有查「几何画出来对不对」**。同一份几何在 `Path` + `Stretch=Uniform` 下与在
   `PathIcon` 下的**渲染结果**是否逐像素相同，未做像素级比对（`Stretch`/`Stroke` 断言只证明
   样式在、不证明像素同）。这一侧的证据仍是「同一份几何 + 同一套样式属性」这个推理。

## 10 验证

- 构建 **0 警告 0 错误**；`App.UiTests` **401 → 405**（+4：反向闭合 3 条 + 行为 1 条）。
- 门禁 `scripts/ci-gate.sh` 提交前 / 提交后各一次，7 步全绿、`GATE_EXIT=0`，逐程序集数字同数。
- 负控 **5/5**（§7）；注入痕迹 grep **0**；工作树只有预期文件改动。
