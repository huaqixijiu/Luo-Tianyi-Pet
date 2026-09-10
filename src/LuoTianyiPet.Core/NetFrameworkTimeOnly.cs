#if NETFRAMEWORK
namespace System;

public readonly struct TimeOnly : IComparable<TimeOnly>, IEquatable<TimeOnly>
{
    private readonly long _ticks;

    public TimeOnly(int hour, int minute)
        : this(hour, minute, 0)
    {
    }

    public TimeOnly(int hour, int minute, int second)
    {
        _ticks = new TimeSpan(hour, minute, second).Ticks;
    }

    public static TimeOnly FromDateTime(DateTime dateTime) =>
        new(dateTime.Hour, dateTime.Minute, dateTime.Second);

    public int CompareTo(TimeOnly other) => _ticks.CompareTo(other._ticks);

    public bool Equals(TimeOnly other) => _ticks == other._ticks;

    public override bool Equals(object? obj) => obj is TimeOnly other && Equals(other);

    public override int GetHashCode() => _ticks.GetHashCode();

    public static bool operator <(TimeOnly left, TimeOnly right) => left._ticks < right._ticks;

    public static bool operator <=(TimeOnly left, TimeOnly right) => left._ticks <= right._ticks;

    public static bool operator >(TimeOnly left, TimeOnly right) => left._ticks > right._ticks;

    public static bool operator >=(TimeOnly left, TimeOnly right) => left._ticks >= right._ticks;

    public static bool operator ==(TimeOnly left, TimeOnly right) => left.Equals(right);

    public static bool operator !=(TimeOnly left, TimeOnly right) => !left.Equals(right);
}
#endif
