namespace HmiEditor.Core.Design;

/// <summary>
/// 控件动作类型元数据 —— 动作 ID 常量 ＋ 生成产物用的中文显示名。
///
/// <para><b>⚠ 本类型现在只服务「生成产物」这一个方向</b>（阶段 94，闭环登记 #61m）。
/// 改动前它的类注释写着「属性面板动作列表展示、代码生成动作注释共用，避免中文名散落硬编码。
/// 如需多语言，后续将 DisplayName 改为本地化 key 解析」—— 那句话既是**事实陈述**也是**缺陷自述**：
/// 同一个方法同时给界面与产物供中文，两个方向的语言要求**相反**
/// （产物必须固定中文、界面必须跟语言走）。阶段 94 把界面那侧拆走：
/// 属性面板（下拉与动作 chip）改走 <c>HmiEditor.App.Services.ActionTextKeys</c>。
/// 于是中文名不再是「两处共用」，而是「**只给产物**」—— 与
/// <c>Core.Tests</c> 台账里 GEN 那一栏（消费方是生成器）同类。</para>
/// </summary>
public static class ActionTypeMeta
{
    // ── 动作类型 ID 常量 ──
    public const string ToggleBit = "ToggleBit";
    public const string SetBit = "SetBit";
    public const string ResetBit = "ResetBit";
    public const string MomentaryOn = "MomentaryOn";
    public const string MomentaryOff = "MomentaryOff";
    public const string SetVariable = "SetVariable";
    public const string SwitchScreen = "SwitchScreen";
    public const string ExecuteScript = "ExecuteScript";
    public const string ShowAlarms = "ShowAlarms";
    public const string ShowTrends = "ShowTrends";
    public const string Login = "Login";
    public const string Logout = "Logout";

    /// <summary>
    /// 动作类型显示名（中文；未知类型返回原 ID）—— <b>只给生成产物用</b>。
    ///
    /// <para><b>不要把它接到界面上</b>：这张表的 12 个值有**两份**存在
    /// （这里的中文 ＋ 字典的 <c>action.type.*</c>），因为产物那一份必须冻结、
    /// 界面那一份必须可翻。两份的一致性由 <c>ActionTextLocalizationGuardTests</c>
    /// 用**相等式**钉住（zh 字典值必须逐字等于本表的返回值）——
    /// 这是「同一件事的两条消费路径不是多余一份，而是必然漂移的两份」那条纪律的落地。</para>
    /// </summary>
    public static string DisplayName(string? actionType) => actionType switch
    {
        ToggleBit => "取反",
        SetBit => "置位",
        ResetBit => "复位",
        MomentaryOn => "按1松0",
        MomentaryOff => "按0松1",
        SetVariable => "设置变量",
        SwitchScreen => "切换屏幕",
        ExecuteScript => "执行脚本",
        ShowAlarms => "显示报警",
        ShowTrends => "显示趋势",
        Login => "登录",
        Logout => "登出",
        _ => actionType ?? string.Empty
    };

    /// <summary>是否为写变量类动作（目标为变量 ID，需要解析友好名）。</summary>
    public static bool IsTargetVariable(string? actionType) =>
        actionType is ToggleBit or SetBit or ResetBit or MomentaryOn or MomentaryOff or SetVariable;

    /// <summary>动作目标直接显示为文本（目标本身友好，无需解析变量名）。</summary>
    public static bool IsTargetLiteral(string? actionType) =>
        actionType is SwitchScreen or ExecuteScript;
}
