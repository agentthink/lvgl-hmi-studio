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
/// CodeGenerator 分部 —— 报警发射 —— 变量报警分组聚合 + 报警管理 C 代码。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>搁置时长下界/上界的毫秒字面量 —— **单源**：<see cref="AlarmShelvePolicy"/>。</summary>
    private static readonly string MinShelveMs =
        ((long)AlarmShelvePolicy.MinDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc cref="MinShelveMs"/>
    private static readonly string MaxShelveMs =
        ((long)AlarmShelvePolicy.MaxDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 生成报警管理 C 代码。
    /// </summary>
    /// <summary>
    /// 将各变量的报警配置（Variable.Alarms）聚合成一个"变量报警"分组，
    /// 与工程级 AlarmGroups 合并后一起生成代码。
    /// 变量报警以变量名为组，报警 ID 保持原样（避免跨变量冲突）。
    /// </summary>
    public static IReadOnlyList<AlarmGroup> MergeVariableAlarmGroups(HmiProject project)
    {
        var varAlarms = project.VariableManager.Variables
            .Where(v => v.Alarms is { Count: > 0 })
            .Select(v => new AlarmGroup
            {
                Id = $"var_{v.Id}",
                Name = $"{v.Name} 报警",
                Alarms = new System.Collections.ObjectModel.ObservableCollection<AlarmConfig>(
                    v.Alarms.Select(a => a.ToAlarmConfig(v.Id))),
            })
            .Where(g => g.Alarms.Any(a => a.IsEnabled))
            .ToList();

        if (varAlarms.Count == 0) return project.AlarmGroups;

        // 合并：工程级分组在前，变量报警分组在后
        var merged = new List<AlarmGroup>(project.AlarmGroups);
        merged.AddRange(varAlarms);
        return merged;
    }

    public string GenerateAlarmCode(IReadOnlyList<AlarmGroup> groups)
    {
        var sb = new StringBuilder();
        // 生成 C 浮点字面量：整数值补充小数部分，避免非标准的「80f」写法（严格编译器报错）。
        static string F(double v)
        {
            var s = v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (s.Length == 0) s = "0";
            if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E')) s += ".0";
            return s + "f";
        }
        sb.AppendLine("// ── Alarm Management ──");
        sb.AppendLine($"// Groups: {groups.Count}, Total alarms: {groups.Sum(g => g.Alarms.Count)}");
        sb.AppendLine();

        if (groups.Count == 0 || groups.All(g => g.Alarms.Count == 0))
        {
            sb.AppendLine("// No alarms configured.");
            return sb.ToString();
        }

        sb.AppendLine("#include \"lvgl.h\"");
        sb.AppendLine("#include <math.h>");
        sb.AppendLine("#include <stdio.h>");
        sb.AppendLine("#include <string.h>");
        sb.AppendLine();
        // 报警引擎排查日志：编译期由 ALARM_TRACE_ENABLE 控制（默认关闭，避免生产刷屏）。
        // 排查时在生成代码顶部定义 ALARM_TRACE_ENABLE 1 即可启用详细状态迁移日志。
        sb.AppendLine("#ifndef ALARM_TRACE_ENABLE");
        sb.AppendLine("#define ALARM_TRACE_ENABLE 0");
        sb.AppendLine("#endif");
        sb.AppendLine("#define ALARM_TRACE(fmt, ...) do { \\");
        sb.AppendLine("    if (ALARM_TRACE_ENABLE) printf(\"[alarm] \" fmt \"\\n\", ##__VA_ARGS__); \\");
        sb.AppendLine("} while (0)");
        sb.AppendLine();

        // 搁置（Shelving，阶段 276；契约 docs/2026-10-01-alarm-shelving-contract.md）——
        // 权限判定用固件的 hmi_has_permission（这里 extern 一次，与模拟器生成段的同款声明兼容）。
        sb.AppendLine("extern void hmi_audit(const char* event, const char* user, const char* role, const char* detail);");
        sb.AppendLine("extern int hmi_has_permission(unsigned int bits);");
        sb.AppendLine("extern int hmi_require_permission(unsigned int bits);");
        sb.AppendLine();
        sb.AppendLine("/* 搁置时长上下界 —— **与编辑器 AlarmShelvePolicy 同源**（由生成器发射，别手改） */");
        sb.AppendLine($"#define HMI_ALARM_SHELVE_MIN_MS {MinShelveMs}ULL");
        sb.AppendLine($"#define HMI_ALARM_SHELVE_MAX_MS {MaxShelveMs}ULL");
        sb.AppendLine();

        // AlarmRecord struct
        sb.AppendLine("typedef struct {");
        sb.AppendLine("    uint16_t config_id;");
        sb.AppendLine("    volatile uint8_t state;    // 0=Inactive,1=Active,2=Acknowledged,3=Returned,4=Shelved(搁置中，停止播报)");
        sb.AppendLine("    uint8_t severity;           // 0=Info,1=Warning,2=Critical,3=Emergency");
        sb.AppendLine("    float trigger_value;");
        sb.AppendLine("    uint32_t occurred_at;");
        sb.AppendLine("    uint32_t acknowledged_at;");
        sb.AppendLine("    uint32_t returned_at;");
        sb.AppendLine("    uint32_t pending_since;   // 延时触发的挂起起点(0=未挂起)，仅 DelayMs>0 使用");
        sb.AppendLine("    uint32_t shelved_until;   // 搁置到期(开机时长 ms)；判到期用 (int32_t)(now-until)>=0 防 tick 回绕");
        sb.AppendLine("    uint8_t  shelved_under;   // 搁置前的对外状态 —— 解除/到期时恢复到它");
        sb.AppendLine("} alarm_record_t;");
        sb.AppendLine();

        // Alarm records and check functions
        foreach (var group in groups)
        {
            sb.AppendLine($"// Group: {EscapeComment(group.Name)}");
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"static alarm_record_t {CIdentifier.AlarmRecord(alarm.Id)} = {{0}};");
                sb.AppendLine($"static void {CIdentifier.AlarmCheckFn(alarm.Id)}(float val);");
            }
            sb.AppendLine();
        }

        // Check function implementations
        int alarmSeq = 0;   // 遍历序即 config_id（与注册表/确认函数对齐）
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                int cfgId = alarmSeq++;
                // 报警 ID/名称是用户可编辑文本，会进入 ALARM_TRACE 的 printf 格式串：
                // 必须同时做 C 字符串转义与 % 转义（名称含 % 或引号时旧实现会产出
                // 非法转换说明符 / 提前结束字符串字面量）。
                var alarmIdEsc = EscapeFormatLiteral(alarm.Id);
                var alarmNameEsc = EscapeFormatLiteral(alarm.Name);
                sb.AppendLine($"static void {CIdentifier.AlarmCheckFn(alarm.Id)}(float val) {{");
                sb.AppendLine($"    alarm_record_t* r = &{CIdentifier.AlarmRecord(alarm.Id)};");
                sb.AppendLine($"    r->config_id = {cfgId};   /* 每次求值保持 config_id 一致 */");
                sb.AppendLine("    /* 搁置中：**不评估、不改对外状态**（搁置＝停止播报）；到期则恢复到搁置前的状态 */");
                sb.AppendLine("    if (r->state == 4) {");
                sb.AppendLine("        if ((int32_t)(lv_tick_get() - r->shelved_until) >= 0) {");
                sb.AppendLine("            r->state = r->shelved_under;");
                sb.AppendLine("            r->shelved_until = 0;");
                sb.AppendLine($"            ALARM_TRACE(\"[{EscapeFormatLiteral(alarm.Id)}/{EscapeFormatLiteral(alarm.Name)}] shelve expired -> resumed state %d\", (int)r->state);");
                sb.AppendLine($"            hmi_audit(\"alarm-unshelve-expired\", NULL, \"\", \"{EscapeFormatLiteral(alarm.Id)} expired\");");
                sb.AppendLine("        } else {");
                sb.AppendLine("            return;   /* 仍在搁置期 */");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("    uint8_t triggered = 0;");
                sb.AppendLine("    uint8_t cleared   = 0;");
                sb.AppendLine();
                // triggered=进入报警的条件；cleared=解除(恢复到阈值内死区)的条件。
                // 死区用于防抖动：激活越过阈值即可，回归必须跌破 Threshold±Deadband。
                sb.AppendLine("    switch (" + (int)alarm.Condition.Type + ") {");
                sb.AppendLine($"        case 0: triggered = (val >= {F(alarm.Condition.Threshold)});   cleared = (val <= {F(alarm.Condition.Threshold - alarm.Condition.Deadband)}); break;");
                sb.AppendLine($"        case 1: triggered = (val <= {F(alarm.Condition.Threshold)});   cleared = (val >= {F(alarm.Condition.Threshold + alarm.Condition.Deadband)}); break;");
                sb.AppendLine($"        case 2: triggered = ((uint32_t)val >> {alarm.Condition.BitOffset}) & 1; cleared = !triggered; break;");
                sb.AppendLine($"        case 3:");
                sb.AppendLine($"        case 4: triggered = (fabsf(val - {F(alarm.Condition.ExpectedValue)}) > {F(alarm.Condition.Threshold)});   cleared = (fabsf(val - {F(alarm.Condition.ExpectedValue)}) <= {F(alarm.Condition.Threshold - alarm.Condition.Deadband)}); break;");
                sb.AppendLine($"        case 5: triggered = (val >= {F(alarm.Condition.Threshold)});   cleared = (val <= {F(alarm.Condition.Threshold - alarm.Condition.Deadband)}); break;  /* HH 上上限 */");
                sb.AppendLine($"        case 6: triggered = (val <= {F(alarm.Condition.Threshold)});   cleared = (val >= {F(alarm.Condition.Threshold + alarm.Condition.Deadband)}); break;  /* LL 下下限 */");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    if (triggered) {");
                sb.AppendLine("        if (r->state == 0) {");
                if (alarm.Condition.DelayMs > 0)
                {
                    // 延时触发：条件持续满足 DelayMs 毫秒才真正触发 Active
                    sb.AppendLine("            uint32_t now = lv_tick_get();");
                    sb.AppendLine($"            if (r->pending_since == 0) {{ r->pending_since = now; ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] condition met, pending {alarm.Condition.DelayMs}ms (t=%lu)\", (unsigned long)now); }}");
                    sb.AppendLine($"            else if ((uint32_t)(now - r->pending_since) >= {alarm.Condition.DelayMs}u) {{");
                    sb.AppendLine("                r->state = 1;");
                    sb.AppendLine("                r->severity = " + (int)alarm.Severity + ";");
                    sb.AppendLine("                r->trigger_value = val;");
                    sb.AppendLine("                r->occurred_at = now;");
                    sb.AppendLine("                r->pending_since = 0;");
                    sb.AppendLine($"                ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] delay elapsed -> Active (state %d->1, severity={(int)alarm.Severity}, t=%lu)\", r->state - 1, (unsigned long)r->occurred_at);");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine("            r->state = 1;");
                    sb.AppendLine("            r->severity = " + (int)alarm.Severity + ";");
                    sb.AppendLine("            r->trigger_value = val;");
                    sb.AppendLine("            r->occurred_at = lv_tick_get();");
                    sb.AppendLine($"            ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] triggered val=%.3f -> Active (state %d->1, severity={(int)alarm.Severity}, t=%lu)\", val, r->state - 1, (unsigned long)r->occurred_at);");
                }
                sb.AppendLine("        }");
                sb.AppendLine("    } else {");
                sb.AppendLine("        if (r->state == 0 && r->pending_since != 0) {");
                sb.AppendLine("            r->pending_since = 0;   /* 条件未持续满足，取消延时挂起 */");
                sb.AppendLine($"            ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] condition un-met during delay -> cancel pending\");");
                sb.AppendLine("        }");
                sb.AppendLine("        if (cleared) {");
                sb.AppendLine("        if (r->state == 1) {");
                if (alarm.RequiresAck)
                {
                    sb.AppendLine("            r->state = 2; // Active -> Acknowledged (auto on condition clear)");
                    sb.AppendLine($"            ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] condition clear val=%.3f -> Acknowledged (state %d->2, t=%lu)\", val, r->state - 1, (unsigned long)lv_tick_get());");
                }
                else
                {
                    sb.AppendLine("            r->state = 3; // Active -> Returned (no ack required)");
                    sb.AppendLine("            r->returned_at = lv_tick_get();");
                    sb.AppendLine($"            ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] condition clear val=%.3f -> Returned (state %d->3, t=%lu)\", val, r->state - 1, (unsigned long)r->returned_at);");
                }
                sb.AppendLine("        } else if (r->state == 2) {");
                sb.AppendLine("            r->state = 3;");
                sb.AppendLine("            r->returned_at = lv_tick_get();");
                sb.AppendLine($"            ALARM_TRACE(\"[{alarmIdEsc}/{alarmNameEsc}] condition clear after ack -> Returned (state %d->3, t=%lu)\", r->state - 1, (unsigned long)r->returned_at);");
                sb.AppendLine("        }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        // Engine evaluate function
        // 边界保护：var_values 为 NULL 或 var_count 不足时，越界报警本次跳过求值，
        // 保持上次状态避免读取越界内存（此前直接 var_values[varIndex] 存在 OOB 风险）。
        sb.AppendLine("void alarm_engine_evaluate(float* var_values, int var_count) {");
        sb.AppendLine("    if (var_values == NULL || var_count <= 0) return;");
        int varIndex = 0;
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"    if ({varIndex} < var_count) {CIdentifier.AlarmCheckFn(alarm.Id)}(var_values[{varIndex}]);");
                varIndex++;
            }
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Engine reset function
        // 冷启动/热重载时复位所有报警记录，使其回到未激活且无延时挂起状态。
        sb.AppendLine("void alarm_engine_init(void) {");
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"    memset(&{CIdentifier.AlarmRecord(alarm.Id)}, 0, sizeof({CIdentifier.AlarmRecord(alarm.Id)}));");
            }
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Active count query
        sb.AppendLine("uint16_t alarm_get_active_count(void) {");
        sb.AppendLine("    uint16_t n = 0;");
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"    if ({CIdentifier.AlarmRecord(alarm.Id)}.state == 1) n++;");
            }
        }
        sb.AppendLine("    return n;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Acknowledge function
        // 注册表把 config_id 映射到对应记录，使 alarm_acknowledge 能真正确认报警
        // （此前为空实现 return 0，无法完成 Active->Acknowledged 确认）。
        sb.AppendLine("typedef struct { uint16_t config_id; alarm_record_t* rec; } alarm_lookup_t;");
        sb.AppendLine("static const alarm_lookup_t _alarm_lookup[] = {");
        int lookupSeq = 0;
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"    {{ {lookupSeq++}, &{CIdentifier.AlarmRecord(alarm.Id)} }},");
            }
        }
        sb.AppendLine("    { 0xFFFF, NULL }");
        sb.AppendLine("};");
        sb.AppendLine();

        // 逐条快照（阶段 291；M2 报警上行的数据源）：
        //   · `_alarm_ids` 与 `_alarm_lookup` **同序**（同一个"启用报警"遍历序）⇒ config_id 口径一致；
        //   · id **定长拷贝**进调用方缓冲（不把 .so 的字符串指针交给宿主，热更新/卸载不耦合）；
        //   · `state == 0`（Inactive）照实返回 —— 是否上行由宿主按契约的 1..4 决定。
        sb.AppendLine("static const char* const _alarm_ids[] = {");
        foreach (var group in groups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!alarm.IsEnabled) continue;
                sb.AppendLine($"    \"{EscapeCString(alarm.Id)}\",");
            }
        }
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("uint16_t alarm_snapshot(hmi_alarm_snapshot_t* out, uint16_t cap) {");
        sb.AppendLine("    if (out == NULL) return 0;");
        sb.AppendLine("    uint16_t n = 0;");
        sb.AppendLine("    for (uint16_t i = 0; _alarm_lookup[i].rec != NULL; i++) {");
        sb.AppendLine("        if (n >= cap) break;   /* 写不下就停，不写半条；调用方按返回的 n 用 */");
        sb.AppendLine("        strncpy(out[n].id, _alarm_ids[i], sizeof(out[n].id) - 1);");
        sb.AppendLine("        out[n].id[sizeof(out[n].id) - 1] = '\\0';   /* strncpy 不保证终止 */");
        sb.AppendLine("        out[n].config_id = _alarm_lookup[i].config_id;");
        sb.AppendLine("        out[n].state     = _alarm_lookup[i].rec->state;");
        sb.AppendLine("        out[n].severity  = _alarm_lookup[i].rec->severity;");
        sb.AppendLine("        n++;");
        sb.AppendLine("    }");
        sb.AppendLine("    return n;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("uint8_t alarm_acknowledge(uint16_t config_id) {");
        sb.AppendLine("    for (uint16_t i = 0; _alarm_lookup[i].rec != NULL; i++) {");
        sb.AppendLine("        if (_alarm_lookup[i].config_id == config_id) {");
        sb.AppendLine("            alarm_record_t* r = _alarm_lookup[i].rec;");
        sb.AppendLine("            if (r->state == 4) {   /* 搁置中不允许确认（契约 §4 注 6）——必须先解除搁置 */");
        sb.AppendLine("                ALARM_TRACE(\"[config id=%u] acknowledge denied: shelved\", (unsigned)config_id);");
        sb.AppendLine("                return 0;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (r->state == 1) {   /* 仅 Active 可被确认 */");
        sb.AppendLine("                r->state = 2;");
        sb.AppendLine("                r->acknowledged_at = lv_tick_get();");
        sb.AppendLine("                ALARM_TRACE(\"[config id=%u] acknowledged -> Acknowledged (t=%lu)\", (unsigned)config_id, (unsigned long)r->acknowledged_at);");
        sb.AppendLine("                return 1;");
        sb.AppendLine("            }");
        sb.AppendLine("            return 0;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    ALARM_TRACE(\"[config id=%u] acknowledge failed: not found\", (unsigned)config_id);");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();

        // 搁置 / 解除搁置（阶段 276）：权限与时长在**设备侧再判一次** —— 界面判过不代表设备可信。
        sb.AppendLine("uint8_t alarm_shelve(uint16_t config_id, uint32_t duration_ms, const char* by) {");
        sb.AppendLine("    char _audit_detail[128];   /* 审计明细拼在这（不动态分配） */");
        sb.AppendLine("    if (!hmi_has_permission(HMI_PERM_SHELVE_ALARM)) {");
        sb.AppendLine("        hmi_audit(\"alarm-shelve-denied\", by, \"\", \"no permission\");");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    if ((unsigned long long)duration_ms < HMI_ALARM_SHELVE_MIN_MS ||");
        sb.AppendLine("        (unsigned long long)duration_ms > HMI_ALARM_SHELVE_MAX_MS) {");
        sb.AppendLine("        ALARM_TRACE(\"[config id=%u] shelve denied: duration out of range\", (unsigned)config_id);");
        sb.AppendLine("        hmi_audit(\"alarm-shelve-denied\", by, \"\", \"duration out of range\");");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    }");
        sb.AppendLine("    for (uint16_t i = 0; _alarm_lookup[i].rec != NULL; i++) {");
        sb.AppendLine("        if (_alarm_lookup[i].config_id != config_id) continue;");
        sb.AppendLine("        alarm_record_t* r = _alarm_lookup[i].rec;");
        sb.AppendLine("        if (r->state == 0 || r->state == 3 || r->state == 4) {   /* 不可搁置 / 不需要搁置 / 已搁置 */");
        sb.AppendLine("            snprintf(_audit_detail, sizeof(_audit_detail), \"state=%d\", (int)r->state);");
        sb.AppendLine("            hmi_audit(\"alarm-shelve-denied\", by, \"\", _audit_detail);");
        sb.AppendLine("            return 0;");
        sb.AppendLine("        }");
        sb.AppendLine("        r->shelved_under = r->state;");
        sb.AppendLine("        r->shelved_until = lv_tick_get() + duration_ms;");
        sb.AppendLine("        r->state = 4;");
        sb.AppendLine("        snprintf(_audit_detail, sizeof(_audit_detail), \"%lu ms under=%d\", (unsigned long)duration_ms, (int)r->shelved_under);");
        sb.AppendLine("        hmi_audit(\"alarm-shelve\", by, \"\", _audit_detail);");
        sb.AppendLine("        return 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    ALARM_TRACE(\"[config id=%u] shelve failed: not found\", (unsigned)config_id);");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("uint8_t alarm_unshelve(uint16_t config_id, const char* by) {");
        sb.AppendLine("    char _audit_detail[128];");
        sb.AppendLine("    for (uint16_t i = 0; _alarm_lookup[i].rec != NULL; i++) {");
        sb.AppendLine("        if (_alarm_lookup[i].config_id != config_id) continue;");
        sb.AppendLine("        alarm_record_t* r = _alarm_lookup[i].rec;");
        sb.AppendLine("        if (r->state != 4) {");
        sb.AppendLine("            ALARM_TRACE(\"[config id=%u] unshelve denied: not shelved\", (unsigned)config_id);");
        sb.AppendLine("            hmi_audit(\"alarm-unshelve-denied\", by, \"\", \"not shelved\");");
        sb.AppendLine("            return 0;");
        sb.AppendLine("        }");
        sb.AppendLine("        r->state = r->shelved_under;");
        sb.AppendLine("        r->shelved_until = 0;");
        sb.AppendLine("        snprintf(_audit_detail, sizeof(_audit_detail), \"Manual -> state=%d\", (int)r->state);");
        sb.AppendLine("        hmi_audit(\"alarm-unshelve\", by, \"\", _audit_detail);");
        sb.AppendLine("        return 1;");
        sb.AppendLine("    }");
        sb.AppendLine("    return 0;");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
