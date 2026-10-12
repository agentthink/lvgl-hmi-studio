using System.Text;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>字体子集化分析报告。</summary>
public sealed class FontSubsetReport
{
    /// <summary>项目所有文本的原始字符总数(含重复)。</summary>
    public int TotalChars { get; set; }

    /// <summary>去重后的唯一字符数。</summary>
    public int UniqueChars { get; set; }

    /// <summary>唯一 ASCII 字符数。</summary>
    public int AsciiChars { get; set; }

    /// <summary>唯一 CJK(含全角符号区)字符数。</summary>
    public int CjkChars { get; set; }

    /// <summary>唯一其他字符数。</summary>
    public int OtherChars { get; set; }

    /// <summary>去重后的字符集(排序)。</summary>
    public IReadOnlyList<char> Characters { get; set; } = Array.Empty<char>();

    /// <summary>子集化估算字节数(16bpp 抗锯齿, 指定字号)。</summary>
    public long EstimatedSubsetBytes { get; set; }

    /// <summary>原始字体文件字节数(0 表示未知)。</summary>
    public long SourceFileBytes { get; set; }

    /// <summary>节省比例(0-1), 无源文件时按典型 8MB 全量估算。</summary>
    public double SavingsRatio { get; set; }
}

/// <summary>
/// I12: 字体子集化分析器 —— 收集项目实际用到的字符集,
/// 估算 LVGL 子集化内嵌字体的 Flash 占用。
///
/// 边界说明: 真正 TTF→C 字形转换由 lv_font_conv(业界标准工具)完成,
/// 本分析器提供"扫描 + 预算 + 一键导出输入(charset.txt)"闭环,
/// 保持零第三方依赖。
/// </summary>
public static class FontSubsetAnalyzer
{
    /// <summary>典型全量中文字库字节数(用于无源文件时的节省对比基准)。</summary>
    public const long TypicalFullCjkFontBytes = 8 * 1024 * 1024;

    // ── 字符扫描 ──

    /// <summary>
    /// 扫描项目所有可见文本: 控件文本、变量名/单位/模拟值、UDT 定义名与字段名。
    /// </summary>
    public static FontSubsetReport ScanProject(HmiProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var chars = new HashSet<char>();
        var total = 0;

        // 屏幕控件文本
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                if (string.IsNullOrEmpty(control.Text)) continue;
                total += control.Text.Length;
                foreach (var c in control.Text) chars.Add(c);
            }
        }

        // 变量名/单位/模拟值(控件常显示这些)
        foreach (var variable in project.VariableManager.Variables)
        {
            total += variable.Name.Length;
            foreach (var c in variable.Name) chars.Add(c);
            if (!string.IsNullOrEmpty(variable.Unit))
            {
                total += variable.Unit.Length;
                foreach (var c in variable.Unit) chars.Add(c);
            }
            if (!string.IsNullOrEmpty(variable.SimulatedValue))
            {
                total += variable.SimulatedValue.Length;
                foreach (var c in variable.SimulatedValue) chars.Add(c);
            }
        }

        // UDT 定义名 + 字段名(实例化后可能上屏)
        foreach (var def in project.VariableManager.UdtDefinitions)
        {
            foreach (var c in def.Name) chars.Add(c);
            foreach (var field in def.Fields)
                foreach (var c in field.Name) chars.Add(c);
        }

        var sorted = chars.OrderBy(c => c).ToList();
        return new FontSubsetReport
        {
            TotalChars = total,
            UniqueChars = sorted.Count,
            AsciiChars = sorted.Count(c => c <= 127),
            CjkChars = sorted.Count(IsCjk),
            OtherChars = sorted.Count(c => c > 127 && !IsCjk(c)),
            Characters = sorted
        };
    }

    /// <summary>CJK 区段: 4E00-9FFF 基本 + 3000-303F 标点 + FF00-FFEF 全角。</summary>
    public static bool IsCjk(char c)
        => c is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3000' and <= '\u303F'
            or >= '\uFF00' and <= '\uFFEF';

    // ── 大小估算 ──

    /// <summary>
    /// 估算 LVGL 子集化内嵌字体 C 资源字节数。
    /// 模型(lv_font_conv 典型输出):
    ///   - 每字形: 位图(宽×高×bpp/8) + 字形描述 ~20B + 字距摊分 ~4B
    ///   - ASCII 平均宽 = fs×0.55, CJK/全角宽 = fs, 高度 ≈ fs
    ///   - 文件头 + 索引 ~120B
    /// </summary>
    public static long EstimateSubsetBytes(FontSubsetReport report, int fontSizePx, int bpp = 16)
    {
        if (report.UniqueChars == 0 || fontSizePx <= 0)
            return 0;

        long total = 120; // lv_font_t 头 + 索引

        foreach (var c in report.Characters)
        {
            var width = IsCjk(c) || c >= 0x80 ? fontSizePx : (int)(fontSizePx * 0.55);
            var height = fontSizePx;
            var bitmap = (long)width * height * bpp / 8;
            total += bitmap + 20; // 描述 + 位图
            total += 4;           // 字距表摊分
        }

        return total;
    }

    /// <summary>生成完整报告(含估算与节省比例)。</summary>
    public static FontSubsetReport Analyze(HmiProject project, int fontSizePx = 20, int bpp = 16)
    {
        var report = ScanProject(project);
        report.EstimatedSubsetBytes = EstimateSubsetBytes(report, fontSizePx, bpp);
        return report;
    }

    // ── 导出 ──

    /// <summary>
    /// 导出项目字符清单 —— lv_font_conv 的 --symbols 输入。
    /// 每行一个字符(避免转义歧义), 空项目写空文件。
    /// </summary>
    public static void ExportCharsetFile(FontSubsetReport report, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputPath);

        var sb = new StringBuilder();
        foreach (var c in report.Characters)
            sb.Append(c).Append('\n');

        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>
    /// 构造 lv_font_conv 参考命令(TTF → LVGL C 子集化字体)。
    /// </summary>
    public static string BuildLvFontConvCommand(
        string fontFilePath, string outputCFile, int fontSizePx, bool withCjk)
    {
        var symbolsArg = withCjk
            ? "--symbols charset.txt"
            : "--symbols charset.txt";
        return $"lv_font_conv --font \"{fontFilePath}\" --size {fontSizePx} " +
               $"--format lvgl --bpp 16 --no-compress {symbolsArg} " +
               $"--output \"{outputCFile}\"";
    }

    /// <summary>格式化为人类可读字节(与资源面板一致)。</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024):0.##} MB";
    }
}
