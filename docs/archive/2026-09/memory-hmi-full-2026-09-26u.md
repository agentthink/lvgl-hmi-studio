# 智数 HMI 长期笔记

> ⚠ 上限 **8,000 ＝ UTF-16 码元**（🩸 星平面字符算 2，别用 `len()`；超了静默截断）——细节析出 `docs/`，只留契约 + 指针。
> 快照 `docs/archive/2026-09/memory-hmi-full-2026-09-26t.md`；细节 ⇒ `…memory-detail-extract.md`（§12–§21：阶段 97–107 ＋ 各节析出全文）；数字 ⇒ `project-status.md`、未闭环 ⇒ 登记表。

## 1 设备 / 构建
- `/rom` 只读 ⇒ `/usr`、`/etc` 重启即失；唯一持久分区 `/mnt/UDISK`。抓帧 `dd if=/dev/fb0` 只读首缓冲 ⇒ 先看 `fb0/pan`。
- 收帧唯一 `CommandFrameCollector`（静默从最后一段起算）；会话超时顺序 `评估 → 打点 → 判定`。
- Tina：`make package/hmi-runtime/compile V=s` + `pack`；SPI NAND 须 `openixSuit`；`lv_conf.h` 以 `src-config/` 为准。
- 同一 `.axaml` 只声明一次（重复键 ⇒ UI 测试死在 `SetupUnsafe`）。
- 🩸 **沙箱 `RLIMIT_AS` 随会话变**（4 GiB 时 `dotnet build` 必 OOM）⇒ **先 `ulimit -Hv` 探**；4 GiB 才走 `.workbuddy/tmp/run-outside.sh`。
- 🩸 **编译绑定＝显式声明**：不写就靠包内两个**相反**默认＋导入顺序 ⇒ 已在 `src/Directory.Build.props` 显式（守卫 `XamlCompiledBindingGuardTests`）。

## 2 生成链契约（不能回退）
- 净化唯一 `CIdentifier.cs`；`obj_*` 无屏幕前缀 ⇒ 控件 id 工程内唯一；资源符号只在 `Export` 入口调一次。
- 发射清单唯一 `EnumerateRenderableControls` ⇒ 不遍历 `project.Screens[].Controls`；Faceplate 克隆换独立集合。
- 绑定：`BindingVisualProperty` + 发射点 `VisualBindingCodeGen`；`BgColor` 须同设 `bg_opa(COVER)`。颜色唯一 `PaletteDefaults.cs`；`apply_theme_styles()` 须在 `lv_screen_load` 后且不设 `bg_color`。
- 权限唯一 `ActionPermissionMap`/`SecurityConfigCodeGen`；判定只在生成头；门只在 `!= Designer` 发射。
- 下沉/凭据：3 字段 → `generated_runtime.h`；编辑器凭据不下发；`hmi_login` 有表只跳屏不授权；AI enum 源 `PromptTemplateEngine`/`AiSchemaEnums`。
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；**产出方给「码+实参+中文回落」**；`.Format` 与 `{res:Loc}`（恒 0 实参）**不得共用同键**。**嵌套实参**＝ `{1}` 装**记录体** ⇒ 填参前 `RenderNestedArgs` **白名单分派**（**别改成「都 `ToString()`」**）。🩸 **同一族显示文案只许一处映射、一份映射在 N 处露出 ⇒ 单一出口**（枚举 `switch` **刻意无默认支**；品牌字面量**双向表** ⇒ §19.8 / §9 / §15.6）。
- 🩸 **分派依据不能是「已有的那一个枚举」**：同一 `ReferenceType` 下**两种措辞** ⇒ 新增「描述种类」`ReferenceDescriptionKind`（**`Unset = 0` 承「忘填」**）；硬分派 ⇒ **编译/字典/台账全不响**（§19.10）。⚠ 实参按种类而异 ⇒ **每支 `Format` 实参个数须是字面量**（C1 读它）。
- **角色名/描述＝界面文案，非落盘数据**：生成头只发 `Id/Name/Level/掩码` ⇒ 唯一键推导 `Services/RoleTextKeys.cs`（用模型 `.Id`）—— **别改回模型**。同族第二 / 三例 ⇒ §20.9 / §15.2 / §18.3。
- 🩸 **「B 类」已从三分类删掉**（只剩 **A 类**产出方 / **C 类** BCL·IO）：外部工具失败**一律走返回值** ⇒ 结构上**不可能**有该类站点（§19.1）。
- 🩸 **插件族**（`hw.issue.` / `comm.issue.`）：码 App 反射不到 ⇒ 取数靠**真触发**、**别用单行正则量码**；前缀**不许当自由变量**；两族契约**同形** ⇒ §20.2 / §19.2 / §19.3。
- 🩸 **码表与产出方同在 App 的族**（`sim.build.issue.`）：回落原文**逐字留抛点**、**不搬工厂**；换异常类型**刻意不继承**旧基类 ⇒ §20.3 / §19.4。
- 详规 ⇒ `docs/…expression-driven-visual`、`…permission-model-sinking`、`…runtime-*`、`…localization-placeholder-closure`、`…ai-issue-localization`

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- `ItemsSource` 装模型对象且**无 `ItemTemplate`** ⇒ 兜底 `ToString()` 得类型全名 ⇒ 建 `ComboBoxItem{Content,Tag}`（**是控件** ⇒ 两个下拉各建一批）。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。
- 🩸 **窗口有「设计器 / 生产」两条构造** ⇒ `WindowProductionPathUiTests` 用**形状签名**区分（**须带集合元素个数**）⇒ §18.5。
- 🩸 **`Tag.ToString()` 比 `Id`** ⇒ 模型没覆写 `ToString()` 时**选中恒失败**（登记 #64，6 处）；同族第二例＝**列表选中被重建吃掉**（新建完立刻删不掉）。

## 4 测试纪律 / 环境

### 4.1 通用
> 判据写法与坑的**全文** ⇒ `…memory-detail-extract.md` §4；技能 `static-guard-authoring`。
- 跑全量必走 `scripts/ci-gate.sh`；「卡住」看日志 mtime。**引用「绿」前先核时序**；间歇缺陷**「有一次绿」零信息**。🩸 **读数读的是工作树 ⇒ 只要求晚于「改动落盘」，不是晚于「提交」**（§18.2③）。
- ⚠ 对拍差异要能归因到**本阶段**（排除 `tests/`）；**NuGet restore 状态行**、**时间戳派生数字**、**驱动轮数 / 数据根计数**先剥（🩸 `Now`＋STJ 裁尾零）；🩸 `git ls-files` 步**内容批前后差 N 行**。
- **先量复现率再谈修法**；**一次只放开一个变量、两侧同一条命令**并**验证变量真的动了**（🩸 `-p:` 改不动 xunit）。
- 🩸 **判据六条铁律**（全文 ⇒ §11；写法 ⇒ 技能 `static-guard-authoring`）：① 守卫与被守对象**同源**；② **做不出负控的判据不发布**（负控**必须能编译**、读**汇总行**）；③ 输入面**不得含「定义面」**；④ 别量**自己不该管的量**（#55）；⑤ 判据会踩既有守卫；⑥ **`switch` 两档警告码**：CS8524（噪声）/ **CS8509（信号）** ⇒ 只许**文件级**禁 CS8524。
- 🩸 **自治 / 观测 / 还原**：全文 ⇒ §10（禁前后快照／读数分层／**写回原文**／判红取 `错误消息:` 并指到哪条腿／红自带信息／内嵌负控的正控全合成）。
- 🩸 **面到 0 ⇒ 改性质**（升格**零容忍**）：基线清空 ＋ `Diff` 收 `baseline` ＋ 自检断 `Empty`；负控喂**合成基线** ⇒ §20.4 / §19.5。
- 🩸 **负控语料按目标文件分叉**；**剥注释后「全注释掉」该报**，别写成零违规 ⇒ §20.5 / §19.6。
- 临时目录/随机红 ⇒ 技能 `dotnet-test-tempdir-hygiene`。
- 🩸 **门禁测试步带 `--blame-hang` 上限**（默认 180s）⇒ 挂死变「**点名**失败」（§17.4）。技能 `…hang-triage`。
- 🩸 **切「进程级全局语言」的测试类必须挂 `[Collection("Language-global")]`**（否则打红**别人的**用例）。判据 `…EveryTestClassThatSwitchesLanguage…`（**单向**）。🩸 别给 `ci-gate.sh` 加排除（阶段 95 走过这段弯路）。
- **子串定位带换行边界**；🩸 仓库根**别全量 `os.walk`**（只走 `src/ tests/ docs/ tools/`）；🩸 **行数守卫**只许改动**行数中性**。⚠ 剥注释**子串面** / `--no-build` / git 目录在工作树**外** / 数据根**读回退写不回退**（§18.4）。
- 🩸 **判「文案走没走字典」用两种语言各渲染一遍**（zh 值等于回落时**中文遍零见证力，en 那遍才是见证**）；🩸 **注入型负控一注一还**、`git add` 前 `grep 注入标记`；🩸 **分档看产出方**（会抛 ⇒ 给码换出口；只 `return` ⇒ **零收益改名**）；🩸 **负控每支一支日志** ＋ **收红集与汇总行交叉核对**；🩸 **负控红了 ≠ 判据在响**（静默输出吞断言消息 ⇒ 要**点名到子串**）。⚠ 其余 ⇒ §12 / §14 / §15.1 / §15.3 / §17.3。
- 🩸 **判据盯着的数字先量「它数的是哪张表」**（两个桶只差一个字段名 ⇒ 照着「改了 N 条文案」改总数，判据当场红）；🩸 **一次性探针量完就地降级成「无 `[Fact]` 的注释桩」**（bind-mount 期内删不掉，留着会搅浑程序集计数）⇒ §20.7 / §20.8。
- 🩸 **动作面第五层＝「点一下改了什么」**（`HostButtonEffectUiTests`）：断言**双向**（声明分量须**等于**声明值 ＋ **未声明分量一字不变**，后半抓副作用外溢）；`ToggleButton`/`CheckBox` **先翻转 `IsChecked` 再引发 `Click`**，否则 `IsCheckedChanged` 接线**零覆盖**；探针分母**先与判据同口径**（`OfType<Button>()` **含 `CheckBox`**）⇒ §21.2。

### 4.2 Avalonia / headless
> **9 条全文** ⇒ `…memory-detail-extract.md` **§13**；读数/探针/字体记账 ⇒ `…headless-dispatch-and-font-accounting.md`；打开/保存 ⇒ `…project-file-open-save-net.md`

### 4.3 动作面守卫（**五层**：菜单 / 工具条 / 对话框内 / 停靠面板 / 宿主效果）
> 前四层 ⇒ §9；第五层 ⇒ §21.2；方法论 ⇒ 技能 `static-guard-authoring`。

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典
- **键可达性** = **有代码引用**的常量值 ∪ **二十机制**（M1–M20），或进 `KeptUnreachable`（**双向**）。族清单 / 面 / 下界 ⇒ §2；取数**复用各族 `Declared*()`**（**插件族真触发**）、下界取满值；**M14** ⇒ 码**从抛错点直出**、不做反推（§19.9）。守卫 `LocalizationKeyReachabilityGuardTests`。🩸 **加机制要同时改「下界表 ＋ 类注释清单」—— 阶段 106 起有判据**（三处来源集合**双向相等** ＋ 空集守卫）；🩸 **代理机制（M6）双写：看不见什么 ＋ 看得太多什么**；**「加键不加 `Lang` 常量」配新机制 ＋ 负控**（摘机制 ⇒ 键**逐条点名**）；豁免表**不许硬塞**。
- 🩸 **新标识符前先查同名 `Lang` 常量**（引用面是**任意标识符** ⇒ 撞上**零引用**常量 ⇒ 它被**意外写活**、基线掉 1，症状＝基线红 ＋ 「建议改成 基线−1」）⇒ **改名**（精确串替换）；**基线不动**。🩸 **别借别族的键**（同样会写活）⇒ **同族新建键**。全文 ⇒ §18.1。
- 🩸 **#49**：判据的**担保方自己不能是死的**（M1 只问常量在不在 ⇒ 死常量替死键担保）。**收紧口径先查下界/基线**（两处 `1800` 被撞穿）；🩸 **面掉下来时先红的****是自检、不是主判据** ⇒ 下界**往下放一档**、**不删自检**、**不贴实测值**（贴了每次**正确**迁移都多红一条）⇒ §18.2②。 🩸 **同族第二例**：**概率性码**（`Guid[..8]` 撞车）**工厂造得出、产线复现不了** ⇒ 命中带 `ReachedByProducer` 分维、闭包只问**产出方调用点**（§15.5）。
- 🩸 **收口 ≠ 改名 ＋「面变小 ≠ 修好」**（83–86）：① 宽捕获站点换码化出口**恒落回落** ⇒ 所见**一字不变**、面绿着变空＝**改名式假闭环**；② 面只数形状 ⇒ 换个写法**一样消失** ⇒ 必须**配一条盯实参**的判据；③ 不认「给了几遍」（补「恰好 1 次」）。直通面 **37 → … → 6**（**面读数是证词、不是目标**）。🩸 **反向也成立：面看不见缺口**（谓词里没有的东西**原理上看不见**）⇒ 新面**先立判据**、盲区**自陈** ＋ 负控**钉「看不见」**（§18.2①）。
- 🩸 **产出方是纯函数 ⇒ 失败要「返回值带码」**（族清单 ＋ 两条 ⚠ 全文 ⇒ §20.6）：出口 `ExternalText.Describe`，上层穿透才用 `*Exception`；⚠ 标准是**产出方有没有码**、不是面变小；消费点**必须读返回值**。
- 🩸 **改返回/异常类型＝取证手段**：**编译器当场点出全部消费点**（**故意不继承旧基类** ⇒ 老 `catch` 不再静默命中，§19.4）；同族盲区：同类型**内部 `unqualified` 调用**（`grep` 看不见、编译器点得出）。
- 🩸 **承接变量**：目标声明在 `try` 内 ⇒ catch 取不到（`CS0103`；**静态判据看不出作用域**）⇒ 外层 `string? x = null;` ＋ try 内赋值；**别写成 `null`**（§15.6）。🩸 另有判据（「带路径」那条只认**字面量**）⇒ §17.2。
- **工程包失败档（M9 / 9 码）**：`Unpack` 的**全部**失败档都码化（含 `CreateDirectory` **之后**的中途失败）；中途失败**回滚只删自己建出来的目录**（先子后父、**吞删除异常**）；码表唯一 `HmiPackageIssueCodes`、异常带 `inner` 重载留取证线索 ⇒ §20.1。
- **字典与台账**（四条细则 ⇒ §21.1）：删键 ⇒ 连 `Lang` 常量一起删 ＋ 清 `VALUE_MAP` ＋ 同步 `UnusedBaseline`（`param.*` **只增不删**）；`UnusedBaseline` **不是清理清单**（**1108**＝新增无人读常量即红；🩸「降了」≠清理、🩸「归位行」数字必上判据）；`KeptUnreachable` 是**例外**不是豁免（可达或消失即红）；`sync_localization.py` 默认干跑（`--write` / `--add-consts`）、守卫锚点落赋值表达式。⚠ 语序相反 ⇒ **具名组**取数；数组用 `.Length`（`.Count` ⇒ **CS8917**）；面读数跨口径不可比；**删 N 键未必带走 N 常量**。
- **资源键（`x:Key`）双向闭合**：定义 ⇒ 有人用；引用面 **5 类**（`SetOrUpdate*`＝被管理≠被读）。🩸 取值**两口径**（索引器**不查**合并字典、`TryGetResource` 查）⇒ 索引到 null ⇒ NRE（只认 `out`）。图标几何 ⇒ `…toolbox-icon-single-source.md`
- **同值键不是「没翻」**：**结构模板**（翻实参）**不能**走 `PairingViolations`（假阳性）⇒ 单独**双向**钉（析出 §7）。
- **字典 JSON 非排序存放** ⇒ `sorted()` 重写产生 **1701 行** diff ⇒ 逐行手术 ＋ `assert count == 1` ＋ 备份 ＋ 回读。
