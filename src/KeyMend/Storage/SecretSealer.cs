using KeyMend.Rules;

namespace KeyMend.Storage;

/// <summary>Итог переноса открытых секретов из настроек в хранилище.</summary>
/// <param name="Settings">Настройки без открытых текстов секретов.</param>
/// <param name="Secrets">Что надо записать в хранилище: ключ → текст. Пусто, если переносить нечего.</param>
internal sealed record SealResult(AppSettings Settings, IReadOnlyDictionary<string, string> Secrets);

/// <summary>
/// Автозапечатывание секретов (SPEC F5.4): сокращение с <c>"secret": true</c> и открытым <c>text</c> теряет текст,
/// получает <c>secretRef</c>, а текст уходит в хранилище. Чистая логика без диска; тексты наружу отдаёт только в
/// <see cref="SealResult.Secrets"/>.
/// </summary>
internal static class SecretSealer
{
    /// <summary>Находит открытые секреты и заменяет их ссылками.</summary>
    /// <param name="settings">Настройки, разобранные из файла.</param>
    /// <param name="newKey">Создаёт ключ нового секрета.</param>
    /// <returns>Настройки без текстов секретов и то, что надо сохранить.</returns>
    public static SealResult Seal(AppSettings settings, Func<string> newKey)
    {
        var secrets = new Dictionary<string, string>();
        var snippets = new List<Snippet>(settings.Snippets.Count);
        foreach (var snippet in settings.Snippets)
        {
            if (snippet.Secret && !string.IsNullOrEmpty(snippet.Text))
            {
                var key = snippet.SecretRef ?? newKey();
                secrets[key] = snippet.Text;
                snippets.Add(snippet with { Text = null, SecretRef = key });
            }
            else
            {
                snippets.Add(snippet);
            }
        }

        return secrets.Count == 0
            ? new SealResult(settings, secrets)
            : new SealResult(settings with { Snippets = snippets }, secrets);
    }
}
