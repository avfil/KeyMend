using KeyMend.Rules;

namespace KeyMend.Input;

/// <summary>Отправка синтетического ввода в систему. Вызывается из потока функций, не из колбэка хука.</summary>
internal interface IInputSender
{
    /// <summary>
    /// Отправляет маскирующую клавишу (нажатие и отпускание), а затем, если задано, отпускание <paramref name="release"/>.
    /// Маска между нажатием и отпусканием Alt или Win не даёт системе открыть меню окна или «Пуск».
    /// </summary>
    /// <param name="release">Клавиша, отпускание которой подавил хук, или <see cref="VirtualKey.None"/>.</param>
    void SendMask(VirtualKey release);

    /// <summary>
    /// Стирает <paramref name="backspaces"/> символов (Backspace) и печатает <paramref name="text"/> символами Unicode,
    /// поэтому результат не зависит от текущей раскладки. Переводы строк печатаются клавишей Enter или Shift+Enter.
    /// Всё уходит одной очередью событий, чтобы ввод пользователя не вклинился в середину.
    /// </summary>
    /// <param name="backspaces">Сколько раз нажать Backspace.</param>
    /// <param name="text">Что напечатать.</param>
    /// <param name="newline">Чем заменять перевод строки.</param>
    /// <returns><c>false</c>, если система приняла не все события: тогда ввод прерван.</returns>
    bool SendTyped(int backspaces, string text, NewlineMode newline);

    /// <summary>
    /// Нажимает и отпускает сочетание: модификаторы по порядку, клавиша, модификаторы в обратном порядке. Бит «любой»
    /// (<c>Ctrl</c>) заменяется левой клавишей семейства.
    /// </summary>
    /// <param name="chord">Модификаторы и клавиша.</param>
    /// <returns><c>false</c>, если система приняла не все события.</returns>
    bool SendKeys(KeyChord chord);

    /// <summary>Нажимает и отпускает сочетание «модификатор + клавиша», например Ctrl+C.</summary>
    /// <param name="modifier">Модификатор.</param>
    /// <param name="key">Клавиша.</param>
    /// <returns><c>false</c>, если система приняла не все события.</returns>
    bool SendChord(VirtualKey modifier, VirtualKey key);

    /// <summary>
    /// Нажимает и отпускает клавишу <paramref name="count"/> раз подряд, при необходимости с зажатым Shift
    /// (Shift+Влево × N выделяет N символов, Shift+Home — до начала строки).
    /// </summary>
    /// <param name="key">Клавиша.</param>
    /// <param name="count">Сколько раз нажать; 0 ничего не отправляет.</param>
    /// <param name="shift">Держать ли левый Shift на время серии.</param>
    /// <returns><c>false</c>, если система приняла не все события.</returns>
    bool SendKeyRepeat(VirtualKey key, int count, bool shift);
}
