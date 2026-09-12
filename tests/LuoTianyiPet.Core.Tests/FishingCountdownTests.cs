using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class FishingCountdownTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 59.999)]
    [InlineData(180, 20)]
    public void ActiveCountdownUsesItsOwnClock(double idleSeconds, double elapsedSeconds)
    {
        Assert.Equal(PetContinuousState.MediumIdleCountdown, IdleSceneResolver.Resolve(
            TimeSpan.FromSeconds(idleSeconds), PetContinuousState.MediumIdleCountdown,
            countdownElapsed: TimeSpan.FromSeconds(elapsedSeconds)).TargetState);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(61)]
    public void MouseMovementDoesNotPreventCompletion(double elapsedSeconds)
    {
        Assert.Equal(PetContinuousState.MediumIdle, IdleSceneResolver.Resolve(
            TimeSpan.Zero, PetContinuousState.MediumIdleCountdown,
            countdownElapsed: TimeSpan.FromSeconds(elapsedSeconds)).TargetState);
        Assert.Equal(PetContinuousState.MediumIdle, IdleSceneResolver.Resolve(
            TimeSpan.Zero, PetContinuousState.MediumIdle).TargetState);
    }

    [Theory]
    [InlineData(IdleSceneProfile.CrystalDress)]
    [InlineData(IdleSceneProfile.NoMediumIdle)]
    public void ChangingAppearanceStillExitsCountdown(IdleSceneProfile profile)
    {
        Assert.Equal(PetContinuousState.Idle, IdleSceneResolver.Resolve(TimeSpan.Zero,
            PetContinuousState.MediumIdleCountdown, profile, TimeSpan.FromSeconds(20)).TargetState);
    }

    [Theory]
    [InlineData(PetContinuousState.MusicPlaying)]
    [InlineData(PetContinuousState.Dragging)]
    [InlineData(PetContinuousState.HiddenForSafety)]
    public void CountdownCannotOverridePriorityState(PetContinuousState state)
    {
        Assert.Equal(state, IdleSceneResolver.Resolve(TimeSpan.Zero, state,
            countdownElapsed: TimeSpan.FromSeconds(60)).TargetState);
    }

    [Fact]
    public void ClickRestoredIdleDoesNotReuseCountdownElapsed()
    {
        Assert.Equal(PetContinuousState.Idle, IdleSceneResolver.Resolve(TimeSpan.Zero,
            PetContinuousState.Idle, countdownElapsed: TimeSpan.FromSeconds(60)).TargetState);
    }

    [Fact]
    public void HeheStillEntersLongSleep()
    {
        Assert.Equal(PetContinuousState.Sleeping, IdleSceneResolver.Resolve(
            TimeSpan.FromMinutes(30), PetContinuousState.MediumIdle).TargetState);
    }
}
