# 模拟器「拉伸模式」显示不全问题诊断报告

> 排查对象：HMI PC 模拟器（LVGL v9.5 + SDL2）。当图形设置为拉伸（Stretch / Fill）后，画面仍只显示一部分。
> 结论先行：**问题不在「图片拉伸」本身，而在模拟器把「窗口尺寸」和「显示画布（display 逻辑分辨率）」混为一谈，且屏幕内容是按固定像素布局、没有整体缩放机制。**

> **修复状态（2026-08-13，已全部完成）**：方案 A（`--scale` 改用 `lv_sdl_window_set_zoom`）、方案 B（`--window` 屏幕自适应，运行时 `fit_screen_to_canvas` 铺满/适配画布，`SIM_FIT_MODE` 支持 Fill/Fit）、方案 D（同步固件 BMP full-decode + `LV_MEM_SIZE` 提到 8MB）、方案 E（禁用窗口自由 resize，避免 SDL resize 改画布不重排）均已落地，回归测试通过、C 代码编译通过、Xvfb 像素验证画面铺满（1280×800 / 800×480 / 1024×600 三档）。

---

## 一、核心结论（TL;DR）

1. **主因**：`main.c` 用 `lv_sdl_window_create(win_w * scale, win_h * scale)` 创建窗口。在 LVGL SDL 后端里，这个函数的两个参数**既是 SDL 窗口尺寸、又是 LVGL 显示设备（display）的逻辑分辨率**——也就是渲染画布的大小。于是 `--scale`、`--window` 改的不是"放大倍数"，而是"画布分辨率"。
2. **次因**：`generated_hmi.c` 里屏幕对象被写死为 `lv_obj_set_size(screen, 1024, 600)`，所有子控件都是固定 `lv_obj_set_pos/set_size` 的绝对像素坐标，**没有任何"跟随窗口/画布拉伸"的逻辑**。
3. **结果**：画布变大 → 1024×600 的画面缩在左上角，其余区域空白；画布变小 → 画面超出画布的部分被裁剪掉。这正是"只显示一部分 / 未能完整拉伸"。
4. **图片级拉伸（`LV_IMAGE_ALIGN_STRETCH`）本身已正确生成**，此前"图片只显示一条线/一部分"是 BMP 解码器的 partial-decode 缺陷，已在外部 LVGL 修复，但**未同步到固件侧 LVGL**（见 §2.5）。

---

## 二、渲染配置与实际显示输出不一致的根因

### 2.1 窗口尺寸 = 显示画布分辨率（关键缺陷）

`SimulatorProjectExporter.BuildMainC` 生成的窗口创建代码：

```c
int32_t win_w = cfg.window_w > 0 ? cfg.window_w : SIM_WINDOW_WIDTH;   // 默认 1024
int32_t win_h = cfg.window_h > 0 ? cfg.window_h : SIM_WINDOW_HEIGHT;  // 默认 600
lv_display_t* disp = lv_sdl_window_create(win_w * cfg.scale, win_h * cfg.scale);
lv_sdl_window_set_resizeable(disp, true);
```

LVGL SDL 后端 `lv_sdl_window.c` 的实现：

```c
lv_display_t * lv_sdl_window_create(int32_t hor_res, int32_t ver_res) {
    ...
    lv_display_t * disp = lv_display_create(hor_res, ver_res);   // ← 显示逻辑分辨率 = 参数
    ...
    /* window_create() 里再 SDL_CreateWindow(hor_res * zoom, ver_res * zoom)，zoom 默认 1.0 */
}
```

也就是说：**没有独立的 viewport/画布尺寸**。显示设备的逻辑分辨率就是画布，`lv_sdl_window_create` 的参数同时决定了画布和窗口。要"放大窗口但保持画布不变"，LVGL 提供的是另一套 API —— `lv_sdl_window_set_zoom(disp, zoom)`（内部把窗口尺寸乘 `zoom`、保持 `disp->hor_res/ver_res` 不变、用 SDL 纹理放大渲染），而**当前代码完全没用它**。

### 2.2 屏幕对象与子控件固定像素布局，无整体缩放

`CodeGenerator` 生成的 `create_screen_*_inner`：

```c
lv_obj_t* screen = lv_obj_create(parent);
lv_obj_set_size(screen, 1024, 600);          // 写死设计尺寸
...
obj_img_banner_top = lv_image_create(screen);
lv_obj_set_pos(obj_img_banner_top, 0, 0);
lv_obj_set_size(obj_img_banner_top, 1024, 120);   // 绝对坐标 + 绝对尺寸
...
```

屏幕对象不是 `LV_SIZE_CONTENT` 也不是跟随 display，而是被 `lv_obj_set_size` 钉死在 1024×600；子控件全部是绝对像素定位。因此无论画布/窗口变成多大，内容尺寸都不变。

### 2.3 `--scale` / `--window` 参数语义被错误实现

| 参数 | 设计意图（README/help） | 实际效果 |
|---|---|---|
| `--scale <n>` | "窗口放大倍数" | 把**画布分辨率**乘 n（如 scale=2 → 画布 2048×1200），内容不放大 |
| `--window WxH` | "窗口分辨率 / 设备分辨率，激活自适应断点" | 直接把画布设为 WxH，内容固定 1024×600 不重排 |

两者还会相乘（`win_w * cfg.scale`），语义进一步混淆。

### 2.4 可缩放窗口 + resize 事件再次改画布

`lv_sdl_window_set_resizeable(disp, true)` 允许用户拖动缩放窗口。SDL 后端收到 `SDL_WINDOWEVENT_RESIZED` 时会执行：

```c
lv_display_set_resolution(disp, event.window.data1 / dsc->zoom, event.window.data2 / dsc->zoom);
```

即**用户每拖一次窗口，画布分辨率就被改写一次**，而屏幕内容从不重排 —— 拖大出现空白、拖小出现裁剪。这同样导致"只显示一部分"。

### 2.5 图片级拉伸（LV_IMAGE_ALIGN_STRETCH）的独立问题（已修复 / 需同步）

`CodeGenerator` 的图片拉伸生成是正确的：

```c
lv_image_set_inner_align(obj_img_banner_top, LV_IMAGE_ALIGN_STRETCH);  // Fill/Stretch → STRETCH
// Contain → LV_IMAGE_ALIGN_CONTAIN（<9.3 回退 STRETCH）；None/Center → LV_IMAGE_ALIGN_CENTER
```

但此前"图片拉伸后只显示一条线/一部分"的根因是 **BMP 解码器 partial-decode 缺陷**：原版 `lv_bmp.c::decoder_open` 只读 54 字节头、`dsc->decoded = NULL`，运行时走逐行 partial decode，把源图高度丢失（`src_h=1`），软件变换器无法缩放。

当前状态：
- ✅ **外部 LVGL（模拟器实际链接的 `/home/chen/Git/lvgl`）已修复**：`decoder_open` 末尾完整解码整张 BMP 到 `dsc->decoded`，走 full decode 路径。
- ❌ **固件侧 LVGL（`firmware/t113/host_test/lvgl/src/libs/bmp/lv_bmp.c`）仍是未修复原版**，`decoder_open` 只读 54 字节头。若固件里也用了 BMP + 拉伸，需同步修复。
- ⚠️ 大图完整解码对内存有要求：`LV_MEM_SIZE` 需 ≥ 8MB（默认 256KB 时 `lv_draw_buf_create` 会返回 NULL，图片加载失败）。

---

## 三、不同窗口大小 / 分辨率下的表现

设计分辨率（bootScreen）= **1024×600**，比例 ≈ 1.707。

| 运行方式 | 画布（display） | 窗口 | 屏幕对象 | 表现 |
|---|---|---|---|---|
| 默认（无参数） | 1024×600 | 1024×600 | 1024×600 | ✅ 正常，画面铺满 |
| `--scale 2` | **2048×1200** | 2048×1200 | 1024×600 | ❌ 画面缩在左上角 1024×600，其余 3/4 空白 |
| `--window 1280x800` | **1280×800** | 1280×800 | 1024×600 | ❌ 画面缩在左上角，右侧/下方空白 |
| `--window 800x480` | **800×480** | 800×480 | 1024×600 | ❌ 画面被裁掉右侧 224px、下方 120px（**真正被裁剪**） |
| `--window 1024x600` | 1024×600 | 1024×600 | 1024×600 | ✅ 与默认一致 |
| 手动拖拽窗口变大/变小 | 跟随窗口 | 跟随窗口 | 1024×600 | ❌ 拖大空白、拖小裁剪，且不重排 |

**宽高比说明**：设计 1024:600 ≈ 1.707；`--window 1280x800` = 1.6。即便引入"整体缩放"，也会因比例不同而面临三种取舍：

| 模式 | 语义 | 效果 |
|---|---|---|
| Stretch/Fill（拉伸铺满） | sx、sy 独立缩放 | 铺满但**比例失真**（圆变椭圆） |
| Fit/Contain（等比适配） | s = min(sx, sy)，居中 | 比例不变，但上下或左右留黑边（letterbox） |
| Cover（等比裁切） | s = max(sx, sy) | 比例不变铺满，但四周被裁 |

`CodeGenerator` 里已有对应的自适应布局生成器 `GenerateAdaptiveLayout`（`ScreenAdaptiveMode.Fit/Fill`），它按 display 分辨率对每个控件做 `set_pos/set_size` 重排。但它有四个局限：
1. 当前屏幕 `AdaptiveMode == None`，**根本没生成**（`generated_hmi.c` 里看不到 `adaptive_layout_*`）。
2. 只在 `create_screen_*` 末尾调用一次，**窗口 resize 后不重跑**。
3. 它不缩放屏幕对象本身和屏幕背景图（`bg_img` 仍是 `screen.Width/Height`）。
4. 它依赖 `lv_display_get_horizontal_resolution`，而该值被 §2.1 的"画布=窗口"逻辑污染。

---

## 四、修复建议

### 方案 A（推荐）：`--scale` 改用 `lv_sdl_window_set_zoom`，画布恒为设计分辨率

在 `SimulatorProjectExporter.BuildMainC` 中把窗口创建改为"先按设计分辨率建画布，再按 `--scale` 设 zoom"：

```c
/* 画布恒为设计分辨率（bootScreen），窗口放大交给 zoom，不动画布 */
lv_display_t* disp = lv_sdl_window_create(SIM_WINDOW_WIDTH, SIM_WINDOW_HEIGHT);
lv_sdl_window_set_zoom(disp, (float)cfg.scale);   /* 1.0 / 2.0 / ... 保持画面等比放大 */
```

效果：`--scale 2` → 画布仍 1024×600，窗口 2048×1200，SDL 纹理放大 2 倍铺满，**画面完整、无空白、无裁剪**。这是 LVGL SDL 后端语义正确、成本最低的改法。

### 方案 B：`--window WxH` 与自适应布局联动（支持真实多分辨率设备）

若 `--window` 的本意是"模拟不同设备分辨率"，则应保留"改画布"，但必须让内容重排：

1. 生成时把屏幕 `AdaptiveMode` 设为 `Fit`/`Fill`（在属性面板暴露该选项）。
2. 在 `main.c` 里，`lv_screen_load` 之后、以及 `LV_EVENT_RESOLUTION_CHANGED`（窗口 resize）回调里，**重新调用** `adaptive_layout_<id>(lv_screen_active())`。
3. 让 `GenerateAdaptiveLayout` 同时缩放屏幕对象与背景图（补上 `lv_obj_set_size(screen, act_w, act_h)` / `bg_img`）。
4. 对"比例不同"的场景，把 Fit/Fill 的选择暴露给用户（对应 Contain / Stretch 语义）。

### 方案 C：屏幕级 transform 整体拉伸（保留固定布局，快速出效果）

不想逐控件重排时，可对屏幕根对象做整体缩放（LVGL 9 支持对象 transform）：

```c
/* 将 1024×600 屏幕整体缩放到画布，sx/sy 独立（Stretch）或等比（Contain） */
lv_obj_set_style_transform_scale_x(screen, sx, 0);
lv_obj_set_style_transform_scale_y(screen, sy, 0);
```

注意：transform 是视觉缩放，**命中测试/输入坐标**需同步处理（或换算事件坐标），交互场景要谨慎；纯展示预览足够。

### 方案 D：图片级拉伸的收尾（防止回潮）

1. 把 `/home/chen/Git/lvgl/src/libs/bmp/lv_bmp.c` 的 full-decode 修复**同步回固件侧** `firmware/*/lvgl/src/libs/bmp/lv_bmp.c`（当前固件侧仍是未修复原版）。
2. `lv_conf.h` 的 `LV_MEM_SIZE` 保持 ≥ 8MB（大图完整解码 + 快照需要）。
3. 补一条回归测试：STRETCH 模式下图 `decoded != NULL`、最终渲染 `src_w/src_h` 等于完整源图尺寸（见既有 `SimulatorFixVerification` 的验证思路）。

### 方案 E（可选）：关闭自由缩放或规范化 resize 行为

若模拟器只用于"固定设备分辨率"预览，可直接 `lv_sdl_window_set_resizeable(disp, false)`，避免拖拽窗口触发 §2.4 的"改画布不重排"问题。

---

## 五、验证清单

- [ ] `--scale 2` 时画面等比放大铺满窗口，无空白、无裁剪。
- [ ] `--window 1280x800`（比例不同）时按选定模式（Stretch/Contain）正确呈现，且说明比例差异的取舍。
- [ ] 拖拽窗口大小后画面仍完整（方案 B/E 生效）。
- [ ] 图片控件 STRETCH 完整铺满其控件框（BMP full-decode 生效，`LV_MEM_SIZE` 充足）。
- [ ] 固件侧 BMP 解码器已同步 full-decode 修复。

---

## 附：关键代码位置

| 位置 | 文件 | 说明 |
|---|---|---|
| 窗口/画布创建 | `src/HmiEditor.Core/Services/SimulatorProjectExporter.cs` → `BuildMainC`（`lv_sdl_window_create(win_w * cfg.scale, ...)`） | **主缺陷点** |
| 屏幕固定尺寸 | `src/HmiEditor.CodeGen/CodeGenerator.cs` L1224-1225 | `lv_obj_set_size(screen, W, H)` |
| 自适应布局生成器 | `src/HmiEditor.CodeGen/CodeGenerator.cs` → `GenerateAdaptiveLayout` L1543 | Fit/Fill 语义已有但未启用 |
| SDL 后端画布/zoom | 外部 `/home/chen/Git/lvgl/src/drivers/sdl/lv_sdl_window.c` | `lv_sdl_window_create` / `set_zoom` / resize 事件 |
| BMP full-decode 修复 | 外部 `/home/chen/Git/lvgl/src/libs/bmp/lv_bmp.c` L184 | 已修复（固件侧未同步） |
| 图片拉伸生成 | `src/HmiEditor.CodeGen/CodeGenerator.cs` L2023-2042 | `lv_image_set_inner_align` |
