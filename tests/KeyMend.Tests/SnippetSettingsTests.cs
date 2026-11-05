using KeyMend.Input;
using KeyMend.Rules;
using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты чтения и записи сокращений и шагов «Вставить текст» и «Нажать» (SPEC 7.3).</summary>
public sealed class SnippetSettingsTests
{
    /// <summary>Пример сокращения из SPEC 7.3 читается целиком.</summary>
    [Fact]
    public void Parse_SpecSnippet_ReadsAllFields()
    {
        const string Json = """
            { "snippets": [
              { "id": "a", "keys": "ll ", "text": "Добрый день!", "secret": false, "insert": "Clipboard", "newline": "Enter",
                "after": [ { "kind": "Press", "keys": "Ctrl+Enter" } ], "enabled": false }
            ] }
            """;

        var result = SettingsJson.Parse(Json);

        var snippet = Assert.Single(Assert.IsType<AppSettings>(result.Settings).Snippets);
        Assert.Equal("ll ", snippet.Keys);
        Assert.Equal("Добрый день!", snippet.Text);
        Assert.Equal(InsertMode.Clipboard, snippet.Insert);
        Assert.Equal(NewlineMode.Enter, snippet.Newline);
        Assert.False(snippet.Enabled);
        var press = Assert.IsType<PressStep>(Assert.Single(snippet.After));
        Assert.Equal(VirtualKey.Return, press.Keys.Key);
    }

    /// <summary>Значения по умолчанию: печать, Shift+Enter, включено.</summary>
    [Fact]
    public void Parse_MinimalSnippet_UsesDefaults()
    {
        var settings = Assert.IsType<AppSettings>(SettingsJson.Parse("""{ "snippets": [ { "keys": "ab " } ] }""").Settings);

        var snippet = Assert.Single(settings.Snippets);
        Assert.Equal(InsertMode.Type, snippet.Insert);
        Assert.Equal(NewlineMode.ShiftEnter, snippet.Newline);
        Assert.True(snippet.Enabled);
        Assert.Empty(snippet.After);
    }

    /// <summary>Битое сокращение пропускается, предупреждение не содержит ни сокращения, ни текста.</summary>
    [Fact]
    public void Parse_BrokenSnippet_IsSkippedWithoutContentInWarning()
    {
        const string Json = """
            { "snippets": [
              { "keys": "test-secret-1 ", "after": [ { "kind": "Press", "keys": "Banana" } ] },
              { "text": "test-secret-2" },
              { "keys": "ok " }
            ] }
            """;

        var result = SettingsJson.Parse(Json);

        var settings = Assert.IsType<AppSettings>(result.Settings);
        Assert.Equal(["ok "], settings.Snippets.Select(s => s.Keys));
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.DoesNotContain("test-secret", w, StringComparison.Ordinal));
    }

    /// <summary>Шаги «Вставить текст» и «Нажать» работают и в обычном правиле.</summary>
    [Fact]
    public void Parse_RuleWithInsertAndPressSteps_ReadsSteps()
    {
        const string Json = """
            { "rules": [ { "name": "r", "trigger": { "kind": "Press", "keys": "F13" },
              "steps": [ { "kind": "InsertText", "text": "привет" }, { "kind": "Press", "keys": "Down" } ] } ] }
            """;

        var rule = Assert.Single(Assert.IsType<AppSettings>(SettingsJson.Parse(Json).Settings).Rules);

        Assert.Equal("привет", Assert.IsType<InsertTextStep>(rule.Steps[0]).Text);
        Assert.Equal(VirtualKey.Down, Assert.IsType<PressStep>(rule.Steps[1]).Keys.Key);
    }

    /// <summary>Запись и чтение сохраняют сокращение; <c>erase</c> в файл не попадает.</summary>
    [Fact]
    public void Serialize_Snippet_RoundTripsAndOmitsErase()
    {
        var settings = new AppSettings
        {
            Snippets =
            [
                new Snippet
                {
                    Id = "s1",
                    Keys = "дд ",
                    Text = "Добрый день!",
                    After = [new PressStep { Keys = new KeyChord(ModifierSet.LCtrl, VirtualKey.Return) }],
                },
            ],
            Rules =
            [
                new Rule
                {
                    Name = "r",
                    Trigger = new PressTrigger { Key = (VirtualKey)0x7C },
                    Steps = [new InsertTextStep { Text = "x", Erase = 3 }],
                },
            ],
        };

        var json = SettingsJson.Serialize(settings);
        var parsed = Assert.IsType<AppSettings>(SettingsJson.Parse(json).Settings);

        Assert.DoesNotContain("erase", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Добрый день!", parsed.Snippets[0].Text);
        var press = Assert.IsType<PressStep>(parsed.Snippets[0].After[0]);
        Assert.Equal(new KeyChord(ModifierSet.LCtrl, VirtualKey.Return), press.Keys);
    }

    /// <summary>Текст и сокращение не попадают в строковое представление записей.</summary>
    [Fact]
    public void ToString_SnippetAndInsertStep_DoNotContainText()
    {
        var snippet = new Snippet { Id = "s1", Keys = "test-secret-1 ", Text = "test-secret-2" };
        var step = new InsertTextStep { Text = "test-secret-3" };

        var text = snippet + " " + step;

        Assert.DoesNotContain("test-secret", text, StringComparison.Ordinal);
    }
}
