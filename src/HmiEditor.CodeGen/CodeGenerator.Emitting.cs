using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— 发射层通用工具 —— 浮点字面量格式化、注释追加、布尔判定表达式、绑定文本语句、C 字符串/格式串/注释转义。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// C 浮点字面量格式化：整数补 .0（"50" → "50.0f"），避免 C 语言 "50f" 非法；
    /// 非整数保留小数部分（最小 10 位小数精度），杜绝 "0f" 类非法字面量。
    /// 历史缺陷：旧实现 "0.##" 会把 0.001 四舍五入为 "0" → "0f" 非法；≥1e9 的整数也会输出 "1000000000f" 非法。
    /// </summary>
    private static string CFloat(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0.0f";
        var s = value.ToString("G10", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E'))
            s += ".0";
        return s + "f";
    }

    /// <summary>仅当 ExportConfig.IncludeComments 开启时追加注释行；代码行始终追加。</summary>
    private void Cmt(StringBuilder sb, string text)
    {
        if (_includeComments) sb.AppendLine(EscapeComment(text));
    }

    /// <summary>
    /// C 数值字面量（几何 / 缩放这类**不加 f 后缀**的 double）：一律 InvariantCulture。
    ///
    /// <para><b>为什么必须集中在一处：</b>`HmiControl.X/Y/Width/Height`、`HmiScreen.Width/Height`
    /// 都是 `double`，而生成 C 的代码若直接 `$"{value}"`，小数点由**进程区域设置**决定 ——
    /// de-DE / fr-FR / ru-RU / pt-BR 下会生成 `lv_obj_set_pos(o, 12,5, 30)`：
    /// 这不是"格式难看"，是**实参个数都变了**、生成的 C 编译不过（阶段 120 修复）。
    /// 同项目的 <see cref="CFloat"/> 与 `RuntimeTagsCodeGen.F` 早就是 InvariantCulture，
    /// 这一族是分叉出来的重复实现。</para>
    /// </summary>
    /// <summary>
    /// **设备空间**的尺寸数字（阶段 228，登记 `#98` 的"进导出"第二半）：先按屏幕 `DpiScale` 换算再格式化。
    /// 只用于**几何**（位置/尺寸/圆角/边宽/字号相关的坐标），**不**用于语义数值（比例、阈值、时长）。
    /// DPI = 1.0（默认）时与 <see cref="CNumber"/> 逐字节相同。
    /// </summary>
    private string CDim(double value) => CNumber(value * _dpiScale);

    private static string CNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
        return value.ToString("G10", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 布尔判定的 C 表达式 —— 唯一来源是 <see cref="BindingSemantics.BoolThreshold"/>，
    /// 保证生成代码与设计器预览的判真规则一致（旧实现硬编码 v &gt; 0.5f）。
    /// </summary>
    private static string CBoolTruth(string rawExpr)
        => BindingSemantics.BoolThreshold == 0.0
            ? $"({rawExpr}) != 0.0f"
            : $"({rawExpr}) > {CFloat(BindingSemantics.BoolThreshold)}";

    /// <summary>
    /// 生成「绑定原始值 → 控件文本」的 C 语句（与设计器共用 <see cref="BindingSemantics"/>）。
    /// Bool 类输出 ON/OFF 文本；数值类按 ValueScaleFactor 缩放后套用统一格式串
    /// （含小数位与前后缀/变量单位），彻底消除"设计器 12.34 / 设备 12.3"的跨端漂移。
    /// </summary>
    private static string BuildBindingTextStatement(string objName, string rawExpr, BindingSemantics sem, string indent)
    {
        if (sem.Kind == BindingDisplayKind.Bool)
            return $"{indent}lv_label_set_text({objName}, {CBoolTruth(rawExpr)} ? \"ON\" : \"OFF\");";

        var value = sem.ScaleFactor == 1.0
            ? $"((double)({rawExpr}))"
            : $"((double)({rawExpr}) * {CFloat(sem.ScaleFactor)})";
        return $"{indent}lv_label_set_text_fmt({objName}, \"{sem.CFormatLiteral()}\", {value});";
    }

    /// <summary>转义 C 字符串字面量中的特殊字符（反斜杠、双引号、换行、回车、制表符）。</summary>
    private static string EscapeCString(string s)
        => s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

    /// <summary>
    /// 生成 printf 格式串中的「字面量片段」（变量单位、表格前后缀等）。
    /// 在 C 字符串转义的基础上把 <c>%</c> 变成 <c>%%</c>：
    /// 用户把单位写成 "%" 或 "%RH" 时，旧实现会产出 "%.1f%" 这种非法转换说明符
    /// （未定义行为；含 %n 时甚至写内存），含 "abc%s" 时还会读取不存在的变参。
    /// </summary>
    private static string EscapeFormatLiteral(string? s)
        => EscapeCString(s ?? string.Empty).Replace("%", "%%");

    /// <summary>
    /// 消毒注释文本（控件名 / 屏幕名 / 报警组名 / 错误消息 / 脚本名）。
    /// 用户文本若含 <c>*/</c> 会提前结束块注释、含换行会把后续文本顶出注释行，
    /// 两种情况都会让注释变成可执行代码。
    /// 实现要点：只中和注释「体内」的 <c>*/</c>，保留注释自身的结束标记，
    /// 因此可以安全地传入完整的注释字符串（如 <c>"/* X */"</c>）。
    /// </summary>
    private static string EscapeComment(string? s)
    {
        var t = (s ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        var end = t.LastIndexOf("*/", StringComparison.Ordinal);
        if (end < 0) return t;                    // 行注释：已无换行，安全
        var head = t[..end].Replace("*/", "* /"); // 中和注释体内的提前结束标记
        return head + t[end..];                   // 保留注释自身结尾的 */
    }
}
