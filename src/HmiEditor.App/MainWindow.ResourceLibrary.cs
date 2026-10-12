using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.App;

/// <summary>
/// MainWindow 的**图库持久化**部分（阶段 120 从 <c>MainWindow.axaml.cs</c> 析出）。
///
/// <para><b>为什么单独一个文件：</b>`MainWindow.axaml.cs` 有行数守卫（相等判定，
/// 只许变小）。图库的安全加载/保存是"长在 3000 行文件里的新职责"，析出后主文件
/// 不被它推高，同时这块逻辑有了独立的名字与注释位。</para>
///
/// <para><b>它守的缺陷：</b><see cref="ResourceLibraryStore.TryLoad"/> 失败
/// （JSON 损坏 / 瞬时读不出来）时，内存里只剩内置资源，而退出时的无条件保存会把
/// 磁盘上的库覆盖成空库 ⇒ 用户自定义资源静默消失。处置：读失败后
/// <c>TrySave</c> 按设计拒绝写盘，这里负责把"为什么没保存"讲给用户听
/// —— 静默失败正是上一版毁数据的帮凶。</para>
/// </summary>
public partial class MainWindow
{
    private bool _resourceLibrarySaveWarned;

    /// <summary>
    /// 加载上次保存的自定义资源。磁盘上的库**不可读**时保持只读（见类注释）：
    /// 不把"只剩内置"的库当成用户数据。
    /// </summary>
    private void LoadResourceLibrary()
    {
        try
        {
            _resourceLibraryStore = new ResourceLibraryStore(AppPaths.DataRoot);
            if (_resourceLibraryStore.TryLoad(out var saved))
            {
                if (saved != null)
                {
                    foreach (var r in saved.CustomResources)
                        _resourceLibrary.CustomResources.Add(r);
                }
                return;
            }
            System.Diagnostics.Debug.WriteLine(
                $"[reslib] 资源库读取失败，本次会话不再保存: {_resourceLibraryStore.LastLoadError}");
        }
        catch
        {
            // 图库加载失败仅使用内置资源，不阻断启动
        }
    }

    /// <summary>
    /// 解包成功后的收尾：报「清单里列着、包里却没有文件」的资产 + 合并包内图库。
    /// 调用点放在 <c>MainWindow.axaml.cs</c>（那边有行数守卫，逻辑本体在本 partial）。
    /// </summary>
    private void InitializePackageState(HmiPackageResult result)
    {
        ReportMissingPackageAssets(result.MissingAssets);
        if (result.ResourceLibrary is { } reslib)
            MergePackageResourceLibrary(reslib);
    }

    /// <summary>
    /// 打开工程包后，把「清单里列着、包里却没有文件」的资产**说出来**（阶段 121）。
    /// 旧行为是打包/解包两边都静默跳过 ⇒ 工程带着断引用打开，用户看不到任何提示。
    /// </summary>
    private void ReportMissingPackageAssets(IReadOnlyList<string>? missingAssets)
    {
        if (missingAssets is not { Count: > 0 }) return;

        var shown = string.Join("、", missingAssets.Take(3));
        if (missingAssets.Count > 3) shown += "…";
        ShowGlobalStatus(AppLocalization.Service.Format(
            Lang.PackageAssetsMissing, missingAssets.Count, shown));
    }

    /// <summary>
    /// 持久化图库资源（内置 + 自定义）。**唯一出口** —— 别处不要直接调
    /// <see cref="ResourceLibraryStore.TrySave"/>，否则读取失败时那次保存
    /// 会绕开保护。
    /// </summary>
    private void PersistResourceLibrary()
    {
        if (_resourceLibraryStore == null) return;
        try
        {
            if (_resourceLibraryStore.TrySave(_resourceLibrary) || _resourceLibrarySaveWarned) return;
            _resourceLibrarySaveWarned = true;
            System.Diagnostics.Debug.WriteLine(
                $"[reslib] 已暂停保存（磁盘上的库不可读，拒绝覆盖）: {_resourceLibraryStore.LastLoadError}");
            ShowGlobalStatus(AppLocalization.Service[Lang.ReslibSaveSuspended]);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[reslib] 保存失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
