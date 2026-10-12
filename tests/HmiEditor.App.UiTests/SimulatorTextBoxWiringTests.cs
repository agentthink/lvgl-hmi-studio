using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Simulator;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`SimulatorView` 的 `TextBox → 仿真变量模型` 驱动面**（阶段 191）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>SimulatorView.axaml</c> 的那 7 条接线从 `census` 升级成 `driven`。
///
/// <para><b>这一片同时补了一条口径</b>：该文件有 **9 个具名框、只有 7 条接线** —— 另外两个
/// （<c>RegAddressInput</c> / <c>RegValueInput</c>）由「读/写寄存器」按钮**点击时一次性读取**，
/// **不是**缺口。老口径（"驱动表必须覆盖每个具名框"）会把它们硬塞进驱动表（假账）；
/// 新口径是 <see cref="ControlWiringFace.AssertFullyDriven"/> 的
/// <c>withoutWiring</c> 登记表：**没接线也要显式登记 ＋ 写理由**，双向校验
/// （登记了但 XAML 里没有 ⇒ 红；登记了但其实接了线 ⇒ 红）。</para>
///
/// <para><b>生产构造</b>：<c>new SimulatorView(depot.Simulator, depot.Comms)</c>；构造里
/// <c>AddDefaultVariable()</c> 会注册并**选中**一条 <c>Var_1</c>（Sine / Amplitude=100…）——
/// 前提自检会把这几个数读出来，确保驱动落在真模型上。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class SimulatorTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言变量模型读到 <paramref name="Expected"/>。</summary>
    /// <param name="Unchanged">true ⇒ 期望**一字不变**（`double.TryParse` 失败后的原值）。</param>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<WindowDepot, string> Read,
        string Text,
        string Expected,
        bool Unchanged = false);

    /// <summary>
    /// ⚠ `ManualValueInput` 的手势**必须排在「切到 Manual 信号」之后**：
    /// `OnManualValueChanged` 首行判 `_currentVariable.Signal != SignalType.Manual` 就 return。
    /// </summary>
    private static readonly Spec[] Specs =
    {
        new("AmplitudeInput", "Amplitude", d => Num(Var(d).Amplitude), "42.5", "42.5"),
        new("OffsetInput", "Offset", d => Num(Var(d).Offset), "-3.5", "-3.5"),
        new("FrequencyInput", "Frequency", d => Num(Var(d).Frequency), "2.5", "2.5"),
        new("MinValueInput", "MinValue", d => Num(Var(d).MinValue), "-10", "-10"),
        new("MaxValueInput", "MaxValue", d => Num(Var(d).MaxValue), "250", "250"),
        new("NoiseInput", "Noise", d => Num(Var(d).Noise), "1.5", "1.5"),

        // 解析拒绝：非法文本**不进模型**（`TryUpdateDouble` 里 TryParse 失败就不调 setter）
        new("AmplitudeInput", "Amplitude", d => Num(Var(d).Amplitude), "abc", "", Unchanged: true),

        // 手动值（前提：Signal 已切到 Manual —— 见夹具里的那一步）
        new("ManualValueInput", "ManualValue", d => Num(Var(d).ManualValue), "7.5", "7.5"),
    };

    /// <summary>
    /// **「没有接线」登记表**：这两个框由「读/写寄存器」按钮点击时读走，**没有** `TextChanged` 接线。
    /// 它们不是缺口，但必须在这里显式登记 —— 新增一个"既没驱动、也没登记"的框会被众数判据点名。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["RegAddressInput"] = "「读/写寄存器」按钮**点击时**读它（`OnReadRegister` / `OnWriteRegister`），没有 TextChanged 接线",
        ["RegValueInput"] = "同上：写寄存器那一刻才读值，改一个字就写一次寄存器不是想要的语义",
    };

    private static SimulatedVariable Var(WindowDepot depot) => depot.Simulator.GetVariables()[0];

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void EverySpec_WritesTheSimulatedVariableAsDeclared()
    {
        using var sandbox = new DataRootSandbox("simulator-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            var window = new SimulatorView(depot.Simulator, depot.Comms);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：构造里 `AddDefaultVariable()` 注册并选中了一条 —— 否则
                // `OnVariablePropertyChanged` 首行判 `_currentVariable == null` 就 return（什么都没驱动）
                var variable = Assert.Single(depot.Simulator.GetVariables());
                Assert.Equal("Var_1", variable.VariableId);
                Assert.Equal(SignalType.Sine, variable.Signal);
                Assert.Equal(100.0, variable.Amplitude, 3);
                Assert.Equal(0, ControlWiringFace.Find<ListBox>(window, "VariableList").SelectedIndex);

                // 前提自检②：手动值那条手势的前提 —— 先把信号切到 Manual（**走生产处理器**
                // `OnSignalTypeChanged`，不是直接改模型）
                var combo = ControlWiringFace.Find<ComboBox>(window, "SignalTypeCombo");
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
                    .Single(i => string.Equals(i.Tag as string, "Manual", StringComparison.Ordinal));
                UiTestHost.Pump(2);
                Assert.Equal(SignalType.Manual, Var(depot).Signal);
                Assert.True(ControlWiringFace.Find<TextBox>(window, "ManualValueInput").IsEnabled,
                    "Manual 信号下手动值框必须是启用的 —— 否则这条手势够不着");

                foreach (var spec in Specs)
                {
                    var before = spec.Read(depot);
                    ControlWiringFace.Write(window, spec.Name, spec.Text);
                    var after = spec.Read(depot);

                    if (spec.Unchanged)
                    {
                        Assert.True(string.Equals(before, after, StringComparison.Ordinal),
                            $"{spec.Name}（{spec.Setting}）本应**不动**：'{spec.Text}' 之后从 '{before}' 变成了 '{after}'");
                        continue;
                    }

                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'");
                }

                // 反面见证：驱动落在**这一条**变量上，集合结构没被顺手改动
                Assert.Same(variable, Var(depot));
                Assert.Single(depot.Simulator.GetVariables());
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：7 条接线全部有驱动条目；两个没接线的框在登记表里（名字与理由都在）。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "SimulatorView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 7, WithoutWiring);
    }
}
