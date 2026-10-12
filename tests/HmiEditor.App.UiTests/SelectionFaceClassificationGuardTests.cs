using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **选择族接线的静态守卫**（阶段 115）—— <see cref="HostButtonEffectUiTests"/> 那句
/// 「面的第二条腿」的另一半，单独成一条判据。
///
/// <para><b>为什么非要有它：</b>运行期那一半只能判两件事 ——「**声明了的**名字真的存在」与
/// 「拨一下它，声明的分量真的变了」。第三件事「宿主里**还有哪些**带 <c>SelectionChanged</c>
/// 接线的控件没被分类」在运行期**原理上算不出来**：Avalonia 不暴露订阅者名单，
/// 而「拨一下什么都没变」与「压根没接线」在读数上完全同形。于是新控件可以静默滑进面 ——
/// 这正是登记 #65 / #67 那一类缺陷当时只能靠人读 XAML 发现的根因。</para>
///
/// <para><b>判据由六条组成，各自的腿与它守的东西一一对应：</b></para>
/// <list type="number">
///   <item><description><b>面内双向闭合</b>（<see cref="EveryWiredSelectControl_InAHost_IsClassifiedInTheRuntimeTable"/>）——
///     二十三个宿主的「接线了却没分类」（新控件静默滑进面）与「分类了却找不到接线」
///     （声明腐烂、或订阅被摘）**都红**。
///     🩸 这个面**必须与运行期表同源**：声明从
///     <see cref="HostButtonEffectUiTests.SelectDeclarations"/> 取，**不让本文件去扫那 2500 行里的
///     <c>SelMoves("X"</c> 字面量** —— 抄成两份之后，表一改两份就对不上，而错的会是守卫那一份
///     （它还会因为注释里提到某个名字而假绿）。</description></item>
///   <item><description><b>面外点名</b>（<see cref="WiredSelectControls_OutsideTheHosts_MatchTheRegisteredCensus"/>）——
///     <c>Views/</c> 下另 **3** 个非宿主视图（**递归**枚举，含子目录）里还有 **39** 个
///     这样的控件。它们**不在本层的面内**，
///     但也不许**无声无息**：按「文件 + 该文件里的接线控件个数」逐个登记理由。
///     往已登记的文件里再塞一个、或新建一个带接线的视图文件，都会红。</description></item>
///   <item><description><b>扫描器自检</b>（<see cref="TheScanner_SeesAllThreeWiringForms_AndOnlyThose"/>）——
///     **两侧都断言**：三种接线形态（XAML 属性 / code-behind <c>+=</c> / 辅助函数注入）必须都被认出来，
///     而四种**不许**进面的东西（退订 <c>-=</c>、类型不匹配的辅助函数实参、Button 族、注释里的散文）
///     必须一个都不进去。</description></item>
///   <item><description><b>辅助函数识别在真语料上活着</b>
///     （<see cref="TheHelperResolution_IsAliveOnTheRealCorpus"/>）——
///     自检用的是合成语料，只能证明「逻辑对」；这一条钉的是「它在这个仓库里真的认出了东西」。
///     少了它，辅助函数识别一旦对真实写法失效，那 14 个下拉会**静默掉出面**，
///     而如果那时表里也正好没登记它们，守卫照样全绿。</description></item>
///   <item><description><b>扫描面自陈</b>
///     （<see cref="EveryAxamlInTheAppProject_IsInsideTheDeclaredScanFace"/>）——
///     App 项目里**全部** <c>.axaml</c>（递归、去 <c>bin/obj</c>）必须被两张表**之一**认领：
///     <c>Views/</c> 下的归判据二那张普查表，<c>Views/</c> 之外的逐文件进
///     <see cref="OutsideViewsAxaml"/>。于是「新加一个 <c>.axaml</c>」这件事
///     **无论放在哪**都会有人逼你表态。判据二只扫 <c>Views/</c>，没有这一条时
///     「把新窗口放到 App 根目录」两处都不红。
///     🩸 <b>阶段 125 起它连「个数」一起钉</b>：此前 <c>MainWindow</c> 那格写的是
///     "跨文件注入 ⇒ 连有几个都数不出来" —— 一个**免证**的格子，往里加第二条接线不会红。
///     现在两张表**同一套机制、同一套双向闭合**（多一个、少一个各自报红）。</description></item>
///   <item><description><b>跨文件解析活着</b>
///     （<see cref="TheCrossFileResolution_SeesCarrierAndCallSiteInDifferentFiles"/>）——
///     合成语料上钉住「载体在文件 A、调用点在文件 B」。判据四钉的是"真语料上认得出注入"，
///     而本仓真语料里**没有**跨文件注入（只有跨文件的直接订阅）⇒ 这一半只能靠合成语料钉，
///     否则 <see cref="ScanHelperInjectionAcross"/> 会退化成一个**没人验过**的分支。</description></item>
/// </list>
///
/// <para><b>面的大小（阶段 133 重数；三块都是读数不是下界）：</b>
/// 面内 **16** 宿主 **58** 个带接线的具名非 Button 控件；<c>Views/</c> 下另有 **9** 个视图文件、
/// **56** 个；<c>Views/</c> 之外 3 个 <c>.axaml</c>、**1 个**（<c>MainWindow</c> 的
/// <c>CboResolutionPreview</c>）—— 三者之差就是**本层与静态面的边界**，它是**自陈的**、
/// 不是被证过的。</para>
///
/// <para>🩸 <b>重数时抓到一处文档漂移</b>：这三行在本阶段之前写的是「9 宿主 47 个 / 16 视图 63 个」，
/// 而**实测（把两个文件 stash 回阶段 125 的 HEAD 再跑一遍）是 9 宿主 51 个 / 16 视图 63 个** ——
/// 也就是说 `Views/` 之外那半一直是对的，**面内那半自阶段 120 起就少算了 4 个控件**
/// （阶段 120 给某个宿主补了 3 枚下拉、阶段 124 又加了 `UserDetailRoleCombo` 那一格）。
/// 阶段 126 加第 10 个宿主（`CrossReferenceView`）后是 **52 / 15 视图 / 62 个**，
/// 阶段 127 加第 11 个（`DownloadToHmiView`）后是 **53 / 14 视图 / 61 个**，
/// 阶段 128 加第 12 个（`LayerPanelView`）后是 **54 / 13 视图 / 60 个**，
/// 阶段 129 加第 13 个（`ProjectExplorerView`）后是 **55 / 12 视图 / 59 个**，
/// 阶段 131 加第 14 个（`Editors/VariablePicker`，**首个子目录宿主**）后是 **56 / 11 视图 / 58 个**，
/// 阶段 132 加第 15 个（`AssetManagerView`）后是 **57 / 10 视图 / 57 个**
/// （⚠ 面内控件数与面外控件数**恰好相等**是巧合，别把它当成口径），
/// 阶段 133 加第 16 个（`FaceplateLibraryView`）后是 **58 / 9 视图 / 56 个**，
/// 阶段 136 加第 17 个（`AnimationEditorView` —— 面外剩下的**最大一块选择族**，8 个接线控件全在同一文件）
/// 后是 **66 / 8 视图 / 48 个**，
/// 阶段 137 删掉两个死视图（`#71` / `#73`）后是 **66 / 7 视图 / 47 个**
/// （`VariableBrowserView` 自己那 1 个接线控件随文件一起走；`PrototypeCanvas` 本来就没在选择面里）、
/// 阶段 142 加第 18 个（`TagMonitorView`）后是 **67 / 6 视图 / 46 个**、
/// 阶段 144 加第 19 个（`DesignCanvasView`，**指针序列手势**）后是 **68 / 5 视图 / 45 个**、
/// 阶段 149 加第 20 个（`SimulatorView`）后是 **73 / 4 视图 / 40 个**、
/// 阶段 150 加第 21 个（`DeviceManagerView`）后是 **74 / 3 视图 / 39 个**、
/// 阶段 157 加第 22 个（`ScriptEditorView`）后是 **77 / 2 视图 / 36 个**、
/// 阶段 158 加第 23 个（`RecipeEditorView`，**面外最后一块真面**）后是 **82 / 1 视图 / 31 个**
/// （⚠ 面外那 1 个视图就是上表里那条 `PropertyPanelView` —— 它**刻意不扩**，理由见表内注释）。
/// ⚠ 这类数字**没有任何判据看着**（它们只是散文）⇒ 只能靠"每次动面都重数"这条纪律，
/// 而本阶段正好是**先重数、再改**。</para>
///
/// <para>🩸 <b>阶段 115 这里写的是「16 个 / 64 个」，那天数错了</b>：枚举用的是
/// <c>Directory.EnumerateFiles(dir, "*.axaml")</c> —— **不带 <c>SearchOption.AllDirectories</c>**，
/// 于是 <c>Views/Editors/VariablePicker.axaml</c> 整个子目录**静默掉出扫描面**（少 1 文件 / 1 控件）。
/// 而同仓 <see cref="SourceScanner.Enumerate"/> 本来就是递归的 —— 守卫却自己手写了一个非递归的。
/// 它之所以瞒得过双向闭合：那张表只对「**扫到的**」做闭合，**扫什么**由枚举决定，
/// 而**枚举的范围本身没有任何判据**。这是「面的边界 ≠ 没有缺口」的第五形态 ——
/// <b>账本的覆盖范围自己没有被钉住</b>。修法见判据二（递归）＋ 判据五（<c>Views/</c> 之外逐文件登记）。</para>
///
/// <para>🩸 <b>同一族缺陷的第六形态（阶段 125 收口）：枚举的**语义单位**选错了。</b>
/// 前五形态问的是"扫了哪些**文件**"，从没问过"这些文件是不是**一个类**的全部"——
/// 于是 <c>MainWindow</c>（代码分在 8 个 <c>.cs</c> 里）的接线掉在两张表**之间**：
/// 判据一/二看不见它（不在 <c>Views/</c>），判据五只登记文件、不声称个数。
/// 修法是把语义单位从"文件"改成"**partial class**"（<see cref="PartialClassFilesByClass"/>），
/// 并让判据五也走同一套机制 —— 判据五因此从"文件账本"升级成"**文件 ＋ 个数**账本"。</para>
///
/// <para>⚠ <b>三个口径别混</b>（阶段 116 实测撞过一次；**阶段 158 重数**）：运行期表里是 **108 条声明**
/// （同一控件可被驱动多次，唯一性键是 <c>(名字, 手势下标)</c> / 阶段 129 起还含**树路径**）；
/// **按宿主去重**是 **82 个控件**（与静态面对齐的就是这个数）；
/// **全仓按名字去重**是 **76 个**（<c>AlarmList</c> / <c>GroupList</c>
/// / <c>CategoryList</c> 等跨宿主同名）。读「面掉了几格」时先问一句**这是哪个口径的数** ——
/// 本仓不只一次在跨口径比较上翻过车。
/// 🩸 这段散文**漂过两次**：阶段 136 写 **81 / 66 / 63**、阶段 118 写 **53 / 47 / 44** ——
/// 它们同样是**没有判据看着的散文**。阶段 157 / 158 重数前**都先在 <c>git show HEAD:</c> 的那一版上
/// 复现出上一轮的值**（94 / 74 / 69 → 99 / 77 / 72，与宿主表那段散文对得上）才采信那支脚本 ——
/// 没有这一步，「重数」只是把上一次的漂移换成一个新的数。
/// ⚠ 阶段 158 这一轮正好是"三个口径各差一点"的活例子：**＋9 条声明 / ＋5 个控件 / ＋4 个名字**
/// —— 差的 4 个里就有 <c>GroupList</c>（它在别的宿主里已经声明过）。</para>
///
/// <para><b>不抓什么（诚实边界）：</b>
/// <list type="number">
///   <item><b>不判「接线接到了正确的处理器上」</b> —— 那是运行期那一半的事（声明目标值才判得出「接错处理器」）。</item>
///   <item><b>不判 Button 族</b> —— <c>Button</c> / <c>ToggleButton</c> / <c>CheckBox</c> / <c>RadioButton</c>
///     上的 <c>SelectionChanged</c> 属运行期双向差集的面，本文件**排除**它们，不重复记账。</item>
///   <item><b>不判没有 <c>x:Name</c> 的控件</b> —— 面是「**具名** 且 接线」；无名控件在运行期表里
///     无从引用。这类站点会被**数出来**并进读数（见判据一的读数行），但不进断言：
///     它们是本面的又一处自陈边界。</item>
///   <item><b>跨文件注入：阶段 125 起**已判**</b> —— 这里此前写着"调用点与定义点只在**同一个
///     <c>.axaml.cs</c> 内**解析 ⇒ 真实调用点若在别处本守卫看不见"，并把 <c>MainWindow.axaml</c>
///     整份文件划在面外（判据五只登记文件、**不声称个数**）。
///     🩸 阶段 125 把这条盲区**量清**了：<c>MainWindow</c> 的代码分在 **8 个 partial 文件**里
///     （<c>MainWindow.axaml.cs</c> ＋ 7 个 <c>Wiring.*.cs</c>），而它**唯一**的面内接线
///     <c>CboResolutionPreview.SelectionChanged +=</c> 正躺在 <c>Wiring.Commands.cs:79</c> ——
///     也就是说法是"看不见"，实际只需把「同文件」换成「同一个 partial class 的全部文件」。
///     现在解析面按**声明了该 partial class 的所有 <c>.cs</c>** 展开（索引见
///     <see cref="PartialClassFilesByClass"/>）：直接 <c>+=</c> 与辅助函数注入都在全部文件上并集，
///     且**载体与调用点可以分居两个文件**（<see cref="ScanHelperInjectionAcross"/>）。
///     <c>MainWindow</c> 那一格因此从"不声称个数"变成**有数的账本**（1 个）。
///     ⚠ 仍不判：**间接**接线（<c>A.SelectionChanged</c> 的处理器里再订 <c>B.SelectionChanged</c>）
///     —— 那要求真的执行代码，属运行期那一半。</item>
///   <item><b>不判动态接线</b> —— 反射 / <c>AddHandler(..., 事件对象)</c> / <c>DataTemplate</c> 里
///     由绑定驱动的选择，都不进面。实测本仓 <c>AddHandler</c> 全部是 Pointer 事件，与选择无关。</item>
/// </list></para>
/// </summary>
public class SelectionFaceClassificationGuardTests
{
    private readonly ITestOutputHelper _output;

    public SelectionFaceClassificationGuardTests(ITestOutputHelper output) => _output = output;

    private const string ViewsRelative = "src/HmiEditor.App/Views";

    /// <summary>Button 族的类型名（含派生）—— 它们不属本面（运行期双向差集已经覆盖）。</summary>
    private static readonly HashSet<string> ButtonFamily = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "CheckBox", "RadioButton",
    };

    /// <summary>
    /// 「类型 A 是不是类型 B（或其子类）」。**只够用就好**：本判据只需要知道
    /// <c>CheckBox is Button</c>、<c>ComboBox : SelectingItemsControl</c> 这一类关系 ——
    /// 目的是让「辅助函数要求的类型」能匹配上实参的**声明**类型，不是做一个完整的继承表。
    /// </summary>
    private static readonly Dictionary<string, string[]> BaseOf = new(StringComparer.Ordinal)
    {
        ["Button"] = new[] { "Control" },
        ["ToggleButton"] = new[] { "Button", "Control" },
        ["CheckBox"] = new[] { "ToggleButton", "Button", "Control" },
        ["RadioButton"] = new[] { "ToggleButton", "Button", "Control" },
        ["ComboBox"] = new[] { "SelectingItemsControl", "ItemsControl", "Control" },
        ["ListBox"] = new[] { "SelectingItemsControl", "ItemsControl", "Control" },
        ["TreeView"] = new[] { "ItemsControl", "Control" },
    };

    /// <summary>
    /// **面外普查表**（阶段 116 修正；阶段 115 那一份漏了子目录）：
    /// <c>Views/</c> 下**不在**本层二十三个宿主里、且**还存在**的视图文件，
    /// 以及该文件里「带 <c>SelectionChanged</c> 接线的具名非 Button 控件」的**个数**。
    ///
    /// <para>🩸 <b>键是「相对 <c>Views/</c> 的路径」，不是文件名</b>（阶段 116）：子目录里的
    /// <c>Editors/VariablePicker</c> 若与某个直系文件同名，取文件名会把两格撞成一格。</para>
    ///
    /// <para>为什么是「文件 + 个数」而不是逐个控件登记：面外 39 个控件逐个写理由是纯噪音，
    /// 而**个数**已经足够有牙齿 —— 往已登记的文件里再塞一个接线控件，个数就对不上；
    /// 新建一个带接线的视图文件，它就根本不在表里。这与 <c>UnusedBaseline</c> 是同一种形状：
    /// **不是清理清单，而是「变动必被点名」的账本**。</para>
    ///
    /// <para>⚠ 理由那一栏是**统一**的（本层宿主是从全仓里**挑**出来的 9 个大面板：
    /// 按钮多、且每个都有后端模型可读），所以它记的不是「为什么这个文件不在面内」，
    /// 而是「这个文件里现在有几个接线控件」这个**事实**。
    /// 🩸 <b>阶段 118 更正了一句在这里放了两个阶段的判断。</b>原文写的是
    /// 「<c>PropertyPanelView</c> 是面外最大的一处，也是这条路往后最该扩的地方」——
    /// 「最大」是对的（31 个接线控件，是第二名的近四倍），但「最该扩」**按个数推不出来**，
    /// 实测三条：
    /// <list type="number">
    ///   <item>它的**数据面已经被判过**了 —— <c>PropertyPanelWiringTests</c> 从逻辑树枚举
    ///     全部 70 个编辑器、按 26 个控件类型场景 ＋ 一个屏幕场景逐个驱动，
    ///     断言 <c>uncovered == 0</c>（9 个驱动不动的带理由豁免）。本层扩它主要是**重复记账**。</item>
    ///   <item>它的写入是**批量**的：<c>SaveProperties()</c> 每次事件把**全部**编辑器读一遍写回模型
    ///     ⇒ 本层「未声明的分量一字不变」这条核心断言会与产品既定行为**正面冲突**
    ///     （阶段 118 实测：在干净绑定态下拨一个下拉，连带写下 <b>6</b> 个模型字段）。</item>
    ///   <item>扩一个宿主的真实成本是「**它的全部具名按钮** ＋ 普查表里那一栏的选择控件」——
    ///     本表只记了后一半。对 <c>PropertyPanelView</c> 就是 15 ＋ 31 ＝ <b>46 条</b>声明。</item>
    /// </list>
    /// ⇒ 它的正确处置不是「不判」，而是**由另一条判据判**（见第 ① 条）。
    /// 本表这一栏因此只回答「这里还有几个接线控件」，**不回答**「该不该扩」。</para>
    ///
    /// <para>⚠ 本表**只覆盖 <c>Views/</c> 下的文件**。<c>Views/</c> 之外的 <c>.axaml</c>
    /// （<c>App.axaml</c> / <c>Assets/Icons.axaml</c> / <c>MainWindow.axaml</c>）另有
    /// <see cref="OutsideViewsAxaml"/> 逐文件登记 —— **两张表合起来才是完整的扫描面自陈**。</para>
    /// </summary>
    private static readonly (string View, int WiredSelects)[] OutsideFaceCensus =
    {
        // ⚠ `AnimationEditorView` 阶段 136 起**不在本表里** —— 已升格为第 17 个宿主
        //   （面外剩下的**最大**一块选择族：8 个带接线的选择控件）。
        // ⚠ `AssetManagerView` 阶段 132 起**不在本表里** —— 已升格为第 15 个宿主。
        // ⚠ `CrossReferenceView` 阶段 126 起**不在本表里** —— 它已升格为第 10 个宿主
        //   （见 HostButtonEffectUiTests 的宿主表）。本表的「陈账要清」两侧都会红，
        //   所以升格时必须把这一行**删掉**：留着它就是「表里有它、可它已经不是面外视图了」。
        // ⚠ `DesignCanvasView` 阶段 144 起**不在本表里** —— 已升格为第 19 个宿主，
        //   而且它进来时还给本层加了**第五类手势**（`Drags` 指针序列，登记 `#77`）。
        // ⚠ `DeviceManagerView` 阶段 150 起**不在本表里** —— 已升格为第 21 个宿主。
        // ⚠ `DownloadToHmiView` 阶段 127 起**不在本表里** —— 已升格为第 11 个宿主
        //   （升格时必须把那一行删掉：本表的「陈账要清」两侧都会红，理由同 CrossReferenceView）。
        // ⚠ `Editors/VariablePicker` 阶段 131 起**不在本表里** —— 已升格为第 14 个宿主，
        //   也是第一个**子目录**宿主（守卫的 ScanHost 本阶段才支持子目录路径）。
        // ⚠ `FaceplateLibraryView` 阶段 133 起**不在本表里** —— 已升格为第 16 个宿主。
        // ⚠ `LayerPanelView` 阶段 128 起**不在本表里** —— 已升格为第 12 个宿主。
        // ⚠ `ProjectExplorerView` 阶段 129 起**不在本表里** —— 已升格为第 13 个宿主。
        ("PropertyPanelView", 31),     // 🩸 面外最大的一处：整体由**选中项**驱动。
                                       //    ⚠ 但它**不是**「最该扩」的那一处 —— 见上方注释里
                                       //    阶段 118 的三条实测（已被 PropertyPanelWiringTests
                                       //    按 26 个类型场景覆盖；写入是批量的、与本层断言冲突）
        // ⚠ `RecipeEditorView` 阶段 158 起**不在本表里** —— 已升格为第 23 个宿主
        //   （它的 5 个接线控件 `GroupList` / `RecipeList` / `ParamList` / `CmbParamDataType` /
        //   `CmbParamVariable` 全进了运行期表）。⚠ 它是**面外最后一块真面** —— 加完它，
        //   面外只剩下面这条 `PropertyPanelView`（阶段 118 已判定不扩）。
        // ⚠ `ScriptEditorView` 阶段 157 起**不在本表里** —— 已升格为第 22 个宿主
        //   （它的 3 个接线控件 `ScriptList` / `CmbCategoryFilter` / `CboCategory` 全进了运行期表；
        //   ⚠ 它是第一个**自己就是 `Window`** 的宿主 ⇒ 两个字段处理器被 `Parent == null` 挡掉，
        //   如实记 `Inert` —— 见该宿主在 HostButtonEffectUiTests 里的注释）。
        // ⚠ `SimulatorView` 阶段 149 起**不在本表里** —— 已升格为第 20 个宿主。
        // ⚠ `TagMonitorView` 阶段 142 起**不在本表里** —— 已升格为第 18 个宿主
        //   （面外 7 个视图里代价最小的一个：**4 个具名按钮 ＋ 1 条接线下拉**）。
        // ⚠ `VariableBrowserView` 阶段 137 起**整条消失** —— 文件被删了（登记 `#71`，用户拍板下架），
        //   所以它不在面内、也不在面外账本里。⚠ 阶段 128 的历史仍值得记住：它一度被升格成宿主、
        //   判据全绿，随后核对才发现 `new VariableBrowserView()` 全仓零构造、事件零订阅 ⇒ 是**死面板**
        //   （见登记表 §50.1）；「判一个用户到不了的面板」正是阶段 135 那条可达性守卫要防的事。
    };

    private static string AppProjectDirectory => Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App");

    private static string ViewsDirectory => Path.Combine(AppProjectDirectory, "Views");

    /// <summary>
    /// <c>Views/</c> 下**递归**枚举到的全部 <c>.axaml</c>（去 <c>bin/obj</c>）。
    ///
    /// <para>🩸 <b>这里必须有 <c>SearchOption.AllDirectories</c></b> —— 阶段 115 漏了它，
    /// <c>Views/Editors/VariablePicker.axaml</c> 整个子目录掉出扫描面，而双向闭合
    /// <b>照样绿</b>（它只对「扫到的」做闭合）。同仓 <see cref="SourceScanner.Enumerate"/>
    /// 本来就是递归的 ⇒ 这里没有理由手写一个非递归的。</para>
    /// </summary>
    private static IReadOnlyList<string> ViewAxamlFiles() =>
        Directory.EnumerateFiles(ViewsDirectory, "*.axaml", SearchOption.AllDirectories)
            .Where(p => !SourceScanner.IsBuildOutput(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    private static string Rel(string absolute) => SourceScanner.Relative(absolute);

    // ══════════════════════════════════════════════════════════════
    //  扫描器（internal static —— 判据三要用合成语料直接测它们）
    // ══════════════════════════════════════════════════════════════

    /// <summary>一条「辅助函数载体」：某个方法的某个参数会把收到的控件挂上 <c>SelectionChanged</c>。</summary>
    internal sealed record HelperCarrier(string Method, string Parameter, int ParameterIndex, string? RequiredType);

    /// <summary>
    /// 方法头的形状。<c>[^()]*</c> 拿参数表（不跨右括号），<c>where …</c> 可选，最后必须跟 <c>{</c>。
    /// 关键字（<c>if</c> / <c>for</c> / <c>using</c> …）靠 <see cref="NotAMethod"/> 剔除。
    /// </summary>
    private static readonly Regex MethodHead = new(
        @"\b([A-Za-z_]\w*)\s*(?:<[^>{}]*>)?\s*\(([^()]*)\)\s*(?:where[^{;]*?)?\{",
        RegexOptions.Compiled);

    /// <summary>会被 <see cref="MethodHead"/> 误认成方法的关键字。</summary>
    private static readonly HashSet<string> NotAMethod = new(StringComparer.Ordinal)
    {
        "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed",
        "checked", "unchecked", "return", "new", "do", "else", "try", "get", "set",
        "add", "remove", "await", "yield", "is", "as", "in", "of", "nameof", "typeof",
        "sizeof", "default", "throw", "when", "stackalloc", "case",
    };

    /// <summary><c>x:Name</c>（拿不到时退回落成 <c>Name</c>）。</summary>
    internal static string? NameOfElement(XElement el)
    {
        foreach (var a in el.Attributes())
            if (a.Name.LocalName == "Name"
                && a.Name.NamespaceName.Contains("xaml", StringComparison.OrdinalIgnoreCase))
                return a.Value;
        foreach (var a in el.Attributes())
            if (a.Name.LocalName == "Name")
                return a.Value;
        return null;
    }

    /// <summary>元素上有没有 <c>SelectionChanged</c> 属性（不查命名空间 —— 它是控件属性，无前缀）。</summary>
    private static string? SelectionHandlerOf(XElement el)
        => el.Attributes().FirstOrDefault(a => a.Name.LocalName == "SelectionChanged")?.Value;

    /// <summary>
    /// 解析一个 <c>.axaml</c>：给出 <c>x:Name → 元素本地名</c> 的类型表 +
    /// 「带 <c>SelectionChanged</c> 属性且**有名字**」的元素 + 「带该属性但**没名字**」的元素。
    ///
    /// <para>⚠ 解析失败**不做容错**（抛出来，由调用方报红）：一个解析不了的 XAML 意味着
    /// 整个面不可信，静默跳过等于把「面塌了」写成「面绿了」。</para>
    /// </summary>
    internal static (Dictionary<string, string> TypeByName,
                     List<(string Name, string Handler)> Wired,
                     List<string> WiredButNameless)
        ScanXaml(string axamlText)
    {
        var typeByName = new Dictionary<string, string>(StringComparer.Ordinal);
        var wired = new List<(string, string)>();
        var nameless = new List<string>();

        var doc = XDocument.Parse(SourceScanner.StripXmlComments(axamlText));
        foreach (var el in doc.Descendants())
        {
            var type = el.Name.LocalName;
            var name = NameOfElement(el);
            if (name is not null) typeByName[name] = type;

            var handler = SelectionHandlerOf(el);
            if (handler is null) continue;
            if (name is null) nameless.Add(type + "（handler " + handler + "）");
            else wired.Add((name, handler));
        }

        return (typeByName, wired, nameless);
    }

    /// <summary>
    /// code-behind 里的**直接**接线：<c>&lt;标识符&gt;.SelectionChanged += …</c>。
    ///
    /// <para>🩸 两个刻意的限制：① **只认 <c>+=</c>，不认 <c>-=</c>** ——
    /// <c>AlarmListView</c> 在卸载时退订，那是反接线，算进面就是假缺口；
    /// ② **名字必须在本文件 XAML 的 <c>x:Name</c> 表里** —— 这一条把「辅助函数里的模式变量」
    /// 挡在外面（<c>cmb.SelectionChanged += …</c> 里的 <c>cmb</c> 是 <c>ctl is ComboBox cmb</c>
    /// 绑出来的局部名，不是控件名）。不带这两条，第一版探针实测多出 2 条假缺口。</para>
    /// </summary>
    internal static (List<string> Subscribed, List<string> Unsubscribed) ScanDirectWiring(
        string code, IReadOnlyDictionary<string, string> typeByName)
    {
        var clean = SourceScanner.StripComments(code);
        var subscribed = new List<string>();
        var unsubscribed = new List<string>();

        foreach (Match m in Regex.Matches(clean, @"(?:this\.)?([A-Za-z_]\w*)\.SelectionChanged\s*(\+=|-=)"))
        {
            var name = m.Groups[1].Value;
            if (!typeByName.ContainsKey(name)) continue;
            (m.Groups[2].Value == "+=" ? subscribed : unsubscribed).Add(name);
        }

        return (subscribed.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
                unsubscribed.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    /// <summary>按括号/尖括号/方括号配平切参数表（只为取到「第 n 个实参」）。</summary>
    internal static List<string> SplitArguments(string args)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case '(' or '<' or '[':
                    depth++;
                    break;
                case ')' or '>' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(args[start..i]);
                    start = i + 1;
                    break;
            }
        }

        if (args.Length > 0) result.Add(args[start..]);
        return result;
    }

    /// <summary>枚举方法头 + 括号配平的方法体。用法位置不参与，只要能拿到 <c>(params)</c> 与 <c>{…}</c>。</summary>
    internal static List<(string Name, string Parameters, string Body)> EnumerateMethods(string code)
    {
        var clean = SourceScanner.StripComments(code);
        var result = new List<(string, string, string)>();

        foreach (Match m in MethodHead.Matches(clean))
        {
            var name = m.Groups[1].Value;
            if (NotAMethod.Contains(name)) continue;

            var bodyStart = m.Index + m.Length;   // ⚠ Match 没有 End（那是 Python 的 m.end()）
            var i = bodyStart;
            var depth = 1;
            while (i < clean.Length && depth > 0)
            {
                if (clean[i] == '{') depth++;
                else if (clean[i] == '}') depth--;
                i++;
            }

            result.Add((name, m.Groups[2].Value, clean[bodyStart..Math.Max(bodyStart, i - 1)]));
        }

        return result;
    }

    /// <summary>
    /// 找出「会把参数上的控件挂上 <c>SelectionChanged</c>」的方法 —— 也就是**辅助函数注入**。
    ///
    /// <para>识别的形状：方法体里有一句 <c>&lt;局部名&gt;.SelectionChanged += …</c>，而那个局部名
    /// 是 <c>is &lt;类型&gt; &lt;局部名&gt;</c> 从**本方法的某个参数**上绑出来的。
    /// 于是「要求的类型」也一起拿到了 —— 这一点是**必需的**：本仓的
    /// <c>ProjectSettingsView.AttachChangeHandler&lt;T&gt;(T ctl)</c> 是个**通用变更跟踪器**，
    /// 四个分支分别订阅 <c>ValueChanged</c> / <c>TextChanged</c> / <c>IsCheckedChanged</c> /
    /// <c>SelectionChanged</c>。不带类型，它剩下的 35 个实参里会有 **25 个**
    /// （16 个数字框 + 9 个文本框）被算成接线 ⇒ **25 条假缺口**
    /// （另 10 个 <c>CheckBox</c> 属 <see cref="Button"/> 族，被本面排除）。
    /// ⚠ 25 是负控 **nc33** 实测点名出来的数 —— 本文件最初写的是 35（把复选框也算进去了），
    /// 是负控把它改对的。</para>
    /// </summary>
    internal static List<HelperCarrier> FindHelperCarriers(string code)
    {
        var clean = SourceScanner.StripComments(code);
        var carriers = new List<HelperCarrier>();

        foreach (var (name, parameters, body) in EnumerateMethods(clean))
        {
            if (!body.Contains(".SelectionChanged", StringComparison.Ordinal)) continue;

            var paramNames = SplitArguments(parameters)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Select(p => p.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length > 0)
                .Select(parts => parts[^1])
                .ToList();
            if (paramNames.Count == 0) continue;

            // 局部名 → (参数名, 要求的类型)。⚠ 只在**本方法体**里找模式匹配 ——
            //   拿整个文件找再靠 `body.Contains` 去对，会把别处同名参数的模式串到这个方法上。
            var bound = new Dictionary<string, (string Param, string? Type)>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(body, @"([A-Za-z_]\w*)\s+is\s+([\w.]+)(?:\s+([A-Za-z_]\w*))?"))
            {
                var source = m.Groups[1].Value;
                if (!paramNames.Contains(source, StringComparer.Ordinal)) continue;

                var local = m.Groups[3].Success ? m.Groups[3].Value : source;
                var type = m.Groups[2].Value.Split('.')[^1];
                if (!bound.ContainsKey(local)) bound[local] = (source, m.Groups[3].Success ? type : null);
            }

            var receivers = Regex.Matches(body, @"([A-Za-z_]\w*)\.SelectionChanged\s*\+=")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);

            foreach (var receiver in receivers)
            {
                if (bound.TryGetValue(receiver, out var b))
                    carriers.Add(new HelperCarrier(name, b.Param, paramNames.IndexOf(b.Param), b.Type));
                else if (paramNames.Contains(receiver, StringComparer.Ordinal))
                    carriers.Add(new HelperCarrier(name, receiver, paramNames.IndexOf(receiver), null));
            }
        }

        return carriers;
    }

    private static bool IsA(string actualType, string requiredType)
        => string.Equals(actualType, requiredType, StringComparison.Ordinal)
           || (BaseOf.TryGetValue(actualType, out var bases) && bases.Contains(requiredType, StringComparer.Ordinal));

    /// <summary>
    /// 找辅助函数的**调用点**：实参在该 XAML 的 <c>x:Name</c> 表里、且类型与「要求的类型」相容的，
    /// 才算接线点。<b>本方法只看**一份**语料</b> —— 跨文件的用法见
    /// <see cref="ScanHelperInjectionAcross"/>。
    /// </summary>
    internal static List<(string Name, string Method, string? RequiredType)> ScanHelperInjection(
        string code, IReadOnlyDictionary<string, string> typeByName)
    {
        var clean = SourceScanner.StripComments(code);
        var result = new List<(string Name, string Method, string? RequiredType)>();
        foreach (var carrier in FindHelperCarriers(clean))
            result.AddRange(CallSites(carrier, clean, typeByName));
        return DedupeInjected(result);
    }

    /// <summary>
    /// **一个**载体在**一份**（已剥注释的）语料里的全部调用点。
    ///
    /// <para>为什么单独抽出来：这是 <see cref="ScanHelperInjectionAcross"/> 的积木 ——
    /// 载体在文件 A、调用点在文件 B 时，两边各自只需要"找到载体"与"在这份语料里找它的调用点"。</para>
    /// </summary>
    internal static List<(string Name, string Method, string? RequiredType)> CallSites(
        HelperCarrier carrier, string cleanCode, IReadOnlyDictionary<string, string> typeByName)
    {
        var result = new List<(string Name, string Method, string? RequiredType)>();
        var call = new Regex(@"\b" + Regex.Escape(carrier.Method) + @"\s*(?:<[^>()]*>)?\s*\(([^()]*)\)");
        foreach (Match m in call.Matches(cleanCode))
        {
            var args = SplitArguments(m.Groups[1].Value);
            if (carrier.ParameterIndex >= args.Count) continue;

            var arg = args[carrier.ParameterIndex].Trim();
            if (!Regex.IsMatch(arg, @"^[A-Za-z_]\w*$")) continue;
            if (!typeByName.TryGetValue(arg, out var actual)) continue;
            if (carrier.RequiredType is not null && !IsA(actual, carrier.RequiredType)) continue;

            result.Add((arg, carrier.Method, carrier.RequiredType));
        }

        return result;
    }

    private static List<(string Name, string Method, string? RequiredType)> DedupeInjected(
        List<(string Name, string Method, string? RequiredType)> items) =>
        items.Distinct().OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

    /// <summary>
    /// **跨文件**的辅助函数注入：载体与调用点可以分居同一个 partial class 的**两个不同文件**。
    ///
    /// <para>🩸 阶段 125 补。此前只有 <see cref="ScanHelperInjection"/>（定义与调用同文件），
    /// 而本仓的 <c>MainWindow</c> 把代码切在 8 个文件里 —— 「同一个类的一半在别的文件里」
    /// 是这类窗口的**常态**，不是特例。<b>做法刻意朴素</b>：载体在每个文件里各找一遍，
    /// 然后拿**每一个**载体去**每一份**语料里找调用点（含它自己那份 ⇒ 与单文件口径一致，
    /// 不会因为多了这条而丢东西）。</para>
    ///
    /// <para>⚠ 代价是 O(文件数²) 次正则扫描 —— 实测 8 个文件、毫秒级；若要扩到全仓，
    /// 得先按「文件名出现在载体里」之类做预筛，别直接套。</para>
    /// </summary>
    internal static List<(string Name, string Method, string? RequiredType)> ScanHelperInjectionAcross(
        IReadOnlyList<string> codes, IReadOnlyDictionary<string, string> typeByName)
    {
        var cleans = codes.Select(SourceScanner.StripComments).ToList();
        var result = new List<(string Name, string Method, string? RequiredType)>();

        foreach (var defining in cleans)
            foreach (var carrier in FindHelperCarriers(defining))
                foreach (var corpus in cleans)
                    result.AddRange(CallSites(carrier, corpus, typeByName));

        return DedupeInjected(result);
    }

    /// <summary>
    /// 一个宿主（<c>.axaml</c> ＋ 它**全部** <c>.cs</c>）的**面**：带接线的具名非 Button 控件。
    ///
    /// <para>⚠ 阶段 125 起 <c>Subscribed</c> / <c>Unsubscribed</c> 带**来源文件**（相对仓库根）：
    /// 解析面从"兄弟 <c>.axaml.cs</c>"扩到"同一个 partial class 的全部文件"之后，
    /// 读数里不写清哪一条来自哪个文件，出问题时就只能靠人再猜一遍。</para>
    /// </summary>
    private sealed record HostFace(
        string Host, Dictionary<string, string> TypeByName,
        List<(string Name, string Handler)> XamlWired,
        List<(string Name, string File)> Subscribed, List<(string Name, string File)> Unsubscribed,
        List<(string Name, string Method, string? RequiredType)> Injected,
        List<string> Nameless);

    /// <summary>
    /// 宿主名 → 它在 <c>Views/</c> 下的相对路径（子目录用 <c>.</c> 分隔）。
    ///
    /// <para>🩸 <b>阶段 131</b>：这个口径是**同时为两件事服务**的 ——
    /// ① `Assembly.GetType($"HmiEditor.App.Views.{hostName}")` 要能命中（子目录视图的类型名
    /// 正好是 <c>Editors.VariablePicker</c>）；② <c>Views/</c> 下的文件路径
    /// （<c>Editors/VariablePicker.axaml</c>）。此前 <see cref="ScanHost"/> 只会拼
    /// <c>Views/&lt;名字&gt;.axaml</c> ⇒ **子目录视图一旦升格就会"文件不存在"**，
    /// 而 C 档唯一的那个视图（`Editors/VariablePicker`）恰好就在子目录里
    /// ⇒ 那条路一直是关着的（阶段 127 的分档里记着这件事）。</para>
    /// </summary>
    private static string RelativeViewPath(string hostName) => hostName.Replace('.', '/');

    /// <summary>宿主名里最后一段（＝ <c>partial class</c> 的类名，供 <see cref="CodeFilesFor"/> 用）。</summary>
    private static string SimpleTypeName(string hostName) =>
        hostName.Contains('.') ? hostName[(hostName.LastIndexOf('.') + 1)..] : hostName;

    private static HostFace ScanHost(string viewName)
    {
        var axamlPath = Path.Combine(ViewsDirectory, RelativeViewPath(viewName) + ".axaml");
        if (!File.Exists(axamlPath))
            throw new FileNotFoundException($"宿主 {viewName} 的 {Rel(axamlPath)} 不存在");

        var (typeByName, wired, nameless) = ScanXaml(File.ReadAllText(axamlPath));
        var codes = CodeFilesFor(SimpleTypeName(viewName), axamlPath);

        var subscribed = new List<(string Name, string File)>();
        var unsubscribed = new List<(string Name, string File)>();
        foreach (var path in codes)
        {
            var (sub, unsub) = ScanDirectWiring(File.ReadAllText(path), typeByName);
            var file = Rel(path);
            subscribed.AddRange(sub.Select(n => (n, file)));
            unsubscribed.AddRange(unsub.Select(n => (n, file)));
        }

        return new HostFace(viewName, typeByName, wired,
                            DedupePairs(subscribed), DedupePairs(unsubscribed),
                            ScanHelperInjectionAcross(codes.Select(File.ReadAllText).ToList(), typeByName),
                            nameless);
    }

    /// <summary>去重 ＋ 按控件名（再按来源文件）排序 —— 同一个控件被两个文件各订一次就只算一条。</summary>
    private static List<(string Name, string File)> DedupePairs(List<(string Name, string File)> items) =>
        items.Distinct()
             .OrderBy(x => x.Name, StringComparer.Ordinal)
             .ThenBy(x => x.File, StringComparer.Ordinal)
             .ToList();

    /// <summary>
    /// 面（去掉 Button 族）：XAML 属性 + code-behind <c>+=</c> + 辅助函数注入，三处并起来。
    /// </summary>
    private static HashSet<string> FaceOf(HostFace host)
    {
        var face = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, _) in host.XamlWired) face.Add(name);
        foreach (var (name, _) in host.Subscribed) face.Add(name);
        foreach (var (name, _, _) in host.Injected) face.Add(name);
        foreach (var name in face.Where(n => host.TypeByName.TryGetValue(n, out var t) && ButtonFamily.Contains(t)).ToList())
            face.Remove(name);
        return face;
    }

    /// <summary>
    /// **`partial class` 名 → 声明了它的全部 `.cs`**（App 项目内递归、去 `bin/obj`）。
    ///
    /// <para>🩸 <b>这张索引就是"跨文件注入"那条盲区的解药</b>（阶段 125）。此前解析面只有
    /// 视图自己的 <c>.axaml.cs</c>，于是 <c>MainWindow</c> 的接线（订在
    /// <c>Wiring.Commands.cs:79</c>）整个掉出扫描面 —— 而它只是**换了个文件**，不是
    /// 换了什么机制。实测 App 里有 3 个多文件 partial class：<c>MainWindow</c>（8 文件）、
    /// <c>PropertyPanelView</c>（11 文件）、<c>DesignCanvas</c>（5 文件，无 .axaml 与本面无关）。</para>
    ///
    /// <para>⚠ 用的是**类名**而不是文件名（<c>Wiring.Commands.cs</c> 与 <c>MainWindow</c> 之间
    /// 没有任何命名关系）—— 这正是文件名推不出来的那一维。索引只建一次（<see cref="Lazy{T}"/>）。</para>
    /// </summary>
    private static readonly Lazy<Dictionary<string, List<string>>> PartialClassFilesByClass =
        new(BuildPartialClassIndex);

    private static readonly Regex PartialClassDecl =
        new(@"\bpartial\s+class\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    private static Dictionary<string, List<string>> BuildPartialClassIndex()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in SourceScanner.Enumerate(AppProjectDirectory))
        {
            var code = SourceScanner.StripComments(File.ReadAllText(path));
            foreach (Match m in PartialClassDecl.Matches(code))
            {
                if (!map.TryGetValue(m.Groups[1].Value, out var list))
                    map[m.Groups[1].Value] = list = new List<string>();
                if (!list.Contains(path, StringComparer.Ordinal)) list.Add(path);
            }
        }

        foreach (var list in map.Values) list.Sort(StringComparer.Ordinal);
        return map;
    }

    /// <summary>
    /// 一份 <c>.axaml</c> 背后的**全部**代码文件：声明了同名 <c>partial class</c> 的所有 <c>.cs</c>
    /// ∪ 它自己的 <c>.axaml.cs</c>。
    ///
    /// <para>后半截是**兜底**：极端写法（没有 <c>partial</c> 关键字、或 x:Class 与类名不一致）下
    /// 索引会空手而归，那时退回老口径（只看兄弟文件）至少不丢已经能看的东西；
    /// 两种情况都没有 ⇒ 空清单 ⇒ 面只剩 XAML 属性那一半，而这由判据一的双向闭合兜着。</para>
    /// </summary>
    private static IReadOnlyList<string> CodeFilesFor(string typeName, string axamlPath)
    {
        var files = new List<string>();
        if (PartialClassFilesByClass.Value.TryGetValue(typeName, out var declaring))
            files.AddRange(declaring);

        var sibling = axamlPath + ".cs";
        if (File.Exists(sibling) && !files.Contains(sibling, StringComparer.Ordinal))
            files.Add(sibling);

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// **任意** <c>.axaml</c>（含 <c>Views/</c> 之外的）的**面**：带接线（三形态并集）的具名非 Button 控件。
    ///
    /// <para>与 <see cref="FaceOf"/> 的区别只有一个：它自己把代码文件集合算出来（<see cref="CodeFilesFor"/>），
    /// 所以对 <c>Views/</c> 之外的 <c>.axaml</c> 也能给数 —— 判据五的账本靠它从"不声称个数"变成个数。</para>
    /// </summary>
    private static (HashSet<string> Face, Dictionary<string, string> TypeByName, List<string> Nameless)
        FaceOfAxaml(string axamlPath)
    {
        var (typeByName, wired, nameless) = ScanXaml(File.ReadAllText(axamlPath));
        var baseName = Path.GetFileNameWithoutExtension(axamlPath);
        var codes = CodeFilesFor(baseName, axamlPath).Select(File.ReadAllText).ToList();

        var face = new HashSet<string>(wired.Select(x => x.Name), StringComparer.Ordinal);
        foreach (var code in codes)
        {
            var (subscribed, _) = ScanDirectWiring(code, typeByName);
            foreach (var name in subscribed) face.Add(name);
        }

        foreach (var (name, _, _) in ScanHelperInjectionAcross(codes, typeByName)) face.Add(name);

        foreach (var name in face.Where(n => typeByName.TryGetValue(n, out var t) && ButtonFamily.Contains(t)).ToList())
            face.Remove(name);

        return (face, typeByName, nameless);
    }

    // ══════════════════════════════════════════════════════════════
    //  判据一：面内双向闭合
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 二十三个宿主里，「带 <c>SelectionChanged</c> 接线的具名非 Button 控件」的集合必须**恰好等于**
    /// 运行期表里声明为选择族的那些名字。
    ///
    /// <para>两个方向都要：</para>
    /// <list type="bullet">
    ///   <item><b>接线了却没分类</b> ⇒ 新控件静默滑进面。这是本条存在的理由，
    ///     也是静态守卫唯一能提供、运行期原理上给不出的东西。</item>
    ///   <item><b>分类了却找不到接线</b> ⇒ 声明腐烂（控件被改名/删掉，或
    ///     <c>SelectionChanged="…"</c> 被摘了）。运行期那一半**抓不住这一半**：
    ///     对 <c>SelInert</c> 条目，摘掉订阅与「拨一下什么都没变」在读数上完全同形 ——
    ///     这正是 <c>CommTree</c> 那条「登记事实、不声称接线」的由来。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void EveryWiredSelectControl_InAHost_IsClassifiedInTheRuntimeTable()
    {
        var declared = HostButtonEffectUiTests.SelectDeclarations();
        Assert.True(declared.Count > 0, "运行期表导出的声明一条都没有 —— 同源出口坏了，本判据会恒真");

        var hostsInTable = declared.Select(x => x.Host)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var readings = new List<string>();
        var offenders = new List<string>();
        var totalFace = 0;
        var totalDeclaredNames = 0;
        var totalDeclarations = 0;

        foreach (var hostName in hostsInTable)
        {
            // ⚠ 两个口径都要报，别混：**声明条数**（同一控件可被驱动多次）与
            //   **按宿主去重的控件数**。与静态面对齐的是后者。
            var hostDeclarations = declared.Where(x => x.Host == hostName).ToList();
            var declaredNames = hostDeclarations.Select(x => x.Control).ToHashSet(StringComparer.Ordinal);

            HostFace host;
            try
            {
                host = ScanHost(hostName);
            }
            catch (Exception ex)
            {
                offenders.Add($"{hostName}: 扫描失败 —— {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var face = FaceOf(host);
            totalFace += face.Count;
            totalDeclaredNames += declaredNames.Count;
            totalDeclarations += hostDeclarations.Count;

            var unclassified = face.Except(declaredNames, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var phantom = declaredNames.Except(face, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

            readings.Add($"{hostName}：面 {face.Count} / 声明 {hostDeclarations.Count} 条"
                         + $"（按宿主去重的控件 {declaredNames.Count} 个）"
                         + $"｜XAML 属性 {host.XamlWired.Count} / code-behind {host.Subscribed.Count}"
                         + $" / 辅助函数注入 {host.Injected.Count}"
                         + (host.Unsubscribed.Count > 0 ? $" / 退订 {host.Unsubscribed.Count}" : ""));
            foreach (var (name, handler) in host.XamlWired.OrderBy(x => x.Name, StringComparer.Ordinal))
                readings.Add($"    XAML      {name} → {handler}  [{host.TypeByName.GetValueOrDefault(name, "?")}]");
            foreach (var (name, file) in host.Subscribed)
                readings.Add($"    code      {name}.SelectionChanged += …  [{host.TypeByName.GetValueOrDefault(name, "?")}]"
                             + $"  ← {file}");
            foreach (var (name, method, required) in host.Injected)
                readings.Add($"    辅助函数  {name} ← {method}(… {required ?? "任意"} …)"
                             + $"  [{host.TypeByName.GetValueOrDefault(name, "?")}]");
            foreach (var (name, file) in host.Unsubscribed)
                readings.Add($"    （退订，不计入面） {name}.SelectionChanged -= …  ← {file}");
            foreach (var nameless in host.Nameless)
                readings.Add($"    （无名，本面够不到） {nameless}");

            if (unclassified.Length > 0)
                offenders.Add($"{hostName}: 这些具名非 Button 控件**接线了却没被分类**："
                              + string.Join(", ", unclassified)
                              + " —— 请在 HostButtonEffectUiTests 的表里给它们各加一条 SelMoves（声明处理器写了什么）"
                              + "或 SelInert（本层读不到，写清为什么）");

            if (phantom.Length > 0)
                offenders.Add($"{hostName}: 这些名字**被分类了却找不到接线**："
                              + string.Join(", ", phantom)
                              + " —— 要么控件被改名/删掉了（改表），要么它的 SelectionChanged 订阅被摘了"
                              + "（那是接线缺失，属缺陷现场，不是改表的事）");
        }

        JudgeReadings.Write(_output, "选择族静态面/面内", readings);

        Assert.True(totalDeclaredNames > 0, "声明面是空的 —— 判据会恒真");
        Assert.True(offenders.Count == 0,
            $"选择族静态面与运行期表不一致（面内 {totalFace} 个控件 / 声明 {totalDeclarations} 条"
            + $" / 按宿主去重 {totalDeclaredNames} 个控件）："
            + Environment.NewLine + string.Join(Environment.NewLine, offenders)
            + Environment.NewLine + Environment.NewLine + "读数："
            + Environment.NewLine + string.Join(Environment.NewLine, readings));
    }

    // ══════════════════════════════════════════════════════════════
    //  判据二：面外点名
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>Views/</c> 下**不在**二十三个宿主里的视图文件：凡有接线控件的，必须按「文件 + 个数」登记在
    /// <see cref="OutsideFaceCensus"/> 里；登记了却对不上（文件没了、个数变了）也红。
    ///
    /// <para>为什么不做成「也全部分类」：那是把本层的面扩到全仓 20 个视图，属另一件事
    /// （见专题文档的后续项）。但**不做**不等于**可以不知道** —— 这条判据把「面外还有多少」
    /// 变成一个**必须维护的账本**，而不是一句「我们只覆盖了 8 个宿主」。</para>
    /// </summary>
    [Fact]
    public void WiredSelectControls_OutsideTheHosts_MatchTheRegisteredCensus()
    {
        // ⚠ 宿主名用 `.` 分隔（那是**类型名**后缀），而本表的键是**相对 Views/ 的路径**（`/`）
        //   ⇒ 比较前统一成路径口径（阶段 131 加子目录宿主时撞上的第一处）。
        var hosts = HostButtonEffectUiTests.SelectDeclarations()
            .Select(x => x.Host.Replace('.', '/')).ToHashSet(StringComparer.Ordinal);
        Assert.True(hosts.Count > 0, "宿主清单是空的 —— 本判据会把整个 Views/ 都算成面外，恒红");

        var actual = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var readings = new List<string>();
        var namelessTotal = 0;

        foreach (var axaml in ViewAxamlFiles())
        {
            // ⚠ 键取「相对 Views/ 的路径」而不是文件名 —— 子目录里的视图与直系文件同名时，
            //   取文件名会把两格撞成一格（阶段 116 立这条键时撞过）。
            //   ⚠⚠ `Path.GetRelativePath` 返回的是**文件名带扩展名**（`AlarmListView.axaml`），
            //   所以必须再去掉 `.axaml` —— 漏这一步会让 16 条登记项**全部**对不上。
            var view = Path.GetRelativePath(ViewsDirectory, axaml).Replace('\\', '/');
            view = view[..^".axaml".Length];
            if (hosts.Contains(view)) continue;

            var (typeByName, wired, nameless) = ScanXaml(File.ReadAllText(axaml));
            // ⚠ 阶段 125：解析面从"兄弟 .axaml.cs"扩到"同一个 partial class 的全部 .cs"
            //   （PropertyPanelView 就是 11 个文件的那种写法）—— 实测本仓今天**不改变任何一格**，
            //   但"往 ViewName.别的.cs 里加一条接线"从此会红，而不是静默掉出面。
            var codes = CodeFilesFor(view, axaml).Select(File.ReadAllText).ToList();
            var subscribed = new List<string>();
            foreach (var code in codes)
                subscribed.AddRange(ScanDirectWiring(code, typeByName).Subscribed);
            var injected = ScanHelperInjectionAcross(codes, typeByName);

            var face = wired.Select(x => x.Name)
                .Concat(subscribed)
                .Concat(injected.Select(x => x.Name))
                .Where(n => !(typeByName.TryGetValue(n, out var t) && ButtonFamily.Contains(t)))
                .ToHashSet(StringComparer.Ordinal);

            namelessTotal += nameless.Count;
            if (face.Count > 0) actual[view] = face.Count;
            readings.Add($"{view}：接线控件 {face.Count}（XAML {wired.Count} / code {subscribed.Count}"
                         + $" / 注入 {injected.Count}；代码文件 {codes.Count} 个）"
                         + (face.Count > 0 ? "  →  " + string.Join(", ", face.OrderBy(x => x, StringComparer.Ordinal)) : ""));
        }

        readings.Add($"面外无名却接线的元素共 {namelessTotal} 个（本面够不到，只登记不判）");

        var registered = OutsideFaceCensus.ToDictionary(x => x.View, x => x.WiredSelects, StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var (view, count) in actual)
            if (!registered.TryGetValue(view, out var expected))
                offenders.Add($"{view}: 面外视图里出现了 {count} 个接线控件，但它**不在普查表里** —— "
                              + "要么把这条视图也建成宿主（更好），要么把它按「文件 + 个数」登记进 OutsideFaceCensus");
            else if (expected != count)
                offenders.Add($"{view}: 普查表记的是 {expected} 个，实际 {count} 个 —— "
                              + "别只改数字：先确认多/少的那一个是**什么**、它接的是哪条线");

        foreach (var view in registered.Keys)
            if (!actual.ContainsKey(view))
                offenders.Add($"{view}: 普查表里有它，但它在 Views/ 下要么不存在、要么已经一个接线控件都没有 —— "
                              + "陈账要清（文件被删/被改名，或最后一条 SelectionChanged 被摘了）");

        readings.Add($"面外共 {actual.Count} 个视图文件 / {actual.Values.Sum()} 个接线控件；"
                     + $"普查表 {OutsideFaceCensus.Length} 条 / {OutsideFaceCensus.Sum(x => x.WiredSelects)} 个");

        // 扫描面自陈：⚠ 这是**读数不是断言** —— 它的正确性由本判据的**双向闭合**兜着
        //   （少扫一个带接线的文件 ⇒ 那一格找不到登记项、或登记项找不到文件，两个方向都红）。
        //   `Views/Editors/VariablePicker` 正是被这条闭合抓出来的。
        var scanned = ViewAxamlFiles();
        var direct = scanned.Where(p => Path.GetDirectoryName(p) == ViewsDirectory).Count();
        readings.Add($"扫描面：Views/ 下**递归**枚举到 {scanned.Count} 个 .axaml"
                     + $"（直系 {direct} ＋ 子目录 {scanned.Count - direct}）");
        JudgeReadings.Write(_output, "选择族静态面/面外", readings);

        Assert.True(actual.Count > 0, "面外一个接线控件都没扫到 —— 扫描器坏了，本判据会恒真（去掉它）；"
                                      + "或者面真的扩到全仓了（那就把本判据删掉，别让它假绿）");
        Assert.True(offenders.Count == 0,
            $"面外普查表与实际不一致：{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
            + Environment.NewLine + Environment.NewLine + "读数："
            + Environment.NewLine + string.Join(Environment.NewLine, readings));
    }

    // ══════════════════════════════════════════════════════════════
    //  判据三：扫描器自检（合成语料，两侧都断言）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 合成一份**同时含三种接线形态 + 四种不该进面的东西**的语料，两面都断言。
    ///
    /// <para>没有这条，扫描器悄悄退化成「只认 XAML 属性」也照样全绿 —— 而那时
    /// <c>ProjectSettingsView</c> 那 14 个辅助函数注入的下拉会掉出面（如果表里也没登记它们的话）。
    /// 本仓的注释里**正在**讨论 <c>SelectionChanged</c>（本文件与
    /// <c>HostButtonEffectUiTests</c> 都是），所以「剥注释」也必须被这条自检钉住
    /// —— 否则散文里的 <c>x:Name="GhostFromProse"</c> 会被算成一个真控件。</para>
    /// </summary>
    [Fact]
    public void TheScanner_SeesAllThreeWiringForms_AndOnlyThose()
    {
        const string xaml = """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <!-- 剥注释自检：下面这个"控件"只存在于散文里，不许进面
                   <ListBox x:Name="GhostFromProse" SelectionChanged="OnGhost"/> -->
              <StackPanel>
                <ListBox x:Name="ListA" SelectionChanged="OnListA"/>
                <ComboBox x:Name="ComboA"/>
                <ComboBox x:Name="ComboB"/>
                <NumericUpDown x:Name="NumA"/>
                <CheckBox x:Name="ChkB" SelectionChanged="OnChkB"/>
                <ListBox SelectionChanged="OnNameless"/>
              </StackPanel>
            </Window>
            """;

        const string code = """
            private void Wire()
            {
                ListA.SelectionChanged += OnListA;      // 直接接线 ⇒ 进面
                ComboA.SelectionChanged -= OnComboA;    // 退订 ⇒ 不进面
                AttachChangeHandler(NumA);              // 类型不匹配 ⇒ 不进面
                AttachChangeHandler(ComboB);            // 辅助函数注入 ⇒ 进面
                AttachChangeHandler(ChkB);              // CheckBox 走的是别的分支 ⇒ 不进面
            }

            private void AttachChangeHandler<T>(T ctl) where T : Control
            {
                if (ctl is NumericUpDown nud)
                    nud.ValueChanged += (_, _) => MarkDirty();
                else if (ctl is TextBox txt)
                    txt.TextChanged += (_, _) => MarkDirty();
                else if (ctl is CheckBox chk)
                    chk.IsCheckedChanged += (_, _) => MarkDirty();
                else if (ctl is ComboBox cmb)
                    cmb.SelectionChanged += (_, _) => MarkDirty();
            }
            """;

        var (typeByName, wired, nameless) = ScanXaml(xaml);
        var (subscribed, unsubscribed) = ScanDirectWiring(code, typeByName);
        var carriers = FindHelperCarriers(code);
        var injected = ScanHelperInjection(code, typeByName);

        // ── ① 三种形态都认得出来 ──
        Assert.Equal(new[] { "ChkB", "ListA" }, wired.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "ListA" }, subscribed);
        Assert.Equal(new[] { "ComboA" }, unsubscribed);
        Assert.Equal(new[] { "ComboB" }, injected.Select(x => x.Name).ToArray());

        // ── ② 载体解析拿到了「要求的类型」（这条是那 35 条假缺口的疫苗）──
        var carrier = Assert.Single(carriers);
        Assert.Equal("AttachChangeHandler", carrier.Method);
        Assert.Equal("ctl", carrier.Parameter);
        Assert.Equal(0, carrier.ParameterIndex);
        Assert.Equal("ComboBox", carrier.RequiredType);

        // ── ③ 四种不该进面的东西，一个都没进去 ──
        Assert.DoesNotContain("GhostFromProse", typeByName.Keys);   // 注释里的散文
        Assert.DoesNotContain("ComboA", FaceNames());               // 只退订
        Assert.DoesNotContain("NumA", FaceNames());                 // 注入但类型不匹配
        Assert.DoesNotContain("ChkB", FaceNames());                 // Button 族
        Assert.Equal(new[] { "ListBox（handler OnNameless）" }, nameless);  // 无名：数得出来，但不进面

        // ── ④ 合起来，非 Button 的面**恰好**是这两个 ──
        Assert.Equal(new[] { "ComboB", "ListA" }, FaceNames());

        JudgeReadings.Write(_output, "选择族静态面/自检", new[]
        {
            "三种形态：XAML 属性（ListA / ChkB）、code-behind +=（ListA）、辅助函数注入（ComboB）",
            "四种不进面：注释散文（GhostFromProse）、只退订（ComboA）、注入但类型不匹配（NumA）、Button 族（ChkB）",
            "载体解析：AttachChangeHandler(ctl[0]: ComboBox) —— 类型这一维是 35 条假缺口的疫苗",
            "无名却接线：数得出来（ListBox handler OnNameless），进读数不进断言",
        });

        List<string> FaceNames()
        {
            var face = wired.Select(x => x.Name).Concat(subscribed).Concat(injected.Select(x => x.Name))
                .Where(n => !(typeByName.TryGetValue(n, out var t) && ButtonFamily.Contains(t)))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            return face;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  判据四：辅助函数识别在**真语料**上活着
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 判据三用的是合成语料，只能证明「逻辑对」。这一条钉的是「它在这个仓库里**真的认出了东西**」。
    ///
    /// <para>少了它会有一个静默退化口：辅助函数识别一旦对真实写法失效
    /// （正则跟不上换行、模式匹配换了写法、那个辅助函数被拆成两个方法…），
    /// <c>ProjectSettingsView</c> 那 14 个下拉就**静默掉出面** —— 而如果那时表里也恰好没登记它们，
    /// 判据一的两个方向**都是空的**、照样全绿。这条断言把「机制活着」变成显式契约：
    /// 它点名要求认出 <c>AttachChangeHandler</c> 这个**具体**载体。</para>
    ///
    /// <para>⚠ 它**刻意不冻结个数**（实测 15 个 <c>ComboBox</c> 实参）—— 那种数字写进断言，
    /// 下一个人就会为改数字而改测试。它只要求「认出来了，且至少一个」。</para>
    /// </summary>
    [Fact]
    public void TheHelperResolution_IsAliveOnTheRealCorpus()
    {
        var code = File.ReadAllText(Path.Combine(ViewsDirectory, "ProjectSettingsView.axaml.cs"));
        var carriers = FindHelperCarriers(code);

        var readings = carriers
            .Select(c => $"{c.Method}(参数 {c.ParameterIndex} 「{c.Parameter}」，要求 {c.RequiredType ?? "任意"})")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        JudgeReadings.Write(_output, "选择族静态面/载体解析", readings);

        Assert.Contains(carriers, c => c.Method == "AttachChangeHandler" && c.RequiredType == "ComboBox");

        // 再确认它真能落到实参上（而不只是"解析出了载体"）：至少要有一个注入点被认出来。
        var (typeByName, _, _) = ScanXaml(File.ReadAllText(Path.Combine(ViewsDirectory, "ProjectSettingsView.axaml")));
        var injected = ScanHelperInjection(code, typeByName);

        JudgeReadings.Write(_output, "选择族静态面/注入点", injected
            .Select(x => $"{x.Name} ← {x.Method}（{x.RequiredType}）")
            .ToList());

        Assert.True(injected.Count > 0,
            "ProjectSettingsView 里一个辅助函数注入点都没认出来 —— 那 14 个『只标脏』的下拉掉出了静态面。"
            + "本层与静态面的边界因此悄悄变小，而判据一两个方向都是空的、照样绿");
    }

    // ══════════════════════════════════════════════════════════════
    //  判据四之二：**跨文件**解析活着（阶段 125）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 合成语料：**载体在文件 A、调用点在文件 B**。这是「跨文件注入」那条盲区的**最小复现**：
    /// 单文件口径下两侧各自都是空的，<see cref="ScanHelperInjectionAcross"/> 必须认出来。
    ///
    /// <para>为什么非要单独一条判据：<c>MainWindow</c> 今天的面内接线**只有 1 条**，
    /// 而那条是**直接 <c>+=</c>**、不是辅助函数注入 ⇒
    /// <see cref="EveryAxamlInTheAppProject_IsInsideTheDeclaredScanFace"/> 只钉住了"直接订阅"
    /// 那一半。跨文件的**注入**写法在本仓真语料上今天一条都没有 —— 于是它只能靠合成语料钉
    /// （与判据三之于判据四同形：一个证"逻辑对"、一个证"真语料上活着"）。</para>
    ///
    /// <para>两侧都断言：分居两文件**必须认出来**；实参不在 <c>x:Name</c> 表里、或类型不匹配的
    /// **不许**混进来（否则"多认"会把面撑大，判据一立刻一片假红）。</para>
    /// </summary>
    [Fact]
    public void TheCrossFileResolution_SeesCarrierAndCallSiteInDifferentFiles()
    {
        const string axaml = """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel>
                <ComboBox x:Name="ComboThere"/>
                <NumericUpDown x:Name="NumThere"/>
              </StackPanel>
            </Window>
            """;

        // 文件 A：只有**载体**（定义），没有调用点。
        const string carrierFile = """
            private void AttachChangeHandler<T>(T ctl) where T : Control
            {
                if (ctl is NumericUpDown nud)
                    nud.ValueChanged += (_, _) => MarkDirty();
                else if (ctl is ComboBox cmb)
                    cmb.SelectionChanged += (_, _) => MarkDirty();
            }
            """;

        // 文件 B：只有**调用点**，没有定义 —— 一个 ComboBox（该进面）＋ 一个 NumericUpDown（类型不匹配）。
        const string callSiteFile = """
            private void WireAll()
            {
                AttachChangeHandler(ComboThere);
                AttachChangeHandler(NumThere);
            }
            """;

        var (typeByName, _, _) = ScanXaml(axaml);

        var sameFile = ScanHelperInjection(carrierFile, typeByName);
        var across = ScanHelperInjectionAcross(new[] { carrierFile, callSiteFile }, typeByName);

        JudgeReadings.Write(_output, "选择族静态面/跨文件注入", new[]
        {
            $"单文件口径（只在定义文件里找调用点）：{sameFile.Count} 条 —— 应为 0（调用点在另一个文件）",
            "跨文件口径：" + string.Join(", ", across
                .Select(x => $"{x.Name} ← {x.Method}（{x.RequiredType}）")),
        });

        // ① 单文件口径**看不见**它（这正是那条盲区的最小复现；删掉这条，本判据就退化成恒真）
        Assert.Empty(sameFile);

        // ② 跨文件口径必须认出来，且**只**认出类型相容的那一个
        var hit = Assert.Single(across);
        Assert.Equal("ComboThere", hit.Name);
        Assert.Equal("AttachChangeHandler", hit.Method);
        Assert.Equal("ComboBox", hit.RequiredType);

        // ③ 同一条判据里的负控：把实参换成不在 x:Name 表里的名字 ⇒ 必须为空
        var renamed = callSiteFile.Replace("ComboThere", "SomeLocal", StringComparison.Ordinal)
                                  .Replace("NumThere", "AnotherLocal", StringComparison.Ordinal);
        Assert.Empty(ScanHelperInjectionAcross(new[] { carrierFile, renamed }, typeByName));
    }

    // ══════════════════════════════════════════════════════════════
    //  判据五：扫描面自陈（App 项目里每个 .axaml 都要被两张表之一认领）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// App 项目里、<c>Views/</c> 之外的 <c>.axaml</c> —— **逐文件登记**，理由必填，
    /// 且阶段 125 起**必须声称个数**（此前 <c>MainWindow</c> 那一格写的是"不声称个数"）。
    ///
    /// <para>⚠ 路径是**相对仓库根**的（<see cref="SourceScanner.Relative"/> 的口径），
    /// 与 <see cref="OutsideFaceCensus"/> 的「相对 <c>Views/</c>」不是同一个口径 ——
    /// 两张表各自局部自洽，**混用会撞**。</para>
    ///
    /// <para>为什么需要第三张表：判据二只扫 <c>Views/</c>，于是「把新窗口放到
    /// <c>src/HmiEditor.App/</c> 根目录」这件事**两处都不红** —— 扫描面静默少了一块，
    /// 而账本一个字都没提。</para>
    ///
    /// <para>🩸 为什么阶段 125 把「不声称个数」改成个数：那一格当时的理由是"跨文件注入 ⇒
    /// 本守卫原理上看不见、连有几个都数不出来"。**前半句是错的**（见类注释那条：把"同文件"
    /// 换成"同一个 partial class 的全部文件"就看得见），而后半句正是**那句话的后果** ——
    /// 一个"数不出来"的格子，谁往里加第二条接线都不会红。实测那格今天的真值是
    /// <c>MainWindow</c> = **1**（<c>CboResolutionPreview</c>，订在 <c>Wiring.Commands.cs:79</c>）。</para>
    /// </summary>
    private static readonly (string File, int WiredSelects, string Why)[] OutsideViewsAxaml =
    {
        ("src/HmiEditor.App/App.axaml", 0,
            "应用级资源与主题挂载；无具名控件、无接线"),
        ("src/HmiEditor.App/Assets/Icons.axaml", 0,
            "图标几何资源字典；无具名控件、无接线"),
        ("src/HmiEditor.App/MainWindow.axaml", 1,
            "主窗口壳（143 个具名控件）。**唯一**的面内接线是 CboResolutionPreview（ComboBox），"
            + "订阅订在 Wiring.Commands.cs:79 —— 它是本守卫此前唯一够不到的**跨文件**站点；"
            + "阶段 125 把解析面按「声明了 partial class MainWindow 的全部 8 个 .cs」展开后就看得见了。"
            + "⚠ 不进判据一/五层：那是**运行期面**的事（要驱动主窗口的按钮），本条只做**静态账本**"),
    };

    /// <summary>
    /// <c>src/HmiEditor.App</c> 下**全部** <c>.axaml</c>（递归、去 <c>bin/obj</c>）必须恰好被两张表
    /// 认领：<c>Views/</c> 下的归判据二那张普查表，<c>Views/</c> 之外的归
    /// <see cref="OutsideViewsAxaml"/>。**双向**都断言。
    ///
    /// <para>这条判据不给「面外还有多少控件」定数（那是判据二的读数），它只回答一个此前
    /// **无人回答**的问题：<b>扫描面到底覆盖了哪些文件</b>。有了它，「新加一个 <c>.axaml</c>」
    /// 无论放在 <c>Views/</c> 里面还是外面，都会有人逼你表态。</para>
    ///
    /// <para>🩸 <b>阶段 125 起它还管个数</b>（此前 <c>MainWindow</c> 那格写的是"不声称个数"）。
    /// 一个"数不出来"的格子等于**免证**：谁往主窗口里再加一条接线都不会红。现在两侧都断言 ——
    /// 多一条（接线数 +1）与少一条（陈账）各自报红，`Why` 那一栏也从"解释为什么数不出来"
    /// 变成"为什么它在本层只做静态账本"。</para>
    ///
    /// <para>⚠ 它**不**断言 <c>Views/</c> 下那些「无接线」的 <c>.axaml</c>：本面的边界就是
    /// 「有接线的具名非 Button 控件」，一个没有任何 <c>SelectionChanged</c> 接线的视图
    /// 与本面**无关**，逼它登记只是噪音。（读数列会把它们点出来，但不进断言。）</para>
    /// </summary>
    [Fact]
    public void EveryAxamlInTheAppProject_IsInsideTheDeclaredScanFace()
    {
        var all = Directory.EnumerateFiles(AppProjectDirectory, "*.axaml", SearchOption.AllDirectories)
            .Where(p => !SourceScanner.IsBuildOutput(p))
            .Select(SourceScanner.Relative)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        Assert.True(all.Count > 0, "App 项目里一个 .axaml 都没扫到 —— 路径口径坏了，本判据会恒假绿");

        var viewsPrefix = SourceScanner.Relative(ViewsDirectory) + "/";
        var inViews = all.Where(p => p.StartsWith(viewsPrefix, StringComparison.Ordinal)).ToList();
        var outside = all.Where(p => !p.StartsWith(viewsPrefix, StringComparison.Ordinal)).ToList();
        Assert.True(inViews.Count > 0,
            $"Views/ 前缀下没扫到 .axaml（前缀「{viewsPrefix}」）—— 路径口径变了，本判据会恒假绿");

        var registered = OutsideViewsAxaml.ToDictionary(x => x.File, x => x.WiredSelects, StringComparer.Ordinal);

        // 面外每一个 .axaml 的**实测**面（与判据二同一套机制：XAML 属性 + 全部 partial 文件的
        // code-behind += + 跨文件辅助函数注入，扣掉 Button 族）。
        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        var readings = new List<string>();
        foreach (var f in outside)
        {
            var (face, _, nameless) = FaceOfAxaml(Path.Combine(SourceScanner.RepoRoot, f));
            measured[f] = face.Count;
            readings.Add($"  【扫描面外·已登记】{f}：面 {face.Count}"
                         + (face.Count > 0 ? "  →  " + string.Join(", ", face.OrderBy(x => x, StringComparer.Ordinal)) : "")
                         + (nameless.Count > 0 ? $"（另有无名却接线 {nameless.Count} 个，本面够不到）" : ""));
        }

        var offenders = new List<string>();
        foreach (var f in outside)
            if (!registered.TryGetValue(f, out var expected))
                offenders.Add($"{f}: Views/ 之外出现了新的 .axaml，但它不在 OutsideViewsAxaml 里 —— "
                              + "要么把它挪进 Views/（并想清楚它该不该进面），要么逐文件登记理由**与个数**");
            else if (expected != measured[f])
                offenders.Add($"{f}: OutsideViewsAxaml 记的是 {expected} 个接线控件，实测 {measured[f]} 个 —— "
                              + "别只改数字：先确认多/少的那一个是**什么**、它接的是哪条线");
        foreach (var f in registered.Keys)
            if (!outside.Contains(f, StringComparer.Ordinal))
                offenders.Add($"{f}: OutsideViewsAxaml 里有它，但它在 src/HmiEditor.App 下要么不存在、"
                              + "要么已经被挪进 Views/、**要么它已经升格成宿主**（那就不该留在本表里 —— "
                              + "升格时删掉这一行是这次改动的一部分，负控 NC-5 实测过这条腿）"
                              + " —— 陈账要清（文件被删 / 改名 / 搬家 / 升格）");

        readings.Insert(0, $"App 项目共 {all.Count} 个 .axaml：Views/ 递归下 {inViews.Count} 个（扫描面）"
                           + $" ＋ Views/ 之外 {outside.Count} 个（逐文件登记）"
                           + $"；登记表 {OutsideViewsAxaml.Length} 条 / {OutsideViewsAxaml.Sum(x => x.WiredSelects)} 个接线控件"
                           + $"，实测 {measured.Values.Sum()} 个");
        JudgeReadings.Write(_output, "选择族静态面/扫描面自陈", readings);

        Assert.True(offenders.Count == 0,
            $"App 项目的 .axaml 与「扫描面自陈」不一致：{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders)
            + Environment.NewLine + Environment.NewLine + "读数："
            + Environment.NewLine + string.Join(Environment.NewLine, readings));
    }
}
