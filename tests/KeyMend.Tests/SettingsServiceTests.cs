using KeyMend.Storage;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="SettingsService"/>: правка из окна, запись и уведомление подписчиков.</summary>
public sealed class SettingsServiceTests
{
    /// <summary>Писатель-заглушка: запоминает записанное или падает.</summary>
    private sealed class FakeWriter(bool fail = false) : ISettingsWriter
    {
        /// <summary>Что записано последним.</summary>
        public AppSettings? Saved { get; private set; }

        /// <inheritdoc />
        public void Save(AppSettings settings)
        {
            if (fail)
            {
                throw new IOException("disk full");
            }

            Saved = settings;
        }
    }

    /// <summary>Успешная правка пишется в файл, становится текущей и сообщается подписчику.</summary>
    [Fact]
    public void Update_Saved_RaisesChanged()
    {
        var writer = new FakeWriter();
        var service = new SettingsService(new AppSettings(), writer, new TestLog());
        AppSettings? seen = null;
        service.Changed += settings => seen = settings;

        var result = service.Update(s => s with { Theme = AppTheme.Dark });

        Assert.True(result);
        Assert.Equal(AppTheme.Dark, service.Current.Theme);
        Assert.Equal(AppTheme.Dark, writer.Saved?.Theme);
        Assert.Equal(AppTheme.Dark, seen?.Theme);
    }

    /// <summary>Сбой записи оставляет прежние настройки и никому не сообщается.</summary>
    [Fact]
    public void Update_SaveFails_KeepsCurrentAndReturnsFalse()
    {
        var service = new SettingsService(new AppSettings(), new FakeWriter(fail: true), new TestLog());
        var raised = false;
        service.Changed += _ => raised = true;

        var result = service.Update(s => s with { Theme = AppTheme.Dark });

        Assert.False(result);
        Assert.Equal(AppTheme.System, service.Current.Theme);
        Assert.False(raised);
    }

    /// <summary>Перечитанные настройки становятся текущими и сообщаются, но не пишутся обратно.</summary>
    [Fact]
    public void Replace_NewSettings_RaisesChangedWithoutSaving()
    {
        var writer = new FakeWriter();
        var service = new SettingsService(new AppSettings(), writer, new TestLog());
        var raised = false;
        service.Changed += _ => raised = true;

        service.Replace(new AppSettings { Theme = AppTheme.Light });

        Assert.True(raised);
        Assert.Equal(AppTheme.Light, service.Current.Theme);
        Assert.Null(writer.Saved);
    }
}
