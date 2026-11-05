using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using KeyMend.Infrastructure;
using KeyMend.Native;

namespace KeyMend.Input;

/// <summary>
/// Глобальные хуки клавиатуры и мыши на отдельном потоке с собственным циклом сообщений (SPEC 7.2). Колбэк только
/// фильтрует событие, спрашивает <see cref="IInputHandler"/> и кладёт сообщение в канал. Содержимое ввода нигде не записывается.
/// </summary>
internal sealed unsafe class KeyboardHook : IDisposable
{
    /// <summary>Период проверки, что хук не снят системой, в миллисекундах.</summary>
    internal const uint HeartbeatIntervalMs = 5000;

    /// <summary>Сколько мс система может получать ввод, которого не видели наши хуки, прежде чем хуки переустановят.</summary>
    internal const int HeartbeatGapMs = 2000;

    /// <summary>Не чаще одной записи «hook reinstalled» в лог за этот интервал, мс.</summary>
    internal const long ReinstallLogIntervalMs = 60_000;

    /// <summary>Не чаще одного сообщения «сверить раскладку» за этот интервал, мс.</summary>
    internal const int LayoutCheckIntervalMs = 100;

    /// <summary>Скан-код ложного Shift, который Windows генерирует при NumLock.</summary>
    internal const ushort FakeShiftScanCode = 0x2A;

    /// <summary>Скан-код ложного LCtrl, который Windows посылает перед RAlt на раскладках с AltGr (SPEC 2.4).</summary>
    internal const ushort FakeAltGrCtrlScanCode = 0x21D;

    /// <summary>Сообщение потоку хука: начать запись триггера.</summary>
    internal const uint RecorderStartMessage = Win32.WmApp + 1;

    /// <summary>Сообщение потоку хука: прервать запись триггера.</summary>
    internal const uint RecorderCancelMessage = Win32.WmApp + 2;

    /// <summary>Единственный экземпляр: колбэки хука статические и находят состояние через это поле.</summary>
    private static KeyboardHook? _instance;

    /// <summary>Приёмник событий.</summary>
    private readonly IInputHandler _handler;

    /// <summary>Канал сообщений потоку функций.</summary>
    private readonly ChannelWriter<HookMessage> _output;

    /// <summary>Журнал; вызывается только из цикла сообщений, не из колбэков.</summary>
    private readonly ILog _log;

    /// <summary>Слежение за окном переднего плана.</summary>
    private readonly ForegroundWatcher _foreground = new();

    /// <summary>Сигнал о том, что поток хука стартовал (успешно или нет).</summary>
    private readonly ManualResetEventSlim _started = new();

    /// <summary>Наборы привязок; заменяются целиком, поэтому чтение безопасно из любого потока.</summary>
    private volatile BindingTables _tables = BindingTables.Empty;

    /// <summary>Пауза всех функций из меню трея.</summary>
    private volatile bool _paused;

    /// <summary>Автомат записи триггера; трогает только поток хука.</summary>
    private readonly TriggerRecorder _recorder = new();

    /// <summary>Поток хука.</summary>
    private Thread? _thread;

    /// <summary>Идентификатор потока хука для <c>PostThreadMessage</c>.</summary>
    private uint _threadId;

    /// <summary>Описатель хука клавиатуры.</summary>
    private nint _keyboardHook;

    /// <summary>Описатель хука мыши.</summary>
    private nint _mouseHook;

    /// <summary>Идентификатор таймера heartbeat.</summary>
    private nuint _timer;

    /// <summary>Идентификатор таймера отложенного одиночного нажатия (0 — не запущен).</summary>
    private nuint _doubleTimer;

    /// <summary>Срок, на который запущен <see cref="_doubleTimer"/>.</summary>
    private uint? _armedDeadline;

    /// <summary>Набор привязок, который сейчас применён в обработчике.</summary>
    private BindingTable? _appliedTable;

    /// <summary>Ошибка запуска потока хука (передаётся в <see cref="Start"/>).</summary>
    private Exception? _startError;

    /// <summary>Тик последнего события, которое видели наши хуки (пишется и читается только потоком хука).</summary>
    private uint _lastHookTick;

    /// <summary>Тик последнего сообщения «сверить раскладку».</summary>
    private uint _lastLayoutCheck;

    /// <summary>CapsLock включён (ведётся по нажатиям; пишется и читается только потоком хука).</summary>
    private bool _capsLock;

    /// <summary>CapsLock сейчас физически нажат: отличает автоповтор от нового нажатия.</summary>
    private bool _capsDown;

    /// <summary>Время последней записи о переустановке в лог.</summary>
    private long _lastReinstallLog = -ReinstallLogIntervalMs;

    /// <summary>Создаёт хук; сам он ставится вызовом <see cref="Start"/>.</summary>
    /// <param name="handler">Приёмник событий ввода.</param>
    /// <param name="output">Канал, куда колбэк кладёт сообщения.</param>
    /// <param name="log">Журнал.</param>
    public KeyboardHook(IInputHandler handler, ChannelWriter<HookMessage> output, ILog log)
    {
        _handler = handler;
        _output = output;
        _log = log;
    }

    /// <summary>Приостановлена ли работа: действуют только правила, снимающие паузу.</summary>
    public bool IsPaused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Просит поток хука начать запись триггера: все клавиши идут в запись, правила и автозамена молчат (SPEC 3.1, п. 5).</summary>
    public void StartRecording() => PostRecorderRequest(RecorderStartMessage);

    /// <summary>Просит поток хука прервать запись триггера.</summary>
    public void CancelRecording() => PostRecorderRequest(RecorderCancelMessage);

    /// <summary>Подменяет наборы привязок (после загрузки или изменения настроек); применяются на ближайшем событии.</summary>
    /// <param name="tables">Новые наборы.</param>
    public void SetTables(BindingTables tables) => _tables = tables;

    /// <summary>Запускает поток и ставит хуки; бросает исключение, если установить не удалось.</summary>
    /// <exception cref="InvalidOperationException">Хук уже запущен или система отказала в установке.</exception>
    public void Start()
    {
        if (_thread is not null)
        {
            throw new InvalidOperationException("Hook already started");
        }

        _instance = this;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "KeyMend.Hook",
            Priority = ThreadPriority.Highest,
        };
        _thread.Start();
        _started.Wait();
        if (_startError is not null)
        {
            _thread.Join();
            _thread = null;
            _instance = null;
            throw new InvalidOperationException("Failed to install keyboard hook", _startError);
        }
    }

    /// <summary>Останавливает цикл сообщений, снимает хуки и ждёт завершения потока.</summary>
    public void Dispose()
    {
        var thread = _thread;
        if (thread is null)
        {
            return;
        }

        // Очередь сообщений создаётся потоком лениво, поэтому первые попытки могут не пройти.
        for (var i = 0; i < 20 && !User32.PostThreadMessage(_threadId, Win32.WmQuit, 0, 0); i++)
        {
            Thread.Sleep(10);
        }

        thread.Join(2000);
        _thread = null;
        _instance = null;
        _started.Dispose();
    }

    /// <summary>Колбэк хука клавиатуры. Исключения наружу не выпускаются: клавиша пропускается, в канал уходит <see cref="HookMessageKind.HookError"/>.</summary>
    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int nCode, nuint wParam, nint lParam)
    {
        var self = _instance;
        if (nCode != Win32.HcAction || self is null)
        {
            return User32.CallNextHookEx(self?._keyboardHook ?? 0, nCode, wParam, lParam);
        }

        try
        {
            if (self.HandleKey(in Unsafe.AsRef<KbdLlHookStruct>((void*)lParam)))
            {
                return 1;
            }
        }
        catch (Exception)
        {
            self._output.TryWrite(new HookMessage(HookMessageKind.HookError));
        }

        return User32.CallNextHookEx(self._keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>Колбэк хука мыши: передаёт обработчику клики и колесо, события мыши никогда не подавляются.</summary>
    [UnmanagedCallersOnly]
    private static nint MouseProc(int nCode, nuint wParam, nint lParam)
    {
        var self = _instance;
        if (nCode != Win32.HcAction || self is null)
        {
            return User32.CallNextHookEx(self?._mouseHook ?? 0, nCode, wParam, lParam);
        }

        try
        {
            self.HandleMouse((uint)wParam, in Unsafe.AsRef<MsLlHookStruct>((void*)lParam));
        }
        catch (Exception)
        {
            self._output.TryWrite(new HookMessage(HookMessageKind.HookError));
        }

        return User32.CallNextHookEx(self._mouseHook, nCode, wParam, lParam);
    }

    /// <summary>Клик любой кнопкой или прокрутка колеса.</summary>
    private static bool IsDownOrWheel(uint message) =>
        message is Win32.WmLButtonDown or Win32.WmRButtonDown or Win32.WmMButtonDown
            or Win32.WmXButtonDown or Win32.WmMouseWheel;

    /// <summary>Текущее время в миллисекундах той же шкалы, что и время событий хука.</summary>
    private static uint Now() => unchecked((uint)Environment.TickCount);

    /// <summary>Тело потока хука: установка, цикл сообщений с таймерами, снятие.</summary>
    private void ThreadMain()
    {
        _threadId = Kernel32.GetCurrentThreadId();

        // Очередь потока создана только что, поэтому состояние переключателя актуально; дальше флаг ведём сами.
        _capsLock = (User32.GetKeyState(Win32.VkCapital) & 1) != 0;
        try
        {
            InstallHooks();
            _timer = User32.SetTimer(0, 0, HeartbeatIntervalMs, 0);
        }
        catch (InvalidOperationException ex)
        {
            _startError = ex;
            _started.Set();
            return;
        }

        _started.Set();
        while (User32.GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.Message is RecorderStartMessage or RecorderCancelMessage)
            {
                ApplyRecorderRequest(msg.Message);
                continue;
            }

            if (msg.Message != Win32.WmTimer)
            {
                continue;
            }

            if (msg.WParam == _timer)
            {
                CheckHeartbeat();
            }
            else if (_doubleTimer != 0 && msg.WParam == _doubleTimer)
            {
                OnDoubleTimer();
            }
        }

        if (_timer != 0)
        {
            User32.KillTimer(0, _timer);
        }

        if (_doubleTimer != 0)
        {
            User32.KillTimer(0, _doubleTimer);
        }

        RemoveHooks();
    }

    /// <summary>Передаёт потоку хука просьбу к записывающему автомату: автомат трогает только этот поток, поэтому блокировки не нужны.</summary>
    /// <param name="message">Сообщение просьбы.</param>
    private void PostRecorderRequest(uint message)
    {
        if (_thread is not null)
        {
            User32.PostThreadMessage(_threadId, message, 0, 0);
        }
    }

    /// <summary>Выполняет просьбу начать или прервать запись. Обработчик правил сбрасывается: он не видел клавиш записи.</summary>
    /// <param name="message">Сообщение просьбы.</param>
    private void ApplyRecorderRequest(uint message)
    {
        if (message == RecorderStartMessage)
        {
            _recorder.Start();
            _log.Info("trigger recording started");
        }
        else
        {
            _recorder.Abort();
            _log.Info("trigger recording cancelled");
        }

        _handler.Reset();
    }

    /// <summary>Ставит оба хука и отмечает текущий момент как «последнее событие».</summary>
    private void InstallHooks()
    {
        _keyboardHook = User32.SetWindowsHookEx(Win32.WhKeyboardLl, &KeyboardProc, 0, 0);
        if (_keyboardHook == 0)
        {
            throw new InvalidOperationException($"SetWindowsHookEx(keyboard) failed, error {Marshal.GetLastPInvokeError()}");
        }

        _mouseHook = User32.SetWindowsHookEx(Win32.WhMouseLl, &MouseProc, 0, 0);
        if (_mouseHook == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            RemoveHooks();
            throw new InvalidOperationException($"SetWindowsHookEx(mouse) failed, error {error}");
        }

        _lastHookTick = Now();
    }

    /// <summary>Снимает хуки, если они стоят.</summary>
    private void RemoveHooks()
    {
        if (_keyboardHook != 0)
        {
            User32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }

        if (_mouseHook != 0)
        {
            User32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }
    }

    /// <summary>
    /// Проверяет, что система не сняла хуки молча (<c>LowLevelHooksTimeout</c>): если ввод в системе новее
    /// последнего события, увиденного нашими хуками, больше чем на <see cref="HeartbeatGapMs"/>, хуки ставятся заново.
    /// </summary>
    private void CheckHeartbeat()
    {
        var info = new LastInputInfo { CbSize = (uint)sizeof(LastInputInfo) };
        if (!User32.GetLastInputInfo(ref info))
        {
            return;
        }

        var gap = unchecked((int)(info.DwTime - _lastHookTick));
        if (gap <= HeartbeatGapMs)
        {
            return;
        }

        try
        {
            RemoveHooks();
            InstallHooks();
            _handler.Reset();
        }
        catch (InvalidOperationException ex)
        {
            _log.Error("hook reinstall failed", ex);
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastReinstallLog >= ReinstallLogIntervalMs)
        {
            _lastReinstallLog = now;
            _log.Info("hook reinstalled");
        }
    }

    /// <summary>Сработал таймер отложенного одиночного нажатия: отдаёт созревшие правила и заводит таймер на следующее.</summary>
    private void OnDoubleTimer()
    {
        User32.KillTimer(0, _doubleTimer);
        _doubleTimer = 0;
        _armedDeadline = null;
        PumpDue(Now());
        ArmDoubleTimer();
    }

    /// <summary>Фильтрует событие клавиатуры и передаёт обработчику; возвращает <c>true</c>, если клавишу надо подавить.</summary>
    private bool HandleKey(in KbdLlHookStruct kb)
    {
        _lastHookTick = Now();
        var isUp = (kb.Flags & Win32.LlkhfUp) != 0;

        // CapsLock учитываем всегда, даже для чужого ввода: иначе флаг разойдётся с системой.
        TrackCapsLock((VirtualKey)kb.VkCode, isUp);
        var e = new KeyEvent(
            (VirtualKey)kb.VkCode,
            (ushort)kb.ScanCode,
            isUp,
            (kb.Flags & Win32.LlkhfExtended) != 0,
            (kb.Flags & Win32.LlkhfInjected) != 0,
            kb.Time,
            _capsLock);

        // Свой ввод, автоввод менеджеров паролей и диктовку не обрабатываем и в состояние не учитываем.
        if (e.IsInjected)
        {
            return false;
        }

        // Windows добавляет ложный LShift со скан-кодом 0x2A вокруг клавиш навигации при включённом NumLock,
        // а на раскладках с AltGr — ложный LCtrl со скан-кодом 0x21D перед RAlt. Это не нажатия пользователя.
        if (e is { Vk: VirtualKey.LShift, IsExtended: true, ScanCode: FakeShiftScanCode }
            or { Vk: VirtualKey.LControl, ScanCode: FakeAltGrCtrlScanCode })
        {
            return false;
        }

        if (_recorder.IsActive)
        {
            return RecordKey(in e);
        }

        Refresh();
        PumpDue(e.Time);
        var decision = _handler.OnKey(in e);
        if (decision.Rule is { } rule)
        {
            _output.TryWrite(new HookMessage(HookMessageKind.RuleFired, rule, decision.SendMask, decision.ReleaseKey));
        }
        else if (decision.SendMask)
        {
            _output.TryWrite(new HookMessage(HookMessageKind.MaskOnly, null, true, decision.ReleaseKey));
        }
        else if (isUp && ModifierSets.IsModifier(e.Vk))
        {
            // Alt+Shift, Ctrl+Shift и Win+Space меняют раскладку без нас: сверить её, когда модификатор отпущен (SPEC F2.2).
            RequestLayoutCheck(e.Time);
        }

        PumpDue(e.Time);
        ArmDoubleTimer();
        return decision.Suppress;
    }

    /// <summary>Отдаёт событие записывающему автомату вместо правил; результат записи уходит потоку функций.</summary>
    private bool RecordKey(in KeyEvent e)
    {
        var outcome = _recorder.OnKey(in e);
        if (outcome.Gesture is { } gesture)
        {
            _output.TryWrite(new HookMessage(HookMessageKind.TriggerRecorded, Gesture: gesture));
        }

        if (!_recorder.IsActive)
        {
            // Правила не видели нажатий записи: начинаем с чистого состояния.
            _handler.Reset();
        }

        return outcome.Suppress;
    }

    /// <summary>Переключает флаг CapsLock на каждое новое нажатие клавиши CapsLock (автоповтор не считается).</summary>
    private void TrackCapsLock(VirtualKey vk, bool isUp)
    {
        if (vk != VirtualKey.CapsLock)
        {
            return;
        }

        if (isUp)
        {
            _capsDown = false;
        }
        else if (!_capsDown)
        {
            _capsDown = true;
            _capsLock = !_capsLock;
        }
    }

    /// <summary>Передаёт обработчику клики и колесо мыши.</summary>
    private void HandleMouse(uint message, in MsLlHookStruct ms)
    {
        _lastHookTick = Now();
        if ((ms.Flags & Win32.LlmhfInjected) != 0 || !IsDownOrWheel(message))
        {
            return;
        }

        Refresh();
        _handler.OnMouseDown(ms.Time);
        PumpDue(ms.Time);
        ArmDoubleTimer();

        // Клик по значку языка на панели задач тоже меняет раскладку.
        RequestLayoutCheck(ms.Time);
    }

    /// <summary>
    /// Подбирает набор привязок под окно переднего плана и паузу. При смене окна или набора сбрасывает обработчик:
    /// нажатые клавиши и буфер набранного относились к прежнему окну.
    /// </summary>
    private void Refresh()
    {
        var windowChanged = _foreground.Update();
        var tables = _tables;
        var wanted = _paused ? tables.Paused : tables.For(_foreground.ProcessName);
        if (!ReferenceEquals(wanted, _appliedTable))
        {
            _appliedTable = wanted;
            _handler.SetBindings(wanted);
        }
        else if (windowChanged)
        {
            _handler.Reset();
        }
    }

    /// <summary>Отдаёт потоку функций правила, чьё одиночное нажатие дождалось конца интервала двойного.</summary>
    private void PumpDue(uint now)
    {
        while (_handler.TryTakeDue(now, out var due))
        {
            _output.TryWrite(new HookMessage(HookMessageKind.RuleFired, due.Rule, false, VirtualKey.None));
        }
    }

    /// <summary>Просит поток функций сверить раскладку; не чаще одного раза за <see cref="LayoutCheckIntervalMs"/>.</summary>
    private void RequestLayoutCheck(uint now)
    {
        if (unchecked((int)(now - _lastLayoutCheck)) < LayoutCheckIntervalMs)
        {
            return;
        }

        _lastLayoutCheck = now;
        _output.TryWrite(new HookMessage(HookMessageKind.LayoutCheck));
    }

    /// <summary>Заводит одноразовый таймер на срок созревания ожидающего одиночного нажатия, если срок изменился.</summary>
    private void ArmDoubleTimer()
    {
        var deadline = _handler.NextDeadline;
        if (deadline == _armedDeadline)
        {
            return;
        }

        if (_doubleTimer != 0)
        {
            User32.KillTimer(0, _doubleTimer);
            _doubleTimer = 0;
        }

        _armedDeadline = deadline;
        if (deadline is { } due)
        {
            var delay = unchecked((int)(due - Now()));
            _doubleTimer = User32.SetTimer(0, 0, (uint)Math.Clamp(delay, 1, 10_000), 0);
        }
    }
}
