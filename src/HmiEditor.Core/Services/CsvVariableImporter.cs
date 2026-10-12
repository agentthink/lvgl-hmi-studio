using System.Globalization;
using System.Text;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>CSV 变量导入结果。</summary>
public record ImportResult(List<Variable> Variables, List<CsvImportIssue> Errors, int TotalRows, int SuccessRows)
{
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// CSV 变量导入器 —— 支持通用格式和西门子 TIA Portal 标签导出格式。
/// 通用格式（首行表头）：Name, DataType, Address, Unit, Scale, Offset, PollRateMs
/// TIA Portal 格式自动检测：Name, DataType, Address, Comment 映射
/// </summary>
public static class CsvVariableImporter
{
    /// <summary>
    /// 从 CSV 文本解析变量。
    /// 导入时通过 <paramref name="connectionId"/> / <paramref name="slaveId"/> 归属到指定连接/子设备。
    /// </summary>
    public static ImportResult Parse(string csvText, string connectionId = "", string slaveId = "")
    {
        var variables = new List<Variable>();
        var errors = new List<CsvImportIssue>();
        var lines = csvText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
        {
            errors.Add(new CsvImportIssue(CsvImportIssueCodes.TOO_FEW_LINES,
                "CSV 文件至少需要包含标题行和 1 行数据"));
            return new ImportResult(variables, errors, lines.Length, 0);
        }

        var headers = ParseCsvLine(lines[0]);
        var headerText = string.Join(", ", headers);
        var columnMap = BuildColumnMap(headers);

        if (columnMap.Count == 0)
        {
            errors.Add(new CsvImportIssue(CsvImportIssueCodes.NO_HEADER_COLUMNS,
                $"无法识别表头列，当前表头: {headerText}", 0, new object[] { headerText }));
            return new ImportResult(variables, errors, lines.Length, 0);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var fields = ParseCsvLine(line);
                var variable = new Variable
                {
                    ConnectionId = connectionId,
                    SlaveId = slaveId,
                };

                // Name (必填)
                if (columnMap.TryGetValue("name", out var nameIdx) && nameIdx < fields.Length)
                    variable.Name = fields[nameIdx].Trim();
                else
                {
                    errors.Add(new CsvImportIssue(CsvImportIssueCodes.ROW_MISSING_NAME,
                        "缺少 Name 列", i + 1));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    errors.Add(new CsvImportIssue(CsvImportIssueCodes.ROW_EMPTY_NAME,
                        "Name 为空", i + 1));
                    continue;
                }

                // DataType (必填)
                if (columnMap.TryGetValue("datatype", out var dtIdx) && dtIdx < fields.Length)
                    variable.DataType = fields[dtIdx].Trim();
                if (string.IsNullOrWhiteSpace(variable.DataType))
                    variable.DataType = "Int16"; // 默认

                // Address
                if (columnMap.TryGetValue("address", out var addrIdx) && addrIdx < fields.Length)
                    variable.Address = fields[addrIdx].Trim();

                // Unit (兼容 TIA Portal "Comment" 列)
                if (columnMap.TryGetValue("unit", out var unitIdx) && unitIdx < fields.Length)
                    variable.Unit = fields[unitIdx].Trim();
                else if (columnMap.TryGetValue("comment", out var commentIdx) && commentIdx < fields.Length)
                    variable.Unit = fields[commentIdx].Trim();

                // Scale
                double scale = variable.Scale;
                if (columnMap.TryGetValue("scale", out var scaleIdx) && scaleIdx < fields.Length)
                    double.TryParse(fields[scaleIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out scale);
                variable.Scale = scale;

                // Offset
                double offset = variable.Offset;
                if (columnMap.TryGetValue("offset", out var offIdx) && offIdx < fields.Length)
                    double.TryParse(fields[offIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out offset);
                variable.Offset = offset;

                // PollRateMs
                int pollRate = variable.PollRateMs;
                if (columnMap.TryGetValue("pollratems", out var pollIdx) && pollIdx < fields.Length)
                    int.TryParse(fields[pollIdx].Trim(), out pollRate);
                variable.PollRateMs = pollRate > 0 ? pollRate : 500;

                variables.Add(variable);
            }
            catch (Exception ex)
            {
                errors.Add(new CsvImportIssue(CsvImportIssueCodes.ROW_PARSE_FAILED,
                    $"行解析失败: {ex.Message}", i + 1, new object[] { ex.Message }));
            }
        }

        return new ImportResult(variables, errors, lines.Length - 1, variables.Count);
    }

    /// <summary>
    /// 将变量列表导出为 CSV 文本（含表头：Name, DataType, Address, Unit, Scale, Offset, PollRateMs）。
    /// 与 <see cref="Parse"/> 的通用格式对称，可无缝回导。
    /// </summary>
    public static string Export(IEnumerable<Variable> variables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,DataType,Address,Unit,Scale,Offset,PollRateMs");
        foreach (var v in variables)
        {
            var fields = new[]
            {
                CsvEscape(v.Name),
                v.DataType,
                v.Address,
                CsvEscape(v.Unit ?? ""),
                v.Scale.ToString("0.###", CultureInfo.InvariantCulture),
                v.Offset.ToString("0.###", CultureInfo.InvariantCulture),
                v.PollRateMs.ToString(),
            };
            sb.AppendLine(string.Join(",", fields));
        }
        return sb.ToString();
    }

    /// <summary>CSV 字段转义：含逗号/引号/换行时加引号包裹。</summary>
    private static string CsvEscape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    /// <summary>解析单行 CSV（支持引号内逗号）。</summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    /// <summary>将表头映射到列索引，支持多种命名变体。</summary>
    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // 同义词表
        var synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { "name", "变量名", "variable", "tagname", "tag" },
            ["datatype"] = new[] { "datatype", "数据类型", "type", "data type", "data_type" },
            ["address"] = new[] { "address", "地址", "addr", "register" },
            ["unit"] = new[] { "unit", "单位", "comment", "描述", "description", "备注" },
            ["scale"] = new[] { "scale", "系数", "缩放", "gain", "factor" },
            ["offset"] = new[] { "offset", "偏移", "bias" },
            ["pollratems"] = new[] { "pollratems", "poll_rate_ms", "采样间隔", "采样周期", "interval" },
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim().Trim('"');
            foreach (var kvp in synonyms)
            {
                if (kvp.Value.Any(s => header.Equals(s, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!map.ContainsKey(kvp.Key))
                        map[kvp.Key] = i;
                    break;
                }
            }
        }
        return map;
    }
}
