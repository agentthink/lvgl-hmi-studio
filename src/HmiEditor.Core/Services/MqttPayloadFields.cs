using System;
using System.Collections.Generic;

namespace HmiEditor.Core.Services;

/// <summary>
/// **契约 payload 的字段切分**（M1/M2 共用）：<c>k=v;k=v</c> → 字典。
///
/// <para>三份解析器（值/报警/状态）如果各写一遍切分，就会出现"某个键的容错口径不一致"这类漂移
/// —— 本仓的规矩是**同源接缝、不另抄一份**（与 `JudgeReadings`、`SourceScanner` 同一处置）。</para>
///
/// <para>口径：按 <c>;</c> 切；空段忽略；<c>k=v</c> 里 <c>k</c> 为空或没有 <c>=</c> ⇒ 整体失败；
/// **重复键后者覆盖前者**（与设备侧"后写赢"的实际行为一致）；**未知键保留在字典里**（解析器各自忽略）。</para>
/// </summary>
internal static class MqttPayloadFields
{
    public static bool TryParse(string? payload, out Dictionary<string, string> map)
    {
        map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(payload)) return false;
        // ⚠ **不**要求含 `;`：遗嘱 `on=0` 就是**单个** k=v（阶段 272 实测踩到过这里）——
        //    "裸数值"由各解析器自己处理：值契约先判 legacy（`!Contains(';')` ⇒ 当数值解），
        //    报警/状态则因为缺 `=` 自然失败。

        foreach (var part in payload.Trim().Split(';'))
        {
            if (part.Length == 0) continue;
            var eq = part.IndexOf('=');
            if (eq <= 0) return false;
            map[part[..eq]] = part[(eq + 1)..];
        }

        return map.Count > 0;
    }
}
