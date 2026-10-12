using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.CodeGen;

/// <summary>
/// 权限模型的 **C 侧命名与字面量** —— 唯一来源。
///
/// <para><b>为什么必须收口：</b>同一个权限位会出现在两个地方 —— 生成的设备头
/// （<see cref="SecurityConfigCodeGen"/> 的 <c>HMI_PERM_* #define</c>）与生成的动作回调门
/// （<c>CodeGenerator</c> 发射的 <c>hmi_require_permission(HMI_PERM_*)</c>）。任何一处自己拼
/// <c>"HMI_PERM_" + name</c>，改名后必然分叉：一处编译不过、另一处引用到未定义宏（C 里就是
/// 隐式函数/未定义标识符，报错点离原因很远）。所以「枚举成员 → 宏名」「位掩码 → 十六进制
/// 字面量」都只在这里算。</para>
/// </summary>
public static class PermissionMacros
{
    /// <summary>权限宏前缀。守卫按它识别"手写的权限位定义"，所以前缀本身也只有这一处。</summary>
    public const string Prefix = "HMI_PERM_";

    /// <summary>权限成员 → C 宏名，如 <c>ViewAlarmHistory</c> → <c>HMI_PERM_VIEW_ALARM_HISTORY</c>。</summary>
    public static string For(Permission permission) => Prefix + SnakeUpper(permission.ToString());

    /// <summary>运行模式成员 → C 宏名，如 <c>Operator</c> → <c>HMI_SEC_MODE_OPERATOR</c>。</summary>
    public static string For(OperationMode mode) => "HMI_SEC_MODE_" + SnakeUpper(mode.ToString());

    /// <summary>
    /// 该行是不是一条"手写权限位定义"（<c>#define HMI_PERM_…</c>）。设备端守卫用它扫固件。
    ///
    /// <para><b>为什么不能只认 <c>#define</c> 字面：</b>设备端回退块自己的风格就是 <c>#  define</c>
    /// （<c>#</c> 与 <c>define</c> 之间留空隙，与周围的 <c>#  if</c>/<c>#  endif</c> 对齐）——
    /// 只按 <c>"#define HMI_PERM_"</c> 前缀判，照抄这个风格写进去的权限位会**完全不被守卫看到**。
    /// 预处理指令里 <c>#</c> 与 <c>define</c> 之间的空白是无意义的，判据必须同样放宽。</para>
    /// </summary>
    public static bool LooksLikeHandWrittenDefinition(string line) =>
        HandWrittenDefinitionRegex.IsMatch(line);

    private static readonly System.Text.RegularExpressions.Regex HandWrittenDefinitionRegex =
        new(@"^\s*#\s*define\s+" + System.Text.RegularExpressions.Regex.Escape(Prefix),
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// C 无符号十六进制字面量（定宽 3 位、带 <c>u</c> 后缀），如 <c>0x04Fu</c>。
    /// 定宽是为了让角色掩码块在头文件里竖向对齐、肉眼可核对；带 <c>u</c> 是避免隐式整型提升告警。
    /// </summary>
    public static string Hex(uint bits) => $"0x{bits:X3}u";

    /// <summary>权限位掩码（<see cref="Permission"/> 是 <c>[Flags]</c>，底层 int，这里按无符号取用）。</summary>
    public static uint Mask(Permission permission) => unchecked((uint)(int)permission);

    /// <summary>PascalCase → SNAKE_UPPER：仅在小写/数字后遇到大写字母时插下划线（不改动已有下划线）。</summary>
    internal static string SnakeUpper(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}
