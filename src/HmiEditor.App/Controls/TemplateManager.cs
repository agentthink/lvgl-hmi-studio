using HmiEditor.Core.Models;
using HmiEditor.App.Resources;
using HmiEditor.Core.Serialization;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HmiEditor.App.Controls;

/// <summary>
/// 模板管理器 —— 管理 Faceplate 模板的创建、存储、加载和实例化。
///
/// Phase R2 扩展：支持参数化模板（TemplateParameter）和 Faceplate 实例化。
/// 实例化时创建带 TemplateId 的 HmiControl 占位控件，而非直接展开控件。
/// 代码生成阶段由 FaceplateExpander 展开实例。
/// </summary>
public class TemplateManager
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsDefaults.Default;
    private readonly ObservableCollection<ControlTemplate> _templates;

    /// <summary>
    /// 实例默认名的进程内序号（阶段 121）。
    /// 旧实现用 `DateTime.Now.Millisecond:D3` 做后缀 ⇒ **同一毫秒内建两个实例会重名**，
    /// 而且后缀每秒重复；这里改成单调序号，保证同一进程内不重名。
    /// </summary>
    private static int _instanceSeq;

    /// <summary>模板列表（可绑定）。</summary>
    public ObservableCollection<ControlTemplate> Templates => _templates;

    /// <summary>模板列表变化事件（通知 Toolbox 刷新）。</summary>
    public event EventHandler? TemplatesChanged;

    /// <summary>
    /// 默认构造 —— 创建内部模板集合（独立使用场景）。
    /// </summary>
    public TemplateManager()
    {
        _templates = new ObservableCollection<ControlTemplate>();
    }

    /// <summary>
    /// 注入构造 —— 绑定到外部集合（如 HmiProject.Templates）。
    /// 所有增删操作直接作用于外部集合，保证数据源唯一。
    /// </summary>
    /// <param name="externalTemplates">外部模板集合（通常为 HmiProject.Templates）。</param>
    public TemplateManager(ObservableCollection<ControlTemplate> externalTemplates)
    {
        _templates = externalTemplates ?? throw new ArgumentNullException(nameof(externalTemplates));
    }

    // ═══════════════════════════════════════════════
    //  模板创建与管理
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 从选中控件创建参数化模板。
    /// 控件坐标转为相对坐标（包围盒左上角为原点）。
    /// </summary>
    public ControlTemplate CreateFromSelection(
        string name,
        string category,
        IEnumerable<HmiControl> selectedControls,
        string description = "")
    {
        var controlList = selectedControls.ToList();
        if (controlList.Count == 0)
            throw new ArgumentException(AppLocalization.Service["tpl.err.no.controls"]);

        var minX = controlList.Min(c => c.X);
        var minY = controlList.Min(c => c.Y);

        var template = new ControlTemplate
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Description = description,
            Category = category,
            Version = 1
        };

        foreach (var c in controlList)
        {
            var cloned = new HmiControl
            {
                Type = c.Type,
                Name = c.Name,
                X = c.X - minX,
                Y = c.Y - minY,
                Width = c.Width,
                Height = c.Height,
                Text = c.Text,
                BgColor = c.BgColor,
                TextColor = c.TextColor,
                Radius = c.Radius,
                BorderWidth = c.BorderWidth,
                BorderColor = c.BorderColor,
                Value = c.Value,
                IsChecked = c.IsChecked,
                VariableBindingId = c.VariableBindingId
            };

            // 联动模型：克隆 VariableBindings 和 Actions 集合
            foreach (var b in c.VariableBindings)
                cloned.VariableBindings.Add(new VariableBindingData
                {
                    PropertyName = b.PropertyName,
                    VariableId = b.VariableId,
                    Direction = b.Direction
                });
            foreach (var a in c.Actions)
                cloned.Actions.Add(new HmiAction
                {
                    ActionType = a.ActionType,
                    Target = a.Target,
                    Value = a.Value,
                    TriggerEvent = a.TriggerEvent
                });

            template.Controls.Add(cloned);
        }

        _templates.Add(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
        return template;
    }

    /// <summary>添加参数到模板。</summary>
    public void AddParameter(ControlTemplate template, TemplateParameter parameter)
    {
        template.Parameters.Add(parameter);
        template.Version++;
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>从模板移除参数。</summary>
    public void RemoveParameter(ControlTemplate template, TemplateParameter parameter)
    {
        template.Parameters.Remove(parameter);
        template.Version++;
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>删除模板。</summary>
    public void RemoveTemplate(ControlTemplate template)
    {
        _templates.Remove(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按 ID 查找模板。</summary>
    public ControlTemplate? FindById(string templateId)
    {
        return _templates.FirstOrDefault(t => t.Id == templateId);
    }

    /// <summary>导入预置/默认模板。</summary>
    public void AddPredefined(IEnumerable<ControlTemplate> templates)
    {
        foreach (var t in templates)
            _templates.Add(t);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════
    //  模板应用（非参数化直接展开）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 直接展开模板到画布 —— 复制模板内控件到指定位置。
    /// 用于工具箱中的非参数化模板快速放置。
    /// 参数化模板（有 Parameters 的）应使用 CreateFaceplateInstance。
    /// </summary>
    public List<HmiControl> ApplyTemplate(ControlTemplate template, double x, double y)
    {
        var result = new List<HmiControl>();
        foreach (var c in template.Controls)
        {
            var cloned = new HmiControl
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = c.Type,
                Name = c.Name,
                X = c.X + x,
                Y = c.Y + y,
                Width = c.Width,
                Height = c.Height,
                Text = c.Text,
                BgColor = c.BgColor,
                TextColor = c.TextColor,
                Radius = c.Radius,
                BorderWidth = c.BorderWidth,
                BorderColor = c.BorderColor,
                Value = c.Value,
                IsChecked = c.IsChecked,
                VariableBindingId = c.VariableBindingId
            };

            // 联动模型：克隆 VariableBindings 和 Actions 集合
            foreach (var b in c.VariableBindings)
                cloned.VariableBindings.Add(new VariableBindingData
                {
                    PropertyName = b.PropertyName,
                    VariableId = b.VariableId,
                    Direction = b.Direction
                });
            foreach (var a in c.Actions)
                cloned.Actions.Add(new HmiAction
                {
                    ActionType = a.ActionType,
                    Target = a.Target,
                    Value = a.Value,
                    TriggerEvent = a.TriggerEvent
                });

            result.Add(cloned);
        }
        return result;
    }

    // ═══════════════════════════════════════════════
    //  Faceplate 实例化
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 创建 Faceplate 实例 —— 在画布上放置一个引用模板的占位控件。
    /// 画布上表现为矩形占位，代码生成时由 FaceplateExpander 展开。
    /// </summary>
    /// <param name="template">要实例化的模板。</param>
    /// <param name="x">画布上的 X 坐标。</param>
    /// <param name="y">画布上的 Y 坐标。</param>
    /// <param name="name">实例名称。</param>
    /// <returns>带 TemplateId 的占位 HmiControl。</returns>
    public HmiControl CreateFaceplateInstance(
        ControlTemplate template,
        double x,
        double y,
        string? name = null)
    {
        // 计算模板包围盒尺寸
        var width = template.Controls.Count > 0
            ? template.Controls.Max(c => c.X + c.Width)
            : 100;
        var height = template.Controls.Count > 0
            ? template.Controls.Max(c => c.Y + c.Height)
            : 60;

        var instance = new HmiControl
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Type = HmiControlType.Screen, // 占位类型
            Name = name ?? $"{template.Name} #{Interlocked.Increment(ref _instanceSeq)}",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            TemplateId = template.Id,
            TemplateVersion = template.Version
        };

        // 用模板参数的默认值预填充绑定
        foreach (var param in template.Parameters)
        {
            instance.ParameterBindings.Add(new ParameterBinding
            {
                ParameterName = param.Name,
                Value = param.DefaultValue,
                VariableId = param.Type == ParameterType.VariableId ? param.DefaultValue : null
            });
        }

        return instance;
    }

    /// <summary>
    /// 更新 Faceplate 实例的参数绑定值。
    /// </summary>
    public void UpdateParameterBinding(
        HmiControl instance,
        string parameterName,
        string? value,
        string? variableId = null)
    {
        var binding = instance.ParameterBindings.FirstOrDefault(
            b => b.ParameterName == parameterName);

        if (binding != null)
        {
            binding.Value = value;
            binding.VariableId = variableId;
        }
        else
        {
            instance.ParameterBindings.Add(new ParameterBinding
            {
                ParameterName = parameterName,
                Value = value,
                VariableId = variableId
            });
        }
    }

    /// <summary>
    /// 获取 Faceplate 实例的参数绑定摘要（用于 UI 显示）。
    /// </summary>
    public List<(TemplateParameter Param, ParameterBinding? Binding)> GetParameterSummary(
        ControlTemplate template,
        HmiControl instance)
    {
        var result = new List<(TemplateParameter, ParameterBinding?)>();
        foreach (var param in template.Parameters)
        {
            var binding = instance.ParameterBindings.FirstOrDefault(
                b => b.ParameterName == param.Name);
            result.Add((param, binding));
        }
        return result;
    }

    // ═══════════════════════════════════════════════
    //  序列化
    // ═══════════════════════════════════════════════

    /// <summary>序列化为 JSON。</summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(_templates.ToList(), JsonOptions);
    }

    /// <summary>从 JSON 反序列化。</summary>
    public void Deserialize(string json)
    {
        var templates = JsonSerializer.Deserialize<List<ControlTemplate>>(json, JsonOptions);
        if (templates != null)
        {
            _templates.Clear();
            foreach (var t in templates)
                _templates.Add(t);
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ═══════════════════════════════════════════════
    //  预置模板
    // ═══════════════════════════════════════════════

    /// <summary>创建预置模板集（含参数化 Faceplate）。</summary>
    public static List<ControlTemplate> CreateDefaultTemplates()
    {
        return new List<ControlTemplate>
        {
            // ── 参数化模板：电机面板 ──
            new()
            {
                Id = "tpl_motor",
                Name = HmiEditor.App.Resources.AppLocalization.Service["tpl.motor.name"],
                Description = HmiEditor.App.Resources.AppLocalization.Service["tpl.motor.desc"],
                Category = HmiEditor.App.Resources.AppLocalization.Service["fp.cat.general"],
                Version = 1,
                Controls = new ObservableCollection<HmiControl>
                {
                    new()
                    {
                        Type = HmiControlType.Label, Name = "Title",
                        X = 0, Y = 0, Width = 120, Height = 24,
                        Text = "{{Parameters.Title}}"
                    },
                    new()
                    {
                        Type = HmiControlType.Label, Name = "Status",
                        X = 0, Y = 30, Width = 120, Height = 24,
                        Text = "Idle",
                        VariableBindingId = "{{Parameters.StatusVar}}"
                    }
                },
                Parameters = new ObservableCollection<TemplateParameter>
                {
                    new()
                    {
                        Name = "Title",
                        DisplayName = HmiEditor.App.Resources.AppLocalization.Service["tpl.motor.p.title"],
                        Direction = ParameterDirection.Input,
                        Type = ParameterType.String,
                        DefaultValue = "Motor"
                    },
                    new()
                    {
                        Name = "StatusVar",
                        DisplayName = HmiEditor.App.Resources.AppLocalization.Service["tpl.motor.p.statusvar"],
                        Direction = ParameterDirection.Input,
                        Type = ParameterType.VariableId
                    }
                }
            },
            // ── 简单模板：启动/停止按钮组 ──
            new()
            {
                Name = HmiEditor.App.Resources.AppLocalization.Service["tpl.startstop.name"],
                Description = HmiEditor.App.Resources.AppLocalization.Service["tpl.startstop.desc"],
                Category = HmiEditor.App.Resources.AppLocalization.Service["fp.cat.industrial"],
                Controls = new ObservableCollection<HmiControl>
                {
                    new() { Type = HmiControlType.Button, Name = HmiEditor.App.Resources.AppLocalization.Service[Lang.TplStartStopNameStart], X = 0, Y = 0, Width = 80, Height = 44, Text = HmiEditor.App.Resources.AppLocalization.Service["tpl.startstop.btn.start"], BgColor = "#22c55e" },
                    new() { Type = HmiControlType.Button, Name = HmiEditor.App.Resources.AppLocalization.Service[Lang.TplStartStopNameStop], X = 90, Y = 0, Width = 80, Height = 44, Text = HmiEditor.App.Resources.AppLocalization.Service["tpl.startstop.btn.stop"], BgColor = "#ef4444" },
                }
            },
            // ── 参数化模板：阀门控制器 ──
            new()
            {
                Id = "tpl_valve",
                Name = HmiEditor.App.Resources.AppLocalization.Service["tpl.valve.name"],
                Description = HmiEditor.App.Resources.AppLocalization.Service["tpl.valve.desc"],
                Category = HmiEditor.App.Resources.AppLocalization.Service["fp.cat.piping"],
                Version = 2,
                Controls = new ObservableCollection<HmiControl>
                {
                    new()
                    {
                        Type = HmiControlType.Label, Name = "ValveName",
                        X = 0, Y = 0, Width = 100, Height = 20,
                        Text = "{{Parameters.ValveName}}"
                    },
                    new()
                    {
                        Type = HmiControlType.Led, Name = "ValveLED",
                        X = 0, Y = 25, Width = 20, Height = 20
                    },
                    new()
                    {
                        Type = HmiControlType.Bar, Name = "OpenLevel",
                        X = 25, Y = 25, Width = 80, Height = 20,
                        VariableBindingId = "{{Parameters.OpenVar}}"
                    }
                },
                Parameters = new ObservableCollection<TemplateParameter>
                {
                    new()
                    {
                        Name = "ValveName",
                        DisplayName = HmiEditor.App.Resources.AppLocalization.Service["tpl.valve.p.valvename"],
                        Direction = ParameterDirection.Input,
                        Type = ParameterType.String,
                        DefaultValue = "Valve"
                    },
                    new()
                    {
                        Name = "OpenVar",
                        DisplayName = HmiEditor.App.Resources.AppLocalization.Service["tpl.valve.p.openvar"],
                        Direction = ParameterDirection.Input,
                        Type = ParameterType.VariableId
                    }
                }
            },
            // ── 简单模板：电机状态指示 ──
            new()
            {
                Name = HmiEditor.App.Resources.AppLocalization.Service["tpl.motorind.name"],
                Description = HmiEditor.App.Resources.AppLocalization.Service["tpl.motorind.desc"],
                Category = HmiEditor.App.Resources.AppLocalization.Service["fp.cat.industrial"],
                Controls = new ObservableCollection<HmiControl>
                {
                    new() { Type = HmiControlType.Led, Name = HmiEditor.App.Resources.AppLocalization.Service[Lang.TplMotorIndNameLed], X = 0, Y = 0, Width = 30, Height = 30 },
                    new() { Type = HmiControlType.Label, Name = HmiEditor.App.Resources.AppLocalization.Service[Lang.TplMotorIndNameState], X = 40, Y = 4, Width = 120, Height = 24, Text = HmiEditor.App.Resources.AppLocalization.Service["tpl.motorind.text.running"] },
                }
            },
        };
    }
}
