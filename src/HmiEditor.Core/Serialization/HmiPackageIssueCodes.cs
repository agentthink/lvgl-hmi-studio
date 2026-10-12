namespace HmiEditor.Core.Serialization;

/// <summary>
/// 工程包（<c>.hmi</c>）**解包失败**的诊断码 —— 单一来源。
///
/// <para><b>它解决什么</b>：<see cref="HmiPackageService.Unpack"/> 的失败以前直接抛
/// <c>InvalidDataException</c>，而 App 层把 <c>ex.Message</c> 原样拼进状态栏
/// （<c>UiOperationGuard.FailureDetail</c>）⇒ 用户看到的是 .NET 的**原始英文消息**，
/// ① 非 ZIP 那一档甚至是 BCL 的 <c>End of Central Directory record could not be found.</c>，
/// ⑤ <c>manifest.json</c> 坏 JSON 那一档会把 <c>System.Text.Json</c> 的内部措辞连
/// <c>LineNumber</c> / <c>BytePositionInLine</c> 一起抖到界面上（阶段 80 探针实测，登记 #59）。</para>
///
/// <para><b>为什么不直接用键而用码</b>：与 <c>HmiEditor.AI.AiIssueCodes</c> /
/// <c>LuaSyntaxValidator</c> 的 <c>LUA_SYNTAX_*</c> 同一套约定 —— Core 只声明**稳定的码**，
/// 键由 App 侧的 <c>ExternalText.PackageIssueKey</c> 统一推导
/// （<c>package.issue.&lt;码小写&gt;</c>）。Core **不引本地化**（依赖方向是 App → Core），
/// 所以它只能给「码 + 实参 + 中文回落原文」三件东西。</para>
///
/// <para><b>为什么必须是 <c>public const string</c></b>：本地化守卫靠**反射枚举**这个类型，
/// 拿"声明了哪些码"与"真正抛得出的码"做**双向差集** —— 声明了抛不出的是死码
/// （它和它的字典键都永远不会被用到），抛出了没声明的是漏登记（键不会进字典 ⇒ 回落中文）。
/// 反射要求 <c>IsLiteral &amp;&amp; FieldType == typeof(string)</c>，所以不能是
/// <c>static readonly</c>。</para>
///
/// <para><b>命名</b>：大写下划线。字典键统一落在 <c>package.issue.</c> 之下。</para>
///
/// <para><b>刻意不码化的路径</b>：① <c>targetAssetDirectory</c> 为空白 ⇒ 仍是
/// <c>ArgumentException</c>（这是**调用方的编程错误**，不是包坏了，界面上不该有对应文案）。
/// 它的用户可见表现是回落 <c>ex.Message</c>，作为**诚实边界**留在文档里。</para>
///
/// <para>② 原来也在这张清单上：<c>Directory.CreateDirectory</c> **之后**的写盘失败
/// 「没有修法也没有判据」（阶段 80 §8.6 / §38.10 第 2 条）。**阶段 105 已收口** ——
/// 补了 <see cref="RESLIB_INVALID"/> / <see cref="ASSET_EXTRACT_FAILED"/> 两条码，
/// 并让 <c>Unpack</c> 在中途失败时**回滚自己建出来的目录**。
/// 补它的依据是探针**实测**（不是"看着像缺口"）：那两条路径确实会留下 <c>&lt;目标&gt;/assets/</c>，
/// 且 <c>JsonException</c> 的 <c>LineNumber</c> / <c>BytePositionInLine</c> 会直通状态栏
/// —— 与登记 <b>#59</b> 同一个病。判据：
/// <c>HmiPackageServiceTests.Unpack_MidFailure_LeavesNoTargetDirectoryBehind</c>。</para>
/// </summary>
public static class HmiPackageIssueCodes
{
    /// <summary>输入流不是 ZIP（BCL 的 <c>ZipArchive</c> 构造抛 <c>InvalidDataException</c>）。0 实参。</summary>
    public const string ZIP_UNREADABLE = "ZIP_UNREADABLE";

    /// <summary>包内没有 <c>manifest.json</c> 条目。0 实参。</summary>
    public const string MANIFEST_MISSING = "MANIFEST_MISSING";

    /// <summary><c>manifest.json</c> 在，但内容不是合法 JSON。0 实参。</summary>
    public const string MANIFEST_INVALID = "MANIFEST_INVALID";

    /// <summary>包格式版本与当前支持的不符。2 实参：包内版本、当前支持版本。</summary>
    public const string FORMAT_VERSION_UNSUPPORTED = "FORMAT_VERSION_UNSUPPORTED";

    /// <summary>包内没有 <c>project.json</c> 条目。0 实参。</summary>
    public const string PROJECT_MISSING = "PROJECT_MISSING";

    /// <summary><c>project.json</c> 在，但解析不出工程数据（坏 JSON 或结构不对）。0 实参。</summary>
    public const string PROJECT_INVALID = "PROJECT_INVALID";

    /// <summary><c>assets.json</c> 在，但内容不是合法 JSON。0 实参。</summary>
    public const string ASSETS_INVALID = "ASSETS_INVALID";

    /// <summary>
    /// <c>reslib.json</c> 在，但内容不是合法 JSON —— <c>HmiResourceLibrary.Deserialize</c>
    /// 直接调 <c>JsonSerializer</c>，抛出的是 <c>System.Text.Json.JsonException</c>。0 实参。
    ///
    /// <para>⚠ 它发生在 <c>Directory.CreateDirectory(assetDir)</c> **之后** ⇒ 与
    /// <see cref="ASSET_EXTRACT_FAILED"/> 同属「中途失败」（阶段 105 收口）：失败时必须**回滚**
    /// 本方法自己建出来的目录，否则磁盘上留一个半填充的工作区目录。</para>
    /// </summary>
    public const string RESLIB_INVALID = "RESLIB_INVALID";

    /// <summary>
    /// 资产文件提取 / 写盘失败（包内条目数据损坏、目标不可写）。1 实参：包内条目名
    /// （即 <c>assets.json</c> 里那条 <c>FilePath</c>）。
    ///
    /// <para>触发样本（阶段 105 探针实测，不是推断）：<c>assets.json</c> 指向一条以 <c>/</c>
    /// 结尾的条目 ⇒ <c>Path.GetFileName</c> 为空 ⇒ <c>ExtractToFile</c> 的目标是**已存在的目录**
    /// ⇒ <c>UnauthorizedAccessException</c>（BCL 英文原文，原先直接进状态栏）。</para>
    /// </summary>
    public const string ASSET_EXTRACT_FAILED = "ASSET_EXTRACT_FAILED";
}
