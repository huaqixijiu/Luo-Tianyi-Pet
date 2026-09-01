using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class TimeSceneTests
{
    [Theory]
    [InlineData(5, 59, StartupTimeScene.Night, StartupTimeSceneResolver.NightAnimation)]
    [InlineData(6, 0, StartupTimeScene.Morning, StartupTimeSceneResolver.MorningAnimation)]
    [InlineData(11, 59, StartupTimeScene.Morning, StartupTimeSceneResolver.MorningAnimation)]
    [InlineData(12, 0, StartupTimeScene.Lunch, StartupTimeSceneResolver.LunchAnimation)]
    [InlineData(13, 29, StartupTimeScene.Lunch, StartupTimeSceneResolver.LunchAnimation)]
    [InlineData(13, 30, StartupTimeScene.Afternoon, StartupTimeSceneResolver.AfternoonAnimation)]
    [InlineData(19, 59, StartupTimeScene.Afternoon, StartupTimeSceneResolver.AfternoonAnimation)]
    [InlineData(20, 0, StartupTimeScene.Night, StartupTimeSceneResolver.NightAnimation)]
    [InlineData(23, 59, StartupTimeScene.Night, StartupTimeSceneResolver.NightAnimation)]
    public void Resolve_UsesConfirmedHalfOpenTimeRanges(
        int hour,
        int minute,
        StartupTimeScene expectedScene,
        string expectedAnimation)
    {
        StartupTimeSceneDecision decision = StartupTimeSceneResolver.Resolve(new TimeOnly(hour, minute));

        Assert.Equal(expectedScene, decision.Scene);
        Assert.Equal(expectedAnimation, decision.AnimationId);
    }

    [Fact]
    public void TransitionTracker_TriggersWhenRunningAppCrossesTimeBoundary()
    {
        TimeSceneTransitionTracker tracker = new();
        tracker.Seed(new TimeOnly(19, 59, 59));

        StartupTimeSceneDecision? decision = tracker.Observe(new TimeOnly(20, 0));

        Assert.NotNull(decision);
        Assert.Equal(StartupTimeScene.Night, decision.Scene);
        Assert.Equal(StartupTimeSceneResolver.NightAnimation, decision.AnimationId);
    }

    [Fact]
    public void TransitionTracker_DoesNotRepeatWithinSameTimeScene()
    {
        TimeSceneTransitionTracker tracker = new();
        tracker.Seed(new TimeOnly(6, 0));

        Assert.Null(tracker.Observe(new TimeOnly(6, 0, 1)));
        Assert.Null(tracker.Observe(new TimeOnly(11, 59, 59)));
    }

    [Fact]
    public void TransitionTracker_FirstObservationOnlyEstablishesBaseline()
    {
        TimeSceneTransitionTracker tracker = new();

        Assert.Null(tracker.Observe(new TimeOnly(13, 30)));
        Assert.Null(tracker.Observe(new TimeOnly(19, 59)));
    }

    [Fact]
    public void ResumeGate_SuppressesUnlockAndPowerResumeFromSameWakeCycle()
    {
        DateTimeOffset now = new(2026, 8, 30, 10, 0, 0, TimeSpan.FromHours(8));
        SystemResumeEventGate gate = new(TimeSpan.FromSeconds(5));

        Assert.True(gate.TryAccept(now));
        Assert.False(gate.TryAccept(now.AddSeconds(2)));
        Assert.True(gate.TryAccept(now.AddSeconds(5)));
    }

    [Fact]
    public void ResumeGate_AcceptsAfterClockMovesBackward()
    {
        DateTimeOffset now = new(2026, 8, 30, 10, 0, 0, TimeSpan.FromHours(8));
        SystemResumeEventGate gate = new();

        Assert.True(gate.TryAccept(now));
        Assert.True(gate.TryAccept(now.AddMinutes(-1)));
    }

    [Fact]
    public void ResumeGate_RejectsNegativeDuplicateWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SystemResumeEventGate(TimeSpan.FromMilliseconds(-1)));
    }
}
