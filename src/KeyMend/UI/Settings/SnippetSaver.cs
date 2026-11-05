using KeyMend.Infrastructure;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>Чем закончилась запись сокращения.</summary>
internal enum SnippetSaveResult
{
    /// <summary>Записано.</summary>
    Saved,

    /// <summary>Не удалось записать секрет в <c>secrets.dat</c>; настройки не менялись.</summary>
    SecretsFailed,

    /// <summary>Не удалось записать <c>settings.json</c>.</summary>
    SettingsFailed,
}

/// <summary>
/// Записывает сокращение из редактора и ведёт <c>secrets.dat</c> (SPEC F5.4): текст секрета до записи настроек уходит в
/// зашифрованное хранилище, в <c>settings.json</c> остаётся только ключ. Окно записывает настройки через
/// <see cref="SettingsService"/>, который секреты сам не запечатывает, поэтому это делает здесь. Тексты в журнал не пишутся.
/// </summary>
/// <param name="settings">Настройки приложения.</param>
/// <param name="secrets">Хранилище секретов.</param>
/// <param name="newGuid">Источник случайных значений для идентификаторов и ключей.</param>
/// <param name="log">Журнал.</param>
internal sealed class SnippetSaver(SettingsService settings, ISecretStore secrets, Func<Guid> newGuid, ILog log)
{
    /// <summary>Записывает новое или изменённое сокращение.</summary>
    /// <param name="original">Сокращение до правки или <c>null</c> для нового.</param>
    /// <param name="draft">Сокращение в редакторе.</param>
    /// <returns>Итог записи.</returns>
    public SnippetSaveResult Save(Snippet? original, SnippetDraft draft)
    {
        string? key = null;
        var createdKey = false;
        if (draft.Secret)
        {
            createdKey = original?.SecretRef is null;
            key = original?.SecretRef ?? "s-" + newGuid().ToString("N");
            if (!secrets.SetMany(new Dictionary<string, string> { [key] = draft.Text }))
            {
                return SnippetSaveResult.SecretsFailed;
            }
        }

        // Открытый текст секрета в настройки не попадает: после переноса в хранилище остаётся только ключ.
        var snippet = draft.ToSnippet(key) with { Text = draft.Secret ? null : draft.Text };
        if (!settings.Update(s => s with { Snippets = SnippetList.Upsert(s.Snippets, original, snippet, newGuid) }))
        {
            if (createdKey && key is not null)
            {
                RemoveKeys([key]);
            }

            return SnippetSaveResult.SettingsFailed;
        }

        // Сокращение перестало быть секретом: старый текст в хранилище больше не нужен.
        if (!draft.Secret && original?.SecretRef is { } oldKey)
        {
            RemoveKeys([oldKey]);
        }

        return SnippetSaveResult.Saved;
    }

    /// <summary>Удаляет сокращение вместе с его секретом.</summary>
    /// <param name="original">Сокращение.</param>
    /// <returns>Итог записи.</returns>
    public SnippetSaveResult Delete(Snippet original)
    {
        if (!settings.Update(s => s with { Snippets = SnippetList.Remove(s.Snippets, original) }))
        {
            return SnippetSaveResult.SettingsFailed;
        }

        if (original.SecretRef is { } key)
        {
            RemoveKeys([key]);
        }

        return SnippetSaveResult.Saved;
    }

    /// <summary>Убирает ключи из хранилища; неудача не мешает: лишняя запись в <c>secrets.dat</c> безвредна, но попадает в журнал.</summary>
    /// <param name="keys">Ключи.</param>
    private void RemoveKeys(string[] keys)
    {
        if (!secrets.Remove(keys))
        {
            log.Error("could not remove an unused secret from secrets.dat");
        }
    }
}
