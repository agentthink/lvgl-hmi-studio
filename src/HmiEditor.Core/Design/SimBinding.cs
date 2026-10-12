namespace HmiEditor.Core.Design;

/// <summary>
/// 模拟器绑定刷新（sim_refresh_bindings）正弦波模拟共享系数 ——
/// 集中振幅/相位换算常量与相位推导，避免生成代码里魔法数字漂移。
/// </summary>
public static class SimBinding
{
    /// <summary>正弦波振幅 = 量程 × 该系数。</summary>
    public const double AmplitudeRatio = 0.45;

    /// <summary>度 → 弧度换算的 C 字面量（用于生成的 C 代码）。</summary>
    public const string Deg2RadC = "0.017453f";

    /// <summary>表格单元格绑定的默认模拟值：50 + 45·sin（对应量程 0-100 的中值摆动）。</summary>
    public const string TableDefaultExpr = "50.0f + 45.0f * sinf";

    /// <summary>相位角取值上限（度）：相位落在 <c>[0, 360)</c>。</summary>
    public const int PhaseDegrees = 360;

    /// <summary>FNV-1a 32 位 offset basis。</summary>
    private const uint FnvOffsetBasis = 2166136261u;

    /// <summary>FNV-1a 32 位质数。</summary>
    private const uint FnvPrime = 16777619u;

    /// <summary>
    /// 模拟正弦波的相位角（度，<c>0..359</c>）—— 由控件 / 单元格标识稳定推导：
    /// **同一标识恒得同一相位**，跨进程、跨机器、跨次导出都一致。
    ///
    /// <para><b>为什么不用 <c>string.GetHashCode()</c></b>：.NET 的字符串哈希**每进程随机加盐**
    /// （<c>UseRandomizedStringHashAlgorithm</c>），用它推导相位会导致同一项目每次导出的
    /// 产物都不同 —— 破坏可复现构建、产物哈希比对与基于 diff 的回归门禁。
    /// 实测：同一份源码连跑两次，<c>sim_refresh_bindings</c> 里 20 行相位值各不相同。</para>
    ///
    /// <para><b>为什么 FNV-1a 之后还要收尾混淆</b>：FNV-1a 对「只有末字节不同」的短串
    /// （<c>c0</c>/<c>c1</c>/<c>c2</c>… 或单元格键 <c>tbl#0:0</c>/<c>tbl#0:1</c>）会产生
    /// **等差相位**：实测相邻相位差的取值只有 19/50 种，模拟曲线会呈现机械的同步骤动。
    /// 收尾混淆（lowbias32）把差异扩散到高位后升到 47/50，且 1000 个 id 的相位覆盖
    /// 340/360 个值（最大桶 8，理想 2.8）。</para>
    ///
    /// <para>哈希按 UTF-16LE 字节流喂入（低字节在前），故中文标识也稳定。</para>
    /// </summary>
    /// <param name="key">控件 Id 或单元格标识。null / 空串按其 FNV-1a 空输入处理，不抛异常。</param>
    public static int PhaseFor(string? key)
    {
        unchecked
        {
            var hash = FnvOffsetBasis;
            foreach (var ch in key ?? string.Empty)
            {
                hash = (hash ^ (byte)ch) * FnvPrime;
                hash = (hash ^ (byte)(ch >> 8)) * FnvPrime;
            }

            // 收尾混淆（lowbias32）：消除短串的等差规律，把变化扩散到高位。
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;

            return (int)(hash % (uint)PhaseDegrees);
        }
    }

    /// <summary>
    /// 表格单元格的模拟相位 —— 行 / 列参与散列，保证同屏单元格相位互相错开。
    ///
    /// <para>键用 <c>#</c> / <c>:</c> 分隔而非数值相加，避免歧义碰撞
    /// （<c>"a1" + 17</c> 与 <c>"a" + 117</c> 曾经会拼成同一个串）。</para>
    /// </summary>
    public static int PhaseForCell(string? controlId, int row, int col)
        => PhaseFor($"{controlId}#{row}:{col}");
}
