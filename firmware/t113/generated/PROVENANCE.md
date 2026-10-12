# `firmware/t113/generated/` 的来源与可再导出性（阶段 306）

> **一句话**：这个目录是**内置样例产物**（非产品工程），其中 `tags.*` 受生成器契约判据守着；
> **`screens.c` 与 `assets/` 的源工程已入仓**（阶段 307）—— 见 `firmware/t113/sample/MockChineseProject/`
> ＋ `scripts/repack-sample-hmi.sh`（现场把工程定义与这里的资产打成 `.hmi`）。

## 1. 清单（**机械守着的**：增删文件必须同步改本块，否则判据红）

<!-- INVENTORY:BEGIN -->
PROVENANCE.md
assets/asset_banner.bmp
assets/asset_bg.bmp
assets/asset_center.bmp
assets/asset_logo.bmp
generated_security.h
screens.c
tags.c
tags.h
<!-- INVENTORY:END -->

## 2. 逐项来源

| 文件 | 是什么 | 谁产出 | 可再导出？ |
|---|---|---|---|
| `tags.h` / `tags.c` | 设备侧标签表（`tag_meta_t` / `HMI_UI_TAG_META` / `TAG_TABLE`） | 编辑器导出（`RuntimeTagsCodeGen`） | 🔶 **手工维护的示例**，但由 `DeviceTagsContractDriftTests` 与**生成器契约**逐条绑定（阶段 289 咬过一次真实漂移） |
| `generated_security.h` | 安全/会话门槛等下沉常量 | 编辑器导出（`RuntimeConfig` 下沉） | 🔶 同上，随生成头一起换 |
| `screens.c` | UI 模块源码（`libhmi_ui.so` 的 C，`#include` 闭包由 `FirmwareGeneratedIncludeClosureTests` 守） | 编辑器导出（**SharedLibrary** 输出模式） | ✅ **源工程已入仓**：`firmware/t113/sample/MockChineseProject/`（`project.json` 的 name/version 与本目录头部逐字一致） |
| `assets/*.bmp` | 4 张示例图片资源（设备侧按 `A:` 前缀加载） | 同上（随工程导出） | ✅ 同上；这 4 个文件就是工程里那 4 张图（名单与字节数逐个一致，由 `SampleProjectProvenanceGuardTests` 守） |

**别拿当前的 `tools/MockHmiGen` 当本目录的来源**：它现在产出的是 `banner_zh/logo_chip/bg_panels.bmp`
（**名字对不上**），而本目录来自 **`MockHmiGen/1.0`（2026-08-17）那一版**的产物 —— 所以它的
`manifest.json` 里写着 `editorVersion: "MockHmiGen/1.0"`。那份 `.hmi` **24.3 MB**，其中 **19.5 MB** 是
`asset_font_cjk.ttc`（CJK 字体，**按约定不入 git**，与固件包对 `font_cjk_sc.ttf` 的处置一致）。

⇒ 仓里存的是**工程定义**（`project.json` / `assets.json`（已摘掉字体条目）/ `manifest.json` ≈ 53 KB），
资产**复用本目录的 `assets/`**；要 `.hmi` 就现场打：

```bash
bash scripts/repack-sample-hmi.sh            # → build/sample/MockChineseProject.hmi（确定性，可 diff）
```

## 3. 两个现状读数（都不是我编的，命令在下面）

- **资产体积**：4 张 BMP 合计 **5,915,416 B ≈ 5.64 MB** 入库。
- **重复**：`asset_bg.bmp` 与 `asset_center.bmp` **逐字节相同**（同 sha256）⇒ 其中 **2,457,654 B ≈ 2.34 MB**
  是**纯重复**。

```bash
ls -l firmware/t113/generated/assets/ | awk 'NR>1 {s+=$5} END {print s" B"}'
sha256sum firmware/t113/generated/assets/*.bmp | awk '{print substr($1,1,16), $2}'
```

## 4. 现在能做什么、还差什么

| | 状态 |
|---|---|
| 源工程在仓 | ✅ `firmware/t113/sample/MockChineseProject/`（工程定义；字体按约定除外） |
| 现场打 `.hmi` | ✅ `scripts/repack-sample-hmi.sh` —— 确定性（时间戳钉死），同一输入两次 sha256 相同 |
| 源工程 ↔ 本目录的**资产**对应 | ✅ `SampleProjectProvenanceGuardTests`（名单 ＋ 字节数逐个一致） |
| **`tags.*` 的生产者已不在仓**（阶段 314 查实） | 按夹具头原文在 `src/`、`tools/` 的 `.cs` 里**零命中**；`CodeGenerator.SharedLibrary.cs:26` 明写 `screens.c` 不含 `TAG_TABLE`/`TAG_BINDINGS`（由**宿主 `tags.c`** 提供）；当前 `RuntimeTagsCodeGen` 走模拟器路径与 `.so` 的 `tags.h`，**形态不同** ⇒ `tags.*` 属**手工维护的宿主产物**，契约由 `DeviceTagsContractDriftTests` 守 |
| **再导出**（实测，阶段 308） | ⚠ **逐字节复现不可能、且夹具相对当前生成器已陈旧**：用仓内工程定义跑当前生成器，`screens.c` 忽略 `Project:` 行后 **514 vs 522 行、500 行不同**（当前生成器多一行 `// Generator: HmiEditor.CodeGen 1.0.0+<git sha>`）；`tags.h` **2,210 B vs 夹具 3,535 B**，且**运行时标签数 = 0**（夹具那份标签不来自本工程当前的绑定） |
| 是否让 `screens.c` 改为构建期生成、不入库 | ⏳ 待拍板（要先有可脚本化的导出 CLI，当前没有） |

## 4.5 生成物**不入库**（阶段 319–320）

`screens.c` 是**生成物**，**不入库**（`.gitignore`）：按拍板"**构建前在开发机生成、构建机只消费**"
（设备构建机没有 dotnet）。

```bash
bash scripts/gen-device-tree.sh --write   # 开发机：生成到 firmware/t113/generated/
bash scripts/gen-device-tree.sh --check   # 只校验（门禁第 7 步就跑它；缺席/漂移 ⇒ rc=1）
```

| 情形 | 判据行为 |
|---|---|
| 生成物**在场** | 与库中基准**逐字节比**（`DeviceExportReproducibilityTests`） |
| 生成物**缺席**（裸跑 `dotnet test`） | **直接红**（用户拍板 2026-10-11）：`DeviceExportReproducibilityTests` 报「缺少生成物 screens.c」并指路 `--write`。理由：缺席等于这条判据没在保护任何东西，红着要你把生成跑起来 |
| 出包时缺产物 | `tina/build.sh` 打印"缺哪个 ＋ 跑 `--write`"后 `exit 1`（**不引入 dotnet**） |

## 5. 与判据的对应

| 性质 | 守着它的判据 |
|---|---|
| 清单与本文档一致（增删即红） | `DeviceFixtureProvenanceGuardTests`（本文件第 1 节的两个标记之间） |
| `screens.c` 必须标明是"内置样例"（不许被当产品工程） | 同上（查 `screens.c` 头部标记） |
| `tags.*` 与生成器契约不漂移 | `DeviceTagsContractDriftTests` |
| `screens.c` 的 `#include` 闭包在仓内可解析 | `FirmwareGeneratedIncludeClosureTests` |
| 设备侧 ABI 尾巴（`alarm_snapshot` 等） | `AlarmShelveAbiTests` 等 |
