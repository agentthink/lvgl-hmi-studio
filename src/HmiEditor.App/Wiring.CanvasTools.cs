using System.Linq;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Diagnostics;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using HmiEditor.Device.Build;
using HmiEditor.App.Resources;

namespace HmiEditor.App;

/// <summary>
/// 画布工具栏/绘图形状按钮的事件装配。
/// 自 MainWindow.axaml.cs 按域拆出（2026-09-13）：逐行未改、行为不变。
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// 切换格式工具组的可见性（仅在画布选中控件时显示）。
    /// 根据选中数量动态控制：
    ///   ≥1 个选中 → 显示编辑组（复制/粘贴/多重复制/删除）
    ///   ≥2 个选中 → 额外显示对齐组 + 统一尺寸 + 组合
    ///   ≥3 个选中 → 额外显示等距排列
    ///   选中含组合 → 额外显示拆分
    /// </summary>
    private void SetAlignmentGroupVisible(bool visible)
    {
        var hasSelection = visible;
        var multiSelected = CanvasPanel?.HasMultiSelection ?? false;
        var hasGroup = CanvasPanel?.SelectionContainsGroup ?? false;

        // 编辑组：选中任意控件即显示
        BtnCopy.IsVisible = hasSelection;
        BtnPaste.IsVisible = hasSelection; // 粘贴始终依赖剪贴板，但仅在编辑上下文显示
        BtnDuplicate.IsVisible = hasSelection;

        // 对齐组 + 统一尺寸：≥2 个选中
        var multiVisible = hasSelection && multiSelected;
        BtnAlignLeft.IsVisible = multiVisible;
        BtnAlignRight.IsVisible = multiVisible;
        BtnAlignTop.IsVisible = multiVisible;
        BtnAlignBottom.IsVisible = multiVisible;
        BtnAlignCenterH.IsVisible = multiVisible;
        BtnAlignCenterV.IsVisible = multiVisible;
        BtnSameSize.IsVisible = multiVisible;
        BtnGroup.IsVisible = multiVisible;

        // 等距排列：≥3 个选中（2 个控件间距唯一无需排列）
        var distributeVisible = multiVisible && CanvasPanel?.GetSelectedControls().Count >= 3;
        BtnDistributeH.IsVisible = distributeVisible;
        BtnDistributeV.IsVisible = distributeVisible;

        // 拆分：选中集中包含组合成员
        BtnUngroup.IsVisible = hasSelection && hasGroup;
    }

    private void WireAlignmentButtons()
    {
        void Align(DesignCanvas.MultiAlignDirection direction)
        {
            if (ApplySelectionEdit(AppLocalization.Service.Format(Lang.UndoAlignControls, direction), () => CanvasPanel.Canvas.AlignSelectedControls(direction)))
            {
                PropertyPanel.BindControl(CanvasPanel.Canvas.GetSelectedControls().FirstOrDefault());
                SetUnsavedChanges(true);
                UpdateStatusBar();
            }
        }

        BtnAlignLeft.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.Left);
        BtnAlignRight.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.Right);
        BtnAlignTop.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.Top);
        BtnAlignBottom.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.Bottom);
        BtnAlignCenterH.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.CenterHorizontal);
        BtnAlignCenterV.Click += (_, _) => Align(DesignCanvas.MultiAlignDirection.CenterVertical);

        // ── 多选操作按钮 ──
        BtnSameSize.Click += (_, _) =>
        {
            if (ApplySelectionEdit(AppLocalization.Service[Lang.CmdDescSameSize], () => CanvasPanel.MakeSameSize()))
            {
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
            }
        };
        BtnDistributeH.Click += (_, _) =>
        {
            if (ApplySelectionEdit(AppLocalization.Service[Lang.CmdDescDistributeH], () => CanvasPanel.DistributeHorizontal()))
            {
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
            }
        };
        BtnDistributeV.Click += (_, _) =>
        {
            if (ApplySelectionEdit(AppLocalization.Service[Lang.CmdDescDistributeV], () => CanvasPanel.DistributeVertical()))
            {
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
            }
        };
        BtnGroup.Click += (_, _) =>
        {
            ApplySelectionEdit(AppLocalization.Service[Lang.CmdDescGroup], () => CanvasPanel.GroupSelectedControls() != null);
            SetUnsavedChanges(true);
            RefreshPanelsAfterEdit();
        };
        BtnUngroup.Click += (_, _) =>
        {
            ApplySelectionEdit(AppLocalization.Service[Lang.CmdDescUngroup], () => CanvasPanel.UngroupSelectedControls() > 0);
            SetUnsavedChanges(true);
            RefreshPanelsAfterEdit();
        };

        // ── 编辑组按钮 ──
        BtnCopy.Click += (_, _) => CanvasPanel.CopySelectedControls();
        BtnPaste.Click += (_, _) =>
        {
            CanvasPanel.PasteControls();
            SetUnsavedChanges(true);
            RefreshPanelsAfterEdit();
        };
        BtnDuplicate.Click += (_, _) => DuplicateCurrentControl();
    }

    /// <summary>
    /// 绑定绘图工具栏：图形工具按钮（互斥单选 → 画布绘图模式）+ 控件快捷按钮（单击即添加）。
    /// 工具栏状态与 DockLayoutState.ActiveDrawTool 双向同步（Dock 重建后画布恢复工具状态）。
    /// </summary>
    private void WireDrawToolbar()
    {
        // 图形工具：选择 / 矩形 / 椭圆 / 折线 / 圆弧 / 直线 / 文本
        BtnToolSelect.Click += (_, _) => SetDrawTool(null);
        BtnToolRectangle.Click += (_, _) => SetDrawTool(HmiControlType.Rectangle);
        BtnToolEllipse.Click += (_, _) => SetDrawTool(HmiControlType.Ellipse);
        BtnToolPolyline.Click += (_, _) => SetDrawTool(HmiControlType.Polyline);
        BtnToolArc.Click += (_, _) => SetDrawTool(HmiControlType.Arc);
        BtnToolLine.Click += (_, _) => SetDrawTool(HmiControlType.Line);
        BtnToolText.Click += (_, _) => SetDrawTool(HmiControlType.Label);

        // 控件快捷按钮：单击在画布中心添加控件并回到选择模式
        BtnQuickButton.Click += (_, _) => QuickAddControl(HmiControlType.Button);
        BtnQuickLabel.Click += (_, _) => QuickAddControl(HmiControlType.Label);
        BtnQuickSlider.Click += (_, _) => QuickAddControl(HmiControlType.Slider);
        BtnQuickSwitch.Click += (_, _) => QuickAddControl(HmiControlType.Switch);
        BtnQuickCheckBox.Click += (_, _) => QuickAddControl(HmiControlType.CheckBox);
        BtnQuickLed.Click += (_, _) => QuickAddControl(HmiControlType.Led);
        BtnQuickGauge.Click += (_, _) => QuickAddControl(HmiControlType.Gauge);
        BtnQuickBar.Click += (_, _) => QuickAddControl(HmiControlType.Bar);
        BtnQuickDropdown.Click += (_, _) => QuickAddControl(HmiControlType.Dropdown);
        BtnQuickTable.Click += (_, _) => QuickAddControl(HmiControlType.Table);
        BtnQuickImage.Click += (_, _) => QuickAddControl(HmiControlType.Image);
        BtnQuickKnob.Click += (_, _) => QuickAddControl(HmiControlType.Knob);
        BtnQuickTextArea.Click += (_, _) => QuickAddControl(HmiControlType.TextArea);
        BtnQuickKeyboard.Click += (_, _) => QuickAddControl(HmiControlType.Keyboard);

        // 工具状态变化 → 同步按钮高亮 + 提示文本
        DockLayoutState.DrawToolChanged += (_, tool) => SyncDrawToolbar(tool);

        // 初始状态（默认选择工具）
        SyncDrawToolbar(DockLayoutState.ActiveDrawTool);
    }
}
