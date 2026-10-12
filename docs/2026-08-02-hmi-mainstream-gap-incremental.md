# HMI Editor 主流对标增量差距分析(2026-08-02)

> **版本**: v2.1.0+ (R1/R2/R3 落地后)
> **日期**: 2026-08-02
> **基准**: `2026-07-30-hmi-mainstream-comparison-report.md` 与 `2026-07-30-hmi-mainstream-comparison-refactor-adr.md`
> **方法**: 以当前代码库实际状态核对(非文档声明),区分"已闭环 / 半闭环 / 完全缺失"

---

## 一、状态核对结果(相对 07-30 报告)

07-30 报告列出的差距中,以下项已在代码中闭环,不再计入本次差距:

| 原差距 | 代码证据 | 状态 |
|---|---|---|
| G1 工具栏专业化 | `MainWindow.axaml` 使用 `fa:FACommandBar` + `FAPathIconSource` | ✅ 闭环 |
| G2 拖放控件添加 | `ToolboxView.axaml.cs` 调用 `DragDrop.DoDragDropAsync` | ✅ 闭环 |
| G3 类型化属性编辑器 | `PropertyPanelView.axaml.cs` 含 `ColorPickerPopup`/`CheckBox`/`ComboBox`/`NumericUpDown` | ✅ 闭环 |
| G4 资源仪表盘 | `MainWindow.axaml` 状态栏含 `FlashBar`/`RamBar` ProgressBar | ✅ 闭环 |
| G5 Dock 布局 | `HmiDockFactory.cs` + `dock:DockControl` | ✅ 闭环 |
| G6 层管理 | `LayerPanelView.axaml` + `LayerIndex` 绑定 | ✅ 闭环 |
| G7 设计时数据预览 | `DesignCanvasView` `PreviewModeChanged` + `PreviewValueProvider` | ✅ 闭环 |
| G9 资源管理器 | `AssetManagerView.axaml` | ✅ 闭环 |
| G10 导航流设计器 | `NavigationFlowView.axaml` | ✅ 闭环 |
| G11 交叉引用分析器 | `CrossReferenceAnalyzer.cs` (R3-T4) | ⚠️ 半闭环(无 UI 面板) |
| G12 脚本执行 | `ScriptSandboxExecutor.cs` (R3) | ⚠️ 半闭环(与仿真联动待验证) |
| G13 表达式引擎 | `ExpressionEngine.cs` (508 行) | ⚠️ 半闭环(App 层无引用) |
| G19 UDT 结构化标签 | `VariableUdt.cs` + `UdtTests.cs` | ⚠️ 半闭环(UI 集成深度待验证) |
| G17 审计追踪 | `AuditLogEntry.cs` + `UserManagerView` | ⚠️ 半闭环(仅有模型,无记录/展示) |

**结论**: 07-30 报告的 P0 全部闭环,P1 大部分闭环。当前瓶颈从"缺功能"转为"缺闭环、缺集成、缺前瞻"。

---

## 二、当前真实差距(增量,按优先级)

### P0 — 半闭环补齐(核心已存在,接通即可产生价值)

| # | 差距 | 现状 | 主流对标 | 补齐动作 |
|---|---|---|---|---|
| I1 | **表达式引擎未接入属性面板** | ✅ **闭环 (2026-08-02)** — 属性面板绑定源切换(变量/表达式)+ 实时校验;预览/CodeGen/运行时标签表全链路支持 | Ignition/WinCC 绑定表达式 `tag>100?red:green` | 已落地;后续可扩展表达式驱动颜色/可见性 |
| I2 | **交叉引用无可视化面板** | ✅ **闭环 (2026-08-02)** — `CrossReferenceView` 停靠面板(底部组),变量/屏幕/脚本三类反查,复用 `CrossReferenceAnalyzer`,菜单"工具→交叉引用" | WinCC 交叉引用(影响分析) | 已落地;含 Enter 搜索与类型切换 |
| I3 | **审计日志无记录与展示** | ✅ **闭环 (2026-08-02)** — JSONL append-only 持久化 + `AuditLogView` 面板 + 导出/登录/权限动作记录 + 启动历史恢复 | TwinCAT TF2400 / 21 CFR Part 11 | 已落地;审计视图支持筛选与刷新 |
| I4 | **UDT 未贯通全链路** | ✅ **闭环 (2026-08-02)** — 变量管理新增"UDT 类型"页签:定义/字段 CRUD + 一键实例化;点号寻址 `motor1.speed` → 变量浏览器字段展开 → 运行时标签表 → CodeGen `TAG_motor1_speed` 全链路;删除保护(有实例拒绝删类型);9 个端到端测试 | FactoryTalk/Optix UDT | 已落地;UDT 嵌套暂不支持 |
| I5 | **脚本沙箱与仿真未闭环** | ✅ **闭环 (2026-08-02)** — 脚本编辑器"调试运行"(F6):Lua 回调体自动翻译为沙箱语句(`hmi_read`/`hmi_write`/`not`/`else`),复用项目变量种子 + 预览仿真值源,输出日志与变量 | Ignition 事件脚本运行时验证 | 已落地;沙箱支持 `if/then/else/end` 分支 |

### P1 — 完全缺失(对标 2025-2026 主流必备)

| # | 差距 | 现状 | 主流对标 | 影响 |
|---|---|---|---|---|
| I6 | **无 SVG/矢量导入** | ✅ **闭环 (2026-08-02)** — 轻量 SVG 解析器(rect/circle/ellipse/line/polyline/polygon/path + 样式/分组/颜色);`Svg` 控件类型(工具箱拖放);画布矢量渲染;CodeGen 生成 `lv_canvas` 绘制序列(线段近似,无 path 渲染依赖);资源管理器"导入 SVG" + 双击应用到选中控件;17 个新测试 | WinCC Unified / AVEVA 全系矢量 | 已落地;渐变/transform 解析留待后续 |
| I7 | **无 OPC UA 客户端** | ✅ **闭环 (2026-08-02)** — 自包含轻量 OPC UA 二进制客户端(零第三方依赖,兼容 NativeAOT):HEL/ACK 握手、OpenSecureChannel/CloseSecureChannel(None 安全策略)、Read/Write 服务(属性 Value)、NodeId 解析(`ns=2;s=x`/`i=n`/Guid/ByteString);`OpcUaDriver` 设计时契约 + `OpcUaRuntimeDriver` 运行时契约(新 `IStringAddressRuntimeDriver` 字符串寻址通道);预览模式直连;17 个新测试(编解码往返 + 内存模拟服务器端到端握手/读写) | TIA V21 / TwinCAT / Optix 标配 OPC UA | 已落地;限制:无签名/加密、无订阅推送(轮询)、无浏览节点树,覆盖工程调试与数据采集主场景 |
| I8 | **无 Git 工作流保障** | ✅ **闭环 (2026-08-02)** — `GitWorkflowService`(git CLI 包装:仓库检测/一键初始化/暂存/提交/历史/状态快照,零第三方依赖兼容 NativeAOT);工具菜单 4 项;状态栏实时 Git 状态(分支/未提交/领先落后);保存/打开自动关联项目目录;8 个新测试 | TIA V21 原生 Git | 已落地;推送需配置 remote |
| I9 | **无多分辨率/自适应布局** | ✅ **闭环 (2026-08-02)** — `ScreenAdaptiveMode`(None/Fit/Fill)+ 纯函数计算器(编辑/生成共用);CodeGen 生成 `adaptive_layout` 回调(依据 LVGL display 实际分辨率重算控件位置/尺寸,Fit 等比居中 / Fill 拉伸);屏幕管理器模式设置;画布工具栏分辨率预览(800x480/1024x600/1280x800/480x800 竖屏,只读,transform 预览);18 个新测试(计算器/CodeGen/序列化,含旧项目兼容) | TwinCAT / WinCC Unified 自适应 | 已落地;控件级锚点(左/中/右)留待后续 |

### P2 — 差异化与前瞻(路线图后半段)

| # | 差距 | 现状 | 机会 |
|---|---|---|---|
| I10 | **AI 辅助生成** | 全手动 | TIA V21 Copilot 刚起步;本项目有代码生成优势,可做"自然语言 → LVGL 画面"弯道超车 |
| I11 | **设计-生成-验证闭环** | ✅ **已落地 (2026-08-02)** — `GeneratedCodeValidator` 静态质量门(结构/控件 id 冲突/Faceplate 未解析参数/表达式语法/悬空引用/未知 LVGL API/生成代码完整性,Error 阻断导出)+ `SimulatorProjectExporter` 一键导出 LVGL v9+SDL2 PC 模拟器工程(main.c 屏幕工厂/CMake/lv_conf/README);菜单"验证生成代码(Ctrl+Shift+V)"与"导出模拟器工程";17 个新测试 | 嵌入式 HMI 独有卖点 | 板级闭环已形成;真正编译运行需用户本机装 SDL2 + 克隆 lvgl |
| I12 | **字体子集化** | ✅ **已落地 (2026-08-02)** — `FontSubsetAnalyzer` 扫描项目实际字符集(控件文本/变量名/单位/模拟值/UDT 字段),估算 LVGL 子集化内嵌字体 Flash 占用(ASCII 半角 vs CJK 全角位图模型),节省比例对比典型全量字库;字体导入真实接入(ttf/otf/ttc);双击字体打开分析对话框(字符统计卡片/预算对比/字符预览/导出 charset.txt 供 lv_font_conv);17 个新测试 | 中文 HMI Flash 占用降一个量级 | 已落地;真正 TTF→C 字形转换由 lv_font_conv 完成(零依赖边界,命令已生成) |

---

## 三、SWOT 更新(2026-08)

**优势**: LVGL C 代码生成(独有赛道)+ R1/R2/R3 工程体验已到位 + 310 测试 0 警告 + JSON 工程文件天然 Git 友好。

**劣势**: 半闭环项阻塞价值兑现(表达式/交叉引用/审计/UDT 有内核无 UI);通信协议面窄(无 OPC UA);无矢量能力。

**机会**: AI 生成 + 验证闭环 = 嵌入式 HMI 工具链差异化;OPC UA 补齐后可直接对标主流 PLC 生态。

**威胁**: LVGL 9→10 API 演进;主流工具(如 WinCC Unified HTML5)若下沉嵌入式代码生成将形成正面竞争。

---

## 四、建议行动顺序

**执行结果 (2026-08-02)**: P1 全部 9 项差距已闭环并推送(commit `29874fd`→`fd84b25`)。

1. **已交付 (I1/I3)** — 表达式引擎接入属性面板 + 审计记录落地。
2. **已交付 (I2/I5)** — 交叉引用面板 + 脚本调试运行(设计→验证闭环)。
3. **已交付 (I6/I7)** — SVG 矢量导入 + 自包含 OPC UA 客户端(零依赖)。
4. **已交付 (I8/I9)** — Git 工作流保障 + 多分辨率自适应布局。

**下一步候选 (P2 路线图)**: I10 AI 辅助生成 → I11 LVGL 模拟器验证闭环 → I12 字体子集化。当前全量 **842 用例通过**,构建 0 错误 0 新警告。

## 五、验证方式(遵循既有适应度函数)

- I1: `ExpressionEngineTests` 已有,新增属性面板表达式绑定 UI 测试;FF6 扩展"表达式非法值拦截率 100%"
- I3: 审计记录写入断言(认证/登录/导出动作均有记录)
- I7: OPC UA 需引入 `OPCFoundation.NetStandard.Opc.Ua.Client` 依赖,新增硬件目标兼容性验证
- I11: FF8 扩展为"生成代码在 LVGL 模拟器可编译可运行",作为发布阻断项

---

*本文档为增量差距分析,行动项落实后应回填状态并更新到 `2026-07-30-hmi-mainstream-comparison-report.md` 主报告。*
