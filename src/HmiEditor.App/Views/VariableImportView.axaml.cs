using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;

namespace HmiEditor.App.Views;

public partial class VariableImportView : ChromeWindow
{
    private readonly VariableManager _variableManager;
    private ImportResult? _lastImportResult;
    private string? _selectedFilePath;

    /// <summary>
    /// **平台文件选择器的注入缝**（阶段 254）：非 null 时"选择文件"这一步走它、不碰
    /// <see cref="TopLevel.StorageProvider"/>。
    ///
    /// <para><b>为什么要有这条缝</b>：`_selectedFilePath` **只能**由平台选择器写入。headless 下选择器桩返回空集合
    /// ⇒ 这一格永远是空 ⇒ <c>BtnParse</c> 首行 `if (string.IsNullOrEmpty(_selectedFilePath)) return;` 直接早退、
    /// <c>BtnImport</c> 也没有可导的行 ⇒ **解析 / 导入两条最强的腿原理上够不到**（与 `AiIconDialog` 阶段 209
    /// 加 `IIconGenerationSource` 是同一个处境）。</para>
    ///
    /// <para>⚠ `internal`：不必多出一条**公共**构造 ⇒ 不惊动 <c>WindowProductionPathUiTests</c> 的
    /// 「双路径」元检查（那条只数公共构造，与阶段 209 同处置）。</para>
    /// </summary>
    private readonly System.Func<System.Threading.Tasks.Task<string?>>? _pickFile;

    public VariableImportView()
    {
        _variableManager = null!;
        InitializeComponent();
    }

    public VariableImportView(VariableManager variableManager) : this()
    {
        _variableManager = variableManager;
        WireEvents();
    }

    /// <summary>注入缝构造（阶段 254）：<paramref name="pickFile"/> 传 null ⇒ 与加缝之前逐字相同。</summary>
    internal VariableImportView(VariableManager variableManager,
                                System.Func<System.Threading.Tasks.Task<string?>>? pickFile) : this(variableManager)
    {
        _pickFile = pickFile;
    }

    private void WireEvents()
    {
        BtnCancel.Click += (_, _) => Close();
        BtnSelectFile.Click += async (_, _) => await SelectFile();
        BtnParse.Click += async (_, _) => await ParseFile();
        BtnImport.Click += (_, _) => ImportVariables();
    }

    private async System.Threading.Tasks.Task SelectFile()
    {
        if (_pickFile is not null)
        {
            var chosen = await _pickFile();
            if (!string.IsNullOrEmpty(chosen)) ApplySelectedFile(chosen);
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.VarImportSelectCsv],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.VarImportFiletypeCsv])
                {
                    Patterns = new[] { "*.csv", "*.txt" }
                },
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeAll])
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count == 1)
            ApplySelectedFile(files[0].Path.LocalPath);
    }

    /// <summary>把"用户选中的文件"落到界面状态（两条路径共用：平台选择器 / 注入缝）。</summary>
    private void ApplySelectedFile(string path)
    {
        _selectedFilePath = path;
        FilePathText.Text = System.IO.Path.GetFileName(_selectedFilePath);
        BtnParse.IsEnabled = true;
        StatusText.Text = AppLocalization.Service.Format(Lang.VarImportSelected, _selectedFilePath);
    }

    private async System.Threading.Tasks.Task ParseFile()
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        try
        {
            StatusText.Text = AppLocalization.Service[Lang.VarImportParsing];
            var csvText = await System.IO.File.ReadAllTextAsync(_selectedFilePath);
            _lastImportResult = CsvVariableImporter.Parse(csvText);

            // 刷新预览
            PreviewList.Items.Clear();
            foreach (var variable in _lastImportResult.Variables)
            {
                var panel = new Avalonia.Controls.Grid
                {
                    ColumnDefinitions = new Avalonia.Controls.ColumnDefinitions("*,100,120,80,60")
                };

                AddTextToGrid(panel, variable.Name, 0);
                AddTextToGrid(panel, variable.DataType, 1);
                AddTextToGrid(panel, variable.Address, 2);
                AddTextToGrid(panel, variable.Unit ?? "", 3);
                AddTextToGrid(panel, variable.Scale.ToString("F1"), 4);

                PreviewList.Items.Add(panel);
            }

            // 状态更新
            var status = AppLocalization.Service.Format("var.import.done", _lastImportResult.SuccessRows, _lastImportResult.TotalRows);
            if (_lastImportResult.HasErrors)
                status += AppLocalization.Service.Format(Lang.VarImportWarnings, _lastImportResult.Errors.Count);
            StatusText.Text = status;

            // 「哪几行坏了」以前到不了界面（登记 #61f，阶段 88）：状态行只放得下条数，
            // 逐条原因挂在提示上 —— 与 ScriptEditorView 处理脚本校验信息的做法同形。
            // 走 ExternalText.Describe（码 → 字典模板 → 填实参），不直接读 Errors 的回落原文；
            // 行号前缀也由它加（CsvImportIssue.Line 是结构化字段，不进 Message）。
            ToolTip.SetTip(StatusText, _lastImportResult.HasErrors
                ? string.Join("\n", _lastImportResult.Errors.Select(ExternalText.Describe))
                : null);

            BtnImport.IsEnabled = _lastImportResult.Variables.Count > 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpImportVariables, _selectedFilePath, ex);
            BtnImport.IsEnabled = false;
        }
    }

    private void ImportVariables()
    {
        if (_lastImportResult == null || _variableManager == null) return;

        var existingNames = new HashSet<string>(_variableManager.Variables.Select(v => v.Name),
            StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;

        foreach (var variable in _lastImportResult.Variables)
        {
            if (existingNames.Contains(variable.Name))
            {
                skipped++;
                continue;
            }
            _variableManager.Variables.Add(variable);
            existingNames.Add(variable.Name);
            imported++;
        }

        StatusText.Text = AppLocalization.Service.Format(Lang.VarImportCompleted, imported, skipped);
        BtnImport.IsEnabled = false;
    }

    private static void AddTextToGrid(Avalonia.Controls.Grid grid, string text, int column)
    {
        var tb = new Avalonia.Controls.TextBlock
        {
            Text = text,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
        };
        Avalonia.Controls.Grid.SetColumn(tb, column);
        grid.Children.Add(tb);
    }
}
