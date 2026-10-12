namespace Hmi.Hardware.Contracts;

/// <summary>
/// 目标板与工程之间的**共享校验规则** —— 放在契约层，供各内置目标复用，
/// 免得每块板把同一条规则各写一遍（写两遍必然漂移）。
/// </summary>
public static class TargetValidation
{
    /// <summary>
    /// 工程选的 LVGL 版本 vs 本板运行时自带的 LVGL 版本；兼容返回 <c>null</c>。
    ///
    /// <para><b>为什么这条是 Error 而不是 Warning</b>：生成代码是按**工程所选版本**写的 ——
    /// <c>LvglApiCompatChecker</c> 只在工程为 9.x 时才把 v8 老 API 改写成 v9 写法；
    /// 工程选 8.3 时它**跳过改写**，生成的 C 里留着 <c>lv_img_set_src</c>、<c>lv_img_set_zoom</c>
    /// 这类 9.x 已改名/移除的符号，而目标板的头文件是 9.5 ⇒ **根本编译不过**。
    /// 这不是"可能有问题"，是"确定不能用"。</para>
    ///
    /// <para>只看**主版本**：<c>LvglApiCompatChecker</c> 的分支判据就是主版本（9.x / 非 9.x），
    /// 所以 9.4 与 9.5 视为兼容，8.3 与 9.5 不兼容。</para>
    ///
    /// <para><b>未声明则不管</b>：目标没填 <see cref="LvglConfig.LvglVersion"/>
    /// （或工程没填）时返回 <c>null</c> —— 缺依据就不下判断。</para>
    /// </summary>
    public static ValidationIssue? CheckProjectLvglVersion(LvglConfig lvgl, string? projectLvglVersion)
    {
        if (string.IsNullOrWhiteSpace(lvgl.LvglVersion)) return null;
        if (string.IsNullOrWhiteSpace(projectLvglVersion)) return null;

        var projectMajor = MajorOf(projectLvglVersion);
        var targetMajor = MajorOf(lvgl.LvglVersion);
        if (projectMajor < 0 || targetMajor < 0 || projectMajor == targetMajor) return null;

        return new(ValidationSeverity.Error, "LVGL_VERSION_MISMATCH",
            $"Project targets LVGL {projectLvglVersion}, but this board's runtime ships LVGL {lvgl.LvglVersion}");
    }

    /// <summary>取版本串的主版本号（<c>"9.5"</c> → 9）；解析不出返回 -1。</summary>
    internal static int MajorOf(string version)
    {
        var head = version.Trim();
        var dot = head.IndexOf('.');
        if (dot >= 0) head = head[..dot];
        return int.TryParse(head, out var major) ? major : -1;
    }
}
