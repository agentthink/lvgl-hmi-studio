using System;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 构建/运行对话框里"框起来"的日志行：**宽度按显示列数算**（CJK 全角 2 列）。
///
/// <para><b>为什么要专门测它</b>：原实现把右侧空格 padding 直接写进字符串，而中文是双列字符 ——
/// 实测那条 <c>[已取消]…</c> 的内容宽 47 列、上下边框只有 44 列，**右边框比上边框多出 3 列
/// （本来就是歪的）**；而且文案一改就继续歪，没有任何东西会报。改成按列宽计算后，
/// 这三条把"框得住"这件事钉下来，顺带挡住"译文过长把框撑破"。</para>
/// </summary>
public class BuildRunDialogBoxTests
{
    private const int Inner = 44;
    private const int FrameWidth = Inner + 2;   // 两侧各一条竖线

    [Theory]
    [InlineData("[已取消] 操作被用户中止")]
    [InlineData("[Cancelled] Operation aborted by the user")]
    [InlineData("混合 mixed 文本 text")]
    [InlineData("")]
    public void BoxedContentLine_MatchesTheFrameWidth(string text)
    {
        Assert.Equal(FrameWidth, BuildRunDialog.DisplayWidth(BuildRunDialog.BoxedContentLine(text, Inner)));
    }

    [Fact]
    public void DisplayWidth_CountsCjkAsTwoColumns()
    {
        Assert.Equal(2, BuildRunDialog.DisplayWidth("中"));
        Assert.Equal(1, BuildRunDialog.DisplayWidth("a"));
        Assert.Equal(5, BuildRunDialog.DisplayWidth("中a中"));   // 2 + 1 + 2
        Assert.Equal(48, BuildRunDialog.DisplayWidth(new string('─', 48)));
    }

    /// <summary>
    /// 两语言的实际文案都必须装得进框里 —— 否则 padding 变负、框被撑破
    /// （<see cref="BuildRunDialog.BoxedContentLine"/> 会把负值夹到 0，于是竖线错位）。
    /// </summary>
    [Fact]
    public void LocalizedBoxedTexts_FitInsideTheFrame()
    {
        foreach (var lang in new[] { "zh-CN", "en-US" })
        {
            var text = AppLocalization.Service.GetString(Lang.BuildrunLogCancelled, lang);
            var width = BuildRunDialog.DisplayWidth(text);
            Assert.True(width <= Inner - 2,
                $"{lang} 的框内文案宽 {width} 列，超过可用 {Inner - 2} 列 —— 框会被撑破");
        }
    }
}
