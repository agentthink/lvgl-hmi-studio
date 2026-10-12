using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 运行时值源 —— 桥接通讯驱动/仿真器/模拟值提供器。
/// 运行时绑定引擎通过此接口与实际数据源交互，编辑器预览/运行时共用。
/// </summary>
public interface IRuntimeValueSource
{
    /// <summary>尝试读取变量当前值。返回 false 表示源无此变量。</summary>
    bool TryRead(string variableId, out double value);

    /// <summary>写入变量值。返回 false 表示写入失败（如只读/未连接）。</summary>
    bool TryWrite(string variableId, double value);

    /// <summary>源是否已连接/可用。</summary>
    bool IsConnected { get; }

    /// <summary>源名称（用于诊断日志）。</summary>
    string Name { get; }
}

/// <summary>
/// 运行时绑定引擎 —— 将控件的 VariableBindings 集合映射为实际通讯标签，
/// 支持 UDT 字段展开、Scale/Offset 换算、连接映射、悬空检测。
///
/// 核心职责（对标 WinCC 运行时变量管理器 / EasyBuilder Pro 的 Tag Database）:
///   - BuildRuntimeTags: 扫描项目所有控件绑定，产出运行时标签表
///   - ReadValue/WriteValue: 通过注入的 IRuntimeValueSource 读写实际值
///   - 工程值 ↔ 原始值 换算 (raw = eng / scale - offset)
///
/// 设计要点 (dev-environment-parity):
///   编辑器预览模式可用模拟值源 (VariableSimulator) 或通讯模拟源，
///   运行时用真实通讯驱动源 —— 引擎本身与数据源解耦。
/// </summary>
public class RuntimeBindingEngine
{
    private readonly HmiProject _project;
    private readonly Dictionary<string, RuntimeTag> _tagsById = new(StringComparer.Ordinal);
    private readonly List<RuntimeTag> _tags = new();

    /// <summary>运行时值源（通讯驱动 / 仿真器 / 模拟提供器）。</summary>
    public IRuntimeValueSource? ValueSource { get; set; }

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public RuntimeBindingEngine(HmiProject project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    /// <summary>全部运行时标签。</summary>
    public IReadOnlyList<RuntimeTag> Tags => _tags;

    /// <summary>标签数量。</summary>
    public int TagCount => _tags.Count;

    // ── 构建 ──

    /// <summary>
    /// 扫描项目所有控件的变量绑定，构建运行时标签表。
    /// 重复变量（多个控件绑定同一变量）合并为单个标签。
    /// </summary>
    public void BuildRuntimeTags()
    {
        _tags.Clear();
        _tagsById.Clear();

        foreach (var screen in _project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                CollectControlBindings(control, screen);
            }
        }

        Log?.Invoke($"[RuntimeBinding] 构建完成: {_tags.Count} 个标签");
    }

    /// <summary>遍历控件绑定集合（含子控件、回退旧字段），收集标签。</summary>
    private void CollectControlBindings(HmiControl control, HmiScreen screen)
    {
        // 联动模型: 集合优先
        foreach (var binding in control.VariableBindings)
        {
            // 表达式绑定: 登记表达式引用的变量为只读标签（运行时由表达式求值消费）
            if (binding.Kind == BindingSourceKind.Expression)
            {
                foreach (var varName in ExpressionBindingEvaluator.ExtractVariables(binding))
                {
                    AddOrMergeTag(varName, BindingDirection.ReadOnly, control, screen, binding.PropertyName);
                }
                continue;
            }

            AddOrMergeTag(binding.VariableId, binding.Direction, control, screen, binding.PropertyName);
        }

        // 回退旧字段
        if (control.VariableBindings.Count == 0 && !string.IsNullOrEmpty(control.VariableBindingId))
        {
            var direction = control.BindingMode == "ReadOnly"
                ? BindingDirection.ReadOnly
                : BindingDirection.ReadWrite;
            AddOrMergeTag(control.VariableBindingId, direction, control, screen, "Text");
        }

        // 递归子控件
        foreach (var child in control.Children)
        {
            CollectControlBindings(child, screen);
        }
    }

    /// <summary>添加或合并标签 —— 相同变量合并，方向提升（任一写方向 → 写方向）。</summary>
    private void AddOrMergeTag(
        string variableId,
        BindingDirection direction,
        HmiControl control,
        HmiScreen screen,
        string propertyName)
    {
        if (string.IsNullOrEmpty(variableId)) return;

        if (_tagsById.TryGetValue(variableId, out var existing))
        {
            // 方向合并: 只要有一个绑定允许写，标签即可写
            if (direction != BindingDirection.ReadOnly)
                existing.Direction = direction;
            return;
        }

        var tag = ResolveVariableTag(variableId, direction, control, screen, propertyName);
        if (tag == null) return;

        _tags.Add(tag);
        _tagsById[variableId] = tag;
    }

    /// <summary>解析变量为运行时标签 —— 支持普通变量与 UDT 字段。</summary>
    private RuntimeTag? ResolveVariableTag(
        string variableId,
        BindingDirection direction,
        HmiControl control,
        HmiScreen screen,
        string propertyName)
    {
        var resolved = _project.VariableManager.ResolveBinding(variableId);
        string address;
        string dataType;
        string connectionId;
        string slaveId;
        double scale;
        double offset;
        string? unit;

        switch (resolved)
        {
            case Variable v:
                address = v.Address;
                dataType = v.DataType;
                connectionId = v.ConnectionId;
                scale = v.Scale;
                offset = v.Offset;
                unit = v.Unit;
                slaveId = v.SlaveId;
                break;

            case UdtFieldVariable f:
                address = f.Address;
                dataType = f.DataType;
                connectionId = f.ConnectionId;
                scale = f.Scale;
                offset = f.Offset;
                unit = f.Unit;
                slaveId = f.SlaveId;
                break;

            default:
                // 无法解析 → 悬空绑定，跳过（保存时 FindDanglingReferences 会提示）
                return null;
        }

        // 查找连接
        var connection = _project.VariableManager.Connections
            .FirstOrDefault(c => c.Id == connectionId);
        var slave = connection?.Slaves.FirstOrDefault(s => s.Id == slaveId);

        // 地址编码约定（2026-08-18 修正）：
        //   - Address  = 显示/诊断用地址。有子设备时带 <站号>:<地址> 前缀（如 "1:40001"），
        //     供 UI/模拟器/日志区分从站；无子设备即原地址。
        //   - RawAddress = 纯 Modbus 地址（"40001"），供固件轮询绑定使用。
        //   - SlaveAddress = 从站号（binding.unit）；固件 hmi_tag_parse_addr 的 "%d:%d"
        //     分支语义是「功能码:偏移」，不是「站号:地址」——把站号拼进地址会把站号
        //     当功能码、完整地址当偏移（40001 偏移越界/错区），导致轮询组请求错误。
        //     因此从站路由必须走 tag_binding_t.unit，地址必须为纯地址。
        var effectiveAddress = slave != null && slave.SlaveAddress > 0
            ? $"{slave.SlaveAddress}:{address}"
            : address;

        return new RuntimeTag
        {
            VariableId = variableId,
            Address = effectiveAddress,
            RawAddress = address,
            DataType = dataType,
            ConnectionId = connectionId,
            ConnectionName = connection?.Name ?? "",
            DriverId = connection?.DriverId ?? "",
            SlaveId = slaveId,
            SlaveAddress = slave?.SlaveAddress ?? 0,
            Direction = direction,
            Scale = scale,
            Offset = offset,
            Unit = unit,
            ScreenName = screen.Name,
            ControlName = control.Name,
            ControlId = control.Id,
            PropertyName = propertyName
        };
    }

    // ── 读写 ──

    /// <summary>读取变量工程值（含 Scale/Offset 换算：eng = raw * scale + offset）。</summary>
    public double? ReadValue(string variableId)
    {
        if (ValueSource == null || !ValueSource.IsConnected)
            return null;

        if (!ValueSource.TryRead(variableId, out var raw))
            return null;

        if (!_tagsById.TryGetValue(variableId, out var tag))
            return raw;

        // 原始值 → 工程值
        return raw * tag.Scale + tag.Offset;
    }

    /// <summary>写入变量工程值（反向换算：raw = (eng - offset) / scale）。</summary>
    public bool WriteValue(string variableId, double engineeringValue)
    {
        if (ValueSource == null || !ValueSource.IsConnected)
        {
            Log?.Invoke($"[RuntimeBinding] 写入失败: 无值源或未连接 ({variableId})");
            return false;
        }

        if (!_tagsById.TryGetValue(variableId, out var tag))
        {
            Log?.Invoke($"[RuntimeBinding] 写入失败: 变量未绑定 ({variableId})");
            return false;
        }

        if (tag.Direction == BindingDirection.ReadOnly)
        {
            Log?.Invoke($"[RuntimeBinding] 写入拒绝: 只读绑定 ({variableId})");
            return false;
        }

        // 工程值 → 原始值
        var raw = tag.Scale != 0
            ? (engineeringValue - tag.Offset) / tag.Scale
            : engineeringValue;

        return ValueSource.TryWrite(variableId, raw);
    }

    /// <summary>获取单个标签。</summary>
    public RuntimeTag? FindTag(string variableId)
    {
        _tagsById.TryGetValue(variableId, out var tag);
        return tag;
    }
}

/// <summary>
/// 运行时标签 —— 变量绑定解析为通讯层的具体条目。
/// </summary>
public class RuntimeTag
{
    /// <summary>变量标识（绑定 Id）。</summary>
    public string VariableId { get; set; } = "";

    /// <summary>通讯地址（显示/诊断用；有子设备时为 "站号:地址" 形式，如 "1:40001"）。</summary>
    public string Address { get; set; } = "";

    /// <summary>纯 Modbus 地址（无站号前缀，如 "40001"）—— 固件 tag_binding_t.addr 使用。</summary>
    public string RawAddress { get; set; } = "";

    /// <summary>数据类型（"Int16" / "Float" / "Bool" 等）。</summary>
    public string DataType { get; set; } = "Int16";

    /// <summary>所属连接 Id。</summary>
    public string ConnectionId { get; set; } = "";

    /// <summary>连接名称。</summary>
    public string ConnectionName { get; set; } = "";

    /// <summary>驱动 Id（如 "modbus-tcp"）。</summary>
    public string DriverId { get; set; } = "";

    /// <summary>归属子设备（从站）Id；空表示直接挂连接。</summary>
    public string SlaveId { get; set; } = "";

    /// <summary>从站地址（解析自 SlaveId 对应子设备；无子设备时为 0）。</summary>
    public int SlaveAddress { get; set; }

    /// <summary>绑定方向。</summary>
    public BindingDirection Direction { get; set; } = BindingDirection.ReadWrite;

    /// <summary>工程换算系数（eng = raw * scale + offset）。</summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>工程换算偏移。</summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>单位。</summary>
    public string? Unit { get; set; }

    /// <summary>所属屏幕名。</summary>
    public string ScreenName { get; set; } = "";

    /// <summary>绑定控件名。</summary>
    public string ControlName { get; set; } = "";

    /// <summary>绑定控件 Id。</summary>
    public string ControlId { get; set; } = "";

    /// <summary>绑定的控件属性名。</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>标签描述（诊断用）。</summary>
    public string Description => $"[{ScreenName}] {ControlName}.{PropertyName} → {Address} ({ConnectionName})";
}
