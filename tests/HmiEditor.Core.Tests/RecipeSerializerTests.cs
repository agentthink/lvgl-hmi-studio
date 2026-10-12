using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class RecipeSerializerTests
{
    // ── Recipe 模型测试 ──

    [Fact]
    public void Recipe_DefaultValues_AreSane()
    {
        var recipe = new Recipe();
        Assert.False(string.IsNullOrEmpty(recipe.Id));
        Assert.Equal(1, recipe.Version);
        Assert.True(recipe.IsEnabled);
        Assert.False(recipe.IsBuiltIn);
        Assert.Empty(recipe.Items);
    }

    [Fact]
    public void Recipe_CanSetProperties()
    {
        var recipe = new Recipe
        {
            Name = "产品A-高温模式",
            GroupName = "温度控制",
            Description = "适用于高温环境"
        };
        Assert.Equal("产品A-高温模式", recipe.Name);
        Assert.Equal("温度控制", recipe.GroupName);
    }

    // ── RecipeItem 模型测试 ──

    [Fact]
    public void RecipeItem_DefaultMinMax_AreOpenRange()
    {
        var item = new RecipeItem();
        Assert.Equal(double.MinValue, item.MinValue);
        Assert.Equal(double.MaxValue, item.MaxValue);
        Assert.Equal("Float", item.DataType);
    }

    [Fact]
    public void RecipeItem_CanSetValues()
    {
        var item = new RecipeItem
        {
            VariableId = "var-temp",
            Label = "目标温度",
            DefaultValue = 180.0,
            MinValue = 0,
            MaxValue = 300,
            Unit = "°C",
            DataType = "Int16",
            SortOrder = 1,
        };
        Assert.Equal(180.0, item.DefaultValue);
        Assert.Equal(0, item.MinValue);
        Assert.Equal(300, item.MaxValue);
        Assert.Equal("°C", item.Unit);
    }

    // ── RecipeGroup 模型测试 ──

    [Fact]
    public void RecipeGroup_CanAddRecipes()
    {
        var group = new RecipeGroup { Name = "温度控制" };
        group.Recipes.Add(new Recipe { Name = "高温模式" });
        group.Recipes.Add(new Recipe { Name = "低温模式" });
        Assert.Equal(2, group.Recipes.Count);
    }

    // ── RecipeManager CRUD 测试 ──

    [Fact]
    public void AddRecipe_Successfully_Adds()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "Test Recipe" };
        Assert.True(mgr.AddRecipe(recipe));
        Assert.Single(mgr.GetAllRecipes());
    }

    [Fact]
    public void AddRecipe_DuplicateId_ReturnsFalse()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "Test" };
        mgr.AddRecipe(recipe);
        var duplicate = new Recipe { Id = recipe.Id, Name = "Duplicate" };
        Assert.False(mgr.AddRecipe(duplicate));
    }

    [Fact]
    public void UpdateRecipe_IncrementsVersion()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "Versioned Recipe" };
        mgr.AddRecipe(recipe);

        var v1 = recipe.Version;
        recipe.Description = "Updated";
        mgr.UpdateRecipe(recipe);
        Assert.Equal(v1 + 1, recipe.Version);
    }

    [Fact]
    public void DeleteRecipe_RemovesFromList()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "Deletable" };
        mgr.AddRecipe(recipe);
        Assert.True(mgr.DeleteRecipe(recipe.Id));
        Assert.Empty(mgr.GetAllRecipes());
    }

    [Fact]
    public void DeleteRecipe_BuiltIn_ReturnsFalse()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "BuiltIn", IsBuiltIn = true };
        mgr.AddRecipe(recipe);
        Assert.False(mgr.DeleteRecipe(recipe.Id));
        Assert.NotEmpty(mgr.GetAllRecipes());
    }

    [Fact]
    public void GetRecipe_ReturnsCorrect()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "FindMe" };
        mgr.AddRecipe(recipe);

        var found = mgr.GetRecipe(recipe.Id);
        Assert.NotNull(found);
        Assert.Equal("FindMe", found!.Name);
    }

    // ── 分组测试 ──

    [Fact]
    public void AddGroup_Deduplicates()
    {
        var mgr = new RecipeManager();
        var g1 = mgr.AddGroup("温度控制");
        var g2 = mgr.AddGroup("温度控制");
        Assert.Same(g1, g2);
    }

    [Fact]
    public void AddRecipe_CreatesGroup_Automatically()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "R1", GroupName = "温度控制" });

        var groups = mgr.GetAllGroups();
        Assert.Contains(groups, g => g.Name == "温度控制");
    }

    [Fact]
    public void GetRecipesByGroup_FiltersCorrectly()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "R1", GroupName = "温度" });
        mgr.AddRecipe(new Recipe { Name = "R2", GroupName = "温度" });
        mgr.AddRecipe(new Recipe { Name = "R3", GroupName = "压力" });

        Assert.Equal(2, mgr.GetRecipesByGroup("温度").Count);
        Assert.Single(mgr.GetRecipesByGroup("压力"));
    }

    // ── 克隆测试 ──

    [Fact]
    public void CloneRecipe_CreatesNewRecipe()
    {
        var mgr = new RecipeManager();
        var original = new Recipe { Name = "Original", GroupName = "G1" };
        original.Items.Add(new RecipeItem { Label = "Temp", VariableId = "v1", DefaultValue = 100 });
        mgr.AddRecipe(original);

        // 阶段 98（#61t）：CloneRecipe 改成**返回值带码** ⇒ 消费点必须从 RecipeCloneResult 取配方。
        // 改动前签名是 `public Recipe`（声明非空）而失败档 `return null!` ——
        // `Assert.NotNull(clone)` 那行其实是**替一个谎言做的担保**。
        var result = mgr.CloneRecipe(original.Id, "Clone");
        var clone = Assert.IsType<Recipe>(result.Recipe);
        Assert.Null(result.Issue);
        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal("Clone", clone.Name);
        Assert.Equal(1, clone.Version);
        Assert.Equal(original.Items.Count, clone.Items.Count);
        Assert.Equal(original.Items[0].DefaultValue, clone.Items[0].DefaultValue);
    }

    /// <summary>
    /// 源配方不在 ⇒ 必须**带码**说明为什么，而不是吐出 <c>null</c>（阶段 98 / #61t）。
    ///
    /// <para><b>这条为什么重要</b>：界面列表可能已过期（别人删了/换了工程），
    /// 那时用户看到的是"克隆按钮点了没反应"。带码之后界面能说清「原配方已不存在」。</para>
    /// </summary>
    [Fact]
    public void CloneRecipe_SourceMissing_ReturnsCodedIssue()
    {
        var mgr = new RecipeManager();

        var result = mgr.CloneRecipe("no-such-id", "Clone");

        Assert.Null(result.Recipe);
        var issue = Assert.IsType<RecipeCloneIssue>(result.Issue);
        Assert.Equal(RecipeIssueCodes.SOURCE_MISSING, issue.Code);
        Assert.Contains("no-such-id", issue.Message, StringComparison.Ordinal);
        Assert.Equal(new object[] { "no-such-id" }, issue.Args);
    }

    /// <summary>
    /// <c>Ok</c> / <c>Failed</c> 两个工厂都**恰好**填一半 —— 两档同时有值会让消费点的
    /// <c>if (result.Recipe is { } clone)</c> 与 <c>else if (result.Issue is { } issue)</c>
    /// 出现"两个都真/两个都假"的歧义（阶段 98 / #61t）。
    /// </summary>
    [Fact]
    public void CloneRecipeResult_CarriesExactlyOneOfRecipeOrIssue()
    {
        var recipe = new Recipe { Name = "R" };
        var ok = RecipeCloneResult.Ok(recipe);
        Assert.Same(recipe, ok.Recipe);
        Assert.Null(ok.Issue);

        var failed = RecipeCloneResult.Failed(RecipeCloneIssue.SourceMissing("x"));
        Assert.Null(failed.Recipe);
        Assert.NotNull(failed.Issue);
    }

    // ── 校验测试 ──

    [Fact]
    public void ValidateRecipe_DetectsOutOfRange()
    {
        var recipe = new Recipe();
        recipe.Items.Add(new RecipeItem { Label = "温度", MinValue = 0, MaxValue = 100, DefaultValue = 150 });
        recipe.Items.Add(new RecipeItem { Label = "压力", MinValue = 0, MaxValue = 50, DefaultValue = 25 });

        var mgr = new RecipeManager();
        var errors = mgr.ValidateRecipe(recipe);

        Assert.Single(errors);
        Assert.Contains("温度", errors[0]);
    }

    [Fact]
    public void ValidateRecipe_ValidValues_ReturnsNoErrors()
    {
        var recipe = new Recipe();
        recipe.Items.Add(new RecipeItem { Label = "温度", MinValue = 0, MaxValue = 100, DefaultValue = 50 });
        recipe.Items.Add(new RecipeItem { Label = "压力", MinValue = 0, MaxValue = 50, DefaultValue = 25 });

        var mgr = new RecipeManager();
        Assert.Empty(mgr.ValidateRecipe(recipe));
    }

    // ── CSV 导出测试 ──

    [Fact]
    public void ExportCsv_Empty_ReturnsEmptyString()
    {
        var mgr = new RecipeManager();
        Assert.Empty(mgr.ExportCsv());
    }

    [Fact]
    public void ExportCsv_ContainsHeaderAndData()
    {
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "高温模式", GroupName = "温度" };
        recipe.Items.Add(new RecipeItem { VariableId = "v1", Label = "温度1", DefaultValue = 180.0 });
        recipe.Items.Add(new RecipeItem { VariableId = "v2", Label = "温度2", DefaultValue = 200.0 });
        mgr.AddRecipe(recipe);

        var csv = mgr.ExportCsv();
        Assert.Contains("RecipeName", csv);
        Assert.Contains("高温模式", csv);
        Assert.Contains("180.0000", csv);
    }

    [Fact]
    public void ExportCsv_MultipleRecipes_AllPresent()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "R1", GroupName = "G1" });
        mgr.AddRecipe(new Recipe { Name = "R2", GroupName = "G1" });

        var csv = mgr.ExportCsv();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // header + 2 data rows
        Assert.Equal(3, lines.Length);
    }

    // ── CSV 导入测试 ──

    [Fact]
    public void ImportCsv_EmptyContent_ReturnsZero()
    {
        var mgr = new RecipeManager();
        var result = mgr.ImportCsv("");
        Assert.Equal(0, result.Imported);
        Assert.Contains(result.Issues, i => i.Code == RecipeImportIssueCodes.TOO_FEW_LINES);
    }

    [Fact]
    public void ImportCsv_ParsesRows()
    {
        var mgr = new RecipeManager();
        var csv = "RecipeName,RecipeVersion,GroupName,v1,v2\n" +
                  "配方1,1,温度,180.0,200.0\n" +
                  "配方2,1,压力,50.0,10.0";

        var result = mgr.ImportCsv(csv);
        Assert.Equal(2, result.Imported);
        Assert.Empty(result.Issues);
        Assert.Equal(2, mgr.GetAllRecipes().Count);
    }

    [Fact]
    public void ImportCsv_ExistingRecipe_UpdatesVersion()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "配方1", GroupName = "温度" });

        var csv = "RecipeName,RecipeVersion,GroupName,v1\n" +
                  "配方1,1,温度,200.0";

        mgr.ImportCsv(csv);

        var recipe = mgr.GetAllRecipes().First(r => r.Name == "配方1");
        Assert.True(recipe.Version >= 2);
    }

    [Fact]
    public void ImportCsv_WithMultilineContent_HandlesCorrectly()
    {
        var mgr = new RecipeManager();
        var csv = "RecipeName,RecipeVersion,GroupName,v1\n" +
                  "配方A,1,温度,100.0\n" +
                  "配方B,1,压力,200.0";

        var result = mgr.ImportCsv(csv);
        Assert.Equal(2, result.Imported);
        Assert.Empty(result.Issues);
        Assert.NotNull(mgr.GetAllRecipes().FirstOrDefault(r => r.Name == "配方A"));
        Assert.NotNull(mgr.GetAllRecipes().FirstOrDefault(r => r.Name == "配方B"));
    }

    // ── 事件测试 ──

    [Fact]
    public void RecipeChangedEvent_FiresOnAdd()
    {
        var fired = false;
        var mgr = new RecipeManager();
        mgr.RecipeChanged += (_, _) => fired = true;
        mgr.AddRecipe(new Recipe { Name = "EventTest" });
        Assert.True(fired);
    }

    [Fact]
    public void RecipeChangedEvent_FiresOnUpdate()
    {
        var fired = false;
        var mgr = new RecipeManager();
        var recipe = new Recipe { Name = "UpdateTest" };
        mgr.AddRecipe(recipe);

        mgr.RecipeChanged += (_, _) => fired = true;
        mgr.UpdateRecipe(recipe);
        Assert.True(fired);
    }

    // ── 清除测试 ──

    [Fact]
    public void Clear_RemovesAll()
    {
        var mgr = new RecipeManager();
        mgr.AddRecipe(new Recipe { Name = "R1" });
        mgr.AddRecipe(new Recipe { Name = "R2" });
        mgr.Clear();
        Assert.Empty(mgr.GetAllRecipes());
        Assert.Empty(mgr.GetAllGroups());
    }
}
