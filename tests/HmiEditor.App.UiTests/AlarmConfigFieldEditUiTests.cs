using Avalonia.Controls;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「报警组配置」对话框的**字段编辑**判据（阶段 120）。
///
/// <para>背景：`OnAlarmFieldChanged` / `OnConditionTypeChanged` 在全仓、全 git 历史里
/// 只有声明那一行 —— 编辑面板的十几个输入控件**一个都没订阅**。后果是用户改
/// 名称 / 描述 / 严重级别 / 变量 / 条件类型 / 阈值 / 死区 / 延时 / 位偏移 / 期望值 /
/// 启用 / 需确认**一个都不落模型**，`AlarmConfigChanged` 也不触发
/// （主窗口 `OpenAlarmConfigDialog` 靠它把工程标脏）⇒ 这个对话框实际上是"能看不能改"。</para>
///
/// <para>判据形状：走**生产构造路径**（真实 `AlarmConfigView` + 真实 `AlarmGroup`），
/// 先选中组与报警让面板回填，再改控件，最后从**模型**上读结果。前置自检保证
/// 「面板真的开了、回填的真是模型值」—— 少了它，"什么都没跑"会以假绿通过。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AlarmConfigFieldEditUiTests
{
    private static T Find<T>(Window window, string name) where T : Control
    {
        var found = window.FindControl<T>(name);
        Assert.NotNull(found);
        return found!;
    }

    [Fact]
    public void EditingFields_WritesThroughToTheModel_AndRaisesChanged()
    {
        using var sandbox = new DataRootSandbox("alarmfield");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var alarm = depot.AlarmGroups[0].Alarms[0];

            var window = new AlarmConfigView(depot.AlarmGroups, depot.Project);
            var changedCount = 0;
            window.AlarmConfigChanged += (_, _) => changedCount++;
            window.Show();
            UiTestHost.Pump(5);

            // 前置自检①：默认选中的那个组必须已经把右列填好（组高亮而右列为空是旧缺陷）
            Assert.Single(Find<ListBox>(window, "AlarmList").Items);

            // 选中报警 → 编辑面板回填
            Find<ListBox>(window, "AlarmList").SelectedIndex = 0;
            UiTestHost.Pump(2);

            // 前置自检②：面板必须真的打开、回填的必须是**模型里的**值
            Assert.True(Find<ScrollViewer>(window, "EditPanel").IsVisible,
                "编辑面板没打开 ⇒ 下面的断言不成立（什么都没跑会假绿）");
            Assert.Equal("液位高", Find<TextBox>(window, "TxtAlarmName").Text);

            var before = changedCount;

            Find<TextBox>(window, "TxtAlarmName").Text = "液位高高";
            Find<TextBox>(window, "TxtThreshold").Text = "123.5";
            Find<CheckBox>(window, "ChkEnabled").IsChecked = false;
            // ISA-18.2 职责字段（阶段 266）：三个新框也必须写回模型。
            // ⚠ 必须走 ControlWiringFace.Write（写文本 **＋ 补发 TextChanged**）：程序化 `TextBox.Text =`
            // 不触发 TextChanged（阶段 187 实测），只靠复选框那一跳会把这三框读成空串 ——
            // 这条路径上"改了却没落模型"正是这个对话框阶段 120 的原病。
            ControlWiringFace.Write(window, "TxtAlarmCause", "冷却水阀关断");
            ControlWiringFace.Write(window, "TxtAlarmResponse", "打开备用泵");
            ControlWiringFace.Write(window, "TxtAlarmConsequence", "反应釜超温");
            UiTestHost.Pump(2);

            Assert.Equal("液位高高", alarm.Name);
            Assert.Equal(123.5, alarm.Condition.Threshold, 3);
            Assert.False(alarm.IsEnabled);
            Assert.Equal("冷却水阀关断", alarm.Cause);
            Assert.Equal("打开备用泵", alarm.Response);
            Assert.Equal("反应釜超温", alarm.Consequence);
            Assert.True(changedCount > before,
                "AlarmConfigChanged 没有触发 ⇒ 主窗口不会把工程标脏、保存时改动会丢");

            window.Close();
        });
    }
}
