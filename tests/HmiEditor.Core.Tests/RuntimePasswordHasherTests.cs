using System.Text;
using HmiEditor.Core.Security;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 运行时密码哈希（<see cref="RuntimePasswordHasher"/>）—— **跨语言契约的编辑器侧一半**。
///
/// <para><b>判据是外部真值，不是自证：</b>期望值由独立实现（Python <c>hashlib</c>）算出，
/// 见 <see cref="RuntimeCredentialVectors"/>。固件 <c>hmi_crypto.c</c> 侧用**同一组**向量
/// 再测一遍 —— 两侧向同一个外部真值收敛，才谈得上"两侧一致"。</para>
/// </summary>
public class RuntimePasswordHasherTests
{
    // ══════════════════ 向外部真值收敛 ══════════════════

    [Fact]
    public void HashWithSalt_MatchesExternalVector_AtProductionIterations()
    {
        var actual = RuntimePasswordHasher.HashWithSalt(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.SaltA);

        Assert.Equal(RuntimeCredentialVectors.HashA, actual);
    }

    /// <summary>第二个用户换了一把 salt —— 钉住"salt 真的参与了运算"，而不是被实现忽略掉。</summary>
    [Fact]
    public void HashWithSalt_MatchesExternalVector_ForSecondUser_DifferentSalt()
    {
        var actual = RuntimePasswordHasher.HashWithSalt(
            RuntimeCredentialVectors.PasswordB, RuntimeCredentialVectors.SaltB);

        Assert.Equal(RuntimeCredentialVectors.HashB, actual);
        Assert.NotEqual(RuntimeCredentialVectors.HashA, actual);
    }

    /// <summary>
    /// 迭代 **1** 次必须等于"单轮 SHA-256(salt‖pwd)"，不得再多算一轮 ——
    /// 否则 <c>iterations</c> 的语义整体偏移 1，两侧只要有一边理解不同就永久登不上。
    /// </summary>
    [Fact]
    public void HashWithSalt_MatchesExternalVector_AtSingleIteration()
    {
        var actual = RuntimePasswordHasher.HashWithSalt(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.SaltA, iterations: 1);

        Assert.Equal(RuntimeCredentialVectors.HashAIter1, actual);
    }

    /// <summary>迭代路径的中间档 —— 只钉住 1 与 1000 的话，"循环里少写一次"可能被两端掩盖。</summary>
    [Fact]
    public void HashWithSalt_MatchesExternalVector_AtThreeIterations()
    {
        var actual = RuntimePasswordHasher.HashWithSalt(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.SaltA, iterations: 3);

        Assert.Equal(RuntimeCredentialVectors.HashAIter3, actual);
    }

    [Fact]
    public void Iterate_IsDeterministic_ForSameInputs()
    {
        var first = RuntimePasswordHasher.Iterate(
            RuntimeCredentialVectors.SaltA, RuntimeCredentialVectors.PasswordA, 1000);
        var second = RuntimePasswordHasher.Iterate(
            RuntimeCredentialVectors.SaltA, RuntimeCredentialVectors.PasswordA, 1000);

        Assert.Equal(first, second);
        Assert.Equal(32, first.Length);   // SHA-256 摘要长度
    }

    [Fact]
    public void Iterate_Throws_WhenIterationsBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RuntimePasswordHasher.Iterate(
                RuntimeCredentialVectors.SaltA, RuntimeCredentialVectors.PasswordA, 0));
    }

    // ══════════════════ 业务路径：随机 salt ══════════════════

    [Fact]
    public void Hash_UsesRandomSalt_SoSamePasswordYieldsDifferentStoredHashes()
    {
        var first = RuntimePasswordHasher.Hash(RuntimeCredentialVectors.PasswordA);
        var second = RuntimePasswordHasher.Hash(RuntimeCredentialVectors.PasswordA);

        Assert.NotEqual(first, second);
        // 但两者都必须能校验通过 —— 否则"随机 salt"就只是把密码弄丢了
        Assert.True(RuntimePasswordHasher.Verify(RuntimeCredentialVectors.PasswordA, first));
        Assert.True(RuntimePasswordHasher.Verify(RuntimeCredentialVectors.PasswordA, second));
    }

    [Fact]
    public void Hash_NeverContainsThePlainPassword()
    {
        var hash = RuntimePasswordHasher.Hash(RuntimeCredentialVectors.PasswordA);

        Assert.DoesNotContain(RuntimeCredentialVectors.PasswordA, hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_UsesColonSeparator_WithBase64OnBothSides()
    {
        var hash = RuntimePasswordHasher.HashWithSalt(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.SaltA);

        var parts = hash.Split(RuntimePasswordHasher.Separator);
        Assert.Equal(2, parts.Length);
        Assert.Equal(RuntimeCredentialVectors.SaltABase64, parts[0]);
        Assert.Equal(RuntimePasswordHasher.HmiSaltBytes, Convert.FromBase64String(parts[0]).Length);
        Assert.Equal(RuntimePasswordHasher.DigestBytes, Convert.FromBase64String(parts[1]).Length);
    }

    // ══════════════════ 校验 ══════════════════

    [Theory]
    [InlineData(RuntimeCredentialVectors.UserA)]
    [InlineData(RuntimeCredentialVectors.UserB)]
    public void Verify_AcceptsVectorCredentials(string user)
    {
        var (password, hash) = user == RuntimeCredentialVectors.UserA
            ? (RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.HashA)
            : (RuntimeCredentialVectors.PasswordB, RuntimeCredentialVectors.HashB);

        Assert.True(RuntimePasswordHasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        Assert.False(RuntimePasswordHasher.Verify("wrong-password", RuntimeCredentialVectors.HashA));
    }

    /// <summary>
    /// 迭代次数是**常量契约**、不存进格式 —— 所以用别的轮数产出的哈希必须验不过。
    /// <para>这条把"两侧轮数必须一致"变成可执行断言：若哪天有人把格式改成带轮数、
    /// 或把 Verify 改成"猜轮数"，这条会红。</para>
    /// </summary>
    [Fact]
    public void Verify_RejectsHashProducedWithDifferentIterationCount()
    {
        Assert.False(RuntimePasswordHasher.Verify(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.HashAIter1));
        Assert.False(RuntimePasswordHasher.Verify(
            RuntimeCredentialVectors.PasswordA, RuntimeCredentialVectors.HashAIter3));
    }

    /// <summary>
    /// 空哈希 = 该账户**无法登录**（不是免密放行）。工程文件被手改坏时也走这条：
    /// 一律拒绝，而不是崩在登录按钮上。
    /// </summary>
    [Fact]
    public void Verify_ReturnsFalse_ForEmptyHash()
    {
        Assert.False(RuntimePasswordHasher.Verify(RuntimeCredentialVectors.PasswordA, ""));
    }

    [Theory]
    [InlineData("no-separator")]                                                  // 没有分隔符
    [InlineData("a:b:c")]                                                         // 段数不对
    [InlineData(":")]                                                             // 两段皆空
    [InlineData("!!!not-base64!!!:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]  // salt 不是合法 Base64
    [InlineData("AAECAwQFBgcICQoLDA0ODw==:AAAA")]                                 // 摘要长度不符
    [InlineData("AAAA:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]              // salt 长度不符
    public void Verify_RejectsMalformedHash(string malformed)
    {
        Assert.False(RuntimePasswordHasher.Verify(RuntimeCredentialVectors.PasswordA, malformed));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForNullPassword()
    {
        Assert.False(RuntimePasswordHasher.Verify(null!, RuntimeCredentialVectors.HashA));
    }

    [Fact]
    public void Hash_Throws_ForNullPassword()
    {
        Assert.Throws<ArgumentNullException>(() => RuntimePasswordHasher.Hash(null!));
    }

    /// <summary>
    /// 判据自检：上面那串"拒绝"用例必须真的是被**哈希比对**拒绝的，
    /// 而不是因为某个更早的守卫把所有输入都挡了（那样上面全是空转）。
    /// 用一个合法格式但错误密码的哈希走通"格式对、比对照"这条路径。
    /// </summary>
    [Fact]
    public void Verify_MalformedRejections_AreNotMaskedByAnEarlierBlanketReject()
    {
        var wellFormed = RuntimePasswordHasher.Hash("some-other-password");

        // 格式完全合法 ⇒ 必须走到比对，且因为密码不同而失败
        Assert.False(RuntimePasswordHasher.Verify(RuntimeCredentialVectors.PasswordA, wellFormed));
        // 同一格式换成对的密码 ⇒ 通过。证明上面那条失败确实来自"比不对"，不是"格式被拒"
        Assert.True(RuntimePasswordHasher.Verify("some-other-password", wellFormed));
    }
}
