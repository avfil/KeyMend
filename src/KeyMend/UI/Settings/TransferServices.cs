using KeyMend.Storage.Transfer;

namespace KeyMend.UI.Settings;

/// <summary>Всё, что окну настроек нужно для раздела «Перенос», кроме самих настроек.</summary>
/// <param name="Transfer">Экспорт, загрузка и восстановление.</param>
/// <param name="Backups">Резервные копии для списка.</param>
internal sealed record TransferServices(SettingsTransfer Transfer, BackupStore Backups);
