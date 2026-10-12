using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 对拍回归：PC 模拟器（<c>sim_refresh_bindings</c>）与设备端（<c>hmi_ui_refresh_timer_cb</c>）
/// 的绑定刷新代码必须由同一实现产出（<c>CodeGenerator.GenerateRefreshBindings</c>）。
///
/// 背景：这两条路径原本是两份各自演化的代码，控件分支逐类型重复，已实际造成行为漂移
/// （Led/Switch 判真不同、Label 小数位不同、设备端缺少部分控件分支）。
/// 本测试锁定"控件类型覆盖集合"与"值源差异"两点，防止再次分叉。
/// </summary>
public class RefreshBindingsParityTests
{
    private static HmiProject MakeProject()
    {
        var project = new HmiProject { Name = "Parity", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "℃",
        });

        void Bind(HmiControl c)
        {
            c.VariableBindings.Add(new VariableBindingData
            {
                VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value",
            });
            scr.Controls.Add(c);
        }

        Bind(new HmiControl { Id = "lbl", Type = HmiControlType.Label, X = 0, Y = 0, Width = 100, Height = 30, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "bar", Type = HmiControlType.Bar, X = 0, Y = 40, Width = 200, Height = 20, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "sld", Type = HmiControlType.Slider, X = 0, Y = 70, Width = 200, Height = 20, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "arc", Type = HmiControlType.Arc, X = 220, Y = 0, Width = 60, Height = 60, MinValue = 0, MaxValue = 100 });
        Bind(new HmiControl { Id = "led", Type = HmiControlType.Led, X = 300, Y = 0, Width = 20, Height = 20 });
        Bind(new HmiControl { Id = "sw", Type = HmiControlType.Switch, X = 330, Y = 0, Width = 60, Height = 30 });
        Bind(new HmiControl { Id = "cb", Type = HmiControlType.CheckBox, X = 400, Y = 0, Width = 80, Height = 30 });

        // 表格单元格绑定（走同一共享实现的单元格分支）
        scr.Controls.Add(new HmiControl
        {
            Id = "tbl", Type = HmiControlType.Table, X = 0, Y = 120, Width = 300, Height = 120,
            RowCount = 2, ColumnCount = 2,
            Cells = new List<TableCellData>
            {
                new TableCellData { Row = 0, Col = 0, VariableId = "v1", DecimalPlaces = 1 },
            },
        });
        return project;
    }

    /// <summary>每个绑定控件类型在两条路径中都必须出现的"处理器标记"。</summary>
    private static readonly (string Type, string Marker)[] Handlers =
    {
        ("Label", "lv_label_set_text_fmt(obj_lbl"),
        ("Bar", "lv_bar_set_value(obj_bar"),
        ("Slider", "lv_slider_set_value(obj_sld"),
        ("Arc", "lv_arc_set_value(obj_arc"),
        ("Led", "lv_led_set_brightness(obj_led"),
        ("Switch", "lv_obj_add_state(obj_sw, LV_STATE_CHECKED)"),
        ("CheckBox", "lv_obj_add_state(obj_cb, LV_STATE_CHECKED)"),
        ("Table", "lv_table_set_cell_value_fmt(obj_tbl"),
    };

    [Fact]
    public void BothPaths_CoverTheSameControlHandlers()
    {
        var project = MakeProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        foreach (var (type, marker) in Handlers)
        {
            Assert.True(sim.Contains(marker), $"模拟器路径缺少 {type} 刷新分支：{marker}");
            Assert.True(device.Contains(marker), $"设备路径缺少 {type} 刷新分支：{marker}");
        }
    }

    [Fact]
    public void BothPaths_DifferOnlyInValueSourceAndGuards()
    {
        var project = MakeProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 值来源：模拟器正弦波；设备读标签缓存
        Assert.Contains("sinf(t +", sim);
        Assert.DoesNotContain("get_tag_value(TAG_v1)", sim);
        Assert.Contains("get_tag_value(TAG_v1)", device);
        Assert.DoesNotContain("sinf(", device);

        // 空指针守卫：设备端有，模拟器没有
        Assert.Contains("if (obj_lbl)", device);
        Assert.DoesNotContain("if (obj_lbl)", sim);

        // 外壳：只有设备端生成 timer 启停
        Assert.Contains("hmi_ui_start_binding_refresh", device);
        Assert.DoesNotContain("hmi_ui_start_binding_refresh", sim);
        Assert.Contains("sim_refresh_bindings(uint32_t tick)", sim);
    }

    [Fact]
    public void BothPaths_ShareLabelSemantics()
    {
        // 同一条 BindingSemantics 规则：两个路径的 Label 格式串必须完全一致
        var project = MakeProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        const string fmt = "lv_label_set_text_fmt(obj_lbl, \"%.2f℃\"";
        Assert.Contains(fmt, sim);
        Assert.Contains(fmt, device);
    }

    [Fact]
    public void BothPaths_ShareBooleanThreshold()
    {
        var project = MakeProject();
        var sim = new CodeGenerator().GenerateSimulatorModule(project, null);
        var device = new CodeGenerator().GenerateSharedLibraryProject(project);

        const string led = "lv_led_set_brightness(obj_led, (v) != 0.0f ? 255 : 0);";
        Assert.Contains(led, sim);
        Assert.Contains(led, device);
    }
}
