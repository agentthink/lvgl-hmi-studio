using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;

namespace HmiEditor.App.Resources;

/// <summary>
/// XAML 本地化标记扩展：<c>Text="{res:Loc menu.file}"</c>。
///
/// <para><b>为什么需要它（而不是直接用索引器绑定）：</b>全仓 XAML 原本一律写
/// <c>{Binding [key], Source={x:Static res:AppLocalization.Service}}</c>。**索引器绑定在
/// Avalonia 里不可观察** —— 索引器节点不订阅 <c>INotifyPropertyChanged</c>，
/// 所以无论 <see cref="LocalizationService"/> 发什么通知，切换语言后这些文本都**原地不动**
/// （实测：同一个 headless 宿主里，绑 <c>LocalizedString.Value</c> 会刷新、绑 <c>[key]</c> 不会）。
/// 此前只能靠 <c>MainWindow.OnLanguageChanged</c> 手动重建 Dock 面板兜底，
/// 于是**菜单栏/工具栏这类不做重建的区域切语言后一直是旧语言**。</para>
///
/// <para>本扩展把绑定改成"绑到一个会通知的对象"：<c>ProvideValue</c> 返回一个指向
/// <see cref="AppLocalization.Get"/> 所给实例的 <see cref="LocalizedString.Value"/> 的绑定，
/// 于是刷新走 <see cref="LocalizedString"/> 的 INPC，与索引器无关。</para>
///
/// <para><b>实例复用：</b>源对象由 <see cref="AppLocalization.Get"/> 按 key 缓存，
/// 因此同一个 key 的所有控件共享一个包装实例 —— 既不会泄漏，也不会因控件数量放大订阅。</para>
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    /// <summary>本地化键（如 <c>menu.file</c>）。</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    /// <summary>返回绑定到该 key 的 <see cref="LocalizedString.Value"/> 的绑定。</summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            throw new InvalidOperationException("LocExtension 需要一个本地化键：{res:Loc menu.file}");

        return new Binding(nameof(LocalizedString.Value))
        {
            Source = AppLocalization.Get(Key),
            Mode = BindingMode.OneWay,
        };
    }
}
