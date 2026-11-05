using KeyMend.Text;

namespace KeyMend.Layouts;

/// <summary>Источник таблиц «клавиша → символ» для установленных раскладок.</summary>
internal interface IKeyTables
{
    /// <summary>Возвращает таблицу раскладки.</summary>
    /// <param name="hkl">Описатель раскладки.</param>
    /// <returns>Таблица; для неизвестной раскладки без запасного варианта — пустая.</returns>
    LayoutTable GetTable(nint hkl);
}
