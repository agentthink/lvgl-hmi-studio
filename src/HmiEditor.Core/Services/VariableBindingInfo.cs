using System;
using System.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 绑定变量的元信息解析 —— 绑定 Id → 数据类型 / 工程单位。
///
/// <para>原先是 <c>MainWindow</c> 里两个私有方法，只被预览运行时使用；放在 Core 是因为
/// 它只依赖 <see cref="VariableManager"/>（纯模型查询，无 UI），这样预览运行时、
/// 代码生成与未来的属性面板都能复用同一份规则，并且可以在 Core 测试里直接覆盖
/// （UDT 字段、系统变量、旧工程的按名回退等分支）。</para>
///
/// <para>查找顺序：变量 Id → 变量 Name（均由 <see cref="VariableManager.ResolveBinding"/>
/// 统一处理，含 UDT 字段的"实例名.字段名"形态）→ 系统变量 Id；都不命中时数据类型
/// 回落 <c>Float</c>。</para>
///
/// <para><b>顺带修掉一个既有缺陷：</b>原实现用 <c>bindingId.Contains('.')</c> 判定
/// "这是 UDT 字段"，一旦命中就直接 <c>return "Float"</c>。而系统变量 Id 形如
/// <c>sys.time.cur</c> **本身带点**，于是全部系统变量都被误判为 UDT 字段、
/// 永远解析成默认的 <c>Float</c>（"系统变量"分支其实是死代码）——绑定系统变量的
/// 控件在设计器里会按数值格式显示。现在不再按点号提前分流。</para>
/// </summary>
public static class VariableBindingInfo
{
    /// <summary>无法解析时的默认数据类型（与原实现一致）。</summary>
    public const string DefaultDataType = "Float";

    /// <summary>解析绑定变量的数据类型。</summary>
    public static string ResolveDataType(VariableManager variables, string bindingId)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (string.IsNullOrEmpty(bindingId)) return DefaultDataType;

        // 不按点号提前分流：系统变量 Id（sys.time.cur）同样带点，
        // 提前 return 会把它们误判成 UDT 字段而永远拿不到真实类型。
        switch (variables.ResolveBinding(bindingId))
        {
            case UdtFieldVariable field:
                return field.DataType;
            case Variable variable:
                return variable.DataType;
        }

        // 系统变量（无单位字段，但有数据类型）
        var systemVariable = variables.SystemVariables.FirstOrDefault(s => s.Id == bindingId);
        return systemVariable?.DataType ?? DefaultDataType;
    }

    /// <summary>解析绑定变量的工程单位；无单位或解析不到返回 null。</summary>
    public static string? ResolveUnit(VariableManager variables, string bindingId)
    {
        ArgumentNullException.ThrowIfNull(variables);
        if (string.IsNullOrEmpty(bindingId)) return null;

        // 系统变量没有单位字段，因此这里只查用户变量与 UDT 字段
        return variables.ResolveBinding(bindingId) switch
        {
            UdtFieldVariable field => field.Unit,
            Variable variable => variable.Unit,
            _ => null,
        };
    }
}
