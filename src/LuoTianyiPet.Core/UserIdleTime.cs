namespace LuoTianyiPet.Core;

public interface IUserIdleTimeSource
{
    TimeSpan? GetIdleDuration();
}

public sealed record IdleSceneDecision(
    PetContinuousState TargetState,
    bool RestoredFromSleep)
{
    public bool ChangesStateFrom(PetContinuousState currentState) => TargetState != currentState;
}

public enum IdleSceneProfile
{
    NoMediumIdle,
    CrystalDress,
    ClassicCatEars,
}

public static class IdleSceneResolver
{
    public static readonly TimeSpan MediumIdleCountdownThreshold = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MediumIdleThreshold = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan SleepThreshold = TimeSpan.FromMinutes(30);

    public static IdleSceneDecision Resolve(
        TimeSpan idleDuration,
        PetContinuousState currentState,
        IdleSceneProfile profile = IdleSceneProfile.ClassicCatEars)
    {
        if (idleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleDuration));
        }

        if (currentState is PetContinuousState.MusicPlaying or
            PetContinuousState.Dragging or
            PetContinuousState.HiddenForSafety)
        {
            return new IdleSceneDecision(currentState, RestoredFromSleep: false);
        }

        PetContinuousState targetState = idleDuration switch
        {
            _ when profile == IdleSceneProfile.ClassicCatEars &&
                idleDuration >= SleepThreshold => PetContinuousState.Sleeping,
            _ when profile == IdleSceneProfile.CrystalDress &&
                idleDuration >= SleepThreshold => PetContinuousState.Sleeping,
            _ when profile == IdleSceneProfile.ClassicCatEars &&
                idleDuration >= MediumIdleThreshold =>
                PetContinuousState.MediumIdle,
            _ when profile == IdleSceneProfile.ClassicCatEars &&
                idleDuration >= MediumIdleCountdownThreshold =>
                PetContinuousState.MediumIdleCountdown,
            _ => PetContinuousState.Idle,
        };

        return new IdleSceneDecision(
            targetState,
            RestoredFromSleep: currentState == PetContinuousState.Sleeping &&
                targetState != PetContinuousState.Sleeping);
    }
}

public enum CrystalLongIdleVariant
{
    Sleep,
    DuckSit,
}

public enum CrystalSleepDecoration
{
    Zzz,
    DreamBun,
    DreamYuezhengLing,
}

public sealed class CrystalLongIdleSelector
{
    private readonly Func<int, int, int> _next;

    public CrystalLongIdleSelector(Func<int, int, int>? next = null)
    {
        _next = next ?? SharedRandom.Next;
    }

    public CrystalLongIdleVariant ChooseVariant() =>
        _next(0, 2) == 0 ? CrystalLongIdleVariant.Sleep : CrystalLongIdleVariant.DuckSit;

    public CrystalSleepDecoration ChooseDecoration()
    {
        int value = _next(0, 100);
        return value switch
        {
            < 60 => CrystalSleepDecoration.Zzz,
            < 95 => CrystalSleepDecoration.DreamBun,
            _ => CrystalSleepDecoration.DreamYuezhengLing,
        };
    }
}

public sealed class CrystalYawnScheduler
{
    public static readonly TimeSpan WindowStart = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan WindowEnd = TimeSpan.FromMinutes(5);
    private const int LatestStartSecond = 285;

    private readonly Func<int, int, int> _nextSecond;
    private TimeSpan? _triggerAt;
    private TimeSpan _lastIdleDuration;
    private bool _triggered;

    public CrystalYawnScheduler(Func<int, int, int>? nextSecond = null)
    {
        _nextSecond = nextSecond ?? SharedRandom.Next;
    }

    public bool ShouldTrigger(TimeSpan idleDuration, bool eligible)
    {
        if (idleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleDuration));
        }

        if (idleDuration < _lastIdleDuration)
        {
            _triggerAt = null;
            _triggered = false;
        }

        _lastIdleDuration = idleDuration;
        _triggerAt ??= TimeSpan.FromSeconds(
            _nextSecond((int)WindowStart.TotalSeconds, LatestStartSecond + 1));

        if (!eligible || _triggered || idleDuration < _triggerAt || idleDuration >= WindowEnd)
        {
            return false;
        }

        _triggered = true;
        return true;
    }
}

public sealed class BirthdayEasterEggScheduler
{
    private readonly Func<int, int> _nextMinutes;
    private DateTimeOffset? _nextTrigger;

    public BirthdayEasterEggScheduler(Func<int, int>? nextMinutes = null)
    {
        _nextMinutes = nextMinutes ?? (maximum => SharedRandom.Next(45, maximum));
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
