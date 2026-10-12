using System.Text.RegularExpressions;

namespace HmiEditor.Core.Services;

/// <summary>
/// **固件版本标记的唯一解析入口**（登记表 §4 第 29 项「版本标记四者对应」的底座）。
///
/// <para>四个标记散落在两个 C 头文件里，此前只有测试里的一份私有解析器在读它们；
/// 发布包清单（<c>tools/ReleasePackager</c>）也要读同一批值 —— 再抄一份正则就等于
/// 让「发布包里写的版本」和「守卫认定的版本」各说各话。故把解析收成这里一处，
/// 测试与打包共用。</para>
///
/// <para>纯文本解析：输入是头文件**内容**，不做任何文件 IO（路径布局由调用方负责）。</para>
/// </summary>
public static class FirmwareVersionMarkers
{
    /// <summary>固件运行时头文件（<c>hmi_runtime.h</c>）里读出的标记。</summary>
    public sealed record RuntimeHeader(string VersionString, string TargetId);

    /// <summary>固件 ABI 头文件（<c>hmi_ui_api.h</c>）里读出的标记。</summary>
    public sealed record ApiHeader(uint AbiVersion, string LvglVersion);

    /// <summary>取 C 宏的字面量值（支持 <c>0x..</c> 与十进制，允许 <c>u</c> 后缀）。</summary>
    /// <exception cref="InvalidOperationException">头文件里找不到该宏。</exception>
    public static uint NumericMacro(string headerText, string name)
    {
        var m = Regex.Match(headerText, $@"#define\s+{name}\s+(0x[0-9A-Fa-f]+|\d+)\s*u?", RegexOptions.None);
        if (!m.Success)
            throw new InvalidOperationException($"头文件里找不到宏 {name}");
        var raw = m.Groups[1].Value;
        return raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(raw[2..], 16)
            : uint.Parse(raw);
    }

    /// <summary>取 C 字符串宏的值（<c>#define X "..."</c>）。</summary>
    /// <exception cref="InvalidOperationException">头文件里找不到该宏。</exception>
    public static string StringMacro(string headerText, string name)
    {
        var m = Regex.Match(headerText, $@"#define\s+{name}\s+""([^""]*)""");
        if (!m.Success)
            throw new InvalidOperationException($"头文件里找不到字符串宏 {name}");
        return m.Groups[1].Value;
    }

    /// <summary><c>0x905</c> → <c>"9.5"</c>（与 <c>LVGL_VERSION_MAJOR*256+MINOR</c> 同构）。</summary>
    public static string PackedLvglVersion(uint packed) => $"{packed >> 8}.{packed & 0xFF}";

    /// <summary>解析 <c>hmi_runtime.h</c>：版本串 + 目标 ID。</summary>
    public static RuntimeHeader ParseRuntimeHeader(string headerText) => new(
        VersionString: StringMacro(headerText, "HMI_RUNTIME_VERSION_STR"),
        TargetId: StringMacro(headerText, "HMI_TARGET_ID"));

    /// <summary>解析 <c>hmi_ui_api.h</c>：ABI 版本 + 运行时自带的 LVGL 版本。</summary>
    public static ApiHeader ParseApiHeader(string headerText) => new(
        AbiVersion: NumericMacro(headerText, "HMI_UI_ABI_VERSION"),
        LvglVersion: PackedLvglVersion(NumericMacro(headerText, "HMI_UI_LVGL_VERSION")));

    /// <summary>版本串 == "MAJOR.MINOR.PATCH" 的自洽判据（供守卫与打包共用）。</summary>
    public static string ExpectedVersionString(string runtimeHeaderText) =>
        $"{NumericMacro(runtimeHeaderText, "HMI_RUNTIME_VERSION_MAJOR")}." +
        $"{NumericMacro(runtimeHeaderText, "HMI_RUNTIME_VERSION_MINOR")}." +
        $"{NumericMacro(runtimeHeaderText, "HMI_RUNTIME_VERSION_PATCH")}";
}
