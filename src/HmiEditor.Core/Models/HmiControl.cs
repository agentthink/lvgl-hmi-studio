using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

public class HmiControl
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public HmiControlType Type { get; set; }
    public string Name { get; set; } = "";

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 40;

    // ── 旋转 / 镜像变换（默认值保证旧项目兼容）──

    /// <summary>旋转角度（度，归一化 0-360）。顺时针/逆时针由 <see cref="RotationDirection"/> 决定。</summary>
    public double Rotation { get; set; }

    /// <summary>
    /// 旋转方向："CW"=顺时针（默认）、"CCW"=逆时针。null 视为顺时针。
    /// 影响 <see cref="EffectiveRotation"/> 的符号。
    /// </summary>
    public string? RotationDirection { get; set; }

    /// <summary>水平镜像（左右翻转）。默认 false。</summary>
    public bool MirrorX { get; set; }

    /// <summary>垂直镜像（上下翻转）。默认 false。</summary>
    public bool MirrorY { get; set; }

    /// <summary>
    /// 实际旋转角（度，含方向）：顺时针为正、逆时针为负。
    /// 设计器渲染与代码生成共用，避免方向语义两处漂移。
    /// </summary>
    public double EffectiveRotation()
        => (RotationDirection == "CCW" ? -1 : 1) * Rotation;

    /// <summary>是否启用任何变换（旋转或镜像），供渲染/命中测试快速跳过。</summary>
    public bool HasTransform => Rotation != 0 || MirrorX || MirrorY;

    /// <summary>归一化角度到 [0, 360)，供工具栏"旋转 N 度"叠加使用。</summary>
    public static double NormalizeRotation(double angle)
        => ((angle % 360) + 360) % 360;

    // ── R2-T1: 层管理属性（默认值保证旧项目兼容）──

    /// <summary>是否锁定（锁定后不可选中/拖拽/编辑）。默认 false。</summary>
    public bool IsLocked { get; set; }

    /// <summary>是否在设计器中可见（隐藏后不渲染）。默认 true。</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>层索引（Z 序排序用，值小的在下层）。默认 0。</summary>
    public int LayerIndex { get; set; }

    public string? Text { get; set; }
    public int Value { get; set; }
    public bool IsChecked { get; set; }

    public string? BgColor { get; set; }
    public string? TextColor { get; set; }
    public int Radius { get; set; }
    public int BorderWidth { get; set; }
    public string? BorderColor { get; set; }

    // ── 状态化样式与图片（完整四态：Normal/Pressed/Checked/Disabled）──

    /// <summary>
    /// 主图 / 默认态图片资源 ID（引用 AssetItem.Id）。
    /// 用于 Image 控件，或按钮等控件的 Normal 态图片。
    /// null 表示无图片，回退到纯色 + 文字渲染。
    /// </summary>
    public string? ImageAssetId { get; set; }

    /// <summary>文本在控件内的水平对齐（默认态 / Normal 态）。</summary>
    public TextAlignMode TextAlign { get; set; } = TextAlignMode.Center;

    /// <summary>文本在控件内的垂直对齐（默认态 / Normal 态）。</summary>
    public TextVAlignMode TextVAlign { get; set; } = TextVAlignMode.Middle;

    /// <summary>控件文本字体族名称（null = 使用主题/项目默认字体）。</summary>
    public string? FontFamily { get; set; }

    /// <summary>控件文本字号（0 = 使用主题默认字号 14px）。</summary>
    public int FontSize { get; set; } = DefaultFontSize;

    /// <summary>控件默认字号（px）—— 模型默认值 / 设计器渲染兜底 / 主题 FontSize 默认值统一引用。</summary>
    public const int DefaultFontSize = 14;

    // ── 值显示格式化（Label 等显示型控件绑定变量后如何显示）──

    /// <summary>
    /// 值显示类型 —— 绑定变量后按此类型格式化显示值。
    /// Auto 按变量数据类型自动判断，其余强制按指定类型。
    /// </summary>
    public ValueDisplayType ValueDisplayType { get; set; } = ValueDisplayType.Auto;

    /// <summary>数值显示缩放比例（显示值 = 原始值 × 该系数）。默认 1.0。</summary>
    public double ValueScaleFactor { get; set; } = 1.0;

    /// <summary>小数显示位数（0-6，仅 Float 显示类型生效）。默认 2。</summary>
    public int ValueDecimalPlaces { get; set; } = 2;

    /// <summary>值前缀文本（显示在值之前，如 "温度: "）。</summary>
    public string? ValuePrefix { get; set; }

    /// <summary>值后缀文本（显示在值之后，如单位 "℃"）。</summary>
    public string? ValueSuffix { get; set; }

    // ── 控件功能参数（各控件按类型取用，JSON 默认值向后兼容）──

    /// <summary>量程下限（Slider/Bar/Gauge/Knob）。默认 0。</summary>
    public double MinValue { get; set; }

    /// <summary>量程上限（Slider/Bar/Gauge/Knob）。默认 100。</summary>
    public double MaxValue { get; set; } = 100;

    /// <summary>步长（Slider）。默认 1。</summary>
    public double StepValue { get; set; } = 1;

    /// <summary>是否显示刻度线（Slider/Bar）。默认 false（不改变既有外观）。</summary>
    public bool ShowTicks { get; set; }

    /// <summary>
    /// 是否显示指针/滑块（Slider 的 knob、Bar 的末端游标）。
    /// null = 按类型默认：Slider → true（保持既有 knob 显示），Bar → false（Bar 原本无指针）。
    /// 老工程反序列化为 null 时保持既有外观不变。
    /// </summary>
    public bool? ShowPointer { get; set; }

    /// <summary>指针有效可见性：显式设置优先，否则按类型默认（Slider 显示 / Bar 隐藏）。</summary>
    public bool EffectiveShowPointer()
        => ShowPointer ?? Type == HmiControlType.Slider;

    /// <summary>当前选中索引（Dropdown/TabView）。默认 0。</summary>
    public int SelectedIndex { get; set; }

    /// <summary>下拉选项列表（Dropdown，换行 \n 分隔）。null 表示未配置。</summary>
    public string? Options { get; set; }

    /// <summary>占位文本（TextArea）。null 表示无占位。</summary>
    public string? PlaceholderText { get; set; }

    /// <summary>最大输入长度（TextArea，0 表示无限制）。默认 0。</summary>
    public int MaxLength { get; set; }

    /// <summary>密码模式（TextArea，输入以圆点显示）。默认 false。</summary>
    public bool PasswordMode { get; set; }

    /// <summary>主刻度数量（Gauge）。默认 6。</summary>
    public int MajorTickCount { get; set; } = 6;

    /// <summary>表格行数（Table）。默认 3。</summary>
    public int RowCount { get; set; } = 3;

    /// <summary>表格列数（Table）。默认 3。</summary>
    public int ColumnCount { get; set; } = 3;

    /// <summary>表头文本（Table，逗号分隔，如 "名称,数值,单位"）。null 表示无表头。</summary>
    public string? HeaderTexts { get; set; }

    /// <summary>表格行高（Table，px；0=按内容自适应）。默认 0。</summary>
    public double RowHeight { get; set; }

    /// <summary>
    /// 表格列宽（Table，逗号分隔，如 "100,150,200"）。
    /// 空 = 各列均分控件宽度（左右均衡填满）；某项 "auto" = 该列按内容自适应（LV_COORD_MAX）。
    /// </summary>
    public string? ColumnWidths { get; set; }

    /// <summary>表格单元格数据（Table，稀疏存储，只存配置过的单元格）。null 表示无单元格内容。</summary>
    public List<TableCellData>? Cells { get; set; }

    /// <summary>页签标题（TabView，逗号分隔）。null 表示未配置。</summary>
    public string? TabTitles { get; set; }

    /// <summary>图片缩放模式（Image：None/Fill/Contain）。null 表示默认。</summary>
    public string? StretchMode { get; set; }

    /// <summary>LED 亮度 0-255（Led）。默认 128。</summary>
    public int Brightness { get; set; } = 128;

    /// <summary>加载动画周期 ms（Spinner）。默认 1000。</summary>
    public int SpinSpeed { get; set; } = 1000;

    /// <summary>线条端点样式（Line：Butt/Round/Square）。null 表示默认。</summary>
    public string? LineCap { get; set; }

    // ── I10: 基础绘图图形参数（Rectangle/Ellipse/Polyline/Arc 按类型取用，JSON 默认值向后兼容）──

    /// <summary>
    /// 折线/多边形顶点列表（Polyline，控件局部坐标，见 <see cref="PointData"/>）。
    /// null 表示未配置 —— 设计器与代码生成回退为一条默认对角线。
    /// </summary>
    public List<PointData>? Points { get; set; }

    /// <summary>键盘模式（Keyboard：Default/Number/Special）。null 表示默认。</summary>
    public string? KeyboardMode { get; set; }

    // ── I7: 过程图形控件参数（Tank/Pipe/Valve 按类型取用，JSON 默认值向后兼容）──

    /// <summary>是否显示液位刻度（Tank）。默认 true。</summary>
    public bool TankShowScale { get; set; } = true;

    /// <summary>主刻度数量（Tank，2-10）。默认 5。</summary>
    public int TankTickCount { get; set; } = 5;

    /// <summary>管道方向（Pipe：Horizontal/Vertical）。默认 Horizontal。</summary>
    public string? PipeDirection { get; set; }

    /// <summary>流向（Pipe：Forward/Reverse）。默认 Forward。</summary>
    public string? PipeFlowDirection { get; set; }

    /// <summary>流向动画周期 ms（Pipe，0=静态无动画）。默认 2000。</summary>
    public int PipeFlowSpeed { get; set; } = 2000;

    /// <summary>阀门类型（Valve：Ball/Gate/Butterfly/Globe）。默认 Ball。</summary>
    public string? ValveType { get; set; }

    // ── LVGL 标准行为配置（对齐官方文档）──

    /// <summary>
    /// 指示器颜色（Slider/Bar/Switch/Gauge/Knob 的 LV_PART_INDICATOR 填充色）。
    /// null 表示未配置，回退到 TextColor/BgColor。
    /// </summary>
    public string? IndicatorColor { get; set; }

    /// <summary>
    /// 标签长文本模式（Label：Wrap/Dots/Scroll/ScrollCircular/Clip）。
    /// null 表示默认换行（LVGL v9 的 LV_LABEL_LONG_WRAP）。
    /// </summary>
    public string? LabelLongMode { get; set; }

    /// <summary>
    /// 数值控件模式（Slider/Bar：Normal/Symmetrical/Range）。
    /// null 表示默认（LV_SLIDER_MODE_NORMAL / LV_BAR_MODE_NORMAL）。
    /// </summary>
    public string? ValueMode { get; set; }

    /// <summary>对象不透明度 0-100（LVGL lv_obj_set_style_opa，255 满值）。默认 100。</summary>
    public double Opacity { get; set; } = 100;

    /// <summary>
    /// 状态化样式覆盖列表 —— 每个条目覆盖一个状态（Pressed/Checked/Disabled）的部分样式字段。
    /// 字段为 null 表示继承默认态（Normal）。
    /// 空集合时全部继承默认样式（行为同未引入此字段前的旧版本）。
    /// </summary>
    public ObservableCollection<ControlVisualState> VisualStates { get; set; } = new();

    public string? ParentId { get; set; }
    public ObservableCollection<HmiControl> Children { get; set; } = new();

    /// <summary>
    /// 组合ID —— 多个控件共享同一 GroupId 时视为一个组合整体。
    /// null 表示不属于任何组合。组合内控件可统一选中、移动、缩放。
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>控件动作（运行时点击触发）。</summary>
    public HmiAction? Action { get; set; }

    /// <summary>绑定的变量 ID（用于显示控件如 Label/Bar/Gauge）。</summary>
    public string? VariableBindingId { get; set; }

    /// <summary>变量绑定模式: ReadOnly（显示）或 ReadWrite（输入）。</summary>
    public string BindingMode { get; set; } = "ReadWrite";

    // ── 联动模型（Phase R5）：集合化绑定与动作 ──

    /// <summary>
    /// 变量绑定集合 —— 控件可绑定多个变量到不同属性。
    /// 每个条目含方向(ReadOnly/WriteOnly/ReadWrite)。
    /// 替代旧的单一 VariableBindingId。
    /// </summary>
    public ObservableCollection<VariableBindingData> VariableBindings { get; set; } = new();

    /// <summary>
    /// 动作集合 —— 控件可注册多个动作到不同触发事件。
    /// 替代旧的单一 Action。
    /// </summary>
    public ObservableCollection<HmiAction> Actions { get; set; } = new();

    // ── 联动辅助属性 ──

    /// <summary>
    /// 主显示绑定变量（第一个 Direction != WriteOnly 的变量）。
    /// 联动模型下，预览解析和数据源显示取此值。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PrimaryBindingVariable
    {
        get
        {
            foreach (var b in VariableBindings)
            {
                if (b.Direction != BindingDirection.WriteOnly)
                    return b.VariableId;
            }
            // 集合为空时回退到旧字段
            return string.IsNullOrEmpty(VariableBindingId) ? null : VariableBindingId;
        }
    }

    /// <summary>
    /// 主写方向绑定变量（第一个 Direction 含 Write 的变量）。
    /// 联动模型下，输入控件的写动作 Target 默认取此值。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PrimaryWriteVariable
    {
        get
        {
            foreach (var b in VariableBindings)
            {
                if (b.Direction is BindingDirection.WriteOnly or BindingDirection.ReadWrite)
                    return b.VariableId;
            }
            return null;
        }
    }

    /// <summary>
    /// 按状态查询样式覆盖。未配置该态则返回 null，调用方回退到默认属性。
    /// Normal 态本身通常不在此集合中（默认属性即 Normal 态），但若显式配置也会返回。
    /// </summary>
    public ControlVisualState? GetStateStyle(VisualState state)
        => VisualStates.FirstOrDefault(s => s.State == state);

    // ── Faceplate 实例字段（Phase R2）──

    /// <summary>
    /// 引用的模板 ID —— 非空时此控件为 Faceplate 实例。
    /// 画布上表现为矩形占位，代码生成时由 FaceplateExpander 展开。
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// 固定的模板版本号 —— null 表示跟随最新版本。
    /// 固定版本可防止模板修改意外影响已部署实例。
    /// </summary>
    public int? TemplateVersion { get; set; }

    /// <summary>
    /// 参数绑定列表 —— 将模板参数名映射到实际值。
    /// 展开时替换模板内部占位符 {{Parameters.参数名}}。
    /// </summary>
    public ObservableCollection<ParameterBinding> ParameterBindings { get; set; } = new();

    // ── I6: SVG 矢量控件（Type == Svg 时有效）──

    /// <summary>SVG 形状列表（viewBox 坐标系）。旧项目无此字段时为 null。</summary>
    public List<SvgShape>? SvgShapes { get; set; }

    /// <summary>SVG viewBox 宽度（用于形状坐标 → 控件尺寸映射）。</summary>
    public double SvgViewBoxWidth { get; set; } = 100;

    /// <summary>SVG viewBox 高度。</summary>
    public double SvgViewBoxHeight { get; set; } = 100;

    /// <summary>
    /// 浅拷贝 —— 逐位复制**全部**字段（含将来新增的属性），但集合与引用型子对象仍与源**共享**。
    ///
    /// <para><b>为什么需要它</b>：Faceplate 展开等场景需要"复制一个控件的全部配置"。
    /// 本类有 80+ 属性，手工逐个赋值<b>必然随新增字段漂移</b> —— 实测
    /// <c>FaceplateExpander.CloneControl</c> 就漏掉了 <c>ShowPointer</c> / <c>ShowTicks</c> /
    /// <c>MinValue</c>/<c>MaxValue</c>/<c>StepValue</c> / <c>IndicatorColor</c> / <c>Rotation</c> /
    /// <c>ImageAssetId</c> / <c>TextAlign</c> / <c>FontSize</c> / <c>Points</c> / <c>Cells</c> /
    /// <c>SvgShapes</c> / <c>VisualStates</c> 等约 50 项，于是 Faceplate 里放的
    /// SVG / 表格 / 滑块 / 折线 / 图片 / 旋转控件展开后**全部退化成默认值**。</para>
    ///
    /// <para><b>调用方义务</b>：浅拷贝后必须把集合与引用型子对象替换成新的独立实例，否则副本与源共享 ——
    /// <see cref="Children"/> / <see cref="VariableBindings"/> / <see cref="Actions"/> /
    /// <see cref="ParameterBindings"/> / <see cref="VisualStates"/> / <see cref="Cells"/> /
    /// <see cref="Points"/> / <see cref="SvgShapes"/> / <see cref="Action"/>。</para>
    /// </summary>
    public HmiControl ShallowCopy() => (HmiControl)MemberwiseClone();
}
