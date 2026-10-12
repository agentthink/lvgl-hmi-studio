namespace HmiEditor.Core.Models;

public enum HmiControlType
{
    Screen,
    Button,
    Label,
    Slider,
    Switch,
    CheckBox,
    Image,
    Bar,
    Dropdown,
    TextArea,

    // Wave 2 新增
    Gauge,       // 仪表盘
    Knob,        // 旋钮
    Keyboard,    // 键盘
    Led,         // LED 指示灯
    Spinner,     // 加载动画
    TabView,     // 选项卡
    Line,        // 线条
    Table,       // 表格

    // I6 新增
    Svg,         // SVG 矢量图形(内嵌形状列表,见 HmiControl.SvgShapes)

    // I7 新增: 过程图形控件
    Tank,        // 罐体（垂直液位填充 + 刻度）
    Pipe,        // 管道（线缆 + 流向动画）
    Valve,       // 阀门（状态色切换,可交互）

    // I10 新增: 基础绘图图形（画布工具栏"画图"按钮创建）
    Rectangle,   // 矩形（填充 + 边框 + 圆角）
    Ellipse,     // 椭圆/圆（填充 + 边框）
    Polyline,    // 折线（顶点列表 HmiControl.Points，线色 BgColor、线宽 BorderWidth）
    Arc,         // 圆弧（角度范围 MinValue..MaxValue，当前值 Value；轨道色 BgColor、指示色 IndicatorColor/TextColor）
}
