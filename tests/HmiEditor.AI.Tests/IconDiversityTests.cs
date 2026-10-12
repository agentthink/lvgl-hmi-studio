using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// 图标多样性优化测试 —— 分类标准、变体选择、提示词注入。
/// 验证"同类型图标差异化设计"的核心算法逻辑。
/// </summary>
public class IconDiversityTests
{
    // ── 分类标准 ──

    [Theory]
    [InlineData("水泵图标", IconCategory.Device)]
    [InlineData("电机启动按钮", IconCategory.Device)]
    [InlineData("阀门图标", IconCategory.Device)]
    [InlineData("启动图标", IconCategory.Action)]
    [InlineData("停止按钮", IconCategory.Action)]
    [InlineData("报警指示灯", IconCategory.Status)]
    [InlineData("电量状态", IconCategory.Status)]
    [InlineData("返回按钮", IconCategory.Navigation)]
    [InlineData("首页导航", IconCategory.Navigation)]
    [InlineData("管道连接", IconCategory.Process)]
    [InlineData("液位计", IconCategory.Process)]
    [InlineData("无线网络", IconCategory.Communication)]
    [InlineData("急停开关", IconCategory.Safety)]
    [InlineData("锁定图标", IconCategory.Safety)]
    [InlineData("温度传感器", IconCategory.Device)]
    [InlineData("湿度图标", IconCategory.Environment)]
    public void Classify_CorrectCategory(string desc, IconCategory expected)
    {
        Assert.Equal(expected, IconCategoryClassifier.Classify(desc));
    }

    [Fact]
    public void Classify_Unknown_ReturnsGeneral()
    {
        Assert.Equal(IconCategory.General, IconCategoryClassifier.Classify("随便什么"));
        Assert.Equal(IconCategory.General, IconCategoryClassifier.Classify(null));
        Assert.Equal(IconCategory.General, IconCategoryClassifier.Classify(""));
    }

    // ── 变体设计规范 ──

    [Fact]
    public void AllVariants_HaveDistinctSpecText()
    {
        var specs = IconVariantCatalog.All
            .Select(v => IconVariantCatalog.GetSpecText(v))
            .ToList();

        Assert.Equal(7, specs.Count);
        // 每种变体规范互不相同
        Assert.Equal(specs.Count, specs.Distinct().Count());
        // 每种变体温度在合理范围
        foreach (var v in IconVariantCatalog.All)
        {
            Assert.InRange(IconVariantCatalog.GetTemperature(v), 0.0, 1.0);
        }
    }

    // ── 变体选择策略 ──

    [Fact]
    public void DefaultMode_UsesCategoryRepresentativeVariant()
    {
        var selector = new IconVariantSelector(VariantSelectionMode.Default);
        Assert.Equal(IconStyleVariant.Linear, selector.Select(IconCategory.Device));
        Assert.Equal(IconStyleVariant.Filled, selector.Select(IconCategory.Action));
        Assert.Equal(IconStyleVariant.Outline, selector.Select(IconCategory.Status));
        Assert.Equal(IconStyleVariant.Geometric, selector.Select(IconCategory.Process));
        Assert.Equal(IconStyleVariant.Accent, selector.Select(IconCategory.Safety));
    }

    [Fact]
    public void RotateMode_SameCategory_YieldsDistinctVariants()
    {
        var selector = new IconVariantSelector(VariantSelectionMode.Rotate);

        // 同一分类(设备)连续选择 7 次, 应覆盖全部 7 种变体且互不重复
        var variants = Enumerable.Range(0, 7)
            .Select(_ => selector.Select(IconCategory.Device))
            .ToList();

        Assert.Equal(7, variants.Distinct().Count());
        // 首轮第一个应为代表性变体(Linear)
        Assert.Equal(IconStyleVariant.Linear, variants[0]);
    }

    [Fact]
    public void RotateMode_DifferentCategories_Independent()
    {
        var selector = new IconVariantSelector(VariantSelectionMode.Rotate);
        var first = selector.Select(IconCategory.Device);    // Linear (Device 第1次)
        var second = selector.Select(IconCategory.Process);  // Geometric (Process 第1次, 不影响 Device)
        var first2 = selector.Select(IconCategory.Device);   // Filled (Device 第2次)

        Assert.Equal(IconStyleVariant.Linear, first);
        Assert.Equal(IconStyleVariant.Geometric, second);
        // Process 的选择不影响 Device 游标: Device 第2次仍为 Filled(线性之后)
        Assert.Equal(IconStyleVariant.Filled, first2);
    }

    [Fact]
    public void RotateMode_Reset_RestartsSequence()
    {
        var selector = new IconVariantSelector(VariantSelectionMode.Rotate);
        selector.Select(IconCategory.Device);
        selector.Select(IconCategory.Device);
        selector.Reset();
        Assert.Equal(IconStyleVariant.Linear, selector.Select(IconCategory.Device));
    }

    [Fact]
    public void RandomMode_ProducesVariedResults()
    {
        var selector = new IconVariantSelector(VariantSelectionMode.Random, seed: 42);
        var set = new HashSet<IconStyleVariant>();
        for (var i = 0; i < 20; i++)
            set.Add(selector.Select(IconCategory.Device));
        // 20 次随机应覆盖多种变体(至少 3 种)
        Assert.True(set.Count >= 3, $"随机多样性不足, 仅覆盖 {set.Count} 种");
    }

    // ── 提示词注入 ──

    [Fact]
    public void BuildIconMessages_WithVariant_InjectsCategoryAndSpec()
    {
        var prompts = new PromptTemplateEngine();
        var messages = prompts.BuildIconMessages("水泵图标", IconCategory.Device, IconStyleVariant.Linear);

        Assert.Equal(2, messages.Count);
        Assert.Equal(LlmRoles.System, messages[0].Role);
        Assert.Equal(LlmRoles.User, messages[1].Role);
        Assert.Contains("设备", messages[1].Content);
        Assert.Contains("LINEAR", messages[1].Content);
        Assert.Contains("水泵图标", messages[1].Content);
    }

    [Fact]
    public void BuildIconMessages_Default_BackwardCompatible()
    {
        var prompts = new PromptTemplateEngine();
        var messages = prompts.BuildIconMessages("测试图标");
        // 默认 General 分类 + Linear 变体
        Assert.Contains("通用", messages[1].Content);
        Assert.Contains("LINEAR", messages[1].Content);
    }
}
