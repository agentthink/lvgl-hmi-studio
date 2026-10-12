using System.Diagnostics;
using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>
/// 应用路径的**唯一来源**：数据根（持久）与临时根（可清理）。
///
/// <para><b>要解决的问题：</b>此前 16 处各写各的 <c>Path.Combine(...)</c>，其中 10 处是
/// <c>Path.Combine(Path.GetTempPath(), "固定名")</c>，带来三个后果：</para>
/// <list type="number">
///   <item>固定名落在**共享**临时目录 ⇒ 两个 IDE 实例互相覆盖（部署中转文件、
///   编译工作目录都属于这一类）；</item>
///   <item>本机 <c>/tmp</c> 只有 **10 MB tmpfs**，被占满时写入直接抛
///   <c>IOException: No space left on device</c> —— 实测导致
///   <c>DeployAsync_RuntimeConfigText_UploadsCommConfig</c> 间歇红（填满 <c>/tmp</c> 必红）；</item>
///   <item>**用户产物**（AI 生成的图标/图像）也写在那里 ⇒ 系统清理临时目录时资产直接消失。
///   原注释写着"使用系统临时目录兜底，保证生成结果不丢失"，恰好把结果放在了**会丢**的地方。</item>
/// </list>
///
/// <para><b>两个根，两种语义（不要混用）：</b></para>
/// <list type="bullet">
///   <item><see cref="DataRoot"/> —— **持久**。用户配置与生成的产物。默认
///   <c>%APPDATA%/zhishu-hmi</c>（与 <c>FileLogger</c>/<c>LayoutSerializer</c>/
///   <c>WindowGeometryService</c> 的既有约定一致 —— 它们现在也统一走这里）。</item>
///   <item><see cref="TempRoot"/> —— **可清理**。编译/模拟/交叉编译的中间产物。默认
///   <c>DataRoot/temp</c>。</item>
/// </list>
///
/// <para><b>为什么中间产物不放 <c>Path.GetTempPath()</c>：</b>它们要的是**空间 + 可预测**，
/// 不是"系统会替我清理"。测试侧对同一根因的收口见
/// <c>tests/HmiEditor.TestSupport/TestTempDir.cs</c>；本类是它的产品侧对应物。
/// <see cref="TempRoot"/> 整个目录**可以随时手工删除**，不影响任何持久状态。</para>
///
/// <para><b>命名约定（为什么 <see cref="TempRoot"/> 里允许固定名）：</b>它是**应用私有**根，
/// 跨应用不会撞名；但**同一应用的两个实例**仍会撞，所以：
/// 中转文件一律用 <see cref="NewTempFilePath"/>（唯一名 + 调用方用完即删）；
/// 需要跨次复用的工作目录才用 <see cref="TempSubdir"/>（稳定名，便于增量复用）。</para>
///
/// <para><b>生命周期：临时根分两层，因为清理策略只能有一个。</b>
/// 混在一层的后果是无论怎么定清理规则都是错的 —— 清掉就破坏增量复用，不清就涨到占满磁盘。</para>
/// <list type="bullet">
///   <item><b>跨会话复用</b>（<see cref="TempSubdir"/>/<see cref="TempPath"/> 的产物：
///   <c>so-build</c> / <c>compile</c> / <c>download</c> / <c>sim/&lt;name&gt;</c>）——
///   落在 <see cref="TempRoot"/> 直下，**稳定名**，下次运行还要用（增量编译复用），
///   **不自动清理**。数量有界（每个工程/每类工作各一个）。</item>
///   <item><b>会话级</b>（<see cref="NewTempSubdir"/>/<see cref="NewTempFilePath"/> 的产物：
///   工程解包目录、资产工作目录、部署中转文件）—— 落在
///   <see cref="SessionTempRoot"/>（<c>TempRoot/.sessions/&lt;pid&gt;-&lt;token&gt;</c>）下，
///   **有主**：进程退出时由 <see cref="ReleaseSessionTemp"/> 整体释放；崩溃/强杀留下的孤儿
///   由下次启动的 <see cref="PruneStaleSessions"/> 扫掉。</item>
/// </list>
///
/// <para>另有一类**历史残留**：第十轮～第十一轮之间，唯一名产物直接平铺在 <see cref="TempRoot"/>
/// 直下。那些目录不属于上面任何一层，由 <see cref="PruneLegacyFlatWorkDirs"/> 按**名字形态**
/// 清掉 —— 判据是「现任代码已不可能产生它」，因此**幂等**，不需要迁移标记。</para>
///
/// <para>为什么要给唯一名目录加"主"：唯一名只解决了"**不互相踩**"，没有解决"**谁负责删**"——
/// 唯一名等于无主名。旧实现（<c>hmi-editor/&lt;name&gt;-&lt;guid&gt;</c>）每次打开工程建两个、
/// 从不删除，只因落在系统临时目录（重启会清）才没被发现；第十轮把它搬到持久根后，
/// 这个兜底就没了 —— 于是必须显式建模生命周期。</para>
///
/// <para><b>清理判据一律偏保守（宁漏删、不误删）：</b>目录名解析不出 pid 的一律不动；
/// 存活与否判不出来时**当作存活**。理由是两种错误的代价不对称 ——
/// 漏删只是浪费空间，误删会让另一个正在运行的实例（或用户数据）当场失败。</para>
///
/// <para><b>覆盖与测试：</b><c>HMI_APP_DATA_ROOT</c> 可把数据根整体指到别处（CI / 多实例隔离 /
/// 测试）。<b>有意不做缓存</b> —— 若用 <c>Lazy</c> 缓存，"先访问后改环境变量"的测试会静默拿到旧值；
/// 不缓存时 <c>Environment.GetFolderPath</c> 本身开销可忽略（运行时内部已缓存）。
/// 唯一的例外是**会话 id**（见 <see cref="SessionTempRoot"/>）：它描述的是"本进程是谁"，
/// 在进程生命周期内本就不该变，与"外部配置可变"是两回事。</para>
/// </summary>
public static class AppPaths
{
    /// <summary>应用数据目录名（`%APPDATA%` 下的子目录）。</summary>
    public const string AppFolderName = "zhishu-hmi";

    /// <summary>
    /// 历史数据目录名（早期版本用的，与 <see cref="AppFolderName"/> 不同）。
    /// 只用于**读**侧兼容，见 <see cref="LegacyDataRoot"/>。
    /// </summary>
    public const string LegacyAppFolderName = "HmiEditor";

    /// <summary>数据根覆盖环境变量。设置后 <see cref="DataRoot"/> 完全以它为准。</summary>
    public const string DataRootEnvVar = "HMI_APP_DATA_ROOT";

    /// <summary>历史数据根覆盖环境变量（测试 / CI 用，正常安装不设）。</summary>
    public const string LegacyDataRootEnvVar = "HMI_APP_LEGACY_DATA_ROOT";

    private const string TempFolderName = "temp";
    private const string AssetsFolderName = "assets";

    /// <summary>持久数据根（默认 <c>%APPDATA%/zhishu-hmi</c>）。**不创建目录**。</summary>
    public static string DataRoot
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(DataRootEnvVar);
            if (!string.IsNullOrWhiteSpace(overridden))
                return Path.GetFullPath(overridden);

            return Path.Combine(ResolveUserDataBaseDir(), AppFolderName);
        }
    }

    /// <summary>
    /// 历史数据根（默认 <c>%APPDATA%/HmiEditor</c>）—— <b>只读</b>。
    ///
    /// <para><b>它为什么还存在：</b>早期版本把 LLM 配置与审计日志写在这个目录名（`HmiEditor`）下。
    /// 改名成 `zhishu-hmi` 时若一刀切，用户**已配置的 LLM 端点/密钥会失效**、审计历史会读不到。
    /// 所以保留这个位置，但只允许**读**：写入一律走 <see cref="DataRoot"/>，旧文件自然冻结。</para>
    ///
    /// <para>收进本类而不是让各调用方自己拼，是为了让"应用路径只有一个来源"这条不变量
    /// **没有例外** —— 守卫因此可以要求 <c>SpecialFolder.ApplicationData</c> 在 <c>src/</c> 全域
    /// 只出现一次。历史位置若散在调用方，例外就会随调用方增长。</para>
    ///
    /// <para>**不创建目录**：它可能根本不存在（新装用户），创建它反而会凭空造出一个空目录。</para>
    /// </summary>
    public static string LegacyDataRoot
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable(LegacyDataRootEnvVar);
            if (!string.IsNullOrWhiteSpace(overridden))
                return Path.GetFullPath(overridden);

            return Path.Combine(ResolveUserDataBaseDir(), LegacyAppFolderName);
        }
    }

    /// <summary>数据根下的文件（**写侧**唯一入口）。不创建任何东西。</summary>
    public static string DataFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(DataRoot, name);
    }

    /// <summary>
    /// 同一文件在**历史数据根**下的只读路径。
    /// 与 <see cref="DataFile"/> 由**同一个文件名字面量**推导 —— 否则新旧两处各写一份，
    /// 改名时必然只改一处。
    /// </summary>
    public static string LegacyDataFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(LegacyDataRoot, name);
    }

    /// <summary>
    /// 用户级数据目录的基目录。两个数据根共用同一条兜底链，避免两处各写一份而漂移。
    ///
    /// <para>兜底链**有意不含** <c>Path.GetTempPath()</c>：那是本类存在的理由（见类型注释）。
    /// 守卫据此可以立一条最干净的不变量 —— <c>src/</c> 全域零出现它。取不到就显式报错，
    /// 而不是退回"当前目录"（会把文件写进工作树）。</para>
    /// </summary>
    private static string ResolveUserDataBaseDir()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(baseDir))
            throw new InvalidOperationException(
                "无法解析应用数据根（ApplicationData / LocalApplicationData / UserProfile 全为空）。" +
                $"可用环境变量 {DataRootEnvVar} 显式指定一个有空间的目录。");

        return baseDir;
    }

    /// <summary>可清理的中间产物根（默认 <c>DataRoot/temp</c>）。**不创建目录**。</summary>
    public static string TempRoot => Path.Combine(DataRoot, TempFolderName);

    // ── 会话层 ────────────────────────────────────────────────────────────
    // 唯一名临时产物统一收在 TempRoot/.sessions/<本进程会话 id>/ 下，
    // 于是"释放"退化成删一个目录 —— 不需要逐个跟踪谁创建了什么。

    /// <summary>会话目录的父目录名（以 <c>.</c> 开头，与稳定名工作目录区分开）。</summary>
    private const string SessionsFolderName = ".sessions";

    /// <summary>本进程的会话 id：<c>&lt;pid&gt;-&lt;token&gt;</c>。token 防 pid 复用认错前任。</summary>
    private static string? _sessionId;

    private static string SessionId => _sessionId ??= $"{Environment.ProcessId}-{Guid.NewGuid():N}";

    /// <summary>
    /// 本进程的会话临时目录。**不创建目录**。
    ///
    /// <para>缓存的是**会话 id**而不是完整路径：<see cref="DataRoot"/> 可被环境变量中途改
    /// （测试/CI 要立刻生效），若把路径一起缓存，会话目录会留在旧的临时根下 ——
    /// 半新半旧比全旧更难查。</para>
    /// </summary>
    public static string SessionTempRoot => Path.Combine(TempRoot, SessionsFolderName, SessionId);

    /// <summary>持久产物目录 <c>DataRoot/assets/&lt;kind&gt;</c>，并确保其存在。</summary>
    public static string AssetDir(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var dir = Path.Combine(DataRoot, AssetsFolderName, kind);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 只**拼**临时路径，不创建任何目录。用于"读侧"（探测候选文件是否存在等），
    /// 保证与写侧由**同一句话**推导出同一个位置 —— 写读分叉是静默失效的经典来源。
    /// </summary>
    public static string TempPath(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var all = new string[parts.Length + 1];
        all[0] = TempRoot;
        Array.Copy(parts, 0, all, 1, parts.Length);
        return Path.Combine(all);
    }

    /// <summary><see cref="TempPath"/> 的"写侧"版本：额外确保目录存在。</summary>
    public static string TempSubdir(params string[] parts)
    {
        var dir = TempPath(parts);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 唯一会话级子目录 <c>SessionTempRoot/&lt;prefix&gt;-&lt;32 位十六进制&gt;</c>，并确保存在。
    /// 用于**本次运行专用**的工作目录（工程解包、资产工作目录）—— 并发调用（多实例、并行测试）
    /// 不会互相踩，且**整个会话目录**会在进程退出时一并释放。
    /// </summary>
    public static string NewTempSubdir(string prefix)
    {
        var dir = NewTempSubdirPath(prefix);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 唯一会话级临时文件路径 <c>SessionTempRoot/&lt;prefix&gt;-&lt;32 位十六进制&gt;&lt;ext&gt;</c>，
    /// **不创建文件**。用于往设备/外部下发的**中转文件**：调用方负责用完即删（<c>try/finally</c>）；
    /// 即使调用方漏了，会话目录整体释放/清理时也会兜住。固定名会让两个实例互相覆盖对方正在传输的内容。
    /// </summary>
    public static string NewTempFilePath(string prefix, string extension = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Directory.CreateDirectory(SessionTempRoot);
        return Path.Combine(SessionTempRoot, $"{prefix}-{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// 唯一会话级子目录的**路径**（<c>SessionTempRoot/&lt;prefix&gt;-&lt;32 位小写十六进制&gt;</c>），
    /// **不创建目录**。唯一名只拼一次，<see cref="NewTempSubdir"/> 加一句 <c>CreateDirectory</c> 即可。
    ///
    /// <para><b>为什么不创建也要有这个入口</b>：有些**目标目录**必须在"包还没校验"时就先定下来
    /// （<c>HmiPackageService.Unpack</c> 需要一个落点参数），但**不能**在那一刻就建出来 ——
    /// 包是坏的时它一个文件都收不到、也没人再引用它，而会话目录的兜底释放点在**进程退出**
    /// ⇒ 一次会话里反复打开坏包会攒一串空目录（阶段 80 实测：5 次失败 = 5 个空目录）。
    /// 改成"先只定路径、由消费者在**第一次真正写盘之前**自建"之后，失败路径**按构造**不留痕迹：
    /// <c>Unpack</c> 在全部前置校验（非 ZIP / manifest / 版本 / project.json）通过之后才
    /// <c>Directory.CreateDirectory(assetDir)</c>，那之前抛出就什么都没建。</para>
    ///
    /// <para><b>开这个口子的边界</b>：返回的路径**不保证存在** —— 调用方要么自己建，
    /// 要么保证下游会建。<c>_assetDirectory</c> 那种"要一个现成可写目录"的用途必须走
    /// <see cref="NewTempSubdir"/>。</para>
    /// </summary>
    public static string NewTempSubdirPath(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return Path.Combine(SessionTempRoot, $"{prefix}-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// 释放**本进程**的会话临时目录（进程退出时调用）。返回是否实际删除了目录。
    ///
    /// <para>释放后会话 id 会重置，下一次 <see cref="NewTempSubdir"/>/<see cref="NewTempFilePath"/>
    /// 会开一个新会话 —— 否则会把文件写回已被删掉的目录（看似成功，实际无主）。</para>
    ///
    /// <para>尽力而为：文件被占用时删除失败不抛异常，遗留目录由下次启动的
    /// <see cref="PruneStaleSessions"/> 兜底。</para>
    /// </summary>
    public static bool ReleaseSessionTemp()
    {
        var dir = SessionTempRoot;
        _sessionId = null;
        return TryDeleteDirectory(dir);
    }

    /// <summary>
    /// 清理**不属于存活进程**的会话目录（崩溃 / 强杀留下的孤儿）。返回删除的目录数。
    /// 在应用启动时调用一次；只扫 <c>TempRoot/.sessions</c>，
    /// **稳定名工作目录（so-build / compile / download / sim）不在扫描面内**，增量复用不受影响。
    /// </summary>
    /// <param name="isProcessAlive">
    /// 存活判定探针；<c>null</c> 表示用进程表实测。留成参数而不是静态可变的钩子：
    /// 测试与受限宿主需要确定性/可替身，而参数是显式的、不会在别处被悄悄改掉。
    /// </param>
    public static int PruneStaleSessions(Func<int, bool>? isProcessAlive = null)
    {
        var probe = isProcessAlive ?? IsProcessAlive;
        var sessionsRoot = Path.Combine(TempRoot, SessionsFolderName);
        if (!Directory.Exists(sessionsRoot))
            return 0;

        var mine = SessionId;
        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(sessionsRoot).ToList())
        {
            // 本次运行自己的会话：留着，正是接下来要用的。
            if (string.Equals(Path.GetFileName(dir), mine, StringComparison.Ordinal))
                continue;

            // 名字里解析不出 pid ⇒ 不是本机制创建的形态 ⇒ **不动**。
            // （保守方向：漏删只是浪费空间，误删是丢数据。）
            if (ParseSessionPid(Path.GetFileName(dir)) is not { } pid)
                continue;

            // 同 pid 但 token 不同 = 该 pid 号的**前任**进程（pid 已被本进程复用）⇒ 必死。
            // 这是唯一能确定"同号已死"的情形；其余交给探针。
            var isPredecessor = pid == Environment.ProcessId;
            if (isPredecessor || !probe(pid))
            {
                if (TryDeleteDirectory(dir))
                    removed++;
            }
        }
        return removed;
    }

    /// <summary>
    /// 清理**历史形态**的平铺工作目录：<see cref="TempRoot"/> **直下**、名如
    /// <c>&lt;prefix&gt;-&lt;32 位小写十六进制&gt;</c> 的目录。返回删掉的个数。
    ///
    /// <para><b>这些目录是什么：</b>第十轮～第十一轮之间，<see cref="NewTempSubdir"/> 的产物直接落在
    /// <see cref="TempRoot"/> 直下（<c>so-build-&lt;guid&gt;</c> 之类）。第十一轮把唯一名产物收进
    /// <see cref="SessionTempRoot"/> 之后，**现任代码不再产生这个形态**：稳定名产物
    /// （<c>so-build</c> / <c>compile</c> / <c>download</c> / <c>sim</c>）**不带** guid 后缀，
    /// 唯一名产物一律在 <c>.sessions/</c> 之下。所以「直下 + guid 后缀」只剩历史残留一种解释。</para>
    ///
    /// <para><b>为什么按形态清理而不是「打标记只做一次」：</b>一次性迁移需要记录「做过了」——
    /// 标记写失败就永不重试，标记丢失就重复执行。按形态清理是**幂等**的：没残留时只是一次目录枚举，
    /// 有残留时自动删，删到一半崩了下次继续。代价是判据必须严格到「现任代码绝不产生它」。</para>
    ///
    /// <para><b>保守方向与 <see cref="PruneStaleSessions"/> 一致 —— 宁漏删、不误删：</b>
    /// 名字解析不出 guid 形态的不动；<b>最后写入不早于 <paramref name="cutoffUtc"/> 的不动</b>
    /// （另一个还在运行的实例可能刚建了它，正在用）；删不掉不抛异常、留给下次。</para>
    /// </summary>
    /// <param name="cutoffUtc">
    /// 只删最后写入**早于**该时刻的目录。调用方传本进程的启动时刻：它表达的是
    /// 「这些既不是本进程建的，也不像有活主」。
    /// </param>
    public static int PruneLegacyFlatWorkDirs(DateTime cutoffUtc)
    {
        if (!Directory.Exists(TempRoot))
            return 0;

        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(TempRoot).ToList())
        {
            var name = Path.GetFileName(dir);

            // 现行会话层不是历史形态 —— 它有自己的清理（PruneStaleSessions）。
            if (string.Equals(name, SessionsFolderName, StringComparison.Ordinal))
                continue;

            if (!IsLegacyFlatWorkDirName(name))
                continue;

            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) >= cutoffUtc)
                    continue;

                Directory.Delete(dir, recursive: true);
                removed++;
            }
            catch (IOException)
            {
                // 被占用 / 文件系统拒绝：留给下次启动
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return removed;
    }

    /// <summary>
    /// 判据：<c>&lt;非空前缀&gt;-&lt;32 位小写十六进制&gt;</c>，即
    /// <c>Guid.NewGuid().ToString("N")</c> 的形态。
    ///
    /// <para><b>大小写敏感是有意的</b>：<c>ToString("N")</c> 只产小写，放过大写会让判据比实际来源宽。
    /// 同理只认 <c>a-f</c> 以外的字符即拒 —— 判据越贴近"只有旧实现能造出来"，误删面越小。</para>
    /// </summary>
    private static bool IsLegacyFlatWorkDirName(string name)
    {
        var dash = name.LastIndexOf('-');
        if (dash <= 0 || name.Length - dash - 1 != 32)
            return false;

        for (var i = 0; i < dash; i++)
        {
            var c = name[i];
            if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')
                return false;
        }

        for (var i = dash + 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                return false;
        }

        return true;
    }

    /// <summary>解析会话目录名 <c>&lt;pid&gt;-&lt;token&gt;</c> 中的 pid；形态不符返回 null。</summary>
    private static int? ParseSessionPid(string name)
    {
        var dash = name.IndexOf('-');
        if (dash <= 0)
            return null;
        return int.TryParse(name.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
               && pid > 0
            ? pid
            : null;
    }

    /// <summary>进程是否存活。判不出来时**当作存活**（保守：宁可漏删）。</summary>
    private static bool IsProcessAlive(int pid)
    {
        if (pid <= 0)
            return false;
        if (pid == Environment.ProcessId)
            return true;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;          // 进程不存在
        }
        catch (InvalidOperationException)
        {
            return false;          // 进程已退出（句柄残留）
        }
        catch (Exception)
        {
            return true;           // 权限/平台限制 ⇒ 判不出来 ⇒ 当作存活
        }
    }

    /// <summary>递归删除目录；失败返回 false，不抛异常。</summary>
    private static bool TryDeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            return false;
        try
        {
            Directory.Delete(dir, recursive: true);
            return true;
        }
        catch (IOException)
        {
            return false;          // 被占用 / 文件系统拒绝
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
