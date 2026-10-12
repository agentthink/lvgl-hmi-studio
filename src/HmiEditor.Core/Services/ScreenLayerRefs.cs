using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 图层引用（公共页面作为背景图层）的收集与校验工具。
///
/// 规则：
///   1. 仅可引用 IsPublicScreen=true 的屏幕作为图层；
///   2. 禁止屏幕引用自身；
///   3. 被引用的图层屏幕不再作为独立屏幕切换（纯图层，注册表排除）；
///   4. 图层屏幕不能再引用其他图层（仅支持一层嵌套）；
///   5. 多个屏幕可引用同一个图层（运行时共享单实例，随屏幕切换转移挂载）。
/// </summary>
public static class ScreenLayerRefs
{
    /// <summary>
    /// 收集所有被引用为图层的屏幕 Id 集合。
    /// 这些屏幕应从可独立切换的屏幕注册表中排除。
    /// </summary>
    public static HashSet<string> CollectReferencedLayerIds(HmiProject project)
    {
        var set = new HashSet<string>();
        foreach (var screen in project.Screens)
        {
            foreach (var layerId in screen.ReferencedLayerIds)
            {
                if (!string.IsNullOrEmpty(layerId))
                    set.Add(layerId);
            }
        }
        return set;
    }

    /// <summary>
    /// 校验项目的图层引用关系，返回错误描述列表（空 = 合法）。
    /// </summary>
    public static IReadOnlyList<string> Validate(HmiProject project)
    {
        var errors = new List<string>();
        var byId = project.Screens.ToDictionary(s => s.Id);

        foreach (var screen in project.Screens)
        {
            foreach (var layerId in screen.ReferencedLayerIds)
            {
                if (string.IsNullOrEmpty(layerId)) continue;

                if (layerId == screen.Id)
                {
                    errors.Add($"屏幕 '{screen.Name}' 不能引用自身作为图层");
                    continue;
                }

                if (!byId.TryGetValue(layerId, out var layer))
                {
                    errors.Add($"屏幕 '{screen.Name}' 引用了不存在的图层 '{layerId}'");
                    continue;
                }

                if (!layer.IsPublicScreen)
                {
                    errors.Add($"屏幕 '{screen.Name}' 引用的 '{layer.Name}' 不是公共页面（仅公共页面可作为图层）");
                    continue;
                }

                if (layer.ReferencedLayerIds.Count > 0)
                {
                    errors.Add($"图层 '{layer.Name}' 不能再引用其他图层（仅支持一层嵌套）");
                }
            }
        }

        return errors;
    }
}
