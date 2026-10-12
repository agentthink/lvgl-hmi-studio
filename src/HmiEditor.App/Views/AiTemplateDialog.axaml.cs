using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// AI 生成模板对话框 —— Faceplate 库入口。
/// 用户描述组件需求 → 调用 AI 生成 ControlTemplate → 校验通过后返回给调用方。
/// </summary>
public partial class AiTemplateDialog : AiGenerateDialogBase<ControlTemplate>
{
    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AiTemplateDialog() : base()
    {
        InitializeComponent();
    }

    public AiTemplateDialog(AiPreviewManager manager) : base(manager)
    {
        InitializeComponent();
        GenerateBtn.Click += OnGenerateClicked;
        CancelBtn.Click += OnCancelClicked;
    }

    protected override TextBox AiInput => InputBox;
    protected override TextBlock AiStatus => StatusText;
    protected override Button AiGenerate => GenerateBtn;
    protected override Button AiCancel => CancelBtn;

    protected override Task<ControlTemplate?> GenerateAsync(string input, HmiProject project, CancellationToken ct)
        => Manager.GenerateTemplateAsync(input, project, ct);
}
