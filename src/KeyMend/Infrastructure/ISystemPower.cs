namespace KeyMend.Infrastructure;

/// <summary>Действия с питанием компьютера (SPEC F4.2). Каждое возвращает <c>false</c>, если система отказала.</summary>
internal interface ISystemPower
{
    /// <summary>Включена ли гибернация в Windows.</summary>
    bool IsHibernateEnabled { get; }

    /// <summary>Сон. Возвращается после пробуждения.</summary>
    /// <returns><c>true</c>, если система приняла команду.</returns>
    bool Sleep();

    /// <summary>Гибернация. Возвращается после пробуждения.</summary>
    /// <returns><c>true</c>, если система приняла команду.</returns>
    bool Hibernate();

    /// <summary>Обычное выключение без принудительного закрытия программ.</summary>
    /// <returns><c>true</c>, если команда выдана.</returns>
    bool Shutdown();

    /// <summary>Обычная перезагрузка без принудительного закрытия программ.</summary>
    /// <returns><c>true</c>, если команда выдана.</returns>
    bool Restart();

    /// <summary>Блокировка сеанса.</summary>
    /// <returns><c>true</c>, если система приняла команду.</returns>
    bool Lock();

    /// <summary>Выход из системы.</summary>
    /// <returns><c>true</c>, если команда выдана.</returns>
    bool SignOut();
}
