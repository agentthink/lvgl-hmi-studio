# 状态文本 / 状态垂直对齐的**运行时回调**（阶段 229）
## —— 登记 `#92` 的功能那半：让面板上设的东西在设备上**真的生效**

> 阶段 202 把"导出**连注释都没有**"的账补上了（`Text` 一行注释、`TextVAlign` 一行注释）。
> 这一阶段把**功能**补上：生成 `LV_EVENT_STATE_CHANGED` 回调。

---

## 1. 为什么静态样式表达不了

| 设置 | 能不能用 `lv_obj_set_style_*(..., LV_STATE_*)`？ |
|---|---|
| 背景色 / 文本色 / 边框色 / **水平对齐** | ✅ 能（阶段 202 起就在发） |
| **状态文本**（按下换字） | ❌ 文本不是样式属性 ⇒ 只能回调里 `lv_label_set_text` |
| **状态垂直对齐** | ❌ **连状态化样式属性都没有**；Normal 态那条路是 `LV_SIZE_CONTENT` ＋ 重定位的**变换**，而**变换带不了状态选择器** |

⇒ 只有事件回调：控件建好时注册 `LV_EVENT_STATE_CHANGED`，回调里按 `lv_obj_get_state(obj)` 分支改写。

## 2. 生成物形状

```c
/* 状态文本 / 状态垂直对齐的运行时切换（LV_EVENT_STATE_CHANGED，控件 c1）*/
static void _vs_state_c1(lv_event_t* e)
{
    lv_obj_t* obj = lv_event_get_target(e);
    lv_state_t st = lv_obj_get_state(obj);
    if (st & LV_STATE_PRESSED)
    {
        lv_label_set_text(obj, "按下");
        lv_obj_set_height(obj, LV_SIZE_CONTENT);
        lv_obj_update_layout(obj);
        int32_t _vs_h_c1 = lv_obj_get_height(obj);
        if (_vs_h_c1 > 40) { _vs_h_c1 = 40; lv_obj_set_height(obj, _vs_h_c1); }
        lv_obj_set_y(obj, 20 + 40 - _vs_h_c1);
    }
    else if (st & LV_STATE_DISABLED)
    {
        lv_label_set_text(obj, "禁用");
    }
    else
    {
        lv_label_set_text(obj, "默认");      // ← **复位**：回默认态必须写回基础文本
    }
}
```

注册（在控件建好之后、`GenerateVisualStates` 里）：

```c
    // 状态文本/垂直对齐：运行时由 _vs_state_c1 按当前状态切换（静态样式表达不了）
    lv_obj_add_event_cb(obj_c1, _vs_state_c1, LV_EVENT_STATE_CHANGED, NULL);
```

## 3. 几条定死的规则

1. **每类型用自己的文本 API**：`Label → lv_label_set_text` · `CheckBox → lv_checkbox_set_text` ·
   `TextArea → lv_textarea_set_text` · `Button → 子 label 0`（带空指针保护）——
   与阶段 219 的文本发射口径一致。
2. **垂直对齐只有 Label 走回调**（只有它有"内容高度"这个概念）；其它类型保持**注释声明**。
3. **做不到的组合不注册**：控件类型不支持（如 `Slider` 的状态文本）⇒ 保持注释、**不生成空转回调**
   （否则"零影响"会被一个什么都不做的回调破坏）。
4. **复位分支必须有**：回到默认态时写回基础文本/基础对齐，否则"按下改的字"会一直留在控件上。
5. **顺序**：回调定义是**文件级**的，必须排在 `create_screen_*` **之前**（C 里不能先用后定义）——
   与既有 `GenerateActionCallbacks` 同一位置；注册则留在控件建好之后。判据钉住这个顺序。

## 4. 零影响

没有任何非 Normal 态的 `Text`/`TextVAlign` 覆盖（或全是"做不到"的组合）⇒ **不生成函数、不注册事件**，
生成物与从前逐字节相同。

## 5. 判据 8 条（`VisualStateRuntimeTextTests`）

无覆盖零影响 · 只有静态样式能表达的东西（`TextAlign`）仍不发回调 · Label 状态文本（回调＋注册＋复位）·
多状态 `else if` 链 · 状态垂直对齐（重定位且**该分支不换文本**）· CheckBox/Button 各用自己的 API ·
做不到的组合不注册 · **回调定义排在 `create_screen_*` 之前、注册在其后**。

同时升级了阶段 202 的两条判据（它们钉的是"声明"措辞）：现在同时钉住"注释还在"与"**注册在**"。

## 6. 负控（5 支，`.workbuddy/tmp/s229-nc.py`）

不注册事件 · 去掉复位分支 · 无覆盖也生成回调 · 状态位写错（Pressed 用 `LV_STATE_CHECKED`）·
Button 不取子 label。五支各自咬住对应判据。

## 7. 读数

**门禁**：`GATE_EXIT=0` / **7 步** / 日志 **983 行**（非空 **947**）；负载 `8.88 / 14.36 / 13.97`；
脚本 **29**；路径卫生 **704**（＋1：新源文件）；步 5 逐程序集 `44 / 44 / 4 / 96 / 78 / 2348 / 315 / 832 / 8`
（合计 **3769**）＝ **3769 通过 / 0 失败**（阶段 228 为 3761，**＋8**）；`HmiEditor.Core.Tests` **2340 → 2348**；
`MainWindow.axaml.cs` 仍 **2983**。

## 8. 仍未做（如实划界）

1. **设备侧没有跑过**：本仓的判据是"生成物长什么样"，回调在真机/LVGL 模拟器里的行为没有验收
   （模拟器不解释生成的 C）。
2. 非 Label 的**状态垂直对齐**仍是注释（要做得先给那些控件定义"内容高度"的语义）。
3. `#92` 里没有别的残留：面板写、预览渲染、导出消费三侧的账到此闭合。
   ⚠ 但**说给用户听的那句话**当时还落在后面：面板上 `prop.visual.state.export.hint.text` / `.valign`
   仍写着"导出只留一行声明注释"（那是阶段 202/208 的事实）⇒ 已由**阶段 230** 改对，
   并把"提示不得落后于行为"配成判据 ⇒ `docs/2026-10-01-export-hint-fidelity.md`。
