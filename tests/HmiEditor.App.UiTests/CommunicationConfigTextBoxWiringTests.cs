using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`CommunicationConfigView` 的 `TextBox → 批量添加预览` 驱动面**（阶段 193）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>CommunicationConfigView.axaml</c> 的那 4 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：4 个批量添加框都接在 <c>UpdateBatchPreview()</c> 上，在此之前**零判据** ——
/// 摘掉任意一条接线，或把"前缀空 / 条数越界 ⇒ 预览清空"这两道闸门删掉，都不会红。</para>
///
/// <para><b>读的是哪一面</b>：这条接线的**全部产出就是那句预览文本**（`TxtBatchPreview.Text`），
/// 没有别的模型落点 —— 与 `AuditLogView` / `VariablePicker` 两条面同形。
/// 断言**刻意不冻文案**：只判"空 / 非空"与"含不含自己写进去的前缀"，不读字典渲染出来的整句。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class CommunicationConfigTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言预览的**空/非空**与**必含串**。</summary>
    private sealed record Spec(string Name, string Text, bool Empty, string? MustContain, string Why);

    /// <summary>
    /// ⚠ **顺序即口径**：每次驱动都会把四个框一起读一遍，所以"某一道闸门"必须在前面的手势里
    /// 先把其余输入摆成合法值（否则空预览是**别的**原因造成的 —— 判据会看不出区别）。
    /// XAML 初值：`TxtBatchStartIndex = "1"`、`TxtBatchCount = "10"`（前缀与地址初始为空）。
    /// </summary>
    private static readonly Spec[] Specs =
    {
        new("TxtBatchNamePrefix", "阀", false, "阀1", "前缀一到手就出预览（条数用的是 XAML 初值 10）"),
        new("TxtBatchCount", "0", true, null, "闸门①下界：条数 < 1 ⇒ 空"),
        new("TxtBatchCount", "501", true, null, "闸门①上界：条数 > 500 ⇒ 空"),
        new("TxtBatchCount", "3", false, "阀1", "回到合法值 ⇒ 预览回来（不是「空过一次就再也不出」）"),
        new("TxtBatchStartIndex", "5", false, "阀5", "起始序号参与样例名"),
        new("TxtBatchStartAddr", "10", false, "阀5", "起始地址参与样例括号（地址串格式随寄存器类型，不冻文案）"),
        new("TxtBatchNamePrefix", "   ", true, null, "闸门②：前缀 **Trim** 之后为空 ⇒ 空"),
        new("TxtBatchNamePrefix", "泵", false, "泵5", "裁掉空白之后再来一次 ⇒ 预览回来"),
    };

    /// <summary>
    /// **「没有接线」登记表**：连接名/子设备名/子设备地址/描述、单条变量名/地址这 6 个框
    /// 走"点保存"那条路一次性读取，没有 `TextChanged` 接线 —— 不是缺口。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["TxtConnName"] = "连接名由「保存」一次性读取，没有 TextChanged 接线",
        ["TxtSlaveName"] = "同上（子设备名）",
        ["TxtSlaveAddress"] = "同上（子设备地址）",
        ["TxtSlaveDesc"] = "同上（子设备描述）",
        ["TxtSlaveVarName"] = "同上（单条变量名）",
        ["TxtSlaveVarAddress"] = "同上（单条变量地址）",
    };

    private static string Preview(Window window) =>
        ControlWiringFace.Find<TextBlock>(window, "TxtBatchPreview").Text ?? "";

    [Fact]
    public void EverySpec_UpdatesTheBatchPreviewAsDeclared()
    {
        using var sandbox = new DataRootSandbox("comm-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            var window = new CommunicationConfigView(depot.Variables, depot.Project);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：这条面靠"其余输入是什么"分辨闸门 ⇒ 先把 XAML 初值量出来（写死在注释里的
                // 前提一旦漂移，本判据的动作序列就整体失效，而症状只是几条莫名其妙的红）
                Assert.Equal("1", ControlWiringFace.Find<TextBox>(window, "TxtBatchStartIndex").Text);
                Assert.Equal("10", ControlWiringFace.Find<TextBox>(window, "TxtBatchCount").Text);
                Assert.True(string.IsNullOrEmpty(
                    ControlWiringFace.Find<TextBox>(window, "TxtBatchNamePrefix").Text));

                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(window, spec.Name, spec.Text);
                    var preview = Preview(window);

                    if (spec.Empty)
                    {
                        Assert.True(preview.Length == 0,
                            $"{spec.Name} 驱动 '{spec.Text}'（{spec.Why}）后预览应当为空，实际 '{preview}'");
                        continue;
                    }

                    Assert.True(preview.Length > 0,
                        $"{spec.Name} 驱动 '{spec.Text}'（{spec.Why}）后预览不该为空");
                    Assert.True(preview.Contains(spec.MustContain!, StringComparison.Ordinal),
                        $"{spec.Name} 驱动 '{spec.Text}'（{spec.Why}）后预览 '{preview}' 里应当含 '{spec.MustContain}'");
                }
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：4 个批量框都有驱动条目；6 个走"保存"的框在登记表里。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "CommunicationConfigView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 4, WithoutWiring);
    }
}
