namespace LuoTianyiPet.Core;

public interface IUserIdleTimeSource
{
    TimeSpan? GetIdleDuration();
}

public sealed class BirthdayEasterEggScheduler
{
    private readonly Func<int, int> _nextMinutes;
    private DateTimeOffset? _nextTrigger;

    public BirthdayEasterEggScheduler(Func<int, int>? nextMinutes = null)
    {
        _nextMinutes = nextMinutes ?? (maximum => Random.Shared.Next(45, maximum));
    }

    public static bool IsBirthday(DateTimeOffset now) =>
        (now.Month == 7 && now.Day == 12) ||
        (now.Month == 12 && now.Day == 12);

    public bool ShouldTrigger(DateTimeOffset now, bool idleEligible)
    {
        if (!IsBirthday(now))
        {
            _nextTrigger = null;
            return false;
        }

        _nextTrigger ??= now.AddMinutes(_nextMinutes(91));
        if (!idleEligible || now < _nextTrigger)
        {
            return false;
        }

        _nextTrigger = now.AddMinutes(_nextMinutes(91));
        return true;
    }
}
