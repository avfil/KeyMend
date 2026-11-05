using KeyMend.Rules;
using KeyMend.Text;

namespace KeyMend.Input;

/// <summary>
/// Готовый к чтению из колбэка хука набор привязок для одной программы: неизменяемый, собирается заранее вне потока хука.
/// Tap-привязки идут «широкими» (больше клавиш) вперёд, чтобы <c>LCtrl+RCtrl</c> проверялся раньше <c>RCtrl</c>.
/// Вместе с привязками лежит набор сокращений автозамены, действующий в этой программе.
/// </summary>
internal sealed class BindingTable
{
    /// <summary>Пустой набор.</summary>
    public static readonly BindingTable Empty = new([], [], SnippetMatcher.Empty);

    /// <summary>Создаёт набор.</summary>
    /// <param name="taps">Привязки tap.</param>
    /// <param name="presses">Привязки press.</param>
    /// <param name="snippets">Сокращения автозамены.</param>
    private BindingTable(Binding[] taps, Binding[] presses, SnippetMatcher snippets)
    {
        Taps = taps;
        Presses = presses;
        Snippets = snippets;
    }

    /// <summary>Привязки tap, самые «широкие» первыми.</summary>
    public Binding[] Taps { get; }

    /// <summary>Привязки press.</summary>
    public Binding[] Presses { get; }

    /// <summary>Сокращения автозамены, действующие в программе (пусто на паузе и там, где автозамена выключена).</summary>
    public SnippetMatcher Snippets { get; }

    /// <summary>Собирает набор из правил.</summary>
    /// <param name="rules">Правила, уже отобранные <see cref="RuleFilter"/>.</param>
    /// <param name="snippets">Сокращения автозамены или <c>null</c>, если их нет.</param>
    /// <returns>Набор привязок.</returns>
    public static BindingTable Build(IEnumerable<Rule> rules, SnippetMatcher? snippets = null)
    {
        var matcher = snippets ?? SnippetMatcher.Empty;
        var bindings = rules.Select(r => new Binding(r)).ToArray();
        var taps = bindings.Where(b => b.Base.Kind == BindingKind.Tap).OrderByDescending(b => b.Keys.Length).ToArray();
        var presses = bindings.Where(b => b.Base.Kind == BindingKind.Press).ToArray();
        return taps.Length == 0 && presses.Length == 0 && matcher.Count == 0 ? Empty : new BindingTable(taps, presses, matcher);
    }
}
