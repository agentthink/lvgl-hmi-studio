# 版本标记四者对应 —— 登记表 §4 第 29 项（第一部分）

> 日期：2026-09-17 ｜ 关联：`docs/2026-09-17-open-items-register.md` §4 第 29 项
> （`Phase 0/5` 清单里未由 ci-gate/verify 覆盖的项）

---

## 0. 结论

`2026-08-19-project-audit-and-development-plan.md` 的 Phase 0 里写着
"**建立版本标记：IDE、CodeGen ABI、固件镜像、板级配置四者对应关系**"。
复核后：**四个标记都存在，但"对应"只是注释里的约稿，没有任何东西去比**。本轮把四组对应
变成**可执行判据**，并顺手修掉一处**真的不兼容**。

## 1. 四个标记在哪里

| 标记 | 落点 | 值 |
|---|---|---|
| 固件版本 | `firmware/t113/app/runtime/hmi_runtime.h` `HMI_RUNTIME_VERSION_{MAJOR,MINOR,PATCH,STR}` | 1 / 1 / 0 / `"1.1.0"` |
| 固件目标 ID | 同文件 `HMI_TARGET_ID` | `"allwinner-t113"` |
| ABI 版本 | `firmware/t113/app/runtime/hmi_ui_api.h` `HMI_UI_ABI_VERSION` | `3u` |
| LVGL 版本 | 同文件 `HMI_UI_LVGL_VERSION` | `0x905`（= 9.5） |
| 编辑器侧（目标插件） | `AllwinnerT113Target.LvglConfig` | 本轮之前**没有**声明版本 |
| 编辑器侧（工程） | `HmiProject.LvglVersion`（下拉可选 8.3…9.5） | 默认 `9.5` |

运行时**真的会校验**后两者：`screen_manager.c` 在 `dlopen` 之后比对
`api->abi_version != HMI_UI_ABI_VERSION` / `api->lvgl_version != HMI_UI_LVGL_VERSION`，
不一致直接拒绝加载。

## 2. 两处"注释里的约稿"（本轮变成判据）

1. `hmi_runtime.h`：`/* 固件版本（与 docs/version-info.md 同步） */` ——
   文档里确实写着"当前版本：v1.1.0"且一致，但**没有任何机制**阻止一边改一边忘。
2. `hmi_runtime.h`：`/* 目标平台标识（与 AllwinnerT113Target.Metadata.Id 对齐） */` ——
   值确实一致，同样是靠人记得。

## 3. 修掉的那处**真的不兼容**

编辑器允许把工程 LVGL 版本选成 **8.3**，而 T113 板子的运行时是 **9.5**。这个组合会怎样：

- `LvglApiCompatChecker.Process(code, project.LvglVersion)` **只在工程为 9.x 时**才把 v8 老 API
  改写成 v9 写法（`lv_img_set_src` → `lv_image_set_src`、`lv_img_set_zoom` → `lv_image_set_scale` …）；
- 工程选 8.3 时它**跳过改写**，生成的 C 里就留着 9.x 已改名/移除的符号；
- 而目标板的头文件是 LVGL 9.5 ⇒ **根本编译不过**；
- 更糟的是生成的 `.so` 里 `lvgl_version = HMI_UI_LVGL_VERSION`（9.5），
  所以运行时**会接受**它 —— 声明与实际不符。

**处置**：把"板子运行时自带的 LVGL 版本 / ABI"写进硬件目标契约，由目标自己校验工程。

| 改动 | 说明 |
|---|---|
| `LvglConfig` 增加 `LvglVersion` / `AbiVersion`（**可省略**） | 省略 = "**未声明**" |
| `AllwinnerT113Target` 声明 `"9.5"` / `3` | 与固件头文件逐字对应（守卫盯着） |
| `RockchipRK3506Target` **刻意留空** | 仓库里没有该板固件可对照（只有文档提到"LVGL v9 on ARM"）——**填一个没验证过的版本号比留空更糟** |
| 新增 `TargetValidation.CheckProjectLvglVersion`（契约层，两块板共用） | 主版本不一致 ⇒ **Error**（不是 Warning：这是"确定不能用"，不是"可能有问题"） |
| `ValidateProject` 调用它 | 结果直接显示在工程设置的"设备兼容性"行 |

未声明 LvglVersion 的目标（RK3506）自动跳过 —— 缺依据就不下判断。

## 4. 插件文案的一个既存问题（本轮登记，未修完）

`Hmi.Hardware.Builtin` 的校验消息（如 `"Screen '{name}' … 超过默认分辨率"`）是**用户可见**的
硬编码中文，而**本地化守卫只扫 `src/HmiEditor.App`** ⇒ 插件程序集不在扫描面内。

本轮做了一半：`ValidationIssue` 本来就带 `Code`，于是 App 侧显示时**先按
`hw.issue.<code 小写>` 查字典**，查不到才回落到插件自带文案。新规则
（`LVGL_VERSION_MISMATCH`）因此有中英文案；**老规则的迁移尚未做**（需逐个补键）。

> 已登记：插件侧全部校验文案迁进字典（或以 `Code` 为准统一由 App 侧渲染），
> 并把本地化守卫的扫描面扩到插件程序集。

## 5. 判据清单（`tests/Hmi.Hardware.Tests/TargetContractVersionTests.cs`，12 条）

| 用例 | 对应关系 |
|---|---|
| `FirmwareVersionString_MatchesItsOwnNumberComponents` | 版本串 ↔ MAJOR/MINOR/PATCH（同一头文件内自洽） |
| `VersionInfoDoc_MatchesTheFirmwareHeader` | `docs/version-info.md` ↔ 固件头文件 |
| `FirmwareTargetId_MatchesTheEditorSideTargetId` | `HMI_TARGET_ID` ↔ `AllwinnerT113Target.Metadata.Id` |
| `T113Target_DeclaresTheAbiAndLvglVersionTheFirmwareRuntimeChecks` | `hmi_ui_api.h` ↔ 目标插件声明 |
| `FirmwareRequiredLvglVersion_IsSelectableInTheEditor` | 固件要求 ↔ 编辑器版本清单（`LvglApiCompatChecker.SupportedLvglVersions`） |
| `NewProjectsDefaultToAVersionTheEditorKnows` | 新建工程默认值 ↔ 版本清单 |
| `DeclaredTargetVersions_AreAlwaysKnownToTheEditor` | 目标要么不声明、要么声明一个认得的版本 |
| `ProjectTargetingAnIncompatibleLvglMajor_IsAnError` + 3 条 Theory/Fact | 工程 ↔ 目标：不兼容必须报 Error，同主版本放行，未声明跳过 |

顺带把"编辑器认得的 LVGL 版本清单"从 `ProjectSettingsView.BuildLvglVersionItems()` 的**局部数组**
收进 `LvglApiCompatChecker.SupportedLvglVersions` —— 那是版本兼容知识所在的层，
而清单放在视图里时，别的入口（守卫、目标插件对照）想用就得再抄一份。

## 6. 负向验证（5 组，全部命中指名用例）

| NV | 注入 | 命中 |
|---|---|---|
| A | `HMI_UI_ABI_VERSION 3u` → `4u` | 目标声明 ↔ 头文件守卫 |
| B | `HMI_RUNTIME_VERSION_STR` → `"1.1.1"` | 版本串自洽（**并连带**文档一致性守卫 —— 两条判据是互锁的） |
| C | `HMI_TARGET_ID` → `"allwinner-t113s3"` | 固件 ↔ 编辑器目标 ID |
| D | 文档里版本改成 `v1.0.9` | 文档 ↔ 代码 |
| E | 版本清单删掉 `"9.5"` | 固件要求可被选中（**并连带**目标声明守卫） |

## 7. 还没做的（如实登记）

- **`CI 增加 firmware host tests 与模拟器导出验证步骤`**：已在阶段 10/11 落地（`ci-gate.sh`
  第 4 步跑固件 host 8 步；`verify.sh` 做 Golden 导出 + 真实 LVGL 编译）—— 本轮复核后**可勾掉**。
- **发布包 / 升级回滚**：固件侧有 OTA 三阶段与 UDISK 优先加载 + 出厂回退；**编辑器侧没有**
  "一键出发布包"的脚本。属独立议题。
- **资源预算校准**：`ResourceConstraints` 里有 Flash/RAM/堆预算的**声明值**，但没有
  "实测 vs 预算"的判据。属独立议题（需要真实固件体积读数）。
- **用户文档 / 示例工程 / 故障排查手册**：示例工程（Golden）已有；用户文档未写。
