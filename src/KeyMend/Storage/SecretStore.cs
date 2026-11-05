using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using KeyMend.Infrastructure;

namespace KeyMend.Storage;

/// <summary>Шифрование байтов для <c>secrets.dat</c>; отдельный интерфейс нужен, чтобы тестировать хранилище без DPAPI.</summary>
internal interface ISecretProtector
{
    /// <summary>Шифрует данные.</summary>
    /// <param name="data">Открытые данные.</param>
    /// <returns>Зашифрованные данные.</returns>
    byte[] Protect(byte[] data);

    /// <summary>Расшифровывает данные.</summary>
    /// <param name="data">Зашифрованные данные.</param>
    /// <returns>Открытые данные.</returns>
    /// <exception cref="CryptographicException">Данные повреждены или зашифрованы другим пользователем.</exception>
    byte[] Unprotect(byte[] data);
}

/// <summary>DPAPI с областью «текущий пользователь» (SPEC F5.4): другой пользователь Windows и другой компьютер секреты не прочтут.</summary>
internal sealed class DpapiProtector : ISecretProtector
{
    /// <inheritdoc />
    public byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    /// <inheritdoc />
    public byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}

/// <summary>Хранилище секретных текстов автозамены по ключу (SPEC F5.4).</summary>
internal interface ISecretStore
{
    /// <summary>Находит текст секрета.</summary>
    /// <param name="key">Ключ секрета (<c>secretRef</c>).</param>
    /// <param name="text">Текст секрета.</param>
    /// <returns><c>true</c>, если секрет есть.</returns>
    bool TryGet(string key, [NotNullWhen(true)] out string? text);

    /// <summary>Сохраняет секреты на диск (шифрованным файлом), заменяя тексты с теми же ключами.</summary>
    /// <param name="secrets">Пары «ключ — текст».</param>
    /// <returns><c>true</c>, если файл записан; при <c>false</c> прежнее состояние не менялось.</returns>
    bool SetMany(IReadOnlyDictionary<string, string> secrets);

    /// <summary>Убирает секреты по ключам, чтобы удалённое сокращение не оставляло текст в <c>secrets.dat</c>.</summary>
    /// <param name="keys">Ключи секретов; отсутствующие игнорируются.</param>
    /// <returns><c>true</c>, если файл записан или менять было нечего; при <c>false</c> прежнее состояние не менялось.</returns>
    bool Remove(IReadOnlyCollection<string> keys);
}

/// <summary>
/// Секреты в <c>secrets.dat</c>: JSON-словарь «ключ — текст», зашифрованный целиком. Файл читается лениво и держится в
/// памяти; запись атомарная. Тексты и ключи в лог и исключения не попадают.
/// </summary>
/// <param name="path">Полный путь к <c>secrets.dat</c>.</param>
/// <param name="protector">Шифрование.</param>
/// <param name="log">Журнал.</param>
internal sealed class SecretStore(string path, ISecretProtector protector, ILog log) : ISecretStore
{
    /// <summary>Защита словаря и файла: читают поток функций, пишет поток настроек.</summary>
    private readonly Lock _lock = new();

    /// <summary>Секреты в памяти; <c>null</c>, пока файл не прочитан.</summary>
    private Dictionary<string, string>? _items;

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out string? text)
    {
        lock (_lock)
        {
            return Load().TryGetValue(key, out text);
        }
    }

    /// <inheritdoc />
    public bool SetMany(IReadOnlyDictionary<string, string> secrets)
    {
        lock (_lock)
        {
            var merged = new Dictionary<string, string>(Load());
            foreach (var (key, text) in secrets)
            {
                merged[key] = text;
            }

            return Write(merged);
        }
    }

    /// <inheritdoc />
    public bool Remove(IReadOnlyCollection<string> keys)
    {
        lock (_lock)
        {
            var remaining = new Dictionary<string, string>(Load());
            var changed = false;
            foreach (var key in keys)
            {
                changed |= remaining.Remove(key);
            }

            return !changed || Write(remaining);
        }
    }

    /// <summary>Копия всех секретов: для проверки перед загрузкой настроек и отката, если загрузка не удалась.</summary>
    /// <returns>Пары «ключ — текст».</returns>
    public IReadOnlyDictionary<string, string> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(Load());
        }
    }

    /// <summary>Заменяет все секреты разом (восстановление из резервной копии, откат).</summary>
    /// <param name="secrets">Новое содержимое хранилища.</param>
    /// <returns><c>true</c>, если файл записан; при <c>false</c> прежнее состояние не менялось.</returns>
    public bool ReplaceAll(IReadOnlyDictionary<string, string> secrets)
    {
        lock (_lock)
        {
            _ = Load();
            return Write(new Dictionary<string, string>(secrets));
        }
    }

    /// <summary>Шифрует словарь и атомарно записывает файл; в памяти он заменяется только после успешной записи.</summary>
    /// <param name="items">Новое содержимое.</param>
    /// <returns><c>true</c>, если файл записан.</returns>
    private bool Write(Dictionary<string, string> items)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var encrypted = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(items));
            AtomicFile.Write(path, encrypted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            log.Error("could not write secrets.dat", ex);
            return false;
        }

        _items = items;
        return true;
    }

    /// <summary>Читает файл при первом обращении; нечитаемый файл сохраняется как <c>.bad</c>, чтобы запись не стёрла его.</summary>
    /// <returns>Секреты в памяти.</returns>
    private Dictionary<string, string> Load()
    {
        if (_items is not null)
        {
            return _items;
        }

        _items = [];
        if (!File.Exists(path))
        {
            return _items;
        }

        try
        {
            var json = protector.Unprotect(File.ReadAllBytes(path));
            _items = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            log.Error("secrets.dat cannot be read, it is kept as secrets.dat.bad", ex);
            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                log.Error("could not save secrets.dat.bad", copy);
            }
        }

        return _items;
    }
}
