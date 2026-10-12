using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.CodeGen;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <c>obj_*</c> 控件对象符号名的**跨发射点契约**守卫。
///
/// <para><b>为什么这个测试类存在</b>：<c>obj_&lt;id&gt;</c> 由
/// <see cref="CodeGenerator"/> 在一处**声明**（<c>Screen.cs</c> 的
/// <c>static lv_obj_t* obj_x;</c>）、在另外十几处**引用**（创建、属性、动作回调、
/// 视觉状态、绑定刷新…）。历史上声明侧写裸 <c>control.Id</c>，而绑定刷新侧走
/// <see cref="CIdentifier.ForControlSymbol"/> —— 于是 id 里含 <c>-</c> / 空格时，
/// 声明是 <c>obj_a-b</c>（非法 C），引用是 <c>obj_a_b</c>，**两边符号名对不上**。
/// 设计器只生成 8 位十六进制 id，所以这条一直没暴露（属**潜在**缺陷），
/// 但"同一个符号名有两套构造方式"本身就是 <see cref="CIdentifier"/> 要消灭的东西。</para>
///
/// <para><b>本类怎么守</b>：不去逐个断言某一行，而是断两条**集合级**不变量 ——
/// ① 产物里出现的所有 <c>obj_*</c> 标识符都必须是**合法 C 标识符**（不含净化的痕迹）；
/// ② **声明的集合必须与引用的集合完全相等**（多一个是漏引用，少一个是未定义符号）。
/// 第二条在真实产物上是精确成立的（实测 87/87、91/91），比逐行断言更能拦住这类漂移。</para>
/// </summary>
public class ControlObjectSymbolContractTests
{
    /// <summary>声明侧：<c>static lv_obj_t* obj_xxx;</c>。</summary>
    private static readonly Regex DeclaredPattern = new(
        @"static\s+lv_obj_t\*\s+(obj_[A-Za-z0-9_]+)\s*;", RegexOptions.Compiled);

    /// <summary>任意位置的 <c>obj_*</c> 标识符。</summary>
    private static readonly Regex ReferencedPattern = new(
        @"\bobj_[A-Za-z0-9_]+", RegexOptions.Compiled);

    /// <summary>
    /// id 含需净化字符时，**声明与引用必须用同一个净化后的符号名**，
    /// 且产物里不得出现裸 id 拼出的非法符号。
    /// </summary>
    [Fact]
    public void ControlIdWithIllegalCharacters_UsesSanitizedSymbolEverywhere()
    {
        var code = GenerateWithControlIds("lbl-1", "bar 2");

        // 修复前这两行会红：声明侧是非法 C 的 obj_lbl-1 / obj_bar 2
        CSource.AssertNoText(code, "obj_lbl-1");
        CSource.AssertNoText(code, "obj_bar 2");

        // 净化后的名字必须真的出现在声明位置（证明不是"两边都改名成别的"）
        CSource.AssertText(code, "static lv_obj_t* obj_lbl_1;");
        CSource.AssertText(code, "static lv_obj_t* obj_bar_2;");
    }

    /// <summary>
    /// 集合级不变量：**声明的 <c>obj_*</c> 与引用的 <c>obj_*</c> 完全相等**。
    /// 引用多于声明 = 未定义符号；声明多于引用 = 有发射点漏了。
    /// </summary>
    [Fact]
    public void DeclaredAndReferencedControlObjects_MatchExactly()
    {
        foreach (var code in new[]
                 {
                     GenerateWithControlIds("lbl-1", "bar 2", "正常id"),
                     GenerateWithControlIds("lbl", "bar", "led"),
                     GenerateWithCanvasControlIds("tank-1", "pipe 2", "valve-3", "shp-4", "svg-5"),
                     GenerateWithCanvasControlIds("tank", "pipe", "valve", "shp", "svg"),
                 })
        {
            var declared = DeclaredPattern.Matches(code).Select(m => m.Groups[1].Value).ToHashSet();
            var referenced = ReferencedPattern.Matches(code).Select(m => m.Value).ToHashSet();

            Assert.True(declared.SetEquals(referenced),
                $"声明与引用的 obj_* 不一致；只在声明 = [{string.Join(", ", declared.Except(referenced))}]，" +
                $"只在引用 = [{string.Join(", ", referenced.Except(declared))}]");
        }
    }

    /// <summary>
    /// canvas 系控件（Tank / Pipe / Valve / Ellipse / Svg）的符号名由**另一组发射器**
    /// 产出（<c>ProcessControlCodeGen</c> / <c>ShapeControlCodeGen</c> /
    /// <c>SvgToLvglConverter</c>）：它们既在 <c>CodeGenerator.Screen.cs</c> 之外**声明**
    /// <c>_buf_*</c> / <c>_shapebuf_*</c> / <c>buf_*</c> 文件级 static，又在自己的
    /// <c>OpenCanvas</c> 里**引用**同一个缓冲、并给 <c>obj_*</c> 赋值。
    ///
    /// <para>这条测试存在的理由：集合级不变量只在**语料覆盖到**时才有意义 ——
    /// 只喂 Label/Bar 的语料永远不会经过这三个发射器。修 <c>obj_*</c> 时正是这里
    /// 曾经被漏掉（声明侧净化、赋值侧仍裸用），所以语料必须钉住。</para>
    /// </summary>
    [Fact]
    public void CanvasControlIdWithIllegalCharacters_UsesSanitizedSymbolInBufferAndObject()
    {
        var code = GenerateWithCanvasControlIds("tank-1", "pipe 2", "valve-3", "shp-4", "svg-5");

        // 非法符号一个都不能出现（裸 id 拼出来的）
        foreach (var bad in new[]
                 {
                     "obj_tank-1", "_buf_tank-1",
                     "obj_pipe 2", "_buf_pipe 2",
                     "obj_valve-3", "_buf_valve-3",
                     "obj_shp-4", "_shapebuf_shp-4",
                     "obj_svg-5", "buf_svg-5",
                 })
        {
            CSource.AssertNoText(code, bad);
        }

        // 净化后的名字必须真的出现（证明不是"两边都改名成别的"）
        foreach (var good in new[]
                 {
                     "static lv_obj_t* obj_tank_1;", "_buf_tank_1",
                     "static lv_obj_t* obj_pipe_2;", "_buf_pipe_2",
                     "static lv_obj_t* obj_valve_3;", "_buf_valve_3",
                     "static lv_obj_t* obj_shp_4;", "_shapebuf_shp_4",
                     "static lv_obj_t* obj_svg_5;", "buf_svg_5",
                 })
        {
            CSource.AssertText(code, good);
        }
    }

    /// <summary>正常 id 的符号名不受影响（净化对合法标识符是恒等）。</summary>
    [Fact]
    public void LegalIds_KeepTheirExactSymbolName()
    {
        var code = GenerateWithControlIds("tbl_main", "a8f3c1d2");

        CSource.AssertText(code, "static lv_obj_t* obj_tbl_main;");
        CSource.AssertText(code, "static lv_obj_t* obj_a8f3c1d2;");
    }

    /// <summary>
    /// canvas 系的正常 id 同样逐字符不变 —— 这是"改动没有移动任何既有产物"的证据，
    /// 三个 canvas 发射器都覆盖到。
    /// </summary>
    [Fact]
    public void LegalCanvasIds_KeepTheirExactSymbolName()
    {
        var code = GenerateWithCanvasControlIds("tank_a", "pipe_b", "valve_c", "shp_d", "svg_e");

        foreach (var name in new[]
                 {
                     "static lv_obj_t* obj_tank_a;", "static uint8_t _buf_tank_a[",
                     "static lv_obj_t* obj_pipe_b;", "static uint8_t _buf_pipe_b[",
                     "static lv_obj_t* obj_valve_c;", "static uint8_t _buf_valve_c[",
                     "static lv_obj_t* obj_shp_d;", "static uint8_t _shapebuf_shp_d[",
                     "static lv_obj_t* obj_svg_e;", "static uint8_t buf_svg_e[",
                 })
        {
            CSource.AssertText(code, name);
        }
    }

    /// <summary>空 id 走 <see cref="CIdentifier.ControlSymbolFallback"/> —— 与绑定刷新侧的历史行为对齐。</summary>
    [Fact]
    public void EmptyId_FallsBackToControlSymbolFallback()
    {
        var code = GenerateWithControlIds("");

        CSource.AssertText(code, $"static lv_obj_t* {CIdentifier.ControlObject(string.Empty)};");
        CSource.AssertText(code, CIdentifier.ControlSymbolFallback);
    }

    // ───────────────────── fixture ─────────────────────

    /// <summary>无 canvas 缓冲的常规控件。</summary>
    private static readonly HmiControlType[] BasicTypes =
    {
        HmiControlType.Label, HmiControlType.Bar, HmiControlType.Led, HmiControlType.Slider,
    };

    /// <summary>canvas 系控件 —— 各自走独立的符号发射器。</summary>
    private static readonly HmiControlType[] CanvasTypes =
    {
        HmiControlType.Tank, HmiControlType.Pipe, HmiControlType.Valve,
        HmiControlType.Ellipse, HmiControlType.Svg,
    };

    /// <summary>
    /// 造一个含指定 id 控件（Label / Bar / Led / Slider，带变量绑定以触发绑定刷新侧的引用）的项目，
    /// 返回模拟器模块产物。绑定是必要的：正是"声明侧 vs 刷新侧"才是原缺陷所在。
    /// </summary>
    private static string GenerateWithControlIds(params string[] ids)
        => GenerateWithControls(ids
            .Select((id, i) => (Id: id, Type: BasicTypes[i % BasicTypes.Length]))
            .ToArray());

    /// <summary>
    /// 同上，但控件类型取自 <see cref="CanvasTypes"/> —— 覆盖
    /// <c>ProcessControlCodeGen</c> / <c>ShapeControlCodeGen</c> / <c>SvgToLvglConverter</c>
    /// 这三个独立发射器（它们自造 <c>_buf_*</c> / <c>_shapebuf_*</c> / <c>buf_*</c> 符号）。
    /// </summary>
    private static string GenerateWithCanvasControlIds(params string[] ids)
        => GenerateWithControls(ids
            .Select((id, i) => (Id: id, Type: CanvasTypes[i % CanvasTypes.Length]))
            .ToArray());

    private static string GenerateWithControls(params (string Id, HmiControlType Type)[] items)
    {
        var project = new HmiProject { Name = "ObjSymbolContract", LvglVersion = "9.5" };
        var screen = new HmiScreen { Id = "scr1", Name = "Main", Width = 1024, Height = 600 };
        project.Screens.Add(screen);

        var vm = project.VariableManager;
        vm.Connections.Add(new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" });
        vm.Variables.Add(new Variable
        {
            Id = "v1", Name = "温度", Address = "40001", DataType = "Float", ConnectionId = "c1",
        });

        for (var i = 0; i < items.Length; i++)
        {
            var ctl = new HmiControl
            {
                Id = items[i].Id,
                Type = items[i].Type,
                X = 10 + i * 60, Y = 10, Width = 100, Height = 30,
                MinValue = 0, MaxValue = 100,
                Text = "t",
            };
            ctl.VariableBindings.Add(new VariableBindingData
            {
                VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value",
            });
            screen.Controls.Add(ctl);
        }

        return new CodeGenerator().GenerateSimulatorModule(project, null);
    }
}
