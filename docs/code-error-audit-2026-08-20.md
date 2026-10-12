# 智数 HMI 项目代码错误检查报告

**日期**：2026-08-20　**范围**：全项目（编辑器 C# / 固件 C / Python 脚本）　**结论**：发现并修复 7 项，建议项 3 项，观察项 3 类

---

## 一、检查范围与方法

| 范围 | 内容 | 方法 |
|---|---|---|
| 编辑器 C# | `src/` 14 个项目（Core/CodeGen/Simulator/Device/App/AI/…）+ `tools/` 3 个 | `dotnet build`（编译级）+ `dotnet test`（1576 用例）+ 静态危险模式扫描 + 关键路径人工审查 |
| 测试 | `tests/` 6 个项目 | 同上（另修 xUnit 规范问题） |
| 固件 C | `firmware/Tina-Linux/package/hmi-runtime/src`（main/comm/drivers/runtime/settings） | 人工审查：缓冲区边界、命令注入、内存安全、并发 |
| Python | `scripts/`、`hardware/**`、`rs485_*`、`tools/*.py` | `py_compile` 语法检查 |

**基线结果**：构建 0 错误；测试 1577/1577 通过；Python 语法全过。下述问题均为静态审查发现的隐性缺陷（测试未覆盖的边界）。

---

## 二、已修复问题（7 项）

### 🔴 中等（4 项，均会导致功能错误或编译失败）

#### 1. `CodeGenerator.CFloat` 生成非法 C 浮点字面量
- **位置**：`src/HmiEditor.CodeGen/CodeGenerator.cs:1965`（原实现）
- **现象**：旧实现 `value.ToString("0.##")` 最多保留 2 位小数——
  - 非整数且 < 0.005（如 0.001、0.0021）→ 四舍五入为 `"0"` → 生成 **`0f`**（C 语言中整数常量加 `f` 后缀非法）；
  - 整数且 ≥ 1e9 → 走 else 分支输出 `1000000000f`（同样非法）。
  - 二者都会导致模拟器/固件 C 编译失败。
- **触发路径**：Slider/Bar 绑定控件的 `MaxValue - MinValue` 为小数值（如 0.003）时，`sim_refresh_bindings` 生成的 `float v = 0f + 0f * sinf(...)` 即非法。
- **修复**：改用 `ToString("G10", InvariantCulture)`，结果无 `.`/`e`/`E` 时补 `.0`，并对 NaN/Infinity 防御。旧实现的 `50f` 类问题一并消除。
- **验证**：新增回归测试 `CFloat_SmallFractionRange_GeneratesValidLiterals`（构造 MinValue=0/MaxValue=0.003 的绑定 Slider），断言生成代码含 `0.003f` 且匹配 `float v = \d+\.\d+f \+ \d+\.\d+f \* sinf`。测试通过。

#### 2. 串口/TCP 通道：无输出命令空等满超时
- **位置**：`SerialDeviceChannel.cs:96`、`TcpDeviceChannel.cs:88`（`SendCommandAsync`）
- **现象**：完成条件要求 `snapshot.Length > 0 && !_dataIncoming`。当命令无输出且设备不回显时，`snapshot` 恒为空 → 循环永不 break → **空等满 `CommandTimeoutSeconds`（10s）**，被上层误判为超时失败。
- **修复**：改为 idle 计时机制——收到数据后静默 `SilentTimeoutMs`（300ms）判输出完成；全程无数据时也在一个静默周期后返回空。
- **⚠️ 修复过程中的关键教训**：首版直接把条件改成 `!_dataIncoming` 会引入**回显延迟竞态**（命令刚发出、回显未达时 `_dataIncoming` 仍为 false → 立即误判空输出返回）。最终采用 `Environment.TickCount64` 记录 idle 起始时刻的方案，两者兼顾。
- **验证**：重新构建通过；该路径由设备交互测试覆盖（无自动化测试，人工核对逻辑）。
- **⚠️ 后续（2026-09-16）**：这次修复**并没有真正消除回显延迟竞态**，只是把误判窗口从 0 ms 挪成了
  `SilentTimeoutMs`（300 ms）—— `idleSince` 从**写入时刻**起算，而设备起步（shell 调度 / 进程抢占 /
  主机满载）可以慢过 300 ms，于是"立即误判空输出"变成"300 ms 后误判空输出"，且那条迟到的回包会落进
  下一条命令的窗口（**响应张冠李戴**）。2026-09-16 由门禁首跑顶出后才彻底修掉：判据改为"从**最后一段
  数据**到达时刻起算静默"，并把首字节宽限拆成独立窗口。详见
  `docs/2026-09-16-channel-frame-completion-contract.md`。

#### 3. `SanitizeIdentifier` 两处实现不一致
- **位置**：`CodeGenerator.cs:1960`（只替换 `-` 和空格）vs `SimulatorProjectExporter.cs:1265`（全字符清理）
- **现象**：控件 ID / 动画 ID / 工程名若含中文、`.`、`#` 等字符（`HmiControl.Id` 默认是 GUID 十六进制可被用户修改），CodeGenerator 路径会生成非法 C 标识符 → 编译失败；两处对同一资源产出的符号名还可能不一致。
- **修复**：统一为全字符清理（非字母数字下划线 → `_`），与 `SanitizeAssetId` 同规则。
- **验证**：重新构建通过；既有 SanitizeIdentifier 相关测试全部通过。

#### 4. 测试中阻塞式 Task 调用（xUnit1031 ×4）
- **位置**：`DeviceLogicTests.cs:81,118`、`SwuPackageBuilderTests.cs:27,95`（`.GetAwaiter().GetResult()`）
- **现象**：同步阻塞异步操作，xUnit 规范警告（有死锁风险，测试在无同步上下文时通常不触发）。
- **修复**：4 处测试方法改为 `async Task` + `await`。
- **验证**：59 个 Device 测试全部通过。

### 🟡 轻微（3 项）

#### 5. `CpioTestReader.ReadAligned` 未检查 Read 返回值（CA2022）
- **位置**：`tests/HmiEditor.Device.Tests/CpioTestReader.cs:70`
- **修复**：`stream.Read(skip, 0, pad)` → `stream.ReadExactly(skip, 0, pad)`（.NET 7+ API，本地文件流不会截断，消除告警）。

#### 6. 硬编码开发机路径
- **位置**：`SimulatorBuildService.cs:60`（`/home/chen/Git/lvgl`）
- **修复**：删除，保留上方 `UserProfile/Git/lvgl` 兜底（跨机器可用）。

#### 7. 构建警告清零
- **结果**：代码警告 19 → **0**（剩余 7 个 NU1900 为 NuGet 漏洞库只读环境问题，与代码无关）。

---

## 三、建议项（未改，需固件构建链验证）

固件 C 代码总体安全（主要 memcpy 均有容量检查、`system()` 调用点无用户输入注入、`sys_cmd_get` 有边界截断、`s_cands` 容量与候选上限匹配、`main.c` malloc 有 NULL 检查）。以下为轻微改进建议：

| 严重度 | 位置 | 问题 | 建议 |
|---|---|---|---|
| 轻微 | `mqtt_client.c:443,460` | RX 缓冲满时数据被静默丢弃，无日志/计数 | 加 `HMI_LOG` + 丢弃计数，便于现场排查 |
| 轻微 | `set_net.c:28 is_ipv4` | 不校验段数值范围（`999.999.999.999` 通过校验，ifconfig 报错） | 补每段 0-255 校验（不影响安全，防注入已满足） |
| 轻微 | `sys_cmd.c:43` | `pclose()` 返回值未检查 | 忽略即可或记录日志 |

> 说明：以上改动需 Tina 交叉编译链验证，本环境未具备，故只报告不修改，避免引入未经验证的固件变更。

---

## 四、观察项（非缺陷，建议性）

1. **`async void` 事件处理器 30+ 处**（Avalonia 事件模型标准做法，可接受）：但部分无内部 try-catch（如 `MainWindow.axaml.cs:1836 RunDockEventRoutingDiagnostic`、各 `OnXxxClicked`），异常会冒泡导致崩溃。建议统一包 try-catch + 日志。
2. **空 catch 块 12 处**：资源清理类（Close/Dispose/Delete）可接受；但 `DesignCanvas.cs:732/1232/1632/1812`、`SettingsService.cs:130` 吞异常无日志，排障困难。建议至少记录 debug 日志。
3. **`PresetResourceLibrary.cs` 大量裸 #hex 颜色**：属预设模板数据（非渲染逻辑），与"颜色单一来源：PaletteDefaults.cs"约定有张力。建议将色值集中到资源数据常量。

---

## 五、验证结果汇总

| 检查项 | 修复前 | 修复后 |
|---|---|---|
| `dotnet build` | 0 错误 / 19 警告 | 0 错误 / 7 警告（全部 NU1900 环境告警） |
| `dotnet test` | 1576 通过 | **1577 通过**（含新增回归测试） |
| Python 语法 | 全过 | 全过（未改动） |
| 固件 C | 未编译（本环境无交叉链） | 未编译（本次未改动固件代码） |
