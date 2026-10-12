using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.Recipe;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **列表行的「就地刷新」行为**（登记表 #57 的验收面）。
///
/// <para><b>它补的是什么：</b>阶段 76 那条判据（<c>check-index-based-control-lookup.py</c>）
/// 管的是**源码形状** —— 「不许按位置钻进项的 <c>Children</c> 找控件」。它能保证以后不再这么写，
/// **不保证**「已经改成持引用的那些行在改字段之后真的还刷新」。阶段 77 把三份视图的列表行
/// 从「按 <c>Children[i]</c> 反查」改成「构造时捕获控件的行类型」（<c>AlarmGroupRow</c> /
/// <c>AnimationRow</c> / <c>RecipeParamRow</c>）时，<b>踩坏的方式恰好是静默的</b>：
/// 行类型没被装进 <c>Items</c>、捕获的控件不是显示那个字段的控件、刷新循环用错了匹配条件 ——
/// 三种都**不抛、不报错**，只是那一行停在旧值。所以需要这一层**行为**判据。</para>
///
/// <para><b>判据形状（两侧都断言）：</b>编辑前那一行**含旧值**、编辑后**含新值且不再含旧值**，
/// 同时断言模型与输入框也变了（把「编辑没生效」与「刷新坏了」分开）。
/// 只断言「含新值」是不够的 —— 若把新值追加而不是替换，那一行会同时出现新旧两个值。</para>
///
/// <para><b>改写字段走真实输入</b>（<c>Show</c> → <c>Focus</c> → <c>KeyTextInput</c>），
/// <b>不是</b>程序化 <c>box.Text = …</c> —— 后者在 Avalonia 12.1.0 上**不触发
/// <c>TextChanged</c>**，于是整条 <c>SyncXxxFromUi</c> 链根本不会跑，判据会以「红」的形式
/// 给出一个**假的**结论（详见本类第三条用例，它把这条框架事实钉住了）。
/// 清空那一行用程序化赋值（正因为不触发，才能静默把输入框腾空）。</para>
///
/// <para><b>为什么断言不按下标读那一行：</b>从行里取文本走
/// <c>Content.GetVisualDescendants().OfType&lt;TextBlock&gt;()</c> —— **按类型收、不看顺序**。
/// 用 <c>Content.Children[1]</c> 去读等于把「位置」写进判据自己身上，那正是本阶段要消灭的东西
/// （而且位置一挪，判据就会以「断言失败」的形式**假红**）。</para>
///
/// <para><b>本层不声称的：</b>① 不覆盖 <c>AlarmConfigView</c> 那处 —— 它刷新的
/// <c>group.Alarms.Count</c> 在该编辑路径上**不可能变过**（增删报警都各自调了
/// <c>RefreshGroupList()</c>），所以没有可观测行为可断言，只做了结构改造；
/// ② 只断言「文本」，不断言状态圆点那种**颜色**（颜色断言要把具体色值抄进判据）；
/// ③ 不覆盖「项被重建后旧引用是否还在表里」。</para>
/// </summary>
[Collection("Language-global")]
public class ListRowInPlaceRefreshUiTests
{
    private const string AnimNewName = "改过的动画名";
    private const string ParamOldLabel = "旧参数名";
    private const string ParamNewLabel = "改过的参数名";

    private readonly ITestOutputHelper _output;

    public ListRowInPlaceRefreshUiTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 把一行里所有 <see cref="TextBlock"/> 的文本收成数组 —— **按类型收、不看顺序**。
    /// 见类注释「为什么断言不按下标读那一行」。
    /// </summary>
    private static string[] RowTexts(object? row) =>
        row is ListBoxItem { Content: Control content }
            ? content.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToArray()
            : Array.Empty<string>();

    // ──────────────────────────────────────────────
    //  动画编辑：编辑名称 ⇒ 那一行的名称文本跟着变
    // ──────────────────────────────────────────────

    [Fact]
    public void AnimationRow_TextsFollowTheEditedField()
    {
        using var sandbox = new DataRootSandbox("rowrefresh-anim");

        var report = UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var anim = depot.Animations[0];          // WindowDepot 里叫「呼吸灯」
            var oldName = anim.Name;

            var view = new Views.AnimationEditorView(depot.Animations, depot.Project);
            view.Show();
            UiTestHost.Pump();

            var list = view.FindControl<ListBox>("AnimationList")!;
            var nameBox = view.FindControl<TextBox>("TxtAnimationName")!;

            // 走生产路径的入口：选中一行 ⇒ SelectionChanged 把 _currentAnimation 装上
            list.SelectedIndex = 0;
            UiTestHost.Pump();

            var row = list.Items.Count > 0 ? list.Items[0] : null;
            var before = RowTexts(row);

            // 腾空输入框（程序化赋值**不触发** TextChanged ⇒ 静默），再**真实输入**
            nameBox.Text = "";
            nameBox.Focus();
            UiTestHost.Pump();
            view.KeyTextInput(AnimNewName);
            UiTestHost.Pump();

            var result = (OldName: oldName, Before: before, After: RowTexts(row),
                          ModelName: anim.Name, BoxText: nameBox.Text, Rows: list.Items.Count);
            view.Close();
            return result;
        });

        // 探针自检：读不到文本 ⇒ 行的内容不是代码构造的树，下面几条断言会**假绿**
        Assert.True(report.Rows > 0, "AnimationList 里一行都没有 —— 生产路径没把数据装上，本判据会假绿");
        Assert.True(report.Before.Length > 0,
            $"探针在动画行里一个 TextBlock 都没读到（{report.Rows} 行）—— 本判据会假绿，先修探针");

        Assert.Contains(report.OldName, report.Before);
        Assert.Equal(AnimNewName, report.ModelName);            // 编辑真的走到了模型
        Assert.Equal(AnimNewName, report.BoxText);              // …也留在了输入框里
        Assert.Contains(AnimNewName, report.After);             // 那一行的显示跟着变
        Assert.DoesNotContain(report.OldName, report.After);    // 旧值必须消失（换掉，不是追加）

        // 再收紧一格：**恰好一个** TextBlock 的文本变了。
        // 只断言「含新值」会漏掉「把引用接错了控件」—— 若 NameText 与 TriggerText 接反，
        // 新名字只是**搬到了另一个格子**里，集合比较依然满足（实测：那次注入在本断言下才红）。
        // 比较的是**同一个行实例**的前后两份，按同一顺序逐位比 ⇒ 不依赖具体的下标值。
        Assert.Equal(report.Before.Length, report.After.Length);
        Assert.Equal(1, report.Before.Zip(report.After).Count(p => !string.Equals(p.First, p.Second)));

        JudgeReadings.Write(_output, "列表行就地刷新/动画行", new[]
        {
            $"行内 TextBlock {report.Before.Length} 个，共 {report.Rows} 行",
            $"编辑前含旧名 {report.Before.Contains(report.OldName)}",
            $"编辑后含新名 {report.After.Contains(AnimNewName)}"
                + $"、仍含旧名 {report.After.Contains(report.OldName)}",
            $"模型 Name = {report.ModelName}；输入框 = {report.BoxText}",
        });
    }

    // ──────────────────────────────────────────────
    //  配方编辑：编辑参数名 ⇒ 那一行的标签文本跟着变
    // ──────────────────────────────────────────────

    [Fact]
    public void RecipeParamRow_LabelFollowsTheEditedField()
    {
        using var sandbox = new DataRootSandbox("rowrefresh-recipe");

        var report = UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            var param = new RecipeItem { Label = ParamOldLabel, SortOrder = 1 };
            var recipe = new Recipe { Name = "配方A", GroupName = "默认分组" };
            recipe.Items.Add(param);
            var added = depot.Recipes.AddRecipe(recipe);

            var view = new Views.RecipeEditorView(depot.Recipes, depot.Variables);
            view.Show();
            UiTestHost.Pump();

            var groupList = view.FindControl<ListBox>("GroupList")!;
            var recipeList = view.FindControl<ListBox>("RecipeList")!;
            var paramList = view.FindControl<ListBox>("ParamList")!;
            var labelBox = view.FindControl<TextBox>("TxtParamLabel")!;

            // 三级联动：分组 → 配方 → 参数。**先置 -1 再置 0** —— 生产构造可能已经预选了第 0 行，
            // 直接再选一次 0 不产生变更，SelectionChanged 也就不会派发（实测踩到）。
            if (groupList.ItemCount > 0)
            {
                groupList.SelectedIndex = -1;
                groupList.SelectedIndex = 0;
            }
            var recipeCount = recipeList.ItemCount;
            if (recipeCount > 0)
            {
                recipeList.SelectedIndex = -1;
                recipeList.SelectedIndex = 0;
            }
            var paramCount = paramList.ItemCount;
            if (paramCount > 0)
            {
                paramList.SelectedIndex = -1;
                paramList.SelectedIndex = 0;
            }
            UiTestHost.Pump();

            var row = paramList.Items.Count > 0 ? paramList.Items[0] : null;
            var before = RowTexts(row);

            labelBox.Text = "";
            labelBox.Focus();
            UiTestHost.Pump();
            view.KeyTextInput(ParamNewLabel);
            UiTestHost.Pump();

            var result = (OldLabel: param.Label, Before: before, After: RowTexts(row),
                          ModelLabel: param.Label, BoxText: labelBox.Text,
                          Added: added, RecipeCount: recipeCount, ParamCount: paramCount);
            view.Close();
            return result;
        });

        // 探针自检：三级联动任何一级断了，下面的断言都会**假绿**
        Assert.True(report.Added, "夹具没能把配方加进 RecipeManager —— 本判据会假绿");
        Assert.True(report.RecipeCount > 0,
            "选了第一个分组之后 RecipeList 里没有配方 —— 生产填充链断了，本判据会假绿");
        Assert.True(report.ParamCount > 0,
            "选了第一个配方之后 ParamList 里没有参数 —— 生产填充链断了，本判据会假绿");
        Assert.True(report.Before.Length > 0,
            $"探针在参数行里一个 TextBlock 都没读到（{report.ParamCount} 行）—— 本判据会假绿，先修探针");

        Assert.Contains(ParamOldLabel, report.Before);
        Assert.Equal(ParamNewLabel, report.ModelLabel);          // 编辑真的走到了模型
        Assert.Equal(ParamNewLabel, report.BoxText);
        Assert.Contains(ParamNewLabel, report.After);
        Assert.DoesNotContain(ParamOldLabel, report.After);

        // 同上：**恰好一个** TextBlock 变了（参数行有 7 个格子，接错引用会红在这一条上）
        Assert.Equal(report.Before.Length, report.After.Length);
        Assert.Equal(1, report.Before.Zip(report.After).Count(p => !string.Equals(p.First, p.Second)));

        JudgeReadings.Write(_output, "列表行就地刷新/配方参数行", new[]
        {
            $"行内 TextBlock {report.Before.Length} 个，共 {report.ParamCount} 行",
            $"编辑前含旧名 {report.Before.Contains(ParamOldLabel)}",
            $"编辑后含新名 {report.After.Contains(ParamNewLabel)}"
                + $"、仍含旧名 {report.After.Contains(ParamOldLabel)}",
            $"模型 Label = {report.ModelLabel}；输入框 = {report.BoxText}",
        });
    }

    // ──────────────────────────────────────────────
    //  框架事实：上面两条判据**依赖**的那个机制，单独钉住
    // ──────────────────────────────────────────────

    /// <summary>
    /// 钉住一条 Avalonia 12.1.0 的事实：**程序化设 <c>TextBox.Text</c> 不触发 <c>TextChanged</c>，
    /// 真实输入才触发**。
    ///
    /// <para><b>为什么要单独立一条</b>：上面两条判据若用程序化赋值去驱动，会**静默地什么都不测**
    /// （handler 不跑 ⇒ 模型不变 ⇒ 判据红，而红的原因与产品缺陷无关）。这条事实一旦被框架改掉，
    /// 最先崩的会是上面那两条判据 —— 有本用例在，就能立刻分清「框架变了」与「产品坏了」。</para>
    ///
    /// <para>另外它顺带说明：产品里那些 <c>TxtX.Text = model.X</c> 装载代码**本身不会**引发
    /// 回写，<c>_isUpdating</c> 那类守卫真正挡的是 <c>ComboBox.SelectedIndex</c> /
    /// <c>CheckBox.IsChecked</c> 这些**会**程序化派发的事件。</para>
    /// </summary>
    [Fact]
    public void FrameworkFact_ProgrammaticTextIsSilent_TypingIsNot()
    {
        var report = UiTestHost.Run(() =>
        {
            var box = new TextBox { Width = 140, Height = 30 };
            var fired = 0;
            box.TextChanged += (_, _) => fired++;

            var window = new Window { Width = 220, Height = 100, Content = box };
            window.Show();
            UiTestHost.Pump();

            box.Text = "程序化";                       // 未 Focus、未输入
            var afterProgrammatic = fired;

            box.Focus();
            UiTestHost.Pump();
            box.Text = "";                             // 再清空一次（同样静默）—— 顺带钉住这一点
            var afterSecondProgrammatic = fired;
            window.KeyTextInput("真实输入");            // ⚠ 在**光标处插入**（光标默认 0），不是追加
            UiTestHost.Pump();

            var result = (AfterProgrammatic: afterProgrammatic,
                          AfterSecondProgrammatic: afterSecondProgrammatic,
                          Total: fired, Text: box.Text);
            window.Close();
            return result;
        });

        Assert.Equal(0, report.AfterProgrammatic);      // 程序化赋值：一次都不派发
        Assert.Equal(0, report.AfterSecondProgrammatic);
        Assert.True(report.Total > 0,                   // 真实输入：至少派发一次
            "真实输入也没有触发 TextChanged —— 本类另外两条判据会以「红」给出假结论，先查框架版本");
        Assert.Equal("真实输入", report.Text);

        JudgeReadings.Write(_output, "列表行就地刷新/框架事实", new[]
        {
            $"程序化赋值触发的 TextChanged 次数 = {report.AfterProgrammatic}"
                + $"、清空赋值后仍是 {report.AfterSecondProgrammatic}",
            $"真实输入累计次数 = {report.Total}；最终文本 = {report.Text}",
        });
    }
}
