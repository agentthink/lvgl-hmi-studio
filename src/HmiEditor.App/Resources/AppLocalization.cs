using System;
using System.Collections.Generic;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Resources;

/// <summary>
/// 全局本地化服务静态访问点。
/// </summary>
public static class AppLocalization
{
    private static readonly LocalizationService _service = Create();

    /// <summary>
    /// 按 key 复用的包装实例缓存。
    /// <para><b>为什么必须缓存：</b><see cref="LocalizedString"/> 在构造时订阅
    /// <see cref="LocalizationService.LanguageChanged"/>，而事件的持有者是**静态单例** ——
    /// 每次调用都 <c>new</c> 一个会让订阅者只增不减（在 <c>DataTemplate</c> 里尤其严重：
    /// 每个实例化的行都会加一个永不释放的订阅）。缓存后订阅者数量上界 = 本地化键数量，
    /// 与"创建次数"无关。</para>
    /// </summary>
    private static readonly Dictionary<string, LocalizedString> Cache = new(StringComparer.Ordinal);

    /// <summary>底层本地化服务（用于直接调用 Format / 索引器等）。</summary>
    public static LocalizationService Service => _service;

    /// <summary>
    /// 返回一个 <see cref="LocalizedString"/>，可在 XAML 中绑定到其
    /// <see cref="LocalizedString.Value"/>。语言切换时自动通知属性变更，实现 UI 刷新。
    /// <para>同一 <paramref name="key"/> 复用同一实例（见 <see cref="Cache"/>）。</para>
    /// </summary>
    public static LocalizedString Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var created = new LocalizedString(key);
            Cache[key] = created;
            return created;
        }
    }

    /// <summary>当前缓存了多少个 key 的包装实例（诊断与测试用）。</summary>
    internal static int CachedKeyCount
    {
        get { lock (Cache) return Cache.Count; }
    }

    private static LocalizationService Create()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();
        return svc;
    }
}
