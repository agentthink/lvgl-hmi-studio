using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// 通讯断线后的恢复策略。
/// </summary>
public enum ConnectionRecoveryMode
{
    /// <summary>自动重连（默认）：断线后立即尝试重连。</summary>
    AutoReconnect = 0,

    /// <summary>手动重连：需用户手动触发重连。</summary>
    ManualReconnect = 1,

    /// <summary>快速失败：断线后立即标记设备离线，不尝试重连。</summary>
    FailFast = 2,
}

/// <summary>
/// HMI 项目变量管理 —— 管理设备连接和全局变量表。
/// 控件通过 VariableBinding 将属性绑定到此管理器中的变量。
/// R3-T1: 支持 UDT（用户自定义类型）—— 定义结构化类型，实例化后按字段绑定。
/// </summary>
public class VariableManager
{
    public ObservableCollection<DeviceConnection> Connections { get; set; } = new();
    public ObservableCollection<Variable> Variables { get; set; } = new();
    public ObservableCollection<VariableGroup> Groups { get; set; } = new();

    /// <summary>R3-T1: UDT 类型定义注册表。</summary>
    public ObservableCollection<UdtDefinition> UdtDefinitions { get; set; } = new();

    /// <summary>系统内部变量（只读，运行时不可修改）。</summary>
    public IReadOnlyList<SystemVariable> SystemVariables => SystemVariable.Defaults;

    // ── 通讯默认策略（工程级）──

    /// <summary>
    /// 变量默认扫描周期（毫秒）。单个 Variable 未指定 ScanInterval 时使用此值。
    /// </summary>
    public int DefaultScanIntervalMs { get; set; } = 100;

    /// <summary>
    /// 通讯请求默认超时（毫秒）。超过此时间未收到响应则判定超时。
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 500;

    /// <summary>
    /// 通讯请求最大重试次数。超时或 CRC 错误时自动重试的次数。
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 重试退避间隔（毫秒）。每次重试之间的等待时间（线性递增：100/200/300...）。
    /// </summary>
    public int RetryBackoffMs { get; set; } = 100;

    /// <summary>
    /// 是否启用写操作合并。同一扫描周期内多个写请求合并为一次 Modbus 写事务。
    /// </summary>
    public bool EnableWriteCoalescing { get; set; } = true;

    /// <summary>
    /// 通讯断线后的恢复策略。
    /// </summary>
    public ConnectionRecoveryMode ConnectionRecoveryMode { get; set; } = ConnectionRecoveryMode.AutoReconnect;

    // ── R3-T1: UDT 操作 ──

    /// <summary>注册 UDT 定义。</summary>
    public UdtDefinition RegisterUdt(UdtDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        UdtDefinitions.Add(definition);
        return definition;
    }

    /// <summary>按名称创建并注册一个新 UDT 定义。</summary>
    public UdtDefinition RegisterUdt(string name, string? description = null)
    {
        var def = new UdtDefinition { Name = name, Description = description };
        return RegisterUdt(def);
    }

    /// <summary>按 Id 查找 UDT 定义。</summary>
    public UdtDefinition? FindUdtDefinition(string udtTypeId)
    {
        if (string.IsNullOrEmpty(udtTypeId)) return null;
        return UdtDefinitions.FirstOrDefault(d => d.Id == udtTypeId);
    }

    /// <summary>按名称查找 UDT 定义。</summary>
    public UdtDefinition? FindUdtByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return UdtDefinitions.FirstOrDefault(d =>
            string.Equals(d.Name, name, StringComparison.Ordinal));
    }

    /// <summary>移除 UDT 定义。如果存在实例则返回 false（需先移除实例）。</summary>
    public bool RemoveUdtDefinition(string udtTypeId)
    {
        var def = FindUdtDefinition(udtTypeId);
        if (def == null) return false;

        // 检查是否有实例正在使用此 UDT
        var hasInstances = Variables.Any(v => v.UdtTypeId == udtTypeId);
        if (hasInstances) return false;

        return UdtDefinitions.Remove(def);
    }

    /// <summary>创建 UDT 实例并添加到变量表。</summary>
    public Variable CreateUdtInstance(UdtDefinition definition, string instanceName, string groupId = "")
    {
        ArgumentNullException.ThrowIfNull(definition);
        var instance = definition.CreateInstance(instanceName, groupId);
        Variables.Add(instance);
        return instance;
    }

    // ── 绑定解析 ──

    /// <summary>
    /// 解析变量绑定 Id。支持两种格式：
    /// 1. 普通变量：直接匹配 Variable.Id 或 Variable.Name
    /// 2. UDT 字段：实例名.字段名（如 "motor1.speed"），返回展开的 UdtFieldVariable
    ///
    /// <para><b>点号不是 UDT 字段的独占标记。</b>PLC/CSV 导入的变量名本身就常带点号
    /// （<c>Motor.Speed</c>、<c>DB1.DBD0</c>、<c>Line1.Temp</c>）。因此 UDT 字段解析
    /// 失败时必须**继续**回退到字面 Id/名匹配 —— 否则这类绑定会静默解析不到变量，
    /// 既不进运行时标签表，预览里也永远是空值（UDT 字段命中时仍然优先）。</para>
    /// </summary>
    public object? ResolveBinding(string bindingId)
    {
        if (string.IsNullOrEmpty(bindingId)) return null;

        // 先尝试 UDT 字段访问（含点号）：实例名.字段名
        var dotIndex = bindingId.IndexOf('.');
        if (dotIndex > 0 && dotIndex < bindingId.Length - 1)
        {
            var instanceName = bindingId[..dotIndex];
            var fieldName = bindingId[(dotIndex + 1)..];

            var instance = Variables.FirstOrDefault(v =>
                string.Equals(v.Name, instanceName, StringComparison.Ordinal) && v.IsUdtInstance);
            var def = instance is null ? null : FindUdtDefinition(instance.UdtTypeId);
            if (def?.FindField(fieldName) is { } field)
            {
                return def.ExpandInstance(instance!)
                    .FirstOrDefault(f => f.FieldName == fieldName);
            }
            // 不是 UDT 字段 → 落下去做字面匹配（变量名/Id 自带点号的情形）
        }

        // 普通变量：先按 Id 匹配，再按名称匹配
        return (Variables.FirstOrDefault(v => v.Id == bindingId))
            ?? Variables.FirstOrDefault(v =>
                string.Equals(v.Name, bindingId, StringComparison.Ordinal));
    }

    /// <summary>
    /// 获取所有可绑定的变量项（包含普通变量和展开的 UDT 字段变量）。
    /// 用于变量浏览器树形展示。
    /// </summary>
    public List<BindableVariableItem> GetAllBindableItems()
    {
        var result = new List<BindableVariableItem>();

        foreach (var variable in Variables)
        {
            if (variable.IsUdtInstance)
            {
                var def = FindUdtDefinition(variable.UdtTypeId);
                if (def == null) continue;

                result.Add(new BindableVariableItem
                {
                    DisplayName = variable.Name,
                    BindingId = variable.Name,
                    DataType = $"UDT:{def.Name}",
                    IsUdtInstance = true,
                    Children = def.ExpandInstance(variable)
                        .Select(f => new BindableVariableItem
                        {
                            DisplayName = $"  {f.FieldName}",
                            BindingId = f.CompoundId,
                            DataType = f.DataType,
                            IsUdtField = true
                        })
                        .ToList()
                });
            }
            else
            {
                result.Add(new BindableVariableItem
                {
                    DisplayName = variable.Name,
                    BindingId = variable.Id,
                    DataType = variable.DataType
                });
            }
        }

        return result;
    }

    // ── 数据一致性校验 ──

    /// <summary>校验结果。</summary>
    public class IntegrityIssue
    {
        public string VariableName { get; set; } = "";
        public string VariableId { get; set; } = "";
        public string IssueType { get; set; } = "";       // "OrphanedConnection" | "OrphanedGroup"
        public string StaleRef { get; set; } = "";
        public string? SuggestedFix { get; set; }
    }

    /// <summary>
    /// 校验变量表的引用完整性 —— 检测悬空的 ConnectionId / GroupId / SlaveId 引用。
    /// 返回发现的问题列表；调用方可根据结果自动修复或提示用户。
    /// </summary>
    public List<IntegrityIssue> ValidateIntegrity()
    {
        var issues = new List<IntegrityIssue>();
        var knownConnectionIds = Connections.Select(c => c.Id).ToHashSet();
        var knownGroupIds = Groups.Select(g => g.Id).ToHashSet();
        // 允许空 GroupId（归入虚拟"未分组"）；但非空且不在列表中的视为孤儿
        knownGroupIds.Add("");
        // 允许空 SlaveId（直接挂连接）；非空则必须属于变量自身的连接
        var knownSlaveIds = Connections
            .SelectMany(c => c.Slaves)
            .Select(s => s.Id)
            .ToHashSet();

        foreach (var v in Variables)
        {
            if (!string.IsNullOrEmpty(v.ConnectionId) && !knownConnectionIds.Contains(v.ConnectionId))
                issues.Add(new IntegrityIssue
                {
                    VariableName = v.Name,
                    VariableId = v.Id,
                    IssueType = "OrphanedConnection",
                    StaleRef = v.ConnectionId,
                    SuggestedFix = "清空 ConnectionId（变量将不再关联通讯连接）"
                });

            if (!string.IsNullOrEmpty(v.GroupId) && !knownGroupIds.Contains(v.GroupId))
                issues.Add(new IntegrityIssue
                {
                    VariableName = v.Name,
                    VariableId = v.Id,
                    IssueType = "OrphanedGroup",
                    StaleRef = v.GroupId,
                    SuggestedFix = "将变量移入「未分组」"
                });

            if (!string.IsNullOrEmpty(v.SlaveId))
            {
                if (!knownSlaveIds.Contains(v.SlaveId))
                {
                    issues.Add(new IntegrityIssue
                    {
                        VariableName = v.Name,
                        VariableId = v.Id,
                        IssueType = "OrphanedSlave",
                        StaleRef = v.SlaveId,
                        SuggestedFix = "清空 SlaveId（变量直接挂到连接下）"
                    });
                }
                else
                {
                    // SlaveId 存在但属于别的连接 → 视作跨连接悬空
                    var slaveOwner = Connections.FirstOrDefault(c =>
                        c.Slaves.Any(s => s.Id == v.SlaveId));
                    if (slaveOwner != null && slaveOwner.Id != v.ConnectionId)
                    {
                        issues.Add(new IntegrityIssue
                        {
                            VariableName = v.Name,
                            VariableId = v.Id,
                            IssueType = "SlaveConnectionMismatch",
                            StaleRef = $"{v.SlaveId}@{slaveOwner.Id}",
                            SuggestedFix = "清空 SlaveId 或把 ConnectionId 改为子设备所属连接"
                        });
                    }
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// 自动修复所有引用完整性问题：清空悬空的 ConnectionId/GroupId/SlaveId。
    /// 返回实际修复数量。
    /// </summary>
    public int AutoFixIntegrityIssues()
    {
        var knownConnectionIds = Connections.Select(c => c.Id).ToHashSet();
        var knownGroupIds = Groups.Select(g => g.Id).ToHashSet();
        knownGroupIds.Add(""); // 允许空分组（虚拟"未分组"）
        var knownSlaveIds = Connections.SelectMany(c => c.Slaves).Select(s => s.Id).ToHashSet();

        int fixedCount = 0;
        foreach (var v in Variables)
        {
            if (!string.IsNullOrEmpty(v.ConnectionId) && !knownConnectionIds.Contains(v.ConnectionId))
            {
                v.ConnectionId = "";
                fixedCount++;
            }
            if (!string.IsNullOrEmpty(v.GroupId) && !knownGroupIds.Contains(v.GroupId))
            {
                v.GroupId = "";
                fixedCount++;
            }
            if (!string.IsNullOrEmpty(v.SlaveId) && !knownSlaveIds.Contains(v.SlaveId))
            {
                v.SlaveId = "";
                fixedCount++;
            }
        }
        return fixedCount;
    }

    /// <summary>
    /// 校验报警分组的引用完整性 —— 检测悬空的 AlarmConfig.GroupId 引用。
    /// </summary>
    public static List<(string AlarmName, string GroupId)> ValidateAlarmGroupRefs(HmiProject project)
    {
        var result = new List<(string, string)>();
        var validGroupIds = project.AlarmGroups.Select(g => g.Id).ToHashSet();

        foreach (var group in project.AlarmGroups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!string.IsNullOrEmpty(alarm.GroupId) && !validGroupIds.Contains(alarm.GroupId))
                    result.Add((alarm.Name, alarm.GroupId));
            }
        }

        return result;
    }

    /// <summary>
    /// 自动修复报警分组的孤儿引用 —— 清空悬空的 AlarmConfig.GroupId。
    /// 返回实际修复数量。
    /// </summary>
    public static int AutoFixAlarmGroupRefs(HmiProject project)
    {
        var validGroupIds = project.AlarmGroups.Select(g => g.Id).ToHashSet();
        int fixedCount = 0;

        foreach (var group in project.AlarmGroups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!string.IsNullOrEmpty(alarm.GroupId) && !validGroupIds.Contains(alarm.GroupId))
                {
                    alarm.GroupId = "";
                    fixedCount++;
                }
            }
        }

        return fixedCount;
    }
}

/// <summary>
/// 可绑定变量项 —— 用于变量浏览器展示，支持 UDT 实例层级结构。
/// </summary>
public class BindableVariableItem
{
    public string DisplayName { get; set; } = "";
    public string BindingId { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsUdtInstance { get; set; }
    public bool IsUdtField { get; set; }
    public List<BindableVariableItem>? Children { get; set; }
}
