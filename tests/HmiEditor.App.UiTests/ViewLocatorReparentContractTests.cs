using System;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「<c>ViewLocator.Build</c> 为什么不把共享视图从旧 Parent 上摘下来」的**实测证据钉子**。
///
/// <para><b>背景</b>：`docs/2026-09-15-dock-view-resolution-contract.md` §7.2 把
/// 「<c>Build</c> 不脱离旧 Parent」登记为**未验证项**，并判断"改它风险高：无法复现拖拽路径时，
/// 把偶发空白换成必崩的概率不低"。</para>
///
/// <para><b>本轮把三件事都测了</b>（headless / Avalonia 12.1）：</para>
/// <list type="number">
/// <item><b>把仍挂着的控件加到新父级会抛</b>：<c>InvalidOperationException: The control … already has
/// a visual parent ContentPresenter … while trying to add it as a child of Panel</c>。
/// ⇒ "谁负责先摘下来"是这条链路上的**硬约束**，不是整洁问题。</item>
/// <item><b>"先摘再挂"可行</b>：把 <c>ContentControl.Content</c> 置 null 后视觉父级**确实**变 null，
/// 之后可正常挂到新父级。⇒ 那份"可能必崩"的担心在**这条路径上不成立**
/// （当初很可能是把"判断父级的时机/对象"搞混了）。</item>
/// <item><b>当前仍不需要在 <c>Build</c> 里加 Detach</b>：真实拖拽回归
/// （<c>DockDragRegressionUiTests</c>）证明重组后共享视图**仍挂在可视树上且有内容** ——
/// Dock 自己完成了转移；而万一有人漏摘，症状是**抛异常**（响亮），
/// 不是归档问题单里那种"静默空白"。</item>
/// </list>
///
/// <para>结论：**保持现状**并把上面两条测量结果钉住 —— 免得后人只看登记里那句
/// "未脱离旧 Parent"就顺手加一个 Detach。</para>
/// </summary>
public class ViewLocatorReparentContractTests
{
    /// <summary>
    /// 硬约束：把**仍挂在可视树上**的控件加到新父级会抛（两种控件都验，排除"只是某个视图的特例"）。
    /// <para>若某天 Avalonia 改成自动移动，这条会红 —— 那时可回来重新评估要不要 Detach。</para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddingAStillAttachedControl_Throws(bool useRealView)
    {
        var error = UiTestHost.Run(() =>
        {
            Control child = useRealView ? new ToolboxView() : new Button { Content = "x" };

            var first = new Window { Width = 300, Height = 200, Content = child };
            first.Show();
            UiTestHost.Pump();

            var target = new Panel();
            var second = new Window { Width = 300, Height = 200, Content = target };
            second.Show();
            UiTestHost.Pump();

            Exception? ex = null;
            try { target.Children.Add(child); }
            catch (Exception e) { ex = e; }

            first.Close();
            second.Close();
            return ex;
        });

        var invalid = Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("already has a visual parent", invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 可行路线：清掉旧父级（<c>ContentControl.Content = null</c>）后视觉父级真的没了，
    /// 于是可以正常挂到新父级 —— "脱旧 Parent"技术上做得到。
    /// <para>本轮**没有**把它加进 <c>Build</c>：真实拖拽回归显示 Dock 自己完成了转移。
    /// 这条证据只说明"将来若需要，该怎么正确地做"。</para>
    /// </summary>
    [Fact]
    public void DetachThenReattach_Works_SoTheFixIsViableIfEverNeeded()
    {
        var report = UiTestHost.Run(() =>
        {
            var child = new ToolboxView();
            var first = new Window { Width = 300, Height = 200, Content = child };
            first.Show();
            UiTestHost.Pump();

            // 摘：清逻辑父级 —— 视觉父级随之消失
            if (child.Parent is ContentControl host) host.Content = null;
            var parentAfterDetach = child.GetVisualParent()?.GetType().Name ?? "<null>";

            var target = new Panel();
            var second = new Window { Width = 300, Height = 200, Content = target };
            second.Show();
            UiTestHost.Pump();

            Exception? ex = null;
            try { target.Children.Add(child); }
            catch (Exception e) { ex = e; }

            var parentAfterAttach = child.GetVisualParent()?.GetType().Name ?? "<null>";
            first.Close();
            second.Close();
            return (parentAfterDetach, parentAfterAttach, ex, inTarget: target.Children.Contains(child));
        });

        Assert.Equal("<null>", report.parentAfterDetach);   // 摘干净了
        Assert.Null(report.ex);                              // 再挂不抛
        Assert.True(report.inTarget);
        Assert.Equal("Panel", report.parentAfterAttach);
    }
}
