using System.Text;
using HmiEditor.Core.Models.Recipe;

namespace HmiEditor.Core.Services;

/// <summary>
/// 配方管理器 —— 配方 CRUD、CSV 导入导出、版本控制。
///
/// 设计要点 (data-contracts):
///   - 每个配方有独立版本号，修改时自动递增
///   - CSV 导入导出作为数据交换契约
///   - RecipeItem 的范围校验（MinValue/MaxValue）
///   - 分组管理 + 按组查询
/// </summary>
public class RecipeManager
{
    private readonly Dictionary<string, Recipe> _recipes = new();
    private readonly Dictionary<string, RecipeGroup> _groups = new();
    private readonly object _lock = new();

    /// <summary>配方变更事件</summary>
    public event EventHandler<Recipe>? RecipeChanged;

    /// <summary>日志输出</summary>
    public Action<string>? Log { get; set; }

    // ── 配方 CRUD ──

    /// <summary>
    /// 添加一个配方。如果配方 ID 已存在则返回 false。
    /// </summary>
    public bool AddRecipe(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        lock (_lock)
        {
            if (_recipes.ContainsKey(recipe.Id)) return false;
            recipe.Version = 1;
            recipe.CreatedAt = DateTime.UtcNow;
            recipe.ModifiedAt = DateTime.UtcNow;
            _recipes[recipe.Id] = recipe;
            EnsureGroup(recipe.GroupName);
        }
        RecipeChanged?.Invoke(this, recipe);
        Log?.Invoke($"[RecipeManager] 添加配方: {recipe.Name} (v{recipe.Version})");
        return true;
    }

    /// <summary>
    /// 更新配方（自动递增版本号）。
    /// </summary>
    public bool UpdateRecipe(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        lock (_lock)
        {
            if (!_recipes.ContainsKey(recipe.Id)) return false;
            recipe.Version++;
            recipe.ModifiedAt = DateTime.UtcNow;
            _recipes[recipe.Id] = recipe;
            EnsureGroup(recipe.GroupName);
        }
        RecipeChanged?.Invoke(this, recipe);
        Log?.Invoke($"[RecipeManager] 更新配方: {recipe.Name} (v{recipe.Version})");
        return true;
    }

    /// <summary>
    /// 删除配方。预置配方不可删除。
    /// </summary>
    public bool DeleteRecipe(string recipeId)
    {
        lock (_lock)
        {
            if (!_recipes.TryGetValue(recipeId, out var recipe)) return false;
            if (recipe.IsBuiltIn) return false;
            _recipes.Remove(recipeId);
        }
        Log?.Invoke($"[RecipeManager] 删除配方: {recipeId}");
        return true;
    }

    /// <summary>
    /// 获取配方。
    /// </summary>
    public Recipe? GetRecipe(string recipeId)
    {
        lock (_lock)
        {
            return _recipes.GetValueOrDefault(recipeId);
        }
    }

    /// <summary>
    /// 获取所有配方。
    /// </summary>
    public IReadOnlyList<Recipe> GetAllRecipes()
    {
        lock (_lock)
        {
            return _recipes.Values.OrderBy(r => r.GroupName).ThenBy(r => r.Name).ToList();
        }
    }

    /// <summary>
    /// 按分组获取配方。
    /// </summary>
    public IReadOnlyList<Recipe> GetRecipesByGroup(string groupName)
    {
        lock (_lock)
        {
            return _recipes.Values.Where(r => r.GroupName == groupName).OrderBy(r => r.Name).ToList();
        }
    }

    /// <summary>
    /// 克隆配方（新 ID，版本号重置为 1）。
    /// </summary>
    /// <returns>
    /// **返回值带码**（登记 #61t，阶段 98）：成功 <see cref="RecipeCloneResult.Ok"/>；
    /// 失败 <see cref="RecipeCloneResult.Failed"/> 并说明**为什么**。
    /// ⚠ 改动前的签名是 <c>public Recipe</c>（**声明非空**）而失败档写 <c>return null!</c> ——
    /// 消费点看到的类型是"绝不会 null"，于是 <c>if (clone != null)</c> 连 <c>else</c> 都没写
    /// ⇒ 克隆失败时界面**一声不响**。两档失败（源不在 / 入库被拒）处置不同，
    /// 只给 <c>null</c> 会把后者重新塞回"静默"里，所以给码。
    /// </returns>
    public RecipeCloneResult CloneRecipe(string recipeId, string newName)
    {
        lock (_lock)
        {
            var source = _recipes.GetValueOrDefault(recipeId);
            if (source == null) return RecipeCloneResult.Failed(RecipeCloneIssue.SourceMissing(recipeId));

            var clone = new Recipe
            {
                Name = newName,
                GroupName = source.GroupName,
                Description = source.Description,
            };
            clone.Items.AddRange(source.Items.Select(i => new RecipeItem
            {
                VariableId = i.VariableId,
                Label = i.Label,
                DefaultValue = i.DefaultValue,
                MinValue = i.MinValue,
                MaxValue = i.MaxValue,
                Unit = i.Unit,
                DataType = i.DataType,
                SortOrder = i.SortOrder,
            }));

            // ⚠ 这个 bool 是「Id 已存在」的守卫，**不许丢**（登记 #61t）：丢掉它的话，
            //    撞车时会照样把 clone 当成功返回 ⇒ 界面提示「已克隆」而**实际没入库**。
            //    Recipe.Id = Guid[..8]（32 位）⇒ 撞车罕见但可达，那条守正为此而写。
            if (!AddRecipe(clone))
                return RecipeCloneResult.Failed(RecipeCloneIssue.Rejected(clone.Id));

            return RecipeCloneResult.Ok(clone);
        }
    }

    // ── 分组管理 ──

    /// <summary>
    /// 获取所有分组。
    /// </summary>
    public IReadOnlyList<RecipeGroup> GetAllGroups()
    {
        lock (_lock)
        {
            return _groups.Values.OrderBy(g => g.Name).ToList();
        }
    }

    /// <summary>
    /// 添加分组。
    /// </summary>
    public RecipeGroup AddGroup(string name)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(name.ToLowerInvariant(), out var existing))
                return existing;

            var group = new RecipeGroup { Name = name };
            _groups[name.ToLowerInvariant()] = group;
            Log?.Invoke($"[RecipeManager] 添加分组: {name}");
            return group;
        }
    }

    // ── CSV 导入导出 ──

    /// <summary>
    /// 导出所有配方为 CSV 格式。
    /// 格式：每行一个配方条目，第一列为配方名称，后续列为变量值。
    /// </summary>
    public string ExportCsv()
    {
        var recipes = GetAllRecipes();
        if (recipes.Count == 0) return "";

        var sb = new StringBuilder();

        // Header
        var allItems = recipes
            .SelectMany(r => r.Items)
            .Select(i => i.VariableId)
            .Distinct()
            .ToList();

        sb.Append("RecipeName,RecipeVersion,GroupName");
        foreach (var varId in allItems)
            sb.Append("," + varId);
        sb.AppendLine();

        // Data rows
        foreach (var recipe in recipes)
        {
            sb.Append($"{EscapeCsv(recipe.Name)},{recipe.Version},{EscapeCsv(recipe.GroupName)}");
            var itemValues = recipe.Items.ToDictionary(i => i.VariableId, i => i.DefaultValue);
            foreach (var varId in allItems)
            {
                var val = itemValues.GetValueOrDefault(varId, 0.0);
                sb.Append($",{val:F4}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 从 CSV 导入配方。
    /// 如果配方名已存在，则更新（递增版本号）。
    ///
    /// <para><b>返回值带码</b>（登记 <b>#61f</b>，阶段 100）：改动前签名是 <c>int</c>，
    /// 里面有 **7 处静默**（<c>return 0</c> / <c>continue</c> 了事）⇒ 非数字的值让配方项
    /// **静默少一个**、而界面照样报「成功导入 N 个配方」；<c>AddRecipe</c> 的 <c>bool</c>
    /// 更是**被丢掉后照样 count++**（与 <see cref="CloneRecipe"/> 同一形状，见 #61t②）。
    /// 现在每一处都产一条 <see cref="RecipeImportIssue"/>。</para>
    ///
    /// <para>⚠ 列序**按位置**认定（前三列固定 <c>RecipeName,RecipeVersion,GroupName</c>），
    /// 与 <see cref="ExportCsv"/> 写出去的那一份一致 ⇒ 导出/导入可以往返；
    /// 表头文字本身不参与解析，只用于给用户回报原文。</para>
    /// </summary>
    public RecipeImportResult ImportCsv(string csvContent)
    {
        var issues = new List<RecipeImportIssue>();

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            issues.Add(RecipeImportIssue.TooFewLines());
            return new RecipeImportResult(0, issues);
        }

        var header = lines[0].Split(',');
        if (header.Length < 3)
        {
            issues.Add(RecipeImportIssue.NoHeaderColumns(lines[0]));
            return new RecipeImportResult(0, issues);
        }

        // 列索引按位置认定（与 ExportCsv 的写出顺序一致）
        const int nameIdx = 0;
        const int groupIdx = 2;
        const int valueStartIdx = 3;

        var count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            // 文件行号：lines[0] 是表头 ⇒ 数据行 i 对应文件的第 i + 1 行
            var lineNumber = i + 1;

            var cols = lines[i].Split(',');
            if (cols.Length < 3)
            {
                issues.Add(RecipeImportIssue.RowColumnsMissing(lineNumber));
                continue;
            }

            var recipeName = cols[nameIdx].Trim('"');
            var groupName = cols.Length > groupIdx ? cols[groupIdx].Trim('"') : "";

            // Find existing or create new
            var existing = GetAllRecipes().FirstOrDefault(r => r.Name == recipeName);

            Recipe recipe;
            if (existing != null)
            {
                recipe = existing;
                recipe.Items.Clear();
            }
            else
            {
                recipe = new Recipe { Name = recipeName, GroupName = groupName };
            }

            // Add items for each variable value
            for (int j = valueStartIdx; j < cols.Length && (j - valueStartIdx) < (header.Length - valueStartIdx); j++)
            {
                var varId = header[j].Trim();
                if (string.IsNullOrEmpty(varId))
                {
                    issues.Add(RecipeImportIssue.EmptyColumnName(lineNumber, j + 1));
                    continue;
                }

                var raw = cols[j].Trim();
                if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    recipe.Items.Add(new RecipeItem
                    {
                        VariableId = varId,
                        Label = varId,
                        DefaultValue = value,
                    });
                }
                else
                {
                    issues.Add(RecipeImportIssue.ValueNotANumber(lineNumber, varId, raw));
                }
            }

            // ⚠ 这个 bool 改动前是被丢掉的（`AddRecipe(recipe);` 语句）——
            //   入库被拒时配方根本没进表，count 却照样 +1 ⇒ 计数多算一条。
            var applied = existing != null ? UpdateRecipe(recipe) : AddRecipe(recipe);
            if (!applied)
            {
                issues.Add(RecipeImportIssue.RecipeNotApplied(lineNumber, recipeName));
                continue;
            }

            count++;
        }

        Log?.Invoke($"[RecipeManager] CSV导入: {count} 个配方，{issues.Count} 条问题");
        return new RecipeImportResult(count, issues);
    }

    /// <summary>
    /// 校验配方值是否在范围内。
    /// </summary>
    public List<string> ValidateRecipe(Recipe recipe)
    {
        var errors = new List<string>();
        foreach (var item in recipe.Items)
        {
            if (item.DefaultValue < item.MinValue)
                errors.Add($"{item.Label}: 值 {item.DefaultValue} 低于最小值 {item.MinValue}");
            if (item.DefaultValue > item.MaxValue)
                errors.Add($"{item.Label}: 值 {item.DefaultValue} 超过最大值 {item.MaxValue}");
        }
        return errors;
    }

    /// <summary>
    /// 清空所有配方。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _recipes.Clear();
            _groups.Clear();
        }
        Log?.Invoke("[RecipeManager] 已清空");
    }

    // ── 内部 ──

    private void EnsureGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        AddGroup(groupName);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
