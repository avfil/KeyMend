using System.Runtime.InteropServices;

namespace KeyMend.Native;

/// <summary>Точка в экранных координатах (<c>POINT</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    /// <summary>Координата X.</summary>
    public int X;

    /// <summary>Координата Y.</summary>
    public int Y;
}

/// <summary>Прямоугольник (<c>RECT</c>): левая/верхняя и правая/нижняя границы.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    /// <summary>Левая граница.</summary>
    public int Left;

    /// <summary>Верхняя граница.</summary>
    public int Top;

    /// <summary>Правая граница.</summary>
    public int Right;

    /// <summary>Нижняя граница.</summary>
    public int Bottom;
}

/// <summary>Данные события низкоуровневого хука клавиатуры (<c>KBDLLHOOKSTRUCT</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KbdLlHookStruct
{
    /// <summary>Виртуальный код клавиши (1–254); модификаторы приходят с указанием стороны.</summary>
    public uint VkCode;

    /// <summary>Скан-код клавиши.</summary>
    public uint ScanCode;

    /// <summary>Флаги: расширенная клавиша, инжектирован, отпускание.</summary>
    public uint Flags;

    /// <summary>Время события в миллисекундах.</summary>
    public uint Time;

    /// <summary>Дополнительные данные; наш <c>SendInput</c> кладёт сюда метку (A6).</summary>
    public nuint DwExtraInfo;
}

/// <summary>Данные события низкоуровневого хука мыши (<c>MSLLHOOKSTRUCT</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MsLlHookStruct
{
    /// <summary>Положение курсора на экране.</summary>
    public Point Pt;

    /// <summary>Дополнительные данные (дельта колеса, номер X-кнопки).</summary>
    public uint MouseData;

    /// <summary>Флаги: инжектирован.</summary>
    public uint Flags;

    /// <summary>Время события в миллисекундах.</summary>
    public uint Time;

    /// <summary>Дополнительные данные события.</summary>
    public nuint DwExtraInfo;
}

/// <summary>Состояние GUI-потока (<c>GUITHREADINFO</c>): окно в фокусе и каретка.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GuiThreadInfo
{
    /// <summary>Размер структуры в байтах; заполняется перед вызовом.</summary>
    public uint CbSize;

    /// <summary>Состояние потока (мигание каретки, меню и т. п.).</summary>
    public uint Flags;

    /// <summary>Активное окно потока.</summary>
    public nint HwndActive;

    /// <summary>Окно с фокусом клавиатуры.</summary>
    public nint HwndFocus;

    /// <summary>Окно, захватившее мышь.</summary>
    public nint HwndCapture;

    /// <summary>Владелец активного меню.</summary>
    public nint HwndMenuOwner;

    /// <summary>Окно в цикле перемещения или изменения размера.</summary>
    public nint HwndMoveSize;

    /// <summary>Окно, в котором показана каретка.</summary>
    public nint HwndCaret;

    /// <summary>Прямоугольник каретки в клиентских координатах окна <see cref="HwndCaret"/>.</summary>
    public Rect RcCaret;
}

/// <summary>Параметры синтезированного события мыши (<c>MOUSEINPUT</c>); нужен, чтобы <see cref="NativeInput"/> имел верный размер.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    /// <summary>Смещение или координата X.</summary>
    public int Dx;

    /// <summary>Смещение или координата Y.</summary>
    public int Dy;

    /// <summary>Дополнительные данные (колесо, X-кнопка).</summary>
    public uint MouseData;

    /// <summary>Флаги события.</summary>
    public uint DwFlags;

    /// <summary>Время события или 0 — тогда система подставит своё.</summary>
    public uint Time;

    /// <summary>Дополнительные данные события.</summary>
    public nuint DwExtraInfo;
}

/// <summary>Параметры синтезированного события клавиатуры (<c>KEYBDINPUT</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KeybdInput
{
    /// <summary>Виртуальный код клавиши (0 при <c>KEYEVENTF_UNICODE</c>).</summary>
    public ushort WVk;

    /// <summary>Скан-код или символ Unicode при <c>KEYEVENTF_UNICODE</c>.</summary>
    public ushort WScan;

    /// <summary>Флаги <c>KEYEVENTF_*</c>.</summary>
    public uint DwFlags;

    /// <summary>Время события или 0.</summary>
    public uint Time;

    /// <summary>Метка нашего ввода (A6); хук по ней узнаёт собственные события.</summary>
    public nuint DwExtraInfo;
}

/// <summary>Объединение полей <see cref="NativeInput"/>; размер определяется самым большим вариантом (<see cref="MouseInput"/>).</summary>
[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    /// <summary>Вариант для мыши.</summary>
    [FieldOffset(0)]
    public MouseInput Mi;

    /// <summary>Вариант для клавиатуры.</summary>
    [FieldOffset(0)]
    public KeybdInput Ki;
}

/// <summary>Запись для <c>SendInput</c> (<c>INPUT</c>); на x64 занимает 40 байт.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeInput
{
    /// <summary>Тип события (<see cref="Win32.InputKeyboard"/>).</summary>
    public uint Type;

    /// <summary>Параметры события.</summary>
    public InputUnion U;
}

/// <summary>Время последнего ввода пользователя (<c>LASTINPUTINFO</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LastInputInfo
{
    /// <summary>Размер структуры в байтах; заполняется перед вызовом.</summary>
    public uint CbSize;

    /// <summary>Тик системного таймера, когда пришёл последний ввод.</summary>
    public uint DwTime;
}

/// <summary>Сообщение из очереди потока (<c>MSG</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Msg
{
    /// <summary>Окно-получатель (0 для потоковых сообщений и таймера без окна).</summary>
    public nint Hwnd;

    /// <summary>Код сообщения.</summary>
    public uint Message;

    /// <summary>Первый параметр.</summary>
    public nuint WParam;

    /// <summary>Второй параметр.</summary>
    public nint LParam;

    /// <summary>Время сообщения.</summary>
    public uint Time;

    /// <summary>Положение курсора на момент сообщения.</summary>
    public Point Pt;
}

/// <summary>Запись о процессе из снимка Toolhelp (<c>PROCESSENTRY32W</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ProcessEntry32W
{
    /// <summary>Длина имени exe в символах, включая завершающий ноль (<c>MAX_PATH</c>).</summary>
    public const int ExeFileLength = 260;

    /// <summary>Размер структуры в байтах; заполняется перед первым вызовом.</summary>
    public uint DwSize;

    /// <summary>Не используется.</summary>
    public uint CntUsage;

    /// <summary>Идентификатор процесса.</summary>
    public uint Th32ProcessID;

    /// <summary>Не используется.</summary>
    public nuint Th32DefaultHeapID;

    /// <summary>Не используется.</summary>
    public uint Th32ModuleID;

    /// <summary>Число потоков процесса.</summary>
    public uint CntThreads;

    /// <summary>Идентификатор родительского процесса.</summary>
    public uint Th32ParentProcessID;

    /// <summary>Базовый приоритет потоков.</summary>
    public int PcPriClassBase;

    /// <summary>Не используется.</summary>
    public uint DwFlags;

    /// <summary>Имя exe без пути, строка UTF-16 с завершающим нулём.</summary>
    public fixed char SzExeFile[ExeFileLength];
}

/// <summary>Сведения о мониторе (<c>MONITORINFO</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MonitorInfo
{
    /// <summary>Размер структуры в байтах; заполняется перед вызовом.</summary>
    public uint CbSize;

    /// <summary>Границы монитора в экранных координатах.</summary>
    public Rect RcMonitor;

    /// <summary>Рабочая область монитора (без панели задач).</summary>
    public Rect RcWork;

    /// <summary>Флаги; бит 1 — основной монитор.</summary>
    public uint DwFlags;
}

/// <summary>Данные значка в области уведомлений (<c>NOTIFYICONDATAW</c>); на x64 занимает 976 байт.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NotifyIconData
{
    /// <summary>Длина подсказки значка в символах, включая завершающий ноль.</summary>
    public const int TipLength = 128;

    /// <summary>Длина текста уведомления в символах, включая завершающий ноль.</summary>
    public const int InfoLength = 256;

    /// <summary>Длина заголовка уведомления в символах, включая завершающий ноль.</summary>
    public const int InfoTitleLength = 64;

    /// <summary>Размер структуры в байтах; заполняется перед вызовом.</summary>
    public uint CbSize;

    /// <summary>Окно, получающее сообщения значка.</summary>
    public nint HWnd;

    /// <summary>Идентификатор значка в рамках окна.</summary>
    public uint UId;

    /// <summary>Какие поля заполнены (<c>NIF_*</c>).</summary>
    public uint UFlags;

    /// <summary>Сообщение окну при событиях мыши и клавиатуры над значком.</summary>
    public uint UCallbackMessage;

    /// <summary>Дескриптор значка.</summary>
    public nint HIcon;

    /// <summary>Подсказка значка, строка UTF-16 с завершающим нулём.</summary>
    public fixed char SzTip[TipLength];

    /// <summary>Состояние значка.</summary>
    public uint DwState;

    /// <summary>Какие биты состояния менять.</summary>
    public uint DwStateMask;

    /// <summary>Текст уведомления, строка UTF-16 с завершающим нулём.</summary>
    public fixed char SzInfo[InfoLength];

    /// <summary>Версия поведения значка (при <c>NIM_SETVERSION</c>) или время показа уведомления.</summary>
    public uint UVersion;

    /// <summary>Заголовок уведомления, строка UTF-16 с завершающим нулём.</summary>
    public fixed char SzInfoTitle[InfoTitleLength];

    /// <summary>Флаги уведомления (<c>NIIF_*</c>).</summary>
    public uint DwInfoFlags;

    /// <summary>Идентификатор-GUID значка; не используется.</summary>
    public Guid GuidItem;

    /// <summary>Значок уведомления; не используется.</summary>
    public nint HBalloonIcon;
}

/// <summary>Запись о потоке из снимка Toolhelp (<c>THREADENTRY32</c>).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ThreadEntry32
{
    /// <summary>Размер структуры в байтах; заполняется перед первым вызовом.</summary>
    public uint DwSize;

    /// <summary>Не используется.</summary>
    public uint CntUsage;

    /// <summary>Идентификатор потока.</summary>
    public uint Th32ThreadID;

    /// <summary>Идентификатор процесса-владельца.</summary>
    public uint Th32OwnerProcessID;

    /// <summary>Базовый приоритет потока.</summary>
    public int TpBasePri;

    /// <summary>Не используется.</summary>
    public int TpDeltaPri;

    /// <summary>Не используется.</summary>
    public uint DwFlags;
}

/// <summary>Параметры запуска процесса (<c>STARTUPINFOW</c>); на x64 занимает 104 байта. Строковые поля не используются.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct StartupInfo
{
    /// <summary>Размер структуры в байтах; заполняется перед вызовом.</summary>
    public uint Cb;

    /// <summary>Не используется.</summary>
    public nint LpReserved;

    /// <summary>Рабочий стол; 0 — унаследовать.</summary>
    public nint LpDesktop;

    /// <summary>Заголовок консольного окна; 0 — по умолчанию.</summary>
    public nint LpTitle;

    /// <summary>Не используется.</summary>
    public uint DwX;

    /// <summary>Не используется.</summary>
    public uint DwY;

    /// <summary>Не используется.</summary>
    public uint DwXSize;

    /// <summary>Не используется.</summary>
    public uint DwYSize;

    /// <summary>Не используется.</summary>
    public uint DwXCountChars;

    /// <summary>Не используется.</summary>
    public uint DwYCountChars;

    /// <summary>Не используется.</summary>
    public uint DwFillAttribute;

    /// <summary>Какие поля заполнены (<c>STARTF_*</c>).</summary>
    public uint DwFlags;

    /// <summary>Как показать первое окно (<c>SW_*</c>), если задан <c>STARTF_USESHOWWINDOW</c>.</summary>
    public ushort WShowWindow;

    /// <summary>Не используется.</summary>
    public ushort CbReserved2;

    /// <summary>Не используется.</summary>
    public nint LpReserved2;

    /// <summary>Не используется.</summary>
    public nint HStdInput;

    /// <summary>Не используется.</summary>
    public nint HStdOutput;

    /// <summary>Не используется.</summary>
    public nint HStdError;
}

/// <summary>Сведения о созданном процессе (<c>PROCESS_INFORMATION</c>); дескрипторы закрывает вызывающий.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessInformation
{
    /// <summary>Дескриптор процесса.</summary>
    public nint HProcess;

    /// <summary>Дескриптор главного потока.</summary>
    public nint HThread;

    /// <summary>Идентификатор процесса.</summary>
    public uint DwProcessId;

    /// <summary>Идентификатор главного потока.</summary>
    public uint DwThreadId;
}
