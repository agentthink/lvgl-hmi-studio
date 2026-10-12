using HmiEditor.Core.Models.Recipe;

namespace HmiEditor.Core.Services;

/// <summary>
/// **配方克隆**失败诊断码 —— 单一来源，值**不带**键前缀（前缀由 App 侧
/// <c>ExternalText.RecipeIssueKey</c> 拼成 <c>recipe.issue.</c>）。
///
/// <para><b>为什么单开一族</b>（登记 <b>#61t</b>，阶段 98）：<c>RecipeManager.CloneRecipe</c>
/// 原先的签名是 <c>public Recipe CloneRecipe(...)</c> —— 一个**声明为非空**的返回类型，
/// 里面的失败档却写成 <c>return null!</c>（用 null-forgiving 压掉警告）⇒ 消费点
/// <c>RecipeEditorView</c> 的 <c>if (clone != null)</c> **没有 else**：
/// 克隆失败时界面**一声不响**（点了按钮什么都没发生）。
/// 更要紧的是它**丢掉了 <c>AddRecipe</c> 的返回值** —— 那个 <c>bool</c> 是作者专门为
/// 「Id 已存在」写的守卫（<c>Recipe.Id</c> = <c>Guid[..8]</c>，**32 位**，撞车虽罕见但可达）
/// ⇒ 撞车时 <c>CloneRecipe</c> 照样 <c>return clone</c>，界面提示**「已克隆」（撒谎）**。</para>
///
/// <para>⇒ 失败是**返回值**、不是异常，所以按本仓库既定模板（<c>SvgParseError</c> 三段式，
/// 见登记 #61f「模板已立」）做成「**返回值带码**」。码表按**产出方**分家
/// （<c>package.issue.</c> / <c>system.issue.</c> / <c>user.issue.</c> / <c>sim.issue.</c> …），
/// 配方这一族此前没有，现在立 <c>recipe.issue.</c>。</para>
/// </summary>
public static class RecipeIssueCodes
{
    /// <summary>
    /// 要克隆的**源配方不在管理器里** —— 界面持有的选中项已经失效（别处删掉了、或工程重载过）。
    /// 这是用户**真能撞到**的运行条件，且修法明确（刷新列表重选）⇒ 给码。
    /// </summary>
    public const string SOURCE_MISSING = "SOURCE_MISSING";

    /// <summary>
    /// 克隆体**入库被拒** —— <c>AddRecipe</c> 因「Id 已存在」返回 <c>false</c>。
    /// ⚠ 实测**极难触发**（新 Id 取自 <c>Guid[..8]</c>），但它**不是**不可达：
    /// 那条守卫正是为此而写 ⇒ 必须让它**响**，绝不能像改动前那样被丢掉后**报成功**。
    /// </summary>
    public const string REJECTED = "REJECTED";
}

/// <summary>
/// 一条配方克隆失败 —— 「**码 + 中文回落原文**（+ 可选实参）」三段式，与
/// <see cref="UserAdminIssue"/> / <see cref="SvgParseError"/> / <c>CsvImportIssue</c> 同形。
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它同时是**排障信息的载体** ——
/// 例如 <see cref="RecipeIssueCodes.SOURCE_MISSING"/> 那一档带着"列表可能已过期"这种
/// 给作者的提示；界面上（走字典时）不显示它，但回落路径与日志里必须还在。
/// ⇒ 回落原文**保持原措辞**，**改口吻只改字典值**。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="RecipeIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record RecipeCloneIssue(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>
    /// 源配方不存在。<paramref name="recipeId"/> 走实参 ⇒ 字典文案里能带上那个 Id
    /// （排障要看的就是它），回落里那句"列表可能已过期"只留给排障。
    /// </summary>
    public static RecipeCloneIssue SourceMissing(string recipeId)
        => new(RecipeIssueCodes.SOURCE_MISSING,
            $"原配方不存在: {recipeId}（列表可能已过期）",
            new object[] { recipeId });

    /// <summary>克隆体入库被拒。<paramref name="newId"/> 是**撞车的那个新 Id**（实参）。</summary>
    public static RecipeCloneIssue Rejected(string newId)
        => new(RecipeIssueCodes.REJECTED,
            $"克隆被拒绝: 新 Id {newId} 与既有配方冲突",
            new object[] { newId });
}

/// <summary>
/// 克隆结果 —— **返回值带码**（产出方**不抛异常**，所以它走
/// <c>ExternalText.Describe(RecipeCloneIssue)</c> 而不是 <c>FailureReason(Exception)</c>）。
///
/// <para><b>两个字段的不变量</b>：<see cref="Recipe"/> 与 <see cref="Issue"/> **恰好有一个非空** ——
/// 成功时 <c>Issue == null</c>、失败时 <c>Recipe == null</c>。用两个工厂
/// （<see cref="Ok"/> / <see cref="Failed"/>）保证这一点，别手工 new。</para>
///
/// <para><b>为什么不直接返回 <c>Recipe?</c></b>：那样站点只知道"失败"，不知道**为什么**
/// —— 而这两档的处置完全不同（一个是"刷新一下重选"，一个是"内部冲突，要报出去"）。
/// 只给 null 等于把 <see cref="RecipeIssueCodes.REJECTED"/> 重新塞回"静默"里，
/// 正是本项要修的东西。</para>
/// </summary>
/// <param name="Recipe">成功时的克隆体；失败时为 <c>null</c>。</param>
/// <param name="Issue">失败时的诊断；成功时为 <c>null</c>。</param>
public sealed record RecipeCloneResult(Recipe? Recipe, RecipeCloneIssue? Issue)
{
    /// <summary>克隆成功。</summary>
    public static RecipeCloneResult Ok(Recipe recipe) => new(recipe, null);

    /// <summary>克隆失败，带上**为什么**。</summary>
    public static RecipeCloneResult Failed(RecipeCloneIssue issue) => new(null, issue);
}
