using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.Core.Services;

// ═══════════════════════════════════════════════════════════
//  R3-T5: 脚本沙箱执行器
//  基于 ExpressionEngine 的安全脚本运行时 —— 无文件 IO、无网络、无进程。
//  支持赋值、if/then/else/end 条件分支、纯表达式求值。
//  保留字：if / then / else / end（不可作为变量名）。
//  I5 扩展: 外部值源回退(桥接 VariableSimulator)、项目变量种子、
//          Lua 回调体翻译(PrepareLuaForSandbox) → 编辑器内"调试运行"。
// ═══════════════════════════════════════════════════════════

/// <summary>
/// R3-T5: 沙箱脚本执行器 —— 在隔离环境中执行简单的表达式脚本。
/// 仅依赖 <see cref="ExpressionEngine"/>，不执行任何文件 / 网络 / 进程操作，
/// 因为表达式引擎本身不支持函数调用，从根本上杜绝了副作用。
/// </summary>
public class ScriptSandboxExecutor
{
    private const int DefaultTimeoutMs = 1000;

    private readonly Dictionary<string, ExpressionResult> _variables =
        new(StringComparer.Ordinal);

    /// <summary>单次执行的最大时长（毫秒），超过则中止并返回错误。默认 1000ms。</summary>
    public int TimeoutMs { get; set; } = DefaultTimeoutMs;

    /// <summary>
    /// 外部值源 —— 沙箱内未定义的变量名回退到此源查找。
    /// 用于桥接 VariableSimulator 模拟值 / 项目变量默认值，实现"调试运行复用仿真数据源"。
    /// </summary>
    public Func<string, double?>? ExternalValueSource { get; set; }

    // ── 变量管理 ──

    /// <summary>设置数值变量。</summary>
    public void SetVariable(string name, double value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _variables[name] = ExpressionResult.Number(value);
    }

    /// <summary>设置布尔变量。</summary>
    public void SetVariable(string name, bool value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _variables[name] = ExpressionResult.Boolean(value);
    }

    /// <summary>设置字符串变量。</summary>
    public void SetVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);
        _variables[name] = ExpressionResult.String(value);
    }

    /// <summary>获取变量值（数值形式）。变量不存在时返回 null。</summary>
    public double? GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var v))
            return v.AsNumber();
        return null;
    }

    /// <summary>清除所有变量。</summary>
    public void Clear() => _variables.Clear();

    /// <summary>
    /// 将项目变量种子化到沙箱（Id 与 Name 均注册）。
    /// 数值取自 <see cref="Variable.SimulatedValue"/>（可解析时），否则 0；
    /// UDT 实例展开为 "实例名.字段名"；系统变量按 Id 注册。
    /// </summary>
    public void SeedProjectVariables(HmiProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        foreach (var v in project.VariableManager.Variables)
        {
            var value = TryParseSimulatedValue(v.SimulatedValue);
            if (!string.IsNullOrEmpty(v.Id))
                _variables[v.Id] = ExpressionResult.Number(value);
            if (!string.IsNullOrEmpty(v.Name) && !string.Equals(v.Name, v.Id, StringComparison.Ordinal))
                _variables[v.Name] = ExpressionResult.Number(value);

            if (v.IsUdtInstance)
            {
                var def = project.VariableManager.FindUdtDefinition(v.UdtTypeId);
                if (def != null)
                {
                    foreach (var field in def.Fields)
                        _variables[$"{v.Name}.{field.Name}"] = ExpressionResult.Number(0);
                }
            }
        }

        foreach (var sv in project.VariableManager.SystemVariables)
        {
            if (!string.IsNullOrEmpty(sv.Id))
                _variables[sv.Id] = ExpressionResult.Number(0);
        }
    }

    private static double TryParseSimulatedValue(string? simulatedValue)
    {
        if (double.TryParse(simulatedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    // ── 执行入口 ──

    /// <summary>执行 <see cref="HmiScript"/> 中的代码。</summary>
    public ScriptExecutionResult Execute(HmiScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var result = Execute(script.Code ?? "");
        if (!string.IsNullOrEmpty(script.Name))
            result.Log.Insert(0, $"[Sandbox] 执行脚本: {script.Name}");
        return result;
    }

    /// <summary>执行原始代码字符串。</summary>
    public ScriptExecutionResult Execute(string code)
    {
        var result = new ScriptExecutionResult
        {
            Outputs = new Dictionary<string, object>(),
            Log = new List<string>(),
        };
        var sw = Stopwatch.StartNew();
        try
        {
            result.Log.Add("[Sandbox] 开始执行");
            ExecuteStatements(code ?? "", result, sw);
            result.Success = true;
            result.Log.Add($"[Sandbox] 执行完成，输出 {result.Outputs.Count} 个变量");
        }
        catch (TimeoutException)
        {
            result.Success = false;
            result.Error = $"执行超时（超过 {TimeoutMs}ms），已中止";
            result.Log.Add("[Sandbox] 执行超时，已中止");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.Log.Add($"[Sandbox] 执行错误: {ex.Message}");
        }
        return result;
    }

    // ── 内部执行 ──

    private void ExecuteStatements(string code, ScriptExecutionResult result, Stopwatch sw)
    {
        foreach (var stmt in SplitStatements(code))
        {
            if (sw.ElapsedMilliseconds > TimeoutMs)
                throw new TimeoutException();
            ExecuteStatement(stmt, result, sw);
        }
    }

    private void ExecuteStatement(string stmt, ScriptExecutionResult result, Stopwatch sw)
    {
        var trimmed = stmt.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        // if 分支
        if (StartsWithKeyword(trimmed, "if"))
        {
            ExecuteIf(trimmed, result, sw);
            return;
        }

        // 赋值
        if (TryParseAssignment(trimmed, out var target, out var expr))
        {
            var value = ExpressionEngine.Evaluate(expr, ResolveVariable);
            _variables[target] = value;
            result.Outputs[target] = ToClrValue(value);
            result.Log.Add($"[Sandbox] {target} = {value}");
            return;
        }

        // 裸表达式：求值并记录，无副作用
        var val = ExpressionEngine.Evaluate(trimmed, ResolveVariable);
        result.Log.Add($"[Sandbox] => {val}");
    }

    private void ExecuteIf(string stmt, ScriptExecutionResult result, Stopwatch sw)
    {
        var thenIdx = IndexOfKeyword(stmt, "then", 0);
        if (thenIdx < 0)
            throw new FormatException("if 语句缺少 'then' 关键字");

        var ifIdx = IndexOfKeyword(stmt, "if", 0);
        var condition = stmt.Substring(ifIdx + 2, thenIdx - (ifIdx + 2)).Trim();

        var afterThen = thenIdx + 4; // "then" 长度
        var endIdx = FindMatchingEnd(stmt, afterThen);
        if (endIdx < 0)
            throw new FormatException("if 语句缺少 'end' 关键字");

        var condResult = ExpressionEngine.Evaluate(condition, ResolveVariable);
        var condBool = condResult.AsBoolean();
        result.Log.Add($"[Sandbox] if ({condition}) => {condBool}");

        // I5: else 分支支持 —— 顶层 else 拆分 true/false 两段执行
        var elseIdx = FindTopLevelKeyword(stmt, "else", afterThen, endIdx);
        if (elseIdx >= 0)
        {
            var trueBody = stmt.Substring(afterThen, elseIdx - afterThen).Trim();
            var falseBody = stmt.Substring(elseIdx + 4, endIdx - (elseIdx + 4)).Trim();
            result.Log.Add($"[Sandbox] else 分支 => {(condBool ? "then" : "else")}");
            if (condBool)
                ExecuteStatements(trueBody, result, sw);
            else
                ExecuteStatements(falseBody, result, sw);
            return;
        }

        var body = stmt.Substring(afterThen, endIdx - afterThen).Trim();
        if (condBool)
            ExecuteStatements(body, result, sw);
    }

    private ExpressionResult? ResolveVariable(string name)
    {
        if (_variables.TryGetValue(name, out var v))
            return v;

        // I5: 外部值源回退（VariableSimulator / 项目变量提供器）
        var external = ExternalValueSource?.Invoke(name);
        if (external.HasValue)
            return ExpressionResult.Number(external.Value);

        return null;
    }

    private static object ToClrValue(ExpressionResult r)
        => r.Type switch
        {
            ExpressionResultType.Number => r.AsNumber(),
            ExpressionResultType.Boolean => r.AsBoolean(),
            ExpressionResultType.String => r.ToString(),
            _ => 0.0,
        };

    // ── 语句切分 ──

    /// <summary>将代码切分为顶层语句；if...end 块作为单条语句保留（支持多行与嵌套）。</summary>
    private static List<string> SplitStatements(string code)
    {
        var statements = new List<string>();
        var fragments = SplitFragments(code);

        int i = 0;
        while (i < fragments.Count)
        {
            if (StartsWithKeyword(fragments[i], "if"))
            {
                var sb = new StringBuilder(fragments[i]);
                int depth = CountKeyword(fragments[i], "if") - CountKeyword(fragments[i], "end");
                int j = i;
                while (depth > 0 && j + 1 < fragments.Count)
                {
                    j++;
                    sb.Append(' ').Append(fragments[j]);
                    depth += CountKeyword(fragments[j], "if") - CountKeyword(fragments[j], "end");
                }
                statements.Add(sb.ToString().Trim());
                i = j + 1;
            }
            else
            {
                statements.Add(fragments[i]);
                i++;
            }
        }
        return statements;
    }

    /// <summary>按换行与分号切分为片段（字符串字面量内的分隔符不切分）。</summary>
    private static List<string> SplitFragments(string code)
    {
        var fragments = new List<string>();
        var sb = new StringBuilder();
        int i = 0;
        while (i < code.Length)
        {
            char c = code[i];
            if (c == '"' || c == '\'')
            {
                char quote = c;
                sb.Append(c);
                i++;
                while (i < code.Length && code[i] != quote)
                {
                    if (code[i] == '\\' && i + 1 < code.Length) { sb.Append(code[i]); i++; }
                    sb.Append(code[i]);
                    i++;
                }
                if (i < code.Length) { sb.Append(code[i]); i++; }
            }
            else if (c == '\n' || c == '\r' || c == ';')
            {
                Flush(sb, fragments);
                i++;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        Flush(sb, fragments);
        return fragments;
    }

    private static void Flush(StringBuilder sb, List<string> fragments)
    {
        var p = sb.ToString().Trim();
        if (!string.IsNullOrEmpty(p)) fragments.Add(p);
        sb.Clear();
    }

    /// <summary>解析赋值语句：identifier = expression（区分单 '=' 与 '=='/'&gt;='/…）。</summary>
    private static bool TryParseAssignment(string s, out string target, out string expr)
    {
        target = "";
        expr = "";
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || !(char.IsLetter(s[i]) || s[i] == '_')) return false;

        int start = i;
        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.')) i++;
        target = s.Substring(start, i - start);

        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || s[i] != '=') return false;
        if (i + 1 < s.Length && s[i + 1] == '=') return false; // '=='，非赋值
        i++;

        expr = s.Substring(i).Trim();
        return !string.IsNullOrEmpty(expr);
    }

    // ── 关键字扫描（字符串感知、整词匹配） ──

    private static List<(string Word, int Index)> ScanWords(string s)
    {
        var words = new List<(string, int)>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '"' || c == '\'')
            {
                char quote = c;
                i++;
                while (i < s.Length && s[i] != quote)
                {
                    if (s[i] == '\\' && i + 1 < s.Length) i++;
                    i++;
                }
                if (i < s.Length) i++;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                words.Add((s.Substring(start, i - start), start));
            }
            else
            {
                i++;
            }
        }
        return words;
    }

    private static bool StartsWithKeyword(string s, string keyword)
    {
        var words = ScanWords(s);
        return words.Count > 0 && words[0].Item1.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountKeyword(string s, string keyword)
    {
        int n = 0;
        foreach (var (w, _) in ScanWords(s))
            if (w.Equals(keyword, StringComparison.OrdinalIgnoreCase)) n++;
        return n;
    }

    private static int IndexOfKeyword(string s, string keyword, int startIndex)
    {
        foreach (var (w, idx) in ScanWords(s))
        {
            if (idx < startIndex) continue;
            if (w.Equals(keyword, StringComparison.OrdinalIgnoreCase)) return idx;
        }
        return -1;
    }

    /// <summary>从 startIndex 起查找与已开启的 if 配对的 'end'（支持嵌套）。</summary>
    private static int FindMatchingEnd(string s, int startIndex)
    {
        int depth = 1;
        foreach (var (w, idx) in ScanWords(s))
        {
            if (idx < startIndex) continue;
            if (w.Equals("if", StringComparison.OrdinalIgnoreCase)) depth++;
            else if (w.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                depth--;
                if (depth == 0) return idx;
            }
        }
        return -1;
    }

    /// <summary>
    /// 在指定区间内查找"顶层"关键字（不被嵌套 if 包裹）—— 用于 else 分支定位。
    /// 深度规则：遇到 if +1、end -1；当前深度为 0 时命中的关键字即为顶层。
    /// </summary>
    private static int FindTopLevelKeyword(string s, string keyword, int startIndex, int endIndex)
    {
        int depth = 0;
        foreach (var (w, idx) in ScanWords(s))
        {
            if (idx <= startIndex) continue;
            if (idx >= endIndex) break;
            if (w.Equals("if", StringComparison.OrdinalIgnoreCase)) depth++;
            else if (w.Equals("end", StringComparison.OrdinalIgnoreCase)) depth--;
            else if (w.Equals(keyword, StringComparison.OrdinalIgnoreCase) && depth == 0)
                return idx;
        }
        return -1;
    }

    // ── I5: Lua 回调体翻译（编辑器"调试运行"预处理）──

    /// <summary>
    /// 将 Lua 脚本代码翻译为沙箱可执行语句。
    /// 处理步骤：
    ///   1. 去除注释行与 function 包装，提取第一个函数体；
    ///   2. hmi_read("x") → 变量引用 x；
    ///   3. hmi_write("x", expr) → 赋值 x = expr；
    ///   4. hmi_set_control_property(...) → 无操作 0（调试运行不施加控件属性修改）；
    ///   5. Lua 的 not → 沙箱的 !；local 前缀去除。
    /// 未找到函数定义时返回 null（调用方可回退到原始代码）。
    /// </summary>
    public static string? PrepareLuaForSandbox(string luaCode)
    {
        if (string.IsNullOrWhiteSpace(luaCode)) return null;

        // 去除整行注释（-- 开头）
        var lines = luaCode.Split('\n').Select(l =>
            l.TrimStart().StartsWith("--", StringComparison.Ordinal) ? "" : l);
        var stripped = string.Join("\n", lines);

        var body = ExtractFunctionBody(stripped);
        if (body == null) return null;

        body = Regex.Replace(body, @"hmi_read\s*\(\s*""([^""]+)""\s*\)", m => m.Groups[1].Value);
        body = Regex.Replace(body,
            @"hmi_write\s*\(\s*""([^""]+)""\s*,\s*(.+?)\s*\)",
            m => $"{m.Groups[1].Value} = {m.Groups[2].Value}");
        body = Regex.Replace(body, @"hmi_set_control_property\s*\([^)]*\)", "0");
        body = Regex.Replace(body, @"(?<![\w.])not(?![\w.])", "!");
        body = Regex.Replace(body, @"\blocal\s+", "");

        return body.Trim();
    }

    /// <summary>提取第一个 function 定义的函数体（匹配对应的 'end'）。</summary>
    private static string? ExtractFunctionBody(string code)
    {
        var m = Regex.Match(code, @"function\s+\w+\s*\([^)]*\)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var start = m.Index + m.Length;

        // 从函数头后扫描：if +1 / end -1，第一个使深度为负的 end 即函数收尾
        int ifDepth = 0;
        foreach (var (w, idx) in ScanWords(code))
        {
            if (idx < start) continue;
            if (w.Equals("if", StringComparison.OrdinalIgnoreCase)) ifDepth++;
            else if (w.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                ifDepth--;
                if (ifDepth < 0)
                    return code[start..idx].Trim();
            }
        }
        return null;
    }
}

/// <summary>脚本执行结果。</summary>
public class ScriptExecutionResult
{
    /// <summary>执行是否成功完成（无错误）。</summary>
    public bool Success { get; set; }

    /// <summary>失败时的错误信息；成功时为 null。</summary>
    public string? Error { get; set; }

    /// <summary>执行期间被设置 / 修改的变量（值类型为 double / bool / string）。</summary>
    public Dictionary<string, object> Outputs { get; set; } = new();

    /// <summary>执行追踪日志。</summary>
    public List<string> Log { get; set; } = new();
}
