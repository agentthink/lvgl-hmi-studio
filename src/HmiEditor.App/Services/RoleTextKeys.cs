using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.Core.Models.User;

namespace HmiEditor.App.Services;

/// <summary>
/// **预置角色的显示文案键推导 —— 唯一实现**（闭环登记 #50）。
///
/// <para><b>为什么需要它</b>：<c>UserRole</c> 是模型，只该存 <c>Id/Name/Level/Permissions</c>；
/// 而「操作员 / 工程师 / 管理员」这三个**界面文案**应当跟界面语言走。
/// 在此之前，界面直接显示模型里的 <c>Name = "Operator"</c> —— 中文界面下是英文、英文界面下
/// 依然是英文，字典里那三条 <c>role.*</c> 键**没有任何消费点**（阶段 64 的键可达性判据
/// 正是把它们报成"不可达"才暴露了这件事）。</para>
///
/// <para><b>为什么键推导住在 App 层</b>：与 <see cref="PluginTextKeys"/> 同一条理由 ——
/// Core 是库、不引用 <c>Lang</c>/<c>LocalizationService</c>；「把模型的东西翻译成键」是**视图侧**的事。
/// <c>Lang</c> 常量也就只有 App 侧能引用，键推导放别处必然要抄一份键字符串。</para>
///
/// <para><b>这张表为什么手写、且用模型的 <c>.Id</c> 而不是字面量</b>：
/// ① 用 <see cref="UserRole.BuiltInOperator"/><c>.Id</c> ⇒ **Id 字符串不出现第二遍**，
/// 改 Id 时这张表跟着走（不会静默对不上）；② 仍然是**手写的一对一表** ⇒ 将来给
/// <c>UserRole.BuiltInAll</c> 加第 4 个预置角色而忘了加一行时，守卫
/// <c>RoleNameLocalizationTests.BuiltInRoles_AndRoleTextMap_AreOneToOne</c> 会**红**，
/// 而不是让那个角色在界面上显示成英文（或类型名）。</para>
///
/// <para><b>自定义角色不翻译</b>：<see cref="DisplayName"/> 对表外 Id 回落到 <c>role.Name</c> ——
/// 那是用户自己起的名字（用户数据），翻它没有意义。</para>
/// </summary>
internal static class RoleTextKeys
{
    /// <summary>
    /// 预置角色 → 文案键。<b>与 <see cref="UserRole.BuiltInAll"/> 必须一对一</b>
    /// （守卫双向断言：表里每个 Id 都在角色表里、角色表里每个 Id 都在表里）。
    /// </summary>
    private static readonly (string RoleId, string NameKey, string DescKey)[] Map =
    {
        (UserRole.BuiltInOperator.Id, Lang.RoleOperator, Lang.RoleOperatorDesc),
        (UserRole.BuiltInEngineer.Id, Lang.RoleEngineer, Lang.RoleEngineerDesc),
        (UserRole.BuiltInAdministrator.Id, Lang.RoleAdministrator, Lang.RoleAdministratorDesc),
    };

    /// <summary>表里用到的**全部**键（名字 + 描述）—— 守卫拿它和字典的 <c>role.*</c> 家族对拍。</summary>
    internal static IReadOnlyList<string> AllKeys =>
        Map.SelectMany(m => new[] { m.NameKey, m.DescKey }).ToList();

    /// <summary>表里登记的**角色 Id** —— 守卫拿它和 <see cref="UserRole.BuiltInAll"/> 对拍（双向）。</summary>
    internal static IReadOnlyList<string> MappedRoleIds =>
        Map.Select(m => m.RoleId).ToList();

    /// <summary>显示名键；<c>null</c> = 不是预置角色（用模型里的名字）。</summary>
    internal static string? NameKey(string roleId) =>
        Map.FirstOrDefault(m => string.Equals(m.RoleId, roleId, StringComparison.Ordinal)).NameKey;

    /// <summary>描述键；<c>null</c> = 不是预置角色。</summary>
    internal static string? DescriptionKey(string roleId) =>
        Map.FirstOrDefault(m => string.Equals(m.RoleId, roleId, StringComparison.Ordinal)).DescKey;

    /// <summary>角色显示名（预置角色走字典，其它用模型 <c>Name</c>）。</summary>
    internal static string DisplayName(UserRole role) =>
        NameKey(role.Id) is { } key ? AppLocalization.Service[key] : role.Name;

    /// <summary>
    /// 「名字 (Lv.n)」—— 角色下拉与角色列表共用的**同一个格式**。
    /// 此前两处各写一遍 <c>$"{r.Name} (Lv.{r.Level})"</c>，本地化时必然只改一处。
    /// </summary>
    internal static string DisplayNameWithLevel(UserRole role) =>
        $"{DisplayName(role)} (Lv.{role.Level})";

    /// <summary>角色描述（预置角色走字典，其它用模型 <c>Description</c>）。</summary>
    internal static string DisplayDescription(UserRole role) =>
        DescriptionKey(role.Id) is { } key
            ? AppLocalization.Service[key]
            : role.Description ?? "";
}
