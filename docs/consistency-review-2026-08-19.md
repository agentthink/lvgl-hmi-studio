# 设计器→CodeGen→模拟器→固件运行时 一致性审查（闭环缺口 + 真实缺陷）

- 审查日期：2026-08-19
- 工作区：`/home/chen/Git/zhishu-hmi`
- 审查方式：只读静态走查（未修改任何文件）
- 四层管线：
  - **设计器** C#/Avalonia：`src/HmiEditor.App/Controls/DesignCanvas.cs`、`src/HmiEditor.App/Views/PropertyPanelView.axaml.cs`、`src/HmiEditor.Core/`（模型 + `Design/` 共享常量）
  - **CodeGen** C#：`src/HmiEditor.CodeGen/CodeGenerator.cs`（3277 行，生成 C）
  - **模拟器** C（SDL 宿主构建）：`sim-output/generated_hmi.c`、`generated_fonts.c`、`generated_images.c`、`main.c`（C# 侧 `src/HmiEditor.Simulator/`、`src/HmiEditor.Core/Services/SimulatorProjectExporter.cs`）
  - **固件运行时** C（设备 `.so` 热加载）：`firmware/t113/generated/screens.c`、`tags.c/.h`、`firmware/t113/app/main.c`、`runtime/variable_table.c`、`runtime/hmi_actions.c`、`runtime/hmi_ui_api.h`

> 说明：既有审计 `docs/archive/2026-08/designer-hardcode-audit.md` 只覆盖了「设计器 + CodeGen」两层，且聚焦硬编码/接口暴露，**未触及模拟器与固件运行时**。本报告补齐后两层并聚焦「功能闭环」与「真实缺陷」。

---

## 一、覆盖矩阵总览（⚠ = 半闭环/缺陷）

| 功能 / 属性 | 设计器 | CodeGen | 模拟器 | 固件运行时 | 结论 |
|---|---|---|---|---|---|
| 绑定刷新-数值(Label/Bar/Slider/Gauge/Knob/Table) | ✅ | ✅(双路径) | ⚠**从不调用** | ✅(timer) | **模拟器刷新是死代码** |
| 绑定刷新-布尔(Led/Switch/CheckBox) | ✅ | ⚠仅固件路径 | ⚠未生成 | ✅ | **模拟器无此刷新分支** |
| 字体-中文(有字体资产) | ✅ | ✅ | ✅(FreeType全字) | ⚠缺失回退16px子集 | **尺寸被钳到16px** |
| 字体-Gauge中心文本 | ✅(默认字体) | ⚠`LV_FONT_DEFAULT` | ⚠Montserrat | ⚠Montserrat | **中文变豆腐框** |
| 字体-刻度标签 | ✅ | ⚠`montserrat_12` | ⚠Montserrat | ⚠Montserrat | **中文刻度变豆腐框** |
| 背景图 | ✅ | ✅ | ✅ | ✅ | 闭环 |
| Gauge/Knob 弧度 | ✅(`ArcLayout`共享) | ✅(`ArcLayout`) | ✅ | ✅ | 闭环（共享常量） |
| Table 单元格色/网格 | ✅(`#1e293b`/`#334155`) | ✅ | ✅ | ✅ | 闭环 |
| Table 单元格绑定刷新 | ✅ | ✅(双路径) | ⚠同刷新死代码 | ✅ | 随刷新死代码失效 |
| 动作-SwitchScreen/SetVar/Bit/Momentary/Script | ✅ | ✅ | ✅ | ✅ | 闭环 |
| 动作-ShowAlarms/ShowTrends/Login/Logout | ✅ | ✅(生成调用) | ✅(extern) | ⚠**桩实现** | **半闭环：配置即无操作** |
| 报警引擎(alarm_evaluate) | 模型✅ | ✅(生成进.so) | ⚠未被轮询 | ✅(main轮询) | **模拟器不跑引擎** |
| 趋势引擎(trend_tick/chart) | 模型✅ | ✅(生成lv_chart) | ⚠未被轮询 | ✅(main轮询) | **模拟器不跑引擎** |
| 登录/权限(UserRole/Permission) | 模型✅ | ❌未下发 | ❌无 | ❌无(仅boot密码) | **设备端无权限模型** |

---

## 二、详细证据与缺陷

### D1【高】模拟器主循环从不调用绑定刷新 —— 全部正弦模拟刷新为死代码
- **证据**：`sim-output/main.c` L272–276 主循环仅 `lv_timer_handler(); lv_tick_inc(5); lv_delay_ms(frame_delay);`，无 `sim_refresh_bindings()` 调用。而 `sim_refresh_bindings(tick)` 由 `CodeGenerator.GenerateSimRefreshBindings`（L632–779）生成，全仓**无任何调用方**（grep 仅命中 `CodeGenerator.cs` 注释与定义）。
- **影响**：模拟器所有「绑定控件」(Label/Bar/Slider/Gauge/Knob/Table单元格) 停在初始值，**设计器预览有值、模拟器无值**，用户误判为「Modbus 无数据」。这是设计器→模拟器最显著的半闭环。
- **对比**：固件侧 `main.c` L265–270 主循环调用 `hmi_engine_poll()`，且 `hmi_ui_apply_theme`(L497) 内 `hmi_ui_start_binding_refresh()` 创建 100ms timer(`GenerateDeviceRefreshBindings` L947–951)。**固件侧是闭环保活，模拟器侧断链**。

### D2【高】绑定刷新分支不对称：布尔型(Led/Switch/CheckBox) 模拟器完全缺失
- **证据**：
  - 模拟器 `GenerateSimRefreshBindings`(L705–754) 的 `switch` **只含 Label/Bar/Slider/Gauge/Knob**，无 `Led/Switch/CheckBox` 分支。
  - 固件 `GenerateDeviceRefreshBindings`(L858–925) 的 `switch` **含 Led/Switch/CheckBox/Label/Bar/Slider/Gauge/Knob/Arc** 全类型。
- **影响**：即使模拟器将来接入刷新循环，Led/开关/勾选框在模拟器里也永远不会随绑定值变化；而固件会。同一控件在两运行时行为不一致。

### D3【高】动作 ShowAlarms / ShowTrends / Login / Logout 在固件是桩实现（配置即无操作）
- **证据**：`CodeGenerator.AppendActionBody`(L2109–2127) 为这 4 类动作生成 `hmi_show_alarms(); hmi_show_trends(); hmi_login(); hmi_logout();`；但固件 `firmware/t113/app/runtime/hmi_actions.c` L10–31 全部是 `HMI_LOG("...stub...")` 桩，**无任何真实 UI/会话逻辑**。
- **影响**：设计器可配置「点击→显示报警 / 显示趋势 / 登录 / 登出」，固件仅打日志，**用户点击无反应**。这是最典型的「半闭环」——编辑器功能看似完整、生成代码齐全、但运行时空转。
- **关联 D6**：因登录是桩，权限模型也无处落地（见 D6）。

### D4【中】Gauge/Knob 中心文本与刻度标签强制 `LV_FONT_DEFAULT`(Montserrat) → 中文豆腐框
- **证据**：
  - Gauge 中心数值文本：`CodeGenerator` L2463 `lv_obj_set_style_text_font(_gauge_lbl_{id}, LV_FONT_DEFAULT, 0);`
  - Gauge 刻度标签：`CodeGenerator` L2898 `lv_obj_set_style_text_font(_ticklbl_{id}_{i}, &lv_font_montserrat_12, 0);`
  - `LV_FONT_DEFAULT` = Montserrat，**无中文字形**。
  - 设计器 `RenderGauge`(L1296–1350) 用 `DrawCenteredText(... 14*z, ...)` 走设计器默认字体（中文正常），且注释称「中心数值文本使用项目默认字体」——**与生成代码实际强制 Montserrat 矛盾**。
- **影响**：Gauge 标题/刻度若含中文（如「温度」「压力」）在模拟器与固件均渲染为方框，而设计器正常。属「设计器↔运行时显示不一致」真实缺陷。

### D5【中】字体尺寸回退不一致：固件缺失资产时钳到 16px 嵌入子集，模拟器用 FreeType 精确尺寸
- **证据**：
  - 模拟器 `generated_fonts.c`：按控件 `FontSize` 生成 `font_asset_font_cjk_18/_20/_21/_32` 等精确尺寸 FreeType 字体（来自资产 `.ttc`，全字覆盖）。
  - 固件 `main.c` `hmi_resolve_font`(L90–125)：`if (!f) f = g_font_cn_16 ? g_font_cn_16 : LV_FONT_DEFAULT;` —— 资产 `.ttf` 缺失时**回退到 16px 嵌入 CJK 子集**（`g_font_cn_20 = &lv_font_source_han_sans_sc_16_cjk`，main.c L134）。
  - 注意 `main.c` L131–135 仍声明 `hmi_font_cn_init` 用 `_14_cjk/_16_cjk` 子集（与 `font-verification-report.md` 声称已补全 2500/2500 字一致，**覆盖无缺字，但尺寸只有 14/16 两档**）。
- **影响**：当工程字体资产未部署到 `/mnt/UDISK/hmi/assets` 时，固件把 20/32px 文本按 16px 子集渲染——**字号与设计器/模拟器不一致**（变小）。覆盖已无 tofu，但尺寸错配，属半闭环。

### D6【高】登录/权限模型未下沉到固件：设备端无 UserRole/Permission 执行
- **证据**：
  - 模型层 `src/HmiEditor.Core/Models/User/`(UserProfile/UserRole/Permission/UserSession/AuditLogEntry)、`SecurityConfig.cs` 完整。
  - 但固件 `app/` 全仓 grep `UserRole|Permission|g_logged|is_admin|require_login|permission` **无任何命中**；登录仅 `sys_settings.c` 的开机设置入口（boot 密码），且 `hmi_login` 是 D3 的桩。
  - CodeGen 对 SecurityConfig/用户角色**无任何生成产物**（grep `SecurityConfig|Permission|UserRole` 在 CodeGenerator 无输出）。
- **影响**：设计器配的「按权限控制屏幕/动作」在设备端**完全不生效**；「报警趋势登录」中的「登录」仅固件桩 + boot 密码，与权限模型脱节。闭环在编辑器内，设备端断链。

### D7【中】报警/趋势引擎在模拟器不被轮询 → 设计态无法验证引擎行为
- **证据**：
  - 引擎代码由 `GenerateAlarmCode`(L1209+)/`GenerateTrendCode`(L1294+) 生成进 `.so`（模拟器与固件共用同段生成代码，故代码本身存在）。
  - 固件 `main.c` L158–178 `hmi_engine_poll` 每帧把变量快照喂给 `alarm_evaluate`/`trend_tick`（已接线）。
  - 模拟器 `main.c` L272–276 主循环**无引擎轮询**（无 `hmi_engine_poll` 等价物）。
- **影响**：模拟器能渲染趋势图控件(lv_chart)但**永远不推进数据点**；报警计数标签停在初始「0 / 总 N」。设计器能配报警/趋势，但模拟器无法验证求值/记录是否正确——半闭环（固件活、模拟器死）。

### D8【低-中】事件映射：动作 `TriggerEvent` 仅 `OnPress`/`OnClick` 两档，与固件 LVGL 事件对齐但设计器语义需核对
- **证据**：`AppendEventRegistration`(L2021) 把 `OnPress`→`press`(LV_EVENT_PRESSED)、其余→`click`(LV_EVENT_CLICKED)；MomentaryOn/Off 拆 press/release(L2003–2015)。设计器 `PropertyPanelView` 动作编辑未在本仓库直接命中中文名（见 `docs/archive/2026-08/designer-hardcode-audit.md` B 节 L1775–1786，动作类型中文映射在属性面板硬编码但未走本地化）。
- **影响**：事件绑定本身闭环，但动作类型显示名/本地化与设计器一致性属可维护性缺口（既有审计已记），不影响功能闭环。

### 已确认闭环（非缺陷，供验收基线）
- **背景图**：设计器 `Render`/`RenderImage` → CodeGen L1593–1598(`bg_img` + `LV_IMAGE_ALIGN_STRETCH` + opa) → 模拟器(`generated_hmi.c` L114–119) → 固件(`screens.c` L114–119) 完全一致。
- **Gauge/Knob 弧度**：`ArcLayout`(StartAngle=135, SweepAngle=270) 被设计器 `RenderGauge`/`RenderKnob` 与 CodeGen `lv_arc_set_rotation(135)+bg_angles(0,270)` 共用，角度语义一致（见 `CodeGenerator` L2432–2434 注释「与设计器共用 ArcLayout，防漂移」）。**闭合良好**。
- **Table 单元格背景/网格色**：设计器 `Slate800(#1e293b)` / `Border(#334155)` ↔ 生成代码 `lv_color_hex(0x1e293b)`/`0x334155`(CodeGenerator L2516/L2505；screens.c L355/L353) 一致。
- **绑定值读取链路(固件)**：`hmi_ui_refresh_timer_cb`→`get_tag_value`(`tags.h` L71, 含 scale/offset + 只读保护)→`var_raw_read`(`variable_table.c` L135) 全链路闭环保活，scale/offset/只读在生成元数据内联。

---

## 三、验收建议（Acceptance Criteria）

### P0（阻断级：功能在模拟器/固件空转或半坏）
1. **D1 模拟器刷新死代码**：在 `sim-output/main.c` 主循环接入 `sim_refresh_bindings(sim_tick++)`（由 `lv_tick_get()` 或自增计数驱动）。**验收**：模拟器打开绑定工程，Label/Bar/Slider/Gauge/Table 单元格随正弦波跳动，与设计器预览值量级一致。
2. **D2 布尔刷新缺失**：模拟器 `GenerateSimRefreshBindings` 补 `Led/Switch/CheckBox` 分支（对齐固件 `GenerateDeviceRefreshBindings` L860–875）。**验收**：开关/勾选框在模拟器随绑定值翻转。
3. **D3 动作桩**：实现 `hmi_show_alarms`/`hmi_show_trends`/`hmi_login`/`hmi_logout` 真实逻辑（至少弹出报警列表/趋势屏/登录对话框），或将动作在设计器置灰提示「固件未实现」。**验收**：点击这 4 类动作在实机有可见响应（非仅日志）。
4. **D6 权限下沉**：明确「登录/权限」在设备端是否落地。若本期不做，需在编辑器/生成代码标注「权限/登录仅 boot 密码，动作登录为桩」；若要做，CodeGen 需生成 SecurityConfig→固件，并由 `hmi_login` 真正建立会话与权限校验。**验收**：配了权限的屏幕/动作在越权账户下被拦截。

### P1（显示正确性）
5. **D4 Gauge 中文豆腐框**：将 `_gauge_lbl_` 与 `_ticklbl_` 字体改为项目字体/嵌入 CJK 子集（同 `font_asset_font_cjk` 而非 `LV_FONT_DEFAULT`/`montserrat_12`）。**验收**：Gauge 标题与刻度含中文时正常渲染。
6. **D5 字体尺寸钳制**：固件 `hmi_resolve_font` 缺失资产时，按需用 FreeType 以**请求尺寸**创建回退（而非固定 16px），或 CodeGen 为非常规尺寸预生成子集。**验收**：缺资产时 20/32px 文本与设计器尺寸一致。

### P2（验证完整性）
7. **D7 模拟器引擎轮询**：模拟器主循环接 `alarm_evaluate`/`trend_tick`（喂 `VariableSimulator` 模拟快照）。**验收**：趋势图在模拟器推进、报警计数随阈值变化。
8. **回归用例**：新增「模拟器↔固件绑定刷新一致性」ctest；对每种绑定控件类型分别断言模拟器与固件最终像素/值等价（参考既有 `tests/HmiEditor.Core.Tests/SimulatorLoopTests.cs`）。

---

## 四、关键文件索引（证据锚点）
- `src/HmiEditor.CodeGen/CodeGenerator.cs`：L484–498(固件刷新接线)、L632–779(模拟器刷新)、L783–954(固件刷新生成)、L1209–1495(报警/趋势引擎生成)、L1980–2169(动作生成)、L2426–2537(Gauge/Knob/Table 生成)、L2463/2898(字体豆腐)、L3045–3101(字体尺寸收集)
- `sim-output/main.c` L272–276（模拟器主循环无刷新/引擎轮询）
- `sim-output/generated_hmi.c`（绑定刷新函数体、背景图、Table、Gauge 标签字体）
- `firmware/t113/app/main.c` L90–136(字体解析/回退)、L152–178(引擎轮询)、L219–260(字体初始化)
- `firmware/t113/app/runtime/hmi_actions.c` L10–31（动作桩）
- `firmware/t113/app/runtime/variable_table.c` L135–172(读缓存/写保护)、L281–343(轮询组)
- `firmware/t113/generated/tags.h` L71–85(get/set_tag_value 桥)
- `firmware/t113/generated/screens.c`（背景图/Table/Gauge 实际落地）
- `src/HmiEditor.App/Controls/DesignCanvas.cs` L1296–1400(RenderGauge/RenderKnob)、L1684+(RenderTablePlaceholder)、L935–1023(RenderGauge/RenderKnob 旧行号段)
- `src/HmiEditor.Core/Design/ArcLayout.cs`、`PaletteDefaults.cs`（共享常量，已收敛）
