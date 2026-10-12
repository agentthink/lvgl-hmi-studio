using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Simulator;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **窗口生产构造路径**的共享依赖夹具（登记表 §4 #39 缺的那个东西）。
///
/// <para><b>它解决什么问题：</b>29 个窗口里 27 个同时有两条构造路径 ——
///
/// <list type="bullet">
///   <item><b>设计器路径</b>（无参构造）：字段写 <c>_x = null!</c>，只跑
///     <c>InitializeComponent()</c>。它的唯一用途是让 XAML 设计器能预览。</item>
///   <item><b>生产路径</b>（带依赖构造）：<c>: this()</c> 之后把依赖装进字段、
///     <b>订事件</b>、<b>装数据</b>。编辑器点菜单走的是这条。</item>
/// </list>
///
/// 此前的冒烟面（<c>WindowViewSmokeUiTests</c>）按「有无参构造」取面，于是**统一走了设计器路径**
/// —— 它能证明「窗口打得开」，**不能**证明「生产路径装得上依赖、订得上事件」。
/// 而生产路径恰恰是用户会走的那条。</para>
///
/// <para><b>为什么要有这个类而不是每个用例自己造：</b>生产路径一共要
/// <see cref="SettingsService"/> / <see cref="AppThemeService"/> / <see cref="AuthenticationService"/> /
/// <see cref="AlarmEngine"/> / <see cref="VariableManager"/> / <see cref="VariableSimulator"/> /
/// <see cref="CommunicationSimulator"/> / <see cref="AiPreviewManager"/> / <see cref="RecipeManager"/> /
/// <see cref="RuntimeBindingEngine"/> 十来个对象，外加一个装了屏幕 / 变量 / 报警 / 趋势 / 脚本 / 动画的
/// <see cref="HmiProject"/>。每个用例各抄一份，等于把「哪些依赖是必需的」这条知识抄 27 遍 ——
/// 而它一旦漂移，症状是「某个窗口的生产路径悄悄只被半装配地测着」。</para>
///
/// <para><b>依赖都是廉价的</b>（实测：十个服务里六个有无参构造，其余只要一个路径或一个工程），
/// 所以这个夹具<b>不需要</b>启动主窗口、也不需要联网。凡是需要落盘的（
/// <see cref="SettingsService"/> / <see cref="AppThemeService"/>）都把路径指到调用方给的
/// 隔离数据根里 —— 调用方持有 <see cref="DataRootSandbox"/>。</para>
///
/// <para><b>数据是刻意「非空且可分辨」的</b>：光是 <c>new HmiProject()</c> 会让「生产路径真的装了数据」
/// 与「生产路径只是没崩」混在一起。所以这里往每个集合里塞 2 条具名条目，
/// 逐窗口的行为判据才有东西可断言。</para>
/// </summary>
internal sealed class WindowDepot
{
    public WindowDepot(string dataRoot)
    {
        Directory.CreateDirectory(dataRoot);
        DataRoot = dataRoot;

        Settings = new SettingsService(Path.Combine(dataRoot, "settings.json"));
        Theme = new AppThemeService(Path.Combine(dataRoot, "theme.json"));
        Auth = new AuthenticationService();
        Alarms = new AlarmEngine();
        Simulator = new VariableSimulator();
        Comms = new CommunicationSimulator();
        Ai = new AiPreviewManager();
        Recipes = new RecipeManager();
        Variables = new VariableManager();

        Project = new HmiProject { Name = "WindowDepot" };

        Screen = ScreenFactory.Create(Project, "主画面");
        Screen.Controls.Add(new HmiControl { Name = "状态灯", Type = HmiControlType.Label, X = 10, Y = 10 });
        Project.Screens.Add(Screen);

        var second = ScreenFactory.Create(Project, "报警画面");
        Project.Screens.Add(second);

        Variables.Variables.Add(new Variable { Name = "液位", Address = "D100", DataType = "Int16" });
        Variables.Variables.Add(new Variable { Name = "温度", Address = "D102", DataType = "Float32" });

        var group = new AlarmGroup { Name = "罐区报警" };
        group.Alarms.Add(new AlarmConfig { Name = "液位高", GroupId = group.Id });
        AlarmGroups.Add(group);

        var trend = new TrendConfig { Name = "液位趋势", ControlId = Screen.Controls[0].Id };
        trend.Pens.Add(new TrendPen { Label = "液位", VariableId = Variables.Variables[0].Id });
        trend.Pens.Add(new TrendPen { Label = "温度", VariableId = Variables.Variables[1].Id });
        Trends.Add(trend);

        Scripts.Add(new HmiScript { Name = "开机脚本" });
        Bindings.Add(new ScriptBinding());

        Animations.Add(new AnimationConfig { Name = "呼吸灯", ControlId = Screen.Controls[0].Id });

        BindingEngine = new RuntimeBindingEngine(Project);
        Assets = new AssetLibrary();
    }

    public SettingsService Settings { get; }

    /// <summary>隔离数据根（由调用方的 <see cref="DataRootSandbox"/> 提供）。构造路径需要目录的窗口用它。</summary>
    public string DataRoot { get; }

    public AppThemeService Theme { get; }

    public AuthenticationService Auth { get; }

    public AlarmEngine Alarms { get; }

    public ObservableCollection<AlarmGroup> AlarmGroups { get; } = new();

    public VariableManager Variables { get; }

    public VariableSimulator Simulator { get; }

    public CommunicationSimulator Comms { get; }

    public AiPreviewManager Ai { get; }

    public RecipeManager Recipes { get; }

    public RuntimeBindingEngine BindingEngine { get; }

    public AssetLibrary Assets { get; }

    public HmiProject Project { get; }

    public HmiScreen Screen { get; }

    public ObservableCollection<TrendConfig> Trends { get; } = new();

    public ObservableCollection<HmiScript> Scripts { get; } = new();

    public ObservableCollection<ScriptBinding> Bindings { get; } = new();

    public ObservableCollection<AnimationConfig> Animations { get; } = new();

    /// <summary>夹具自检用：所有"必须非空"的依赖。判据类先断言一遍，避免夹具自己退化。</summary>
    public IReadOnlyList<(string Name, object? Value)> Dependencies => new List<(string, object?)>
    {
        (nameof(Settings), Settings),
        (nameof(Theme), Theme),
        (nameof(Auth), Auth),
        (nameof(Alarms), Alarms),
        (nameof(Variables), Variables),
        (nameof(Simulator), Simulator),
        (nameof(Comms), Comms),
        (nameof(Ai), Ai),
        (nameof(Recipes), Recipes),
        (nameof(BindingEngine), BindingEngine),
        (nameof(Project), Project),
        (nameof(Screen), Screen),
    };
}
