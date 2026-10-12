# 运行时用户表的编辑器 UI —— 从「模型有、没人能配」到「界面能配且配不错」

**日期**：2026-09-16
**承接**：阶段 6（运行时凭据校验）遗留 2。

## 1. 症状与落点

上一轮（阶段 6）把凭据链路打通了：用户表进工程 → 下发进 `generated_security.h` →
设备端真校验 → 模拟器 `--login` 选预览身份。但**没有任何界面能配账户** ——
`SecurityConfig.RuntimeUsers` 只能手工编辑工程 JSON，而手写的哈希要先算
`Base64(SHA256 迭代 1000 次(salt ‖ utf8(pwd)))`，实际上等于配不了。

落点选**工程设置 → 安全设置面板**，而不是新开一个对话框：

| 理由 | 说明 |
|---|---|
| 语义 | 用户表随工程保存（`RuntimeUsers` 是工程级配置），`ProjectSettingsView` 的职责正是「统一管理 HmiProject 所有工程级配置项」 |
| 现成语义 | 该对话框已有 `_hasChanges` + 确定/应用/取消 的完整保存链，不必再造一套 |
| 已有邻居 | 安全面板里已经有操作模式、会话超时、密码策略、锁定策略 —— 用户表正好是这些策略的**作用对象** |
| 减少改动面 | 新对话框要额外接菜单项、命令、快捷键；本轮不碰这些 |

**刻意不混进 `UserManagerView`**：那是编辑器**自身**的账户管理，走
`AuthenticationService` 的 `UserProfile`（进程内、不落工程）。两者是**安全边界**关系，
共用界面等于诱导用户以为「这里建的用户设备上也能登」。

## 2. 三个关键设计决定

### 2.1 草稿语义：两层拷贝，缺一层都不行

对话框的取消语义建立在「模型只在 `SaveToProject()` 里被写」之上（其余控件都是"控件中介"）。
用户表是**集合**，绑不到单个控件上 ⇒ 必须引入编辑草稿：

```csharp
private List<RuntimeUser> _runtimeUsersDraft = new();
```

- `BindRuntimeUsers()`：模型 → 草稿（`u.Clone()` 深拷贝）
- `SaveRuntimeUsers()`：草稿 → 模型（**再 `Clone()` 一层**）

**两层拷贝都是必需的**：

| 缺哪层 | 后果 |
|---|---|
| 缺第一层（草稿直接用模型对象） | 编辑期就在改模型 ⇒ 点「取消」改动已生效 —— "取消了却没取消掉"比不做还糟 |
| 缺第二层（草稿列表直接赋给模型） | 模型与草稿**共享同一批对象引用** ⇒ 点完「确定」再接着编辑，仍会改到模型 |

`RuntimeUser.Clone()` 放在模型里而不是让调用方手写字段拷贝：手写的拷贝方法在本类
新增字段后**仍然编译通过**，只是静默漏拷一个字段（表现为"填了没生效"或"取消了又回来了"）。

### 2.2 密码：点按即哈希，明文只存在一瞬

界面里**不出现密码哈希**，只显示「已设密码 / 未设密码」。明文只在输入框里存在到点下
「设置密码」为止：

```
输入明文 → 校验长度 → RuntimePasswordHasher.Hash(pwd) → 写入草稿 → 清空输入框
```

好处是明文既不进草稿、也不进任何列表项对象。

- **新增用户刻意不给默认口令**：`PasswordHash = ""`，问题行会明说「已启用但未设密码 ——
  该账户无法登录」。预填一个默认口令比"暂时登不上"危险得多 —— 默认口令几乎不会被改。
- **长度下限取当前面板里的 `NumPwdMinLen`**（不是模型里的旧值）：用户可能刚改过策略还没保存。
  改了策略会立即刷新输入框的提示语（`NumPwdMinLen.ValueChanged` → `UpdateRuntimeUserPasswordHint`）。

### 2.3 角色下拉：只认预置角色表，且只有一份

选项来自 `SecurityConfigCodeGen.BuiltInRoles`（本轮已把它的实现收敛为
`UserRole.BuiltInAll` 的**纯转发** —— 唯一清单在 Models 层，因为 Security 层与 App 层
都要读它）。让用户自由填角色 Id 只会得到「口令对了也登不上」：固件 `hmi_role_lookup`
`strcmp` 查不到就返回 -1，`hmi_login_as_role` 直接拒绝。

## 3. 校验规则：四条，判据与固件同源

`HmiEditor.Core/Security/RuntimeUserValidator.cs` 返回**结构化问题**（枚举 + 下标 + 用户名），
不产文案 —— 文案由 App 层映射（`RuntimeUserIssueText`）。这样 Core 侧断言的是枚举（稳定），
改一次措辞不会红一片测试。

| 问题 | 固件侧依据 | 后果 |
|---|---|---|
| `EmptyUsername` | `hmi_user_lookup` 用 `strcmp`，空输入 `strcmp("", "") == 0` | 提交空用户名会**命中**该条目（不是"死条目"，见 §4） |
| `DuplicateUsername` | 线性查表 + **首个匹配胜出** | 后面那条永远匹配不到 —— 改它也毫无效果（最隐蔽的"改了没反应"） |
| `UnknownRole` | `hmi_role_lookup` `strcmp` 查不到返回 -1 | 即使口令正确，`hmi_login_as_role` 也拒绝 |
| `EnabledWithoutPassword` | `hmi_verify_password` 解析空哈希即失败 | 该账户无法登录（不是免密放行） |

**判据必须与固件同源**：用户名与角色 Id 的比对一律 `StringComparison.Ordinal`
（固件是 `strcmp`，大小写敏感）。若用 `OrdinalIgnoreCase`，编辑器会：
- 把 `Op` / `op` 这对「在设备上其实是两个不同用户」误报成重名；
- 或反过来放过 `ROLE-OPERATOR` 这种**表外**角色。

测试对这两种形态都有反例钉住（`UsernameCaseDifference_IsNotDuplicate`、
`UnknownRole_Reported` 的 `ROLE-OPERATOR` 档）。

**「停用且无密码」不算问题**：停用账户在设备端直接拒绝，无密码与否不影响可达性。
算成问题会让"我特意停用留着以后启用"的账户一直飘着一条红字。

列表项的状态点用**与设备端同口径的"能不能进得去"**：
`非空用户名 && 角色在表内 && 已启用 && 有密码`（= `RuntimeUserValidator.CountUsable`）。

## 4. 顺带修掉一处**错误的**诊断注释

阶段 6 在 `SecurityConfigCodeGen` 里为「用户名为空」发的 C 注释是：

> `/* ⚠ 存在用户名为空的条目 —— 设备端任何输入都匹配不到它（死条目） */`

**这是错的。** 复查 `hmi_actions.c`：`hmi_login_submit` 只挡 `user == NULL`，
不挡空串；而 `hmi_user_lookup("")` 会与空用户名条目 `strcmp("", "") == 0` **命中**。
也就是说空用户名条目**比正常条目更容易被命中**（任何空提交都落在它身上），
只是仍需通过口令校验。

改后的注释与 `SimulatorProjectExporter` 里同一处措辞都按实际行为重写。
`RuntimeUserValidator.EmptyUsername` 的文档注释里也写清了这一点。

> 教训：把"我没验证过"的行为写进注释，比不写更糟 —— 它会被后来人当事实用。
> 判据要落到 `strcmp` 这种可指认的代码位置上，而不是"显然匹配不到"的直觉。

## 5. 已登记的口径差异：字符数 vs 字节数

`SecurityConfig.PasswordMinLength` 的模型语义是**字符数**，编辑器 UI 按字符数校验
（`password.Length`）。固件 `hmi_login_submit` 用的是 `strlen(password)`，即 **UTF-8 字节数**：

| 密码 | 编辑器（字符） | 固件（字节） | 结果 |
|---|---|---|---|
| `abcdef` | 6 | 6 | 两侧一致 |
| `中文中文` | 4 < 6 ⇒ **拒绝** | 12 ≥ 6 ⇒ 通过 | 编辑器更严 |
| `abc中文` | 6 ≥ 6 ⇒ 通过 | 9 ≥ 6 ⇒ 通过 | 两侧一致 |

偏差方向是**设备端更宽松**（字节数 ≥ 字符数），不会造成「编辑器放行、设备拒绝」这种
危险方向；实际影响是**中文密码在设备端比预期更容易满足下限**。

本轮**不修**（改哪侧都有代价：改固件要按 UTF-8 计码点、改模型语义要动全工程），
在此登记。真要统一，应统一到**字节数**（因为固件是判据的最终执行者，
而模型语义可以改注释）。

## 6. 验证

| 项 | 结果 |
|---|---|
| `dotnet build src/HmiEditor.App -c Release` | **0 警告 0 错误** |
| `RuntimeUserValidatorTests` | 新增 20 项全过 |
| `SecurityConfigCodeGenTests` | 46 项全过（含新增转发守卫） |
| `LocalizationTests` | 22 项全过（zh/en 键数、占位符数量一致） |
| 全量 + 门禁 | 见 `docs/project-status.md` 阶段 7 |
| 本地化键 | zh-CN / en-US 各 +25（2028 → 2053），**锚点插入不重排文件** |

本地化用锚点文本插入而不是 `json.dump(sorted(...))`：现有 `Strings.*.json`
整体**不是**字典序（`project.settings.*` 段是，整份不是），排序写回会把 2028 个键
全部重排、产出几千行假 diff。

## 7. 遗留

> 状态以 `docs/2026-09-17-open-items-register.md` 为准（本清单只保留说明，不再各自维护状态）。

1. ~~**界面无自动化测试**：App 层没有 `ProjectSettingsView` 的测试基建（App.UiTests 里零覆盖），
   本轮把可测逻辑抽进了 Core（`RuntimeUserValidator`），但"草稿语义 / 两层拷贝"
   这类绑在对话框生命周期上的行为仍只有人工验证。~~
   **✅ 2026-09-17 已解决**：新增 `ProjectSettingsRuntimeUserUiTests`（13 项行为测试，
   真的把面板 `Show()` 出来、按真实控件走完装载→编辑→取消/确定/应用），
   并**当场抓出 2 个真缺陷**：① 「表非空但零可用」被显示成常规提示「共 0 个可用账户」
   （方法注释写着"要分开说"但那个分支不存在，而该形态是阶段 12 的导出级 Error）；
   ② `BindRuntimeUsers` 不重算提示 ⇒ 打开**已配账户**的工程时提示区显示
   "用户表为空 —— 设备端不校验凭据"（**两句话都与事实相反**，且只在装载那一刻错，
   动一下控件就被纠正 ⇒ 人工验证必然漏掉）。见
   `docs/2026-09-17-projectsettings-runtime-user-ui-tests.md`。
2. §5 的字符数 vs 字节数口径差异。**仍开着**（见登记表 §4 #18）。
3. ~~运行时用户表的**导出前校验**未接进 `ValidateHardwareTarget` 那条链
   （目前只在面板内提示，导出时不拦）。~~
   **✅ 2026-09-17 已解决（阶段 12）**：接进了 `GeneratedCodeValidator`
   （`FindingCategory.RuntimeUser` + `ValidateRuntimeUsers`），而非 `ValidateHardwareTarget` ——
   后者是"设备兼容性"校验，职责不同；导出闸门本来就在 `GeneratedCodeValidator` 上。见
   `docs/2026-09-17-config-honesty-export-gate-and-ai-tool.md`。
