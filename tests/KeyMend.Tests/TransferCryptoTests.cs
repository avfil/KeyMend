using KeyMend.Storage.Transfer;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="TransferCrypto"/>: секреты в файле переноса шифруются паролем (SPEC F8).</summary>
public sealed class TransferCryptoTests
{
    /// <summary>Секреты для проверок; вместо настоящих паролей заглушки.</summary>
    private static readonly Dictionary<string, string> _secrets = new() { ["s-1"] = "test-secret-1", ["s-2"] = "test-secret-2 ёж" };

    /// <summary>Верный пароль возвращает те же секреты.</summary>
    [Fact]
    public void TryDecrypt_RightPassword_ReturnsSecrets()
    {
        var sealedSecrets = TransferCrypto.Encrypt(_secrets, "test-password-1");

        Assert.True(TransferCrypto.TryDecrypt(sealedSecrets, "test-password-1", out var secrets));
        Assert.Equal(_secrets, secrets);
    }

    /// <summary>Неверный пароль не открывает секреты.</summary>
    [Fact]
    public void TryDecrypt_WrongPassword_ReturnsFalse()
    {
        var sealedSecrets = TransferCrypto.Encrypt(_secrets, "test-password-1");

        Assert.False(TransferCrypto.TryDecrypt(sealedSecrets, "test-password-2", out var secrets));
        Assert.Null(secrets);
    }

    /// <summary>Испорченный шифртекст или метка не проходят проверку подлинности.</summary>
    [Fact]
    public void TryDecrypt_TamperedData_ReturnsFalse()
    {
        var sealedSecrets = TransferCrypto.Encrypt(_secrets, "test-password-1");
        var data = Convert.FromBase64String(sealedSecrets.Data);
        data[0] ^= 1;

        Assert.False(TransferCrypto.TryDecrypt(sealedSecrets with { Data = Convert.ToBase64String(data) }, "test-password-1", out _));
        Assert.False(TransferCrypto.TryDecrypt(sealedSecrets with { Tag = Convert.ToBase64String(new byte[16]) }, "test-password-1", out _));
    }

    /// <summary>Не base64, чужая функция ключа и подозрительное число итераций отвергаются без исключения.</summary>
    [Theory]
    [InlineData("not base64!", "PBKDF2-SHA256", 600_000)]
    [InlineData("AAAA", "PBKDF2-SHA1", 600_000)]
    [InlineData("AAAA", "PBKDF2-SHA256", 1)]
    [InlineData("AAAA", "PBKDF2-SHA256", 2_000_000_000)]
    public void TryDecrypt_MalformedFile_ReturnsFalse(string field, string kdf, int iterations)
    {
        var sealedSecrets = new SealedSecrets(kdf, iterations, field, field, field, field);

        Assert.False(TransferCrypto.TryDecrypt(sealedSecrets, "test-password-1", out _));
    }

    /// <summary>Соль и одноразовое число каждый раз новые: одинаковые секреты шифруются по-разному.</summary>
    [Fact]
    public void Encrypt_Twice_GivesDifferentCiphertext()
    {
        var first = TransferCrypto.Encrypt(_secrets, "test-password-1");
        var second = TransferCrypto.Encrypt(_secrets, "test-password-1");

        Assert.NotEqual(first.Data, second.Data);
        Assert.NotEqual(first.Salt, second.Salt);
    }
}
