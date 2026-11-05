namespace KeyMend.Native;

/// <summary>Константы WinAPI, нужные хуку, отправке ввода и окну подсказки. Значения сверены с документацией Microsoft Learn.</summary>
internal static class Win32
{
    /// <summary>Тип хука: низкоуровневая клавиатура (<c>WH_KEYBOARD_LL</c>).</summary>
    public const int WhKeyboardLl = 13;

    /// <summary>Тип хука: низкоуровневая мышь (<c>WH_MOUSE_LL</c>).</summary>
    public const int WhMouseLl = 14;

    /// <summary>Код хука: параметры содержат данные события (<c>HC_ACTION</c>).</summary>
    public const int HcAction = 0;

    /// <summary>Сообщение: клавиша нажата (<c>WM_KEYDOWN</c>).</summary>
    public const uint WmKeyDown = 0x0100;

    /// <summary>Сообщение: клавиша отпущена (<c>WM_KEYUP</c>).</summary>
    public const uint WmKeyUp = 0x0101;

    /// <summary>Сообщение: системная клавиша нажата, то есть с Alt или F10 (<c>WM_SYSKEYDOWN</c>).</summary>
    public const uint WmSysKeyDown = 0x0104;

    /// <summary>Сообщение: системная клавиша отпущена (<c>WM_SYSKEYUP</c>).</summary>
    public const uint WmSysKeyUp = 0x0105;

    /// <summary>Сообщение: завершить цикл сообщений (<c>WM_QUIT</c>).</summary>
    public const uint WmQuit = 0x0012;

    /// <summary>Сообщение: сработал таймер (<c>WM_TIMER</c>).</summary>
    public const uint WmTimer = 0x0113;

    /// <summary>Сообщение: запрос смены раскладки окну (<c>WM_INPUTLANGCHANGEREQUEST</c>).</summary>
    public const uint WmInputLangChangeRequest = 0x0050;

    /// <summary>Сообщение мыши: левая кнопка нажата.</summary>
    public const uint WmLButtonDown = 0x0201;

    /// <summary>Сообщение мыши: правая кнопка нажата.</summary>
    public const uint WmRButtonDown = 0x0204;

    /// <summary>Сообщение мыши: средняя кнопка нажата.</summary>
    public const uint WmMButtonDown = 0x0207;

    /// <summary>Сообщение мыши: дополнительная кнопка нажата.</summary>
    public const uint WmXButtonDown = 0x020B;

    /// <summary>Сообщение мыши: прокручено колесо.</summary>
    public const uint WmMouseWheel = 0x020A;

    /// <summary>Флаг события клавиатуры: расширенная клавиша (<c>LLKHF_EXTENDED</c>).</summary>
    public const uint LlkhfExtended = 0x01;

    /// <summary>Флаг события клавиатуры: ввод инжектирован из любого процесса (<c>LLKHF_INJECTED</c>).</summary>
    public const uint LlkhfInjected = 0x10;

    /// <summary>Флаг события клавиатуры: клавиша отпускается (<c>LLKHF_UP</c>).</summary>
    public const uint LlkhfUp = 0x80;

    /// <summary>Флаг события мыши: ввод инжектирован из любого процесса (<c>LLMHF_INJECTED</c>).</summary>
    public const uint LlmhfInjected = 0x01;

    /// <summary>Тип записи <see cref="NativeInput"/>: клавиатура (<c>INPUT_KEYBOARD</c>).</summary>
    public const uint InputKeyboard = 1;

    /// <summary>Флаг <c>SendInput</c>: расширенная клавиша.</summary>
    public const uint KeyEventFExtendedKey = 0x0001;

    /// <summary>Флаг <c>SendInput</c>: клавиша отпускается.</summary>
    public const uint KeyEventFKeyUp = 0x0002;

    /// <summary>Флаг <c>SendInput</c>: символ Unicode вместо клавиши.</summary>
    public const uint KeyEventFUnicode = 0x0004;

    /// <summary>Флаг <c>SendInput</c>: использовать скан-код вместо виртуального кода.</summary>
    public const uint KeyEventFScanCode = 0x0008;

    /// <summary>Тип перевода <c>MapVirtualKeyEx</c>: виртуальный код в скан-код (<c>MAPVK_VK_TO_VSC</c>).</summary>
    public const uint MapVkToVsc = 0;

    /// <summary>Флаг <c>ToUnicodeEx</c>: не менять состояние клавиатуры потока, чтобы не ломать мёртвые клавиши (Windows 10 1607+).</summary>
    public const uint ToUnicodeKeepState = 0x4;

    /// <summary>Виртуальный код Shift без указания стороны (<c>VK_SHIFT</c>) для массива состояния <c>ToUnicodeEx</c>.</summary>
    public const int VkShift = 0x10;

    /// <summary>Виртуальный код CapsLock (<c>VK_CAPITAL</c>).</summary>
    public const int VkCapital = 0x14;

    /// <summary>Идентификатор объекта MSAA: текстовая каретка (<c>OBJID_CARET</c> = −8).</summary>
    public const uint ObjIdCaret = 0xFFFFFFF8;

    /// <summary>Индекс <c>GetWindowLongPtr</c>: расширенный стиль окна (<c>GWL_EXSTYLE</c> = −20).</summary>
    public const int GwlExStyle = -20;

    /// <summary>Расширенный стиль: окно прозрачно для кликов (<c>WS_EX_TRANSPARENT</c>).</summary>
    public const nint WsExTransparent = 0x00000020;

    /// <summary>Расширенный стиль: окно-инструмент, не попадает в Alt+Tab (<c>WS_EX_TOOLWINDOW</c>).</summary>
    public const nint WsExToolWindow = 0x00000080;

    /// <summary>Расширенный стиль: слоистое окно (<c>WS_EX_LAYERED</c>).</summary>
    public const nint WsExLayered = 0x00080000;

    /// <summary>Расширенный стиль: окно не получает фокус (<c>WS_EX_NOACTIVATE</c>).</summary>
    public const nint WsExNoActivate = 0x08000000;

    /// <summary>Дескриптор «поверх всех окон» для <c>SetWindowPos</c> (<c>HWND_TOPMOST</c> = −1).</summary>
    public static readonly nint HwndTopmost = -1;

    /// <summary>Флаг <c>SetWindowPos</c>: не менять размер.</summary>
    public const uint SwpNoSize = 0x0001;

    /// <summary>Флаг <c>SetWindowPos</c>: не перемещать окно.</summary>
    public const uint SwpNoMove = 0x0002;

    /// <summary>Флаг <c>SetWindowPos</c>: не менять порядок окон.</summary>
    public const uint SwpNoZOrder = 0x0004;

    /// <summary>Флаг <c>SetWindowPos</c>: перерисовать рамку окна (<c>SWP_FRAMECHANGED</c>).</summary>
    public const uint SwpFrameChanged = 0x0020;

    /// <summary>Флаг <c>SetWindowPos</c>: не активировать окно.</summary>
    public const uint SwpNoActivate = 0x0010;

    /// <summary>Флаг <c>SetWindowPos</c>: показать окно.</summary>
    public const uint SwpShowWindow = 0x0040;

    /// <summary>Право доступа к процессу: ограниченный запрос сведений (<c>PROCESS_QUERY_LIMITED_INFORMATION</c>).</summary>
    public const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>Флаги снимка Toolhelp: процессы и потоки (<c>TH32CS_SNAPPROCESS | TH32CS_SNAPTHREAD</c>).</summary>
    public const uint Th32CsSnapProcessAndThread = 0x00000002 | 0x00000004;

    /// <summary>Значение, которое <c>CreateToolhelp32Snapshot</c> возвращает при ошибке (<c>INVALID_HANDLE_VALUE</c>).</summary>
    public static readonly nint InvalidHandleValue = -1;

    /// <summary>Начало сообщений приложения (<c>WM_APP</c>); сюда попадает <c>WM_USER</c>-подобное сообщение значка трея.</summary>
    public const uint WmApp = 0x8000;

    /// <summary>Сообщение: изменилась системная настройка (<c>WM_SETTINGCHANGE</c>); для темы приложений <c>lParam</c> — строка <c>ImmersiveColorSet</c>.</summary>
    public const uint WmSettingChange = 0x001A;

    /// <summary>Сообщение: рамка окна должна перерисоваться как активная или неактивная (<c>WM_NCACTIVATE</c>).</summary>
    public const uint WmNcActivate = 0x0086;

    /// <summary>Сообщение мыши: левая кнопка отпущена.</summary>
    public const uint WmLButtonUp = 0x0202;

    /// <summary>Сообщение мыши: двойной клик левой кнопкой.</summary>
    public const uint WmLButtonDblClk = 0x0203;

    /// <summary>Сообщение мыши: правая кнопка отпущена.</summary>
    public const uint WmRButtonUp = 0x0205;

    /// <summary>Сообщение: запрос контекстного меню (<c>WM_CONTEXTMENU</c>); со значком версии 4 приходит вместо <see cref="WmRButtonUp"/>.</summary>
    public const uint WmContextMenu = 0x007B;

    /// <summary>Уведомление значка версии 4: значок выбран мышью или клавишей (<c>NIN_SELECT</c> = <c>WM_USER</c>).</summary>
    public const uint NinSelect = 0x0400;

    /// <summary>Уведомление значка версии 4: значок выбран с клавиатуры (<c>NIN_KEYSELECT</c>).</summary>
    public const uint NinKeySelect = 0x0401;

    /// <summary>Команда <c>Shell_NotifyIcon</c>: добавить значок (<c>NIM_ADD</c>).</summary>
    public const uint NimAdd = 0;

    /// <summary>Команда <c>Shell_NotifyIcon</c>: изменить значок (<c>NIM_MODIFY</c>).</summary>
    public const uint NimModify = 1;

    /// <summary>Команда <c>Shell_NotifyIcon</c>: удалить значок (<c>NIM_DELETE</c>).</summary>
    public const uint NimDelete = 2;

    /// <summary>Команда <c>Shell_NotifyIcon</c>: задать версию поведения (<c>NIM_SETVERSION</c>).</summary>
    public const uint NimSetVersion = 4;

    /// <summary>Версия поведения значка с <c>WM_CONTEXTMENU</c> и <c>NIN_SELECT</c> (<c>NOTIFYICON_VERSION_4</c>).</summary>
    public const uint NotifyIconVersion4 = 4;

    /// <summary>Флаг <c>NOTIFYICONDATA</c>: поле сообщения заполнено (<c>NIF_MESSAGE</c>).</summary>
    public const uint NifMessage = 0x01;

    /// <summary>Флаг <c>NOTIFYICONDATA</c>: поле значка заполнено (<c>NIF_ICON</c>).</summary>
    public const uint NifIcon = 0x02;

    /// <summary>Флаг <c>NOTIFYICONDATA</c>: поле подсказки заполнено (<c>NIF_TIP</c>).</summary>
    public const uint NifTip = 0x04;

    /// <summary>Флаг <c>NOTIFYICONDATA</c>: показывать стандартную подсказку и при поведении версии 4 (<c>NIF_SHOWTIP</c>).</summary>
    public const uint NifShowTip = 0x80;

    /// <summary>Флаг <c>NOTIFYICONDATA</c>: поля уведомления заполнены (<c>NIF_INFO</c>).</summary>
    public const uint NifInfo = 0x10;

    /// <summary>Значок уведомления: информация (<c>NIIF_INFO</c>).</summary>
    public const uint NiifInfo = 0x01;

    /// <summary>Значок уведомления: предупреждение (<c>NIIF_WARNING</c>).</summary>
    public const uint NiifWarning = 0x02;

    /// <summary>Значок уведомления: ошибка (<c>NIIF_ERROR</c>).</summary>
    public const uint NiifError = 0x03;

    /// <summary>Флаг <c>MonitorFromPoint</c>: если точка вне мониторов — взять ближайший (<c>MONITOR_DEFAULTTONEAREST</c>).</summary>
    public const uint MonitorDefaultToNearest = 2;

    /// <summary>Тип <c>LoadImage</c>: значок (<c>IMAGE_ICON</c>).</summary>
    public const uint ImageIcon = 1;

    /// <summary>Флаг <c>LoadImage</c>: общий ресурс, уничтожать не нужно (<c>LR_SHARED</c>).</summary>
    public const uint LrShared = 0x8000;

    /// <summary>Идентификатор стандартного значка приложения (<c>IDI_APPLICATION</c>).</summary>
    public const nint IdiApplication = 32512;

    /// <summary>Версия формата значка или курсора для <c>CreateIconFromResourceEx</c> (<c>0x00030000</c>).</summary>
    public const uint IconResourceVersion = 0x00030000;

    /// <summary>Цвет по умолчанию для <c>CreateIconFromResourceEx</c> (<c>LR_DEFAULTCOLOR</c>).</summary>
    public const uint LrDefaultColor = 0x0000;

    /// <summary>Метрика: ширина малого значка (<c>SM_CXSMICON</c>).</summary>
    public const int SmCxSmIcon = 49;

    /// <summary>Метрика: высота малого значка (<c>SM_CYSMICON</c>).</summary>
    public const int SmCySmIcon = 50;

    /// <summary>Режим показа окна: обычный (<c>SW_SHOWNORMAL</c>).</summary>
    public const int SwShowNormal = 1;

    /// <summary>Флаг <c>GlobalAlloc</c>: перемещаемая память (<c>GMEM_MOVEABLE</c>), обязательная для буфера обмена.</summary>
    public const uint GmemMoveable = 0x0002;

    /// <summary>Формат буфера обмена: текст ANSI (<c>CF_TEXT</c>).</summary>
    public const uint CfText = 1;

    /// <summary>Формат буфера обмена: растровое изображение GDI (<c>CF_BITMAP</c>).</summary>
    public const uint CfBitmap = 2;

    /// <summary>Формат буфера обмена: метафайл GDI (<c>CF_METAFILEPICT</c>).</summary>
    public const uint CfMetafilePict = 3;

    /// <summary>Формат буфера обмена: текст OEM (<c>CF_OEMTEXT</c>).</summary>
    public const uint CfOemText = 7;

    /// <summary>Формат буфера обмена: палитра GDI (<c>CF_PALETTE</c>).</summary>
    public const uint CfPalette = 9;

    /// <summary>Формат буфера обмена: текст Unicode (<c>CF_UNICODETEXT</c>).</summary>
    public const uint CfUnicodeText = 13;

    /// <summary>Формат буфера обмена: расширенный метафайл GDI (<c>CF_ENHMETAFILE</c>).</summary>
    public const uint CfEnhMetafile = 14;

    /// <summary>Формат буфера обмена: рисует владелец (<c>CF_OWNERDISPLAY</c>).</summary>
    public const uint CfOwnerDisplay = 0x0080;

    /// <summary>Начало диапазона форматов-«отображений» <c>CF_DSP*</c>.</summary>
    public const uint CfDspFirst = 0x0081;

    /// <summary>Конец диапазона форматов-«отображений» <c>CF_DSP*</c> (<c>CF_DSPENHMETAFILE</c>).</summary>
    public const uint CfDspLast = 0x008E;

    /// <summary>Начало приватных форматов и форматов с объектами GDI (<c>CF_PRIVATEFIRST</c>).</summary>
    public const uint CfPrivateFirst = 0x0200;

    /// <summary>Конец приватных форматов и форматов с объектами GDI (<c>CF_GDIOBJLAST</c>).</summary>
    public const uint CfGdiObjLast = 0x03FF;

    /// <summary>Право токена: копирование (<c>TOKEN_DUPLICATE</c>).</summary>
    public const uint TokenDuplicate = 0x0002;

    /// <summary>
    /// Права токена для запуска процесса: <c>TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT |
    /// TOKEN_ADJUST_SESSIONID</c>. Последние два нужны службе вторичного входа: без них <c>CreateProcessWithTokenW</c>
    /// из процесса администратора отвечает «доступ запрещён» (код 5).
    /// </summary>
    public const uint TokenForNewProcess = 0x0001 | 0x0002 | 0x0008 | 0x0080 | 0x0100;

    /// <summary>Уровень олицетворения токена (<c>SecurityImpersonation</c>).</summary>
    public const int SecurityImpersonation = 2;

    /// <summary>Тип токена: основной (<c>TokenPrimary</c>).</summary>
    public const int TokenPrimary = 1;

    /// <summary>Флаг <c>STARTUPINFO</c>: поле <c>wShowWindow</c> заполнено (<c>STARTF_USESHOWWINDOW</c>).</summary>
    public const uint StartfUseShowWindow = 0x0001;

    /// <summary>Показ окна: скрыто (<c>SW_HIDE</c>).</summary>
    public const int SwHide = 0;

    /// <summary>Показ окна: свёрнуто без активации (<c>SW_SHOWMINNOACTIVE</c>).</summary>
    public const int SwShowMinNoActive = 7;

    /// <summary>Код завершения, который <c>GetExitCodeProcess</c> возвращает для работающего процесса (<c>STILL_ACTIVE</c>).</summary>
    public const int StillActive = 259;
}
