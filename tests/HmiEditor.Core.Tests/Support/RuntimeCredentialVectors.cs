using System.Linq;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 跨语言凭据向量 —— 编辑器侧与固件侧**共用同一组**（固件那份以字面量写在
/// <c>firmware/t113/tests/hmi_actions_credential_test.c</c> 里）。
///
/// <para><b>为什么必须用"外部真值"：</b>哈希实现两侧各有一份（C# <c>RuntimePasswordHasher</c>
/// 与 C <c>hmi_crypto.c</c>）。若测试只是"用 C# 算一遍、再断言 C# 算出来的值"，那两边
/// **一起错**也不会红 —— 而"两侧一起错"恰恰是跨语言契约最危险的失效形态（设备上谁也别想登进去，
/// 且看不出哪边错）。所以本文件里的 Base64/hex 字面量取自**独立实现**
/// （Python <c>hashlib</c> + <c>base64</c>），两侧实现都必须向它收敛。</para>
///
/// <para><b>salt 刻意固定不随机：</b>随机 salt 无法产出跨语言可复现的向量。</para>
///
/// <para><b>这些是测试凭据，不对应任何真实账户。</b></para>
/// </summary>
internal static class RuntimeCredentialVectors
{
    /// <summary>salt A = 字节 0x00…0x0F。</summary>
    public static readonly byte[] SaltA = Enumerable.Range(0x00, 16).Select(i => (byte)i).ToArray();

    /// <summary>salt B = 字节 0x10…0x1F（第二个用户换 salt：同 salt 复用是坏习惯，向量层面也别示范）。</summary>
    public static readonly byte[] SaltB = Enumerable.Range(0x10, 16).Select(i => (byte)i).ToArray();

    /// <summary>saltA 的 Base64（即全零…全 F 那 16 字节）。</summary>
    public const string SaltABase64 = "AAECAwQFBgcICQoLDA0ODw==";

    /// <summary>saltB 的 Base64。</summary>
    public const string SaltBBase64 = "EBESExQVFhcYGRobHB0eHw==";

    /// <summary>用户 A 名（fixture 运行时用户表第一行）。</summary>
    public const string UserA = "op";

    /// <summary>用户 A 口令。</summary>
    public const string PasswordA = "op1234";

    /// <summary>
    /// 用户 A 在 <see cref="SaltA"/> + <c>RuntimePasswordHasher.HmiHashIterations</c>(1000) 下的存盘哈希。
    /// </summary>
    public const string HashA = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    /// <summary>用户 A 摘要的 hex 形式（固件测试用它比对 <c>hmi_sha256_iter</c> 的裸 32 字节）。</summary>
    public const string HashAHex = "c0dbb584355371bd9739789d1a091be1cf4612beb0c630c6006687d955ab38b9";

    /// <summary>
    /// 用户 A 在**迭代 1 次**（单轮，不二次哈希）下的存盘哈希 —— 钉住"迭代下界"这一档，
    /// 免得实现把 <c>iterations==1</c> 也当成"至少再算一轮"。
    /// </summary>
    public const string HashAIter1 = "AAECAwQFBgcICQoLDA0ODw==:Rd78B4SY8nqmzqniA7dig+WH3gpckz8lYhQvyhvJrgs=";

    /// <summary>用户 A 在迭代 3 次下的存盘哈希 —— 迭代路径的中间档。</summary>
    public const string HashAIter3 = "AAECAwQFBgcICQoLDA0ODw==:s+Mgm8ySh44XbRhtTA/aqr32t0p7Kw5dWdxoFNunSSo=";

    /// <summary>用户 B 名（fixture 运行时用户表第二行）。</summary>
    public const string UserB = "eng";

    /// <summary>用户 B 口令。</summary>
    public const string PasswordB = "eng1234";

    /// <summary>用户 B 在 <see cref="SaltB"/> + 1000 次下的存盘哈希。</summary>
    public const string HashB = "EBESExQVFhcYGRobHB0eHw==:Crs25Mtrbu7xzjNeHEL7A2pa0pc/PcJ067I3ohZ0/U4=";

    /// <summary>用户 B 摘要的 hex 形式。</summary>
    public const string HashBHex = "0abb36e4cb6b6eeef1ce335e1c42fb036a5ad2973f3dc274ebb237a21674fd4e";

    /// <summary>
    /// 第三个用户：**账户停用**。<see cref="IsEnabled"/> = false。
    /// <para>刻意复用 <see cref="PasswordA"/>/<see cref="SaltA"/>（即与用户 A 同一份凭据）：
    /// 这样"停用被拒"这条用例提交的口令是**正确的** —— 排除"口令错"的干扰，
    /// 纯粹测 enabled 语义。若两边口令不同，测试红了也说不清是停用生效还是口令不对。</para>
    /// </summary>
    public const string UserDisabled = "dis";

    /// <summary>停用用户的口令（与用户 A 相同，见上）。</summary>
    public const string PasswordDisabled = PasswordA;

    // ── 纯 SHA-256 的 NIST 标准向量（不涉 salt）—— 钉住哈希原语本身 ──

    /// <summary>SHA-256("abc")。</summary>
    public const string NistAbcHex = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    /// <summary>SHA-256("")。</summary>
    public const string NistEmptyHex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>SHA-256 的标准双块向量（56 字节输入，触发 padding 跨块路径）。</summary>
    public const string NistTwoBlockHex = "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1";
}
