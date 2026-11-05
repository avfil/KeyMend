using KeyMend.Input;
using KeyMend.Text;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="WordBuffer"/>: добавление, Backspace, очистка и потолок ёмкости.</summary>
public sealed class WordBufferTests
{
    /// <summary>Клавиша-буква по номеру (A = 0).</summary>
    private static KeyStroke Letter(int index) => new((VirtualKey)(0x41 + index), false, false);

    /// <summary>Новый буфер пуст.</summary>
    [Fact]
    public void Count_NewBuffer_IsZero()
    {
        var buffer = new WordBuffer(10);
        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());
    }

    /// <summary>Клавиши лежат в порядке нажатия.</summary>
    [Fact]
    public void Push_Keys_KeepsOrder()
    {
        var buffer = new WordBuffer(10);
        buffer.Push(Letter(0));
        buffer.Push(Letter(1));
        buffer.Push(Letter(2));
        Assert.Equal(new[] { Letter(0), Letter(1), Letter(2) }, buffer.Snapshot());
    }

    /// <summary>Backspace убирает последнюю клавишу.</summary>
    [Fact]
    public void Backspace_TwoKeys_RemovesLastKey()
    {
        var buffer = new WordBuffer(10);
        buffer.Push(Letter(0));
        buffer.Push(Letter(1));
        buffer.Backspace();
        Assert.Equal(new[] { Letter(0) }, buffer.Snapshot());
    }

    /// <summary>Backspace в пустом буфере ничего не делает.</summary>
    [Fact]
    public void Backspace_EmptyBuffer_DoesNothing()
    {
        var buffer = new WordBuffer(10);
        buffer.Backspace();
        Assert.Equal(0, buffer.Count);
    }

    /// <summary>При переполнении старые клавиши отбрасываются.</summary>
    [Fact]
    public void Push_Overflow_DropsOldestKeys()
    {
        var buffer = new WordBuffer(3);
        for (var i = 0; i < 5; i++)
        {
            buffer.Push(Letter(i));
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal(new[] { Letter(2), Letter(3), Letter(4) }, buffer.Snapshot());
    }

    /// <summary>После переполнения Backspace убирает самую новую клавишу.</summary>
    [Fact]
    public void Backspace_AfterOverflow_RemovesNewestKey()
    {
        var buffer = new WordBuffer(3);
        for (var i = 0; i < 5; i++)
        {
            buffer.Push(Letter(i));
        }

        buffer.Backspace();
        buffer.Push(Letter(9));
        Assert.Equal(new[] { Letter(2), Letter(3), Letter(9) }, buffer.Snapshot());
    }

    /// <summary>Очистка опустошает буфер, и он снова годен к работе.</summary>
    [Fact]
    public void Clear_AfterOverflow_EmptiesBufferAndAllowsReuse()
    {
        var buffer = new WordBuffer(3);
        for (var i = 0; i < 5; i++)
        {
            buffer.Push(Letter(i));
        }

        buffer.Clear();
        Assert.Equal(0, buffer.Count);
        buffer.Push(Letter(7));
        Assert.Equal(new[] { Letter(7) }, buffer.Snapshot());
    }

    /// <summary>Снимок не меняется от последующих нажатий.</summary>
    [Fact]
    public void Snapshot_AfterPush_IsACopy()
    {
        var buffer = new WordBuffer(3);
        buffer.Push(Letter(0));
        var snapshot = buffer.Snapshot();
        buffer.Push(Letter(1));
        Assert.Single(snapshot);
    }

    /// <summary>Нулевая ёмкость недопустима.</summary>
    [Fact]
    public void Constructor_ZeroCapacity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new WordBuffer(0));
}
