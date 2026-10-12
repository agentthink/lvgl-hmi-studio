using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace HmiEditor.Core.Services;

// ═══════════════════════════════════════════════════════════
//  R3-T2: 表达式引擎
//  支持算术、比较、逻辑、三元运算，变量引用（含 UDT 字段）
//  用于设计时预览求值和 CodeGen 表达式转译
// ═══════════════════════════════════════════════════════════

/// <summary>表达式求值结果类型。</summary>
public enum ExpressionResultType
{
    Number,
    Boolean,
    String,
    Null
}

/// <summary>表达式求值结果。</summary>
public readonly struct ExpressionResult
{
    public ExpressionResultType Type { get; init; }
    public double NumberValue { get; init; }
    public bool BooleanValue { get; init; }
    public string StringValue { get; init; }

    public static ExpressionResult Number(double v) => new()
    {
        Type = ExpressionResultType.Number,
        NumberValue = v,
        StringValue = v.ToString(CultureInfo.InvariantCulture)
    };

    public static ExpressionResult Boolean(bool v) => new()
    {
        Type = ExpressionResultType.Boolean,
        BooleanValue = v,
        NumberValue = v ? 1 : 0,
        StringValue = v.ToString().ToLowerInvariant()
    };

    public static ExpressionResult String(string v) => new()
    {
        Type = ExpressionResultType.String,
        StringValue = v,
        NumberValue = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0
    };

    public static ExpressionResult Null => new() { Type = ExpressionResultType.Null, StringValue = "" };

    /// <summary>获取布尔值（兼容数字非零为 true）。</summary>
    public bool AsBoolean() => Type == ExpressionResultType.Boolean
        ? BooleanValue
        : NumberValue != 0;

    /// <summary>获取数字值。</summary>
    public double AsNumber() => NumberValue;

    /// <summary>获取字符串值。</summary>
    public override string ToString() => StringValue ?? "";
}

// ── AST 节点 ──

/// <summary>表达式 AST 基类（公开，用于缓存编译结果）。</summary>
public abstract class ExprNode
{
    /// <summary>使用变量解析器求值此 AST 节点。</summary>
    public abstract ExpressionResult Evaluate(Func<string, ExpressionResult?> resolver);

    /// <summary>
    /// 转译为 C 表达式字符串，变量引用经 <paramref name="variableMapper"/> 映射为任意 C 表达式。
    /// 运算符映射为 C 运算符（&& || ! == != &lt;= &gt;= &lt; &gt; + - * / % ?:）。
    ///
    /// <para>生成期用 mapper 把变量接到**既有的取值机制**上（设备端 <c>get_tag_value(TAG_x)</c>、
    /// 模拟器局部正弦波），而不是新增一个「按字符串名取值」的运行时函数 ——
    /// 后者会给设备链路引入新的 extern 定义面，而「每个 extern 声明都必须有定义」是本项目的铁律。</para>
    /// </summary>
    public abstract string ToCExpression(Func<string, string> variableMapper);

    /// <summary>
    /// 默认转译 —— 变量引用映射为 <c>hmi_get_variable_value("name")</c>。
    /// 脚本模板与诊断输出沿用此口径。
    /// </summary>
    public string ToCExpression()
        => ToCExpression(static name => "hmi_get_variable_value(\"" + name + "\")");
}

internal class NumberLiteral : ExprNode
{
    private readonly double _value;
    public NumberLiteral(double value) => _value = value;
    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> _) => ExpressionResult.Number(_value);
    public override string ToCExpression(Func<string, string> _) => _value.ToString("R", CultureInfo.InvariantCulture);
}

internal class StringLiteral : ExprNode
{
    private readonly string _value;
    public StringLiteral(string value) => _value = value;
    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> _) => ExpressionResult.String(_value);
    public override string ToCExpression(Func<string, string> _) => ExpressionEngine.QuoteCString(_value);
}

internal class BoolLiteral : ExprNode
{
    private readonly bool _value;
    public BoolLiteral(bool value) => _value = value;
    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> _) => ExpressionResult.Boolean(_value);
    public override string ToCExpression(Func<string, string> _) => _value ? "1" : "0";
}

internal class VariableRef : ExprNode
{
    private readonly string _name;
    public VariableRef(string name) => _name = name;
    public string Name => _name;
    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> resolver)
    {
        return resolver(_name) ?? ExpressionResult.Number(0);
    }
    public override string ToCExpression(Func<string, string> variableMapper) => variableMapper(_name);
}

internal class UnaryOp : ExprNode
{
    private readonly string _op;
    private readonly ExprNode _operand;
    public UnaryOp(string op, ExprNode operand) { _op = op; _operand = operand; }

    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> resolver)
    {
        var val = _operand.Evaluate(resolver);
        return _op switch
        {
            "-" => ExpressionResult.Number(-val.AsNumber()),
            "!" => ExpressionResult.Boolean(!val.AsBoolean()),
            _ => ExpressionResult.Number(val.AsNumber())
        };
    }

    public override string ToCExpression(Func<string, string> variableMapper)
    {
        var op = _op == "-" ? "-" : "!";
        return $"({op}{_operand.ToCExpression(variableMapper)})";
    }
}

internal class BinaryOp : ExprNode
{
    private readonly string _op;
    private readonly ExprNode _left;
    private readonly ExprNode _right;
    public BinaryOp(string op, ExprNode left, ExprNode right)
    { _op = op; _left = left; _right = right; }

    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> resolver)
    {
        var l = _left.Evaluate(resolver);
        var r = _right.Evaluate(resolver);

        return _op switch
        {
            "+" => l.Type == ExpressionResultType.String || r.Type == ExpressionResultType.String
                ? ExpressionResult.String(l.ToString() + r.ToString())
                : ExpressionResult.Number(l.AsNumber() + r.AsNumber()),
            "-" => ExpressionResult.Number(l.AsNumber() - r.AsNumber()),
            "*" => ExpressionResult.Number(l.AsNumber() * r.AsNumber()),
            "/" => r.AsNumber() == 0 ? ExpressionResult.Number(0) : ExpressionResult.Number(l.AsNumber() / r.AsNumber()),
            "%" => r.AsNumber() == 0 ? ExpressionResult.Number(0) : ExpressionResult.Number(l.AsNumber() % r.AsNumber()),
            ">" => ExpressionResult.Boolean(l.AsNumber() > r.AsNumber()),
            "<" => ExpressionResult.Boolean(l.AsNumber() < r.AsNumber()),
            ">=" => ExpressionResult.Boolean(l.AsNumber() >= r.AsNumber()),
            "<=" => ExpressionResult.Boolean(l.AsNumber() <= r.AsNumber()),
            "==" => ExpressionResult.Boolean(
                l.Type == ExpressionResultType.String && r.Type == ExpressionResultType.String
                    ? l.ToString() == r.ToString()
                    : Math.Abs(l.AsNumber() - r.AsNumber()) < 0.0001),
            "!=" => ExpressionResult.Boolean(
                l.Type == ExpressionResultType.String && r.Type == ExpressionResultType.String
                    ? l.ToString() != r.ToString()
                    : Math.Abs(l.AsNumber() - r.AsNumber()) >= 0.0001),
            "&&" => ExpressionResult.Boolean(l.AsBoolean() && r.AsBoolean()),
            "||" => ExpressionResult.Boolean(l.AsBoolean() || r.AsBoolean()),
            _ => ExpressionResult.Number(0)
        };
    }

    public override string ToCExpression(Func<string, string> variableMapper)
        => $"({_left.ToCExpression(variableMapper)} {_op} {_right.ToCExpression(variableMapper)})";
}

internal class TernaryOp : ExprNode
{
    private readonly ExprNode _cond;
    private readonly ExprNode _trueExpr;
    private readonly ExprNode _falseExpr;
    public TernaryOp(ExprNode cond, ExprNode trueExpr, ExprNode falseExpr)
    { _cond = cond; _trueExpr = trueExpr; _falseExpr = falseExpr; }

    public override ExpressionResult Evaluate(Func<string, ExpressionResult?> resolver)
    {
        return _cond.Evaluate(resolver).AsBoolean()
            ? _trueExpr.Evaluate(resolver)
            : _falseExpr.Evaluate(resolver);
    }

    public override string ToCExpression(Func<string, string> variableMapper)
        => $"({_cond.ToCExpression(variableMapper)} ? {_trueExpr.ToCExpression(variableMapper)} : {_falseExpr.ToCExpression(variableMapper)})";
}

// ── Tokenizer ──

internal enum TokenType
{
    Number, String, Identifier,
    Plus, Minus, Asterisk, Slash, Percent,
    Gt, Lt, Ge, Le, Eq, Ne,
    AndAnd, OrOr, Not,
    Question, Colon,
    LParen, RParen,
    Dot,
    Comma,
    EOF
}

internal readonly struct Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; }
    public int Pos { get; init; }
}

internal class Tokenizer
{
    private readonly string _input;
    private int _pos;

    public Tokenizer(string input) => _input = input;

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (_pos < _input.Length)
        {
            SkipWhitespace();
            if (_pos >= _input.Length) break;

            var ch = _input[_pos];
            var start = _pos;

            if (char.IsDigit(ch) || (ch == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber(start));
            }
            else if (ch == '"' || ch == '\'')
            {
                tokens.Add(ReadString(ch, start));
            }
            else if (char.IsLetter(ch) || ch == '_')
            {
                tokens.Add(ReadIdentifier(start));
            }
            else
            {
                tokens.Add(ReadOperator(start));
            }
        }
        tokens.Add(new Token { Type = TokenType.EOF, Value = "", Pos = _pos });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
            _pos++;
    }

    private Token ReadNumber(int start)
    {
        var sb = new StringBuilder();
        while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        return new Token { Type = TokenType.Number, Value = sb.ToString(), Pos = start };
    }

    private Token ReadString(char quote, int start)
    {
        _pos++; // skip opening quote
        var sb = new StringBuilder();
        while (_pos < _input.Length && _input[_pos] != quote)
        {
            if (_input[_pos] == '\\' && _pos + 1 < _input.Length)
            {
                _pos++;
                sb.Append(_input[_pos] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\'' => '\'',
                    '\\' => '\\',
                    _ => _input[_pos]
                });
            }
            else
            {
                sb.Append(_input[_pos]);
            }
            _pos++;
        }
        _pos++; // skip closing quote
        return new Token { Type = TokenType.String, Value = sb.ToString(), Pos = start };
    }

    private Token ReadIdentifier(int start)
    {
        var sb = new StringBuilder();
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_' || _input[_pos] == '.'))
        {
            sb.Append(_input[_pos]);
            _pos++;
        }
        var value = sb.ToString();

        // Check for keywords
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return new Token { Type = TokenType.Number, Value = "1", Pos = start };
        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return new Token { Type = TokenType.Number, Value = "0", Pos = start };

        return new Token { Type = TokenType.Identifier, Value = value, Pos = start };
    }

    private Token ReadOperator(int start)
    {
        var ch = _input[_pos];
        var next = _pos + 1 < _input.Length ? _input[_pos + 1] : '\0';

        var (type, len) = (ch, next) switch
        {
            ('>', '=') => (TokenType.Ge, 2),
            ('<', '=') => (TokenType.Le, 2),
            ('=', '=') => (TokenType.Eq, 2),
            ('!', '=') => (TokenType.Ne, 2),
            ('&', '&') => (TokenType.AndAnd, 2),
            ('|', '|') => (TokenType.OrOr, 2),
            ('+', _) => (TokenType.Plus, 1),
            ('-', _) => (TokenType.Minus, 1),
            ('*', _) => (TokenType.Asterisk, 1),
            ('/', _) => (TokenType.Slash, 1),
            ('%', _) => (TokenType.Percent, 1),
            ('>', _) => (TokenType.Gt, 1),
            ('<', _) => (TokenType.Lt, 1),
            ('!', _) => (TokenType.Not, 1),
            ('?', _) => (TokenType.Question, 1),
            (':', _) => (TokenType.Colon, 1),
            ('(', _) => (TokenType.LParen, 1),
            (')', _) => (TokenType.RParen, 1),
            (',', _) => (TokenType.Comma, 1),
            _ => (TokenType.EOF, 1) // unknown char, skip
        };

        _pos += len;
        return new Token { Type = type, Value = _input.Substring(start, len), Pos = start };
    }
}

// ── Parser (递归下降) ──

internal class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens) => _tokens = tokens;

    private Token Current => _tokens[_pos];
    private Token Advance() => _tokens[_pos++];

    private bool Match(params TokenType[] types)
    {
        if (types.Contains(Current.Type))
        {
            _pos++;
            return true;
        }
        return false;
    }

    /// <summary>解析入口：ternary → logical_or → logical_and → equality → comparison → additive → multiplicative → unary → primary</summary>
    public ExprNode Parse()
    {
        var node = ParseTernary();
        return node;
    }

    // ternary: logical_or ? ternary : ternary
    private ExprNode ParseTernary()
    {
        var cond = ParseLogicalOr();
        if (Match(TokenType.Question))
        {
            var trueExpr = ParseTernary();
            if (!Match(TokenType.Colon))
                throw new ExpressionSyntaxException(ExpressionSyntaxError.TernaryMissingColon);
            var falseExpr = ParseTernary();
            return new TernaryOp(cond, trueExpr, falseExpr);
        }
        return cond;
    }

    // logical_or: logical_and (|| logical_and)*
    private ExprNode ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (Match(TokenType.OrOr))
        {
            var right = ParseLogicalAnd();
            left = new BinaryOp("||", left, right);
        }
        return left;
    }

    // logical_and: equality (&& equality)*
    private ExprNode ParseLogicalAnd()
    {
        var left = ParseEquality();
        while (Match(TokenType.AndAnd))
        {
            var right = ParseEquality();
            left = new BinaryOp("&&", left, right);
        }
        return left;
    }

    // equality: comparison ((== | !=) comparison)*
    private ExprNode ParseEquality()
    {
        var left = ParseComparison();
        while (Current.Type == TokenType.Eq || Current.Type == TokenType.Ne)
        {
            var op = Advance().Value;
            var right = ParseComparison();
            left = new BinaryOp(op, left, right);
        }
        return left;
    }

    // comparison: additive ((> | < | >= | <=) additive)*
    private ExprNode ParseComparison()
    {
        var left = ParseAdditive();
        while (Current.Type is TokenType.Gt or TokenType.Lt or TokenType.Ge or TokenType.Le)
        {
            var op = Advance().Value;
            var right = ParseAdditive();
            left = new BinaryOp(op, left, right);
        }
        return left;
    }

    // additive: multiplicative ((+ | -) multiplicative)*
    private ExprNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance().Value;
            var right = ParseMultiplicative();
            left = new BinaryOp(op, left, right);
        }
        return left;
    }

    // multiplicative: unary ((* | / | %) unary)*
    private ExprNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current.Type is TokenType.Asterisk or TokenType.Slash or TokenType.Percent)
        {
            var op = Advance().Value;
            var right = ParseUnary();
            left = new BinaryOp(op, left, right);
        }
        return left;
    }

    // unary: (! | -) unary | primary
    private ExprNode ParseUnary()
    {
        if (Match(TokenType.Not))
            return new UnaryOp("!", ParseUnary());
        if (Match(TokenType.Minus))
            return new UnaryOp("-", ParseUnary());
        return ParsePrimary();
    }

    // primary: number | string | identifier | ( ternary )
    private ExprNode ParsePrimary()
    {
        var tok = Current;
        switch (tok.Type)
        {
            case TokenType.Number:
                Advance();
                // ⚠ 阶段 91：tokenizer 按 [0-9.]+ 收集数字 ⇒ "1.2.3" 这类记号**到得了这里**，
                // 而以前写的 double.Parse 会抛 .NET 的 FormatException —— 消息是**英文原文**，
                // 中英界面下都是英文。改成 TryParse 并在**同一处**给码：
                // 解析器里不该存在"唯一靠捕获兜"的失败。
                if (!double.TryParse(tok.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    throw new ExpressionSyntaxException(ExpressionSyntaxError.MalformedNumber(tok.Value));
                return new NumberLiteral(number);
            case TokenType.String:
                Advance();
                return new StringLiteral(tok.Value);
            case TokenType.Identifier:
                Advance();
                return new VariableRef(tok.Value);
            case TokenType.LParen:
                Advance();
                var expr = ParseTernary();
                if (!Match(TokenType.RParen))
                    throw new ExpressionSyntaxException(ExpressionSyntaxError.MissingRParen);
                return expr;
            default:
                // ⚠ 分两档，判据是 **Value 是否为空**、不是 Type（阶段 91 量出来的）：
                //   tokenizer 把**未知字符**（如 '@'）也降级成 TokenType.EOF
                //   （ReadOperator 的 `_ => (TokenType.EOF, 1)` 分支），只有 Tokenize 末尾
                //   追加的哨兵才是空 Value ⇒「输入结束」的真实判据是空串。
                //   合成一档的话 EOF 那次的实参是空串，用户会看到「意外的符号:  (位置 4)」。
                throw new ExpressionSyntaxException(tok.Value.Length == 0
                    ? ExpressionSyntaxError.UnexpectedEnd(tok.Pos)
                    : ExpressionSyntaxError.UnexpectedSymbol(tok.Value, tok.Pos));
        }
    }
}

// ═══════════════════════════════════════════════════════════
//  公共 API
// ═══════════════════════════════════════════════════════════

/// <summary>
/// R3-T2: HMI 表达式引擎 —— 支持算术、比较、逻辑、三元运算。
/// 变量引用通过 resolver 委托解析，支持 UDT 字段（如 motor1.speed）。
/// 用于设计时预览求值和 CodeGen 表达式转译。
/// </summary>
public static class ExpressionEngine
{
    /// <summary>
    /// 编译表达式为 AST（缓存友好，可重复求值）。
    ///
    /// <para><b>失败一律抛 <see cref="ExpressionSyntaxException"/></b>（阶段 91 / #61i）——
    /// 本方法的返回契约是 AST ⇒ 失败只能靠异常出来；而以前的
    /// <c>FormatException</c> **没有码**，消费点只能拿 <c>ex.Message</c> 当中文文案用
    /// （界面切成英文时那一半照旧中文）。异常里带的是
    /// <see cref="ExpressionSyntaxError"/>（码 + 中文回落 + 实参）。</para>
    ///
    /// <para><b>为什么空表达式也算"语法失败"而不是 <c>ArgumentNullException</c></b>：
    /// 调用方真的会传空 —— <c>GeneratedCodeValidator</c> 写的是
    /// <c>binding.Expression ?? ""</c>，于是"Kind = Expression 但表达式为空"的工程
    /// 走到这里；而 <c>ArgumentNullException</c> 的消息是 .NET 英文原文。
    /// 对用户来说"表达式为空"与"缺右括号"是同一类事：**这个表达式用不了、原因是这个**。</para>
    /// </summary>
    /// <exception cref="ExpressionSyntaxException">表达式为空 / 纯空白，或语法不合法。</exception>
    public static ExprNode Compile(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionSyntaxException(ExpressionSyntaxError.Empty);

        var tokens = new Tokenizer(expression).Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    /// <summary>
    /// 求值表达式。resolver 接收变量名（含 UDT 字段 compound id），
    /// 返回 ExpressionResult 或 null（变量不存在）。
    /// </summary>
    public static ExpressionResult Evaluate(
        string expression,
        Func<string, ExpressionResult?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var ast = Compile(expression);
        return ast.Evaluate(resolver);
    }

    /// <summary>
    /// 求值表达式（使用字典作为变量源）。
    /// </summary>
    public static ExpressionResult Evaluate(
        string expression,
        Dictionary<string, double> variables)
    {
        return Evaluate(expression, name =>
        {
            // 支持普通变量和 UDT 字段
            if (variables.TryGetValue(name, out var v))
                return ExpressionResult.Number(v);
            return null;
        });
    }

    /// <summary>
    /// 提取表达式中引用的所有变量名。
    /// 用于交叉引用分析和预览模式变量注册。
    /// </summary>
    public static HashSet<string> ExtractVariableNames(string expression)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(expression)) return result;

        try
        {
            var tokens = new Tokenizer(expression).Tokenize();
            foreach (var tok in tokens)
            {
                if (tok.Type == TokenType.Identifier)
                    result.Add(tok.Value);
            }
        }
        catch
        {
            // 解析失败时返回已提取的变量
        }
        return result;
    }

    /// <summary>
    /// 验证表达式语法是否正确 —— **校验器与属性面板的唯一入口**（阶段 91 / #61i）。
    ///
    /// <para><paramref name="error"/> 为 <c>null</c> 表示通过；否则给出一条
    /// <see cref="ExpressionSyntaxError"/>（**码 + 中文回落 + 实参**）。
    /// 消费点按 <see cref="ExpressionSyntaxError.Code"/> 渲染用户语言，
    /// 而不是把消息原文硬拼进句子。</para>
    /// </summary>
    public static bool TryCompile(
        string expression,
        [NotNullWhen(false)] out ExpressionSyntaxError? error)
    {
        try
        {
            Compile(expression);
            error = null;
            return true;
        }
        catch (ExpressionSyntaxException ex)
        {
            error = ex.Error;
            return false;
        }
        catch (Exception ex)
        {
            // 防线，不是正常路径：解析器现在**全部**失败都抛 ExpressionSyntaxException，
            // tokenizer 四个读取方法一个 throw 都没有、Current 有 EOF 哨兵、
            // 数字改走 TryParse 后也不再抛 ⇒ 这一支结构上到不了
            // （见 ExpressionSyntaxIssueCodes.GENERIC 的论证 + 守卫的 NotProducible 表）。
            // 留着是因为"万一有"，而它绝不静默：给码 + 原始消息当实参。
            error = ExpressionSyntaxError.Generic(ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 将表达式转译为 C 表达式字符串（供 CodeGen 运行时集成交付）。
    /// 语法错误时返回 null。
    /// </summary>
    public static string? ToCExpression(string expression)
    {
        try
        {
            return Compile(expression).ToCExpression();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将表达式转译为 C 表达式，变量引用经 <paramref name="variableMapper"/> 映射。
    /// 生成期用它把变量接到既有取值机制上（设备端 <c>get_tag_value(TAG_x)</c>、
    /// 模拟器局部正弦波），避免引入新的运行时取值函数。语法错误时返回 null。
    /// </summary>
    public static string? ToCExpression(string expression, Func<string, string> variableMapper)
    {
        ArgumentNullException.ThrowIfNull(variableMapper);
        try
        {
            return Compile(expression).ToCExpression(variableMapper);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将字符串转译为 C 字符串字面量（转义引号与反斜杠）。
    /// </summary>
    public static string QuoteCString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
