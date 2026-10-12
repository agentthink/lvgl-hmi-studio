namespace HmiEditor.Core.Services;

/// <summary>
/// **配方 CSV 导入**的问题诊断码 —— 单一来源，值**不带**键前缀（前缀由 App 侧
/// <c>ExternalText.RecipeImportIssueKey</c> 拼成 <c>recipe.import.issue.</c>）。
///
/// <para><b>为什么单开一族</b>（登记 <b>#61f</b>，阶段 100）：<c>RecipeManager.ImportCsv</c>
/// 有 **7 处静默** —— 坏行 / 非数字的值 / 空列名 / 入库被拒，全都是 <c>return 0</c> 或
/// <c>continue</c> 了事，而它的签名只是 <c>int count</c>。消费点
/// <c>RecipeEditorView.OnImportCsv</c> 又恰好是 <c>if (count &gt; 0) ... else ...</c> 的二分
/// ⇒ 用户**没有任何渠道**知道坏了哪一行：值写成 <c>abc</c> 的配方项被**静默丢掉**，
/// 界面照样报「成功导入 N 个配方」。这与 <c>CsvVariableImporter</c>（阶段 88，同一登记项）
/// 是**同一个形状**：产出方不抛异常，失败是**返回值**。</para>
///
/// <para><b>为什么不复用 <c>CsvImportIssueCodes</c></b>：那张表的 5 条措辞是**变量导入专属**
/// —— <c>NO_HEADER_COLUMNS</c> 点名「Name / DataType / Address」三列、<c>ROW_*_NAME</c> 指
/// **变量名**、<c>ROW_PARSE_FAILED</c> 是"行循环的防御性兜底"。配方 CSV 的失败面不同
/// （列序固定、值必须是数字、配方有 Id 守卫）⇒ 复用要往里加 3–4 个语义不符的码，
/// 反而把那一族的读数含义搅糊。按**产出方分家**（<c>package.issue.</c> /
/// <c>system.issue.</c> / <c>csv.issue.</c> / <c>recipe.issue.</c> …）是本仓库既有约定。</para>
/// </summary>
public static class RecipeImportIssueCodes
{
    /// <summary>整个文件级：连表头带数据都凑不出两行。0 实参。</summary>
    public const string TOO_FEW_LINES = "TOO_FEW_LINES";

    /// <summary>
    /// 整个文件级：表头**列数不足 3**（配方 CSV 的前三列固定是
    /// <c>RecipeName,RecipeVersion,GroupName</c>）。1 实参 = 当前表头原文，
    /// 便于用户一眼看出自己那份文件的列对不对。
    /// </summary>
    public const string NO_HEADER_COLUMNS = "NO_HEADER_COLUMNS";

    /// <summary>行级：该行列数不足 3 ⇒ 取不到配方名，整行跳过。0 实参（行号是结构化字段）。</summary>
    public const string ROW_COLUMNS_MISSING = "ROW_COLUMNS_MISSING";

    /// <summary>
    /// 行级：表头第 N 列**没有列名** ⇒ 该列的值整列跳过（值没有对应的变量可挂）。
    /// 1 实参 = 列号（1 起）。
    /// </summary>
    public const string EMPTY_COLUMN_NAME = "EMPTY_COLUMN_NAME";

    /// <summary>
    /// 行级：某一格的值**不是数字**（空、<c>abc</c>、<c>1,5</c>、带单位…）⇒ 该**配方项**跳过。
    /// 2 实参 = 变量名 / 原值。
    ///
    /// <para>⚠ 这是本族里**最要紧**的一档：改动前它连"跳过"都是静默的 —— 配方**少了一项**
    /// 而界面报成功。可确定性复现（CSV 里任意一格留空即可）。</para>
    /// </summary>
    public const string VALUE_NOT_A_NUMBER = "VALUE_NOT_A_NUMBER";

    /// <summary>
    /// 行级：这一行**没进管理器** —— <c>AddRecipe</c> / <c>UpdateRecipe</c> 返回了 <c>false</c>。
    /// 1 实参 = 配方名。
    ///
    /// <para>⚠ 改动前这个 <c>bool</c> 是**被丢掉的**（<c>AddRecipe(recipe);</c> 语句丢弃返回值），
    /// 而 <c>count++</c> 照样执行 ⇒ 计数**多算一条**。这是与 <c>CloneRecipe</c>
    /// （登记 #61t②）**完全同一形状**的缺陷，出现在**同一个文件**里。</para>
    ///
    /// <para>⚠ 实测**极难触发**（新配方的 Id 取自 <c>Guid[..8]</c>，撞车才会 <c>false</c>）
    /// ⇒ 它进守卫的 <c>NotProducible</c> 表并写明理由；但那条守卫正是为此而写，
    /// 绝不能像改动前那样被丢掉后**报成功**。</para>
    /// </summary>
    public const string RECIPE_NOT_APPLIED = "RECIPE_NOT_APPLIED";
}

/// <summary>
/// 一条配方 CSV 导入问题 —— 「**码 + 中文回落原文**（+ 可选实参 + 可选行号）」四段式，
/// 与 <see cref="CsvImportIssue"/> / <see cref="SvgParseError"/> / <c>ScriptError</c>
/// / <see cref="RecipeCloneIssue"/> 同形。
///
/// <para><b>它是记录、不是异常</b>：<see cref="RecipeManager.ImportCsv"/> 是产线服务、
/// 契约就是「返回 (导入条数, 问题表)」。需要它的那条路（<c>RecipeEditorView</c>）由
/// <c>ExternalText.Describe(RecipeImportIssue)</c> 渲染，于是「码」只有一个来源、
/// 键只有一套（<c>recipe.import.issue.&lt;码小写&gt;</c>）。</para>
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。</para>
///
/// <para><b><see cref="Line"/> 是结构化字段、不进 <see cref="Message"/></b>
/// （照 <see cref="CsvImportIssue"/> / <see cref="ScriptError"/> 的既有约定）：
/// 行号前缀由 App 侧渲染时加。<c>0</c> = 整个文件级的问题（不附行号）。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="RecipeImportIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**，不含行号。</param>
/// <param name="Line">1 起的行号；<c>0</c> = 文件级问题（渲染时不加前缀）。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record RecipeImportIssue(
    string Code, string Message, int Line = 0, IReadOnlyList<object>? Args = null)
{
    /// <summary>CSV 连表头带数据都不到两行。</summary>
    public static RecipeImportIssue TooFewLines()
        => new(RecipeImportIssueCodes.TOO_FEW_LINES, "CSV 里连表头带数据都不到两行");

    /// <summary>表头列数不足 3。<paramref name="header"/> 是表头原文（实参）。</summary>
    public static RecipeImportIssue NoHeaderColumns(string header)
        => new(RecipeImportIssueCodes.NO_HEADER_COLUMNS,
            $"表头列数不足 3（配方 CSV 的前三列固定是 配方名/版本/分组）: {header}",
            Args: new object[] { header });

    /// <summary>该行列数不足 3，取不到配方名。</summary>
    public static RecipeImportIssue RowColumnsMissing(int line)
        => new(RecipeImportIssueCodes.ROW_COLUMNS_MISSING,
            "该行列数不足 3，取不到配方名", line);

    /// <summary>表头第 <paramref name="columnNumber"/> 列没有列名，该列的值整列跳过。</summary>
    public static RecipeImportIssue EmptyColumnName(int line, int columnNumber)
        => new(RecipeImportIssueCodes.EMPTY_COLUMN_NAME,
            $"表头第 {columnNumber} 列没有列名 ⇒ 该列的值整列跳过",
            line, new object[] { columnNumber });

    /// <summary>
    /// 某一格的值不是数字。<paramref name="variableId"/> / <paramref name="raw"/> 走实参
    /// ⇒ 字典文案里能同时带上「哪个变量」和「原来写的是什么」。
    /// </summary>
    public static RecipeImportIssue ValueNotANumber(int line, string variableId, string raw)
        => new(RecipeImportIssueCodes.VALUE_NOT_A_NUMBER,
            $"「{variableId}」的值「{raw}」不是数字 ⇒ 该配方项被跳过",
            line, new object[] { variableId, raw });

    /// <summary>这一行没进管理器（<c>AddRecipe</c> / <c>UpdateRecipe</c> 返回 false）。</summary>
    public static RecipeImportIssue RecipeNotApplied(int line, string recipeName)
        => new(RecipeImportIssueCodes.RECIPE_NOT_APPLIED,
            $"配方「{recipeName}」入库被拒（Id 冲突）",
            line, new object[] { recipeName });
}

/// <summary>
/// 配方 CSV 导入结果 —— **返回值带码**（产出方不抛异常，所以它走
/// <c>ExternalText.Describe(RecipeImportIssue)</c> 而不是 <c>FailureReason(Exception)</c>）。
///
/// <para><b>为什么不继续返回 <c>int</c></b>：那个 <c>int</c> 只说"成了几条"，
/// 说不出"**坏在哪几行、为什么**" —— 而改动前正是靠它把 7 处静默档盖住的
/// （值不是数字 ⇒ 配方项少一个，<c>count</c> 却照样 +1）。</para>
///
/// <para><b><see cref="Imported"/> 与 <see cref="Issues"/> 不互斥</b>：一次导入可以
/// 既有成功的行、又有问题的行（这正是本项要暴露的常态）⇒ 两个字段都可能有内容，
/// 没有"恰好一个非空"那种不变量。</para>
/// </summary>
/// <param name="Imported">真正进了管理器的配方条数（入库被拒的**不算**）。</param>
/// <param name="Issues">逐条问题，按行号顺序。</param>
public sealed record RecipeImportResult(int Imported, IReadOnlyList<RecipeImportIssue> Issues)
{
    /// <summary>有没有问题 —— 消费点用它决定要不要把明细摆出来。</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>一条都没有（空文件 / 表头就不合格时，问题表里必有内容，所以这是纯兜底）。</summary>
    public static RecipeImportResult Empty() => new(0, Array.Empty<RecipeImportIssue>());
}
