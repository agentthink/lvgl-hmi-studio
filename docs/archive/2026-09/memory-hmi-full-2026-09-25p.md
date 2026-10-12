# 智数 HMI 长期笔记

> ⚠ 上限 **8,000 ＝ UTF-16 码元**（🩸 星平面字符算 2，别用 `len()`；超了静默截断）——细节析出 `docs/`，只留契约 + 指针。
> 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-25o.md`；细节 ⇒ `…memory-detail-extract.md`（§12–§13 阶段 97；§14 阶段 98；§15 阶段 99；§16 阶段 100；**§17** 阶段 101 ＋ 各节析出的全文）；数字 ⇒ `project-status.md`、未闭环 ⇒ 登记表。

## 1 设备 / 构建
- `/rom` 只读 ⇒ `/usr`、`/etc` 重启即失；唯一持久分区 `/mnt/UDISK`。抓帧 `dd if=/dev/fb0` 只读首缓冲 ⇒ 先看 `fb0/pan`。
- 收帧唯一 `CommandFrameCollector`（静默从最后一段起算）；会话超时顺序 `评估 → 打点 → 判定`。
- Tina：`make package/hmi-runtime/compile V=s` + `pack`；SPI NAND 须 `openixSuit`；`lv_conf.h` 以 `src-config/` 为准。
- 同一 `.axaml` 只声明一次（重复键 ⇒ UI 测试死在 `SetupUnsafe`）。
- 🩸 **沙箱 `RLIMIT_AS` 随会话变**（4 GiB 时 `dotnet build` 必 OOM）⇒ **先 `ulimit -Hv` 探**；4 GiB 才走 `.workbuddy/tmp/run-outside.sh`（借 `systemd --user`）。
- 🩸 **编译绑定＝显式声明**：`AvaloniaUseCompiledBindingsByDefault` 不写就靠包内两个**相反**默认＋导入顺序 ⇒ 已在 `src/Directory.Build.props` 显式（守卫 `XamlCompiledBindingGuardTests`）。

## 2 生成链契约（不能回退）
- 净化唯一 `CIdentifier.cs`；`obj_*` 无屏幕前缀 ⇒ 控件 id 工程内唯一；资源符号只在 `Export` 入口调一次。
- 发射清单唯一 `EnumerateRenderableControls` ⇒ 不遍历 `project.Screens[].Controls`；Faceplate 克隆换独立集合。
- 绑定：`BindingVisualProperty` + 发射点 `VisualBindingCodeGen`；`BgColor` 须同设 `bg_opa(COVER)`。颜色唯一 `PaletteDefaults.cs`；`apply_theme_styles()` 须在 `lv_screen_load` 后且不设 `bg_color`。
- 权限唯一 `ActionPermissionMap`/`SecurityConfigCodeGen`；判定只在生成头；门只在 `!= Designer` 发射。
- 下沉/凭据：3 字段 → `generated_runtime.h`；编辑器凭据不下发；`hmi_login` 有表只跳屏不授权；AI enum 源 `PromptTemplateEngine`/`AiSchemaEnums`。
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；**产出方给「码+实参+中文回落」**；`.Format` 与 XAML `{res:Loc}`（恒 0 实参）不得共用同键。**嵌套实参**＝ `{1}` 装**记录体** ⇒ App **填参前** `RenderNestedArgs` **白名单分派**（别改成「都 `ToString()`」）。🩸 **同一族显示文案只许一处映射**：枚举 `switch` **刻意无默认支**（默认支静默吞新取值）；**品牌字面量**判过**不翻** ⇒ **双向表**钉住。🩸 **一份映射在 N 处露出 ⇒ 单一出口**（阶段 95/96 ⇒ 析出 §9）：控件类型显示名曾**两份表已分叉**（漏 `Screen`）⇒ `ControlTypeTextKeys`（§15.6）。
- 🩸 **分派依据不能是「已有的那一个枚举」**：同一个 `ReferenceType` 下有**两种措辞** ⇒ 须新增「描述种类」枚举（`ReferenceDescriptionKind`，**`Unset = 0` 承「忘填」**）；硬分派 ⇒ 两对措辞粘一份模板而**编译/字典/台账全不响**。⚠ 实参按种类而异 ⇒ **每支 `Format` 实参个数须是字面量**（C1 读它）。
- **角色名/描述＝界面文案，非落盘数据**：生成头只发 `Id/Name/Level/掩码`、`SecurityConfig` **无** `Roles` ⇒ 唯一键推导 `Services/RoleTextKeys.cs`（用模型 `.Id`）。**别改回模型**（#54）。**同族第二例＝动作展示名**（#61m）⇒ 详析出 §15.2。
- 详规 ⇒ `docs/…expression-driven-visual`、`…permission-model-sinking`、`…runtime-*`、`…localization-placeholder-closure`、`…ai-issue-localization`

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- `ItemsSource` 装模型对象且**无 `ItemTemplate`** ⇒ 兜底 `ToString()` 得类型全名 ⇒ 建 `ComboBoxItem{Content,Tag}`；`ComboBoxItem` 是控件 ⇒ 两个下拉各建一批。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。
- 🩸 **窗口有「设计器 / 生产」两条构造**（`WindowViewSmokeUiTests` 只覆盖前者）；`WindowProductionPathUiTests` 用**形状签名**区分 —— 不能只看「null→非空」（有值但空会静默放过）⇒ 须带集合元素个数。

## 4 测试纪律 / 环境

### 4.1 通用
> 判据写法与坑的**全文** ⇒ `…memory-detail-extract.md` §4；技能 `static-guard-authoring`。
- 跑全量必走 `scripts/ci-gate.sh`；「卡住」看日志 mtime。**引用「绿」前先核时序**（`>` 重定向 birth 陈旧）；间歇缺陷**「有一次绿」零信息**。
- ⚠ 对拍差异要能归因到**本阶段**（排除 `tests/`）；**NuGet restore 状态行**、**时间戳派生数字**、**驱动轮数 / 数据根计数**先剥（🩸 `Now`＋STJ 裁尾零）；🩸 `git ls-files` 步**内容批前后差 N 行**。
- **先量复现率再谈修法**；**一次只放开一个变量、两侧同一条命令**并**验证变量真的动了**（🩸 `-p:` 改不动 xunit）。
- 🩸 **判据六条铁律**（全文 ⇒ 析出 §11；写法 ⇒ 技能 `static-guard-authoring`）：① 守卫与被守对象**同源**；② **做不出负控的判据不发布**（负控**必须能编译**、读**汇总行**）；③ 输入面**不得含「定义面」**；④ 别量**自己不该管的量**（#55）；⑤ 判据会踩既有守卫；⑥ **枚举 `switch` 两档警告码**：CS8524（噪声）/ **CS8509（信号）** ⇒ 只许**文件级**禁 CS8524。
- 🩸 **自治 / 观测 / 还原**：全文 ⇒ 析出 §10（禁前后快照／读数分层／**写回原文**／判红取 `错误消息:` 并指到哪条腿／红自带信息／内嵌负控的正控全合成）。
- 临时目录/随机红 ⇒ 技能 `dotnet-test-tempdir-hygiene`。
- 🩸 **门禁测试步带 `--blame-hang` 上限**（默认 180s）⇒ 挂死变「**点名**失败」；枚举临时根时**别 `ReadAllText` FIFO**（§15.6）。技能 `…hang-triage`。
- 🩸 **切「进程级全局语言」的测试类必须挂 `[Collection("Language-global")]`**（否则打红**别人的**用例）。判据 `LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage…`（**单向**）。🩸 别给 `ci-gate.sh` 加排除（阶段 95 走过这段弯路）。
- **子串定位带换行边界**；🩸 仓库根**别全量 `os.walk`**（`firmware/` 174 万文件）⇒ 只走 `src/ tests/ docs/ tools/`；剥注释要剥**子串面**；测「没重建」要 `--no-build`；git 目录须在工作树**外**；数据根**改名读回退、写不回退**；🩸 **行数守卫**只许改动**行数中性**（涨了先压注释）。
- 🩸 **阶段 97/98 四条**（全文 ⇒ §12 / §14）：① 判「文案走没走字典」用**两种语言各渲染一遍**（zh 值**刻意等于**回落时中文遍**零见证力** ⇒ **en 那遍才是见证**）；② 注入型负控**一注一还**、`git add` 前 `grep 注入标记`；③ 清单条数用 `Coll.Count`、**别写死**；④ 分档看**产出方**（会抛 ⇒ 给码换出口；只 `return` ⇒ 换出口是零收益改名）。🩸 **另有四条**（`no_match()` 守卫 / **接线形状判据** / 工厂 ≠ 产出 / **负控别读累计日志**）⇒ §15.3 / §15.1。🩸 **负控每支一支日志**（注入/还原分开）；**收红集与汇总行交叉核对**（「收不到」≠「没有红」）⇒ §17.3。

### 4.2 Avalonia / headless
> **9 条全文** ⇒ `…memory-detail-extract.md` **§13**；读数/探针/字体记账 ⇒ `…headless-dispatch-and-font-accounting.md`；打开/保存 ⇒ `…project-file-open-save-net.md`

### 4.3 动作面守卫
> 全文（含阶段 95 追加）⇒ `…memory-detail-extract.md` §9；方法论 ⇒ 技能 `static-guard-authoring`。

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典
- **键可达性** = **有代码引用**的常量值 ∪ **十八机制**（M1–M18；清单 ⇒ 析出 §2），或进 `KeptUnreachable`（**双向**）。**M14**＝`expr.syntax.`（面 7）⇒ **码在抛错点给**、不做反推。**M15–M18**＝`user.issue.`/`sim.issue.` · `recipe.issue.` · `login.issue.` · `recipe.import.issue.`（面 3/2/5/6）⇒ 取数**复用 `Declared*()`**、下界取满值；§17.1。守卫 `LocalizationKeyReachabilityGuardTests`。🩸 **代理机制（M6）要写两件：看不见什么 ＋ 看得太多什么**（#61s）；**「加键不加 `Lang` 常量」要配新机制 ＋ 负控**（摘机制 ⇒ 键**逐条点名**）；豁免表**不许硬塞**。详 析出 §2。
- 🩸 **新标识符前先查同名 `Lang` 常量**：引用面是「**任意标识符**」（`src/`＋`tests/`，`ReferenceFace.Code`）⇒ 撞上**零引用**常量 ⇒ 它被**意外写活**、`UnusedBaseline` 掉 1（症状＝基线红 + "建议改成 **基线−1**"）。修法＝**改名**（精确串替换，`replace_all` 误伤带前缀兄弟）＋ 同名校验判据（**反射取两侧集合**）；**基线不动**。🩸 **别借别族的键**（会让零引用常量被写活）⇒ **同族新建键**。
- 🩸 **#49**：判据的**担保方自己不能是死的**（M1 曾只问常量在不在 ⇒ 死常量替死键担保）。**收紧口径先查下界/基线**（两处 `1800` 被撞穿 ⇒ 下界跟着口径走）。 🩸 **同族第二例**：三态闭包里**概率性码**（`Guid[..8]` 撞车）**工厂造得出、产线复现不了** ⇒ 命中须带 `ReachedByProducer` 分维、闭包只问**产出方调用点**（详析出 §15.5）。
- 🩸 **收口 ≠ 改名 ＋「面变小 ≠ 修好」**（阶段 83–86）：① 宽捕获站点换码化出口**恒落回落** ⇒ 所见**一字不变**、面绿着变空＝**改名式假闭环**；② 面只数形状 ⇒ 换个写法**一样消失** ⇒ 必须**配一条盯实参**的判据（详 析出 §2）；🩸 都不认「给了几遍」（阶段 90 补「恰好 1 次」）。C 类走完：面 **37 → 29 → 21 → 15**；阶段 91 起**连续零变化**（**第四~十二例「面读数是证词、不是目标」**）。
- 🩸 **产出方是纯函数 ⇒ 失败要「返回值带码」**：模板＝`SvgParseError` / `CsvImportIssue`（行号不进 `Message`）/ `GeneratedCodeIssueCodes` / **`ExpressionSyntaxError`（码从抛错点直出）** / 其余各族的 `*Issue`/`*Result`（**两个格子恰好一个非空** / 行号结构化；族清单 ⇒ §16.5）；出口 `ExternalText.Describe`；上层穿透才用 `*Exception`。⚠ **失败档可以有意共码**（「用户不存在 / 密码错」共用 ⇒ 否则界面成了**用户名枚举预言机**）—— 那是**产品判断**，写进码表注释。⚠ 标准是**产出方有没有码**、不是面变小；消费点**必须读返回值** ⇒ 判据带**源码面接线**；**定位两档**（有码／中性直出）。
- 🩸 **改返回/异常类型＝取证手段**：**编译器当场点出全部消费点**。**换异常类型要刻意「不继承」旧基类**（否则老 `catch` 继续静默命中、**收口在编译面看不见**）；同族盲区：同类型**内部 `unqualified` 调用**（`grep` 看不见、编译器点得出）。
- 🩸 **承接变量**：目标声明在 `try` 内 ⇒ catch 取不到（`CS0103` 当场报，**静态判据看不出作用域**）⇒ 外层 `string? x = null;` ＋ try 内赋值；**别写成 `null`**（§15.6）。🩸 **承接变量档另有一条判据**（「带路径」那条只认**字面量**）⇒ §17.2。
- **字典处置链**：删键 ⇒ **连 `Lang` 常量一起删**（`LangKeyCoverageTests`）+ 清 `sync_localization.py` `VALUE_MAP` + 同步 `UnusedBaseline`。`param.*` **只增不删**。
- **`UnusedBaseline` 不是清理清单**：**1110** = M2 828 / M6 259 / M3 18 / 无机制 5；语义＝**新增一个没人读的常量即红**。🩸 **「降了」不一定是清理**（脚本锚点替换**吞掉锚点行** ⇒ 先查是哪一条）。🩸 **「归位行」的数字必上判据**（#61w）：台账通道数**少 1 漂了 3 个提交** ⇒ 把三处钉在**算出来的值**上。⚠ **语序相反** ⇒ 靠**具名组**取数；数组用 `.Length`（`.Count` ⇒ **CS8917**）。⚠ 面读数跨口径不可比；新增机制要核别的面没跟着涨；⚠ **删 N 键未必带走 N 常量**。
- **`sync_localization.py`**：会给「字典有、`Lang.cs` 没有」的键**生成常量**（干跑顶穿基线 ⇒ 红）⇒ **只堵陷阱不动基线**：默认干跑（落盘 `--write`、生成常量 `--add-consts`），守卫锚点落在赋值表达式。
- **接线后必须删差异表行**：`KeptUnreachable` 是**例外**不是豁免（变得可达或从字典消失时红）。
- **资源键（`x:Key`）双向闭合**：定义 ⇒ 有人用；引用面 **5 类**（`SetOrUpdate*`＝被管理≠被读）。🩸 取值**两口径**（索引器**不查**合并字典、`TryGetResource` 查）⇒ 索引到 null ⇒ NRE（只认 `out` 值）。图标几何坑 ⇒ `…toolbox-icon-single-source.md`
- **同值键不是「没翻」**：**结构模板**（翻实参）**不能**走 `PairingViolations`（假阳性）⇒ 单独**双向**钉（析出 §7）。
- **字典 JSON 非排序存放** ⇒ `json.dump(sorted(...))` 重写产生 **1701 行** diff ⇒ 逐行手术 + `assert count == 1` + 备份 + 回读。
