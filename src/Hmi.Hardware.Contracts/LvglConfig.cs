namespace Hmi.Hardware.Contracts;

/// <summary>
/// 目标板 LVGL 配置 + **运行时契约版本**。
///
/// <para><b>为什么版本要进契约</b>：板子的固件运行时对 LVGL 版本与 ABI 有**硬要求** ——
/// <c>firmware/t113/app/runtime/screen_manager.c</c> 在 <c>dlopen</c> 之后会比对
/// <c>api->abi_version</c> / <c>api->lvgl_version</c>，不一致直接拒绝加载。
/// 而在本项之前，目标插件**没有声明**这两个值，于是编辑器无法判断
/// "工程选的 LVGL 版本能不能跑在这块板子上"（见登记表 §4 第 29 项"版本标记四者对应"）。
/// 声明之后由 <see cref="IHardwareTarget.ValidateProject"/> 在设置界面里直接给出不兼容提示。</para>
/// </summary>
/// <param name="LvglVersion">
/// 本板**运行时自带**的 LVGL 版本（如 <c>"9.5"</c>）。与固件头文件
/// <c>HMI_UI_LVGL_VERSION</c> 必须一致 —— 有跨边界守卫盯着
/// （<c>HmiEditor.Core.Tests.TargetContractVersionTests</c> 直接解析 C 头文件比对）。
/// </param>
/// <param name="AbiVersion">本板运行时要求的 ABI 版本（对应 <c>HMI_UI_ABI_VERSION</c>）。</param>
/// <remarks>
/// 后两项**可省略**，省略即"**未声明**"：仓库里没有该板固件时无从对照，
/// 这时宁可不声明，也不填一个没验证过的版本号（<c>RockchipRK3506Target</c> 就是这种情形）。
/// 只有声明了的目标才会参与"工程 LVGL 版本 ↔ 板子运行时"的兼容性判定。
/// </remarks>
public record LvglConfig(
    int DefaultColorDepth,
    int DefaultMemorySize,
    bool UseDoubleBuffering,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyDictionary<string, string> CustomDefines,
    string LvglVersion = "",
    uint AbiVersion = 0
);
