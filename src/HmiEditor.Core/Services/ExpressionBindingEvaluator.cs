using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 表达式绑定求值器 —— 将 VariableBindingData（变量/表达式两种源）统一解析为值。
///
/// 设计说明：
/// - Variable 源：直接按变量 ID 查询 resolver（与原行为等价）。
/// - Expression 源：先用 ExpressionEngine 编译校验，再求值；语法错误返回 null，
///   由调用方回退到编辑态渲染，避免异常中断预览/生成流程。
/// 该类型为纯函数，不修改模型，无副作用（对齐 PreviewValueResolver 的隔离约束）。
/// </summary>
public static class ExpressionBindingEvaluator
{
    /// <summary>
    /// 求值绑定的值。
    /// </summary>
    /// <param name="binding">绑定条目（只读引用）。</param>
    /// <param name="resolver">变量名（含 UDT 字段，如 motor1.speed）→ 值 的查找函数。</param>
    /// <returns>求值结果；表达式语法错误、表达式为空、或变量缺失时返回 null。</returns>
    public static ExpressionResult? EvaluateBinding(
        VariableBindingData binding,
        Func<string, ExpressionResult?> resolver)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(resolver);

        if (binding.Kind == BindingSourceKind.Expression)
        {
            if (string.IsNullOrWhiteSpace(binding.Expression)) return null;
            if (!ExpressionEngine.TryCompile(binding.Expression, out _)) return null;
            try
            {
                return ExpressionEngine.Evaluate(binding.Expression, resolver);
            }
            catch
            {
                return null;
            }
        }

        return resolver(binding.VariableId);
    }

    /// <summary>
    /// 提取绑定引用的全部变量名。
    /// Variable 源返回 [VariableId]；Expression 源返回表达式内引用的所有标识符
    /// （用于预览模式变量注册与交叉引用分析）。
    /// </summary>
    public static HashSet<string> ExtractVariables(VariableBindingData binding)
    {
        if (binding.Kind == BindingSourceKind.Expression)
            return ExpressionEngine.ExtractVariableNames(binding.Expression ?? "");

        return string.IsNullOrEmpty(binding.VariableId)
            ? new HashSet<string>()
            : new HashSet<string>(StringComparer.Ordinal) { binding.VariableId };
    }
}
