namespace HmiEditor.Core.Models;

/// <summary>
/// 「带稳定标识的模型」—— 列表控件（<c>ListBoxItem.Tag</c>）里装的那些模型对象所共有的最小契约。
/// </summary>
/// <remarks>
/// <para>
/// 存在的理由：界面上「按 Id 选中某一行」是常见操作，而落到 <c>Tag</c> 里的对象类型各不相同
/// （变量 / 变量组 / 变量报警 / UDT 定义…）。若靠 <c>Tag.ToString()</c> 去比 <c>Id</c>，
/// 这些模型都没覆写 <c>ToString()</c> ⇒ <c>ToString()</c> 恒为类型全名、**永不相等**
/// ⇒ 选中**静默失败**（登记 #64 就是这么来的）。
/// </para>
/// <para>
/// 所以把「有 <c>Id</c>」这件事从**口头约定**升格为**类型契约**：比对方问的是接口，
/// 不再猜对象长什么样。⚠ **别把它退化成按类型 <c>switch</c> / 反射反推 <c>Id</c>** ——
/// 那样新增一种 Tag 类型时会**静默退回 <c>ToString()</c>**，正好复现本缺陷的形状。
/// </para>
/// </remarks>
public interface IIdentifiedModel
{
    /// <summary>工程内的稳定标识（与 Name 无关；重命名不该丢选中）。</summary>
    string Id { get; }
}
