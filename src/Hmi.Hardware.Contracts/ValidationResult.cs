namespace Hmi.Hardware.Contracts;

public enum ValidationSeverity { Error, Warning }

/// <summary>
/// 一条校验结果。
///
/// <para><b><see cref="Args"/> 是干什么的</b>：<see cref="Message"/> 是插件侧自带的**回落文案**
/// （开发/边缘消费者可见），而编辑器界面显示的是按 <c>hw.issue.&lt;code 小写&gt;</c>
/// 查到的**本地化模板**。模板里的 <c>{0}</c>/<c>{1}</c> 就由 <see cref="Args"/> 填 ——
/// 没有它，界面只能显示一句不含具体画面名/尺寸的笼统提示（信息量下降）。
/// 为 <c>null</c> 时模板不含占位符，编辑器直接取模板文本。</para>
///
/// <para>可省略参数，既有构造点（只传三段）不受影响。</para>
/// </summary>
public record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    IReadOnlyList<object>? Args = null);

public record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
