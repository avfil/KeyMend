using System.Text;

namespace KeyMend.Rules;

/// <summary>Как вставляется текст сокращения или шага «Вставить текст» (SPEC F5.3).</summary>
internal enum InsertMode
{
    /// <summary>Печать символами Unicode через <c>SendInput</c>.</summary>
    Type,

    /// <summary>Через буфер обмена с восстановлением его содержимого.</summary>
    Clipboard,
}

/// <summary>Чем разделяются строки многострочного текста при печати (SPEC F5.3).</summary>
internal enum NewlineMode
{
    /// <summary>Shift+Enter: форма хелпдеска не отправится раньше времени.</summary>
    ShiftEnter,

    /// <summary>Обычный Enter.</summary>
    Enter,
}

/// <summary>
/// Сокращение автозамены (SPEC F5): набранная последовательность физических клавиш заменяется текстом. Текст и
/// секреты не попадают в <see cref="object.ToString"/>, чтобы не оказаться в логе.
/// </summary>
internal sealed record Snippet
{
    /// <summary>Устойчивый идентификатор сокращения.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Сокращение, как его набирают (в любой установленной раскладке), включая конечный символ.</summary>
    public required string Keys { get; init; }

    /// <summary>Текст замены; у секретного сокращения только временно, до переноса в <c>secrets.dat</c>.</summary>
    public string? Text { get; init; }

    /// <summary>Секретный ли текст: хранится в <c>secrets.dat</c> и вставляется только через буфер обмена (SPEC F5.4).</summary>
    public bool Secret { get; init; }

    /// <summary>Ключ текста в <c>secrets.dat</c>.</summary>
    public string? SecretRef { get; init; }

    /// <summary>Способ вставки.</summary>
    public InsertMode Insert { get; init; } = InsertMode.Type;

    /// <summary>Разделитель строк при печати.</summary>
    public NewlineMode Newline { get; init; } = NewlineMode.ShiftEnter;

    /// <summary>Шаги после вставки (например, Ctrl+Enter).</summary>
    public IReadOnlyList<Step> After { get; init; } = [];

    /// <summary>Включено ли сокращение.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Печатает только идентификатор: сокращение и текст в логи не попадают.</summary>
    /// <param name="builder">Приёмник текста.</param>
    /// <returns>Всегда <c>true</c>.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Id = ").Append(Id);
        return true;
    }
}
