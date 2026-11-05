using KeyMend.Layouts;
using KeyMend.Storage;

namespace KeyMend.UI.Settings;

/// <summary>Всё, что окну настроек нужно для раздела «Автозамена», кроме самих настроек.</summary>
/// <param name="Saver">Запись и удаление сокращений вместе с секретами.</param>
/// <param name="Secrets">Хранилище секретов: редактор читает из него текст секрета.</param>
/// <param name="Tables">Таблицы установленных раскладок для проверок.</param>
internal sealed record SnippetServices(SnippetSaver Saver, ISecretStore Secrets, IInstalledTables Tables);
