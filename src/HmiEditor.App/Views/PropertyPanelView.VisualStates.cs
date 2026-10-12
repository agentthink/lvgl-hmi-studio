using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Views.Editors;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class PropertyPanelView : UserControl
{
    /// <summary>
    /// 构建水平/垂直对齐下拉。Normal 态编辑的是控件默认对齐（无"继承"项）；
    /// 其它态编辑的是覆盖值（有"继承"项，Tag=null）。
    /// </summary>
    private void PopulateAlignCombos(bool isNormal)
    {
        VsTextAlign.Items.Clear();
        VsTextVAlign.Items.Clear();

        if (!isNormal)
        {
            VsTextAlign.Items.Add(new ComboBoxItem
            {
                Content = AppLocalization.Service[Lang.PropVisualStateInherit],
                Tag = (string?)null
            });
            VsTextVAlign.Items.Add(new ComboBoxItem
            {
                Content = AppLocalization.Service[Lang.PropVisualStateInherit],
                Tag = (string?)null
            });
        }

        VsTextAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateAlignLeft],
            Tag = TextAlignMode.Left.ToString()
        });
        VsTextAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateAlignCenter],
            Tag = TextAlignMode.Center.ToString()
        });
        VsTextAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateAlignRight],
            Tag = TextAlignMode.Right.ToString()
        });

        VsTextVAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateVAlignTop],
            Tag = TextVAlignMode.Top.ToString()
        });
        VsTextVAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateVAlignMid],
            Tag = TextVAlignMode.Middle.ToString()
        });
        VsTextVAlign.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateVAlignBot],
            Tag = TextVAlignMode.Bottom.ToString()
        });
    }

    /// <summary>填充字体下拉：来自全局 AssetLibrary 中 Type==Font 的条目。</summary>
    private void PopulateFontFamilyCombo(string? selectedFontFamily)
    {
        PropFontFamily.Items.Clear();
        // 默认项（null = 使用主题/项目默认字体）
        PropFontFamily.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.FontDefaultPlaceholder],
            Tag = (string?)null
        });

        int selIdx = 0;
        var library = DockLayoutState.AssetLibrary;
        if (library != null)
        {
            int i = 1;
            foreach (var asset in library.Assets.Where(a => a.Type == AssetType.Font))
            {
                PropFontFamily.Items.Add(new ComboBoxItem
                {
                    Content = string.IsNullOrEmpty(asset.Name)
                        ? Path.GetFileName(asset.FilePath)
                        : asset.Name,
                    Tag = asset.Id
                });
                if (!string.IsNullOrEmpty(selectedFontFamily) && asset.Id == selectedFontFamily)
                    selIdx = i;
                i++;
            }
        }
        PropFontFamily.SelectedIndex = selIdx;
    }

    /// <summary>填充状态图片下拉：来自全局 AssetLibrary 中 Type==Image 的条目。</summary>
    private void PopulateVisualStateImageItems()
    {
        CboVsImage.Items.Clear();
        CboVsImage.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropVisualStateNoImage],
            Tag = (string?)null
        });

        var library = DockLayoutState.AssetLibrary;
        if (library != null)
        {
            foreach (var asset in library.Assets.Where(a => a.Type == AssetType.Image))
            {
                CboVsImage.Items.Add(new ComboBoxItem
                {
                    Content = $"{asset.Name} ({asset.Width}x{asset.Height})",
                    Tag = asset.Id
                });
            }
        }
        CboVsImage.SelectedIndex = 0;
    }

    /// <summary>把指定状态的已配置字段回填到输入框（未配置则留空，表示继承默认态）。</summary>
    private void LoadVisualStateFields(HmiControl control, VisualState state)
    {
        var st = control.GetStateStyle(state);

        // 🩸 阶段 201 修 `#91` 的另一半：Normal 态的三色 = **控件自己的三色**
        //   （与下面 text/align/image 在 Normal 取 `control.*` 同形）；其它态取状态条目（空 = 继承）。
        var isNormal = state == VisualState.Normal;
        VsBgColor.Text = (isNormal ? control.BgColor : st?.BgColor) ?? "";
        VsTextColor.Text = (isNormal ? control.TextColor : st?.TextColor) ?? "";
        VsBorderColor.Text = (isNormal ? control.BorderColor : st?.BorderColor) ?? "";

        // 文本：Normal 态编辑控件默认 Text；其它态编辑状态覆盖文本（留空=继承）
        VsText.Text = state == VisualState.Normal
            ? (control.Text ?? "")
            : (st?.Text ?? "");

        // 对齐：Normal 态用控件默认对齐；其它态用覆盖值（null=继承）
        SelectAlignItem(VsTextAlign, state == VisualState.Normal
            ? control.TextAlign.ToString()
            : st?.TextAlign?.ToString());
        SelectAlignItem(VsTextVAlign, state == VisualState.Normal
            ? control.TextVAlign.ToString()
            : st?.TextVAlign?.ToString());

        // 图片选择：Normal 态用 control.ImageAssetId 作为默认图；其它态用状态条目的 ImageAssetId
        var imageAssetId = state == VisualState.Normal
            ? control.ImageAssetId
            : st?.ImageAssetId;
        SelectVisualStateImage(imageAssetId);

        UpdateVisualStateSwatches();
    }

    /// <summary>按 Tag 字符串选中对齐下拉项；null/未匹配则选第一项（继承或默认值）。</summary>
    private void SelectAlignItem(ComboBox combo, string? tagValue)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem cbi)
            {
                var tag = cbi.Tag as string;
                if (string.IsNullOrEmpty(tagValue) && string.IsNullOrEmpty(tag))
                {
                    combo.SelectedIndex = i;
                    return;
                }
                if (!string.IsNullOrEmpty(tagValue) && tag == tagValue)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
        combo.SelectedIndex = 0;
    }

    /// <summary>按 AssetId 选中图片下拉项；未匹配则选“(无图片)”。</summary>
    private void SelectVisualStateImage(string? imageAssetId)
    {
        for (int i = 0; i < CboVsImage.Items.Count; i++)
        {
            if (CboVsImage.Items[i] is ComboBoxItem cbi)
            {
                var tag = cbi.Tag as string;
                if (string.IsNullOrEmpty(imageAssetId) && string.IsNullOrEmpty(tag))
                {
                    CboVsImage.SelectedIndex = i;
                    return;
                }
                if (!string.IsNullOrEmpty(imageAssetId) && tag == imageAssetId)
                {
                    CboVsImage.SelectedIndex = i;
                    return;
                }
            }
        }
        CboVsImage.SelectedIndex = 0;
    }

    /// <summary>刷新状态样式色板的背景色。</summary>
    private void UpdateVisualStateSwatches()
    {
        try
        {
            SwatchVsBgColor.Background = ParseSwatchBrush(VsBgColor.Text, "#2563eb");
            SwatchVsTextColor.Background = ParseSwatchBrush(VsTextColor.Text, "#e2e8f0");
            SwatchVsBorderColor.Background = ParseSwatchBrush(VsBorderColor.Text, "#334155");
        }
        catch { /* 忽略色板解析异常 */ }
    }

    private static IBrush ParseSwatchBrush(string? text, string fallbackHex)
    {
        if (string.IsNullOrEmpty(text)) return new SolidColorBrush(Color.Parse(fallbackHex));
        try
        {
            var hex = text.StartsWith('#') ? text : "#" + text;
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse(fallbackHex));
        }
    }

    /// <summary>状态选择或字段文本变化时触发：保存当前编辑态到模型。</summary>
    private void OnVisualStateChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        // 若是状态切换下拉触发 —— 先保存旧编辑态，再重建对齐下拉并加载新态字段
        if (sender == CboVisualState)
        {
            SaveVisualState(_currentControl, _currentEditingState);
            if (CboVisualState.SelectedItem is ComboBoxItem cbi
                && Enum.TryParse<VisualState>(cbi.Tag?.ToString(), out var newstate))
            {
                _currentEditingState = newstate;
            }
            _isUpdating = true;
            try
            {
                // Normal 态编辑控件默认对齐（无继承项），其它态编辑覆盖值（含继承项）
                PopulateAlignCombos(isNormal: _currentEditingState == VisualState.Normal);
                LoadVisualStateFields(_currentControl, _currentEditingState);
            }
            finally { _isUpdating = false; }
            PropertyChanged?.Invoke(this, _currentControl);
            return;
        }

        // 字段文本变化 —— 保存当前态
        SaveVisualState(_currentControl, _currentEditingState);
        UpdateVisualStateSwatches();
        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>把当前编辑器字段写回指定状态的样式条目；全空则移除该条目。</summary>
    private void SaveVisualState(HmiControl control, VisualState state)
    {
        var bg = string.IsNullOrWhiteSpace(VsBgColor.Text) ? null : VsBgColor.Text.Trim();
        var fg = string.IsNullOrWhiteSpace(VsTextColor.Text) ? null : VsTextColor.Text.Trim();
        var bc = string.IsNullOrWhiteSpace(VsBorderColor.Text) ? null : VsBorderColor.Text.Trim();

        // 文本：Normal 态编辑控件默认 Text；其它态编辑覆盖值（空=继承）
        var text = string.IsNullOrWhiteSpace(VsText.Text) ? null : VsText.Text.Trim();

        // 对齐：从下拉 Tag 解析（null 表示继承；Normal 态下拉无继承项，必为具体值）
        TextAlignMode? hAlign = ParseAlignTag<TextAlignMode>(VsTextAlign.SelectedItem);
        TextVAlignMode? vAlign = ParseAlignTag<TextVAlignMode>(VsTextVAlign.SelectedItem);

        // 图片 AssetId：从下拉选中项读取
        string? imgId = null;
        if (CboVsImage.SelectedItem is ComboBoxItem imgCbi)
            imgId = imgCbi.Tag as string;

        // Normal 态：文本/图片/对齐/三色写入控件默认字段；其它态写入状态条目
        if (state == VisualState.Normal)
        {
            control.Text = text;
            control.TextAlign = hAlign ?? TextAlignMode.Center;
            control.TextVAlign = vAlign ?? TextVAlignMode.Middle;
            control.ImageAssetId = imgId;

            // 🩸 阶段 201 修 `#91`：Normal 态的**三色就是控件自己的三色** ——
            //   提示语写着「其它态留空 = 继承 Normal」，装载侧（`LoadVisualStateFields`）
            //   在 Normal 也把这三格当成"控件当前色"来填；但写回侧原先**只写文本/对齐/图片**，
            //   于是这三个框在 Normal 态**改了什么都不发生**（连状态条目都不建）——
            //   而 `UpdateVisualStateSwatches()` 会把色块刷成新颜色 ⇒ 用户看到"改成功了"的假象。
            //   口径与「内容区」的三个色框一致（同一批字段的不同视角），并像 `PropText` 那样双向同步。
            //   ⚠ 只在**非空**时写：清空这三格不代表"把控件颜色清成 null"（其它态的清空才表示"继承"）。
            if (bg != null) control.BgColor = bg;
            if (fg != null) control.TextColor = fg;
            if (bc != null) control.BorderColor = bc;

            // 同步内容区（同一字段：`PropBgColor` / `PropTextColor` / `PropBorderColor`）
            SyncContentBox(PropBgColor, control.BgColor);
            SyncContentBox(PropTextColor, control.TextColor);
            SyncContentBox(PropBorderColor, control.BorderColor);

            // 同步内容区的文本输入框（同一字段 control.Text）
            if (!_isUpdating && PropText.Text != (control.Text ?? ""))
            {
                _isUpdating = true;
                try { PropText.Text = control.Text ?? ""; }
                finally { _isUpdating = false; }
            }
        }
        else
        {
            var existing = control.GetStateStyle(state);
            if (bg == null && fg == null && bc == null && imgId == null
                && text == null && hAlign == null && vAlign == null)
            {
                // 全空 —— 移除该态条目（避免存空记录）
                if (existing != null) control.VisualStates.Remove(existing);
                return;
            }

            if (existing == null)
            {
                existing = new ControlVisualState { State = state };
                control.VisualStates.Add(existing);
            }
            existing.BgColor = bg;
            existing.TextColor = fg;
            existing.BorderColor = bc;
            existing.ImageAssetId = imgId;
            existing.Text = text;
            existing.TextAlign = hAlign;
            existing.TextVAlign = vAlign;
        }
    }

    /// <summary>
    /// 把**内容区**的色框同步成刚落进模型的值 —— Normal 态的"视觉状态"三色与内容区三框
    /// （`PropBgColor` / `PropTextColor` / `PropBorderColor`）是**同一批字段**的两个视角
    /// （阶段 201 修 `#91` 时补的对称；`control.Text` 早就有同形的同步）。
    /// ⚠ 程序化写 <c>TextBox.Text</c> **不触发** <c>TextChanged</c>（阶段 187 实测）⇒ 不会回环触发保存。
    /// </summary>
    private void SyncContentBox(TextBox box, string? value)
    {
        if (_isUpdating) return;
        var text = value ?? "";
        if (box.Text == text) return;

        _isUpdating = true;
        try { box.Text = text; }
        finally { _isUpdating = false; }
    }

    /// <summary>从对齐下拉选中项解析枚举；选中"继承"或未选中时返回 null。</summary>
    private static TEnum? ParseAlignTag<TEnum>(object? selected) where TEnum : struct, Enum
    {
        if (selected is ComboBoxItem cbi
            && cbi.Tag is string tag
            && Enum.TryParse<TEnum>(tag, out var value))
        {
            return value;
        }
        return null;
    }

    /// <summary>"清除图片"按钮：把图片下拉重置为"(无图片)"并保存。</summary>
    private void OnVsImageClearClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentControl == null) return;
        CboVsImage.SelectedIndex = 0;
        SaveVisualState(_currentControl, _currentEditingState);
        PropertyChanged?.Invoke(this, _currentControl);
    }
}
