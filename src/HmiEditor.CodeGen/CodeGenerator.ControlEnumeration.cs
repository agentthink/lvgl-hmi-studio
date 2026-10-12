using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— 「**哪些控件会被发射**」的唯一定义。
///
/// <para><b>为什么必须收口到一个入口</b>：一个工程里有两种"控件清单"——
/// 屏幕上的**原始**控件（<c>screen.Controls</c>，其中 Faceplate 实例只是一个占位）
/// 与**展开后**的原子控件（<c>FaceplateExpander.ExpandScreen</c>）。
/// <c>GenerateScreenCode</c> 一直用的是后者，于是凡是用前者的发射器都会与它错位。</para>
///
/// <para><b>错位的实际后果</b>（实测，2026-09-15）：</para>
/// <list type="bullet">
///   <item><c>GeneratePointerDeclarations</c> / <c>AppendPointerValueCallbacks</c>：
///   Faceplate 模板含 Slider/Bar + ShowPointer 时，克隆控件会发射
///   <c>_cur_&lt;cloneId&gt; = lv_obj_create(o);</c> 与
///   <c>lv_obj_add_event_cb(o, _bar_&lt;cloneId&gt;_val_cb, …)</c>，
///   而那个文件级 static 从未声明、那个回调从未定义
///   ⇒ <b>C 编译错误</b>（未声明的标识符 / 隐式函数声明）。</item>
///   <item><c>GenerateRefreshBindings</c>：克隆控件的绑定静默不刷新 ——
///   不是编译错误，但功能缺失（克隆控件的 VariableBindings 是深拷贝来的，本该刷新）。</item>
/// </list>
///
/// <para><b>约定</b>：任何遍历"项目里有哪些控件"的发射器都必须经
/// <see cref="EnumerateRenderableControls(HmiProject)"/> —— 不要直接遍历
/// <c>project.Screens[].Controls</c>。守卫见
/// <c>ControlSymbolSanitizationTests.ExpandedControls_HaveMatchingPointerDeclarationsAndCallbacks</c>。</para>
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 枚举一个屏幕上**实际会被渲染发射**的控件 —— 即 Faceplate 实例展开后的原子控件。
    /// 无 <paramref name="expander"/> 时退化为原始控件列表（兼容旧调用路径）。
    /// </summary>
    internal static List<HmiControl> EnumerateRenderableControls(HmiScreen screen, FaceplateExpander? expander)
        => expander != null ? expander.ExpandScreen(screen) : screen.Controls.ToList();

    /// <summary>
    /// 枚举整个项目**实际会被渲染发射**的控件（跨屏，保持屏幕顺序与屏内控件顺序）。
    /// 展开器在内部按 <c>project.Templates</c> 构造，与被调用的 <c>GenerateScreenCode</c> 同一语义。
    ///
    /// <para>每次调用都会重新展开一遍（<see cref="FaceplateExpander.ExpandScreen"/> 不改动源数据）。
    /// 克隆 id 是**确定性**派生的（见 <c>FaceplateExpander.CloneControl</c>），
    /// 所以多处调用得到的符号名一致 —— 这正是"声明侧与使用侧能对上"的前提。</para>
    /// </summary>
    internal static IEnumerable<HmiControl> EnumerateRenderableControls(HmiProject project)
    {
        var expander = new FaceplateExpander(project.Templates);
        foreach (var screen in project.Screens)
        {
            foreach (var control in EnumerateRenderableControls(screen, expander))
            {
                yield return control;
            }
        }
    }
}
