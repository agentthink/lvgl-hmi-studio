namespace HmiEditor.Core;

/// <summary>
/// HMI Editor 版本信息中心。
/// 所有版本字符串统一从此处引用。
/// </summary>
public static class VersionInfo
{
    /// <summary>主版本号</summary>
    public const string Version = "2.1.0";

    /// <summary>完整的版本显示字符串</summary>
    public const string DisplayVersion = $"HMI Editor v{Version}";

    /// <summary>版本后缀（预览版 = -preview, RC = -rc, 正式版 = ""）</summary>
    public const string Suffix = "";

    /// <summary>完整版本标识</summary>
    public static string FullVersion => $"{Version}{Suffix}";
}
