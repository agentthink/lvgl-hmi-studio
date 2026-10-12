namespace Hmi.Communication.Contracts;

public enum ValidationSeverity { Error, Warning }

/// <summary>
/// 一条校验结果。
///
/// <para><b><see cref="Args"/> 是做什么的</b>（阶段 104 补，登记 #61x）：<see cref="Message"/>
/// 是驱动侧自带的**回落文案**（插件程序集拿不到 App 的字典），而编辑器界面显示的是按
/// <c>comm.issue.&lt;code 小写&gt;</c> 查到的**本地化模板**（推导在
/// <c>HmiEditor.App.Services.PluginTextKeys.CommIssueKey</c>）。模板里的 <c>{0}</c>/<c>{1}</c>
/// 由 <see cref="Args"/> 填 —— 没有它，界面只能给一句不含具体波特率的笼统提示。</para>
///
/// <para><b>原先为什么没有它</b>：这一族此前的唯一消费点
/// （<c>CommunicationConfigView.OnTestConnection</c>）把 <see cref="Message"/> 直接拼进
/// 「配置无效: …」—— **不读 <see cref="Code"/>、也没有实参**，所以"缺参数 / 缺翻译"这两件事
/// 一直不可见（症状：界面切英文后那一半照旧中文）。补上之后与兄弟契约
/// <c>Hmi.Hardware.Contracts.ValidationIssue</c> **同形**。</para>
///
/// <para>可省略参数，既有构造点（只传三段）不受影响。</para>
/// </summary>
public record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    IReadOnlyList<object>? Args = null);

public record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
