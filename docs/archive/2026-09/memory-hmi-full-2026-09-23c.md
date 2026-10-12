# 智数 HMI 长期笔记

> ⚠ 注入上限 **8,000 字符**（按字符，超了静默截断）——细节析出 `docs/`，这里只留契约 + 指针。
> 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23b.md`；**析出的细节** ⇒ `docs/2026-09-23-memory-detail-extract.md`；数字以 `project-status.md`、未闭环以登记表为准。

## 1 设备 / 构建
- `/rom` 只读 ⇒ `/usr`、`/etc` 重启即失；唯一持久分区 `/mnt/UDISK`。抓帧 `dd if=/dev/fb0` 只读首缓冲 ⇒ 先看 `fb0/pan`。
- 收帧唯一 `CommandFrameCollector`（静默从最后一段数据起算）；会话超时顺序 `评估 → 打点 → 判定`。
- Tina：`make package/hmi-runtime/compile V=s` + `pack`；SPI NAND 须 `openixSuit`；`lv_conf.h` 以 `src-config/` 为准。
- 同一 `.axaml` 只声明一次（重复键 ⇒ UI 测试死在 `SetupUnsafe`）。
- 🩸 **沙箱 `RLIMIT_AS` 随会话变**（4 GiB 时 `dotnet build` 必 OOM）⇒ **先 `ulimit -Hv` 探**；4 GiB 才走 `.workbuddy/tmp/run-outside.sh`（借 `systemd --user`）。
- 🩸 **编译绑定＝显式声明**：`AvaloniaUseCompiledBindingsByDefault` 不写就靠包内两个**相反**默认 + 导入顺序才是 `true` ⇒ 已显式声明于 `src/Directory.Build.props`（守卫 `XamlCompiledBindingGuardTests`）。

## 2 生成链契约（不能回退）
- 净化唯一 `CIdentifier.cs`；`obj_*` 无屏幕前缀 ⇒ 控件 id 工程内唯一；资源符号只在 `Export` 入口调一次。
- 发射清单唯一 `EnumerateRenderableControls` ⇒ 不遍历 `project.Screens[].Controls`；Faceplate 克隆换独立集合。
- 绑定：`BindingVisualProperty` + 发射点 `VisualBindingCodeGen`；`BgColor` 须同设 `bg_opa(COVER)`。颜色唯一 `PaletteDefaults.cs`；`apply_theme_styles()` 须在 `lv_screen_load` 后且不设 `bg_color`。
- 权限唯一 `ActionPermissionMap`/`SecurityConfigCodeGen`；判定只在生成头；门只在 `!= Designer` 发射。
- 下沉/凭据：3 字段 → `generated_runtime.h`；编辑器凭据不下发；`hmi_login` 有表只跳屏不授权；AI enum 源 `PromptTemplateEngine`/`AiSchemaEnums`。
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；**产出方给「码+实参+中文回落」**（Core 不引本地化；阶段 81）；`.Format` 与 XAML `{res:Loc}`（恒 0 实参）不得共用同键。
- **角色名/描述＝界面文案，非落盘数据**：生成头只发 `Id/Name/Level/掩码`、`SecurityConfig` **无** `Roles` ⇒ 唯一键推导 `Services/RoleTextKeys.cs`（用模型 `.Id`，表外回落 `role.Name`）。**别改回模型**（#54）。
- 详规 ⇒ `docs/…expression-driven-visual`、`…permission-model-sinking`、`…runtime-*`、`…localization-placeholder-closure`、`…ai-issue-localization`

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- `ItemsSource` 装模型对象且**无 `ItemTemplate`** ⇒ 兜底 `ToString()` 得类型全名 ⇒ 建 `ComboBoxItem{Content,Tag}` 或给 `ItemTemplate`；`ComboBoxItem` 是控件 ⇒ 两个下拉各建一批。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。
- 🩸 **窗口有「设计器 / 生产」两条构造**（`WindowViewSmokeUiTests` 只覆盖前者）；`WindowProductionPathUiTests` 用**形状签名**区分 —— 不能只看「null → 非空」（有值但为空会静默放过）⇒ 须带集合元素个数。

## 4 测试纪律 / 环境

### 4.1 通用
> 判据写法（负控/输入面/自治/观测出口/豁免…）与坑的**全文** ⇒ `docs/2026-09-23-memory-detail-extract.md` §4；技能 `static-guard-authoring`。
- 跑全量必走 `scripts/ci-gate.sh`；「卡住」看日志 mtime。**引用「绿」前先核时序**（⚠ `>` 重定向的日志 birth 陈旧，**只认 mtime**）；间歇缺陷**「有一次绿」零信息**。
- ⚠ 对拍差异要能归因到**本阶段**（扫描面排除 `tests/`）；**NuGet restore 状态行**先剥。
- **先量复现率再谈修法**；**一次只放开一个变量、两侧同一条命令**并**验证变量真的动了**（🩸 `-p:` 改不动 xunit）。
- 🩸 **判据四条铁律**：① 守卫与被守对象**同源**；② **做不出负控的判据不发布**（同一注入下新写法绿 / 旧写法红；绿时要排除「注入没生效」——恒等变换、同加后缀＝自收敛）；③ 输入面**不得含「定义面」**（拿输入表自证＝被那张表养着）；④ 别量**自己不该管的量**（#55，先问「这件事该由这条判据管吗」）。
- 🩸 **判据自治**：不许对共享资源做前后快照（单跑绿、门禁红）；**按唯一标记归属** + **枚举候选根** + **只查单方向差集**；输入面自证、刻意不递归。
- 🩸 **观测出口会留着上一次的值** ⇒ 置**唯一标记** → 驱动到**被换掉**；读数三层**别合成一个谓词**（会把「换错形状」归成「没换」）。
- 🩸 **负控还原别用 `shutil.copy2`**（连 mtime ⇒ MSBuild 不重建 ⇒ **正控跑的是负控的二进制、假红**）⇒ **写回原文**。判红谓词要匹配汇总行**且**取到 `错误消息:` 并**指到是哪条腿**（`wrong-leg`/`anchor-error` 不算通过）。
- 临时目录/随机红 ⇒ 技能 `dotnet-test-tempdir-hygiene`（🩸 沙箱 `/tmp` 是 10 MB tmpfs ⇒ 门禁**测试步**会瞬时写满，**判据＝同命令重跑即绿**，别挪 `TMPDIR`）。
- 🩸 **门禁测试步带 `--blame-hang` 上限**（`TEST_HANG_TIMEOUT` 默认 180s）⇒ 挂死变「**点名**失败」；枚举临时根时**别 `ReadAllText` FIFO**（`st_size=0` 跳过）。详技能 `…hang-triage`。
- **子串定位要带换行边界**（`## 验证记录` 是 `### 验证记录` 的子串 ⇒ 自检**假失败**）；元组元素可空性**不协变** ⇒ 用 `params`。
- 🩸 **仓库根别全量 `os.walk`**：`firmware/`＝**174 万文件**，被 SIGTERM(137)；点目录 bind-mount 会卡死 ⇒ 分析面只走 `src/ tests/ docs/ tools/`。
- 🩸 `XDocument` **元素面免疫注释** ⇒ 剥注释要剥**子串面**；**`dotnet test` 先 build** ⇒ 测「改了没重建」要 `--no-build`。
- 跑 `git` 的目录须在工作树外；数据根改名读回退、写不回退。**行数基线不调大**（压回行数反逼出更好修法）。

### 4.2 Avalonia / headless
> 读数/探针/已否证的解释/字体记账 ⇒ `docs/2026-09-22-headless-dispatch-and-font-accounting.md`；打开/保存 ⇒ `…project-file-open-save-net.md`
- 会话＝ **`PerTest` 隔离**；`s_uiThread` 被 Reset 后**第一个**构造 Dispatcher 的线程抢下（#47）⇒ 探针别读 `Dispatcher.UIThread`（反射 `TryGetUIThread()`、判会话线程用 `SessionThreadId`）。
- 🩸 **并发派发是竞态前提** ⇒ **闸门已落地**（`SemaphoreSlim`，**20 → 1**，守卫 `HeadlessDispatchContractTests`）；`Pump` 也在闸门里但**会话线程上必须旁路**；#47 偶发红 ⇒ 修法不落地；#45 根因未确证 ⇒ 独占进程规避保留。诊断 `HMI_DIAG_OUT`。
- 🩸 **headless 布局死区**：`ScrollViewer`（垂直可滚）+ `Wrap` 多行 `TextBlock` ⇒ 首次布局不收敛、挂死（`124`），真机不挂；**垂直 `Disabled`/`NoWrap` 收敛** ⇒ 接缝＝**造窗/显示分开**。详 `…scrollviewer-wrap-hang.md`。
- `ShowDialog` 不阻塞、弹窗同步进 `OwnedWindows`。⚠ headless 下**选择器静默空返回** ⇒「没弹窗」不可判 ⇒ 判据落「mtime 真被改」。
- 🩸 **驱动 headless 异步入口用墙钟、别用「轮数」**；**终态谓词用 `Title`**。
- ⚠ headless 的 `Bitmap` 解码器是**桩**（真 4×4 PNG 也解成 1×1、从不抛）⇒ 该档**不可实测**；判「宽容」还是「桩」必须放**正常输入**当对照（阶段 84）。
- **接线两类「双路」**（`.axaml` 声明 + `.axaml.cs` 再 `+=`；视图事件转发总线 + 宿主直订）⇒ `[1d]` / `[1e]`。

### 4.3 动作面守卫
- 方法论全在技能 `static-guard-authoring`（分桶 + 双向差集；集合属性元素 ⇒ `FindControl`；读数出口 `JudgeReadings`/`HMI_READINGS_OUT` 零副作用 ⇒ 跨阶段对比先 `sort` 再 diff、剥墙钟）。落地 ⇒ `…menu-`/`…dialog-inner-`/`…mainwindow-toolbar-`/`…dockpanel-invocation-net.md`。
- **`[1f]` 位置找控件**：`check-index-based-control-lookup.py`（两级硬判 0、基线已空）。🩸 **清空基线 ≠ 只改口径** ⇒ fail-closed 改由 `meta_positive_control()` 承担；**类型导航/读取不硬判**，真漏的是**变量下标** ⇒ 行为判据兜（`FaceplateParamRefreshUiTests`）。
- 🩸 **「名字即引用」类判据会被自己的文档/字面量满足**（#48）：三档 `ReferenceFace{Raw,NoComments,Code}`，生产走 `Code`（`.cs` 剥注释+抹字面量留插值孔、`.axaml` 剥 XML 注释）；「引用」＝**代码标识符**（散文/失败信息/表头/夹具不算）；语义判据两侧都断言、单调性逐级真变窄。
- 🩸 **豁免类别会被「写法」而非「语义」决定**（阶段 81）：`DevMessage`（`throw new`）吞掉 `HmiPackageException` 的**面向用户回落文案** ⇒ 数字随纯重构漂移 ⇒ 修法＝加**优先级更高**的语义规则。

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典
- **键可达性** = **有代码引用**的常量值 ∪ **十机制**（M1 `Lang` 常量 / M2 `{res:Loc}` / M3 插件族派生 / M4 `cfg.note` / M5 脚本诊断码 / M7 枚举名小写 / M8 AI 诊断码 / M9 包诊断码（`HmiPackageIssueCodes`）/ **M10 系统诊断码（`SystemFailureCodes`，阶段 85 #61e）** / **M6 整键字面量＝代理、排最后**），或进 `KeptUnreachable`（**双向**：两侧相等 ⇒ 反向也红）。守卫 `LocalizationKeyReachabilityGuardTests`。🩸 **代理机制必须写明它看不见什么**；豁免表不许硬塞。🩸 **「加键不加 `Lang` 常量」必须配一条新机制**：键由前缀拼出 ⇒ 源码里没有**整键字面量** ⇒ 老机制原理上全看不见；新机制**要做负控**（摘掉它 ⇒ 那些键必须被**逐条点名**判不可达，光有下界断言不够）。
- 🩸 **#49**：判据的**担保方自己不能是死的**（M1 曾只问「常量还在不在」⇒ 死常量替死键担保）。
- **收紧口径时先查下界/基线**：两处 `1800` 因口径换了被撞穿 ⇒ 下界必须跟着口径走。
- 🩸 **收口 ≠ 改名，且要先量收益**（阶段 83/84）：宽捕获站点（`catch (Exception ex)`）换码化出口**恒落回落分支** ⇒ 用户所见**一字不变**、面**绿着变空** = **改名式假闭环**（面变小不算迁移成功）。C 类「带上对象」**阶段 86 已落地**（#61b 闭环：9 站迁 8，面 37 → **29**，批量档归 #61a）—— 它等的正是 #61e 先把 BCL 类型码化。三分类 ⇒ `docs/2026-09-23-exception-passthrough-triage.md` §83–86。
- 🩸 **「面变小 ≠ 修好」**：面只数**形状**（`Format(键, ex.Message)`）⇒ 不那样写就从面上消失 —— 换成 `FailureDetail(op, null, ex)` **一样消失**，用户仍看不到文件。⇒ 面必须**配一条盯实参**的判据（`EveryDetailCallSite_PassesAConcreteTargetPath`：三实参不得填 `null`/`default`；输入面**剔定义面**，否则被豁免表养着）。实测成对：注入 `null` ⇒ 新判据红、**面 3/3 仍绿**。
- 🩸 **承接变量**：路径声明在 `try` 内 ⇒ catch 取不到（`CS0103` 编译器当场报）⇒ 外层 `string? x = null;` + try 内赋值；**别把实参写成 `null`**（既丢"带上对象"、面又看不见）。
- **字典处置链**：删字典键 ⇒ **必须连 `Lang` 常量一起删**（`LangKeyCoverageTests`）+ 清 `sync_localization.py` 的 `VALUE_MAP` + 同步 `UnusedBaseline`。`param.*` **只增不删**。
- **`UnusedBaseline` 不是清理清单**：**1120** = M2 828 / M6 269 / M3 18 / 无机制 5（阶段 86；**改了就要跟着改这里**）；语义＝**新增一个没人读的常量即红**。⚠ 面读数跨口径不可比；新增机制要核别的面没跟着涨（涨＝隐形担保）。
- **`sync_localization.py`（#52）**：第 2 步会给「字典有、`Lang.cs` 没有」的键**生成常量**（干跑顶穿基线 ⇒ 红）⇒ **只堵陷阱不动基线**：默认干跑（落盘 `--write`、生成常量 `--add-consts`）；守卫锚点须落在赋值表达式。
- **接线后必须删差异表行**：`KeptUnreachable` 是**例外**不是豁免（变得可达或从字典消失时红）。
- **资源键（`x:Key`）双向闭合**：定义 ⇒ 有人用（差异表 3 条）；引用面 **5 类**（`SetOrUpdate*`＝被管理≠被读）。🩸 取值**两口径**（索引器不查合并字典、`TryGetResource` 查）⇒ 合并键「查得到、索引到 null」⇒ NRE（只认 `out` 值）。图标几何/描边坑 ⇒ `…toolbox-icon-single-source.md`
- **字典 JSON 非排序存放** ⇒ `json.dump(sorted(...))` 重写产生 **1701 行** diff ⇒ 逐行手术 + `assert count == 1` + 备份 + 回读。
