using System.IO;
using System.Text.Json;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// LLM 配置持久化 —— 保存到应用数据目录 ai.config.json。
/// 注意: 文件中包含 API Key, 属机密信息, 仅存本地用户目录, 不进入项目文件/git。
///
/// <para><b>路径由 <see cref="AppPaths"/> 唯一解析</b>（不再自己拼 <c>%APPDATA%</c>）。
/// 早期版本写在 <c>%APPDATA%/HmiEditor/</c> 下 —— 改名成 <c>zhishu-hmi</c> 时若一刀切，
/// 用户已配置的端点/密钥会**静默失效**，所以保留一条**只读**的历史回退（见 <see cref="Load"/>）。</para>
/// </summary>
public static class LlmConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsDefaults.LlmConfig;

    /// <summary>文件名。新旧位置由**同一个字面量**推导，避免改名时只改一处。</summary>
    private const string FileName = "ai.config.json";

    /// <summary>配置文件路径（**写侧唯一入口**）: %APPDATA%/zhishu-hmi/ai.config.json</summary>
    public static string ConfigFilePath => AppPaths.DataFile(FileName);

    /// <summary>历史配置路径（<b>只读</b>，仅供升级后首次读取）: %APPDATA%/HmiEditor/ai.config.json</summary>
    public static string LegacyConfigFilePath => AppPaths.LegacyDataFile(FileName);

    /// <summary>
    /// 读取配置；文件不存在或损坏时返回默认配置。
    ///
    /// <para><b>回退判据是"新位置不存在"，不是"新位置读不出来"。</b>
    /// 两者刻意区分开：新文件**损坏**说明用户环境出了问题，此时静默启用历史配置
    /// 会把旧凭据"复活"，比直接回默认值更难排查。只有"新位置还没有文件"
    /// （= 升级后第一次运行）才是历史回退该管的场景。</para>
    /// </summary>
    public static LlmProviderConfig Load()
    {
        if (File.Exists(ConfigFilePath))
            return ReadFrom(ConfigFilePath) ?? new LlmProviderConfig();

        if (File.Exists(LegacyConfigFilePath))
            return ReadFrom(LegacyConfigFilePath) ?? new LlmProviderConfig();

        return new LlmProviderConfig();
    }

    /// <summary>保存配置。写入失败时不抛出(配置非关键路径, 不影响主流程)。**只写新位置**。</summary>
    public static bool Save(LlmProviderConfig config)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读一个具体路径；不存在 / 不可读 / 损坏一律返回 null（由调用方决定怎么兜）。</summary>
    private static LlmProviderConfig? ReadFrom(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LlmProviderConfig>(json, JsonOptions);
        }
        catch
        {
            // 配置损坏时回退默认, 不阻断编辑器启动
            return null;
        }
    }
}
