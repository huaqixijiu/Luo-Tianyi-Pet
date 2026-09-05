using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class IdleSceneResolverTests
{
    [Theory]
    [InlineData(0, 0, PetContinuousState.Idle)]
    [InlineData(2, 59, PetContinuousState.Idle)]
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
            mediumIdleEnabled: false);

        Assert.Equal(expectedTarget, decision.TargetState);
    }

    [Fact]
    public void DisablingMediumIdleRestoresAnExistingMediumIdleState()
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(8),
            PetContinuousState.MediumIdle,
            mediumIdleEnabled: false);

        Assert.Equal(PetContinuousState.Idle, decision.TargetState);
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
