using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>P/Invoke к <c>user32.dll</c> (A3). Сигнатуры сверены с документацией Microsoft Learn.</summary>
internal static unsafe partial class User32
{
    /// <summary>Устанавливает хук; колбэк передаётся указателем, чтобы не держать делегат (см. SPEC 5.5).</summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial nint SetWindowsHookEx(int idHook, delegate* unmanaged<int, nuint, nint, nint> lpfn, nint hMod, uint dwThreadId);

    /// <summary>Снимает хук.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);

    /// <summary>Передаёт событие следующему хуку в цепочке.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

    /// <summary>Берёт сообщение из очереди потока; возвращает 0 на <c>WM_QUIT</c> и −1 при ошибке.</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    public static partial int GetMessage(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    /// <summary>Отправляет сообщение в очередь потока (так цикл хука останавливается через <c>WM_QUIT</c>).</summary>
    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint idThread, uint msg, nuint wParam, nint lParam);

    /// <summary>Запускает таймер без окна: <c>WM_TIMER</c> приходит в очередь вызвавшего потока.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    /// <summary>Останавливает таймер.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hWnd, nuint uIDEvent);

    /// <summary>Возвращает окно переднего плана.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    /// <summary>Пишет имя оконного класса в буфер; возвращает число символов без завершающего нуля (0 — ошибка).</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    public static partial int GetClassName(nint hWnd, char* lpClassName, int nMaxCount);

    /// <summary>Возвращает идентификатор потока окна и пишет идентификатор его процесса.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    /// <summary>Заполняет сведения о GUI-потоке: фокус и каретка (<see cref="GuiThreadInfo.CbSize"/> задаётся заранее).</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo pgui);

    /// <summary>Возвращает раскладку (HKL) указанного потока.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetKeyboardLayout(uint idThread);

    /// <summary>Заполняет список установленных раскладок; при пустом буфере возвращает их количество.</summary>
    [LibraryImport("user32.dll")]
    public static partial int GetKeyboardLayoutList(int nBuff, Span<nint> lpList);

    /// <summary>Кладёт сообщение в очередь окна и не ждёт обработки.</summary>
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

    /// <summary>Синтезирует ввод; возвращает число принятых записей.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint SendInput(uint cInputs, ReadOnlySpan<NativeInput> pInputs, int cbSize);

    /// <summary>Заполняет время последнего ввода (<see cref="LastInputInfo.CbSize"/> задаётся заранее).</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetLastInputInfo(ref LastInputInfo plii);

    /// <summary>Возвращает асинхронное состояние клавиши; старший бит — «нажата».</summary>
    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int vKey);

    /// <summary>Возвращает состояние клавиши по данным очереди вызвавшего потока: старший бит — «нажата», младший — «включена» (CapsLock).</summary>
    [LibraryImport("user32.dll")]
    public static partial short GetKeyState(int nVirtKey);

    /// <summary>Переводит виртуальный код в скан-код или символ для заданной раскладки (<paramref name="uMapType"/>); 0 — перевода нет.</summary>
    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    public static partial uint MapVirtualKeyEx(uint uCode, uint uMapType, nint dwhkl);

    /// <summary>
    /// Переводит клавишу и состояние клавиатуры (массив из 256 байт) в символы раскладки <paramref name="dwhkl"/>.
    /// Результат больше нуля — число записанных символов UTF-16, меньше нуля — мёртвая клавиша, нуль — перевода нет.
    /// </summary>
    [LibraryImport("user32.dll")]
    public static partial int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte* lpKeyState, char* pwszBuff, int cchBuff, uint wFlags, nint dwhkl);

    /// <summary>Возвращает положение курсора мыши на экране.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Point lpPoint);

    /// <summary>Возвращает прямоугольник окна в экранных координатах.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    /// <summary>Переводит клиентские координаты окна в экранные.</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ClientToScreen(nint hWnd, ref Point lpPoint);

    /// <summary>Отправляет сообщение окну и ждёт обработки.</summary>
    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial nint SendMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

    /// <summary>Перемещает окно и меняет порядок отображения (используется подсказкой EN/RU).</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>Читает атрибут окна (на x64 — единственная форма для стилей).</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>Записывает атрибут окна.</summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>Делает окно передним; нужно перед показом меню трея, иначе оно не потеряет активность и не закроется кликом мимо.</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    /// <summary>Регистрирует сообщение с уникальным кодом для всей системы (например, <c>TaskbarCreated</c>); 0 — ошибка.</summary>
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial uint RegisterWindowMessage(string lpString);

    /// <summary>Находит монитор, ближайший к точке (экранные координаты).</summary>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(Point pt, uint dwFlags);

    /// <summary>Заполняет сведения о мониторе (<see cref="MonitorInfo.CbSize"/> задаётся заранее).</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint hMonitor, ref MonitorInfo lpmi);

    /// <summary>Загружает значок или курсор; для стандартных значков <paramref name="name"/> — числовой идентификатор ресурса.</summary>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
    public static partial nint LoadImage(nint hInst, nint name, uint type, int cx, int cy, uint fuLoad);

    /// <summary>Уничтожает значок, созданный без <see cref="Win32.LrShared"/>.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    /// <summary>Создаёт значок из одного изображения ICO, загруженного из ресурса (нужен выровненный буфер).</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint CreateIconFromResourceEx(byte* presbits, uint dwResSize, [MarshalAs(UnmanagedType.Bool)] bool fIcon, uint dwVer, int cxDesired, int cyDesired, uint flags);

    /// <summary>Возвращает DPI окна (96 — 100%).</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);

    /// <summary>Возвращает системную метрику для заданного DPI (например, размер малого значка).</summary>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    /// <summary>Открывает буфер обмена для окна-владельца; <c>false</c> — его держит другое окно.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint hWndNewOwner);

    /// <summary>Закрывает буфер обмена; вызывается после каждого успешного <see cref="OpenClipboard"/>.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    /// <summary>Очищает буфер обмена и делает окно, открывшее его, владельцем.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyClipboard();

    /// <summary>Перечисляет форматы буфера обмена: 0 — первый, результат 0 — форматы кончились.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint EnumClipboardFormats(uint format);

    /// <summary>Возвращает дескриптор данных формата; память принадлежит системе, копировать надо до <see cref="CloseClipboard"/>.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetClipboardData(uint uFormat);

    /// <summary>Кладёт данные в буфер обмена; при успехе память переходит системе, при ошибке (0) её освобождает вызывающий.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint SetClipboardData(uint uFormat, nint hMem);

    /// <summary>Номер изменения буфера обмена: растёт при каждой смене содержимого.</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetClipboardSequenceNumber();

    /// <summary>Регистрирует формат буфера обмена по имени и возвращает его код; 0 — ошибка.</summary>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterClipboardFormat(string lpszFormat);

    /// <summary>Возвращает окно рабочего стола оболочки (Проводника); 0 — оболочки нет.</summary>
    [LibraryImport("user32.dll")]
    public static partial nint GetShellWindow();

    /// <summary>Блокирует рабочую станцию, как Win+L.</summary>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LockWorkStation();
}
