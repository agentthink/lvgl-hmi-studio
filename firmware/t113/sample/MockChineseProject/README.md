# 示例工程定义 —— `MockChineseProject`（阶段 307）

这里存的是 `firmware/t113/generated/`（设备侧示例产物）的**源工程定义**，不是完整 `.hmi`。

## 为什么不是完整 `.hmi`

源包由用户提供（`MockChineseProject.hmi`，`editorVersion = MockHmiGen/1.0`，2026-08-17），
**24.3 MB**，构成：

| 条目 | 大小 | 入库？ |
|---|---|---|
| `assets/asset_font_cjk.ttc` | **19,484,784 B ≈ 18.6 MB** | ❌ **不入 git**（与固件包对 `font_cjk_sc.ttf` 的处置一致） |
| `assets/asset_bg.bmp` | 2,457,654 B | ❌ 不必 —— 与 `firmware/t113/generated/assets/` 里那份**逐字节相同** |
| `assets/asset_center.bmp` | 2,457,654 B | 同上（且与 `asset_bg.bmp` 也逐字节相同 ⇒ 工程里本就重复） |
| `assets/asset_banner.bmp` | 640,054 B | 同上 |
| `assets/asset_logo.bmp` | 360,054 B | 同上 |
| `project.json` | 51,600 B | ✅ 入库 |
| `assets.json` | 1,278 B | ✅ 入库（**摘掉了字体条目**） |
| `manifest.json` | 273 B | ✅ 入库（`assetCount` 随之改为 4） |

⇒ 仓里只放**工程定义**（≈ 53 KB），资产**复用** `firmware/t113/generated/assets/`。

## 怎么打回一个 `.hmi`

```bash
bash scripts/repack-sample-hmi.sh                 # → build/sample/MockChineseProject.hmi
bash scripts/repack-sample-hmi.sh /tmp/x.hmi      # 或者指定输出
```

产物 **确定性**（时间戳钉死为 1980-01-01，压缩参数固定）⇒ 同一输入两次打出来 **sha256 相同**
（实测 `7cecf687…`，包约 **236 KB** —— deflate 把 BMP 压得很好）。

打出来的包**不含字体**；若要在编辑器里打开并重新导出，需要自己补一份 CJK 字体进 `assets/`
（这属于"再导出"那一步，见下）。

## 与本目录/产物的一致性由谁守

| 关系 | 判据 |
|---|---|
| 工程名 ＋ 版本 ↔ `screens.c` 头部（`Mock中文显示测试工程 v2.0.0-test-cn-img`） | `SampleProjectProvenanceGuardTests` |
| `assets.json` 的每个图片资产 ↔ `firmware/t113/generated/assets/`（**按名按字节数**） | 同上 |
| 字体条目不许回来（否则 19.5 MB 悄悄入库） | 同上 |
| `manifest.assetCount` ↔ 实际条目数 | 同上 |
| 设备产物目录清单 ↔ `firmware/t113/generated/PROVENANCE.md` | `DeviceFixtureProvenanceGuardTests` |
| `tags.*` 与生成器契约不漂移 | `DeviceTagsContractDriftTests` |

## 还没做（下一步）

- **再导出漂移判据**：加载打出来的 `.hmi` → 跑设备侧导出（`CodeGenerator.GenerateSharedLibraryProject`）
  → 与 `firmware/t113/generated/screens.c` 比对。**脚本化导出 CLI 当前不存在**（编辑器里是交互式动作），
  这是它没做成的前提。
- `screens.c` 是否改为"构建期生成、不入库" —— 待拍板。
