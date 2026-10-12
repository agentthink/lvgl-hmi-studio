# LVGL 修复补丁

本目录存放对 LVGL（v9.5）源码的本地修复补丁，用于固件侧与模拟器侧保持一致。

## 0001-fix-bmp-full-decode.patch

**问题**：LVGL 原版 `lv_bmp.c::decoder_open()` 只读取 54 字节 BMP 文件头、不解码像素
（`dsc->decoded = NULL`）。运行时 `img_decode_and_draw` 检测到 `decoded==NULL` 会走
**partial decode 路径**：`decoder_get_area` 每次只解码 1 行到临时 draw_buf，把
`absolute_decoded_area`（1 行高）当作 `img_coords` 传给 `transform_and_recolor`，
软件变换器据此算出 `src_w=img_width, src_h=1` —— **完全丢失源图高度**，导致
STRETCH/缩放失效，图片只能 1:1 显示一行（"图片只显示一部分"）。

**修复**：在 `decoder_open()` 末尾完整解码整张 BMP 到 `dsc->decoded`，使
`img_decode_and_draw` 走 **full decode 路径**，`img_coords` 携带完整源图尺寸，
软件变换器正确执行 STRETCH/Contain 缩放。

**应用**（在固件 lvgl 源码目录执行）：

```bash
git apply /path/to/zhishu-hmi/patches/lvgl/0001-fix-bmp-full-decode.patch
# 或
patch -p1 < /path/to/zhishu-hmi/patches/lvgl/0001-fix-bmp-full-decode.patch
```

**已同步位置**（2026-08-13 手动应用，均与补丁一致）：

| 位置 | 说明 |
|---|---|
| `firmware/Tina-Linux/package/gui/lvgl/` | Tina-Linux SDK LVGL 包源码 |
| `firmware/Tina-SDK/package/gui/lvgl/` | Tina-SDK LVGL 包源码 |
| `firmware/t113/host_test/lvgl/` | T113 host_test 副本 |
| `/home/chen/Git/lvgl/` | 模拟器实际链接的本地 LVGL fork |

> 注：上述 lvgl 目录多为 `.gitignore` 排除的 vendored 副本（不入库），因此以补丁形式固化修复。
> `firmware/*/build/` 目录为派生产物，重新构建时从 package 源自动带上该修复。

## 0002-fix-negative-scale-mirror.patch

**问题**：LVGL 9.5 对象级负 scale（`transform_scale_x/y = -256`，即镜像）会导致
**整个对象不渲染**。三处拦截 + 一处计算缺陷：

1. `lv_draw_image.c::lv_draw_layer/lv_draw_image` 入口 `if(scale_x <= 0 || scale_y <= 0) return;`
   直接拒绝负 scale（layer 变换路径，默认配置）。
2. `lv_refr.c::obj_get_matrix`（`LV_DRAW_TRANSFORM_USE_MATRIX` 路径）同样拒绝 `scale <= 0`。
3. `lv_image_buf_get_transformed_area` 的 scale 参数为 `uint16_t`（无符号），负值
   传入会截断成 255 倍放大，real_area 计算错误。
4. `lv_draw_sw_transform.c` 的 scale-only 优化用 `x_max = (src_w-1-pivot.x)*scale>>8 + pivot.x`
   限制目标区域——**负 scale 时该式算出的是最小边界**，导致目标区域被 clamp 成近 0 宽。

**修复**：
- 拦截放宽为仅拒绝 0（负 scale = 镜像，数学上受支持，软件变换器逆变换除负 scale
  自动反转采样方向，越界坐标输出透明，安全）；
- `lv_image_buf_get_transformed_area` 签名 `uint16_t` → `int32_t`；
- `x_max/y_max` 改用 `LV_ABS`（负 scale 时目标区域在 pivot 反向一侧，正边界必须取绝对值）。

**应用**（在固件 lvgl 源码目录执行）：

```bash
git apply /path/to/zhishu-hmi/patches/lvgl/0002-fix-negative-scale-mirror.patch
# 或
patch -p1 < /path/to/zhishu-hmi/patches/lvgl/0002-fix-negative-scale-mirror.patch
```

**已同步位置**（2026-08-14 手动应用，均与补丁一致）：

| 位置 | 说明 |
|---|---|
| `firmware/Tina-Linux/package/gui/lvgl/` | Tina-Linux SDK LVGL 包源码 |
| `firmware/Tina-SDK/package/gui/lvgl/` | Tina-SDK LVGL 包源码 |
| `firmware/t113/host_test/lvgl/` | T113 host_test 副本 |
| `/home/chen/Git/lvgl/` | 模拟器实际链接的本地 LVGL fork（已验证截图：镜像对象正常渲染） |

**验证**：模拟器端到端（导出→cmake→Xvfb snapshot）确认 `transform_scale_x=-256`
镜像对象正常渲染（此前整对象不渲染，修复后按钮背景+文本完整显示）。
