# 应用数据根改名：收口第二个目录名 `HmiEditor`（读回退、写不回退）

> 2026-09-15（第十二轮）。承接第十轮 —— 那次把 16 处路径收进 `AppPaths`，
> 但**有意留下**了第二个目录名 `HmiEditor`，并把它登记进守卫白名单。
> 本轮把这两处（也是白名单里**最后两条**例外）收口。

## 1. 归因：改一次目录名，为什么不能一刀切

第十轮改名（`HmiEditor` → `zhishu-hmi`）时，有两处**各自**拼 `%APPDATA%/HmiEditor`：

| 位置 | 文件 | 内容 | 绕过方式 |
|---|---|---|---|
| `App/Services/LlmConfigStore.cs` | `ai.config.json` | **LLM 端点 + API Key** | `SpecialFolder.ApplicationData` + `"HmiEditor"` |
| `App/MainWindow.axaml.cs` | `audit.log.jsonl` | 审计日志 | 同上 |

一刀切的后果是**静默**的、而且方向相反：

| 处 | 一刀切（直接换新目录）会怎样 |
|---|---|
| `ai.config.json` | 用户**已配置的端点/密钥失效**。对话框照常打开、不报错，只是发出去的请求全都不带凭据 —— 用户会以为"AI 坏了" |
| `audit.log.jsonl` | 审计历史读不到。合规/追溯场景下这是**数据丢失**，而且旧文件仍在磁盘上，只是没人再读它 |

两处都不是"忘了迁"，是**必须先有迁移策略才能迁**。所以第十轮把它们连同理由写进了
`DataRootAllowed` 白名单 —— 白名单在这里的作用不是"豁免"，是**登记待办**。

## 2. 设计：`LegacyDataRoot` / `LegacyDataFile`

```csharp
public const string LegacyAppFolderName = "HmiEditor";       // 历史目录名（只读）
public const string LegacyDataRootEnvVar = "HMI_APP_LEGACY_DATA_ROOT";

public static string LegacyDataRoot      // %APPDATA%/HmiEditor —— 只读，**不创建目录**
public static string LegacyDataFile(string name)             // 与 DataFile 同一个文件名字面量
```

三个决定：

**① 收进 `AppPaths`，而不是让调用方各拼一次。**
守卫要能立一条**没有例外**的不变量（"`SpecialFolder.ApplicationData` 在 `src/` 全域只出现一次"），
历史位置若散在调用方，例外就会随调用方增长。`ResolveUserDataBaseDir()` 因此抽出来给两个根共用
—— 兜底链（`ApplicationData → LocalApplicationData → UserProfile`，全空显式报错）只有一份。

**② `LegacyDataFile(name)` 与 `DataFile(name)` 由同一个文件名字面量推导。**
否则新旧两处各写一份 `"ai.config.json"`，改名时必然只改一处。两处调用方现在都写成：

```csharp
private const string FileName = "ai.config.json";
public static string ConfigFilePath       => AppPaths.DataFile(FileName);
public static string LegacyConfigFilePath => AppPaths.LegacyDataFile(FileName);
```

**③ `LegacyDataRoot` 的 getter 不建目录。** 新装用户根本没有它 —— 创建它反而凭空造出一个空目录。
（一条测试专门断言"解析后该路径仍不存在"。）

## 3. 两处持久化，两种迁移策略

**关键判据是数据形态，不是"统一才好"。**

### 3.1 `ai.config.json`：覆盖式单文件 ⇒ **读回退、写不回退**

```csharp
public static LlmProviderConfig Load()
{
    if (File.Exists(ConfigFilePath))          // 新位置存在 ⇒ 一律用它
        return ReadFrom(ConfigFilePath) ?? new LlmProviderConfig();

    if (File.Exists(LegacyConfigFilePath))    // 只有"新位置还没有文件"才回退
        return ReadFrom(LegacyConfigFilePath) ?? new LlmProviderConfig();

    return new LlmProviderConfig();
}

public static bool Save(LlmProviderConfig config)   // **只写新位置**
```

**最容易写错的一行是回退判据：`File.Exists(新位置)` 还是"新位置读不出来"。**
两者刻意区分开：

| 情形 | 判据取 `File.Exists`（采用） | 判据取"读不出来"（错） |
|---|---|---|
| 升级后第一次运行（新位置无文件） | 回退到历史 ✓ | 回退到历史 ✓（同） |
| 新文件**损坏** | 回默认值，用户重新配置 | **静默启用历史凭据** ✗ |

新文件损坏说明**用户环境出了问题**（比如手改坏了 JSON），此时把历史配置（可能含已废弃的密钥）
"复活"，会让"我改了设置但没生效"这种问题极难排查。**损坏 ≠ 不存在。**

"写不回退"同样重要：写侧唯一 ⇒ 旧文件自然**冻结**，不需要"搬移"这个动作，
也就不存在"搬移途中崩溃"的丢数据窗口。

### 3.2 `audit.log.jsonl`：append-only + 按 Id 去重 ⇒ **新旧都读，不搬移**

侦察到 `AuthenticationService.LoadAuditLogFromFile` 本身就按 Id 去重：

```csharp
var existing = new HashSet<string>(_auditLog.Select(e => e.Id));
foreach (var entry in loaded)
    if (existing.Add(entry.Id)) { _auditLog.Add(entry); added++; }
```

于是接线可以简单到只有"多做一次读"：

```csharp
var auditPath = AppPaths.DataFile(auditFileName);
_authService.AuditLogFilePath = auditPath;          // 写侧：新位置，唯一
_authService.LoadAuditLogFromFile(auditPath);

var legacyAuditPath = AppPaths.LegacyDataFile(auditFileName);
if (File.Exists(legacyAuditPath))
    _authService.LoadAuditLogFromFile(legacyAuditPath);   // 读侧：幂等，重复加载安全
```

- **幂等**来自去重 —— 两个文件都读不会产生重复条目。
- **不搬移**：append-only 日志在旧文件冻结后继续往新文件追加即可。
  **没有"搬移途中崩溃"的窗口**，也不需要一次性迁移脚本。
- 这与 3.1 形成对照：同样的"读回退"，一个必须靠 `File.Exists` 分叉，
  另一个可以无条件多读一次 —— **策略由数据形态决定**。

## 4. 测试基础设施：两个根一起接管

`tests/_shared/TestAppDataRootSetup.cs` 原来只接管 `HMI_APP_DATA_ROOT`。
本轮**同时接管历史根**，否则"验证回退读"的测试会读到开发者**真实的**
`~/.config/HmiEditor/ai.config.json`（里面可能有真的 API Key），结论随机器而变：

```csharp
RedirectOne(EnvVar,       Path.Combine(baseDir, "appdata"));
RedirectOne(LegacyEnvVar, Path.Combine(baseDir, "appdata-legacy"));  // 同级不同名 ⇒ 保留"两个根确实不同"
```

## 5. 守卫：例外表清空，并**断言它为空**

`AppPathsSingleSourceGuardTests` 三处调整：

| 变更 | 内容 |
|---|---|
| `DataRootAllowed` 清空 | 原两条例外（`LlmConfigStore`、`MainWindow`）随之消失 |
| 新增 `AppDataRootAllowList_IsEmpty_AndAnyEntryPointsToARealFile` | **断言 `Count == 0`**；万一条目，其指向的文件必须存在 |
| 新增 `LegacyDataFolderName_IsNotRecomposedOutsideItsSingleSource` | 历史目录名字面量 `"HmiEditor"` 不得在 `AppPaths.cs` 之外被重新拼出来 |

**「没有例外」本身就是被守的不变量。**
如果只是把白名单条目删掉、不留空表与断言，下次往白名单加一条不会有任何阻力 ——
而"多处各自解析"正是从加第一条例外开始的。判据取**带引号**的名字面量，
不带引号的 `HmiEditor.Core`（命名空间前缀）不会被误报。

## 6. 测试

| 项目 | 新增 | 守什么 |
|---|---|---|
| `Core.Tests/AppPathsTests` | 3 条 | 历史根用**旧目录名**（与当前名不同、同父目录）；`LegacyDataFile` 与 `DataFile` 同文件名字面量；**解析历史根不得建目录** |
| `App.UiTests/LlmConfigStoreTests`（新） | 7 条 | 路径归属；升级回退；**新位置存在时不回退**；**损坏 ≠ 不存在**；两处都无 ⇒ 默认且**无副作用**；**Save 只写新位置、历史文件逐字节不变**；Save 会建数据根 |
| `App.UiTests/DataRootMigrationWiringTests`（新） | 3 条 | 审计日志**写侧**来自当前根（不得来自历史根）；**读侧同时加载当前 + 历史**；扫描面自检 |

`LlmConfigStoreTests` 用一对**隔离根**（从测试已重定向的数据根同级派生），
把两个环境变量临时指过去，`Dispose` 时还原并删除 —— 既不污染共享根，也不留垃圾。
它归入 `AppDataRoot-global` 集合（`DisableParallelization = true`），
与 Core 侧 `AppPaths-global` 同一权衡。

顺带把"找仓库根 + 枚举 App 的 `.cs`"抽成 `AppSourceScanner` ——
出现第二个消费者时复制一份，就等于制造第二套扫描面（`bin/obj` 过滤与根解析会漂移，
而**扫描面本身**正是这类守卫最大的盲区）。

## 7. 负向验证（7 组，逐组注入）

| 组 | 注入（只改这一件事） | 期望红 |
|---|---|---|
| a | `Load` 回退判据改成"新位置**读不出来**"（把损坏当不存在） | 1：`Load_CorruptNewLocation_ReturnsDefaults_NotLegacy` |
| b | `Save` **同时写**历史位置 | 1：`Save_WritesOnlyCurrentLocation_AndLeavesLegacyFrozen` |
| c | `LegacyDataRoot` 解析时顺手建目录 | 1：`LegacyDataRoot_IsResolvedWithoutCreatingTheDirectory` |
| d | 历史目录名换成当前名（`"HmiEditor"` → `"zhishu-hmi"`） | 2：`LegacyDataRoot_DefaultsToTheHistoricalFolderName_NotTheCurrentOne`、`LegacyDataFile_UsesTheSameFileNameAsDataFile` |
| e | 守卫例外表加一条 | 1：`AppDataRootAllowList_IsEmpty_AndAnyEntryPointsToARealFile` |
| f | 审计日志写侧改到历史位置 | 1：`AuditLog_WritesToCurrentRoot_NotTheHistoricalOne` |
| g | 审计日志不再加载历史位置 | 1：`AuditLog_AlsoLoadsTheHistoricalFile_SoUpgradeHistorySurvives` |

实跑结果 **7/7 与期望完全一致**（每组"实际红掉"的集合 = 期望集合，且汇总失败数相符）。

### 7.1 两次"红数少于预期"都查出了真问题

**（一）解析器零匹配。** 首轮 harness 每组都报"实际红掉 `[]`"，但汇总失败数是 1 ——
即**注入其实生效了**，是我的正则没抓到。原因是失败行走势为：

```
[xUnit.net 00:00:00.36]     HmiEditor.App.UiTests.LlmConfigStoreTests.Load_Corrupt… [FAIL]
```

按「行首第一个 token + `[FAIL]`」去抓，`(\S+)` 只能匹配到 `[xUnit.net`，后面接不上 ⇒ 零匹配。
**必须锚在行尾**（`[FAIL]` 是**后缀**），再取它前面那个非空白 token。
—— 判据里那句"汇总失败数必须等于期望条数"的交叉校验救了这一轮；
没有它，"解析不到"会被直接读成"注入无效"。

**（二）一条自己写的弱断言。** NV-d 期望 2 条红、实测 1 条。查清后发现：
`LegacyDataFile_UsesTheSameFileNameAsDataFile` 跑在**覆盖变量**下，两个根由环境变量直接给定，
所以 `Assert.NotEqual(DataFile(name), LegacyDataFile(name))` **恒真** ——
看着像被守，其实没守。修法不是改期望，而是把这一半挪到**默认解析链**下验
（`SuspendDataRootOverrides()` 临时清空两个变量）⇒ 重跑 NV-d 得到 2 条红。

## 8. 验证

- `scripts/ci-gate.sh`：**5 步全过**（脚本语法 21 个 / 路径卫生 562 个文件 / Release 构建 **0 警告 0 错误** / 全量测试 / 固件 host 测试 70+16×3+18×3+9 全绿）。
- 全量测试：**2241 通过 / 0 失败**（7 项目：44 + 22 + 75 + 64 + 272 + 129 + 1635；基线 2227，**+14**）。
- 分项：`AppPathsTests` 10 → **13**；守卫 6 → **7**；`HmiEditor.App.UiTests` 119 → **129**（`LlmConfigStoreTests` 7 + `DataRootMigrationWiringTests` 3）。
- 注入实验全部还原后逐字节校验一致，工作区无残留（`git status` 只有本轮预期改动）。

## 9. 遗留

1. **旧目录不会被清理。** 本轮只保证"读得到"（用户不丢配置），
   **没有**删除 `%APPDATA%/HmiEditor`。这是有意的：删用户数据不可逆，
   且旧文件留着当"影子备份"成本极低（两个小文件）。
   若日后要清，判据应是"新位置已存在且可读" —— 但需要先有产品决策。
2. **`TempRoot` 直下的稳定名目录仍无清理入口**（`so-build-<guid>` / `written-<guid>` 这类
   第十轮以前平铺的旧产物，不在会话层 `.sessions/` 扫描面内）。
   见 [`docs/2026-09-15-app-paths-session-lifecycle.md`](2026-09-15-app-paths-session-lifecycle.md)。
