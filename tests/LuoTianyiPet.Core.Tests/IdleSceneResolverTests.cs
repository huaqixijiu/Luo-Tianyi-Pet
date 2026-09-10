using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class IdleSceneResolverTests
{
    [Theory]
    [InlineData(0, 0, PetContinuousState.Idle)]
    [InlineData(1, 59, PetContinuousState.Idle)]
    [InlineData(2, 0, PetContinuousState.MediumIdleCountdown)]
    [InlineData(2, 59, PetContinuousState.MediumIdleCountdown)]
    [InlineData(3, 0, PetContinuousState.MediumIdle)]
    [InlineData(29, 59, PetContinuousState.MediumIdle)]
    [InlineData(30, 0, PetContinuousState.Sleeping)]
    public void ResolvesIdleThresholdBoundaries(
        int minutes,
        int seconds,
        PetContinuousState expected)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            new TimeSpan(0, 0, minutes, seconds),
            PetContinuousState.Idle);

        Assert.Equal(expected, decision.TargetState);
        Assert.False(decision.RestoredFromSleep);
    }

    [Theory]
    [InlineData(0, PetContinuousState.Idle)]
    [InlineData(2, PetContinuousState.MediumIdleCountdown)]
    [InlineData(8, PetContinuousState.MediumIdle)]
    public void LeavingSleepRequestsAVisualRestoreWithoutWakeAnimation(
        int idleMinutes,
        PetContinuousState expectedTarget)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(idleMinutes),
            PetContinuousState.Sleeping);

        Assert.Equal(expectedTarget, decision.TargetState);
        Assert.True(decision.RestoredFromSleep);
    }

    [Theory]
    [InlineData(2, PetContinuousState.Idle)]
    [InlineData(3, PetContinuousState.Idle)]
    [InlineData(29, PetContinuousState.Idle)]
    [InlineData(30, PetContinuousState.Sleeping)]
    public void MediumIdleCanBeDisabledWithoutDisablingLongSleep(
        int idleMinutes,
        PetContinuousState expectedTarget)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(idleMinutes),
            PetContinuousState.Idle,
            IdleSceneProfile.NoMediumIdle);

        Assert.Equal(expectedTarget, decision.TargetState);
    }

    [Fact]
    public void DisablingMediumIdleRestoresAnExistingMediumIdleState()
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(8),
            PetContinuousState.MediumIdle,
            IdleSceneProfile.NoMediumIdle);

        Assert.Equal(PetContinuousState.Idle, decision.TargetState);
    }

    [Theory]
    [InlineData(1, 0, PetContinuousState.Idle)]
    [InlineData(2, 0, PetContinuousState.Idle)]
    [InlineData(4, 59, PetContinuousState.Idle)]
    [InlineData(5, 0, PetContinuousState.MediumIdle)]
    [InlineData(29, 59, PetContinuousState.MediumIdle)]
    [InlineData(30, 0, PetContinuousState.Sleeping)]
    public void CrystalDressSkipsCountdownAndStartsHeheAtFiveMinutes(
        int minutes,
        int seconds,
        PetContinuousState expected)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            new TimeSpan(0, 0, minutes, seconds),
            PetContinuousState.Idle,
            IdleSceneProfile.CrystalDress);

        Assert.Equal(expected, decision.TargetState);
    }

    [Fact]
    public void CrystalYawnTriggersOnceAtScheduledPointAndResetsAfterInput()
    {
        CrystalYawnScheduler scheduler = new((minimum, maximum) =>
        {
            Assert.Equal(60, minimum);
            Assert.Equal(286, maximum);
            return 120;
        });

        Assert.False(scheduler.ShouldTrigger(TimeSpan.FromSeconds(119), eligible: true));
        Assert.True(scheduler.ShouldTrigger(TimeSpan.FromSeconds(120), eligible: true));
        Assert.False(scheduler.ShouldTrigger(TimeSpan.FromSeconds(200), eligible: true));
        Assert.False(scheduler.ShouldTrigger(TimeSpan.FromMinutes(5), eligible: true));
        Assert.False(scheduler.ShouldTrigger(TimeSpan.Zero, eligible: true));
        Assert.True(scheduler.ShouldTrigger(TimeSpan.FromSeconds(120), eligible: true));
    }

    [Fact]
    public void CrystalYawnDoesNotTriggerOutsideEligibleAppearance()
    {
        CrystalYawnScheduler scheduler = new((_, _) => 60);

        Assert.False(scheduler.ShouldTrigger(TimeSpan.FromMinutes(2), eligible: false));
    }

    [Theory]
    [InlineData(PetContinuousState.MusicPlaying)]
    [InlineData(PetContinuousState.Dragging)]
    [InlineData(PetContinuousState.HiddenForSafety)]
    public void NonIdleContinuousStatesAreNotChanged(PetContinuousState state)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(TimeSpan.FromHours(1), state);

        Assert.Equal(state, decision.TargetState);
        Assert.False(decision.RestoredFromSleep);
        Assert.False(decision.ChangesStateFrom(state));
    }

    [Fact]
    public void NegativeDurationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdleSceneResolver.Resolve(TimeSpan.FromMilliseconds(-1), PetContinuousState.Idle));
    }
}
