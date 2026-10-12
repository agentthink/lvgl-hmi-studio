# 通过插件控制嘉立创EDA专业版直接绘制 T113 HMI 原理图 — 可行性评估

评估日期：2026-09-12
结论：**能力上可行，官方 API 与本地桥接通道均已具备；但当前无法直接开画，存在 3 个阻断项。**

---

## 1. 结论摘要

| 问题 | 答案 |
|---|---|
| 嘉立创EDA专业版是否开放原理图编程接口？ | 是。官方扩展 API，`eda.sch_*` 命名空间，含器件创建、导线创建、网络标识、自动布局/布线、DRC |
| 是否有现成的外部驱动通道？ | 是。官方 `run-api-gateway` 扩展 + WebSocket bridge（端口 49620-49629），本机已自建等价实现 |
| Linux 能否运行？ | 能。官方提供 `lceda-pro-linux-x64` 客户端，本机已安装 `/usr/bin/lceda-pro` |
| T113-S3 是否有现成符号？ | 有。嘉立创EDA 系统库内置，LCSC 编号 C5197687（ELQFP-128） |
| 现在就能自动画出来吗？ | **不能。** 3 个阻断项未解决，见第 3 节 |
| 适合正式投板设计吗？ | **不适合直接依赖。** 全部原理图写入 API 标记为 BETA |

---

## 2. 已验证的就绪项（附证据）

### 2.1 官方 API 能力（文档核实，非二手资料）

来源：https://prodocs.lceda.cn/cn/api/reference/

| API | 方法 | 作用 |
|---|---|---|
| `eda.sch_PrimitiveComponent` | `create(component, x, y, subPartName, rotation, mirror, addIntoBom, addIntoPcb)` | 从库放置器件 |
| | `createNetFlag(identification, net, x, y, rotation, mirror)` | 创建 GND/VCC 等网络标识 |
| | `createNetPort(direction, net, x, y, ...)` | 创建网络端口 |
| | `getAllPinsByPrimitiveId(primitiveId)` | 取器件全部引脚 |
| | `modify(primitiveId, property)` | 修改器件 |
| `eda.sch_PrimitiveWire` | `create(line, net, color, lineWidth, lineType)` | 创建导线 |
| `eda.sch_Document` | `autoLayout(props)` / `autoRouting(props)` | 自动布局 / 自动布线 |
| | `save()` | 保存文档 |
| `eda.lib_Device` | `getByLcscIds(lcscIds, libraryUuid, allowMultiMatch)` | 按立创 C 编号取器件 |
| | `search(key, ...)` | 搜索器件 |

坐标单位：SCH 画布 10mil。autoLayout / autoRouting 为 BETA。

### 2.2 本地已有实现：`~/项目/dsh-lceda-pro`

自建桥接工具，非官方，但内部调用的全是官方 API。

| 组件 | 路径 | 状态 |
|---|---|---|
| CLI | `~/bin/dsh-lceda-pro` → `~/项目/dsh-lceda-pro/bin/dsh-lceda-pro.mjs` | 可用 |
| Bridge Server | Node.js HTTP/WS，端口 49620-49629 | **正在运行** |
| 桥接扩展 | `build/extension/dsh-lceda-pro-bridge.eext` | 已构建，未导入客户端 |
| systemd 服务 | `~/.config/systemd/user/dsh-lceda-pro-bridge.service` | 已配置 |
| 上游同协议扩展 | https://jlc-ext.com/item/oshwhub/run-api-gateway | 官方，协议兼容 |

实测 `dsh-lceda-pro health` 输出：

```json
{
  "service": "easyeda-bridge",
  "status": "ok",
  "edaConnected": false,
  "edaWindowCount": 0,
  "activeWindowId": null,
  "pendingRequests": 0
}
```

扩展入口调用 `eda.sys_WebSocket.register`（由扩展主动连出，因 Electron 无法接收入站连接）。

---

## 3. 阻断项（按优先级）

### P0-1 — EDA 客户端未运行，桥接扩展未导入

**证据**：`edaConnected: false`；`~/.config/LCEDA-Pro/` 下仅有 `cache.model.2`、`cache.x64.3`、`updater`，无扩展目录；`~/文档/LCEDA-Pro/projects/` 为空；`ps aux` 无 lceda 进程。

**需人工完成（无法脚本化）**：
1. 启动 `/usr/bin/lceda-pro`（客户端模式为 ONLINE，需登录）
2. **高级 → 扩展管理器 → 导入** → 选 `dsh-lceda-pro-bridge.eext`
3. 扩展管理器内勾选 **允许外部交互** + **显示在顶部菜单**
4. 复验：`dsh-lceda-pro health` 应返回 `edaConnected: true, edaWindowCount: 1`

### P0-2 — 写入能力封装不完整

**证据**（`src/tools.ts`）：

```typescript
// placeComponent 实际是 modify，不是 create
export async function placeComponent(client, symbolUuid, x, y, options?) {
  if (!options?.refDesignator) throw new Error('refDesignator is required ...');
  // ... 查找已存在的 designator，然后 eda.sch_PrimitiveComponent.modify(...)
}
```

| 缺口 | 影响 |
|---|---|
| `placeComponent` 只能**移动已存在的器件**，未调用 `create()` | 无法从零放置新器件 |
| `placeWire` 仅接受 `(x1,y1,x2,y2)` 单段直线 | 拐弯连线需多次调用；官方 API 支持多段且**要求各段首尾相连**，否则 `create` 失败 |
| 无引脚坐标读取封装 | 自动连线的前提缺失（导线网络归属靠坐标吸附到引脚） |
| `autoLayout` / `autoRouting` 未封装 | 需自行调用，且为 BETA |

**规避路径**：`lceda_execute` 可执行任意 JS，直接调用官方 API 即可绕过现有封装。
**建议路径**：补齐 `create` / `getAllPinsByPrimitiveId` / 多段 wire 三个封装（工作量小，收益确定）。

### P0-3 — 原理图源数据颗粒度不足

**证据**：`normalized-netlist.csv` 仅 111 行，且为块级描述，例如一行 `U6 | RGB24 pins | LCD_B0..LCD_R7` 实际代表 24 个独立网络。缺 LCSC 编号、封装、位号全表。

细颗粒度数据仅覆盖 LCD 局部：

| 文件 | 行数 | 粒度 | 可用性 |
|---|---|---|---|
| `lcd-t113-pin-handoff.csv` | 51 | 逐脚（LCD 50pin） | 可用 |
| `t113-lcd-net-assignment.csv` | 39 | 逐脚（SoC 侧 38 项） | 可用 |
| `normalized-netlist.csv` | 111 | 块级 | 不足 |
| `kicad-export/out/P1..P9.kicad_sch` | 9 页 | 完整原理图 | 真源，需转换 |
| `hmi-control-board-t113/*.kicad_sch` | 6 文件 | 完整原理图 | 真源，需转换 |

且 `project.json` 中 `pending_before_pcb` 自列 6 项未定：RTP FPC 机械封装与接触面、背光 LED 串并拓扑与恒流控制器、T113 非 RGB 外设官方 pinmux 复核、PHY 具体型号与 RMII 时钟方向、全部器件封装与厂家料号、LCEDA-Pro 内 ERC。

**关键判断**：插件不是设计能力，它只把已确定的设计落到画布上。上述 6 项未定时，插件画不出正确电路。

---

## 4. 技术风险（官方原文）

> 此 API 当前处于 BETA 预览状态，希望得到开发者的反馈。它的任何功能都可能在接下来的开发进程中被修改，**请不要将它用于任何正式环境**。

涉及全部原理图写入路径：`sch_PrimitiveComponent.create`、`sch_PrimitiveWire.create`、`sch_Document.autoLayout`、`sch_Document.autoRouting`。

推论：
- 自动布局/布线的实际排版质量未经验证，专业原理图排版（电源树、去耦阵列、总线分组）需大量人工微调
- API 版本变更可能导致脚本失效，需锁定客户端版本
- 投板前必须在 EDA 内人工复核 + 跑 ERC，不能依赖自动结果

---

## 5. 推进路径

| 阶段 | 内容 | 完成标准 | 依赖 |
|---|---|---|---|
| S0 | 导入扩展、接通客户端 | `health` 返回 `edaConnected: true` | 人工 GUI 操作 |
| S1 | 最小写入验证 | `execute` 调 `lib_Device.getByLcscIds(['C5197687'])` 成功取到 T113-S3，并在空页 `create()` 放置成功 | S0 |
| S2 | 补齐写入封装 | `place_component` 走 `create`；`wire` 支持多段折线；新增引脚坐标读取 | S1 |
| S3 | 数据提纯 | 从 `kicad-export/out/*.kicad_sch` 或 capture-package 生成逐脚网表（含 LCSC 编号 + 封装 + 位号） | 独立，可并行 |
| S4 | 单页试点 | 选 P2 SOC_CORE 或 P1 POWER_INPUT 生成一页，人工复核排版与网络正确性 | S2 + S3 |
| S5 | 全量 9 页 | 生成 P1..P9，跑 ERC，导出网表/BOM | S4 |
| S6 | 复核投板 | EDA 内人工复核 + ERC 通过，BOM 与封装全部落定 | S5 + pending_before_pcb 清空 |

**建议**：S3 与 S0-S2 可并行。S4 只选一页试点，不要一次性上 9 页——BETA API 的失败模式在早期最需要暴露。

---

## 6. 替代路径（不推荐作为主路径）

`HMI_T113_SZCLX070H_R1.eprj2` 是 SQLite 3.x 数据库（504KB）。理论上可直接改库文件写入原理图，但官方无格式文档保证、版本间格式会变（V2/V3 已做数据隔离）、绕过 ERC 与保存逻辑。**仅作为应急排查手段**，不作为生成路径。

---

## 参考

- API 文档：https://prodocs.lceda.cn/cn/api/reference/pro-api.html
- 官方 SDK：https://github.com/easyeda/pro-api-sdk
- 上游 Skill：https://github.com/easyeda/easyeda-api-skill
- 网关扩展：https://jlc-ext.com/item/oshwhub/run-api-gateway
- T113-S3 器件：LCSC C5197687
