using KeyMend.Rules;
using KeyMend.Text;
using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SnippetValidation"/>: ошибки, занятые клавиши и предупреждения о перекрытии.</summary>
public sealed class SnippetValidationTests
{
    /// <summary>Таблицы установленных раскладок: EN, затем RU.</summary>
    private static readonly LayoutTable[] _tables = [FallbackLayouts.Us, FallbackLayouts.Ru];

    /// <summary>Готовый черновик.</summary>
    private static SnippetDraft Draft(string keys = "дд ", string text = "Добрый день!") => new() { Keys = keys, Text = text };

    /// <summary>Сокращение настроек.</summary>
    private static Snippet Other(string id, string keys, bool enabled = true) =>
        new() { Id = id, Keys = keys, Text = "test-text", Enabled = enabled };

    /// <summary>Проверка с обычными таблицами.</summary>
    private static SnippetValidationResult Run(SnippetDraft draft, params Snippet[] others) =>
        SnippetValidation.Validate(draft, others, _tables, "scripts");

    /// <summary>Корректное сокращение сохраняется без замечаний.</summary>
    [Fact]
    public void Validate_Correct_CanSave()
    {
        var result = Run(Draft());

        Assert.True(result.CanSave);
        Assert.Empty(result.Overlaps);
    }

    /// <summary>Пустые сокращение и текст — ошибки.</summary>
    [Fact]
    public void Validate_EmptyKeysAndText_ReportsBoth()
    {
        var result = Run(Draft(keys: string.Empty, text: string.Empty));

        Assert.Equal(SnippetProblems.KeysEmpty | SnippetProblems.TextEmpty, result.Problems);
    }

    /// <summary>Символ, которого нет ни в одной раскладке, — ошибка.</summary>
    [Fact]
    public void Validate_UntypableKeys_ReportsUntypable()
    {
        Assert.Equal(SnippetProblems.KeysUntypable, Run(Draft(keys: "дд§ ")).Problems);
    }

    /// <summary>Те же клавиши в другой раскладке у включённого сокращения — ошибка со ссылкой на него.</summary>
    [Fact]
    public void Validate_SameKeysInOtherLayout_ReportsTaken()
    {
        var other = Other("dd", "ll ");

        var result = Run(Draft(), other);

        Assert.Equal(SnippetProblems.KeysTaken, result.Problems);
        Assert.Same(other, result.Taken);
    }

    /// <summary>Выключенное сокращение с теми же клавишами не мешает, как и выключенное редактируемое.</summary>
    [Fact]
    public void Validate_DisabledOnEitherSide_NoConflict()
    {
        Assert.True(Run(Draft(), Other("dd", "дд ", enabled: false)).CanSave);
        Assert.True(Run(Draft() with { Enabled = false }, Other("dd", "дд ")).CanSave);
    }

    /// <summary>Сокращение — начало другого: предупреждение, сохранить можно, в обе стороны.</summary>
    [Fact]
    public void Validate_Prefix_WarnsButCanSave()
    {
        var longer = Run(Draft(keys: "дд"), Other("dd", "дд "));
        var shorter = Run(Draft(keys: "дд "), Other("dd", "дд"));

        Assert.True(longer.CanSave);
        Assert.Equal(SnippetRelation.PrefixOf, Assert.Single(longer.Overlaps).Relation);
        Assert.True(shorter.CanSave);
        Assert.Equal(SnippetRelation.StartsWith, Assert.Single(shorter.Overlaps).Relation);
    }

    /// <summary>Проверка перед включением находит только включённое сокращение с теми же клавишами.</summary>
    [Fact]
    public void FindTaken_SameKeys_ReturnsOnlyEnabledOne()
    {
        var enabled = Other("dd", "ll ");
        var disabled = Other("obn2", "дд ", enabled: false);

        Assert.Same(enabled, SnippetValidation.FindTaken("дд ", [disabled, enabled], _tables));
        Assert.Null(SnippetValidation.FindTaken("дд ", [disabled], _tables));
        Assert.Null(SnippetValidation.FindTaken("gm ", [enabled], _tables));
    }

    /// <summary>Ошибки шагов «После вставки» приходят по порядку и запрещают сохранение.</summary>
    [Fact]
    public void Validate_BadAfterStep_ReportsStepError()
    {
        var draft = Draft() with { After = [new PressStep { Keys = default }] };

        var result = Run(draft);

        Assert.Equal(SnippetProblems.StepError, result.Problems);
        Assert.Equal([StepProblem.NoKeys], result.StepProblems);
    }
}
