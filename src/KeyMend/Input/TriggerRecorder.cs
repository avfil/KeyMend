using System.Numerics;

namespace KeyMend.Input;

/// <summary>
/// Автомат записи триггера (SPEC 3.1, п. 5). Работает на потоке хука, пока в настройках идёт запись: вместо
/// <see cref="HotkeyEngine"/> принимает все события клавиатуры, подавляет их и собирает жест. Без аллокаций после создания.
/// Нажатые клавиши и жест нигде не записываются.
/// </summary>
internal sealed class TriggerRecorder
{
    /// <summary>
    /// На сколько мс раньше последней нажатой клавиши жеста должен быть нажат модификатор, чтобы считаться удерживаемым
    /// («держать RCtrl, нажать RShift»); иначе модификаторы нажаты «вместе».
    /// </summary>
    internal const int HoldGapMs = 100;

    /// <summary>Сколько мс после конца записи подавляем отпускание клавиш жеста; потом считаем, что отпускание потеряно.</summary>
    internal const int DrainTimeoutMs = 3000;

    /// <summary>Число модификаторов со стороной.</summary>
    internal const int SidedCount = 8;

    /// <summary>Фаза автомата.</summary>
    private enum Phase
    {
        /// <summary>Запись не идёт.</summary>
        Idle,

        /// <summary>Идёт запись: ждём клавиши.</summary>
        Recording,

        /// <summary>Жест записан; подавляем отпускание его клавиш, чтобы оно не дошло до окон.</summary>
        Draining,
    }

    /// <summary>Клавиши, нажатие которых мы видели во время записи и которые ещё не отпущены.</summary>
    private readonly bool[] _seen = new bool[byte.MaxValue + 1];

    /// <summary>Время нажатия каждого модификатора со стороной (индекс — номер бита).</summary>
    private readonly uint[] _pressTime = new uint[SidedCount];

    /// <summary>Время отпускания каждого модификатора со стороной.</summary>
    private readonly uint[] _releaseTime = new uint[SidedCount];

    /// <summary>Текущая фаза.</summary>
    private Phase _phase;

    /// <summary>Сколько клавиш из <see cref="_seen"/> ещё зажаты.</summary>
    private int _seenCount;

    /// <summary>Модификаторы, зажатые сейчас.</summary>
    private ModifierSet _downMods;

    /// <summary>Все модификаторы, нажатые за запись.</summary>
    private ModifierSet _gestureMods;

    /// <summary>Время конца записи для тайм-аута <see cref="Phase.Draining"/>.</summary>
    private uint _drainStart;

    /// <summary>Время конца записи ещё не известно (запись отменили извне, событий не было).</summary>
    private bool _drainStartPending;

    /// <summary>Идёт ли запись или подавление отпусканий после неё: события клавиатуры надо отдавать автомату.</summary>
    public bool IsActive => _phase != Phase.Idle;

    /// <summary>Начинает запись.</summary>
    public void Start()
    {
        Array.Clear(_seen);
        _seenCount = 0;
        _downMods = ModifierSet.None;
        _gestureMods = ModifierSet.None;
        _phase = Phase.Recording;
    }

    /// <summary>Прерывает запись извне (кнопка «Отмена», потеря окна). Если клавиши зажаты, их отпускание ещё подавляется.</summary>
    public void Abort()
    {
        if (_phase == Phase.Recording)
        {
            BeginDrain(0, pending: true);
        }
    }

    /// <summary>Принимает событие клавиатуры.</summary>
    /// <param name="e">Событие; чужой и ложный ввод хук уже отсеял.</param>
    /// <returns>Подавить ли событие и, если запись закончилась, её результат.</returns>
    public RecorderOutcome OnKey(in KeyEvent e)
    {
        var vk = (int)e.Vk & byte.MaxValue;
        return _phase switch
        {
            Phase.Recording => e.IsUp ? OnUp(e, vk) : OnDown(e, vk),
            Phase.Draining => OnDraining(e, vk),
            _ => default,
        };
    }

    /// <summary>Нажатие во время записи: модификатор копится, обычная клавиша завершает жест.</summary>
    private RecorderOutcome OnDown(in KeyEvent e, int vk)
    {
        if (_seen[vk])
        {
            // Автоповтор.
            return new RecorderOutcome(true);
        }

        _seen[vk] = true;
        _seenCount++;
        var bit = ModifierSets.FromKey(e.Vk);
        if (bit != ModifierSet.None)
        {
            _downMods |= bit;
            _gestureMods |= bit;
            _pressTime[BitOperations.TrailingZeroCount((int)bit)] = e.Time;
            return new RecorderOutcome(true);
        }

        // Esc сам по себе отменяет запись; с модификатором это обычное сочетание.
        var gesture = e.Vk == VirtualKey.Escape && _downMods == ModifierSet.None
            ? RecordedGesture.Cancel
            : new RecordedGesture(ModifierSet.None, _downMods, e.Vk);
        BeginDrain(e.Time, pending: false);
        return new RecorderOutcome(true, gesture);
    }

    /// <summary>Отпускание во время записи: когда отпущен последний модификатор, жест из одних модификаторов готов.</summary>
    private RecorderOutcome OnUp(in KeyEvent e, int vk)
    {
        if (!_seen[vk])
        {
            // Клавиша была зажата ещё до записи: пусть дойдёт до окна как обычно.
            return default;
        }

        _seen[vk] = false;
        _seenCount--;
        var bit = ModifierSets.FromKey(e.Vk);
        if (bit == ModifierSet.None)
        {
            return new RecorderOutcome(true);
        }

        _downMods &= ~bit;
        _releaseTime[BitOperations.TrailingZeroCount((int)bit)] = e.Time;
        if (_downMods != ModifierSet.None)
        {
            return new RecorderOutcome(true);
        }

        var gesture = BuildTap();
        BeginDrain(e.Time, pending: false);
        return new RecorderOutcome(true, gesture);
    }

    /// <summary>После записи подавляет отпускание клавиш жеста; всё остальное тоже подавляет, пока они зажаты.</summary>
    private RecorderOutcome OnDraining(in KeyEvent e, int vk)
    {
        if (_drainStartPending)
        {
            _drainStart = e.Time;
            _drainStartPending = false;
        }

        if (unchecked((int)(e.Time - _drainStart)) > DrainTimeoutMs)
        {
            _phase = Phase.Idle;
            return default;
        }

        if (e.IsUp)
        {
            if (!_seen[vk])
            {
                return default;
            }

            _seen[vk] = false;
            Settle();
            return new RecorderOutcome(true);
        }

        // Новое нажатие до того, как отпущен прошлый жест: тоже глотаем и ждём его отпускания.
        if (!_seen[vk])
        {
            _seen[vk] = true;
            _seenCount++;
        }

        return new RecorderOutcome(true);
    }

    /// <summary>Переходит к подавлению отпусканий, если есть зажатые клавиши, иначе завершает работу.</summary>
    private void BeginDrain(uint time, bool pending)
    {
        _drainStart = time;
        _drainStartPending = pending;
        _phase = _seenCount > 0 ? Phase.Draining : Phase.Idle;
    }

    /// <summary>Пересчитывает зажатые клавиши после отпускания; когда их не осталось, автомат свободен.</summary>
    private void Settle()
    {
        _seenCount--;
        if (_seenCount <= 0)
        {
            _seenCount = 0;
            _phase = Phase.Idle;
        }
    }

    /// <summary>Собирает tap: модификатор, нажатый заметно раньше другого и отпущенный не раньше него, — удерживаемый.</summary>
    private RecordedGesture BuildTap()
    {
        var keys = ModifierSet.None;
        var hold = ModifierSet.None;
        for (var i = 0; i < SidedCount; i++)
        {
            var bit = (ModifierSet)(1 << i);
            if ((_gestureMods & bit) == 0)
            {
                continue;
            }

            if (IsHeldDuringOther(i))
            {
                hold |= bit;
            }
            else
            {
                keys |= bit;
            }
        }

        return new RecordedGesture(keys, hold, VirtualKey.None);
    }

    /// <summary>Есть ли другой модификатор жеста, нажатый позже модификатора <paramref name="index"/> на <see cref="HoldGapMs"/> и отпущенный не позже него.</summary>
    private bool IsHeldDuringOther(int index)
    {
        for (var other = 0; other < SidedCount; other++)
        {
            if (other == index || ((int)_gestureMods & (1 << other)) == 0)
            {
                continue;
            }

            var laterBy = unchecked((int)(_pressTime[other] - _pressTime[index]));
            var releasedAfter = unchecked((int)(_releaseTime[index] - _releaseTime[other]));
            if (laterBy >= HoldGapMs && releasedAfter >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
