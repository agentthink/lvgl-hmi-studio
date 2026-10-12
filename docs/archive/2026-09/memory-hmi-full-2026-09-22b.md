# 智数 HMI 长期笔记

> ⚠ 注入上限 **8,000 字符**（按字符，超了静默截断）——细节析出 `docs/`，这里只留契约 + 指针。
> 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-22.md`；数字以 `project-status.md`、未闭环以登记表为准。

## 1 设备 / 构建
- `/rom` 只读 ⇒ `/usr`、`/etc` 重启即失；唯一持久分区 `/mnt/UDISK`。抓帧 `dd if=/dev/fb0` 只读首缓冲 ⇒ 先看 `fb0/pan`。
- 收帧唯一 `CommandFrameCollector`（静默从最后一段数据起算）；会话超时顺序 `评估 → 打点 → 判定`。
- Tina：`make package/hmi-runtime/compile V=s` + `pack`；SPI NAND 须 `openixSuit`；`lv_conf.h` 以 `src-config/` 为准。
- 同一 `.axaml` 只声明一次（重复键 ⇒ UI 测试死在 `SetupUnsafe`）。
- 🩸 **沙箱 `RLIMIT_AS` 随会话变**（曾硬 4 GiB ⇒ `dotnet build` 必 OOM；今 unlimited）⇒ **先 `ulimit -Hv` 探**，4 GiB 才走 `.workbuddy/tmp/run-outside.sh`（借 `systemd --user`，依赖 D-Bus）。
- 🩸 **编译绑定＝显式声明**：`AvaloniaUseCompiledBindingsByDefault` 不写就靠包内两个**相反**默认 + 导入顺序才是 `true` ⇒ 已显式声明于 `src/Directory.Build.props`，守卫 `XamlCompiledBindingGuardTests`（4 条）。⚠ `XamlDynamicSetters` **不是**证据（XAML 预编译产物）。

## 2 生成链契约（不能回退）
- 净化唯一 `CIdentifier.cs`；`obj_*` 无屏幕前缀 ⇒ 控件 id 工程内唯一；资源符号只在 `Export` 入口调一次。
- 发射清单唯一 `EnumerateRenderableControls` ⇒ 不遍历 `project.Screens[].Controls`；Faceplate 克隆换独立集合。
- 绑定：`BindingVisualProperty` + 发射点 `VisualBindingCodeGen`；`BgColor` 须同设 `bg_opa(COVER)`。颜色唯一 `PaletteDefaults.cs`；`apply_theme_styles()` 须在 `lv_screen_load` 后且不设 `bg_color`。
- 权限唯一 `ActionPermissionMap`/`SecurityConfigCodeGen`；判定只在生成头；门只在 `!= Designer` 发射。
- 下沉/凭据：3 字段 → `generated_runtime.h`；编辑器凭据不下发；`hmi_login` 有表只跳屏不授权；AI enum 源 `PromptTemplateEngine`/`AiSchemaEnums`。
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；`.Format` 与 XAML `{res:Loc}`（恒 0 实参）不得共用同键。
- **角色名/描述＝界面文案，非落盘数据**：生成头只发 `Id/Name/Level/掩码`、`SecurityConfig` **无** `Roles` ⇒ 唯一键推导 `Services/RoleTextKeys.cs`（用模型 `.Id`，表外回落 `role.Name`）；模型 `Name` 只留给生成头与调试日志。**别改回模型**（正是 #54）。
- 详规 ⇒ `docs/…expression-driven-visual`、`…permission-model-sinking`、`…runtime-*`、`…localization-placeholder-closure`、`…ai-issue-localization`（均 `.md`）

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- `ItemsSource` 直接装模型对象且**无 `ItemTemplate`** ⇒ 兜底 `ToString()` 拿到**类型全名**（**不是**「英文名」）⇒ 建 `ComboBoxItem{Content,Tag}` 或给 `ItemTemplate`；`ComboBoxItem` **是控件** ⇒ 两个下拉**各建一批**。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。
- 🩸 **窗口有「设计器 / 生产」两条构造**（`WindowViewSmokeUiTests` 只覆盖前者：`_x = null!`、不订事件、不装数据）。`WindowProductionPathUiTests` 用**形状签名**区分 —— 不能只看「null → 非空」（**有值但为空**会静默放过）⇒ 必须带「集合元素个数」。

## 4 测试纪律 / 环境

### 4.1 通用
- 临时目录/随机红见技能 `dotnet-test-tempdir-hygiene`；跑 `git` 的目录须在工作树外；数据根改名读回退、写不回退。
- 跑全量必走 `scripts/ci-gate.sh`；「卡住」看日志 mtime。**引用「绿」前先核时序**（`stat -c %y` 对 `git log --date=iso-strict`）；间歇缺陷**「有一次绿」零信息**。
⚠ 对拍差异要能归因到**本阶段**（曾把 `637→638` 记成自己的新测试文件，实为上一阶段的新脚本：`check-path-hygiene.sh` 扫描面**排除 `tests/`**）；**NuGet restore 状态行**先剥。
- **先量复现率再谈修法**；**一次只放开一个变量、两侧同一条命令** —— 并**验证变量真的动了**（🩸 `-p:` 改不动 xunit ⇒ 阶段 57 那条否证**从未执行**）。
- **守卫与被守对象必须同源**；**做不出负控的判据不发布**。负控成对：**同一注入**下新写法**绿**、旧写法**红**。
- 🩸 **判据的输入面不得含「定义面」**：允许「输入表本身」当满足证据，判据就被那张表养着（补键工具的 `VALUE_MAP` 不是证据）。
- 🩸 **判据必须自治：不许对共享资源做前后快照比较**（`TestTempDir.Root` 进程共享、被并行写 ⇒ 单跑绿、进门禁红）⇒ **按内容找唯一标记**；**刻意不递归**。
- 🩸 **负控还原别用 `shutil.copy2`**（连 mtime 复制 ⇒ MSBuild 不重建 ⇒ **正控跑的是负控的二进制、假红**）⇒ **写回原文**。
- **负控判红谓词**要匹配测试汇总行**且**取到 `错误消息:`（细节见技能 `static-guard-authoring`）；认不出归 `unknown`（`build-fail` **不算数**）。
- **子串定位要带换行边界**：`## 验证记录` 是 `### 验证记录` 的子串 ⇒ 自检**假失败**。
- 🩸 **判据别量自己不该管的量**（#55）：原断言把「记下了 150」与「其后无采样」捆住（`DataLogger` 定时器决定）⇒ 派发拖后就红。**只留「存在一条 > 100」**。先问「这件事该由这条判据管吗」。
- 🩸 **仓库根不能全量 `os.walk`**：`firmware/`（Tina SDK）＝ **174 万文件**，全量 walk 被 SIGTERM(137)；点目录 bind-mount 会卡死 ⇒ 分析面只走 `src/ tests/ docs/ tools/`。
- 🩸 **阶段 69 两坑**：① `XDocument` 元素面免疫注释 ⇒ 剥注释的是**子串面**；② **`dotnet test` 先 build** ⇒ 测「改了没重建」要 `--no-build`。

### 4.2 Avalonia / headless
> 读数 / 探针现场 / 已否证的解释 / IL 普查 / 字体记账 / 墙钟负载 ⇒ `docs/2026-09-22-headless-dispatch-and-font-accounting.md`
- 会话＝ **`PerTest` 隔离**：每次 `Run` 重置全局状态 + 重建平台。`s_uiThread` 由「Reset 后**第一个**构造 Dispatcher 的线程」抢下 ⇒ **任何线程读一次 `Dispatcher.UIThread` 就抢走它**（#47）。⚠ 该 getter 在 `s_uiThread==null` 时当场构造 ⇒ **探针别读它**（反射 `TryGetUIThread()`）；判「在不在会话线程上」用 `SessionThreadId`。
- 🩸 **并发派发是确定存在的竞态前提**：`DispatchCore` 在**调用线程**上跑全进程级、无锁的平台重置，xunit 默认并行 ⇒ 实测 `maxInFlight=20`。**闸门已落地**（`Run/RunAsync` 包 `SemaphoreSlim`）⇒ **20 → 1**、代价为零。守卫 `HeadlessDispatchContractTests`。诊断默认关：`HMI_DIAG_OUT`。
- **`Pump` 也在闸门里**（按调用线程取 Dispatcher），但**会话线程上必须旁路**（纳闸必自锁）；#47 偶发红（阶段 76 又命中 1 次，≈2/23）—— 窗口由「每次派发重置」造成，**修法不落地**，判据 `CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`。
- **#45 挂死 0/5**（阶段 52 是 4/4）⇒ 根因未确证，独占进程的规避**保留**。
- headless 走 **`HeadlessRenderTimer`**（挂 Dispatcher 的 `DispatcherTimer`），**不是** `SleepLoopRenderTimer`；`DefaultRenderLoop` **不是**静态单例。
- 🩸 **headless 布局死区**：`ScrollViewer`（垂直可滚）+ `Wrap` 多行 `TextBlock` ⇒ **首次布局不收敛**、挂死（`124`），真机不挂。**垂直 `Disabled`/`NoWrap` 收敛** ⇒ 接缝＝**造窗/显示分开**，⚠「会挂死」≠「测不到」（阶段 75）。详 `…scrollviewer-wrap-hang.md`。
- `ShowDialog` 不阻塞、弹窗同步进 `OwnedWindows`。
- 🩸 **`DrawText` 单次分配环境相关**（`2,604+416×N`）⇒ 预算判据**别跨配置比绝对字节**；`PerTest`/预算不动，`416` 出处**未闭环**（详 headless 专题）。
- **接线两类「双路」**：① 控件事件 `.axaml` 声明 + `.axaml.cs` 再 `+=`（Avalonia **不去重**）⇒ `check-xaml-duplicate-wiring.py`（门禁 `[1d]`）；② 视图事件转发静态总线 **+** 宿主直订视图（**28 处**，非幂等 5 处会写坏数据）⇒ #46 **已闭环（留总线删直订）**，`check-dock-bus-only-subscription.py` 接 `[1e]`。

### 4.3 动作面守卫
- 方法论全在技能 `static-guard-authoring`（分桶 + 双向差集；集合属性元素不进逻辑树 ⇒ 取实例走 `FindControl`；开面板须独占进程（#45）；绿判据的读数出口 `JudgeReadings`（`HMI_READINGS_OUT`，**零副作用、不做成断言**）⇒ 跨阶段对比**先 `sort` 再逐行 diff**，且**剥掉墙钟**才看得出「逐条同数」）。落地 ⇒ `…menu-`/`…dialog-inner-`/`…mainwindow-toolbar-`/`…dockpanel-invocation-net.md`。
- 🆕 **`[1f]` 位置找控件**：`check-index-based-control-lookup.py`（阶段 77 起**两级都硬判 0**、基线已空；阶段 78 加**位置写入**那一半 `RemoveAt(<字面整数>)`）。🩸 **清空基线 ≠ 只改口径**：非空基线兼着「正则改坏 ⇒ 全条目消失 ⇒ 红」的 fail-closed ⇒ 换承担者＝`meta_positive_control()` 每次验**四**条坏形状。🩸 **类型导航 / 类型读取不该硬判**（实测全合法）；真漏的是**变量下标**删改 ⇒ 靠**行为判据**兜（`FaceplateParamRefreshUiTests`）。
- 🩸 **「名字即引用」类判据会被自己的文档/字面量满足**（已踩三次）⇒ **#48 已闭环**：三档 `ReferenceFace{Raw, NoComments, Code}`，生产判据走 `Code`（`.cs` 剥注释+抹字面量、保留插值孔；`.axaml` 剥 XML 注释）。**「引用」＝代码标识符**；散文/失败信息/CSV 表头/控件名/合成夹具都不算。配套：语义判据**两侧都断言**（片段用不存在的名字）；**单调性**逐级真的变窄。⇒ 注释里可放心写常量名。

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典
- **键可达性** = **有代码引用**的常量值 ∪ **八机制**（M1 `Lang` 常量 / M2 axaml `{res:Loc}` / M3 插件族派生（复用 `DerivedKeys()`）/ M4 `cfg.note` / M5 脚本诊断码 / M7 枚举名小写 / **M8 AI 诊断码**（`AiIssueCodes`→`ExternalText.AiIssueKey`）/ **M6 整键字面量＝代理、排最后**），或进 `KeptUnreachable`（**两侧相等判定 ⇒ 反向也红**）。守卫 `LocalizationKeyReachabilityGuardTests`。🩸 **代理机制必须写明「它看不见什么」**：模板拼接 `$"…{…}…"` M1–M6 全看不见 ⇒ **表不全就报红**。豁免表**不许硬塞**。
- 🩸 **#49（已闭环）的教训**：判据的**担保方自己不能是死的** —— M1 曾只问「常量还在不在」，于是死常量替死键做担保（阶段 64 已收紧）。
- **收紧口径时先查下界/基线**：两处 `1800` 因口径换了被撞穿。下界必须跟着口径走。
- **字典处置链**：删字典键 ⇒ **必须连 `Lang` 常量一起删**（`LangKeyCoverageTests`）+ 清 `sync_localization.py` 的 `VALUE_MAP`（**双向**工具）+ 同步 `UnusedBaseline`。`param.*` **只增不删**（计数别抄进本文件）。
- **`UnusedBaseline` 不是清理清单**：1125 = M2 828 / M6 274 / M3 18 / 无机制 5（= `KeptUnreachable`）；前两桶**键都活着**，只是不「读常量」⇒ 常量是**键空间镜像**，基线语义 = 「**新增一个没人读的常量即红**」。⚠ 面读数**跨口径不可比大小**（M6 的输入集是前面机制的**残差键**）。新增机制要核**别的面没跟着涨**（涨了＝新键有隐形担保）。
- **`sync_localization.py`（#52，已闭环）**：第 2 步会给「字典有、`Lang.cs` 没有」的键**生成常量**（干跑 **360** 个 ⇒ 基线 → ≈1485 ⇒ 红）。处置＝**只堵陷阱不动基线**：默认只干跑（落盘要 `--write`）、生成常量另要 `--add-consts`；守卫**锚点必须落在赋值表达式上**（落旗标字符串会被工具自己的文件头注释满足）。
- **接线后必须删差异表行**：`KeptUnreachable` 是**判据的例外**不是豁免 —— 双向判定在「变得可达」或「从字典消失」时红（接上 3 条 `role.*` 时 9→5）。
- **资源键（`x:Key`）双向闭合**：定义 ⇒ 有人用（差异表 3 条）；引用面 **5 类**（+`SetOrUpdate*(r,"K")` 被管理 ≠ 被读；+`Theme.Brush("K")`）。⚠ `PathIcon` 用 `Fill` ⇒ 描边图标渲染色块/空白，换 `Path` ≠ 内联几何。
  🩸 取值**两口径**：索引器 `resources[key]` **不查合并字典**、`TryGetResource` 查 ⇒ 合并键「查得到、索引到 null」⇒ NRE（**只认 `out` 值**）。`StreamGeometry.ToString()` 是**类型名**、无「几何→文本」API ⇒ 比几何内容只能比**源文本**。⇒ `…toolbox-icon-single-source.md`
- **字典 JSON 不是排序存放的** ⇒ `json.dump(sorted(...))` 重写产生 **1701 行** diff。做法：逐行手术 + `assert count == 1` + 备份 + 回读。
