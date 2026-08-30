using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class IdleSceneResolverTests
{
    [Theory]
    [InlineData(0, 0, PetContinuousState.Idle)]
    [InlineData(4, 59, PetContinuousState.Idle)]
    [InlineData(5, 0, PetContinuousState.MediumIdle)]
    [InlineData(14, 59, PetContinuousState.MediumIdle)]
    [InlineData(15, 0, PetContinuousState.Sleeping)]
    public void ResolvesIdleThresholdBoundaries(
        int minutes,
        int seconds,
        PetContinuousState expected)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            new TimeSpan(0, 0, minutes, seconds),
            PetContinuousState.Idle);

        Assert.Equal(expected, decision.TargetState);
        Assert.False(decision.PlayWakeReaction);
    }

    [Theory]
    [InlineData(0, PetContinuousState.Idle)]
    [InlineData(8, PetContinuousState.MediumIdle)]
    public void LeavingSleepRequestsOneWakeReaction(
        int idleMinutes,
        PetContinuousState expectedTarget)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(idleMinutes),
            PetContinuousState.Sleeping);

        Assert.Equal(expectedTarget, decision.TargetState);
        Assert.True(decision.PlayWakeReaction);
    }

    [Theory]
    [InlineData(PetContinuousState.MusicPlaying)]
    [InlineData(PetContinuousState.Dragging)]
    [InlineData(PetContinuousState.HiddenForSafety)]
    public void NonIdleContinuousStatesAreNotChanged(PetContinuousState state)
    {
        IdleSceneDecision decision = IdleSceneResolver.Resolve(TimeSpan.FromHours(1), state);

        Assert.Equal(state, decision.TargetState);
        Assert.False(decision.PlayWakeReaction);
        Assert.False(decision.ChangesStateFrom(state));
    }

    [Fact]
    public void NegativeDurationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IdleSceneResolver.Resolve(TimeSpan.FromMilliseconds(-1), PetContinuousState.Idle));
    }
}
