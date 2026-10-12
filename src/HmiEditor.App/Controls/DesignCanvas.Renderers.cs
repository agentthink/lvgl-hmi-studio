using Avalonia;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.IO;
using System.Linq;

namespace HmiEditor.App.Controls;

/// <summary>
/// DesignCanvas 分部 —— 控件渲染器（基本控件与图形控件）。
/// 覆盖 Button/Switch/CheckBox/Image/Dropdown/Gauge/Knob/Led/Spinner/
/// Line/Rectangle/Ellipse/Polyline/Arc/Keyboard/TabView/Table，
/// 以及文本对齐、圆弧几何等渲染期公用的绘制 helper。
/// </summary>
public partial class DesignCanvas : Control
{
    // ── 状态化样式渲染（Button/Image）──

    /// <summary>
    /// 按钮渲染：根据 IsChecked 选择 Normal/Checked 状态样式，回退到默认属性。
    /// 若配置了图片则绘制图片，否则绘制背景+边框+文字。
    /// </summary>
    private void RenderButton(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // 设计器预览态：IsChecked=true 显示 Checked 态样式，否则 Normal
        // Pressed/Disabled 在设计器中不模拟（仅运行时体现）
        var previewState = control.IsChecked ? VisualState.Checked : VisualState.Normal;
        var stateStyle = control.GetStateStyle(previewState);

        // 颜色回退链：状态覆盖 → 默认属性 → 内置兜底色
        var bgColor = stateStyle?.BgColor ?? control.BgColor;
        // 文本色：状态覆盖 → 控件默认 → 调色板兜底（与 RenderControl 同一解析器）
        var textColor = ControlTextStyleResolver.PreferHex(stateStyle, control, null);
        var borderColor = stateStyle?.BorderColor ?? control.BorderColor;

        // 文本：状态覆盖 → 控件默认文本；对齐：状态覆盖 → 控件默认对齐
        var label = stateStyle?.Text ?? control.Text ?? Services.ControlNameDefaults.Display(control);
        var hAlign = ControlTextStyleResolver.HorizontalAlign(stateStyle, control, previewState);
        var vAlign = ControlTextStyleResolver.VerticalAlign(stateStyle, control, previewState);

        // 图片：状态条目优先，回退到控件主图
        var imageAssetId = previewState == VisualState.Normal
            ? (control.ImageAssetId)
            : (stateStyle?.ImageAssetId ?? control.ImageAssetId);

        if (!string.IsNullOrEmpty(imageAssetId) && TryDrawImage(context, imageAssetId, rect))
        {
            // 图片已绘制，仅在边框存在时叠加边框
            if (control.BorderWidth > 0)
            {
                var borderBrush = ParseColor(borderColor, DesignPalette.Border);
                context.DrawRectangle(
                    RenderResourceCache.Pen(borderBrush, control.BorderWidth * z),
                    rect,
                    (float)(control.Radius * z));
            }
            return;
        }

        // 无图片 —— 走矩形+文字渲染（未配置 BgColor 则不填充背景，保持透明，仅边框+文字）
        if (!string.IsNullOrEmpty(bgColor))
        {
            var bgBrush = ParseColor(bgColor, DesignPalette.Surface);
            context.FillRectangle(bgBrush, rect, (float)(control.Radius * z));
        }

        if (control.BorderWidth > 0)
        {
            var borderBrush = ParseColor(borderColor, DesignPalette.Border);
            context.DrawRectangle(
                RenderResourceCache.Pen(borderBrush, control.BorderWidth * z),
                rect,
                (float)(control.Radius * z));
        }

        if (!string.IsNullOrEmpty(label))
        {
            var labelColor = ParseColorHex(textColor, DesignPalette.Text);
            var fontSz = (control.FontSize > 0 ? control.FontSize : HmiControl.DefaultFontSize) * z;
            DrawAlignedText(context, label, rect, fontSz, labelColor, hAlign, vAlign);
        }
    }

    /// <summary>
    /// Switch 渲染：胶囊轨道 + 圆形滑钮，与 LVGL lv_switch 形态一致。
    /// 轨道底色取 BgColor（默认深灰），滑钮/激活色取 IndicatorColor ?? TextColor（默认亮蓝）。
    /// </summary>
    private void RenderSwitch(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var trackColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Surface);
        var onColor = ParseColorHex(control.IndicatorColor ?? EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Primary);

        // 胶囊轨道：圆角 = 高度一半（LVGL 默认主题行为）
        var radius = Math.Max(2, (int)(control.Height / 2 * z));
        context.FillRectangle(RenderResourceCache.Brush(trackColor), rect, radius);

        // 滑钮：直径 = 高度 × 0.9，Checked 时靠右，否则靠左（LVGL switch 滑钮语义）
        var knobD = control.Height * 0.9 * z;
        var knobX = control.IsChecked
            ? rect.Right - knobD - (control.Height * 0.05 * z)
            : rect.Left + (control.Height * 0.05 * z);
        var knobY = rect.Y + (rect.Height - knobD) / 2;
        var knobFill = RenderResourceCache.Brush(control.IsChecked ? onColor : DesignPalette.OffKnob);
        context.DrawEllipse(knobFill,
            RenderResourceCache.Pen(DesignPalette.Slate900, 1 * z),
            new Rect(knobX, knobY, knobD, knobD));
    }

    /// <summary>
    /// CheckBox 渲染：方形勾选框 + 右侧文本，与 LVGL lv_checkbox 形态一致。
    /// 勾选色取 IndicatorColor ?? TextColor（默认亮蓝），边框取 BorderColor。
    /// </summary>
    private void RenderCheckBox(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var boxSize = Math.Min(rect.Height * 0.8, rect.Width * 0.35);
        var boxRect = new Rect(rect.X, rect.Y + (rect.Height - boxSize) / 2, boxSize, boxSize);
        var boxRadius = (float)(2 * z);

        // 勾选框底色（Checked 时用激活色填充）
        var onColor = ParseColorHex(control.IndicatorColor ?? EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Primary);
        var boxBg = control.IsChecked
            ? RenderResourceCache.Brush(onColor)
            : RenderResourceCache.Brush(ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.CheckBoxOff));
        context.FillRectangle(boxBg, boxRect, boxRadius);

        // 边框
        var borderColor = ParseColorHex(control.BorderColor, DesignPalette.Border);
        context.DrawRectangle(
            RenderResourceCache.Pen(borderColor, Math.Max(1, control.BorderWidth * z)),
            boxRect, boxRadius);

        // 勾选标记：对勾（两段折线），仅 Checked 时绘制
        if (control.IsChecked)
        {
            var markPen = RenderResourceCache.Pen(DesignPalette.Text, Math.Max(1.5, boxSize * 0.12));
            var m = boxSize * 0.22;
            context.DrawLine(markPen,
                new Point(boxRect.X + boxSize * 0.22, boxRect.Y + boxSize * 0.52),
                new Point(boxRect.X + boxSize * 0.44, boxRect.Y + boxSize * 0.72));
            context.DrawLine(markPen,
                new Point(boxRect.X + boxSize * 0.44, boxRect.Y + boxSize * 0.72),
                new Point(boxRect.X + boxSize * 0.82, boxRect.Y + boxSize * 0.28));
        }

        // 右侧文本（CheckBox 文本在勾选框右侧，与 LVGL 默认布局一致）
        var label = control.Text ?? Services.ControlNameDefaults.Display(control) ?? "CheckBox";
        if (!string.IsNullOrEmpty(label))
        {
            var labelColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Text);
            var fontSz = (control.FontSize > 0 ? control.FontSize : HmiControl.DefaultFontSize) * z;
            var textRect = new Rect(rect.X + boxSize + 6 * z, rect.Y,
                rect.Width - boxSize - 6 * z, rect.Height);
            DrawAlignedText(context, label, textRect, fontSz, labelColor,
                control.TextAlign, control.TextVAlign);
        }
    }

    /// <summary>
    /// Image 控件渲染：解析 ImageAssetId 绘制位图；无图时绘制占位矩形+"图片"文字。
    /// </summary>
    private void RenderImage(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // StretchMode 透传：设计器渲染与 LVGL lv_image_set_inner_align 语义一致
        if (!string.IsNullOrEmpty(control.ImageAssetId) &&
            TryDrawImage(context, control.ImageAssetId, rect, control.StretchMode))
            return;

        // 占位渲染（未配置 BgColor 则不填充，保持透明，仅边框+文字）
        if (!string.IsNullOrEmpty(control.BgColor))
        {
            var bgBrush = ParseColor(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Slate800);
            context.FillRectangle(bgBrush, rect, (float)(control.Radius * z));
        }

        if (control.BorderWidth > 0)
        {
            var borderBrush = ParseColor(control.BorderColor, DesignPalette.Border);
            context.DrawRectangle(
                RenderResourceCache.Pen(borderBrush, control.BorderWidth * z),
                rect,
                (float)(control.Radius * z));
        }

        var label = control.Text ?? DefaultTexts.ImagePlaceholder;
        var fontSz = (control.FontSize > 0 ? control.FontSize : HmiControl.DefaultFontSize) * z;
        DrawCenteredText(context, label, rect, fontSz, DesignPalette.Slate400);
    }

    /// <summary>
    /// Dropdown 渲染：与 LVGL lv_dropdown 形态一致 —— 主体背景（未配置 BgColor 时用中性深色，
    /// 与生成器 lv_dropdown 默认主题背景一致）+ 边框 + 左对齐选中项文本 + 右侧下拉箭头。
    /// </summary>
    private void RenderDropdown(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // 主体背景：BgColor 优先；未配置时用 Slate800（与通用路径 Dropdown 例外一致）
        var bgColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Slate800);
        var radius = (float)(control.Radius * z);
        context.FillRectangle(RenderResourceCache.Brush(bgColor), rect, radius);

        if (control.BorderWidth > 0)
        {
            var borderBrush = ParseColor(control.BorderColor, DesignPalette.Border);
            context.DrawRectangle(RenderResourceCache.Pen(borderBrush, control.BorderWidth * z), rect, radius);
        }

        // 选中项文本（左对齐，与 lv_dropdown 文本默认左对齐一致；右侧预留箭头空间）
        var label = control.Text ?? Services.ControlNameDefaults.Display(control);
        if (!string.IsNullOrEmpty(control.Options))
        {
            var opts = control.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (opts.Length > 0)
                label = opts[Math.Clamp(control.SelectedIndex, 0, opts.Length - 1)];
        }
        if (string.IsNullOrEmpty(label)) label = Services.ControlTypeTextKeys.Describe(control.Type);

        var arrowSize = Math.Min(6.0, rect.Height * 0.18) * z;
        var textRect = new Rect(rect.X + 4 * z, rect.Y,
            rect.Width - arrowSize * 2 - 8 * z, rect.Height);
        var fontSz = (control.FontSize > 0 ? control.FontSize : HmiControl.DefaultFontSize) * z;
        DrawAlignedText(context, label, textRect, fontSz,
            ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), ThemeFallback(Theme?.TextColor, DesignPalette.Text)),
            TextAlignMode.Left, TextVAlignMode.Middle);

        // 下拉箭头：右侧实心小三角（LVGL 默认主题箭头位置）
        var arrowColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), ThemeFallback(Theme?.TextSecondaryColor, DesignPalette.Slate400));
        var ax = rect.Right - arrowSize * 2;
        var ay = rect.Center.Y;
        var arrowPen = RenderResourceCache.Pen(arrowColor, Math.Max(1, 1.5 * z), PenLineCap.Flat, PenLineJoin.Round);
        context.DrawLine(arrowPen,
            new Point(ax - arrowSize, ay - arrowSize * 0.7),
            new Point(ax, ay + arrowSize * 0.7));
        context.DrawLine(arrowPen,
            new Point(ax, ay + arrowSize * 0.7),
            new Point(ax + arrowSize, ay - arrowSize * 0.7));
    }

    /// <summary>
    /// 加载并绘制图片位图。成功返回 true。位图按 AssetItem.Id 缓存。
    /// stretchMode 与生成器 LvglImageAlign 语义对齐（LVGL lv_image_set_inner_align）：
    ///   null/empty/Fill/Stretch → 拉伸填满（LV_IMAGE_ALIGN_STRETCH）
    ///   Contain → 等比缩放完整显示（LV_IMAGE_ALIGN_CONTAIN）
    ///   Center/None → 原始尺寸居中（LV_IMAGE_ALIGN_CENTER）
    ///   Tile → 平铺（LV_IMAGE_ALIGN_TILE）
    /// 未命中时回退拉伸（与 LVGL 默认 STRETCH 一致）。
    /// </summary>
    private bool TryDrawImage(DrawingContext context, string? assetId, Rect rect, string? stretchMode = null)
    {
        if (string.IsNullOrEmpty(assetId)) return false;

        var bitmap = ResolveBitmap(assetId);
        if (bitmap == null) return false;

        try
        {
            // 缩放模式解析与生成器共用 StretchModeMap（消除两处字符串 switch 漂移）
            switch (StretchModeMap.Parse(stretchMode))
            {
                case StretchModeKind.Contain:
                    DrawImageContain(context, bitmap, rect);
                    break;
                case StretchModeKind.Center:
                    DrawImageCenter(context, bitmap, rect);
                    break;
                case StretchModeKind.Tile:
                    DrawImageTile(context, bitmap, rect);
                    break;
                default: // Stretch → 拉伸填满（LVGL 默认 STRETCH）
                    context.DrawImage(bitmap, rect);
                    break;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>等比缩放完整显示（LV_IMAGE_ALIGN_CONTAIN）：缩放至适配矩形内，居中，不裁剪。</summary>
    private void DrawImageContain(DrawingContext context, Bitmap bitmap, Rect rect)
    {
        var z = EffectiveZoom;
        var iw = bitmap.PixelSize.Width * z;
        var ih = bitmap.PixelSize.Height * z;
        if (iw <= 0 || ih <= 0) return;
        var scale = Math.Min(rect.Width / iw, rect.Height / ih);
        var w = iw * scale;
        var h = ih * scale;
        context.DrawImage(bitmap, new Rect(
            rect.X + (rect.Width - w) / 2,
            rect.Y + (rect.Height - h) / 2,
            w, h));
    }

    /// <summary>原始尺寸居中（LV_IMAGE_ALIGN_CENTER）：不缩放，居中摆放，超出部分裁剪。</summary>
    private void DrawImageCenter(DrawingContext context, Bitmap bitmap, Rect rect)
    {
        var z = EffectiveZoom;
        var iw = bitmap.PixelSize.Width * z;
        var ih = bitmap.PixelSize.Height * z;
        using (context.PushClip(rect))
        {
            context.DrawImage(bitmap, new Rect(
                rect.X + (rect.Width - iw) / 2,
                rect.Y + (rect.Height - ih) / 2,
                iw, ih));
        }
    }

    /// <summary>平铺（LV_IMAGE_ALIGN_TILE）：按原始尺寸循环铺满，超出部分裁剪。</summary>
    private void DrawImageTile(DrawingContext context, Bitmap bitmap, Rect rect)
    {
        var z = EffectiveZoom;
        var tileW = Math.Max(1, bitmap.PixelSize.Width * z);
        var tileH = Math.Max(1, bitmap.PixelSize.Height * z);
        using (context.PushClip(rect))
        {
            for (double y = rect.Y; y < rect.Bottom; y += tileH)
            {
                for (double x = rect.X; x < rect.Right; x += tileW)
                {
                    context.DrawImage(bitmap, new Rect(x, y, tileW, tileH));
                }
            }
        }
    }

    /// <summary>按 AssetItem.Id 解析位图（带缓存）。加载失败返回 null。</summary>
    private Bitmap? ResolveBitmap(string assetId)
    {
        if (_imageCache.TryGetValue(assetId, out var cached)) return cached;

        Bitmap? result = null;
        try
        {
            var library = DockLayoutState.AssetLibrary;
            var item = library?.Assets.FirstOrDefault(a => a.Id == assetId);
            if (item != null && !string.IsNullOrEmpty(item.FilePath))
            {
                var path = item.FilePath;
                if (!Path.IsPathRooted(path) && !string.IsNullOrEmpty(DockLayoutState.ProjectDirectory))
                    path = Path.Combine(DockLayoutState.ProjectDirectory, path);
                if (File.Exists(path))
                    result = new Bitmap(path);
            }
        }
        catch
        {
            result = null;
        }

        _imageCache[assetId] = result;
        return result;
    }

    private static void DrawCenteredText(DrawingContext context, string text, Rect rect,
        double fontSize, Color color)
        => DrawAlignedText(context, text, rect, fontSize, color, TextAlignMode.Center, TextVAlignMode.Middle);

    /// <summary>
    /// 9 宫格对齐绘制文本：按水平/垂直对齐计算文本锚点。
    /// 边缘对齐保留 4px 内边距，避免贴边。
    ///
    /// <para><b>为什么要重试一次：</b>缓存里的 <see cref="FormattedText"/> 会因平台侧
    /// 文本对象被释放而失效（渲染后端重建、字体服务重载、headless 会话结束），
    /// 此后读 <c>Width</c> 或绘制会抛 <c>ObjectDisposedException</c>。原先这里是一个
    /// 裸的 <c>catch { }</c> —— 一次失效就让这段文本**永久**画不出来，而且现场零线索。
    /// 现在改成「捕获 → 丢弃整批文本缓存 → 重建重试一次」。</para>
    /// </summary>
    private static void DrawAlignedText(DrawingContext context, string text, Rect rect,
        double fontSize, Color color, TextAlignMode hAlign, TextVAlignMode vAlign)
    {
        var brush = RenderResourceCache.Brush(color);
        try
        {
            DrawAlignedTextCore(context, RenderResourceCache.Text(text, fontSize, brush),
                rect, hAlign, vAlign);
        }
        catch
        {
            RenderResourceCache.InvalidateTexts();
            try
            {
                DrawAlignedTextCore(context, RenderResourceCache.Text(text, fontSize, brush),
                    rect, hAlign, vAlign);
            }
            catch
            {
                // 二次失败说明不是缓存陈旧所致；绘制路径不能把异常抛回 UI 线程。
            }
        }
    }

    private static void DrawAlignedTextCore(DrawingContext context, FormattedText ft, Rect rect,
        TextAlignMode hAlign, TextVAlignMode vAlign)
    {
        const double pad = 4;
        double x = hAlign switch
        {
            TextAlignMode.Left => rect.X + pad,
            TextAlignMode.Right => rect.X + rect.Width - ft.Width - pad,
            _ => rect.X + (rect.Width - ft.Width) / 2
        };
        double y = vAlign switch
        {
            TextVAlignMode.Top => rect.Y + pad,
            TextVAlignMode.Bottom => rect.Y + rect.Height - ft.Height - pad,
            _ => rect.Y + (rect.Height - ft.Height) / 2
        };

        context.DrawText(ft, new Point(x, y));
    }

    // ── Wave 2 工业控件渲染器 ──

    /// <summary>
    /// 精确圆弧（Avalonia <c>ArcTo</c>）。角度走 <see cref="ArcLayout.PointAt"/>
    /// —— LVGL 约定（0° 在 3 点钟、顺时针为正），与 <c>lv_draw_arc</c> 一致。
    /// </summary>
    private static void DrawArcGeometry(DrawingContext context, IPen? pen, Rect rect,
        double startAngle, double sweepAngle)
    {
        // 几何只依赖矩形与角度，走缓存复用（原先每次 new StreamGeometry ≈ 480 B/次）
        context.DrawGeometry(null, pen, RenderResourceCache.Arc(rect, startAngle, sweepAngle, asPolyline: false));
    }

    /// <summary>
    /// 折线近似的圆弧（64 段），角度同样走 <see cref="ArcLayout.PointAt"/>。
    ///
    /// <para>与 <see cref="DrawArcGeometry"/> 是同一套角度约定，只是几何构造方式不同 ——
    /// 保留折线版是因为 headless（<c>UseHeadlessDrawing=true</c>）下 Avalonia 的
    /// <c>ArcTo</c> 是桩实现（只记端点、不算弧），折线版的 <c>Bounds</c> 才可用于断言。</para>
    /// </summary>
    private static void DrawArcPolyline(DrawingContext context, IPen? pen, Rect rect,
        double startAngle, double sweepAngle)
    {
        context.DrawGeometry(null, pen, RenderResourceCache.Arc(rect, startAngle, sweepAngle, asPolyline: true));
    }

    private void RenderGauge(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var bgArc = RenderResourceCache.Pen(DesignPalette.Surface, 12 * z);
        DrawArcGeometry(context, bgArc, rect, ArcLayout.StartAngle, ArcLayout.SweepAngle);

        // R2-T2: 预览模式下，活动弧度由模拟值驱动（归一化进度 * 270°）
        double activeSweep = ArcLayout.DefaultActiveSweep;
        var gaugeLabel = control.Text ?? Services.ControlNameDefaults.Display(control) ?? "Gauge";
        if (IsPreviewMode)
        {
            var progress = PreviewValueResolver.ResolveProgress(control, PreviewValueProvider,
                control.MinValue, control.MaxValue);
            if (progress.HasValue)
            {
                activeSweep = progress.Value * ArcLayout.SweepAngle;
                var previewText = PreviewValueResolver.ResolveDisplayText(
                    control, PreviewValueProvider, PreviewValueTypeProvider, PreviewValueUnitProvider);
                if (!string.IsNullOrEmpty(previewText)) gaugeLabel = previewText;
            }
        }
        else if (control.MaxValue > control.MinValue)
        {
            // 编辑态：按控件当前 Value 显示活动弧度
            activeSweep = Math.Clamp(
                (control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1) * ArcLayout.SweepAngle;
        }

        var activeColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Primary);
        var activeArc = RenderResourceCache.Pen(activeColor, 12 * z);
        DrawArcGeometry(context, activeArc, rect, ArcLayout.StartAngle, activeSweep);

        // 刻度线（MajorTickCount 主刻度，沿 270° 弧均布；刻度色用 TextColor 兜底灰）
        // 几何算术抽在 Core 的 GaugeTickLayout（纯函数），与 ArcLayout 共享角度常量，可行为测试。
        var tickCount = GaugeTickLayout.NormalizeCount(control.MajorTickCount);
        var tickColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Slate400);
        var tickPen = RenderResourceCache.Pen(tickColor, 2 * z);
        var tickLen = 8 * z;
        var center = rect.Center;
        var radius = Math.Min(rect.Width, rect.Height) / 2;
        foreach (var tick in GaugeTickLayout.ComputeTicks(
                     center.X, center.Y, radius, tickLen, tickCount))
        {
            context.DrawLine(tickPen,
                new Point(tick.X1, tick.Y1),
                new Point(tick.X2, tick.Y2));
        }

        DrawCenteredText(context, gaugeLabel, rect, 14 * z,
            ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Text));
    }

    private void RenderKnob(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var center = rect.Center;
        var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.8;

        var outerEllipse = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        var bgPen = RenderResourceCache.Pen(DesignPalette.Surface, 6 * z);
        context.DrawEllipse(null, bgPen, outerEllipse);

        // R2-T2: 预览模式下，指针角度由模拟值驱动（135°..405°，270° 量程）
        double pointerAngle = ArcLayout.PointerDefault;
        if (IsPreviewMode)
        {
            var progress = PreviewValueResolver.ResolveProgress(control, PreviewValueProvider,
                control.MinValue, control.MaxValue);
            if (progress.HasValue)
                pointerAngle = ArcLayout.PointerStart + progress.Value * ArcLayout.SweepAngle;
        }
        else if (control.MaxValue > control.MinValue)
        {
            // 编辑态：按控件当前 Value 显示指针位置
            var staticProgress = Math.Clamp(
                (control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1);
            pointerAngle = ArcLayout.PointerStart + staticProgress * ArcLayout.SweepAngle;
        }

        // 指针方向同样走 ArcLayout.PointAt（LVGL 约定），与固件 lv_arc 指示弧起点一致
        var (px, py) = ArcLayout.PointAt(center.X, center.Y, radius * 0.7, pointerAngle);
        var pointerColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Primary);
        var pointerPen = RenderResourceCache.Pen(pointerColor, 3 * z);
        context.DrawLine(pointerPen, center, new Point(px, py));
    }

    private void RenderLed(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var center = rect.Center;
        var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.7;

        // R2-T2: 预览模式下，根据模拟值决定亮灭
        // 默认（编辑态/无绑定）：亮绿色
        bool lit = true;
        if (IsPreviewMode)
        {
            var value = PreviewValueResolver.ResolveDisplayText(control, PreviewValueProvider);
            if (value != null)
                lit = value == "ON";
        }

        // LED 颜色：取 BgColor（与生成器 lv_led_set_color 一致），未配置时默认绿色；
        // 亮灭语义：预览态按绑定值 ON/OFF，编辑态默认亮。
        var litColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Success);
        var glowColor = lit ? litColor : DesignPalette.LedOff;
        // 亮度调节：Brightness(0-255) 缩放颜色强度
        var brightness = Math.Clamp(control.Brightness, 0, 255) / 255.0;
        var coreColor = lit
            ? ScaleColor(litColor, 0.35 + 0.65 * brightness)
            : DesignPalette.Surface;

        if (lit)
        {
            var glowBrush = RenderResourceCache.Brush(glowColor, 0.3);
            context.DrawEllipse(glowBrush, null, center, radius * 1.5, radius * 1.5);
        }
        var ledBrush = RenderResourceCache.Brush(coreColor);
        context.DrawEllipse(ledBrush, null, center, radius, radius);
    }

    private void RenderSpinner(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var center = rect.Center;
        var radius = Math.Min(rect.Width, rect.Height) / 2 * 0.6;

        var arcColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.SkyAccent);
        var arcPen = RenderResourceCache.Pen(arcColor, 4 * z);
        DrawArcGeometry(context, arcPen,
            new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2),
            0, 270);

        DrawCenteredText(context, "⏳", rect, 16 * z, DesignPalette.Text);
    }

    private void RenderLineShape(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var lineColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.LineFallback);
        var cap = control.LineCap switch
        {
            "Round" => PenLineCap.Round,
            "Square" => PenLineCap.Square,
            _ => PenLineCap.Flat
        };
        var pen = RenderResourceCache.Pen(lineColor, Math.Max(1, (control.BorderWidth > 0 ? control.BorderWidth : 2) * z), cap);
        // 点击式绘制：Line 的 Points 保存了两端点（局部坐标），按点连线；
        // 无 Points 时回退为对角线（与旧拖拽语义一致）。
        if (control.Points is { Count: >= 2 })
        {
            var pts = ResolvePolylinePoints(control, rect);
            context.DrawLine(pen, pts[0], pts[1]);
        }
        else
        {
            context.DrawLine(pen, rect.TopLeft, rect.BottomRight);
        }
    }

    // ── I10: 基础绘图图形渲染 ──

    /// <summary>
    /// 矩形：填充（BgColor）+ 边框（BorderWidth/BorderColor）+ 圆角（Radius）。
    /// 语义与通用样式块（CodeGen lv_obj bg/radius/border）及生成器一致。
    /// </summary>
    private void RenderRectangleShape(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var radius = (float)(control.Radius * z);
        if (!string.IsNullOrEmpty(control.BgColor))
        {
            context.FillRectangle(ParseColor(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Surface),
                rect, radius);
        }
        if (control.BorderWidth > 0)
        {
            var borderBrush = ParseColor(control.BorderColor, DesignPalette.Border);
            context.DrawRectangle(RenderResourceCache.Pen(borderBrush, control.BorderWidth * z), rect, radius);
        }
    }

    /// <summary>
    /// 椭圆/圆：填充（BgColor）+ 边框（BorderWidth/BorderColor）。
    /// 图形内接于控件矩形 —— 与 LVGL canvas 椭圆绘制（采样多边形）视觉一致。
    /// </summary>
    private void RenderEllipseShape(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        IBrush? fill = string.IsNullOrEmpty(control.BgColor)
            ? null
            : ParseColor(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Surface);
        IPen? pen = control.BorderWidth > 0
            ? RenderResourceCache.Pen(ParseColor(control.BorderColor, DesignPalette.Border), control.BorderWidth * z)
            : null;
        context.DrawEllipse(fill, pen, rect);
    }

    /// <summary>
    /// 折线：按 Points（控件局部坐标）映射到控件矩形逐段连线；
    /// 未配置 Points 时回退为一条对角线（与 Line 语义一致）。
    /// 线色取 BgColor 兜底 LineFallback，线宽取 BorderWidth 兜底 2。
    /// </summary>
    private void RenderPolylineShape(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var lineColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.LineFallback);
        var cap = control.LineCap switch
        {
            "Round" => PenLineCap.Round,
            "Square" => PenLineCap.Square,
            _ => PenLineCap.Flat
        };
        var pen = RenderResourceCache.Pen(lineColor, Math.Max(1, (control.BorderWidth > 0 ? control.BorderWidth : 2) * z), cap);

        var pts = ResolvePolylinePoints(control, rect);
        if (pts.Count < 2) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(pts[0], false);
            for (var i = 1; i < pts.Count; i++)
                ctx.LineTo(pts[i]);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    /// 解析折线顶点：控件局部坐标 → 画布坐标（缩放 + 平移）。
    /// 顶点按控件尺寸归一化映射，缩放控件时折线随形变。
    /// </summary>
    private static List<Point> ResolvePolylinePoints(HmiControl control, Rect rect)
    {
        var result = new List<Point>();
        if (control.Points is { Count: >= 2 })
        {
            foreach (var p in control.Points)
            {
                result.Add(new Point(
                    rect.X + Math.Clamp(p.X, 0, control.Width) / Math.Max(1, control.Width) * rect.Width,
                    rect.Y + Math.Clamp(p.Y, 0, control.Height) / Math.Max(1, control.Height) * rect.Height));
            }
        }
        else
        {
            // 回退：左上 → 右下对角线
            result.Add(rect.TopLeft);
            result.Add(rect.BottomRight);
        }
        return result;
    }

    /// <summary>
    /// 圆弧：轨道弧（BgColor 兜底 Surface）+ 指示弧（IndicatorColor ?? TextColor 兜底主色）。
    /// 角度范围 MinValue..MaxValue（默认 0..360），指示弧扫角 = Value - MinValue。
    /// 角度遵循 LVGL 约定（0°=3 点钟、<b>顺时针为正</b>），与生成器的
    /// <c>lv_arc_set_rotation(0) + lv_arc_set_bg_angles(MinValue, MaxValue)</c> 同源。
    /// 弧宽取 BorderWidth 兜底 ArcLayout.ArcWidthDefault。
    /// </summary>
    private void RenderArcShape(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var arcW = Math.Max(2, (control.BorderWidth > 0 ? control.BorderWidth : ArcLayout.ArcWidthDefault) * z);
        var start = control.MinValue;
        var sweep = Math.Max(0, control.MaxValue - control.MinValue);

        // 轨道弧
        var trackColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Surface);
        var trackPen = RenderResourceCache.Pen(trackColor, arcW, PenLineCap.Round);
        DrawArcPolyline(context, trackPen, rect, start, sweep);

        // 指示弧（Value 相对 MinValue 的进度）
        if (control.MaxValue > control.MinValue)
        {
            var valueSweep = Math.Clamp(
                (control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1) * sweep;
            if (valueSweep > 0.5)
            {
                var activeColor = ParseColorHex(control.IndicatorColor ?? EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Primary);
                var activePen = RenderResourceCache.Pen(activeColor, arcW, PenLineCap.Round);
                DrawArcPolyline(context, activePen, rect, start, valueSweep);
            }
        }
    }

    private void RenderKeyboardPlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // 键盘主体背景（与 lv_keyboard 默认主题深色主体一致）
        var bgColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.CheckBoxOff);
        context.FillRectangle(RenderResourceCache.Brush(bgColor), rect, (float)(4 * z));

        var keyBrush = RenderResourceCache.Brush(DesignPalette.KeyboardKey);
        var keyTextColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Text);
        var modBrush = RenderResourceCache.Brush(ParseColorHex(control.IndicatorColor, DesignPalette.OffKnob));
        var fontSz = 9 * z;

        // QWERTY 布局：数字行 + 三行字母 + 底行（Shift/空格/退格），与 lv_keyboard 结构一致
        string[] rows =
        {
            "1234567890",
            "QWERTYUIOP",
            "ASDFGHJKL",
            "ZXCVBNM",
        };
        var gap = 2 * z;
        var nRows = rows.Length + 1; // +底行
        var keyH = (rect.Height - gap * (nRows + 1)) / nRows;
        var y = rect.Y + gap;

        for (var r = 0; r < rows.Length; r++)
        {
            var rowKeys = rows[r];
            var keyW = (rect.Width - gap * (rowKeys.Length + 1)) / rowKeys.Length;
            var x = rect.X + gap;
            foreach (var ch in rowKeys)
            {
                var keyRect = new Rect(x, y, keyW, keyH);
                context.FillRectangle(keyBrush, keyRect, (float)(2 * z));
                try
                {
                    var ft = RenderResourceCache.Text(ch.ToString(), fontSz, RenderResourceCache.Brush(keyTextColor));
                    context.DrawText(ft, new Point(
                        keyRect.X + (keyRect.Width - ft.Width) / 2,
                        keyRect.Y + (keyRect.Height - ft.Height) / 2));
                }
                catch { }
                x += keyW + gap;
            }
            y += keyH + gap;
        }

        // 底行：Shift | 空格 | 退格（与 lv_keyboard 底部功能键布局一致）
        var bottomH = keyH;
        var spaceW = rect.Width * 0.5;
        var sideW = (rect.Width - spaceW - gap * 4) / 2;
        var bottomY = y;
        var shiftRect = new Rect(rect.X + gap, bottomY, sideW, bottomH);
        context.FillRectangle(modBrush, shiftRect, (float)(2 * z));
        var bsRect = new Rect(rect.Right - gap - sideW, bottomY, sideW, bottomH);
        context.FillRectangle(modBrush, bsRect, (float)(2 * z));
        var spaceRect = new Rect(shiftRect.Right + gap, bottomY, spaceW, bottomH);
        context.FillRectangle(keyBrush, spaceRect, (float)(2 * z));

        DrawCenteredText(context, "⇧", shiftRect, fontSz, keyTextColor);
        DrawCenteredText(context, "␣", spaceRect, fontSz, keyTextColor);
        DrawCenteredText(context, "⌫", bsRect, fontSz, keyTextColor);
    }

    private void RenderTabViewPlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var headerH = 28 * z;
        var bg = RenderResourceCache.Brush(DesignPalette.TabBg);
        context.FillRectangle(bg, rect, 4);

        // 页签标题（逗号分隔），无配置时回退 "Tab1"
        var titles = (control.TabTitles ?? "").Split(',', StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0).ToList();
        if (titles.Count == 0) titles.Add(DefaultTexts.TabFallback);

        var tabW = Math.Max(60 * z, rect.Width / Math.Min(titles.Count, 8));
        var active = Math.Clamp(control.SelectedIndex, 0, titles.Count - 1);
        for (var i = 0; i < titles.Count; i++)
        {
            var tabRect = new Rect(rect.X + i * tabW, rect.Y, tabW, headerH);
            var tabBrush = i == active
                ? RenderResourceCache.Brush(DesignPalette.Primary)
                : RenderResourceCache.Brush(DesignPalette.CheckBoxOff);
            context.FillRectangle(tabBrush, tabRect, i == active ? 4 : 0);
            DrawCenteredText(context, titles[i], tabRect, 11 * z,
                ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Text));
        }

        var contentBrush = RenderResourceCache.Brush(DesignPalette.TabContent);
        context.FillRectangle(contentBrush,
            new Rect(rect.X, rect.Y + headerH, rect.Width, rect.Height - headerH));
    }

    private void RenderTablePlaceholder(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // 背景：消费 control.BgColor（模拟器表格主体背景 = 用户 BgColor，LVGL 无主题时
        // 单元格透明、显示主体背景）；未设置时用中性深色近似（与 Dropdown/TextArea 惯例一致）。
        var bgColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor), DesignPalette.Slate800);
        var gridBrush = RenderResourceCache.Brush(bgColor);
        context.FillRectangle(gridBrush, rect, 4);

        var cols = Math.Max(1, control.ColumnCount);
        var rows = Math.Max(1, control.RowCount);
        var textColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor), DesignPalette.Text);

        // ── 收集每列文本（表头 + 单元格），供 auto 列内容估算 ──
        var colTexts = new List<string>[cols];
        for (var i = 0; i < cols; i++) colTexts[i] = new List<string>();
        if (!string.IsNullOrEmpty(control.HeaderTexts))
        {
            var headers = control.HeaderTexts.Split(',', StringSplitOptions.TrimEntries);
            for (var i = 0; i < headers.Length && i < cols; i++)
                colTexts[i].Add(headers[i]);
        }
        if (control.Cells != null)
        {
            foreach (var cell in control.Cells)
            {
                if (cell.Row < 0 || cell.Col < 0 || cell.Col >= cols) continue;
                var txt = ResolveTableCellText(cell);
                if (!string.IsNullOrEmpty(txt)) colTexts[cell.Col].Add(txt);
            }
        }

        // ── 计算每列像素宽度：均分填满（默认）/ 指定 px / auto 内容自适应 ──
        var widths = TableLayoutHelper.ParseColumnWidths(control.ColumnWidths, cols);
        var colW = new double[cols];
        double fixedSum = 0; var fillCount = 0;
        for (var i = 0; i < cols; i++)
        {
            if (widths[i] == TableLayoutHelper.Auto)
                colW[i] = EstimateColumnWidth(colTexts[i], 11 * z);
            else if (widths[i] > 0)
            {
                colW[i] = widths[i] * z;
                fixedSum += colW[i];
            }
            else fillCount++;
        }
        double fillEach = fillCount > 0
            ? Math.Max(1, (rect.Width - fixedSum - colW.Where((w, i) => widths[i] == TableLayoutHelper.Auto).Sum()) / fillCount)
            : 0;
        for (var i = 0; i < cols; i++)
            if (widths[i] == TableLayoutHelper.Fill) colW[i] = fillEach;

        // ── 行高：固定行高或均分控件高度 ──
        var rowH = control.RowHeight > 0 ? control.RowHeight * z : rect.Height / rows;

        // 网格线：颜色与生成代码 LV_PART_ITEMS 边框同源（BorderColor 或默认深灰蓝）
        var gridColor = ParseColorHex(control.BorderColor, DesignPalette.Border);
        var linePen = RenderResourceCache.Pen(gridColor, 0.5 * z);

        // 累积列偏移
        double xCursor = rect.X;
        var colX = new double[cols];
        for (var c = 0; c < cols; c++) { colX[c] = xCursor; xCursor += colW[c]; }

        for (int c = 1; c < cols; c++)
            context.DrawLine(linePen,
                new Point(colX[c], rect.Y),
                new Point(colX[c], rect.Y + rect.Height));

        for (int r = 1; r < rows; r++)
            context.DrawLine(linePen,
                new Point(rect.X, rect.Y + r * rowH),
                new Point(rect.X + rect.Width, rect.Y + r * rowH));

        // ── 表头（第 0 行）──
        if (!string.IsNullOrEmpty(control.HeaderTexts))
        {
            var headers = control.HeaderTexts.Split(',', StringSplitOptions.TrimEntries);
            for (var i = 0; i < headers.Length && i < cols; i++)
            {
                var cellRect = new Rect(colX[i], rect.Y, colW[i], rowH);
                DrawAlignedText(context, headers[i], cellRect, 11 * z, textColor, TextAlignMode.Center, TextVAlignMode.Middle);
            }
        }

        // ── 单元格内容（静态文本 / 绑定变量预览值）──
        if (control.Cells != null)
        {
            foreach (var cell in control.Cells)
            {
                if (cell.Row < 0 || cell.Col < 0 || cell.Row >= rows || cell.Col >= cols) continue;
                var txt = ResolveTableCellText(cell);
                if (string.IsNullOrEmpty(txt)) continue;
                var cellRect = new Rect(colX[cell.Col], rect.Y + cell.Row * rowH, colW[cell.Col], rowH);
                DrawAlignedText(context, txt, cellRect, 11 * z, textColor, TextAlignMode.Center, TextVAlignMode.Middle);
            }
        }
    }

    /// <summary>解析表格单元格的显示文本：静态文本，或绑定变量在预览模式下的模拟值。</summary>
    private string? ResolveTableCellText(TableCellData cell)
    {
        if (cell.IsBound)
        {
            var val = PreviewValueProvider?.Invoke(cell.VariableId!);
            var body = val.HasValue
                ? val.Value.ToString("F" + Math.Clamp(cell.DecimalPlaces, 0, BindingFormat.MaxDecimals))
                : $"{{{cell.VariableId}}}";   // 无模拟值（编辑态/未提供）→ 显示变量占位
            return $"{cell.Prefix}{body}{cell.Suffix}";
        }
        return cell.Text;
    }

    /// <summary>估算某列内容所需宽度（取最长文本测量宽 + 内边距）。</summary>
    private static double EstimateColumnWidth(IEnumerable<string> texts, double fontSize)
    {
        double max = 0;
        foreach (var t in texts)
        {
            if (string.IsNullOrEmpty(t)) continue;
            try
            {
                var ft = RenderResourceCache.Text(t, fontSize, Brushes.White);
                if (ft.Width > max) max = ft.Width;
            }
            catch { }
        }
        return max > 0 ? max + 16 : fontSize * 3;   // 兜底：无内容时给一个最小列宽
    }

    /// <summary>按比例缩放 RGB 颜色强度（0..1）。</summary>
    private static Color ScaleColor(Color c, double t)
        => Color.FromRgb((byte)(c.R * t), (byte)(c.G * t), (byte)(c.B * t));

    /// <summary>颜色亮度是否偏亮（用于挑选高对比前景色）。</summary>
    private static bool IsLightColor(Color c)
        => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 140;

    /// <summary>
    /// 在指针圆形上绘制指向刻度位置的小三角形。
    /// 刻度线位于轨道上边缘外侧（图形之外），因此三角形位于圆内上部、
    /// 尖端朝上指向外部刻度；填充色与圆形底色亮度互补（高对比）：
    /// 圆形偏亮 → 深色三角形，圆形偏暗 → 亮色三角形，另加反向描边保证任意底色可见。
    /// </summary>
    private static void DrawPointerTriangle(DrawingContext context, Point center, double radius,
        Color fillColor, double zoom)
    {
        if (radius <= 2 * zoom) return;   // 圆太小不画
        var light = IsLightColor(fillColor);
        var triFill = light ? DesignPalette.Slate900 : DesignPalette.Text;
        var triStroke = light ? DesignPalette.Text : DesignPalette.Slate900;
        var w = radius * 0.5;
        var tipY = center.Y - radius * 0.88;             // 尖端朝上（指向外部刻度）
        var baseY = center.Y - radius * 0.18;            // 底边在圆内下部
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(center.X, tipY), false);          // 尖端
            ctx.LineTo(new Point(center.X - w, baseY), true);
            ctx.LineTo(new Point(center.X + w, baseY), true);
            ctx.EndFigure(true);
        }
        context.DrawGeometry(RenderResourceCache.Brush(triFill),
            RenderResourceCache.Pen(triStroke, Math.Max(0.75, 1 * zoom)), g);
    }
}
