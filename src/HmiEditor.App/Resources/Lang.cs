namespace HmiEditor.App.Resources;

/// <summary>
/// 本地化键常量类，所有 UI 文本通过此类的常量键引用。
/// XAML 中使用方式：Content="{x:Static lang:Lang.win_variable_manager}"
/// CS 代码中使用：Loc["win_variable_manager"]
/// </summary>
public static class Lang
{
    // ── 窗口/对话框标题 ──────────────────────────────
    public const string WinVariableManager  = "win.variable.manager";
    public const string WinTagMonitor       = "win.tag.monitor";
    public const string WinSimulator        = "win.simulator";
    public const string WinValidationResult = "win.validation.result";
    public const string WinFaceplateLib     = "win.faceplate.lib";
    public const string WinAiIcon           = "win.ai.icon";
    public const string WinAiImage          = "win.ai.image";
    public const string WinAiScript         = "win.ai.script";
    public const string WinAiTemplate       = "win.ai.template";
    public const string WinVarImport        = "win.var.import";
    public const string WinUserManager      = "win.user.manager";
    public const string WinTrendConfig      = "win.trend.config";
    public const string WinStylePreset      = "win.style.preset";
    public const string WinThemeEditor      = "win.theme.editor";
    public const string WinScreenManager    = "win.screen.manager";
    public const string WinRecipeEditor     = "win.recipe.editor";
    public const string WinFaceplateEditor  = "win.faceplate.editor";
    public const string WinAlarmList        = "win.alarm.list";
    public const string WinAlarmConfig      = "win.alarm.config";
    public const string WinAnimationEditor  = "win.animation.editor";
    public const string WinFontSubset       = "win.font.subset";
    public const string WinProjectExplorer  = "win.project.explorer";
    public const string WinDesignCanvas     = "win.design.canvas";
    public const string WinProjectSettings  = "win.project.settings";

    // ── 工程设置 ────────────────────────────────────
    public const string ProjectSettingsTitle              = "project.settings.title";
    public const string ProjectSettingsCategoryBasic      = "project.settings.category.basic";
    public const string ProjectSettingsCategoryDisplay    = "project.settings.category.display";
    public const string ProjectSettingsCategoryComm       = "project.settings.category.comm";
    public const string ProjectSettingsCategoryRuntime    = "project.settings.category.runtime";
    public const string ProjectSettingsCategoryExport     = "project.settings.category.export";
    public const string ProjectSettingsCategorySecurity   = "project.settings.category.security";
    public const string ProjectSettingsBasicDesc          = "project.settings.basic.desc";
    public const string ProjectSettingsBasicAuthor        = "project.settings.basic.author";
    public const string ProjectSettingsBasicCompany       = "project.settings.basic.company";
    public const string ProjectSettingsBasicVersion       = "project.settings.basic.version";
    public const string ProjectSettingsBasicLvgl           = "project.settings.basic.lvgl";
    public const string ProjectSettingsBasicHwTarget      = "project.settings.basic.hw.target";
    public const string HwTargetValidationOk             = "hw.target.validation.ok";
    public const string ProjectSettingsBasicCreated       = "project.settings.basic.created";
    public const string ProjectSettingsBasicModified      = "project.settings.basic.modified";
    public const string ProjectSettingsDefaultWidth       = "project.settings.display.default.width";
    public const string ProjectSettingsDefaultHeight      = "project.settings.display.default.height";
    public const string ProjectSettingsDisplayPreset      = "project.settings.display.preset";
    public const string ProjectSettingsSizeScopeHint      = "project.settings.display.size.scope.hint";
    public const string ProjectSettingsApplySizeToAll     = "project.settings.display.apply.size.all";
    public const string ProjectSettingsApplySizeDone      = "project.settings.display.apply.size.done";

    /// <summary>「尚未生效」标注：灰显项的悬停提示模板（{0} = 原因）。</summary>
    public const string SettingsNotEffectiveTip           = "settings.not.effective.tip";

    /// <summary>「尚未生效」标注：面板底部的图例。</summary>
    public const string SettingsNotEffectiveLegend        = "settings.not.effective.legend";
    public const string ScreenCustom                      = "screen.custom";
    public const string ProjectSettingsDefaultDepth       = "project.settings.display.default.depth";
    public const string ProjectSettingsDefaultOrientation = "project.settings.display.default.orientation";
    public const string ProjectSettingsDefaultAdaptive    = "project.settings.display.default.adaptive";
    public const string ProjectSettingsDefaultBgColor     = "project.settings.display.default.bg.color";
    public const string ProjectSettingsDefaultDpi         = "project.settings.display.default.dpi";
    public const string ProjectSettingsCommScanInterval   = "project.settings.comm.scan.interval";
    public const string ProjectSettingsCommTimeout        = "project.settings.comm.timeout";
    public const string ProjectSettingsCommRetry          = "project.settings.comm.retry";
    public const string ProjectSettingsCommRetryBackoff   = "project.settings.comm.retry.backoff";
    public const string ProjectSettingsCommWriteCoalesce  = "project.settings.comm.write.coalesce";
    public const string ProjectSettingsCommRecovery       = "project.settings.comm.recovery";
    public const string ProjectSettingsRuntimeScan        = "project.settings.runtime.scan";
    public const string ProjectSettingsRuntimeHistory     = "project.settings.runtime.history";
    public const string ProjectSettingsRuntimeLogLevel    = "project.settings.runtime.log.level";
    public const string ProjectSettingsRuntimeWatchdog    = "project.settings.runtime.watchdog";
    public const string ProjectSettingsRuntimeHeartbeat   = "project.settings.runtime.heartbeat";
    public const string ProjectSettingsRuntimeBootScreen  = "project.settings.runtime.boot.screen";
    public const string ProjectSettingsRuntimeCrashDump   = "project.settings.runtime.crash.dump";
    public const string ProjectSettingsRuntimeCrashSector = "project.settings.runtime.crash.sector";
    public const string ProjectSettingsRuntimeMaxTasks    = "project.settings.runtime.max.tasks";
    public const string ProjectSettingsRuntimeEnableReboot = "project.settings.runtime.enable.reboot";
    public const string ProjectSettingsExportLanguage     = "project.settings.export.language";
    public const string ProjectSettingsExportOutput       = "project.settings.export.output";
    public const string ProjectSettingsExportDebugSymbols = "project.settings.export.debug.symbols";
    public const string ProjectSettingsExportCodeStyle    = "project.settings.export.code.style";
    public const string ProjectSettingsExportNaming       = "project.settings.export.naming";
    public const string ProjectSettingsExportBuildSystem  = "project.settings.export.build.system";
    public const string ProjectSettingsExportComments     = "project.settings.export.comments";
    public const string ProjectSettingsExportMergeRes     = "project.settings.export.merge.resources";
    public const string ProjectSettingsExportOptimization = "project.settings.export.optimization";
    public const string ProjectSettingsExportLicense      = "project.settings.export.license";
    public const string ProjectSettingsSecurityMode       = "project.settings.security.mode";
    public const string ProjectSettingsSecurityTimeout    = "project.settings.security.timeout";
    public const string ProjectSettingsSecurityAudit      = "project.settings.security.audit";
    public const string ProjectSettingsSecurityAuditPath  = "project.settings.security.audit.path";
    public const string ProjectSettingsSecurityPwdMinLen  = "project.settings.security.password.min.len";
    public const string ProjectSettingsSecurityMaxAttempts = "project.settings.security.max.attempts";
    public const string ProjectSettingsSecurityLockout    = "project.settings.security.lockout";
    public const string ProjectSettingsSecurityAutoLogout = "project.settings.security.auto.logout";
    public const string ProjectSettingsSecurityLockScreen = "project.settings.security.lock.screen";
    public const string ProjectSettingsSecurityTamper     = "project.settings.security.tamper";
    public const string ProjectSettingsSecurityDefaultRole = "project.settings.security.default.role";

    // ── 变量管理 ────────────────────────────────────
    public const string VarGroupTitle           = "var.group.title";
    public const string VarAddGroup             = "var.add.group";
    public const string VarDeleteGroup          = "var.delete.group";
    public const string VarTabVariable          = "var.tab.variable";
    public const string VarListTitle            = "var.list.title";
    public const string VarAdd                  = "var.add";
    public const string VarDelete               = "var.delete";
    public const string VarCopy                 = "var.copy";
    public const string VarSave                 = "var.save";
    public const string VarName                 = "var.name";
    public const string VarDataType             = "var.data.type";
    public const string VarAddress              = "var.address";
    public const string VarAddressHint          = "var.address.hint";
    public const string VarConnection           = "var.connection";
    public const string VarUnassigned           = "var.unassigned";
    public const string VarUnit                 = "var.unit";
    public const string VarScale                = "var.scale";
    public const string VarPollRate             = "var.poll.rate";
    public const string VarSimValue             = "var.sim.value";
    public const string VarSimValueHint         = "var.sim.value.hint";
    public const string VarDeleted              = "var.deleted";
    public const string VarSaved                = "var.saved";
    public const string VarCopied               = "var.copied";
    public const string VarAdded                = "var.added";
    public const string VarUdtTab               = "var.udt.tab";
    public const string VarUdtTitle             = "var.udt.title";
    public const string VarUdtNewType           = "var.udt.new.type";
    public const string VarUdtDelete            = "var.udt.delete";
    public const string VarUdtSaveName          = "var.udt.save.name";
    public const string VarUdtDefList           = "var.udt.def.list";
    public const string VarUdtFields            = "var.udt.fields";
    public const string VarUdtAddField          = "var.udt.add.field";
    public const string VarUdtTypeName          = "var.udt.type.name";
    public const string VarUdtFieldName         = "var.udt.field.name";
    public const string VarUdtFieldType         = "var.udt.field.type";
    public const string VarUdtCommitField       = "var.udt.commit.field";
    public const string VarUdtDeleteField       = "var.udt.delete.field";
    public const string VarUdtInstanceLabel     = "var.udt.instance.label";
    public const string VarUdtInstanceName      = "var.udt.instance.name";
    public const string VarUdtCreateInstance    = "var.udt.create.instance";
    public const string VarUdtInstanceHint      = "var.udt.instance.hint";
    public const string VarUdtInstancePlaceholder = "var.udt.instance.placeholder";
    public const string VarUdtCannotDelete      = "var.udt.cannot.delete";
    public const string VarUdtNameEmpty         = "var.udt.name.empty";
    public const string VarUdtSavedName         = "var.udt.saved.name";
    public const string VarUdtCreatedInstance    = "var.udt.created.instance";
    public const string VarUdtInstanceExists     = "var.udt.instance.exists";
    public const string VarUdtDeletedType       = "var.udt.deleted.type";
    public const string VarUdtDeletedField      = "var.udt.deleted.field";
    public const string VarUdtSelectOrCreate    = "var.udt.select.or.create";
    public const string VarUdtFieldNameEmpty     = "var.udt.field.name.empty";
    public const string VarUdtEnterFieldHint     = "var.udt.enter.field.hint";
    public const string VarUdtUpdatedField       = "var.udt.updated.field";
    public const string VarUdtAddedField         = "var.udt.added.field";
    public const string VarUdtCreateUdtFirst     = "var.udt.create.udt.first";

    // ── 通讯配置 ───────────────────────────────────
    public const string CommConfigTitle        = "comm.config.title";
    public const string CommConnList           = "comm.conn.list";
    public const string CommAdd                = "comm.add";
    public const string CommDelete             = "comm.delete";
    public const string CommConfig             = "comm.config";
    public const string CommTest               = "comm.test";
    public const string CommBasicInfo          = "comm.basic.info";
    public const string CommConnName           = "comm.conn.name";
    public const string CommDriverType         = "comm.driver.type";
    public const string CommConnParams         = "comm.conn.params";
    public const string CommAdded              = "comm.added";
    public const string CommSelectFirst        = "comm.select.first";
    public const string CommDeleted            = "comm.deleted";
    public const string CommSelected           = "comm.selected";
    public const string CommDriverSwitched     = "comm.driver.switched";
    public const string CommUnknownDriver      = "comm.unknown.driver";
    public const string CommTesting            = "comm.testing";
    public const string CommValid              = "comm.valid";
    public const string CommInvalid            = "comm.invalid";
    public const string CommTestFailed         = "comm.test.failed";

    // ── 仿真器 ─────────────────────────────────────
    public const string SimTitle               = "sim.title";
    public const string SimVarList             = "sim.var.list";
    public const string SimAddVar              = "sim.add.var";
    public const string SimDelVar              = "sim.del.var";
    public const string SimParamEdit           = "sim.param.edit";
    public const string SimSignalType          = "sim.signal.type";
    public const string SimConst               = "sim.const";
    public const string SimSine                = "sim.sine";
    public const string SimSquare              = "sim.square";
    public const string SimRamp                = "sim.ramp";
    public const string SimIncrement           = "sim.increment";
    public const string SimRandom              = "sim.random";
    public const string SimManual              = "sim.manual";
    public const string SimAmplitude           = "sim.amplitude";
    public const string SimOffset              = "sim.offset";
    public const string SimFreq                = "sim.freq";
    public const string SimRange               = "sim.range";
    public const string SimNoise               = "sim.noise";
    public const string SimManualVal           = "sim.manual.val";
    public const string SimWaveHint            = "sim.wave.hint";
    public const string SimWaveConst           = "sim.wave.const";
    public const string SimStart               = "sim.start";
    public const string SimRunning             = "sim.running";
    public const string SimTickInterval        = "sim.tick.interval";
    public const string SimReset               = "sim.reset";
    public const string SimVarId               = "sim.var.id";
    public const string SimCurrentVal          = "sim.current.val";
    public const string SimSignalCol           = "sim.signal.col";
    public const string SimWaveHintCol         = "sim.wave.hint.col";
    public const string SimCommTab             = "sim.comm.tab";
    public const string SimConnected           = "sim.connected";
    public const string SimDisconnect          = "sim.disconnect";
    public const string SimReconnect           = "sim.reconnect";
    public const string SimRespMode            = "sim.resp.mode";
    public const string SimEcho                = "sim.echo";
    public const string SimInvert              = "sim.invert";
    public const string SimRespond             = "sim.respond";
    public const string SimRegTest             = "sim.reg.test";
    public const string SimAddrHint            = "sim.addr.hint";
    public const string SimValHint             = "sim.val.hint";
    public const string SimRead                = "sim.read";
    public const string SimWrite               = "sim.write";
    public const string SimLogTab              = "sim.log.tab";
    public const string SimLogHint             = "sim.log.hint";
    public const string SimSelectVar           = "sim.select.var";
    public const string SimDelVarLog           = "sim.del.var.log";

    // ── 工具箱 ─────────────────────────────────────
    public const string TbSearchHint           = "tb.search.hint";
    public const string TbControlsList         = "tb.controls.list";
    public const string TbSearchEmpty          = "tb.search.empty";
    public const string TbSearchClear          = "tb.search.clear";
    public const string TbSearchShortcut       = "tb.search.shortcut";
    public const string TbSearchCount          = "tb.search.count";
    public const string TbDragHint             = "tb.drag.hint";

    // ── 通讯驱动类型 ───────────────────────────────
    public const string DriverModbusRtu    = "driver.modbus.rtu";
    public const string DriverModbusTcp    = "driver.modbus.tcp";
    public const string DriverOpcUa        = "driver.opc.ua";
    public const string DriverMqtt         = "driver.mqtt";

    // ── 通讯驱动参数 ───────────────────────────────
    public const string ParamPort            = "param.port";
    public const string ParamBaudRate        = "param.baud.rate";
    public const string ParamDataBits        = "param.data.bits";
    public const string ParamStopBits        = "param.stop.bits";
    public const string ParamParity          = "param.parity";
    public const string ParamSlaveId         = "param.slave.id";
    public const string ParamIpAddress       = "param.ip.address";
    public const string ParamTcpPort         = "param.tcp.port";
    public const string ParamEndpointUrl     = "param.endpoint.url";
    public const string ParamSecurityPolicy  = "param.security.policy";
    public const string ParamNodeIdFormat    = "param.node.id.format";
    public const string ParamBroker          = "param.broker";
    public const string ParamMqttPort        = "param.mqtt.port";
    public const string ParamClientId        = "param.client.id";
    public const string ParamUsername        = "param.username";
    public const string ParamPassword        = "param.password";
    public const string ParamKeepAlive       = "param.keep.alive";
    public const string ParamQos             = "param.qos";

    // ── 通用 ───────────────────────────────────────
    public const string GenFailed         = "gen.failed";
    public const string GenCreateOrOpen   = "gen.create.or.open.project";
    public const string GenDisabled       = "gen.disabled";
    public const string GenUnlocked       = "gen.unlocked";
    public const string GenConfirmDelete  = "gen.confirm.delete";
    public const string GenSuccess        = "gen.success";
    public const string GenWarning        = "gen.warning";
    public const string GenError          = "gen.error";
    public const string GenCancel         = "gen.cancel";
    public const string GenClose          = "gen.close";
    public const string GenSave           = "gen.save";
    public const string GenApply          = "gen.apply";
    public const string GenPreview        = "gen.preview";
    public const string GenCopyAll        = "gen.copy.all";
    public const string GenNotConfigured  = "gen.not.configured";

    // ── 变量浏览器 ─────────────────────────────────
    public const string BrowserSearchHint   = "browser.search.hint";
    public const string BrowserSelectVar    = "browser.select.var";

    // ── 趋势图 ─────────────────────────────────────
    public const string TrendConfig          = "trend.config";
    public const string TrendAdd             = "trend.add";
    public const string TrendCurveConfig     = "trend.curve.config";
    public const string TrendAddPen          = "trend.add.pen";
    public const string TrendPenEdit         = "trend.pen.edit";
    public const string TrendLabel           = "trend.label";
    public const string TrendVarId           = "trend.var.id";
    public const string TrendColor           = "trend.color";
    public const string TrendLineWidth       = "trend.line.width";
    public const string TrendShowDot         = "trend.show.dot";
    public const string TrendYAxisRange      = "trend.y.axis.range";
    public const string TrendMin             = "trend.min";
    public const string TrendMax             = "trend.max";
    public const string TrendEnabled         = "trend.enabled";
    public const string TrendChart           = "trend.chart";
    public const string TrendTimeWindow      = "trend.time.window";
    public const string TrendTimeWindowCustom = "trend.time.custom";
    public const string TrendRunSim          = "trend.run.sim";
    public const string TrendLegend          = "trend.legend";
    public const string TrendGrid            = "trend.grid";
    public const string TrendXAxis           = "trend.x.axis";
    public const string TrendYAxis           = "trend.y.axis";
    public const string TrendStartRecord     = "trend.start.record";
    public const string TrendReportTitle         = "trend.report.title";
    public const string TrendReportBtn           = "trend.report.btn";
    public const string TrendReportSaveTitle     = "trend.report.save.title";
    public const string TrendReportExportEmpty   = "trend.report.export.empty";
    public const string UiOpExportTrendReport    = "ui.op.export.trend.report";

    // ── 标签监视器 ─────────────────────────────────
    public const string TagMonitorTitle           = "tag.monitor.title";
    public const string TagMonitorAllDir          = "tag.monitor.all.dir";
    public const string TagMonitorReadOnly        = "tag.monitor.read.only";
    public const string TagMonitorReadWrite       = "tag.monitor.read.write";
    public const string TagMonitorRefresh         = "tag.monitor.refresh";
    public const string TagMonitorRebuild         = "tag.monitor.rebuild";
    public const string TagMonitorVar             = "tag.monitor.var";
    public const string TagMonitorAddr            = "tag.monitor.addr";
    public const string TagMonitorConn            = "tag.monitor.conn";
    public const string TagMonitorDir             = "tag.monitor.dir";
    public const string TagMonitorControl         = "tag.monitor.control";
    public const string TagMonitorCurVal          = "tag.monitor.cur.val";
    public const string TagMonitorStatus          = "tag.monitor.status";
    public const string TagMonitorManualWrite     = "tag.monitor.manual.write";
    public const string TagMonitorWrite           = "tag.monitor.write";
    public const string TagMonitorWriteOK         = "tag.monitor.write.ok";
    public const string TagMonitorWriteFail       = "tag.monitor.write.fail";

    // ── 用户管理 ───────────────────────────────────
    public const string UserMgrTitle           = "user.mgr.title";
    public const string UserMgrUserList        = "user.mgr.user.list";
    public const string UserMgrUserDetail      = "user.mgr.user.detail";
    public const string UserMgrUsername        = "user.mgr.username";
    public const string UserMgrDisplayName     = "user.mgr.display.name";
    public const string UserMgrRole            = "user.mgr.role";
    public const string UserMgrStatus          = "user.mgr.status";
    public const string UserMgrDelete          = "user.mgr.delete";
    public const string UserMgrDisable         = "user.mgr.disable";
    public const string UserMgrResetPwd        = "user.mgr.reset.pwd";
    public const string UserMgrUnlock          = "user.mgr.unlock";
    public const string UserMgrAddUser         = "user.mgr.add.user";
    public const string UserMgrRoleList        = "user.mgr.role.list";
    public const string UserMgrRoleDetail      = "user.mgr.role.detail";
    public const string UserMgrRoleName        = "user.mgr.role.name";
    public const string UserMgrRoleLevel       = "user.mgr.role.level";
    public const string UserMgrRoleDesc        = "user.mgr.role.desc";
    public const string UserMgrPerm            = "user.mgr.perm";
    public const string UserMgrPermViewScreen  = "user.mgr.perm.view.screen";
    public const string UserMgrPermOpCtrl      = "user.mgr.perm.op.ctrl";
    public const string UserMgrPermEditRecipe  = "user.mgr.perm.edit.recipe";
    public const string UserMgrPermAckAlarm    = "user.mgr.perm.ack.alarm";
    public const string UserMgrPermConfig      = "user.mgr.perm.config";
    public const string UserMgrPermManageUser  = "user.mgr.perm.manage.user";
    public const string UserMgrPermViewAlarm   = "user.mgr.perm.view.alarm";
    public const string UserMgrPermExport      = "user.mgr.perm.export";
    public const string UserMgrPermDebug       = "user.mgr.perm.debug";
    public const string UserMgrSaveRole        = "user.mgr.save.role";
    public const string UserMgrAuditTab        = "user.mgr.audit.tab";
    public const string UserMgrAuditRefresh    = "user.mgr.audit.refresh";
    public const string UserMgrAuditClear      = "user.mgr.audit.clear";
    public const string UserMgrAuditTime       = "user.mgr.audit.time";
    public const string UserMgrAuditUser       = "user.mgr.audit.user";
    public const string UserMgrAuditAction     = "user.mgr.audit.action";
    public const string UserMgrAuditDetail     = "user.mgr.audit.detail";
    public const string UserMgrAuditResult     = "user.mgr.audit.result";
    public const string UserMgrAuditSource     = "user.mgr.audit.source";
    public const string UserMgrLoginFailed     = "user.mgr.login.failed";
    public const string UserMgrDeletedUser     = "user.mgr.deleted.user";
    public const string UserMgrSavedRole       = "user.mgr.saved.role";
    public const string UserMgrCreateFailed    = "user.mgr.create.failed";

    // ── AI 助手 ────────────────────────────────────
    public const string AiConfigTitle         = "ai.config.title";
    public const string AiProvider            = "ai.provider";
    public const string AiProviderOpenAi      = "ai.provider.openai";
    public const string AiProviderClaude      = "ai.provider.claude";
    public const string AiEndpoint            = "ai.endpoint";
    public const string AiEndpointHint        = "ai.endpoint.hint";
    public const string AiModel               = "ai.model";
    public const string AiModelHint           = "ai.model.hint";
    public const string AiApiKey              = "ai.api.key";
    public const string AiApiKeyHint          = "ai.api.key.hint";
    public const string AiSaveConfig          = "ai.save.config";
    public const string AiPreview             = "ai.preview";
    public const string AiApply               = "ai.apply";
    public const string AiCancel              = "ai.cancel";
    public const string AiCancelled           = "ai.cancelled";
    public const string SplashSubtitle          = "splash.subtitle";
    public const string SplashNewProject        = "splash.new.project";
    public const string SplashNewProjectHint    = "splash.new.project.hint";
    public const string SplashOpenProject       = "splash.open.project";
    public const string SplashOpenProjectHint   = "splash.open.project.hint";
    public const string SplashRecent            = "splash.recent";
    public const string AiNewChat               = "ai.new.chat";
    public const string GenRetry                = "gen.retry";
    public const string AiInputHint           = "ai.input.hint";
    public const string AiSend                = "ai.send";
    public const string AiClose               = "ai.close";
    public const string AiGenerate            = "ai.generate";
    public const string AiIconPerLineHint     = "ai.icon.per.line.hint";
    public const string AiIconPlaceholder     = "ai.icon.placeholder";
    public const string AiIconVariantLabel    = "ai.icon.variant.label";
    public const string AiIconVariantAuto     = "ai.icon.variant.auto";
    public const string AiIconVariantLinear   = "ai.icon.variant.linear";
    public const string AiIconVariantFilled   = "ai.icon.variant.filled";
    public const string AiIconVariantOutline  = "ai.icon.variant.outline";
    public const string AiIconVariantRounded  = "ai.icon.variant.rounded";
    public const string AiIconVariantMinimal  = "ai.icon.variant.minimal";
    public const string AiIconVariantGeometric = "ai.icon.variant.geometric";
    public const string AiIconVariantAccent   = "ai.icon.variant.accent";
    public const string AiScriptFuncHint      = "ai.script.func.hint";
    public const string AiScriptPlaceholder   = "ai.script.placeholder";
    public const string AiScriptApiTitle      = "ai.script.api.title";
    public const string AiScriptApiContent    = "ai.script.api.content";
    public const string AiTemplateHint        = "ai.template.hint";
    public const string AiTemplatePlaceholder = "ai.template.placeholder";
    public const string AiTemplateDesc        = "ai.template.desc";
    public const string AiNotConfigured       = "ai.not.configured";
    public const string AiGenFailed           = "ai.gen.failed";
    public const string AiSaved               = "ai.saved";
    public const string AiSaveFailed          = "ai.save.failed";
    public const string AiExpandConfig        = "ai.expand.config";
    public const string AiValidationResult    = "ai.validation.result";

    // ── 其他 ───────────────────────────────────────
    public const string LoginTitle            = "login.title";
    public const string LoginSubtitle         = "login.subtitle";
    public const string LoginUser             = "login.user";
    public const string LoginPwd              = "login.pwd";
    public const string LoginBtn              = "login.btn";
    public const string LoginFailed           = "login.failed";

    public const string ScreenMgrTitle        = "screen.mgr.title";
    public const string ScreenAdd             = "screen.add";
    public const string ScreenSwitch          = "screen.switch";
    public const string ScreenEmptyHint       = "screen.empty.hint";
    public const string ScreenAdaptive        = "screen.adaptive";
    public const string ScreenAdaptiveNone    = "screen.adaptive.none";
    public const string ScreenAdaptiveFit     = "screen.adaptive.fit";
    public const string ScreenAdaptiveFill    = "screen.adaptive.fill";
    public const string ScreenDeleted         = "screen.deleted";

    public const string RecipeTitle           = "recipe.title";
    public const string RecipeGroupList       = "recipe.group.list";
    public const string RecipeAddGroup        = "recipe.add.group";
    public const string RecipeNoGroupHint     = "recipe.no.group.hint";
    public const string RecipeDeleteGroup     = "recipe.delete.group";
    public const string RecipeList            = "recipe.list";
    public const string RecipeNew             = "recipe.new";
    public const string RecipeClone           = "recipe.clone";
    public const string RecipeNoRecipeHint    = "recipe.no.recipe.hint";
    public const string RecipeCsvImport       = "recipe.csv.import";
    public const string RecipeCsvExport       = "recipe.csv.export";
    public const string RecipeBasicInfo       = "recipe.basic.info";
    public const string RecipeName            = "recipe.name";
    public const string RecipeNameHint        = "recipe.name.hint";
    public const string RecipeGroupLabel      = "recipe.group.label";
    public const string RecipeDesc            = "recipe.desc";
    public const string RecipeDescHint        = "recipe.desc.hint";
    public const string RecipeStatus          = "recipe.status";
    public const string RecipeEnabled         = "recipe.enabled";
    public const string RecipeVersion         = "recipe.version";
    public const string RecipeCreated         = "recipe.created";
    public const string RecipeModified        = "recipe.modified";
    public const string RecipeParams          = "recipe.params";
    public const string RecipeParamList       = "recipe.param.list";
    public const string RecipeAddParam        = "recipe.add.param";
    public const string RecipeDelParam        = "recipe.del.param";
    public const string RecipeUpParam         = "recipe.up.param";
    public const string RecipeDownParam       = "recipe.down.param";
    public const string RecipeParamEdit       = "recipe.param.edit";
    public const string RecipeParamLabel      = "recipe.param.label";
    public const string RecipeParamLabelHint  = "recipe.param.label.hint";
    public const string RecipeParamVarId      = "recipe.param.var.id";
    public const string RecipeParamDefault    = "recipe.param.default";
    public const string RecipeParamDefaultHint= "recipe.param.default.hint";
    public const string RecipeParamUnit       = "recipe.param.unit";
    public const string RecipeParamUnitHint   = "recipe.param.unit.hint";
    public const string RecipeParamMin        = "recipe.param.min";
    public const string RecipeParamMax        = "recipe.param.max";
    public const string RecipeParamType       = "recipe.param.type";
    public const string RecipeParamOrder      = "recipe.param.order";
    public const string RecipeParamOrderHint  = "recipe.param.order.hint";
    public const string RecipeSave            = "recipe.save";
    public const string RecipeSortNo          = "recipe.sort.no";

    public const string AlarmConfigTitle      = "alarm.config.title";
    public const string AlarmGroupList        = "alarm.group.list";
    public const string AlarmAddGroup         = "alarm.add.group";
    public const string AlarmAdd              = "alarm.add";
    public const string AlarmSelectGroupHint  = "alarm.select.group.hint";
    public const string AlarmBasicInfo        = "alarm.basic.info";
    public const string AlarmName             = "alarm.name";
    public const string AlarmDesc             = "alarm.desc";
    public const string AlarmSeverity         = "alarm.severity";
    public const string AlarmTriggerValue     = "alarm.trigger.value";
    public const string AlarmAckBy            = "alarm.ack.by";
    public const string AlarmEnabled          = "alarm.enabled";
    public const string AlarmNeedAck          = "alarm.need.ack";
    public const string AlarmTrigger          = "alarm.trigger";
    public const string AlarmVariable         = "alarm.variable";
    public const string AlarmCondType         = "alarm.cond.type";
    public const string AlarmThreshold        = "alarm.threshold";
    public const string AlarmDeadband         = "alarm.deadband";
    public const string AlarmDelay            = "alarm.delay";
    public const string AlarmBitOffset        = "alarm.bit.offset";
    public const string AlarmExpectedVal      = "alarm.expected.val";
    public const string AlarmDeletedGroup     = "alarm.deleted.group";
    public const string AlarmSelectGroup      = "alarm.select.group";
    public const string AlarmDeleted          = "alarm.deleted";

    public const string AlarmListTitle        = "alarm.list.title";
    public const string AlarmFilterAll        = "alarm.filter.all";
    public const string AlarmFilterActive     = "alarm.filter.active";
    public const string AlarmFilterAcked      = "alarm.filter.acked";
    public const string AlarmFilterCleared    = "alarm.filter.cleared";
    public const string AlarmLevelAll         = "alarm.level.all";
    public const string AlarmLevelInfo        = "alarm.level.info";
    public const string AlarmLevelWarning     = "alarm.level.warning";
    public const string AlarmLevelCritical    = "alarm.level.critical";
    public const string AlarmLevelEmergency   = "alarm.level.emergency";
    public const string AlarmAckSelected      = "alarm.ack.selected";
    public const string AlarmClearCleared     = "alarm.clear.cleared";
    public const string AlarmNoRecords        = "alarm.no.records";

    public const string AssetTitle            = "asset.title";
    public const string AssetImportImg        = "asset.import.img";
    public const string AssetImportSvg        = "asset.import.svg";
    public const string AssetImportFont       = "asset.import.font";
    public const string AssetAiGenerate       = "asset.ai.generate";
    public const string AssetRefresh          = "asset.refresh";
    public const string AssetBudget           = "asset.budget";
    public const string AssetReady            = "asset.ready";
    public const string AssetCreateOrOpen     = "asset.create.or.open";
    public const string AssetSvgParseFail     = "asset.svg.parse.fail";

    public const string AnimList              = "anim.list";
    public const string AnimAdd               = "anim.add";
    public const string AnimNoAnimHint        = "anim.no.anim.hint";
    public const string AnimDelete            = "anim.delete";
    public const string AnimBasicInfo         = "anim.basic.info";
    public const string AnimName              = "anim.name";
    public const string AnimNameHint          = "anim.name.hint";
    public const string AnimTarget            = "anim.target";
    public const string AnimProp              = "anim.prop";
    public const string AnimDesc              = "anim.desc";
    public const string AnimDescHint          = "anim.desc.hint";
    public const string AnimStatus            = "anim.status";
    public const string AnimTriggerMode       = "anim.trigger.mode";
    public const string AnimParams            = "anim.params";
    public const string AnimFromVal           = "anim.from.val";
    public const string AnimToVal             = "anim.to.val";
    public const string AnimDuration          = "anim.duration";
    public const string AnimDelay             = "anim.delay";
    public const string AnimEasing            = "anim.easing";
    public const string AnimRepeat            = "anim.repeat";
    public const string AnimTimeline          = "anim.timeline";
    public const string AnimPlayPreview       = "anim.play.preview";
    public const string AnimAddKeyframe       = "anim.add.keyframe";
    public const string AnimDelKeyframe       = "anim.del.keyframe";
    public const string AnimTimePct           = "anim.time.pct";
    public const string AnimKeyVal            = "anim.key.val";
    public const string AnimEasingLabel       = "anim.easing.label";
    public const string AnimTimeCol           = "anim.time.col";
    public const string AnimValCol            = "anim.val.col";
    public const string AnimEasingCol         = "anim.easing.col";
    public const string AnimNoteCol           = "anim.note.col";
    public const string AnimOpCol             = "anim.op.col";
    public const string AnimLvglPreview       = "anim.lvgl.preview";

    public const string ScriptTitle           = "script.title";
    public const string ScriptList            = "script.list";
    public const string ScriptAdd             = "script.add";
    public const string ScriptSelectHint      = "script.select.hint";
    public const string ScriptName            = "script.name";
    public const string ScriptCategory        = "script.category";
    public const string ScriptEnabled         = "script.enabled";
    public const string ScriptDesc            = "script.desc";
    public const string ScriptDescHint        = "script.desc.hint";
    public const string ScriptCodeHint        = "script.code.hint";
    public const string ScriptInsertTemplate  = "script.insert.template";
    public const string ScriptOnClick          = "script.on.click";
    public const string ScriptOnValueChanged   = "script.on.value.changed";
    public const string ScriptTimer            = "script.timer";
    public const string ScriptAlarm            = "script.alarm";
    public const string ScriptAiGen            = "script.ai.gen";
    public const string ScriptReady            = "script.ready";
    public const string ScriptValidate         = "script.validate";
    public const string ScriptInsertTpl       = "script.insert.tpl";
    public const string ScriptDebugRun         = "script.debug.run";
    public const string ScriptLuaHelp          = "script.lua.help";
    public const string ScriptOutput           = "script.output";
    public const string ScriptClear            = "script.clear";
    public const string ScriptSyntaxError      = "script.syntax.error";
    public const string ScriptExecFail         = "script.exec.fail";
    public const string ScriptExecOk           = "script.exec.ok";
    public const string ScriptDeleted          = "script.deleted";
    public const string ScriptAll              = "script.all";
    public const string ScriptCount            = "script.count";
    public const string ScriptDisabled         = "script.disabled";
    public const string ScriptNoDesc           = "script.no.desc";
    public const string ScriptNewDefault       = "script.new.default";
    public const string ScriptKeepOne          = "script.keep.one";
    public const string ScriptSyntaxOk         = "script.syntax.ok";
    public const string ScriptErrorPrefix      = "script.error.prefix";
    public const string ScriptTemplateInserted = "script.template.inserted";
    public const string ScriptGenerated        = "script.generated";
    public const string ScriptNoFunction       = "script.no.function";
    public const string ScriptTranslatedCode   = "script.translated.code";
    public const string ScriptExecResult       = "script.exec.result";
    public const string ScriptOutputVars       = "script.output.vars";
    public const string ScriptExecLog          = "script.exec.log";
    public const string ScriptHelpTitle        = "script.help.title";
    public const string ScriptSaved            = "script.saved";

    /// <summary>「运行前检查」提示的前缀 —— **非阻断**，见 <c>ScriptValidationResult.Warnings</c>。</summary>
    public const string ScriptWarnPrefix       = "script.warn.prefix";
    /// <summary>同类提示的计数后缀（除首条之外还有几处）。</summary>
    public const string ScriptWarnMore         = "script.warn.more";

    /// <summary>
    /// 校验信息的行号前缀（<c>第 {0} 行: {1}</c>）。
    /// <para>行号**只在这里**加：它是界面呈现的一部分，而 <c>ScriptError.Message</c>
    /// 是字典缺键时的回落 —— 两处都有行号就必然有一处先过期。</para>
    /// </summary>
    public const string ScriptLinePrefix       = "script.line.prefix";
    public const string CsvLinePrefix          = "csv.line.prefix";

    public const string FaceplateTitle        = "faceplate.title";
    public const string FaceplateTemplate     = "faceplate.template";
    public const string FaceplateEdit         = "faceplate.edit";
    public const string FaceplateDelete       = "faceplate.delete";
    public const string FaceplateNew          = "faceplate.new";
    public const string FaceplateSearchHint   = "faceplate.search.hint";
    public const string FaceplateContainsControls = "faceplate.contains.controls";

    public const string StyleSuccessColor     = "style.success.color";
    public const string StyleWarningColor     = "style.warning.color";
    public const string StyleErrorColor       = "style.error.color";

    public const string FontSubsetTitle       = "font.subset.title";
    public const string FontRawChars          = "font.raw.chars";
    public const string FontUniqueChars       = "font.unique.chars";
    public const string FontCjk               = "font.cjk";
    public const string FontAfterSubset       = "font.after.subset";
    public const string FontFullCjk           = "font.full.cjk";
    public const string FontSaved             = "font.saved";

    public const string CrossRefTitle         = "cross.ref.title";
    public const string CrossRefVar           = "cross.ref.var";
    public const string CrossRefScreen        = "cross.ref.screen";
    public const string CrossRefScript        = "cross.ref.script";
    public const string CrossRefSearchHint    = "cross.ref.search.hint";
    public const string CrossRefFind          = "cross.ref.find";

    // 交叉引用面板「描述列」的模板（阶段 95，闭环登记 #61n）—— 与 Core 的
    // CrossReferenceAnalyzer 那 9 条 Description 回落**逐字对应**，两份的一致性由
    // ReferenceDescriptionLocalizationGuardTests 用**相等式**钉住（不是靠约定）。
    public const string CrossRefDescBinding        = "cross.ref.desc.binding";
    public const string CrossRefDescLegacyBinding  = "cross.ref.desc.legacy_binding";
    public const string CrossRefDescScreenSwitch   = "cross.ref.desc.screen_switch";
    public const string CrossRefDescScriptRun      = "cross.ref.desc.script_run";
    public const string CrossRefDescScriptBinding  = "cross.ref.desc.script_binding";

    // ── 交叉引用面板「引用类型列」与「绑定方向」（阶段 96，闭环 #61o）──
    // 收的是一行里**另外三个枚举名**：改动前 ReferenceType / HmiControlType 直显 ToString()，
    // 绑定方向作为 cross.ref.desc.binding 的 {1} 实参也是 ToString() ⇒ 中文界面下分别是
    // VariableBinding / Label / WriteOnly。控件类型走工具箱已有的 tb.*（见 ControlTypeTextKeys），
    // 这里只管引用类型与方向。
    public const string CrossRefTypeVariableBinding    = "cross.ref.type.variable_binding";
    public const string CrossRefTypeActionTarget       = "cross.ref.type.action_target";
    public const string CrossRefTypeScriptBinding      = "cross.ref.type.script_binding";
    public const string CrossRefTypeExpressionVariable = "cross.ref.type.expression_variable";
    // 方向**刻意不复用** prop.bind.readonly/readwrite 或 tag.monitor.read.only/…：
    // ① prop.bind.* 只有 2 条（缺 WriteOnly，BindingDirection 却有 3 个成员）；
    // ② 复用会把那两个常量从「无代码引用」集合里拽出来 ⇒ UnusedBaseline 1113 会 −2，
    //    而那是「数字没动」之外唯一能让基线动的情形 —— 没必要的代价不付。
    public const string CrossRefDirReadOnly            = "cross.ref.dir.readonly";
    public const string CrossRefDirWriteOnly           = "cross.ref.dir.writeonly";
    public const string CrossRefDirReadWrite           = "cross.ref.dir.readwrite";

    public const string PropTitle             = "prop.title";
    public const string PropNoSelection       = "prop.no.selection";
    public const string PropPosSize           = "prop.pos.size";
    public const string PropContent           = "prop.content";
    public const string PropAppearance        = "prop.appearance";
    public const string PropSectionColor      = "prop.section.color";
    public const string PropSectionContent    = "prop.section.content";
    public const string PropSectionImage      = "prop.section.image";
    public const string PropFont             = "prop.font";
    public const string PropFontFamily       = "prop.font.family";
    public const string PropFontSize         = "prop.font.size";
    public const string PropTextAlign        = "prop.text.align";
    public const string PropTextVAlign       = "prop.text.valign";
    public const string PropX                 = "prop.x";
    public const string PropY                 = "prop.y";
    public const string PropWidth             = "prop.width";
    public const string PropHeight            = "prop.height";
    public const string PropName              = "prop.name";
    public const string PropText              = "prop.text";
    public const string PropBgColor           = "prop.bg.color";
    public const string PropColorHint         = "prop.color.hint";
    public const string PropSelected          = "prop.selected";
    public const string PropTextColor         = "prop.text.color";
    public const string PropRadius            = "prop.radius";
    public const string PropBorderWidth       = "prop.border.width";
    public const string PropBorderColor       = "prop.border.color";
    public const string PropVarBinding        = "prop.var.binding";
    public const string PropBindSource        = "prop.bind.source";
    public const string PropBindSourceExpression = "prop.bind.source.expression";
    public const string PropBindVar           = "prop.bind.var";
    public const string PropExpression        = "prop.expression";
    public const string PropExprHint          = "prop.expr.hint";
    public const string PropBindMode          = "prop.bind.mode";
    public const string PropBindReadWrite     = "prop.bind.readwrite";
    public const string PropBindReadOnly      = "prop.bind.readonly";
    public const string PropFaceplateParams   = "prop.faceplate.params";
    public const string PropActions           = "prop.actions";
    public const string PropAddAction         = "prop.add.action";
    public const string PropActionType        = "prop.action.type";
    public const string PropActionTarget      = "prop.action.target";
    public const string PropActionTargetHint  = "prop.action.target.hint";
    public const string PropActionValue       = "prop.action.value";
    public const string PropActionValueHint   = "prop.action.value.hint";
    public const string PropAiSuggest         = "prop.ai.suggest";
    public const string PropOneClickFix       = "prop.one.click.fix";

    public const string ProjectResources      = "project.resources";
    public const string ProjectResource       = "project.resource";
    public const string ProjectRefresh        = "project.refresh";
    public const string ProjectSave           = "project.save";
    public const string ProjectLoad           = "project.load";
    public const string ProjectExport         = "project.export";
    public const string ProjectNew            = "project.new";
    public const string ProjectOpen           = "project.open";
    public const string ProjectExportC        = "project.export.c";
    public const string ProjectExit           = "project.exit";
    public const string ProjectUndo           = "project.undo";
    public const string ProjectRedo           = "project.redo";
    public const string ProjectSelectAll      = "project.select.all";
    public const string ProjectZoomIn         = "project.zoom.in";
    public const string ProjectZoomOut        = "project.zoom.out";
    public const string ProjectZoomReset      = "project.zoom.reset";
    public const string ProjectAbout          = "project.about";
    public const string ProjectVersion        = "project.version";
    public const string ProjectUntitled       = "project.untitled";
    public const string ProjectStatusReady    = "project.status.ready";
    public const string ProjectStatusSaved    = "project.status.saved";
    public const string ProjectStatusExported = "project.status.exported";
    public const string ProjectToolbox        = "project.toolbox";
    public const string ProjectProps          = "project.props";

    public const string NavFlowTitle          = "nav.flow.title";
    public const string NavFlowEmptyHint      = "nav.flow.empty.hint";
    public const string NavFlowSave           = "nav.flow.save";
    public const string NavFlowLoad           = "nav.flow.load";

    public const string LayerTitle            = "layer.title";
    public const string LayerEye              = "layer.eye";
    public const string LayerLock             = "layer.lock";
    public const string LayerUp               = "layer.up";
    public const string LayerDown             = "layer.down";
    public const string LayerLockAll          = "layer.lock.all";
    public const string LayerUnlockAll        = "layer.unlock.all";

    public const string AuditTitle            = "audit.title";
    public const string AuditFilterHint       = "audit.filter.hint";
    public const string AuditRefresh          = "audit.refresh";
    public const string AuditReportTitle         = "audit.report.title";
    public const string AuditReportGenerated     = "audit.report.generated";
    public const string AuditReportExportBtn     = "audit.report.btn";
    public const string AuditReportSaveTitle     = "audit.report.save.title";
    public const string ReportFilterCsv     = "report.filter.csv";
    public const string ReportFilterHtml    = "report.filter.html";
    public const string ReportFilterXlsx    = "report.filter.xlsx";
    public const string ReportFilterPdf     = "report.filter.pdf";
    public const string ReportExportNoFont        = "report.export.no.font";
    public const string ReportExportOk      = "report.export.ok";
    public const string ReportBundleBtn          = "report.bundle.btn";
    public const string ReportBundleSaveTitle    = "report.bundle.save.title";
    public const string ReportBundleOk           = "report.bundle.ok";
    public const string UiOpExportBundle         = "ui.op.export.bundle";
    public const string ReportRangeTitle         = "report.range.title";
    public const string ReportRangeHint          = "report.range.hint";
    public const string ReportRangeAll           = "report.range.all";
    public const string ReportRangeLastHour      = "report.range.last.hour";
    public const string ReportRangeLast24h       = "report.range.last.24h";
    public const string ReportRangeLast7d        = "report.range.last.7d";
    public const string ReportRangeLast30d       = "report.range.last.30d";
    public const string ReportRangeCustom        = "report.range.custom";
    public const string ReportRangeFrom          = "report.range.from";
    public const string ReportRangeTo            = "report.range.to";
    public const string ReportDesignerTitle      = "report.designer.title";
    public const string ReportDesignerHint       = "report.designer.hint";
    public const string ReportDesignerBtn        = "report.designer.btn";
    public const string ReportDesignerReset      = "report.designer.reset";
    public const string UiOpReportDesigner       = "ui.op.report.designer";
    public const string ReportAutoExportOk            = "report.auto.ok";
    public const string ReportAutoExportFailed        = "report.auto.failed";
    public const string ReportAutoExportNotConfigured = "report.auto.not.configured";
    public const string ReportAutoExportNoSources     = "report.auto.no.sources";
    public const string ReportAutoExportMissingSource = "report.auto.missing.source";
    public const string ReportColumnsTitle       = "report.columns.title";
    public const string ReportColumnsNone        = "report.columns.none";
    public const string ReportColumnsHint        = "report.columns.hint";
    public const string AuditReportExportEmpty   = "audit.report.export.empty";
    public const string UiOpExportAuditReport    = "ui.op.export.audit.report";

    public const string VarImportTitle        = "var.import.title";
    public const string VarImportSelectCsv    = "var.import.select.csv";
    public const string VarImportNoFile       = "var.import.no.file";
    public const string VarImportParse        = "var.import.parse";
    public const string VarImportColName      = "var.import.col.name";
    public const string VarImportColType      = "var.import.col.type";
    public const string VarImportColAddr      = "var.import.col.addr";
    public const string VarImportColUnit      = "var.import.col.unit";
    public const string VarImportColScale     = "var.import.col.scale";
    public const string VarImportDoImport     = "var.import.do.import";
    public const string VarImportDone          = "var.import.done";

    // 错误/提示消息
    // ⚠ 阶段 91 / #61i 删掉了 `MsgExprError`：它唯一的消费点是 PropertyPanelView 里那个
    //   `??` 兜底分支，而那半由 ExpressionEngine.TryCompile 的 NotNullWhen(false) 保证取不到
    //   ⇒ 键与常量一起退场（实测口径：字典 2219 → 2218 键、Lang 常量 1776 → 1775，
    //   两数由 `python3 -c "import json;print(len(json.load(...)))"` 与
    //   `grep -c "public const string"` 在 HEAD 上量得）。
    public const string MsgFindings          = "msg.findings";

    // 数据位类型
    public const string DataTypeBool       = "data.type.bool";
    public const string DataTypeInt16      = "data.type.int16";
    public const string DataTypeInt32      = "data.type.int32";
    public const string DataTypeUInt16     = "data.type.uint16";
    public const string DataTypeUInt32     = "data.type.uint32";
    public const string DataTypeFloat32    = "data.type.float32";
    public const string DataTypeString     = "data.type.string";
    public const string RecipeCount          = "recipe.count";
    public const string RecipePromptGroupName          = "recipe.prompt.group.name";
    public const string RecipeWatermarkGroupName          = "recipe.watermark.group.name";
    public const string RecipeConfirmDeleteGroup          = "recipe.confirm.delete.group";
    public const string RecipeBuiltin          = "recipe.builtin";
    public const string RecipeNoDesc          = "recipe.no.desc";
    public const string RecipeSelectGroupFirst          = "recipe.select.group.first";
    public const string RecipePromptName          = "recipe.prompt.name";
    public const string RecipeWatermarkName          = "recipe.watermark.name";
    public const string RecipeSelectRecipeFirst          = "recipe.select.recipe.first";
    public const string RecipePromptCloneName          = "recipe.prompt.clone.name";
    public const string RecipeCopySuffix          = "recipe.copy.suffix";
    public const string RecipeCloned          = "recipe.cloned";
    public const string RecipeCloneFailed          = "recipe.clone.failed";
    public const string RecipeBuiltinNoDelete          = "recipe.builtin.no.delete";
    public const string RecipeConfirmDelete          = "recipe.confirm.delete";
    public const string RecipeDeleted          = "recipe.deleted";
    public const string RecipeNoSelected          = "recipe.no.selected";
    public const string RecipeNameEmpty          = "recipe.name.empty";
    public const string RecipeValidationDefaultBelow          = "recipe.validation.default.below";
    public const string RecipeValidationDefaultAbove          = "recipe.validation.default.above";
    public const string RecipeValidationFailed          = "recipe.validation.failed";
    public const string RecipeValidationMore          = "recipe.validation.more";
    public const string RecipeSavedFormat          = "recipe.saved.format";
    public const string RecipeSaveFailed          = "recipe.save.failed";
    public const string RecipeParamDefaultName          = "recipe.param.default.name";
    public const string RecipeParamAdded          = "recipe.param.added";
    public const string RecipeSelectParamFirst          = "recipe.select.param.first";
    public const string RecipeParamDeleted          = "recipe.param.deleted";
    public const string RecipeCsvImportTitle          = "recipe.csv.import.title";
    public const string RecipeCsvFileFilter          = "recipe.csv.file.filter";
    public const string RecipeCsvImportOk          = "recipe.csv.import.ok";
    public const string RecipeCsvImportInvalid          = "recipe.csv.import.invalid";
    public const string RecipeCsvImportWarnings         = "recipe.csv.import.warnings";
    public const string RecipeCsvExportTitle          = "recipe.csv.export.title";
    public const string RecipeCsvExportEmpty          = "recipe.csv.export.empty";
    public const string RecipeCsvExportOk          = "recipe.csv.export.ok";
    public const string GenOk          = "gen.ok";
    public const string GenConfirm          = "gen.confirm";
    public const string RecipeDialogInputTitle          = "recipe.dialog.input.title";
    public const string AiRoleYou          = "ai.role.you";
    public const string AiRoleAssistant          = "ai.role.assistant";
    public const string AiStatusConfigured          = "ai.status.configured";
    public const string AiSupportHint          = "ai.support.hint";
    public const string AiGenerating          = "ai.generating";
    public const string AiErrNetwork          = "ai.err.network";
    public const string AiErrValidationScreen          = "ai.err.validation.screen";
    public const string AiErrInvalidScreen          = "ai.err.invalid.screen";
    public const string AiErrValidationTemplate          = "ai.err.validation.template";
    public const string AiErrInvalidTemplate          = "ai.err.invalid.template";
    public const string AiErrValidationScript          = "ai.err.validation.script";
    public const string AiErrInvalidScript          = "ai.err.invalid.script";
    public const string AiWarningCount          = "ai.warning.count";
    public const string AiGeneratedScreen          = "ai.generated.screen";
    public const string AiGeneratedTemplate          = "ai.generated.template";
    public const string AiGeneratedScript          = "ai.generated.script";
    public const string AiTokenUsage          = "ai.token.usage";
    public const string AiAgentMode              = "ai.agent.mode";
    public const string AiAgentModeHint          = "ai.agent.mode.hint";
    public const string AiAgentRollback          = "ai.agent.rollback";
    public const string AiAgentRunning           = "ai.agent.running";
    public const string AiAgentNotReady          = "ai.agent.not.ready";
    public const string AiAgentRolledBack        = "ai.agent.rolled.back";
    public const string AiStreamingHint          = "ai.streaming.hint";
    public const string AiStreamingStatus        = "ai.streaming.status";
    public const string SimWaveDescSine          = "sim.wave.desc.sine";
    public const string SimWaveDescSquare          = "sim.wave.desc.square";
    public const string SimWaveDescSawtooth          = "sim.wave.desc.sawtooth";
    public const string SimWaveDescIncrement          = "sim.wave.desc.increment";
    public const string SimWaveDescRandom          = "sim.wave.desc.random";
    public const string SimWaveDescManual          = "sim.wave.desc.manual";
    public const string SimVarAdded          = "sim.var.added";
    public const string SimNoVars          = "sim.no.vars";
    public const string SimStop          = "sim.stop";
    public const string SimStarted          = "sim.started";
    public const string SimStopped          = "sim.stopped";
    public const string SimTickChanged          = "sim.tick.changed";
    public const string SimClockMultiplier      = "sim.clock.multiplier";
    public const string SimClockMultiplierTip   = "sim.tt.clock.multiplier";
    public const string SimClockChanged         = "sim.clock.changed";
    public const string SimRunningStats         = "sim.running.stats";
    public const string SimResetDone          = "sim.reset.done";
    public const string SimDisconnected          = "sim.disconnected";
    public const string SimCommDisconnected          = "sim.comm.disconnected";
    public const string SimCommReconnected          = "sim.comm.reconnected";
    public const string SimRespModeChanged          = "sim.resp.mode.changed";
    public const string SimInvalidAddr          = "sim.invalid.addr";
    public const string SimRegRead          = "sim.reg.read";
    public const string SimRegReadFailed          = "sim.reg.read.failed";
    public const string SimInvalidValue          = "sim.invalid.value";
    public const string SimRegWrite          = "sim.reg.write";
    public const string SimRegWriteFailed          = "sim.reg.write.failed";
    public const string SimRegLog          = "sim.reg.log";
    public const string FaceplateEditorMinControls          = "faceplate.editor.min.controls";
    public const string FaceplateEditorNewTemplate          = "faceplate.editor.new.template";
    public const string FaceplateEditorCategoryGeneral          = "faceplate.editor.category.general";
    public const string FaceplateEditorTitleNew          = "faceplate.editor.title.new";
    public const string FaceplateEditorTitleEdit          = "faceplate.editor.title.edit";
    public const string FaceplateEditorNoParams          = "faceplate.editor.no.params";
    public const string FaceplateEditorParamName          = "faceplate.editor.param.name";
    public const string FaceplateEditorParamDisplay          = "faceplate.editor.param.display";
    public const string FaceplateEditorParamDirection          = "faceplate.editor.param.direction";
    public const string FaceplateEditorParamDirInput          = "faceplate.editor.param.dir.input";
    public const string FaceplateEditorParamDirOutput          = "faceplate.editor.param.dir.output";
    public const string FaceplateEditorParamDirInOut          = "faceplate.editor.param.dir.inout";
    public const string FaceplateEditorParamType          = "faceplate.editor.param.type";
    public const string FaceplateTypeString              = "faceplate.type.string";
    public const string FaceplateTypeNumber              = "faceplate.type.number";
    public const string FaceplateTypeBoolean             = "faceplate.type.boolean";
    public const string FaceplateTypeColor               = "faceplate.type.color";
    public const string FaceplateTypeVariableId          = "faceplate.type.variableid";
    public const string FaceplateEditorParamDefault          = "faceplate.editor.param.default";
    public const string FaceplateEditorParamOptional          = "faceplate.editor.param.optional";
    public const string FaceplateEditorParamUnlimited          = "faceplate.editor.param.unlimited";
    public const string FaceplateEditorParamDescOptional          = "faceplate.editor.param.desc.optional";
    public const string FaceplateEditorParamDefaultName          = "faceplate.editor.param.default.name";
    public const string FaceplateEditorSummary          = "faceplate.editor.summary";
    public const string FaceplateEditorParamDup          = "faceplate.editor.param.dup";
    public const string UserMgrLocked          = "user.mgr.locked";
    public const string UserMgrInputUserPwd          = "user.mgr.input.user.pwd";
    public const string UserMgrUsernameExists          = "user.mgr.username.exists";
    public const string UserMgrAddedUser          = "user.mgr.added.user";
    public const string UserMgrRoleTab        = "user.mgr.role.tab";
    public const string UserMgrAdd            = "user.mgr.add";
    public const string AlarmDelete           = "alarm.delete";
    public const string AlarmDeleteGroup      = "alarm.delete.group";
    public const string CommConnNew           = "comm.conn.new";
    public const string RoleOperator          = "role.operator";
    public const string RoleEngineer          = "role.engineer";
    public const string RoleAdministrator     = "role.administrator";
    public const string RoleOperatorDesc      = "role.operator.desc";
    public const string RoleEngineerDesc      = "role.engineer.desc";
    public const string RoleAdministratorDesc = "role.administrator.desc";

    public const string UserMgrCannotDeleteAdmin          = "user.mgr.cannot.delete.admin";
    public const string UserMgrEnabled          = "user.mgr.enabled";
    public const string UserMgrDisabled          = "user.mgr.disabled";
    public const string UserMgrInputNewPwd          = "user.mgr.input.new.pwd";
    public const string UserMgrConfirmReset          = "user.mgr.confirm.reset";
    public const string UserMgrSetPwdFor          = "user.mgr.set.pwd.for";
    public const string UserMgrPwdReset          = "user.mgr.pwd.reset";
    public const string UserMgrPwdResetFailed    = "user.mgr.pwd.reset.failed";
    public const string UserMgrRoleUpdated          = "user.mgr.role.updated";
    public const string UserMgrRoleChanged          = "user.mgr.role.changed";
    public const string UserMgrProfileUpdated       = "user.mgr.profile.updated";
    public const string UserMgrAuditLogout          = "user.mgr.audit.logout";
    public const string UserMgrAuditCreateUser          = "user.mgr.audit.create.user";
    public const string UserMgrAuditPermChange          = "user.mgr.audit.perm.change";
    public const string UserMgrAuditProfileChange       = "user.mgr.audit.profile.change";
    public const string UserMgrAuditConfigChange          = "user.mgr.audit.config.change";
    public const string UserMgrAuditPwdChange          = "user.mgr.audit.pwd.change";
    public const string UserMgrAuditEmergency          = "user.mgr.audit.emergency";
    public const string UserMgrAuditExport          = "user.mgr.audit.export";
    public const string UserMgrAuditRefreshed          = "user.mgr.audit.refreshed";
    public const string UserMgrAuditCleared          = "user.mgr.audit.cleared";
    public const string AnimFilterAll          = "anim.filter.all";
    public const string AnimEasingLinear          = "anim.easing.linear";
    public const string AnimEasingEaseInOut          = "anim.easing.easeinout";
    public const string AnimEasingEaseIn          = "anim.easing.easein";
    public const string AnimEasingEaseOut          = "anim.easing.easeout";
    public const string AnimEasingElastic          = "anim.easing.elastic";
    public const string AnimEasingBounce          = "anim.easing.bounce";
    public const string AnimEasingStep          = "anim.easing.step";
    public const string AnimEasingGlobal          = "anim.easing.global";
    public const string AnimDefaultName          = "anim.default.name";
    public const string AnimConfirmDelete          = "anim.confirm.delete";
    public const string AnimStop          = "anim.stop";
    public const string AssetTypeImage         = "asset.type.image";
    public const string AssetTypeFont          = "asset.type.font";
    public const string AssetTypeLanguage      = "asset.type.language";
    public const string AssetTypeSvg           = "asset.type.svg";
    public const string AssetGroupHeader       = "asset.group.header";
    public const string AssetImportImgTitle    = "asset.import.img.title";
    public const string AssetFiletypeImg       = "asset.filetype.img";
    public const string AssetImgImported       = "asset.img.imported";
    public const string AssetImportSvgTitle    = "asset.import.svg.title";
    public const string AssetFiletypeSvg       = "asset.filetype.svg";
    public const string AssetFiletypeAll       = "asset.filetype.all";
    public const string AssetSvgNoShapes       = "asset.svg.no.shapes";
    public const string AssetSvgImported       = "asset.svg.imported";
    public const string AssetImportFontTitle   = "asset.import.font.title";
    public const string AssetFiletypeFont      = "asset.filetype.font";
    public const string AssetFontImported      = "asset.font.imported";
    public const string AssetRefreshed         = "asset.refreshed";
    public const string AssetOrphanNote        = "asset.orphan.note";
    public const string AssetRefOrphan         = "asset.ref.orphan";
    public const string AssetRefCount          = "asset.ref.count";
    public const string AlarmSevShortInfo      = "alarm.sev.short.info";
    public const string AlarmSevShortWarning   = "alarm.sev.short.warning";
    public const string AlarmSevShortCritical  = "alarm.sev.short.critical";
    public const string AlarmSevShortEmergency = "alarm.sev.short.emergency";
    public const string AlarmStateInactive     = "alarm.state.inactive";
    public const string AlarmReportTitle         = "alarm.report.title";
    public const string AlarmReportColState      = "alarm.report.col.state";
    public const string AlarmReportBtn           = "alarm.report.btn";
    public const string AlarmReportSaveTitle     = "alarm.report.save.title";
    public const string AlarmReportExportEmpty   = "alarm.report.export.empty";
    public const string UiOpExportAlarmReport    = "ui.op.export.alarm.report";
    public const string AlarmStateActive         = "alarm.state.active";
    public const string AlarmStateAcknowledged   = "alarm.state.acknowledged";
    public const string AlarmStateReturned       = "alarm.state.returned";
    public const string AlarmStatusSummary     = "alarm.status.summary";
    public const string StyleEditorTitle          = "style.editor.title";
    public const string StyleColorPrimary         = "style.color.primary";
    public const string StyleColorSecondary       = "style.color.secondary";
    public const string StyleColorSurface         = "style.color.surface";
    public const string StyleColorBackground      = "style.color.background";
    public const string StyleColorText            = "style.color.text";
    public const string StyleColorTextSecondary   = "style.color.text.secondary";
    public const string StyleColorBorder          = "style.color.border";
    public const string StyleDimFontSize          = "style.dim.font.size";
    public const string StyleDimRadius            = "style.dim.radius";
    public const string StyleDimBorderWidth       = "style.dim.border.width";
    public const string StylePresetTemplate       = "style.preset.template";
    public const string VarImportFiletypeCsv      = "var.import.filetype.csv";
    public const string VarImportSelected         = "var.import.selected";
    public const string VarImportParsing          = "var.import.parsing";
    public const string VarImportWarnings         = "var.import.warnings";
    public const string VarImportCompleted        = "var.import.completed";
    public const string TrendWindowSec            = "trend.window.sec";
    public const string TrendPenCount             = "trend.pen.count";
    public const string TrendDefaultName          = "trend.default.name";
    public const string TrendPenUnnamed           = "trend.pen.unnamed";
    public const string TrendPenDefaultName       = "trend.pen.default.name";
    public const string TrendStopRecord           = "trend.stop.record";
    public const string FindingPassed             = "finding.passed";
    public const string FindingLevelInfo          = "finding.level.info";
    public const string FindingCategoryStructure  = "finding.category.structure";
    public const string FindingCategoryBinding    = "finding.category.binding";
    public const string FindingCategoryExpression = "finding.category.expression";
    public const string FindingCategoryDangling   = "finding.category.dangling";
    public const string FindingCategoryGeneration = "finding.category.generation";
    public const string FindingCategoryRuntimeUser = "finding.category.runtime.user";
    public const string FindingCopied             = "finding.copied";
    /// <summary>「复制全部」里一行的形状（阶段 92 / #61j）：`[{0}] {1}: {2} ({3})`。</summary>
    public const string FindingCopyLine           = "finding.copy.line";
    public const string AiIconGeneratorUnavailable = "ai.icon.generator.unavailable";
    public const string AiIconNoItems              = "ai.icon.no.items";
    public const string AiIconDone                 = "ai.icon.done";
    public const string AiIconGenerated            = "ai.icon.generated";
    public const string AiIconNoneGenerated        = "ai.icon.none.generated";
    public const string AiImageMode                = "ai.image.mode";
    public const string AiIconMode                 = "ai.icon.mode";
    public const string AiImageHint                = "ai.image.hint";
    public const string AiImagePlaceholder         = "ai.image.placeholder";
    public const string AiImageSizeLabel           = "ai.image.size.label";
    public const string AiImageWidthLabel          = "ai.image.width.label";
    public const string AiImageHeightLabel         = "ai.image.height.label";
    public const string AiImageGenerated           = "ai.image.generated";
    public const string AiImageSaveSvg             = "ai.image.save.svg";
    public const string AiImageSavePng             = "ai.image.save.png";
    public const string AiImageAddToLibrary        = "ai.image.add.to.library";
    public const string AiImageGeneratorUnavailable = "ai.image.generator.unavailable";
    public const string TagMonitorConnected        = "tag.monitor.connected";
    public const string TagMonitorNoSource         = "tag.monitor.no.source";
    public const string TagMonitorNoValue          = "tag.monitor.no.value";
    public const string TagMonitorNoConnection     = "tag.monitor.no.connection";
    public const string TagMonitorWriteOnly        = "tag.monitor.write.only";
    public const string TagMonitorSummary          = "tag.monitor.summary";
    public const string PropExprEmpty              = "prop.expr.empty";
    public const string PropBindingReadonlyHint    = "prop.binding.readonly.hint";
    public const string PropBindingTargetMismatch  = "prop.binding.target.mismatch";
    public const string PropBindingActionOnlyHint  = "prop.binding.action.only.hint";

    // ── 状态化样式（完整四态：Normal/Pressed/Checked/Disabled）──
    public const string PropVisualStates           = "prop.visual.states";
    public const string PropVisualStateSelect      = "prop.visual.state.select";
    public const string PropVisualStateNormal      = "prop.visual.state.normal";
    public const string PropVisualStatePressed     = "prop.visual.state.pressed";
    public const string PropVisualStateChecked     = "prop.visual.state.checked";
    public const string PropVisualStateDisabled    = "prop.visual.state.disabled";
    public const string PropVisualStateBgColor     = "prop.visual.state.bg.color";
    public const string PropVisualStateTextColor   = "prop.visual.state.text.color";
    public const string PropVisualStateBorderColor = "prop.visual.state.border.color";
    public const string PropVisualStateImage       = "prop.visual.state.image";
    public const string PropVisualStateImageClear  = "prop.visual.state.image.clear";
    public const string PropVisualStateInheritHint = "prop.visual.state.inherit.hint";
    public const string PropVisualStateNoImage     = "prop.visual.state.no.image";
    public const string PropVisualStateText        = "prop.visual.state.text";
    public const string PropVisualStateTextAlign   = "prop.visual.state.text.align";
    public const string PropVisualStateTextVAlign  = "prop.visual.state.text.valign";
    public const string PropVisualStateAlignLeft   = "prop.visual.state.align.left";
    public const string PropVisualStateAlignCenter = "prop.visual.state.align.center";
    public const string PropVisualStateAlignRight  = "prop.visual.state.align.right";
    public const string PropVisualStateVAlignTop   = "prop.visual.state.valign.top";
    public const string PropVisualStateVAlignMid   = "prop.visual.state.valign.middle";
    public const string PropVisualStateVAlignBot   = "prop.visual.state.valign.bottom";
    public const string PropVisualStateInherit     = "prop.visual.state.inherit";

    // ── 值显示配置（Label 等显示型控件：绑定变量后的格式化）──
    public const string PropValueDisplay        = "prop.value.display";
    public const string PropValueDisplayType    = "prop.value.display.type";
    public const string PropValueDisplayAuto    = "prop.value.display.auto";
    public const string PropValueDisplayBool    = "prop.value.display.bool";
    public const string PropValueDisplayInt     = "prop.value.display.int";
    public const string PropValueDisplayFloat   = "prop.value.display.float";
    public const string PropValueDisplayString  = "prop.value.display.string";
    public const string PropValueDisplayScale   = "prop.value.display.scale";
    public const string PropValueDisplayDecimals = "prop.value.display.decimals";
    public const string PropValueDisplayPrefix  = "prop.value.display.prefix";
    public const string PropValueDisplaySuffix  = "prop.value.display.suffix";

    // ── 控件功能参数（按控件类型显隐）──
    public const string PropParamTitle       = "prop.param.title";
    public const string PropParamMin         = "prop.param.min";
    public const string PropParamMax         = "prop.param.max";
    public const string PropParamStep        = "prop.param.step";
    public const string PropParamOptions     = "prop.param.options";
    public const string PropParamSelected    = "prop.param.selected";
    public const string PropParamPlaceholder = "prop.param.placeholder";
    public const string PropParamMaxLen      = "prop.param.maxlen";
    public const string PropParamPassword    = "prop.param.password";
    // I10: 圆弧角度范围标签
    public const string ArcStartAngle        = "arc.start.angle";
    public const string ArcEndAngle          = "arc.end.angle";
    public const string PropParamTicks       = "prop.param.ticks";
    public const string PropParamRows        = "prop.param.rows";
    public const string PropParamCols        = "prop.param.cols";
    public const string PropParamHeader      = "prop.param.header";
    public const string PropParamHeaderHint  = "prop.param.header.hint";
    public const string PropParamRowHeight   = "prop.param.rowheight";
    public const string PropParamColWidths   = "prop.param.colwidths";
    public const string PropParamColWidthsHint = "prop.param.colwidths.hint";
    public const string PropTableCellsEdit   = "prop.table.cells.edit";
    public const string PropTableCellsTitle  = "prop.table.cells.title";
    public const string PropTableCellsHint   = "prop.table.cells.hint";
    public const string PropTableCellsInsert = "prop.table.cells.insert";
    public const string PropParamTabs        = "prop.param.tabs";
    public const string PropParamTabsHint    = "prop.param.tabs.hint";
    public const string PropParamStretch     = "prop.param.stretch";
    public const string PropParamBrightness  = "prop.param.brightness";
    public const string PropParamSpeed       = "prop.param.speed";
    public const string PropParamLineCap     = "prop.param.linecap";
    public const string PropParamKeyboard    = "prop.param.keyboard";

    // ── LVGL 标准行为配置 ──
    public const string PropIndicatorColor    = "prop.indicator.color";
    public const string PropOpacity           = "prop.opacity";
    public const string PropParamValueMode    = "prop.param.valmode";
    public const string PropParamValueModeNormal = "prop.param.valmode.normal";
    public const string PropParamValueModeSym = "prop.param.valmode.sym";
    public const string PropParamValueModeRange = "prop.param.valmode.range";
    public const string PropParamLongMode     = "prop.param.longmode";
    public const string PropParamLongModeWrap = "prop.param.longmode.wrap";
    public const string PropParamLongModeDots = "prop.param.longmode.dots";
    public const string PropParamLongModeScroll = "prop.param.longmode.scroll";
    public const string PropParamLongModeScrollCirc = "prop.param.longmode.scrollcirc";
    public const string PropParamLongModeClip = "prop.param.longmode.clip";
    public const string PropParamImage       = "prop.param.image";
    public const string PropParamImageNone   = "prop.param.image.none";
    public const string PropParamSvg         = "prop.param.svg";
    public const string PropParamSvgNone     = "prop.param.svg.none";

    public const string PropFaceplateTemplate      = "prop.faceplate.template";
    public const string PropFaceplateNoParams      = "prop.faceplate.no.params";
    public const string PropVarPlaceholder         = "prop.var.placeholder";
    public const string PropLayoutScore            = "prop.layout.score";
    public const string PropLayoutError            = "prop.layout.error";
    public const string PropLayoutWarning          = "prop.layout.warning";
    public const string PropLayoutGood             = "prop.layout.good";
    public const string PropIssueAutofix           = "prop.issue.autofix";
    public const string VarPickerRecommendTitle    = "var.picker.recommend.title";
    public const string VarPickerRecommendReason   = "var.picker.recommend.reason";
    public const string VarPickerNotLoaded         = "var.picker.not.loaded";
    public const string VarPickerRefLost           = "var.picker.ref.lost";
    public const string VarPickerUngrouped         = "var.picker.ungrouped";
    public const string VarPickerSystemPrefix      = "var.picker.system.prefix";
    public const string NavFlowNothing       = "nav.flow.nothing";
    public const string NavFlowSaved         = "nav.flow.saved";
    public const string NavFlowLoaded        = "nav.flow.loaded";
    public const string NavFlowSummary       = "nav.flow.summary";
    public const string ScreenStatusAdaptive = "screen.status.adaptive";
    public const string ScreenItemInfo       = "screen.item.info";
    public const string ScreenItemAdaptive   = "screen.item.adaptive";
    public const string ScreenCreated        = "screen.created";
    public const string ScreenSize           = "screen.size";

    // ── 公共页面（多用户共享查看 + 实时同步 + 权限控制）──
    public const string ScreenPublic            = "screen.public";
    public const string ScreenPublicHint        = "screen.public.hint";
    public const string ScreenPublicAccess      = "screen.public.access";
    public const string ScreenPublicItemBadge   = "screen.public.item.badge";
    public const string ScreenStatusPublic      = "screen.status.public";
    public const string EnumPublicAccessPublic  = "enum.public.access.public";
    public const string EnumPublicAccessViewers = "enum.public.access.viewers";
    public const string EnumPublicAccessAdmin   = "enum.public.access.admin";
    public const string PropScreenPublic        = "prop.screen.public";
    public const string PropScreenPublicAccess  = "prop.screen.public.access";

    // ── 图层引用（公共页面作为背景图层嵌入其他屏幕）──
    public const string ScreenLayerRefs         = "screen.layer.refs";
    public const string ScreenLayerRefsHint     = "screen.layer.refs.hint";
    public const string PropScreenLayerRefs     = "prop.screen.layer.refs";
    public const string ScreenLayerBadge        = "screen.layer.badge";
    public const string ScreenStatusLayers      = "screen.status.layers";

    // ── 屏幕页面属性面板（未选中控件时显示）──
    public const string PropScreen            = "prop.screen";
    public const string PropScreenPageSize    = "prop.screen.page.size";
    public const string PropScreenOrientation = "prop.screen.orientation";
    public const string PropScreenBg          = "prop.screen.bg";
    public const string PropScreenBgImage     = "prop.screen.bg.image";
    public const string PropScreenBgImageChoose = "prop.screen.bg.image.choose";
    public const string PropScreenBgImageClear  = "prop.screen.bg.image.clear";
    public const string PropScreenBgImageMode   = "prop.screen.bg.image.mode";
    public const string PropScreenBgImageModeCenter  = "prop.screen.bg.image.mode.center";
    public const string PropScreenBgImageModeStretch = "prop.screen.bg.image.mode.stretch";
    public const string PropScreenBgImageModeTile    = "prop.screen.bg.image.mode.tile";
    public const string PropScreenBgImageNone        = "prop.screen.bg.image.none";
    public const string PropScreenBgImageOpacity     = "prop.screen.bg.image.opacity";
    public const string PropScreenLayout      = "prop.screen.layout";
    public const string PropScreenAdaptive    = "prop.screen.adaptive";
    public const string PropScreenDpi         = "prop.screen.dpi";
    public const string PropScreenSafeArea    = "prop.screen.safe.area";
    public const string PropScreenSafeLeft    = "prop.screen.safe.left";
    public const string PropScreenSafeTop     = "prop.screen.safe.top";
    public const string PropScreenSafeRight   = "prop.screen.safe.right";
    public const string PropScreenSafeBottom  = "prop.screen.safe.bottom";
    public const string PropScreenInteraction = "prop.screen.interaction";
    public const string PropScreenBoot        = "prop.screen.boot";
    public const string FontNoVisibleText    = "font.no.visible.text";
    public const string FontExportTitle      = "font.export.title";
    public const string FontFiletypeTxt      = "font.filetype.txt";
    public const string FontExported         = "font.exported";
    public const string TbScreen       = "tb.screen";
    public const string TbButton       = "tb.button";
    public const string TbSlider       = "tb.slider";
    public const string TbSwitch       = "tb.switch";
    public const string TbCheckbox     = "tb.checkbox";
    public const string TbLabel        = "tb.label";
    public const string TbImage        = "tb.image";
    public const string TbBar          = "tb.bar";
    public const string TbDropdown     = "tb.dropdown";
    public const string TbTextarea     = "tb.textarea";
    public const string TbGauge        = "tb.gauge";
    public const string TbKnob         = "tb.knob";
    public const string TbKeyboard     = "tb.keyboard";
    public const string TbLed          = "tb.led";
    public const string TbSpinner      = "tb.spinner";
    public const string TbTabview      = "tb.tabview";
    public const string TbLine         = "tb.line";
    public const string TbTable        = "tb.table";
    public const string TbSvg          = "tb.svg";
    // I7: 过程图形控件
    public const string TbTank         = "tb.tank";
    public const string TbPipe         = "tb.pipe";
    public const string TbValve        = "tb.valve";
    public const string TbCatProcess   = "tb.cat.process";
    public const string TbCatContainer = "tb.cat.container";
    public const string TbCatInput     = "tb.cat.input";
    public const string TbCatDisplay   = "tb.cat.display";
    public const string TbCatChart     = "tb.cat.chart";
    public const string TbCatSelect    = "tb.cat.select";
    public const string TbCatSpecial   = "tb.cat.special";
    // I10: 基础绘图图形
    public const string TbRectangle    = "tb.rectangle";
    public const string TbEllipse      = "tb.ellipse";
    public const string TbPolyline     = "tb.polyline";
    public const string TbArc          = "tb.arc";
    public const string TbCatDraw      = "tb.cat.draw";

    // ── I10: 绘图工具栏 ──
    public const string DrawGroupShapes = "draw.group.shapes";
    public const string DrawGroupControls = "draw.group.controls";
    public const string DrawSelect     = "draw.select";
    public const string DrawRectangle  = "draw.rectangle";
    public const string DrawEllipse    = "draw.ellipse";
    public const string DrawPolyline   = "draw.polyline";
    public const string DrawArc        = "draw.arc";
    public const string DrawLine       = "draw.line";
    public const string DrawText       = "draw.text";
    public const string DrawHint       = "draw.hint";
    public const string DrawEscExit    = "draw.esc.exit";

    // ── 自动同步（JSON 有但 Lang.cs 原无常量）──
    public const string AppAbout                      = "app.about";
    public const string AppTitle                      = "app.title";
    public const string CanvasDragHint                = "canvas.drag.hint";
    public const string GeneralControls               = "general.controls";
    public const string GeneralScreens                = "general.screens";
    public const string GeneralUntitled               = "general.untitled";
    public const string MenuEdit                      = "menu.edit";
    public const string MenuEditDelete                = "menu.edit.delete";
    public const string MenuFile                      = "menu.file";
    public const string MenuHelp                      = "menu.help";
    public const string MenuHelpAbout                 = "menu.help.about";
    public const string MenuLanguage                  = "menu.language";
    public const string MenuView                      = "menu.view";
    public const string MenuViewThemeEditor           = "menu.view.themeEditor";
    public const string PackageAssetsMissing          = "package.assets.missing";
    public const string ReslibSaveSuspended           = "reslib.save.suspended";
    public const string ScreenManager                 = "screen.manager";
    public const string ScreenMinCount                = "screen.minCount";
    public const string StatusControls                = "status.controls";
    public const string StatusCurrent                 = "status.current";
    public const string StatusExported                = "status.exported";
    public const string StatusReady                   = "status.ready";
    public const string StatusSaved                   = "status.saved";
    public const string StatusSelected                = "status.selected";
    public const string ThemeApply                    = "theme.apply";
    public const string ThemeColors                   = "theme.colors";
    public const string ThemeDimensions               = "theme.dimensions";
    public const string ThemeEditor                   = "theme.editor";
    public const string ThemeName                     = "theme.name";
    public const string ThemeReset                    = "theme.reset";
    public const string ToolbarNewScreen              = "toolbar.newScreen";
    public const string ToolbarTheme                  = "toolbar.theme";

    // ── MainWindow 补全（自动生成）──
    public const string MenuValidateCode              = "menu.validate.code";
    public const string MenuExportSimulator           = "menu.export.simulator";
    public const string MenuCreateTemplate            = "menu.create.template";
    public const string MenuPanel                     = "menu.panel";
    public const string MenuReloadPanels              = "menu.reload.panels";
    public const string MenuTools                     = "menu.tools";
    public const string MenuGitInit                   = "menu.git.init";
    public const string MenuGitCommit                 = "menu.git.commit";
    public const string MenuGitHistory                = "menu.git.history";
    public const string MenuGitRefresh                = "menu.git.refresh";
    public const string MenuLangZhCN                  = "menu.lang.zhCN";
    public const string CbResolutionOff               = "cb.resolution.off";
    public const string CbResolution800x480           = "cb.resolution.800x480";
    public const string CbResolution1024x600          = "cb.resolution.1024x600";
    public const string CbResolution1280x800          = "cb.resolution.1280x800";
    public const string CbResolution480x800           = "cb.resolution.480x800";
    public const string ToolbarReloadPanels           = "toolbar.reloadPanels";
    public const string ToolbarReloadPanelsTt         = "toolbar.reloadPanels.tt";
    public const string ToolbarAlignLeft              = "toolbar.alignLeft";
    public const string ToolbarAlignRight             = "toolbar.alignRight";
    public const string ToolbarAlignTop               = "toolbar.alignTop";
    public const string ToolbarAlignTopTt             = "toolbar.alignTop.tt";
    public const string ToolbarAlignBottom            = "toolbar.alignBottom";
    public const string ToolbarAlignBottomTt          = "toolbar.alignBottom.tt";
    public const string ToolbarAlignCenterH           = "toolbar.alignCenterH";
    public const string ToolbarAlignCenterV           = "toolbar.alignCenterV";
    public const string StatusTarget                  = "status.target";
    /// <summary>资源仪表盘：硬件目标没声明该轴预算时的提示（缺依据就不显示百分比）。</summary>
    public const string StatusResourceNoBudget        = "status.resource.no.budget";
    public const string StatusGitNotReady             = "status.git.not.ready";
    public const string StatusReloadingPanels         = "status.reloading.panels";
    public const string StatusPanelsReloaded          = "status.panels.reloaded";
    public const string StatusReloadFailed            = "status.reload.failed";
    public const string StatusAssetInfo               = "status.asset.info";
    public const string StatusSaveFirst               = "status.save.first";
    public const string StatusIconsGenerated          = "status.icons.generated";
    public const string StatusSelectSvgFirst          = "status.select.svg.first";
    public const string StatusSvgApplied              = "status.svg.applied";
    public const string AppSlogan                     = "app.slogan";
    public const string StatusPreviewNoAction         = "status.preview.no.action";
    public const string StatusLoggedIn                = "status.logged.in";
    public const string StatusUnknownUser             = "status.unknown.user";
    public const string StatusGitUnversioned          = "status.git.unversioned";
    public const string StatusGitInfo                 = "status.git.info";
    public const string ConfirmDiscardChanges         = "confirm.discard.changes";
    public const string StatusProjectCancelled        = "status.project.cancelled";
    public const string StatusProjectCreated          = "status.project.created";
    public const string DialogOpenHmiProject          = "dialog.open.hmi.project";
    public const string FileTypeHmiProject            = "file.type.hmi.project";
    public const string FileTypeAll                   = "file.type.all";
    public const string StatusProjectLoaded           = "status.project.loaded";
    public const string DialogSaveHmiProject          = "dialog.save.hmi.project";
    public const string StatusProjectSaved            = "status.project.saved";
    public const string DialogSelectExportDir         = "dialog.select.export.dir";
    public const string AuditExportCCode              = "audit.export.c.code";
    public const string StatusVerifyCompleted         = "status.verify.completed";
    public const string StatusVerifyGenerated         = "status.verify.generated";
    public const string StatusVerifyFailed             = "status.verify.failed";
    public const string AuditVerifyCodeFailed         = "audit.verify.code.failed";
    public const string GitBranch                     = "git.branch";
    public const string GitUncommitted                = "git.uncommitted";
    public const string StatusSaveFirstForGit         = "status.save.first.for.git";
    public const string StatusGitNotDetected          = "status.git.not.detected";
    public const string StatusGitInitialized         = "status.git.initialized";
    public const string StatusGitInitFailed           = "status.git.init.failed";

    // ── 模拟器导出 ──
    public const string DialogSelectSimulatorDir    = "dialog.select.simulator.dir";
    public const string StatusSimulatorBlockedErrors = "status.simulator.blocked.errors";
    public const string StatusSimulatorBlockedCount  = "status.simulator.blocked.count";
    public const string StatusSimulatorExported      = "status.simulator.exported";
    public const string AuditSimulatorExport          = "audit.simulator.export";

    // ── 预览模式 ──
    public const string StatusPreviewScreenMissing   = "status.preview.screen.missing";
    public const string StatusPreviewScreenSwitched  = "status.preview.screen.switched";
    public const string StatusPreviewScriptMissing   = "status.preview.script.missing";
    public const string StatusPreviewScriptExecuting = "status.preview.script.executing";
    public const string StatusPreviewSystemAction    = "status.preview.system.action";
    public const string StatusPreviewTags            = "status.preview.tags";
    public const string StatusPreviewModeBound       = "status.preview.mode.bound";
    public const string StatusPreviewModeConnected   = "status.preview.mode.connected";

    // ── Git 提交对话框 ──
    public const string StatusGitNoRepo              = "status.git.no.repo";
    public const string DialogGitCommitPlaceholder   = "dialog.git.commit.placeholder";
    public const string DialogGitCommitTitle         = "dialog.git.commit.title";
    public const string DialogGitCommitUncommitted   = "dialog.git.commit.uncommitted";
    public const string DialogGitCommitButton        = "dialog.git.commit.button";
    public const string StatusGitCommitEmpty         = "status.git.commit.empty";
    public const string StatusGitCommitted            = "status.git.committed";
    public const string StatusGitCommitFailed        = "status.git.commit.failed";

    // ── Git 历史 ──
    public const string DialogGitHistoryTitle       = "dialog.git.history.title";
    public const string DialogGitHistoryRecent      = "dialog.git.history.recent";
    public const string DialogGitHistoryEmpty        = "dialog.git.history.empty";

    // ── 模板/控件选择 ──
    public const string StatusControlsSelected       = "status.controls.selected";
    public const string StatusTemplateCreated        = "status.template.created";
    public const string StatusTemplateUpdated        = "status.template.updated";

    // ── 分辨率预览 ──
    public const string StatusAdaptiveNotEnabled    = "status.adaptive.not.enabled";
    public const string StatusResolutionPreview      = "status.resolution.preview";

    // ── 其他 ──
    public const string StatusSelectControlsFirst    = "status.select.controls.first";
    public const string DialogConfirmNewProject      = "dialog.confirm.new.project";
    public const string DialogButtonDiscardChanges   = "dialog.button.discard.changes";

    // ── View ToolTip 同步（自动生成）──
    public const string SimTtAddVar                   = "sim.tt.add.var";
    public const string SimTtDelVar                   = "sim.tt.del.var";
    public const string SimTtSignalType               = "sim.tt.signal.type";
    public const string SimTtConst                    = "sim.tt.const";
    public const string SimTtSine                     = "sim.tt.sine";
    public const string SimTtSquare                   = "sim.tt.square";
    public const string SimTtRamp                     = "sim.tt.ramp";
    public const string SimTtIncrement                = "sim.tt.increment";
    public const string SimTtRandom                   = "sim.tt.random";
    public const string SimTtManual                   = "sim.tt.manual";
    public const string SimTtAmplitude                = "sim.tt.amplitude";
    public const string SimTtOffset                   = "sim.tt.offset";
    public const string SimTtFreq                     = "sim.tt.freq";
    public const string SimTtMinVal                   = "sim.tt.min.val";
    public const string SimTtMaxVal                   = "sim.tt.max.val";
    public const string SimTtNoise                    = "sim.tt.noise";
    public const string SimTtManualVal                = "sim.tt.manual.val";
    public const string SimTtWaveDesc                 = "sim.tt.wave.desc";
    public const string SimTtStartStop                = "sim.tt.start.stop";
    public const string SimTtTickInterval             = "sim.tt.tick.interval";
    public const string SimTtReset                    = "sim.tt.reset";
    public const string SimTtDisconnect               = "sim.tt.disconnect";
    public const string SimTtReconnect                = "sim.tt.reconnect";
    public const string SimTtWriteMode                = "sim.tt.write.mode";
    public const string SimTtEcho                     = "sim.tt.echo";
    public const string SimTtInvert                   = "sim.tt.invert";
    public const string SimTtRandomResp               = "sim.tt.random.resp";
    public const string SimTtRespond                  = "sim.tt.respond";
    public const string SimTtRegAddr                  = "sim.tt.reg.addr";
    public const string SimTtRegVal                   = "sim.tt.reg.val";
    public const string SimTtReadReg                  = "sim.tt.read.reg";
    public const string SimTtWriteReg                 = "sim.tt.write.reg";

    // ── View ToolTip 同步（自动生成）──
    public const string RecipeTtAddGroup              = "recipe.tt.add.group";
    public const string RecipeTtDelGroup              = "recipe.tt.del.group";
    public const string RecipeTtNew                   = "recipe.tt.new";
    public const string RecipeTtClone                 = "recipe.tt.clone";
    public const string RecipeTtDelRecipe             = "recipe.tt.del.recipe";
    public const string RecipeTtImportCsv             = "recipe.tt.import.csv";
    public const string RecipeTtExportCsv             = "recipe.tt.export.csv";
    public const string RecipeTtName                  = "recipe.tt.name";
    public const string RecipeTtGroup                 = "recipe.tt.group";
    public const string RecipeTtDesc                  = "recipe.tt.desc";
    public const string RecipeTtEnabled               = "recipe.tt.enabled";
    public const string RecipeTtAddParam              = "recipe.tt.add.param";
    public const string RecipeTtDelParam              = "recipe.tt.del.param";
    public const string RecipeTtUpParam               = "recipe.tt.up.param";
    public const string RecipeTtDownParam             = "recipe.tt.down.param";
    public const string RecipeTtParamLabel            = "recipe.tt.param.label";
    public const string RecipeTtParamVar              = "recipe.tt.param.var";
    public const string RecipeTtParamDefault          = "recipe.tt.param.default";
    public const string RecipeTtParamUnit             = "recipe.tt.param.unit";
    public const string RecipeTtParamMin              = "recipe.tt.param.min";
    public const string RecipeTtParamMax              = "recipe.tt.param.max";
    public const string RecipeTtParamType             = "recipe.tt.param.type";
    public const string RecipeTtParamOrder            = "recipe.tt.param.order";
    public const string RecipeTtSave                  = "recipe.tt.save";

    // ── View ToolTip 同步（自动生成）──
    public const string AnimTtAdd                     = "anim.tt.add";
    public const string AnimTtTriggerFilter           = "anim.tt.trigger.filter";
    public const string AnimTtDel                     = "anim.tt.del";
    public const string AnimTtName                    = "anim.tt.name";
    public const string AnimTtTarget                  = "anim.tt.target";
    public const string AnimTtProp                    = "anim.tt.prop";
    public const string AnimTtDesc                    = "anim.tt.desc";
    public const string AnimTtEnabled                 = "anim.tt.enabled";
    public const string AnimTtTrigger                 = "anim.tt.trigger";
    public const string AnimTtFromVal                 = "anim.tt.from.val";
    public const string AnimTtToVal                   = "anim.tt.to.val";
    public const string AnimTtDuration                = "anim.tt.duration";
    public const string AnimTtDelay                   = "anim.tt.delay";
    public const string AnimTtEasing                  = "anim.tt.easing";
    public const string AnimTtRepeat                  = "anim.tt.repeat";
    public const string AnimTtPlayPreview             = "anim.tt.play.preview";
    public const string AnimTtAddKeyframe             = "anim.tt.add.keyframe";
    public const string AnimTtDelKeyframe             = "anim.tt.del.keyframe";
    public const string AnimTtKfTime                  = "anim.tt.kf.time";
    public const string AnimTtKfVal                   = "anim.tt.kf.val";
    public const string AnimTtKfEasing                = "anim.tt.kf.easing";

    // ── View ToolTip 同步（自动生成）──
    public const string TrendTtAdd                    = "trend.tt.add";
    public const string TrendTtDel                    = "trend.tt.del";
    public const string TrendTtAddPen                 = "trend.tt.add.pen";
    public const string TrendTtDelPen                 = "trend.tt.del.pen";

    // ── View ToolTip 同步（自动生成）──
    public const string FaceplateTtAiGen              = "faceplate.tt.ai.gen";
    public const string FaceplateTtEdit               = "faceplate.tt.edit";
    public const string FaceplateTtDel                = "faceplate.tt.del";
    public const string FaceplateTtCreateFromCtrl     = "faceplate.tt.create.from.ctrl";

    // ── View ToolTip 同步（自动生成）──
    public const string NavFlowTtRebuild              = "nav.flow.tt.rebuild";
    public const string NavFlowTtSave                 = "nav.flow.tt.save";
    public const string NavFlowTtLoad                 = "nav.flow.tt.load";

    // ── View ToolTip 同步（自动生成）──
    public const string VarImportTtParse              = "var.import.tt.parse";
    public const string VarImportTtAdd                = "var.import.tt.add";

    // ── View ToolTip 同步（自动生成）──

    // ── View ToolTip 同步（自动生成）──
    public const string ProjectTtDel                  = "project.tt.del";

    // ── View ToolTip 同步（自动生成）──
    public const string CanvasTtTogglePreview         = "canvas.tt.toggle.preview";

    // ── View ToolTip 同步（自动生成）──
    public const string AssetTtAiSvg                  = "asset.tt.ai.svg";

    // ── 非 ToolTip 硬编码本地化（手动生成）──
    public const string ProjectSettingsHistoryPathHint   = "project.settings.history.path.hint";
    public const string ProjectSettingsAuditPathHint     = "project.settings.audit.path.hint";
    public const string VarUdtNameHint                   = "var.udt.name.hint";
    public const string VarUdtFieldHint                  = "var.udt.field.hint";
    public const string FontSubsetActualCharset          = "font.subset.actual.charset";
    public const string FontSubsetNoText                 = "font.subset.no.text";
    public const string FontSubsetExport                 = "font.subset.export";
    public const string TagMonitorValueHint              = "tag.monitor.value.hint";
    public const string FaceplateTemplateCategory        = "faceplate.template.category";

    // ── .axaml.cs 硬编码本地化（手动生成）──
    public const string AlarmCondHighLimit        = "alarm.cond.high.limit";
    public const string AlarmCondLowLimit         = "alarm.cond.low.limit";
    public const string AlarmCondBitTrigger       = "alarm.cond.bit.trigger";
    public const string AlarmCondStateChange      = "alarm.cond.state.change";
    public const string AlarmCondDeviation        = "alarm.cond.deviation";
    public const string AlarmCount                = "alarm.count";
    public const string AlarmGroupCreated         = "alarm.group.created";
    public const string AiRetryHint               = "ai.retry.hint";
    public const string VarUdtCreatedType         = "var.udt.created.type";
    public const string VarUdtInstanceNameEmpty   = "var.udt.instance.name.empty";
    public const string StylePresetApply          = "style.preset.apply";
    public const string LoginInputRequired        = "login.input.required";
    public const string LoginInProgress           = "login.in.progress";

    // ── AlarmConfigView 补充（手动生成）──
    public const string AlarmKeepAtLeastOne   = "alarm.keep.at.least.one";
    public const string AlarmSelectedGroup    = "alarm.selected.group";
    public const string AlarmCreated          = "alarm.created";
    public const string AlarmUpdated          = "alarm.updated";

    // ── ProjectExplorer/ProjectSettings 补充 ──
    public const string ProjectExplorerAlarms       = "project.explorer.alarms";
    public const string ProjectExplorerTrends       = "project.explorer.trends";
    public const string ProjectSettingsUseDefault   = "project.settings.use.default";

    // ── 项目浏览器右键增强菜单 ──
    public const string ExplorerCtxNew          = "explorer.ctx.new";
    public const string ExplorerCtxOpen        = "explorer.ctx.open";
    public const string ExplorerCtxStartup     = "explorer.ctx.startup";
    public const string ExplorerCtxPublic      = "explorer.ctx.public";
    public const string ExplorerCtxPublicOff   = "explorer.ctx.public.off";
    public const string ExplorerCtxRename      = "explorer.ctx.rename";
    public const string ExplorerCtxDuplicate   = "explorer.ctx.duplicate";
    public const string ExplorerCtxDelete      = "explorer.ctx.delete";
    public const string ExplorerCtxCopy        = "explorer.ctx.copy";
    public const string ExplorerCtxPaste       = "explorer.ctx.paste";
    public const string ExplorerCtxMoveUp      = "explorer.ctx.move.up";
    public const string ExplorerCtxMoveDown    = "explorer.ctx.move.down";
    public const string ExplorerRenameTitle    = "explorer.rename.title";
    public const string ExplorerStartupDone    = "explorer.startup.done";
    public const string ExplorerPublicDone     = "explorer.public.done";
    public const string ExplorerPublicOffDone  = "explorer.public.off.done";
    public const string ExplorerRenamedDone    = "explorer.renamed.done";
    public const string ExplorerDuplicatedDone = "explorer.duplicated.done";
    public const string ExplorerDeletedDone    = "explorer.deleted.done";
    public const string ExplorerCopyDone       = "explorer.copy.done";
    public const string ExplorerPasteDone      = "explorer.paste.done";
    public const string ExplorerMoveDone       = "explorer.move.done";
    public const string ExplorerNoClipboard    = "explorer.no.clipboard";
    public const string ExplorerMinScreen      = "explorer.min.screen";
    public const string ExplorerDeleteConfirm  = "explorer.delete.confirm";
    public const string ExplorerEnabled        = "explorer.enabled";
    public const string ExplorerMenuItems      = "explorer.menu.items";
    public const string ExplorerDuplicatedSuffix = "explorer.duplicated.suffix";

    // ── 设置：项目浏览器配置页 ──
    public const string SettingsExplorerTitle = "settings.explorer.title";
    public const string SettingsExplorerDesc  = "settings.explorer.desc";

    // ── 设置：PC 模拟器配置页 ──
    public const string SettingsSimulatorNav       = "settings.simulator.nav";
    public const string SettingsSimulatorTitle     = "settings.simulator.title";
    public const string SettingsSimulatorDesc      = "settings.simulator.desc";
    public const string SettingsSimulatorLvglDir   = "settings.simulator.lvgl.dir";
    public const string SettingsSimulatorLvglHint  = "settings.simulator.lvgl.hint";
    public const string SettingsSimulatorWorkDir   = "settings.simulator.work.dir";
    public const string SettingsSimulatorWorkHint  = "settings.simulator.work.hint";
    public const string SettingsDeviceNav          = "settings.device.nav";
    public const string SettingsDeviceTitle        = "settings.device.title";
    public const string SettingsDeviceDesc         = "settings.device.desc";
    public const string SettingsDeviceSdkDir       = "settings.device.sdk.dir";
    public const string SettingsDeviceSdkHint      = "settings.device.sdk.hint";
    public const string SettingsDeviceToolchainDir = "settings.device.toolchain.dir";
    public const string SettingsDeviceToolchainHint = "settings.device.toolchain.hint";

    // ── ScriptEditorView Lua 帮助文本 ──
    public const string ScriptLuaTemplate   = "script.lua.template";
    public const string ScriptLuaHelpText   = "script.lua.help.text";

    // ── 第一批：面板标题+颜色+撤销+错误提示（手动生成）──
    public const string AssetSvgNoShapesHint   = "asset.svg.no.shapes.hint";
    public const string StatusCanvasDropHint   = "status.canvas.drop.hint";
    public const string DockTitleToolbox       = "dock.title.toolbox";
    public const string DockTitleCanvas        = "dock.title.canvas";
    public const string DockTitleExplorer      = "dock.title.explorer";
    public const string DockTitleAudit         = "dock.title.audit";
    public const string DockTitleCrossRef      = "dock.title.crossref";
    public const string DockTitleProperty      = "dock.title.property";
    public const string DockTitleLayers        = "dock.title.layers";
    public const string DockTitleAssets        = "dock.title.assets";
    public const string DockTitleNavigation    = "dock.title.navigation";
    public const string DockTitleAiChat        = "dock.title.aichat";
    public const string DockTitleResourceLibrary = "dock.title.resource.library";
    public const string ColorBlue              = "color.blue";
    public const string ColorSky               = "color.sky";
    public const string ColorCyan              = "color.cyan";
    public const string ColorGreen             = "color.green";
    public const string ColorYellow            = "color.yellow";
    public const string ColorOrange            = "color.orange";
    public const string ColorRed               = "color.red";
    public const string ColorPink              = "color.pink";
    public const string ColorPurple            = "color.purple";
    public const string ColorWhite             = "color.white";
    public const string ColorGray              = "color.gray";
    public const string ColorDark              = "color.dark";
    public const string ColorPickerTitle       = "color.picker.title";
    public const string ColorPickerClear       = "color.picker.clear";
    public const string CanvasPreviewBanner    = "canvas.preview.banner";
    public const string UndoMove               = "undo.move";
    public const string UndoAdd                = "undo.add";
    public const string UndoDelete             = "undo.delete";
    public const string UndoModify             = "undo.modify";
    public const string AiErrNotConfigured     = "ai.err.not.configured";
    public const string TplErrNoControls       = "tpl.err.no.controls";

    // ── 第二批：TemplateManager 预置模板 ──
    public const string TplMotorName         = "tpl.motor.name";
    public const string TplMotorDesc         = "tpl.motor.desc";
    public const string TplMotorPTitle       = "tpl.motor.p.title";
    public const string TplMotorPStatusVar   = "tpl.motor.p.statusvar";
    public const string TplStartStopName     = "tpl.startstop.name";
    public const string TplStartStopDesc     = "tpl.startstop.desc";
    public const string TplValveName         = "tpl.valve.name";
    public const string TplValveDesc         = "tpl.valve.desc";
    public const string TplValvePValveName   = "tpl.valve.p.valvename";
    public const string TplValvePOpenVar     = "tpl.valve.p.openvar";
    public const string TplMotorIndName      = "tpl.motorind.name";
    public const string TplMotorIndDesc      = "tpl.motorind.desc";

    // ── 第二批补充：Template 按钮文本 ──
    public const string TplStartStopBtnStart     = "tpl.startstop.btn.start";
    public const string TplStartStopBtnStop      = "tpl.startstop.btn.stop";
    public const string TplMotorIndTextRunning   = "tpl.motorind.text.running";

    // ── 第三批：Faceplate 分类 + VariablePicker ──
    public const string FpCatIndustrial   = "fp.cat.industrial";
    public const string FpCatPiping       = "fp.cat.piping";
    public const string FpCatInstrument   = "fp.cat.instrument";
    public const string FpCatSafety       = "fp.cat.safety";
    public const string FpCatGeneral      = "fp.cat.general";
    public const string VarPickerBrowse   = "var.picker.browse";

    // ── Simulator: 本地化名/ID 分离 ──
    public const string SimVarName   = "sim.var.name";

    // ── ProjectSettings: 选取模式 + 默认字体 ──
    public const string FontDefaultPlaceholder   = "font.default.placeholder";

    // ── ProjectSettings: 6 个枚举下拉本地化 ──
    public const string EnumOrientationLandscape   = "enum.orientation.landscape";
    public const string EnumOrientationPortrait    = "enum.orientation.portrait";
    public const string EnumAdaptiveNone           = "enum.adaptive.none";
    public const string EnumAdaptiveFit            = "enum.adaptive.fit";
    public const string EnumAdaptiveFill           = "enum.adaptive.fill";
    public const string EnumRecoveryAuto           = "enum.recovery.auto";
    public const string EnumRecoveryManual         = "enum.recovery.manual";
    public const string EnumRecoveryFailFast       = "enum.recovery.failfast";
    public const string EnumLogLevelDebug          = "enum.loglevel.debug";
    public const string EnumLogLevelInfo           = "enum.loglevel.info";
    public const string EnumLogLevelWarning        = "enum.loglevel.warning";
    public const string EnumLogLevelError          = "enum.loglevel.error";
    public const string EnumLogLevelFatal          = "enum.loglevel.fatal";
    public const string EnumOptSize                = "enum.opt.size";
    public const string EnumOptSpeed               = "enum.opt.speed";
    public const string EnumOptReadability         = "enum.opt.readability";
    public const string EnumOpModeDesigner         = "enum.opmode.designer";
    public const string EnumOpModeOperator         = "enum.opmode.operator";
    public const string EnumOpModeMaintenance      = "enum.opmode.maintenance";
    public const string EnumOpModeReadonly         = "enum.opmode.readonly";

    // ── 编译转换 / 下载到 HMI ──
    public const string ToolbarCompile        = "toolbar.compile";
    public const string ToolbarCompileTt      = "toolbar.compile.tt";
    public const string ToolbarDownload       = "toolbar.download";
    public const string ToolbarDownloadTt     = "toolbar.download.tt";
    public const string MenuCompile           = "menu.compile";
    public const string MenuDownload          = "menu.download";
    public const string CompileProgressStart  = "compile.progress.start";
    public const string CompileProgressGen    = "compile.progress.generating";
    public const string CompileProgressSave   = "compile.progress.saving";
    public const string CompileOutput         = "compile.output";
    public const string CompileSoBuilding     = "compile.so.building";
    public const string CompileSoSuccess      = "compile.so.success";
    public const string CompileSoFailed       = "compile.so.failed";
    public const string CompileSoToolchainMissing = "compile.so.toolchain.missing";
    public const string DownloadNeedCompile   = "download.need.compile";
    public const string DownloadAutoSharedLib = "download.auto.sharedlibrary";

    // ── 编译并运行（PC 模拟器集成）──
    public const string MenuBuildRun             = "menu.build.run";
    public const string ToolbarBuildRun          = "toolbar.build.run";
    public const string ToolbarBuildRunTt        = "toolbar.build.run.tt";
    public const string WinBuildRun              = "win.build.run";
    public const string BuildRunPhaseReady       = "buildrun.phase.ready";
    public const string BuildRunPhaseRun         = "buildrun.phase.run";
    public const string BuildRunStatusRunning    = "buildrun.status.running";
    public const string BuildRunStatusExited     = "buildrun.status.exited";
    public const string BuildRunStatusExitedAbn  = "buildrun.status.exited.abnormal";
    public const string BuildRunStatusCompleted  = "buildrun.status.completed";
    public const string BuildRunStatusFailed     = "buildrun.status.failed";
    public const string BuildRunStatusCancelled  = "buildrun.status.cancelled";
    public const string BuildRunErrorsTitle      = "buildrun.errors.title";
    public const string BuildRunBtnStop          = "buildrun.btn.stop";
    public const string BuildRunBtnClose         = "buildrun.btn.close";
    public const string BuildRunNoLvgl           = "buildrun.no.lvgl";
    public const string DownloadTargetHint    = "download.target.hint";
    public const string DownloadSoLabel       = "download.so.label";
    public const string DownloadSoBrowse      = "download.so.browse";
    public const string DownloadSoWatermark   = "download.so.watermark";
    public const string DownloadSoBrowseTitle = "download.so.browse.title";
    public const string DownloadSoAutoFound   = "download.so.auto.found";
    public const string DownloadSoMissingHint = "download.so.missing.hint";
    public const string DownloadSoRenamedHint = "download.so.renamed.hint";
    public const string DeviceNoOutput        = "device.no.output";
    public const string DeviceDownloadFailed  = "device.download.failed";
    public const string DeviceDeployDone      = "device.deploy.done";
    public const string DeviceDeployRestartFailed = "device.deploy.restart.failed";
    public const string DeviceSoInvalid       = "device.so.invalid";
    public const string DeviceSourceOnlyDone  = "device.source.only.done";
    public const string DeviceSoBuildHint     = "device.so.build.hint";

    // ── 设备管理（真实硬件联通）──
    public const string DeviceTitle           = "device.title";
    public const string DeviceChannel         = "device.channel";
    public const string DeviceSerial          = "device.channel.serial";
    public const string DeviceEthernet        = "device.channel.ethernet";
    public const string DevicePort            = "device.port";
    public const string DeviceBaudRate        = "device.baudrate";
    public const string DeviceHost            = "device.host";
    public const string DeviceTcpPort         = "device.tcp.port";
    public const string DeviceOpen            = "device.open";
    public const string DeviceClose           = "device.close";
    public const string DeviceDetect          = "device.detect";
    public const string DeviceDetecting       = "device.detecting";
    public const string DeviceRefreshPorts    = "device.refresh.ports";
    public const string DeviceInfo            = "device.info";
    public const string DeviceModel           = "device.model";
    public const string DeviceArch            = "device.arch";
    public const string DeviceKernel          = "device.kernel";
    public const string DeviceFw              = "device.fw";
    public const string DeviceHmiVersion      = "device.hmi.version";
    public const string DeviceConnectFirst    = "device.connect.first";
    public const string DeviceCheckUpdate     = "device.check.update";
    public const string DeviceChecking        = "device.checking";
    public const string DeviceHmiUpdate       = "device.hmi.update";
    public const string DeviceSwuPath         = "device.swu.path";
    public const string DeviceBrowse          = "device.browse";
    public const string DeviceLocalVersion    = "device.local.version";
    public const string DeviceDeviceVersion   = "device.device.version";
    public const string DeviceUpdateNow       = "device.update.now";
    public const string DeviceUpToDate        = "device.up.to.date";
    public const string DeviceUpdateAvailable = "device.update.available";
    public const string DeviceDeviceNewer     = "device.device.newer";
    public const string DeviceUnknownVer      = "device.unknown.version";
    public const string DeviceUpdating        = "device.updating";
    public const string DeviceFirmwareFlash   = "device.firmware.flash";
    public const string DeviceFwSwuPath       = "device.fw.swu.path";
    public const string DeviceFlashNow        = "device.flash.now";
    public const string DeviceFwUpdating      = "device.fw.updating";
    public const string DeviceUsbPackage      = "device.usb.package";
    public const string DeviceHmiLibPath      = "device.hmi.lib.path";
    public const string DeviceOutputDir       = "device.output.dir";
    public const string DeviceGenerate        = "device.generate";
    public const string DeviceGenerating      = "device.generating";
    public const string DeviceGenerated       = "device.generated";
    public const string DeviceFelFlash        = "device.fel.flash";
    public const string DeviceFelTool         = "device.fel.tool";
    public const string DeviceFelImage        = "device.fel.image";
    public const string DeviceFelArgs         = "device.fel.args";
    public const string DeviceFelHint         = "device.fel.hint";
    public const string DeviceFelRunning      = "device.fel.running";
    public const string DeviceFelDone         = "device.fel.done";
    public const string DeviceLog             = "device.log";
    public const string DeviceProgress        = "device.progress";
    public const string DeviceErr             = "device.err";
    public const string DeviceOperBusy        = "device.oper.busy";
    public const string DeviceSelectDir       = "device.select.dir";

    // ── 日志面板段落标签（设备管理 / 下载到 HMI 的可见日志）──
    // 这些标签以前是写死在 $"[连接] {本地化正文}" 里的：正文入了字典，标签没有，
    // 于是英文界面里同一条日志中英混排。段落标签同为用户可见文案，一并入字典。
    public const string DeviceLogTagConnect  = "device.log.tag.connect";
    public const string DeviceLogTagHint     = "device.log.tag.hint";
    public const string DeviceLogTagDetect   = "device.log.tag.detect";
    public const string DeviceLogTagUpdate   = "device.log.tag.update";
    public const string DeviceLogTagFirmware = "device.log.tag.firmware";
    public const string DeviceLogTagUsb      = "device.log.tag.usb";
    public const string DeviceLogTagDownload = "device.log.tag.download";

    // ── 编辑器命令文案（撤销/重做菜单项 + "未选中控件"提示）──
    public const string CmdDescGroup         = "cmd.desc.group";
    public const string CmdDescUngroup       = "cmd.desc.ungroup";
    public const string CmdDescAlignLeft     = "cmd.desc.align.left";
    public const string CmdDescAlignCenterH  = "cmd.desc.align.center.h";
    public const string CmdDescAlignRight    = "cmd.desc.align.right";
    public const string CmdDescAlignTop      = "cmd.desc.align.top";
    public const string CmdDescAlignCenterV  = "cmd.desc.align.center.v";
    public const string CmdDescAlignBottom   = "cmd.desc.align.bottom";
    public const string CmdDescSameSize      = "cmd.desc.same.size";
    public const string CmdDescDistributeH   = "cmd.desc.distribute.h";
    public const string CmdDescDistributeV   = "cmd.desc.distribute.v";

    // ── UiOperationGuard 的操作名与失败提示 ──
    // 操作名会出现在**用户可见**的失败提示里（视图状态行 / 全局状态栏），
    // 所以它必须是键：传文案就等于在调用点写死中文，英文界面下会中英混排。
    public const string UiOpDeleteUser       = "ui.op.delete.user";
    public const string UiOpRenameScreen     = "ui.op.rename.screen";
    public const string UiOpDeleteScreen     = "ui.op.delete.screen";
    public const string UiOpRenameControl    = "ui.op.rename.control";
    public const string UiOpDeleteTemplate   = "ui.op.delete.template";
    public const string UiOpAiScript         = "ui.op.ai.script";
    public const string UiOpPickFile         = "ui.op.pick.file";
    public const string UiOpPickFolder       = "ui.op.pick.folder";
    public const string UiOpGenIcon          = "ui.op.gen.icon";
    public const string UiOpPickHmi          = "ui.op.pick.hmi";
    public const string UiOpEditGridCell     = "ui.op.edit.grid.cell";
    public const string UiOpRetryAgent       = "ui.op.retry.agent";
    public const string UiOpFailed           = "ui.op.failed";
    public const string UiOpFailedDetail     = "ui.op.failed.detail";
    public const string UiOpFailedDetailFile = "ui.op.failed.detail.file";
    public const string UiOpReportFailed     = "ui.op.report.failed";

    // ── 设备日志正文（日志面板 + 状态行）──
    public const string DeviceLogTagDeploy         = "device.log.tag.deploy";
    public const string DeviceLogTagFel            = "device.log.tag.fel";
    public const string DeviceLogReady             = "device.log.ready";
    public const string DeviceLogPortsRefreshed    = "device.log.ports.refreshed";
    public const string DeviceLogClosed            = "device.log.closed";
    public const string DeviceLogSelectPort        = "device.log.select.port";
    public const string DeviceLogEnterHost         = "device.log.enter.host";
    public const string DeviceLogConnected         = "device.log.connected";
    public const string DeviceLogDetectDone        = "device.log.detect.done";
    public const string DeviceLogUpdateEnterLocal  = "device.log.update.enter.local";
    public const string DeviceLogUpdateChecking    = "device.log.update.checking";
    public const string DeviceLogUpdateSelectSwu   = "device.log.update.select.swu";
    public const string DeviceLogUpdateTriggered   = "device.log.update.triggered";
    public const string DeviceLogUpdateFailed      = "device.log.update.failed";
    public const string DeviceLogFwSelectSwu       = "device.log.fw.select.swu";
    public const string DeviceLogFwTriggered       = "device.log.fw.triggered";
    public const string DeviceLogUsbSelectSo       = "device.log.usb.select.so";
    public const string DeviceLogUsbSelectOutDir   = "device.log.usb.select.out.dir";
    public const string DeviceLogFelSelectImage    = "device.log.fel.select.image";
    public const string DeviceLogFelFlashFailed    = "device.log.fel.flash.failed";
    public const string DeviceLogDeploySelectSo    = "device.log.deploy.select.so";
    public const string DeviceLogDeployNoSo        = "device.log.deploy.no.so";
    public const string DeviceLogDownloadSummary   = "device.log.download.summary";
    public const string DevicePendingSummary       = "device.pending.summary";
    public const string DevicePendingAssets        = "device.pending.assets";
    public const string DevicePendingNoSo          = "device.pending.no.so";
    public const string DeviceSoDeployed           = "device.so.deployed";
    public const string DeviceSoNotDeployed        = "device.so.not.deployed";

    // ── 变量管理器：报警类型/级别与从站显示名（下拉项与列表标签，纯显示）──
    public const string VarAlarmTypeHigh             = "var.alarm.type.high";
    public const string VarAlarmTypeLow              = "var.alarm.type.low";
    public const string VarAlarmTypeHighHigh         = "var.alarm.type.highhigh";
    public const string VarAlarmTypeLowLow           = "var.alarm.type.lowlow";
    public const string VarAlarmTypeBit              = "var.alarm.type.bit";
    public const string VarAlarmTypeDeviation        = "var.alarm.type.deviation";
    public const string VarAlarmTypeStateChange      = "var.alarm.type.statechange";
    public const string VarAlarmTypeRateOfChange     = "var.alarm.type.rateofchange";
    public const string VarAlarmSeverityInfo         = "var.alarm.severity.info";
    public const string VarAlarmSeverityWarning      = "var.alarm.severity.warning";
    public const string VarAlarmSeverityCritical     = "var.alarm.severity.critical";
    public const string VarAlarmSeverityEmergency    = "var.alarm.severity.emergency";
    public const string VarAlarmShortHigh            = "var.alarm.short.high";
    public const string VarAlarmShortLow             = "var.alarm.short.low";
    public const string VarAlarmShortHighHigh        = "var.alarm.short.highhigh";
    public const string VarAlarmShortLowLow          = "var.alarm.short.lowlow";
    public const string VarAlarmShortBit             = "var.alarm.short.bit";
    public const string VarAlarmShortDeviation       = "var.alarm.short.deviation";
    public const string VarAlarmShortStateChange     = "var.alarm.short.statechange";
    public const string VarAlarmShortRateOfChange    = "var.alarm.short.rateofchange";
    public const string VarAlarmShortUnknown         = "var.alarm.short.unknown";
    public const string VarSlaveStation              = "var.slave.station";
    public const string VarAlarmDisabledSuffix       = "var.alarm.disabled.suffix";

    // ── 主窗口：操作名、状态提示、撤销描述 ──
    public const string UiOpDockDiagnostics      = "ui.op.dock.diagnostics";
    public const string UiOpOpenProject          = "ui.op.open.project";
    public const string UiOpNewProject           = "ui.op.new.project";
    public const string UiOpSaveProject          = "ui.op.save.project";
    public const string UiOpExportCode           = "ui.op.export.code";
    public const string UiOpCompileProject       = "ui.op.compile.project";
    public const string UiOpDownloadHmi          = "ui.op.download.hmi";
    public const string UiOpExportSimulator      = "ui.op.export.simulator";
    public const string UiOpExportRecipe         = "ui.op.export.recipe";
    public const string UiOpExportSlaveVars      = "ui.op.export.slave.vars";
    public const string UiOpImportSvg            = "ui.op.import.svg";
    public const string UiOpImportFont           = "ui.op.import.font";
    public const string UiOpImportImage          = "ui.op.import.image";
    public const string UiOpImportVariables = "ui.op.import.variables";
    public const string UiOpImportRecipe    = "ui.op.import.recipe";
    public const string UiOpImportSlaveVars = "ui.op.import.slave.vars";
    public const string UiOpExportCharset   = "ui.op.export.charset";
    public const string UiOpApplySvg             = "ui.op.apply.svg";
    public const string UiOpPrepareSharedLib     = "ui.op.prepare.shared.lib";
    public const string StatusFileNotFound       = "status.file.not.found";
    public const string StatusThemeSwitched      = "status.theme.switched";
    public const string UndoDeleteControl        = "undo.delete.control";
    public const string UndoDeleteControls       = "undo.delete.controls";
    public const string UndoRotate               = "undo.rotate";
    public const string UndoMirrorH              = "undo.mirror.h";
    public const string UndoMirrorV              = "undo.mirror.v";
    public const string UndoResetTransform       = "undo.reset.transform";
    public const string UndoModifyControl        = "undo.modify.control";
    public const string UndoModifyScreen         = "undo.modify.screen";

    // ── 第八批：撤销描述补充 / 通信从站 / 画布占位 / 主题说明 / AI 图标文件类型 ──
    public const string UndoDeleteControlNamed     = "undo.delete.control.named";
    public const string UndoResizeControl          = "undo.resize.control";
    public const string UndoMoveControl            = "undo.move.control";
    public const string CommSlaveStation           = "comm.slave.station";
    public const string DefaultImagePlaceholder    = "default.image.placeholder";
    public const string ThemeDescDark              = "theme.desc.dark";
    public const string ThemeDescLight             = "theme.desc.light";
    public const string ThemeDescOcean             = "theme.desc.ocean";
    public const string AiIconFilterSvg            = "ai.icon.filter.svg";
    public const string AiIconFilterPng            = "ai.icon.filter.png";

    // ── 第九批：模拟器构建日志 / 构建运行对话框 / 画布多选操作 / AI 面板状态 ──
    public const string SimBuildStep1              = "sim.build.step1";
    public const string SimBuildFiles              = "sim.build.files";
    public const string SimBuildStep2              = "sim.build.step2";
    public const string SimBuildStep3              = "sim.build.step3";
    public const string SimBuildOk                 = "sim.build.ok";
    public const string SimBuildFailed             = "sim.build.failed";
    public const string SimBuildFailedDetail       = "sim.build.failed.detail";
    public const string BuildrunLogTagSim          = "buildrun.log.tag.sim";
    public const string BuildrunLogNoAutostart     = "buildrun.log.no.autostart";
    public const string BuildrunLogStaleWindow     = "buildrun.log.stale.window";
    public const string BuildrunLogCancelled       = "buildrun.log.cancelled";
    public const string BuildrunLogError           = "buildrun.log.error";
    public const string BuildrunLogStarted         = "buildrun.log.started";
    public const string BuildrunLogExited          = "buildrun.log.exited";
    public const string UndoAlignControls          = "undo.align.controls";
    public const string UndoPasteControl           = "undo.paste.control";
    public const string UndoPasteControls          = "undo.paste.controls";
    public const string UndoDuplicateControl       = "undo.duplicate.control";
    public const string UndoDuplicateControls      = "undo.duplicate.controls";
    public const string AiChatSteps                = "ai.chat.steps";
    public const string AiChatTaskDone             = "ai.chat.task.done";
    public const string AiChatTaskFailed           = "ai.chat.task.failed";

    // ── 新建实体的默认名（跟随 UI 语言；见 docs/2026-09-17-persisted-content-language-decision.md）──
    // 与 ScriptNewDefault / TrendDefaultName / AnimDefaultName / comm.conn.new 等既有键同一约定：
    // 在**创建那一刻**取文案写进模型，之后不再因切语言而改写。
    public const string VarAlarmDefaultName        = "var.alarm.default.name";
    public const string VarAlarmUnnamed            = "var.alarm.unnamed";
    public const string ResourceSubCategoryAi      = "resource.subcategory.ai";
    public const string ResourceDescAi             = "resource.desc.ai";
    public const string AiImageAssetName           = "ai.image.asset.name";
    public const string TplStartStopNameStart      = "tpl.startstop.name.start";
    public const string TplStartStopNameStop       = "tpl.startstop.name.stop";
    public const string TplMotorIndNameLed         = "tpl.motorind.name.led";
    public const string TplMotorIndNameState       = "tpl.motorind.name.state";

    // ── AI 面板：聊天消息正文（此前被'兄弟属性连坐豁免'藏在对象初始化器里）──
    public const string AiChatImageFailed          = "ai.chat.image.failed";
    public const string AiChatImageGenerated       = "ai.chat.image.generated";
    public const string AiGenFailedPngRender       = "ai.gen.failed.png.render";

    // ── 硬件目标校验文案（插件侧只给 Code，文案由 App 侧提供）──
    public const string HwIssueLvglVersionMismatch = "hw.issue.lvgl_version_mismatch";

    // ── 控件动作的展示文案（阶段 94，闭环登记 #61m）──
    // 动作类型的短名（下拉框 + 动作 chip）与一句描述（交叉引用面板）各一族。
    // ⚠ action.type.* 的 zh 值必须与 Core 的 ActionTypeMeta.DisplayName 逐字相等
    //   （判据 ActionTextLocalizationGuardTests 用相等式钉住 —— 两份必然漂移）。
    public const string ActionTypeNone            = "action.type.none";
    public const string ActionTypeToggleBit       = "action.type.toggle_bit";
    public const string ActionTypeSetBit          = "action.type.set_bit";
    public const string ActionTypeResetBit        = "action.type.reset_bit";
    public const string ActionTypeMomentaryOn     = "action.type.momentary_on";
    public const string ActionTypeMomentaryOff    = "action.type.momentary_off";
    public const string ActionTypeSetVariable     = "action.type.set_variable";
    public const string ActionTypeSwitchScreen    = "action.type.switch_screen";
    public const string ActionTypeExecuteScript   = "action.type.execute_script";
    public const string ActionTypeShowAlarms      = "action.type.show_alarms";
    public const string ActionTypeShowTrends      = "action.type.show_trends";
    public const string ActionTypeLogin           = "action.type.login";
    public const string ActionTypeLogout          = "action.type.logout";
    public const string ActionDescNone            = "action.desc.none";
    public const string ActionDescToggleBit       = "action.desc.toggle_bit";
    public const string ActionDescSetBit          = "action.desc.set_bit";
    public const string ActionDescResetBit        = "action.desc.reset_bit";
    public const string ActionDescMomentaryOn     = "action.desc.momentary_on";
    public const string ActionDescMomentaryOff    = "action.desc.momentary_off";
    public const string ActionDescSetVariable     = "action.desc.set_variable";
    public const string ActionDescSwitchScreen    = "action.desc.switch_screen";
    public const string ActionDescExecuteScript   = "action.desc.execute_script";
    public const string ActionDescShowAlarms      = "action.desc.show_alarms";
    public const string ActionDescShowTrends      = "action.desc.show_trends";
    public const string ActionDescLogin           = "action.desc.login";
    public const string ActionDescLogout          = "action.desc.logout";
    public const string ActionRefAction           = "action.ref.action";
}
