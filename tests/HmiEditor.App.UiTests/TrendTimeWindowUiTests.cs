using System.Collections.ObjectModel;
using Avalonia.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Trend;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 趋势「时间窗口」下拉的两条判据（阶段 124）。
///
/// <para><b>① 表外值显示要诚实（#67②）</b>：旧实现只在 4 个预设档里找匹配，工程里的
/// <c>TimeWindowMs = 45000</c> 一个都不匹配 ⇒ 下拉**保持上一条趋势的选中项**，
/// 显示的不是模型值，而且点同一项不会再触发事件 ⇒ 纠正不了。
/// 现在会追加一个「自定义（N ms）」项并选中。</para>
///
/// <para><b>② 选项标签不得被静态初始化冻结</b>：旧实现把选项表写成
/// <c>static readonly</c> 并在字段初始化时 <c>Format</c> ⇒ 语言被冻结在首次使用那一刻
/// （同族缺陷见登记表 §12.3 的 <c>ConfigFieldInventory</c>）。改成本方法后每次构造都按当前语言解析。</para>
/// </summary>
[Collection("Language-global")]
public class TrendTimeWindowUiTests
{
    private static ComboBox Combo(Window window)
    {
        var combo = window.FindControl<ComboBox>("TimeWindowCombo");
        Assert.NotNull(combo);
        return combo!;
    }

    private static int CustomItemCount(ComboBox combo) =>
        combo.Items.OfType<ComboBoxItem>().Count(i => i.Tag is int ms && ms == 45000);

    [Fact]
    public void OutOfTableTimeWindow_ShowsCustomItem_MatchingTheModel()
    {
        using var sandbox = new DataRootSandbox("trend-window");
        UiTestHost.Run(() =>
        {
            var inTable = new TrendConfig { Name = "A", TimeWindowMs = 30000 };
            var outOfTable = new TrendConfig { Name = "B", TimeWindowMs = 45000 };
            var window = new TrendConfigView(new ObservableCollection<TrendConfig> { inTable, outOfTable });
            window.Show();
            UiTestHost.Pump(5);

            var combo = Combo(window);
            var trendList = window.FindControl<ListBox>("TrendList");
            Assert.NotNull(trendList);

            // 选中表外值那条趋势 ⇒ 下拉必须显示 45000（旧行为显示初始选中项 = 30 秒）
            trendList!.SelectedIndex = 1;
            UiTestHost.Pump(2);
            var selected = Assert.IsType<ComboBoxItem>(combo.SelectedItem);
            Assert.Contains("45000", selected.Content?.ToString() ?? "");
            Assert.Equal(45000, outOfTable.TimeWindowMs);   // 而且不改用户数据
            Assert.Equal(1, CustomItemCount(combo));

            // 切回表内那条 ⇒ 自定义项被移除（不残留）
            trendList.SelectedIndex = 0;
            UiTestHost.Pump(2);
            Assert.Equal(0, CustomItemCount(combo));

            // 再切回表外 ⇒ 只应有一个自定义项（不累积）
            trendList.SelectedIndex = 1;
            UiTestHost.Pump(2);
            Assert.Equal(1, CustomItemCount(combo));

            // 预设项仍按 **Tag** 写模型（下标映射已不再使用 —— 自定义项会让下标错位）
            combo.SelectedIndex = 0;
            UiTestHost.Pump(2);
            Assert.Equal(30000, outOfTable.TimeWindowMs);

            window.Close();
        });
    }

    [Fact]
    public void Labels_FollowTheCurrentLanguage_NotFrozenByStaticInit()
    {
        using var sandbox = new DataRootSandbox("trend-lang");
        UiTestHost.Run(() =>
        {
            var service = AppLocalization.Service;
            var original = service.CurrentLanguage;
            try
            {
                service.SwitchLanguage("zh-CN");
                var zhWindow = new TrendConfigView(new ObservableCollection<TrendConfig>
                    { new TrendConfig { Name = "T", TimeWindowMs = 30000 } });
                zhWindow.Show();
                UiTestHost.Pump(3);
                var zhFirst = FirstItemText(zhWindow);
                zhWindow.Close();

                service.SwitchLanguage("en-US");
                var enWindow = new TrendConfigView(new ObservableCollection<TrendConfig>
                    { new TrendConfig { Name = "T", TimeWindowMs = 30000 } });
                enWindow.Show();
                UiTestHost.Pump(3);
                var enFirst = FirstItemText(enWindow);
                enWindow.Close();

                Assert.Contains("秒", zhFirst);
                // 静态数组会把这里冻成 "30 秒"（类型初始化只跑一次）
                Assert.Equal("30s", enFirst);
            }
            finally
            {
                service.SwitchLanguage(original);   // 静态单例：不还原会污染其它 UI 测试
            }
        });
    }

    private static string FirstItemText(Window window) =>
        (Combo(window).Items[0] as ComboBoxItem)?.Content?.ToString() ?? "";
}
