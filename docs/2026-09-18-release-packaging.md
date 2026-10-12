# 编辑器侧「一键出包」—— 登记表 §4 第 29 项

> 日期：2026-09-18 ｜ 关联：`docs/2026-09-17-open-items-register.md` §4 第 29 项
> （"发布包 / 回滚：编辑器侧无一键出包脚本"）

---

## 0. 结论

编辑器侧此前只有 `dotnet build` / `dotnet publish` 的手工步骤：没有版本清单、没有校验和、
没有随包的安装/升级/回滚说明，也没有任何东西保证"包里写的版本 == 源码里的版本"。
本轮补上 `scripts/package-release.sh`（真实逻辑在 `tools/ReleasePackager`，可单测），
并把**版本清单**接到 §4 第 29 项第一部分建立的四个版本标记上。

## 1. 一键出包

```bash
# 一条命令：publish 编辑器 → 装配文档/清单/校验和 → tar.gz
bash scripts/package-release.sh --out release

# 需要把 T113 交叉工具包一起发（客户机无需完整 SDK）时
bash scripts/package-release.sh --out release --with-kit

# 对任意已装好的包重跑同一套校验（不重新打包）
bash scripts/package-release.sh --verify release/zhishu-hmi-editor-<版本>-<git短号>-<日期>
```

实测（2026-09-18，本机）：

```
[1/4] dotnet publish (Release / linux-x64) ...
[2/4] 跳过交叉工具包（未指定 --with-kit）
[3/4] 装配文档 / 清单 / 校验和 ...
   版本清单: 编辑器 1.0.0 (git fc8a633) / 固件 1.1.0 / 目标 allwinner-t113 / ABI 3 / LVGL 9.5
   载荷 96 个文件 / 34889 KB
[4/4] 打包 tar.gz ...
✅ 完成: release/zhishu-hmi-editor-1.0.0-fc8a633-20260918.tar.gz
```

发布用 `-r <RID>`（默认 `linux-x64`）而不是可移植发布：同样的 App，可移植发布产物
**574 MB**，RID 发布 **35 MB**（Avalonia 的跨平台原生库被按 RID 裁掉）。

## 2. 包内布局

```
zhishu-hmi-editor-<版本>-<git短号>-<日期>/
├── editor/            dotnet publish 的编辑器（框架依赖，需 .NET 10 运行时）
├── docs/              随包固件文档（烧录指南 / WavePI-S6 刷写 / 版本信息 / 触摸诊断）
├── UPGRADE.md         安装 / 升级 / 回滚说明（由版本清单生成）
├── manifest.json      版本清单 + 逐文件 字节数/sha256
└── SHA256SUMS         sha256sum -c 兼容
```

## 3. 版本清单怎么来的（四者对应落到发布物上）

`manifest.json` 的 `Versions` 同时写两侧八个值：

| 侧 | 值 | 事实源 |
| --- | --- | --- |
| 编辑器 | 版本 / 目标 id / ABI / LVGL | 发布的程序集 + `AllwinnerT113Target`（`Metadata.Id`、`LvglConfig.AbiVersion/LvglVersion`） |
| 固件 | 版本 / 目标 id / ABI / LVGL | `firmware/t113/app/runtime/hmi_runtime.h`、`hmi_ui_api.h` |

两侧都经 `FirmwareVersionMarkers`（`src/HmiEditor.Core/Services/FirmwareVersionMarkers.cs`）
解析 —— 与 §4 第 29 项第一部分的跨边界守卫**同一处**，不再有第二份正则。
`UPGRADE.md` 明确写了"两侧不一致时不要混用本包的编辑器与旧固件"：设备端 `dlopen` 会比对
`abi_version`/`lvgl_version`，不一致直接拒绝加载。

## 4. 升级 / 回滚（写进包里的 `UPGRADE.md`）

- **编辑器升级**：保留旧包目录 → 解包新包 → 用新目录的 `editor/` 启动；工程（`*.hmi`）与包无关。
- **编辑器回滚**：包按目录自包含 ⇒ 用旧目录的 `editor/` 启动即可（所以升级要求保留旧目录）。
- **设备侧 UI 热更新 / 回滚**：`screen_manager.c` 的两级加载 ——
  先 `/mnt/UDISK/hmi/libhmi_ui.so`（OTA/IDE 部署位），失败回退
  `/usr/lib/hmi/libhmi_ui.so`（出厂位）⇒ 回滚 = 删除/替换 UDISK 那份并重启 `hmi_runtime`。
- **设备侧整机固件**：没有 A/B 或整包 OTA 回滚，回滚 = 重刷整包，步骤指向包内
  `docs/flashing-guide.md`。

## 5. 判据清单

| 判据 | 位置 |
| --- | --- |
| 清单的固件侧四值 == 头文件、编辑器侧四值 == 目标插件、两侧互相一致 | `tests/HmiEditor.Device.Tests/ReleasePackageTests.cs` |
| 装配完自检通过；`SHA256SUMS` 覆盖 `manifest.json`；清单不登记两个封条文件自身 | 同上 |
| **可失败性**：改一个字节 / 删一个文件 / 换一份源码状态，自检必须点名报出 | 同上（3 条负例） |
| `UPGRADE.md` 引用的固件文档确实随包发出、引用的设备端路径真实 | 同上 |
| CLI 级复核：`--verify <包目录>` | `tools/ReleasePackager`（`--verify` 模式） |

## 6. 负向验证

| NV | 注入 | 命中 |
| --- | --- | --- |
| A | 发布包里的 `docs/version-info.md` 追加一行 | CLI `--verify` 报 `sha256 不符: docs/version-info.md`（清单与校验和各一次） |
| B | 删掉包里的 `docs/flashing-guide.md` | 自检报"登记的文件不存在: docs/flashing-guide.md"；单测 `Verify_DetectsDeletedPayloadFile` |
| C | 拿另一个版本的源码状态（`HMI_RUNTIME_VERSION_STR="9.9.9"`）去核 | 报"版本清单与当前仓库事实源不一致"；单测 `Verify_DetectsVersionTableThatNoLongerMatchesTheRepo` |
| D | 包目录里没有 `editor/` | 报"缺少 editor/ 产物"；单测 `Assemble_ReportsMissingEditorArtifacts` |

## 7. 还没做的（如实登记）

- **Windows 发布包**：脚本支持 `--runtime win-x64`，但本轮只在 Linux 上实测过。
- **代码签名 / 校验来源**：目前只有 sha256 完整性校验，没有签名与可信发布渠道。
- **`--with-kit` 端到端**：工具链路径已接到 `T113KitBuilder`，但本轮未跑（要复制 ~300 MB
  工具链）；`kit.json` 标记与 `T113SoBuilder` 的探测约定另有既有测试覆盖。
- **增量/差分升级**：整包替换，不做差分。

## 8. 与用户文档的关系

用户手册的"发布/升级"相关描述见 `docs/user-guide.md`；故障排查里原先写着
"据登记表复核时还没有一键出发布包脚本"——本轮已同步改正并指向本文件
（`docs/troubleshooting.md` §4.2）。
