# HMI 编辑器本地化任务总结报告

**任务**：全量中文本地化（zh-CN / en-US 双语资源）
**范围**：`HmiEditor.App` 全部视图 + `HmiEditor.Core` 本地化服务
**状态**：✅ 完成（构建 0 错误 0 警告）

---

## 一、交付内容

### 1. 新增文件（2）

| 文件 | 职责 |
|---|---|
| `src/HmiEditor.App/Resources/Lang.cs` | 本地化键常量类（662 个键名） |
| `src/HmiEditor.App/Resources/AppLocalization.cs` | 全局静态服务访问点（`AppLocalization.Service`） |

### 2. 修改文件（主要）

| 文件 | 改动 |
|---|---|
| `HmiEditor.Core/Services/LocalizationService.cs` | 字典扩充至 727 键 × 2 语言段；新增 `Format()` 扩展方法 |
| `HmiEditor.App/MainWindow.axaml(.cs)` | 菜单/工具栏本地化绑定；`LocService` 静态访问点 |
| 36 个视图 XAML | 全部硬编码文本 → `{Binding [key], Source={x:Static res:AppLocalization.Service}}` |
| 全部视图 code-behind | 用户可见字符串 → `AppLocalization.Service["key"]` / `.Format("key", args)` |

---

## 二、新增键值对统计（727 键 × 2 语言段 = 1454 条目）

| 类别 | 键数 | 覆盖界面 |
|---|---|---|
| sim.* | 46 | 离线仿真 |
| script.* | 44 | Lua 脚本编辑器 |
| recipe.* | 43 | 配方编辑器 |
| user.* | 42 | 用户管理 |
| ai.* | 39 | AI 助手 |
| alarm.* | 37 | 报警配置/列表 |
| prop.* | 35 | 属性面板 |
| gen.* | 31 | 通用操作/状态 |
| win.* | 31 | 全部窗口标题 |
| project.* | 29 | 项目菜单/工具栏 |
| trend.* | 23 | 趋势图配置 |
| comm.* | 21 | 通讯配置 |
| menu.* | 20 | 主菜单 |
| param.* | 18 | 通讯参数 |
| tag.* | 17 | 标签监视器 |
| msg.* | 15 | 通用消息 |
| panel.* | 15 | 面板标题/属性字段 |
| asset.* | 11 | 资源管理 |
| font.* | 10 | 字体子集化 |
| 其余 19 类 | 158 | 屏幕/图层/登录/主题/审计/导航/样式等 |
| **合计** | **727** | |

**数据流验证**：zh/en 两段键集合完全一致；含 `{0}`/`{1}` 占位符的键编号连续、两语言段数量对齐；无空值、无残留 TODO。

---

## 三、修复的问题（6 项）

| # | 问题 | 影响 | 修复 |
|---|---|---|---|
| 1 | **字典重复键**（`screen.switch` 出现 4 次） | `Dictionary` 初始化抛异常 → 本地化服务加载失败，界面显示原始英文键名（如 `tb.search.hint`） | 从干净版本重建字典，脚本去重 |
| 2 | **CS0236 字段初始化器冲突** | `LocService = Resources.AppLocalization.Service` 中 `Resources` 与基类属性冲突，编译失败 | 改为全限定名 `HmiEditor.App.Resources.AppLocalization.Service` |
| 3 | **`\n` 换行转义被转成真实换行**（`ai.icon.placeholder`、`script.no.function`） | C# 字符串跨行 → CS1010/CS1003 编译错误（最多 1322 个） | 改为字面 `\n` 转义序列 |
| 4 | **中英语义不一致**（`sim.wave.const`：zh "常量" vs en "Constant Wave"） | 与"正弦波/方波"命名不平行，易与 `sim.const` 混淆 | 中文改为"常量波形" |
| 5 | **CS8604 空引用警告**（`Format(key, result.Error)`） | 编译器警告 | 参数补 `?? ""` |
| 6 | **并行编辑竞态**（子代理并行改文件导致替换丢失） | 部分替换未生效 | 改为顺序逐条重放 |

---

## 四、提交记录（5 次）

| 提交 | 说明 |
|---|---|
| `0b6a1c2` | 全量中文本地化：36 个 XAML + code-behind 迁移至资源字典，字典扩充 650+ 键 |
| `7660d2b` | 修复字典重复键导致初始化失败；补齐全部 662 键到两段字典（占位值） |
| `7f0c61b` | 批量翻译首批 163 键（gen/tb/prop/var/alarm/comm/ai/param） |
| `23afdd4` | 批量翻译 163 键（recipe/screen/script/style/sim/tag.monitor） |
| `02e226e` | 翻译 127 键（prop/var/win + gen 收尾），全部 662 键翻译完成 |

---

## 五、遗留事项

> 状态以 `docs/2026-09-17-open-items-register.md` 为准（本清单只保留说明，不再各自维护状态）。

1. **运行时动态文案**（未入字典）：~~ToolboxView 控件显示名、PropertyPanelView 表达式错误、AiChatPanelView 生成中状态、UserManagerView 审计文案、AnimationEditorView 缓动函数选项等~~
   **⚠ 2026-09-17 复核：上述举例中「ToolboxView 控件显示名」与「AnimationEditorView 缓动函数选项」
   早已在用字典键**（`Lang.TbScreen` / `Lang.AnimEasingLinear` 等）—— 照这份举例动手会白跑。
   真实剩余为 **226 条 / 27 个 .cs 文件 + 14 条 / 2 个 .axaml**（后者已于本轮本地化），
   已逐文件登记进 `docs/2026-09-17-localization-hardcoded-inventory.md` §3。
   **仍开着**（按文件分批做；总量已由 `HardcodedLocalizationGuardTests` 锁成只减不增）。
2. ~~**语言切换即时刷新**：`LocalizationService` 未实现 `INotifyPropertyChanged`，XAML 绑定在运行时切换语言后不自动刷新（已有 `LanguageChanged` 事件与 `SwitchLanguage`，需代码手动刷新界面）。~~
   **✅ 2026-09-17 已解决（阶段 13）**，且实测比原文更严重：不只是"没实现 INPC" ——
   **Avalonia 的索引器绑定根本不订阅 INPC**，所以"补上 INPC"并不能修好它。
   全仓 **1287 处** `{Binding [key], Source={x:Static res:AppLocalization.Service}}` 实为**不可观察**；
   只有 `MainWindow` 手动重建 Dock 面板兜底，于是**菜单栏/工具栏切语言后一直是旧语言**。
   修法：新增 `LocExtension`（`{res:Loc key}`）把绑定改成"绑到会通知的 `LocalizedString.Value`"，
   1287 处一次性迁移；`AppLocalization.Get` 按 key 缓存实例（顺带修掉一个订阅只增不减的潜在泄漏）。
   有正面对照实验（同宿主里旧写法确实不刷新）+ 回归守卫（不得再写回旧写法）。
   见 `docs/2026-09-17-open-items-register.md` §6。
3. **en-US 翻译质量**：部分 en-US 值为精简翻译，未逐字校对，可后续完善。**仍开着。**
