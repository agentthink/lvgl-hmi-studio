# `ProjectSettingsView` 配置往返保真：打开+应用后工程必须逐字段原样

> 对应 `docs/2026-09-17-open-items-register.md` §4 第 17 项
> （"`ProjectSettingsView` 其余 5 个分类仍只有静态源码扫描守卫（行为面未覆盖）"），
> 是 `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md`（运行时用户表那一块）的**续篇**。
> 日期：2026-09-17

---

## 0. 一句话结论

给这个面板的其余 5 个分类（基本 / 显示 / 通讯 / 运行时 / 导出）补上了行为网，判据是
**"打开工程设置 → 点「应用」→ 工程序列化结果逐字节不变"**。它当场抓出一个真缺陷：
**一个没有指定硬件型号的工程，只要打开设置点一下「应用」，就会被静默绑到清单里的第一块板**。

---

## 1. 判据：为什么用"整个工程 JSON 逐字节不变"

这个面板有约 **50 个**"一个控件对一个字段"的绑定。逐字段手写断言有两个必然的坑：

- **漏字段**：写到第 30 个就累了 —— 而漏掉的那个正好是将来出问题的地方；
- **遮差异**：只断言"我关心的字段对"，就把"顺手改了别的字段"放过去了。

所以判据换成：`ProjectSerializer.Serialize(project)` 在**操作前**与**操作后**比对，
**任何**字段变化都红（包括以后新加的字段，不需要回来补断言）。失败时按行输出差异，
直接点名是哪个字段：

```
打开工程设置并点「应用」后，工程内容发生了变化（应逐字段原样）：
仅改动后有: "hardwareTargetId": "allwinner-t113",
```

配套一条互补用例：**改一个字段 → 应用 → 恰好只有那一行变**（证明"改了的动了"而不只是"没改的没动"）。

---

## 2. 覆盖（新增 10 项）

| 组 | 用例 | 钉住的性质 |
|---|---|---|
| 主判据 | `Apply_WithoutEditing_PreservesEveryConfigField` | 打开+应用 ⇒ 工程逐字节不变 |
| 互补 | `Apply_AfterEditingOneField_ChangesOnlyThatField` | 改一处 ⇒ 恰好一行 dif |
| 双向 | `EveryCategory_RepresentativeField_WritesBackToModel` | 6 个分类各取一个代表字段，UI → 模型 |
| 历史缺陷 | `Apply_DoesNotTouchExistingScreens_OnlyDefaults` | 应用**不得**连带改写已有屏幕尺寸 |
| null 语义 | `ClearingOptionalTextFields_WritesNullNotEmptyString` | 可空文本清空 ⇒ `null`（不是空串） |
| null 语义 | `UnsetScreenSelectors_SaveNull_NotFirstScreen` | 未选屏 ⇒ `null`（不是第一块屏） |
| 正常路径 | `NonDefaultVersionAndOutputDir_RoundTripExactly` | 非默认值原样往返 |
| 有意归一 | `NullVersionAndOutputDir_NormalizeToModelDefaults_Intentionally` | 钉住"这两项 null 归一到默认"是设计 |
| **本轮缺陷** | `UnknownHardwareTargetId_BecomesUnspecified_NeverAnotherBoard` | 失效型号 ⇒ "未指定"，**绝不**绑到另一块板 |
| **本轮缺陷** | `ValidHardwareTargetId_RoundTripsExactly` | 有效型号原样保留（修法不能把正常路径也变成未指定） |

夹具 `RichProject()` 把**每个**配置字段都设成**非默认但合法**的取值（枚举成员名、真实屏幕 Id、
真实角色 Id），否则测的就成了"非法值归一"而不是"往返保真"。

---

## 3. 抓出的真缺陷：硬件型号被静默绑到另一块板

### 3.1 机制

```csharp
// BindToProject
CmbHardwareTarget.SelectedItem = FindHardwareTargetItem(_project.HardwareTargetId);

// FindHardwareTargetItem（旧）
foreach (var item in ...) if (Equals(item.Tag as string, targetId)) return item;
return CmbHardwareTarget.Items.OfType<ComboBoxItem>().FirstOrDefault();   // ← 回退第一项

// SaveToProject
_project.HardwareTargetId = GetHardwareTargetId();                        // ← 把回退结果持久化
```

于是 `HardwareTargetId == null`（或是一个**已经不存在**的旧 Id）时：

> 打开工程设置 → 点「应用」 ⇒ `HardwareTargetId` 变成清单第一项（本机是 `allwinner-t113`）。

### 3.2 为什么这是真缺陷（而不是"补个默认值"）

`HardwareTargetId` 是 `string?`，**`null` 有明确语义**，而且代码各处都支持它：

| 消费点 | 对 null 的处理 |
|---|---|
| `CodeGenerator.Simulator.cs` / `SharedLibrary.cs` | `// Target: {HardwareTargetId ?? "generic"}` |
| `MainWindow` 状态栏 | `_currentProject.HardwareTargetId ?? "generic"` |
| `ValidateHardwareTarget` / `GetSelectedHardwareTarget` | 取不到目标 ⇒ **隐藏校验行**（本文件自己写着"无匹配（旧工程保存了失效 id）时返回 null"） |

也就是说：**"未指定"是被显式支持的状态**，而 UI 层那句"避免 `SelectedItem = null`"的
防御性回退，经 `SaveToProject` 变成了**对工程的持久化改动**。后果：

1. **产物变了**：生成代码里的 `// Target:` 注释从 `generic` 变成那块板子；
2. **校验对象变了**：`ValidateHardwareTarget` 开始按用户从未选择过的板子做兼容性校验；
3. **失效 Id 被绑到"另一块板"**：比"未指定"更糟 —— 用户以为自己的选择还在，其实被换成了别的型号；
4. **无操作的设置访问产生 git diff**：只打开看一眼再确定，工程 JSON 就多一行。

### 3.3 修法

`FindHardwareTargetItem` 无匹配时**返回 `null`**（下拉保持未选中），不再回退第一项：

```csharp
if (string.IsNullOrEmpty(targetId)) return null;
foreach (var item in ...) if (Equals(item.Tag as string, targetId)) return item;
return null;            // 失效 Id 诚实表示"未指定"，而不是绑到另一块板
```

三处都自洽：下拉未选中 → `GetHardwareTargetId()` 得 null → 保存写 `null`（往返保真）；
`GetSelectedHardwareTarget()` 得 null → 校验行隐藏（与旧工程的"失效 id"路径一致）；
用户在界面上显式选择型号后，行为与以前完全相同。

---

## 4. 同时钉住：一条历史缺陷 + 三条有意归一

| 项 | 类型 | 判据 |
|---|---|---|
| 应用时**不得**覆盖已有屏幕尺寸 | **历史缺陷**回归锁 | `SaveToProject` 里那段注释记载：*"曾经的实现是无条件 foreach 覆盖，导致每次打开工程设置点确定都会静默重置用户逐屏手工调过的尺寸"*。契约是 `ScreenDefaultsConfig` 只影响**后续新建**的屏幕 |
| 清空可空文本 ⇒ `null` | null 语义 | 空串会让下游 `IsNullOrWhiteSpace` 判定与 JSON 都多出一段无意义内容 |
| 未选屏 ⇒ `null` | null 语义 | 不能被塞回第一块屏的 Id（与 §3 同族，但这两处新实现的写法是对的） |
| `ProjectVersion` / `OutputDirectory` 的 null ⇒ 默认值 | **有意归一**（钉住，防"误修"） | 这两项在模型上是**非空**属性（默认 `1.0.0` / `./export`），null 不是有意义的状态；保留 null 会让 `ResolveOutputDirectory` 直接返回 null、导出对话框拿不到默认目录 |

---

## 5. 负向验证（4 组，全部命中）

| NV | 注入 | 期望红的用例 |
|---|---|---|
| A | `FindHardwareTargetItem` 恢复"无匹配回退第一项"（**复现本轮修掉的缺陷**） | `UnknownHardwareTargetId_BecomesUnspecified_NeverAnotherBoard` |
| B | 恢复历史缺陷：应用时无条件 `foreach` 覆盖已有屏幕尺寸 | `Apply_DoesNotTouchExistingScreens_OnlyDefaults` |
| C | 清空的可选文本写成 `""` 而非 `null` | `ClearingOptionalTextFields_WritesNullNotEmptyString` |
| D | 未选屏时回退第一块屏的 Id | `UnsetScreenSelectors_SaveNull_NotFirstScreen` |

---

## 6. 两个测试侧的坑（记下来省下一次重踩）

1. **这些数字框不是 `NumericUpDown`，是 `FluentAvalonia` 的 `FANumberBox`**
   （XAML 写的是 `<fa:FANumberBox>`）。`FindControl<NumericUpDown>` 会抛
   *Expected control 'NumScreenWidth' to be 'Avalonia.Controls.NumericUpDown' but it was '…FANumberBox'*。
   仓库已有先例：`PropertyPanelUiTests` 用 `Find<FANumberBox>(…)` + `Convert.ToDouble(…Value)`。
2. **比对必须用同一个工程实例**。首版我拿"新建的等价工程"当基准，
   结果屏幕上 `id`（每次生成不同）与 `createdAt`/`lastModifiedAt` 全被算成差异。
   改成"同一个实例操作前后"即可。

---

## 7. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2624 通过 / 0 失败**（App.UiTests 172 → **182**；本轮基线 2614 ⇒ **+10**） |
| 负向验证 | **4/4 命中** |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 8. 范围边界

- 登记表 §4 第 17 项（其余 5 个分类的行为面）**已闭环**。
- **仍未覆盖的行为**（登记在此，不假装已覆盖）：
  1. **`CmbLvglVersion` 的失效值**：项是固定列表 `8.3…9.5`，工程里存了表外版本（例如更老工程写的 `8.2`）
     时，装载会 `SelectedItem = null`、保存会归一成 `"9.5"` —— 与 §3 同族。
     本轮的 `RichProject` 用的是表内值 `9.4`，所以往返测试不会碰它；
     要闭环得先定"表外版本该保留还是该报错"，属产品决策（登记表 §2）。
  2. **`CmbColorDepth` 的死代码**：`ResolveColorDepthItem` 定义了但从未被调用，其文档声称
     防止"无效值被静默改写成默认值"。实测该回退**行为等价**（`ColorDepth` 首成员就是
     `NormalizeColorDepth(null)` 返回的 `RGB565`），所以只是死代码、没有可观测缺陷。
     按本项目"不留没有证据的改动"，本轮**未删**（删它属独立清理，且要连带处理它的文档声明）。
  3. 面板的**布局/命中测试**仍不在本文件的判据内（由 `ViewSmokeUiTests` / `HeadlessInputUiTests` 覆盖）。
