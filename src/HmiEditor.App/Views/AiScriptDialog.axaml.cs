using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.App.Views;

/// <summary>
/// AI 生成脚本对话框 —— 脚本编辑器入口。
/// 用户描述脚本功能 → 调用 AI 生成 Lua 脚本 → 校验通过后返回给调用方。
/// </summary>
public partial class AiScriptDialog : AiGenerateDialogBase<HmiScript>
{
    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AiScriptDialog() : base()
    {
        InitializeComponent();
    }

    public AiScriptDialog(AiPreviewManager manager) : base(manager)
    {
        InitializeComponent();
        GenerateBtn.Click += OnGenerateClicked;
        CancelBtn.Click += OnCancelClicked;
    }

    protected override TextBox AiInput => InputBox;
    protected override TextBlock AiStatus => StatusText;
    protected override Button AiGenerate => GenerateBtn;
    protected override Button AiCancel => CancelBtn;

    protected override Task<HmiScript?> GenerateAsync(string input, HmiProject project, CancellationToken ct)
        => Manager.GenerateScriptAsync(input, project, ct);
}
