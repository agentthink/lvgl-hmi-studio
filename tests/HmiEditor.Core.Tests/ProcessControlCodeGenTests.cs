using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I7 过程图形控件（Tank/Pipe/Valve）的 CodeGen 测试。
/// 覆盖：缓冲声明、生成代码非空、layer 绘制 API、运行时更新函数存在、阀门类型分支。
/// 注：CodeGen 采用与 SvgToLvglConverter 一致的 layer 绘制 API（lv_draw_rect/line/arc/polygon），
/// 绘制内容与设计器 DesignCanvas.RenderTank/RenderPipe/RenderValve 一一对应。
/// </summary>
public class ProcessControlCodeGenTests
{
    private static HmiControl Make(HmiControlType type, string id = "c1") => new()
    {
        Id = id, Name = type.ToString(), Type = type,
        X = 10, Y = 20, Width = 140, Height = 200,
        MinValue = 0, MaxValue = 100, Value = 50,
        BgColor = "#0ea5e9", IndicatorColor = "#22c55e",
        BorderWidth = 3, TankShowScale = true, TankTickCount = 5,
        PipeDirection = "Horizontal", PipeFlowDirection = "Forward", PipeFlowSpeed = 2000,
        ValveType = "Ball",
    };

    [Fact]
    public void GenerateBufferDeclaration_Tank_ProducesStaticBuffer()
    {
        var ctrl = Make(HmiControlType.Tank);
        var decl = ProcessControlCodeGen.GenerateBufferDeclaration(ctrl);
        CSource.AssertText(decl, "static uint8_t _buf_c1[LV_CANVAS_BUF_SIZE(140, 200, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        Assert.DoesNotContain("lv_color_t", decl);
    }

    [Fact]
    public void GenerateCode_Tank_ProducesCanvasCode()
    {
        var code = ProcessControlCodeGen.GenerateCode(Make(HmiControlType.Tank));
        Assert.Contains("lv_canvas_create", code);
        Assert.Contains("lv_canvas_set_buffer", code);
        Assert.Contains("lv_canvas_init_layer", code);
        Assert.Contains("lv_canvas_finish_layer", code);
        Assert.Contains("update_tank_c1", code);
        // 罐壁 + 液位填充用 lv_draw_rect；高光线 + 刻度用 lv_draw_line
        Assert.Contains("lv_draw_rect", code);
        Assert.Contains("lv_draw_line", code);
        // 液位色 IndicatorColor 兜底
        Assert.Contains("0x22C55E", code);
    }

    [Fact]
    public void GenerateCode_Pipe_ProducesPipeCode()
    {
        var code = ProcessControlCodeGen.GenerateCode(Make(HmiControlType.Pipe));
        Assert.Contains("lv_canvas_create", code);
        // 主轴 + 流向箭头 + 流动虚线段
        Assert.Contains("lv_draw_line", code);
        Assert.Contains("流动动画周期: 2000ms", code);
        // 箭头/流动色 IndicatorColor
        Assert.Contains("0x22C55E", code);
    }

    [Fact]
    public void GenerateCode_Valve_ProducesValveCode()
    {
        var code = ProcessControlCodeGen.GenerateCode(Make(HmiControlType.Valve));
        Assert.Contains("lv_canvas_create", code);
        Assert.Contains("update_valve_c1", code);
        // 球阀（默认）：圆体 lv_draw_arc + 手柄/管道 lv_draw_line
        Assert.Contains("lv_draw_arc", code);
        Assert.Contains("lv_draw_line", code);
    }

    [Fact]
    public void GenerateCode_Valve_ButterflyType_ProducesButterflyCode()
    {
        var ctrl = Make(HmiControlType.Valve);
        ctrl.ValveType = "Butterfly";
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.Contains("lv_draw_arc", code);   // 圆体
        Assert.Contains("lv_draw_rect", code);  // 蝶盘
    }

    [Fact]
    public void GenerateCode_Valve_GateType_ProducesGateCode()
    {
        var ctrl = Make(HmiControlType.Valve);
        ctrl.ValveType = "Gate";
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.Contains("lv_draw_rect", code);  // 闸体
        Assert.Contains("lv_draw_line", code);  // 闸板/阀杆/管道
    }

    [Fact]
    public void GenerateCode_Valve_GlobeType_ProducesGlobeCode()
    {
        var ctrl = Make(HmiControlType.Valve);
        ctrl.ValveType = "Globe";
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.Contains("lv_draw_triangle", code); // 上下三角形（LVGL 9.x 无 lv_draw_polygon）
        Assert.Contains("lv_draw_line", code);     // 阀杆
        Assert.DoesNotContain("lv_draw_polygon", code);
    }

    [Fact]
    public void GenerateCode_Pipe_VerticalDirection_ProducesVerticalCode()
    {
        var ctrl = Make(HmiControlType.Pipe);
        ctrl.PipeDirection = "Vertical";
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.Contains("lv_draw_line", code);
        // 垂直管道主轴：X 恒为宽度一半（LVGL 9.x API 只接 (layer, dsc)，几何在 dsc.p1/p2）
        Assert.Contains("dsc.p1.x = 70; dsc.p1.y = 0;", code);
        Assert.DoesNotContain(", pts, ", code);
    }

    [Fact]
    public void GenerateCode_Pipe_ReverseFlow_ProducesReverseCode()
    {
        var ctrl = Make(HmiControlType.Pipe);
        ctrl.PipeFlowDirection = "Reverse";
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.Contains("反向", code);
    }

    [Fact]
    public void GenerateCode_Pipe_NoFlowSpeed_ProducesNoAnimation()
    {
        var ctrl = Make(HmiControlType.Pipe);
        ctrl.PipeFlowSpeed = 0;
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        Assert.DoesNotContain("流动动画周期", code);
    }

    [Fact]
    public void GenerateCode_Tank_NoScale_ProducesNoTickLabels()
    {
        var ctrl = Make(HmiControlType.Tank);
        ctrl.TankShowScale = false;
        var code = ProcessControlCodeGen.GenerateCode(ctrl);
        // 关闭刻度后不再输出刻度标签声明；罐壁/液位/高光线仍绘制
        Assert.DoesNotContain("Tank 刻度标签", code);
        Assert.Contains("lv_draw_rect", code);
    }
}
