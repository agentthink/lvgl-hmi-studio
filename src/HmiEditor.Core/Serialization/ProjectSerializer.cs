using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Serialization;

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = JsonOptionsDefaults.Default;

    public static string Serialize(HmiProject project)
    {
        return JsonSerializer.Serialize(project, Options);
    }

    public static HmiProject? Deserialize(string json)
    {
        try
        {
            var project = JsonSerializer.Deserialize<HmiProject>(json, Options);
            if (project == null) return null;

            // 迁移旧模型到新联动模型
            MigrateLegacyBindings(project);
            return project;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }

    /// <summary>
    /// 将旧 JSON 格式 (variableBindingId string + action 单个) 迁移到新联动模型 (variableBindings 集合 + actions 集合)。
    /// 迁移后保留旧字段作为回退，不破坏任何数据。
    /// </summary>
    private static void MigrateLegacyBindings(HmiProject project)
    {
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                MigrateLegacyControl(control);
            }
        }
    }

    private static void MigrateLegacyControl(HmiControl control)
    {
        // 迁移单一 VariableBindingId → VariableBindings 集合
        if (!string.IsNullOrEmpty(control.VariableBindingId) && control.VariableBindings.Count == 0)
        {
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = "Text",
                VariableId = control.VariableBindingId,
                Direction = control.BindingMode == "ReadOnly"
                    ? BindingDirection.ReadOnly
                    : BindingDirection.ReadWrite
            });
        }

        // 迁移单一 Action → Actions 集合
        if (control.Action != null && control.Actions.Count == 0)
        {
            control.Actions.Add(control.Action);
        }
    }
}
