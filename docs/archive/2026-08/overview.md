# T30: Slider/Bar 设计器与模拟器显示差异修复

## 问题

用户截图对比发现滑动条(Slider)和柱状图(Bar)控件在设计器内与模拟器实际显示存在差异。

## 根因分析

通过 LVGL 源码分析，确认了三个导致差异的因素：

### 1. LV_PART_KNOB 默认 padding（主要根因）
LVGL 默认主题对 slider 的 `LV_PART_KNOB` 添加 `knob` 样式，其中 `pad_all = LV_DPX_CALC(dpi, 6)` ≈ 6-10px。
- **设计器**：knob 直径 = `min(width, height)` = 控件高度（如 30px）
- **模拟器**：knob 基础尺寸 = `min(width, height) / 2` = 15px，加上 padding 后总尺寸 ≈ 27-35px，**大于控件高度**

### 2. LV_PART_INDICATOR 圆角被主题覆盖
LVGL 默认主题对 `LV_PART_INDICATOR` 添加 `circle` 样式（`LV_RADIUS_CIRCLE`），当用户设置自定义 `Radius > 0` 时：
- **设计器**：指示器圆角 = 用户 Radius 值
- **模拟器**：指示器圆角 = `LV_RADIUS_CIRCLE`（胶囊形），**忽略用户自定义值**

### 3. LV_PART_MAIN padding（防御性）
虽然 LVGL 默认 padding 为 0，但主题更新可能引入隐含 padding，导致指示器不充满控件宽高。

## 修复内容

**文件**: `src/HmiEditor.CodeGen/CodeGenerator.cs` (`GenerateControlParameters` 方法)

### Slider 分支新增：
```c
lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);     // 指示器充满控件
lv_obj_set_style_pad_all(obj, 0, LV_PART_KNOB);     // knob 直径=控件高度
lv_obj_set_style_radius(obj, indicRadius, LV_PART_INDICATOR);  // 用户圆角
```

### Bar 分支新增：
```c
lv_obj_set_style_pad_all(obj, 0, LV_PART_MAIN);     // 指示器充满控件
lv_obj_set_style_radius(obj, indicRadius, LV_PART_INDICATOR);  // 用户圆角
```

其中 `indicRadius = Radius > 0 ? Radius : Height / 2`（与设计器语义一致）。

## 验证结果

使用 MockChineseProject（slider value=42, bar value=68）重新生成并截图分析：

| 控件 | 期望进度 | 实际进度 | 填充起点 | 状态 |
|------|---------|---------|---------|------|
| Slider (传送速度) | 42% | 45.5% (含knob延伸) | x=600 (左缘) | ✅ |
| Bar (罐体液位) | 68% | 67.9% | x=600 (左缘) | ✅ |

- 填充从控件左缘开始，无 padding 偏移 ✅
- 轨道透明（`bg_opa=LV_OPA_TRANSP`），显示画布背景色 ✅
- 指示器颜色精确匹配（#8b5cf6 / #06b6d4）✅
- 全部 1093 测试通过（39 Simulator + 1054 Core）✅

## 截图

`sim-slider-bar-verify.png` — 修复后的模拟器截图
