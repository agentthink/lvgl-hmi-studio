# SimulatorProjectExporter 按域拆 partial（2026-09-18，P0-3）

## 1. 背景

`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` 长期是单文件 **1540 行**：

| 指标 | 拆分前 |
|---|---|
| 行数 | 1540 |
| 顶层成员 | `SimulatorExportResult` record + 11 个方法 |
| partial 分部 | 0 |
| `#region` | 0 |

它是「设计→生成→板级验证」闭环的模拟器侧出口（I11），同时承载 main.c 生成、三个配置产物
（sim_config.h / CMakeLists.txt / lv_conf.h）、README、图片字体资源 C 文件 —— 且 09-14 时
1312 行、仍在涨。它是有测试网（`SimulatorProjectExporterTests` 21 条）的 Core 层最后一个
未被治理的大文件（登记表 §4 #32）。

## 2. 拆分结果

| 文件 | 行数 | 原区间（1 基） | 职责 |
|---|---:|---:|---|
| `SimulatorProjectExporter.cs` | 205 | 36–203 | 头 + `SimulatorExportResult` + `ScreenFuncRegex` + `ExtractScreenFunctions` + `Export` + `CopyAssetFiles` |
| `SimulatorProjectExporter.MainC.cs` | 676 | 205–864 | `BuildMainC`（SDL/LVGL 主循环、标签表、登录门、引擎轮询） |
| `SimulatorProjectExporter.Configs.cs` | 334 | 866–1183 | `BuildSimConfig` / `BuildCmakeLists` / `BuildLvConf` |
| `SimulatorProjectExporter.Readme.cs` | 156 | 1185–1324 | `BuildReadme` |
| `SimulatorProjectExporter.Assets.cs` | 229 | 1326–1538 | `BuildImagesC` / `BuildFontsC` |

主文件 1540 → **205 行**；最大分部 676 行（`BuildMainC`，**单个巨型方法**整段 ——
方法级分解属 P1/A4 统一发射器范畴，不在本项）。

**分域依据**：切分点全部落在方法边界（方法间恰好一个空行），不改动任何语句顺序。
`SimulatorExportResult` record 保留在主文件（分部一律是类体）。

## 3. 拆分的机械保证（零丢失验证）

`tools/split-simulator-exporter.py` 负责切分并自证零丢失（与 split-propertypanelview.py 同套路）：

1. **边界行内容断言**：每个分段的段首/段末行必须与脚本登记原文逐字相等（防 off-by-one）；
2. **段间空行检查**：相邻两段之间必须恰好 1 个空行且为空；
3. **零丢失**：各段 body 按序拼接 == 原文类体（去除段间空行）逐行相等。

运行输出：

```
边界断言: 全部通过 ✓
段间空行: 全部恰好 1 行且为空 ✓
OK  SimulatorProjectExporter.cs       body  168 行 == 原 36-203
OK  SimulatorProjectExporter.MainC.cs body  660 行 == 原 205-864
OK  SimulatorProjectExporter.Configs.cs body 318 行 == 原 866-1183
OK  SimulatorProjectExporter.Readme.cs body  140 行 == 原 1185-1324
OK  SimulatorProjectExporter.Assets.cs body  213 行 == 原 1326-1538
结论: 零丢失 ✓（类体各段逐行一致，去除段间空行）
```

## 4. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release --no-incremental` | **0 警告 0 错误** |
| 全量测试（7 程序集） | 见提交读数（Core 含 `SimulatorProjectExporterTests` 21 条全绿） |

> 拆分是纯结构搬家：`partial` 关键字 + 文件头 + 文件尾 `}`，类体逐行未变；
> 生成产物（main.c / 配置 / README / 资源 C）由既有测试网锁定。

## 5. 行数守卫同步

`SingleFileSizeGuardTests` 的 `SimulatorProjectExporter` 条目从「相等判定（1540）」
改为「分部上限（`SimulatorProjectExporter.*.cs` ≤ 700）」—— MainC.cs 是 676 行的
单方法整段，上限 700 留整段空间，防的是「合并回巨型单类」（一个文件再次容纳全部方法）。