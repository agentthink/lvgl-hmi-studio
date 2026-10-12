using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// 控件模板（Faceplate）—— 一组控件的参数化预设组合。
/// 对标 WinCC Faceplate / FactoryTalk Global Object / Ignition Template。
///
/// 定义阶段：在模板编辑器中摆放控件并声明参数接口（Input/Output/InOut）。
/// 实例化阶段：在画布上放置 Faceplate 实例，为每个参数提供绑定值。
/// 代码生成阶段：FaceplateExpander 展开实例为控件副本，执行参数替换。
///
/// 模板内部控件通过占位符引用参数："{{Parameters.参数名}}"
/// 占位符可出现在 Text、BgColor、TextColor、BorderColor、VariableBindingId 等字符串属性中。
/// </summary>
public class ControlTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New Template";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "通用";

    /// <summary>模板版本号 —— 每次保存自动递增。实例可固定版本或跟随最新。</summary>
    public int Version { get; set; } = 1;

    /// <summary>模板包含的控件定义（相对坐标，左上角为 0,0）。</summary>
    public ObservableCollection<HmiControl> Controls { get; set; } = new();

    /// <summary>模板参数接口定义 —— 实例化时由 ParameterBindings 提供实际值。</summary>
    public ObservableCollection<TemplateParameter> Parameters { get; set; } = new();
}
