using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **三个库/工具箱的 `SearchBox` 驱动面**（阶段 200）—— 它们是
/// <see cref="AppXamlAttributeWiringFaceTests"/> 登记表里"3 条已驱动 / 23 条待驱动"中
/// 最便宜的那三条：接线是 **XAML 属性式**（<c>TextChanged="OnSearchTextChanged"</c>，
/// 阶段 199 才量出来的第三形态），处理器把关键词过滤到列表/画廊上。
///
/// <para><b>三条共同的坑</b>：① 处理器首行都是 <c>if (Parent == null) return;</c>
/// ⇒ 必须把视图塞进 <see cref="Window"/> 并 <c>Show()</c>，否则写值也什么都不发生；
/// ② 程序化写 <c>TextBox.Text</c> **不触发** <c>TextChanged</c> ⇒ 用
/// <see cref="ControlWiringFace.Write"/>（它会补发事件）；③ 断言**不读本地化文案**：
/// 工具箱搜的是**类型名**（`Slider` 与语言无关），库/模板夹具用自己的名字（`Alpha` / `通用`）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class LibrarySearchBoxXamlWiringTests
{
    /// <summary>把一个视图放进窗口并显示（三条处理器都要有 `Parent` 才干活）。</summary>
    private static Window Show(Control view)
    {
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        UiTestHost.Pump(3);
        return window;
    }

    [Fact]
    public void ToolboxSearchBox_FiltersTheToolList()
    {
        using var sandbox = new DataRootSandbox("xaml-search-toolbox");

        UiTestHost.Run(() =>
        {
            var view = new ToolboxView();
            var window = Show(view);
            try
            {
                var list = ControlWiringFace.Find<ListBox>(view, "ToolboxList");

                // 夹具自检：目录是构造时填的静态表（≥15 项），起点是全量
                var all = list.Items.OfType<ToolboxItem>().ToList();
                Assert.True(all.Count >= 15, $"工具箱目录只有 {all.Count} 项 —— 目录塌了或夹具没装好");
                Assert.Equal(all.Count, list.ItemCount);
                var byType = all.Count(i => i.ControlType == HmiControlType.Slider);
                Assert.True(byType >= 1, "目录里一条 Slider 都没有 —— 夹具前提不成立");

                // ① 搜**类型名**（与界面语言无关）：只剩那个类型
                ControlWiringFace.Write(view, "SearchBox", "Slider");
                Assert.Equal(byType, list.ItemCount);
                Assert.All(list.Items.OfType<ToolboxItem>(),
                    i => Assert.Equal(HmiControlType.Slider, i.ControlType));

                // ② 搜不存在的词：列表空、计数文案清空、空状态出来
                ControlWiringFace.Write(view, "SearchBox", "zzz-没有这个工具");
                Assert.Equal(0, list.ItemCount);
                Assert.False(list.IsVisible);
                Assert.True(ControlWiringFace.Find<Border>(view, "EmptyStateBorder").IsVisible);
                Assert.Equal("", ControlWiringFace.Find<TextBlock>(view, "ResultCountText").Text);

                // ③ 清空 ⇒ 回到全量（不是"卡在过滤态"）
                ControlWiringFace.Write(view, "SearchBox", "");
                Assert.Equal(all.Count, list.ItemCount);
                Assert.True(list.IsVisible);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    private static HmiResourceItem Res(string name, string display, string description) => new()
    {
        Id = name.ToLowerInvariant(),
        Name = name,
        DisplayName = display,
        Description = description,
        Tags = Array.Empty<string>(),
    };

    [Fact]
    public void ResourceLibrarySearchBox_FiltersTheGallery()
    {
        using var sandbox = new DataRootSandbox("xaml-search-reslib");

        UiTestHost.Run(() =>
        {
            var view = new ResourceLibraryView();
            var window = Show(view);
            try
            {
                // 夹具：3 条自定义资源（**Show 之后再灌** —— 装载时会从共享库恢复，别被它盖掉）
                var library = new HmiResourceLibrary();
                library.CustomResources.Add(Res("Alpha", "甲", "描述A"));
                library.CustomResources.Add(Res("Beta", "乙", "描述B"));
                library.CustomResources.Add(Res("Gamma", "丙", "描述C"));
                view.LoadLibrary(library);
                UiTestHost.Pump(2);

                var gallery = ControlWiringFace.Find<WrapPanel>(view, "Gallery");
                Assert.Equal(3, gallery.Children.Count);   // 夹具自检：起点 3 张卡

                // ① 按 Name 搜（**大小写不敏感**：`alp` 要能命中 `Alpha`）
                ControlWiringFace.Write(view, "SearchBox", "alp");
                Assert.Single(gallery.Children);

                // ② 按 DisplayName 搜（中文名也走同一条过滤）
                ControlWiringFace.Write(view, "SearchBox", "乙");
                Assert.Single(gallery.Children);

                // ③ 搜不存在的词 ⇒ 画廊空
                ControlWiringFace.Write(view, "SearchBox", "zzz-没有这个资源");
                Assert.Empty(gallery.Children);

                // ④ 清空 ⇒ 回到 3
                ControlWiringFace.Write(view, "SearchBox", "");
                Assert.Equal(3, gallery.Children.Count);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    private static ControlTemplate Tpl(string name, string category, string description) => new()
    {
        Id = name.ToLowerInvariant(),
        Name = name,
        Category = category,
        Description = description,
        Version = 1,
    };

    [Fact]
    public void FaceplateLibrarySearchBox_FiltersTheTemplateList()
    {
        using var sandbox = new DataRootSandbox("xaml-search-faceplate");

        UiTestHost.Run(() =>
        {
            var view = new FaceplateLibraryView();
            var window = Show(view);
            try
            {
                view.LoadTemplates(new[]
                {
                    Tpl("Alpha", "通用", "第一个"),
                    Tpl("Beta", "输入", "第二个"),
                    Tpl("Gamma", "通用", "第三个"),
                });
                UiTestHost.Pump(2);

                var list = ControlWiringFace.Find<ListBox>(view, "TemplateListBox");
                Assert.Equal(3, list.ItemCount);   // 夹具自检

                // ① 按 Name 搜（大小写不敏感）
                ControlWiringFace.Write(view, "SearchBox", "alp");
                Assert.Equal(1, list.ItemCount);

                // ② 按 Category 搜：两条"通用"
                ControlWiringFace.Write(view, "SearchBox", "通用");
                Assert.Equal(2, list.ItemCount);

                // ③ 两侧空白被 Trim 吃掉（处理器里 `.Trim().ToLowerInvariant()`）
                ControlWiringFace.Write(view, "SearchBox", "  beta  ");
                Assert.Equal(1, list.ItemCount);

                // ④ 搜不存在的词 ⇒ 0；清空 ⇒ 回到 3
                ControlWiringFace.Write(view, "SearchBox", "zzz-没有这个模板");
                Assert.Equal(0, list.ItemCount);
                ControlWiringFace.Write(view, "SearchBox", "");
                Assert.Equal(3, list.ItemCount);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }
}
