using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HmiEditor.App.Resources;

/// <summary>
/// 可绑定到 XAML 的本地化字符串包装。实现 INotifyPropertyChanged，
/// 在 LocalizationService.LanguageChanged 触发时通过 ValueChanged 回调通知属性变更，
/// 使 Avalonia 的 {Binding Value} 自动刷新。
///
/// 用法：
///   xmlns:res="clr-namespace:HmiEditor.App.Resources"
///   Text="{Binding Value, Source={x:Static res:AppLocalization.Get('menu.file')}}"
/// 或通过 markup extension 简化（见 LocalizedBindingExtension）。
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    private string _value;
    private readonly string _key;

    public LocalizedString(string key)
    {
        _key = key;
        _value = AppLocalization.Service[key];
        AppLocalization.Service.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>当前语言下的字符串值。</summary>
    public string Value
    {
        get => _value;
        private set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    /// <summary>原始 key（调试用）。</summary>
    public string Key => _key;

    private void OnLanguageChanged(object? sender, string language)
    {
        Value = AppLocalization.Service[_key];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
