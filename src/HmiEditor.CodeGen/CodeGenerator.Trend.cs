using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— 趋势图与数据记录发射 —— lv_chart + 静态环形缓冲区。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成趋势图 + 数据记录 C 代码。
    /// 基于 LVGL Chart (lv_chart) + 静态环形缓冲区。
    /// </summary>
    public string GenerateTrendCode(IReadOnlyList<TrendConfig> charts, IReadOnlyList<DataLogConfig> loggers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ── Trend Charts & Data Logging ──");
        sb.AppendLine($"// Charts: {charts.Count}, Loggers: {loggers.Count}");
        sb.AppendLine();

        if (charts.Count == 0 && loggers.Count == 0)
        {
            sb.AppendLine("// No trend charts or data loggers configured.");
            return sb.ToString();
        }

        sb.AppendLine("#include \"lvgl.h\"");
        sb.AppendLine("#include <string.h>");
        sb.AppendLine("#include <stdio.h>");
        sb.AppendLine();
        sb.AppendLine("#define DATA_LOG_MAX_VARS 16");
        sb.AppendLine();
        // 趋势/数据记录排查日志：编译期由 TREND_TRACE_ENABLE 控制（默认关闭）。
        // 排查时定义 TREND_TRACE_ENABLE 1 即可查看环形缓冲回绕/停止与图表守卫事件。
        sb.AppendLine("#ifndef TREND_TRACE_ENABLE");
        sb.AppendLine("#define TREND_TRACE_ENABLE 0");
        sb.AppendLine("#endif");
        sb.AppendLine("#define TREND_TRACE(fmt, ...) do { \\");
        sb.AppendLine("    if (TREND_TRACE_ENABLE) printf(\"[trend] \" fmt \"\\n\", ##__VA_ARGS__); \\");
        sb.AppendLine("} while (0)");
        sb.AppendLine();

        // Data log ring buffers
        var maxRecords = loggers.Count > 0 ? loggers.Max(l => l.MaxRecords) : 100;
        sb.AppendLine($"#define DATA_LOG_MAX_RECORDS {maxRecords}");
        sb.AppendLine();
        sb.AppendLine("typedef struct {");
        sb.AppendLine("    uint32_t timestamp_ms;");
        sb.AppendLine("    float values[DATA_LOG_MAX_VARS];");
        sb.AppendLine("    uint8_t var_count;");
        sb.AppendLine("    uint8_t is_valid;");
        sb.AppendLine("} data_log_sample_t;");
        sb.AppendLine();

        // Logger instances
        foreach (var logger in loggers)
        {
            if (!logger.IsEnabled) continue;
            sb.AppendLine($"static data_log_sample_t {CIdentifier.LogBuffer(logger.Id)}[{logger.MaxRecords}];");
            sb.AppendLine($"static volatile uint16_t {CIdentifier.LogWriteIndex(logger.Id)} = 0;");
            sb.AppendLine($"static volatile uint16_t {CIdentifier.LogCount(logger.Id)} = 0;");
            sb.AppendLine($"static volatile uint8_t {CIdentifier.LogFullFlag(logger.Id)} = 0;");
            sb.AppendLine();
        }

        // Logger init/sample functions
        foreach (var logger in loggers)
        {
            if (!logger.IsEnabled) continue;
            sb.AppendLine($"void {CIdentifier.DataLogInitFn(logger.Id)}(void) {{");
            sb.AppendLine($"    {CIdentifier.LogWriteIndex(logger.Id)} = 0;");
            sb.AppendLine($"    {CIdentifier.LogCount(logger.Id)} = 0;");
            sb.AppendLine($"    {CIdentifier.LogFullFlag(logger.Id)} = 0;");
            sb.AppendLine($"    memset({CIdentifier.LogBuffer(logger.Id)}, 0, sizeof({CIdentifier.LogBuffer(logger.Id)}));");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine($"void {CIdentifier.DataLogSampleFn(logger.Id)}(float* values, uint8_t count) {{");
            sb.AppendLine("    if (values == NULL || count == 0) return;   /* 无有效数据不入环形缓冲 */");
            sb.AppendLine($"    data_log_sample_t* s = &{CIdentifier.LogBuffer(logger.Id)}[{CIdentifier.LogWriteIndex(logger.Id)}];");
            sb.AppendLine("    s->timestamp_ms = lv_tick_get();");
            sb.AppendLine("    s->var_count = (count > DATA_LOG_MAX_VARS) ? DATA_LOG_MAX_VARS : count;");
            sb.AppendLine("    s->is_valid = 1;");
            sb.AppendLine("    memcpy(s->values, values, s->var_count * sizeof(float));");
            sb.AppendLine();
            sb.AppendLine($"    {CIdentifier.LogWriteIndex(logger.Id)}++;");
            sb.AppendLine($"    if ({CIdentifier.LogWriteIndex(logger.Id)} >= {logger.MaxRecords}) {{");
            sb.AppendLine($"        {CIdentifier.LogWriteIndex(logger.Id)} = 0;");
            sb.AppendLine($"        if (!{CIdentifier.LogFullFlag(logger.Id)}) TREND_TRACE(\"log-%s ring filled (%d records) -> wrap modes\", \"{logger.Id}\", {logger.MaxRecords});");
            sb.AppendLine($"        {CIdentifier.LogFullFlag(logger.Id)} = 1;");
            if (logger.FullPolicy == LogFullPolicy.StopWhenFull)
            {
                sb.AppendLine($"        TREND_TRACE(\"log-{logger.Id} policy=StopWhenFull, logged=%d records, sampling stops\", {CIdentifier.LogCount(logger.Id)});");
                sb.AppendLine("        return;");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    if (!{CIdentifier.LogFullFlag(logger.Id)}) {CIdentifier.LogCount(logger.Id)}++;");
            sb.AppendLine("}");
            sb.AppendLine();

            // CSV 导出：按时间顺序把环形缓冲采样写出行（callback 写入），供 SD 卡/串口记录。
            // ⚠ 分隔符取工程配置（`DataLogConfig.CsvDelimiter`）——**不再硬编码逗号**：
            //   该字段的文档写着「CSV 导出分隔符」，而设备 CSV 是设备侧唯一的 CSV 出口；
            //   硬编码会让这个设置在设备上完全无效（阶段 176 实测 + 修）。
            var delim = EscapeCString(logger.CsvDelimiter ?? ",");
            if (delim.Length == 0) delim = ",";
            sb.AppendLine($"void {CIdentifier.DataLogExportCsvFn(logger.Id)}(void (*write_fn)(const char*)) {{");
            sb.AppendLine("    if (!write_fn) return;");
            sb.AppendLine($"    uint16_t total = {CIdentifier.LogFullFlag(logger.Id)} ? {logger.MaxRecords} : {CIdentifier.LogCount(logger.Id)};");
            sb.AppendLine($"    uint16_t start = {CIdentifier.LogFullFlag(logger.Id)} ? {CIdentifier.LogWriteIndex(logger.Id)} : 0;");
            sb.AppendLine("    char buf[128];");
            sb.AppendLine("    write_fn(\"timestamp_ms\");");
            int varNameIdx = 0;
            foreach (var varId in logger.VariableIds)
            {
                sb.AppendLine($"    write_fn(\"{delim}{varId}\");");
                varNameIdx++;
            }
            if (varNameIdx == 0)
            {
                sb.AppendLine($"    write_fn(\"{delim}v0\");   /* 未绑定变量名时按列序输出 */");
            }
            sb.AppendLine("    write_fn(\"\\n\");");
            sb.AppendLine();
            sb.AppendLine($"    for (uint16_t i = 0; i < total; i++) {{");
            sb.AppendLine($"        uint16_t idx = (uint16_t)((start + i) % {logger.MaxRecords});");
            sb.AppendLine($"        data_log_sample_t* s = &{CIdentifier.LogBuffer(logger.Id)}[idx];");
            sb.AppendLine("        if (!s->is_valid) continue;");
            sb.AppendLine("        snprintf(buf, sizeof(buf), \"%lu\", (unsigned long)s->timestamp_ms);");
            sb.AppendLine("        write_fn(buf);");
            sb.AppendLine("        for (uint8_t j = 0; j < s->var_count; j++) {");
            sb.AppendLine($"            snprintf(buf, sizeof(buf), \"{delim}%.3f\", (double)s->values[j]);");
            sb.AppendLine("            write_fn(buf);");
            sb.AppendLine("        }");
            sb.AppendLine("        write_fn(\"\\n\");");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Trend chart init/update
        foreach (var chart in charts)
        {
            if (!chart.IsEnabled) continue;
            var enabledPens = chart.Pens.Where(p => p.IsEnabled).ToList();
            var chartIdEsc = EscapeFormatLiteral(chart.Id);
            var chartObj = CIdentifier.ChartObject(chart.Id);
            var chartSeries = CIdentifier.ChartSeries(chart.Id);
            sb.AppendLine($"// Chart: {EscapeComment(chart.Name)} ({enabledPens.Count} enabled pens, {chart.MaxPoints} points)");
            sb.AppendLine($"static lv_obj_t* {chartObj} = NULL;");
            /* 每个启用的曲线笔对应一个 series 指针；lv_chart_set_next_value
             * 要求传有效的 series，不能为 NULL，否则 LVGL 内部解引用空指针崩溃。 */
            sb.AppendLine($"static lv_chart_series_t* {chartSeries}[{Math.Max(1, enabledPens.Count)}] = {{0}};");
            sb.AppendLine();

            sb.AppendLine($"void {CIdentifier.TrendInitFn(chart.Id)}(lv_obj_t* parent) {{");
            sb.AppendLine($"    {chartObj} = lv_chart_create(parent);");
            sb.AppendLine($"    lv_obj_set_size({chartObj}, LV_PCT(100), LV_PCT(100));");
            sb.AppendLine($"    lv_chart_set_type({chartObj}, LV_CHART_TYPE_LINE);");
            sb.AppendLine($"    lv_chart_set_point_count({chartObj}, {chart.MaxPoints});");

            if (chart.ShowGrid)
                sb.AppendLine($"    lv_chart_set_div_line_count({chartObj}, 5, 5);");

            if (!string.IsNullOrEmpty(chart.BgColor))
            {
                var hex = chart.BgColor.TrimStart('#');
                sb.AppendLine($"    lv_obj_set_style_bg_color({chartObj}, lv_color_hex(0x{hex}), 0);");
            }

            sb.AppendLine();

            // Series
            int penIdx = 0;
            foreach (var pen in chart.Pens)
            {
                if (!pen.IsEnabled) continue;
                var color = pen.Color.TrimStart('#');
                sb.AppendLine($"    // Pen {penIdx}: {EscapeComment(pen.Label)} ({color})");
                sb.AppendLine($"    {chartSeries}[{penIdx}] = lv_chart_add_series({chartObj}, lv_color_hex(0x{color}), LV_CHART_AXIS_PRIMARY_Y);");
                penIdx++;
            }

            sb.AppendLine($"    lv_chart_set_range({chartObj}, LV_CHART_AXIS_PRIMARY_X, 0, {chart.MaxPoints});");
            sb.AppendLine($"    lv_chart_refresh({chartObj});");
            sb.AppendLine("}");
            sb.AppendLine();

            // Update
            sb.AppendLine($"void {CIdentifier.TrendUpdateFn(chart.Id)}(float* values, uint8_t count) {{");
            sb.AppendLine($"    if ({chartObj} == NULL) {{ TREND_TRACE(\"chart-{chartIdEsc} not initialized, trend_update skipped\"); return; }}");
            if (enabledPens.Count == 0)
            {
                sb.AppendLine("    (void)values; (void)count;");
            }
            else
            {
                // 依次为每条启用的曲线推入对应数据槽（values[0..N]）；缺省保底，
                // 避免 series 为 NULL 时 lv_chart_set_next_value 内部解引用崩溃。
                sb.AppendLine($"    if (values == NULL) {{ TREND_TRACE(\"chart-{chartIdEsc} update with NULL values, skipped\"); lv_chart_refresh({chartObj}); return; }}");
                sb.AppendLine($"    uint8_t n = count < {enabledPens.Count} ? count : {enabledPens.Count};");
                sb.AppendLine($"    TREND_TRACE(\"chart-{chartIdEsc} update: n=%u values[0]=%.3f\", (unsigned)n, values[0]);");
                sb.AppendLine($"    for (uint8_t i = 0; i < n; i++) {{");
                sb.AppendLine($"        if ({chartSeries}[i]) lv_chart_set_next_value({chartObj}, {chartSeries}[i], values[i]);");
                sb.AppendLine("    }");
            }
            sb.AppendLine($"    lv_chart_refresh({chartObj});");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Engine entry
        sb.AppendLine("void trend_engine_init(void) {");
        foreach (var logger in loggers)
        {
            if (!logger.IsEnabled) continue;
            sb.AppendLine($"    {CIdentifier.DataLogInitFn(logger.Id)}();");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("void trend_engine_tick(float* values, uint8_t count) {");
        foreach (var logger in loggers)
        {
            if (!logger.IsEnabled) continue;
            sb.AppendLine($"    {CIdentifier.DataLogSampleFn(logger.Id)}(values, count);");
        }
        foreach (var chart in charts)
        {
            if (!chart.IsEnabled) continue;
            sb.AppendLine($"    {CIdentifier.TrendUpdateFn(chart.Id)}(values, count);");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }
}
