using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PetVisualStateTests
{
    [Fact]
    public void SleepingUsesTheConfiguredLongIdleAnimation()
    {
        PetVisualState state = new(ContinuousState: PetContinuousState.Sleeping);

        Assert.Equal(PetVisualState.SleepingAnimation, state.ResolveContinuousAnimation());
    }

    [Fact]
    public void MediumIdleUsesResonanceHeheAnimation()
    {
        PetVisualState state = new(ContinuousState: PetContinuousState.MediumIdle);

        Assert.Equal("resonance-hehe", PetVisualState.MediumIdleAnimation);
        Assert.Equal(PetVisualState.MediumIdleAnimation, state.ResolveContinuousAnimation());
    }

    [Fact]
    public void MusicTemporarilyOverridesButDoesNotChangeSelectedMode()
    {
        PetVisualState state = new(
            PetDisplayMode.FullBodyInteractive,
            PetContinuousState.MusicPlaying,
            PetVisualState.EnjoyMusicAnimation);

        Assert.Equal(PetVisualState.EnjoyMusicAnimation, state.ResolveContinuousAnimation());
        Assert.Equal(PetDisplayMode.FullBodyInteractive, state.SelectedDisplayMode);

        PetVisualState stopped = state with { ContinuousState = PetContinuousState.Idle };
        Assert.Equal(PetVisualState.FullBodyIdleAnimation, stopped.ResolveContinuousAnimation());
    }

    [Theory]
    [InlineData(PetVisualState.EnjoyMusicAnimation)]
    [InlineData(PetVisualState.MusicSwayAnimation)]
    public void MusicUsesTheAnimationSelectedForThisPlaybackSession(string animationId)
    {
        PetVisualState state = new(
            PetDisplayMode.Compact,
            PetContinuousState.MusicPlaying,
            animationId);

        Assert.Equal(animationId, state.ResolveContinuousAnimation());
    }

    [Theory]
    [InlineData(PetDisplayMode.Compact, PetVisualState.CompactIdleAnimation)]
    [InlineData(PetDisplayMode.FullBodyInteractive, PetVisualState.FullBodyIdleAnimation)]
    public void IdleUsesSelectedDisplayMode(PetDisplayMode mode, string expectedAnimation)
    {
        Assert.Equal(expectedAnimation, new PetVisualState(mode).ResolveContinuousAnimation());
    }

    [Fact]
    public void CompactModeUsesTheUserSuppliedChibiIdle()
    {
        Assert.Equal("user-chibi-compact-idle", PetVisualState.CompactIdleAnimation);
    }

    [Fact]
    public void FullBodyModeUsesTheUserSelectedAppearanceForIdleAndDrag()
    {
        PetVisualState idle = new(
            PetDisplayMode.FullBodyInteractive,
            FullBodyAnimationId: AppearanceOptionIds.ClassicCatEarsAnimation);

        Assert.Equal(AppearanceOptionIds.ClassicCatEarsAnimation, idle.ResolveContinuousAnimation());
        Assert.Equal(
            AppearanceOptionIds.ClassicCatEarsAnimation,
            (idle with { ContinuousState = PetContinuousState.Dragging }).ResolveContinuousAnimation());
    }
}
