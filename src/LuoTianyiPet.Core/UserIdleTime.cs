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

public static class IdleSceneResolver
{
    public static readonly TimeSpan MediumIdleThreshold = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SleepThreshold = TimeSpan.FromMinutes(15);

    public static IdleSceneDecision Resolve(
        TimeSpan idleDuration,
        PetContinuousState currentState)
    {
        if (idleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleDuration));
        }

        if (currentState is PetContinuousState.MusicPlaying or
            PetContinuousState.MusicPaused or
            PetContinuousState.Dragging or
            PetContinuousState.HiddenForSafety)
        {
            return new IdleSceneDecision(currentState, RestoredFromSleep: false);
        }

        PetContinuousState targetState = idleDuration switch
        {
            _ when idleDuration >= SleepThreshold => PetContinuousState.Sleeping,
            _ when idleDuration >= MediumIdleThreshold => PetContinuousState.MediumIdle,
            _ => PetContinuousState.Idle,
        };

        return new IdleSceneDecision(
            targetState,
            RestoredFromSleep: currentState == PetContinuousState.Sleeping &&
                targetState != PetContinuousState.Sleeping);
    }
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
