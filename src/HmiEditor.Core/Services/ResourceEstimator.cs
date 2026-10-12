using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 估算 HMI 项目在嵌入式目标上的 Flash 和 RAM 资源占用。
/// 系数基于 LVGL v9 控件典型内存占用经验值。
/// </summary>
public class ResourceEstimator
{
    /// <summary>各控件类型的 Flash 代码体积估算系数（字节）。</summary>
    private static readonly Dictionary<HmiControlType, long> s_flashCoefficients = new()
    {
        { HmiControlType.Button,   120 },
        { HmiControlType.Label,     80 },
        { HmiControlType.Slider,   200 },
        { HmiControlType.Switch,   150 },
        { HmiControlType.CheckBox, 130 },
        { HmiControlType.Image,     64 },
        { HmiControlType.Bar,      200 },
        { HmiControlType.Dropdown, 300 },
        { HmiControlType.TextArea, 250 },
        { HmiControlType.Gauge,    350 },
        { HmiControlType.Knob,     280 },
        { HmiControlType.Keyboard, 800 },
        { HmiControlType.Led,      100 },
        { HmiControlType.Spinner,  180 },
        { HmiControlType.TabView,  600 },
        { HmiControlType.Line,      60 },
        { HmiControlType.Table,    500 },
        // I6/I7: SVG 与过程图形控件（canvas 像素缓冲 + 绘制代码）
        { HmiControlType.Svg,      400 },
        { HmiControlType.Tank,     480 },
        { HmiControlType.Pipe,     380 },
        { HmiControlType.Valve,    420 },
        // I10: 基础绘图图形
        { HmiControlType.Rectangle,  90 },
        { HmiControlType.Ellipse,   140 },
        { HmiControlType.Polyline,   80 },
        { HmiControlType.Arc,       200 },
    };

    /// <summary>各控件类型的 RAM 运行时占用估算系数（字节）。</summary>
    private static readonly Dictionary<HmiControlType, long> s_ramCoefficients = new()
    {
        { HmiControlType.Button,   128 },
        { HmiControlType.Label,     96 },
        { HmiControlType.Slider,   192 },
        { HmiControlType.Switch,   128 },
        { HmiControlType.CheckBox, 128 },
        { HmiControlType.Image,    160 },
        { HmiControlType.Bar,      192 },
        { HmiControlType.Dropdown, 256 },
        { HmiControlType.TextArea, 320 },
        { HmiControlType.Gauge,    384 },
        { HmiControlType.Knob,     320 },
        { HmiControlType.Keyboard, 768 },
        { HmiControlType.Led,      112 },
        { HmiControlType.Spinner,  192 },
        { HmiControlType.TabView,  512 },
        { HmiControlType.Line,      80 },
        { HmiControlType.Table,    640 },
        // I6/I7: SVG 与过程图形控件（canvas 像素缓冲按尺寸另计）
        { HmiControlType.Svg,      256 },
        { HmiControlType.Tank,     256 },
        { HmiControlType.Pipe,     224 },
        { HmiControlType.Valve,    224 },
        // I10: 基础绘图图形
        { HmiControlType.Rectangle,  96 },
        { HmiControlType.Ellipse,    96 },
        { HmiControlType.Polyline,   96 },
        { HmiControlType.Arc,       160 },
    };

    /// <summary>估算项目 Flash 占用（字节）。</summary>
    public long EstimateFlashUsage(HmiProject project)
    {
        long total = 4096; // 基础框架开销

        foreach (var screen in project.Screens)
        {
            total += 2048; // 每屏基础开销
            foreach (var control in screen.Controls)
            {
                total += EstimateControlFlash(control);
            }
        }

        // 变量表开销
        total += project.VariableManager.Variables.Count * 48;

        // 报警配置开销
        total += project.AlarmGroups.Sum(g => g.Alarms.Count * 64);

        return total;
    }

    /// <summary>估算项目 RAM 占用（字节）。</summary>
    public long EstimateRamUsage(HmiProject project)
    {
        long total = project.Screens.Count * 2048; // 每屏画面缓冲

        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                total += EstimateControlRam(control);
            }
        }

        // 变量表运行时开销
        total += project.VariableManager.Variables.Count * 64;

        // LVGL 堆建议值
        total += 32768;

        return total;
    }

    private static long EstimateControlFlash(HmiControl control)
    {
        s_flashCoefficients.TryGetValue(control.Type, out var baseSize);

        // 图片控件按尺寸估算额外占用
        if (control.Type == HmiControlType.Image)
        {
            baseSize += (long)control.Width * (long)control.Height * 2; // RGB565
        }

        // I10: 椭圆使用 lv_canvas 像素缓冲（ARGB8888，每像素 4 字节）
        if (control.Type == HmiControlType.Ellipse)
        {
            baseSize += (long)Math.Max(1, control.Width) * (long)Math.Max(1, control.Height) * 4;
        }

        // 子控件递归
        foreach (var child in control.Children)
        {
            baseSize += EstimateControlFlash(child);
        }

        return baseSize;
    }

    private static long EstimateControlRam(HmiControl control)
    {
        s_ramCoefficients.TryGetValue(control.Type, out var baseSize);

        // I6/I7/I10: canvas 像素缓冲（ARGB8888，每像素 4 字节）
        // Svg/Tank/Pipe/Valve/Ellipse 均在运行时分配 lv_color_t 缓冲数组
        if (control.Type is HmiControlType.Svg or HmiControlType.Tank
            or HmiControlType.Pipe or HmiControlType.Valve or HmiControlType.Ellipse)
        {
            baseSize += (long)Math.Max(1, control.Width) * (long)Math.Max(1, control.Height) * 4;
        }

        foreach (var child in control.Children)
        {
            baseSize += EstimateControlRam(child);
        }

        return baseSize;
    }
}
