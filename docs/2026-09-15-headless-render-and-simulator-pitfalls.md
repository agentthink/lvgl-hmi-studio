# 无头渲染与模拟器自检：现行坑位清单

> 2026-09-15 从 `.workbuddy/memory/MEMORY.md` 的 §5/§6 **析出**（那份长期笔记要控字节，
> 细节移到这里；合并了更早的 `archive/MEMORY-2026-09-15-21k-full.md` §5 内容）。
> 每条都是**实测**出来的，不是推测 —— 保留"现象 → 正解"的对照，因为现象往往看起来像别的问题。

## 1. 模拟器自检

- **必须带 `SDL_VIDEODRIVER=dummy`**。本机 SDL2 的 x11 后端不可用，换 Xvfb 也不行 ——
  NVIDIA 的 EGL 会劫持 Mesa，直接段错误。CTest 已声明该变量，**手工跑也要带**。
- **截图不能用 `scrot`**：截 SDL 窗口得到**空白**（SDL 的绘制不落进 X11 的窗口内容）。
  正解是 `lv_snapshot_take()` 读 `draw_buf`，并**避开 `lv_timer_handler`**（它会改写缓冲）。
- `--scale` 走 `lv_sdl_window_set_zoom`；`--window` 走 `fit_screen_to_canvas`
  （用真实布局坐标缩放），`SIM_FIT_MODE`：0=Fill / 1=Fit；窗口 resize 已禁用。

## 2. BMP 解码

- `lv_bmp.c` 的 `decoder_open()` 必须**完整解码**到 `dsc->decoded`。半解会让 STRETCH 模式
  只显示 **1 行** —— 看起来像"图片文件坏了"，实际是解码器偷懒。
- 像素格式实际是 **32-bit BGRA**，不是 24-bit RGB。

## 3. Table（LVGL 9.5）

- **没有** `lv_table_set_row_height`。行高只能用 style 的 `min_height` / `max_height`
  （`PART_ITEMS`）。
- 列宽均分要**显式**逐列调 `lv_table_set_column_width(col, w / cols)`，没有"均分"开关。

## 4. 渲染缓存与 Avalonia 绘制

- `RenderResourceCache` 必须存 `Immutable*`：把可变的 `AvaloniaObject` 放进去共享，
  会**污染**其他使用者（症状是"某个视图改了颜色，另一个跟着变"）。
- `FormattedText` 重建后立刻读 `.Width` 抛 `ObjectDisposedException` ⇒
  `InvalidateTexts()` + 重试。**`catch` 里要留线索** —— 不留的话这个异常看起来像随机崩溃。
- headless 平台三个陷阱：
  - `ArcTo` 是**桩**实现（高 0）⇒ 不能对它做任何几何断言；
  - `DrawingGroup.Open()` 只录**顶层**，要看全部内容得递归展开；
  - 「先热身再测」会**捂热待测键**（缓存命中造成假象）⇒ 别热身。
- 渲 PNG 必须用**独立会话** + `rtb.CreateDrawingContext()`；
  直接 `rtb.Render` 出来是白板。

## 5. Avalonia Grid

- 只设 `ColumnDefinitions` 不设 `RowDefinitions` ⇒ Grid 只有**一行**，
  所有 `Grid.Row >= 1` 的子元素被 clamp 到 row 0 ⇒ **元素重合**。
- 这条极隐蔽：XAML 完全合法、编译不报错、运行不抛异常，只是布局全错。

## 6. 设计器侧的文本与数值

- 设计器绘制文本必须消费 `control.TextColor`，**禁止**写死 `Colors.White`
  （深色主题下会"消失"）；对齐要用九宫格文本绘制（`DrawAlignedText`）。
- 生成的 C 浮点字面量必须整数补 `.0` 并带 `f`：要 `50.0f`，不是 `50f`
  （`50f` 在部分工具链上会被当成整数常量而截断）。
