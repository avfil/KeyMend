using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KeyMend.Storage.Transfer;

/// <summary>
/// Секреты в файле переноса (<c>secrets.json</c> внутри <c>.keymend</c>): зашифрованный паролем словарь «ключ — текст».
/// Все двоичные поля — base64.
/// </summary>
/// <param name="Kdf">Имя функции получения ключа; сейчас только <c>PBKDF2-SHA256</c>.</param>
/// <param name="Iterations">Число итераций PBKDF2.</param>
/// <param name="Salt">Соль.</param>
/// <param name="Nonce">Одноразовое число AES-GCM.</param>
/// <param name="Tag">Метка подлинности AES-GCM.</param>
/// <param name="Data">Шифртекст.</param>
internal sealed record SealedSecrets(string Kdf, int Iterations, string Salt, string Nonce, string Tag, string Data);

/// <summary>
/// Шифрование секретов для переноса паролем (SPEC F8): PBKDF2-SHA256 и AES-256-GCM из платформы, своей криптографии нет.
/// Тексты секретов и пароль в исключения и сообщения не попадают.
/// </summary>
internal static class TransferCrypto
{
    /// <summary>Имя функции получения ключа в файле.</summary>
    public const string KdfName = "PBKDF2-SHA256";

    /// <summary>Число итераций при записи.</summary>
    public const int Iterations = 600_000;

    /// <summary>Наименьшее число итераций, которое принимается при чтении: меньше — подделка или слабая защита.</summary>
    private const int _minIterations = 100_000;

    /// <summary>Наибольшее число итераций при чтении: файл с огромным числом завис бы на расчёте ключа.</summary>
    private const int _maxIterations = 5_000_000;

    /// <summary>Длина соли в байтах.</summary>
    private const int _saltSize = 16;

    /// <summary>Длина метки AES-GCM в байтах.</summary>
    private const int _tagSize = 16;

    /// <summary>Длина ключа AES в байтах.</summary>
    private const int _keySize = 32;

    /// <summary>Шифрует секреты паролем.</summary>
    /// <param name="secrets">Пары «ключ — текст».</param>
    /// <param name="password">Пароль владельца.</param>
    /// <returns>Зашифрованные секреты для записи в файл.</returns>
    public static SealedSecrets Encrypt(IReadOnlyDictionary<string, string> secrets, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(_saltSize);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var plain = JsonSerializer.SerializeToUtf8Bytes(secrets);
        var cipher = new byte[plain.Length];
        var tag = new byte[_tagSize];
        using (var aes = new AesGcm(DeriveKey(password, salt, Iterations), _tagSize))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        CryptographicOperations.ZeroMemory(plain);
        return new SealedSecrets(
            KdfName, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
    }

    /// <summary>Расшифровывает секреты.</summary>
    /// <param name="sealedSecrets">Секреты из файла.</param>
    /// <param name="password">Пароль владельца.</param>
    /// <param name="secrets">Пары «ключ — текст».</param>
    /// <returns><c>true</c>, если пароль верен и данные целы; иначе <c>false</c> (неверный пароль и порча не различаются).</returns>
    public static bool TryDecrypt(SealedSecrets sealedSecrets, string password, [NotNullWhen(true)] out IReadOnlyDictionary<string, string>? secrets)
    {
        secrets = null;
        if (sealedSecrets.Kdf != KdfName || sealedSecrets.Iterations is < _minIterations or > _maxIterations)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(sealedSecrets.Salt);
            var nonce = Convert.FromBase64String(sealedSecrets.Nonce);
            var tag = Convert.FromBase64String(sealedSecrets.Tag);
            var cipher = Convert.FromBase64String(sealedSecrets.Data);
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(DeriveKey(password, salt, sealedSecrets.Iterations), _tagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plain);
            }

            secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(plain);
            CryptographicOperations.ZeroMemory(plain);
            return secrets is not null;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException or JsonException)
        {
            return false;
        }
    }

    /// <summary>Получает ключ из пароля.</summary>
    /// <param name="password">Пароль.</param>
    /// <param name="salt">Соль.</param>
    /// <param name="iterations">Число итераций.</param>
    /// <returns>Ключ AES-256.</returns>
    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, _keySize);
}
