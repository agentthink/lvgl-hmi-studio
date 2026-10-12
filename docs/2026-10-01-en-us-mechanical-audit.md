# en-US 的机械核对（阶段 226）
## —— 登记 `#27` 能做的机器部分，以及**交给母语级校对**的清单

> 登记 `#27` 说「**en-US 部分为精简翻译**」。逐字校对需要母语级判断，**我不冒充**；
> 这一阶段把**机器能判的错**判完，并把需要人看的东西列成清单。

---

## 1. 既有守卫管到哪、这一面补什么

| 已有 | 管什么 |
|---|---|
| `LocalizationTests` | 两本字典的**键集**一致 |
| `LangKeyCoverageTests` | `Lang` 常量在**两语**里都解析得出（不会显示键名） |
| 各类域守卫 | 硬编码中文、日志/格式调用点、各域 issue 文案… |

**没人看取值内容**。这一面补四件机器能判的：

| 判据 | 结果（阶段 226 实测，**2363** 条键） |
|---|---|
| ① 占位符（含 `{0:F2}` 这类格式说明）两语**必须一致** | **0 条不一致** ⇒ 这条目前是**防新增**的棘轮（少一个占位符会显示裸 `{0}` 或直接抛异常） |
| ② 英文取值里不许出现 **CJK** | **1 条**，且是**合法例外**：`menu.lang.zhCN = "中文"`（语言切换项本来就该显示「中文」）—— 逐条登记在 `CjkAllowed` 里 |
| ③ 取值不得为空/空白 | **0 条** |
| ④ 两语**取值完全相同**的键必须逐条登记 | **38 条**全部登记（技术名词/单位/格式模板/语言名 —— 相同才是对的翻译） |
| ⑤ 「英文明显偏长」（≥40 字符且 >3× 中文）清单 | **127 条**（只减不增上限），前 15 条打在测试输出里 |

⚠ **本面不判**：术语统一、语气、语序、大小写惯例、缩写是否地道 —— 那些需要人。

## 2. 交给母语级校对的清单

### 2.1 英文明显偏长（前 15，全量 127）

| 倍数 | 键 | zh 长度 | en 长度 | zh | en |
|---|---|---|---|---|---|
| 5.7× | `system.issue.io_error` | 9 | 51 | `读写文件时发生错误` | An error occurred while reading or writing the file |
| 5.6× | `cfg.note.VariableManager.ConnectionRecoveryMode` | 14 | 79 | `断线恢复策略没有接到通讯层。` | The reconnection recovery strategy is not connected to the communicati |
| 5.5× | `cfg.note.VariableManager.EnableWriteCoalescing` | 13 | 72 | `写合并开关没有接到通讯层。` | The write-coalescing switch is not connected to the communication laye |
| 5.4× | `ai.script.func.hint` | 10 | 54 | `描述脚本要实现的功能` | Describe the functionality the script should implement |
| 5.3× | `nav.flow.tt.rebuild` | 10 | 53 | `从当前工程重建导航图` | Rebuild the navigation graph from the current project |
| 5.1× | `hw.target.validation.ok` | 12 | 61 | `所选设备与工程配置兼容。` | Selected device is compatible with the project configuration. |
| 5.0× | `anim.tt.desc` | 9 | 45 | `描述动画效果和用途` | Describe the animation effect and its purpose |
| 4.8× | `faceplate.tt.create.from.ctrl` | 9 | 43 | `从选中控件创建模板` | Create a template from the selected control |
| 4.6× | `anim.tt.enabled` | 11 | 51 | `启用后动画在运行时生效` | When enabled, the animation takes effect at runtime |
| 4.6× | `prop.visual.state.inherit.hint` | 9 | 41 | `留空表示继承默认态` | Leave empty to inherit from default state |
| 4.5× | `sim.tt.wave.desc` | 13 | 58 | `当前信号类型的波形特征描述` | Waveform characteristic description of current signal type |
| 4.4× | `anim.tt.trigger` | 9 | 40 | `选择动画的触发方式` | Select the trigger mode of the animation |
| 4.4× | `ai.retry.hint` | 9 | 40 | `请调整描述后重试。` | Please adjust the description and retry. |
| 4.4× | `cfg.note.RuntimeConfig.HeartbeatIntervalMs` | 12 | 53 | `心跳周期不会下发到固件。` | The heartbeat interval is not pushed to the firmware. |
| 4.4× | `anim.tt.target` | 10 | 44 | `选择要应用动画的控件` | Select the control to apply the animation to |

> 这些**不一定错**（英文天然更长），但它们是"最容易在界面上被截断/显得啰嗦"的一批，
> 值得人扫一眼：能不能更短、更地道。

### 2.2 两语完全相同的 38 条（已登记，供人抽查）

技术名词与格式模板为主：`API Key` / `Endpoint` / `Model` / `Provider` / `Claude` /
`Modbus RTU` / `Modbus TCP` / `MQTT` / `OPC UA` / `QoS` / `UDT` / `Bool` / `Int16`…`String` /
`1024x600`、`1280x800` / `Git: {0}` 与 `[{0}] {1}: {2} ({3})`（格式模板）/ `L T R B`（安全区四格缩写）/
`中文` 与 `English`（语言名）。

**请抽查**：有没有哪一条其实**该翻**（例如 `var.udt.tab = "UDT"` 在英文界面是否要 `UDT` 还是 `UDT Types`）。

### 2.3 其它建议人工过一遍的

1. **语气/人称**：中文多用无主语句（"未连接"），英文有的地方是 `The … is not connected…`（见 §2.1 的
   `cfg.note.*`）—— 要不要统一成更短的祈使/名词短语，属风格决策。
2. **大小写惯例**：按钮/菜单项首字母大写 vs 句首大写，目前混用（机器判不了）。
3. **缩写**：`DPI`/`UDT`/`QoS`/`FEL` 等保持一致即可；但 `Screenshot` vs `Screenshot…` 这类要看上下文。

## 3. 负控（5 支，`.workbuddy/tmp/s226-nc.py`）

`status.git.info` 去掉 `{0}`（读数 `占位符两语不一致`）· `ai.model` 改成中文（`英文取值里出现中文`）·
从 `IdenticalAllowed` 删一条登记（`没登记（多半是漏翻）` ＋ 点名 `data.type.bool`）·
`ai.endpoint` 改空串（`空/空白取值`）· 把偏长基线压到 100（`超过基线`）。

## 4. 读数

**门禁**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载见日志；
脚本 **29**；路径卫生 **703**；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2324 / 315 / 832 / 8`（合计 **3745**）
＝ **3745 通过 / 0 失败**（阶段 225 为 3739，**＋6** ＝ 新判据 6 条）；`MainWindow.axaml.cs` 仍 **2983**；
**产品/字典内容 0 行改动**（本阶段只加判据与文档）。

## 5. 仍未做

**逐字校对本身**（术语统一、语气、大小写、缩写地道性）—— 需要母语级判断，本阶段的产物是
"机器能挑的已挑完 ＋ 上面那份清单"。另外：`#27` 原文提到的"运行时动态文案未入字典"那条
在阶段 94–100 已各自闭环（登记里记着），本阶段没有重开它。
