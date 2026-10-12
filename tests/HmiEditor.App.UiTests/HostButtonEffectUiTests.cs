using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Resources;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 宿主内部按钮的**可观测效果**判据 —— 登记表 §4 #39 的「事件接线」那一半。
///
/// <para><b>为什么要有第五层：</b>既有四层动作面判据
/// （<c>MainWindowMenuInvocationUiTests</c> / <c>MainWindowToolbarInvocationUiTests</c> /
/// <c>DialogInnerInvocationUiTests</c> / <c>DockPanelInvocationUiTests</c>）判的都是
/// **弹窗的层数与类型**。那对「点下去会开一个什么窗」的按钮够用，但对**面板里绝大多数
/// 不弹窗的按钮是零信息**：改数据、刷列表、写状态栏都发生在内存里，四层看不见它们 ——
/// 而且那四份文件的「不抓什么」里都**明确声明**了不判副作用。</para>
///
/// <para>后果正是登记 #39 记的那件事：一个按钮的 <c>Click +=</c> 被删掉、或者接到了**另一个**
/// 处理器上，四层判据**全绿**。本层补的就是这一刀。</para>
///
/// <para><b>判据形状（三层）：</b></para>
/// <list type="number">
///   <item><b>可观测效果</b> —— 每个宿主声明一组 <see cref="Probe"/>，分量从**两处**读：
///     <b>模型</b>（<c>depot</c> 里的集合计数）证明「数据真的变了」，<b>窗口控件</b>
///     （<c>FindControl</c> 到的 <see cref="ListBox"/>.Items.Count）证明「界面真的刷新了」。
///     逐条按钮声明点击后**哪些分量变成什么值**。断言是**双向**的：
///     <list type="bullet">
///       <item>声明的分量必须**等于**声明值（不是「变了就行」—— 见下）；</item>
///       <item>**没声明的数据面分量必须一字不变** —— 把「副作用外溢」（点一个按钮顺带改了
///         别的东西）也变成红。</item>
///     </list></item>
///   <item><b>面双向差集</b> —— 宿主里**每一个具名 <see cref="Button"/>** 必须在表里被分类
///     （<c>Moves</c> / <c>Inert</c> / <c>Never</c>）：少一个（控件被删/改名）与多一个
///     （新增按钮没人分类）都红。新按钮于是会被**点名**。注意 Avalonia 的
///     <see cref="Button"/> 面**包含** <see cref="CheckBox"/>（<c>CheckBox : ToggleButton : Button</c>），
///     这一点与 <c>DialogInnerInvocationUiTests</c> 同口径。</item>
///   <item><b>表卫生</b> —— 见 <see cref="BucketTables_AreNotEmptiedAndAreSelfConsistent"/>。</item>
/// </list>
///
/// <para><b>为什么 <c>Moves</c> 一定要声明「变成了什么」而不是「变了」：</b>「变了」太弱 ——
/// 把 <c>Click += AddGroup</c> 接成 <c>DeleteGroup</c>，读数照样变，「变了」照样绿。
/// 声明**目标值**才能把「接错处理器」点出来（这也是本层的核心负控 NC2）。</para>
///
/// <para><b>为什么另有一条「声明值恰好等于点击前的值」的自检：</b>防止有人把 <c>Moves</c>
/// 写成一份「当时读到的值」抄本 —— 那种声明恒真、等于没写。声明里至少要有一条真的动了。</para>
///
/// <para><b>「点一下」怎么模拟（<see cref="CheckBox"/> 是这里的关键）：</b>
/// 对普通 <see cref="Button"/> 就是 <c>RaiseEvent(Button.ClickEvent)</c>。对
/// <see cref="ToggleButton"/>（<see cref="CheckBox"/> 的基类）**先翻转
/// <c>IsChecked</c>、再引发 Click** —— 因为框架里 <c>ToggleButton.OnClick</c> 干的正是这两件事
/// （翻转 → 引发 <c>IsCheckedChanged</c> → 引发 <c>Click</c>）。只做后者会得到**假阴性**：
/// <c>IsCheckedChanged="OnXxx"</c> 这种接线一次都跑不到（<c>ScreenManagerView.ChkPublic</c> 就是
/// 这一类，XAML 里**没有** <c>Click=</c>）。</para>
///
/// <para><b>点击顺序是显式声明的量，不是字母序。</b>大面板的按钮**互相依赖**：
/// <c>VariableManagerView.BtnAddAlarm</c> 的前提是「上一步刚选中了一个变量」，
/// 而选中是谁造成的，只有按顺序点过才知道。表里的数组顺序**就是**点击顺序，
/// 改动它等于改动被测语义。</para>
///
/// <para><b>布景（<see cref="ClickSpec.Arrange"/> / <see cref="Arranged"/>）：</b>
/// 有一类处理器的**第一行**就读输入框或看选中项 ——「名字为空 ⇒ 只写状态栏并 return」。
/// 没有布景这层机制时，这类按钮只能登记成 <c>Inert</c>，而那条 <c>Inert</c> 记的是**夹具形状**
/// （「没人往框里填过东西」），不是接线 ⇒ 这一层对它们零信息。
/// 布景让声明可以说「**先**填好名字、**再**点它，于是变量数 +1」，把接线真的验到了。
///
/// <para>⚠ 布景同时是一条**新的作弊面**：把「点击的效果」搬进布景里预先做掉，本层就再也看不见它。
/// 因此有一条自检盯着它：跑的时候比对布景前后的全分量读数，**布景只许动输入面分量**
/// （<see cref="Probe.IsInputFace"/>），动了别的当场报红。这条自检本身有负控（把布景改成直接
/// 往 <c>Variables</c> 里塞一条 ⇒ 必须红）。</para></para>
///
/// <para><b>面的第二条腿：选择族（阶段 115）。</b>本判据原先的面只有 <see cref="Button"/> 族 ——
/// 于是挂在 <c>ComboBox</c> / <c>ListBox</c> / <c>TreeView</c> 上的 <c>SelectionChanged</c> 处理器
/// **原理上不在面内**。代价在两处露出来：① 登记 #65（<c>TrendConfigView</c> 那 6 个处理器一个订阅都没有）
/// 只能靠人读 XAML 发现；② 登记 #67（<c>OnTimeWindowChanged</c> 缺抑噪守卫）没有任何判据盯着。
/// 阶段 115 把面扩到**「具名 且 订阅了 <c>SelectionChanged</c>」的非 Button 控件**。</para>
///
/// <para>⚠ <b>两半的责任是分开的，别把它们混成一条：</b>
/// <list type="bullet">
///   <item><b>运行期</b>（本文件）管两件事：「**声明了的**名字真的存在」＋「拨一下它，声明的分量真的变了」。
///     后一件其实就等价于「订阅在不在」—— 但**只在那个分量由处理器写的前提下**成立
///     （见 <see cref="SelMoves"/> 那条 🩸）。</item>
///   <item><b>「有没有接线」本身运行期看不见</b>（Avalonia 不暴露订阅者名单），所以
///     「宿主里每个带 <c>SelectionChanged</c> 接线的具名非 Button 控件都必须被分类」这一条只能由
///     **静态**判据守，见 <c>SelectionFaceClassificationGuardTests</c>。
///     ⚠ 那条守卫得认出**辅助函数注入** —— <c>ProjectSettingsView.AttachChangeHandler(CmbColorDepth)</c>
///     把控件当参数传进去订阅，名字扫描看不见它（**15 个** <c>ComboBox</c> 是这么接的）。
///     🩸 而那次调用还有 **35 个别的实参**（10 个 <c>CheckBox</c> + 16 个数字框 + 9 个文本框），
///     它们订阅的是 <c>ValueChanged</c> / <c>TextChanged</c> / <c>IsCheckedChanged</c> ——
///     所以识别这个辅助函数**必须带类型**：不带类型会把其中 **25 个**（数字框 + 文本框；
///     复选框属 <see cref="Button"/> 族、被本面排除）算成假缺口，
///     而假缺口比缺口更坏（它逼人把假的东西也登记一遍）。<b>25 是负控 nc33 实测点名的数</b>。
///     <para>它判两件事：① **面内双向闭合** —— 每个宿主里「接线了却没分类」（新控件静默滑进面）
///     与「分类了却找不到接线」（声明腐烂、或订阅被摘）**都红**；② **面外点名** ——
///     `Views/` 下另 **1** 个非宿主视图里还有 **31** 个接线控件，逐个按「文件 + 个数」登记理由，
///     新冒出一个带接线的视图文件同样会红。面内 **23 宿主 / 82 控件**、面外 **1 视图 / 31 控件**
///     就是**本层与静态面的边界**，它是自陈的、不是被证过的。
///     ⚠ 上面这几个数是**散文读数**（阶段 249 从 `HMI_READINGS_OUT` 的 `选择族静态面/面内`＋`/面外`
///     两份读数抄来），**没有判据看着它们** —— 改面时记得重数；旧版曾写「十七个宿主 / 8 个视图 / 48 个控件」，
///     那三个数一路没跟着改，正是这一档会腐烂的证据。</para></item>
/// </list></para>
///
/// <para><b>手势下标有两个档（阶段 115 的第二半加的第二档）：</b>
/// <list type="bullet">
///   <item><c>n ≥ 0</c> —— 拨到第 n 项（与按钮族同形，<c>ListBox</c> / <c>ComboBox</c> 走
///     <c>SelectedIndex</c>、<c>TreeView</c> 走**根节点**下标）；</item>
///   <item><c>-1</c> —— **清空选中**（<c>SelectedItem = null</c>）。</item>
/// </list>
/// 为什么非要第二档：宿主里有控件在整条序列里**只有「选中 → 清空」那一支是真能观测的** ——
/// <c>ProjectSettingsView.RuntimeUserList</c> 全程只有一条条目、拨同一下标不引发事件；
/// <c>CommunicationConfigView.CommTree</c> 的选中只能靠根节点下标驱动（子节点进不去），
/// 而「什么都没选 ⇒ <c>ShowEmptyState</c>」正是它两条腿之一。
/// <para>⚠ <b>诚实边界</b>：<c>-1</c> **不是**鼠标手势 —— 单选 <c>ListBox</c> 上点已选项不会取消选中。
/// 它是**程序性**清空，而这条路径在生产里真的走得到（删掉最后一条 ⇒ <c>SelectRuntimeUser(null)</c>；
/// 删掉最后一个连接 ⇒ <c>ShowEmptyState</c>），所以处理器里那一支是活代码、值得判。
/// 与「不判真实命中测试」同一性质边界：驱动的是**状态迁移**，不是**像素上的动作**。</para></para>
///
/// <para><b>不抓什么（诚实边界）：</b>
/// <list type="number">
///   <item><b>不判真实命中测试</b> —— 控件「可不可达」（被遮住、被禁用）不在面内。</item>
///   <item><b>不判跨层副作用</b> —— 写盘 / 起进程 / 走 <c>IStorageProvider</c> 的按钮不点
///     （headless 的文件选择器是平台桩，观测到的是桩而不是接线）。⚠ 这一档还有**第三个**成员：
///     「完成时刻不由 <c>Pump</c> 决定」的 <c>async void</c> + <c>Task.Run</c> 站点
///     （<c>CommunicationConfigView.BtnTestConnection</c> 是第一个）—— 它不是跨层，而是**读数会抖**：
///     点击后 <c>Read</c> 到的是哪个瞬间没有因果保证，坏了本层的可复现性前提。</item>
///   <item><b><c>Voice</c> 分量只进读数、不进断言</b> —— 状态栏文案是本地化文案的影子，
///     把它做成断言等于把文案冻结（一改文案全表红，而那条红不带任何接线信息）。
///     它在读数里以 <c>空/非空</c> 露出，用来核对「处理器到底跑了没有」。</item>
///   <item><b>不断言「界面看起来对」</b> —— 本层判的是**状态迁移**，不是渲染结果。</item>
///   <item><b>本层不声称「这条接线是对的」</b>，只声称「行为与声明一致」。
///     表里 <c>Inert</c> 记的是**事实**、不是期望：若某条 <c>Inert</c> 其实是缺陷现场，
///     缺陷修好之后它就会**变红**，逼人来改声明 —— 这条机制已经真的触发过一次
///     （阶段 108 修好登记 #64 时，4 条 <c>Inert</c> 当场变红并按预案转成 <c>Moves</c>）。
///     ⚠ 反过来**不成立**：<c>Inert</c> 的 <c>Why</c> 是**免证**的 —— 写错了没有任何判据会响
///     （阶段 107 就有 3 条把成因错记成 #64，其实现场是**点击顺序**）。写 <c>Why</c> 时请真的把处理器读一遍。</item>
///   <item><b><c>IsInputFace</c> 是**声明**，没有判据验证它标得对不对</b> —— 把一条**数据面**分量
///     标成输入面，就能让它合法地被布景改动（自检于是放行）。这条盲区是**自陈**的，不是被证过的：
///     判据只能证明「布景没动**未声明为输入面**的分量」，不能证明「输入面的划分是合理的」。
///     换句话说法：<see cref="Probe.IsInputFace"/> 的**取值表**要人读，就像 <c>Inert</c> 的 <c>Why</c> 一样是**免证**的。</item>
/// </list></para>
/// </summary>
[Collection("Language-global")]
public class HostButtonEffectUiTests
{
    private readonly ITestOutputHelper _out;

    public HostButtonEffectUiTests(ITestOutputHelper o) => _out = o;

    // ──────────────────────────────────────────────
    //  表的结构
    // ──────────────────────────────────────────────

    /// <summary>一次探测的现场：宿主窗口 + 它的后端数据（同一个 <see cref="WindowDepot"/> 实例）。</summary>
    /// <summary>一次探测的现场：宿主窗口 + 它的后端数据（同一个 <see cref="WindowDepot"/> 实例）。</summary>
    private sealed record HostContext(Window Window, WindowDepot Depot)
    {
        /// <summary>
        /// **事件观测探针的载荷袋**（阶段 128；**一趟一个**）。
        ///
        /// <para>为什么需要它：面外那批视图里有几个的选择处理器**只 `Invoke` 一个公开事件**、
        /// 面板自己不写任何可读状态（阶段 127 的 A/B/C 分档里那一档）。判它们只能由**本层订阅
        /// 那个事件**，而那需要一个"每个宿主一趟"的存储位。</para>
        ///
        /// <para>⚠ 挂在 <see cref="HostContext"/> 上、**不挂静态字段**：探针表是**静态的**，
        /// 一份静态可变状态会被下一趟读到上一趟的残留（这层的每一趟都新建
        /// <see cref="WindowDepot"/>，同一个道理）。</para>
        /// </summary>
        public Dictionary<string, string> Observed { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// 一个**可观测分量**。<paramref name="IsVoice"/> 为 true 的分量只进读数、不进断言
    /// （见类注释「不抓什么」第 3 条）。
    ///
    /// <para><paramref name="IsInputFace"/> 为 true 的分量属于**输入面**（用户填的框、用户选的项）——
    /// 只有输入面分量允许被「布景」（<see cref="ClickSpec.Arrange"/>）改动。
    /// 默认 <c>false</c> ⇒ 布景**不许**动它，动了就报红。</para>
    /// </summary>
    private sealed record Probe(string Label, Func<HostContext, string> Read,
                                bool IsVoice = false, bool IsInputFace = false);

    private enum Bucket
    {
        /// <summary>本状态下点下去**会**改这些分量。必须至少声明一条，且至少一条真的动了。</summary>
        Moves,

        /// <summary>
        /// 本状态下点下去**数据面一字不变**。四种成色，别混着写：
        /// <list type="number">
        ///   <item><b>未被观测</b> —— 处理器真跑了，但它改的东西本层的两个出口都看不见；</item>
        ///   <item><b>提前 <c>return</c></b> —— 处理器的第一个分支就把它挡掉了（夹具缺前置状态）；</item>
        ///   <item><b>写入同值</b> —— 循环真跑了，但写的**就是原来的那个数**；</item>
        ///   <item><b>事件从未订阅</b> —— 处理器写在代码里、却**没有任何地方订阅它**
        ///     （<c>UserManagerView</c> 的 9 枚权限复选框就是这一类）。⚠ 这一档与前三种**性质不同**：
        ///     前三种是「这一跑看不见」，这一档是**接线本身缺失**、是缺陷现场 ——
        ///     补上订阅之后它当场变红，逼人来改声明。
        ///     <para>⚠⚠ **但这句话有个前提**：探针得**真读得到**那个处理器写的字段。否则
        ///     「接线了」与「没接线」在读数上完全同形，判据原理上看不见区别 ——
        ///     即「面的边界 ≠ 没有缺口」在**这一档上**的体现。<c>TrendConfigView</c> 为此专门补了
        ///     6 个显示位探针；**先补探针、再补订阅**，顺序反了这条 <c>Inert</c> 就等于零信息。</para>
        ///     <para>🩸 **本档已有两次「修好 ⇒ 变红」的实战**：阶段 108（登记 #64）4 条 <c>Inert</c> 转
        ///     <c>Moves</c>；阶段 114（登记 #65）6 条转 <c>Moves</c> —— <b>后者还多带出一条</b>：光补订阅会引入
        ///     <b>回归</b>（反向同步没抑噪 ⇒ 打开对话框就把第一条趋势的网格 / X 轴 / Y 轴静默改成「关」），
        ///     判据首跑当场点出来 ⇒ 本档盯的不只是「缺不缺订阅」，还有「补上订阅之后有没有把别的写坏」。</para></item>
        /// </list></summary>
        Inert,

        /// <summary>不点：理由必填（点了会关窗 / 走了平台桩 / 完成时刻不由 <c>Pump</c> 决定）。</summary>
        Never,
    }

    /// <summary>
    /// 一条按钮。<paramref name="Expect"/> 里的 <c>(分量名, 期望值)</c> 必须能在宿主的 Probe 里找到。
    /// <see cref="Arrange"/> 是可选的**布景**（点击前先填输入框 / 先装选中项），用 <see cref="Arranged"/> 挂上。
    /// </summary>
    private sealed record ClickSpec(string Name, Bucket Bucket, (string Label, string Value)[] Expect, string Why)
    {
        /// <summary>点击**之前**的布景。⚠ 只许动输入面分量（<see cref="Probe.IsInputFace"/>），跑的时候有自检盯着。</summary>
        public Action<HostContext>? Arrange { get; init; }

        /// <summary>
        /// **选择族**的手势：把该控件的选中项拨到这个下标（<c>-1</c> = **清空选中**）。
        /// 非 <c>null</c> ⇒ 本条不走「点击」，而走 <see cref="Gesture"/> 的选择那一支
        /// （两档的含义与诚实边界见类注释「手势下标有两个档」）。
        /// </summary>
        public int? SelectIndex { get; init; }

        /// <summary>
        /// **选择族**的第二种手势（阶段 129）：把选中项拨到**树里的一条路径**上
        /// （<c>[1, 0, 0]</c> = 第 1 个根节点的第 0 个子节点的第 0 个子节点）。
        ///
        /// <para>为什么需要它：<see cref="SelectIndex"/> 只能选中**根节点**，而
        /// `ProjectExplorerView` 这类树的根是 `CONFIG_ROOT` / `SCREENS_ROOT` 这种**分组**节点
        /// （处理器对它们只走 else 支、抬 null），真数据在**孩子**里 ⇒ 没有这条手势，
        /// 那些处理器最值得判的那一半**原理上够不到**（阶段 127 的 A/B/C 分档里
        /// `ProjectExplorerView` 与 C 档的 `VariablePicker` 都卡在这里）。</para>
        ///
        /// <para>⚠ 与 <see cref="SelectIndex"/> **互斥**（同时给两条手势是表错误，有自检拦）。
        /// 唯一性键随之扩成 <c>(名字, 下标, 路径)</c> —— 同一个控件可以用不同路径被驱动多次。</para>
        /// </summary>
        public int[]? SelectPath { get; init; }

        /// <summary>
        /// **指针序列**手势（阶段 144，登记 `#77`）：一串 <see cref="PointerStep"/>（按下 → 移动… → 抬起），
        /// 坐标是**目标控件的局部坐标**（与 <c>PointerEventArgs.GetPosition(control)</c> 同义）。
        ///
        /// <para><b>为什么需要它（实测）</b>：`DesignCanvasView` 的面是**指针语义** ——
        /// 它的"选择"是画布**自己的**事件（`CanvasLayer.SelectionChanged`），编辑动作在
        /// `DesignCanvas.Input.cs` 的 `OnPointerPressed/Moved/Released` 里；前四类手势
        /// （按钮族 / 选择族 / 树路径）**一个都盖不住它**（专题
        /// `docs/2026-10-01-design-canvas-pointer-primitive.md`）。</para>
        ///
        /// <para>⚠ 与另外三种手势**互斥**（有自检拦）；驱动时**每一步之间都 `Pump()`** ——
        /// 布局与命中测试都要走一次消息循环，否则 `HitTestControl` 拿不到几何。</para>
        /// </summary>
        public IReadOnlyList<PointerStep>? PointerPath { get; init; }
    }

    private sealed record HostSpec(string HostTypeName, Func<WindowDepot, Window> Build, Probe[] Probes, ClickSpec[] Clicks);

    private static ClickSpec Moves(string name, string why, params (string Label, string Value)[] expect)
        => new(name, Bucket.Moves, expect, why);

    private static ClickSpec Inert(string name, string why)
        => new(name, Bucket.Inert, Array.Empty<(string, string)>(), why);

    private static ClickSpec Never(string name, string why)
        => new(name, Bucket.Never, Array.Empty<(string, string)>(), why);

    /// <summary>
    /// **选择族**的一条 <c>Moves</c>：手势 = 把 <paramref name="name"/> 的选中项拨到
    /// <paramref name="index"/>（<c>-1</c> = **清空选中** —— 见类注释「手势下标有两个档」），
    /// 于是它订阅的 <c>SelectionChanged</c> 处理器被调用。
    ///
    /// <para>🩸 <b>声明里的分量必须是「处理器写的」，不能是「手势自己写的」。</b>
    /// 例：<c>UserManagerView.UserList</c> 那条若声明 <c>选中用户名</c>（它读的是
    /// <c>UserList.SelectedItem</c>），那么把 XAML 里 <c>SelectionChanged=</c> 摘掉、这条声明
    /// <b>照样绿</b> —— 选中项是手势拨的，与处理器无关，判据原理上看不见区别。
    /// 要声明 <c>ShowUserDetail</c> 写出来的东西（<c>DetailUsername.Text</c>）才成立。
    /// 这就是「面的边界 ≠ 没有缺口」的第二形态：<b>探针没读处理器写的字段</b>。</para>
    /// </summary>
    private static ClickSpec SelMoves(string name, int index, string why,
                                      params (string Label, string Value)[] expect)
        => new(name, Bucket.Moves, expect, why) { SelectIndex = index };

    /// <summary>
    /// **选择族**的一条 <c>Moves</c>，手势 = **树里的一条路径**（见 <see cref="ClickSpec.SelectPath"/>）。
    /// 其余语义与 <see cref="SelMoves"/> 完全相同（包括「声明里的分量必须是处理器写的」那条）。
    /// </summary>
    private static ClickSpec SelMovesPath(string name, int[] path, string why,
                                          params (string Label, string Value)[] expect)
        => new(name, Bucket.Moves, expect, why) { SelectPath = path };

    /// <summary>
    /// **指针序列**的一条 <c>Moves</c>（阶段 144）：<paramref name="from"/> 按下 → 中点 → 
    /// <paramref name="to"/> 抬起（坐标是**控件局部坐标**）。
    ///
    /// <para><c>from == to</c> 就是"点一下"（按下即抬起，无位移）；拖动则给一个足够大的位移
    /// （画布的手势识别有阈值，实测 +50px 稳）。</para>
    /// </summary>
    private static ClickSpec Drags(string name, (double X, double Y) from, (double X, double Y) to,
                                   string why, params (string Label, string Value)[] expect)
    {
        var steps = new List<PointerStep>
        {
            new(PointerStepKind.Press, from.X, from.Y),
            new(PointerStepKind.Move, (from.X + to.X) / 2, (from.Y + to.Y) / 2),
            new(PointerStepKind.Move, to.X, to.Y),
            new(PointerStepKind.Release, to.X, to.Y),
        };
        return new(name, Bucket.Moves, expect, why) { PointerPath = steps };
    }

    /// <summary>
    /// 选择族的一条 <c>Inert</c>：拨了选中项，但**本层读不到**处理器写的东西 ⇒ 零信息。
    /// ⚠ 用它之前先问一句：能不能给处理器写的那个字段补一枚探针？能补就别用 <c>Inert</c>
    /// （补探针的价值远大于登记一条 <c>Inert</c>）。
    /// </summary>
    private static ClickSpec SelInert(string name, int index, string why)
        => new(name, Bucket.Inert, Array.Empty<(string, string)>(), why) { SelectIndex = index };

    /// <summary>
    /// 给一条声明挂上**点击前的布景**（例：往输入框里填一个名字、把列表的选中项装上）。
    ///
    /// <para>它的存在是因为本层原先**没有**「先填输入框」的机制 —— 于是凡处理器首行读输入框的按钮，
    /// 只能登记成 <c>Inert</c>（「输入框是空的 ⇒ 提前 return」）。那种 <c>Inert</c> 记的是**夹具形状**，
    /// 不是接线，等于这一层对它零信息。</para>
    ///
    /// <para>⚠ 布景同时是**一条新的作弊面**：把「点击的效果」搬进布景里预先做掉，本层就再也看不见它。
    /// 所以跑的时候有一条自检盯着 —— 布景只许动**输入面**分量（<see cref="Probe.IsInputFace"/>），
    /// 只要有一个数据面分量被布景改动就报红。见类注释「不抓什么」第 6 条。</para>
    /// </summary>
    private static ClickSpec Arranged(ClickSpec spec, Action<HostContext> arrange)
        => spec with { Arrange = arrange };

    /// <summary>
    /// 「**只标脏**」那一族下拉共用的声明理由（阶段 115，为静态面闭合而登记）。
    ///
    /// <para>它们与上面 10 个复选框**同源**：都出自 <c>ProjectSettingsView.AttachChangeHandler</c> ——
    /// 那个辅助函数四个分支分别订阅 <c>nud.ValueChanged</c> / <c>txt.TextChanged</c> /
    /// <c>chk.IsCheckedChanged</c> / <c>cmb.SelectionChanged</c>，本体只写一句 <c>_hasChanges = true</c>。
    /// 静态面的识别因此**必须带类型**：不带类型的话，同一次调用的另外 35 个实参里有 **25 个**
    /// （16 个数字框 + 9 个文本框）会被算成假缺口 —— 另 10 个 <c>CheckBox</c> 属
    /// <see cref="Button"/> 族，被本面排除。<b>25 这个数是负控 nc33 实测点名出来的</b>，不是估算。</para>
    ///
    /// <para>🩸 为什么是 <c>SelInert</c> 而不是干脆不登记：**订阅真的在**（拨动会引发
    /// <c>SelectionChanged</c>），但本层原理上读不到差别 —— <c>_hasChanges</c> 是 private 字段，
    /// 而唯一判它的地方是 <c>OnOkClicked</c> 里那句 <c>if (_hasChanges)</c>，挂在**点了会关窗**的
    /// <c>BtnOk</c> 上；同族的 <c>BtnApply</c> 是**无条件** <c>ApplyInternal()</c>。
    /// 这是「面的边界」第一形态（**未被观测**）的又一例。</para>
    ///
    /// <para>⚠ 所以这 14 条的**唯一作用是把静态面闭合** —— 让
    /// <c>SelectionFaceClassificationGuardTests</c> 能把「本宿主有 19 个带 <c>SelectionChanged</c>
    /// 接线的非 Button 控件」与声明对上。它们**不验证行为**，别当成「已经测过了」。
    /// 要让它们有信息，得走布景：拨好这 14 个 ⇒ 再给 <c>BtnApply</c> 的声明补 14 个模型落点
    /// （登记为后续项，本阶段刻意不做 —— 那是 <c>SaveToProject</c> 的读点覆盖，属另一件事）。</para>
    ///
    /// <para><paramref name="landing"/> 是该控件在 <c>SaveToProject</c> 里的落点。</para>
    /// </summary>
    private static string DirtyOnly(string landing) =>
        "AttachChangeHandler 只做 `_hasChanges = true`（private 字段，当场没有任何模型字段被写）；"
        + "它的值由 SaveToProject 读走、落点是 " + landing + "。"
        + "⚠ 与上面 10 个复选框同源（那个辅助函数的 `cmb` 分支订阅的正是 SelectionChanged）⇒ "
        + "拨动**真的会**引发事件，故它不是「没接线」；但本层读不到差别，见本方法的 xmldoc";

    private static Probe Num(string label, Func<HostContext, int> read) => new(label, c => read(c).ToString());

    /// <summary>数据面分量，值本身就是字符串（例：窗口可见与否）。</summary>
    private static Probe Str(string label, Func<HostContext, string> read) => new(label, read);

    /// <summary>Voice 分量：只进读数（值被归一成 <c>空</c>/<c>非空</c>，不冻结文案）。</summary>
    private static Probe Voice(string label, Func<HostContext, string> read) => new(label, read, IsVoice: true);

    /// <summary>
    /// **事件观测探针**（阶段 128）：把"处理器只 <c>Invoke</c> 一个公开事件"的那种宿主接进本层。
    ///
    /// <para>挂载时机 = **首次读取时**（那时窗口已经 <c>Show</c> 过、手势还没开始）；用"袋里有没有
    /// 这个标签"当"已挂"的标记 ⇒ <see cref="Probe.Read"/> 仍然幂等，也不需要 runner 认识它。</para>
    ///
    /// <para><b><c>isVoice</c></b>（阶段 133 补）：载荷本身只进读数、不进断言 —— 当载荷的值
    /// 取决于**本层控制不了的输入**（例：`FaceplateLibraryView` 的模板表来自**静态**
    /// `DockLayoutState.Templates`）时，断言"载荷是哪一个"会把宿主钉在共享状态上；
    /// 那时应当另立一条**一致性探针**去断言"载荷随选中"（见该宿主的 `载荷随选中`）。</para>
    ///
    /// <para>🩸 <b>诚实边界（用它时必须在 <c>Why</c> 里点名）</b>：它观测的是**视图的公开事件**
    /// ⇒ 判得到「处理器读了什么、抬了谁、载荷对不对」，**判不到**「视图构造函数里那层往静态总线的
    /// 转发」—— 摘掉转发它照样绿。这是"面的边界"第一形态（未被观测）的又一处，写清楚就不是缺口。</para>
    /// </summary>
    private static Probe EventObserved(string label, Action<HostContext, Action<string>> attach,
                                       bool isVoice = false)
        => new(label, ctx =>
        {
            if (!ctx.Observed.ContainsKey(label))
            {
                ctx.Observed[label] = "";      // 先占位：既是"已挂"标记，也是初值
                attach(ctx, payload => ctx.Observed[label] = payload);
            }

            return ctx.Observed[label];
        }, IsVoice: isVoice);

    /// <summary>**输入面**数值分量 —— 只有它允许被布景改动（<see cref="Arranged"/>）。</summary>
    private static Probe InputNum(string label, Func<HostContext, int> read)
        => new(label, c => read(c).ToString(), IsInputFace: true);

    /// <summary>**输入面**字符串分量（例：列表当前选中的那一项）—— 布景的第二个合法落点。</summary>
    private static Probe InputStr(string label, Func<HostContext, string> read)
        => new(label, read, IsInputFace: true);

    /// <summary>
    /// 读窗口里的具名控件；找不到就抛（**不许静默** —— 控件改名会让 Probe 恒读到同一个值）。
    ///
    /// <para>🩸 <b>两条路都要走</b>（阶段 126 加第 10 个宿主 —— 首个 <c>UserControl</c> 宿主 —— 时实测）：
    /// 窗口**自己的** XAML 名字在窗口的 NameScope 里（<c>FindControl</c> 走这条）；
    /// 而被装配进来的 <c>UserControl</c> 的 XAML 名字在**它自己的** NameScope 里 ——
    /// 那时 <c>window.FindControl</c> 不是返回 <c>null</c>，而是**抛**
    /// <c>InvalidOperationException: Could not find parent name scope</c>
    /// （首跑实测：`CrossReferenceView` 的探针读数一行都没打出来就整趟抛了）。</para>
    ///
    /// <para>⚠ 那个异常**只在这里兜**、且必须**窄**：它是"这条路找不到"，不是"控件没了" ——
    /// 后面那条逻辑树的路才是判据要的答案；两条都空手才报红。</para>
    ///
    /// <para>⚠ 找不到与**类型不对**要分开报：只报"找不到"会把「名字对、类型写错」的 Probe
    /// 说成「控件没了」，下一个人的第一反应会是去 XAML 里翻一个其实还在的控件。</para>
    /// </summary>
    private static T Control<T>(Window window, string name) where T : Control
    {
        Control? found = null;
        try
        {
            found = window.FindControl<T>(name);
        }
        catch (InvalidOperationException)
        {
            // 窗口自己的 NameScope 里没有这个名字（它属于装配进来的 UserControl 那一套）——
            // 交给下面那条路，别把异常当成"控件没了"。
        }

        found ??= window.GetLogicalDescendants().OfType<Control>()
                        .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

        if (found is not T typed)
            throw new InvalidOperationException(
                $"{window.GetType().Name} 里找不到具名 {typeof(T).Name} {name} —— "
                + (found is null
                    ? "控件被删/改名了，或者它不在这个窗口的逻辑树里"
                    : $"同名控件是 {found.GetType().Name}、不是 {typeof(T).Name}")
                + "（后者说明 Probe 的读法错了，不是控件没了）");
        return typed;
    }

    private static int Items(Window window, string listName) => Control<ListBox>(window, listName).Items.Count;

    /// <summary>读具名 <see cref="TreeView"/> 的**根节点数** —— <see cref="Items"/> 那只认 <see cref="ListBox"/>。</summary>
    private static int TreeItems(Window window, string treeName) => Control<TreeView>(window, treeName).Items.Count;

    /// <summary>
    /// <see cref="TreeView"/> 的**选中下标**（<c>-1</c> = 没选中）。
    /// ⚠ <c>TreeView</c> **没有** <c>SelectedIndex</c>（与 <c>ListBox</c> / <c>ComboBox</c> 不同，
    /// 它只暴露 <c>SelectedItem</c>）⇒ 必须自己在 <c>Items</c> 里定位；直接写
    /// <c>tree.SelectedIndex</c> 是编译期就拦下的（阶段 128 实测）。
    /// </summary>
    private static int TreeSelectedIndex(Window window, string treeName)
    {
        var tree = Control<TreeView>(window, treeName);
        return tree.SelectedItem is { } item
            ? tree.Items.Cast<object>().ToList().IndexOf(item)
            : -1;
    }

    /// <summary>
    /// <see cref="TreeView"/> 选中项**在树里的路径**（`"1,0,0"`；没选中 = <c>"-"</c>）。
    ///
    /// <para>为什么需要它：<see cref="TreeSelectedIndex"/> 只回答"在第几个**根**节点上"，
    /// 而 <see cref="ClickSpec.SelectPath"/> 驱动的是**嵌套**节点 —— 那时下标恒为 <c>-1</c>
    /// （阶段 129 实测：`SelMovesPath` 选中的是孩子，根下标当然找不到它）。</para>
    /// </summary>
    private static string TreeSelectedPath(Window window, string treeName)
    {
        var tree = Control<TreeView>(window, treeName);
        if (tree.SelectedItem is not { } target) return "-";

        var path = new List<int>();
        return Descend(tree.Items.Cast<object>().ToList(), target, path)
            ? string.Join(",", path)
            : "-";                                   // 不在树里（不该发生）

        // ⚠ 必须**递归**找后代：只查"直接孩子"会在两层以上的节点上返回"-"
        //   （阶段 129 实测：`[1,0,0]` 那一步先写成"直接孩子"版，读数就是"-"）。
        static bool Descend(List<object> items, object target, List<int> path)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], target))
                {
                    path.Add(i);
                    return true;
                }

                if (items[i] is TreeViewItem node)
                {
                    path.Add(i);
                    if (Descend(node.Items.Cast<object>().ToList(), target, path)) return true;
                    path.RemoveAt(path.Count - 1);
                }
            }

            return false;
        }
    }

    /// <summary>列表当前选中项的 <c>Tag</c> 是不是指定的模型类型（1/0）——
    /// 「新建后自动选中」这类行为的**直接**见证（比「下游按钮还能不能干活」更贴题）。</summary>
    private static int SelectedTagIs<T>(Window window, string listName)
    {
        var list = Control<ListBox>(window, listName);
        return list.SelectedItem is ListBoxItem item && item.Tag is T ? 1 : 0;
    }

    private static string StatusOf(Window window)
    {
        var text = Control<TextBlock>(window, "StatusText").Text;
        return string.IsNullOrEmpty(text) ? "空" : "非空";
    }

    /// <summary>
    /// 报警配置右列（<c>AlarmList</c>）**第一条报警的名字**；列表空记 <c>无</c>。
    ///
    /// <para>🩸 为什么要读**身份**而不是计数：<c>OnGroupSelectionChanged</c> 换组时右列会被换成
    /// **另一个组**的报警，而夹具里两个组各只有一条 ⇒ 计数（<c>报警列表</c>）换组前后都是 1，
    /// **看不见差别**。计数探针在这里是盲的 —— 这是「面的边界 ≠ 没有缺口」在**探针口径**上的
    /// 第三个形态（前两个：<c>TrendConfigView</c> 的显示位、<c>UserManagerView</c> 的详情框）。</para>
    /// </summary>
    private static string FirstAlarmName(Window window)
    {
        var list = Control<ListBox>(window, "AlarmList");
        return list.Items.Count > 0 && list.Items[0] is ListBoxItem { Tag: AlarmConfig alarm }
            ? alarm.Name
            : "无";
    }

    /// <summary>右列**当前选中**的那条报警（编辑面板正在编辑的对象）。未选中时为 null。</summary>
    private static AlarmConfig? SelectedAlarm(Window window)
    {
        var list = Control<ListBox>(window, "AlarmList");
        return list.SelectedItem is ListBoxItem { Tag: AlarmConfig alarm } ? alarm : null;
    }

    // ──────────────────────────────────────────────
    //  宿主专用读数（够具体，不值得做成通用助手；写在这里是为了让表本身读得清）
    // ──────────────────────────────────────────────

    /// <summary>通讯树选中项的形状：<c>1</c>=连接节点、<c>2</c>=子设备节点、<c>0</c>=什么都没选。</summary>
    private static int CommTreeShape(Window window)
        => Control<TreeView>(window, "CommTree").SelectedItem is Views.CommTreeNode node
            ? (node.IsConnection ? 1 : 2)
            : 0;

    /// <summary>
    /// 模型里那条连接的 <c>DriverId</c> 落在驱动下拉的**第几格**（<c>1</c> 起；
    /// <c>0</c> = 下拉里没有这个 Id；<c>-1</c> = **还没有连接**）。
    ///
    /// <para><b>为什么要绕这一下、不直接读 <c>DriverId</c>：</b>读原文等于把**注册表里的 Id 字面量**
    /// 冻进判据，而那张表会增删（<c>BuiltinDriverCatalog</c> 存在的理由就是「清单只有一处」）。
    /// 换算成格号之后回答的仍是同一件事 ——「处理器有没有把下拉当前项写进模型」——
    /// 而 <c>OnDriverTypeChanged</c> 写出去的**只有** <c>_selectedConnection.DriverId</c> 这一样。</para>
    ///
    /// <para>🩸 它读的是**模型**（<c>DriverId</c>）与下拉的**项表**（静态），
    /// **不读下拉的选中项** —— 否则「手势拨了选中项」会被算成处理器的功劳
    /// （见 <see cref="SelMoves"/> 那条 🩸）。</para>
    /// </summary>
    private static int ConnDriverSlot(WindowDepot depot, Window window)
    {
        var conn = depot.Variables.Connections.FirstOrDefault();
        if (conn is null) return -1;
        var combo = Control<ComboBox>(window, "CboDriverType");
        for (int i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is ComboBoxItem item && item.Tag is string id && id == conn.DriverId)
                return i + 1;
        return 0;
    }

    /// <summary>
    /// 连接名输入框里的字**是不是模型里那条连接的名字**（<c>一致</c> / <c>不一致</c>）。
    ///
    /// <para><b>它是一枚「一致性探针」，两侧都不冻：</b>左边是框里的字（可以被布景弄脏），
    /// 右边是模型。<c>OnConnectionSelected</c> 写出去的、本层够得到的只有这个框
    /// （<c>TxtConnName.Text = conn.Name</c>）—— 而其余几样（驱动下拉、参数字段、按钮可见位）
    /// 在「已经选中过这条连接」之后都是**同值**、读不出接线。
    /// ⇒ 布景先把框弄脏、再驱动，就能把「处理器回填了」变成一次真的迁移。</para>
    ///
    /// <para>⚠ 它读的是**模型**那一侧与框的字面值，不读任何本地化文案（连接名是模型数据：
    /// `OnAddConnection` 用 <c>comm.conn.new</c> 模板生成，但模板结果落在模型里、就是这个框要装的东西）。</para>
    /// </summary>
    private static string ConnNameBoxMatchesModel(WindowDepot depot, Window window)
    {
        var conn = depot.Variables.Connections.FirstOrDefault();
        var box = Control<TextBox>(window, "TxtConnName").Text ?? "";
        if (conn is null) return box.Length == 0 ? "一致" : "不一致";
        return string.Equals(box, conn.Name, StringComparison.Ordinal) ? "一致" : "不一致";
    }

    /// <summary>
    /// 地址提示那两处文案**当前属于哪一档寄存器类型**（<c>holding</c> / <c>input</c> /
    /// <c>discrete</c> / <c>coil</c> / <c>未知</c>）。
    ///
    /// <para><b>为什么要这么绕：</b><c>UpdateSlaveVarAddressHint</c> 写出去的东西**只有两串本地化文案**
    /// （<c>TxtSlaveVarAddressHint.Text</c> 与 <c>TxtSlaveVarAddress.PlaceholderText</c>）——
    /// 把文案冻进判据等于冻住一份本地化结果（换语言全表红，而那条红不带任何接线信息）。
    /// ⇒ **反向查表**：把屏幕上那两串字拿去和字典里四档的值逐一对表，命中哪一档就报哪一档。
    /// 换语言时字典与屏幕**一起变**，读数不变；而「处理器没跑（文案停在上一次选的档）」与
    /// 「处理器跑了」这样就分得开了 —— 这正是它作为见证的全部价值。</para>
    ///
    /// <para>⚠ 两串必须**同时**命中同一档才算：<c>hint</c> 更新了、占位符还是旧的，要报红。
    /// ⚠ 它把「档名」这四个字面量（holding/input/discrete/coil）抄进了判据，这是**刻意的**：
    /// 档名是**模型的枚举口径**（与 <c>BuildModbusAddress</c> 里那四个前缀一一对应），不是界面文案。
    /// 真加了第五档寄存器类型 ⇒ 这里读 <c>未知</c> ⇒ 报红，逼人来补。</para>
    /// </summary>
    private static string AddressHintKind(Window window)
    {
        var hint = Control<TextBlock>(window, "TxtSlaveVarAddressHint").Text ?? "";
        var placeholder = Control<TextBox>(window, "TxtSlaveVarAddress").PlaceholderText ?? "";
        foreach (var kind in new[] { "holding", "input", "discrete", "coil" })
        {
            if (AppLocalization.Service[$"comm.slave.var.reg.{kind}.hint"] == hint
                && AppLocalization.Service[$"comm.slave.var.reg.{kind}.addr"] == placeholder)
                return kind;
        }
        return "未知";
    }

    /// <summary>
    /// 批量预览行里**第一个样本地址的寄存器前缀**（<c>4</c>=保持 / <c>3</c>=输入 / <c>1</c>=离散 /
    /// <c>0</c>=线圈；<c>无</c> = 预览是空的）。
    ///
    /// <para><b>为什么不读整行文案：</b>预览 = 一句本地化模板（<c>comm.slave.var.batch.preview</c>）
    /// 套上样本串，冻它等于冻一份本地化结果。而样本里那段地址**不是文案** ——
    /// 它是 <c>BuildModbusAddress</c> 的产物，包住它的 <c>(</c> <c>)</c> 是代码里的字面量、不进字典。
    /// ⇒ 只取第一个 <c>(</c> 后面那一个字符，回答「处理器是不是按**当前**寄存器类型重算了预览」。</para>
    ///
    /// <para>⚠ 装它的控件是 <see cref="TextBlock"/>，**不是** <see cref="TextBox"/> ——
    /// 首跑实测<b>点名</b>了这一点（<c>Expected control 'TxtBatchPreview' to be
    /// 'Avalonia.Controls.TextBox but it was 'Avalonia.Controls.TextBlock'</c>）。
    /// 本视图里「预览」是只读展示，没有输入面。</para>
    /// </summary>
    private static string BatchPreviewRegPrefix(Window window)
    {
        var text = Control<TextBlock>(window, "TxtBatchPreview").Text ?? "";
        var open = text.IndexOf('(');
        return open >= 0 && open + 1 < text.Length ? text[open + 1].ToString() : "无";
    }

    /// <summary>
    /// 第一条连接的启用位：<c>1</c>=启用、<c>0</c>=停用、<c>-1</c>=**还没有连接**。
    ///
    /// <para>⚠ 第三个档位是**刻意**分出来的：把它并进 <c>0</c>，读数就同时意味着「停用了」与「压根没有」，
    /// 而这两件事下一步完全不同（与 <see cref="FirstSlaveVarAddress"/> 同一条理由）。
    /// 代价是每多一处「有 ↔ 没有」的迁移就多一条声明 —— 这正是本层「声明要等于事实」的价钱，付得值。</para>
    /// </summary>
    private static int ConnEnabled(WindowDepot depot)
        => depot.Variables.Connections.FirstOrDefault() is { } conn ? (conn.IsEnabled ? 1 : 0) : -1;

    /// <summary>第一个子设备的启用位（三档同 <see cref="ConnEnabled"/>：<c>-1</c> = 还没有子设备）。</summary>
    private static int SlaveEnabled(WindowDepot depot)
        => depot.Variables.Connections.SelectMany(c => c.Slaves).FirstOrDefault() is { } slave
            ? (slave.IsEnabled ? 1 : 0)
            : -1;

    /// <summary>
    /// 第一个子设备下**第一个变量**的 Modbus 地址 —— 「地址由 <c>BuildModbusAddress</c> 组合出来」
    /// 这件事唯一的落点见证（<c>OnSaveEditSlaveVar</c> 那条腿除了它没有任何分量会动）。
    ///
    /// <para>两档「没有」刻意分开：<c>无从站</c> / <c>无变量</c> —— 它们各自见证一步（子设备建了没、
    /// 变量加了没），合成一个「无」就丢掉这两条信息。</para>
    /// </summary>
    private static string FirstSlaveVarAddress(WindowDepot depot)
    {
        var slave = depot.Variables.Connections.SelectMany(c => c.Slaves).FirstOrDefault();
        if (slave is null) return "无从站";
        return depot.Variables.Variables.FirstOrDefault(v => v.SlaveId == slave.Id)?.Address ?? "无变量";
    }

    /// <summary>
    /// 夹具里**最后一条**趋势的笔数；<c>-1</c> = 一条趋势都没有。
    ///
    /// <para>两档「没有」刻意分开（与 <see cref="ConnEnabled"/> 同一条理由）：<c>-1</c> 是「趋势表空了」、
    /// <c>0</c> 是「这条趋势还没有笔」—— 它们各自见证一步。⚠ 用「最后一条」而不是「夹具那一条」，
    /// 是因为 <c>BtnAddTrend</c> 把新趋势**追加**到表尾并让它成为当前趋势：
    /// 这个分量于是同时见证「最后一条换了人」（真实覆盖 2 → 0）与「新趋势加上笔了」。</para>
    /// </summary>
    private static int LastTrendPenCount(WindowDepot depot)
        => depot.Trends.Count == 0 ? -1 : depot.Trends[^1].Pens.Count;

    /// <summary>
    /// <c>TrendConfigView</c> 那 6 条 <c>Inert</c> 的**眼睛**：全工程里满足条件的趋势数 / 笔数。
    ///
    /// <para>「全工程累计」而不是「当前那条」是刻意的：<c>_currentTrend</c> / <c>_currentPen</c>
    /// 都是私有字段，表里够不到；而 <c>BtnAddTrend</c> / <c>BtnDeleteTrend</c> 中途会换人 ——
    /// 累计口径只回答「**有没有任何一条被改过**」，这正是本层要问的问题。</para>
    ///
    /// <para>⚠ 探针只数「关着的 / 停用的」，所以本轮全程读 <c>0</c>：这不是恒真，
    /// 是夹具与生产默认值恰好一致（显示位默认全开、笔默认启用）。把默认值改成关闭，
    /// 这六条读数会立刻抬起来 —— 而它们**一条都没被声明读过** ⇒ 抬起来就是报红。</para>
    /// </summary>
    private static int TrendsWith(WindowDepot depot, Func<TrendConfig, bool> predicate)
        => depot.Trends.Count(predicate);

    /// <summary>同上，跨**所有**趋势的笔累计（见 <see cref="TrendsWith"/>）。</summary>
    private static int PensWith(WindowDepot depot, Func<TrendPen, bool> predicate)
        => depot.Trends.Sum(t => t.Pens.Count(predicate));

    /// <summary>
    /// `_currentPen` 的标签**是不是夹具那两支之一**（`液位` / `温度`），其余一律记 <c>其它</c>。
    ///
    /// <para>⚠ **刻意不直接冻结标签原文**：新建的笔标签走
    /// <c>Lang.TrendPenDefaultName = "trend.pen.default.name"</c> —— 那是**字典文案**（首跑实测 `笔 1`）。
    /// 把它抄进判据等于冻住一份本地化结果：换语言全表红，而那条红不带任何接线信息
    /// （与 <see cref="Voice"/> 分量、<c>SelectRole</c> 读 Id 不读显示名同一条理由）。</para>
    ///
    /// <para>归一化之后它仍然够用：它要回答的是「`_currentPen` 指的是夹具第 1 支 / 第 2 支 / 别的」，
    /// 而这三档正是 <c>PenList</c> / <c>BtnAddPen</c> / <c>BtnDeleteTrend</c> 各自动的那一档。
    /// 夹具那两支的标签是**模型数据**（<c>WindowDepot</c> 亲手塞的），不是 UI 文案。</para>
    /// </summary>
    private static string PenLabelKind(Window window)
    {
        var text = Control<TextBox>(window, "PenLabelInput").Text ?? "";
        return text is "液位" or "温度" ? text : "其它";
    }

    // ──────────────────────────────────────────────
    //  UserManagerView 专用读数与布景（第八个宿主）
    // ──────────────────────────────────────────────

    /// <summary>
    /// <c>UserManagerView</c> 窗口标题的**后缀**那一半，归一成 <c>空</c>/<c>非空</c>。
    ///
    /// <para>这个宿主**没有** <c>StatusText</c> 那种提示行 —— <c>ShowStatus</c> 把提示写进
    /// <c>Title</c>（<c>「窗口名 — 提示」</c>）。归一化口径与 <see cref="StatusOf"/> 一致：
    /// 只回答「有没有反馈」，**不冻结文案**（文案走字典，冻进来等于把一份本地化结果抄进判据）。</para>
    ///
    /// <para>为什么非要有它：<c>BtnRefreshLog</c> 那种「只刷一次界面」的按钮，数据面读数点击前后
    /// **完全相等** ⇒ 没有这枚分量，它在本层只能写成一条**零信息**的 <c>Inert</c>。</para>
    /// </summary>
    private static string TitleTail(Window window)
        => window.Title is { Length: > 0 } t && t.Contains('—') ? "非空" : "空";

    /// <summary>
    /// 校验提示行的三档：<c>隐藏</c> / <c>空</c> / <c>非空</c>。
    ///
    /// <para>⚠ **不读文案本身**：`ValidateHardwareTarget` 写的那几条文案全走字典（通过 / 警告 / 错误），
    /// 读它等于冻住一份本地化结果（与 <see cref="Voice"/>、读角色 Id 不读显示名同一条理由）。
    /// 三档里「隐藏 ↔ 非空」是处理器真会走的迁移（`target == null` 那一支把它藏起来）。</para>
    /// </summary>
    private static string BannerState(Window window, string name)
    {
        var t = Control<TextBlock>(window, name);
        if (!t.IsVisible) return "隐藏";
        return string.IsNullOrEmpty(t.Text) ? "空" : "非空";
    }

    /// <summary>
    /// 模型里第一条运行时用户的 <c>RoleId</c> 是否**等于** <c>CmbRuntimeUserRole</c> 当前项的 Tag（1/0）。
    ///
    /// <para><b>为什么要有这一枚：</b><c>CmbRuntimeUserRole</c> 的两个出口（草稿对象、列表项文案）本层都读不到，
    /// 于是它的驱动只能记成 <c>SelInert</c>；但「订阅在不在」会在**下一次落盘**时显形 ——
    /// <c>BtnApply</c> 把草稿整体写回模型，模型里的角色于是等于「下拉当时选的那一项」
    /// **当且仅当**那条订阅在。这就是「两段式声明」在选择族上的第一次落地
    /// （与那 10 个只标脏的复选框 + <c>BtnApply</c> 同形）。</para>
    /// </summary>
    private static int ModelRoleFollowsCombo(WindowDepot depot, Window window)
    {
        var users = depot.Project.SecurityConfig.RuntimeUsers;
        if (users is null || users.Count == 0) return 0;
        var tag = (Control<ComboBox>(window, "CmbRuntimeUserRole").SelectedItem as ComboBoxItem)?.Tag as string;
        return string.Equals(users[0].RoleId, tag, StringComparison.Ordinal) ? 1 : 0;
    }

    /// <summary><c>UserList</c> 选中项 <c>Tag</c> 里的用户名（<c>""</c> = 什么都没选）。</summary>
    private static string SelectedUsername(Window window)
        => Control<ListBox>(window, "UserList").SelectedItem is ListBoxItem item && item.Tag is UserProfile user
            ? user.Username : "";

    /// <summary>
    /// <c>RoleList</c> 选中项 <c>Tag</c> 里的角色 **Id**（<c>""</c> = 什么都没选）。
    /// ⚠ 读 Id 不读显示名：显示名走字典（#50）⇒ 读它等于冻住一份本地化结果。
    /// </summary>
    private static string SelectedRoleId(Window window)
        => Control<ListBox>(window, "RoleList").SelectedItem is ListBoxItem item && item.Tag is UserRole role
            ? role.Id : "";

    /// <summary>
    /// <see cref="TreeView"/> 里**带字符串 `Tag` 的节点数**（＝ 叶子数；分组节点的 `Tag` 为 null）。
    ///
    /// <para>为什么按 `Tag` 而不是按"有没有孩子"：本仓的树（`VariablePicker` / `VariableBrowserView`）
    /// 给**可绑定项**挂字符串 `Tag`（绑定标识），分组节点不挂 —— 这正是处理器的分派依据，
    /// 所以按它数出来的数**就是处理器眼里的叶子数**。</para>
    /// </summary>
    private static int TreeLeafCount(Window window, string treeName, Func<string, bool>? only = null)
    {
        var tree = Control<TreeView>(window, treeName);
        return Count(tree.Items.Cast<object>().ToList());

        int Count(List<object> items) => items.Sum(i => i switch
        {
            TreeViewItem { Tag: string tag } node => (only is null || only(tag) ? 1 : 0)
                                                      + Count(node.Items.Cast<object>().ToList()),
            TreeViewItem node => Count(node.Items.Cast<object>().ToList()),
            _ => 0,
        });
    }

    /// <summary>具名 <see cref="TextBox"/> 的文本长度 —— 布景「往输入框里填东西」的唯一见证。</summary>
    private static int TextLen(Window window, string boxName)
        => Control<TextBox>(window, boxName).Text?.Length ?? 0;

    /// <summary>
    /// 具名 <see cref="TextBox"/> 的**有 / 无内容**（<c>空</c> / <c>非空</c>）。
    ///
    /// <para>⚠ 既不读长度也不读原文：框里装的可能是**生成出来的**内容
    /// （<c>RuntimeUserValidator.NextDefaultUsername</c> 造的用户名）或走字典的文案，
    /// 两种都不该冻进判据（与 <see cref="Voice"/>、读角色 Id 不读显示名同一条理由）。
    /// 它只回答「处理器有没有往这个框里写过东西」—— 这正是选择族声明要问的那一句。</para>
    /// </summary>
    private static string BoxState(Window window, string boxName)
        => string.IsNullOrEmpty(Control<TextBox>(window, boxName).Text) ? "空" : "非空";

    /// <summary>布景：把 <c>UserList</c> 里指定用户名的条目装上。装不上就**抛** —— 静默装不上等于布景没生效。</summary>
    private static void SelectUser(Window window, string username)
    {
        var list = Control<ListBox>(window, "UserList");
        list.SelectedItem = list.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag is UserProfile u && u.Username == username)
            ?? throw new InvalidOperationException($"UserList 里没有用户 {username} —— 夹具变了，布景要跟着改");
    }

    /// <summary>布景：把 <c>RoleList</c> 里指定角色 Id 的条目装上（同 <see cref="SelectUser"/>）。</summary>
    private static void SelectRole(Window window, string roleId)
    {
        var list = Control<ListBox>(window, "RoleList");
        list.SelectedItem = list.Items.OfType<ListBoxItem>()
            .FirstOrDefault(i => i.Tag is UserRole r && r.Id == roleId)
            ?? throw new InvalidOperationException($"RoleList 里没有角色 {roleId} —— 夹具变了，布景要跟着改");
    }

    // ──────────────────────────────────────────────
    //  表
    // ──────────────────────────────────────────────

    private static readonly HostSpec[] Hosts =
    {
        // ── 变量管理器：本层刻意选它作「大面板」的代表（登记 #39 的残余里它最大，20 个具名按钮） ──
        // 夹具状态（WindowDepot）：Variables 有 2 条（液位 / 温度，都没有 GroupId ⇒ 是孤儿）、
        // Groups 空、UdtDefinitions 空、Connections 空。
        // 阶段 107 有 4 条 Inert 是**登记 #64**（「新建后自动选中」不生效）的现场；
        // 阶段 108 修好 #64 之后，这 4 条按「修好就红」的预案当场转成 Moves。
        // ⚠ 另有 3 条（BtnSaveVar / BtnSaveAlarm / ChkAlarmEnabled）当时把 Why **错记成** #64 ——
        // 它们的空操作其实是**点击顺序**造成的（选中项在更早一步被显式置空）。为什么没被抓住？
        // 因为 **Inert 的 Why 是免证的**：没有任何判据会去打它。阶段 108 一并更正。
        new("VariableManagerView",
            d => new Views.VariableManagerView(d.Variables),
            new[]
            {
                Num("分组", c => c.Depot.Variables.Groups.Count),
                Num("变量", c => c.Depot.Variables.Variables.Count),
                Num("UDT", c => c.Depot.Variables.UdtDefinitions.Count),
                Num("分组列表", c => Items(c.Window, "GroupList")),
                Num("变量列表", c => Items(c.Window, "VariableList")),
                Num("变量选中", c => SelectedTagIs<Variable>(c.Window, "VariableList")),
                // ── 「选择族」的眼睛（阶段 115）──
                // ⚠ 两枚的分档刻意不同：`变量名框` 本表没有任何布景要动它 ⇒ 留在**严格的一侧**；
                // `字段名框` 有一条布景（BtnCommitField）要往里写名字 ⇒ 必须标成输入面。
                Str("变量名框", c => Control<TextBox>(c.Window, "TxtVarName").Text ?? ""),
                InputStr("字段名框", c => Control<TextBox>(c.Window, "TxtFieldName").Text ?? ""),
                Num("报警列表", c => Items(c.Window, "AlarmList")),
                Num("报警选中", c => SelectedTagIs<VariableAlarmConfig>(c.Window, "AlarmList")),
                Num("UDT列表", c => Items(c.Window, "UdtList")),
                Num("UDT选中", c => SelectedTagIs<UdtDefinition>(c.Window, "UdtList")),
                Num("字段列表", c => Items(c.Window, "FieldList")),
                Voice("状态栏", c => StatusOf(c.Window)),
            },
            new[]
            {
                Moves("BtnAddGroup", "新建分组：Groups +1（列表里另有一条虚拟「未分组」，所以列表数也是 +1）",
                    ("分组", "1"), ("分组列表", "2")),

                Moves("BtnAddVariable", "首次点它时 _selectedGroup 还是 null ⇒ 它会先把第一个分组选上，再把变量加进去；"
                    + "变量列表只显示**当前分组**里的变量，所以模型 +1、列表也 +1。"
                    + "「变量选中」**直接**盯住那句注释声明的行为（新建后自动选中它）—— "
                    + "阶段 107 时它恒为 0，阶段 108 修好登记 #64 之后才是 1。"
                    + "⚠ `变量名框` 变成 `Var3`：那是产品自己的命名（`$\"Var{count + 1}\"`，**不是**字典文案）"
                    + "⇒ 可以冻进判据，换语言也不会红",
                    ("变量", "3"), ("变量列表", "1"), ("变量选中", "1"), ("变量名框", "Var3")),

                // ── 两条「选择族」（阶段 115）：同一个控件驱动**两次** —— 唯一性键是 (名字, 手势下标) ──
                SelMoves("GroupList", 1, "OnGroupSelected（**代码里** `SelectionChanged +=` 接的）：拨到第 2 项"
                    + "（虚拟「未分组」—— 夹具里那两条变量都没有 GroupId）⇒ 处理器 `RefreshVariables` 按新分组重筛，"
                    + "右列从「Group1 的 Var3」换成「未分组的 液位 / 温度」⇒ 1 → 2。"
                    + "⚠ `变量选中` 归 0：`RefreshVariables` 重建列表**不带选中**"
                    + "（与 BtnDuplicateVariable 那条同一个成因）",
                    ("变量列表", "2"), ("变量选中", "0")),

                SelMoves("GroupList", 0, "同一条处理器的第二次驱动：拨回第 1 项（Group1）⇒ 右列换回它那一条 ⇒ 2 → 1。"
                    + "⚠ **这一条存在的意义是把状态还原**：不拨回来，后面 `BtnDeleteVariable` 就会在「未分组」的视角下"
                    + "删变量，那条声明里的 `变量列表` 会从 1 变成 2（删了反而更多，读数看着像假的）",
                    ("变量列表", "1")),

                SelInert("CboConnectionId", 0, "RefreshSlaveCombo（**代码里** `SelectionChanged +=` 接的）："
                    + "夹具里 `Connections` 是空的 ⇒ 这个下拉只有一项「未分配」且**已经是选中项** ⇒ 拨到同一个下标"
                    + "**不引发 SelectionChanged**（Avalonia 的索引设置器在值不变时不发通知），处理器压根没跑；"
                    + "退一步说，即便跑了，它也只是用同一个「未分配」重填 `CboSlaveId`（写入同值）。"
                    + "⚠ 这是**夹具形状**、不是接线缺失：夹具里放一条连接，它立刻变成 Moves。"
                    + "⚠ 也正因为如此本条**做不出负控**（摘掉订阅读出来一模一样）—— 本层对它的全部信息就是"
                    + "「这个具名控件存在、且被分类了」"),

                // 下面四条：登记 #64 的现场。SelectListItemByTag 拿 Tag.ToString() 去比 Id，
                // 而 Variable/VariableGroup/VariableAlarmConfig/UdtDefinition 都没覆写 ToString()
                // ⇒ 「新建后自动选中」这条注释声明的行为**不存在**，于是依赖选中的按钮全都提前 return。
                Moves("BtnAddAlarm", "报警挂在**当前选中的变量**上，而上一步新建的变量现在真的被选中了"
                    + "（阶段 107 时不是 —— 登记 #64）⇒ 报警列表 0 → 1，且新报警被自动选上",
                    ("报警列表", "1"), ("报警选中", "1")),

                // ⚠ 这一条夹在 AddAlarm / DeleteAlarm 之间，是**唯一**一个 `AlarmList` 有内容的窗口。
                SelInert("AlarmList", 0, "OnAlarmSelected（**代码里** `SelectionChanged +=` 接的）：本序列里 "
                    + "`AlarmList` 有内容的那一刻（上一步之后）它**已经被自动选中在第 0 项**"
                    + "（`AddAlarm` 末尾 `SelectListItemByTag`）⇒ 拨同一个下标不发通知；"
                    + "另一边 `BtnDeleteAlarm` 之后列表又空了。⚠ 夹具形状 + 点击顺序的合成结果，不是接线缺失。"
                    + "⚠ 同样做不出负控：要看见它，得先有一条**自带报警**的夹具变量，或者先清掉选中再拨"),

                Moves("BtnDeleteAlarm", "上一步真的加了一条报警并被选中（阶段 107 时不是 —— 登记 #64）"
                    + "⇒ 删得掉：报警列表 1 → 0，选中随之清空",
                    ("报警列表", "0"), ("报警选中", "0")),

                Moves("BtnDuplicateVariable", "_selectedVariable 有值（阶段 107 时为 null —— 登记 #64）⇒ 复制生效："
                    + "模型 +1、当前分组列表 +1。⚠ 复制后 RefreshVariables() 重建列表会把**界面选中清掉**"
                    + "（_selectedVariable 仍有值，只是列表 SelectedItem 变 null）⇒「变量选中」归 0",
                    ("变量", "4"), ("变量列表", "2"), ("变量选中", "0")),

                // ⚠ 排在 `BtnDuplicateVariable` 之后：那一步 `RefreshVariables` 重建列表时把选中清掉了
                // （`变量选中` 归 0），于是这里拨得动 —— 排在前面的话拨的是「已选中的第 0 项」。
                SelMoves("VariableList", 1, "OnVariableSelected（**代码里** `SelectionChanged +=` 接的）："
                    + "拨到第 2 条（`Var3_Copy`，上一步复制出来的）⇒ 处理器 `LoadVariableToForm` 把它填进表单。"
                    + "🩸 见证是 `变量名框` —— **处理器写的**；若只声明 `变量选中`，摘掉订阅这条照样绿"
                    + "（选中项是手势拨的，与处理器无关）",
                    ("变量选中", "1"), ("变量名框", "Var3_Copy")),

                Moves("BtnDeleteVariable", "上一步那条选择族把 `_selectedVariable` 指上了 `Var3_Copy` ⇒ 删得掉"
                    + "（阶段 107 时它为 null ⇒ 提前 return —— 登记 #64）：模型 −1、列表 −1；"
                    + "`RefreshVariables` 重建列表又把选中清掉 ⇒ `变量选中` 归 0",
                    ("变量", "3"), ("变量列表", "1"), ("变量选中", "0")),

                Inert("BtnSaveVar", "**与 #64 无关**：上一步删除时把 _selectedVariable 显式置空了（本序列的顺序）"
                    + "⇒ 处理器首行就 return。⚠ 即便它有值，也只是把表单里的字符串写回**同一个**变量"
                    + "（表单本来就是从它读出来的）再由 RefreshVariables() 重建列表 ⇒ 本层分量看不出差别"),

                Inert("BtnSaveAlarm", "**与 #64 无关**：_selectedAlarm 在第 4 步「删除报警」时已置空（本序列的顺序）"
                    + "⇒ 处理器提前 return。⚠ 这条是「**未被观测**」而不是「没动作」：本层分量里没有"
                    + "「报警启用位 / 需确认位」，所以即使它跑到了，读出来也一样（见诚实边界）"),

                Moves("BtnDeleteGroup", "删掉那个分组，组里的变量被**移入虚拟「未分组」**（GroupId 清空）⇒ "
                    + "模型里变量数不变、分组数减 1、列表清空后重建成虚拟分组那一项",
                    ("分组", "0"), ("分组列表", "1"), ("变量列表", "0")),

                Inert("BtnValidateIntegrity", "夹具里没有连接、没有 UDT，校验只报「无待修项」并写状态栏 —— 数据面不动"),

                Moves("BtnAddUdt", "新建 UDT 定义并**直接**把它赋给 _selectedUdt（不依赖列表选择）；"
                    + "本次新增的「UDT选中」才盯住那条选择路径 —— 阶段 107 时它恒为 0（同属登记 #64，"
                    + "但当时**没有任何判据盯着 UdtList 这条**）",
                    ("UDT", "1"), ("UDT列表", "1"), ("UDT选中", "1")),

                // ⚠ 排在这里：`BtnAddUdt` 刚把 UDT 列表填出一条（也是它唯一有内容的窗口）。
                SelInert("UdtList", 0, "OnUdtSelected（**代码里** `SelectionChanged +=` 接的）：`BtnAddUdt` 建的那条 "
                    + "**已经被自动选中**（`AddUdt` 末尾 `SelectListItemByTag`）⇒ 拨同一个下标不发通知；"
                    + "而 `BtnDeleteUdt` 之后列表空了。⚠ 夹具里只有一条 UDT ⇒ 拨不到别的项，"
                    + "这是**夹具形状**、不是接线缺失；⚠ 同样做不出负控"),

                Inert("BtnAddField", "它只清空字段名输入框、给下拉选第一项、把焦点移过去 —— 是「准备输入」，不落任何数据。"
                    + "⚠ 本步确实动了 `字段名框` 这个**输入面**分量吗？**没有**：此刻它本来就是空的"),

                Arranged(Moves("BtnCommitField", "**上一步清空的框，这一步先布景填上** —— 阶段 115 把它从"
                    + "「夹具形状」升级成真接线：处理器首行读字段名框，空则只写状态栏 return。"
                    + "⚠ 布景刻意写带前后空格的 `“ F1 ”`：`CommitField` 会 `Trim()` 之后存进模型，"
                    + "而框里留着的仍是带空格的原串 —— 这一点正是下一条选择族**唯一**的可观测见证"
                    + "（否则处理器把同名同值写回同一个框，读数一字不变）",
                    ("字段列表", "1")),
                    c => Control<TextBox>(c.Window, "TxtFieldName").Text = " F1 "),

                SelMoves("FieldList", 0, "OnFieldSelected（**代码里** `SelectionChanged +=` 接的）：上一步刚给 UDT "
                    + "加了一条字段，`RefreshFields` 重建列表**不带选中** ⇒ 拨得动 ⇒ 处理器把这条字段填进编辑区。"
                    + "🩸 见证就是那条带空格的原串被 `Trim` 之后的结果（`“ F1 ”` → `F1`）——"
                    + "选择族声明的分量必须是**处理器写的**，而这里处理器写的正是这个文本框",
                    ("字段名框", "F1")),

                Moves("BtnDeleteField", "**阶段 115 起够得着了**：上一条选择族把 `_selectedField` 指上了那条字段 ⇒ "
                    + "删得掉，字段列表 1 → 0。（此前它只能登记成「本序列没有选中字段的动作 ⇒ 提前 return」，"
                    + "那条 Inert 记的是**夹具形状**、不是接线）",
                    ("字段列表", "0")),

                Inert("BtnSaveUdtName", "输入框里是新建时的默认名（OnUdtSelected 填的）⇒ 写回的是同一个字符串，数据面读不出差别"),

                Moves("BtnDeleteUdt", "上一步新建的 UDT 仍被 _selectedUdt 指着（不依赖列表选中）⇒ 删得掉；"
                    + "删完列表中重建、选中随之清空 ⇒「UDT选中」归 0",
                    ("UDT", "0"), ("UDT列表", "0"), ("UDT选中", "0")),

                Inert("BtnCreateInstance", "UDT 刚被删光 ⇒ CboUdtInstanceType 已被重填成空列表、SelectedItem 为 null ⇒ 提前 return"),

                // 两个复选框：XAML 里既没有 Click= 也没有 IsCheckedChanged= —— 它们的值只在
                // SaveCurrentAlarm 里被**读走**（`_selectedAlarm.IsEnabled = ChkAlarmEnabled.IsChecked == true`），
                // 而那条路在本序列里够不着（见上面 BtnSaveAlarm）。
                Inert("ChkAlarmEnabled", "**根本没有接线**（XAML 里既无 `Click=` 也无 `IsCheckedChanged=`）："
                    + "翻转 IsChecked 本身不改任何数据面分量。⚠ 值只在 SaveCurrentAlarm 里被读走 ——"
                    + "退一步说，那条路在本序列里也够不着（_selectedAlarm 已在第 4 步置空）"),

                Inert("ChkAlarmRequiresAck", "同上：没有接线，翻转 IsChecked 本身不改任何数据面分量"),

                Never("BtnTitleClose", "点了会关掉宿主窗口本身，后续按钮就无从验证了（与四层判据同一处置）"),
            }),

        // ── 报警配置：夹具里 1 个组「罐区报警」，组内 1 条报警「液位高」 ──
        new("AlarmConfigView",
            d =>
            {
                // 变量下拉的来源是 Project.VariableManager（不是 depot.Variables）。
                // 不补一条，CboVariableId 就没有可选项、那条接线在运行期无从驱动。
                // Id 写死是为了让声明里的期望值可读（默认是随机 GUID）。
                d.Project.VariableManager.Variables.Add(
                    new Variable { Id = "var-level", Name = "液位", Address = "D100", DataType = "U16" });
                return new Views.AlarmConfigView(d.AlarmGroups, d.Project);
            },
            new[]
            {
                Num("分组", c => c.Depot.AlarmGroups.Count),
                Num("报警", c => c.Depot.AlarmGroups.Sum(g => g.Alarms.Count)),
                Num("分组列表", c => Items(c.Window, "GroupList")),
                Num("报警列表", c => Items(c.Window, "AlarmList")),
                // ── 「选择族」的眼睛（阶段 115 / 120）：都读**处理器写的**出口 ──
                Str("右列首条", c => FirstAlarmName(c.Window)),
                Str("编辑面板", c => Control<ScrollViewer>(c.Window, "EditPanel").IsVisible ? "开" : "关"),
                Str("报警名框", c => Control<TextBox>(c.Window, "TxtAlarmName").Text ?? ""),
                // 阶段 120：编辑面板字段处理器接线后，这几枚读的是**当前选中报警的模型字段**
                Str("严重级别", c => SelectedAlarm(c.Window)?.Severity.ToString() ?? "无"),
                Str("条件类型", c => SelectedAlarm(c.Window)?.Condition.Type.ToString() ?? "无"),
                Str("变量绑定", c => SelectedAlarm(c.Window)?.Condition.VariableId ?? "无"),
                Str("启用", c => (SelectedAlarm(c.Window)?.IsEnabled ?? false) ? "True" : "False"),
                Str("需确认", c => (SelectedAlarm(c.Window)?.RequiresAck ?? false) ? "True" : "False"),
                Voice("状态栏", c => StatusOf(c.Window)),
            },
            new[]
            {
                Moves("BtnAddGroup", "新建报警组：模型 +1、左列表 +1（新组被自动选中，右列表切到空组）。"
                    + "⚠ 阶段 120 起：**打开对话框时右列就已按默认组填好**（此前那次默认选中发生在订阅之前 ⇒ 右列恒空），"
                    + "所以本步右列 1 → 0、首条「液位高」→「无」—— 这两条此前不在表里，因为旧初态右列本来就是空的",
                    ("分组", "2"), ("分组列表", "2"), ("报警列表", "0"), ("右列首条", "无")),

                Moves("BtnAddAlarm", "报警加进**当前选中的那个组**（上一步新建的），总数 +1；右列表先显示该组的报警 ⇒ 0 → 1。"
                    + "⚠ 处理器末尾那句 RefreshGroupList() 又把左列表重建、选中重置回第 0 项 ⇒ 右列表**当场被换成**"
                    + "「罐区报警」的报警。🩸 `右列首条` 就是为这句话装的眼睛（阶段 115）：两个组各只有一条报警，"
                    + "计数 `报警列表` 这一栏**看不出右列换人了**，只有身份看得出。"
                    + "⚠ `报警名框` 也在这里被填上（`AddAlarm` 末尾那次选中会引发 SelectionChanged ⇒ "
                    + "`PopulateEditPanel` 把模型填进面板），而面板**随后又被 RefreshGroupList 关掉** ⇒ "
                    + "框里留着残值「Alarm 1」。这是真实行为：面板关着、框里却是上一条的值",
                    ("报警", "2"), ("报警列表", "1"), ("右列首条", "液位高"), ("报警名框", "Alarm 1")),

                // 这条是本层顺带量出来的一个**真实现象**（不是缺陷）：OnAddAlarm 末尾会
                // RefreshGroupList() 重建左列表 —— 那次重建把 GroupList 的选中重置到第 0 项，
                // 于是右列表被切回**第一个组**，刚选中的新报警没了。
                // ⇒ 紧接着点「删除报警」删不动，用户必须先在新组里点一下那条报警。
                Inert("BtnDeleteAlarm", "上一步 RefreshGroupList() 重建左列表时把右列表切回了第一个组，"
                    + "先前选中的新报警随之丢失 ⇒ 处理器提前 return。"
                    + "「新建完立刻删不掉」是真实的交互事实，不是接线断了"),

                // ── 两条「选择族」（阶段 115）：都排在 `BtnDeleteAlarm` **之后**，理由见第一条的 Why ──
                SelMoves("GroupList", 1, "OnGroupSelectionChanged（**代码里** `SelectionChanged +=` 接的，XAML 里没有）："
                    + "拨到第 2 项（`BtnAddGroup` 新建的那个组）⇒ 处理器 `RefreshAlarmList` 把右列换成**另一个组**的报警。"
                    + "🩸 本条声明的是 `右列首条`（**身份**）而不是 `报警列表`（计数）—— 两个组各只有一条 ⇒ "
                    + "计数在换组前后都是 1，**看不出接线在不在**（见 FirstAlarmName）。"
                    + "⚠ 它排在 `BtnDeleteAlarm` **之后**是必须的：排到前面，右列就会带着选中项进入那一步，"
                    + "把「新建完立刻删不掉」那条 Inert 悄悄变成「删得掉」",
                    ("右列首条", "Alarm 1"), ("报警列表", "1")),

                SelMoves("AlarmList", 0, "OnAlarmSelectionChanged（同上，代码接线）：右列此时有 1 条报警、"
                    + "**没有选中项**（`RefreshAlarmList` 每次重建都把 SelectedItem 清掉）⇒ 拨到第 0 项，"
                    + "处理器 `PopulateEditPanel` 把模型填进编辑面板。🩸 见证的两枚都是**处理器写的**："
                    + "`报警名框` 与 `编辑面板` 可见位。若只声明选中项，摘掉订阅这条声明照样绿 —— "
                    + "那是「手势自己写的」分量。"
                    + "⚠ 「严重级别 / 条件类型 / 变量绑定 / 启用 / 需确认」这五枚探针读的是**当前选中的那条报警**，"
                    + "选中之前读不到（值为「无」）⇒ 本步必须把它们一并声明，否则那五处会被记成「未声明的分量变了」",
                    ("编辑面板", "开"), ("报警名框", "Alarm 1"),
                    ("严重级别", "Warning"), ("条件类型", "HighLimit"), ("变量绑定", ""),
                    ("启用", "True"), ("需确认", "True")),

                // ── 编辑面板的**字段处理器**（阶段 120）：这些控件此前一个都没订阅 ──
                // 面板此刻是开着的（上一条刚选上 Alarm 1），四条声明直接驱动。
                // ⚠ 期望值必须与**当前值不同**，否则手势不引发事件、这条声明会假绿：
                // 报警默认严重级别就是 Warning（index 1），所以拨 index 2 = Critical。
                SelMoves("CboSeverity", 2, "OnAlarmFieldChanged（阶段 120 接线）：拨到 Critical ⇒ "
                    + "处理器把 `alarm.Severity` 写进模型。⚠ 拨 index 1 会**假绿** —— 报警默认就是 Warning，"
                    + "选中项没变 ⇒ 事件不触发、声明照样过",
                    ("严重级别", "Critical")),

                SelMoves("CboConditionType", 2, "OnConditionTypeChanged（阶段 120 接线）：拨到 BitTrigger ⇒ "
                    + "处理器写 `alarm.Condition.Type` 并刷新条件字段可见性。⚠ 默认是 HighLimit（index 0）",
                    ("条件类型", "BitTrigger")),

                SelMoves("CboVariableId", 0, "OnAlarmFieldChanged（阶段 120 接线）：拨到唯一那条变量 ⇒ "
                    + "处理器把 `alarm.Condition.VariableId` 写成该变量的 Id（= 夹具里写死的 `var-level`）",
                    ("变量绑定", "var-level")),

                // 两个复选框（阶段 120 起 `IsCheckedChanged` 已接线 ⇒ 从 Inert 转 Moves）
                Moves("ChkEnabled", "阶段 120 接线（`ChkEnabled.IsCheckedChanged += OnAlarmFieldChanged`）："
                    + "手势先翻转 IsChecked（true → false）再引发事件 ⇒ 处理器把 `alarm.IsEnabled` 写成 false。"
                    + "⚠ 此前登记为 Inert，理由是「本序列够不着回写路径」—— 那条路径当时**从未被订阅**，"
                    + "任何序列都够不着；现在读的是模型字段，接线断了这条就红",
                    ("启用", "False")),

                Moves("ChkRequiresAck", "同上：`alarm.RequiresAck` true → false",
                    ("需确认", "False")),

                Moves("BtnDeleteGroup", "删掉那个新组（上一条选择族把它选上了）：**组里的报警跟着组一起走**"
                    + "（模型里报警数也减 1）；删完 RefreshGroupList 把选中重置到剩下的那个组，"
                    + "右列随之换成它的报警 ⇒ `右列首条` 再变一次。⚠ `报警名框` 此刻**不变**"
                    + "（`RefreshAlarmList` 只清选中、不回填文本框）—— 面板已关、框里留着上一步的残值。"
                    + "这是真实行为，不是本层读错了：声明里不写它，正好把「没动」也钉住。"
                    + "⚠ 阶段 120：这一步把右列的选中项清掉了，所以上面五枚「读当前选中报警」的字段探针"
                    + "一并归零（三条读不到对象 ⇒「无」，两个布尔取不到 ⇒ False）—— 它们是真实变化，必须声明",
                    ("分组", "1"), ("分组列表", "1"), ("报警", "1"),
                    ("右列首条", "液位高"), ("编辑面板", "关"),
                    ("严重级别", "无"), ("条件类型", "无"), ("变量绑定", "无"),
                    ("启用", "False"), ("需确认", "False")),

                Never("BtnTitleClose", "同上：点了会关掉宿主窗口"),
            }),

        // ── 屏幕管理：夹具里 2 个屏（主画面 / 报警画面） ──
        new("ScreenManagerView",
            d => new Views.ScreenManagerView(d.Project, d.Screen),
            new[]
            {
                Num("屏幕", c => c.Depot.Project.Screens.Count),
                Num("屏幕列表", c => Items(c.Window, "ScreenList")),
                Num("公开屏", c => c.Depot.Project.Screens.Count(s => s.IsPublicScreen)),
                Str("可见", c => c.Window.IsVisible ? "是" : "否"),
                // ── 三枚「选择族」的眼睛（阶段 115）──
                // 「公开开关」读的是**控件**、但它由 `OnSelectionChanged` 写 ⇒ 是处理器出口
                //   （`ChkPublic` 那一条自己的手势只翻转 IsChecked，所以它靠模型分量「公开屏」见证）。
                // 另两枚都是**模型计数**，与界面语言、列表顺序都无关。
                Num("公开开关", c => Control<CheckBox>(c.Window, "ChkPublic").IsChecked == true ? 1 : 0),
                Num("非查看者屏", c => c.Depot.Project.Screens
                    .Count(s => s.PublicAccess != ScreenPublicAccess.Viewers)),
                // ⚠ 口径是「等于 **Fill**」而不是「非 None」：首跑实测初值就是 2 ——
                // `ScreenDefaultsConfig.DefaultAdaptiveMode = Fit`（**不是** None），`ScreenFactory`
                // 把它带进每一条新屏 ⇒「非默认」这个说法在这里根本不成立。
                // 数一个**具体模式**才既有初值 0、又不会被 `BtnAddScreen` 顺带抬起来（新屏也是 Fit）。
                Num("自适应Fill屏", c => c.Depot.Project.Screens
                    .Count(s => s.AdaptiveMode == ScreenAdaptiveMode.Fill)),
                Voice("状态栏", c => StatusOf(c.Window)),
            },
            new[]
            {
                // 它排在最前：XAML 里 ChkPublic 用 IsCheckedChanged= 接线（**没有** Click=），
                // 是本层「先翻转再引发 Click」那个手法的唯一见证者。
                Moves("ChkPublic", "构造时 RefreshList 已经选中了当前屏 ⇒ OnPublicChanged 有屏可写："
                    + "把该屏的 IsPublicScreen 置为真",
                    ("公开屏", "1"),
                    // ⚠ 这一枚是**手势自己**写的（ToggleButton 的 IsChecked 被翻转），不是处理器写的
                    // ⇒ 它对「订阅在不在」零信息；本条的电报由模型分量「公开屏」发。
                    // 但它必须被记账 —— 双向口径里「没声明却动了」照样报红。
                    ("公开开关", "1")),

                // ── 三条「选择族」（阶段 115）：顺序即语义，排在这里有三个前置 ──
                //   ① `CboPublicAccess` 要写得动模型，前提是 `screen.IsPublicScreen` 为真 —— 只有上一条能做到；
                //   ② `CboAdaptiveMode` 要 `ScreenList.SelectedItem` 非空（构造时 RefreshList 已选中）；
                //   ③ `ScreenList` 拨到另一条屏之后 `ChkPublic` 会被处理器改掉，必须在①②之后。
                SelMoves("CboPublicAccess", 0, "OnPublicChanged（XAML 里 `SelectionChanged=`）：`screen.PublicAccess` "
                    + "被写成 `Public`（Tag→枚举的映射在处理器里）⇒「非查看者屏」0 → 1。"
                    + "⚠ 它的前置是 `ChkPublic.IsChecked == true`（上面那条刚拨成真）—— 否则处理器会跳掉"
                    + "那一段赋值，本分量一动不动。"
                    + "🩸 见证用 `screen.PublicAccess` 这个**模型字段**；不用 `CboPublicAccess.IsEnabled`"
                    + "那种（两种情况下它都可能为真，读不出差别）",
                    ("非查看者屏", "1")),

                SelMoves("CboAdaptiveMode", 2, "OnAdaptiveModeChanged（**代码里** `SelectionChanged +=` 接的，"
                    + ".axaml 上一字没有）⇒ `screen.AdaptiveMode = Fit`（Tag \"Fit\"）⇒「自适应Fill屏」0 → 1。⚠ 拨 index 1（Fit）看不出差别 —— 那正是夹具与工程默认值本来就是的"
                    + "那个值（`ScreenDefaultsConfig.DefaultAdaptiveMode = Fit`）。"
                    + "⚠ 它是本表里第一条「**名字扫描看不见的接线**」：`s115-hosts.py` 只扫 .axaml 时它显示"
                    + "「无事件接线」，补上 code-behind 的 `+=` 才露出来 —— 面扩的一半价值在这里。"
                    + "⚠ 它顺手 `RefreshList(screen)` 重建左列表（选中项被重新装上、`OnSelectionChanged` "
                    + "再跑一遍，但程序性写入被 `_isUpdating` 护着 ⇒「公开开关」不变）",
                    ("自适应Fill屏", "1")),

                SelMoves("ScreenList", 1, "OnSelectionChanged（XAML 里 `SelectionChanged=`）：拨到第 2 条屏"
                    + "（报警画面，**不是**公共屏）⇒ 处理器把选中屏的属性载入控件："
                    + "`ChkPublic.IsChecked = screen.IsPublicScreen` = false ⇒「公开开关」1 → 0。"
                    + "🩸 见证刻意选 `ChkPublic.IsChecked` 而**不是** `ScreenList.SelectedItem` 那种"
                    + "—— 后者是手势自己拨的，摘掉订阅照样变（零信息）。"
                    + "⚠ 这句赋值在 `_isUpdating = true` 里 ⇒ 不回头写模型：「公开屏」仍是 1、"
                    + "「非查看者屏」仍是 1（模型那两枚一动不动，正是「载入控件 ≠ 改数据」的见证）",
                    ("公开开关", "0")),

                Moves("BtnAddScreen", "新建屏幕走 ScreenFactory（尺寸/自适应模式/背景色一起带上）并加进工程",
                    ("屏幕", "3"), ("屏幕列表", "3")),

                Never("BtnTitleClose", "同上：点了会关掉宿主窗口"),

                // 排在最后：它是本层唯一一条「点了会关窗」的**真路**（所以不能被 Skip 掩盖），
                // 关窗后本宿主不再有其他要点的按钮。
                Moves("BtnSwitchScreen", "切换屏幕 = 发 ScreenSwitchRequested + 关窗（主窗口接住事件去换屏）——"
                    + "效果的证据就是窗口关掉了",
                    ("可见", "否"), ("屏幕", "3")),
            }),

        // ── 设置对话框：**第一个「设置面」宿主** ──
        // 与前三处的形状差异：数据面**不在模型集合里**，而在 `SettingsService` / `AppThemeService`
        // 两个**服务对象**上 —— 它们就是 `WindowDepot` 里那两个实例，与窗口共享同一个引用，
        // 所以「点一下之后服务里的值变成什么」直接在 Probe 里读得到。
        // ⚠ 这批 26 条里 6 条的字段在 `ConfigFieldInventory` 里登记为「面板上有、其实不生效」
        // （ChkAutoHide / ChkAnimations / ChkShowRulers / ChkLivePreview / ChkToolTips / ChkPreviewOverlay）——
        // 本层判的是「**接线**真的把值写进去了」，与「下游消不消费」是**正交的两层**：
        // 判据说接线真实，清单说不生效，两者不互相撒谎（见类注释「不抓什么」第 5 条）。
        // 夹具初态（`UserSettings()` 默认 + 无 settings.json）：除 `AutoHidePanels` 为 false 外全为 true；
        // 主题服务默认 `Dark`。
        new("SettingsDialog",
            d => new Views.SettingsDialog(d.Settings, d.Theme),
            new[]
            {
                Str("主题", c => c.Depot.Theme.Current.ToString()),
                Num("布局·自动隐藏", c => c.Depot.Settings.Layout.AutoHidePanels ? 1 : 0),
                Num("交互·动画", c => c.Depot.Settings.Interaction.EnableAnimations ? 1 : 0),
                Num("交互·吸附", c => c.Depot.Settings.Interaction.EnableSnapToGrid ? 1 : 0),
                Num("交互·网格", c => c.Depot.Settings.Interaction.ShowGrid ? 1 : 0),
                Num("交互·标尺", c => c.Depot.Settings.Interaction.ShowRulers ? 1 : 0),
                Num("交互·实时预览", c => c.Depot.Settings.Interaction.EnableLivePreview ? 1 : 0),
                Num("交互·自动保存", c => c.Depot.Settings.Interaction.EnableAutoSave ? 1 : 0),
                Num("显示·提示", c => c.Depot.Settings.Display.ShowToolTips ? 1 : 0),
                Num("显示·预览叠层", c => c.Depot.Settings.Display.ShowPreviewOverlay ? 1 : 0),
                Num("浏览器·总开关", c => c.Depot.Settings.Explorer.EnableContextMenu ? 1 : 0),
                Num("浏览器·新建", c => c.Depot.Settings.Explorer.ShowCreate ? 1 : 0),
                Num("浏览器·打开", c => c.Depot.Settings.Explorer.ShowOpenScreen ? 1 : 0),
                Num("浏览器·启动屏", c => c.Depot.Settings.Explorer.ShowStartupScreen ? 1 : 0),
                Num("浏览器·公开页", c => c.Depot.Settings.Explorer.ShowPublicPage ? 1 : 0),
                Num("浏览器·重命名", c => c.Depot.Settings.Explorer.ShowRename ? 1 : 0),
                Num("浏览器·复制", c => c.Depot.Settings.Explorer.ShowDuplicate ? 1 : 0),
                Num("浏览器·删除", c => c.Depot.Settings.Explorer.ShowDelete ? 1 : 0),
                Num("浏览器·剪切板", c => c.Depot.Settings.Explorer.ShowCopyPaste ? 1 : 0),
                Num("浏览器·移层", c => c.Depot.Settings.Explorer.ShowMoveLayer ? 1 : 0),
                // ── 阶段 187：自动导出（`#87` 的设置面）──
                Num("自动导出·开关", c => c.Depot.Settings.AutoExport.Enabled ? 1 : 0),
                Num("自动导出·间隔", c => c.Depot.Settings.AutoExport.IntervalMinutes),
                // ── 「选择族」的眼睛（阶段 115）──
                // 分类列表写的是 `Panel*.IsVisible` —— 那不是模型、也不是服务，是本宿主唯一的界面出口。
                Str("主题面板", c => Control<Panel>(c.Window, "PanelTheme").IsVisible ? "开" : "关"),
                Str("布局面板", c => Control<Panel>(c.Window, "PanelLayout").IsVisible ? "开" : "关"),
                Str("显示·预览分辨率", c => c.Depot.Settings.Display.PreviewResolution),
                // ⚠ 这一枚只说明**环境事实**（本机字体集大小），不是行为分量：它永不变化。
                // 之所以要写出来，是为了让下一条 SelInert 的环境前提**可见**（而不是只在注释里）。
                Num("字体项数", c => Control<ComboBox>(c.Window, "CmbDefaultFont").Items.Count),
                // ⚠ 本宿主**没有** Voice("状态栏") 分量：`SettingsDialog` 里根本没有 `StatusText` 具名控件
                // （本层第一次跑就抛在这里）—— 它是对话框、不是带状态栏的面板。不设影子分量也不影响
                // 覆盖面：数据面 20 个分量已经全在 `SettingsService` / `AppThemeService` 上。
            },
            new[]
            {
                // 三个主题按钮（ToggleButton，Click 声明在 XAML 里、Tag 也是）必须**按能改变 Current 的顺序**
                // 点：初态 Dark ⇒ 先点 Light（真的变），再点 Dark（真的变回来），最后 Ocean。
                // 若把 `BtnThemeDark` 放在最前，它会写一个**已经是** Dark 的值 ⇒ 声明「等于点击前的值」
                // ⇒ 触发「Moves 却没动」的自检。⚠ 这正是那条自检存在的理由。
                Moves("BtnThemeLight", "OnThemePresetClicked 读 e.Source(Button).Tag 里的枚举名 ⇒ "
                    + "AppThemeService.SetTheme(Light) ⇒ Current Dark → Light",
                    ("主题", "Light")),

                Moves("BtnThemeDark", "同上，Tag=Dark：Current Light → Dark（这一步真的变了，不是空转）",
                    ("主题", "Dark")),

                Moves("BtnThemeOcean", "同上，Tag=Ocean：Current Dark → Ocean",
                    ("主题", "Ocean")),

                // 下面 19 条都是 CheckBox：XAML 里**没有** Click=，接线一律在 WireEvents 的
                // `IsCheckedChanged += (_, _) => _settings.X.SetY(Chk.IsChecked == true)` 上。
                // 判据的 Gesture 先翻转 IsChecked（ToggleButton.OnClick 真正做的事）⇒ 值 = 取反后的 true/false。
                // 每条点名的都是它自己那一项设置。
                Moves("ChkAutoExport", "WireEvents：AutoExport.SetEnabled(IsChecked==true)；"
                    + "夹具默认 false ⇒ 翻转后写入 true", ("自动导出·开关", "1")),

                Moves("ChkAutoHide", "WireEvents：Layout.SetAutoHidePanels(IsChecked==true)；"
                    + "夹具默认 false ⇒ 翻转后写入 true", ("布局·自动隐藏", "1")),

                Moves("ChkAnimations", "WireEvents：Interaction.SetEnableAnimations(IsChecked==true)；默认 true ⇒ false",
                    ("交互·动画", "0")),

                Moves("ChkSnapToGrid", "WireEvents：Interaction.SetEnableSnapToGrid(IsChecked==true)；默认 true ⇒ false",
                    ("交互·吸附", "0")),

                Moves("ChkShowGrid", "WireEvents：Interaction.SetShowGrid(IsChecked==true)；默认 true ⇒ false",
                    ("交互·网格", "0")),

                Moves("ChkShowRulers", "WireEvents：Interaction.SetShowRulers(IsChecked==true)；默认 true ⇒ false",
                    ("交互·标尺", "0")),

                Moves("ChkLivePreview", "WireEvents：Interaction.SetEnableLivePreview(IsChecked==true)；默认 true ⇒ false",
                    ("交互·实时预览", "0")),

                Moves("ChkAutoSave", "WireEvents：Interaction.SetEnableAutoSave(IsChecked==true)；默认 true ⇒ false",
                    ("交互·自动保存", "0")),

                Moves("ChkToolTips", "WireEvents：Display.SetShowToolTips(IsChecked==true)；默认 true ⇒ false",
                    ("显示·提示", "0")),

                Moves("ChkPreviewOverlay", "WireEvents：Display.SetShowPreviewOverlay(IsChecked==true)；默认 true ⇒ false",
                    ("显示·预览叠层", "0")),

                Moves("ChkExplorerCtx", "WireEvents：Explorer.SetEnableContextMenu(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·总开关", "0")),

                Moves("ChkCtxCreate", "WireEvents：Explorer.SetShowCreate(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·新建", "0")),

                Moves("ChkCtxOpen", "WireEvents：Explorer.SetShowOpenScreen(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·打开", "0")),

                Moves("ChkCtxStartup", "WireEvents：Explorer.SetShowStartupScreen(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·启动屏", "0")),

                Moves("ChkCtxPublic", "WireEvents：Explorer.SetShowPublicPage(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·公开页", "0")),

                Moves("ChkCtxRename", "WireEvents：Explorer.SetShowRename(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·重命名", "0")),

                Moves("ChkCtxDuplicate", "WireEvents：Explorer.SetShowDuplicate(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·复制", "0")),

                Moves("ChkCtxDelete", "WireEvents：Explorer.SetShowDelete(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·删除", "0")),

                Moves("ChkCtxCopyPaste", "WireEvents：Explorer.SetShowCopyPaste(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·剪切板", "0")),

                Moves("ChkCtxMoveLayer", "WireEvents：Explorer.SetShowMoveLayer(IsChecked==true)；默认 true ⇒ false",
                    ("浏览器·移层", "0")),

                // ── 三条「选择族」（阶段 115）──
                SelMoves("CategoryList", 1, "OnCategorySelected（**代码里** `SelectionChanged +=` 接的）："
                    + "拨到第 2 项（Tag `layout`）⇒ 处理器按 Tag 把主题面板关掉、把布局面板打开。"
                    + "⚠ 它写的是 `Panel*.IsVisible` —— 界面出口，不是模型；XAML 初值 主题=True / 布局=False。"
                    + "⚠ 初态**没有选中项**：`CategoryList` 的项是代码填的（`PopulateCategoryList` 只 "
                    + "`Items.Clear()` + 追加 7 项、不设 SelectedIndex），而 XAML 里那个 `SelectedIndex=\"0\"` "
                    + "是在**空列表**上设的 ⇒ 装配期没有任何处理器跑过",
                    ("主题面板", "关"), ("布局面板", "开")),

                SelMoves("CmbPreviewRes", 2, "Display.SetPreviewResolution(Tag)（**代码里** `SelectionChanged +=` "
                    + "接的 lambda）⇒ 拨到第 3 项（Tag `1024x600`）⇒ 服务里 `PreviewResolution` 从空串写成它。"
                    + "⚠ 必须拨到一个**真不同**的档：`PopulateDisplayControls` 按服务里的值找下标，"
                    + "找不到就 `SelectedIndex = 0`（Tag 空串）⇒ 拨 0 是「写入同值」",
                    ("显示·预览分辨率", "1024x600")),

                SelInert("CmbDefaultFont", 0, "Display.SetDefaultFont(Tag)（**代码里** `SelectionChanged +=` "
                    + "接的 lambda）：① 它的项来自 `FontManager.Current.SystemFonts` —— **项数与内容都是"
                    + "环境相关的**（本机字体集），把「拨到第 N 项」当稳定手势不成立；② 服务里的值无论如何都非空"
                    + "（默认 `Noto Sans CJK SC`）⇒ 抗环境的口径「空 / 非空」读不出差别。"
                    + "⚠ 这是**环境形状**、不是接线缺失；⚠ 也做不出负控。"
                    + "⚠ 要给它真见证，产品侧得先让字体值有一个**与字体集无关**的可观测落点"),

                // 它是**真的接上了线**的（WireEvents 里 `BtnApply.Click += OnApplyClicked`），
                // 但 OnApplyClicked 的方法体只有一句注释（「所有设置项均为即时保存，Apply 仅提供确认反馈」）
                // ⇒ 本条 Inert 记的是「**处理器跑了、什么都不做**」，不是「没接线」—— 两者形状相同、含义不同。
                Inert("BtnApply", "接线是有的（BtnApply.Click += OnApplyClicked），但处理器体是空的 —— "
                    + "源码注释明写「所有设置项均为即时保存，Apply 仅提供确认反馈」⇒ 点它不改任何数据面分量"),

                // 三条都走 Close()：把它们全登记成 Never，所以本序列不会关窗、后面的按钮都验得到。
                Never("BtnPickAutoExportDirectory",
                    "阶段 187 新增的「选择目录…」：走 StorageProvider.OpenFolderPickerAsync，"
                    + "headless 下退化为空集合 ⇒ 观测到的是**平台桩**而不是接线"
                    + "（接线与行为由 AutoExportSettingsUiTests 的判据钉住）。"),
                Never("BtnOk", "OnOkClicked 就是 Close() ⇒ 点了会关掉对话框本身，后续按钮无从验证"),
                Never("BtnCancel", "OnCancelClicked 就是 Close() ⇒ 同上"),
                Never("BtnTitleClose", "ChromeWindow 标题栏的关闭按钮（XAML 里 Click=OnTitleCloseClicked）⇒ 同上"),
            }),

        // ── 工程设置：**第二种设置面**，而且比 `SettingsDialog` 多两层结构 ──
        // 它比上一个宿主多出来的两个形状：
        //   ① **「只标脏、不当场写」的复选框**：10 个 `ChkXxx` 走 `AttachChangeHandler`，
        //      那条路只做 `_hasChanges = true`（一个 private 字段），**一个模型字段都不写**。
        //      它们的值要到 `SaveToProject()` 里才被**读走**（`_project.ExportConfig.MergeResources =
        //      ChkMergeResources.IsChecked ?? true` 这一类）。⇒ 本表的处置是**两段式**：
        //      这 10 条各自登记 `Inert`（点下去当场数据面一字不变，这是**事实**），
        //      再由 `BtnApply` 那条把它们的值**一次性见证**（`SaveToProject` 会把它们写进 `_project`）。
        //      ⇒ 这一对声明合起来才说清「`_hasChanges` 不是空转、也不当场落盘」。
        //   ② **草稿语义的集合编辑**：运行时用户表改的是 `_runtimeUsersDraft`（**草稿**），
        //      模型 `_project.SecurityConfig.RuntimeUsers` 要等 `ApplyInternal()` 才整体写回。
        //      ⇒ `BtnAddRuntimeUser` / `BtnRemoveRuntimeUser` 只看得见**窗口列表**的变化，
        //      而 `BtnApply` 才让 `用户·模型` 从 0 变 1。
        // ⚠ 本宿主**没有** `StatusText` 具名控件 ⇒ 与 `SettingsDialog` 一样不设 Voice 分量。
        // ⚠ 夹具初态：`WindowDepot.Project` 是 `new HmiProject()`（用户表空、屏幕尺寸 = 默认尺寸）。
        new("ProjectSettingsView",
            d => new Views.ProjectSettingsView(d.Project),
            new[]
            {
                // 10 个「只标脏」的复选框各自的模型落点（`SaveToProject` 的读点）
                Num("通讯·写合并", c => c.Depot.Project.VariableManager.EnableWriteCoalescing ? 1 : 0),
                Num("运行·崩溃转储", c => c.Depot.Project.RuntimeConfig.EnableCrashDump ? 1 : 0),
                Num("运行·重启", c => c.Depot.Project.RuntimeConfig.EnableHmiReboot ? 1 : 0),
                Num("导出·调试符号", c => c.Depot.Project.ExportConfig.GenerateDebugSymbols ? 1 : 0),
                Num("导出·注释", c => c.Depot.Project.ExportConfig.IncludeComments ? 1 : 0),
                Num("导出·合并资源", c => c.Depot.Project.ExportConfig.MergeResources ? 1 : 0),
                Num("导出·LGPL", c => c.Depot.Project.ExportConfig.IncludeLgplDisclaimer ? 1 : 0),
                Num("安全·审计", c => c.Depot.Project.SecurityConfig.EnableAuditLog ? 1 : 0),
                Num("安全·自动登出", c => c.Depot.Project.SecurityConfig.AutoLogoutEnabled ? 1 : 0),
                Num("安全·防篡改", c => c.Depot.Project.SecurityConfig.EnableTamperDetection ? 1 : 0),
                // 用户表两条通道：草稿（窗口列表）vs 模型（只有 ApplyInternal 写）
                Num("用户草稿列表", c => Items(c.Window, "RuntimeUserList")),
                Num("用户·模型", c => c.Depot.Project.SecurityConfig.RuntimeUsers?.Count ?? 0),
                // 屏幕尺寸：`BtnApplySizeToAll` 唯一的写入面
                Num("屏宽", c => (int)c.Depot.Project.Screens[0].Width),
                Num("屏高", c => (int)c.Depot.Project.Screens[0].Height),
                // ── 「选择族」的眼睛（阶段 115）──
                Str("基本信息面板", c => Control<Panel>(c.Window, "PanelBasicInfo").IsVisible ? "开" : "关"),
                Str("显示设置面板", c => Control<Panel>(c.Window, "PanelDisplaySettings").IsVisible ? "开" : "关"),
                // ⚠ 这两枚读的是**数字框**（控件），不是 `屏宽`/`屏高`（模型）—— 预置尺寸改的是控件，
                // 模型要等 `BtnApplySizeToAll` / `BtnApply`。选那两枚会把 CmbScreenPreset 那条写成恒真。
                // ⚠ 类型必须是 `FANumberBox`：它**不是** `NumericUpDown` 的子类（首跑实测抛
                // 「Expected control 'NumScreenWidth' to be 'Avalonia.Controls.NumericUpDown but it was
                // 'FluentAvalonia.UI.Controls.FANumberBox'」）⇒ `Control<T>` 的 `is T` 过不去。
                // ⚠ 它的 `.Value` 是 **非可空的 `double`**（二次编译报
                // `CS0019: 运算符“??”无法应用于“double”和“int”` 即是证据）—— 别照抄 `SettingsDialog` 里
                // 那种 `Value ?? 0`：那边 `NumLeftPanel` 是 **Avalonia 的 `NumericUpDown`**，两种控件的成员形状不同。
                Num("屏宽框", c => (int)Control<FluentAvalonia.UI.Controls.FANumberBox>(
                    c.Window, "NumScreenWidth").Value),
                Num("屏高框", c => (int)Control<FluentAvalonia.UI.Controls.FANumberBox>(
                    c.Window, "NumScreenHeight").Value),
                Str("硬件校验", c => BannerState(c.Window, "TxtHardwareValidation")),
                // ⚠ 环境事实（部署目录里 `firmware/*/firmware.json` 有几份），不是行为分量：它永不变化。
                Num("硬件目标项数", c => Control<ComboBox>(c.Window, "CmbHardwareTarget").ItemCount),
                Num("用户角色随下拉", c => ModelRoleFollowsCombo(c.Depot, c.Window)),
                // ⚠ 写这个框的**只有**两处，两处都是选择处理器的下游：
                //   ① ShowRuntimeUserDetail（选中某条 ⇒ 回填）；② SetRuntimeUserDetailEnabled(false)
                //   （选中被清空 ⇒ 清框）。读「空 / 非空」不读原文：里面装的是
                //   NextDefaultUsername 生成出来的用户名 —— 那是**生成物**，冻它等于冻一份实现细节。
                Str("用户名框", c => BoxState(c.Window, "TxtRuntimeUserName")),
            },
            new[]
            {
                // ── 四条「选择族」（阶段 115）：分类 / 型号 / 预置尺寸 / 用户角色 ──
                //   前三条都排在所有按钮之前：它们各自只动**界面出口**（面板可见位、校验行、两个数字框），
                //   与后面 19 条按钮不共享任何分量，因此顺序不影响它们。
                //   第四条（CmbRuntimeUserRole）必须排到 `BtnAddRuntimeUser` **之后** —— 它的前置是
                //   「草稿里有一条且被选中」；而它的见证刻意不在自己身上（见那条 SelInert 的 Why）。
                SelMoves("CategoryList", 1, "ShowPanel(CategoryList.SelectedIndex)（**代码里** "
                    + "`SelectionChanged +=` 接的 lambda）：拨到第 2 项（display）⇒ 关掉基本信息面板、"
                    + "打开显示设置面板。⚠ `InitializeCategoryList` 只填项、**不设选中**"
                    + "（末尾那句是显式 `ShowPanel(0)`）⇒ 初态 `SelectedIndex` 是 -1，拨到 1 一定引发事件",
                    ("基本信息面板", "关"), ("显示设置面板", "开")),

                SelMoves("CmbScreenPreset", 0, "CmbScreenPreset_SelectionChanged（XAML 里 `SelectionChanged=`）："
                    + "拨到第 1 项（480 × 272）⇒ 处理器把宽高两个数字框写成 480 / 272。"
                    + "🩸 见证刻意选**两个数字框**而不是 `屏宽`/`屏高`（那两枚读 `_project`）—— "
                    + "预置尺寸只改控件、**模型要等 `BtnApplySizeToAll`**（草稿语义的又一处）；"
                    + "选那两枚会写成一条恒真的声明。⚠ 初值 1024 / 600：`SelectedIndex = 3` 是装配期设的"
                    + "（处理器当时还没订阅），框里的值是 `LoadProjectIntoUi` 写的。"
                    + "⚠ 它同时**解锁**了后面 `BtnApplySizeToAll` 那条（此前它只能记成「写入同值」）",
                    ("屏宽框", "480"), ("屏高框", "272")),

                SelMoves("CmbHardwareTarget", 0, "ValidateHardwareTarget（**代码里** `SelectionChanged +=` 接的）："
                    + "拨到第 1 个型号 ⇒ 处理器重跑 `ValidateProject` 并把结果写进那条校验行。"
                    + "⚠ 这条手势**能被拨动**的前提是「初态没有选中项」：`LoadProjectIntoUi` 用 "
                    + "`_project.HardwareTargetId`（夹具里是 null）去找下拉项，找不到 ⇒ `SelectedItem` 为 null "
                    + "⇒ 装配期那次 `ValidateHardwareTarget()` 走的是 `target == null` 那一支（把行藏起来）。"
                    + "⚠ 项数由**部署目录**决定（见 `硬件目标项数` 那枚探针）：测试输出里只有 `firmware/t113` 一份"
                    + "⇒ 只有一项，所以这里拨的是**唯一**那一项、靠的就是上面那个 null 初值",
                    ("硬件校验", "非空")),

                // ── 10 条「只标脏」的复选框：当场一字不变（`_hasChanges` 是 private，本层读不到，
                //    它的值要等 `BtnApply` 才显形）⇒ 理由写的是「点下去确实什么都没写」，
                //    而**不是**「没接线」。⚠ 反过来：若有人给它们接上写模型的线，这 10 条会当场变红
                //    逼人改声明 —— 这正是它们该有的行为。
                Inert("ChkWriteCoalescing", "AttachChangeHandler 只做 `_hasChanges = true`（private 字段、"
                    + "当场没有任何模型字段被写）；它的值由 SaveToProject 读走，本序列在 BtnApply 那条见证"),
                Inert("ChkCrashDump", "同上：只标脏。落点在 `RuntimeConfig.EnableCrashDump`（见 BtnApply 的声明）"),
                Inert("ChkEnableReboot", "同上：只标脏。落点在 `RuntimeConfig.EnableHmiReboot`"),
                Inert("ChkDebugSymbols", "同上：只标脏。落点在 `ExportConfig.GenerateDebugSymbols`"),
                Inert("ChkIncludeComments", "同上：只标脏。落点在 `ExportConfig.IncludeComments`"),
                Inert("ChkMergeResources", "同上：只标脏。落点在 `ExportConfig.MergeResources`"),
                Inert("ChkLicense", "同上：只标脏。落点在 `ExportConfig.IncludeLgplDisclaimer`"),
                Inert("ChkAuditLog", "同上：只标脏。落点在 `SecurityConfig.EnableAuditLog`"),
                Inert("ChkAutoLogout", "同上：只标脏。落点在 `SecurityConfig.AutoLogoutEnabled`"),
                Inert("ChkTamper", "同上：只标脏。落点在 `SecurityConfig.EnableTamperDetection`"),

                // ── 同源的 14 条下拉：同一个「变更跟踪」辅助函数的 `cmb` 分支 ──
                //   ⚠ 它们排在这里（而不是任意处）只有一个理由：与上面 10 个复选框是**同一族**，
                //   放一起读得出来。选哪一项都不影响其它声明 —— 处理器只写 `_hasChanges`。
                SelInert("CmbLvglVersion", 1, DirtyOnly("`_project.LvglVersion`")),
                SelInert("CmbColorDepth", 1, DirtyOnly("`ScreenDefaultsConfig.DefaultColorDepth`")),
                SelInert("CmbOrientation", 1, DirtyOnly("`ScreenDefaultsConfig.DefaultOrientation`")),
                SelInert("CmbAdaptiveMode", 1, DirtyOnly("`ScreenDefaultsConfig.DefaultAdaptiveMode`")),
                SelInert("CmbRecoveryMode", 1, DirtyOnly("`VariableManager.ConnectionRecoveryMode`")),
                SelInert("CmbLogLevel", 1, DirtyOnly("`RuntimeConfig.LogLevel`")),
                SelInert("CmbBootScreen", 1, DirtyOnly("`RuntimeConfig.BootScreenId`")),
                SelInert("CmbTargetLanguage", 1, DirtyOnly("`ExportConfig.TargetLanguage`")),
                SelInert("CmbBuildSystem", 1, DirtyOnly("`ExportConfig.GenerateBuildSystem`")),
                SelInert("CmbCodeStyle", 1, DirtyOnly("`ExportConfig.CodeStyle`")),
                SelInert("CmbNaming", 1, DirtyOnly("`ExportConfig.ResourceNaming`")),
                SelInert("CmbOptimization", 1, DirtyOnly("`ExportConfig.CodeGenOptimization`")),
                SelInert("CmbOperationMode", 1, DirtyOnly("`SecurityConfig.OperationMode`")),
                SelInert("CmbLockScreen", 1, DirtyOnly("`SecurityConfig.LockScreenId`")),

                // ── 用户表：先加一个（草稿 +1，列表重建并自动选中）──
                Moves("BtnAddRuntimeUser", "OnAddRuntimeUser 往**草稿**里加一条（用户名走 NextDefaultUsername、"
                    + "口令留空、默认启用），然后 RefreshRuntimeUserListItems 重建列表 ⇒ 窗口列表 +1。"
                    + "⚠ 模型此刻**不动**（草稿语义）—— 模型要等 BtnApply。"
                    + "⚠ 「用户名框」非空也是这一步的结果，而且它**只可能是订阅在的功劳**："
                    + "末尾 `SelectRuntimeUser(user)` 只把 `SelectedItem` 装上，回填详情靠的是 "
                    + "`SelectionChanged`（作者在 `SelectRuntimeUser` 里显式补的那一次只覆盖 `user == null` 那一支）"
                    + "⇒ 摘掉 `RuntimeUserList` 的订阅，这个框会留在「空」⇒ 本条报红",
                    ("用户草稿列表", "1"), ("用户名框", "非空")),

                // ── 三级依赖的复选框：改的是**草稿对象**，本层没有它的出口 ──
                Inert("ChkRuntimeUserEnabled", "OnRuntimeUserEnabledChanged 改的是**草稿对象**的 IsEnabled"
                    + "（上一步新建的那个用户，默认 true ⇒ 翻转后 false）⇒ 但本层分量的两个出口"
                    + "（窗口列表**计数**、模型）都不反映单个条目的启用位。"
                    + "⚠ 这条是「**未被观测**」而不是「没动作」，与阶段 107 记的 BtnSaveAlarm 同形。"
                    + "它的真效果要等 BtnApply 把草稿写进模型之后才可能读到（但那时已是同一个值，读不出差别）"),

                // ── 密码按钮：输入框为空 ⇒ 走「需要密码」分支，只写提示文案 ──
                Inert("BtnSetRuntimeUserPassword", "处理器的第一个分支就是「password.Length == 0 ⇒ 只把"
                    + "『需要密码』写进提示行并 return」——夹具里没人往 TxtRuntimeUserPassword 里填过东西"
                    + "（本层没有「先填输入框」的机制）⇒ 数据面一字不变"),

                // ── 用户角色下拉：驱动本身读不到差别，见证在**下一次落盘**上（两段式）──
                SelInert("CmbRuntimeUserRole", 1, "OnRuntimeUserRoleChanged（**代码里** `SelectionChanged +=` 接的）："
                    + "它写的是**草稿对象**的 `RoleId`，再加 `RefreshRuntimeUserListItems` / `RefreshRuntimeUserIssues` ——"
                    + "三样东西本层一个都读不到（草稿是 private；列表项文案与问题行全走字典，冻不得）。"
                    + "⚠ **但这条不是零信息**：接线在不在会在 `BtnApply` 那一步显形（见 `用户角色随下拉` 那枚探针）——"
                    + "那是本层「两段式声明」在选择族上的第一次落地。"
                    + "⚠ 它排在 `BtnSetRuntimeUserPassword` 之后：前置是「草稿里有一条**且被选中**」"
                    + "（`OnAddRuntimeUser` 建的、默认角色是 operator ⇒ 拨到第 2 项真的换了人）"),

                // ── 「应用到全部屏幕」：**阶段 115 起不再写同值** ──
                Moves("BtnApplySizeToAll", "`foreach (var screen in _project.Screens)` 把每个屏的尺寸写成"
                    + "**数字框里的当前值**。⚠ 阶段 114 时它只能记成 Inert（「夹具里屏幕尺寸本来就是默认值 ⇒ "
                    + "写进去的是同一个数」）—— 那条 Inert 记的是**夹具巧合**、不是接线；"
                    + "上面那条 `CmbScreenPreset` 把数字框拨成 480 × 272 之后，这一步就真的写了新值"
                    + "（阶段 115 顺手把它从 Inert 升级成 Moves）",
                    ("屏宽", "480"), ("屏高", "272")),

                // ── 核心一条：它把上面 10 个复选框的值一次性写进模型 ──
                Moves("BtnApply", "OnApplyClicked → ApplyInternal → **SaveToProject()**（不关窗）："
                    + "把 UI 上每个控件的当前值写回 `_project`。上面 10 个复选框虽然当场只标了脏，"
                    + "但它们的值在这里被**读走**写进模型 ⇒ 10 个 bool 全部翻转；"
                    + "同时 SaveRuntimeUsers() 把草稿整体写回 ⇒ `用户·模型` 0 → 1。"
                    + "⚠ 这是本表**唯一**能见证那 10 条的地方（两段式声明）",
                    ("通讯·写合并", "0"), ("运行·崩溃转储", "0"), ("运行·重启", "0"),
                    ("导出·调试符号", "1"), ("导出·注释", "0"), ("导出·合并资源", "0"),
                    ("导出·LGPL", "0"), ("安全·审计", "0"), ("安全·自动登出", "0"),
                    ("安全·防篡改", "1"), ("用户·模型", "1"),
                    // 🩸 这一枚是**选择族**（CmbRuntimeUserRole）那条接线的唯一见证：
                    // 「模型里的角色 == 下拉当前项」当且仅当那条订阅在（摘掉它，下拉变了、草稿不变 ⇒ 归 0）。
                    ("用户角色随下拉", "1")),

                // ── 用户表：把选中**清空**（本表的第二条「清空档」手势，下标 -1）──
                SelMoves("RuntimeUserList", -1, "OnRuntimeUserSelectionChanged（**代码里** "
                    + "`SelectionChanged +=` 接的）的**另一支**：选中被清空 ⇒ `SelectedRuntimeUser` 为 null "
                    + "⇒ `SetRuntimeUserDetailEnabled(false)` —— 它把详情区四个控件清空、六处置灰。"
                    + "🩸 见证刻意选 `用户名框`（回填 / 清空都出自这一支）与 `用户角色随下拉`（下拉被清成 null "
                    + "⇒ 模型里的角色自然对不上）—— 两样**都是处理器写的**；"
                    + "若只声明「列表选中没了」，摘掉订阅照样绿（那是手势自己拨的，见 SelMoves 那条 🩸）。"
                    + "⚠ 它是**程序性**清空、不是鼠标手势（生产里由删光条目那条路触发），"
                    + "两档下标的诚实边界见类注释「手势下标有两个档」",
                    ("用户名框", "空"), ("用户角色随下拉", "0")),

                // ── 再把它**选回来**：上一条清空把下一步的前置打掉了 ──
                //    `OnRemoveRuntimeUser` 的第一行就是「没有选中条目就 return」⇒ 清空之后接着点它，
                //    一行都跑不到（首跑实测：「用户草稿列表」期望 0、实际 1，且 Moves 一条都没动）。
                //    这条回选同样是**处理器写的**两样：回填详情（用户名框）与把角色下拉装回去。
                //    ⚠ 同一控件在一张表里驱动两次是本阶段早有先例的（TrendList / GroupList 各两次），
                //    唯一性键是 (名字, 手势下标)，两次的见证各自独立。
                SelMoves("RuntimeUserList", 0, "OnRuntimeUserSelectionChanged 的**回填支**："
                    + "选中被重新装上 ⇒ `ShowRuntimeUserDetail(user)` 回填详情区并选中角色那一项。"
                    + "🩸 它同时是**上一条的还原**：本序列后面 `BtnRemoveRuntimeUser` 的第一行就读选中项，"
                    + "不还原的话那条声明会「一行都跑不到」（首跑实测就是这样红的）",
                    ("用户名框", "非空"), ("用户角色随下拉", "1")),

                // ── 删：只动草稿；模型刚被 Apply 写进 1 条，此刻**不动**（草稿语义再次生效）──
                Moves("BtnRemoveRuntimeUser", "OnRemoveRuntimeUser 从**草稿**里删掉选中条目 ⇒ 窗口列表 1 → 0；"
                    + "末尾 `SelectRuntimeUser(null)` 顺带把详情区清空（`SetRuntimeUserDetailEnabled(false)` ⇒ "
                    + "`用户名框` 从非空变空）。"
                    + "⚠ `用户·模型` 保持 1（模型只在 ApplyInternal 时被整体替换）—— 这条正是「取消能整体回滚」"
                    + "那个承诺的反面证据：草稿删了不等于模型删了",
                    ("用户草稿列表", "0"), ("用户名框", "空"),
                    // ⚠ 删完 `SelectRuntimeUser(null)` → `SetRuntimeUserDetailEnabled(false)` 会把
                    // 角色下拉**清空**（`SelectedItem = null`）⇒ 这一枚从 1 归 0。它记的是真实行为。
                    ("用户角色随下拉", "0")),

                // 三条 Never：Ok 会 ApplyInternal + Close；Cancel/TitleClose 都 Close
                Never("BtnOk", "OnOkClicked = `if (_hasChanges) ApplyInternal(); Close();` ⇒ 点了会关窗，"
                    + "后续按钮无从验证（本序列用 BtnApply 代替它见证落盘）"),
                Never("BtnCancel", "OnCancelClicked 就是 Close() ⇒ 同上"),
                Never("BtnTitleClose", "ChromeWindow 标题栏的关闭按钮（XAML 里 Click=OnTitleCloseClicked）⇒ 同上"),
            }),

        // ── 通讯配置：**第一个「输入面驱动」的宿主** —— 本层原先缺的那块 ──
        // 前六个宿主里，处理器要么自己造数据（`BtnAddGroup` 这一类），要么现场在夹具里已经摆好
        // （列表里躺着选中项）。这个视图不一样：**绝大多数有意义的按钮，第一行就读输入框或看列表选中项**，
        // 而这两样本层原先既填不了也装不上 ⇒ 它们只能登记成「框是空的 ⇒ 提前 return」那种 Inert，
        // 那条 Inert 记的是**夹具形状**、不是接线，等于这一层对它零信息。
        // ⇒ 本宿主是「布景」（<see cref="Arranged"/>）这套机制的第一个用户，也是它唯一的见证者。
        //
        // 夹具初态（WindowDepot）：`Variables` 有 2 条（液位 D100 / 温度 D102，都没有 ConnectionId/SlaveId
        // ⇒ 不属于任何连接）、`Connections` 空、`Groups` 空。
        //
        // ⚠ 本视图的**运行期面是 15 个**具名按钮，而静态名字探针给的残余只有 **14** —— 差的那个是
        // `BtnTitleClose`：它出现在 28 个 .axaml 里，早被别处的守卫提过名字，于是被「顺带」算作已覆盖。
        // 这是阶段 109 那条「顺带覆盖」的**镜像**：那次让降幅**高估**真实覆盖，这次让残余**低估**了 1。
        // ⇒ 读「残余掉了几个」时永远要问一句：这个数是不是**这个视图**自己的。
        //
        // ⚠ 两条**自陈的盲区**（本宿主量出来的，不是被证过的）：① `TxtConnName.LostFocus →
        // PersistConnectionName` 的回写路径**不在本层的面内**（它不是按钮）；② 本表「一个按钮名只能有一条」
        // ⇒ `BtnDeleteSelection` 这个三分派器只验得到「删子设备」那一支，「删连接」「什么都没选」两支够不到。
        // 详见专题 §111.6。
        new("CommunicationConfigView",
            d => new Views.CommunicationConfigView(d.Variables, d.Project),
            new[]
            {
                Num("连接", c => c.Depot.Variables.Connections.Count),
                Num("从站", c => c.Depot.Variables.Connections.Sum(x => x.Slaves.Count)),
                Num("变量", c => c.Depot.Variables.Variables.Count),
                Num("树根", c => TreeItems(c.Window, "CommTree")),
                Num("树选中", c => CommTreeShape(c.Window)),
                Num("从站变量列表", c => Items(c.Window, "SlaveVarList")),
                // ⚠ 这一条是**输入面**，理由只有一个：`BtnSaveEditSlaveVar` 的前提就是选中项，
                // 而本层没有任何按钮能重新选中它（选中是 ListBox 的行为、不是按钮的行为）。
                // 「树选中」刻意**不**标输入面 —— 本表没有任何一条布景需要动它，于是它留在严格的一侧。
                InputNum("变量选中", c => Control<ListBox>(c.Window, "SlaveVarList").SelectedItem is Variable ? 1 : 0),
                Num("连接启用", c => ConnEnabled(c.Depot)),
                Num("从站启用", c => SlaveEnabled(c.Depot)),
                Str("从站首变量地址", c => FirstSlaveVarAddress(c.Depot)),
                Str("批量面板", c => Control<Border>(c.Window, "BatchAddPanel").IsVisible ? "开" : "关"),
                // 选择族（阶段 115）的四枚眼睛 —— 每一枚都对着某个处理器**真的**写出去的东西：
                Str("连接测试按钮", c => Control<Button>(c.Window, "BtnTestConnection").IsVisible ? "可见" : "隐藏"),
                Num("连接驱动槽位", c => ConnDriverSlot(c.Depot, c.Window)),
                // ⚠ 输入面：CommTree 那条声明的布景要先把框弄脏，才看得见「处理器把它填回来」。
                InputStr("连接名框一致", c => ConnNameBoxMatchesModel(c.Depot, c.Window)),
                Str("地址提示档", c => AddressHintKind(c.Window)),
                Str("预览寄存器前缀", c => BatchPreviewRegPrefix(c.Window)),
                // ⚠ 本枚是**输入面**：`BtnAddSlaveVar` 那条布景就往它里面填名字，而布景只许动输入面。
                InputStr("变量名框", c => BoxState(c.Window, "TxtSlaveVarName")),
                Voice("状态栏", c => StatusOf(c.Window)),
            },
            new[]
            {
                // ── 建连接：本宿主唯一的「自己造数据」按钮，也是后面 13 条的前提 ──
                // ⚠ `连接启用` 从 **-1**（还没有连接）到 1：这一档刻意与「停用」分开（见 ConnEnabled）。
                Moves("BtnAddConnection", "OnAddConnection：模型里加一条 DeviceConnection（名字走 comm.conn.new "
                    + "模板、驱动取注册表第一个）→ RefreshConnectionList 重建树 → SelectTreeByConnection 选上新节点 "
                    + "→ OnConnectionSelected 填表单。⚠ 本表唯一一条「从无到有」的连接 —— "
                    + "它一坏，后面所有按钮会连锁失败。"
                    + "⚠ 另两枚分量也是这一步的**真实结果**：`OnConnectionSelected` 把测试按钮显出来"
                    + "（XAML 初值 `IsVisible=False`），而模型的 `DriverId` 从「还没有连接」变成落进下拉第 1 格",
                    ("连接", "1"), ("树根", "1"), ("树选中", "1"), ("连接启用", "1"),
                    ("连接测试按钮", "可见"), ("连接驱动槽位", "1")),

                // ── 连接启用位：XAML 里是 IsCheckedChanged=（**没有** Click=）⇒ Gesture 先翻转再引发 Click ──
                Moves("ChkConnEnabled", "WireEvents：ChkConnEnabled.IsCheckedChanged → PersistConnectionEnabled；"
                    + "上一步 OnConnectionSelected 把 IsChecked 同步成 true（那是在 _isUpdating 里做的，不写模型）"
                    + "⇒ 翻转成 false ⇒ 模型里那条连接的 IsEnabled 真的被写成 false",
                    ("连接启用", "0")),

                // ── 加子设备：前提是 _selectedConnection 非空（上一步已满足）──
                Moves("BtnAddSlave", "OnAddSlave：往**当前选中的连接**下加一条 DeviceSlave（名字走 comm.slave.new、"
                    + "站号 = 现有子设备数 + 1）→ 树重建并选中它 → OnSlaveSelected 填表单。"
                    + "⚠「从站启用」-1 → 1：走的是**新子设备的默认值**，不是这个按钮在写启用位；"
                    + "真正写启用位的是后面的 BtnSaveSlave。⚠「从站首变量地址」从**无从站**变成**无变量**："
                    + "它两档「没有」刻意分开，这一条正好见证「子设备真的建出来了」",
                    ("从站", "1"), ("树选中", "2"), ("从站启用", "1"), ("从站首变量地址", "无变量")),

                // ── 子设备启用位：与 ChkConnEnabled 形状**相反** —— 它没有接线 ──
                Inert("ChkSlaveEnabled", "**根本没有接线**（XAML 里既无 Click= 也无 IsCheckedChanged=）："
                    + "翻转 IsChecked 本身不改任何数据面分量。⚠ 它的值**后面真的被读走**了 —— "
                    + "点 BtnSaveSlave 时 `_selectedSlave.IsEnabled = ChkSlaveEnabled.IsChecked == true`，"
                    + "所以本条只是「这一步不动数据面」，不是「这个控件没用」（与 VariableManagerView.ChkAlarmEnabled 同形）"),

                // ── 保存子设备：它是 ChkSlaveEnabled 那条值的**唯一**消费点 ──
                Moves("BtnSaveSlave", "OnSaveSlave 把表单读回模型：名字/站号/描述写的都是**同一个值**"
                    + "（表单本来就是 OnSlaveSelected 从它读出来的），唯一真的变的是启用位 —— "
                    + "上一步翻转过的 ChkSlaveEnabled 此刻是 false ⇒ 模型 1 → 0。"
                    + "⚠ 这是「两段式」在本宿主里的样子：复选框只改控件，值到这一步才落进模型",
                    ("从站启用", "0")),

                // ── 加子设备变量：本条要布景（名字与地址两个框）──
                Arranged(Moves("BtnAddSlaveVar", "OnAddSlaveVar 的第一行就读 TxtSlaveVarName，为空即 return —— "
                    + "布景先把名字填成「启动指令」、地址填成纯偏移 7 ⇒ 处理器走完全程：变量表 +1、"
                    + "当前子设备的变量列表 +1，地址由 BuildModbusAddress 组合成「前缀 4 + 5 位」= 40007"
                    + "（默认寄存器类型是保持寄存器）",
                    ("变量", "3"), ("从站变量列表", "1"), ("从站首变量地址", "40007"),
                    // ⚠ 处理器收尾把名字框**清空**（`TxtSlaveVarName.Text = ""`）——
                    // 声明「非空 → 空」而不是躲开它：这一步清空的正是布景填进去的东西，
                    // 而下一步 `SelMoves(SlaveVarList)` 又要靠「空 → 非空」来见证回填。
                    ("变量名框", "空")),
                    c =>
                    {
                        Control<TextBox>(c.Window, "TxtSlaveVarName").Text = "启动指令";
                        Control<TextBox>(c.Window, "TxtSlaveVarAddress").Text = "7";
                    }),

                // ── 子设备变量列表：驱动它 ⇒ 回填表单（本表第三条选择族声明）──
                SelMoves("SlaveVarList", 0, "OnSlaveVarSelected（**代码里** `SelectionChanged +=` 接的）："
                    + "上一步 `RefreshSlaveVars()` 把列表重建、选中清成了 null，而处理器收尾又把名字框清了空 "
                    + "⇒ 此刻拨到唯一的第 1 项，处理器把那条变量读回表单：名字框从**空**变**非空**。"
                    + "⚠ 「变量选中」是手势自己写的（`SelectedItem`），但「变量名框」不是 —— "
                    + "它由 `OnSlaveVarSelected` 写 ⇒ 摘掉订阅，框会留在「空」、本条报红。"
                    + "⚠ 本条同时**解锁**了下一步：`BtnSaveEditSlaveVar` 的前提正是「列表里有选中项」，"
                    + "它原来的布景要自己装选中（见那条的改动）",
                    ("变量名框", "非空"), ("变量选中", "1")),

                // ── 改子设备变量：本条要布景（装上列表选中项 + 改地址框）──
                Arranged(Moves("BtnSaveEditSlaveVar", "OnSaveEditSlaveVar 的前提是 SlaveVarList.SelectedItem 是 "
                    + "Variable —— 上一条 `SelMoves(SlaveVarList)` 已经把选中装上了，所以这条布景"
                    + "**只剩**「把地址框改成偏移 8」一件事（阶段 115 之前它得自己装选中，"
                    + "那条布景记的是「夹具没选中」这个**形状**、不是接线）。"
                    + "处理器于是重写那条变量：名字/类型/归属都没变，只有地址 40007 → 40008。"
                    + "⚠ 本层能读到这个变化的只有「从站首变量地址」—— 那就是它存在的**唯一**理由",
                    ("从站首变量地址", "40008")),
                    c =>
                    {
                        Control<TextBox>(c.Window, "TxtSlaveVarAddress").Text = "8";
                    }),

                // ── 删子设备变量：上一步末尾把选中恢复了 ⇒ 本条**不用**布景 ──
                Moves("BtnDeleteSlaveVar", "OnDeleteSlaveVar：上一步 OnSaveEditSlaveVar 末尾那句 "
                    + "`SelectListItemByTag(SlaveVarList, v.Id)` 把选中**恢复**了（还是同一条变量）"
                    + "⇒ 处理器拿得到它 ⇒ 从变量表删掉：模型 −1、列表清空、选中归 0、"
                    + "「从站首变量地址」回到**无变量**",
                    ("变量", "2"), ("从站变量列表", "0"), ("变量选中", "0"), ("从站首变量地址", "无变量")),

                // ── 批量面板开关：它只切一个 Border 的 IsVisible ──
                Moves("BtnBatchAddSlaveVars", "处理器只有一句 `BatchAddPanel.IsVisible = !BatchAddPanel.IsVisible`"
                    + "（XAML 里那个 Border 初值 IsVisible=False）⇒ 面板从关到开；数据面一字不动。"
                    + "⚠ 这条属于「只动控件」，它在新加进来的「批量面板」分量上可观测",
                    ("批量面板", "开")),

                // ── 批量类型下拉：**订阅在不在原理上不可观测**的一例（自陈盲区，不是漏掉）──
                SelInert("CboBatchVarType", 2, "它订阅到的处理器 `UpdateBatchPreview` **不读这个控件** —— "
                    + "预览行的输入只有「前缀 / 数量 / 起始序号 / 起始地址 / 寄存器类型」五样，"
                    + "类型这一维压根不在里面 ⇒ 拨前拨后预览**一字不变**。"
                    + "⚠ 这条登记的是**事实**，本表**不声称**这条接线被验过：它唯一的出口是那句本地化预览模板，"
                    + "而「类型」连模板实参都不是 ⇒ 补探针也补不出来（这是「面的边界 ≠ 没有缺口」的第四形态："
                    + "**处理器不读那个控件**）。它与 `CboBatchVarRegType` 共用同一条处理器，"
                    + "后者的声明（预览里那段地址）才是那条接线的见证。"
                    + "⚠ 真收得动的只有「让预览也读类型」那种改动 —— 那时本条会当场变红逼人改声明"),

                // ── 批量生成：本条要布景（前缀 / 数量 / 起始地址三个框）──
                Arranged(Moves("BtnApplyBatchVars", "OnApplyBatchVars 依次读前缀、数量、起始序号、起始地址："
                    + "布景填「泵」+ 2 条 + 起始地址 10 ⇒ 生成 泵1/泵2（地址 40010/40011；起始序号用 XAML 里的"
                    + "默认值 1，没人填过它）⇒ 模型 +2、列表 +2",
                    ("变量", "4"), ("从站变量列表", "2"), ("从站首变量地址", "40010")),
                    c =>
                    {
                        Control<TextBox>(c.Window, "TxtBatchNamePrefix").Text = "泵";
                        Control<TextBox>(c.Window, "TxtBatchCount").Text = "2";
                        Control<TextBox>(c.Window, "TxtBatchStartAddr").Text = "10";
                    }),

                // ── 批量寄存器类型下拉：拨它 ⇒ 预览按新类型重算地址 ──
                SelMoves("CboBatchVarRegType", 3, "UpdateBatchPreview（**代码里** `SelectionChanged +=` 接的）："
                    + "预览里每个样本的地址都由 `BuildModbusAddress(CboBatchVarRegType, …)` 组合，"
                    + "所以拨到第 4 项（线圈，前缀 `0`）之后，`泵1` 的地址从 `40010` 变成 `00010`。"
                    + "🩸 见证刻意只取**地址那一位前缀**（`预览寄存器前缀`）：预览整行是本地化模板套出来的，"
                    + "冻它等于冻一份本地化结果（见那枚探针的注释）。"
                    + "⚠ 它排在 `BtnApplyBatchVars` **之后**：那一步的布景填了前缀/数量/起始地址，"
                    + "预览才有内容可改（此前预览是空串 ⇒ 拨它只会写空串、什么都观测不到）",
                    ("预览寄存器前缀", "0")),

                // ── 删选中项（级联）：删的是子设备，顺带见证「变量不被删、只被摘掉归属」──
                Moves("BtnDeleteSelection", "OnDeleteSelection 按选中层级分派：_selectedSlave 非空 ⇒ 走 OnDeleteSlave。"
                    + "级联是**摘归属**而不是删数据（泵1/泵2 的 SlaveId 被清空、所以模型里 `变量` 仍是 4）；"
                    + "树重建后按 _selectedConnection 恢复选中 ⇒ 选中从子设备节点退回连接节点；"
                    + "「从站启用」0 → **-1**（子设备没了，不是被停用）。"
                    + "⚠ 这个按钮是个**三分派器**，本表只够得到它的一支 —— 见专题 §111.6",
                    ("从站", "0"), ("树选中", "1"), ("从站变量列表", "0"), ("从站启用", "-1"),
                    ("从站首变量地址", "无从站")),

                // ── 驱动下拉：本宿主唯一「写模型」的选择族（onDriverTypeChanged）──
                SelMoves("CboDriverType", 1, "OnDriverTypeChanged（**代码里** `SelectionChanged +=` 接的）："
                    + "它写出去的只有 `_selectedConnection.DriverId = 下拉当前项的 Tag` 这一样"
                    + "（另外两样是重画参数字段与重建树，都不进本表的分量）。"
                    + "⚠ 排在 `BtnDeleteSelection` **之后**是有原因的：那个按钮的前提是「连着子设备」，"
                    + "而换了驱动的连接仍能承载子设备 —— 但把顺序反过来会让「从站首变量地址」那条声明"
                    + "多一层与驱动无关的解释。⚠ 见证读的是**模型**落在下拉第几格，不读下拉选中项"
                    + "（后者是手势自己拨的）",
                    ("连接驱动槽位", "2")),

                // ── 寄存器类型下拉：只改地址提示那两处文案（唯一出口就是它们）──
                SelMoves("CboSlaveVarRegType", 3, "UpdateSlaveVarAddressHint（**代码里** `SelectionChanged +=` 接的）："
                    + "处理器把占位符与说明行**一起**换成当前档的那两条字典文案。"
                    + "🩸 见 `地址提示档` 那枚探针：它**反向查表**认档，所以「文案真的换了」与「文案冻住了」"
                    + "分得开，而换语言时两侧一起变、读数不动。"
                    + "⚠ 它排在最后三条里：早拨会让 `SelectRegTypeFromAddress` 回头把它改回保持寄存器",
                    ("地址提示档", "coil")),

                // ── 通讯树：清空选中（下标 -1）—— 本条登记的是**实测出来的一个事实** ──
                // 🩸 插桩实测（`.workbuddy/tmp/comm-tree-probe.log`）：整条序列里
                //   `OnTreeSelectionChanged` 一共进了 7 次，**没有一次**是这一步引出来的 ——
                //   `CommTree.SelectedItem = null` 改了**属性**（`树选中` 读数 1 → 0 是真的），
                //   却没有改 Avalonia 内部的选择模型 ⇒ **事件没引发**、处理器一行都没跑。
                //   成因是上面那些 `RefreshConnectionList()` 在抑噪守卫里重建了 `Items`：
                //   属性与选择模型就此**错位**（属性还指着节点、模型里其实已经空了）。
                // ⇒ 本条**刻意只声明手势自己写的那一枚**（`树选中`），把「本档对通讯树无效」记成事实；
                //   它同时是下一条的前提（把属性拨到 null，回选才是一次**真的**变化）。
                //   真正的接线见证在下一条。⚠ 本表的诚实边界：这条声明**不含**接线信息。
                SelMoves("CommTree", -1, "**登记事实、不声称接线**：`CommTree.SelectedItem = null` 在本宿主里"
                    + "只动属性、不引发 `SelectionChanged`（见上一条注释里的插桩读数），"
                    + "所以处理器一个字都没写。它是下一条回选的**前提**（先把属性拨空），"
                    + "而它自己唯一动的是手势写的那枚 `树选中`",
                    ("树选中", "0")),

                // ── 通讯树：回选（下标 0）—— 本表对 CommTree **唯一**的接线见证 ──
                //   选择模型此刻是空的 ⇒ 装一个节点进去是**真的**变化 ⇒ 事件引发 ⇒
                //   处理器走连接那一支、把表单按模型回填 —— 布景先把连接名框弄脏，
                //   于是「回填」看得见（不一致 → 一致）。摘掉订阅 ⇒ 框留在「不一致」⇒ 红。
                Arranged(SelMoves("CommTree", 0, "OnTreeSelectionChanged 的**连接支**"
                    + "（`node.IsConnection` ⇒ `_selectedSlave = null` + `OnConnectionSelected(conn)`）："
                    + "处理器写出去的东西里，本层够得到且**不是同值**的只有连接名框"
                    + "（驱动下拉/参数字段/按钮可见位在「早就选中过这条连接」之后全是同值，读不出接线）。"
                    + "⚠ 所以本条要**布景**：先把框写成 `XYZ`（脏），处理器再把它填回模型里的名字。"
                    + "⚠ 下标 `0` 打的是**根**（连接节点）—— 子设备节点在本层的下标域里够不到",
                    ("树选中", "1"), ("连接名框一致", "一致")),
                    c => Control<TextBox>(c.Window, "TxtConnName").Text = "XYZ"),

                // ── 四条 Never：三种互不相同的理由 ──
                Never("BtnImportSlaveVars", "`async Task` + `IStorageProvider.OpenFilePickerAsync` ⇒ headless 下"
                    + "走到的是**平台桩**，观测到的是桩而不是接线（本层「不判跨层副作用」那一档）"),
                Never("BtnExportSlaveVars", "`async void` + `SaveFilePickerAsync` ⇒ 同上（而且要先等该子设备的"
                    + "变量列表非空才会走到选择器那一步，读到的会是两个瞬间的混合）"),
                Never("BtnTestConnection", "**新的一档 Never**：它既不关窗、也不走平台桩，而是 `async void` + "
                    + "`await Task.Run(() => driver.ValidateConnection(config))` —— 完成时刻与 UiTestHost.Pump "
                    + "之间**没有因果关系** ⇒ 点完读到的分量属于哪个瞬间没有保证（读数会抖），"
                    + "而跑在 UI 线程上的 async void 抛异常还会变成另一起无法归因的崩溃。"
                    + "要收它得先给本层加一个「等静默」的原语，不是改声明"),
                Never("BtnTitleClose", "ChromeWindow 标题栏的关闭按钮（XAML 里 Click=OnTitleCloseClicked）⇒ "
                    + "点了会关掉宿主窗口。⚠ 它在**运行期面**的 15 个里，但静态名字探针把它算作「已覆盖」——"
                    + "因为它出现在 28 个 .axaml 里（见上面那段注释）"),
            }),

        // ── 趋势配置：**第一个「接线缺失」的宿主**（登记 #65）—— 阶段 114 修好，本段留取证 ──
        // 阶段 112 建本宿主时，12 个具名按钮里有 6 个的事件订阅整段缺失。依据（三条都核过）：
        //   ① XAML 里只有两个 On* 事件属性（PointerPressed=OnTitleBarPointerPressed、
        //      Click=OnTitleCloseClicked），**没有**任何 IsCheckedChanged= / SelectionChanged=；
        //   ② code-behind 里的 `+=` 只有 5 行，全是按钮的 Click；
        //   ③ 那 6 个处理器在**全仓零引用**（`grep -rn` 去掉 bin/obj 后无命中）。
        // 对照：ScreenManagerView / PropertyPanelView 都在用 `IsCheckedChanged="OnPublicChanged"`
        // 这个写法 ⇒ 模式是存在的，是本视图**丢了订阅**。
        //
        // ⚠ **阶段 114 的量的那一遍把这笔账改大了**：缺的不是「6 个控件」而是
        //   **6 个处理器 × 10 个控件**（登记 #65 只点了 8 个控件 / 4 个处理器，漏了
        //   `OnPenColorChanged`@`PenColorCombo` 与 `OnTimeWindowChanged`@`TimeWindowCombo`）。
        //   其中 **6 个控件**（4 枚显示选项 ＋ 2 枚笔复选框）在**本层的面内**；
        //   两个 `ListBox` ＋ 两个 `ComboBox` **不在面内**（面只数 `Button` 族）⇒ 那 4 处即使修好了
        //   本层也看不见 —— 这是本层**自陈的盲区**，不是漏掉。
        //
        // ⇒ 用户可见的真缺陷（阶段 114 **已修**）：趋势对话框里
        //   ⓐ 4 个显示选项复选框（图例 / 网格 / X 轴 / Y 轴）与 ⓑ 笔编辑器的「显示点」「启用」
        //      两枚复选框，**拨动之后不写回模型**；ⓒ 列表选中、颜色下拉、时间窗口下拉同样没订阅。
        //
        // ⚠ ⚠ **「修好就红」这句话有前提，阶段 112 把话说准、阶段 114 兑现**：
        //   光补订阅**不红** —— 原来的 4 个探针读的全是「条数」，而这两个处理器写的是**布尔位**
        //   ⇒「写了模型」与「没接线」在读数上**完全同形**（「面的边界 ≠ 没有缺口」）。
        //   ⇒ 阶段 112 为此补了 6 个显示位探针（关图例趋势 / 关网格趋势 / 关X轴趋势 / 关Y轴趋势 /
        //     开显示点笔 / 停用笔）。**先补探针、再补订阅** —— 阶段 114 补订阅时这 6 条**条条变红**
        //     （首跑读数 `.workbuddy/tmp/s114-post.log`），按预案转成 `Moves`。
        //   ⇒ 这个「之后」由负控钉住（阶段 112 三支 ＋ 阶段 114 一支）：
        //     NC6 给「图例」补订阅 ⇒ 红（4 条显示选项的代表）；NC7 给「显示点」补订阅 ⇒ 红
        //     （笔那两条的代表，且证明本表把顺序排对了）；NC8 拿新探针做**布景** ⇒ 布景守卫报红；
        //     NC9 摘掉 `_suppressDisplayEvents` ⇒ 红（钉住下面那条回归的修法）。
        //
        // 🩸 **阶段 114 的第二条账：光补订阅会引入回归，判据首跑当场拦住**
        //   `UpdateTrendDisplay()` 是「模型 → 控件」的反向同步，它**一枚一枚**写那 4 个复选框，
        //   而 `OnDisplayOptionChanged` **一次读全部 4 枚** ⇒ **写到某一枚**就会触发处理器；
        //   处理器把「控制件当时的残值」当成用户意图写回模型，随后那几枚又从**已被写坏**的模型
        //   刷回来 ⇒ 不再触发（自我巩固）⇒ 错误被静默锁死。**两处现场**（`s114-post.log`）：
        //     ① **初态**（构造函数里那一遍 `UpdateTrendDisplay()` 就够了）：夹具第一条趋势的
        //        **网格 / X 轴 / Y 轴**被写成「关」（这三个分量 0 → 1）—— 也就是说
        //        **打开这个对话框本身**就改掉了用户的趋势；
        //     ② `BtnAddTrend`：新趋势自带四个 true，但控制件还持着上一条的残值 ⇒ 写到「网格」
        //        那一步才触发 ⇒ 新趋势的 **X 轴 / Y 轴**被写成「关」（`关X轴趋势` / `关Y轴趋势` 1 → 2）。
        //   ⚠ 被写坏的是**哪几位**取决于控制件残值（= 上一条趋势的值），**不是固定几位的形状**
        //     ⇒ 更该由守卫兜住，不能靠人肉 review。
        //   修法是照作者自己的模式补抑噪守卫（`UpdatePenEditor()` 早就有 `_suppressPenEvents`，
        //   这里漏了同款 `_suppressDisplayEvents`）⇒ 负控 NC9 摘掉它即红。
        //   ⇒ 这是本层的**第二种用途**：不只记「接线缺不缺」，还拦「接线补上之后会不会把别的写坏」。
        //
        // 夹具初态（WindowDepot）：`Trends` 有 1 条「液位趋势」，**四个显示位全开**，内含 2 支笔
        // （液位 D100 / 温度 D102，`VariableId` 都非空 ⇒ 后面 `BtnToggleRecord` 真能启动记录）。
        // 构造之后 `_currentTrend = _trends[0]`；`_currentPen` 在**订阅补齐之后**会被
        // `TrendList.SelectionChanged` 装成夹具那条趋势的第一支笔（订阅补齐前恒为 null）。
        //
        // ⚠ **本宿主不需要布景，靠的是声明顺序**：`BtnDeletePen` 与两枚笔复选框的前提 `_currentPen`
        //   在**本条序列**里只能由 `BtnAddPen` 装上（`BtnAddTrend` 刚把当前趋势换成一条空的）
        //   ⇒ 它们必须排在 AddPen **之后**。这正是「数组顺序 = 点击顺序」在起作用。
        new("TrendConfigView",
            d => new Views.TrendConfigView(d.Trends),
            new[]
            {
                Num("趋势数", c => c.Depot.Trends.Count),
                Num("末条趋势笔数", c => LastTrendPenCount(c.Depot)),
                Num("趋势列表", c => Items(c.Window, "TrendList")),
                Num("笔列表", c => Items(c.Window, "PenList")),
                // ── 六个「显示位」探针：给那 6 条 未接线 Inert 装的**眼睛** ──
                // 没有它们，那 6 条是**零信息**的：本层 Inert 的口径是「探针一字不变」，而上面四个
                // 探针读的全是**条数**，读不到 ShowLegend / ShowPoints 这类布尔位 ⇒
                // 「处理器写了模型」与「处理器没接线」在读数上**完全同形**，判据看不见区别。
                // ⚠ 六条都数「**关着的 / 停用的**」，因此初值天然全是 0：夹具那条趋势的四个显示位
                // 默认全开、两支笔默认启用，而 BtnAddTrend 新建的趋势同样全开、BtnAddPen 新建的笔
                // 同样启用 ⇒ 反过来数「开着的」会让 BtnAddTrend 平白多出四条**与它无关**的声明。
                // 这不是凑数，是选口径。
                // ⚠ 都用**全工程累计**而不是「当前那条趋势」：_currentTrend 是私有字段，表里够不到；
                // 而 AddTrend / DeleteTrend 中途会把它换人 —— 累计口径对「写的是哪一条」不敏感，
                // 只回答本层唯一关心的问题：「有没有任何一条被改过」。
                Num("关图例趋势", c => TrendsWith(c.Depot, t => !t.ShowLegend)),
                Num("关网格趋势", c => TrendsWith(c.Depot, t => !t.ShowGrid)),
                Num("关X轴趋势", c => TrendsWith(c.Depot, t => !t.ShowXAxis)),
                Num("关Y轴趋势", c => TrendsWith(c.Depot, t => !t.ShowYAxis)),
                Num("开显示点笔", c => PensWith(c.Depot, p => p.ShowPoints)),
                Num("停用笔", c => PensWith(c.Depot, p => !p.IsEnabled)),
                // ── 三条「选择族」专用的眼睛（阶段 115）：面扩到非 Button 之后，
                //    声明的分量**必须是处理器写的**，否则摘掉订阅读数不变（见 SelMoves 那条 🩸）──
                // `PenLabelInput` 由 `UpdatePenEditor()` 写 ⇒ 是处理器出口。
                // 🩸 它顺带是「TrendList 订阅」的**间接见证**：摘掉 TrendList 的订阅之后，
                //    `BtnDeleteTrend` 那一步 `_currentPen` 会停在 null ⇒ 这里退不回「液位」。
                Str("笔标签类", c => PenLabelKind(c.Window)),
                Num("非默认色笔", c => PensWith(c.Depot, p => p.Color != "#38bdf8")),
                Num("非默认时间窗趋势", c => TrendsWith(c.Depot, t => t.TimeWindowMs != 60000)),
            },
            new[]
            {
                // ── 两条「选择族」（阶段 115 新面的第一处落地）：拨选中项 ⇒ 跑它订阅的 SelectionChanged ──
                // ⚠ 排在最前是**语义要求**，不是随手放：`UpdatePenEditor()` 的出口「笔标签框」要从
                //    「液位」变成「温度」，前提是「第 2 支笔」还在当前趋势上 —— 而 `BtnAddTrend` 一跑，
                //    当前趋势换成一条 0 支笔的新趋势、`_currentPen` 当场被置成 null。
                SelMoves("PenList", 1, "OnPenSelectionChanged：把 `_currentPen` 换成列表里第 2 支笔（温度）"
                    + "⇒ `UpdatePenEditor()` 把它的标签写进输入框 ⇒「笔标签框」液位 → 温度。"
                    + "🩸 见证刻意选**处理器写的东西**（UpdatePenEditor 的出口），不选「选中笔」那种"
                    + "—— 后者读的是 `PenList.SelectedItem`，而选中项是**这个手势自己拨的**，"
                    + "把 XAML 里那句 `SelectionChanged=` 摘掉读数照样变 ⇒ 判据原理上看不见 —— 这正是本条**不用**它的理由",
                    ("笔标签类", "温度")),

                SelMoves("PenColorCombo", 1, "OnPenColorChanged：把 `_currentPen.Color` 换成调色板第 2 色 "
                    + "#f87171（`PredefinedColors[1]`）—— 前提是 `_currentPen` 非空，而上一条刚把它装上"
                    + "（顺序即语义）。⚠ 它顺带 `RefreshPenList()` 重建笔列表：选中项被重新装上，"
                    + "于是 `OnPenSelectionChanged` 再跑一遍，但值不变（这条声明因此在两个出口上都稳）"
                    + "⚠ 它**不需要** `_suppressPenEvents` 那种抑噪：程序性重建列表写的是同一个选中项，"
                    + "而模型里那支笔的颜色是本条手势**有意**改的",
                    ("非默认色笔", "1")),

                // ── 建趋势：没有任何前提，同时是后面三条的前提 ──
                Moves("BtnAddTrend", "OnAddTrend：往 `_trends` 加一条 TrendConfig（名字走 trend.default.name "
                    + "模板、默认 60 s / 600 点、四个显示位全开）→ RefreshTrendList 重建列表 → 它成为 `_currentTrend`。"
                    + "⚠「末条趋势笔数」2 → **0**、「笔列表」2 → **0**：新趋势是空的，而点击前这两个分量读的是"
                    + "夹具那条（2 支笔）⇒ 它们同时见证「最后一条真的换了人」",
                    ("趋势数", "2"), ("趋势列表", "2"), ("末条趋势笔数", "0"), ("笔列表", "0"),
                    // 「笔标签类」：新趋势 0 支笔 ⇒ `_currentPen` 被置成 null ⇒ UpdatePenEditor 写空串
                    // ⇒ 归一化之后是「其它」。⚠ 这一条同时是上面两条选择族声明的**下游**。
                    ("笔标签类", "其它")),

                // ── `TrendList` 被驱动**两次**（阶段 115）：拨回夹具那条看一眼、再拨回来 ──
                // 为什么必须成对、且必须插在这里：本表「数组顺序 = 点击顺序」，而 `TrendList` 能在两项之间
                // 拨的窗口只有「BtnAddTrend 之后 ~ BtnDeleteTrend 之前」；直接拨走会把后面整段的语义改掉
                // （那时 `BtnAddPen` 要往新趋势上添笔）⇒ 只能是「拨过去看一眼、再拨回来」。
                // ⚠ 表尾不行：那时 `_trends` 只剩夹具一条、`TrendList` 只有 1 项，拨不动
                //   ⇒ 会触发「声明成 Moves 却一个分量都没动」的自检。
                SelMoves("TrendList", 0, "OnTrendSelectionChanged（XAML 里 `SelectionChanged=`）：拨回夹具那条"
                    + "（2 支笔）⇒ 处理器把 `_currentTrend` 换人并 `RefreshPenList()` ⇒ "
                    + "「笔列表」0 → 2、「笔标签类」其它 → 液位。"
                    + "🩸 两条见证都是**处理器与列表重建的出口**，不用 `TrendList.SelectedItem`"
                    + "（那是手势自己拨的，摘掉订阅照样变 ⇒ 零信息）",
                    ("笔列表", "2"), ("笔标签类", "液位")),

                SelMoves("TrendList", 1, "同一条处理器的第二次驱动：拨回**新建那条**（0 支笔）⇒ "
                    + "`_currentTrend` 换回去、「笔列表」2 → 0、「笔标签类」→「其它」。"
                    + "🩸 这一对（0 号位与 1 号位各来一次）把「**选中项换了人**」这件事本身钉住了 —— "
                    + "阶段 114 之前它一条声明都没有：那时这句 `SelectionChanged=` 明明接着，"
                    + "但面的边界（只收 Button 族）看不见它 ⇒「换人会换掉 `_currentPen`」"
                    + "只是注释里的说法、没有判据",
                    ("笔列表", "0"), ("笔标签类", "其它")),

                // ── 加笔：前提 `_currentTrend` 非空（上一步给的是新趋势，0 支笔）──
                Moves("BtnAddPen", "OnAddPen：往 `_currentTrend.Pens` 加一支 TrendPen（颜色取第一个没被占用的 "
                    + "#38bdf8、ShowPoints=false、IsEnabled=true、VariableId 留空）⇒ 笔数 0 → 1。"
                    + "⚠ 它顺手把 `_currentPen` 装上了 —— **本宿主唯一**能装上它的地方（两条选择处理器都没接线），"
                    + "所以 BtnDeletePen 必须排在它后面",
                    ("末条趋势笔数", "1"), ("笔列表", "1")),

                // ── 两条「未接线」（笔复选框）：**刻意**排在这里，不是随手放的 ──
                // 它们与下面四条显示选项同属「④ 事件从未订阅」，但只有排**在 BtnAddPen 之后**
                // 才可能被负控点红：OnPenPropertyChanged 的第一行是
                // `if (_suppressPenEvents || _currentPen == null) return;`，而「装上 _currentPen」
                // 的唯一活路就是上一步的 BtnAddPen ⇒ 顺序在这里是**为负控服务的**。
                // 排到表尾会怎样：BtnDeletePen 把 _currentPen 清成 null，这两条就退化成
                // 「未接线 ＋ 提前 return」的**合取** —— 光补订阅再也点不红，负控 NC7 会直接失效。
                Moves("PenShowPointsCheck", "**曾经是 ④ 未接线，阶段 114（登记 #65）修好**："
                    + "OnPenPropertyChanged 里那句 `_currentPen.ShowPoints = PenShowPointsCheck.IsChecked ?? false` "
                    + "就是为它写的。接线前翻转 IsChecked 不调用任何处理器、数据面一字不动；"
                    + "补上 `IsCheckedChanged=` 之后 ⇒ `_currentPen.ShowPoints = true` ⇒ 「开显示点笔」0 → 1。"
                    + "⚠ 这条声明现在的唯一作用是**守卫**：谁把订阅摘掉，它立刻红（负控 NC1 那一类）。"
                    + "⚠ 它同时是「**先补探针、再补订阅**」的见证 —— 探针不到位时读数看不见这个差"
                    + "（阶段 112 的负控 NC7 实测过）",
                    ("开显示点笔", "1")),

                Moves("PenEnabledCheck", "**曾经是 ④ 未接线，阶段 114 修好**，与 PenShowPointsCheck 同源"
                    + "（同一句处理器里相邻的 `_currentPen.IsEnabled = PenEnabledCheck.IsChecked ?? true`）"
                    + "⇒ 同一次订阅缺失的两个受害者。补上订阅后拨掉它 ⇒ `_currentPen.IsEnabled = false`"
                    + "⇒ 「停用笔」0 → 1。⚠ 反方向的同步（`UpdatePenEditor()` 里 `PenEnabledCheck.IsChecked = "
                    + "_currentPen.IsEnabled`）本来就有 `_suppressPenEvents` 护着，所以它没有上面那对"
                    + "「显示选项」的同款回归",
                    ("停用笔", "1")),

                // ── 删笔：前提 `_currentPen` 完全来自上一步 ──
                Moves("BtnDeletePen", "OnDeletePen 第一行判 `_currentTrend == null || _currentPen == null` ⇒ "
                    + "**前提来自 BtnAddPen**（中间那两条笔复选框不碰 `_currentPen`）⇒ 把那支笔从 `Pens` 移除："
                    + "笔数 1 → 0、笔列表 1 → 0。⚠ 它还带走前两条刚写进模型的东西：「开显示点笔」1 → **0**、"
                    + "「停用笔」1 → **0** —— 被删的正是它们改过的那支笔，两对分量一起把"
                    + "「`_currentPen` 指的是哪一支」钉死。⚠ 本条是「顺序即语义」的又一个见证："
                    + "按字母序点它（排在 AddPen 之前）会拿到一条「提前 return」的假 Inert。"
                    + "⚠ 订阅补齐之后 `_currentPen` **也**能被 `TrendList.SelectionChanged` 装上，"
                    + "但本条序列里 `BtnAddTrend` 刚把当前趋势换成一条空的 ⇒ 那一路上装的是 null",
                    ("末条趋势笔数", "0"), ("笔列表", "0"), ("开显示点笔", "0"), ("停用笔", "0")),

                // ── 删趋势：当前趋势仍是 AddTrend 建的那条 ──
                Moves("BtnDeleteTrend", "OnDeleteTrend：把 `_currentTrend` 从 `_trends` 移除，再 "
                    + "`_currentTrend = _trends[0]`、`_currentPen = null` ⇒ 趋势数 2 → 1、趋势列表 2 → 1；"
                    + "「末条趋势笔数」0 → **2**、「笔列表」0 → **2**（表里只剩夹具那条「液位趋势」，"
                    + "它本来就有 2 支笔）—— 四个分量一起动，正好把「删掉的是新那条、留下的是夹具那条」钉死",
                    ("趋势数", "1"), ("趋势列表", "1"), ("末条趋势笔数", "2"), ("笔列表", "2"),
                    // 🩸 「笔标签类」在这里从「其它」**退回**「液位」—— 注意它退回来靠的是两跳订阅接力：
                    //    `RefreshTrendList()` 的选中 → `OnTrendSelectionChanged` 把 `_currentPen` 装上
                    //    → `RefreshPenList()` 再选中 → `OnPenSelectionChanged` → UpdatePenEditor。
                    //    摘掉 **TrendList** 那句 `SelectionChanged=` ⇒ 这里拿不到「液位」（负控 nc34：`s115-nc34.log`，红集 = 本控件 ＋ 下游 `BtnAddPen` / `BtnAddTrend` / `BtnDeleteTrend` 三条）。
                    ("笔标签类", "液位")),

                // ── 六条「未接线」：静默的第四种成色 ──
                // ── 四条「未接线」（显示选项）── 与上面两条同属「④ 事件从未订阅」──

                Moves("ChkShowLegend", "**曾经是 ④ 未接线，阶段 114 修好**：OnDisplayOptionChanged 一次读四个"
                    + "复选框（`_currentTrend.ShowLegend = ChkShowLegend.IsChecked ?? true` 是它的第一句）。"
                    + "接线前拨动它模型里的 ShowLegend 不变、而 `UpdateTrendDisplay()` 又把它从模型刷回来 ⇒ "
                    + "「拨完就弹回去」；接线后 ⇒ `ShowLegend = false` ⇒ 「关图例趋势」0 → 1。"
                    + "⚠ ⚠ 修这一族时**必须同时补反向同步的抑噪守卫**，否则 `UpdateTrendDisplay()` 写到第二枚"
                    + "就会把混合值写回模型（见上面那段注释与负控 NC9）",
                    ("关图例趋势", "1")),

                Moves("ChkShowGrid", "**曾经是 ④ 未接线，阶段 114 修好**，同一订阅缺失的第二个受害者"
                    + "（写的是 `_currentTrend.ShowGrid`）⇒ 接线后「关网格趋势」0 → 1。"
                    + "⚠ 它在**静态名字探针**里算「已覆盖」（别处提过这个名字），但本层的**运行期面**照样把它列出来"
                    + "—— 两个口径的差别见上面那段注释",
                    ("关网格趋势", "1")),

                Moves("ChkShowXAxis", "**曾经是 ④ 未接线，阶段 114 修好**，同一订阅缺失的第三个受害者"
                    + "（写 `_currentTrend.ShowXAxis`）⇒ 接线后「关X轴趋势」0 → 1。"
                    + "⚠ 它的 Content 走 `{res:Loc trend.x.axis}` ⇒ 文案是对的、控件是活的，"
                    + "只有**回写**那一半断了 —— 这类「界面看着没问题」的接线缺失正是本层存在的理由。"
                    + "⚠ 它也是那条**回归**的第一现场：抑噪守卫漏掉时，这个分量会在 `BtnAddTrend` 那一步"
                    + "从 1 跳到 2（新建的趋势被写成了「关」，负控 NC9）",
                    ("关X轴趋势", "1")),

                Moves("ChkShowYAxis", "**曾经是 ④ 未接线，阶段 114 修好**，同一订阅缺失的第四个受害者"
                    + "（写 `_currentTrend.ShowYAxis`）⇒ 接线后「关Y轴趋势」0 → 1。"
                    + "⚠ 四条合起来曾经是**整族**失效：一个处理器管四枚，一次订阅缺失就带走整族 ⇒ "
                    + "表里因此有四条各自的声明，而不是一条「一族」—— 本层的粒度是**按钮**，不是处理器",
                    ("关Y轴趋势", "1")),

                // ── 两条「选择族」收在表尾：时间窗下拉与趋势列表（阶段 115）──
                SelMoves("TimeWindowCombo", 3, "OnTimeWindowChanged：`_currentTrend.TimeWindowMs = "
                    + "TimeWindowOptions[3].Ms` = 300000 ⇒「非默认时间窗趋势」0 → 1。"
                    + "⚠ 表尾的 `_currentTrend` 是夹具那条（60000，正好是 `TimeWindowOptions[1]`），"
                    + "而下拉的初值就是 index 1（构造函数里 `InitializeTimeWindowCombo` 写的）"
                    + "⇒ 拨到 3 是一次**真的**变化。"
                    + "🩸 本条是**登记 #67** 的守卫：那条登记说的是这枚下拉的处理器**没有抑噪守卫**"
                    + "（`UpdateTrendDisplay()` 里那句 `TimeWindowCombo.SelectedIndex = i` 是程序性写它），"
                    + "与本条正交 —— 本条管的是**订阅在不在**：摘掉 `SelectionChanged=` 它立刻红（负控 nc35：`s115-nc35.log`，红集只本控件一条）",
                    ("非默认时间窗趋势", "1")),

                // ── 两条 Never：两种理由 ──
                Never("BtnToggleRecord", "**读数会抖**（与 CommunicationConfigView.BtnTestConnection 同一档）："
                    + "此刻 `_currentTrend` 是夹具那条「液位趋势」、它有 2 支 VariableId 非空的笔 ⇒ 点下去 "
                    + "StartRecording 会**真的走完全程**：new 一个 DataLogger（1000 ms 采样）＋ 构造函数里那个 "
                    + "500 ms 的 System.Threading.Timer 开始真的刷图 —— 两路都是异步的，"
                    + "「点完读到的分量属于哪个瞬间」没有因果保证。要收它得先给本层加「等静默」原语，不是改声明"),
                Never("BtnExportReport",
                    "阶段 175 新增的导出按钮：点了会开**文件选择器**（headless 没有可交互对话框）⇒ 本层不点。"
                    + "它的接线不是「没判据」—— 由 `HistoryReportExportGuardTests` 钉住（源码契约：走公共出口 "
                    + "ReportExport.SaveAsync、面板里没有写出器 switch、列名取 `_logger?.Config.VariableIds`、"
                    + "按钮在 XAML 里存在且被 `+=` 订阅）。"),
                Never("BtnExportBundle",
                    "阶段 179 新增的报表包按钮：同样先弹**目录选择器** ⇒ 本层不点"
                    + "（接线由 ReportBundleExportGuardTests 的源码契约判据钉住）。"),
                Never("BtnReportDesigner",
                    "阶段 183 新增的报表设计器按钮：点了会弹**模态对话框**（报表设计器）⇒ 本层不点"
                    + "（接线由 ReportDesignerGuardTests 的源码契约判据钉住）。"),
                Never("BtnTitleClose", "ChromeWindow 标题栏的关闭按钮（XAML 里 Click=OnTitleCloseClicked）⇒ "
                    + "点了会关掉宿主窗口"),
            }),

        // ════════════════════════════════════════════════════════════════════
        //  第八个宿主：用户管理对话框 —— 本层第一个「按钮面全在、接线却缺了一大片」的宿主
        // ════════════════════════════════════════════════════════════════════
        //
        // 面：18 个具名动作控件（9 个 Button ＋ 9 枚权限 CheckBox）。Avalonia 里
        // `CheckBox : ToggleButton : Button` ⇒ 本层的面两种都收（类注释里那条，第三层同口径）。
        //
        // 🩸 **这个宿主的取证（三路独立，都指向同一件事）**：
        //   ① `.axaml` 全文只有 4 个事件属性 —— `PointerPressed="OnTitleBarPointerPressed"`、
        //      `Click="OnTitleCloseClicked"`、两个 `SelectionChanged="On*SelectionChanged"`；
        //   ② code-behind 全文只有 **1** 句 `+=`（`confirmBtn.Click +=`）—— 那是 OnResetPassword
        //      在**运行时新建**的重置口令框里的按钮，不是这一页任何一枚；
        //   ③ `git log -S` 在**全部历史**里搜 `Click="OnAddUser"` **零命中**，且 `1fc6701`
        //      （引入本文件的那个提交）里 `.axaml` **一个事件属性都没有**
        //      ⇒ 不是哪次重构删掉的接线，是**生来就没接**。
        // ⇒ `OnAddUser` / `OnDeleteUser` / `OnToggleEnabled` / `OnUnlockUser` / `OnResetPassword` /
        //   `OnSaveRole` / `OnRefreshLog` / `OnClearLog` / `OnPermissionChanged` / `OnDetailChanged`
        //   这 **10 条处理器没有一个订阅点**。用户能看见的症状：这个对话框「能看不能改」——
        //   点用户/点角色能刷详情面板（那两个 `SelectionChanged` 是接了的），
        //   但增、删、启停、解锁、重置口令、存角色、刷日志、清日志**一个都不响应**。
        //   注意这与 TrendConfigView 那 6 条**性质相同、规模不同**：那边 6 条、这边 10 条，
        //   而且这边连「表面上像是活的」都没有（那 6 条至少还有反向同步把控件刷成模型值）。
        //   ⚠ 那 6 条已于**阶段 114 修好**（登记 #65，且因此又逼出一条回归）⇒ 本宿主的 9 枚权限复选框
        //   成了本层**唯一现存的 ④ 实例**。
        //
        // ⇒ 阶段 113 分两半处置：
        //   * **8 个按钮补回接线**（内容批）—— 处理器体是现成的、带本地化键、带 catch，
        //     只是没人接；本层这 5 条 Moves 就是那次修复的守卫（负控 NC1 摘掉接线即红）。
        //   * **9 枚权限复选框 + `OnPermissionChanged` 刻意仍不接** —— 那个处理器**本身是空壳**
        //     （首行的守卫之后什么都没有），接上去只会把「看不出来」变成「看着像有事发生」。
        //     它们在表里是 9 条 ④ 事件从未订阅，负控 NC6/NC7 把这件事钉死。
        //   * `OnDetailChanged`（显示名 ＋ 详情角色下拉改角色）在**阶段 124 已接线**（登记 #66②）——
        //     此前它全历史零引用（`DetailDisplayName.LostFocus` / `DetailRoleCombo.SelectionChanged`
        //     都没订）⇒ 改显示名与改角色**静默丢失**。本表为它加了 ⑨ 那条 `SelMoves`：
        //     选择族的那一半（角色）能进本层的面，显示名那一半（`LostFocus`）**进不了** ——
        //     静态面只扫 `SelectionChanged`（见 SelectionFaceClassificationGuardTests 的类注释
        //     「不抓什么」），`LostFocus` 不在其内。显示名那条腿由
        //     `UserProfileEditUiTests` 的**行为判据**兜着，两条腿合起来才是这件事的完整网。
        //
        // 顺序即语义（两处，都不是随手排的；排错会让负控**静默失效**）：
        //   ① `BtnRefreshLog` 必须**排第一** ——「标题尾」只有这一条能看见「空 → 非空」这个迁移；
        //      排在它后面每一条调 `ShowStatus` 的按钮看见的都是「非空」，读数上再也分不出
        //      「这次点击到底有没有反馈」。
        //   ② `PermManageUsers` 必须**紧邻**在 `BtnSaveRole` 之前 —— 保存读的是**界面当前状态**
        //      （`GetPermissionsFromUI()`），把这枚拨开是「保存会写新值」的**唯一**来源；
        //      中间只要再插一条带角色布景的声明，`ShowRoleDetail` 就会把 IsChecked 刷回模型值，
        //      于是 `BtnSaveRole` 变成「写入同值」——它的声明值 2 会独立地把它点红（这条依赖自证）。
        //
        // 夹具初态（`WindowDepot.Auth = new AuthenticationService()`）：3 个预置角色
        // （role-operator 3 级掩码 75 / role-engineer 7 级 223 / role-admin 15 级 511）、
        // 1 个用户 admin，**审计表初态是空的**。
        // 🩸 审计条目有**两级入口**，只看公开那一个会得出反向的结论：公开的 `RecordAudit(...)`
        // 全仓确实只有 MainWindow 导出那一站在调，但 `CreateUser` / `DeleteUser` / `SetUserEnabled` /
        // `ChangeUserRole` 走的是**私有** `AppendAudit(...)`（同一个 `_auditLog`、同样触发 `AuditLogged`）
        // ⇒ 用户管理的增/删/启停**都会留痕**。我第一次只搜公开入口 ⇒ 误判成「用户管理一条都不落」，
        // 被首跑读数当场打脸（见 ⑧ 那条声明的由来）。
        // ⇒ **量「谁在写」要搜写入点（`_auditLog.Add` / 私有 helper），不是搜公开入口名**。
        // 两个列表初态都没有选中项（`_selectedUser` / `_selectedRole` 都是 null）⇒
        // 凡首行判它俩的处理器都得靠**布景**装前置 —— 第三层对这批按钮只能写 `Quiet(...)`，
        // 理由逐字写着「初始未选中用户 ⇒ 提前 return」；布景原语就是为这一档加出来的。
        new("UserManagerView",
            d => new Views.UserManagerView(d.Auth),
            new[]
            {
                // ── 数据面：服务侧（AuthenticationService 是唯一真值）──
                Num("用户数", c => c.Depot.Auth.GetAllUsers().Count),
                Num("停用用户数", c => c.Depot.Auth.GetAllUsers().Count(u => !u.IsEnabled)),
                Num("锁住用户数", c => c.Depot.Auth.GetAllUsers().Count(u => u.IsLocked)),
                Num("失败次数合计", c => c.Depot.Auth.GetAllUsers().Sum(u => u.FailedLoginAttempts)),
                Num("角色数", c => c.Depot.Auth.GetAllRoles().Count),
                // 权限位两枚：一枚**具体**（谁能管用户 ⇒ BtnSaveRole 的声明值）、一枚**伞**
                // （全部角色掩码之和 ⇒ 九枚复选框里**任何一枚**真被回写都会抬起来）。
                // ⚠ 伞必须读**掩码本身**：换成「开着的角色数」这种计数会漏掉「拨掉一位」（值变小）。
                Num("开放管理用户", c => c.Depot.Auth.GetAllRoles().Count(r => r.HasPermission(Permission.ManageUsers))),
                Num("权限位合计", c => c.Depot.Auth.GetAllRoles().Sum(r => (int)r.Permissions)),
                // ── 数据面：窗口侧 ──
                Num("用户列表", c => Items(c.Window, "UserList")),
                Num("角色列表", c => Items(c.Window, "RoleList")),
                // 审计日志**拆两枚**：服务侧是唯一真值、面板侧是控件 —— 合成一枚就分不清
                // 「服务里没有」与「有但没显示到面板上」（与 ConnEnabled 的两档「没有」同一条理由）。
                Num("审计条数", c => c.Depot.Auth.GetAuditLog(500).Count),
                Num("审计面板", c => Items(c.Window, "AuditLogPanel")),
                // 本宿主没有 StatusText 那种提示行：ShowStatus 写的是窗口标题后缀
                Str("标题尾", c => TitleTail(c.Window)),
                // ── 输入面（只有这一档允许被布景动）──
                InputStr("选中用户名", c => SelectedUsername(c.Window)),
                InputStr("选中角色", c => SelectedRoleId(c.Window)),
                InputNum("新用户名长度", c => TextLen(c.Window, "NewUsername")),
                InputNum("新密码长度", c => TextLen(c.Window, "NewPassword")),
                // ── 两枚「选择族」的眼睛（阶段 115）：都读**处理器写的**出口 ──
                // `ShowUserDetail` 写 DetailUsername、`ShowRoleDetail` 写 RoleLevelSlider。
                // ⚠ 两枚都**不标** InputFace：本宿主的布景走的是同一批处理器（SelectUser / SelectRole），
                //    所以只要布景选的就是当前那一个，这两枚在布景前后**相等** —— 这一点由自检自己验。
                Str("详情用户名", c => Control<TextBlock>(c.Window, "DetailUsername").Text ?? ""),
                Num("角色等级", c => (int)Control<Slider>(c.Window, "RoleLevelSlider").Value),
            },
            new[]
            {
                // ── ① 排第一：只有它能看见「标题尾 = 空」这个初值 ──
                Moves("BtnRefreshLog", "OnRefreshLog：`RefreshAuditLog()` ＋ `ShowStatus`。"
                    + "⚠ 它在**数据面上其实一条都不动**：服务侧审计表是它**读**的源、不是它写的对象，"
                    + "而面板条数在「先清空、再按同一份数据重建」下前后相等 ⇒ 若没有「标题尾」这枚分量，"
                    + "本条只能写成一条**零信息**的 Inert。这正是本宿主给 ShowStatus 配探针的理由",
                    ("标题尾", "非空")),

                // ── ② 新增用户：本宿主唯一需要往输入框里预填的按钮 ──
                Arranged(Moves("BtnAddUser", "OnAddUser：读三个输入框（用户名/口令/显示名，显示名空则回落用户名）"
                    + "＋ 角色下拉的 Tag（`NewRoleCombo.SelectedIndex = 0` ⇒ role-operator）⇒ "
                    + "`_auth.CreateUser` 落一个新用户、清空三个输入框、把 `_selectedUser` 换成新用户、"
                    + "`RefreshUserList()` 再把它自动选中（`选中用户名` → u1）。"
                    + "⚠ 首行判「用户名或口令为空就 return」⇒ **不布景这条只能是 Inert** —— "
                    + "第三层就是这么记的（新增用户是行内草稿、不弹窗）；布景把这一档从「记夹具形状」"
                    + "救成了一次真点击。⚠ 两个输入框在点击后被清空，也一并写进声明（这正是双向口径）",
                    ("用户数", "2"), ("用户列表", "2"), ("选中用户名", "u1"),
                    ("新用户名长度", "0"), ("新密码长度", "0"), ("标题尾", "非空"),
                    // 下面两枚是首跑读数逼出来的：CreateUser 经私有 AppendAudit 落一条审计，
                    // 且 AuditLogged 让 OnAuditLogged 把面板 +1（ctor 里订的，那条订阅是接了的）
                    ("审计条数", "1"), ("审计面板", "1"),
                    // `RefreshUserList()` 末尾把 `_selectedUser` 重新选中 ⇒ SelectionChanged 再跑一次
                    // ⇒ `ShowUserDetail(u1)` 把详情面板填上。⚠ 这一条是**下游**：面扩之前它没人读。
                    ("详情用户名", "u1")),
                    c =>
                    {
                        Control<TextBox>(c.Window, "NewUsername").Text = "u1";
                        Control<TextBox>(c.Window, "NewPassword").Text = "pw1234";
                    }),

                // ── ③-a 选择族：把 `UserList` 拨回 admin（阶段 115）──
                // 位置是**语义要求**：本宿主下面的布景一律 `SelectUser("admin")`，而「详情用户名」
                // 是 `ShowUserDetail` 的出口 ⇒ 只有先把它拨成 admin，后面那些布景才**不动**这枚分量
                // （布景只许动输入面，动了「详情用户名」当场红）。
                SelMoves("UserList", 0, "OnUserSelectionChanged：`UserList` 此时是「u1 选中」（上一步 BtnAddUser "
                    + "末尾装的），列表按用户名排序 ⇒ index 0 是 admin。拨过去 ⇒ `ShowUserDetail(admin)`："
                    + "「详情用户名」u1 → admin。"
                    + "🩸 见证用 `DetailUsername.Text`（处理器写）而**不是**「选中用户名」—— 后者读的是 "
                    + "`UserList.SelectedItem`，是手势自己拨的，摘掉订阅照样变 ⇒ 零信息。"
                    + "后者也一并声明，因为它的变化本来就得被记账（它是输入面分量）",
                    ("详情用户名", "admin"), ("选中用户名", "admin")),

                // ── ③ 启用/停用：首行判 `_selectedUser`，靠布景装上 ──
                Arranged(Moves("BtnToggleEnabled", "OnToggleEnabled：`_auth.SetUserEnabled(id, !IsEnabled)` ⇒ "
                    + "admin 由启用变停用（`停用用户数` 0 → 1）。⚠ 它**丢掉返回值** —— "
                    + "`SetUserEnabled` 返回 bool 却被无视，用户不存在时静默无操作；本层读不到这一档"
                    + "（夹具里它一定存在），是**自陈的盲区**，不是被证过的结论",
                    ("停用用户数", "1"), ("标题尾", "非空"),
                    ("审计条数", "2"), ("审计面板", "2")),
                    c => SelectUser(c.Window, "admin")),

                // ── ④ 解锁：本宿主第一枚「③ 写入同值」──
                Arranged(Inert("BtnUnlock", "**③ 写入同值**：OnUnlockUser 写 `IsLocked = false` 与 "
                    + "`FailedLoginAttempts = 0`，而夹具的 admin **本来就没锁、失败次数本来就是 0** ⇒ "
                    + "两处赋值都写回原值，数据面一字不动。⚠ 它还是本宿主里**唯一绕过 `_auth` 直接写模型**的处理器"
                    + "（`AuthenticationService` 根本没有 UnlockUser 方法）⇒「解锁」这件事不落审计、"
                    + "也不经过任何服务层契约。⚠ 反方向是通的：ShowUserDetail 按锁定态开关 `BtnUnlock.IsVisible`，"
                    + "夹具里它初始**不可见**，而本层照样点（RaiseEvent 不看可见性）——"
                    + "这正是「看得见外壳、看不见接线」的写照。夹具给 admin 上锁它当场变红（负控 NC8）。"
                    + "⚠ 它和 BtnSaveRole 是本宿主里**仅有的两处绕过服务层、直接改模型**的处理器 ⇒ "
                    + "两处都**不落审计**；而 BtnToggleEnabled 改的同样是用户，却落了 2 条。"
                    + "「审计条数」在几条声明里一起读得到这个差分（双向口径把它变成活的：谁给解锁/存角色"
                    + "补上服务层调用，这里就红）"),
                    c => SelectUser(c.Window, "admin")),

                // ── ④-a 选择族：把 `RoleList` 拨到 engineer（阶段 115）──
                // 位置同样由**下游**决定：这一族下面 9 条权限复选框的布景全是 `SelectRole("role-engineer")`，
                // 而 `ShowRoleDetail` 会写 `RoleLevelSlider` ⇒ 先在这里把它拨成 engineer，
                // 后面那些布景写的就是**同一个值**，不会把「角色等级」这枚数据面分量抬起来（否则红）。
                // ⚠ `RefreshRoleList` 按等级**降序**排（admin 15 / engineer 7 / operator 3）⇒ engineer = index 1。
                SelMoves("RoleList", 1, "OnRoleSelectionChanged：`RoleList` 初态**没有选中项**（见本宿主那段注释），"
                    + "拨到 engineer ⇒ `ShowRoleDetail(engineer)` 把 `RoleLevelSlider.Value` 写成它的等级 7 "
                    + "⇒「角色等级」0 → 7。"
                    + "🩸 见证用 `RoleLevelSlider.Value`（处理器写、且与界面语言无关）—— 不用 "
                    + "`RoleNameBox.Text` / `RoleDescText.Text`（那两个走 `RoleTextKeys` 字典，"
                    + "读它们等于把一份本地化结果冻进判据）",
                    ("角色等级", "7"), ("选中角色", "role-engineer")),

                // ── ⑤ 权限复选框族的头一枚；它的布景同时是 ⑥ 的前置 ──
                Arranged(Inert("PermManageUsers", "**④ 事件从未订阅**（前三种成色是未被观测 / 提前 return / 写入同值）："
                    + "`.axaml` 里这 10 枚 CheckBox **一个事件属性都没有** ⇒ 拨动它不调用任何处理器、"
                    + "数据面一字不动。⚠ 它比 TrendConfigView 那 6 条（阶段 114 已修）**更深一层**：那 6 条至少各有一个"
                    + "真会写模型的处理器、缺的只是订阅；这一枚的处理器 `OnPermissionChanged` **本身是空壳**"
                    + "（`if (_isUpdatingRole || _selectedRole == null) return;` 之后一行都没有）"
                    + "⇒「补订阅」并不是唯一缺的那件东西（负控 NC7 实测：只补订阅它照样绿）。"
                    + "⚠ 本条的布景同时给 ⑥ 装前置：`_selectedRole` 只能由 RoleList.SelectionChanged 装上"),
                    c => SelectRole(c.Window, "role-engineer")),

                // ── ⑥ 保存角色：读的是界面当前状态 ⇒ 必须紧跟 ⑤ ──
                Moves("BtnSaveRole", "OnSaveRole：`_selectedRole.Level = (int)RoleLevelSlider.Value`（布景刚把它刷成 7 "
                    + "⇒ 写入同值）＋ `_selectedRole.Permissions = GetPermissionsFromUI()` —— 后者把 10 枚复选框"
                    + "**当前的 IsChecked** 重新拼成掩码。上一条刚把 PermManageUsers 拨开 ⇒ engineer 的掩码 "
                    + "255 → **767**（阶段 275 起多了 `ShelveAlarm` 0x200 一位）：`开放管理用户` 1 → 2、"
                    + "`权限位合计` 1865 → 2377 两枚一起动，"
                    + "正好把「写进去的是整张掩码、不是某一枚复选框」钉死。⚠ 两条副作用也一并声明："
                    + "`RefreshRoleList()` **没有** RefreshUserList 那样的回选逻辑 ⇒ 存完角色列表的选中项被丢掉"
                    + "（`选中角色` → 空），这是界面上真看得见的毛刺，不是读数噪声。"
                    + "⚠ 它直接改 `_selectedRole`（不调 `_auth`）⇒ **不落审计**（`审计条数` 仍是 2）",
                    ("开放管理用户", "2"), ("权限位合计", "2377"), ("选中角色", ""), ("标题尾", "非空")),

                // ── ⑦ 余下 8 枚：与 ⑤ 同源，逐枚登记（本层粒度是**控件**，不是处理器）──
                Arranged(Inert("PermViewScreen", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 它对应「查看画面」—— 拨掉它看起来像收回了最基础的权限，实际一字未改；"
                    + "而且拨完**界面不回弹**（这一族连反向同步都没有）⇒ 用户看不到任何异常"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermOperateControl", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 这一位 Operator 与 Engineer 都有 ⇒ 在掩码读数上它与「本来就开着」不可分辨，"
                    + "本层靠「拨动后一字不变」才把它与已接线的那种区分开"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermEditRecipe", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 拨掉它再点保存，engineer 就真丢了配方编辑 —— 但那一步是 BtnSaveRole 的效果，"
                    + "与本枚无关；本层把两者分开登记，正是为了不让「保存」替这九枚担保"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermAcknowledgeAlarm", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 连同下面四枚，十枚里有九枚对 engineer 的掩码是**已置位**的 ⇒ 拨动它们只会让"
                    + "`GetPermissionsFromUI()`（若被调用）算出更小的值；本枚自己不调用它"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermChangeConfig", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 本枚是「十枚里只有一枚会红」这件事的边界见证：负控 NC6 一次注入让九条**一起**红，"
                    + "靠的是共用的伞探针「权限位合计」——单靠「开放管理用户」只能点着 PermManageUsers 那一枚"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermViewAlarmHistory", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 它与 PermViewScreen 只差一个位，在界面上同样是「看着像有权、其实点了没用」"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermExportData", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 这一位在生成侧是**审计导出**那条链路的前置（MainWindow 的导出站在 RecordAudit）"
                    + "⇒ 拨掉它本该收紧导出，而实际上什么都没发生"),
                    c => SelectRole(c.Window, "role-engineer")),

                Arranged(Inert("PermDebugMode", "④ 事件从未订阅，与 PermManageUsers 同源。"
                    + "⚠ 这是 Engineer **没有**的一位（掩码 223 里不含 256）⇒ 拨开它再保存会让工程师拿到"
                    + "调试模式，是本宿主里后果最重的一次误授权；本枚自己不写，写它的是 BtnSaveRole"),
                    c => SelectRole(c.Window, "role-engineer")),
                Arranged(Inert("PermShelveAlarm", "④ 事件从未订阅，与 PermManageUsers 同源（**阶段 275 新增的第 10 枚**：搁置权限位 `ShelveAlarm` 0x200，与「确认报警」分开 ——搁置会把报警静音）。⚠ 它与 `PermAcknowledgeAlarm` 只差一位，界面上同样「看着像有权、其实点了没用」"),
                    c => SelectRole(c.Window, "role-engineer")),


                // ── ⑧ 清空日志：正好把「只清 UI、不碰服务侧」钉成一对分量 ──
                Moves("BtnClearLog", "OnClearLog 只 `_auditEntries.Clear()` ＋ 一条状态文案 —— 它自己的注释写着"
                    + "「审计日志设计为 append-only，这里的『清空』仅指 UI 显示」。"
                    + "⚠ 本条是**声明被实测改过**的一处：本层首跑前它写的是 `Inert`（成色③ 写入同值），"
                    + "依据是「夹具的审计表是空的」—— 而那个依据本身错了（漏了私有 AppendAudit）。"
                    + "真跑一次：前面的 BtnAddUser / BtnToggleEnabled 各落一条 ⇒ 到这里面板是 2 ⇒ "
                    + "清空**真的动了**（2 → 0）。现在把两枚分量一起声明，正好把「只清 UI」钉死："
                    + "`审计面板` 2 → 0 而 `审计条数` 2 → 2（服务侧一字未动）",
                    ("审计面板", "0"), ("审计条数", "2")),

                // ── ⑨ 选择族：详情面板的角色下拉（阶段 124，#66② 接线）──
                // 位置在 **BtnClearLog 之后**：前面的 `审计条数` / `审计面板` 声明用的都是绝对值，
                // 本条的处理器**会写审计** ⇒ 插在前面会把那一串数全部顶掉（而它们记的是别的行为）。
                // 布景：`_selectedUser` 是 admin（BtnToggleEnabled 那一步装的，之后无人改动），
                // 而详情下拉此刻显示的就是 admin 的 role-admin ⇒ 拨到 index 1 必为**换角色**。
                // 条目序 = `_auth.GetAllRoles()`（预置三角色按 BuiltInAll 注册）⇒ 1 = role-engineer。
                // 🩸 见证必须读**处理器写的**出口：`审计条数` / `审计面板` 是
                // `ChangeUserRole → 私有 AppendAudit → AuditLogged → OnAuditLogged` 的手印。
                // ⚠ 不声明 `选中角色`（那是 RoleList 的选中，本手势根本不碰）也不声明任何
                // `DetailRoleCombo` 自己的选中态 —— 那是手势自己拨的，摘掉订阅照样变（零信息）。
                SelMoves("DetailRoleCombo", 1,
                    "OnDetailChanged 的**角色臂**（阶段 124 接上的两条订阅之一）：读 "
                    + "`(DetailRoleCombo.SelectedItem as ComboBoxItem)?.Tag` ⇒ 与 `user.RoleId` 不同 ⇒ "
                    + "`_auth.ChangeUserRole(admin, role-engineer)`。它经**私有** `AppendAudit` 落一条 "
                    + "`UserPermissionChanged` ⇒ 服务侧 2 → 3；`AuditLogged` 让 `OnAuditLogged` 把面板 "
                    + "+1 ⇒ 0 → 1（BtnClearLog 刚把面板清成 0，所以这一步的迁移是**空 → 非空**，不是噪声）。"
                    + "⚠ 本条的**反向**也一并钉住：`UpdateUserDisplayName(admin, DetailDisplayName.Text)` "
                    + "在文本等于模型值时**不写审计**（否则「拨一下下拉」会多留一条空记录）—— "
                    + "面板若多出第 2 条，两枚分量一起红",
                    ("审计条数", "3"), ("审计面板", "1")),

                // ── ⑩ 三条不点，三种理由 ──
                Never("BtnDeleteUser", "走 `UiOperationGuard.Run` ＋ `AppDialogs.ConfirmAsync`：确认框是**平台桩**，"
                    + "`Pump(5)` 决定不了它的完成时刻，异步体落在哪一帧没有因果保证 ⇒ 读数会抖"
                    + "（与 SettingsDialog / CommunicationConfigView 的同类档同一条理由）"),
                Never("BtnResetPassword", "OnResetPassword 在**运行时新建**一个模态窗口并 `dialog.ShowDialog(this)`；"
                    + "口令校验与 `SetUserPassword` 都在那个窗口的按钮里 ⇒ 本层够不到，且它会挡住宿主"),
                Never("BtnTitleClose", "ChromeWindow 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 9 个宿主：AlarmListView（报警列表对话框）—— 阶段 118
        //
        //  为什么挑它（阶段 118 实测）：它是**当时**面外 16 个视图里**与第五层同形**的那一支 ——
        //  接线完整（两个筛选下拉 ＋ 两个按钮，全在 code-behind 里 `+=`）、写入面窄
        //  （筛选 → 只在 UI 列上过滤；确认 → 引擎里改一条记录的状态；清除 → 引擎里删已恢复的），
        //  且**没有任何测试驱动过它的这四个接线**（278 个测试文件里零引用）。
        //
        //  ⚠ 与面外最大的那处 `PropertyPanelView` 的对比见静态守卫的普查表注释：那一处的
        //    数据面已由 `PropertyPanelWiringTests` 按 26 个控件类型场景覆盖，且它的
        //    `SaveProperties()` 是**批量写**（实测点一个下拉会连带写 6 个字段）⇒ 与本层
        //    「未声明的分量一字不变」这条核心断言**冲突**，不宜硬塞进来。
        // ══════════════════════════════════════════════════════════════════════════
        new("AlarmListView",

            // 夹具：先**确定性地造三条记录**（两条 Active ＋ 一条 Returned）。不造数的话，
            // 拨筛选下拉会让「列表条数」纹丝不动 ⇒ 声明恒真，等于零信息。
            d =>
            {
                SeedAlarmRecords(d);
                return new Views.AlarmListView(d.Alarms);
            },

            new Probe[]
            {
                Num("列表条数", c => Items(c.Window, "AlarmList")),
                Num("引擎条数", c => c.Depot.Alarms.GetAllRecords().Count),
                Num("活动数", c => c.Depot.Alarms.GetAllRecords().Count(r => r.State == AlarmState.Active)),
                Num("已确认数", c => c.Depot.Alarms.GetAllRecords().Count(r => r.State == AlarmState.Acknowledged)),
                Num("已恢复数", c => c.Depot.Alarms.GetAllRecords().Count(r => r.State == AlarmState.Returned)),
                Str("空提示", c => Control<TextBlock>(c.Window, "EmptyHint").IsVisible ? "显示" : "隐藏"),

                // 选中项是**输入面**（布景的合法落点），但它同时是一条真实行为的见证 ——
                // 见第 ① 条声明：确认之后 RefreshData 会 Items.Clear() 重建整列 ⇒ 选中被丢掉。
                InputNum("选中下标", c => Control<ListBox>(c.Window, "AlarmList").SelectedIndex),

                Voice("状态文案", c => Control<TextBlock>(c.Window, "StatusText").Text ?? ""),
            },

            new ClickSpec[]
            {
                // ── ① 确认列表里选中那条 ──
                // 声明的是**计数**而不是「哪一条」：按名字断言会把「哪条排在 index 0」这个
                // **夹具细节**冻进声明里，而它并不是本按钮的行为。顺序的确定性由
                // SeedAlarmRecords 保证（见那个方法的 xmldoc）。
                // ⚠ `选中下标 → -1` 不是凑数：RefreshData 走 `AlarmList.Items.Clear()` 重建整列
                //   ⇒ 选中被丢掉（用户确认一条之后，要重新点一次才能确认下一条）。
                //   本条是这条真实行为在本层**唯一**的见证。
                Arranged(Moves("BtnAcknowledge",
                        "OnAcknowledge 读 AlarmList.SelectedItem 的 Tag（AlarmRecord）→ "
                        + "_engine.AcknowledgeAlarm(record.Id)，**成功才** RefreshData()。"
                        + "⚠ 它读的是**选中项**而不是某个确定的对象 ⇒ 布景必须先装上选中项；"
                        + "而 AcknowledgeAlarm 只对 State==Active 返回 true ⇒ 夹具要把一条 Active 排到 index 0，"
                        + "否则本声明会静默变成 flaky（确认失败、计数不动）。",
                        ("已确认数", "1"), ("活动数", "1"), ("选中下标", "-1")),
                    c => Control<ListBox>(c.Window, "AlarmList").SelectedIndex = 0),

                // ── ②③ 状态筛选：拨到「已确认」再拨回「全部」 ──
                // 下标含义由 XAML 的项序决定（all/active/acked/cleared）；比对那一侧走
                // Lang.AlarmFilterAcked ⇄ "alarm.filter.acked"，与 XAML 的 {res:Loc alarm.filter.acked}
                // 是**同一个键**（已核，不是同一个值的两处抄写）。
                SelMoves("CmbStateFilter", 2,
                    "OnFilterChanged 读两个下拉的选中项 → _stateFilter / _severityFilter → ApplyFilters() "
                    + "→ RenderAlarmList()。本条只动 UI 那一列：`引擎条数` 保持 3、`已恢复数` 保持 1 "
                    + "就是它的**反向见证** —— 筛选若被误写成「顺带删记录」，这两条会红。",
                    ("列表条数", "1")),
                SelMoves("CmbStateFilter", 0,
                    "拨回「全部」。同一控件被驱动第二次 —— 唯一性键是 (名字, 手势下标)，不是名字。",
                    ("列表条数", "3")),

                // ── ④⑤ 严重级别筛选 ──
                SelMoves("CmbSeverityFilter", 3,
                    "同上，但换严重级别这一维：只剩 Critical 那条 ⇒ 1 条。与 ②③ 合起来证明两个下拉"
                    + "**各自**都接到了同一个 OnFilterChanged（而不是其中一个空转）。",
                    ("列表条数", "1")),
                SelMoves("CmbSeverityFilter", 0, "拨回「全部」。", ("列表条数", "3")),

                // ── ⑥ 清除已恢复 ──
                // 本宿主里**唯一**真正删记录的一步（对照 ②③：筛选只动 UI）。
                Moves("BtnClearReturned",
                    "OnClearReturned 直接 _engine.ClearReturnedRecords() 再 RefreshData()。"
                    + "`已恢复数 1 → 0` 与 `引擎条数 3 → 2` 成对，把「**只清 Returned**、"
                    + "Active 与已确认那两条一字未动」钉死（活动数 1、已确认数 1 都没被声明改变）。",
                    ("引擎条数", "2"), ("已恢复数", "0"), ("列表条数", "2")),

                // ── ⑦ 不点：导出报表 ──
                // 点了会开**文件选择器**（headless 里没有真实对话框）⇒ 本层不点。
                // ⚠ 它的接线不是"没判据"：由 `AlarmReportExportGuardTests` 钉住
                //   （源码契约：两个面板都走 ReportExport.SaveAsync、面板里没有写出器 switch；
                //     ＋ 按钮在 XAML 里存在且被 `+=` 订阅）。
                Never("BtnExportReport",
                    "按钮把当前筛选后的记录交给 ReportExport.SaveAsync ⇒ 先弹文件选择器；"
                    + "headless 下没有可交互对话框 ⇒ 本层不点（接线由源码契约判据覆盖）。"),

                // ── ⑧ 不点 ──
                Never("BtnExportBundle",
                    "阶段 179 新增的报表包按钮：先弹**目录选择器** ⇒ 本层不点"
                    + "（接线由 ReportBundleExportGuardTests 的源码契约判据钉住）。"),
                Never("BtnReportDesigner",
                    "阶段 183 新增的报表设计器按钮：点了会弹**模态对话框**（报表设计器）⇒ 本层不点"
                    + "（接线由 ReportDesignerGuardTests 的源码契约判据钉住）。"),
                Never("BtnTitleClose",
                    "ChromeWindow 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 UserManagerView 同形）。"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 10 个宿主：CrossReferenceView（交叉引用面板）—— 阶段 126
        //
        //  为什么挑它（**按判据挑，不按个数挑** —— 阶段 118 明确推翻过「最大 ≠ 最该扩」）：
        //   ① **数据面明确且可读**：唯一的写入是 `Search()` 往一个 ObservableCollection 里塞行，
        //      于是「结果条数」就是处理器**亲手写的**那一格 ⇒ 差量判据写得出来（不是"未被观测"）；
        //   ② **零驱动**：实测 `FindBtn` / `TargetTypeCombo` / 甚至 `Search()` 本身在 278 个测试
        //      文件里**一处都没有**被驱动过（`grep` 零命中）—— 这正是阶段 113 那条
        //      「名字级残余会骗人」要盯的形状：它在普查表里只是"1 个接线控件"这一格；
        //   ③ **写入面窄**：`Search()` 只清空 + 重建自己的结果集合，不碰工程、不碰服务；
        //   ④ **代价最小**：面内只有 1 个具名 Button ＋ 1 个带选择接线的 ComboBox
        //      ⇒ 3 条声明就能把这一格从"个数台账"变成"行为判据"；
        //   ⑤ 🩸 **它顺带把一类装配机制建起来**：**当时**面外 16 个视图里 **9 个是 `UserControl`**、
        //      7 个是 `ChromeWindow`（**实测**：`partial class X : <基类>` 逐个读出来的；
        //      ⚠ 我第一稿把这里写成"12 个"，那是凭印象写的 —— 同一族的教训见阶段 118 的
        //      「先确认你在读的东西真的是你要的那个信号」）⇒ 不放开"宿主必须是 Window"，
        //      那 9 个（本宿主升格后还剩 8 个）永远只能停在个数账本上。
        //      本宿主是**第一个 `UserControl` 宿主**，机制的两处改动见 `Control<T>` 与
        //      `BucketTables_AreNotEmptiedAndAreSelfConsistent` 里的宿主类型断言。
        //
        //  ⚠ 刻意**不**挑 `PropertyPanelView`（31 个接线控件、面外最大）：阶段 118 已实测三条
        //    反对理由（数据面已被 `PropertyPanelWiringTests` 按 26 个场景覆盖 ⇒ 主要是重复记账；
        //    `SaveProperties()` 是**批量写**、与本层「未声明的分量一字不变」正面冲突；
        //    扩它的真实成本是 15 按钮 ＋ 31 下拉 ＝ 46 条声明）。也不挑 `DeviceManagerView`
        //    （15 个具名按钮）—— 代价与收益不成比例。
        //
        //  夹具：`SeedCrossReferenceFixture(depot)` 往 `depot.Project` 里铺**两条**对「液位」的
        //  绑定。不铺这一层，`Search()` 永远返回 0 条，三条声明就全成「0 → 0」的零信息断言。
        //  初态：`TargetTypeCombo.SelectedIndex = 0`（Variable，XAML 里写死）、SearchBox 空、
        //  结果 0 条。
        // ══════════════════════════════════════════════════════════════════════════
        new("CrossReferenceView",

            d =>
            {
                SeedCrossReferenceFixture(d);
                var view = new Views.CrossReferenceView();
                view.LoadProject(d.Project);      // 生产路径：宿主在工程切换时调它
                return new Window { Content = view, Width = 900, Height = 600 };
            },

            new Probe[]
            {
                // 数据面：处理器写的唯一出口（`Search()` 只清空 + 重建这个集合）
                Num("结果条数", c => Items(c.Window, "ResultList")),
                // 输入面：手势自己拨的那个下拉（`Tag` 是 "Variable"/"Screen"/"Script"）
                InputStr("目标类型", c => (Control<ComboBox>(c.Window, "TargetTypeCombo").SelectedItem
                                            as ComboBoxItem)?.Tag as string ?? ""),
                // 输入面：关键词框（布景的唯一落点）
                InputNum("关键词长度", c => TextLen(c.Window, "SearchBox")),
            },

            new ClickSpec[]
            {
                // ── ① 唯一具名按钮：读关键词框 ⇒ 按当前类型搜 ⇒ 结果集合被重建 ──
                Arranged(Moves("FindBtn",
                    "OnClick（ctor 里 `FindBtn.Click += (_, _) => Search();`）⇒ `Search()`："
                    + "读 `SearchBox.Text`（布景填「液位」）＋ `TargetTypeCombo` 的 **Tag**"
                    + "（初值 Variable）⇒ `CrossReferenceAnalyzer.FindVariableReferences` ⇒ "
                    + "结果集合 0 → **2**（夹具铺了两条绑定）。"
                    + "⚠ 首行判「关键词为空就 return」⇒ **不布景这条只能是 Inert**（记的是夹具形状、"
                    + "不是接线）—— 布景把这一档救成了一次真驱动，与 CommunicationConfigView 那三条同形。"
                    + "⚠ 声明的是**结果条数**（处理器写的），不是下拉自己的选中态。",
                    ("结果条数", "2")),
                    c => Control<TextBox>(c.Window, "SearchBox").Text = "液位"),

                // ── ② 选择族：换搜索**类型**（同一个关键词、同一个处理器）──
                SelMoves("TargetTypeCombo", 1,
                    "OnSelectionChanged（ctor 里 `TargetTypeCombo.SelectionChanged += (_, _) => Search();`）⇒ "
                    + "`Search()` 按 **Tag** 分派到 `FindScreenReferences`。关键词仍是「液位」，"
                    + "而夹具里没有叫「液位」的画面 ⇒ 结果条数 **2 → 0**。"
                    + "🩸 这一条是本宿主**唯一**能把「按类型分派」与「恒定按变量搜」分开的腿："
                    + "下一条拨回去会回到 2，两条合起来才证明分派真的读了 Tag（而不是恰好为空）。"
                    + "⚠ 同时声明「目标类型」（输入面分量，手势自己写的）—— 不声明就是未声明分量外溢",
                    ("结果条数", "0"), ("目标类型", "Screen")),

                SelMoves("TargetTypeCombo", 0,
                    "拨回 Variable ⇒ `Search()` 再跑一遍 ⇒ 结果条数 **0 → 2**。"
                    + "同一控件被驱动第二次 —— 唯一性键是 (名字, 手势下标)，不是名字。"
                    + "⚠ 关键词框里仍是「液位」（没人清它）⇒ 这条不依赖布景",
                    ("结果条数", "2"), ("目标类型", "Variable")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 11 个宿主：DownloadToHmiView（下载到设备对话框）—— 阶段 127
        //
        //  为什么挑它（**同一套判据**，与第 10 个宿主逐条对齐）：
        //   ① **数据面明确且可读**：`OnChannelTypeChanged` 写的是**两个命名容器的可见性**
        //      （`SerialPanel` / `TcpPanel`）—— 处理器**亲手写的**、且与界面语言**无关**的读数
        //      （同 `CommunicationConfigView` 的 `BatchAddPanel` / `BannerState`）；
        //   ② **零驱动**：`CmbChannelType` 与它的处理器在全部测试文件里 `grep` 零命中；
        //   ③ **写入面窄**：只翻转两个 `IsVisible`，不碰设备、不碰工程、不落盘；
        //   ④ 与该视图的**设备动作**一刀切开：真碰硬件的三个如实登记成 `Never`。
        //
        //  🆕 **它还是第一个"接线写在 XAML 属性里"的新宿主**：本阶段新加的两个宿主恰好是两种形态的
        //     对照 —— `CrossReferenceView` 是 code-behind `+=`，它是 `SelectionChanged="…"` /
        //     `Click="…"`。静态守卫把两种形态都算进面（判据三的合成语料也钉着这一点），
        //     但**运行期**这一层此前没有任何"只靠 XAML 属性接线"的宿主 ⇒ 这一格是新的。
        //
        //  夹具：真构造（两个参数都是纯数据）—— ctor 里 `RefreshPorts()` 读 `SerialPort.GetPortNames()`
        //  （无设备时为空、**不抛**）、`DetectSharedLibrary()` 只做 `File.Exists` 探测；
        //  ctor 末尾往日志推一条"待下载摘要" ⇒ 初值 `日志条数 = 1`。
        //
        //  初态：`CmbChannelType.SelectedIndex = 0`（串口，XAML 写死）⇒ `SerialPanel` 显示、
        //  `TcpPanel` 隐藏；`_isInitializing` 把 XAML 装载期那次初始 `SelectionChanged` 挡掉了
        //  ⇒ 初态那两条读数不是"某次事件"的产物。
        // ══════════════════════════════════════════════════════════════════════════
        new("DownloadToHmiView",

            d => new Views.DownloadToHmiView(
                outputFiles: new[] { "main.c", "ui.c" },
                outputDir: d.DataRoot,
                assets: null,
                runtimeConfigText: null),

            new Probe[]
            {
                // ── 数据面：两个容器的可见性（处理器写的、与语言无关）──
                Str("串口面板", c => Control<Grid>(c.Window, "SerialPanel").IsVisible ? "显示" : "隐藏"),
                Str("网口面板", c => Control<StackPanel>(c.Window, "TcpPanel").IsVisible ? "显示" : "隐藏"),
                // ── 数据面：日志条数。🩸 它在本层**永远读 0**，而这正是下面 `BtnRefreshPorts`
                //    那条 `Inert` 的**理由本身**：`Log` 走 `Dispatcher.UIThread.Post`，而本层的
                //    `Pump` 只推**渲染定时器**、**不抽派发队列**（`UiTestHost.TickRenderTimer`）
                //    ⇒ 凡"把效果 Post 出去"的处理器，本层原理上读不到（"未被观测"成色）。
                //    刻意留着它：换了别人来看这份表，这一格就是那条结论的证据。
                Num("日志条数", c => Items(c.Window, "LogBox")),
                // ── 输入面：手势自己拨的那个下拉 ──
                InputNum("通道类型", c => Control<ComboBox>(c.Window, "CmbChannelType").SelectedIndex),
                // 只进读数：状态栏文案（走字典，冻结它等于把一份本地化结果抄进判据）
                Voice("状态文案", c => Control<TextBlock>(c.Window, "TxtStatus").Text ?? ""),
            },

            new ClickSpec[]
            {
                // ── ① 选择族：换到"以太网" ⇒ 两个面板互换可见性 ──
                SelMoves("CmbChannelType", 1,
                    "OnChannelTypeChanged（**XAML 属性**接线）读 `CmbChannelType.SelectedIndex == 0` ⇒ "
                    + "`SerialPanel.IsVisible = false` ＋ `TcpPanel.IsVisible = true` ⇒ 两条读数**一起**翻。"
                    + "⚠ 首行判 `_isInitializing` ⇒ XAML 装载期那次事件被挡掉（这正是初态「串口显示」"
                    + "不是事件产物、而是 XAML 默认值的原因）。"
                    + "🩸 两条一起声明不是凑数：只声明一条的话，「只改了一半」照样绿",
                    ("串口面板", "隐藏"), ("网口面板", "显示"), ("通道类型", "1")),

                SelMoves("CmbChannelType", 0,
                    "拨回「串口」⇒ 两条读数**一起**翻回来（同一控件第二次驱动 —— 唯一性键是 "
                    + "(名字, 手势下标)）。",
                    ("串口面板", "显示"), ("网口面板", "隐藏"), ("通道类型", "0")),

                // ── ② 刷新端口：**两个原因叠在一起**，所以只能如实记 Inert ──
                Inert("BtnRefreshPorts",
                    "**两个原因叠在一起，所以只能记 Inert**（别只写一个）："
                    + "① `RefreshPorts()` 在本环境无串口设备 ⇒ `SerialPort.GetPortNames()` 返回空 ⇒ "
                    + "端口列表 **0 → 0**（写入同值）；"
                    + "② `LogTagged(...)` **确实**会往 `_logs` 推一条，但 `Log` 走 "
                    + "`Dispatcher.UIThread.Post` —— 而本层的 `Pump` 只推**渲染定时器**、"
                    + "**不抽派发队列**（见 `UiTestHost.TickRenderTimer`）⇒ 那一条在本层**读不到**"
                    + "（`日志条数` 实测 0，见读数行）。"
                    + "⚠ 后者是**本层的能力边界**（「未被观测」成色），不是「没接线」：摘掉 XAML 里那个 "
                    + "`Click=\"OnRefreshPortsClicked\"` 也不会让这一条变红 —— 这正是它必须写成 "
                    + "`Inert` 而不是 `Moves` 的原因"),

                // ── ③ 四个不点：三种理由分别写清 ──
                Never("BtnOpenClose",
                    "`async void` ＋ 真去 `_channel.Open()`（需实机通道）：完成时刻不由 `Pump` 决定 ⇒ "
                    + "读数会抖（与 `CommunicationConfigView.BtnTestConnection` 同一条理由）"),
                Never("BtnDownload",
                    "同上：`async void` ＋ 需要已打开的通道 ＋ 进度/状态由设备回包推进 ⇒ 本层够不到，"
                    + "而且它一旦真跑起来会去写 /mnt/UDISK"),
                Never("BtnBrowseSo",
                    "`OnBrowseSoClicked` 会弹**文件选择器**（平台桩）⇒ headless 下不归 `Pump` 管"),
                Never("BtnTitleClose",
                    "ChromeWindow 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 UserManagerView 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 12 个宿主：LayerPanelView（图层面板）—— 阶段 128
        //
        //  **第一个"事件型数据面"的宿主**（阶段 127 A/B/C 分档里的 **B 档**）。用户拍板允许本层
        //  加一层"订阅视图公开事件"的观测面（`EventObserved`），这就是它的第一次落地。
        //
        //  🩸 **本阶段先走了一次弯路，路标留在这里**：第一版挑的是 `VariableBrowserView`
        //  （载荷同样是字符串、面前只有 1 按钮 ＋ 1 下拉，看着更便宜），落地后才发现
        //  **`new VariableBrowserView()` 全仓零构造、它的 `VariableSelected` 全仓零订阅**
        //  ⇒ 那是一个**用户到不了的面板**。判它等于把"本层覆盖了多少"记成假的。
        //  已撤回，并给挑宿主补上**第 ⑤ 条判据**：「这个视图在生产里**真的会被构造出来吗**」。
        //  （与阶段 118 的「构造 ≠ 驱动」是同一族的**下一层**：那次是"构造了没驱动"，
        //    这次是"连构造都没有"。登记见 #71。）
        //
        //  为什么挑它（同一套判据 + 第 ⑤ 条）：
        //   ① **生产里真的会被构造**：`HmiDockFactory:304` ＋ `MainWindow.axaml.cs:231` 两处
        //      `new LayerPanelView()`；② **数据面双重可读** —— 选中那条腿由事件观测探针读
        //      （`SelectionChanged` 的载荷是 `HmiControl?`），而四个按钮**直接写模型**
        //      （`LayerIndex` / `IsLocked`）⇒ 有"不是事件"的硬读数；③ **零驱动**：
        //      `LayerList` 与那四个按钮在全部测试文件里 `grep` 零命中；
        //   ④ 面前是 1 个 ListBox ＋ **4 个具名按钮**（行内的可见/锁定小按钮是**代码构造的、
        //      没有名字** ⇒ 不进本面，与 `VariableManagerView` 的那批同形）。
        //
        //  ⚠ **这条（以及 `EventObserved` 的每一条）判的是哪一跳**：判的是
        //  「`OnSelectionChanged` 读到的 `LayerList.SelectedItem` 是不是**正确的那一个**、
        //  载荷传对了没有」；**判不到**「构造函数里 `SelectionChanged += … RaiseLayerSelectionChanged`
        //  那层往总线的转发」（正控 PC1 实测：摘掉转发，本判据**照样绿**）。那一跳属宿主装配。
        //
        //  夹具：`LoadScreen(d.Screen)` —— 主画面只有 **1 个**控件（状态灯）⇒ 层列表 1 条。
        //  ⚠ 这个"1 个图层"是**夹具形状**、也是本宿主筛选器的一部分：`BtnMoveUp`/`BtnMoveDown`
        //  在单图层下**仍然可观测**（直接改 `LayerIndex`），而"多图层重排"那条路径在这里测不到
        //  ⇒ 想测它要另铺夹具（登记为后续项，见 §50.6）。
        // ══════════════════════════════════════════════════════════════════════════
        new("LayerPanelView",

            d =>
            {
                var view = new Views.LayerPanelView();
                view.LoadScreen(d.Screen);
                return new Window { Content = view, Width = 300, Height = 500 };
            },

            new Probe[]
            {
                // ── 数据面：四个按钮**直接写**的模型格 ──
                Num("图层数", c => Items(c.Window, "LayerList")),
                Num("锁定数", c => c.Depot.Screen.Controls.Count(x => x.IsLocked)),
                Num("首层图层号", c => c.Depot.Screen.Controls[0].LayerIndex),
                // ── 数据面：**事件载荷**（经事件观测探针记成控件名）──
                EventObserved("选中图层", (ctx, record) =>
                {
                    var view = (Views.LayerPanelView)ctx.Window.Content!;
                    view.SelectionChanged += (_, control) => record(control?.Name ?? "(null)");
                }),
                // ── 输入面：手势自己拨的那个列表（用它把"事件抬了"与"列表只是被拨动了"分开）──
                InputNum("列表选中下标", c => Control<ListBox>(c.Window, "LayerList").SelectedIndex),
            },

            new ClickSpec[]
            {
                // ── ① 选择族：拨到唯一那个图层 ⇒ 事件载荷被抬出来 ──
                SelMoves("LayerList", 0,
                    "OnSelectionChanged（**XAML 属性**接线）读 `LayerList.SelectedItem as HmiControl` ⇒ "
                    + "`SelectionChanged?.Invoke(this, selected)` ⇒ 本层的事件观测探针记成控件名："
                    + "「选中图层」空 → 状态灯。"
                    + "⚠ 同时声明「列表选中下标」：它是**手势自己写的**（不声明就是未声明分量外溢），"
                    + "但它**证明不了接线** —— 摘掉 XAML 那条属性，「选中图层」会停在空中而它照样变成 0；"
                    + "见证是「选中图层」那一条",
                    ("选中图层", "状态灯"), ("列表选中下标", "0")),

                // ── ②③ 层排序：直接改模型里的 `LayerIndex` ──
                Moves("BtnMoveUp",
                    "OnMoveUp（XAML 属性接线）读 `LayerList.SelectedItem is HmiControl ctrl` ⇒ "
                    + "`ctrl.LayerIndex++` ⇒「首层图层号」0 → 1（`Refresh()` 会保住选中）。"
                    + "⚠ 它**依赖前一条留下的选中**（没有选中就提前 return）⇒ 表的顺序在这里是前提、"
                    + "不是装饰（与 `UserManagerView` 的 `PermManageUsers` 紧邻 `BtnSaveRole` 同理）",
                    ("首层图层号", "1")),

                Moves("BtnMoveDown",
                    "OnMoveDown 同形、方向相反 ⇒「首层图层号」1 → 0（顺带把 ② 的效果还原，"
                    + "让后面的声明读到的仍是初值）。",
                    ("首层图层号", "0")),

                // ── ④⑤ 全锁 / 全解锁：整表写、读数是一枚计数 ──
                Moves("BtnLockAll",
                    "OnLockAll 遍历 `_layers` 把每一个 `IsLocked = true` ⇒「锁定数」0 → 1。"
                    + "⚠ 它是**整表写**（不是对选中项）⇒ 与 ②③ 不共前提，即使 ① 的选中丢了它也照样跑",
                    ("锁定数", "1")),

                Moves("BtnUnlockAll",
                    "OnUnlockAll 同形、反向 ⇒「锁定数」1 → 0。两条合起来才钉住「写的是所有图层、"
                    + "不是恰好那一个」（夹具只有 1 个图层 ⇒ 单看一条分不出）。",
                    ("锁定数", "0")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 13 个宿主：ProjectExplorerView（工程资源浏览器）—— 阶段 129
        //
        //  **第一个用"树里的一条路径"驱动的宿主**（`SelMovesPath`，本阶段新加的手势）。
        //  为什么非要有这条手势：本视图的根节点是 `CONFIG_ROOT` / `SCREENS_ROOT` 这类**分组**
        //  （处理器对它们只走 else 支、抬 null），真数据在**孩子**里 ⇒ 只用根下标的话，
        //  这个处理器最值得判的那一半**原理上够不到**（阶段 127 的 A/B/C 分档里它与
        //  C 档的 `VariablePicker` 都卡在这一条上）。
        //
        //  为什么挑它（同一套判据 + 第 ⑤ 条）：
        //   ① **生产里真的会被构造**：`HmiDockFactory:134` ＋ `MainWindow.axaml.cs:229` 两处
        //      `new ProjectExplorerView()`；② **数据面有三条腿、其中一条不是事件** ——
        //      `OnSelectionChanged` **直接写** `BtnDelete.IsVisible`（可见/隐藏），
        //      另外两条是事件载荷（`SelectionChanged` / `ConfigNodeActivated` / `DeleteRequested`）；
        //   ③ **零驱动**：`ProjectTree` / `OnSelectionChanged` / `OnDeleteClick` 在全部测试文件里
        //      `grep` 零命中；④ 面前是 1 个 TreeView ＋ **1 个具名按钮**（`BtnDelete`）。
        //
        //  🩸 **配置支（`Tag` 是 `CONFIG:<key>`）刻意没有声明 —— 不是它不值得判，是本层现在判不了它**：
        //  它会抬 `ConfigNodeActivated`，而这个事件在**静态总线** `DockLayoutState` 上
        //  （`Wiring.DockAggregate.cs:300` 订阅 → `MainWindow.OpenConfigWindow` → `ShowModalDialog`）。
        //  总线**没有退订**（产品侧不需要：主窗口活到进程结束；但**测试进程里**不是这样）⇒
        //  只跑本宿主时一切正常，**跑全量时**前序测试留下的、**已关闭**的 MainWindow 仍在总线名单上
        //  ⇒ 实测抛 `InvalidOperationException: Cannot show a window with a closed owner.`
        //  ⇒ 一条"红不红取决于别的测试跑没跑"的声明**不能发布**。配置支因此留空（登记为 **#72**）。
        //  ⚠ 本宿主**另外两条腿也过总线**（`RaiseExplorerSelectionChanged` / `RaiseExplorerDeleteRequested`），
        //  全量跑实测**没有**出问题（那些处理器不弹窗）—— 但同一个静态总线隐患对它们同样存在。
        //
        //  ⚠ **判的是哪一跳**（`EventObserved` 那条老规矩）：判「`OnSelectionChanged` 按 `Tag` 分派得对不对」
        //  ＋「`OnDeleteClick` 读到的选中项对不对」；**判不到**构造函数里往 `DockLayoutState` 的三处转发
        //  （`RaiseExplorerSelectionChanged` / `RaiseExplorerDeleteRequested` / `RaiseConfigNodeActivated`）
        //  —— 与 `LayerPanelView` 的 PC1 同一族，属宿主装配。
        //
        //  夹具：`LoadProject(d.Project, d.Screen)` ⇒ 树有 6 个根；本表只驱动前两条路径：
        //    `[0, 0]` = `CONFIG_ROOT` → `CONFIG:project_settings`（字符串 Tag ⇒ 配置支）
        //    `[1, 0]` = `SCREENS_ROOT` → 「主画面」（`Tag` 是 `HmiScreen` ⇒ else 支、抬 null）
        //    `[1, 0, 0]` = …→「状态灯」（`Tag` 是 `HmiControl` ⇒ 控件支）
        //  ⚠ **只有 `_currentScreen` 那块屏才展开控件**（`LoadProject` 的第二个实参决定）——
        //  换夹具时路径会变，越界会**抛**（本层刻意不静默选中 null，见 `Gesture`）。
        // ══════════════════════════════════════════════════════════════════════════
        new("ProjectExplorerView",

            d =>
            {
                var view = new Views.ProjectExplorerView();
                view.LoadProject(d.Project, d.Screen);
                return new Window { Content = view, Width = 320, Height = 600 };
            },

            new Probe[]
            {
                // ── 数据面·**不是事件**的那一条：处理器直接写按钮可见性 ──
                Str("删除按钮", c => Control<Button>(c.Window, "BtnDelete").IsVisible ? "显示" : "隐藏"),
                // ── 数据面·三条事件载荷 ──
                EventObserved("选中控件", (ctx, record) =>
                {
                    var view = (Views.ProjectExplorerView)ctx.Window.Content!;
                    view.SelectionChanged += (_, control) => record(control?.Name ?? "(null)");
                }),
                EventObserved("删除请求", (ctx, record) =>
                {
                    var view = (Views.ProjectExplorerView)ctx.Window.Content!;
                    view.DeleteRequested += (_, control) => record(control.Name);
                }),
                EventObserved("配置项", (ctx, record) =>
                {
                    var view = (Views.ProjectExplorerView)ctx.Window.Content!;
                    view.ConfigNodeActivated += (_, key) => record(key);
                }),
                // ── 输入面：手势自己拨到的**那条路径**（与载荷那几条互为对照）──
                InputStr("树选中路径", c => TreeSelectedPath(c.Window, "ProjectTree")),
            },

            new ClickSpec[]
            {
                // ── ① 配置支：`Tag` 是 `CONFIG:<key>` ⇒ 抬 ConfigNodeActivated 并**提前返回** ──
                //  🩸 阶段 129 时这条腿**刻意没声明**：它会经构造函数转发到静态总线
                //  （`Wiring.DockAggregate.cs:300 → MainWindow.OpenConfigWindow → ShowModalDialog`），
                //  全量跑时会打到别的测试留下的**已关闭**主窗口（`#72`）。
                //  阶段 134 加了**静态总线卫生**（`StaticBusHygiene.Silence()`，在每条宿主跑前清掉陈旧订阅）
                //  ⇒ 这条腿**现在可判**：判的是"配置支提前返回、只抬 `ConfigNodeActivated`（不抬
                //  `SelectionChanged`）"这件事本身。
                SelMovesPath("ProjectTree", new[] { 0, 0 },
                    "OnSelectionChanged（**XAML 属性**接线）按 `Tag` 分派：`CONFIG:project_settings` ⇒ "
                    + "`ConfigNodeActivated?.Invoke(this, key)` ＋ `BtnDelete.IsVisible = false` ＋ **提前 return**。"
                    + "⚠ 初值 `删除按钮` 就是**隐藏**（XAML 里写死 `IsVisible=\"False\"`）⇒ 本条的 `删除按钮=隐藏`"
                    + "是一条**反证**：配置支没有把它误显出来。"
                    + "🩸 而 `选中控件`（另一条载荷）**不动** —— 配置支提前返回、根本不抬 `SelectionChanged`，"
                    + "这正是「按 Tag 分派」的可见后果（不声明它，是「未声明分量没有外溢」的反向见证）",
                    ("配置项", "project_settings"), ("删除按钮", "隐藏"), ("树选中路径", "0,0")),

                // ── ② 屏幕节点支：`Tag` 是 `HmiScreen`（不是 `HmiControl`）⇒ 走 else 支、抬 null ──
                SelMovesPath("ProjectTree", new[] { 1, 0 },
                    "`Tag` 是 `HmiScreen` ⇒ 落到最后的 else 支：`SelectionChanged?.Invoke(this, null)` "
                    + "＋ `BtnDelete.IsVisible = false` ⇒「选中控件」空 → **(null)**（载荷真的抬了、只是值为 null）。"
                    + "⚠ 与 ① 合起来才钉住「分派看的是 `Tag` 的**类型**，不是「选了第几个」」",
                    ("选中控件", "(null)"), ("树选中路径", "1,0")),

                // ── ③ 控件支：`Tag` 是 `HmiControl` ⇒ 抬控件 ＋ 显示删除按钮 ──
                SelMovesPath("ProjectTree", new[] { 1, 0, 0 },
                    "`Tag` 是 `HmiControl` ⇒ `SelectionChanged?.Invoke(this, control)` ＋ "
                    + "`BtnDelete.IsVisible = true` ⇒ 两条**一起**翻：载荷 (null) → 状态灯、"
                    + "删除按钮 隐藏 → 显示。🩸 两条一起声明不是凑数 —— 只声明载荷那条的话，"
                    + "「按钮可见性没跟着改」照样绿",
                    ("选中控件", "状态灯"), ("删除按钮", "显示"), ("树选中路径", "1,0,0")),

                // ── ④ 删除：读**当前选中项** ⇒ 抬 DeleteRequested ──
                Moves("BtnDelete",
                    "OnDeleteClick（XAML 属性接线）读 `ProjectTree.SelectedItem as TreeViewItem` 的 `Tag` "
                    + "（必须是 `HmiControl`）⇒ `DeleteRequested?.Invoke(this, control)` ⇒ 载荷空 → 状态灯。"
                    + "⚠ 它**依赖 ③ 留下的选中**（没有选中控件就什么都不抬）⇒ 表的顺序是前提，不是装饰"
                    + "（与 `LayerPanelView` 的 `BtnMoveUp` 依赖 ① 同形）。"
                    + "⚠ 它**不真删**：只抬事件，真正删的是宿主（本层判的就是「抬没抬、抬的是哪一个」）",
                    ("删除请求", "状态灯")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 14 个宿主：Editors/VariablePicker（变量选择器）—— 阶段 131
        //
        //  **第一个子目录宿主**（C 档唯一的那个）。它此前卡在两件事上，两件本阶段都办了：
        //   ① 静态守卫按「`Views/<名字>.axaml`」拼路径 ⇒ 子目录视图一升格就"文件不存在"
        //      （见 `RelativeViewPath` / `SimpleTypeName`）；
        //   ② 它的真数据在**两层树**里：`VarTree` 的根是**分组**节点（"未分组"），叶子才是变量
        //      ⇒ 没有阶段 129 的 `SelMovesPath`，这个处理器的分派根本够不到。
        //
        //  为什么挑它（同一套判据 + 第 ⑤ 条）：
        //   ① **生产里真的会被构造**：`PropertyPanelView.axaml:640` 里 `<editors:VariablePicker
        //      x:Name="VariablePicker"/>`，且 `PropertyPanelView.axaml.cs:111` 真的订阅了
        //      `VariableSelected += OnVariableSelected`（不是死面板 —— 与 `#71` 那条正相反）；
        //   ② **数据面三条腿、全部处理器写**：`DisplayField.Text`（`UpdateDisplay()` 解析成变量名）、
        //      `PickerPopup.IsOpen`、以及 `VariableSelected` 的事件载荷；
        //   ③ **零驱动**：`VarTree` / `OnTreeSelectionChanged` / `BrowseBtn` / `ClearBtn` 在全部
        //      测试文件里 `grep` 零命中；④ 面前是 1 个 TreeView ＋ **2 个具名按钮**。
        //
        //  ⚠ **判的是哪一跳**（`EventObserved` 的老规矩）：判「按 `Tag` 分派 / 显示名解析 / 弹层开关」；
        //  **判不到**宿主 `PropertyPanelView` 那条订阅（`OnVariableSelected` 写回属性）—— 那一跳属宿主装配。
        //
        //  夹具形状（**为什么自己造变量 ＋ 自己建一个组**）：本视图的树按
        //  `GroupBy(v => v.GroupId).OrderBy(g => g.Key)` 分组、组内再 `OrderBy(v => v.Name)`，
        //  而 `Comparer<string>.Default` 是**当前文化**比较器 ⇒ 名序**依赖文化**。
        //  🩸 第一版我按「ASCII 排在最前」的直觉把路径写成 `[0,0]`，实测**错**：这台机器上
        //  （ICU zh-CN）实测顺序是 温度 < 液位 < NcAlpha < NcBravo —— Han 在 Latin **之前**。
        //  ⇒ 改用**组键**锚定：`OrderBy` 把 `null` 键排在**任何**非 null 之前（`Comparer<string>.Default`
        //  对 null 的定义，与语言无关）⇒「未分组」那组恒在 `[0]`，我自建的组恒在 `[1]`
        //  ⇒ 路径 `[1,0]` / `[1,1]` 是**确定**的（真换了文化，断言会**红**而不是静默错位）。
        // ══════════════════════════════════════════════════════════════════════════
        new("Editors.VariablePicker",

            d =>
            {
                SeedPickerVariables(d);
                var view = new Views.Editors.VariablePicker();
                view.LoadVariables(d.Variables);
                return new Window { Content = view, Width = 420, Height = 200 };
            },

            new Probe[]
            {
                // ── 数据面：显示框（`UpdateDisplay()` 解析出的**变量名**；null → 空）──
                Str("显示框", c => Control<TextBox>(c.Window, "DisplayField").Text ?? "空"),
                // ── 数据面：弹层开合（`BrowseBtn` 开、选中/清除关）──
                Str("弹出层", c => Control<Popup>(c.Window, "PickerPopup").IsOpen ? "开" : "关"),
                // ── 数据面：事件载荷（记**变量名**：Id 是夹具当场生成的 Guid，写不进静态表）──
                EventObserved("选中变量", (ctx, record) =>
                {
                    var view = (Views.Editors.VariablePicker)ctx.Window.Content!;
                    view.VariableSelected += (_, id) => record(
                        string.IsNullOrEmpty(id)
                            ? "(null)"
                            : ctx.Depot.Variables.Variables.FirstOrDefault(v => v.Id == id)?.Name ?? "(未知)");
                }),
                // ── 数据面：**己方那两条**叶子在不在树里（0 → 2）。
                //    ⚠ 刻意**不**数"整棵树的叶子数"：本视图的树还含**系统变量**（产品表，实测 17 条）
                //    ⇒ 数总数等于把这个宿主钉在系统变量表的条数上（产品一加系统变量就红，而那与本宿主行为无关）。
                Num("己方叶子数", c => TreeLeafCount(c.Window, "VarTree",
                    tag => c.Depot.Variables.Variables.Any(v => v.Id == tag && v.Name.StartsWith("Nc", StringComparison.Ordinal)))),
                // ── 输入面：手势自己拨到的那条路径 ──
                InputStr("树选中路径", c => TreeSelectedPath(c.Window, "VarTree")),
            },

            new ClickSpec[]
            {
                // ── ① 浏览按钮：建树 + 开弹层（两条腿一起，都是处理器写的）──
                Moves("BrowseBtn",
                    "OnBrowseClicked（ctor 里 `BrowseBtn.Click += OnBrowseClicked`）：`RebuildTree(\"\")` "
                    + "**建出整棵树** ⇒「己方叶子数」0 → 2（我自建的那组两条：NcAlpha / NcBravo）"
                    + "＋ `PickerPopup.IsOpen = true` ⇒「弹出层」关 → 开。"
                    + "🩸 两条一起声明不是凑数：只声明弹层的话，「树没建出来」照样绿；"
                    + "`LoadVariables()` 本身**只存管理器、不建树**（本视图的既定行为）",
                    ("己方叶子数", "2"), ("弹出层", "开")),

                // ── ②③ 选择族：**两层树**——先分组节点、再叶子 ──
                SelMovesPath("VarTree", new[] { 1, 0 },
                    "OnTreeSelectionChanged（**ctor 里** `VarTree.SelectionChanged += …` 接的 —— "
                    + "🩸 我第一稿写成「XAML 属性接线」，实测那条属性**不存在**：本视图的树订阅在构造函数里；"
                    + "这与 `VariableBrowserView`（那条真在 XAML 里）恰好相反，是同一族「凭印象写接线形态」的错）"
                    + "读 `VarTree.SelectedItem as TreeViewItem` 的 "
                    + "`Tag`（仅**叶子**携带绑定标识）⇒ `_selectedVariableName = id` ⇒ `UpdateDisplay()` "
                    + "把 Id 解析成**变量名**写进只读显示框 ＋ `PickerPopup.IsOpen = false` ＋ 抬事件。"
                    + "本条同时钉三条腿：显示框 空 → NcAlpha、弹出层 开 → 关、载荷 → NcAlpha。"
                    + "⚠ 路径是 `[1,0]` 而**不是** `[1]`：分组节点不挂 `Tag` "
                    + "⇒ 只选分组的话处理器**什么都不做**（这正是阶段 129 加 `SelMovesPath` 的理由）。"
                    + "⚠ 路径第一段是 `1`（我自建的那一组）而不是 `0` —— 见宿主注释里「组键锚定」那段推理"
                    + "（第一版按直觉写 `[0,0]`，被 Han/Latin 的实际排序打了回来）",
                    ("显示框", "NcAlpha"), ("弹出层", "关"), ("选中变量", "NcAlpha"),
                    ("树选中路径", "1,0")),

                SelMovesPath("VarTree", new[] { 1, 1 },
                    "换第二个叶子 ⇒ 显示框与载荷跟着换成 NcBravo。🩸 **只测一条不够**：只测 `[0,0]` 的话，"
                    + "「显示框恒定写第一条」也能满足它 —— 两条合起来才钉住「写的是被选中的那一个」。"
                    + "⚠ 弹层已经是关的（上一条关的）⇒ 本条的**反向见证**是它不再变化",
                    ("显示框", "NcBravo"), ("选中变量", "NcBravo"), ("树选中路径", "1,1")),

                // ── ④ 清除按钮：把绑定清空（显示框与载荷一起回到"无"）──
                Moves("ClearBtn",
                    "OnClearClicked（ctor 里 `ClearBtn.Click += OnClearClicked`）：`_selectedVariableName = null` "
                    + "⇒ `UpdateDisplay()` ⇒ `DisplayField.Text = null`（读数归一成「空」）＋ "
                    + "`PickerPopup.IsOpen = false`（已是关）＋ 抬 `VariableSelected(null)`。"
                    + "⇒ 两条**一起**回到「无」：显示框 NcBravo → 空、载荷 NcBravo → (null)。"
                    + "⚠ 它是本宿主唯一不看树选中的驱动（清空不需要选中）",
                    ("显示框", "空"), ("选中变量", "(null)")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 15 个宿主：AssetManagerView（资源管理器面板）—— 阶段 132
        //
        //  为什么挑它（同一套判据 + 第 ⑤ 条）：
        //   ① **生产里真的会被构造**：`HmiDockFactory:318` ＋ `MainWindow.axaml.cs:232` 两处
        //      `new AssetManagerView()`；② **零驱动**：`AssetList` / `OnSelectionChanged` /
        //      五个按钮在全部测试文件里 `grep` 零命中；
        //   ③ 面前是 1 个 ListBox ＋ **5 个具名按钮**；④ 数据面正好用得上阶段 128 建的事件观测层
        //      （`AssetSelected` 是**事件型**数据面：处理器只抬 `row?.Asset`，不写任何控件）。
        //
        //  ⚠ **判的是哪一跳**：判「`OnSelectionChanged` 从**混排列表**（分组标题 ＋ 资源行）里
        //  挑对了哪一项、载荷传对了没有」；**判不到**构造函数里四处往 `DockLayoutState` 的转发。
        //  🩸 其中 `AiIconRequested` 那条转发**踩到 `#72`**（见最后一条声明的理由）：
        //  它落到 `Wiring.DockAggregate.cs:70 → OpenAiIconDialogAsync()`，全量跑时会打到别的测试
        //  留下的**已关闭**主窗口 ⇒ 那条腿**不能点**（`#72` 的第二个实例）。
        //
        //  夹具：`SeedAssetFixture(depot)` 往 `depot.Assets` 里铺 **2 张图 ＋ 1 个字体**
        //  （`depot.Assets` 原本是**空的** —— 不铺这一层，列表里连一行都没有，三条选择声明全成
        //  「null → null」的零信息断言）。列表是**混排**：`AssetType` 枚举序 = Image, Font, Language, Svg
        //  ⇒ `_items` = [Image 标题, NcAlpha.png, NcBravo.png, Font 标题, NcZeta.ttf]
        //  ⇒ 下标 0 = 标题、1 / 2 = 两张图、3 = 字体标题、4 = 字体。
        // ══════════════════════════════════════════════════════════════════════════
        new("AssetManagerView",

            d =>
            {
                SeedAssetFixture(d);
                var view = new Views.AssetManagerView();
                view.LoadAssets(d.Assets);
                return new Window { Content = view, Width = 420, Height = 600 };
            },

            new Probe[]
            {
                // ── 数据面：事件载荷（记**资源名**；`AssetItem` 对象本身写不进静态表）──
                EventObserved("选中资源", (ctx, record) =>
                {
                    var view = (Views.AssetManagerView)ctx.Window.Content!;
                    view.AssetSelected += (_, asset) => record(asset?.Name ?? "(null)");
                }),
                // ── 数据面：列表项数（分组标题 ＋ 资源行**混排**，`Refresh()` 亲手重建）──
                Num("列表项数", c => Items(c.Window, "AssetList")),
                // ── 输入面：手势自己拨的那个下标（混排列表里「第几项」）──
                InputNum("列表选中下标", c => Control<ListBox>(c.Window, "AssetList").SelectedIndex),
                // ── 数据面：AI 图标请求载荷（阶段 134 清总线之后可判）──
                EventObserved("AI图标请求", (ctx, record) =>
                {
                    var view = (Views.AssetManagerView)ctx.Window.Content!;
                    view.AiIconRequested += (_, _) => record("已抬");
                }),
                // 只进读数：预算行走字典（本层不冻结文案），只读「空 / 非空」
                Str("预算行", c => string.IsNullOrEmpty(Control<TextBlock>(c.Window, "BudgetText").Text) ? "空" : "非空"),
            },

            new ClickSpec[]
            {
                // ── ①②③ 选择族：**混排列表**里三个位置各拨一次 ──
                SelMoves("AssetList", 0,
                    "OnSelectionChanged（**XAML 属性**接线）读 `AssetList.SelectedItem as AssetRow` "
                    + "—— 下标 0 是**分组标题**（`AssetGroupHeader`，不是资源行）⇒ `as` 得 null ⇒ "
                    + "`AssetSelected?.Invoke(this, null)` ⇒ 载荷空 → **(null)**。"
                    + "🩸 本条是「混排列表」的**边界见证**：把标题也当成资源，是这一族最容易写错的形状",
                    ("选中资源", "(null)"), ("列表选中下标", "0")),

                SelMoves("AssetList", 1,
                    "下标 1 是第一张图（`NcAlpha.png`）⇒ 载荷 (null) → NcAlpha.png。"
                    + "⚠ 与上一条合起来才说明「下标 → 身份」的映射来自**列表自己**（不是恒定抬第一个）",
                    ("选中资源", "NcAlpha.png"), ("列表选中下标", "1")),

                SelMoves("AssetList", 2,
                    "下标 2 是第二张图（`NcBravo.png`）⇒ 载荷跟着换成 NcBravo.png —— "
                    + "三条选择声明互为「身份映射」的正反见证",
                    ("选中资源", "NcBravo.png"), ("列表选中下标", "2")),

                // ── ④ 刷新：本层读不到差别（如实记 Inert）──
                Inert("BtnRefresh",
                    "OnRefreshClick（XAML 属性接线）＝ `Refresh()` ＋ 写**状态行**文案。"
                    + "`Refresh()` 按 `AssetType` 分组重建同一份列表、并按 **Id 恢复选中** ⇒ "
                    + "「列表项数」「选中资源」「列表选中下标」三条全都不动；状态行**走字典**"
                    + "（本层只读「空/非空」，而它一直是「非空」）⇒ 数据面一字不变。"
                    + "⚠ 这是「未被观测」成色、**不是**「没接线」：摘掉 `Click=` 本层也不会红"),

                // ── ⑤–⑦ 三个导入按钮：都弹**文件选择器**（平台桩）⇒ 不点 ──
                Never("BtnImportImage",
                    "`async void` ＋ `TopLevel.StorageProvider.OpenFilePickerAsync`（平台桩）："
                    + "headless 下不归 `Pump` 管（与 `CommunicationConfigView.BtnTestConnection` 同一条理由）"),
                Never("BtnImportSvg",
                    "同上：`OnImportSvg` 也是 `async void` ＋ 文件选择器"),
                Never("BtnImportFont",
                    "同上：`OnImportFont` 也是 `async void` ＋ 文件选择器"),

                Moves("BtnAiIcon",
                    "OnAiIconClicked 只抬 `AiIconRequested` ⇒ 载荷空 → 「已抬」。"
                    + "🩸 阶段 134 之前这条腿**不能声明**：构造函数把它转发到静态总线"
                    + "（`Wiring.DockAggregate.cs:70 → OpenAiIconDialogAsync()`），全量跑时会打到别的测试"
                    + "留下的**已关闭**主窗口（`#72` 的第二个实例）。现在宿主跑前会清掉陈旧订阅 ⇒ 可判。"
                    + "⚠ 判的是**视图这一跳**（事件抬了没），不是「那个对话框有没有弹出来」",
                    ("AI图标请求", "已抬")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 16 个宿主：FaceplateLibraryView（Faceplate 模板库）—— 阶段 133
        //
        //  **B 档最后一个**。先说结论：**它的按钮面几乎判不了** —— 四个具名按钮的处理器
        //  要么走平台桩（确认框）、要么把事件转发到**静态总线**（→ 别的 MainWindow 上开对话框，
        //  即 `#72` 那一族）⇒ 四条只能如实记 `Never`。**能判的是它的选择族**：
        //  `OnTemplateSelectionChanged` 把"选中的那一个模板"抬成事件载荷。
        //  ⚠ 挑它的理由因此**只有一条**（不是"它有 4 个按钮"）：它是 B 档最后一个，
        //    且它的选择处理器带一条**独特的前置守卫** `if (Parent == null) return;`
        //    —— 那是本层第一次遇到"处理器依赖**已装配**"的形状（本宿主的装配方式正好把它变成可判的）。
        //
        //  为什么挑它（同一套判据 + 第 ⑤ 条）：
        //   ① **生产里真的会被构造**：`HmiDockFactory:174` ＋ `MainWindow.axaml.cs:227` 两处
        //      `new FaceplateLibraryView()`；② **零驱动**：`TemplateListBox` / 四个按钮在全部测试里
        //      `grep` 零命中；③ 面前是 1 个 ListBox ＋ **4 个具名按钮**。
        //
        //  ⚠ **判的是哪一跳**：判「`OnTemplateSelectionChanged` 从**平铺列表**（模板对象本身当条目）
        //  里挑对了哪一个、载荷传对了没有」＋「`Parent == null` 时它会不会哑火」这一前置条件**已在
        //  本宿主被满足**（装在 Window 里 ⇒ `Parent != null`）。**判不到**四条总线转发那一跳。
        //
        //  夹具：`LoadTemplates(new[]{ NcFaceA, NcFaceB })` —— `ApplySearchFilter()` 只做过滤、
        //  **不排序** ⇒ 下标顺序＝给定顺序（与语言/文化**无关**）。
        //  🩸 **它与"共享状态"的关系（阶段 139 起已治本）**：本视图 `OnAttachedToVisualTree` 会从
        //  **静态** `DockLayoutState.Templates` 自初始化（产品行为：ViewLocator 每次 Build 新实例、
        //  MainWindow 构造期写的旧实例不会显示）⇒ 阶段 133 实测：夹具那两条在**全量跑**时被
        //  **别的测试留下的**模板覆盖（全量 4 条、内置模板排在前面；单跑 2 条）⇒ 当时只能把声明退成
        //  **「载荷随选中」**（一致性探针，与 `ModelRoleFollowsCombo` 同形）。
        //  🆕 **阶段 139 治本**：`StaticBusHygiene.ResetSharedData()` 在每条宿主跑前把
        //  `DockLayoutState` 的 **8 个**静态属性（含 `Templates`）清回基线 ⇒ 本视图**读不到
        //  别的测试留下的数据** ⇒ 三条断言**恢复字面名**（`选中模板` / `模板条数` / `编辑请求`）。
        //  ⚠ 一致性探针**保留**：它判「处理器抬的是不是当前选中项」，与字面断言（「夹具真的生效了」）是两件事。
        //  🩸 **中途的弯路记在这里（免得下一个人重走）**：本阶段一度量到"恢复字面断言后全量跑崩/挂"
        //  并据此退回 s133 形状、还把它归因到产品侧两处静态读。收尾复测**推翻**了那个结论 ——
        //  全 8 Reset ＋ 字面断言，**全量连跑 3 次全绿**（624 / 31–33 s）；当时那 6 连败是
        //  **本 session 前面几趟被 kill 的测试跑坏了进程环境**。见登记表 §61 的更正块。
        // ══════════════════════════════════════════════════════════════════════════
        new("FaceplateLibraryView",

            d =>
            {
                var view = new Views.FaceplateLibraryView();
                view.LoadTemplates(new[]
                {
                    new ControlTemplate { Name = "NcFaceA", Category = "通用" },
                    new ControlTemplate { Name = "NcFaceB", Category = "通用" },
                });
                return new Window { Content = view, Width = 320, Height = 600 };
            },

            new Probe[]
            {
                // ── 数据面：事件载荷（记**模板名**；`ControlTemplate` 对象本身写不进静态表）──
                //    ⚠ 阶段 133 时它只能 `isVoice: true`（只进读数，值取决于共享状态）；
                //    阶段 139 起共享数据每跑前清回基线 ⇒ 它是**可断言**的（写具体夹具名）。
                EventObserved("选中模板", (ctx, record) =>
                {
                    var view = (Views.FaceplateLibraryView)ctx.Window.Content!;
                    view.TemplateSelected += (_, template) => record(template?.Name ?? "(null)");
                }),
                // ── **一致性探针**（数据面）：载荷是不是**就是当前选中项**（与 `ModelRoleFollowsCombo` 同形）──
                //    🩸 为什么不直接断言"载荷 = NcFaceA"：本视图在 `OnAttachedToVisualTree` 里会
                //    从**静态** `DockLayoutState.Templates` 自初始化 ⇒ 夹具 `LoadTemplates` 进来的两条
                //    在**全量跑**时会被共享状态覆盖（实测：全量 4 条、内置模板排在前面；单跑 2 条）。
                //    ⇒ 断言"哪一个"会把宿主钉在**别的测试留下的共享状态**上；断言"载荷随选中"不会。
                //    三档：无载荷 / 一致 / 不一致（不一致 = 处理器抬错了对象）。
                Str("载荷随选中", c =>
                {
                    var payload = c.Observed.TryGetValue("选中模板", out var p) ? p : "";
                    if (payload.Length == 0) return "无载荷";
                    var selected = (Control<ListBox>(c.Window, "TemplateListBox").SelectedItem
                                    as ControlTemplate)?.Name ?? "";
                    if (selected.Length == 0) return "有载荷无选中";
                    return string.Equals(payload, selected, StringComparison.Ordinal) ? "一致" : "不一致";
                }),
                // ── 数据面：条目数（夹具 2 条）—— 阶段 139 起**可断言**（曾因共享数据覆盖只能"只进读数"）──
                Num("模板条数", c => Items(c.Window, "TemplateListBox")),
                // ── 数据面：三条请求载荷（阶段 134 清总线之后可判）──
                EventObserved("新建请求", (ctx, record) =>
                {
                    var view = (Views.FaceplateLibraryView)ctx.Window.Content!;
                    view.CreateTemplateRequested += (_, _) => record("已抬");
                }),
                //    ⚠ 阶段 139 起**可断言**（数据已清回基线）：载荷必须是**当时选中的那个夹具名**。
                EventObserved("编辑请求", (ctx, record) =>
                {
                    var view = (Views.FaceplateLibraryView)ctx.Window.Content!;
                    view.EditTemplateRequested += (_, template) => record(template?.Name ?? "(null)");
                }),
                Str("编辑载荷随选中", c =>
                {
                    var payload = c.Observed.TryGetValue("编辑请求", out var p) ? p : "";
                    if (payload.Length == 0) return "无载荷";
                    var selected = (Control<ListBox>(c.Window, "TemplateListBox").SelectedItem
                                    as ControlTemplate)?.Name ?? "";
                    if (selected.Length == 0) return "有载荷无选中";
                    return string.Equals(payload, selected, StringComparison.Ordinal) ? "一致" : "不一致";
                }),
                EventObserved("AI模板请求", (ctx, record) =>
                {
                    var view = (Views.FaceplateLibraryView)ctx.Window.Content!;
                    view.AiTemplateRequested += (_, _) => record("已抬");
                }),
                // ── 输入面：手势自己拨的那个下标 ──
                InputNum("列表选中下标", c => Control<ListBox>(c.Window, "TemplateListBox").SelectedIndex),
            },

            new ClickSpec[]
            {
                // ── ①② 选择族：平铺列表里两项各拨一次 ──
                SelMoves("TemplateListBox", 0,
                    "OnTemplateSelectionChanged（**XAML 属性**接线）**首行是 `if (Parent == null) return;`** "
                    + "⇒ 本宿主把它装进 `Window`（`Show()` 过）才跑得起来 —— 这是本层第一次遇到"
                    + "「处理器依赖**已装配**」的形状。选中项是 `ControlTemplate` 本身（**平铺列表**、"
                    + "没有分组标题）⇒ `_selectedTemplate = template` ＋ `TemplateSelected?.Invoke` ⇒ "
                    + "载荷空 → 抬出该模板的名字 ⇒ 一致性探针从「无载荷」变成「一致」",
                    ("选中模板", "NcFaceA"), ("模板条数", "2"),
                    ("载荷随选中", "一致"), ("列表选中下标", "0")),

                SelMoves("TemplateListBox", 1,
                    "换第二项 ⇒ 载荷跟着换成第二项的名字 ⇒ 一致性探针仍是「一致」。"
                    + "⚠ 两条合起来才说明「抬的是被选中的那一个」（只测一条的话，「恒定抬第一个」也能满足它；"
                    + "负控 NC-2 打的正是这条腿）"
                    + "⚠ 顺序**不依赖文化**（`ApplySearchFilter()` 只过滤、不排序）；阶段 139 之前还"
                    + "**依赖共享状态**（见头部注释），治本之后本条可以直接写 `NcFaceB`",
                    ("选中模板", "NcFaceB"), ("载荷随选中", "一致"), ("列表选中下标", "1")),

                // ── ③–⑥ 四个按钮：两条平台桩 ＋ 两条总线（#72 族）──
                Moves("BtnNewTemplate",
                    "OnNewTemplateClicked 只抬 `CreateTemplateRequested` ⇒ 载荷空 → 「已抬」。🩸 阶段 134 之前它**不能声明**（构造函数把事件转发到静态总线 → `Wiring.DockAggregate.cs:273 → CreateTemplateFromSelection()`：无画布选中时写**别的窗口**的状态行、有选中时开编辑器对话框）；清总线之后判的就是**视图这一跳**",
                    ("新建请求", "已抬")),
                Moves("BtnEditTemplate",
                    "OnEditTemplateClicked 在 `_selectedTemplate != null` 时抬 `EditTemplateRequested` ⇒ "
                    + "载荷从「无载荷」变成**当时选中的那个模板名** ⇒「编辑载荷随选中」一致。"
                    + "⚠ 它**依赖上一条选择留下的 `_selectedTemplate`**（没选中就什么都不抬）—— "
                    + "表的顺序在这里是前提，不是装饰。⚠ 载荷写**字面名** `NcFaceB`（上一条刚选中的那条）"
                    + "—— 阶段 139 清共享数据之后才敢这么写",
                    ("编辑请求", "NcFaceB"), ("编辑载荷随选中", "一致")),
                Never("BtnDeleteTemplate",
                    "`OnDeleteTemplateClicked` 走 `UiOperationGuard.Run` ＋ `AppDialogs.ConfirmAsync`"
                    + "（**平台桩**）：确认框的完成时刻不由 `Pump` 决定 ⇒ 读数会抖"
                    + "（与 `UserManagerView.BtnDeleteUser` 同一条理由）"),
                Moves("BtnAiTemplate",
                    "OnAiTemplateClicked 只抬 `AiTemplateRequested` ⇒ 载荷空 → 「已抬」。🩸 与上两条同源（构造函数转发到静态总线 → `Wiring.DockAggregate.cs:280` 开 `AiTemplateDialog`）；清总线之后可判。⚠ 本视图是 `#43` 双接线缺陷的现场，静态守卫 `check-xaml-duplicate-wiring.py` 守着它",
                    ("AI模板请求", "已抬")),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 17 个宿主：AnimationEditorView（动画编辑器）—— 阶段 136
        //
        //  **它是面外剩下的**最大**一块选择族**（8 个带接线的选择控件，是**当时**面外 56 个里的一整片；
        //  升格之后面外降到 48 个 —— 数字见 `SelectionFaceClassificationGuardTests` 的口径链）
        //  ⇒ 按"面覆盖"这条理由挑它（**不是**按控件总数 —— 阶段 118 否的是"按个数随手挑一刀"，
        //  这里能说清价值：8 条选择接线此前一条都没判据）。
        //
        //  为什么它**能**进来（三条判据都对上）：
        //   ① **写入面窄且可界定**：四条编辑下拉都走 `SyncAnimationFromUi()`，而它只写
        //      **`_currentAnimation` 一个对象**的字段（Name/PropertyName/Trigger/PathType/…），
        //      **不是** PropertyPanelView 那种"每次事件把全部编辑器写回模型"的批量写 ⇒
        //      与本层「未声明的分量一字不变」**不冲突**（改哪个下拉，只有它那一个字段变 ⇒ 只声明它）；
        //   ② **零驱动**：`AnimationList` / 8 个下拉 / 6 个按钮在全部测试里 `grep` 零命中；
        //   ③ **可达**：`HmiDockFactory` 里 `new AnimationEditorView(...)`（且阶段 135 的
        //      `UnreachableViewGuardTests` 现在**机器盯着**这一条）。
        //
        //  ⚠ **判的是哪一跳**：判"下拉 → `SyncAnimationFromUi`/`SyncKeyframeFromUi` → **模型字段**"
        //  ＋ "列表 → `Load*ToEditPanel` → **面板控件**"这两跳；判不到它自己的事件转发（它没有）。
        //
        //  夹具（`WindowDepot`）：`Animations` 里 1 条「呼吸灯」（`ControlId` 已指向主画面唯一的那个控件；
        //  `Trigger` 默认 **"OnLoad"**、`PropertyName` 默认 **"Opacity"**、`PathType` 默认 **EaseInOut**、
        //  **无关键帧**）；`Project` 里有 1 块屏、1 个控件 ⇒ `CmbTargetControl` 只有一项。
        //  ⚠ `CmbTargetControl` 因此只能记 `SelInert`（**写入同值**：夹具的 `ControlId` **本来就是**
        //  那唯一一个控件 ⇒ 拨到 index 0 写回同一个值）—— 这是"成色③"，不是"没接线"。
        // ══════════════════════════════════════════════════════════════════════════
        new("AnimationEditorView",

            d => new Views.AnimationEditorView(d.Animations, d.Project),

            new Probe[]
            {
                // ── 数据面：模型字段（处理器**直接写**的那些格）──
                Num("动画条数", c => c.Depot.Animations.Count),
                Num("关键帧数", c => c.Depot.Animations.Sum(a => a.Keyframes.Count)),
                Str("属性名", c => c.Depot.Animations[0].PropertyName),
                Str("触发方式", c => c.Depot.Animations[0].Trigger),
                Str("动画路径类型", c => c.Depot.Animations[0].PathType.ToString()),
                Str("关键帧路径类型", c => c.Depot.Animations[0].Keyframes.FirstOrDefault()?.PathType?.ToString()
                                          ?? "(跟随全局)"),
                // ── 数据面：面板侧（`LoadAnimationToEditPanel` / `LoadKeyframeToEditPanel` 写的）──
                Str("动画名字框", c => Control<TextBox>(c.Window, "TxtAnimationName").Text ?? "空"),
                Str("关键帧面板", c => Control<Border>(c.Window, "KeyframeEditPanel").IsVisible ? "显示" : "隐藏"),
                // ── 输入面：两个手势自己拨的下标 ──
                InputNum("动画选中下标", c => Control<ListBox>(c.Window, "AnimationList").SelectedIndex),
                InputNum("关键帧选中下标", c => Control<ListBox>(c.Window, "KeyframeList").SelectedIndex),
                // ⚠ 「动画条数」读的是**模型集合**（`Depot.Animations.Count`），而筛选**不碰模型**
                //   ⇒ 判「列表真的被重建了」必须有这一枚（列表行数），否则筛选那一跳在读数上
                //   与「压根没接线」同形（阶段 112 `TrendConfigView` 的同一条教训）。
                InputNum("列表行数", c => Items(c.Window, "AnimationList")),
                // 启用位：`ChkEnabled` 是 `CheckBox : ToggleButton : Button` ⇒ 它在**按钮面**里，
                // 必须分类；而它写的是 `IsEnabled`（本层此前一枚探针都没有 → 会变成「未声明分量外溢」）。
                Num("启用位", c => c.Depot.Animations[0].IsEnabled ? 1 : 0),
            },

            new ClickSpec[]
            {
                // ── ① 选中动画 ⇒ 面板被模型回填（列表 → 面板那一跳）──
                SelMoves("AnimationList", 0,
                    "OnAnimationSelectionChanged（ctor 里 `AnimationList.SelectionChanged += …`）读 "
                    + "`SelectedItem as ListBoxItem` 的 `Tag`（`AnimationConfig`）⇒ `_currentAnimation = anim` "
                    + "⇒ `LoadAnimationToEditPanel(anim)` **把模型写进面板** ⇒「动画名字框」空 → 呼吸灯。"
                    + "⚠ 本条是后面四条下拉编辑的**前提**（`SyncAnimationFromUi` 首行判 "
                    + "`_currentAnimation == null` 就 return）⇒ 表的顺序不是装饰",
                    ("动画名字框", "呼吸灯"), ("动画选中下标", "0")),

                // ── ②–④ 三条编辑下拉：各写**一个**模型字段 ──
                SelMoves("CmbAnimationProperty", 0,
                    "`CmbAnimationProperty.SelectionChanged += (_, _) => SyncAnimationFromUi()` ⇒ "
                    + "`if (CmbAnimationProperty.SelectedItem is string prop) _currentAnimation.PropertyName = prop` ⇒ "
                    + "「属性名」Opacity → **X**（`_properties[0]`）。"
                    + "⚠ `SyncAnimationFromUi` 一次写 12 个字段，但**其余 11 个写的是原值** ⇒ 读数上只有这一格动 "
                    + "—— 这正是「它只写当前那一个对象、且未声明的分量一字不变」两条能同时成立的原因",
                    ("属性名", "X")),

                SelMoves("CmbTrigger", 2,
                    "同上一条同一条链（同一个 `SyncAnimationFromUi`）⇒ 「触发方式」OnLoad → **OnValueChange**"
                    + "（`_triggers[2]`）。⚠ 与上一条**共用一个处理器**：两条各自只动自己那一格 ⇒ "
                    + "合起来才证明「每个下拉都真的写进了它对应的字段」（而不是只接了一个）",
                    ("触发方式", "OnValueChange")),

                SelMoves("CmbPathType", 2,
                    "`_currentAnimation.PathType = (AnimationPathType)CmbPathType.SelectedIndex` ⇒ "
                    + "「动画路径类型」EaseInOut → **EaseIn**（枚举序 Linear=0 / EaseInOut=1 / EaseIn=2）。"
                    + "⚠ 读的是**枚举名**（`ToString()`）而不是下拉文案（那 7 项走字典 ⇒ 读文案等于冻一份本地化结果）",
                    ("动画路径类型", "EaseIn")),

                // ── ⑤ 目标控件：写入同值（夹具只有一个控件）──
                SelInert("CmbTargetControl", 0,
                    "**成色③ 写入同值**：`SyncAnimationFromUi` 会把 `_currentAnimation.ControlId` 写成 "
                    + "选中项 Id，而**夹具的 `ControlId` 本来就是**主画面那唯一一个控件（`WindowDepot` 就是这么造的）"
                    + "⇒ 拨到 index 0 写回同一个值，数据面一字不动。"
                    + "⚠ 本层读不到差别**不是**「没接线」：要判它得先有一个「ControlId 指向别处」的夹具"
                    + "（登记为后续项，本阶段刻意不为此改夹具）"),

                // ── ⑥ 启用位：本视图唯一一枚 CheckBox（它在**按钮面**里）──
                Moves("ChkEnabled",
                    "`ChkEnabled.IsCheckedChanged += (_, _) => SyncAnimationFromUi()` ⇒ "
                    + "`_currentAnimation.IsEnabled = ChkEnabled.IsChecked ?? true` ⇒ 「启用位」1 → **0**。"
                    + "⚠ 手势见类注释：`ToggleButton` 先翻转 `IsChecked`（true → false）再引发 `Click` —— "
                    + "只引发 `Click` 的话 `IsCheckedChanged` 一次都跑不到（假阴性）。"
                    + "⚠ 它也是**写入同值**的反面对照：这里 `?? true` 的兜底值不参与，因为翻转后是真 false",
                    ("启用位", "0")),

                // ── ⑦ 加关键帧 ⇒ 为 ⑧⑨ 造出「有关键帧可选中」的前提 ──
                Moves("BtnAddKeyframe",
                    "OnAddKeyframe（ctor 里 `BtnAddKeyframe.Click += (_, _) => OnAddKeyframe()`）："
                    + "`if (_currentAnimation == null) return;` ⇒ 给**当前动画**加一条 `AnimationKeyframe`"
                    + "（时间点按既有帧推算）＋ `SortKeyframes()` ＋ `RefreshKeyframeList()` ⇒ "
                    + "「关键帧数」0 → 1。⚠ 它依赖 ① 留下的 `_currentAnimation`",
                    ("关键帧数", "1")),

                // ── ⑧⑨ 关键帧选中 ＋ 它自己的缓动下拉 ──
                SelMoves("KeyframeList", 0,
                    "OnKeyframeSelectionChanged 读 `SelectedItem as ListBoxItem` 的 `Tag`（`AnimationKeyframe`）⇒ "
                    + "`LoadKeyframeToEditPanel(kf)` ⇒ `KeyframeEditPanel.IsVisible = true` ⇒ "
                    + "「关键帧面板」隐藏 → 显示（＋ 选中下标 -1 → 0）。"
                    + "⚠ 新加的关键帧 `PathType` 是 **null**（＝「跟随全局」）⇒ `LoadKeyframeToEditPanel` "
                    + "把 `CmbKeyframePathType.SelectedIndex` 置 0",
                    ("关键帧面板", "显示"), ("关键帧选中下标", "0")),

                SelMoves("CmbKeyframePathType", 3,
                    "`CmbKeyframePathType.SelectionChanged += (_, _) => SyncKeyframeFromUi()`（前提 "
                    + "`_selectedKeyframe != null`）⇒ `_selectedKeyframe.PathType = "
                    + "(AnimationPathType)(SelectedIndex - 1)` ⇒ index 3 → **EaseIn** ⇒ "
                    + "「关键帧路径类型」(跟随全局) → EaseIn。⚠ index 0 是「使用全局设置」（写 null）。"
                    + "🩸 **本行是本层第三次「判据逼出产品修复」的现场（本阶段首跑红）**："
                    + "`SyncKeyframeFromUi` 尾部那个「选中排序后的关键帧」的循环原先比对 `_selectedKeyframe`，"
                    + "而它上面的 `RefreshKeyframeList()`（`Items.Clear()`）已经引发 `SelectionChanged` "
                    + "把同一个字段置成 **null** ⇒ 循环**永远不命中**（死代码）。症状就是本条首跑时读到的"
                    + "「关键帧面板 显示 → 隐藏」＋「关键帧选中下标 0 → -1」——**改一下缓动下拉就把面板收起来**。"
                    + "产品侧本阶段同批修（抓局部引用再比对，3 行）⇒ **本行把「面板仍显示、选中仍是 0」"
                    + "写成期望值**：撤掉修复它就红（负控 nc136-3 实测）。见登记 **#74**",
                    ("关键帧路径类型", "EaseIn"), ("关键帧面板", "显示"), ("关键帧选中下标", "0")),

                // ── ⑩ 删关键帧：把 ⑧ 的面板收回去 ──
                Moves("BtnDeleteKeyframe",
                    "OnDeleteKeyframe：`_currentAnimation.Keyframes.Remove(_selectedKeyframe)` ⇒ "
                    + "「关键帧数」1 → 0 ＋ `KeyframeEditPanel.IsVisible = false` ⇒「关键帧面板」显示 → 隐藏 ＋ "
                    + "`RefreshKeyframeList()` 重建列表 ⇒ 选中被清 ⇒「关键帧选中下标」0 → -1。"
                    + "⚠ 首行判 `_selectedKeyframe == null` ⇒ 依赖 ⑧ 留下的选中",
                    ("关键帧数", "0"), ("关键帧面板", "隐藏"), ("关键帧选中下标", "-1"),
                    // ⚠ 「关键帧路径类型」这一格读的就是**第一条关键帧**的 `PathType` ⇒ 唯一的
                    //   关键帧被删掉之后它必然落回「(跟随全局)」。**必须声明**：不声明就是
                    //   「未声明分量外溢」（探针读的是模型，不是界面）。
                    ("关键帧路径类型", "(跟随全局)")),

                // ── ⑪ 新建动画：集合真的加一条 ──
                Moves("BtnNewAnimation",
                    "OnNewAnimation（ctor 里接的）：`_animationCounter++` ⇒ 造一条 `AnimationConfig`"
                    + "（Name 走字典的默认名、PropertyName=Opacity、PathType=EaseInOut）＋ 加进集合 ＋ "
                    + "刷新列表并**选中它** ⇒ 「动画条数」1 → **2** ＋「动画名字框」呼吸灯 → 动画 1 ＋ "
                    + "「动画选中下标」0 → 1。"
                    + "⚠ 它把 `_currentAnimation` 换成新的那条 ⇒ 所以本表把三条「改夹具那条动画」的声明"
                    + "（②③④⑥）排在它**之前**。⚠「列表行数」1 → 2 是本条**唯一的界面侧**见证"
                    + "（「动画条数」读的是模型集合 —— 两者一起才说明列表真的跟着模型重建了）",
                    ("动画条数", "2"), ("动画名字框", "动画 1"), ("动画选中下标", "1"),
                    ("列表行数", "2")),

                // ── ⑫⑬ 筛选下拉：只重建**列表**，模型一字不动 —— ⚠ 排最后，理由见本条 ──
                SelMoves("CmbTriggerFilter", 2,
                    "`CmbTriggerFilter.SelectionChanged += (_, _) => RefreshAnimationList()` ⇒ 按 `Trigger` 过滤："
                    + "夹具那条是 **OnLoad**、⑪ 新建的那条也是 **OnLoad**，而 index 2 是 **OnClick** ⇒ "
                    + "「列表行数」2 → 0（列表空）。"
                    + "⚠ **模型一字不动**（筛选只重建列表）：本行读数里「属性名/触发方式/动画路径类型」"
                    + "仍是 X / OnValueChange / EaseIn ⇒ 这是它的反向见证（与 `AlarmListView` 的两个筛选同形）。"
                    + "⚠ 本条**必须排最后**：列表清空会连**选中**一起清掉 ⇒ `OnAnimationSelectionChanged` "
                    + "走 else 分支把 `_currentAnimation` 置 **null** ⇒ 排在前面的任何一条编辑腿都够不到它了"
                    + "（首跑实测：本行读出的「动画选中下标」-1 就是这条副作用，故**声明**它、不藏它）",
                    ("列表行数", "0"), ("动画选中下标", "-1")),

                SelMoves("CmbTriggerFilter", 0,
                    "拨回「全部」（index 0）⇒ 「列表行数」0 → 2（列表重建）。"
                    + "⚠ 同一控件第二次驱动（唯一性键是 (名字, 手势下标)）；"
                    + "两条合起来才说明筛选**真的是双向**的（只测一条的话「恒定清空」也能满足它）。"
                    + "⚠ `RefreshAnimationList` 只在 `_currentAnimation != null` 时才把选中选回来 —— "
                    + "此时它已是 null ⇒ 「动画选中下标」停在 -1**不是**漏声明",
                    ("列表行数", "2")),

                // ── ⑭–⑯ 三个不点（三种不同理由）──
                Never("BtnDeleteAnimation",
                    "`OnDeleteAnimation` 会 new 一个 **`ConfirmDeleteDialog`** 模态对话框（`ShowDialog`）⇒ "
                    + "完成时刻不由 `Pump` 决定、且会挡住宿主（与 `UserManagerView.BtnDeleteUser` 同形）"),
                Never("BtnPlayPreview",
                    "`TogglePlayback()` 起一个 **`DispatcherTimer`** 播放时间轴：完成时刻不由 `Pump` 决定 ⇒ "
                    + "读数会抖（与 `CommunicationConfigView.BtnTestConnection` 同一条理由）"),
                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 18 个宿主：TagMonitorView（标签监视器）—— 阶段 142
        //
        //  为什么挑它：面外 7 个视图里**代价最小**的一个（**4 个具名按钮 ＋ 1 条接线下拉**），
        //  而且四条判据都对上：
        //   ① **可读的数据面**：列表行数 / 摘要行 / 写入目标项数 / **引擎里的值**（模型侧！）；
        //   ② **零驱动**：`BtnRefresh` / `BtnRebuild` / `BtnWrite` / `CmbDirectionFilter` 在全部测试里
        //      零命中（此前只有「构造 → Show → 布局」那一层碰过这个视图）；
        //   ③ **可达**：`Wiring.Panels.cs:152` 的 `new Views.TagMonitorView(engine, …)`（菜单
        //      `MenuTagMonitor` 能打开）⇒ 阶段 135 的可达性守卫也盯着；
        //   ④ 面前就是那 4 ＋ 1 个控件。
        //
        //  **判的是哪一跳**：判「按钮/下拉 → 处理器 → **引擎或列表或摘要**」；判不到它自己的事件转发
        //  （本视图**不抬事件** —— 它是终端消费者，与前面那些「只抬事件」的宿主正好相反）。
        //
        //  夹具（`SeedTagFixture`）：往 `depot.Project.Screens[0]` 加 **2 个带绑定的控件**
        //  （`液位` 可写 / `温度` 只读）＋ 给引擎注入**桩值源**（初值 液位=10 / 温度=20）
        //  ＋ 调一次 `BuildRuntimeTags()`。
        //  ⚠ **刻意不调 `RefreshRows()`**，视图构造函数也不调 ⇒ 初态是「引擎有 2 个标签、界面还是空的」
        //  ⇒ 第一条 `BtnRefresh` 才有可判的效果（0 → 2 行）。
        // ══════════════════════════════════════════════════════════════════════════
        new("TagMonitorView",

            d =>
            {
                SeedTagFixture(d);
                return new Views.TagMonitorView(d.BindingEngine, null);
            },

            new Probe[]
            {
                // ── 数据面：界面侧 ──
                Num("标签数", c => Items(c.Window, "TagList")),
                Str("摘要", c => string.IsNullOrEmpty(Control<TextBlock>(c.Window, "SummaryText").Text) ? "空" : "非空"),
                Num("写入目标项数", c => Control<ComboBox>(c.Window, "CmbWriteTarget").ItemCount),
                // ── 数据面：**模型侧**（引擎里的值 —— `BtnWrite` 写的就是它）──
                Num("引擎值", c => (int)(c.Depot.BindingEngine.ReadValue(
                    c.Depot.Project.VariableManager.Variables.First(v => v.Name == "NcTagLevel").Id) ?? -1)),
                // ── 输入面：处理器/手势拨的下标与填的文本 ──
                InputNum("方向筛选下标", c => Control<ComboBox>(c.Window, "CmbDirectionFilter").SelectedIndex),
                InputNum("写入目标下标", c => Control<ComboBox>(c.Window, "CmbWriteTarget").SelectedIndex),
                InputNum("写入值长度", c => TextLen(c.Window, "TxtWriteValue")),
            },

            new ClickSpec[]
            {
                // ── ① 刷新：界面从「空的」变成「两行」（本表其余腿的前提）──
                Moves("BtnRefresh",
                    "`BtnRefresh.Click += (_, _) => RefreshRows()` ⇒ 读引擎标签表建 `_rows`（2 条）"
                    + "＋ `RenderRows()`（换 `TagList.ItemsSource`）＋ 写 `SummaryText` ＋ `PopulateWriteTargets()`"
                    + "（只收**非只读**标签 ⇒ 1 项，并自动选中第 0 项）。"
                    + "⚠ 初态是空的（构造函数**不**刷、夹具也刻意不刷）⇒ 这一条是 `CmbDirectionFilter` 与 "
                    + "`BtnWrite` 的**前提**，顺序不是装饰",
                    ("标签数", "2"), ("摘要", "非空"), ("写入目标项数", "1"), ("写入目标下标", "0")),

                // ── ②③ 方向筛选：只换 ItemsSource，模型一字不动 ──
                SelMoves("CmbDirectionFilter", 1,
                    "`CmbDirectionFilter.SelectionChanged += (_, _) => RenderRows()` ⇒ index 1 ＝ 只读那一条"
                    + "（`DirectionLabel == tag.monitor.read.only`）⇒「标签数」2 → **1**。"
                    + "⚠ **模型一字不动**（筛选只换 ItemsSource）—— 读数里 `摘要` / `写入目标项数` / `引擎值` "
                    + "全不动就是它的反向见证",
                    ("标签数", "1"), ("方向筛选下标", "1")),

                SelMoves("CmbDirectionFilter", 0,
                    "拨回「全部」（index 0）⇒「标签数」1 → 2。"
                    + "⚠ 同一控件第二次驱动（唯一性键是 (名字, 手势下标)）；两条合起来才说明筛选**真的是双向**的"
                    + "（只测一条的话「恒定清空」也能满足它）",
                    ("标签数", "2"), ("方向筛选下标", "0")),

                // ── ④ 重建：项目没变 ⇒ 重建出同一张表（如实记 Inert）──
                Inert("BtnRebuild",
                    "`BtnRebuild.Click` ＝ `_bindingEngine.BuildRuntimeTags()` ＋ `RefreshRows()` —— "
                    + "而**夹具的项目在本条之前没变**（没有新绑定）⇒ 重建出**同一张表**："
                    + "「标签数」「摘要」「写入目标项数」「引擎值」全都不动。"
                    + "⚠ 这是**成色③ 写入同值**、**不是**「没接线」：摘掉 `Click=` 本层也不会红"),

                // ── ⑤ 手动写入：**模型侧**真的变了（引擎里的值）──
                Arranged(Moves("BtnWrite",
                    "`BtnWrite.Click += (_, _) => TryWriteValue()`：读 `CmbWriteTarget.SelectedItem`"
                    + "（① 已自动选中第 0 项 ＝ 可写的「液位」）＋ `double.TryParse(TxtWriteValue.Text)`"
                    + "⇒ `_bindingEngine.WriteValue(液位, 42)` ⇒ **桩值源**真的被写 ⇒ 再 `RefreshRows()`。"
                    + "⚠ 判的是**模型侧**那一格：「引擎值」10 → **42**（`ReadValue` 回读）—— "
                    + "这正是本层最想要的那种见证（读数据，不读界面文案）",
                    ("引擎值", "42")),
                    c => Control<TextBox>(c.Window, "TxtWriteValue").Text = "42"),

                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 19 个宿主：DesignCanvasView（设计画布）—— 阶段 144（登记 `#77`）
        //
        //  **它是本层第一个「指针语义」宿主**，也是 `#77` 的兑现：进来的同时给本层加了
        //  **第五类手势 `Drags(...)`**（按下 → 移动 → 抬起，坐标是控件局部坐标）。
        //
        //  为什么必须加新原语（实测）：它的"选择"不是选择族控件的 `SelectionChanged`，而是画布
        //  **自己的**事件（`DesignCanvasView.axaml.cs:148` 的 `CanvasLayer.SelectionChanged`）——
        //  静态面守卫按事件名把它算成 1 个接线控件（普查表那行注释就写着「选择来自画布自身，
        //  不是控件事件」），而前四类手势**一个都驱动不了它**；编辑动作全在
        //  `DesignCanvas.Input.cs:87/278/401` 的指针处理器里。全仓测试此前**从未**驱动过画布指针交互
        //  （现有画布测试只碰 `DesignCanvas.Render`）。专题 ⇒ `docs/2026-10-01-design-canvas-pointer-primitive.md`
        //
        //  **三条指针腿都已落**（按专题 §4 的风险排序）：①点中控件 ⇒ 选中；②拖动位移 ⇒
        //  `目标控件X` 40 → 90；③绘制新建 ⇒ `控件数` 2 → 3（`AddControlAt` 直接写模型，不靠总线）。
        //  ⚠ **不按空白**：`OnPointerPressed` 的空白支会起 `DispatcherTimer`（长按框选）——
        //  那会让读数在后续腿里抖（本层的老规矩：完成时刻不由 `Pump` 决定的都不点）。
        //
        //  夹具（`SeedCanvasFixture`）：往 `depot.Screen` 加一个 **40,40 大小 120×60** 的控件
        //  （`WindowDepot` 原有那个 `状态灯` 没给宽高 ⇒ 命中矩形是 0×0，点不中）＋ `ZoomLevel = 1.0`。
        //  指针打 `(60,60)` ⇒ 落在那个矩形里。
        // ══════════════════════════════════════════════════════════════════════════
        new("DesignCanvasView",

            d =>
            {
                SeedCanvasFixture(d);
                var view = new Views.DesignCanvasView();
                view.SetScreen(d.Screen);
                view.Canvas.ZoomLevel = 1.0;
                return new Window { Content = view, Width = 900, Height = 650 };
            },

            new Probe[]
            {
                // ── 数据面：画布自己的选中集合（本阶段新原语判的就是它）──
                Num("选中数", c => ((Views.DesignCanvasView)c.Window.Content!).Canvas.SelectedControls.Count),
                // ── 数据面：模型侧（控件数与位置 —— 留给 ②/③ 两条腿）──
                Num("控件数", c => c.Depot.Screen.Controls.Count),
                Num("目标控件X", c => (int)c.Depot.Screen.Controls.First(x => x.Name == "NcCanvasTarget").X),
                // ── 数据面：**绘制出来的那个控件**（模型侧；见 ③ 绘制腿）──
                Str("末位控件类型", c => c.Depot.Screen.Controls[^1].Type.ToString()),
                Num("末位控件X", c => (int)c.Depot.Screen.Controls[^1].X),
                // ── 数据面：预览模式（`PreviewToggleButton` 写的）与软键盘可见性 ──
                Num("预览模式", c => ((Views.DesignCanvasView)c.Window.Content!).Canvas.IsPreviewMode ? 1 : 0),
                Str("软键盘", c => Control<Border>(c.Window, "SoftKeyboardPanel").IsVisible ? "显示" : "隐藏"),
            },

            new ClickSpec[]
            {
                // ── ① 指针序列：点中控件 ⇒ 选中（**本层第一次用指针手势**）──
                Drags("CanvasLayer", (60, 60), (60, 60),
                    "🆕 **指针序列手势**（`Drags`，阶段 144）：在 (60,60) 按下→抬起（无位移＝点一下）。"
                    + "`DesignCanvas.OnPointerPressed` 走 `HitTestControl(pos)` —— 控件在 (40,40) 大小 120×60、"
                    + "`ZoomLevel = 1.0` ⇒ 命中 ⇒ `_selectedControls` 写入 ＋ `FireSelectionChanged()` ⇒ "
                    + "`CanvasLayer.SelectionChanged` ⇒ 视图转发 ⇒「选中数」0 → **1**。"
                    + "⚠ 这是 `#77` 的设计里**风险最低**的那条腿（不碰拖动状态机与绘图工具）；"
                    + "② 拖动位移 / ③ 绘制新建见专题 §4（留待后续）",
                    ("选中数", "1")),

                // ── ② 指针序列·**拖动位移**（`#77` 的第二条腿）──
                Drags("CanvasLayer", (60, 60), (110, 60),
                    "🆕 **拖动位移**（`#77` 的第 ② 条腿）：在控件内 (60,60) 按下 → 移到 (110,60) → 抬起。"
                    + "`OnPointerMoved` 的 `DragMode.Move` 支：`dx = (pos.X - _dragStartPoint.X) / z` ⇒ "
                    + "`ctrl.X = SnapToGrid(_dragStartBounds.X + dx)`。夹具 `SnapStep = 5`（默认）、"
                    + "起点 X=40、dx=+50 ⇒ **40 + 50 = 90 恰好落在栅格上** ⇒「目标控件X」40 → **90**。"
                    + "⚠ 这里**不按空白**收尾：`OnPointerPressed` 的空白支会 `StartLongPressSelection` **起一个 "
                    + "`DispatcherTimer`**（长按框选）——那会让读数在后续腿里抖，本层刻意避开（见宿主注释）",
                    ("目标控件X", "90"),
                    // ⚠ 「末位控件X」这一格读的是 `Controls[^1]` —— 而夹具里 `NcCanvasTarget` **正好是最后一个**
                    //   ⇒ 拖动它就会让这一格跟着动（40 → 90）。**必须声明**：不声明就是「未声明分量外溢」。
                    //   （绘制那条腿之后 `Controls[^1]` 才是新造出来的那个控件 ⇒ 那一格改成 400。）
                    ("末位控件X", "90")),

                // ── ③ 指针序列·**绘制新建**（`#77` 的第三条腿）──
                Arranged(Drags("CanvasLayer", (400, 200), (500, 260),
                    "🆕 **绘制新建**（`#77` 的第 ③ 条腿 —— 专题 §4 里风险最高的一条）：布景把画布的 "
                    + "`DrawTool` 设成 `Button`（⚠ **经 `view.Canvas.DrawTool` 直接设**，不是 "
                    + "`DockLayoutState.SetDrawTool` —— 那条路要靠静态总线的订阅者把值推给画布，"
                    + "而总线在宿主跑前已被清（`StaticBusHygiene`）；这里判的是**画布这一跳**）。"
                    + "指针 (400,200) 按下 → (500,260) 抬起：`OnPointerPressed` 进绘图支"
                    + "（`_isDrawing = true`，且**不会**起长按 `DispatcherTimer`）→ `OnPointerMoved` 更新预览 → "
                    + "`OnPointerReleased` 走 `CommitDraw`：`w = |Δx| = 100 ≥ MinControlSize(=10)` ⇒ "
                    + "走**拖拽矩形**那一支 ⇒ `AddControlAt(Button, SnapToGrid(400), SnapToGrid(200), 100, 60)`；"
                    + "而 `AddControlAt` **直接写 `screen.Controls`**（不靠总线，阶段 146 实测）⇒ "
                    + "「控件数」2 → **3**、「末位控件类型」Button、「末位控件X」**400**"
                    + "（前两条是夹具的 `状态灯` 与 `NcCanvasTarget`；指针打的是空白处）",
                    ("控件数", "3"), ("末位控件类型", "Button"), ("末位控件X", "400")),
                    c => ((Views.DesignCanvasView)c.Window.Content!).Canvas.DrawTool = HmiControlType.Button),

                // ── ③ 预览开关：Moves（写 IsPreviewMode ＋ **清空选中**）──
                Moves("PreviewToggleButton",
                    "`PreviewToggleButton.IsCheckedChanged`（**代码接线**）⇒ `CanvasLayer.IsPreviewMode = isPreview`"
                    + "＋ `if (isPreview) CanvasLayer.ClearSelection()` ⇒ 「预览模式」0 → 1、"
                    + "「选中数」**1 → 0**（上一条留下的选中被清掉）。"
                    + "⚠ 它还会 `RaiseCanvasPreviewModeChanged` 到静态总线 —— 总线在宿主跑前已被清"
                    + "（`StaticBusHygiene`），所以这里判的是**视图这一跳**",
                    ("预览模式", "1"), ("选中数", "0")),

                // ── ③④⑤ 软键盘三键：如实记 Inert（两种不同成色）──
                Inert("KbdDoneBtn",
                    "`OnKbdDoneClicked` ＝ `HideSoftKeyboard()` —— 而**夹具里软键盘本来就是隐藏的**"
                    + "（要双击一个文本控件才会弹出，`_keyboardTarget` 非空才会走那条路）"
                    + "⇒ 它写的是**同值**（`IsVisible = false` → `false`）。"
                    + "⚠ 这是**成色③ 写入同值**、不是「没接线」：摘掉 `Click=` 本层也不会红"),

                Inert("KbdSpaceBtn",
                    "`OnKbdSpaceClicked` 首行判 `if (_keyboardTarget == null) return;` —— "
                    + "夹具里没有文本控件被激活 ⇒ **提前 return**（成色②）。"
                    + "⚠ 它真要有作用得先双击一个文本控件把软键盘叫出来，那是**另一条前置链**（未做）"),

                Inert("KbdBackspaceBtn",
                    "同 `KbdSpaceBtn`：`OnKbdBackspaceClicked` 首行也是 `_keyboardTarget == null` 就 return "
                    + "⇒ 本夹具下**什么都不写**（成色② 提前 return）"),

                Inert("KbdShiftBtn",
                    "`OnKbdShiftClicked` 只翻 `_kbdShiftOn` ＋ `UpdateKbdShiftState()`（重写**键盘自己**那些键的"
                    + "显示）⇒ 本层的探针里**没有对应分量**（成色① 未被观测）。"
                    + "⚠ 如实登记：它确实有行为，只是这一层的读数看不见"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 20 个宿主：SimulatorView（离线仿真）—— 阶段 149
        //
        //  为什么挑它（面外 5 个里**判据价值最高**的一个）：
        //   ① **数据面厚**：模型侧就有三格可读 —— `Simulator.VariableCount` / `Comms.IsConnected` /
        //      `Simulator.ClockMultiplier`（前两个还各有一条直连的界面见证：列表条数、连接状态）；
        //   ② **零驱动**：它只被"构造 → Show → 布局"那一层碰过（`WindowProductionPathUiTests`）；
        //   ③ **可达**：`Wiring.Panels.cs` 的 `new Views.SimulatorView(simulator, comms)`（菜单能打开）；
        //   ④ **面前 9 个具名按钮**（比 `DeviceManagerView` 的 15 个少），其中 **5 个真有可判效果**。
        //
        //  ⚠ **两个计时器是刻意不点的**：`_runTimeTimer` / `_uiRefreshTimer` 在 `OnToggleSimulation`
        //  里 `Start()`（构造里只创建、不启动 —— 实测读代码）⇒ `BtnStartStop` 记 `Never`；
        //  于是本宿主是**静止**的（没有"完成时刻不由 `Pump` 决定"的腿）。
        //
        //  **判的是哪一跳**：判「按钮/下拉 → 处理器 → **模拟器/通讯模拟器/列表/日志**」。
        // ══════════════════════════════════════════════════════════════════════════
        new("SimulatorView",

            d => new Views.SimulatorView(d.Simulator, d.Comms),

            new Probe[]
            {
                // ── 数据面：模型侧（三格）──
                Num("模拟器变量数", c => c.Depot.Simulator.VariableCount),
                Num("连接中", c => c.Depot.Comms.IsConnected ? 1 : 0),
                Num("时钟倍速x10", c => (int)(c.Depot.Simulator.ClockMultiplier * 10)),
                Str("信号", c => c.Depot.Simulator.GetVariables().FirstOrDefault()?.Signal.ToString() ?? "(无)"),
                // ── 数据面：界面侧 ──
                Num("变量条数", c => Items(c.Window, "VariableList")),
                Str("日志", c => (Control<TextBlock>(c.Window, "LogTextBlock").Text ?? "").Length > 0 ? "非空" : "空"),
                Num("寄存器日志条数", c => Items(c.Window, "RegisterLogList")),
                // ── 输入面 ──
                InputNum("变量选中下标", c => Control<ListBox>(c.Window, "VariableList").SelectedIndex),
                InputNum("振幅长度", c => TextLen(c.Window, "AmplitudeInput")),
                InputNum("信号类型下标", c => Control<ComboBox>(c.Window, "SignalTypeCombo").SelectedIndex),
                InputNum("倍速下标", c => Control<ComboBox>(c.Window, "ClockMultiplierCombo").SelectedIndex),
                InputNum("间隔下标", c => Control<ComboBox>(c.Window, "TickIntervalCombo").SelectedIndex),
                InputNum("写模式下标", c => Control<ComboBox>(c.Window, "WriteModeCombo").SelectedIndex),
                InputNum("寄存器地址长度", c => TextLen(c.Window, "RegAddressInput")),
                Str("寄存器值", c => TextLen(c.Window, "RegValueInput") > 0 ? "非空" : "空"),
            },

            new ClickSpec[]
            {
                // ── ① 加变量：型号侧 + 界面侧同时见证（本表其余编辑腿的前提）──
                Moves("BtnAddVariable",
                    "`BtnAddVariable.Click += (_, _) => OnAddVariable()`：`if (_running) return;`（夹具没在跑）"
                    + "⇒ 造一个 `SimulatedVariable`（`Signal = Constant`、`Amplitude = 100`）⇒ "
                    + "`_variableSimulator.RegisterVariable(...)` ＋ `RefreshVariableList()` ＋ `SelectVariable(...)`"
                    + "＋ `AppendLog(...)` ⇒ **模型侧**「模拟器变量数」1 → **2**、「信号」Sine → **Constant**；"
                    + "**界面侧**「变量条数」1 → **2**、「信号类型下标」1 → **0**（新变量是 Constant ⇒ 下拉被回填）。"
                    + "⚠ **夹具初态不是零**：`SimulatorView` 构造期自己注册了**一个演示变量**（`Signal = Sine`、已选中、"
                    + "还写了一条日志）—— 所以本表的期望全是「1 → 2」这种**增量**，不是「0 → 1」。"
                    + "⚠ 这一条是后面 `VariableList` / `SignalTypeCombo` / `BtnReset` 的**前提**，顺序不是装饰",
                    ("模拟器变量数", "2"), ("变量条数", "2"), ("信号类型下标", "0"),
                    ("信号", "Constant")),

                // ── ②③ 变量列表：清空 → 回选（判"面板随选中"的两向）──
                SelMoves("VariableList", -1,
                    "OnVariableSelectionChanged：选中被清 ⇒ `_currentVariable = null`（**但右侧编辑框不跟着清** —— "
                    + "实测「振幅长度」仍是 3）⇒ 「变量选中下标」0 → **-1**。"
                    + "⚠ 清空这一档（`-1`）是选择族自阶段 115 起就有的第二档",
                    ("变量选中下标", "-1")),

                SelMoves("VariableList", 0,
                    "回选第 0 条 ⇒ `_currentVariable` 装回来 ＋ `LoadVariableToPanel`（`_suppressEvents` 抑制回写）"
                    + "⇒ 「变量选中下标」-1 → **0**（面板控件由 `LoadVariableToPanel` 回填，与上面的「不清」合起来看："
                    + "**选中变、面板只在装回来时被写**）。"
                    + "⚠ 两条合起来才说明「面板真的跟着选中走」（只测一条的话「恒定清空」也能满足）",
                    ("变量选中下标", "0")),

                // ── ④ 信号类型下拉：写**模型**（`SimulatedVariable.Signal`）──
                SelMoves("SignalTypeCombo", 1,
                    "`SignalTypeCombo.SelectionChanged += OnSignalTypeChanged`：`if (_suppressEvents || "
                    + "`_currentVariable` == null) return;`（②③ 刚把当前变量装回来）⇒ 解析 `Tag` 里的枚举名 ⇒ "
                    + "`_currentVariable.Signal = signalType` ＋ 按信号类型开关频率输入框。"
                    + "⇒ **模型侧**「信号」Constant → **Sine**（`SignalType` 枚举第 2 项；下标 1）",
                    ("信号", "Sine"), ("信号类型下标", "1")),

                // ── ⑤ 时钟倍速：写模型 + 日志（**无 `_running` 守卫**）──
                SelMoves("ClockMultiplierCombo", 1,
                    "`OnClockMultiplierChanged` **没有 `_running` 守卫**（与相邻的 `TickIntervalCombo` 正好相反）"
                    + "⇒ `_variableSimulator.ClockMultiplier = Math.Clamp(倍速, 0.1, 10)` ＋ `AppendLog(...)`"
                    + "⇒ **模型侧**「时钟倍速x10」10 → **20**（夹具初值 1.0；下拉第 2 项 Tag = \"2\"）",
                    ("倍速下标", "1")),

                SelMoves("TickIntervalCombo", 1,
                    "`OnTickIntervalChanged` **首行就是 `if (_running) { … }`** —— 而夹具里仿真**没在跑**"
                    + "⇒ 走**提前 return**（成色②），模型与界面都没变。"
                    + "⚠ 本条因此**只声明手势自己拨的下标**（`Moves` 而不是 `SelInert`：后者不许带期望值，"
                    + "而下标这一格确实动了）。⚠ 与上一条的对照正是本表刻意留的：**同样一个下拉，一个有守卫、一个没有**",
                    ("间隔下标", "1")),

                SelMoves("WriteModeCombo", 1,
                    "`OnWriteModeChanged` 把 `Tag` 解析成 `WriteMode` 写进**私有字段 `_writeMode`** ⇒ "
                    + "它只在后面 `BtnWriteRegister` 那条路径里被读走 ⇒ 本层的探针里**没有对应分量**（成色① **未被观测**）。"
                    + "⚠ 如实登记：它确实有行为，只是这一层看不见 ⇒ 本条只声明手势下标",
                    ("写模式下标", "1")),

                // ── ⑥ 断开 / ⑦ 重连：模型侧 `IsConnected` 双向（+ 日志）──
                Moves("BtnDisconnect",
                    "`BtnDisconnect.Click += (_, _) => OnDisconnect()`：`_commSimulator.Disconnect()` ＋ "
                    + "`UpdateConnectionStatus()` ＋ `AppendLog(...)` ⇒ **模型侧**「连接中」1 → **0**"
                    + "（`Comms.IsConnected` 是 `CommunicationSimulator` 的公开属性）＋「日志」空 → 非空",
                    ("连接中", "0")),

                Moves("BtnReconnect",
                    "`OnReconnect()`：`_commSimulator.Reconnect()` ＋ 状态行 ＋ 日志 ⇒ **模型侧**「连接中」0 → **1**。"
                    + "⚠ 与上一条合起来是**双向**见证（只测一条的话「恒置 0」也能满足）",
                    ("连接中", "1")),

                // ── ⑧⑨ 寄存器读 / 写：布景填地址（输入面）⇒ 写模型 + 寄存器日志 ──
                Arranged(Moves("BtnReadRegister",
                    "`OnReadRegister`：`int.TryParse(RegAddressInput.Text)` —— 布景先把地址填成 `100`（输入面）⇒ "
                    + "`_commSimulator.ReadRegister(100)` ⇒ 值写进 `RegValueInput` ＋ `AppendLog(...)`。"
                    + "⚠ **实测**：`AppendLog` 写的是 `LogTextBlock`（初态已非空 ⇒ 看不见差别），"
                    + "「寄存器日志条数」也不动 ⇒ 本条真正的见证是**「寄存器值」空 → 非空**。"
                    + "⚠ 不布景的话它走「无效地址」支（也是一条路，但那条路只证明 TryParse 失败）",
                    ("寄存器值", "非空")),
                    c => Control<TextBox>(c.Window, "RegAddressInput").Text = "100"),

                Arranged(Inert("BtnWriteRegister",
                    "**成色① 未被观测**（如实登记）：`OnWriteRegister` 布景后（地址 `100` ＋ 值 `1.5`）会 "
                    + "`_commSimulator.WriteRegister(address, value)` ＋ `AppendLog(...)` —— 但**两处落点都看不见**："
                    + "`AppendLog` 写进 `LogTextBlock`，而那一行在夹具初态**已经非空**（构造期演示变量写过日志）"
                    + "⇒ 空/非空口径读不到差别；「寄存器日志条数」只由 `_commSimulator.RegisterWritten` 事件驱动"
                    + "（`OnRegisterWritten → RefreshRegisterLog`），**直接写不抬那个事件**。"
                    + "🩸 首跑实测：这一条原本按「写成功 ⇒ 寄存器日志 +1」声明，结果**一个分量都没动** —— "
                    + "读数把这条口径错误当场顶出来了（本条因此从 `Moves` 转 `Inert`，理由按实测写）。"
                    + "⚠ 它不是「没接线」：摘掉 `Click=` 本层也不会红，但**读寄存器那条**（上一条）会 —— "
                    + "两条合起来才说明「读路径判得到、写路径这一层看不见」"),
                    c => { Control<TextBox>(c.Window, "RegAddressInput").Text = "100";
                           Control<TextBox>(c.Window, "RegValueInput").Text = "1.5"; }),

                // ── ⑩ 复位：把 ① 造出来的东西清掉（模型 + 界面同向）──
                Moves("BtnReset",
                    "`BtnReset.Click += (_, _) => OnReset()`：`_variableSimulator.Clear()` ＋ `_commSimulator.Clear()`"
                    + "＋ 清 `_currentValues`/`_registerChanges` ＋ `_variableCounter = 0` ＋ 清空 UI"
                    + "⇒ ⚠ **实测**：`Clear()` 之后**仍是 1 个变量**（构造期那个演示变量会被重建）"
                    + "⇒ 「模拟器变量数」2 → **1**、「变量条数」2 → **1**、「寄存器日志条数」1 → **0**。"
                    + "⚠ 它排在最后：前面几条造出来的状态正是它的输入",
                    ("模拟器变量数", "1"), ("变量条数", "1"), ("寄存器日志条数", "0")),

                // ── ⑪–⑬ 三个不点（三种不同理由）──
                Never("BtnDeleteVariable",
                    "`BtnDeleteVariable.Click += async (_, _) => await OnDeleteVariable()` —— **`async`**"
                    + "（多半还带确认框）⇒ 完成时刻不由 `Pump` 决定、读数会抖"
                    + "（与 `CommunicationConfigView.BtnTestConnection` 同一条理由）"),
                Never("BtnStartStop",
                    "`OnToggleSimulation()` 会 `_runTimeTimer.Start()` ＋ `_uiRefreshTimer.Start()` —— "
                    + "**两个 `DispatcherTimer`**：完成时刻不由 `Pump` 决定 ⇒ 读数会抖。"
                    + "⚠ 本宿主因此是**静止**的（构造里只创建、不启动那两个计时器）"),
                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 21 个宿主：DeviceManagerView（设备管理器）—— 阶段 150
        //
        //  为什么挑它：面外 4 个里**未知量最少**的一个 —— 15 个具名按钮里 **14 个是平台桩**
        //  （6 个文件/目录选择器 ＋ 8 个 `async void` 的烧写/升级/生成路径）⇒ 如实记 `Never`；
        //  真正有可判效果的只有 **1 个按钮 ＋ 1 个下拉**：
        //   · `CmbChannelType`（XAML 属性接线）⇒ `ApplyChannelPanels()` 三个面板的 `IsVisible`（**三个见证**）；
        //   · `BtnRefreshPorts` ⇒ `RefreshPorts()` ＋ `LogTagged(...)`。
        //
        //  🩸 **`BtnRefreshPorts` 的诚实边界（本层第二次撞到「Post 不排水」）**：它的日志走
        //  `Log()` → **`Dispatcher.UIThread.Post(...)`**，而本层的 `Pump` **不排水** `Post`
        //  （见类注释「面的边界」）⇒ 日志条数在本层看不见变化；端口列表在无头沙箱里也是空的
        //  （`SerialPort.GetPortNames()` 没设备）⇒ 这一条如实记 **`Inert`（成色① 未被观测）**。
        //
        //  **判的是哪一跳**：判「下拉 → 处理器 → 三个面板的可见性」这一跳；判不到它自己的事件转发。
        // ══════════════════════════════════════════════════════════════════════════
        new("DeviceManagerView",

            _ => new Views.DeviceManagerView(),

            new Probe[]
            {
                // ── 数据面：三个通道面板的可见性（`ApplyChannelPanels` 写的）──
                Str("串口面板", c => Control<Control>(c.Window, "SerialPanel").IsVisible ? "显示" : "隐藏"),
                Str("波特率面板", c => Control<Control>(c.Window, "BaudPanel").IsVisible ? "显示" : "隐藏"),
                Str("TCP面板", c => Control<Control>(c.Window, "TcpPanel").IsVisible ? "显示" : "隐藏"),
                // ── 数据面：端口列表与日志（`RefreshPorts` / `Log` 写的）──
                Num("端口项数", c => Control<ComboBox>(c.Window, "CmbPort").ItemCount),
                InputNum("日志条数", c => Items(c.Window, "LogBox")),
                // ── 输入面 ──
                InputNum("通道类型下标", c => Control<ComboBox>(c.Window, "CmbChannelType").SelectedIndex),
                InputNum("端口下标", c => Control<ComboBox>(c.Window, "CmbPort").SelectedIndex),
            },

            new ClickSpec[]
            {
                // ── ① 通道类型 → 以太网：三个面板同时翻（本宿主的**主判据**）──
                SelMoves("CmbChannelType", 1,
                    "`CmbChannelType` 是 **XAML 属性接线**（`SelectionChanged=\"OnChannelTypeChanged\"`）⇒ "
                    + "`ApplyChannelPanels()`：`isSerial = SelectedIndex == 0` ⇒ 三个面板一次性落地 —— "
                    + "「串口面板」显示 → **隐藏**、「波特率面板」显示 → **隐藏**、「TCP面板」隐藏 → **显示**。"
                    + "⚠ 三个见证一起动，正好排除了「只是某个面板自己抖了一下」那种巧合",
                    ("串口面板", "隐藏"), ("波特率面板", "隐藏"), ("TCP面板", "显示"),
                    ("通道类型下标", "1")),

                SelMoves("CmbChannelType", 0,
                    "拨回「串口」（index 0）⇒ 三个面板**原路返回**（串口/波特率 显示、TCP 隐藏）。"
                    + "⚠ 同一控件第二次驱动（唯一性键是 (名字, 手势下标)）——两条合起来才是**双向**见证",
                    ("串口面板", "显示"), ("波特率面板", "显示"), ("TCP面板", "隐藏"),
                    ("通道类型下标", "0")),

                // ── ② 刷新端口：如实记 Inert（日志经 Dispatcher.Post、本层不排水）──
                Inert("BtnRefreshPorts",
                    "`OnRefreshPortsClicked` ＝ `RefreshPorts()` ＋ `LogTagged(...)` —— 但**两处都不落在本层的读数上**："
                    + "① `RefreshPorts()` 读的是**真实串口名**（无头沙箱里没有设备 ⇒ `CmbPort` 项数与下标都不动）；"
                    + "② 日志走 `Log()` → **`Dispatcher.UIThread.Post(...)`**，而本层的 `Pump` **不排水 `Post`**"
                    + "（见类注释「面的边界」）⇒ 「日志条数」也不动。"
                    + "⚠ 这是**成色① 未被观测**、**不是**「没接线」：摘掉 `Click=` 本层也不会红 —— "
                    + "要判它得先有「值源桩（假串口名）」或「排水 `Post`」的原语，两者都还没建"),

                // ── ③–⑯ 十四个平台桩 / 关窗（三种理由）──
                Never("BtnBrowseFelImage",
                    "`OnBrowseFelImageClicked` 走 `PickFileAsync(...)` → **`TopLevel.StorageProvider.OpenFilePickerAsync`**"
                    + "（平台桩）：headless 下不归 `Pump` 管（与 `AssetManagerView.BtnImportImage` 同一条理由）"),
                Never("BtnBrowseFelTool",
                    "同上：`PickFileAsync(...)` 文件选择器（FEL 工具）"),
                Never("BtnBrowseFwSwu",
                    "同上：`PickFileAsync(...)` 文件选择器（固件包）"),
                Never("BtnBrowseHmiLib",
                    "同上：`PickFileAsync(...)` 文件选择器（HMI 库）"),
                Never("BtnBrowseHmiSwu",
                    "同上：`PickFileAsync(...)` 文件选择器（HMI 升级包）"),
                Never("BtnBrowseOutput",
                    "`OnBrowseOutputClicked` 走 `PickFolderAsync(...)` → **`OpenFolderPickerAsync`**（平台桩）"),
                Never("BtnOpenClose",
                    "`async void OnOpenCloseClicked`：开/关设备通道 —— 完成时刻不由 `Pump` 决定 ⇒ 读数会抖"
                    + "（与 `CommunicationConfigView.BtnTestConnection` 同一条理由）"),
                Never("BtnDetect",
                    "`async void OnDetectClicked`：探测设备（走通道/超时）⇒ 同上"),
                Never("BtnCheckUpdate",
                    "`async void OnCheckUpdateClicked`：检查更新（网络）⇒ 同上"),
                Never("BtnUpdateHmi",
                    "`async void OnUpdateHmiClicked`：升级 HMI（走 `_flasher`）⇒ 同上"),
                Never("BtnFlashFw",
                    "`async void OnFlashFwClicked`：烧写固件（`_flasher.FlashAsync`）⇒ 同上"),
                Never("BtnGenerate",
                    "`async void OnGenerateClicked`：生成产物（可能带文件选择器）⇒ 同上"),
                Never("BtnFelFlash",
                    "`async void OnFelFlashClicked`：FEL 烧写（`_flasher.FelFlashAsync`）⇒ 同上"),
                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 22 个宿主：ScriptEditorView（Lua 脚本编辑器）—— 阶段 157
        //
        //  **为什么挑它**：面外只剩 3 个视图，其中 `PropertyPanelView` 已判定不扩（阶段 118），
        //  候选只有 `ScriptEditorView`（3 个选择控件）与 `RecipeEditorView`（5 个）。三条理由：
        //   ① 选择控件少（3 < 5）；
        //   ② **它的编辑动作直接写模型** —— `OnNewScript` / `OnDeleteScript` / `InsertTemplate`
        //      都不经过对话框；而 `RecipeEditorView` 的新建/克隆/删除**全在 `dialog.Closed` 里**，
        //      本层不驱动对话框 ⇒ 那 14 个按钮会一起退化成 `Never`（宿主很薄）；
        //   ③ 它有 **4 条同步的模板插入腿**（每条都真写模型代码），是面外视图里唯一能给出
        //      ≥4 条独立 `Moves` 的一个。
        //
        //  ⚠⚠ **本宿主是第一个"自己是 `Window`"的宿主**（`ScriptEditorView : ChromeWindow`）。
        //  前面 21 个里，凡处理器依赖 `Parent != null` 的（如第 16 个 `FaceplateLibraryView`）
        //  都是 `UserControl`，靠「装进 `Window.Content`」把 `Parent` 装配出来；而 `Window`
        //  **不能**当另一个 `Window` 的子控件 ⇒ 本宿主这一跑里 `Parent == null` **恒成立**
        //  （分量「有Parent」当场量出来，不靠读代码断定）。阶段 157 当时有三个字段处理器
        //  （`OnScriptFieldChanged` / `OnCategoryChanged` / `OnCodeTextChanged`）首行就判
        //  `Parent == null` ⇒ `CboCategory` / `ChkEnabled` 两条只能如实记 `Inert`。
        //
        //  🩸 **阶段 191 把这条前提量到了生产形状上**（登记 `#89`）：那三个处理器在
        //  `ShowDialog(owner)`（`Owner` 有、`Parent` 仍 null）下同样首行返回 ⇒ **用户打的字
        //  一个都不落模型**。阶段 192 **修掉了守卫**（防重入改由 `_isUpdating` ＋ `_currentScript`
        //  负责，程序化回填全部收进 `_isUpdating` 窗口）⇒ 本宿主那两条从 `Inert` 转
        //  `SelMoves` / `Moves`（**腿的期望值一个字没改**：改的只是"处理器到底跑没跑"）。
        //
        //  ⚠ 另一条性质仍然成立：`PopulateEditPanel` 里那句 `TxtCodeEditor.Text = script.Code`
        //  **不会**点起 500ms 去抖定时器 —— 它现在在 `_isUpdating` 窗口内（阶段 192 把它挪进去的，
        //  见产品代码注释）。本宿主因此仍是**静止**的（没有"完成时刻不由 `Pump` 决定"的腿），
        //  这正是本层喜欢的样子。
        //
        //  🩸 夹具要点：`WindowDepot` 只给一条脚本（`开机脚本`）—— 只有一条时「选中 0」与
        //  「选中 1」分不出来，筛选腿也无从判起。`SeedScriptFixture` 补第二条（`报警联动脚本`，
        //  **分类刻意不同**：Alarm）⇒ 选择腿有两条可比、筛选腿有东西可筛。
        //
        //  **判的是哪一跳**：判「按钮/选择 → 处理器 → **脚本集合 / 列表 / 编辑面板 / 校验点**」，
        //  含三条字段回写（阶段 192 起可判）。
        // ══════════════════════════════════════════════════════════════════════════
        new("ScriptEditorView",

            d =>
            {
                SeedScriptFixture(d);
                return new Views.ScriptEditorView(d.Scripts, d.Bindings, d.Project, null, d.Ai);
            },

            new Probe[]
            {
                // ── 数据面：模型侧（脚本集合 / 第二条脚本的代码与分类 / 绑定数）──
                Num("模型脚本数", c => c.Depot.Scripts.Count),
                Num("绑定数", c => c.Depot.Bindings.Count),
                Str("报警脚本分类", c => ScriptByName(c, "报警联动脚本")?.Category ?? "(无)"),
                Str("报警脚本启用", c => ScriptByName(c, "报警联动脚本") is { } s
                    ? (s.IsEnabled ? "开" : "关") : "(无)"),
                Num("报警脚本代码长度", c => ScriptByName(c, "报警联动脚本")?.Code.Length ?? -1),
                // 四个钩子的**有无**压成一个短串（"1" → "12" → "123" → "1234"）——
                // 一条腿只插一个模板，压成串既省分量、又让"插了哪一个"直接可读。
                Str("报警脚本钩子", c =>
                {
                    var code = ScriptByName(c, "报警联动脚本")?.Code ?? "";
                    return (code.Contains("on_click", StringComparison.Ordinal) ? "1" : "")
                         + (code.Contains("on_value_changed", StringComparison.Ordinal) ? "2" : "")
                         + (code.Contains("on_timer", StringComparison.Ordinal) ? "3" : "")
                         + (code.Contains("on_alarm", StringComparison.Ordinal) ? "4" : "");
                }),
                // ── 数据面：界面侧 ──
                Num("列表项数", c => Items(c.Window, "ScriptList")),
                Str("编辑面板", c => Control<Control>(c.Window, "EditPanel").IsVisible ? "显示" : "隐藏"),
                Str("校验点色", c => ScriptValidationDot(c.Window)),
                Num("运行面板可见", c => Control<Control>(c.Window, "RunOutputPanel").IsVisible ? 1 : 0),
                Str("输出", c => TextLen(c.Window, "TxtRunOutput") > 0 ? "非空" : "空"),
                Str("状态栏", c => string.IsNullOrEmpty(Control<TextBlock>(c.Window, "ListStatusText").Text)
                    ? "空" : "非空"),
                // ── 只进读数：本宿主自己是 `Window` ⇒ `Parent` 恒 null。阶段 192 修 `#89` 之后
                //    处理器的防重入不再依赖它 ⇒ 这一格只记录"夹具形状"，没有任何腿靠它承重 ──
                Num("有Parent", c => c.Window.Parent is null ? 0 : 1),
                // ── 输入面 ──
                InputNum("脚本选中下标", c => Control<ListBox>(c.Window, "ScriptList").SelectedIndex),
                InputNum("筛选下标", c => Control<ComboBox>(c.Window, "CmbCategoryFilter").SelectedIndex),
                InputNum("描述长度", c => TextLen(c.Window, "TxtDescription")),
                // ── 只进读数（本地化文案 / 长度：不冻进判据）──
                Voice("名称", c => Control<TextBox>(c.Window, "TxtScriptName").Text ?? "(空)"),
                Voice("校验文案", c => string.IsNullOrEmpty(Control<TextBlock>(c.Window, "TxtValidationStatus").Text)
                    ? "空" : "非空"),
                Voice("编辑器代码长度", c => TextLen(c.Window, "TxtCodeEditor").ToString()),
            },

            new ClickSpec[]
            {
                // ── ① 选中第一条：右侧面板被装填（后面所有编辑腿的前提）──
                SelMoves("ScriptList", 0,
                    "`OnScriptSelectionChanged` → `SelectScript` → `PopulateEditPanel`：把第 0 条"
                    + "（`开机脚本`）装进右侧面板 ⇒ 「编辑面板」隐藏 → **显示**、「描述长度」0 → **5**"
                    + "（夹具给这一条铺了 5 个字的描述）；并跑一次 `Validate`（该条代码是空的 ⇒ `EMPTY_CODE`）"
                    + "⇒ 「校验点色」灰 → **红**。"
                    + "⚠ 那句 `TxtCodeEditor.Text = script.Code` 在阶段 192 之后**在 `_isUpdating` 窗口内**"
                    + "（阶段 192 把它挪进去的，原先是靠 `Parent == null` 守卫挡着）⇒ 去抖定时器"
                    + "**没被点起来**，本宿主因此是静止的",
                    ("脚本选中下标", "0"), ("编辑面板", "显示"), ("描述长度", "5"), ("校验点色", "红")),

                // ── ② 换到第二条：面板整块被重写 ──
                SelMoves("ScriptList", 1,
                    "换到第 1 条（`报警联动脚本`）⇒ `PopulateEditPanel` 重写面板。"
                    + "⚠ 只判「选中下标变了」不够（那是手势自己写的）——「描述长度」是**处理器写的**"
                    + "（0 → **8**，夹具里那条描述是 8 个字），它才证明换的真是那一条、而不是只动了下标。"
                    + "⚠ 见证刻意选**描述**而不是**名称**：新脚本的名称走字典（`新脚本 2`），"
                    + "名称冻进判据等于让判据依赖语言 ⇒ 名称只进 `Voice` 读数",
                    ("脚本选中下标", "1"), ("描述长度", "8")),

                // ── ③④ 两条字段腿：阶段 192 修 `#89` 后**真的会写模型**（阶段 157~191 记的是 `Inert`）──
                SelMoves("CboCategory", 1,
                    "`OnCategoryChanged`：`_currentScript.Category = \"OnValueChanged\"`（`CboCategory` 第 1 项）"
                    + "⇒ 「报警脚本分类」Alarm → **OnValueChanged**。"
                    + "⚠ **阶段 157~191 这里记的是 `Inert`**（前提不满足）：处理器首行 `Parent == null`，"
                    + "而本宿主自己是 `Window` ⇒ 恒 null。阶段 191 量出这条在**生产形状**下也成立（登记 `#89`），"
                    + "阶段 192 修掉守卫 ⇒ 同一条手势现在**真的**写模型（期望值一个字没改）。"
                    + "⚠ 拨动只改了 `CboCategory.SelectedIndex`（手势自己写的；本层刻意不为它设探针 ⇒ 读数不动）",
                    ("报警脚本分类", "OnValueChanged")),

                Moves("ChkEnabled",
                    "`ChkEnabled.IsCheckedChanged += OnScriptFieldChanged`：翻转 `IsChecked`（开 → 关）⇒"
                    + " `_currentScript.IsEnabled = false` ⇒ 「报警脚本启用」开 → **关**。"
                    + "⚠ 手势翻转的 `IsChecked` 本层**不为它设探针**：那是手势自己写的，拿它当见证等于"
                    + "「把效果搬进手势里」（本层对「布景」同一条纪律）。"
                    + "⚠ 同 ③：这一条在阶段 192 之前记的是 `Inert`（同一条 `Parent == null` 守卫）",
                    ("报警脚本启用", "关")),

                // ── ⑤–⑧ 四条模板插入腿：真写模型代码（长度是模板的字面长度、与语言无关）──
                Moves("BtnTemplateOnClick",
                    "`InsertTemplate(ScriptTemplate.OnClick)`：`TxtCodeEditor.Text.Insert(CaretIndex, code)`"
                    + " ＋ `_currentScript.Code = newText`（**模型侧**）⇒「报警脚本代码长度」0 → **287**、"
                    + "「报警脚本钩子」(空) → **1**；同时把校验文案写成「已插入模板 …」（Voice，只进读数）。"
                    + "⚠ `287` 是 `ScriptTemplate.OnClick.Code` 的**字面长度**（C# verbatim 字符串、与语言无关）",
                    ("报警脚本代码长度", "287"), ("报警脚本钩子", "1")),

                Moves("BtnTemplateOnValueChanged",
                    "同上，插入 `OnValueChanged` 模板（`CaretIndex` 已被上一条推到末尾）⇒ 累计 **572**、钩子 → **12**",
                    ("报警脚本代码长度", "572"), ("报警脚本钩子", "12")),

                Moves("BtnTemplateTimer",
                    "插入 `Timer` 模板 ⇒ 累计 **807**、钩子 → **123**",
                    ("报警脚本代码长度", "807"), ("报警脚本钩子", "123")),

                Moves("BtnTemplateAlarm",
                    "插入 `Alarm` 模板 ⇒ 累计 **1076**、钩子 → **1234**（四条腿合起来证明四个按钮"
                    + "各自插的是**自己那一个**模板：只按长度也算得出来，但钩子串直接点名了是哪一个）",
                    ("报警脚本代码长度", "1076"), ("报警脚本钩子", "1234")),

                // ── ⑨ 校验：把"四段模板拼起来"真解析一遍 ──
                Moves("BtnValidate",
                    "`ValidateCurrentScript()` → `UpdateValidationStatus(_currentScript)` →"
                    + " `ScriptEngineService.Validate`（MoonSharp 只 `LoadString`、不执行）：四钩子互不重名 ⇒"
                    + " 语法有效 ⇒ 「校验点色」红 → **绿**。"
                    + "⚠ 见证刻意选**颜色**不选文案：文案走字典（zh/en 不同），颜色是调色板常量 ⇒ 与语言无关",
                    ("校验点色", "绿")),

                // ── ⑩⑪ 调试运行 → 清输出（一写一清，两条合起来才说明"清"真清了东西）──
                Moves("BtnDebugRun",
                    "`RunCurrentScript()`：`PrepareLuaForSandbox` → `ScriptSandboxExecutor.Execute`"
                    + "（2000ms 超时）⇒ `RunOutputPanel.IsVisible = true` ＋ 结果写进 `TxtRunOutput` ⇒"
                    + "「运行面板可见」0 → **1**、「输出」空 → **非空**。"
                    + "⚠ 执行是**同步**的（就在点击这一跳里跑完）⇒ 读数不依赖 `Pump` 之外的时刻；"
                    + "本层**不判**执行结果本身（沙箱里 `hmi_read` 给出什么由沙箱决定）",
                    ("运行面板可见", "1"), ("输出", "非空")),

                Moves("BtnClearOutput",
                    "`BtnClearOutput.Click += (_, _) => TxtRunOutput.Text = \"\";` ⇒ 「输出」非空 → **空**。"
                    + "⚠ 面板本身**不**隐藏（「运行面板可见」仍是 1）—— 这两格合起来说明它清的只是正文",
                    ("输出", "空")),

                // ── ⑫ 删除当前脚本：模型 -1、列表 -1、面板兜底回选第一条 ──
                Moves("BtnDeleteScript",
                    "此刻选中第 1 条 ⇒ `_scripts.Remove`（2 → **1**）＋ `_engine.UnloadScript` ＋ 清相关绑定"
                    + "（夹具那条 `ScriptBinding.ScriptId` 是空的 ⇒ 「绑定数」不动）＋ `RefreshScriptList` ⇒"
                    + " 列表 2 → **1**、「描述长度」8 → **5**、「校验点色」绿 → **红**"
                    + "（新选中那条代码是空的）、「状态栏」空 → **非空**（`ScriptDeleted` 文案）；"
                    + "两条按名字找的探针落到「对象没了」那一档 ⇒ 钩子 → **\"\"**、代码长度 → **-1**、"
                    + "分类/启用 → **(无)**。"
                    + "🩸 「脚本选中下标」**1 → -1**：删掉的正是当前选中项 ⇒ `Items.Clear()` 触发的选择变更把"
                    + " `_currentScript` 清空、回填后「恢复选中」又找不到那条 ⇒ 列表停在 **-1**；"
                    + "随后 `OnDeleteScript` 的兜底分支 `SelectScript(firstScript)` **只装面板、不写 `SelectedItem`**"
                    + "（与下一条新建腿同形）⇒ 面板写着 `开机脚本`、列表却是「没选中」",
                    ("模型脚本数", "1"), ("列表项数", "1"), ("脚本选中下标", "-1"), ("描述长度", "5"),
                    ("校验点色", "红"), ("状态栏", "非空"), ("报警脚本分类", "(无)"), ("报警脚本启用", "(无)"),
                    ("报警脚本钩子", ""), ("报警脚本代码长度", "-1")),

                // ── ⑬ 新建：模型 +1、列表 +1，但**面板与列表选中的不是同一条** ──
                Moves("BtnNewScript",
                    "`OnNewScript()`：造一条新脚本（名字走字典、代码是 `script.lua.template`）⇒"
                    + " 模型 1 → **2**、列表 1 → **2**。🩸 最有信息的一格是「脚本选中下标」**-1 → 0**："
                    + "进本条之前列表**没有**选中项 ⇒ `RefreshScriptList` 里 `Items.Clear()` **不触发**"
                    + " `SelectionChanged`（没有选中可清）⇒ `_currentScript` 存活 ⇒ 回填后「恢复选中」"
                    + "按 Id 把**老那一条**选回来（下标 0）⇒ 随后 `SelectScript(new)` 只写右侧面板、"
                    + "不写 `SelectedItem` ⇒ **面板显示新脚本、列表选中的却是第一条**（面板与列表不同步，"
                    + "本条如实钉住它；与上一条删除腿合起来，这条不对称在两处出现）；"
                    + "新脚本的代码是 `script.lua.template`（合法 Lua）⇒ 「校验点色」红 → **绿**；"
                    + "新脚本的描述是空的 ⇒ 「描述长度」5 → **0**",
                    ("模型脚本数", "2"), ("列表项数", "2"), ("脚本选中下标", "0"),
                    ("校验点色", "绿"), ("描述长度", "0")),

                // ── ⑭⑮ 筛选：列表被过滤、面板被关掉（两向：2 → 0 → 2）──
                SelMoves("CmbCategoryFilter", 3,
                    "筛选到「Timer」⇒ `RefreshScriptList(\"Timer\")`：此刻两条脚本都是 `OnClick` ⇒"
                    + " 列表 2 → **0**；这一下 `Items.Clear()` 时**有**选中项（新建那条刚落在下标 0）⇒"
                    + " 触发 `SelectionChanged` ⇒ `_currentScript` 被清空 ⇒ 「编辑面板」显示 → **隐藏**、"
                    + "「脚本选中下标」0 → **-1**（这是筛选的**副作用**，判出来才诚实）",
                    ("筛选下标", "3"), ("列表项数", "0"), ("编辑面板", "隐藏"), ("脚本选中下标", "-1")),

                SelMoves("CmbCategoryFilter", 0,
                    "拨回「全部」⇒ 列表 0 → **2**；「编辑面板」仍**隐藏**（没有选中项可装 ——"
                    + "它只在有人真的选中时才被装填）",
                    ("筛选下标", "0"), ("列表项数", "2")),

                // ── ⑯–⑲ 不点 / 看不见的四条（理由都是实测过的）──
                Never("BtnAiGenerate",
                    "`OnAiGenerateScript` → `UiOperationGuard.Run(...)` → `dialog.ShowDialog<HmiScript?>(this)`"
                    + "（`AiScriptDialog` 模态框）：本层不驱动对话框（与 `CommunicationConfigView.BtnTestConnection` 同族）"),

                Never("BtnHelp",
                    "`ShowLuaHelp()` 会 `ShowDialog` 那个 Lua 帮助窗 —— 实测：headless 下"
                    + "「垂直可滚动的 `ScrollViewer` ＋ 多行 `Wrap` 的 `TextBlock`」首次布局**定不下来、"
                    + "把整个测试进程挂住**（`EXIT=124`，见 `docs/2026-09-22-headless-scrollviewer-wrap-hang.md`）"
                    + "⇒ **绝不能点**（这条 Never 买的是「进程别挂」）"),

                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),

                // ⚠ 放在表尾：`MenuFlyout` 弹出后会留在界面上，只点它这一下、后面没有腿 ⇒ 不影响别的读数
                Inert("BtnInsertTemplate",
                    "`ShowTemplatePicker()` 造一个 `MenuFlyout` 并 `ShowAt(BtnInsertTemplate)` ——"
                    + " **不写任何数据面分量、也不开 `Window`**（flyout 不是 `Window`，本层读数看不见它）"
                    + "⇒ 成色①未被观测。⚠ 它**不是**「没接线」：`ShowAt` 真的执行了；"
                    + "要让本层看见这一跳，得先有「popup / flyout」原语（与 `BtnRefreshPorts` 同族）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 23 个宿主：RecipeEditorView（配方编辑器）—— 阶段 158
        //
        //  **面外最后一块真面**（加完它，面外只剩 `PropertyPanelView` —— 阶段 118 已判定不扩）。
        //  它的形状与第 22 个正好互补：那个是「按钮很实、选择很少」，这个是**选择很多**（5 条）
        //  而 14 个具名按钮里有 **8 个走对话框/平台桩**（新增/克隆/删除分组与配方 = `TextInputDialog`
        //  / `ConfirmDialog`；导入导出 CSV = 文件选择器）⇒ 本层不驱动对话框 ⇒ 如实记 `Never`。
        //  真正能判的是**参数编辑那一块**：`BtnAddParam` / `BtnDeleteParam` / `BtnMoveUp` / `BtnMoveDown`
        //  / `BtnSaveRecipe` 与两个参数下拉（`CmbParamVariable` / `CmbParamDataType`）**都直接写模型**。
        //
        //  ⚠ 三条与其它宿主不同的性质：
        //   ① **状态反馈写的是窗口 `Title`**：`ShowStatus(msg)` ⇒ `Title = "配方编辑器 — msg"`
        //      ⇒ 分量「状态条」按**有没有那个分隔符**判（不冻文案、与语言无关）；
        //   ② `ChkEnabled` **根本没有接线**（全文件只有两处写、一处读，没有订阅）⇒ 记 `Inert`
        //      （成色④ 事件从未订阅那一类），理由是实测的：`OnSaveRecipe` 会**读**它，但没有任何
        //      东西在它变化时做响应；
        //   ③ `RefreshRecipes()` 末尾调 `ClearEditPanel()` ⇒ **换分组会顺带清编辑面板**；而
        //      `BtnSaveRecipe` 成功后又 `RefreshGroups/RefreshRecipes` ＋ 重新选中当前配方
        //      ⇒ 那几条腿的读数要把这层「清-装」的净效果算进去（下面的期望全是实测值）。
        //
        //  🩸 夹具：`WindowDepot.Recipes` 是**空**的（`new RecipeManager()`）⇒ 不铺的话
        //  「分组 0 / 配方 0 / 参数 0」，全部腿退化成零信息断言。`SeedRecipeFixture` 铺
        //  **2 分组 / 3 配方**（A 组两条、B 组一条），并把 A2 的两条参数**刻意设成不同**
        //  （个数相同、默认值长度不同：`120` vs `5`）—— 「换一条参数」那条腿靠这个差别才有
        //  **处理器写的**见证（否则只能读手势自己写的下标）。
        //  ⚠ 名字全用 ASCII（`NcGroupA` / `NcRecipeA1` …）：`GetAllGroups()` / `GetRecipesByGroup()`
        //  都按 `Name` 排序，而 `OrderBy` 用**当前区域性**比较器 ⇒ 中文名在不同 locale 下顺序
        //  可能不同；ASCII 名把这个不确定性摁掉（检查单：夹具要可分辨、且与语言无关）。
        //
        //  **判的是哪一跳**：判「选择/按钮 → 处理器 → **模型里的配方与参数 / 三个列表 / 编辑面板 / 标题**」。
        // ══════════════════════════════════════════════════════════════════════════
        new("RecipeEditorView",

            d =>
            {
                SeedRecipeFixture(d);
                return new Views.RecipeEditorView(d.Recipes, d.Variables);
            },

            new Probe[]
            {
                // ── 数据面：模型侧 ──
                Num("模型分组数", c => c.Depot.Recipes.GetAllGroups().Count),
                Num("模型配方数", c => c.Depot.Recipes.GetAllRecipes().Count),
                Num("A2参数数", c => RecipeByName(c, "NcRecipeA2").Items.Count),
                // 排序后**前两项的标签**：`Take(2)` 是为了让"新增参数"那条腿不影响它
                // （新参数的 SortOrder = max + 1 ⇒ 总排在末尾）。
                Str("A2前两项", c => string.Join(",", RecipeByName(c, "NcRecipeA2").Items
                    .OrderBy(i => i.SortOrder).Take(2).Select(i => i.Label))),
                Num("A2版本", c => RecipeByName(c, "NcRecipeA2").Version),
                Str("下限类型", c => ItemByLabel(c, "NcRecipeA2", "压力下限").DataType),
                Str("下限变量", c => string.IsNullOrEmpty(ItemByLabel(c, "NcRecipeA2", "压力下限").VariableId)
                    ? "未绑" : "已绑"),
                Str("备用类型", c => ItemByLabel(c, "NcRecipeA2", "压力备用").DataType),
                Str("备用变量", c => string.IsNullOrEmpty(ItemByLabel(c, "NcRecipeA2", "压力备用").VariableId)
                    ? "未绑" : "已绑"),
                // ── 数据面：界面侧 ──
                Num("分组项数", c => Items(c.Window, "GroupList")),
                Num("配方项数", c => Items(c.Window, "RecipeList")),
                Num("参数项数", c => Items(c.Window, "ParamList")),
                Num("参数面板可见", c => Control<Control>(c.Window, "ParamEditPanel").IsVisible ? 1 : 0),
                // `ShowStatus` 写的是 **`Title`**：按分隔符判"有没有提示过"，不冻文案
                Str("状态条", c => (c.Window.Title ?? "").Contains("—") ? "已提示" : "无提示"),
                // ── 输入面 ──
                InputNum("分组下标", c => Control<ListBox>(c.Window, "GroupList").SelectedIndex),
                InputNum("配方下标", c => Control<ListBox>(c.Window, "RecipeList").SelectedIndex),
                InputNum("参数下标", c => Control<ListBox>(c.Window, "ParamList").SelectedIndex),
                InputNum("配方分组下标", c => Control<ComboBox>(c.Window, "CmbRecipeGroup").SelectedIndex),
                InputNum("类型下标", c => Control<ComboBox>(c.Window, "CmbParamDataType").SelectedIndex),
                InputNum("变量下标", c => Control<ComboBox>(c.Window, "CmbParamVariable").SelectedIndex),
                InputNum("默认值长度", c => TextLen(c.Window, "TxtParamDefault")),
                InputStr("配方名", c => string.IsNullOrEmpty(Control<TextBox>(c.Window, "TxtRecipeName").Text)
                    ? "(空)" : Control<TextBox>(c.Window, "TxtRecipeName").Text!),
                // ── 只进读数（本地化文案：不冻进判据）──
                Voice("参数标签", c => Control<TextBox>(c.Window, "TxtParamLabel").Text ?? "(空)"),
                Voice("窗口标题", c => c.Window.Title ?? ""),
            },

            new ClickSpec[]
            {
                // ── ① 选分组：配方列表被填（后面所有腿的前提）──
                SelMoves("GroupList", 0,
                    "`OnGroupSelectionChanged` → `_selectedGroup = NcGroupA` ＋ `RefreshRecipes()`："
                    + "把该组的**两条**配方填进列表 ⇒ 「配方项数」0 → **2**。"
                    + "⚠ 「分组下标」是**手势自己写的**，见证靠「配方项数」—— 下面每条选择腿都按这个分工读",
                    ("分组下标", "0"), ("配方项数", "2")),

                // ── ②③ 选配方：右侧编辑面板被重写（两条，证明换的是哪一条）──
                SelMoves("RecipeList", 0,
                    "`OnRecipeSelectionChanged` → `LoadRecipeToEditPanel(NcRecipeA1)`：名称/描述/启用/版本/"
                    + "时间**全部被重写**，`CmbRecipeGroup.ItemsSource` 装成组名表并选中本配方那一组，"
                    + "`RefreshParamList` 把参数填进来 ⇒ 「配方名」(空) → **NcRecipeA1**、「参数项数」0 → **2**、"
                    + "「配方分组下标」-1 → **0**",
                    ("配方下标", "0"), ("配方名", "NcRecipeA1"), ("参数项数", "2"), ("配方分组下标", "0")),

                SelMoves("RecipeList", 1,
                    "换到 `NcRecipeA2` ⇒ 面板整块重写 ⇒ 「配方名」NcRecipeA1 → **NcRecipeA2**、"
                    + "「参数项数」2 → **3**（两条配方的参数个数刻意不同：A1 两条 / A2 三条 ⇒"
                    + " 「参数表真的换了内容」多一个见证）。⚠ 两条名字长度相同，只有**名字**能把它俩分开"
                    + " —— 这正是「读处理器写的分量」的理由",
                    ("配方下标", "1"), ("配方名", "NcRecipeA2"), ("参数项数", "3")),

                // ── ④⑤ 选参数：参数编辑面板被装填（默认值长度当"换的是哪一条"的见证）──
                SelMoves("ParamList", 0,
                    "`OnParamSelectionChanged` → `LoadParamToEditPanel(压力上限)` ⇒ 「参数面板可见」0 → **1**、"
                    + "「默认值长度」0 → **3**（`120`）、「参数下标」-1 → **0**。"
                    + "⚠ 两个下拉也在这条腿上被装填（`CmbParamDataType` 选到 `Float` = 0、"
                    + "`CmbParamVariable` 因 `VariableId` 为空而**不选中**）—— 它们与初值相同 ⇒ 不进声明",
                    ("参数下标", "0"), ("参数面板可见", "1"), ("默认值长度", "3")),

                // ── ⑤ 排序（第一刀，选中项在**首**位 ⇒ 往下移）──
                Moves("BtnMoveDown",
                    "`OnMoveParam(+1)`：把选中项（`压力上限`，`SortOrder = 1`）与**后一条**的 `SortOrder` 互换 ＋"
                    + " `RefreshParamList()` ⇒ 模型侧「A2前两项」压力上限,压力下限 → **压力下限,压力上限**。"
                    + "🩸 **同一条腿量出一处产品行为**：`RefreshParamList` 里那句 `ParamList.Items.Clear()`"
                    + "会把 `_selectedParam` 清成 null（选择变更处理器走 else 支），**然后**才是 `OnMoveParam`"
                    + "末尾那段「按 `Tag` 把移动后的参数选回来」—— 它比的是刚被清成 null 的字段 ⇒"
                    + " **那三行是死代码**，一次都选不回来。于是本腿的净效果是「参数下标」0 → **-1**、"
                    + "「参数面板可见」1 → **0**（面板被关掉）。⚠ 这个性质决定了后面每条 `Move` 腿之前"
                    + "**必须**重新选一次参数 —— 下面就是这么排的",
                    ("A2前两项", "压力下限,压力上限"), ("参数下标", "-1"), ("参数面板可见", "0")),

                // ── ⑥ 重新选中（`Move` 之后列表没有选中项，得先补回来）──
                SelMoves("ParamList", 1,
                    "上一条之后列表停在 -1 ⇒ 重选下标 1（此时是 `压力上限`：两条的 `SortOrder` 刚互换过）⇒"
                    + " `LoadParamToEditPanel` 重新装填面板 ⇒ 「参数下标」-1 → **1**、「参数面板可见」0 → **1**。"
                    + "⚠ 「默认值长度」仍是 3（`120`，上一条只关了面板、没改文本）⇒ 不进声明",
                    ("参数下标", "1"), ("参数面板可见", "1")),

                // ── ⑦ 排序（第二刀，选中项在**末**位 ⇒ 往回移；两条合起来是双向见证）──
                Moves("BtnMoveUp",
                    "`OnMoveParam(-1)` 把 `压力上限` 移回首位 ⇒ 「A2前两项」回到 **压力上限,压力下限**；"
                    + "同样撞上那条死代码 ⇒ 「参数下标」1 → **-1**、「参数面板可见」1 → **0**。"
                    + "⚠ 两条 `Move` 腿一起看才说明「SortOrder 真的被互换」：单向写死只能满足其中一条",
                    ("A2前两项", "压力上限,压力下限"), ("参数下标", "-1"), ("参数面板可见", "0")),

                // ── ⑧ 再选第三条（给两个下拉腿一个"选中的参数"）──
                SelMoves("ParamList", 2,
                    "再选下标 2（`压力备用`，`SortOrder = 3`，默认值 `7`）⇒ 「参数下标」-1 → **2**、"
                    + "「参数面板可见」0 → **1**、「默认值长度」3 → **1**（`7`）。"
                    + "⚠ 第三条的**唯一作用**是给下面两条下拉腿提供一个「选中的参数」—— 没有它，"
                    + "`SyncParamEdit()` 首行 `_selectedParam == null` 就把两条腿一起挡成零信息断言",
                    ("参数下标", "2"), ("参数面板可见", "1"), ("默认值长度", "1")),

                // ── ⑨⑩ 两个参数下拉：都走 `SyncParamEdit()` 写模型 ──
                SelMoves("CmbParamDataType", 1,
                    "`CmbParamDataType.SelectionChanged += (_, _) => SyncParamEdit()` ⇒ 把下拉选中的字符串"
                    + "写进 `_selectedParam.DataType` ⇒ 模型侧「备用类型」Float → **Int16**（下标 1）。"
                    + "⚠ 同一处理器还会把面板里**其余**字段回写一遍（Label / Default / Min / Max / Unit /"
                    + " SortOrder / VariableId）—— 那些读的正是面板里的现值 ⇒ 一字不变"
                    + "（不然本层会按「未声明外溢」当场报红）",
                    ("类型下标", "1"), ("备用类型", "Int16")),

                SelMoves("CmbParamVariable", 1,
                    "`CmbParamVariable.SelectionChanged` 同走 `SyncParamEdit()`：它用**反射**取出选中项的 `Id`"
                    + "写进 `_selectedParam.VariableId`。选中第 1 个变量（夹具的 `温度`）⇒"
                    + " 模型侧「备用变量」未绑 → **已绑**、「变量下标」-1 → **1**"
                    + "（⚠ 只判「绑了」、不判绑的是哪个 `Id` —— 那是生成的 8 位随机串，冻进判据没有意义）",
                    ("变量下标", "1"), ("备用变量", "已绑")),

                // ── ⑪ 保存：唯一一条"把面板整体写回 ＋ 版本自增"的腿 ──
                Moves("BtnSaveRecipe",
                    "`OnSaveRecipe()`：读面板（名称/描述/启用/分组名）写回 `_selectedRecipe` ＋"
                    + " `UpdateRecipe(...)`（**版本自增**）＋ `RefreshGroups/RefreshRecipes` ＋"
                    + " 重新选中当前配方 ＋ `ShowStatus(...)`（写 **`Title`**）⇒ 模型侧「A2版本」1 → **2**、"
                    + "「状态条」无提示 → **已提示**。⚠ `RefreshRecipes()` 末尾那句 `ClearEditPanel()` 会把"
                    + "参数面板关掉、参数表清空，随后那次「重新选中」又把参数表填回来（**个数净不变**）"
                    + "⇒ 净效果是「参数下标」2 → **-1**、「参数面板可见」1 → **0**；`RefreshGroups()` 的"
                    + " `GroupList.Items.Clear()` 还会把**分组选中**清掉 ⇒ 「分组下标」0 → **-1**。"
                    + "本条的**前提**是 `TxtRecipeName` 非空且每条参数 `DefaultValue ∈ [Min, Max]`"
                    + " —— 夹具三条都满足",
                    ("A2版本", "2"), ("状态条", "已提示"), ("参数下标", "-1"), ("参数面板可见", "0"),
                    ("分组下标", "-1")),

                // ── ⑫ 加参数：模型 +1、列表 +1、新参数被选中 ──
                Moves("BtnAddParam",
                    "`OnAddParam()`：往 `_selectedRecipe.Items` 加一条（`SortOrder = max + 1`）＋"
                    + " `ParamList.Items.Add` ＋ 选中末项 ⇒ 模型侧「A2参数数」3 → **4**、「参数项数」3 → **4**、"
                    + "「参数下标」-1 → **3**、「参数面板可见」0 → **1**；新参数是 `Float` 且 `VariableId` 空 ⇒"
                    + " 两个下拉被**回填**：「类型下标」1 → **0**、「变量下标」1 → **-1**。"
                    + "⚠ 新参数的标签走字典（`RecipeParamDefaultName`）⇒ 只进 `Voice` 读数、不进判据；"
                    + "「A2前两项」也**不动**（新项排在末尾）—— 这正是那条探针取 `Take(2)` 的原因。"
                    + "⚠ 它能跑起来的前提是**保存腿**刚刚把 `_selectedRecipe` 重新选中过"
                    + "（`_selectedRecipe == null` 时首行就 `ShowStatus` 走人）",
                    ("A2参数数", "4"), ("参数项数", "4"), ("参数下标", "3"), ("参数面板可见", "1"),
                    ("类型下标", "0"), ("变量下标", "-1")),

                // ── ⑬ 删参数：模型 -1、列表 -1、面板关掉 ──
                Moves("BtnDeleteParam",
                    "`OnDeleteParam()`：删掉**当前选中**的那条（就是上一条刚加、被自动选中的那条）⇒"
                    + " 模型侧「A2参数数」4 → **3**、「参数项数」4 → **3**、`RefreshParamList` 后列表停在"
                    + " 「参数下标」3 → **-1**、`ParamEditPanel.IsVisible = false` ⇒ 「参数面板可见」1 → **0**。"
                    + "⚠ 「状态条」**不动**（它已经是「已提示」了）⇒ 不进声明 —— 这一格是**写入同值**那一类，"
                    + "如实不声明，免得把它当成见证",
                    ("A2参数数", "3"), ("参数项数", "3"), ("参数下标", "-1"), ("参数面板可见", "0")),

                // ── ⑭ 换分组：`RefreshRecipes` 顺带 `ClearEditPanel()`（净效果是"面板被清掉"）──
                SelMoves("GroupList", 1,
                    "换到 `NcGroupB` ⇒ `RefreshRecipes()`：配方列表 2 → **1**；它末尾那句 `ClearEditPanel()`"
                    + "把编辑面板整块清掉 ⇒ 「配方名」→ **(空)**、「配方下标」1 → **-1**、"
                    + "「参数项数」3 → **0**、`CmbRecipeGroup.ItemsSource = null` ⇒ 「配方分组下标」0 → **-1**。"
                    + "⚠ 「参数面板可见」与「参数下标」**不动**（上一条已经把它们落到 0 / -1 了）",
                    ("分组下标", "1"), ("配方项数", "1"), ("配方下标", "-1"), ("配方名", "(空)"),
                    ("参数项数", "0"), ("配方分组下标", "-1")),

                // ── ⑮ 那枚没有接线的复选框 ──
                Inert("ChkEnabled",
                    "🩸 **它根本没有接线**（全文件只有两处 `IsChecked` 的**写** ＋ `OnSaveRecipe` 里一处**读**，"
                    + "**没有任何订阅**）⇒ 翻转它不会引发任何处理器、模型一字不变。这正是 `Inert` 成色④"
                    + "「事件从未订阅」那一类（与 `AlarmConfigView.ChkAlarmEnabled` 同形）—— **不是**"
                    + "「本层看不见」，而是**接线本身不在**；要让它变成 `Moves` 得先给产品补订阅"),

                // ── ⑭–㉑ 八条 `Never`（对话框 / 平台桩 / 关窗）──
                Never("BtnAddGroup",
                    "`OnAddGroup()` → `new TextInputDialog` ＋ `ShowDialog(this)`（模态框）：本层不驱动对话框"),
                Never("BtnDeleteGroup",
                    "`OnDeleteGroup()` → `new ConfirmDialog` ＋ `ShowDialog(this)`：同上"),
                Never("BtnNewRecipe",
                    "`OnNewRecipe()` → `TextInputDialog.ShowDialog(this)`：同上（真新建发生在 `dialog.Closed` 里）"),
                Never("BtnCloneRecipe",
                    "`OnCloneRecipe()` → `TextInputDialog.ShowDialog(this)`：同上（真克隆发生在 `dialog.Closed` 里）"),
                Never("BtnDeleteRecipe",
                    "`OnDeleteRecipe()` → `ConfirmDialog.ShowDialog(this)`：同上"),
                Never("BtnImportCsv",
                    "`async void` 的 `OnImportCsv()` 走 **`StorageProvider.OpenFilePickerAsync`**（平台桩）："
                    + "headless 下不归 `Pump` 管（与 `DeviceManagerView.BtnBrowseFelImage` 同族）"),
                Never("BtnExportCsv",
                    "同上：`SaveFilePickerAsync` 平台桩"),
                Never("BtnTitleClose",
                    "`ChromeWindow` 标题栏的关闭按钮 ⇒ 点了会关掉宿主窗口（与 `UserManagerView` 同形）"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 24 个宿主：AiChatPanelView（AI 助手面板）—— 阶段 249（登记 #39 的残余面）
        //
        //  为什么挑它（阶段 247 的残余面实测：它 **8 个按钮全部无人点名**，是 §163.3 清单里最"干净"的一格）：
        //   ① **数据面明确**：`SendBtn` 往 `_messages` 里加条目、`NewChatBtn` 清空它 —— 都是处理器
        //      **亲手写**的、与界面语言无关的读数（`MessageList.Items.Count`）；
        //   ② **不用真 Provider**：`OnSendClicked` 的第一支就是 `if (!_manager.IsConfigured)` ⇒
        //      加两条消息后**同步 `return`**（本层只需要 `Pump`，不需要网络/时钟）；
        //   ③ 三个"请求"按钮只 `Invoke` 公开事件 ⇒ 走 `EventObserved`（阶段 128 的那条出口）；
        //   ④ `WindowDepot` **已经**持有 `Ai = new AiPreviewManager()`（`WindowDepot.cs:62`）⇒ **不需要新假件**。
        //
        //  两条 Inert 与一条 Never 是**如实划界**（不是"没接线"）：
        //   · `PreviewBtn` / `ApplyBtn` 的处理器首行/全分支读 `_manager.PendingScreen`，而它是
        //     `{ get; private set; }` ⇒ 本层**造不出**前置状态（要有值先得真跑一次生成）；
        //   · `StopBtn` 在 XAML 里 `IsVisible="False"`（只在真发请求时显示）＋ `_cts` 为 null。
        //
        //  ⚠ 顺序有讲究：`SendBtn` 必须排在 `NewChatBtn` **之前** —— 后者的声明是"会话条数 2 → 0"，
        //  没有前者那两条消息就什么也读不出（与 `CrossReferenceView` 那三条同形：**顺序本身是前提**）。
        // ══════════════════════════════════════════════════════════════════════════
        new("AiChatPanelView",

            d =>
            {
                var view = new Views.AiChatPanelView(d.Ai);
                return new Window { Content = view, Width = 460, Height = 700 };
            },

            new Probe[]
            {
                // 数据面：会话列表条数（`SendBtn` 加、`NewChatBtn` 清）
                Num("会话条数", c => Items(c.Window, "MessageList")),
                // 输入面：输入框长度（`SendBtn` 的布景落点；处理器点完会清空它 ⇒ 也是要声明的分量）
                InputNum("输入长度", c => TextLen(c.Window, "InputBox")),
                // 事件面：三个请求 —— 处理器只 `Invoke` 公开事件，本层只能订阅观测
                EventObserved("预览请求", (c, rec) =>
                    ((Views.AiChatPanelView)c.Window.Content!).PreviewRequested += (_, s) => rec(s?.Name ?? "(null)")),
                EventObserved("应用请求", (c, rec) =>
                    ((Views.AiChatPanelView)c.Window.Content!).ApplyRequested += (_, s) => rec(s?.Name ?? "(null)")),
                EventObserved("取消请求", (c, rec) =>
                    ((Views.AiChatPanelView)c.Window.Content!).CancelRequested += (_, _) => rec("已抬")),
                // 配置变更：只记**模型名**（布景给的就是它）—— 不记 Provider，免得钉在配置默认值上
                EventObserved("配置变更模型", (c, rec) =>
                    ((Views.AiChatPanelView)c.Window.Content!).ConfigChanged += (_, cfg) => rec(cfg.Model)),
                // 只进读数：配置状态文案（走字典，不冻文案）
                Voice("配置状态", c => Control<TextBlock>(c.Window, "ConfigStatusText").Text ?? ""),
            },

            new ClickSpec[]
            {
                // ── ① 发送：未配置管理器 ⇒ 走本地分支（加两条消息）后同步 return ──
                Arranged(Moves("SendBtn",
                    "ctor 里 `SendBtn.Click += OnSendClicked`（⚠ **不是** XAML 属性）：首行 `string.IsNullOrEmpty(input)` "
                    + "⇒ 布景往 `InputBox` 填「液位报警」；再往下 `if (!_manager.IsConfigured)` 这一支"
                    + "**不发网络请求**，只 `_messages.Add(用户条目)` ＋ `_messages.Add(「请先配置」助手条目)` 然后 `return`，"
                    + "`finally` 再把输入框清空、`_busy = false` ⇒ **会话条数 0 → 2、输入长度 5 → 0**。"
                    + "⚠ 第二条是**处理器自己写的**（不是手势）：不声明它就是未声明分量外溢。",
                    ("会话条数", "2"), ("输入长度", "0")),
                    c => Control<TextBox>(c.Window, "InputBox").Text = "液位报警"),

                // ── ② 新会话：清空消息集合（顺序前提见上面的大注释）──
                Moves("NewChatBtn",
                    "XAML 里 `Click=\"OnNewChatClicked\"` ⇒ `_messages.Clear()`（同时 `CancelInFlight()` 是空操作、"
                    + "`ActionBar.IsVisible = false`）⇒ **会话条数 2 → 0**。"
                    + "⚠ 它排在 `SendBtn` 之后：没有那两条消息，这条声明读不出任何变化（顺序即前提）。"
                    + "🩸 该按钮的接线**只写在 .axaml 属性里**，而代码里明写「不得再 `+=`」—— 重复订阅是缺陷 `#43` 的形状。",
                    ("会话条数", "0")),

                // ── ③ 取消：唯一**无前置条件**的请求按钮 ──
                Moves("CancelBtn",
                    "ctor 里 `CancelBtn.Click += (_, _) => RequestCancel()` ⇒ `CancelRequested?.Invoke` ＋ "
                    + "`ClearPendingState()`。**首行没有任何前置判空**（对比 Preview/Apply 那两条）⇒ "
                    + "裸夹具里也能真抬事件。⚠ 声明的是**视图的公开事件**；构造期往静态总线的转发本层判不到"
                    + "（见 `EventObserved` 的诚实边界）。",
                    ("取消请求", "已抬")),

                // ── ④ 保存配置：写管理器配置 ＋ 抬 ConfigChanged ──
                Arranged(Moves("SaveConfigBtn",
                    "ctor 里 `SaveConfigBtn.Click += OnSaveConfigClicked`：从四个输入框拼 `LlmProviderConfig` ⇒ "
                    + "`_manager.UpdateConfig(config)`。**ApiKey 留空 ⇒ `IsConfigured == false` ⇒ 走「未配置」那一支"
                    + "直接 `return true`（不落盘）** ⇒ `ConfigChanged?.Invoke` ⇒ 观测到模型名。"
                    + "⚠ 布景只填 `ModelBox`（输入面）；`Provider` 取 `ProviderBox.SelectedIndex` 的当前值"
                    + "（ctor 里 `LoadConfigIntoUi` 按 `config.Provider` 拨好）—— 本层**不**断言它，"
                    + "免得把这条声明钉在配置默认值上。",
                    ("配置变更模型", "m1")),
                    c => Control<TextBox>(c.Window, "ModelBox").Text = "m1"),

                // ── ⑤⑥ 两个"提前 return"档（如实划界，不是没接线）──
                Inert("PreviewBtn",
                    "ctor 里 `PreviewBtn.Click += (_, _) => RequestPreview()`，而 `RequestPreview()` **首行**是 "
                    + "`if (_manager.PendingScreen is not { } screen) return;` ⇒ 未配置/无待预览时**提前 return**。"
                    + "⚠ 本层**造不出**前置状态：`AiPreviewManager.PendingScreen` 是 "
                    + "`public HmiScreen? { get; private set; }`（没有任何公开 setter），要有值必须先真跑一次生成"
                    + "（需 Provider）⇒ 这一档记的是**夹具形状**，不是接线（`Bucket.Inert` 的「提前 return」成色）。"),
                Inert("ApplyBtn",
                    "ctor 里 `ApplyBtn.Click += (_, _) => RequestApply()`，而 `RequestApply()` 的三个分支"
                    + "（`PendingScreen` / `PendingTemplate` / `PendingScript`）**全不满足** ⇒ 一个事件都不抬。"
                    + "判定与理由同 `PreviewBtn`：前置状态只能由真生成造出来。"),

                // ── ⑦ 纯控件态：代码里根本没有处理器 ──
                Inert("AgentModeToggle",
                    "**代码里没有任何处理器**：`AgentModeToggle` 只在 `OnSendClicked` 里被**读**一次"
                    + "（`if (AgentModeToggle.IsChecked == true)`），没有任何 `Click` / `IsCheckedChanged` 订阅。"
                    + "`Gesture` 会显式翻转它的 `IsChecked`（`ToggleButton` 那一支）⇒ 变的是**控件自身状态**，"
                    + "而本层**不探**这个字段 ⇒ 数据面/事件面一字不变。这是「未被观测」成色，**不是**「没接线」。"),

                // ── ⑧ 不点：隐藏 ＋ 无在途 ──
                Never("StopBtn",
                    "XAML 里 `IsVisible=\"False\"`（只有真发请求时 `StopBtn.IsVisible = true`），而 `_cts` 为 null "
                    + "⇒ `OnStopClicked` → `CancelInFlight()` 是**空操作**。要造在途请求得先过一个**真 Provider**"
                    + "（`_manager.IsConfigured`）⇒ 本层不点，如实登记。"),
            }),

        // ══════════════════════════════════════════════════════════════════════════
        //  第 25 个宿主：AuditLogView（审计日志面板）—— 阶段 251（登记 #39 的残余面）
        //
        //  为什么挑它（阶段 251 把 12 个停靠面板逐个过了前缀）：第四层（`DockPanelInvocationUiTests`）
        //  的面**按 `Btn*` 前缀筛**（`BtnNamesOf` 里那句 `StartsWith("Btn")`）⇒ 本视图的 4 个按钮
        //  （`ExportReportBtn` / `ExportBundleBtn` / `DesignerBtn` / `RefreshBtn`）**一个都不在第四层的面内**，
        //  而它又不在第五层的宿主表里 ⇒ **两层都够不到**（同族清单与收口见 §167）。
        //
        //  ⚠ 顺序有讲究：`ExportBundleBtn` 必须排在 `RefreshBtn` **之前** —— 后者的布景会往服务里放一条
        //  审计，列表就不再为空，前者就会走到**文件夹选择器**（平台桩）那条路上；先点它则命中
        //  `SaveBundleAsync` 的 `rowCount == 0` 提前返回，读数是干净的。
        // ══════════════════════════════════════════════════════════════════════════
        new("AuditLogView",

            d =>
            {
                var view = new Views.AuditLogView();
                view.Attach(d.Auth);      // 生产路径：Dock 装配时注入认证服务（MainWindow.axaml.cs:262）
                return new Window { Content = view, Width = 900, Height = 600 };
            },

            new Probe[]
            {
                // 数据面：审计列表条数（`Refresh()` 按服务里的日志重建它）
                Num("日志条数", c => Items(c.Window, "AuditList")),
                // 输入面：筛选框长度（本宿主不布景它，但它是这张面唯一的输入口）
                InputNum("筛选长度", c => TextLen(c.Window, "FilterBox")),
                // 只进读数：筛选框原文（用户输入，不冻进断言）
                Voice("筛选文本", c => Control<TextBox>(c.Window, "FilterBox").Text ?? ""),
            },

            new ClickSpec[]
            {
                // ── ① 导出报表包：初始日志为空 ⇒ 提前 return（连平台桩都不碰）──
                Inert("ExportBundleBtn",
                    "ctor 里 `ExportBundleBtn.Click += OnExportBundle` ⇒ `ReportExport.SaveBundleAsync(...)`，"
                    + "而它的**首行**是 `if (rowCount == 0) return emptyMessage;` —— 初始日志为空 ⇒ **不碰"
                    + "文件夹选择器**就返回；`message` 走 `UiOperationGuard.UiSink`（静态出口，本层不探）⇒ 无分量变化。"
                    + "⚠ 它排在 `RefreshBtn` 之前正是为这个：排在后面时列表已非空，这条路会走到 "
                    + "`OpenFolderPickerAsync`（headless 平台桩）—— 那时它记的就不是同一件事了。"),

                // ── ② 刷新：把服务里的日志重建到列表 ──
                Arranged(Moves("RefreshBtn",
                    "ctor 里 `RefreshBtn.Click += (_, _) => Refresh()`，而 `Refresh()` 首行是 "
                    + "`if (_auth == null) return;`（本宿主已 `Attach(d.Auth)`）⇒ 读 `_auth.GetAuditLog(1000)` "
                    + "重建 `_entries` ⇒ **日志条数 0 → 1**。布景往沙箱里写一条 JSONL 再 "
                    + "`LoadAuditLogFromFile` —— 那条 API **只进内存、不抬 `AuditLogged`**（面板不会自己先加一条 ⇒ "
                    + "布景没有替处理器做事，这条正是本层那条布景自检要盯的形状）。"
                    + "⚠ 若改用 `RecordAudit` 布景：事件会让面板经 `Dispatcher.Post(AddEntry)` 当场自己加进去 ⇒ "
                    + "那才是「把效果搬进布景」，本层会红。",
                    ("日志条数", "1")),
                    c =>
                    {
                        try
                        {
                            var path = System.IO.Path.Combine(c.Depot.DataRoot, "audit-seed.jsonl");
                            AuditLogStore.Append(path, new AuditLogEntry
                            {
                                Id = "seed-phase251", Username = "布景用户",
                                Action = AuditAction.Login, Detail = "布景条目", Success = true,
                            });
                            c.Depot.Auth.LoadAuditLogFromFile(path);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"布景（写审计 JSONL ＋ 载入内存）失败：{ex.Message}", ex);
                        }
                    }),

                // ── ③④ 两条不点：都会弹**模态**对话框并 await 用户输入 ──
                Never("ExportReportBtn",
                    "`OnExportReport` ⇒ `ReportExport.ChooseRangeAsync(this)`：**弹范围选择对话框**并 `await` 用户输入。"
                    + "本层只 `Pump`、不驱动模态交互 ⇒ 点了会给宿主挂一个**未关闭的模态窗**，"
                    + "后续按钮与收尾都被它挡住（与 `RecipeEditorView` 那几条 `Never` 同一档）。"),
                Never("DesignerBtn",
                    "`OnEditReportTemplate` ⇒ `ReportExport.EditTemplateAsync(...)`：弹**报表设计器**对话框，"
                    + "同上（模态 ＋ 等用户）。"),
            }),
    };

    /// <summary>
    /// 给 <see cref="Views.DesignCanvasView"/> 的夹具铺一个**能被指针点中**的控件（阶段 144）。
    ///
    /// <para>为什么必须铺：`WindowDepot` 原有那一个控件（`状态灯`）**没给宽高** ⇒ 默认 0×0 ⇒
    /// `DesignCanvas.HitTestControl` 的命中矩形也是 0×0，指针打上去永远落空（那条腿会变成
    /// "点了但什么都没发生"的零信息断言）。</para>
    ///
    /// <para>位置/尺寸取整（40,40 / 120×60）：指针打 (60,60) —— 稳稳落在矩形内，
    /// 且离边框与手柄都有余量（`HitTestHandle` 只对单选显示的手柄生效，本阶段不碰它）。</para>
    /// </summary>
    private static void SeedCanvasFixture(WindowDepot depot)
    {
        depot.Screen.Controls.Add(new HmiControl
        {
            Name = "NcCanvasTarget",
            Type = HmiControlType.Button,
            X = 40,
            Y = 40,
            Width = 120,
            Height = 60,
        });
    }

    /// <summary>
    /// 给 <see cref="Views.ScriptEditorView"/> 的夹具补**第二条脚本**（阶段 157）。
    ///
    /// <para>为什么必须补：`WindowDepot` 只往 <c>Scripts</c> 里塞了一条（`开机脚本`）——
    /// 只有一条时「选中第 0 条」与「选中第 1 条」在读数上分不出来，筛选腿也无从判起。</para>
    ///
    /// <para>**分类刻意与第 0 条不同**（`Alarm` vs 默认的 `OnClick`）：筛选腿靠这个差别
    /// 把列表从 2 条筛到 0 条；两条同类的话「筛了」与「没筛」在读数上同形。</para>
    ///
    /// <para>⚠ 代码刻意**留空**（`HmiScript.Code` 的默认值）：四条模板腿的期望长度
    /// （287 / 572 / 807 / 1076）全靠"起点是空串 ＋ `CaretIndex` 从 0 开始"才成立。</para>
    /// </summary>
    private static void SeedScriptFixture(WindowDepot depot)
    {
        // 第 0 条是 `WindowDepot` 自带的「开机脚本」：补一条描述，让"选中它"也有**处理器写的**见证
        var first = depot.Scripts.First(s => s.Name == "开机脚本");
        first.Description = "开机初始化";        // 5 字

        depot.Scripts.Add(new HmiScript
        {
            Name = "报警联动脚本",
            Category = "Alarm",
            Description = "报警联动用的脚本",   // 8 字：给"换一条 ⇒ 面板被重写"当**非本地化**的见证
        });
    }

    /// <summary>按**名字**取夹具里的脚本（不按下标 —— 删除腿之后下标会整体挪位）。</summary>
    private static HmiScript? ScriptByName(HostContext ctx, string name)        => ctx.Depot.Scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// `ValidationDot` 的填充色 → 人可读状态（阶段 157）。
    ///
    /// <para>**刻意不读文案**：校验文案走字典（zh/en 两份），把它冻进判据等于让判据依赖语言；
    /// 而"校验状态变了没有"这件事由**调色板常量**回答，与语言无关。三种判定色和两种灰
    /// （`ValidationIdle` 与 XAML 里那个 `HmiTextHintBrush` 初值）都在这里归位。</para>
    /// </summary>
    private static string ScriptValidationDot(Window window)
    {
        var fill = Control<Avalonia.Controls.Shapes.Ellipse>(window, "ValidationDot").Fill
                   as Avalonia.Media.SolidColorBrush;
        var color = fill?.Color;
        if (color == UiPalette.ValidationError) return "红";
        if (color == UiPalette.ValidationWarn) return "黄";
        if (color == UiPalette.ValidationOk) return "绿";
        // 两种"还没有判定"的灰：UiPalette.ValidationIdle 与 App.axaml 的 HmiTextHintBrush（#7C8AA0）
        if (color == UiPalette.ValidationIdle) return "灰";
        if (color == Avalonia.Media.Color.FromRgb(0x7C, 0x8A, 0xA0)) return "灰";
        return "其它";
    }

    /// <summary>
    /// 给 <see cref="Views.RecipeEditorView"/> 的夹具铺 **2 个分组 / 3 条配方**（阶段 158）。
    ///
    /// <para>为什么必须铺：`WindowDepot.Recipes` 是**空**的（`new RecipeManager()`）—— 不铺的话
    /// 三个列表全是 0 行，「选分组 / 选配方 / 选参数」那几条腿都会退化成零信息断言。</para>
    ///
    /// <para>**A2 的两条参数刻意"个数相同、默认值长度不同"**（`120` vs `5`）：换参数那条腿需要
    /// 一个**处理器写的**见证，「默认值长度」就是它 —— 只读列表下标的话，手势自己写的那一格
    /// 会把"处理器没跑"与"跑了"读成同一个样子。</para>
    ///
    /// <para>⚠ 名字全用 ASCII：`GetAllGroups()` / `GetRecipesByGroup()` 都按 `Name` 排序，
    /// 而 `OrderBy` 用的是**当前区域性**比较器 ⇒ 中文名在不同 locale 下顺序可能不同。</para>
    ///
    /// <para>⚠ A2 每条参数的 `DefaultValue` 都落在默认的 `[Min, Max]`（±∞）里 ⇒ `BtnSaveRecipe`
    /// 那段"默认值越界才拦"的校验不会挡住保存。</para>
    /// </summary>
    private static void SeedRecipeFixture(WindowDepot depot)
    {
        depot.Recipes.AddGroup("NcGroupA");
        depot.Recipes.AddGroup("NcGroupB");

        depot.Recipes.AddRecipe(new Recipe
        {
            Name = "NcRecipeA1",
            GroupName = "NcGroupA",
            Description = "A1 的口径",
            Items =
            {
                new RecipeItem { Label = "温度上限", DefaultValue = 80, SortOrder = 1 },
                new RecipeItem { Label = "保温时长", DefaultValue = 30, SortOrder = 2, DataType = "Int16" },
            },
        });
        depot.Recipes.AddRecipe(new Recipe
        {
            Name = "NcRecipeA2",
            GroupName = "NcGroupA",
            Description = "A2 的口径",
            Items =
            {
                new RecipeItem { Label = "压力上限", DefaultValue = 120, SortOrder = 1 },
                new RecipeItem { Label = "压力下限", DefaultValue = 5, SortOrder = 2 },
                // 第三条的**唯一作用**是给「两个下拉」那两条腿提供一个可被选中的参数
                // （`Move` 腿撞上死代码之后列表会停在 -1，而 ParamList 的每个下标只能用一次）
                new RecipeItem { Label = "压力备用", DefaultValue = 7, SortOrder = 3 },
            },
        });
        depot.Recipes.AddRecipe(new Recipe
        {
            Name = "NcRecipeB1",
            GroupName = "NcGroupB",
            Items = { new RecipeItem { Label = "锅炉温度", DefaultValue = 100, SortOrder = 1 } },
        });
    }

    /// <summary>按**名字**取夹具里的配方（保存腿会改版本号，但名字在本表里不变）。</summary>
    private static Recipe RecipeByName(HostContext ctx, string name)
        => ctx.Depot.Recipes.GetAllRecipes().First(r => r.Name == name);

    /// <summary>按**名字**取某条配方里的参数条目。</summary>
    private static RecipeItem ItemByLabel(HostContext ctx, string recipeName, string label)
        => RecipeByName(ctx, recipeName).Items.First(i => i.Label == label);

    /// <summary>
    /// 给 <see cref="Views.TagMonitorView"/> 的夹具铺 **2 个带绑定的控件** ＋ 桩值源（阶段 142）。
    ///
    /// <para>为什么必须铺：`RuntimeBindingEngine.Tags` 来自**扫描项目控件的绑定**
    /// （`BuildRuntimeTags()`）—— `WindowDepot` 原有那 1 个控件**没有绑定** ⇒ 不铺这一层，
    /// 标签表就是空的，四条声明全成「0 → 0」的零信息断言。</para>
    ///
    /// <para>两条绑定的**方向刻意不同**（`液位` 可写 / `温度` 只读）：方向筛选那两条腿（2 → 1 → 2）
    /// 与写入目标（只收非只读 ⇒ 恰 1 项）都靠这个差别。</para>
    ///
    /// <para>桩值源让 `WriteValue` **真的成功**（产品里真源未连接时它会 return false）——
    /// 于是 `BtnWrite` 那条腿读的是**模型侧**的「引擎值」，而不是界面文案。</para>
    /// </summary>
    private static void SeedTagFixture(WindowDepot depot)
    {
        // ⚠ 变量必须挂在 **`Project.VariableManager`** 上（不是 `depot.Variables`）：
        //   `RuntimeBindingEngine.ResolveVariableTag` 走的是 `_project.VariableManager.ResolveBinding(…)`，
        //   解析不到就**静默不建标签** —— 首跑实测「标签数 0 → 0」就是踩在这里（`depot.Variables`
        //   是另一个 VariableManager，引擎看不见它）。
        var level = new Variable { Name = "NcTagLevel", Address = "D300", DataType = "Int16" };
        var temp = new Variable { Name = "NcTagTemp", Address = "D302", DataType = "Float32" };
        depot.Project.VariableManager.Variables.Add(level);
        depot.Project.VariableManager.Variables.Add(temp);
        var levelId = level.Id;   // 可写
        var tempId = temp.Id;     // 只读

        depot.Project.Screens[0].Controls.Add(new HmiControl
        {
            Name = "NcLevelLamp",
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    VariableId = levelId, PropertyName = "Text", Direction = BindingDirection.ReadWrite,
                },
            },
        });
        depot.Project.Screens[0].Controls.Add(new HmiControl
        {
            Name = "NcTempGauge",
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    VariableId = tempId, PropertyName = "Text", Direction = BindingDirection.ReadOnly,
                },
            },
        });

        depot.BindingEngine.ValueSource = new NcTagValueSource(levelId, tempId);
        depot.BindingEngine.BuildRuntimeTags();
    }

    /// <summary>
    /// 桩值源（阶段 142）：让 `RuntimeBindingEngine.ReadValue` / `WriteValue` 在测试里有确定行为。
    /// 初值 液位=10 / 温度=20；`WriteValue` 写进来之后 `ReadValue` 读得到（`BtnWrite` 那条腿判的就是它）。
    /// </summary>
    private sealed class NcTagValueSource : IRuntimeValueSource
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

        public NcTagValueSource(string levelId, string tempId)
        {
            _values[levelId] = 10;
            _values[tempId] = 20;
        }

        public bool IsConnected => true;

        public string Name => "nc-tag-source";

        public bool TryRead(string variableId, out double value) => _values.TryGetValue(variableId, out value);

        public bool TryWrite(string variableId, double value)
        {
            _values[variableId] = value;
            return true;
        }
    }

    /// <summary>
    /// 给 <see cref="Views.AssetManagerView"/> 的夹具铺 **2 张图 ＋ 1 个字体**（阶段 132）。
    ///
    /// <para>为什么必须铺：<see cref="WindowDepot.Assets"/> 原本是**空的** ⇒ 不铺这一层，
    /// 列表里连一行都没有，三条选择声明全成「null → null」的**零信息**断言。</para>
    ///
    /// <para>名字取 ASCII、顺序可预期：列表是「按 <c>AssetType</c> 分组 ＋ 组内 <c>OrderBy(Name)</c>」，
    /// 而 <c>AssetType</c> 的枚举序是 Image, Font, Language, Svg ⇒
    /// <c>[Image 标题, NcAlpha.png, NcBravo.png, Font 标题, NcZeta.ttf]</c> —— 下标就是身份。</para>
    /// </summary>
    private static void SeedAssetFixture(WindowDepot depot)
    {
        depot.Assets.AddAsset(new AssetItem { Name = "NcAlpha.png", Type = AssetType.Image, FilePath = "nc/alpha.png", SizeBytes = 1024, Width = 16, Height = 16, ColorDepth = 32 });
        depot.Assets.AddAsset(new AssetItem { Name = "NcBravo.png", Type = AssetType.Image, FilePath = "nc/bravo.png", SizeBytes = 2048, Width = 32, Height = 32, ColorDepth = 32 });
        depot.Assets.AddAsset(new AssetItem { Name = "NcZeta.ttf", Type = AssetType.Font, FilePath = "nc/zeta.ttf", SizeBytes = 4096 });
    }

    /// <summary>
    /// 给 <see cref="Views.Editors.VariablePicker"/> 的夹具加两条 **ASCII 名**变量（阶段 131）。
    ///
    /// <para>为什么非要加：本视图的树按 <c>OrderBy(v =&gt; v.Name)</c> 排序，而
    /// <c>Comparer&lt;string&gt;.Default</c> 是**当前文化**比较器 ⇒ 夹具原有那两条中文名谁在前**依赖文化**。
    /// 加两条 ASCII 名之后，它们在**任何**比较器下都排在中文名之前 ⇒ 路径 <c>[0,0]</c>/<c>[0,1]</c>
    /// 指向哪一条是**确定**的（真换了文化，断言会红而不是静默错位）。</para>
    ///
    /// <para>⚠ 只动 <c>depot.Variables</c>（本视图的输入），不动 <c>depot.Project</c> ——
    /// 每个宿主各拿一个全新的 <see cref="WindowDepot"/>，但仍然刻意不越界改别人的夹具面。</para>
    /// </summary>
    private static void SeedPickerVariables(WindowDepot depot)
    {
        const string groupId = "nc-probe";
        depot.Variables.Groups.Add(new VariableGroup { Id = groupId, Name = "NcProbe" });
        depot.Variables.Variables.Add(new Variable { Name = "NcAlpha", Address = "D200", DataType = "Int16", GroupId = groupId });
        depot.Variables.Variables.Add(new Variable { Name = "NcBravo", Address = "D202", DataType = "Int16", GroupId = groupId });
    }

    /// <summary>
    /// 给 <see cref="Views.CrossReferenceView"/> 的夹具铺**可被引用的**数据（阶段 126）。
    ///
    /// <para><b>为什么必须铺</b>：<c>Search()</c> 走 <c>CrossReferenceAnalyzer</c>，而它读的是
    /// <c>project.VariableManager.Variables</c>（做"名字 ↔ Id"解析）与
    /// <c>screen.Controls[].VariableBindings</c>（真正的引用点）。而 <see cref="WindowDepot"/>
    /// 的两条变量只加在**另一本**管理器（<c>depot.Variables</c>）里、屏幕上的控件**一条绑定都没有**
    /// ⇒ 不铺这一层，任何关键词都返回 0 条，三条声明全成「0 → 0」的**零信息**断言
    /// （与 `SeedAlarmRecords` 同一条理由：不造数，声明恒真）。</para>
    ///
    /// <para>⚠ **只动 <c>depot.Project</c>、不动 <c>depot.Variables</c>**：前者是本宿主的输入，
    /// 后者是别的宿主（`VariableManagerView` 等）的读数 —— 每个宿主各拿一个全新的
    /// <see cref="WindowDepot"/>，但仍然刻意不越界改别人的夹具面。</para>
    ///
    /// <para>铺出来的是**恰好 2 条**引用：两条控件各绑一条「液位」，且**都不带动作**
    /// （动作也算引用点，带上就把条数抬成别的数）。</para>
    /// </summary>
    private static void SeedCrossReferenceFixture(WindowDepot depot)
    {
        var project = depot.Project;
        var variable = new Variable { Name = "液位", Address = "D100", DataType = "Int16" };
        project.VariableManager.Variables.Add(variable);

        var screen = project.Screens[0];
        foreach (var (name, type) in new[] { ("液位显示", HmiControlType.Label), ("液位条", HmiControlType.Bar) })
        {
            var control = new HmiControl { Name = name, Type = type, X = 10, Y = 40 };
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = "Value",
                VariableId = variable.Id,
            });
            screen.Controls.Add(control);
        }
    }

    /// <summary>
    /// 给 <see cref="Views.AlarmListView"/> 的夹具**确定性地**造三条记录
    /// （两条 <c>Active</c> ＋ 一条 <c>Returned</c>）。
    ///
    /// <para><b>为什么不能只 <c>RegisterAlarm</c>：</b>记录是**引擎的定时器**造的 ——
    /// <c>UpdateVariableValues</c> 只写字典、**不触发评估**。所以要等产品状态达标，
    /// 而**不能用固定 <c>Sleep</c>**（那是「间歇红」的经典来源）。这里照
    /// <c>AlarmEngineTests</c> 的纪律：设小评估间隔 ＋ 轮询产品状态，超时即抛。</para>
    ///
    /// <para>🩸 <b>造数顺序是判据的一部分，不是随手写的：</b>列表按 <c>OccurredAt</c> 降序，
    /// 于是**最后造的那条排在最前**。<c>BtnAcknowledge</c> 只确认列表的**选中项**，
    /// 所以必须让一条 <c>Active</c> 的记录落到 <c>index 0</c> —— 否则确认会静默失败
    /// （<c>AcknowledgeAlarm</c> 只对 <c>State == Active</c> 返回 <c>true</c>），
    /// 那条声明就成了 flaky。因此顺序是：先造 Critical 并把它压回 <c>Returned</c>，
    /// 再造 Info、最后造 Warning（它排到最前）。</para>
    ///
    /// <para>⚠ 造完就 <c>Stop()</c>：定时器继续跑只会给判据多引入一个变量 —— 它会在两次
    /// <c>Pump</c> 之间按自己的节奏发事件、把 <c>RefreshData</c> 排进 UI 队列。
    /// 本层的四条点击都不依赖周期评估（确认/清除都是对 <c>_activeRecords</c> 的直接操作）。</para>
    /// </summary>
    private static void SeedAlarmRecords(WindowDepot depot)
    {
        var engine = depot.Alarms;
        engine.EvaluationIntervalMs = 50;

        void Register(string id, string name, AlarmSeverity severity, string variableId, bool requiresAck)
            => engine.RegisterAlarm(new AlarmConfig
            {
                Id = id,
                Name = name,
                Severity = severity,
                RequiresAck = requiresAck,
                Condition = new AlarmCondition
                {
                    VariableId = variableId,
                    Type = AlarmConditionType.HighLimit,
                    Threshold = 50.0,
                },
            });

        Register("seed-crit", "压力高高", AlarmSeverity.Critical, "seed-v3", requiresAck: false);
        Register("seed-info", "液位高", AlarmSeverity.Info, "seed-v1", requiresAck: true);
        Register("seed-warn", "温度高", AlarmSeverity.Warning, "seed-v2", requiresAck: true);

        // ① Critical 先触发、再解除 ⇒ 造出**唯一**一条 Returned
        engine.UpdateVariableValue("seed-v3", 100.0);
        engine.Start();
        WaitUntil(() => engine.GetAllRecords().Count == 1, "seed-v3 超阈值后应产生第 1 条记录");
        engine.UpdateVariableValue("seed-v3", 0.0);
        WaitUntil(() => engine.GetAllRecords().Count(r => r.State == AlarmState.Returned) == 1,
            "seed-v3 回落且 RequiresAck=false ⇒ 那条应转成 Returned");

        // ② 再依次造两条 Active —— 顺序决定它们在列表里的先后（见本方法 xmldoc）
        engine.UpdateVariableValue("seed-v1", 100.0);
        WaitUntil(() => engine.GetAllRecords().Count == 2, "seed-v1 超阈值后应有 2 条记录");
        engine.UpdateVariableValue("seed-v2", 100.0);
        WaitUntil(() => engine.GetAllRecords().Count == 3, "seed-v2 超阈值后应有 3 条记录");

        engine.Stop();
    }

    /// <summary>
    /// 等产品状态达标。🩸 **超时即抛** —— 不把「没等到」静默变成「读数恰好如此」：
    /// 那会让声明在夹具失灵时照样绿（本仓纪律：读不到就要报，不能回落成默认值）。
    /// </summary>
    private static void WaitUntil(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return;
            System.Threading.Thread.Sleep(10);
        }

        throw new InvalidOperationException($"等待 {timeoutMs} ms 仍未达标：{because}");
    }

    public static IEnumerable<object[]> HostKeys() => Hosts.Select(h => new object[] { h.HostTypeName });

    /// <summary>
    /// **给静态守卫用的唯一声明出口**（阶段 115）：列出 (宿主类型名, 选择族控件名) 的**全部声明**。
    ///
    /// <para>为什么要开一个出口，而不是让守卫自己去读本文件：守卫与被守对象必须**同源**
    /// （判据六条铁律之一）。让守卫去扫本文件里的 <c>SelMoves("X"</c> / <c>SelInert("X"</c> 字面量，
    /// 等于把「声明是什么」抄成两份 —— 表一改两份就对不上，而错的会是守卫那一份。
    /// 🩸 本阶段实测到了这个坑的**具体形状**：本方法的 xmldoc 里那句 <c>SelMoves("X"</c>
    /// 会让「按字面量计数」多出 **1 条**（阶段 115 那次是 49 → 50）—— 注释把计数喂饱了。</para>
    ///
    /// <para>⚠ 返回的是**带重复的声明列表**（同一控件可以被驱动多次，唯一性键是
    /// <c>(名字, 手势下标)</c>），**不是**去重后的集合。两个口径都真实存在，别把它们混成一个数：
    /// **阶段 134 重数**是 **72 条声明**，而「按宿主去重」是 **58 个控件**、
    /// 「全仓按名字去重」是 **55 个**（<c>AlarmList</c> / <c>GroupList</c> 等跨宿主同名）。
    /// 面是**按宿主**算的 ⇒ 与静态面对齐的口径是「按宿主去重」那个 **58**。
    /// 🩸 这里上一版写的是阶段 118 的 **53 / 47 / 44** —— 本轮 stash 回 HEAD 复测是 **57 / 51 / 48**，
    /// 即三个口径**同时漂了 +4**（阶段 120 补的 3 枚下拉 ＋ 阶段 124 的 <c>DetailRoleCombo</c>）；
    /// 阶段 126 加第 10 个宿主后是 **59 / 52 / 49**、阶段 127 加第 11 个后是 **61 / 53 / 50**、
    /// 阶段 128 加第 12 个后是 **62 / 54 / 51**、阶段 129 加第 13 个后是 **64 / 55 / 52**、
    /// 阶段 131 加第 14 个（子目录宿主）后是 **66 / 56 / 53**、
    /// 阶段 132 加第 15 个后是 **69 / 57 / 54**、阶段 133 加第 16 个后是 **71 / 58 / 55**、
    /// 阶段 134 补上「抬总线」的腿后是 **72 / 58 / 55**（只多一条声明：`ProjectExplorerView` 的配置支）、
    /// 阶段 136 加第 17 个（`AnimationEditorView`）后是 **81 / 66 / 63**、
    /// 阶段 142 加第 18 个（`TagMonitorView`）后是 **83 / 67 / 64**、
    /// 阶段 144 加第 19 个（`DesignCanvasView`，**指针序列手势**）后是 **84 / 68 / 65**、
    /// 阶段 149 加第 20 个（`SimulatorView`）后是 **92 / 73 / 69**、
    /// 阶段 150 加第 21 个（`DeviceManagerView`）后是 **94 / 74 / 69**
    /// （⚠ 名字数**没变**：`CmbChannelType` 与别的视图重名 —— 这正是「按宿主去重」与「按名字去重」两个口径的差别）
    /// （＋9 / ＋8 / ＋8 —— 它的 8 个选择控件**全是新名字**，与既有 16 个宿主无一重名；
    /// ⚠ 重数方法：一个只数 `SelMoves("X"` / `SelInert("X"` / `SelMovesPath("X"` **声明形态**的脚本，
    /// 且先在 `git show HEAD:` 的那一版上复现出 **72 / 58 / 55** 才采信 —— 仪器先自检再读数）、
    /// 阶段 157 加第 22 个（`ScriptEditorView`，**第一个"自己就是 `Window`"的宿主**）后是
    /// **99 / 77 / 72**（＋5 / ＋3 / ＋3 —— 5 条声明落在 3 个控件上：`ScriptList` 驱动两次、
    /// `CmbCategoryFilter` 两次、`CboCategory` 一次；三个名字与既有 21 个宿主**都不重名**
    /// ⇒ 与阶段 150 的「＋8 个控件而名字数不动」正好形成对照）、
    /// 阶段 158 加第 23 个（`RecipeEditorView`，**面外最后一块真面**）后是 **108 / 82 / 76**
    /// （＋9 / ＋5 / ＋4 —— 9 条声明落在 5 个控件上：`ParamList` 驱动**三次**、`GroupList` 两次、
    /// `RecipeList` 两次、两个参数下拉各一次；**＋4 而不是 ＋5** 是因为 `GroupList` 这个名字
    /// 在别的宿主里已经声明过 ⇒ 又一次「三个口径各差一点」的活例子）、
    /// 阶段 249 加第 24 个（`AiChatPanelView`，**纯按钮族、零选择族声明**）后仍是 **108 / 82 / 76**
    /// —— 三个口径**一个都没动**：它那 8 条声明全是按钮（4 Moves / 3 Inert / 1 Never），
    /// 走运行期双向差集那条面，不进 <see cref="SelectDeclarations"/>。
    /// 它们**没有任何判据看着**（纯散文）⇒ 只能靠"动面就重数"。</para>
    ///
    /// <para>⚠ 它只导出**选择族**（<c>SelectIndex is not null</c> **或** <c>SelectPath is not null</c>）
    /// 那一半：按钮族的面在运行期
    /// 是双向差集（<see cref="CollectFace"/>），不需要静态守卫搭手。</para>
    /// </summary>
    internal static IReadOnlyList<(string Host, string Control)> SelectDeclarations()
        => Hosts.SelectMany(h => h.Clicks
                .Where(c => c.SelectIndex is not null || c.SelectPath is not null || c.PointerPath is not null)
                .Select(c => (Host: h.HostTypeName, Control: c.Name)))
            .ToList();

    // ──────────────────────────────────────────────
    //  判据一：逐宿主，逐按钮
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(HostKeys))]
    public void HostButtons_HaveTheDeclaredEffect(string hostTypeName)
    {
        var spec = Hosts.Single(h => h.HostTypeName == hostTypeName);
        using var sandbox = new DataRootSandbox("hosteffect");
        var errors = new List<string>();
        var readings = new List<string>();

        UiTestHost.Run(() =>
        {
            Window? window = null;
            try
            {
                // ── 静态总线卫生（阶段 134 / 登记 #72）──
                //   先清掉**别的测试留下的**总线订阅，再建宿主：否则"处理器只抬事件、但构造函数把事件
                //   转发到总线"的那几条腿会打到**已关闭**的主窗口上（红不红取决于别的测试跑没跑）。
                //   ⚠ 本类在独占集合里跑（DisableParallelization）⇒ 清总线不会打断别处的并行用例。
                var silenced = StaticBusHygiene.Silence();
                var sharedDataReset = StaticBusHygiene.ResetSharedData();

                var depot = new WindowDepot(sandbox.DataRoot);
                window = spec.Build(depot);
                var ctx = new HostContext(window, depot);

                window.Show();
                UiTestHost.Pump(5);

                var named = CollectFace(spec, ctx, errors);
                var buttonNames = named.Where(kv => kv.Value is Button).Select(kv => kv.Key)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var declaredSelects = spec.Clicks.Where(c => c.SelectIndex is not null || c.SelectPath is not null
                                                              || c.PointerPath is not null)
                    .Select(c => c.Name).Distinct(StringComparer.Ordinal).ToArray();
                readings.Add($"面：宿主内具名 Button {buttonNames.Length} 个 —— " + string.Join(", ", buttonNames));
                readings.Add($"面：本表另声明了 {declaredSelects.Length} 个**选择族**控件（非 Button）"
                             + (declaredSelects.Length == 0
                                ? "" : " —— " + string.Join(", ", declaredSelects)));
                readings.Add($"面：声明 {spec.Clicks.Length} 条（Moves {spec.Clicks.Count(c => c.Bucket == Bucket.Moves)}"
                             + $" / Inert {spec.Clicks.Count(c => c.Bucket == Bucket.Inert)}"
                             + $" / Never {spec.Clicks.Count(c => c.Bucket == Bucket.Never)}）");
                readings.Add($"    静态总线：清掉陈旧订阅 {silenced} 个事件字段 ＋ 清回 {sharedDataReset} 个共享数据字段"
                                + "（登记 #72 的**两半**：阶段 134 清订阅、阶段 139 清数据 —— 之后「抬总线」的腿与\n"
                                + "「读共享数据的腿」才都判得准）");
                readings.Add("    初态 → " + Describe(spec, ReadAll(spec, ctx)));

                var clicked = 0;
                foreach (var click in spec.Clicks)
                {
                    if (click.Bucket == Bucket.Never)
                    {
                        readings.Add($"    {click.Name}（跳过：{click.Why}）");
                        continue;
                    }

                    if (!named.TryGetValue(click.Name, out var control))
                        continue;   // 面守卫已经报过 missing

                    // ── 布景：点击**之前**先填输入框 / 装选中项 ──
                    // ⚠ 它只许动**输入面**分量。下面这段比对布景前后的全分量读数，任何一个
                    // 非输入面分量被布景改了都报红 —— 没有这条自检，「布景」立刻变成一条新的
                    // 作弊面（把「点击的效果」搬进布景里预先做掉，本层就再也看不见它）。
                    if (click.Arrange is not null)
                    {
                        var preArrange = ReadAll(spec, ctx);
                        try
                        {
                            click.Arrange(ctx);
                            UiTestHost.Pump(5);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{spec.HostTypeName}.{click.Name}：布景抛出 {ex.GetType().Name}: {ex.Message}");
                            continue;
                        }

                        var postArrange = ReadAll(spec, ctx);
                        var touched = spec.Probes
                            .Where(p => !string.Equals(preArrange[p.Label], postArrange[p.Label], StringComparison.Ordinal))
                            .ToArray();
                        readings.Add($"    {click.Name} 布景 → " + (touched.Length == 0
                            ? "（没动任何分量）"
                            : string.Join("  ", touched.Select(p => $"{p.Label}={postArrange[p.Label]}"))));

                        var illegal = touched.Where(p => !p.IsInputFace).Select(p => p.Label).ToArray();
                        if (illegal.Length > 0)
                            errors.Add($"{spec.HostTypeName}.{click.Name}：布景改动了**输入面以外**的分量："
                                       + $"{string.Join(", ", illegal)} —— 布景只许填输入框 / 装选中项，"
                                       + "不许直接造数据（否则等于把效果搬进布景，本层就看不见它了）");
                    }

                    var before = ReadAll(spec, ctx);
                    try
                    {
                        Gesture(control, click.SelectIndex, click.SelectPath, click.PointerPath);
                        UiTestHost.Pump(5);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{spec.HostTypeName}.{click.Name}：点击抛出 {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    clicked++;
                    var after = ReadAll(spec, ctx);
                    readings.Add($"    {click.Name} → " + Describe(spec, after));

                    Check(spec, click, before, after, errors);

                    if (!window.IsVisible)
                    {
                        readings.Add($"    （点击 {click.Name} 之后窗口已关闭）");
                        var rest = spec.Clicks
                            .SkipWhile(c => !ReferenceEquals(c, click)).Skip(1)
                            .Where(c => c.Bucket != Bucket.Never)
                            .Select(c => c.Name).ToArray();
                        if (rest.Length > 0)
                            errors.Add($"{spec.HostTypeName}.{click.Name}：点了之后窗口关掉了，"
                                       + $"后面这些**还要点**的按钮无从验证：{string.Join(", ", rest)}"
                                       + " —— 要么把它也登记成 Never，要么挪到表尾");
                        break;
                    }
                }

                if (clicked < 2)
                    errors.Add($"{spec.HostTypeName}：只真点了 {clicked} 个按钮 —— 面太薄，判据接近恒真");
            }
            catch (Exception ex)
            {
                errors.Add($"{spec.HostTypeName}：跑这一趟就抛了 {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (window is not null)
                {
                    try { window.Close(); UiTestHost.Pump(); }
                    catch { /* 收尾尽力而为 */ }
                }
            }
        });

        JudgeReadings.Write(_out, $"宿主按钮效果/{spec.HostTypeName}", readings);

        Assert.True(errors.Count == 0,
            $"{spec.HostTypeName} 内部按钮的可观测效果不符声明：\n" + string.Join("\n", errors)
            + "\n\n读数：\n" + string.Join("\n", readings));
    }

    /// <summary>指针序列的一步（阶段 144）：按下 / 移动 / 抬起，坐标是**目标控件的局部坐标**。</summary>
    internal enum PointerStepKind { Press, Move, Release }

    /// <summary>指针序列的一步（见 <see cref="ClickSpec.PointerPath"/>）。</summary>
    internal readonly record struct PointerStep(PointerStepKind Kind, double X, double Y);

    /// <summary>
    /// 走一步指针序列（阶段 144）。
    ///
    /// <para>🩸 <b>用 <c>Avalonia.Headless</c> 的 <c>MouseDown/MouseMove/MouseUp</c>，不要手工造
    /// <see cref="PointerEventArgs"/></b>（落地时实测）：手工构造的那个 8 参 ctor **不设
    /// <c>RoutedEvent</c>** ⇒ `RaiseEvent` 当场抛
    /// <c>ArgumentException: Cannot raise an event whose RoutedEvent is null</c>
    /// （而带 <c>RoutedEvent</c> 的重载**不存在**）。Headless 扩展走的是**真正的输入管线**，
    /// 而且本仓已有先例（<c>HeadlessInputAndDialogUiTests</c> 就是这么点按钮的）。</para>
    ///
    /// <para>⚠ 坐标语义：声明里的坐标是**目标控件的局部坐标**（与 <c>GetPosition(control)</c> 同义），
    /// 而 Headless 扩展吃的是**根窗口坐标** ⇒ 这里用 <c>TranslatePoint</c> 换算。</para>
    /// </summary>
    private static void RaisePointer(Control control, TopLevel root, PointerStep step)
    {
        var local = new Point(step.X, step.Y);
        var inRoot = control.TranslatePoint(local, root) ?? local;

        switch (step.Kind)
        {
            case PointerStepKind.Press:
                root.MouseDown(inRoot, MouseButton.Left);
                break;
            case PointerStepKind.Move:
                root.MouseMove(inRoot);
                break;
            default:
                root.MouseUp(inRoot, MouseButton.Left);
                break;
        }
    }

    /// <summary>
    /// 模拟「点一下」。对 <see cref="ToggleButton"/> 系（含 <see cref="CheckBox"/>）先翻转
    /// <c>IsChecked</c> —— 那是 <c>ToggleButton.OnClick</c> 真正做的事，只引发 Click 会漏掉
    /// <c>IsCheckedChanged="…"</c> 这种接线（见类注释）。
    /// </summary>
    private static void Gesture(Control control, int? selectIndex, int[]? selectPath = null,
                                IReadOnlyList<PointerStep>? pointerPath = null)
    {
        // ── 指针序列（阶段 144，登记 #77）──
        //   每步之间 Pump()：布局/命中测试要走消息循环（headless 下不 Pump 就拿不到几何）。
        if (pointerPath is { } steps)
        {
            var root = TopLevel.GetTopLevel(control)
                ?? throw new InvalidOperationException(
                    $"{control.GetType().Name} {control.Name} 不在可视树里 —— 指针序列必须打在已装配的控件上");

            foreach (var step in steps)
            {
                RaisePointer(control, root, step);
                UiTestHost.Pump(2);
            }

            return;
        }
        // ── 选择族·第二种手势：**树里的一条路径**（阶段 129）──
        //   逐级走 `TreeViewItem.Items`（子节点挂在父 TreeViewItem 的 Items 上）。
        //   ⚠ 任何一级越界都**抛**：静默选中 null 会让"拨了"与"处理器不干活"在读数上同形 ——
        //     那正是本层最怕的那种同形。
        if (selectPath is { } path)
        {
            if (control is not TreeView pathTree)
                throw new InvalidOperationException(
                    $"{control.GetType().Name} {control.Name} 不是 TreeView，但声明里给了 SelectPath "
                    + "—— 要么改声明，要么给 Gesture 补一支");

            object? node = null;
            var level = pathTree.Items.Cast<object>().ToList();
            for (var depth = 0; depth < path.Length; depth++)
            {
                var step = path[depth];
                if (step < 0 || step >= level.Count)
                    throw new InvalidOperationException(
                        $"{control.Name} 的选择路径 [{string.Join(", ", path)}] 在第 {depth} 级越界"
                        + $"（该级有 {level.Count} 个节点）—— 夹具或路径写错了，别静默选中 null");

                node = level[step];
                level = node is TreeViewItem parent
                    ? parent.Items.Cast<object>().ToList()
                    : new List<object>();
            }

            pathTree.SelectedItem = node;
            return;
        }

        // ── 选择族：拨选中项 ⇒ 引发 SelectionChanged ⇒ 跑它订阅的处理器 ──
        //    ⚠ `index = -1` 是第二档：**清空选中**（见类注释「手势下标有两个档」）。
        //    `SelectedIndex = -1` 本来就是 ListBox / ComboBox 的清空口径；
        //    而 TreeView 没有「清空下标」这回事，必须显式把 SelectedItem 写 null
        //    （写 `ElementAtOrDefault(-1)` 会当场抛 ArgumentOutOfRange）。
        if (selectIndex is { } index)
        {
            switch (control)
            {
                case ListBox list:
                    list.SelectedIndex = index;
                    return;
                case ComboBox combo:
                    combo.SelectedIndex = index;
                    return;
                case TreeView tree:
                    tree.SelectedItem = index < 0
                        ? null
                        : tree.Items.Cast<object>().ElementAtOrDefault(index);
                    return;
                default:
                    throw new InvalidOperationException(
                        $"{control.GetType().Name} {control.Name} 不是选择族控件，但声明里给了 SelectIndex "
                        + "—— 要么改声明，要么给 Gesture 补一支");
            }
        }

        if (control is not Button button)
            throw new InvalidOperationException(
                $"{control.GetType().Name} {control.Name} 既不是 Button 族、也没给 SelectIndex —— "
                + "本条声明不知道怎么驱动它");

        if (button is ToggleButton toggle)
            toggle.IsChecked = !(toggle.IsChecked ?? false);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>逐分量核对一条按钮声明（双向：声明的要对、没声明的不许动）。</summary>
    private static void Check(HostSpec spec, ClickSpec click,
                              Dictionary<string, string> before, Dictionary<string, string> after,
                              List<string> errors)
    {
        var moved = 0;

        foreach (var probe in spec.Probes.Where(p => !p.IsVoice))
        {
            var isDeclared = false;
            var expected = "";
            foreach (var e in click.Expect)
            {
                if (!string.Equals(e.Label, probe.Label, StringComparison.Ordinal)) continue;
                isDeclared = true;
                expected = e.Value;
            }

            var changed = !string.Equals(before[probe.Label], after[probe.Label], StringComparison.Ordinal);

            if (isDeclared)
            {
                if (!string.Equals(after[probe.Label], expected, StringComparison.Ordinal))
                    errors.Add($"{spec.HostTypeName}.{click.Name}：分量「{probe.Label}」期望 {expected}、"
                               + $"实际 {after[probe.Label]}（点击前 {before[probe.Label]}）");
                if (changed) moved++;
            }
            else if (changed)
            {
                errors.Add($"{spec.HostTypeName}.{click.Name}：**未声明**的分量「{probe.Label}」从 "
                           + $"{before[probe.Label]} 变成了 {after[probe.Label]} —— 要么把它写进本条的声明，"
                           + "要么这个按钮改了不该改的东西（副作用外溢）");
            }
        }

        if (click.Bucket == Bucket.Moves && moved == 0)
            errors.Add($"{spec.HostTypeName}.{click.Name}：声明成 Moves，但**声明的分量一个都没动**"
                       + "（声明值恰好等于点击前的值）—— 这条声明等于没写。"
                       + "若它确实不该改数据面，请改成 Inert 并写清理由");
    }

    // ──────────────────────────────────────────────
    //  读数
    // ──────────────────────────────────────────────

    private static Dictionary<string, string> ReadAll(HostSpec spec, HostContext ctx)
        => spec.Probes.ToDictionary(p => p.Label, p => p.Read(ctx), StringComparer.Ordinal);

    private static string Describe(HostSpec spec, Dictionary<string, string> readout)
        => string.Join("  ", spec.Probes.Select(p => $"{p.Label}={readout[p.Label]}"));

    // ──────────────────────────────────────────────
    //  面守卫（运行期）：表里的每个名字都要在窗口里找得到，窗口里每个具名按钮都要在表里
    // ──────────────────────────────────────────────

    /// <summary>
    /// 枚举宿主的**面**：窗口逻辑树里所有**具名**控件。
    ///
    /// <para>⚠ **两半的判法刻意不同**，别把它们混成一条：
    /// <list type="bullet">
    ///   <item><b>Button 族</b> —— 双向差集（少一个 / 多一个都报）。这是原先就有的面。</item>
    ///   <item><b>非 Button（选择族的所在处）</b> —— 只查「**声明了的**名字在不在」。反向那一半
    ///     （「宿主里还有哪些带 <c>SelectionChanged</c> 接线的控件没被分类」）**运行期算不出来**：
    ///     Avalonia 不暴露订阅者名单，而「拨一下什么都没变」与「没接线」同形。它由静态守卫接过，
    ///     见类注释「面的第二条腿」。</item>
    /// </list></para>
    /// </summary>
    private static Dictionary<string, Control> CollectFace(HostSpec spec, HostContext ctx, List<string> errors)
    {
        var all = ctx.Window.GetLogicalDescendants().OfType<Control>().Where(b => b.Name is not null).ToList();

        // GetLogicalDescendants 会重复枚举同一实例 ⇒ 面要按名字去重，
        // 但「同名不同实例」必须报 —— 否则 XAML 里真出现两个同名控件会被悄悄抹平。
        foreach (var group in all.GroupBy(b => b.Name!, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Any(b => !ReferenceEquals(b, first)))
                errors.Add($"{spec.HostTypeName}：控件名 {group.Key} 对应 {group.Count()} 个**不同实例**"
                           + " —— XAML 里出现了同名控件");
        }

        var found = all.GroupBy(b => b.Name!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var declared = spec.Clicks.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var actual = found.Keys.ToHashSet(StringComparer.Ordinal);

        var missing = declared.Except(actual, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            errors.Add($"{spec.HostTypeName}：表里登记了但宿主里找不到这些按钮：{string.Join(", ", missing)}"
                       + " —— 控件被删/改名了，或者表里的名字写错了");

        var buttons = found.Where(kv => kv.Value is Button).Select(kv => kv.Key)
            .ToHashSet(StringComparer.Ordinal);
        var extra = buttons.Except(declared, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (extra.Length > 0)
            errors.Add($"{spec.HostTypeName}：宿主里有这些具名按钮**没被分类**：{string.Join(", ", extra)}"
                       + " —— 请判断它点下去会改哪些分量（Moves）、还是本初始状态下不动数据面（Inert），"
                       + "或写清为什么不点（Never）");

        return found;
    }

    // ──────────────────────────────────────────────
    //  判据二：表卫生（不需要建窗口）
    // ──────────────────────────────────────────────

    /// <summary>
    /// 表的卫生：面非空、桶与字段自洽、理由写得出、<c>Expect</c> 的分量名真的存在。
    /// 没有它，表可以腐烂成「全 Inert + 空理由」而判据仍然全绿。
    /// </summary>
    [Fact]
    public void BucketTables_AreNotEmptiedAndAreSelfConsistent()
    {
        Assert.True(Hosts.Length >= 3, $"宿主表只剩 {Hosts.Length} 个 —— 面被掏空了");

        var totalMoves = 0;
        var totalInert = 0;
        var totalNever = 0;
        var totalClicks = 0;
        var totalArranged = 0;
        var totalSelects = 0;
        var seenHosts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var spec in Hosts)
        {
            Assert.True(seenHosts.Add(spec.HostTypeName), $"宿主表里 {spec.HostTypeName} 出现两次");

            // 分量：名字唯一，且至少要有一个**数据面**分量（否则断言恒真）
            Assert.True(spec.Probes.Length >= 3,
                $"{spec.HostTypeName} 只声明了 {spec.Probes.Length} 个分量 —— 太薄，判据读不出什么");
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in spec.Probes)
                Assert.True(labels.Add(p.Label), $"{spec.HostTypeName} 的分量「{p.Label}」声明了两次");
            var dataLabels = spec.Probes.Where(p => !p.IsVoice).Select(p => p.Label).ToHashSet(StringComparer.Ordinal);
            Assert.True(dataLabels.Count >= 1,
                $"{spec.HostTypeName} 一个数据面分量都没有（全是 Voice）⇒ 断言恒真");

            Assert.True(spec.Clicks.Length >= 3,
                $"{spec.HostTypeName} 只登记了 {spec.Clicks.Length} 条按钮 —— 面太薄");

            // ⚠ 唯一性按 **(名字, 手势种类与取值)** 算，不按名字 —— 选择族与指针族都可以**在序列里被驱动多次**
            // （例：`TrendList` 先拨回夹具那条、再拨回新建那条；两次都是真的驱动、各有各的见证）。
            // 按钮族的下标恒为 `null` ⇒ 与原先「一个按钮名只能有一条」**完全等价**。
            var keys = new HashSet<(string, int?, string?, string?)>();
            var moves = 0;
            var live = 0;
            var selects = 0;

            foreach (var click in spec.Clicks)
            {
                // ⚠ 阶段 144 起第四个分量是**指针路径**：同一个控件可以被两条不同的指针序列驱动
                //   （本宿主就是「点一下」与「拖一段」两条），只按 (名字, 下标) 算会误报重复。
                var pointerKey = click.PointerPath is { } steps
                    ? string.Join(";", steps.Select(x => $"{x.Kind}:{x.X}:{x.Y}"))
                    : null;
                Assert.True(keys.Add((click.Name, click.SelectIndex,
                                      click.SelectPath is { } p ? string.Join(",", p) : null, pointerKey)),
                    $"{spec.HostTypeName} 里 {click.Name} 以同一个手势登记了两次");

                // 选择族：下标要合法；且「不驱动」的条目不该同时给下标（与「Never 不该有布景」同一条理由）。
                if (click.Bucket == Bucket.Never)
                    Assert.True(click.SelectIndex is null && click.SelectPath is null,
                        $"{spec.HostTypeName}.{click.Name} 属于 Never 桶却给了选择手势 —— 不驱动的条目不该带手势");
                Assert.True(click.SelectIndex is null || click.SelectPath is null,
                    $"{spec.HostTypeName}.{click.Name} 同时给了 SelectIndex 与 SelectPath —— 两种手势互斥");

                var gestureKinds = (click.SelectIndex is not null ? 1 : 0)
                                 + (click.SelectPath is not null ? 1 : 0)
                                 + (click.PointerPath is not null ? 1 : 0);
                Assert.True(gestureKinds <= 1,
                    $"{spec.HostTypeName}.{click.Name} 给了 {gestureKinds} 种手势（选择下标 / 树路径 / 指针序列）"
                    + " —— 三种互斥，只许给一条");
                if (click.SelectIndex is { } si)
                {
                    // ⚠ 合法域是**两档**：`-1`（清空选中）与 `≥ 0`（拨到第 n 项）—— 见类注释。
                    // 这条断言本身是「口径」的一部分：把 -1 当成非法值，那两个宿主就永远只能
                    // 登记一条零信息的 SelInert（面还在，判据却够不着它）。
                    Assert.True(si >= -1, $"{spec.HostTypeName}.{click.Name} 的选择下标是 {si} —— "
                        + "合法域是 -1（清空选中）或 ≥ 0（拨到第 n 项）");
                }

                if (click.SelectIndex is not null || click.SelectPath is not null || click.PointerPath is not null)
                    selects++;

                switch (click.Bucket)
                {
                    case Bucket.Moves:
                        Assert.True(click.Expect.Length >= 1,
                            $"{spec.HostTypeName}.{click.Name} 是 Moves 桶却一条期望值都没有 —— 那它和 Inert 就没区别了");
                        Assert.True(click.Why.Trim().Length >= 1, $"{spec.HostTypeName}.{click.Name} 的理由是空的");
                        foreach (var e in click.Expect)
                            Assert.True(dataLabels.Contains(e.Label),
                                $"{spec.HostTypeName}.{click.Name} 声明了分量「{e.Label}」，"
                                + $"但 {spec.HostTypeName} 的 Probe 里没有这个名字 —— 名字写错就是**静默放过**");
                        moves++;
                        live++;
                        break;

                    case Bucket.Inert:
                        Assert.True(click.Expect.Length == 0,
                            $"{spec.HostTypeName}.{click.Name} 是 Inert 桶却带了期望值 —— 两种桶的字段混了");
                        Assert.True(click.Why.Trim().Length >= 10,
                            $"{spec.HostTypeName}.{click.Name} 属于 Inert 桶，理由太短、说明不了什么：\"{click.Why}\"");
                        live++;
                        break;

                    case Bucket.Never:
                        Assert.True(click.Expect.Length == 0,
                            $"{spec.HostTypeName}.{click.Name} 属于 Never 桶却带了期望值");
                        Assert.True(click.Why.Trim().Length >= 10,
                            $"{spec.HostTypeName}.{click.Name} 属于 Never 桶，理由太短：\"{click.Why}\"");
                        break;

                    default:
                        Assert.Fail($"未处理的桶：{click.Bucket}");
                        break;
                }

                // 不点的按钮不该有布景：布景是「点击前把现场摆好」，与 Never 的语义直接冲突。
                if (click.Bucket == Bucket.Never)
                    Assert.True(click.Arrange is null,
                        $"{spec.HostTypeName}.{click.Name} 属于 Never 桶却带了布景 —— 不点的按钮不该有布景");
            }

            Assert.True(moves >= 1, $"{spec.HostTypeName} 一条 Moves 都没有 —— 这一层对它等于没测");
            Assert.True(live >= 2, $"{spec.HostTypeName} 只有 {live} 个按钮会被真点到 —— 判据接近恒真");

            totalMoves += moves;
            totalInert += spec.Clicks.Count(c => c.Bucket == Bucket.Inert);
            totalNever += spec.Clicks.Count(c => c.Bucket == Bucket.Never);
            totalClicks += spec.Clicks.Length;
            totalArranged += spec.Clicks.Count(c => c.Arrange is not null);
            totalSelects += selects;
        }

        Assert.True(totalMoves >= 6, $"全表只有 {totalMoves} 条 Moves —— 面被掏空了");
        Assert.True(totalInert >= 3, $"全表只有 {totalInert} 条 Inert —— 「本状态下不动数据面」这一类知识没被记下来");
        // 选择族的下界刻意留余量（阶段 115 收口时实测 31 个控件 / 35 条声明 —— 见 `s115-f4-readings.txt` 逐宿主那几行）。
        // ⚠ 不把实测值抄成下界：那样每次**正确**的合并/迁移都会多红一条。掉了就是有人把面缩回按钮族了，
        //   那时该来改这条而不是删它。
        Assert.True(totalSelects >= 30,
            $"全表只有 {totalSelects} 条选择族声明 —— 面被缩回 Button 族了？（阶段 115 扩到 SelectionChanged 一族时，"
            + "九宿主实测 **47 个控件 / 53 条声明** —— 其中 33 个控件是『有选择语义』的，另 14 个是 "
            + "`ProjectSettingsView` 里同源的『只标脏』下拉，为静态面闭合而登记；"
            + "🩸 **阶段 136 重数**（第 17 个宿主 `AnimationEditorView` 之后）：**81 条声明 / 66 个控件**"
            + "（阶段 134 那次是 **72 / 58**）；"
            + "而下界的口径**一个字没动** —— 它守的是「面没被缩回 Button 族」，不是「面正好是哪个数」；"
            + "下界仍刻意压在 30（只抬过一次，从 20），不贴实测值）");
        Assert.True(totalNever >= 1, "没有一条 Never —— 「点了会关窗」这类知识没法登记，会退化成乱点");

        // 宿主类型必须真的存在（表是手写的，类型名打错不会被上面任何一条抓到）
        foreach (var spec in Hosts)
        {
            var type = typeof(Views.VariableManagerView).Assembly.GetType($"HmiEditor.App.Views.{spec.HostTypeName}");
            Assert.True(type is not null, $"表里写了宿主 {spec.HostTypeName}，但程序集里没有这个类型 —— 名字写错了");
            // ⚠ 阶段 126 起这里**不再是"必须是 Window"**：本层的宿主定义是「**能装配进一个 Window 的面**」
            //   —— 九个窗口宿主照旧，而**当时**面外 16 个视图里有 **9 个是 UserControl**（**实测**：逐个读
            //   `partial class X : <基类>`；我第一稿凭印象写成"12 个"）⇒ 不放开这一条，那 9 个
            //   永远只能停在"个数台账"上。装配方式见 `Build`：UserControl 由
            //   `new Window { Content = view }` 包一层；名字解析靠 `Control<T>` 的第二条路（逻辑树）。
            Assert.True(typeof(Window).IsAssignableFrom(type!) || typeof(UserControl).IsAssignableFrom(type!),
                $"{spec.HostTypeName} 既不是 Window 也不是 UserControl —— 本层要求「能装配进 Window」，"
                + $"它是 {type!.BaseType?.Name}");
        }

        // Never 桶里的名字必须真的存在于某个 .axaml（防表腐烂成「名字打错也能过」）
        var axaml = AppSourceScanner.ScanAppXaml().Select(s => s.Text).ToArray();
        foreach (var name in Hosts.SelectMany(h => h.Clicks)
                     .Where(c => c.Bucket == Bucket.Never)
                     .Select(c => c.Name)
                     .Distinct(StringComparer.Ordinal))
        {
            Assert.True(axaml.Any(t => t.Contains($"x:Name=\"{name}\"", StringComparison.Ordinal)),
                $"Never 桶里的 {name} 在任何 .axaml 里都找不到 —— 名字写错了或控件已被删");
        }

        JudgeReadings.Write(_out, "宿主按钮效果/表", new[]
        {
            $"宿主 {Hosts.Length} 个；按钮 {totalClicks} 条（Moves {totalMoves} / Inert {totalInert} / Never {totalNever}），"
            + $"其中带布景 {totalArranged} 条",
        });
    }

    /// <summary>
    /// **第四层面的前缀盲区**（阶段 251 实测）：`DockPanelInvocationUiTests.BtnNamesOf` 只收名字以 `Btn`
    /// 开头的具名控件 ⇒ 停靠面板里**不以 `Btn` 开头**的动作控件在那一层的面外；它们只能由**本层**托底
    /// ⇒ 这几个视图**必须在宿主表里**，一个都不许掉（掉了就是"两层都够不到"）。
    ///
    /// <para><b>清单从哪来</b>：阶段 251 把 12 个停靠面板视图逐个过了前缀（§167.2 的实测量）。
    /// 非 `Btn*` 的动作控件只出现在 **6** 个面板视图里 ——
    /// `DesignCanvasView` 5 · `CrossReferenceView` 1 · `AiChatPanelView` 8 · `AuditLogView` 4 ·
    /// `PropertyPanelView` 10 · `ToolboxView` 1。前四个已在本表；`PropertyPanelView` 是**判过的不做**
    /// （阶段 118：数据面由 `PropertyPanelWiringTests` 判过、且写入是批量的，与本层"未声明分量一字不变"
    /// 正面冲突）；`ToolboxView` 的 `ClearSearchButton` 仍是**真缺口**（已登记为下一刀）。
    /// ⇒ 豁免清单是**事实陈述**，不是"可以随便加"：要加先量一遍它在不在第四层的面内。</para>
    ///
    /// <para><b>2026-10-01 阶段 252 更新</b>：`ToolboxView` 那 1 个按钮
    /// （`ClearSearchButton`）已由**专题判据** `ToolboxSearchClearUiTests` 覆盖（它进不了本层 ——
    /// 本层的表卫生要求每宿主 ≥3 条声明，而它只有 1 个按钮）。⇒ 它继续留在 `waived` 里，
    /// 但性质从"真缺口"变成"**有别的落点**"；`PropertyPanelView` 仍是唯一的"判过的不做"。</para>
    /// </summary>
    [Fact]
    public void DockPanelsOutsideTheBtnPrefixedFace_MustBeHostedOrExplicitlyWaived()
    {
        string[] hosted = { "DesignCanvasView", "CrossReferenceView", "AiChatPanelView", "AuditLogView" };
        // PropertyPanelView = 判过的不做（阶段 118）；ToolboxView = 由专题判据覆盖（阶段 252）
        string[] waived = { "PropertyPanelView", "ToolboxView" };

        var all = Hosts.Select(h => h.HostTypeName).ToHashSet(StringComparer.Ordinal);

        var missing = hosted.Where(v => !all.Contains(v)).ToArray();
        Assert.True(missing.Length == 0,
            "这些停靠面板视图的动作控件**不在第四层的 `Btn*` 面内**，必须由本层托底，但宿主表里没有："
            + string.Join(", ", missing));

        var wronglyWaived = waived.Where(all.Contains).ToArray();
        Assert.True(wronglyWaived.Length == 0,
            "这些被记成「两层都够不到」的视图其实已经在宿主表里了 —— 从豁免清单里删掉："
            + string.Join(", ", wronglyWaived));
    }
}
