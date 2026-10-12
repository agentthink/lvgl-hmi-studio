using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class AlarmEngineTests
{
    // 定时相关用例统一用 TestWait.Until（产品状态同步），不要用 Thread.Sleep 等定时器回调。
    // 原因与失败史见 Support/TestWait.cs。

    // ── AlarmConfig 模型测试 ──

    [Fact]
    public void AlarmConfig_DefaultValues_AreSane()
    {
        var config = new AlarmConfig();
        Assert.False(string.IsNullOrEmpty(config.Id));
        Assert.Equal(AlarmSeverity.Warning, config.Severity);
        Assert.True(config.RequiresAck);
        Assert.True(config.IsEnabled);
        Assert.NotNull(config.Condition);
    }

    [Fact]
    public void AlarmConfig_CanSetProperties()
    {
        var config = new AlarmConfig
        {
            Name = "电机过载",
            Description = "电机电流超过额定值 120%",
            Severity = AlarmSeverity.Critical,
            RequiresAck = false
        };
        Assert.Equal("电机过载", config.Name);
        Assert.Equal(AlarmSeverity.Critical, config.Severity);
        Assert.False(config.RequiresAck);
    }

    // ── AlarmCondition 模型测试 ──

    [Fact]
    public void AlarmCondition_DefaultType_IsHighLimit()
    {
        var condition = new AlarmCondition();
        Assert.Equal(AlarmConditionType.HighLimit, condition.Type);
    }

    // ── AlarmRecord 模型测试 ──

    [Fact]
    public void AlarmRecord_DefaultState_IsInactive()
    {
        var record = new AlarmRecord();
        Assert.Equal(AlarmState.Inactive, record.State);
    }

    [Fact]
    public void AlarmRecord_StateTransitions_AreValid()
    {
        var record = new AlarmRecord
        {
            AlarmName = "Test Alarm",
            State = AlarmState.Active,
            OccurredAt = DateTime.UtcNow
        };
        Assert.Equal(AlarmState.Active, record.State);

        // Acknowledge
        record.State = AlarmState.Acknowledged;
        record.AcknowledgedAt = DateTime.UtcNow;
        Assert.Equal(AlarmState.Acknowledged, record.State);
        Assert.NotNull(record.AcknowledgedAt);

        // Returned
        record.State = AlarmState.Returned;
        record.ReturnedAt = DateTime.UtcNow;
        Assert.Equal(AlarmState.Returned, record.State);
        Assert.NotNull(record.ReturnedAt);
    }

    // ── AlarmGroup 模型测试 ──

    [Fact]
    public void AlarmGroup_CanAddAlarms()
    {
        var group = new AlarmGroup { Name = "温度异常" };
        group.Alarms.Add(new AlarmConfig { Name = "高温报警" });
        group.Alarms.Add(new AlarmConfig { Name = "低温报警" });
        Assert.Equal(2, group.Alarms.Count);
    }

    // ── CheckConditionOnce（一次性条件检查）──

    [Fact]
    public void CheckConditionOnce_HighLimit_AboveThreshold_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.HighLimit,
            Threshold = 100.0
        };
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 150.0));
    }

    [Fact]
    public void CheckConditionOnce_HighLimit_AtThreshold_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.HighLimit,
            Threshold = 100.0
        };
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 100.0));
    }

    [Fact]
    public void CheckConditionOnce_HighLimit_BelowThreshold_ReturnsFalse()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.HighLimit,
            Threshold = 100.0
        };
        Assert.False(AlarmEngine.CheckConditionOnce(condition, 50.0));
    }

    [Fact]
    public void CheckConditionOnce_LowLimit_BelowThreshold_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.LowLimit,
            Threshold = 20.0
        };
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 10.0));
    }

    [Fact]
    public void CheckConditionOnce_LowLimit_AboveThreshold_ReturnsFalse()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.LowLimit,
            Threshold = 20.0
        };
        Assert.False(AlarmEngine.CheckConditionOnce(condition, 30.0));
    }

    [Fact]
    public void CheckConditionOnce_BitTrigger_BitSet_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.BitTrigger,
            BitOffset = 2
        };
        // 0b0100 = bit 2 is set
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 0b0100));
    }

    [Fact]
    public void CheckConditionOnce_BitTrigger_BitNotSet_ReturnsFalse()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.BitTrigger,
            BitOffset = 2
        };
        // 0b0010 = bit 2 is NOT set
        Assert.False(AlarmEngine.CheckConditionOnce(condition, 0b0010));
    }

    [Theory]
    [InlineData(64)]      // C# 位移按 64 取模 ⇒ 旧实现会静默去测 bit 0
    [InlineData(-1)]      // 负数回绕
    [InlineData(int.MaxValue)]
    public void CheckConditionOnce_BitTrigger_OffsetOutOfRange_ReturnsFalse(int bitOffset)
    {
        // 值 1（bit 0 置位）：若越界位移被静默取模，offset=64 会误报"条件成立"。
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.BitTrigger,
            BitOffset = bitOffset
        };
        Assert.False(AlarmEngine.CheckConditionOnce(condition, 1));
    }

    [Fact]
    public void CheckConditionOnce_Deviation_ExceedsThreshold_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.Deviation,
            ExpectedValue = 100.0,
            Threshold = 10.0
        };
        // |150 - 100| = 50 > 10
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 150.0));
    }

    [Fact]
    public void CheckConditionOnce_Deviation_WithinThreshold_ReturnsFalse()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.Deviation,
            ExpectedValue = 100.0,
            Threshold = 10.0
        };
        // |105 - 100| = 5 <= 10
        Assert.False(AlarmEngine.CheckConditionOnce(condition, 105.0));
    }

    [Fact]
    public void CheckConditionOnce_StateChange_ExceedsThreshold_ReturnsTrue()
    {
        var condition = new AlarmCondition
        {
            Type = AlarmConditionType.StateChange,
            ExpectedValue = 50.0,
            Threshold = 5.0
        };
        Assert.True(AlarmEngine.CheckConditionOnce(condition, 100.0));
    }

    // ── AlarmEngine 状态机测试 ──

    [Fact]
    public void Engine_RegisterAlarm_AddsToConfigList()
    {
        using var engine = new AlarmEngine();
        engine.RegisterAlarm(new AlarmConfig { Name = "Test", Id = "test-1" });

        var configs = engine.GetAlarmConfigs();
        Assert.Single(configs);
        Assert.Equal("Test", configs[0].Name);
    }

    [Fact]
    public void Engine_UnregisterAlarm_RemovesFromConfigList()
    {
        using var engine = new AlarmEngine();
        engine.RegisterAlarm(new AlarmConfig { Name = "Test", Id = "test-1" });
        engine.UnregisterAlarm("test-1");

        Assert.Empty(engine.GetAlarmConfigs());
    }

    [Fact]
    public void Engine_TriggerHighLimit_CreatesActiveRecord()
    {
        using var engine = new AlarmEngine();
        var alarmId = "alarm-001";
        var config = new AlarmConfig
        {
            Id = alarmId,
            Name = "高温报警",
            Severity = AlarmSeverity.Critical,
            Condition = new AlarmCondition
            {
                VariableId = "var-temp",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0
            }
        };
        engine.RegisterAlarm(config);
        engine.UpdateVariableValue("var-temp", 150.0);
        engine.Start();

        // 等产品状态达标，不等固定周期
        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmConfigId == alarmId && r.State == AlarmState.Active),
            "var-temp=150 超过阈值 100 后应产生活动报警记录");

        var active = engine.GetActiveAlarms();
        Assert.NotEmpty(active);
        Assert.Contains(active, r => r.AlarmConfigId == alarmId && r.State == AlarmState.Active);
    }

    [Fact]
    public void Engine_TriggerThenReturn_CreatesReturnedRecord()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;
        var config = new AlarmConfig
        {
            Name = "快速报警",
            RequiresAck = false,
            Condition = new AlarmCondition
            {
                VariableId = "var-x",
                Type = AlarmConditionType.HighLimit,
                Threshold = 50.0
            }
        };
        engine.RegisterAlarm(config);
        engine.UpdateVariableValue("var-x", 100.0);
        engine.Start();

        TestWait.Until(() => engine.GetAllRecords().Any(r => r.AlarmName == "快速报警"),
            "var-x=100 超过阈值 50 后应产生报警记录");

        // 条件解除
        engine.UpdateVariableValue("var-x", 0.0);
        TestWait.Until(() => engine.GetAllRecords().Any(r => r.AlarmName == "快速报警" && r.State == AlarmState.Returned),
            "var-x=0 解除条件后（RequiresAck=false）应转为 Returned");

        var all = engine.GetAllRecords();
        Assert.NotEmpty(all);

        // 无需确认时，条件解除 → Returned
        var record = all.First(r => r.AlarmName == "快速报警");
        Assert.Equal(AlarmState.Returned, record.State);
    }

    [Fact]
    public void Engine_AcknowledgeAlarm_SetsState()
    {
        using var engine = new AlarmEngine();
        var config = new AlarmConfig
        {
            Name = "需确认报警",
            RequiresAck = true,
            Condition = new AlarmCondition
            {
                VariableId = "var-y",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0
            }
        };
        engine.RegisterAlarm(config);
        engine.UpdateVariableValue("var-y", 200.0);
        engine.Start();

        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "需确认报警"),
            "var-y=200 超过阈值 100 后应产生活动报警记录");

        var active = engine.GetActiveAlarms();
        var record = active.FirstOrDefault(r => r.AlarmName == "需确认报警");
        Assert.NotNull(record);

        // 确认
        var ackResult = engine.AcknowledgeAlarm(record!.Id, "Operator1");
        Assert.True(ackResult);
        Assert.Equal(AlarmState.Acknowledged, record.State);
        Assert.Equal("Operator1", record.AcknowledgedBy);
    }

    [Fact]
    public void Engine_DelayedTrigger_OnlyFiresAfterDelay()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        var config = new AlarmConfig
        {
            Name = "延迟报警",
            Condition = new AlarmCondition
            {
                VariableId = "var-z",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0,
                DelayMs = 200
            }
        };
        // 探针：同一变量上的「无延迟」报警。它一旦出现就证明引擎至少完成过一轮评估，
        // 从而给下面「延迟内不应触发」提供确定性同步点（不依赖 Sleep 的时序假设）。
        // 探针与延迟报警在同一轮 EvaluateAll 里被评估，故延迟报警的计起点与探针同时建立。
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "延迟探针",
            Condition = new AlarmCondition
            {
                VariableId = "var-z",
                Type = AlarmConditionType.HighLimit,
                Threshold = 100.0
            }
        });
        engine.RegisterAlarm(config);
        engine.UpdateVariableValue("var-z", 150.0);
        engine.Start();

        // 延迟内不应触发：先等探针出现（证明评估已发生且延迟起点已建立），此刻 200ms 尚未到点
        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "延迟探针"),
            "探针报警未触发（引擎未完成首轮评估）");
        Assert.DoesNotContain(engine.GetActiveAlarms(), r => r.AlarmName == "延迟报警");

        // 延迟过后应触发
        TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "延迟报警"),
            "DelayMs=200 到期后延迟报警应触发");
    }

    [Fact]
    public void Engine_UpdateVariableValues_Batch_Works()
    {
        using var engine = new AlarmEngine();
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "Batch报警",
            Condition = new AlarmCondition
            {
                VariableId = "var-batch",
                Type = AlarmConditionType.HighLimit,
                Threshold = 50.0
            }
        });
        engine.UpdateVariableValues(new Dictionary<string, double>
        {
            ["var-batch"] = 100.0,
            ["other-var"] = 42.0
        });
        engine.Start();

        TestWait.Until(() => engine.GetActiveAlarms().Count > 0,
            "批量更新 var-batch=100 超过阈值 50 后应产生活动报警");
    }

    [Fact]
    public void Engine_AlarmTriggeredEvent_Fires()
    {
        using var engine = new AlarmEngine();
        var triggered = false;

        engine.AlarmTriggered += (_, _) => triggered = true;
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "EventTest",
            Condition = new AlarmCondition
            {
                VariableId = "var-ev",
                Type = AlarmConditionType.HighLimit,
                Threshold = 10.0
            }
        });
        engine.UpdateVariableValue("var-ev", 99.0);
        engine.Start();

        TestWait.Until(() => triggered, "AlarmTriggered 事件未在超时内触发");

        Assert.True(triggered);
    }

    [Fact]
    public void Engine_ClearReturnedRecords_RemovesOnlyReturned()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;
        // 注册两个报警，一个触发后恢复，一个保持激活
        engine.RegisterAlarm(new AlarmConfig
        {
            Id = "returned-alarm",
            Name = "已恢复报警",
            RequiresAck = false,
            Condition = new AlarmCondition
            {
                VariableId = "var-r1",
                Type = AlarmConditionType.HighLimit,
                Threshold = 50.0
            }
        });
        engine.RegisterAlarm(new AlarmConfig
        {
            Id = "active-alarm",
            Name = "活跃报警",
            Condition = new AlarmCondition
            {
                VariableId = "var-r2",
                Type = AlarmConditionType.HighLimit,
                Threshold = 50.0
            }
        });

        engine.UpdateVariableValues(new Dictionary<string, double>
        {
            ["var-r1"] = 100.0,
            ["var-r2"] = 100.0
        });
        engine.Start();

        // 同步点 1：两个报警都进入 Active
        TestWait.Until(() => engine.GetAllRecords().Count(r => r.State == AlarmState.Active) == 2,
            "两个报警应都进入 Active");

        // 解除 returned-alarm 的条件；同步点 2：它转为 Returned
        engine.UpdateVariableValue("var-r1", 0.0);
        TestWait.Until(() => engine.GetAllRecords().Any(r => r.AlarmName == "已恢复报警" && r.State == AlarmState.Returned),
            "已恢复报警在条件解除后应转为 Returned");

        // active-alarm 仍保持激活
        engine.ClearReturnedRecords();

        var all = engine.GetAllRecords();
        Assert.DoesNotContain(all, r => r.AlarmName == "已恢复报警");
        Assert.Contains(all, r => r.AlarmName == "活跃报警");
    }

    [Fact]
    public void Engine_Dispose_StopsAndClears()
    {
        var engine = new AlarmEngine();
        engine.RegisterAlarm(new AlarmConfig
        {
            Name = "DisposeTest",
            Condition = new AlarmCondition
            {
                VariableId = "var-d",
                Type = AlarmConditionType.HighLimit,
                Threshold = 10.0
            }
        });
        engine.UpdateVariableValue("var-d", 99.0);
        engine.Start();
        TestWait.Until(() => engine.GetAllRecords().Count > 0, "DisposeTest 报警应已触发（否则本用例测不到「清空」）");

        engine.Dispose();

        Assert.False(engine.IsRunning);
        Assert.Empty(engine.GetAllRecords());
    }

    [Fact]
    public void Engine_MultipleAlarms_AllEvaluate()
    {
        using var engine = new AlarmEngine();
        engine.EvaluationIntervalMs = 50;

        // 注册 5 个报警
        for (int i = 0; i < 5; i++)
        {
            engine.RegisterAlarm(new AlarmConfig
            {
                Id = $"multi-{i}",
                Name = $"报警{i}",
                Condition = new AlarmCondition
                {
                    VariableId = $"var-{i}",
                    Type = AlarmConditionType.HighLimit,
                    Threshold = 10.0
                }
            });
            engine.UpdateVariableValue($"var-{i}", 99.0);
        }

        engine.Start();

        TestWait.Until(() => engine.GetActiveAlarms().Count == 5, "5 个报警应全部进入 Active");

        var active = engine.GetActiveAlarms();
        Assert.Equal(5, active.Count);
    }
}
