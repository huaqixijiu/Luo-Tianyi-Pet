namespace LuoTianyiPet.Core;

public enum StartupTimeScene
{
    Morning,
    Lunch,
    Afternoon,
    Night,
}

public sealed record StartupTimeSceneDecision(
    StartupTimeScene Scene,
    string AnimationId);

public static class StartupTimeSceneResolver
{
    public const string MorningAnimation = "startup-morning-float";
    public const string LunchAnimation = "startup-lunch-bounce";
    public const string AfternoonAnimation = "startup-afternoon-float";
    public const string NightAnimation = "startup-night-breathe";

    private static readonly TimeOnly MorningStart = new(6, 0);
    private static readonly TimeOnly LunchStart = new(12, 0);
    private static readonly TimeOnly AfternoonStart = new(13, 30);
    private static readonly TimeOnly NightStart = new(20, 0);

    public static StartupTimeSceneDecision Resolve(TimeOnly localTime)
    {
        if (localTime >= MorningStart && localTime < LunchStart)
        {
            return new(StartupTimeScene.Morning, MorningAnimation);
        }

        if (localTime >= LunchStart && localTime < AfternoonStart)
        {
            return new(StartupTimeScene.Lunch, LunchAnimation);
        }

        if (localTime >= AfternoonStart && localTime < NightStart)
        {
            return new(StartupTimeScene.Afternoon, AfternoonAnimation);
        }

        return new(StartupTimeScene.Night, NightAnimation);
    }
}

public sealed class TimeSceneTransitionTracker
{
    private StartupTimeScene? _observedScene;

    public StartupTimeSceneDecision Seed(TimeOnly localTime)
    {
        StartupTimeSceneDecision decision = StartupTimeSceneResolver.Resolve(localTime);
        _observedScene = decision.Scene;
        return decision;
    }

    public StartupTimeSceneDecision? Observe(TimeOnly localTime)
    {
        StartupTimeSceneDecision decision = StartupTimeSceneResolver.Resolve(localTime);
        if (_observedScene is null)
        {
            _observedScene = decision.Scene;
            return null;
        }

        if (_observedScene == decision.Scene)
        {
            return null;
        }

        _observedScene = decision.Scene;
        return decision;
    }
}

public enum SystemResumeReason
{
    SessionUnlocked,
    PowerResumed,
}

public enum SystemSuspendReason
{
    SessionLocked,
    PowerSuspended,
}

public sealed class SystemResumeEventArgs(
    SystemResumeReason reason,
    DateTimeOffset occurredAt) : EventArgs
{
    public SystemResumeReason Reason { get; } = reason;

    public DateTimeOffset OccurredAt { get; } = occurredAt;
}

public sealed class SystemSuspendEventArgs(
    SystemSuspendReason reason,
    DateTimeOffset occurredAt) : EventArgs
{
    public SystemSuspendReason Reason { get; } = reason;

    public DateTimeOffset OccurredAt { get; } = occurredAt;
}

public interface ISystemResumeSource : IDisposable
{
    event EventHandler<SystemResumeEventArgs>? Resumed;

    event EventHandler<SystemSuspendEventArgs>? Suspended;

    void Start();
}

public sealed class SystemResumeEventGate(TimeSpan? duplicateWindow = null)
{
    private readonly TimeSpan _duplicateWindow = ValidateWindow(
        duplicateWindow ?? TimeSpan.FromSeconds(5));
    private readonly object _sync = new();
    private DateTimeOffset? _lastAcceptedAt;

    public bool TryAccept(DateTimeOffset occurredAt)
    {
        lock (_sync)
        {
            if (_lastAcceptedAt is DateTimeOffset lastAcceptedAt &&
                occurredAt >= lastAcceptedAt &&
                occurredAt - lastAcceptedAt < _duplicateWindow)
            {
                return false;
            }

            _lastAcceptedAt = occurredAt;
            return true;
        }
    }

    private static TimeSpan ValidateWindow(TimeSpan duplicateWindow) =>
        duplicateWindow >= TimeSpan.Zero
            ? duplicateWindow
            : throw new ArgumentOutOfRangeException(nameof(duplicateWindow));
}
