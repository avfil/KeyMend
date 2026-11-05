using KeyMend.Rules;

namespace KeyMend.UI.Settings;

/// <summary>
/// Сокращение в редакторе до сохранения. В <see cref="Text"/> лежит и открытый текст секрета: черновик живёт только в
/// памяти окна, а на диск секрет попадает через <see cref="SnippetSaver"/> уже зашифрованным. Текст в
/// <see cref="object.ToString"/> не печатается.
/// </summary>
internal sealed record SnippetDraft
{
    /// <summary>Идентификатор сокращения; у нового пуст.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Сокращение как его набирают, включая конечный символ.</summary>
    public string Keys { get; init; } = string.Empty;

    /// <summary>Включено ли сокращение.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Секретный ли текст.</summary>
    public bool Secret { get; init; }

    /// <summary>Текст замены; у секрета — его открытый текст.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Способ вставки; у секрета всегда буфер обмена.</summary>
    public InsertMode Insert { get; init; } = InsertMode.Type;

    /// <summary>Разделитель строк при печати.</summary>
    public NewlineMode Newline { get; init; } = NewlineMode.ShiftEnter;

    /// <summary>Шаги после вставки.</summary>
    public IReadOnlyList<Step> After { get; init; } = [];

    /// <summary>Строит черновик из сокращения.</summary>
    /// <param name="snippet">Сокращение.</param>
    /// <param name="secretText">Открытый текст секрета из хранилища; для обычного сокращения не используется.</param>
    /// <returns>Черновик с теми же значениями.</returns>
    public static SnippetDraft From(Snippet snippet, string? secretText) => new()
    {
        Id = snippet.Id,
        Keys = snippet.Keys,
        Enabled = snippet.Enabled,
        Secret = snippet.Secret,
        Text = (snippet.Secret ? secretText : snippet.Text) ?? string.Empty,
        Insert = snippet.Secret ? InsertMode.Clipboard : snippet.Insert,
        Newline = snippet.Newline,
        After = snippet.After,
    };

    /// <summary>
    /// Превращает черновик в сокращение для записи в <c>settings.json</c>. Текст секрета остаётся в <see cref="Snippet.Text"/>
    /// только до <see cref="SnippetSaver"/>, который заменяет его ссылкой на <c>secrets.dat</c>.
    /// </summary>
    /// <param name="secretRef">Ключ секрета в <c>secrets.dat</c>, если он уже есть.</param>
    /// <returns>Сокращение.</returns>
    public Snippet ToSnippet(string? secretRef) => new()
    {
        Id = Id,
        Keys = Keys,
        Enabled = Enabled,
        Secret = Secret,
        Text = Text,
        SecretRef = Secret ? secretRef : null,
        Insert = Secret ? InsertMode.Clipboard : Insert,
        Newline = Newline,
        After = After,
    };

    /// <summary>Печатает только идентификатор: сокращение и тексты в логи не попадают.</summary>
    /// <param name="builder">Приёмник текста.</param>
    /// <returns>Всегда <c>true</c>.</returns>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Id = ").Append(Id);
        return true;
    }
}
