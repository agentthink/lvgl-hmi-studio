using System.Security.Cryptography;
using System.Text;

namespace HmiEditor.Core.Security;

/// <summary>
/// 运行时凭据的密码哈希 —— 编辑器侧唯一实现，与固件 C 侧（<c>firmware/t113/app/runtime/hmi_crypto.c</c>）
/// 构成一份**跨语言算法契约**。
///
/// <para><b>算法（固件必须逐位一致）：</b></para>
/// <code>
/// h0   = SHA256( salt ‖ utf8(password) )     // salt 恰为 HmiSaltBytes 字节
/// h_n  = SHA256( h_{n-1} )                   // n = 1 … HmiHashIterations-1
/// 结果 = h_{HmiHashIterations-1}
/// </code>
/// <para>迭代 ≥ 1；<c>iterations == 1</c> 即单轮（不二次哈希）。存盘格式
/// <c>"Base64(salt):Base64(hash)"</c>，迭代次数**不存进格式**而是作为常量下发
/// （<c>HMI_SEC_HASH_ITERATIONS</c>）—— 强度调整只需改这里的常量并重新导出，
/// 设备端自动跟随，不会出现"表里是旧轮数、代码按新轮数算"的错配。</para>
///
/// <para><b>迭代挡得住什么、挡不住什么（说清楚，别让使用者误判）：</b>
/// 哈希随用户表编译进固件、随固件一起落盘，拿到 dump 的人可以离线爆破；迭代只是把
/// 每次猜测的成本乘以 <see cref="HmiHashIterations"/>。1000 轮配合
/// <c>SecurityConfig.PasswordMinLength</c> = 6，仍属秒级到分钟级可爆破。
/// 所以这套凭据的真实定位是**防误操作**（操作员别越权改配方、别乱按调试按钮），
/// 不是防未授权访问。要真正抗爆破需要 PBKDF2 / Argon2 级别的 KDF，
/// 那要求设备端引入相应实现，本轮**刻意不做**。</para>
///
/// <para><b>为什么 salt 定长：</b>固件侧不做动态分配，salt 必须是定长缓冲区。
/// <see cref="Verify"/> 对 salt 长度严格校验，长度不符即判为无效凭据（拒绝登录），
/// 而不是"尽力解析出点什么"。</para>
///
/// <para><b>为什么这里不复用 <c>AuthenticationService.HashPassword</c>：</b>那是编辑器自身登录
/// 账户的算法（同样 SHA-256+salt，但单轮、且格式与轮数不受这里约束）。两套凭据服务于
/// 两个边界，各自的强度要求与演化节奏不同，强行合并会让"改设备端强度"变成"改编辑器登录强度"。</para>
/// </summary>
public static class RuntimePasswordHasher
{
    /// <summary>salt 字节数（跨语言契约，同时下发为固件缓冲区尺寸）。</summary>
    public const int HmiSaltBytes = 16;

    /// <summary>哈希迭代次数（跨语言契约，同时下发为 <c>HMI_SEC_HASH_ITERATIONS</c>）。</summary>
    public const int HmiHashIterations = 1000;

    /// <summary>SHA-256 摘要长度（字节）。</summary>
    public const int DigestBytes = 32;

    /// <summary>salt 与哈希在存盘格式里的分隔符。</summary>
    public const char Separator = ':';

    /// <summary>用随机 salt 生成存盘格式的密码哈希。</summary>
    public static string Hash(string password, int iterations = HmiHashIterations)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(HmiSaltBytes);
        return Format(salt, Iterate(salt, password, iterations));
    }

    /// <summary>用**指定** salt 生成存盘格式（固定向量测试与对拍用，业务路径请用 <see cref="Hash"/>）。</summary>
    public static string HashWithSalt(string password, byte[] salt, int iterations = HmiHashIterations)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);
        return Format(salt, Iterate(salt, password, iterations));
    }

    /// <summary>
    /// 校验明文密码是否匹配存盘哈希。
    /// <para>格式非法、salt 长度不符、摘要长度不符、分隔符数量不对 —— 一律返回 <c>false</c>，
    /// **不抛异常**（凭据来自工程文件，可能是手改坏的；这里要的是"拒绝登录"而不是"崩在登录按钮上"）。</para>
    /// </summary>
    public static bool Verify(string password, string passwordHash)
    {
        if (password is null) return false;
        if (string.IsNullOrEmpty(passwordHash)) return false;

        var parts = passwordHash.Split(Separator);
        if (parts.Length != 2) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != HmiSaltBytes) return false;
        if (expected.Length != DigestBytes) return false;

        var actual = Iterate(salt, password, HmiHashIterations);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>组装存盘格式 <c>"Base64(salt):Base64(hash)"</c>。</summary>
    public static string Format(byte[] salt, byte[] hash) =>
        Convert.ToBase64String(salt) + Separator + Convert.ToBase64String(hash);

    /// <summary>
    /// 迭代哈希核心。固件 <c>hmi_sha256_iter()</c> 必须产出完全相同的 32 字节。
    /// </summary>
    /// <param name="salt">盐，长度应为 <see cref="HmiSaltBytes"/>。</param>
    /// <param name="password">明文密码（按 UTF-8 编码）。</param>
    /// <param name="iterations">迭代次数，≥ 1。</param>
    public static byte[] Iterate(byte[] salt, string password, int iterations)
    {
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(password);
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "迭代次数必须 ≥ 1。");

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[salt.Length + passwordBytes.Length];
        salt.CopyTo(combined, 0);
        passwordBytes.CopyTo(combined, salt.Length);

        var hash = SHA256.HashData(combined);
        for (var i = 1; i < iterations; i++)
            hash = SHA256.HashData(hash);
        return hash;
    }
}
