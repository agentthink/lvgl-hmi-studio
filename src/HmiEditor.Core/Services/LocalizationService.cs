using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HmiEditor.Core.Services;

/// <summary>
/// 本地化/国际化服务 —— 基于 JSON 资源文件，支持运行时语言切换。
/// 所有 UI 文本通过此服务获取，提供统一的本地化入口。
///
/// <para><b>为什么实现 <see cref="INotifyPropertyChanged"/>：</b>XAML 里大量使用
/// <c>{Binding [key], Source={x:Static res:AppLocalization.Service}}</c> 这类**索引器绑定**，
/// 而索引器绑定唯一的刷新途径就是源对象的属性变更通知。此前没有实现它 ⇒
/// **切了语言、界面文本原地不动**（只有走 <c>LocalizedString</c> 旁路的绑定会变），
/// <c>MainWindow</c> 还得手动重建 Dock 面板来兜底。实现之后，切语言即自动刷新所有索引器绑定。</para>
///
/// <para><b>通知的三种名字各自的作用：</b><c>"Item[]"</c> 是 .NET 对**索引器**的约定名
/// （索引器绑定的刷新依据）；<c>CurrentLanguage</c> 供直接绑定语言的控件；
/// 二者之外不再发通配通知 —— 无差别地发 <c>null</c>/空串会让每次切语言都重绑全部绑定。</para>
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    /// <summary>.NET 对索引器的约定属性名 —— 索引器绑定靠它刷新。</summary>
    private const string IndexerPropertyName = "Item[]";

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _currentLanguage = "zh-CN";
    private static readonly string[] SupportedLanguages = { "zh-CN", "en-US" };

    /// <summary>
    /// 当前语言代码（如 "zh-CN"、"en-US"）。
    /// </summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set => _currentLanguage = value;
    }

    /// <summary>
    /// 语言切换时触发，通知 UI 刷新。
    /// <para>与 <see cref="PropertyChanged"/> 并存：事件给"需要做额外动作"的宿主
    /// （例：重建 Dock 面板），属性通知给**数据绑定**。两者的消费者不同，不能互相替代。</para>
    /// </summary>
    public event EventHandler<string>? LanguageChanged;

    /// <summary>
    /// 属性变更通知 —— 索引器绑定（<c>[key]</c>）与 <see cref="CurrentLanguage"/> 绑定的刷新依据。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 获取当前语言的字符串。
    /// </summary>
    public string this[string key] => GetString(key);

    /// <summary>
    /// 获取指定键值的本地化字符串。
    /// 如果找不到，返回键名自身。
    /// </summary>
    public string GetString(string key)
    {
        if (_resources.TryGetValue(_currentLanguage, out var langStrings) &&
            langStrings.TryGetValue(key, out var value))
        {
            return value;
        }

        // fallback: 尝试英文
        if (_currentLanguage != "en-US" &&
            _resources.TryGetValue("en-US", out var enStrings) &&
            enStrings.TryGetValue(key, out var enValue))
        {
            return enValue;
        }

        return key;
    }

    /// <summary>
    /// 获取格式化的本地化字符串。
    /// 如果资源字符串中的占位符与 <paramref name="args"/> 对不上，返回未格式化的原始字符串（不抛异常）。
    ///
    /// <para>"对得上"的定义**只有一个来源**：<see cref="LocalizationPlaceholders.MatchesArgumentCount"/>。
    /// 在它被抽出来之前，这里用的是"数占位符**出现次数**"的口径 —— 那个口径会把
    /// "同一个占位符出现多次"的合法模板判成不匹配，于是**原样返回**，用户看到 <c>{1}</c> 字面量。</para>
    /// </summary>
    public string Format(string key, params object[] args) => FormatCore(key, null, args);

    /// <summary>
    /// 获取格式化的本地化字符串，使用指定文化进行数字/日期等格式化处理。
    /// </summary>
    public string Format(string key, CultureInfo culture, params object[] args) => FormatCore(key, culture, args);

    /// <summary>
    /// 两个 <see cref="Format(string, object[])"/> 重载的**共同实现** —— 少一份拷贝就少一处漂移点。
    ///
    /// <para><b>为什么"不符就原样返回"而不是抛</b>：模板是数据、实参是代码，两者对不上时
    /// 表现成"界面上有一句带花括号的话"是可接受的，崩掉不是。洞（索引不连续）那种情况
    /// 若放行到 <c>string.Format</c> 会抛 <see cref="FormatException"/> ⇒ 判据必须在这里挡住。</para>
    /// </summary>
    private string FormatCore(string key, CultureInfo? culture, object[] args)
    {
        var s = GetString(key);
        if (args.Length == 0) return s;

        if (!LocalizationPlaceholders.MatchesArgumentCount(s, args.Length))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalizationService] 模板占位符与实参不符 key='{key}'：" +
                $"模板 {LocalizationPlaceholders.DescribeIndexes(s)}，实参 {args.Length} 个。按契约原样返回。");
            return s;
        }

        return culture is null ? string.Format(s, args) : string.Format(culture, s, args);
    }

    /// <summary>
    /// 获取指定语言的字符串。
    /// </summary>
    public string GetString(string key, string language)
    {
        if (_resources.TryGetValue(language, out var langStrings) &&
            langStrings.TryGetValue(key, out var value))
        {
            return value;
        }
        return key;
    }

    /// <summary>
    /// 加载 JSON 格式的资源文件。
    /// JSON 格式示例：
    /// { "app.title": "HMI编辑器", "file.menu": "文件", ... }
    /// </summary>
    public void LoadResource(string language, string jsonContent)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);
            if (dict != null)
            {
                if (_resources.ContainsKey(language))
                {
                    foreach (var kvp in dict)
                        _resources[language][kvp.Key] = kvp.Value;
                }
                else
                {
                    _resources[language] = dict;
                }
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] 加载 {language} 资源失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从文件加载资源。
    /// </summary>
    public void LoadResourceFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var language = Path.GetFileNameWithoutExtension(filePath);
        // 提取语言代码：从 "Strings.zh-CN" → "zh-CN"
        var parts = language.Split('.');
        if (parts.Length > 1)
            language = parts[^1]; // 取最后一段

        var json = File.ReadAllText(filePath);
        LoadResource(language, json);
    }

    /// <summary>
    /// 切换当前语言。
    /// <para>不支持的语言、以及与当前相同的语言都**不产生任何通知** ——
    /// 冗余通知会让每次无谓的切换触发全界面重绑。</para>
    /// </summary>
    public void SwitchLanguage(string language)
    {
        if (!SupportedLanguages.Contains(language)) return;
        if (_currentLanguage == language) return;

        _currentLanguage = language;

        // 先发属性通知（绑定刷新），再发事件（宿主做额外动作）——
        // 顺序与既有行为一致：宿主在事件里可能读取新语言，必须已经是新值。
        // "Item[]" 是 .NET 索引器约定名；空串是"全部属性已变"的通配约定 ——
        // 索引器绑定具体认哪一个由绑定引擎决定，两个都发以覆盖 WPF/Avalonia 两种实现，
        // 且它们都只影响"绑到本服务"的绑定（切语言本就是全量文案变化）。
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerPropertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        LanguageChanged?.Invoke(this, language);
    }

    /// <summary>
    /// 获取所有支持的语言列表。
    /// </summary>
    public IReadOnlyList<string> GetSupportedLanguages() => SupportedLanguages;

    /// <summary>
    /// 获取语言对应的显示名称。
    /// </summary>
    public static string GetLanguageDisplayName(string code) => code switch
    {
        "zh-CN" => "中文",
        "en-US" => "English",
        _ => code
    };

    /// <summary>
    /// 初始化默认资源（从程序集嵌入资源加载 JSON）。
    /// 嵌入资源命名格式：HmiEditor.Core.Resources.Strings.<lang>.json
    /// 例如：Strings.zh-CN.json → 语言代码 "zh-CN"
    /// 加载失败时记录日志，不抛异常（保证应用可启动）。
    /// </summary>
    public void InitializeDefaults()
    {
        var assembly = typeof(LocalizationService).Assembly;
        string[] resourceNames;

        try
        {
            resourceNames = assembly.GetManifestResourceNames();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalizationService] 枚举嵌入资源失败: {ex.Message}");
            return;
        }

        // 匹配末尾 .Strings.<lang>.json，<lang> 不含点（BCP 47 用连字符分隔）
        var namePattern = new Regex(
            @"\.Strings\.([^.]+)\.json$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var name in resourceNames)
        {
            var match = namePattern.Match(name);
            if (!match.Success) continue;

            var language = match.Groups[1].Value;

            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[LocalizationService] 嵌入资源流为空: {name}");
                    continue;
                }

                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                var json = reader.ReadToEnd();
                LoadResource(language, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LocalizationService] 加载嵌入资源 {name} 失败: {ex.Message}");
            }
        }
    }
}
