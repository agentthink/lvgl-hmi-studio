using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 画布渲染性能基线：量化 <see cref="DesignCanvas.Render"/> 的**每次调用托管堆分配**与耗时。
///
/// <para><b>为什么需要单独一套测法：</b><c>UseHeadlessDrawing = true</c> 的 headless 平台
/// <b>不做合成遍历</b>，<c>InvalidateVisual()</c> + <c>ForceRenderTimerTick()</c> 不会让
/// <see cref="DesignCanvas.Render"/> 被调用一次（实测调用次数恒为 0）。因此这里改为
/// 自行创建 <see cref="RenderTargetBitmap"/> 取一个真实 <see cref="DrawingContext"/>，
/// 直接驱动 <c>canvas.Render(dc)</c> —— 这条路径不依赖布局通道，
/// 因为渲染用的矩形完全由模型坐标 × 缩放算出（见 <c>RenderControl</c>）。</para>
///
/// <para><b>测量口径：</b>用 <see cref="GC.GetAllocatedBytesForCurrentThread"/> 取精确分配字节数
/// （不是 GC 后的存活量），扣除「0 控件」场景的框架底噪，再除以迭代数与控件数，
/// 得到「每个控件每次渲染的分配字节」。分配字节数在 .NET 里是确定性的，
/// 不受 GC 时机影响，因此可以稳定比较改动前后。</para>
/// </summary>
public class CanvasRenderBaselineTests
{
    private const int WarmupIterations = 50;
    private const int MeasureIterations = 200;

    /// <summary>每种控件类型在「单类型归因」场景里放多少个（用于摊薄固定开销）。</summary>
    private const int PerTypeRepeat = 20;

    private readonly ITestOutputHelper _out;

    public CanvasRenderBaselineTests(ITestOutputHelper o) => _out = o;

    private sealed record Cost(double BytesPerRender, double BytesPerControl, double MsPerRender);

    private static readonly HmiControlType[] AllTypes =
    {
        HmiControlType.Button, HmiControlType.Label, HmiControlType.Slider,
        HmiControlType.Switch, HmiControlType.CheckBox, HmiControlType.Image,
        HmiControlType.Bar, HmiControlType.Dropdown, HmiControlType.TextArea,
        HmiControlType.Gauge, HmiControlType.Knob, HmiControlType.Keyboard,
        HmiControlType.Led, HmiControlType.Spinner, HmiControlType.TabView,
        HmiControlType.Line, HmiControlType.Table, HmiControlType.Svg,
        HmiControlType.Tank, HmiControlType.Pipe, HmiControlType.Valve,
        HmiControlType.Rectangle, HmiControlType.Ellipse, HmiControlType.Polyline,
        HmiControlType.Arc,
    };

    private static HmiControl MakeControl(HmiControlType type, int n) => new()
    {
        Id = $"c{n}",
        Name = $"c{n}",
        Type = type,
        Text = "标签",
        X = 20 + (n % 8) * 120,
        Y = 20 + (n / 8) * 70,
        Width = 100,
        Height = 60,
        MinValue = 0,
        MaxValue = 100,
        Value = 50,
        BgColor = "#0EA5E9",
        IndicatorColor = "#22C55E",
        TextColor = "#111827",
        BorderWidth = 2,
        TankShowScale = true,
        TankTickCount = 5,
        PipeDirection = "Horizontal",
        PipeFlowDirection = "Forward",
        ValveType = "Ball",
    };

    private static HmiScreen BuildScreen(IEnumerable<HmiControlType> types)
    {
        var screen = new HmiScreen { Name = "Baseline", Width = 1024, Height = 600 };
        var n = 0;
        foreach (var t in types)
            screen.Controls.Add(MakeControl(t, n++));
        return screen;
    }

    /// <summary>
    /// 量一个屏幕的渲染成本。返回每帧平均分配字节、每帧耗时。
    /// 注意：这里刻意**不**把画布挂进窗口 —— <c>Render</c> 只依赖模型坐标与缩放，
    /// 挂窗口反而会引入无关的布局/合成开销。
    /// </summary>
    private static Cost Measure(HmiScreen screen)
    {
        var canvas = new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };

        using var rtb = new RenderTargetBitmap(new PixelSize(1024, 600));
        using var dc = rtb.CreateDrawingContext();

        for (var i = 0; i < WarmupIterations; i++)
            canvas.Render(dc);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < MeasureIterations; i++)
            canvas.Render(dc);
        sw.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return new Cost(
            allocated / (double)MeasureIterations,
            0,
            sw.Elapsed.TotalMilliseconds / MeasureIterations);
    }

    /// <summary>框架底噪：0 控件屏幕的每帧分配。</summary>
    private static Cost MeasureFrameworkFloor() => Measure(BuildScreen(Array.Empty<HmiControlType>()));

    [Fact]
    public void Baseline_UnitCost_OfRenderPrimitives()
    {
        UiTestHost.Run(() =>
        {
            const int N = 2000;
            var color = Color.Parse("#0EA5E9");
            var brush = new SolidColorBrush(color);

            _out.WriteLine("| 基元 | 每次分配 (B) |");
            _out.WriteLine("|---|---|");
            _out.WriteLine($"| `new SolidColorBrush(Color)` | {PerCall(N, () => _ = new SolidColorBrush(color)):N0} |");
            _out.WriteLine($"| `new Pen(brush, 1.0)` | {PerCall(N, () => _ = new Pen(brush, 1.0)):N0} |");
            _out.WriteLine($"| `Color.Parse(\"#0EA5E9\")` | {PerCall(N, () => _ = Color.Parse("#0EA5E9")):N0} |");
            _out.WriteLine($"| `new FormattedText(\"标签\")` | {PerCall(N, () => _ = new FormattedText("标签", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 14, brush)):N0} |");
            _out.WriteLine($"| `new FormattedText(\"Q\")` | {PerCall(N, () => _ = new FormattedText("Q", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 9, brush)):N0} |");
            _out.WriteLine($"| `new StreamGeometry()` | {PerCall(N, () => _ = new StreamGeometry()):N0} |");
            _out.WriteLine($"| `StreamGeometry`+Open+Dispose | {PerCall(N, () =>
            {
                var g = new StreamGeometry();
                using var c = g.Open();
                c.BeginFigure(new Point(0, 0), false);
                c.LineTo(new Point(10, 10));
                c.EndFigure(false);
            }):N0} |");

            _out.WriteLine("");
            _out.WriteLine("参照：全类型屏（25 控件）实测 418,133 B/帧；Keyboard 单控件 225,240 B/帧，");
            _out.WriteLine("其中每帧新建 36 个 FormattedText + 36 个 SolidColorBrush（见 RenderKeyboardPlaceholder）。");
        });
    }

    /// <summary>量单次调用的托管分配字节数（先热身，再取精确计数器差值）。</summary>
    private static double PerCall(int iterations, Action body)
    {
        // 阶段 120：这几个 "Baseline_*" 用例**只把读数打印给文档用**（真正的判据是同文件的
        // `Gate_CanvasRenderAllocation_StaysWithinBudget`），于是它们此前**一条断言都没有** ——
        // 测量机构坏掉时会在文档里静默写出 NaN/Inf 之类的假数字。
        // 这里给测量机构本身加最小信号：迭代数必须为正、读数必须有限。
        if (iterations <= 0)
            throw new InvalidOperationException("iterations 必须 > 0 —— 否则读数恒为 0，基线表是假的");

        for (var i = 0; i < 200; i++) body();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) body();
        var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
        if (!double.IsFinite(perCall))
            throw new InvalidOperationException($"每调用分配量不是有限值: {perCall}");
        return perCall;
    }

    /// <summary>
    /// 拆分「我们自己 new 的对象」与「DrawingContext 调用本身」的分配占比。
    /// 这一步是基线可信度的关键：如果 <c>context.Xxx()</c> 的框架开销占大头，
    /// 那么「每控件 9 KB」就不是我们能优化的部分，方案必须据此调整。
    /// </summary>
    [Fact]
    public void Baseline_UnitCost_OfDrawingContextOps()
    {
        UiTestHost.Run(() =>
        {
            const int N = 2000;
            using var rtb = new RenderTargetBitmap(new PixelSize(64, 64));
            using var dc = rtb.CreateDrawingContext();

            var color = Color.Parse("#0EA5E9");
            var brush = new SolidColorBrush(color);
            var pen = new Pen(brush, 1.0);
            var rect = new Rect(0, 0, 100, 60);
            var ft = new FormattedText("标签", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 14, brush);
            var geom = new StreamGeometry();
            using (var gc = geom.Open())
            {
                gc.BeginFigure(new Point(0, 0), false);
                gc.LineTo(new Point(10, 10));
                gc.EndFigure(false);
            }

            _out.WriteLine("| DrawingContext 操作 | 每次分配 (B) |");
            _out.WriteLine("|---|---|");
            _out.WriteLine($"| `FillRectangle(brush,rect,4f)` | {PerCall(N, () => dc.FillRectangle(brush, rect, 4f)):N0} |");
            _out.WriteLine($"| `DrawRectangle(pen,rect)` | {PerCall(N, () => dc.DrawRectangle(pen, rect)):N0} |");
            _out.WriteLine($"| `DrawLine(pen,p1,p2)` | {PerCall(N, () => dc.DrawLine(pen, new Point(0, 0), new Point(10, 10))):N0} |");
            _out.WriteLine($"| `DrawText(ft,p)` | {PerCall(N, () => dc.DrawText(ft, new Point(0, 0))):N0} |");
            _out.WriteLine($"| `DrawGeometry(null,pen,geom)` | {PerCall(N, () => dc.DrawGeometry(null, pen, geom)):N0} |");
            _out.WriteLine($"| `PushTransform(m)`+Dispose | {PerCall(N, () =>
            {
                using var _ = dc.PushTransform(Matrix.Identity);
            }):N0} |");
            _out.WriteLine($"| `PushOpacity(0.5)`+Dispose | {PerCall(N, () =>
            {
                using var _ = dc.PushOpacity(0.5);
            }):N0} |");

            // ── 环境探针：框架 `DrawText` 的单次成本是**环境相关**的（2026-09-21 实测）。
            //
            // 起因：`Gate_CanvasRenderAllocation_StaysWithinBudget` 在「本类单独跑」下
            // Slider/TabView/Gauge/Tank 分别为 3,280 / 3,635 / 3,004 / 8,807，
            // 而在 `App.UiTests` 整队同进程跑时变成 4,149 / 6,022 / 4,075 / 13,545
            // —— 非文本类型逐字节不变，文本类型一律上抬。逐层排除后定位到框架：
            // 同一次 `dc.DrawText(ft,p)`，「单类」= 2,604 B、「整队同进程」= 3,465~3,856 B，
            // 且**与文本内容无关**（ASCII 与 CJK 同样上抬，长文本按字形数线性放大：
            // 斜率 416 → 932 B/字形）。`new FormattedText` 两处都是 384；
            // `FillRectangle`/`DrawRectangle`/`DrawLine`/`DrawGeometry`/`Push*` 两处都是 0。
            //
            // 这组读数就是识别「当前进程是否处在抬升态」的探针：把两次运行的同名表格对齐看。
            // 成因尚未定位到具体字段（已排除：隔离级别、缓存污染、集合并行；
            // 「测试平台未按生产入口指定 FontManagerOptions.DefaultFamilyName」只解释其中约三成）。
            // 详见 docs/project-status.md 阶段 57 与登记表 #47b。
            _out.WriteLine("");
            _out.WriteLine("| 环境探针：文本内容 | `new FormattedText` | `DrawText`(已布局) | `Width`读一次 |");
            _out.WriteLine("|---|---|---|---|");
            foreach (var (tag, s) in new[]
                     {
                         ("CJK(标签)", "标签"),
                         ("ASCII(AB)", "AB"),
                         ("ASCII10(QWERTYUIOP)", "QWERTYUIOP"),
                     })
            {
                var f2 = new FormattedText(s, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 14, brush);
                _ = f2.Width;
                _ = f2.Height;
                var newCost = PerCall(N, () => _ = new FormattedText(s,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 14, brush));
                var drawCost = PerCall(N, () => dc.DrawText(f2, new Point(0, 0)));
                var widthCost = PerCall(N, () => _ = f2.Width);
                _out.WriteLine($"| {tag} | {newCost:N0} | {drawCost:N0} | {widthCost:N0} |");
            }

            // ── 环境探针（阶段 58 补）：`Typeface.Default` 到底解析到了什么。
            //    这是**本文件所有文本读数能否跨环境比较**的前提，所以留着而不是删掉。
            //    判定性问题：抬升态是不是「`Typeface.Default` 解析到了不同的字体」？
            //    若是，则**显式**指定一个字体族时，两个环境应当给出同样的读数。
            var explicitTypeface = new Typeface(new FontFamily("Noto Sans CJK SC"));
            var fe = new FormattedText("标签", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, explicitTypeface, 14, brush);
            _ = fe.Width;
            _ = fe.Height;
            _out.WriteLine($"| 显式族 Noto Sans CJK SC | 384 | {PerCall(N, () => dc.DrawText(fe, new Point(0, 0))):N0} | {PerCall(N, () => _ = fe.Width):N0} |");

            var fm = FontManager.Current;
            _out.WriteLine("");
            _out.WriteLine($"诊断：DefaultFontFamily=`{fm.DefaultFontFamily}`" +
                           $"，Typeface.Default.FontFamily=`{Typeface.Default.FontFamily}`" +
                           $"，culture={System.Globalization.CultureInfo.CurrentCulture.Name}" +
                           $"，uiCulture={System.Globalization.CultureInfo.CurrentUICulture.Name}");
            if (fm.TryGetGlyphTypeface(Typeface.Default, out var gt))
            {
                var hasCjk = gt.CharacterToGlyphMap.TryGetGlyph('标', out var gCjk);
                var hasAscii = gt.CharacterToGlyphMap.TryGetGlyph('A', out var gAscii);
                _out.WriteLine($"诊断：Typeface.Default → family=`{gt.FamilyName}`" +
                               $"，glyphCount={gt.GlyphCount}" +
                               $"，'标'={hasCjk}({gCjk})，'A'={hasAscii}({gAscii})");
            }
            else
            {
                _out.WriteLine("诊断：Typeface.Default 解析失败（TryGetGlyphTypeface 返回 false）");
            }

            // ⚠ 阶段 58 实测（反编译 + 单变量实验，2026-09-21）：headless 的 `FontManager` 由
            //    `HeadlessFontManagerStub` 支撑，它的 `TryCreateGlyphTypeface` **完全不看 `familyName`**，
            //    一律加载 `resm:Avalonia.Headless.BareMinimum.ttf`，却返回 `true`（「未命中却算命中」）。
            //    ⇒ ① 测试里所有文本都按 4 字形 `BareMinimum`（`'标'`/`'A'` 都是 `.notdef`）绘制；
            //       ② **每请求一个本进程没见过的族名，文本绘制成本就永久 +416 B/次**，
            //          与画的是哪段文本无关（见本文件末的 `Precondition_PerDispatchPlatformReset_*`）。
            //    ⇒ 本文件的绝对字节判据**只在「每次派发重建平台」时才有意义**（PerTest 隔离）。
        });
    }

    /// <summary>
    /// 单变量 A/B：把 Label 的每帧分配按「背景 / 边框 / 文本」逐项拆开，
    /// 避免靠读代码猜测成本归属。
    /// </summary>
    [Fact]
    public void Baseline_LabelCost_Decomposed()
    {
        UiTestHost.Run(() =>
        {
            const int Reps = 20;
            var floor = MeasureFrameworkFloor().BytesPerRender;

            HmiScreen Screen(Action<HmiControl> configure)
            {
                var s = new HmiScreen { Name = "AB", Width = 1024, Height = 600 };
                for (var i = 0; i < Reps; i++)
                {
                    var c = MakeControl(HmiControlType.Label, i);
                    c.BgColor = null;
                    c.BorderWidth = 0;
                    c.Text = null;
                    configure(c);
                    s.Controls.Add(c);
                }
                return s;
            }

            double PerControl(Action<HmiControl> configure) =>
                (Measure(Screen(configure)).BytesPerRender - floor) / Reps;

            var bare = PerControl(_ => { });
            var withBg = PerControl(c => c.BgColor = "#0EA5E9");
            var withBorder = PerControl(c => c.BorderWidth = 2);
            var withText = PerControl(c => c.Text = "标签");
            var all = PerControl(c =>
            {
                c.BgColor = "#0EA5E9";
                c.BorderWidth = 2;
                c.Text = "标签";
            });

            _out.WriteLine("| Label 场景（20 个/帧，已扣底噪） | B/控件/帧 | 增量 |");
            _out.WriteLine("|---|---|---|");
            _out.WriteLine($"| 裸控件 | {bare:N0} | — |");
            _out.WriteLine($"| + BgColor | {withBg:N0} | {withBg - bare:N0} |");
            _out.WriteLine($"| + BorderWidth=2 | {withBorder:N0} | {withBorder - bare:N0} |");
            _out.WriteLine($"| + Text | {withText:N0} | {withText - bare:N0} |");
            _out.WriteLine($"| 全开（实测普查值） | {all:N0} | {all - bare:N0} |");

            _out.WriteLine("");
            _out.WriteLine($"预算核对：brush(616)+pen(1264)+bgBrush(616)+FT(384)+DrawText(2756) = {616 + 1264 + 616 + 384 + 2756:N0} B");
            _out.WriteLine($"实测全开 = {all:N0} B，差额（未归因）= {all - (616 + 1264 + 616 + 384 + 2756):N0} B");
        });
    }

    /// <summary>
    /// 定位「裸控件 7,106 B/帧」的大头：FormattedText 的**惰性文本布局**。
    /// <c>FormattedText.Width/Height</c> 会触发布局构建，这一步的分配量决定了
    /// 「缓存 FormattedText」能省多少。
    /// </summary>
    [Fact]
    public void Baseline_UnitCost_OfFormattedTextLayout()
    {
        UiTestHost.Run(() =>
        {
            const int N = 2000;
            var brush = new SolidColorBrush(Colors.White);
            var culture = System.Globalization.CultureInfo.CurrentCulture;

            FormattedText New() => new("标签", culture, FlowDirection.LeftToRight,
                Typeface.Default, 14, brush);

            _out.WriteLine("| FormattedText 相关操作 | 每次分配 (B) |");
            _out.WriteLine("|---|---|");
            _out.WriteLine($"| `new FormattedText(...)`（构造） | {PerCall(N, () => _ = New()):N0} |");
            _out.WriteLine($"| 同上 + 读 `.Width` | {PerCall(N, () =>
            {
                var ft = New();
                _ = ft.Width;
            }):N0} |");
            _out.WriteLine($"| 同上 + 读 `.Width`+`.Height` | {PerCall(N, () =>
            {
                var ft = New();
                _ = ft.Width;
                _ = ft.Height;
            }):N0} |");

            var warm = New();
            _ = warm.Width;
            _ = warm.Height;
            _out.WriteLine($"| 复用同一个实例连续读 `.Width` | {PerCall(N, () => _ = warm.Width):N0} |");

            using var rtb = new RenderTargetBitmap(new PixelSize(64, 64));
            using var dc = rtb.CreateDrawingContext();
            var drawFt = New();
            _ = drawFt.Width;
            _out.WriteLine($"| 复用实例 `DrawText`（已布局） | {PerCall(N, () => dc.DrawText(drawFt, new Point(0, 0))):N0} |");
        });
    }

    /// <summary>
    /// 分类型预算例外。默认每控件 4,000 B/帧（优化后普通控件实测约 2.8 KB，
    /// 等于框架 <c>DrawText</c> 的底价 2,604 B/次，已无可再削的余量）。
    /// 这几个类型每帧要画多次文本，底价自然更高，按实测值留余量单列。
    /// </summary>
    private static readonly Dictionary<HmiControlType, double> TypeBudgets = new()
    {
        // 罐壁 + 液位 + 刻度 + 两个量程标签（2 次 DrawText ≈ 5.2 KB 框架底价）→ 实测 8.8 KB
        [HmiControlType.Tank] = 12_000,

        // 39 个键位文本（36 个字母数字 + Shift/空格/退格）→ 实测 86.7 KB
        [HmiControlType.Keyboard] = 120_000,
    };

    /// <summary>普通控件的每帧分配上限。</summary>
    private const double DefaultPerControlBudget = 4_000;

    /// <summary>25 控件全类型屏的每帧分配上限（优化后实测 134,730 B/帧）。</summary>
    private const double FullScreenBudget = 200_000;

    /// <summary>
    /// 回归门禁：画布渲染的每帧分配不得回退。
    ///
    /// <para>上限取「优化后实测值 + 余量」：2026-09-14 引入
    /// <see cref="HmiEditor.App.Controls.RenderResourceCache"/> 后，
    /// 全类型屏从 418,133 B/帧 降到 134,730 B/帧、0.574 → 0.278 ms/帧。
    /// 如果谁在渲染路径上重新引入每帧 new 画刷/画笔/文本布局，这里会立刻变红。</para>
    /// </summary>
    [Fact]
    public void Gate_CanvasRenderAllocation_StaysWithinBudget()
    {
        UiTestHost.Run(() =>
        {
            // 阶段 294：从**干净的字体表**起量 —— 否则那张按族名键的缓存会被"之前跑过哪些用例"
            // 污染（阶段 293 已定位到 `_glyphTypefaceCache`），绝对字节预算就随执行序漂
            // （阶段 56 的越线现场）。归一化之后本判据与隔离级别无关。
            HeadlessFontCache.Clear();

            var floor = MeasureFrameworkFloor();
            var over = new List<string>();
            var report = new List<string>();

            foreach (var type in AllTypes)
            {
                var screen = BuildScreen(Enumerable.Repeat(type, PerTypeRepeat));
                var perControl = (Measure(screen).BytesPerRender - floor.BytesPerRender) / PerTypeRepeat;
                var budget = TypeBudgets.TryGetValue(type, out var b) ? b : DefaultPerControlBudget;

                report.Add($"{type,-12} {perControl,9:N0} B/控件/帧  (上限 {budget:N0})");
                if (perControl > budget)
                    over.Add($"{type}: {perControl:N0} B/控件/帧 > 上限 {budget:N0}");
            }

            var fullScreen = BuildScreen(AllTypes);
            var full = Measure(fullScreen);
            report.Add($"[全类型屏 {fullScreen.Controls.Count} 控件] {full.BytesPerRender:N0} B/帧 " +
                       $"(上限 {FullScreenBudget:N0})，{full.MsPerRender:F3} ms/帧");

            if (full.BytesPerRender > FullScreenBudget)
                over.Add($"全类型屏: {full.BytesPerRender:N0} B/帧 > 上限 {FullScreenBudget:N0}");

            _out.WriteLine(string.Join(Environment.NewLine, report));

            Assert.True(over.Count == 0,
                "画布渲染分配超出预算，渲染路径上可能又出现了每帧新建画刷/画笔/文本布局："
                + Environment.NewLine + string.Join(Environment.NewLine, over));
        });
    }

    /// <summary>
    /// <b>前置条件判据</b>：本文件里那些**绝对字节**判据，只有在「每次派发都重建平台」时才可比 ——
    /// 这条把该前提本身钉住。
    ///
    /// <para><b>被守的对象：</b><c>Gate_CanvasRenderAllocation_StaysWithinBudget</c>。
    /// 它断言的是「每控件每帧分配多少字节」的绝对值，环境一变就没有意义（阶段 57 的结论）。</para>
    ///
    /// <para><b>为什么绝对字节会随环境漂（阶段 58 定论，含 IL 核实）：</b>
    /// headless 的 <c>FontManager</c> 由 <c>HeadlessFontManagerStub</c> 支撑，其
    /// <c>TryCreateGlyphTypeface(familyName, …)</c> <b>完全不看 <c>familyName</c></b>，
    /// 一律加载 <c>resm:Avalonia.Headless.BareMinimum.ttf</c>，却返回 <c>true</c>
    /// （「未命中却算命中」）。于是每请求一个**本平台实例没见过**的族名，
    /// 就在管理器里常驻一条记录，文本绘制成本**永久** +416 B/次 —— 与画的是哪段文本无关。
    /// 实测阶梯（全新进程，<c>dc.DrawText(ft, p)</c>）：干净 2,604 → 1 个新族 3,020 →
    /// 2 个 3,436 → … → 7 个 5,516；
    /// <b>负控</b>：把同一个族名重复解析 6 次，读数纹丝不动（5,516）；
    /// 只 <c>new FontFamily(…)</c> 而不解析，也不动（2,604）。
    /// ⇒ 成本是「**不同**族名的累计个数」的函数，而那是**平台实例级**的全局状态。</para>
    ///
    /// <para><b>于是本判据一次用例、三次派发，与执行顺序无关：</b>
    /// 派发① —— 量干净起点；派发② —— <b>正控</b>：解析若干个全新族名后，断言成本确实被抬高
    /// （机制若变了，后半段就不说明任何问题，必须先重新确认机制）；
    /// 派发③ —— <b>被守的不变量</b>：重置后那一次离「干净」比离「抬高」更近，即平台重建确实清了字体表。</para>
    ///
    /// <para><b>⚠ 第三条断言为什么是相对关系而不是「起点 ≤ 干净」：</b>
    /// 实测（2026-09-21，全量门禁第一次跑）：同一 `PerTest` 口径下，「干净起点」在<b>单独跑</b>
    /// 与<b>整队跑</b>之间就不同（2,604 vs 2,232），两次派出之间也会差约 42 字节。
    /// 绝对字节**本来就不能跨上下文比** —— 那正是阶段 57/58 的结论本身，
    /// 判据若写成绝对值就等于自己踩回去。所以只问本次运行自己的两个数之间的关系。</para>
    ///
    /// <para><b>负控（已实测）</b>：把 <see cref="UiTestHost"/> 的会话切成
    /// <c>AvaloniaTestIsolationLevel.PerAssembly</c>，派发③的起点停在抬高态 ⇒ 本判据红。
    /// 这正是阶段 56 全量跑时预算越线的根因，也是「不落地 <c>PerAssembly</c>」的直接理由。</para>
    ///
    /// <para><b>为什么断言的是关系而不是 416 这个数：</b>416 是框架实现细节（环境相关），
    /// 断言它等于把自己钉死在 Avalonia 12.1.0 上；断言「会被抬高」与「跨派发回到同一起点」
    /// 则只依赖被守的性质本身。</para>
    /// </summary>
    [Fact]
    public void Precondition_FontCacheNormalization_KeepsTextBudgetsComparable()
    {
        const int N = 800;
        const int NewFamilies = 4;

        // 每次调用都是一次**独立派发**；取两次的较小值排掉首次热身伪影（实测 2,755 → 2,604）。
        // ⚠ 族名带 Guid：`PerAssembly` 下平台只建一次，写死名字只会"第一次没见过"，
        //    这个变量就不成立了（阶段 294 把本判据改成与隔离级别无关时踩到的第一件事）。
        double MeasureClean() => Math.Min(RunMeasure(0, clear: true), RunMeasure(0, clear: true));

        double RunMeasure(int pollute, bool clear) => UiTestHost.Run(() =>
        {
            var fm = FontManager.Current;
            for (var k = 0; k < pollute; k++)
                fm.TryGetGlyphTypeface(
                    new Typeface(new FontFamily($"GuardProbe Family {k}-{Guid.NewGuid():N}")), out _);

            if (clear) HeadlessFontCache.Clear();

            using var rtb = new RenderTargetBitmap(new PixelSize(64, 64));
            using var dc = rtb.CreateDrawingContext();
            var brush = new SolidColorBrush(Colors.White);
            var ft = new FormattedText("标签", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 14, brush);
            _ = ft.Width;
            _ = ft.Height;
            return PerCall(N, () => dc.DrawText(ft, new Point(0, 0)));
        });

        var clean = MeasureClean();
        var polluted = RunMeasure(NewFamilies, clear: false);   // 不清 ⇒ 抬高（正控）
        var normalized = RunMeasure(NewFamilies, clear: true);  // 解析完再清 ⇒ 应回到干净起点

        // 绿判据也要有读数（登记表 #42 的教训）
        _out.WriteLine($"[前置条件] 干净 {clean:N0} / 污染后 {polluted:N0} / 归一化后 {normalized:N0} B/次" +
                       $"（注入：{NewFamilies} 个全新族名 ⇒ 实测抬高 {polluted - clean:N0}）");

        Assert.True(polluted - clean >= 100,
            $"正控失败：解析了 {NewFamilies} 个全新字体族名，文本绘制成本却没被抬高"
            + $"（干净 {clean:N0} → 污染后 {polluted:N0} B/次）。"
            + "机制若真的变了，本判据的后半段就不再说明任何问题，必须先重新确认机制"
            + "（见 docs/project-status.md 阶段 58/293）。");

        // ⚠ **相对关系**，不是绝对值：绝对字节本来就跨上下文可比性差（阶段 57/58 的结论）。
        //    这里用的是本次运行自己的三个数 ⇒ 与隔离级别、与"之前跑过哪些用例"都无关。
        Assert.True(normalized - clean < polluted - normalized,
            $"归一化没有回到干净起点（干净 {clean:N0}，污染后 {polluted:N0}，归一化后 {normalized:N0} B/次："
            + "它离污染态比离干净值更近）—— `HeadlessFontCache.Clear()` 没清掉那张按族名键的表，"
            + "此时 `Gate_CanvasRenderAllocation_StaysWithinBudget` 的绝对字节预算会随"
            + "「之前跑过哪些用例」漂移（阶段 56 的越线现场，登记表 #47b）。");
    }
}
