using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 视图装载冒烟测试：把每个 <c>HmiEditor.App.Views</c> 下的 UserControl 真正构造出来，
/// 跑一次布局，断言没有异常、可视树非空。
///
/// <para>这层覆盖是以前完全缺失的：控件模板缺资源（<c>ControlTheme</c> 找不到、模板里的
/// 绑定目标不存在）、构造期初始化顺序出错，过去只有在人工启动编辑器点到那个面板时
/// 才会暴露（甚至直接崩在全局异常处理器里）。headless 让这类问题在 CI 里立刻可见。</para>
///
/// <para><b>两处曾被高估的能力（2026-09-20 更正）：</b></para>
/// <list type="number">
///   <item>本层**接不住** <c>{StaticResource}</c> 键缺失。阶段 47 实测定性：键缺失落在控件属性上
///     是**静默跳过赋值**（属性回落主题默认，布局照样跑完，不抛不报）。静态扫描那一层才是它的
///     判据，见 <c>XamlResourceKeyClosureTests</c>。</item>
///   <item>本层**收不到窗口视图**。清单口径是 <c>typeof(UserControl).IsAssignableFrom</c>，
///     而窗口视图（<c>ChromeWindow</c> 子类 + <c>SplashWindow</c>）都继承 <c>Window</c> —— 
///     一个都进不来。那部分由 <see cref="WindowViewSmokeUiTests"/> 负责，
///     并由那边的 <c>EveryViewType_IsInSomeSmokeSurface</c> 把两个面并起来守。</item>
/// </list>
/// </summary>
public class ViewSmokeUiTests
{
    /// <summary>被测视图类型清单：全部公开的无参 UserControl 视图。</summary>
    public static IEnumerable<object[]> ViewTypes() =>
        ViewCatalog().Select(t => new object[] { t.Name });

    /// <summary>
    /// 面的推导。**刻意是 <c>internal</c>**：<see cref="WindowViewSmokeUiTests"/> 的
    /// 面覆盖守卫需要把两个面并起来看，否则"还有没有第三类视图"没人问。
    /// </summary>
    internal static IReadOnlyList<Type> ViewCatalog()
    {
        var asm = typeof(HmiEditor.App.Views.FaceplateLibraryView).Assembly;
        return asm.GetTypes()
            .Where(t => t.Namespace is not null
                     && t.Namespace.StartsWith("HmiEditor.App.Views", StringComparison.Ordinal)
                     && !t.IsAbstract
                     && typeof(UserControl).IsAssignableFrom(t)
                     && t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void ViewCatalog_IsNotEmpty()
    {
        // 守卫：清单推导若失效（例如命名空间被改），下面的逐视图测试会变成空跑。
        var catalog = ViewCatalog();
        // ⚠ 下界随视图数**一起**降：阶段 137 删掉两个死视图（`VariableBrowserView` / `PrototypeCanvas`，
        //   登记 `#71` / `#73`）⇒ 15 → **13**。下界的用途是"清单推导失效"的空跑守卫（命名空间一改名
        //   下面就变成空跑），所以它必须跟着**真实**的面走，而不是留一个偏高的数在红着。
        Assert.True(catalog.Count >= 13, $"只推导出 {catalog.Count} 个视图，清单可能失效（当前应为 13 个 UserControl 视图）");
    }

    [Theory]
    [MemberData(nameof(ViewTypes))]
    public void View_LoadsAndLaysOut(string typeName)
    {
        var type = ViewCatalog().Single(t => t.Name == typeName);

        var result = UiTestHost.Run(() =>
        {
            var view = (UserControl)Activator.CreateInstance(type)!;
            var window = new Window { Width = 1024, Height = 640, Content = view };
            window.Show();
            UiTestHost.Pump();

            // 触发布局：Laoyut 期异常（NaN 尺寸、无法解析的模板）会在这里抛出。
            window.Measure(new Size(1024, 640));
            window.Arrange(new Rect(0, 0, 1024, 640));
            UiTestHost.Pump();

            var visualCount = view.GetVisualDescendants().Count();
            var hasName = view.GetVisualDescendants().OfType<Control>().Any(c => c.Name is not null);
            window.Close();
            return (visualCount, hasName);
        });

        Assert.True(result.visualCount > 0, $"{typeName} 构造后可视树为空");
        Assert.True(result.hasName, $"{typeName} 的 XAML 似乎未加载（没有任何具名控件）");
    }
}
