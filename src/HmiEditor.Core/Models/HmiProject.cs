using System.Collections.ObjectModel;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.Trend;

namespace HmiEditor.Core.Models;

public class HmiProject
{
    // ── 基本元数据 ──

    public string Name { get; set; } = "Untitled";
    public string LvglVersion { get; set; } = "9.5";
    public string? HardwareTargetId { get; set; }

    /// <summary>项目描述（用途、设备型号、备注等）。</summary>
    public string? Description { get; set; }

    /// <summary>项目作者/工程师姓名。</summary>
    public string? Author { get; set; }

    /// <summary>所属公司/组织。</summary>
    public string? Company { get; set; }

    /// <summary>项目语义化版本号（区别于 LvglVersion）。</summary>
    public string ProjectVersion { get; set; } = "1.0.0";

    /// <summary>项目创建时间（UTC+时区）。新建项目时自动生成。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>最后修改时间。每次保存时由 ProjectSerializer 自动更新。</summary>
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.Now;

    // ── 核心数据集合 ──

    public ObservableCollection<HmiScreen> Screens { get; set; } = new();

    /// <summary>变量管理（通讯连接 + 变量表 + 绑定关系）</summary>
    public VariableManager VariableManager { get; set; } = new();

    /// <summary>全局主题/样式配置</summary>
    public HmiTheme Theme { get; set; } = new();

    /// <summary>报警组配置列表（Phase 3 工业运行时）</summary>
    public ObservableCollection<AlarmGroup> AlarmGroups { get; set; } = new();

    /// <summary>趋势图配置列表（Phase 3 工业运行时）</summary>
    public ObservableCollection<TrendConfig> TrendConfigs { get; set; } = new();

    /// <summary>数据记录器配置列表（Phase 3 工业运行时；环形缓冲 + CSV 导出）</summary>
    public ObservableCollection<DataLogConfig> DataLogConfigs { get; set; } = new();

    /// <summary>Lua 脚本集合（Phase 3 脚本引擎）</summary>
    public ObservableCollection<HmiScript> Scripts { get; set; } = new();

    /// <summary>脚本与控件的绑定关系集合</summary>
    public ObservableCollection<ScriptBinding> ScriptBindings { get; set; } = new();

    /// <summary>动画配置列表（Phase 2 动画编辑器）</summary>
    public ObservableCollection<AnimationConfig> AnimationConfigs { get; set; } = new();

    /// <summary>Faceplate 模板集合（Phase R2 参数化复合组件系统）</summary>
    public ObservableCollection<ControlTemplate> Templates { get; set; } = new();

    // ── 工程设置子模型 ──

    /// <summary>嵌入式运行时行为配置（扫描周期、看门狗、日志级别等）。</summary>
    public RuntimeConfig RuntimeConfig { get; set; } = new();

    /// <summary>代码导出配置（目标语言、输出路径、构建系统等）。</summary>
    public ExportConfig ExportConfig { get; set; } = new();

    /// <summary>运行时安全策略配置（操作模式、会话、密码、审计等）。</summary>
    public SecurityConfig SecurityConfig { get; set; } = new();

    /// <summary>编辑器行为偏好（网格、撤销栈、语言、字体等）。</summary>
    public EditorConfig EditorConfig { get; set; } = new();

    /// <summary>屏幕默认模板配置（新建屏幕的默认分辨率、方向、色深等）。</summary>
    public ScreenDefaultsConfig ScreenDefaultsConfig { get; set; } = new();
}
