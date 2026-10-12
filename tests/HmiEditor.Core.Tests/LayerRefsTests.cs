using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 图层引用（公共页面作为背景图层）机制测试：
/// 注册表排除、_inner 工厂、图层引用表、校验规则。
/// </summary>
public class LayerRefsTests
{
    [Fact]
    public void SharedLibraryProject_WithLayerRefs_ExcludesLayerFromRegistry()
    {
        var project = new HmiProject { Name = "Layer", LvglVersion = "9.5" };
        var main = new HmiScreen { Id = "main", Name = "Main", Width = 800, Height = 480 };
        var layer = new HmiScreen { Id = "lyr", Name = "背景", Width = 800, Height = 480, IsPublicScreen = true };
        main.ReferencedLayerIds.Add(layer.Id);
        project.Screens.Add(main);
        project.Screens.Add(layer);

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        // 普通屏幕保留顶层包装（非 static，供模拟器 main.c 链接）并进入注册表
        CSource.AssertCode(code, "lv_obj_t* create_screen_main(void)");
        Assert.Contains("\"Main\", \"main\", create_screen_main", code);

        // 被引用图层：仅生成 _inner 工厂，不生成顶层包装、不进注册表
        CSource.AssertCode(code, "static lv_obj_t* create_screen_lyr_inner(lv_obj_t* parent)");
        CSource.AssertNoCode(code, "create_screen_lyr(void)");
        Assert.DoesNotContain("\"lyr\", \"lyr\", create_screen_lyr,", code);

        // 图层引用表 + 访问器（运行时 dlsym 获取）
        Assert.Contains("\"main\", \"lyr\", create_screen_lyr_inner", code);
        Assert.Contains("hmi_ui_get_layers", code);
        Assert.Contains("HMI_LAYER_REF_COUNT = 1;", code);
    }

    [Fact]
    public void SharedLibraryProject_NoLayerRefs_EmitsEmptyLayerTable()
    {
        var project = new HmiProject { Name = "P", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });

        var code = new CodeGenerator().GenerateSharedLibraryProject(project);

        Assert.Contains("HMI_LAYER_REF_COUNT = 0;", code);
        Assert.Contains("hmi_ui_get_layers", code);
    }

    [Fact]
    public void FullProject_WithLayerRefs_EmitsLayerPoolAndBootAttach()
    {
        var project = new HmiProject { Name = "FP", LvglVersion = "9.5" };
        var main = new HmiScreen { Id = "main", Name = "Main", Width = 800, Height = 480 };
        var layer = new HmiScreen { Id = "lyr", Name = "背景", Width = 800, Height = 480, IsPublicScreen = true };
        main.ReferencedLayerIds.Add(layer.Id);
        project.Screens.Add(main);
        project.Screens.Add(layer);
        project.RuntimeConfig.BootScreenId = "main";

        var code = new CodeGenerator().GenerateFullProject(project);

        // 共享图层池（detached + 初始隐藏）+ 挂载到启动屏幕背景层
        CSource.AssertCode(code, "lv_obj_t* layer_lyr = create_screen_lyr_inner(NULL);");
        CSource.AssertCode(code, "lv_obj_add_flag(layer_lyr, LV_OBJ_FLAG_HIDDEN);");
        CSource.AssertCode(code, "lv_obj_set_parent(layer_lyr, screen_main);");
        CSource.AssertCode(code, "lv_obj_remove_flag(layer_lyr, LV_OBJ_FLAG_HIDDEN);");
        CSource.AssertCode(code, "lv_obj_move_background(layer_lyr);");

        // 图层不作为独立屏幕创建
        CSource.AssertNoCode(code, "screen_lyr = create_screen_lyr()");
    }

    [Fact]
    public void Validate_CatchesInvalidReferences()
    {
        var project = new HmiProject { Name = "P", LvglVersion = "9.5" };
        var a = new HmiScreen { Id = "a", Name = "A" };
        var b = new HmiScreen { Id = "b", Name = "B", IsPublicScreen = true };
        var c = new HmiScreen { Id = "c", Name = "C" };
        a.ReferencedLayerIds.Add("a");        // 自引用
        a.ReferencedLayerIds.Add("c");        // 目标非公共页面
        a.ReferencedLayerIds.Add("missing");  // 目标不存在
        b.ReferencedLayerIds.Add("a");        // 图层嵌套引用其他图层
        project.Screens.Add(a);
        project.Screens.Add(b);
        project.Screens.Add(c);

        var errors = ScreenLayerRefs.Validate(project);

        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public void Validate_ValidReferences_Passes()
    {
        var project = new HmiProject { Name = "P", LvglVersion = "9.5" };
        var a = new HmiScreen { Id = "a", Name = "A" };
        var b = new HmiScreen { Id = "b", Name = "B", IsPublicScreen = true };
        var d = new HmiScreen { Id = "d", Name = "D" };
        a.ReferencedLayerIds.Add(b.Id);
        d.ReferencedLayerIds.Add(b.Id);   // 多屏共享同一图层：允许
        project.Screens.Add(a);
        project.Screens.Add(b);
        project.Screens.Add(d);

        Assert.Empty(ScreenLayerRefs.Validate(project));

        // 被引用集合包含 b（注册表排除依据）
        var referenced = ScreenLayerRefs.CollectReferencedLayerIds(project);
        Assert.Contains(b.Id, referenced);
        Assert.DoesNotContain(a.Id, referenced);
        Assert.DoesNotContain(d.Id, referenced);
    }
}
