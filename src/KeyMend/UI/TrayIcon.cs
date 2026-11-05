using System.Collections.Concurrent;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyMend.Infrastructure;
using KeyMend.Native;

namespace KeyMend.UI;

/// <summary>Команды меню трея.</summary>
internal enum TrayCommand
{
    /// <summary>Включить или выключить KeyMend (пауза).</summary>
    ToggleEnabled,

    /// <summary>Открыть настройки.</summary>
    OpenSettings,

    /// <summary>Включить или выключить обычный автозапуск.</summary>
    ToggleAutostart,

    /// <summary>Включить или выключить автозапуск от администратора (SPEC 2.6).</summary>
    ToggleAdminAutostart,

    /// <summary>Отменить таймер питания (SPEC F4.2).</summary>
    CancelPowerTimer,

    /// <summary>Открыть папку данных.</summary>
    OpenDataFolder,

    /// <summary>Показать сведения о программе.</summary>
    About,

    /// <summary>Выйти из программы.</summary>
    Exit,
}

/// <summary>Состояние пунктов меню; запрашивается каждый раз при открытии меню.</summary>
/// <param name="Enabled">KeyMend включён (не на паузе).</param>
/// <param name="AutostartOn">Обычный автозапуск включён.</param>
/// <param name="AdminAutostartOn">Автозапуск от администратора включён; обычный автозапуск при этом недоступен.</param>
/// <param name="PowerTimer">Описание активного таймера питания или <c>null</c>, если таймера нет.</param>
internal readonly record struct TrayMenuState(bool Enabled, bool AutostartOn, bool AdminAutostartOn, string? PowerTimer);

/// <summary>
/// Значок KeyMend в области уведомлений на <c>Shell_NotifyIcon</c> (SPEC F10, В22), без WinForms. Своё скрытое окно верхнего
/// уровня принимает сообщения значка и широковещательное <c>TaskbarCreated</c> (значок возвращается после перезапуска
/// Проводника). Все методы, кроме <see cref="Notify"/>, вызываются из потока UI.
/// </summary>
internal sealed class TrayIcon : INotifier, IDisposable
{
    /// <summary>Сообщение окну от значка (в рамках <c>WM_APP</c>).</summary>
    internal const uint CallbackMessage = Win32.WmApp + 1;

    /// <summary>Идентификатор значка в рамках окна.</summary>
    internal const uint IconId = 1;

    /// <summary>Сообщение окну: выполнить отложенные действия из <see cref="Post"/>.</summary>
    internal const uint PostedMessage = Win32.WmApp + 2;

    /// <summary>Журнал.</summary>
    private readonly ILog _log;

    /// <summary>Получатель команд меню.</summary>
    private readonly Action<TrayCommand> _onCommand;

    /// <summary>Источник состояния пунктов меню.</summary>
    private readonly Func<TrayMenuState> _getState;

    /// <summary>Диспетчер потока UI: уведомления можно слать из других потоков.</summary>
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    /// <summary>Действия из других потоков, ожидающие выполнения в потоке UI.</summary>
    private readonly ConcurrentQueue<Action> _posted = new();

    /// <summary>Скрытое окно для сообщений значка.</summary>
    private readonly HwndSource _window;

    /// <summary>Кэш HICON встроенных вариантов; освобождается после удаления значка из трея.</summary>
    private readonly TrayIconCache _icons;

    /// <summary>Дескриптор окна, сохранённый для отправки сообщений из других потоков.</summary>
    private readonly nint _handle;

    /// <summary>Код сообщения <c>TaskbarCreated</c>.</summary>
    private readonly uint _taskbarCreated = User32.RegisterWindowMessage("TaskbarCreated");

    /// <summary>Подсказка значка.</summary>
    private string _tip = TrayTip.Build(paused: false, elevated: false, powerTimer: null);

    /// <summary>KeyMend на паузе; вместе с <see cref="_elevated"/> выбирает вариант значка.</summary>
    private bool _paused;

    /// <summary>Программа работает от администратора.</summary>
    private bool _elevated;

    /// <summary>Открытое сейчас меню или <c>null</c>.</summary>
    private TrayMenuWindow? _menu;

    /// <summary>Значок добавлен в область уведомлений.</summary>
    private bool _added;

    /// <summary>Создаёт окно и добавляет значок.</summary>
    /// <param name="log">Журнал.</param>
    /// <param name="onCommand">Получает выбранную команду меню.</param>
    /// <param name="getState">Возвращает состояние пунктов меню.</param>
    public TrayIcon(ILog log, Action<TrayCommand> onCommand, Func<TrayMenuState> getState)
    {
        _log = log;
        _onCommand = onCommand;
        _getState = getState;
        _icons = new TrayIconCache(log);
        _window = new HwndSource(new HwndSourceParameters("KeyMend.Tray", 0, 0) { WindowStyle = 0 });
        _handle = _window.Handle;
        _window.AddHook(WndProc);
        AddIcon();
    }

    /// <summary>Меняет подсказку значка.</summary>
    /// <param name="paused">KeyMend на паузе.</param>
    /// <param name="elevated">Программа работает от администратора.</param>
    /// <param name="powerTimer">Описание активного таймера питания или <c>null</c>.</param>
    public void UpdateStatus(bool paused, bool elevated, string? powerTimer = null)
    {
        _paused = paused;
        _elevated = elevated;
        _tip = TrayTip.Build(paused, elevated, powerTimer);
        if (!_added)
        {
            return;
        }

        var data = CreateData(Win32.NifIcon | Win32.NifTip | Win32.NifShowTip);
        data.HIcon = CurrentIcon();
        unsafe
        {
            CopyText(data.SzTip, NotifyIconData.TipLength, _tip);
        }

        Modify(ref data);
    }

    /// <summary>Windows сменила тему приложений (светлая или тёмная).</summary>
    public event Action? SystemThemeChanged;

    /// <summary>Описатель скрытого окна значка: окно-владелец для буфера обмена.</summary>
    public nint WindowHandle => _window.Handle;

    /// <summary>Ставит действие в очередь потока UI; можно вызывать из любого потока.</summary>
    /// <param name="action">Что выполнить.</param>
    public void Post(Action action)
    {
        _posted.Enqueue(action);
        User32.PostMessage(_handle, PostedMessage, 0, 0);
    }

    /// <inheritdoc />
    public void Notify(string title, string message, NotifyLevel level = NotifyLevel.Info)
    {
        if (!_dispatcher.CheckAccess())
        {
            Post(() => Notify(title, message, level));
            return;
        }

        if (!_added)
        {
            return;
        }

        // NIF_SHOWTIP нужен в каждом NIM_MODIFY: без него в режиме версии 4 стандартная подсказка значка пропадает.
        var data = CreateData(Win32.NifInfo | Win32.NifShowTip);
        data.DwInfoFlags = level switch
        {
            NotifyLevel.Error => Win32.NiifError,
            NotifyLevel.Warning => Win32.NiifWarning,
            _ => Win32.NiifInfo,
        };
        unsafe
        {
            CopyText(data.SzInfoTitle, NotifyIconData.InfoTitleLength, title);
            CopyText(data.SzInfo, NotifyIconData.InfoLength, message);
        }

        Modify(ref data);
    }

    /// <summary>
    /// Убирает значок из области уведомлений. Безопасно вызывать повторно и при аварийном выходе: иначе от процесса остаётся
    /// «призрачный» значок, который исчезает только при наведении мыши.
    /// </summary>
    public void RemoveIcon()
    {
        if (_added)
        {
            _added = false;
            var data = CreateData(0);
            Shell32.ShellNotifyIcon(Win32.NimDelete, ref data);
        }
    }

    /// <summary>Убирает значок и закрывает окно.</summary>
    public void Dispose()
    {
        _menu?.CloseMenu();
        RemoveIcon();
        _icons.Dispose();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }

    /// <summary>Копирует строку в фиксированный буфер структуры, обрезая по ёмкости и завершая нулём.</summary>
    /// <param name="destination">Начало буфера.</param>
    /// <param name="capacity">Ёмкость буфера в символах.</param>
    /// <param name="text">Строка.</param>
    private static unsafe void CopyText(char* destination, int capacity, string text)
    {
        var buffer = new Span<char>(destination, capacity);
        var length = Math.Min(text.Length, capacity - 1);
        text.AsSpan(0, length).CopyTo(buffer);
        buffer[length] = '\0';
    }

    /// <summary>Распаковывает координату из слова: значения со знаком (мониторы слева и сверху имеют отрицательные координаты).</summary>
    /// <param name="value">Значение.</param>
    /// <param name="shift">Сдвиг: 0 — младшее слово, 16 — старшее.</param>
    /// <returns>Координата со знаком.</returns>
    private static int SignedWord(nint value, int shift) => (short)(((long)value >> shift) & 0xFFFF);

    /// <summary>Создаёт структуру с заполненными размером, окном и идентификатором.</summary>
    /// <param name="flags">Флаги <c>NIF_*</c>.</param>
    /// <returns>Структура для <c>Shell_NotifyIcon</c>.</returns>
    private NotifyIconData CreateData(uint flags)
    {
        unsafe
        {
            return new NotifyIconData
            {
                CbSize = (uint)sizeof(NotifyIconData),
                HWnd = _window.Handle,
                UId = IconId,
                UFlags = flags,
            };
        }
    }

    /// <summary>Добавляет значок в область уведомлений и включает поведение версии 4. Если оболочки ещё нет, сделает это по <c>TaskbarCreated</c>.</summary>
    private void AddIcon()
    {
        var data = CreateData(Win32.NifMessage | Win32.NifIcon | Win32.NifTip | Win32.NifShowTip);
        data.UCallbackMessage = CallbackMessage;
        data.HIcon = CurrentIcon();
        unsafe
        {
            CopyText(data.SzTip, NotifyIconData.TipLength, _tip);
        }

        _added = Shell32.ShellNotifyIcon(Win32.NimAdd, ref data);
        if (!_added)
        {
            _log.Info("tray icon not added yet (shell is not ready)");
            return;
        }

        data.UVersion = Win32.NotifyIconVersion4;
        Shell32.ShellNotifyIcon(Win32.NimSetVersion, ref data);
    }

    /// <summary>Выбирает встроенный значок текущего состояния под фактический размер для DPI; при сбое берёт системный запасной.</summary>
    /// <returns>Дескриптор, владение которым определяет его источник.</returns>
    private nint CurrentIcon()
    {
        var dpi = User32.GetDpiForWindow(_window.Handle);
        var size = User32.GetSystemMetricsForDpi(Win32.SmCxSmIcon, dpi == 0 ? 96 : dpi);
        var icon = _icons.Handle(_paused, _elevated, size);
        return icon != 0 ? icon : LoadSystemFallback();
    }

    /// <summary>Загружает стандартный общий значок приложения, если встроенный не удалось создать.</summary>
    /// <returns>Общий дескриптор значка; уничтожать не нужно.</returns>
    private nint LoadSystemFallback()
    {
        var icon = User32.LoadImage(0, Win32.IdiApplication, Win32.ImageIcon, 0, 0, Win32.LrShared);
        if (icon == 0)
        {
            _log.Error("could not load fallback tray icon");
        }

        return icon;
    }

    /// <summary>Отправляет <c>NIM_MODIFY</c>; ошибка пишется в лог.</summary>
    /// <param name="data">Структура с изменяемыми полями.</param>
    private void Modify(ref NotifyIconData data)
    {
        if (!Shell32.ShellNotifyIcon(Win32.NimModify, ref data))
        {
            _log.Error("Shell_NotifyIcon(NIM_MODIFY) failed");
        }
    }

    /// <summary>Обрабатывает сообщения скрытого окна: события значка и <c>TaskbarCreated</c>.</summary>
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)msg == CallbackMessage)
        {
            // Версия 4: событие — младшее слово lParam, координаты привязки — слова wParam.
            switch ((uint)((long)lParam & 0xFFFF))
            {
                case Win32.WmContextMenu:
                    ShowMenu(SignedWord(wParam, 0), SignedWord(wParam, 16));
                    break;
                case Win32.WmLButtonDblClk:
                    _onCommand(TrayCommand.OpenSettings);
                    break;
            }

            handled = true;
        }
        else if ((uint)msg == PostedMessage)
        {
            while (_posted.TryDequeue(out var action))
            {
                action();
            }

            handled = true;
        }
        else if ((uint)msg == Win32.WmSettingChange)
        {
            if (System.Runtime.InteropServices.Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
            {
                SystemThemeChanged?.Invoke();
            }
        }
        else if ((uint)msg == _taskbarCreated)
        {
            _added = false;
            AddIcon();
            handled = true;
        }

        return 0;
    }

    /// <summary>Показывает меню у значка и передаёт выбранную команду.</summary>
    /// <param name="x">Экранная координата X привязки.</param>
    /// <param name="y">Экранная координата Y привязки.</param>
    private void ShowMenu(int x, int y)
    {
        _menu?.CloseMenu();
        var menu = new TrayMenuWindow(TrayMenuItems.Build(_getState()));
        _menu = menu;
        menu.Chosen += _onCommand;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_menu, menu))
            {
                _menu = null;
            }
        };
        menu.ShowAt(x, y);
    }
}
