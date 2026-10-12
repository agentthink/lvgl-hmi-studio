using HmiEditor.CodeGen;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 全链路完整性契约测试 —— 枚举所有 HmiControlType，断言每一类控件在
/// LVGL 代码生成（create 函数 + 参数分支）与资源估算、本地化名称上均已打通。
/// 新增控件类型时若未同时接通这些环节，此测试将失败。
/// </summary>
public class ControlPipelineCompletenessTests
{
    /// <summary>不需要控件的类型（屏幕本身不是控件）。</summary>
    private static readonly HmiControlType[] SkipEstimator =
        { HmiControlType.Screen };

    [Fact]
    public void AllControlTypes_HaveLvglCreateFunction()
    {
        foreach (var type in Enum.GetValues<HmiControlType>())
        {
            var func = ControlMappingRegistry.GetCreateFunction(type);
            Assert.False(string.IsNullOrEmpty(func), $"{type} 缺少 LVGL create 函数");
            Assert.Matches(@"^lv_\w+_create$", func);
        }
    }

    [Fact]
    public void AllControlTypes_HaveFlashRamEstimates()
    {
        var estimator = new ResourceEstimator();
        var project = new HmiProject();

        foreach (var type in Enum.GetValues<HmiControlType>())
        {
            if (SkipEstimator.Contains(type)) continue;

            var before = estimator.EstimateFlashUsage(project);
            var testProject = new HmiProject();
            var screen = new HmiScreen { Id = "s", Name = "S", Width = 800, Height = 480 };
            screen.Controls.Add(new HmiControl
            {
                Type = type, Name = type.ToString(),
                Width = 100, Height = 100, BgColor = "#334155"
            });
            testProject.Screens.Add(screen);

            var afterFlash = estimator.EstimateFlashUsage(testProject);
            Assert.True(afterFlash > before, $"{type} 缺少 Flash 估算系数");
        }
    }

    [Fact]
    public void AllControlTypes_HaveLocalizedName()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        foreach (var type in Enum.GetValues<HmiControlType>())
        {
            var key = $"tb.{type.ToString().ToLowerInvariant()}";
            var zh = svc.GetString(key, "zh-CN");
            var en = svc.GetString(key, "en-US");

            // 未命中时返回键名自身
            Assert.NotEqual(key, zh);
            Assert.NotEqual(key, en);
            Assert.False(string.IsNullOrWhiteSpace(zh));
            Assert.False(string.IsNullOrWhiteSpace(en));
        }
    }

    [Fact]
    public void AllCanvasControls_HaveBufferDeclaration()
    {
        // canvas 像素缓冲类型（Svg/过程图形/椭圆）必须能生成文件级缓冲声明
        var canvasTypes = new[]
        {
            HmiControlType.Svg, HmiControlType.Tank,
            HmiControlType.Pipe, HmiControlType.Valve,
            HmiControlType.Ellipse,
        };
        foreach (var type in canvasTypes)
        {
            var ctrl = new HmiControl { Id = "x1", Type = type, Width = 120, Height = 80 };
            var decl = type switch
            {
                HmiControlType.Svg => SvgToLvglConverter.GenerateBufferDeclaration(ctrl),
                HmiControlType.Ellipse => ShapeControlCodeGen.GenerateBufferDeclaration(ctrl),
                _ => ProcessControlCodeGen.GenerateBufferDeclaration(ctrl),
            };
            // 缓冲必须按字节分配（LVGL 9.x 的 lv_color_t 是 3 字节，ARGB8888 需 4 B/px）
            Assert.Contains("static uint8_t ", decl);
            CSource.AssertText(decl, "LV_CANVAS_BUF_SIZE(120, 80, 32, LV_DRAW_BUF_STRIDE_ALIGN)");
            Assert.DoesNotContain("lv_color_t", decl);
        }
    }
}
