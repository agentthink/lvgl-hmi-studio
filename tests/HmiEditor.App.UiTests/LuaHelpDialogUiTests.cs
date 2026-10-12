using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **「Lua 帮助」窗的判据**（阶段 73 立；阶段 75 补上关闭接线那一维）。
///
/// <para><b>这块面为什么此前是 0 覆盖：</b>帮助窗是 <c>ScriptEditorView</c> 里**手搓**出来的
/// （不是 XAML 文件），而它在 Avalonia.Headless 下 <c>Show()</c> 就会让布局不收敛、
/// 把整个测试进程挂住（<c>EXIT=124</c>）⇒ <c>DialogInnerInvocationUiTests</c> 只能把
/// <c>BtnHelp</c> 登记进 <c>Never</c> 桶。登记表 §5 #40 一直写着「触发条件未定」，
/// 阶段 73 把它测清了 —— 触发条件是「**垂直可滚动**的 <c>ScrollViewer</c> 包着
/// <c>TextWrapping.Wrap</c> 的多行 <c>TextBlock</c>、在**首次布局**里定不下来」，
/// 与内容是否溢出视口无关，且**真平台（X11/Skia）下不挂** ⇒ 是 headless 的限制，
/// 不是产品缺陷。取证与两侧对照：<c>docs/2026-09-22-headless-scrollviewer-wrap-hang.md</c>。</para>
///
/// <para><b>判据形状：</b>走 <c>ScriptEditorView.CreateLuaHelpDialog()</c> —— 阶段 73 把
/// 「造窗」与「显示窗」拆开留下的那个接缝（纯重构，<c>ShowLuaHelp()</c> 行为不变）。
/// **只构造、不 Show** ⇒ 不碰那条会挂死的布局路径，于是标题 / 正文 / 关闭按钮文案都能真的核对。</para>
///
/// <para><b>关闭按钮的接线：阶段 73 判它「测不到」，阶段 75 把这条论断推翻了。</b>
/// 不需要 <c>Show()</c> 也能观察它，靠两条实测事实（探针 3 条，读数见
/// <c>docs/project-status.md</c> 阶段 75）：
/// <list type="number">
///   <item>从未 <c>Show()</c> 过的 <c>Window.Close()</c> **不抛**，且**会**触发 <c>Closed</c>；</item>
///   <item>未 attach 到可视树的按钮上 <c>RaiseEvent(Button.ClickEvent)</c> **会**触发
///         直接挂上去的 handler（<c>Click +=</c> 就是这种）。</item>
/// </list>
/// ⇒ 「点那个按钮到底有没有调 <c>Close()</c>」是可判定的：<c>Closed</c> 只有 <c>Close()</c> 会触发。</para>
///
/// <para><b>不抓什么（重要，别把它读成全覆盖）：</b>
/// ① 不判「真显示出来后窗确实消失了」——那要走 <c>ShowDialog</c>，即会挂死那一步；
/// 本类证明的是**接线通到 <c>Close()</c>**，而 <c>Close()</c> 自身的行为是框架的。
/// ② **测不出「重复接线」**：<c>Close()</c> 在已关闭的窗上是幂等的 ⇒ 接两遍也只关一次、
/// <c>Closed</c> 只响一次（这一族真正的坑见登记表 #43，那边是因为「重复会开 4 个窗」才可见）；
/// 对**这个**窗，重复接线的用户可见后果本来就是零。
/// ③ 布局 / 尺寸 / 文本换行结果不判（同 ①）。④ 真实命中测试（不做像素点击）。
/// ⑤ 只读语言 ⇒ 与改语言的用例并行会读到切换中的文案，所以进同一 collection。</para>
/// </summary>
[Collection("Language-global")]
public class LuaHelpDialogUiTests
{
    /// <summary>
    /// 走「内容属性」的递归 —— **不要**换成 <c>GetVisualDescendants()</c>：
    /// 那个要求控件已 attach 到可视树（要 <c>Show()</c>，正是会挂死的一步），
    /// 而未显示窗口的模板也没应用。
    ///
    /// <para>也**不能**用 <c>GetLogicalDescendants()</c>：<c>Panel.Children</c> 的元素不进逻辑树
    /// （项目的既有结论），<c>Grid</c> 里的东西一个都枚举不到。</para>
    /// </summary>
    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;

        switch (root)
        {
            case ContentControl { Content: Control c }:
                foreach (var d in Descendants(c)) yield return d;
                break;
            case Decorator { Child: Control dc }:
                foreach (var d in Descendants(dc)) yield return d;
                break;
            case Panel p:
                foreach (var child in p.Children)
                    foreach (var d in Descendants(child)) yield return d;
                break;
        }
    }

    [Fact]
    public void HelpDialog_TitleAndBodyComeFromTheDictionary()
    {
        UiTestHost.Run(() =>
        {
            var dialog = ScriptEditorView.CreateLuaHelpDialog();

            // ① 标题
            Assert.Equal(AppLocalization.Service[Lang.ScriptHelpTitle], dialog.Title);

            // ② 正文：字典里那条帮助文案真的落进了某个 TextBlock。
            //    两个自检先挡住「判据自己的输入是坏的」：
            //      · 文案不能是空的（否则后面的 Assert.Contains 恒真）
            //      · 文案不能等于键名本身（字典缺键时回落成键名的经典缺陷）
            var helpText = AppLocalization.Service[Lang.ScriptLuaHelpText];
            Assert.False(string.IsNullOrWhiteSpace(helpText), "帮助文案本身是空的");
            Assert.NotEqual(Lang.ScriptLuaHelpText, helpText);

            var bodies = Descendants(dialog).OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains(helpText, bodies);
        });
    }

    [Fact]
    public void HelpDialog_HasExactlyOneButtonLabelledClose()
    {
        UiTestHost.Run(() =>
        {
            var dialog = ScriptEditorView.CreateLuaHelpDialog();

            var closeLabel = AppLocalization.Service[Lang.GenClose];
            Assert.False(string.IsNullOrWhiteSpace(closeLabel), "关闭按钮文案是空的");
            Assert.NotEqual(Lang.GenClose, closeLabel);

            // 按**文案**找，不按 grid.Children 的下标找：下标一变就会假红，
            // 而且那是复述产品实现（守卫与被守对象同源），抓不出接线错误。
            var closes = Descendants(dialog).OfType<Button>()
                .Where(b => (b.Content as string) == closeLabel)
                .ToList();

            Assert.Single(closes);
        });
    }

    /// <summary>
    /// 关闭按钮**真的接上了** <c>Close()</c>（阶段 75）。
    ///
    /// <para>这一维在阶段 73 被写成「测不到 —— 要把窗口真显示出来才观察得到，而那正是会挂死的一步」。
    /// **阶段 75 推翻了它**：headless 里**不需要 <c>Show()</c>** 也能观察到这条接线，见类注释里的两条实测事实。</para>
    ///
    /// <para>为什么是「按文案找到的按钮」而不是「产品里的那个下标」：守卫不与被守对象同源 ——
    /// 产品怎么定位那个按钮是它自己的事（阶段 75 顺带把 <c>grid.Children[1]</c> 这个**位置下标**
    /// 换成了对按钮实例的直接引用），判据只认「界面上那个写着『关闭』的按钮，点下去会不会关窗」。</para>
    /// </summary>
    [Fact]
    public void HelpDialog_CloseButtonIsActuallyWiredToClose()
    {
        UiTestHost.Run(() =>
        {
            var dialog = ScriptEditorView.CreateLuaHelpDialog();

            var closed = 0;
            dialog.Closed += (_, _) => closed++;

            var closeLabel = AppLocalization.Service[Lang.GenClose];
            var closes = Descendants(dialog).OfType<Button>()
                .Where(b => (b.Content as string) == closeLabel)
                .ToList();
            Assert.Single(closes);

            // 内置负控：**只构造、不点**时必须一条都没关 —— 否则下面的 Assert.Equal(1, …) 是恒真的
            Assert.Equal(0, closed);

            closes[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // `Closed` 只有 `Close()` 会触发 ⇒ 这个计数就是「那条 handler 调没调 Close()」的等价物
            Assert.Equal(1, closed);
        });
    }
}
