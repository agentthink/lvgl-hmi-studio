using Avalonia.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Faceplate 参数区刷新的**行为**判据 —— 守的是「**静态区不被误删**」这一半。
///
/// <para>背景（阶段 78）：这段刷新原先写的是
/// <c>while (FaceplateParamPanel.Children.Count &gt; 2) RemoveAt(2);</c> ——
/// 那个字面 <c>2</c> 把「前两个子元素是 XAML 里声明的固定表头」编码成了**位置契约**。
/// 后果：往 XAML 的静态区再加一个元素（分隔线、说明文字…），它会在**每次刷新**时被静默删掉。</para>
///
/// <para>结构（修后）：<c>FaceplateParamPanel</c> = 两个静态表头 + **一个动态容器
/// <c>FaceplateParamHost</c>**；刷新只动 Host。所以静态区的子元素个数**恒为 3**，
/// 与参数个数无关 —— 本条判据把这一点也钉住。</para>
///
/// <para>为什么**不能**只靠静态形状判据（<c>scripts/check-index-based-control-lookup.py</c>）：
/// 形状判据只认**字面整数**下标，等价改写
/// （<c>for (var i = X.Children.Count - 1; i &gt;= 2; i--) X.Children.RemoveAt(i);</c>）
/// 会以**绿**通过而行为照样坏。这里往静态区注入一个探针元素、真驱动一次刷新，
/// 断言探针**存活** —— 那正是形状判据看不见的那一半（阶段 78 的 NC2 实测了这一对）。</para>
/// </summary>
public class FaceplateParamRefreshUiTests
{
    private const string TemplateId = "tpl-test";

    /// <summary>静态区子元素个数：两个 XAML 表头 + 动态容器。</summary>
    private const int StaticChildCount = 3;

    private sealed record Fixture(Window Window, PropertyPanelView Panel);

    /// <summary>造一个有 <paramref name="parameterCount"/> 个参数的模板，交给真实面板。</summary>
    private static Fixture Host(int parameterCount = 2)
    {
        var template = new ControlTemplate
        {
            Id = TemplateId,
            Name = "泵模板",
            Version = 3,
            Description = "测试用模板",
        };
        var names = new[] { "speed", "tag", "mode" };
        for (var i = 0; i < parameterCount; i++)
        {
            template.Parameters.Add(new TemplateParameter
            {
                Name = names[i],
                DisplayName = $"参数{i}",
                Direction = i == 0 ? ParameterDirection.InOut : ParameterDirection.Input,
                Type = i == 1 ? ParameterType.String : ParameterType.Number,
                DefaultValue = i == 1 ? "P1" : "100",
            });
        }

        var manager = new TemplateManager();
        manager.AddPredefined(new[] { template });

        var panel = new PropertyPanelView();
        panel.SetTemplateManager(manager);   // 生产入口

        var window = new Window { Width = 900, Height = 700, Content = panel };
        window.Show();
        UiTestHost.Pump();
        return new Fixture(window, panel);
    }

    /// <summary>带 <c>TemplateId</c> 的控件就会被当作 Faceplate 实例（类型枚举里没有 Faceplate 成员）。</summary>
    private static HmiControl MakeFaceplateInstance() => new()
    {
        Id = "fp1",
        Type = HmiControlType.Label,
        Name = "fp1",
        Text = "",
        TemplateId = TemplateId,
    };

    private static T Find<T>(PropertyPanelView panel, string name) where T : Control
    {
        var found = panel.FindControl<T>(name);
        Assert.NotNull(found);
        return found!;
    }

    /// <summary>
    /// 探针**自检前置**：先证明这次绑定真的走进了 faceplate 分支。
    /// 少了这一步，「刷新根本没发生」会以「静态区没被动过」的**假绿**呈现 ——
    /// 探针存活断言会因为什么都没跑而通过。
    /// </summary>
    private static void AssertRefreshActuallyRan(PropertyPanelView panel)
    {
        Assert.True(Find<Expander>(panel, "FaceplateParamExpander").IsVisible,
            "探针自检失败：扩展区块没显示 ⇒ 这次绑定没走进 faceplate 分支，下面的断言不成立");
        var title = Find<TextBlock>(panel, "FaceplateTemplateName").Text;
        Assert.NotNull(title);
        Assert.Contains("泵模板", title!);
    }

    private static StackPanel StaticPanel(PropertyPanelView panel) =>
        Find<StackPanel>(panel, "FaceplateParamPanel");

    private static StackPanel DynamicHost(PropertyPanelView panel) =>
        Find<StackPanel>(panel, "FaceplateParamHost");

    /// <summary>
    /// 主判据：静态区**多出一个元素**时，刷新不得把它删掉。
    ///
    /// <para>探针元素模拟「后来有人往 XAML 的静态区加了个分隔线」这一演进 ——
    /// 旧写法（<c>while (Count &gt; 2) RemoveAt(2)</c>）会把它当成旧编辑器删掉。</para>
    /// </summary>
    [Fact]
    public void AThirdStaticChild_SurvivesARefresh()
    {
        UiTestHost.Run(() =>
        {
            var f = Host();
            var staticPanel = StaticPanel(f.Panel);

            var probe = new Border { Name = "StaticProbe78" };
            staticPanel.Children.Add(probe);

            f.Panel.BindControl(MakeFaceplateInstance());
            UiTestHost.Pump();

            AssertRefreshActuallyRan(f.Panel);

            // 静态区：两个 XAML 表头 + 动态容器 + 探针，一个都不能少
            Assert.Contains(probe, staticPanel.Children);
            Assert.Contains(Find<TextBlock>(f.Panel, "FaceplateTemplateName"), staticPanel.Children);
            Assert.Contains(Find<TextBlock>(f.Panel, "FaceplateTemplateDesc"), staticPanel.Children);
            Assert.Equal(StaticChildCount + 1, staticPanel.Children.Count);

            // 且动态编辑器**没有**混进静态区（它们只该待在 Host 里）
            Assert.Equal(2, DynamicHost(f.Panel).Children.Count);

            f.Window.Close();
        });
    }

    /// <summary>重复绑定不累积：刷新是「清空 + 重建」，不是「追加」。</summary>
    [Fact]
    public void Rebind_DoesNotAccumulateDynamicEditors()
    {
        UiTestHost.Run(() =>
        {
            var f = Host();
            var staticPanel = StaticPanel(f.Panel);
            var probe = new Border { Name = "StaticProbe78b" };
            staticPanel.Children.Add(probe);

            f.Panel.BindControl(MakeFaceplateInstance());
            UiTestHost.Pump();
            AssertRefreshActuallyRan(f.Panel);
            Assert.Equal(2, DynamicHost(f.Panel).Children.Count);

            f.Panel.BindControl(MakeFaceplateInstance());
            UiTestHost.Pump();
            AssertRefreshActuallyRan(f.Panel);

            Assert.Equal(2, DynamicHost(f.Panel).Children.Count);   // 不是 4
            Assert.Contains(probe, staticPanel.Children);           // 两轮刷新后仍在
            Assert.Equal(StaticChildCount + 1, staticPanel.Children.Count);

            f.Window.Close();
        });
    }

    /// <summary>零参数模板：提示文案也要进**动态区**，不能挤进静态区。</summary>
    [Fact]
    public void TemplateWithoutParameters_PutsTheHintInTheDynamicHost()
    {
        UiTestHost.Run(() =>
        {
            var f = Host(parameterCount: 0);

            f.Panel.BindControl(MakeFaceplateInstance());
            UiTestHost.Pump();

            AssertRefreshActuallyRan(f.Panel);

            var host = DynamicHost(f.Panel);
            Assert.Single(host.Children);                       // 恰好一条「无参数」提示
            Assert.IsType<TextBlock>(host.Children[0]);
            Assert.Equal(StaticChildCount, StaticPanel(f.Panel).Children.Count);

            f.Window.Close();
        });
    }
}
