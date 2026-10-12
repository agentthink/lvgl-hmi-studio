# 智数 HMI 长期笔记

> ⚠ 上限 **8,000 字符**（按字符，超了静默截断）——细节析出 `docs/`，这里只留契约 + 指针。
> 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-23e.md`；细节 ⇒ `docs/2026-09-23-memory-detail-extract.md`；数字以 `project-status.md`、未闭环以登记表为准。

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
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；**产出方给「码+实参+中文回落」**（Core 不引本地化）；`.Format` 与 XAML `{res:Loc}`（恒 0 实参）不得共用同键。**嵌套实参**（本仓库第一处）＝ `{1}` 装**记录体** ⇒ App 侧**填参前** `RenderNestedArgs` **白名单式分派**（别改成「对象都 `ToString()`」）。
- **角色名/描述＝界面文案，非落盘数据**：生成头只发 `Id/Name/Level/掩码`、`SecurityConfig` **无** `Roles` ⇒ 唯一键推导 `Services/RoleTextKeys.cs`（用模型 `.Id`，表外回落 `role.Name`）。**别改回模型**（#54）。
- 详规 ⇒ `docs/…expression-driven-visual`、`…permission-model-sinking`、`…runtime-*`、`…localization-placeholder-closure`、`…ai-issue-localization`

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- `ItemsSource` 装模型对象且**无 `ItemTemplate`** ⇒ 兜底 `ToString()` 得类型全名 ⇒ 建 `ComboBoxItem{Content,Tag}`；`ComboBoxItem` 是控件 ⇒ 两个下拉各建一批。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。
- 🩸 **窗口有「设计器 / 生产」两条构造**（`WindowViewSmokeUiTests` 只覆盖前者）；`WindowProductionPathUiTests` 用**形状签名**区分 —— 不能只看「null → 非空」（有值但为空会静默放过）⇒ 须带集合元素个数。

## 4 测试纪律 / 环境

### 4.1 通用
> 判据写法与坑的**全文** ⇒ `docs/2026-09-23-memory-detail-extract.md` §4；技能 `static-guard-authoring`。
- 跑全量必走 `scripts/ci-gate.sh`；「卡住」看日志 mtime。**引用「绿」前先核时序**（只认 mtime，`>` 重定向的 birth 陈旧）；间歇缺陷**「有一次绿」零信息**。
- ⚠ 对拍差异要能归因到**本阶段**（扫描面排除 `tests/`）；**NuGet restore 状态行**先剥。
- **先量复现率再谈修法**；**一次只放开一个变量、两侧同一条命令**并**验证变量真的动了**（🩸 `-p:` 改不动 xunit）。
- 🩸 **判据四条铁律**（全文 ⇒ 析出文档 §4；写法 ⇒ 技能 `static-guard-authoring`）：① 守卫与被守对象**同源**；② **做不出负控的判据不发布**（同一注入：新写法绿 / 旧写法红；先排除「注入没生效」＝恒等变换、同加后缀自收敛；🩸 **负控必须能编译** —— 改名/换符号先 `CS0103`，到不了判据；读**汇总行**要认两种格式（`失败数: N` / `失败: N`，随 logger 变 ⇒ 认错会把红读成绿），`build-fail` 单独排除）；③ 输入面**不得含「定义面」**；④ 别量**自己不该管的量**（#55）；⑤ 🩸 **判据自己会踩既有守卫**（`TestAssertionStyleGuardTests` 拦「同一行 `Assert.Contains(` ＋ 含 `(`/`,` 的字面量」⇒ 先落局部变量再断言）。
- 🩸 **自治 / 观测 / 还原**：不许对共享资源做前后快照（单跑绿、门禁红）；读数三层**别合成一个谓词**；**负控还原写回原文**（`copy2` 连 mtime ⇒ MSBuild 不重建 ⇒ 假红）；判红谓词须取到 `错误消息:` 并**指到哪条腿**（细节 ⇒ 析出文档 §4）；🩸 **红必须自带信息**（xunit **截断**长串 ⇒ `Assert.True(count == 0, …)` 拼全文）。
- 临时目录/随机红 ⇒ 技能 `dotnet-test-tempdir-hygiene`（🩸 沙箱 `/tmp` 是 10 MB tmpfs ⇒ 门禁**测试步**会瞬时写满 ⇒ **同命令重跑即绿**，别挪 `TMPDIR`）。
- 🩸 **门禁测试步带 `--blame-hang` 上限**（`TEST_HANG_TIMEOUT` 默认 180s）⇒ 挂死变「**点名**失败」；枚举临时根时**别 `ReadAllText` FIFO**（`st_size=0` 跳过）。详技能 `…hang-triage`。
- **子串定位带换行边界**（`## 验证记录` 是 `### 验证记录` 的子串）；🩸 仓库根**别全量 `os.walk`**（`firmware/`＝174 万文件 ⇒ SIGTERM(137)）⇒ 只走 `src/ tests/ docs/ tools/`；剥注释要剥**子串面**；测「没重建」要 `--no-build`；git 目录须在工作树**外**；数据根**改名读回退、写不回退**；🩸 **相等判定的行数守卫**只许改动**行数中性**（阶段 90 被拦 `3018 ≠ 3013`）。

### 4.2 Avalonia / headless
> 读数/探针/字体记账 ⇒ `docs/2026-09-22-headless-dispatch-and-font-accounting.md`；打开/保存 ⇒ `…project-file-open-save-net.md`
- 会话＝ **`PerTest` 隔离**；`s_uiThread` 被 Reset 后**第一个**构造 Dispatcher 的线程抢下（#47）⇒ 探针别读 `Dispatcher.UIThread`（反射 `TryGetUIThread()`）。
- 🩸 **并发派发是竞态前提** ⇒ **闸门已落地**（`SemaphoreSlim`，**20 → 1**，守卫 `HeadlessDispatchContractTests`）；`Pump` 也在闸门里但**会话线程上必须旁路**；#47 偶发红 ⇒ 修法不落地；#45 ⇒ 独占进程规避保留。
- 🩸 **headless 布局死区**：`ScrollViewer`（垂直可滚）+ `Wrap` 多行 `TextBlock` ⇒ 首次布局不收敛、挂死；**垂直 `Disabled`/`NoWrap` 收敛** ⇒ 接缝＝**造窗/显示分开**。详 `…scrollviewer-wrap-hang.md`。
- `ShowDialog` 不阻塞、弹窗同步进 `OwnedWindows`。⚠ headless 下**选择器静默空返回** ⇒「没弹窗」不可判 ⇒ 判据落「mtime 真被改」。
- 🩸 **驱动 headless 异步入口用墙钟、别用「轮数」**；**终态谓词用 `Title`**。
- ⚠ headless 的 `Bitmap` 解码器是**桩**（4×4 PNG 解成 1×1）⇒ 该档**不可实测**；判「宽容」还是「桩」须放**正常输入**当对照。
- **接线两类「双路」**（`.axaml` 声明 + `.axaml.cs` 再 `+=`；视图事件转发总线 + 宿主直订）⇒ `[1d]` / `[1e]`。

### 4.3 动作面守卫
- 方法论全在技能 `static-guard-authoring`（分桶 + 双向差集；读数出口 `JudgeReadings`/`HMI_READINGS_OUT` **零副作用、要绝对路径** ⇒ 跨阶段对比先 `sort` 再 diff、剥墙钟）。落地 ⇒ `…menu-`/`…dialog-inner-`/`…mainwindow-toolbar-`/`…dockpanel-invocation-net.md`。
- **`[1f]` 位置找控件**：`check-index-based-control-lookup.py`（两级硬判 0）；🩸 **清空基线 ≠ 只改口径** ⇒ fail-closed 改由 `meta_positive_control()` 承担；真漏的是**变量下标**（行为判据兜）。
- 🩸 **「名字即引用」判据会被自己的文档/字面量满足**（#48）⇒ 三档 `ReferenceFace{Raw,NoComments,Code}`，生产走 `Code`（`.cs` 剥注释+抹字面量留插值孔）；「引用」＝**代码标识符**（散文/失败信息/表头/夹具不算）；语义判据两侧都断言、单调性逐级真变窄。
- 🩸 **豁免类别会被「写法」而非「语义」决定** ⇒ 修法＝加**优先级更高**的语义规则；🩸 阶段 91 把 Core 台账的 `DevMessage` **口径反转成「只认 BCL 类型」**（删 `UserFacingException` 白名单）⇒ 自有异常回落**自动计入**（失效方向：静默豁免 → 报红）。

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典
- **键可达性** = **有代码引用**的常量值 ∪ **十四机制**（M1–M14；清单/顺序 ⇒ 析出文档 §2），或进 `KeptUnreachable`（**双向**：两侧相等 ⇒ 反向也红）。**M14**＝`expr.syntax.`（面 7 = 6 可产出＋1 兜底进 `NotProducible`），产出方是**自家解析器** ⇒ **码在抛错点给、不做字符串反推**。守卫 `LocalizationKeyReachabilityGuardTests`。🩸 **代理机制（M6）必须写明它看不见什么**；豁免表**不许硬塞**。🩸 **「加键不加 `Lang` 常量」必须配一条新机制 + 负控**（键由前缀拼出 ⇒ 源码无整键字面量 ⇒ 老机制全看不见；摘掉新机制 ⇒ 键被**逐条点名**）。
- 🩸 **#49**：判据的**担保方自己不能是死的**（M1 曾只问常量在不在 ⇒ 死常量替死键担保）。**收紧口径先查下界/基线**（两处 `1800` 被撞穿 ⇒ 下界跟着口径走）。
- 🩸 **收口 ≠ 改名 ＋「面变小 ≠ 修好」**（阶段 83–86，专题 §83–86）：① 宽捕获站点换码化出口**恒落回落分支** ⇒ 用户所见**一字不变**、面**绿着变空** = **改名式假闭环**；② 面只数**形状** ⇒ 换 `FailureDetail(op, null, ex)` **一样消失** ⇒ 面必须**配一条盯实参**的判据（做法 ⇒ 析出文档 §2）；🩸 两者都不认「给了几遍」⇒ 阶段 90 补「路径**恰好 1 次** + 中英各归各」（实测成对：注入 `null` ⇒ 新判据红、**面仍绿**）。C 类走完：面 **37 → 29 → 21 → 15**；阶段 91 的嵌套实参走 `Args[1]` ⇒ 面**零变化**（**第四例**）。
- 🩸 **产出方是纯函数 ⇒ 失败要「返回值带码」**（三例）：产出方**不抛异常** ⇒ 模板＝`SvgParseError` / `CsvImportIssue`（**行号不进 `Message`**，M12）/ `GeneratedCodeIssueCodes`（M13）/**`ExpressionSyntaxError`（M14，自家解析器 ⇒ 码从抛错点直出）**；出口 `ExternalText.Describe`，上层**包成异常**穿透时才用 `*Exception` —— **同一套码两条搬运**。⚠ 判「裸换算 ≠ 收口」的标准是**「产出方有没有码」**，不是「面有没有变小」；消费点**必须读返回值** ⇒ 判据要带**源码面接线**；阶段 89 的**定位分三档**（有码 / 中性直出 / 外来；后两档各一张**双向**表守）。
- 🩸 **改返回类型＝取证手段**：`Parse` 的 `(…, string?)` → `(…, SvgParseError?)` ⇒ **编译器当场点出全部消费点**（~40 处里只有 4 处真把 `Error` 当串用）。
- 🩸 **承接变量**：目标声明在 `try` 内 ⇒ catch 取不到（`CS0103` 当场报，**静态判据看不出作用域**）⇒ 外层 `string? x = null;` + try 内赋值；**别写成 `null`**（既丢"带上对象"、面又看不见）。
- **字典处置链**：删字典键 ⇒ **连 `Lang` 常量一起删**（`LangKeyCoverageTests`）+ 清 `sync_localization.py` 的 `VALUE_MAP` + 同步 `UnusedBaseline`。`param.*` **只增不删**。
- **`UnusedBaseline` 不是清理清单**：**1115** = M2 828 / M6 264 / M3 18 / 无机制 5（阶段 91）；语义＝**新增一个没人读的常量即红**。⚠ 面读数跨口径不可比；新增机制要核别的面没跟着涨；⚠ **删 N 键未必带走 N 常量**（阶段 90 删 5 键只带 4；阶段 91 删 1 键带 1 ⇒ 基线 −1）。
- **`sync_localization.py`**：会给「字典有、`Lang.cs` 没有」的键**生成常量**（干跑顶穿基线 ⇒ 红）⇒ **只堵陷阱不动基线**：默认干跑（落盘 `--write`、生成常量 `--add-consts`），守卫锚点落在赋值表达式。
- **接线后必须删差异表行**：`KeptUnreachable` 是**例外**不是豁免（变得可达或从字典消失时红）。
- **资源键（`x:Key`）双向闭合**：定义 ⇒ 有人用；引用面 **5 类**（`SetOrUpdate*`＝被管理≠被读）。🩸 取值**两口径**（索引器不查合并字典、`TryGetResource` 查）⇒ 合并键查得到、索引到 null ⇒ NRE（只认 `out` 值）。图标几何/描边坑 ⇒ `…toolbox-icon-single-source.md`
- **字典 JSON 非排序存放** ⇒ `json.dump(sorted(...))` 重写产生 **1701 行** diff ⇒ 逐行手术 + `assert count == 1` + 备份 + 回读。
