# 【归档·全文快照】智数 HMI 长期笔记（截至 2026-09-17）

> **这份文件是什么**：`.workbuddy/memory/MEMORY.md` 的**逐字快照**，在 2026-09-17 对该文件做「只压措辞、不减事实」的瘦身**之前**抓取，共 20,741 字符。
> **为什么要归档**：那份笔记每次会话都会被注入上下文，体量超限后**尾部会被截断**（§8 配置诚实性、§9 未开工 曾因此读不到）。瘦身时把叙述性的「实测 / 曾… / 为什么」压成了一行指针，**为了避免丢掉事实**，原文在此留档。
> **为什么要放这里**：`.workbuddy/` 在 `.gitignore` 里（第 57 行）⇒ 那份笔记**只存在于本机、不进仓库**；而 `docs/archive/**` 被显式反忽略（`.gitignore` 第 90-92 行）且已被追踪 ⇒ 这里比原文件更耐久。
> **怎么用**：查某条契约的**来龙去脉**（当时的观测数据、翻车过程、推理链）来这里；查当前生效的**一句话契约**与指针看 `.workbuddy/memory/MEMORY.md`；查**数字与阻断项**看 `docs/project-status.md`。

---

# 智数 HMI 长期笔记

> 只留**会重复踩的坑**与**不能回退的契约**；逐轮细节见当日日志、专题见 `docs/`。
> **数字（测试数 / 固件 host 步数 / 门禁步数 / 验证记录 / 未开工清单）与阻断项一律以 `docs/project-status.md` 为准，本文件不复制** —— 曾因重复而两处走样。
> 原则：**只压措辞、不减事实**（要减先把事实落到 doc 再留指针）；加内容前先问「能否只留一句 + 指针」。

## 1. 设备 / 固件

- squashfs 只读 `/rom` + tmpfs overlay ⇒ `/usr`、`/etc` 落 RAM、**重启即失**；唯一持久分区 `/mnt/UDISK`（ADB push 到 `/usr/lib` 只是临时手段）。
- **抓帧铁律**：`dd if=/dev/fb0` 恒读第一缓冲 ⇒ 先 `cat /sys/class/graphics/fb0/pan`；`pan=0,600` 时 `dd … bs=1024 skip=2400 count=2400`。
- **收帧唯一实现** `CommandFrameCollector`：生产者只 `Append`+时间戳、**不判帧结束**；帧内静默从**最后一段数据**起算；首字节独立宽限窗口（`FirstByteTimeoutMs` ≥ 静默期）。旧判据从**写入时刻**起算 ⇒ 起步慢即**假空响应**+**串帧**。⇒ `…channel-frame-completion-contract.md`
- **会话超时（阶段 8）**：入口顺序 `评估超时 → 打点 → 判定` 是**契约**（破前者＝这次操作被送出去；破后者＝**超时永不触发**）；打点**复用** `hmi_require_permission`→`hmi_has_permission` ⇒ **不得**有周期性调用者。审计写 `/mnt/UDISK/hmi/audit.log`（超 256 KB 滚 `.1`）。⇒ `…session-timeout-and-audit.md`

## 2. 构建 / 烧录

- Tina：`lunch t113_mq_r-tina` → `make package/hmi-runtime/compile V=s` + `pack`；编译前清 `package/gui/lvgl` 宿主残留。烧录：SPI NAND(`storage_type=5`)，**`sunxi-fel` 不能烧 T113** → `openixSuit`；日常走 ADB 热更新。
- `lv_conf.h` 以 `package/hmi-runtime/src-config/` 为准（**改 `lvgl/lv_conf.h` 无效**）；须 `LV_USE_FS_STDIO=1`（`"A:"`）+ `LV_MEM_SIZE≥16MB`；`fangsong.ttf` 缺字勿用；`SIM_ASSET_DIR` 三层转义。
- **Avalonia 资源：同一个 `.axaml` 只声明一次**。默认 glob（`EnableDefaultItems` 为真时 `**\*.axaml`→`AvaloniaXaml`）已收走它，再写 `<AvaloniaResource>` 会被 `GenerateAvaloniaResources` 折进同一 item 列表 ⇒ `@(AvaloniaResource)` 两条同路径 ⇒ 程序集资源目录重复键 ⇒ 运行时 `AssemblyDescriptor.ToDictionary` 抛 `ArgumentException … Key: /Assets/Icons.axaml`（症状＝UI 测试成片死在 `SetupUnsafe`，且 `clean` 重建即好）。**没有 `EnableDefaultAvaloniaItems` 这个属性**。判据：解析 `obj/<cfg>/net10.0/Avalonia/resources`，**不得有重复路径**。⇒ `docs/2026-09-17-avalonia-resource-duplicate-key.md`

## 3. 生成链契约（不能回退）

链路：`SimulatorBuildService.BuildAsync`→`CodeGenerator.GenerateSimulatorModule`→`SimulatorProjectExporter.Export`。

- **净化唯一实现** `Core/CodeGen/CIdentifier.cs`（**前缀字面量只在它内部**；**勿改** `char.IsLetterOrDigit`）。
- **符号族一律走构造器**：`obj_*`/`create_screen_*`/`screen_*`/`layer_*`/`_rec_*`/`check_alarm_*`/`_log*`/`datalog_*`/`_chart_*`/`trend_*`。
- **资源符号**：`CResourceSymbols.Create` 在 `Export` 入口只调一次 ⇒ **导出器内不得再调 `CIdentifier`**（唯一实现 `CResourceDeclarations`）。
- **「哪些控件会被发射」唯一入口** `EnumerateRenderableControls` ⇒ 发射器**不得**遍历 `project.Screens[].Controls`（否则克隆体被发射却无声明 = C 编译错误）。
- **Faceplate 克隆逐属性保真**：`ShallowCopy()`（`MemberwiseClone`）+ 集合**换独立实例** + `ParentId=null`。
- **遮蔽铁律**：文件级 static **绝不**在函数内写「类型前缀+同名」（`lv_obj_t* _cur_x` 是**新自动变量** ⇒ 引用侧空指针）。
- **`obj_*` 不带屏幕前缀**（跨 `create_screen_*`/`apply_animations` 共享）⇒ **控件 id 必须工程内唯一**；canvas 类发射器**赋值侧**也须净化。
- **跨模块**：`ScreenFuncRegex` 不含 `-`/空格 ⇒ 拼 `create_screen_*` 须走 `CIdentifier.ScreenFactory`（否则启动屏重排**静默失效**）。
- `"A:"`：`IMG_PATH_*` **带**（`LV_FS_STDIO`）、`FONT_PATH_*` **不带**。`_soMode`：`static const lv_font_t* font_X = NULL;`（**无** `&`）；模拟器 `extern lv_font_t font_X;`（**有** `&`）。
- **判据先解析成枚举、再按枚举分支**，别比对自产字符串。颜色单一来源 `PaletteDefaults.cs`（禁裸 RGB/hex）；扩展点 `DesignCanvas.RegisterRenderer`/`ThemeFallback`/`PropertyPanelView.CustomValidator`。
- `apply_theme_styles()` 必须在 `lv_screen_load` **之后**（否则中文 tofu），且**不得设 `bg_color`**。
- **动画路径唯一来源** `AnimationPathHelper.GetPathCCode`；App 预览必须调 `CodeGenerator.GenerateAnimationCode`。
- **AI 侧 enum 唯一来源**：`PromptTemplateEngine`（Prompt）/`AiSchemaEnums`（schema）⇒ **`.cs`/`.json` 不得手写 enum**（三坏法见 doc）；占位符须登记 `KnownPlaceholders`；`Get*Schema()` 必须走渲染链。`Screen` 是可拖入**容器控件**（非页面 `HmiScreen`）。
- **权限契约（2026-09-16）**：设备端**不得手写权限位定义**；唯一来源 `ActionPermissionMap`（**须配独立期望表**）/`PermissionMacros`/`SecurityConfigCodeGen`（生成 `generated_security.h`，与生成代码**同批落盘**）；判定公式只在生成头 `hmi_perm_allows`（两消费方**只剩转发**）；**每个 `extern` 声明导出工程都必须有定义**（守卫**反推**宿主依赖）；门只在 `OperationMode != Designer` 发射 ⇒ 默认产物**逐字节不变**、Login/Logout **恒不门控**；导航候选名单一来源 `ScreenNavTargets.cs`（固件 `candidates[]` 是**孪生副本**+**顺序**守卫）；`SIM_TAG_COUNT` **==** 生成代码 `TAG_COUNT`；**`1f` 非法**（`"0.0########"`+`double.IsFinite`）；`LockScreenId` 是 **Id** ⇒ **生成期**解析（`LockScreenTargets`，回退**登录屏**；解析不出**不是错误**）；`HMI_SEC_SESSION_ENABLED` **下沉进生成头**（放段**末尾**：`HMI_SEC_ENFORCE` 可被 `-D` 覆盖）；`EnableAuditLog`/`AuditLogStoragePath` **刻意不下发**。⇒ `…simulator-export-link-selfcontained.md`、`…permission-model-sinking.md`、`…session-timeout-and-audit.md`
- **运行时配置下沉（阶段 9）**：`RuntimeConfig` 10 字段只有 3 个真到设备（`BootScreenId`、`ScanIntervalMs`、`LogLevel`）；`RuntimeConfigCodeGen` → `generated_runtime.h`，固件走 `__has_include` + `#ifndef` 缺省回退。**回退分支给不给数值是语义决定**：轮询周期**无条件要用** ⇒ **必须**给 100；级别常量在门槛关闭时**整段被编译掉** ⇒ **不给**（`HMI_LOG_AT` 的 `#else` 丢掉 `lvl` 实参 ⇒ 未定义也不报错）。**日志宏必须独立成文件 `hmi_log.h`**（**不得**挂聚合头 `hmi_runtime.h` —— 它拉 `lvgl.h`、host 测试没 LVGL ⇒ 门槛链路在主机上编不过 = 闭环退化成注释声称）。`HMI_LOG(...)` **无条件**（启动横幅/FATAL/审计），`HMI_LOG_DEBUG…` 才受门槛约束。**有意行为变更**：默认 Warning 一生效，19 处 `HMI_LOG("DBG:` → `HMI_LOG_DEBUG` **默认静音**。⇒ `…runtime-config-sinking.md`
- **凭据链路（2026-09-16）**：编辑器凭据**不得**下发（跨安全边界）；跨语言哈希向**外部真值**收敛；迭代次数**不存格式**；口令长度不足**不计入**失败计数；`hmi_login` 有用户表则**只跳屏不授权**；模拟器**不抄校验**（`--login` + 生成期掩码）。⇒ `…runtime-credential-verification.md`
- **运行时用户表（编辑器侧）**：预置角色清单唯一来源 `UserRole.BuiltInAll`（`BuiltInRoles` **纯转发**须**表达式体**）；校验判据与固件同源 `Ordinal`（放宽成 `OrdinalIgnoreCase` 会误报重名+放过表外角色）；用户表**草稿两层拷贝**；文本编辑刷新**不得回填详情**；**空用户名不是死条目**；`PasswordMinLength` UI **字符** / 固件 `strlen` **字节**。⇒ `…runtime-user-editor-ui.md`

## 4. 产物可复现

- 非确定性两条已修：**相位**（FNV-1a 32 + **lowbias32 收尾**，必须保留）、**Faceplate 克隆 id**。
- **产物横幅**嵌 git 提交号 ⇒ **A/B 须同一提交**（`git stash push -u -- src tests`）。
- `tools/ReproHarness`（**不入 slnx**）→ `<outDir>/norm/`+`hashes.txt`，输出放 `.refactor-tmp/`；`diff = 0` 先问「**比到了吗**」（先数**文件**）。

## 5. 模拟器渲染（已修，勿回退）

> 细节见 `…headless-render-and-simulator-pitfalls.md`；这里只留**跨控件**判据。

- **LVGL v9 角度基准 = 3 点钟 0°、顺时针**；`StretchMode`：null/empty/Fill→STRETCH，Contain→CONTAIN，None/Center→CENTER。
- **单调时钟**：LVGL tick 必须 `CLOCK_MONOTONIC`；`VariableSimulator` 走 `TimeProvider.GetTimestamp()`/`GetElapsedTime()`（`DateTime.UtcNow` 随 NTP 跳变 ⇒ 波形回卷）；`_hasStarted` 不能删。
- 负 scale 镜像补丁 `patches/lvgl/0002-*.patch`。

## 6. UI 铁律

- **Avalonia Grid**：只设 `ColumnDefinitions` 不设 `RowDefinitions` ⇒ 只单行，`Grid.Row≥1` 全 clamp 到 row 0 ⇒ 子元素重合。
- **AI 取消链路**：唯一接缝在 **App 层 UI 入口**（`ct = default` ⇒ 漏传静默通过）；生成入口必须传 token；取消按钮 busy 时 `Cancel()` 而非 `Close()`；取消 ≠ 失败（守 `AiCancellation*`）。
- **Dock 解析契约**：`ViewLocator.CanResolve` 必须**同时**是 `Match` 与 `Build` 的判据（`Match` 假 ⇒ 框架**不调** `Build`，曾使 12 处 `RegisterFactory` 成死代码）；注册键一律传 `tool.Id`（守 `DockViewResolutionUiTests`）。

## 7. 测试纪律 / 环境陷阱

**临时目录**（唯一入口 `TestSupport/TestTempDir.cs`）：`NewDir`=**要空间**、`NewIsolatedDir`=**要位置**（**须在仓库树外**、仓内拒收、全空**硬失败**）；**向上搜索**类测试（`FindSdkRoot`/`git init`）必须用它，否则逃逸到真实仓库。**禁** `Path.GetTempPath()`/`CreateTempSubdirectory()`/`GetTempFileName()`（`src/` 同禁）。**跑 `git` 的测试，临时目录绝不可落在工作树内**（曾致脏提交）。⇒ `…test-tempdir-two-roots.md`（产品侧 `AppPaths` 见 `…app-paths-session-lifecycle.md`）

**数据根改名 / 只读回退**：改目录名会让已配凭据**静默失效** ⇒ 契约「**读回退、写不回退**」，**判据是「新位置不存在」而非「读不出来」**；append-only 审计日志**新旧都读** ⇒ **无搬移崩溃窗口**；只读解析**不得建目录**。⇒ `…app-data-root-migration.md`

**时序 / 假时钟**：**不用 `Thread.Sleep(固定值)` 等回调**（排 ThreadPool）→ `TestWait.Until` 等**产品状态**；**`async`/`sync` 等待不可互换**；`Task.Delay` 续体同样排 ThreadPool ⇒ 用**专用 `Thread`**；**扫 flaky 按写法特征**。假时钟：`GetElapsedTime(...)` 非 virtual（`CS0506`）⇒ 只覆写 `GetTimestamp()`。⇒ `…test-timer-flakiness.md`

**判据别用过窄的绝对余量**（同族五例）：`SlowFirstByte_…` 首字节窗口与注入延迟只差 400 ms（满负载被吃掉，门禁 37 s vs 单跑 3 s）⇒ 拉到 **5000 ms**；`ModbusTcpRuntimeDriverTests` 的 **2000 ms 是测试自己的 helper 塞的**（非产品默认）⇒ 正例窗口拉 10 s、需报错的负例各自覆写；**「等 git 进程」必须单源**（`IsGitAvailable()` 3 s / `RunGit()` 15 s ⇒ 同为 `GitProcessWaitMs`，**更紧的那个必然先失效**）；`CommandFrameCollectorTests` ① 延迟生产者 `Task.Run`+`await Task.Delay` ⇒ **续体排 ThreadPool**、被阻塞 IO 占住 ⇒ 推迟过 800 ms 窗口（应改**专用 `Thread`**）；② 三段数据**各起一线程** ⇒ 段间间隔 = 两次启动抖动**叠加**，名义 70 ms 对静默 100 ms 只留 **30 ms** ⇒ 改**同一线程按时间轴依次投递** + 尺度取 **0.6 × 静默期**。⇒ **判据靠相对大小成立，不靠绝对余量；一条「临时红」里可能同时藏着「队列」与「绝对余量」两种病 —— 改掉一种必须再跑一轮。**

**环境（本沙箱）＝阻断项 11（已修，根因是环境不是产品）**：对**首次出现的文件路径**收 ≈**60 ms**、且该开销**全局串行**（8 进程各建 20 文件 ≈ 8× 单进程墙钟；20 核全程闲置；tmpfs 与 btrfs 同；三种 spawn 同）。**目录创建几乎免费** —— 曾把 python3 启动 90 ms 误算成目录开销，量测时把「进程启动」与「文件系统操作」分开计时。⇒ `git init` 在**新建**目录恒定 **1.2 s**（固定目录 58 ms）；大队灌满队列时**外部子进程 1 ms → 4~10 s**（0 失败，只是排队）⇒ 撞穿任何等待窗口（阻断项 11：43 s ≈ 三次 15 s 窗口被撞穿）。**窗口不是解药，xUnit 并行度也不是**（20/8/4/2 线程墙钟 83~90 s 同量级 —— 吞吐由那条串行资源决定）⇒ 解药是**让 spawn 外部进程的用例在队列为空时单独跑**（门禁第 3 步已拆两段：大队 Core 1892 + git 8 条 7 s）。判据 `bash tools/probe-fs-newpath-cost.sh`（纯 shell，不碰 .NET）。⇒ `docs/2026-09-17-sandbox-newpath-serialization.md`

**本机 / 测试宿主**：`ulimit` **别凭记忆**（2026-09-17 实测 Bash 工具里 `-v` 是 **unlimited**、`-u` 255897，与早前记的「4 GB / 4096」不符 ⇒ 用前自己 `ulimit -v` 确认）；`dotnet test HmiEditor.slnx`（Debug、隐含 build）**必被 OOM 杀**（137、无输出）⇒ **跑全量走 `bash scripts/ci-gate.sh`**（Release + `--no-build --no-restore`）。
**「卡住无输出」看日志 mtime，别看时长**；加 `--blame-hang --blame-hang-timeout 90s --blame-hang-dump-type none` ⇒ 报「**测试主机进程崩溃**」+「**崩溃时运行的测试**」清单（清单里每条都同时"在运行" ⇒ 那就是并发集合）。定位手法：**一次只放开一个变量**（单独跑 → 走 slnx 只匹配它 → 全量并发 → 再换 Configuration）；「**单独绿 + 并发崩**」⇒ 差异在并发、不在用例。⇒ 阻断项 12。

**别把诊断工具当因果**：`--blame-hang` 自己改运行环境（附加数据收集器、按不活动判定）⇒ **「带 blame 崩 / 不带 blame 绿」不是「并发 vs 配置」的因果**，两侧必须**同一条命令**。**改测试配置要做「行为可观测」的正控** —— 「把值写成非法类型、看它报不报错」**这条判据不成立**：
2026-09-17 实测「值写成字符串」确实不报错，但同一天两个**行为可观测**的探针证明了文件**确实被读取**
（`diagnosticMessages:true` ⇒ 输出 239→**317** 行；`methodDisplay:"method"` ⇒ 带类名的用例名 **142→0**）。
⇒ 正控要挑**能改变可观测行为**的键，别挑「期待它报错」的键。**xUnit 不会自动拷贝 `xunit.runner.json`**
—— 靠 csproj `CopyToOutputDirectory`，或直接写进 `bin/<cfg>/<tfm>/`（后者还不污染仓库）。**「回退后失败面反而扩大」＝构建产物污染的强信号** ⇒ 先 `dotnet clean` + 全量重建，别再推理「哪个改动害的」。**引用一条「绿」前先确认它跑在修复之后**（`git log --date=iso-strict` 对 `stat -c %y <日志>`）—— 「有一次绿」对间歇缺陷等于零信息。⇒ `docs/2026-09-17-verify-end-to-end-revalidation.md` §6/§7

**沙箱文件系统 / 命名空间**：新路径是**逐文件 bind-mount** ⇒ 被挂住的文件 `rm` 报 EBUSY、`umount` 反说未挂载 ⇒ **删不掉，只能人工清**；delete shim 对**文件**目标算回收站路径有 bug（`<file>/.Trash-0` ⇒ fail-closed，owner 变 root）；`os.rename` 未被拦但 `/tmp` 跨 fs（EXDEV）⇒ 移进仓库 `.Trash-0/files/`（同 fs 且已 gitignore）。**`sudo` 不可用**（`/etc/sudo.conf` owner=65534 ⇒ `setresuid … Invalid argument`，与密码无关）。批量删除按后代**文件**数判 50 个阈值，别落在 `.refactor-tmp/` 之外的大目录上。`{ … } | tee` 管道里的 `exit` **只退子 shell** ⇒ 断言写在管道外。**每个 Bash 调用有独立 PID 命名空间** ⇒ 交互 shell 里 `pgrep dotnet` 恒空、**看不到后台任务的进程**；要抓「卡住」时的现场（`wchan` 直方图 / 线程名 / `State`），必须让**脚本自己**抓（它是那些子进程的父）。

**负向验证**：完整纪律在 `generated-artifact-rollout` 技能里；最易翻车的四条：① **一组破坏只证明它打算证明的那件事**；② **红数少于预期也要查**、**没红先怀疑「注入与缺陷不等价」**；③ **期望文本须落在该用例自己的失败块里** —— **别用 `[FAIL]` 定位**（那是 xUnit 末尾汇总行），块头是 `失败 <完整名> [N ms]` 且判据**要容忍 Theory 参数里的空格**（`失败\s+(\S+)\s*\[` 会一条 Theory 都匹配不上而 `[Fact]` 侥幸通过）；④ **解析器自身要有双向自检**，否则「未命中」与「守卫无效」**无法区分**（连踩两轮 1/8、7/8）。其余：**先确认基线全绿**、还原后**复跑回全绿**；**惰性状态读取不能当判据**（判「是否超时」必须触发一次评估）；**定向 `--filter` 替代不了全量门禁**；**比文本先后的判据须另配行为判据**；新测试避开 `TestAssertionStyleGuardTests`（C 断言走 `CSource.Assert*`，字面量含 `(`+`,` 即违规）；拆巨型文件前**先补行为测试网**。

**静态守卫**：`check-path-hygiene.sh` 判据是「**路径被引号包裹**」而非「路径出现」；范围 `src/ tools/ scripts/ firmware/`（`tests/` 排除）。**契约：注释里举例路径不加引号**。⇒ `…path-hygiene-and-orphan-artifacts.md`

## 8. 配置诚实性（面板上有的 ≠ 生效的）— 阶段 10

- **接线铁律**：接线前，应用级默认值**必须等于原硬编码常量**（否则默认行为会变）。**`null` = 「用下游默认」**，别让「没给」与「给了 X」不可区分（`int Width = 800` 须改 `int?`，由 `Core/Services/ScreenFactory.cs` 单点兜底：显式参数 > 工程默认 > 1024×600）。改设置走 `SettingChanged` **即时生效**，不必重启。
- **网格点阵 ≠ 吸附步长**：点阵 **10px**、吸附 **5px**（二分格）是**两个数**、曾**三处建模**；合并会连带改掉拖拽精度。点距唯一来源 = `SettingsService.Interaction.GridSize`（`SnapGrid.GridSize` 默认值须与它一致；`DesignCanvasView.axaml` **不得**再写 `GridSize="10"`）。
- **设置持久化两条**：① 反序列化 `Dictionary<string, object?>` 拿到的是 **`JsonElement`**，而 `Get<T>` 用 `is T` 判等 ⇒ **磁盘上每个值都退回默认值**；更坏的是打开一次设置对话框会把默认值灌回控件 → `ValueChanged` → **抹掉用户存的值** ⇒ 必须 `Dictionary<string, JsonElement>` + `Materialize()` + `Get<T>` 折算。② **`int : long : double` 三元链的公共类型是 `double`** ⇒ 整数装箱成 `double`、`is int` 判据**静默失效** ⇒ `MaterializeNumber` **必须早期返回**。
- **受管清单三向守卫**（`App/Services/ConfigFieldInventory.cs` 登记「面板上有、其实不生效」的字段 + `NotYetEffective` 置灰）：清单 ↔ **字段真实存在** ↔ **白名单外零消费**，外加清单 ↔ 面板标注（**键 + 控件名 + XAML 里真有该 `x:Name`** 双向）；维护方式是**接线后删条目**（不是改状态）。两条易错：**「写入面」≠「消费面」**（`AgentToolFactory` 把 AI 的值写进工程配置但无人读 ⇒ 算未接线）；**`Control = null` 是第三态** —— 键没人读、但控件真生效（`Theme.AccentColor` 写的是 `AppThemeService.CustomAccent`），不区分会把能用的控件灰掉。`Core/Models/EditorConfig.cs` 已**废弃为只读兼容载体**（只为老 `project.hmi` 无损往返）⇒ **不要再往上加字段**。⇒ `docs/2026-09-16-config-honesty-and-editor-preferences.md`

## 9. 未开工（完整清单见 `docs/project-status.md`）

Dock **真实拖拽**；AI SSE 流式；`design-previews` 无共享模型；`TemplateGenerationSchema.category` 硬约束；`TempRoot` 稳定名目录**无上限**；`EnableTamperDetection` **零消费**（宏下发了、固件无 `#if` ⇒ 属**新功能**）；`update_runtime` 的 **2/5 参数空转**（`watchdog_timeout_ms` / `enable_hmi_reboot` 写进工程也没后果 ⇒ AI 会照着描述改死键）。
