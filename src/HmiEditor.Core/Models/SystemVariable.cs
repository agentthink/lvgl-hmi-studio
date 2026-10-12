namespace HmiEditor.Core.Models;

/// <summary>系统变量类别。</summary>
public enum SystemVariableCategory
{
    System,        // 系统基本信息
    Security,      // 用户与访问控制
    Communication, // 通讯状态
    HmiStatus,     // HMI 自身状态
}

/// <summary>
/// 系统内部变量 —— 对标 WinCC 的系统变量。
/// 始终存在于每个项目中，运行时由引擎自动填充。
/// 用户可在报警/趋势/脚本中引用。
/// </summary>
public class SystemVariable
{
    /// <summary>唯一标识符（如 "sys.time.cur"）。</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name。</summary>
    public string Name { get; set; } = "";

    /// <summary>数据类型。</summary>
    public string DataType { get; set; } = "String";

    /// <summary>分类。</summary>
    public SystemVariableCategory Category { get; set; }

    /// <summary>唯一地址前缀（如 "$SYS$"）。</summary>
    public string Address { get; set; } = "";

    /// <summary>说明。</summary>
    public string Description { get; set; } = "";

    /// <summary>是否只读（运行时不可写入）。</summary>
    public bool IsReadOnly { get; set; } = true;

    // ── 预定义系统变量 ──

    /// <summary>所有预定义系统变量的列表。</summary>
    public static IReadOnlyList<SystemVariable> Defaults { get; } = new List<SystemVariable>
    {
        // ── 系统时间 ──
        new() { Id = "sys.time.cur", Name = "当前时间", DataType = "String", Address = "$SYS$TIME",
                Category = SystemVariableCategory.System, Description = "格式 HH:mm:ss 的系统时间" },
        new() { Id = "sys.time.date", Name = "当前日期", DataType = "String", Address = "$SYS$DATE",
                Category = SystemVariableCategory.System, Description = "格式 yyyy-MM-dd 的系统日期" },
        new() { Id = "sys.time.datetime", Name = "当前日期时间", DataType = "String", Address = "$SYS$DT",
                Category = SystemVariableCategory.System, Description = "格式 yyyy-MM-dd HH:mm:ss 的完整时间" },
        new() { Id = "sys.time.unix", Name = "Unix 时间戳", DataType = "Int32", Address = "$SYS$TS",
                Category = SystemVariableCategory.System, Description = "Unix 时间戳（秒）" },
        new() { Id = "sys.time.uptime", Name = "运行时长(秒)", DataType = "Int32", Address = "$SYS$UP",
                Category = SystemVariableCategory.System, Description = "系统开机后运行的秒数" },

        // ── 用户与安全 ──
        new() { Id = "sys.user.name", Name = "当前用户名", DataType = "String", Address = "$SYS$USER",
                Category = SystemVariableCategory.Security, Description = "当前登录用户名（未登录为空）" },
        new() { Id = "sys.user.level", Name = "当前用户等级", DataType = "Int16", Address = "$SYS$ULVL",
                Category = SystemVariableCategory.Security, Description = "当前登录用户的访问等级 (0-15)" },
        new() { Id = "sys.user.role", Name = "当前用户角色", DataType = "String", Address = "$SYS$ROLE",
                Category = SystemVariableCategory.Security, Description = "当前登录用户的角色名称" },
        new() { Id = "sys.user.loggedin", Name = "用户已登录", DataType = "Bool", Address = "$SYS$LOGIN",
                Category = SystemVariableCategory.Security, Description = "是否有用户已登录 (true/false)" },

        // ── 通讯状态 ──
        new() { Id = "sys.comm.connected", Name = "PLC 已连接", DataType = "Bool", Address = "$SYS$CONN",
                Category = SystemVariableCategory.Communication, Description = "与 PLC 是否已建立连接" },
        new() { Id = "sys.comm.timeout", Name = "通讯超时次数", DataType = "Int16", Address = "$SYS$TOUT",
                Category = SystemVariableCategory.Communication, Description = "累计通讯超时次数" },
        new() { Id = "sys.comm.lastRead", Name = "最后读取时间", DataType = "String", Address = "$SYS$LREAD",
                Category = SystemVariableCategory.Communication, Description = "最后一次成功从 PLC 读取的时间" },
        new() { Id = "sys.comm.error", Name = "通讯错误码", DataType = "Int16", Address = "$SYS$ERR",
                Category = SystemVariableCategory.Communication, Description = "最近一次通讯错误码（0=正常）" },

        // ── HMI 自身状态 ──
        new() { Id = "sys.hmi.currentScreen", Name = "当前屏幕", DataType = "String", Address = "$SYS$SCR",
                Category = SystemVariableCategory.HmiStatus, Description = "当前显示的屏幕名称" },
        new() { Id = "sys.hmi.screenId", Name = "当前屏幕ID", DataType = "Int16", Address = "$SYS$SCRID",
                Category = SystemVariableCategory.HmiStatus, Description = "当前显示屏幕的索引号" },
        new() { Id = "sys.hmi.actAlarms", Name = "活动报警数", DataType = "Int16", Address = "$SYS$ALM",
                Category = SystemVariableCategory.HmiStatus, Description = "当前未确认的报警数量" },
        new() { Id = "sys.hmi.lang", Name = "当前语言", DataType = "String", Address = "$SYS$LANG",
                Category = SystemVariableCategory.HmiStatus, Description = "当前选中的语言代码" },
    };
}
