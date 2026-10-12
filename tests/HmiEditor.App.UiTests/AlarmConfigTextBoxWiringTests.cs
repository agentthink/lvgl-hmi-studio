using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Alarm;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`AlarmConfigView` 的 `TextBox → 报警模型` 驱动面**（阶段 190）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>AlarmConfigView.axaml</c> 的那 7 条接线从 `census` 升级成 `driven`。
///
/// <para><b>为什么这 7 条还是缺口</b>：阶段 120 的 <c>AlarmConfigFieldEditUiTests</c> 确实驱动了
/// 这个对话框的字段编辑，但它是靠 <c>ChkEnabled.IsChecked = false</c> 那一跳把
/// <c>OnAlarmFieldChanged</c> 叫起来的（复选框的 <c>IsCheckedChanged</c> 程序化赋值**会**触发，
/// 而 <c>TextBox.Text</c> 不会 —— 阶段 187 实测）⇒ 逐个 TextBox 的那 7 条接线
/// **仍然是零驱动**：单独摘掉 <c>TxtThreshold.TextChanged</c>，既有判据不会红
/// （复选框那一跳会把阈值从界面上一起读走，读数上看不出差别）。</para>
///
/// <para><b>判据形状</b>：走生产构造路径建真实 <see cref="AlarmConfigView"/> ＋ 真实
/// <see cref="HmiEditor.Core.Models.Alarm.AlarmGroup"/>，选中所属组与报警让编辑面板回填，
/// 再逐框驱动（写文本 ＋ 补发 <c>TextChangedEvent</c>），最后从**模型**上读结果。
/// 起点由前提自检把关（面板必须真开、回填的必须是模型值、复选框那一跳没被用上）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AlarmConfigTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言报警模型读到 <paramref name="Expected"/>。</summary>
    /// <param name="Unchanged">true ⇒ 期望**一字不变**（解析拒绝后的原值）。</param>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<WindowDepot, string> Read,
        string Text,
        string Expected,
        bool Unchanged = false);

    private static readonly Spec[] Specs =
    {
        // ── 基本信息：名称**不裁剪**、描述**不裁剪**（照实现的口径写成期望值）──
        new("TxtAlarmName", "Name", d => AlarmOf(d).Name, "液位高高", "液位高高"),
        new("TxtAlarmDescription", "Description", d => AlarmOf(d).Description ?? "", "罐区液位过高", "罐区液位过高"),
        new("TxtAlarmDescription", "Description", d => AlarmOf(d).Description ?? "",
            "  两侧空白原样保留  ", "  两侧空白原样保留  "),

        // ── ISA-18.2 职责字段（阶段 266；登记 §2-#11 切片㊀）：三框都**原样**写回（不裁剪）──
        new("TxtAlarmCause", "Cause", d => AlarmOf(d).Cause ?? "", "冷却水阀关断", "冷却水阀关断"),
        new("TxtAlarmResponse", "Response", d => AlarmOf(d).Response ?? "", "开备用泵", "开备用泵"),
        new("TxtAlarmConsequence", "Consequence", d => AlarmOf(d).Consequence ?? "",
            "  超温停车  ", "  超温停车  "),

        // ── ISA-18.2 切片㈡（阶段 292）：允许响应时间（ms，**可空**）──
        //    空 ⇒ null（用该严重度的默认值）；非法文本 ⇒ 保持原值（与兄弟数值字段同口径）；
        //    0 是合法值（Core 把它当"立即"，优先级按最严处理）。
        new("TxtAllowedResponseMs", "AllowedResponseMs", d => AllowedOf(d), "120000", "120000"),
        new("TxtAllowedResponseMs", "AllowedResponseMs", d => AllowedOf(d), "", ""),
        new("TxtAllowedResponseMs", "AllowedResponseMs", d => AllowedOf(d), "abc", "", Unchanged: true),
        new("TxtAllowedResponseMs", "AllowedResponseMs", d => AllowedOf(d), "0", "0"),

        // ── 数值字段：解析成功 ⇒ 写入 ──
        new("TxtThreshold", "Condition.Threshold",
            d => Num(AlarmOf(d).Condition.Threshold), "123.5", "123.5"),
        new("TxtDeadband", "Condition.Deadband",
            d => Num(AlarmOf(d).Condition.Deadband), "2.5", "2.5"),
        new("TxtDelayMs", "Condition.DelayMs",
            d => AlarmOf(d).Condition.DelayMs.ToString(CultureInfo.InvariantCulture), "1500", "1500"),
        new("TxtExpectedValue", "Condition.ExpectedValue",
            d => Num(AlarmOf(d).Condition.ExpectedValue), "7.5", "7.5"),

        // ── 解析拒绝：非法文本**不进模型**（`int.TryParse` 失败就不写）──
        new("TxtDelayMs", "Condition.DelayMs",
            d => AlarmOf(d).Condition.DelayMs.ToString(CultureInfo.InvariantCulture), "abc", "", Unchanged: true),

        // ── 位偏移：**钳到 0…63**（AlarmEngine 的位移按 64 取模 ⇒ 见产品代码注释）──
        new("TxtBitOffset", "Condition.BitOffset",
            d => AlarmOf(d).Condition.BitOffset.ToString(CultureInfo.InvariantCulture), "999", "63"),
        new("TxtBitOffset", "Condition.BitOffset",
            d => AlarmOf(d).Condition.BitOffset.ToString(CultureInfo.InvariantCulture), "-3", "0"),
    };

    private static AlarmConfig AlarmOf(WindowDepot depot) =>
        depot.AlarmGroups[0].Alarms[0];

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>可空字段：null 显示为空串（面板里"空"就是"用该严重度的默认值"）。</summary>
    private static string AllowedOf(WindowDepot depot) =>
        AlarmOf(depot).AllowedResponseMs?.ToString(CultureInfo.InvariantCulture) ?? "";

    [Fact]
    public void EverySpec_WritesTheAlarmModelAsDeclared()
    {
        using var sandbox = new DataRootSandbox("alarm-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var alarm = AlarmOf(depot);

            var window = new AlarmConfigView(depot.AlarmGroups, depot.Project);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：选中所属组的报警 ⇒ 编辑面板回填（面板没开的话下面全是空转）
                var list = ControlWiringFace.Find<ListBox>(window, "AlarmList");
                Assert.Single(list.Items);
                list.SelectedIndex = 0;
                UiTestHost.Pump(2);

                Assert.True(ControlWiringFace.Find<ScrollViewer>(window, "EditPanel").IsVisible,
                    "编辑面板没打开 ⇒ 下面的断言不成立（什么都没跑会假绿）");

                // 前提自检②：回填的必须是**模型里的**值（不是 XAML 里写死的默认值）
                Assert.Equal("液位高", ControlWiringFace.Find<TextBox>(window, "TxtAlarmName").Text);

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

                // 反面见证：编辑面板改的就是夹具手里那个对象（不是面板自己造了一份副本），
                // 且手势没有顺手改动集合结构
                Assert.Same(alarm, AlarmOf(depot));
                Assert.Single(depot.AlarmGroups);
                Assert.Single(depot.AlarmGroups[0].Alarms);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`AlarmConfigView.axaml` 的每个具名 `TextBox` 与每条 `TextChanged` 接线
    /// 都必须在本表里（反面同样成立）。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsDrivenByTheTable()
    {
        ControlWiringFace.AssertFullyDriven(
            "AlarmConfigView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 10);
    }
}
