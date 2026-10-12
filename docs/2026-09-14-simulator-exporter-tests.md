# SimulatorProjectExporter 行为测试网（2026-09-14）

## 1. 背景与目的

`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`（1312 行、596 处 `AppendLine`）把 HMI 工程导出成可编译的 LVGL + SDL2 PC 模拟器工程：`main.c` / `sim_config.h` / `CMakeLists.txt` / `lv_conf.h` / `generated_hmi.c` / `generated_images.c` / `generated_fonts.c` / `generated_comm.h` / `README.md` + `assets/`。

它属于 P2「抽 IR 统一发射器」的改造对象。**动手前必须先有行为测试网** —— 与 P1-3（`DesignCanvas` 拆 partial）同一纪律：编译器只能发现"符号丢失"，发现不了"生成的内容变了"。

> 纪律原文（`.workbuddy/memory/MEMORY.md`）：*抽 IR 前必须先给 `SimulatorProjectExporter` 补测试网，否则回归半径过大。*

## 2. 现状盘点：既有测试已经很厚

导出器此前**并非无测试**。全仓共 4 个测试文件调用它：

| 文件 | 与 Export 相关的用例数 | 侧重 |
|---|---:|---|
| `SimulatorLoopTests.cs` | 20+ | main.c 运行时装配（引擎轮询、切屏、软键盘）、CMake、lv_conf、README、字体大小回退 |
| `MockFontLoadingTests.cs` | 14 | `generated_fonts.c` 的 FreeType 契约、lv_conf 的 FreeType 开关 |
| `MockImageLoadingTests.cs` | 6 | `generated_images.c` 的路径宏、Windows→Unix 路径 |
| `MockProjectEndToEndTests.cs` | 4 | 端到端导出 + 重新生成一致性 |

**所以本轮不是"从零补测试"，而是补缺口。** 盘出来的真实缺口：

| 缺口 | 说明 |
|---|---|
| **启动屏选择与重排** | `Export` 里唯一的业务分支：四级回退（`BootScreenId` → 首个非图层屏 → 首个屏 → 兜底 1024×600）+ `screenFunctions` 重排。**零覆盖** —— `BootScreenId` 只在序列化/`CodeGenerator` 测试里出现过，从未经过 `Export`。 |
| **`generated_comm.h` 内容** | 既有测试只断言 `File.Exists`。 |
| **`tags` 路径** | 绑定刷新钩子 `sim_refresh_bindings` 的有/无分支从未被导出侧验证。 |
| **参数校验** | 3 个 `ArgumentNullException` 未测；错误消息未断言。 |
| **`Files` 清单契约** | 只断过 `Contains`，没断过**精确顺序**（调用方依赖该顺序）。 |
| **失败不阻断与幂等** | 资产文件缺失、重复导出到同一目录。 |

## 3. 新增测试类

`tests/HmiEditor.Core.Tests/SimulatorProjectExporterTests.cs` —— **21 项**，按域分组：

| 组 | 项数 | 覆盖的行为 |
|---|---:|---|
| A. `ExtractScreenFunctions` | 3 | 无工厂→空；前置声明+定义**去重**；带参数的声明不算工厂（否则注册表登记签名不符的函数，C 编译期报错） |
| B. 启动屏选择与重排 | 6 | 四级回退各自一条；`sim_config.h` 分辨率随 boot 屏；`screen_factories[]`/`screen_names[]` **同序**且 boot 在首位；`screen_names` 用屏幕 Name 而非 Id |
| C. 参数与错误路径 | 2 | 3 个 Null 参数；无工厂时的 `InvalidOperationException` 消息可指导排查 |
| D. 产物清单契约 | 3 | 7 项（最小）/ 9 项（含图/字资产）的**精确顺序** + 报告即落盘；`generated_hmi.c` 与传入代码**逐字符一致** |
| E. `generated_comm.h` | 2 | 无连接时仍是自洽头文件（guard 成对、无协议宏）；RTU 连接的宏值（`COM3`→`/dev/ttyS3`、波特率、校验位、超时） |
| F. `tags` 路径 | 2 | 有 tags → 声明 `extern` 且主循环轮询；无 tags → 不留任何引用（否则链接期未定义符号） |
| G. 幂等 | 1 | 重复导出到同一目录可覆盖、结果一致 |
| H. 资产复制 | 2 | 源文件缺失**不阻断**导出；存在时复制到 `assets/` 且内容一致 |

写法上统一走既有基建：`TestTempDir.NewDir/Delete`（每个用例自清理）+ `CSource.AssertText/AssertCode/AssertNoText`（排版无关的结构化断言，而不是"包含这个子串"）。

## 4. 负向验证（证明测试网不是摆设）

"测试全绿"不等于"测试有效"。逐条破坏产品代码，确认对应测试会红：

| 破坏 | 结果 | 命中 |
|---|---|---|
| 停用启动屏重排（注释掉 `screenFunctions = orderedFns;`） | **2 项红** | `Export_BootScreenId_MovesItsFactoryToTheFront`、`..._KeepsFactoryAndNameArraysInTheSameOrder` |
| 停用「跳过图层屏」（注释掉第二级回退行） | **1 项红** | `Export_WithoutBootScreenId_SkipsScreensReferencedAsLayers` |

两次破坏均已还原，`git diff src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` 为**空**。

## 5. 过程中发现的两条事实

1. **图层屏不产出工厂函数**：`CodeGenerator` 不把被引用为图层的屏幕生成 `create_screen_*`。因此"所有屏幕互为图层"在真实生成链上根本走不到 `Export` —— `Export` 的第三级回退（`Screens.FirstOrDefault()`）是**防御性分支**。测试该项时改用**手写生成代码**，把 `Export` 自身的回退顺序与上游生成器行为解耦。
2. **`sim_config.h` 的分辨率取自启动屏的设计尺寸**，不是项目默认 1024×600 —— 所以启动屏选错会直接表现为模拟器窗口尺寸不对。这是把 B 组 6 项当作核心缺口的原因。

## 6. 验证结果

| 项 | 结果 |
|---|---|
| `dotnet build -c Release` | **0 警告 0 错误** |
| 新增测试类 | **21/21 通过** |
| 全量测试 | **1859 项**（原 1838 + 21），`App.UiTests` / `Simulator` / `Device` 等不受影响 |

## 7. 下一步

P2「抽 IR 统一发射器」现在具备条件：`SimulatorProjectExporter` 与 `CodeGenerator.cs` 之间 `IMG_PATH_` 等宏的逐字重复，可以在**生成内容被 21 项契约钉住**的前提下安全收敛。
