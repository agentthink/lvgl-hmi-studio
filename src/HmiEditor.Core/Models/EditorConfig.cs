namespace HmiEditor.Core.Models;

/// <summary>
/// 工具箱添加控件的交互模式。
/// <para><b>尚未实现</b>：工具箱目前只有拖放一种添加方式，
/// 这个枚举的三个取值没有任何消费点。</para>
/// </summary>
public enum ComponentPickerMode
{
    /// <summary>拖拽到画布（当前唯一实现的方式）。</summary>
    DragDrop = 0,

    /// <summary>点击工具箱后点击画布位置添加。（未实现）</summary>
    ClickAdd = 1,

    /// <summary>双击工具箱直接在画布中心添加。（未实现）</summary>
    DoubleClick = 2,
}

/// <summary>
/// 编辑器行为偏好 —— **已废弃的只读兼容载体**。
///
/// <para><b>它现在什么都不做。</b>这些字段既没有 UI 入口，也没有任何消费点 ——
/// 决定画布行为的真实来源是启用级设置 <c>SettingsService</c>
/// （命令栏「设置」按钮 → SettingsDialog）。改这里的值，编辑器行为不会有任何变化。</para>
///
/// <para><b>为什么还留着类型和属性：</b>已有 <c>project.hmi</c> 里带有这一节，
/// 保留形状可以让老工程**无损往返**（读进来再存回去，值原样保留），
/// 而不是静默丢掉用户文件里的一段。新工程不再写入有意义的值。</para>
///
/// <para><b>不要再往上加字段，也不要把任何字段接回消费点。</b>
/// 编辑器偏好是**应用级**的：它跟人不跟工程 —— 同一个工程被两个用户打开时，
/// 网格大小不该跟着工程跑。结构守卫 <c>EditorPreferenceWiringTests.ProductCode_NoLongerReferencesEditorConfig</c>
/// 会拦住重新接线。</para>
/// </summary>
public class EditorConfig
{
    /// <summary>（已废弃）画布网格尺寸。真实来源：<c>SettingsService.Interaction.GridSize</c>。</summary>
    public int GridSize { get; set; } = 8;

    /// <summary>（已废弃）网格吸附开关。真实来源：<c>SettingsService.Interaction.EnableSnapToGrid</c>。</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>（已废弃）显示网格。真实来源：<c>SettingsService.Interaction.ShowGrid</c>。</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>（已废弃）编辑器画布背景色。画布底色由主题资源决定。</summary>
    public string CanvasBgColor { get; set; } = "#131c31";

    /// <summary>（已废弃）标尺开关。标尺功能不存在。</summary>
    public bool CanvasRulerVisible { get; set; } = true;

    /// <summary>（已废弃）撤销栈深度。<c>UndoRedoManager</c> 目前无容量上限。</summary>
    public int UndoStackSize { get; set; } = 100;

    /// <summary>（已废弃）自动保存间隔。真实来源：<c>SettingsService.Interaction.AutoSaveInterval</c>。</summary>
    public int AutoSaveIntervalSec { get; set; } = 60;

    /// <summary>（已废弃）界面语言。真实来源：语言菜单 → <c>LocalizationService.SwitchLanguage</c>。</summary>
    public string PreferredLanguage { get; set; } = "zh-CN";

    /// <summary>（已废弃）新建控件默认字体。真实来源：<c>SettingsService.Display.DefaultFont</c>（尚未接线）。</summary>
    public string DefaultFontFamily { get; set; } = "Noto Sans CJK SC";

    /// <summary>（已废弃）字号步进。真实来源：<c>SettingsService.Display.FontStep</c>（尚未接线）。</summary>
    public int FontSizeStep { get; set; } = 2;

    /// <summary>（已废弃）实时预览。真实来源：<c>SettingsService.Interaction.EnableLivePreview</c>（尚未接线）。</summary>
    public bool EnableLivePreview { get; set; } = true;

    /// <summary>（已废弃）控件拾取模式。该功能未实现。</summary>
    public ComponentPickerMode ComponentPickerMode { get; set; } = ComponentPickerMode.DragDrop;
}
