# `ProjectSettingsView` 行为级 UI 测试：补基建，顺带抓出两个真缺陷

> 对应 `docs/2026-09-17-open-items-register.md` §4 第 16 项（软件侧仍开着里最有价值的一项），
> 原始登记见 `docs/2026-09-16-runtime-user-editor-ui.md` §7.1：
> *「App 层没有 `ProjectSettingsView` 的测试基建（App.UiTests 里零覆盖），本轮把可测逻辑抽进了 Core
> （`RuntimeUserValidator`），但『草稿语义 / 两层拷贝』这类绑在对话框生命周期上的行为仍只有人工验证。」*
> 日期：2026-09-17

---

## 0. 一句话结论

补齐了这块基建（13 项行为测试，真的把面板 `Show()` 出来、按真实控件驱动一遍生命周期），
**并且它立刻抓出两个真缺陷** —— 两个都**只在装载/特定形态下错**，人工点两下就会被纠正，
所以此前多轮人工验证**必然**漏掉它们。这是"补测试基建"与"查出一个 bug"的典型差别：
基建的价值不在这一次抓到的两个，而在**它以后会一直抓**。

---

## 1. 基建：怎么驱动一个模态窗口

| 决策 | 做法 | 理由 |
|---|---|---|
| 线程 | 整个用例体跑在 `UiTestHost.Run(...)` 里 | 控件必须在 headless UI 线程上构造 —— 否则 `ChromeWindow` 构造函数报 *Unable to locate 'Avalonia.Platform.IWindowingPlatform'*。这一条踩过（首版把构造成在 `Run` 外面，12 个用例全红） |
| 窗口 | `view.Show()` → 用例 → `finally { view.Close(); }` | 走真实 `Show`/`Close` 生命周期（`BtnOk`/`BtnCancel` 都依赖 `Close()`），并在失败路径上也收尾，避免残留窗口影响同程序集其它用例 |
| 分类 | `CategoryList.SelectedIndex = 5`（security）后 `Pump()` | 用户表控件在 `PanelSecuritySettings` 内（默认 `IsVisible=False`） |
| 触发 | `button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))`，**不是**坐标点击 | 本文件测**接线与语义**（事件订阅 + 处理器 + 控件交互）；命中测试/布局是另一条轴，已由 `HeadlessInputUiTests` / `ViewSmokeUiTests` 覆盖。用户表在 `ScrollViewer` 内，坐标点击会让用例依赖"窗口高度够不够把按钮露出来"这种与语义无关的条件 |
| 观测 | 草稿 = `RuntimeUserList` 各项的 `Tag`（就是草稿的 `RuntimeUser` 实例） | 于是"草稿 vs 模型"可以**直接用引用比较**断言，不靠间接推断 |

---

## 2. 覆盖的 13 项

| 组 | 用例 | 钉住的性质 |
|---|---|---|
| 装载 | `SecurityPanel_PopulatesRuntimeUsersFromProject` | 列表按模型填充 |
| 装载 | `Load_WithConfiguredUsers_ShowsUsableCount_NotEmptyHint` | **装载即正确**（缺陷 #2 的回归锁） |
| 两层拷贝 | `EditingDraft_DoesNotTouchModel_FirstLayerCopy` | 第一层：改草稿条目字段 → 模型纹丝不动 |
| 两层拷贝 | `Apply_WritesModel_AndSecondLayerCopyPreventsAliasing` | 第二层：保存后再改草稿 → 模型不受影响，且引用不同 |
| 增删 | `AddUser_ChangesDraftOnly` / `RemoveUser_ChangesDraftOnly` | 增删只进草稿 |
| 生命周期 | `Cancel_AfterEditing_LeavesModelUntouched` | 增+改+删后取消 ⇒ 模型原样 |
| 生命周期 | `Ok_WithChanges_WritesModelAndCloses` | 有改动时确定 = 写模型 + 关闭 |
| 生命周期 | `Ok_WithoutChanges_DoesNotRewriteModel` | 无改动时确定**不重写**（实例未被替换） |
| 提示 | `TableNotEmptyButNothingUsable_IsShownAsProblem` | **零可用必说成问题**（缺陷 #1 的回归锁） |
| 提示 | `EmptyTable_ShowsEmptyHint` | 空表提示 |
| 提示 | `NewUser_HasNoDefaultPassword_AndSaysSoInIssues` | 新账户不预填默认口令 |
| 口令安全 | `SelectingUser_NeverShowsSecretInPlaintextBox` | 选中已有口令的账户时明文框为空、不回填哈希、"已设/未设"有说明 |

---

## 3. 抓出来的真缺陷 #1：「表非空但一条可用都没有」被显示成常规提示

### 3.1 现象

`RefreshRuntimeUserIssues` 的**方法注释**写着：

> 把校验结果渲染成提示文本。**空表与"表非空但一条都进不去"要分开说。**

但实现里**没有**"表非空但零可用"这个分支：

```csharp
if (issues.Count == 0)
{
    TxtRuntimeUserIssues.Text = _runtimeUsersDraft.Count == 0
        ? 空表提示
        : Format("...table.ok", CountUsable(_runtimeUsersDraft));   // ← 零可用也走这里
    return;
}
```

而 `RuntimeUserValidator.Validate` 的四条规则**都不覆盖"账户被停用"**（停用是合法状态），
所以一张**全部停用**的表返回**零问题**，落进"正常"分支，显示成：

> 共 **0** 个可用账户

且用 **常规色**（`HmiTextPrimarySoftBrush`）渲染。

### 3.2 为什么这是真缺陷（不是措辞问题）

"表非空但没有一条可用"的后果是 **设备端任何账户都登不上、所有权限门永久拒绝**
（`hmi_login` 在 `HMI_SEC_USER_COUNT > 0` 时刻意不授权 —— 见
`docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md` §1.2），
**阶段 12 已经把它判为导出级 Error**。也就是说：

> 面板把一个**阻断级**状态，用**常规色**显示成了「共 0 个可用账户」。

数字确实是 0，但它在"正常"分支、常规色，读起来像"暂时没有账户"。这与本项目一直在收口的
「UI 在撒谎」同族 —— 而且这条更隐蔽：**注释已经描述了正确行为，实现里那个分支不存在**。

### 3.3 修法

1. 新增分支：`draft.Count > 0 && usable == 0` ⇒ 新文案
   `project.settings.security.users.table.none.usable`（说清"N 条账户但一条可用都没有 + 后果 + 会被导出拦下"）。
2. **文案与颜色一起变**：抽出 `SetRuntimeUserIssues(text, isProblem)`，
   出问题用 `Theme.Warning`，空表/正常用 `Theme.TextPrimarySoft`。
   只改文案不改色，用户扫一眼仍会把阻断级问题当成普通说明。
3. 四条规则产出的问题行**同样**走警示色（它们本来就是问题，此前是常规色）。

---

## 4. 抓出来的真缺陷 #2：装载后提示区一直是「用户表为空」

### 4.1 现象

构造函数顺序：

```csharp
InitializeRuntimeUserSection();   // ← 此刻草稿还是空的 ⇒ 提示被写成「用户表为空 —— 设备端不校验凭据…」
InitializeCategoryList();
BindToProject();                  // ← 调 BindRuntimeUsers()，草稿在这里才被填上
WireButtons();
```

而 `BindRuntimeUsers()` 只做三件事：填草稿、重建列表、清空选中 —— **不重算提示**。

于是：**打开一个已经配好账户的工程，提示区显示的是**：

> 用户表为空 —— 设备端不校验凭据，按默认角色直接授权

**两句话都与事实相反**：表不是空的，设备端**会**校验凭据（阶段 6 落地）。
这条提示正是该面板最重要的一行信息（它决定用户是否需要配账户）。

### 4.2 为什么人工验证必然漏掉

它**只在装载那一刻错**：此后随便动一下用户表的任一控件（选中、改名、加一条……）
都会触发 `RefreshRuntimeUserIssues()`，提示立刻被纠正成正确文案。
人工走查的路径通常是"打开面板 → 看一眼 → 点两下试试"，**点完两下错误就消失了**。
只有"打开面板后**不动任何控件**直接读提示区"才能看见 —— 而这恰恰是自动化测试的默认行为。

### 4.3 修法

在 `BindRuntimeUsers()` 末尾补 `RefreshRuntimeUserIssues();`，并把"为什么必须在这里重算"
写进注释（含构造顺序与"只在装载时错"的原因），避免后人当成冗余调用删掉。

---

## 5. 负向验证（5 组，全部命中）

注入器：源码 `replace` 一次 → 跑定向用例 → 断言**期望用例名落在失败行上** → 还原并校验字节一致。

| NV | 注入 | 期望红的用例 |
|---|---|---|
| A | `if (usable == 0)` → `if (false)`（复现缺陷 #1） | `TableNotEmptyButNothingUsable_IsShownAsProblem` |
| B | 从 `BindRuntimeUsers` 去掉 `RefreshRuntimeUserIssues()`（复现缺陷 #2） | `Load_WithConfiguredUsers_ShowsUsableCount_NotEmptyHint` |
| C | 第二层拷贝退化为 `= _runtimeUsersDraft;` | `Apply_WritesModel_AndSecondLayerCopyPreventsAliasing` |
| D | 第一层拷贝退化为直接引用模型对象 | `EditingDraft_DoesNotTouchModel_FirstLayerCopy` |
| E | 去掉 `OnOkClicked` 的 `_hasChanges` 闸门 | `Ok_WithoutChanges_DoesNotRewriteModel` |

A/B 复现的是**本轮修掉的两个真缺陷**，所以它们同时是"缺陷确实被修好"的证据；
C/D/E 钉的是该面板赖以成立的**三条不变量**（两层拷贝 + 无改动不写）。

---

## 6. 验证

| 项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release` | **0 警告 / 0 错误** |
| 全量测试（7 程序集） | **2614 通过 / 0 失败**（App.UiTests 159 → **172**；本轮基线 2601 ⇒ **+13**） |
| 负向验证 | **5/5 命中** |
| 本地化 | zh-CN / en-US 各 +1 键（`project.settings.security.users.table.none.usable`），占位符数量一致 |
| `scripts/ci-gate.sh` | 见 `docs/project-status.md` 验证记录 |

---

## 7. 结论与范围边界

- 登记表 §4 第 16 项（"`ProjectSettingsView` 无行为级 UI 测试"）**已闭环**。
- **本轮不做**：只覆盖了**运行时用户表**这一块（该面板共 6 个分类、1183 行代码）。
  其余分类（导出/显示/通讯/运行时/基本）目前仍只有静态源码扫描守卫
  （`ConfigFieldInventoryTests` / `EditorPreferenceWiringTests`）。
  它们的行为面（下拉项与模型的往返、`NotYetEffective` 标注、屏幕预设联动等）
  属**下一批**候选 —— 基基建已就位，扩展成本比本轮低得多。
- **方法论**：两条缺陷都不是"新功能缺失"，而是**已有实现与它自己的注释/意图不符**。
  这与阶段 7/8/9/12 反复撞见的是同一族（错误注释、未实现的分支、只在一瞬间错的界面）。
  可复用的判据是：**只要一个方法/字段的注释在用"要分开说 / 必须 / 刻意"这类词描述行为，
  就该有一条测试去证明那个行为真的存在。**
