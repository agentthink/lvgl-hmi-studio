using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;

namespace HmiEditor.CodeGen;

/// <summary>
/// Faceplate 展开器 —— 在代码生成前将 Faceplate 实例替换为模板内部控件的副本。
///
/// 展开流程：
/// 1. 遍历屏幕控件列表，识别 TemplateId 非空的 Faceplate 实例
/// 2. 查找对应模板（按 ID + 版本号）
/// 3. 深拷贝模板内部控件，坐标平移到实例位置
/// 4. 执行参数替换：将 {{Parameters.参数名}} 替换为实例绑定的实际值
/// 5. 递归展开嵌套 Faceplate（最大深度 8 层，防止循环依赖）
///
/// 展开后输出等价于不使用 Faceplate 的纯原子控件列表，
/// CodeGenerator 无需感知 Faceplate 概念。
/// </summary>
public class FaceplateExpander
{
    /// <summary>最大嵌套展开深度 —— 防止循环依赖导致无限递归。</summary>
    public const int MaxExpansionDepth = 8;

    /// <summary>占位符正则 —— 匹配 {{Parameters.参数名}} 格式。</summary>
    private static readonly Regex s_placeholderRegex = new(
        @"\{\{Parameters\.(\w+)\}\}",
        RegexOptions.Compiled);

    private readonly Dictionary<string, ControlTemplate> _templateIndex;

    /// <param name="templates">项目中的所有模板定义。</param>
    public FaceplateExpander(IEnumerable<ControlTemplate> templates)
    {
        _templateIndex = new Dictionary<string, ControlTemplate>();
        foreach (var t in templates)
        {
            _templateIndex[t.Id] = t;
        }
    }

    /// <summary>
    /// 展开屏幕中的所有 Faceplate 实例。
    /// 返回一个新的控件列表，其中 Faceplate 实例已被替换为展开后的原子控件。
    /// 非 Faceplate 控件原样保留。
    /// </summary>
    public List<HmiControl> ExpandScreen(HmiScreen screen)
    {
        var result = new List<HmiControl>();
        var expansionChain = new HashSet<string>();

        foreach (var control in screen.Controls)
        {
            if (!string.IsNullOrEmpty(control.TemplateId))
            {
                var expanded = ExpandFaceplate(control, depth: 0, expansionChain);
                result.AddRange(expanded);
            }
            else
            {
                result.Add(control);
            }
        }

        return result;
    }

    /// <summary>
    /// 展开单个 Faceplate 实例 —— 递归处理嵌套 Faceplate。
    /// </summary>
    /// <param name="instance">画布上的 Faceplate 实例控件。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <param name="expansionChain">当前展开链 —— 用于循环依赖检测。</param>
    /// <returns>展开后的原子控件列表。</returns>
    private List<HmiControl> ExpandFaceplate(
        HmiControl instance,
        int depth,
        HashSet<string> expansionChain)
    {
        // 深度限制
        if (depth >= MaxExpansionDepth)
        {
            return new List<HmiControl>();
        }

        // 循环依赖检测
        var chainKey = $"{instance.TemplateId}:{instance.Id}";
        if (!expansionChain.Add(chainKey))
        {
            // 检测到循环依赖，跳过展开
            return new List<HmiControl>();
        }

        // 查找模板
        if (string.IsNullOrEmpty(instance.TemplateId) ||
            !_templateIndex.TryGetValue(instance.TemplateId, out var template))
        {
            // 模板不存在，返回空（实例无法展开）
            expansionChain.Remove(chainKey);
            return new List<HmiControl>();
        }

        // 构建参数值表
        var paramValues = BuildParameterValues(template, instance);

        // 深拷贝模板控件并应用变换
        var expandedControls = new List<HmiControl>();
        for (var i = 0; i < template.Controls.Count; i++)
        {
            var templateControl = template.Controls[i];

            // 克隆 id 由「实例 id + 模板内序号」确定性派生 —— 见 CloneControl 的注释。
            // 前缀先过 CIdentifier：id 里的非标识符字符（`-`、空格…）若原样进 `obj_{Id}`
            // 会生成非法 C；净化后与全仓其它 obj_* 引用点的约定一致。
            var copy = CloneControl(templateControl, CIdentifier.ForControlSymbol(instance.Id), i);

            // 坐标平移 —— 模板相对坐标 + 实例绝对坐标
            copy.X += instance.X;
            copy.Y += instance.Y;

            // 参数替换
            ApplyParameterSubstitution(copy, paramValues);

            // 递归展开嵌套 Faceplate
            if (!string.IsNullOrEmpty(copy.TemplateId))
            {
                var nested = ExpandFaceplate(copy, depth + 1, expansionChain);
                expandedControls.AddRange(nested);
            }
            else
            {
                expandedControls.Add(copy);
            }
        }

        expansionChain.Remove(chainKey);
        return expandedControls;
    }

    /// <summary>
    /// 构建参数值表 —— 合并模板默认值和实例绑定值。
    /// 实例绑定值优先于模板默认值。
    /// </summary>
    private Dictionary<string, string> BuildParameterValues(
        ControlTemplate template,
        HmiControl instance)
    {
        var values = new Dictionary<string, string>();

        // 1. 填入默认值
        foreach (var param in template.Parameters)
        {
            if (param.DefaultValue != null)
            {
                values[param.Name] = param.DefaultValue;
            }
        }

        // 2. 覆盖为实例绑定值
        foreach (var binding in instance.ParameterBindings)
        {
            if (!string.IsNullOrEmpty(binding.ParameterName))
            {
                // VariableId 类型：使用 VariableId 字段值
                if (!string.IsNullOrEmpty(binding.VariableId))
                {
                    values[binding.ParameterName] = binding.VariableId;
                }
                else if (binding.Value != null)
                {
                    values[binding.ParameterName] = binding.Value;
                }
            }
        }

        return values;
    }

    /// <summary>
    /// 对控件的所有字符串属性执行参数占位符替换。
    /// 支持 Text、BgColor、TextColor、BorderColor、VariableBindingId 属性。
    /// 联动模型：同时替换 VariableBindings 集合中的 VariableId 和 Actions 集合中的 Target/Value。
    /// </summary>
    private void ApplyParameterSubstitution(HmiControl control, Dictionary<string, string> paramValues)
    {
        control.Text = Substitute(control.Text, paramValues);
        control.BgColor = Substitute(control.BgColor, paramValues);
        control.TextColor = Substitute(control.TextColor, paramValues);
        control.BorderColor = Substitute(control.BorderColor, paramValues);
        control.VariableBindingId = Substitute(control.VariableBindingId, paramValues);
        control.Name = Substitute(control.Name, paramValues) ?? control.Name;

        // 联动模型：替换 VariableBindings 集合中的 VariableId
        foreach (var binding in control.VariableBindings)
        {
            binding.VariableId = Substitute(binding.VariableId, paramValues) ?? binding.VariableId;
        }

        // 联动模型：替换 Actions 集合中的 Target/Value
        foreach (var action in control.Actions)
        {
            action.Target = Substitute(action.Target, paramValues);
            action.Value = Substitute(action.Value, paramValues);
        }

        // 回退旧字段：替换单 Action
        if (control.Action != null)
        {
            control.Action.Target = Substitute(control.Action.Target, paramValues);
            control.Action.Value = Substitute(control.Action.Value, paramValues);
        }

        // 递归处理子控件
        foreach (var child in control.Children)
        {
            ApplyParameterSubstitution(child, paramValues);
        }
    }

    /// <summary>替换字符串中的所有 {{Parameters.参数名}} 占位符。</summary>
    private static string? Substitute(string? input, Dictionary<string, string> paramValues)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return s_placeholderRegex.Replace(input, match =>
        {
            var paramName = match.Groups[1].Value;
            return paramValues.TryGetValue(paramName, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// 深拷贝控件（含子控件），生成**确定性**的新 ID。
    ///
    /// <para><b>为什么必须是确定性的</b>：克隆 id 直接进 C 符号名
    /// （<c>CodeGenerator.Screen.cs</c> 的 <c>static lv_obj_t* obj_{control.Id};</c>，
    /// 以及 SVG / 过程图形控件的 <c>_shapebuf_{Id}</c>）。原实现用
    /// <c>Guid.NewGuid().ToString("N")[..8]</c>，而 <see cref="ExpandScreen"/> 在
    /// <c>GenerateSimulatorModule</c> / <c>GenerateSharedLibraryProject</c> 中都会跑
    /// —— 于是**只要项目里有 Faceplate 实例，导出产物就不可复现**（实测 19/34 个产物
    /// 跨进程不同，连修好模拟正弦相位都救不回来）。</para>
    ///
    /// <para><b>派生规则</b>：<c>{idPrefix}_{index}</c>，子控件递归为
    /// <c>{父克隆id}_{子序号}</c>。顶层 <paramref name="idPrefix"/> 是**经过
    /// <see cref="CIdentifier.ForControlSymbol"/> 净化的实例 id**（空 id 落到 <c>anim</c>），
    /// 因为克隆 id 会原样进 <c>obj_{Id}</c>。选这个规则的理由：</para>
    /// <list type="bullet">
    /// <item>发射器把 <c>obj_*</c> 声明为**文件作用域静态且不带屏幕前缀**，
    /// 故 id 必须**工程内唯一**。以实例 id 为前缀 ⇒ 实例之间天然不撞；
    /// 序号在模板内唯一 ⇒ 同一实例内部不撞。</item>
    /// <item>可追溯：看到 <c>a8f3c1d2_1</c> 就知道它是实例 <c>a8f3c1d2</c> 展开出的
    /// 模板第 1 个控件（嵌套则继续往后缀）。原来的随机 id 完全查不出来源。</item>
    /// <item>不依赖任何运行时随机源 ⇒ 同源导出必得同物。</item>
    /// </list>
    ///
    /// <para><b>已知边界</b>：若两个 Faceplate 实例的 id 相同（同屏重名 → 该项目调用
    /// 发射器本身就会产出重复 <c>obj_*</c>，属非法工程），或实例 id 为空串，
    /// 则派生结果相同。此时工程已另有问题，不在此处兜底。</para>
    /// </summary>
    /// <param name="source">模板内的源控件。</param>
    /// <param name="idPrefix">本次克隆的 id 前缀 —— 顶层为实例 id，子控件为父克隆 id。</param>
    /// <param name="index">该控件在父集合（模板控件列表 / 父控件的 Children）中的序号。</param>
    private static HmiControl CloneControl(HmiControl source, string idPrefix, int index)
    {
        // ① 浅拷贝**全部**属性 —— 逐位复制，将来新增字段自动带上。
        //    手工列属性必然随新增字段漂移：旧实现只赋了 18 个属性，漏掉
        //    ShowPointer / ShowTicks / MinValue / MaxValue / StepValue / IndicatorColor / Rotation /
        //    ImageAssetId / TextAlign / FontSize / Points / Cells / SvgShapes / VisualStates 等约 50 项，
        //    于是 Faceplate 里的 SVG / 表格 / 滑块范围 / 折线 / 图片 / 旋转控件展开后**全部退化成默认值**。
        var clone = source.ShallowCopy();
        clone.Id = $"{idPrefix}_{index}";
        // 父子关系由下面的 Children 重建；沿用模板内的旧 ParentId 会指向一个不存在的控件
        clone.ParentId = null;

        // ② 集合与引用型子对象必须换成独立实例 —— 浅拷贝后它们与源**共享**，
        //    而 ExpandFaceplate 随后会对副本做参数替换（ApplyParameterSubstitution），
        //    共享集合会让替换直接改到**模板本身**，污染后续所有实例。
        clone.Action = source.Action?.ShallowCopy();
        clone.VariableBindings = new ObservableCollection<VariableBindingData>(
            source.VariableBindings.Select(b => b.ShallowCopy()));
        clone.Actions = new ObservableCollection<HmiAction>(
            source.Actions.Select(a => a.ShallowCopy()));
        clone.ParameterBindings = new ObservableCollection<ParameterBinding>(
            source.ParameterBindings.Select(p => p.ShallowCopy()));
        clone.VisualStates = new ObservableCollection<ControlVisualState>(
            source.VisualStates.Select(s => s.ShallowCopy()));
        clone.Cells = source.Cells?.Select(c => c.ShallowCopy()).ToList();
        // PointData 是 record（值语义），拷贝列表即等价深拷贝
        clone.Points = source.Points == null ? null : new List<PointData>(source.Points);
        clone.SvgShapes = source.SvgShapes?.Select(s => s.ShallowCopy()).ToList();

        // ③ 深拷贝子控件 —— 前缀接在父克隆 id 后，形成「实例 → 模板控件 → 子控件」的路径
        clone.Children = new ObservableCollection<HmiControl>();
        for (var j = 0; j < source.Children.Count; j++)
        {
            clone.Children.Add(CloneControl(source.Children[j], clone.Id, j));
        }

        return clone;
    }
}
