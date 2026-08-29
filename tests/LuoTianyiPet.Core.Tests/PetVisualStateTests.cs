using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class PetVisualStateTests
{
    [Fact]
    public void MusicTemporarilyOverridesButDoesNotChangeSelectedMode()
    {
        PetVisualState state = new(PetDisplayMode.FullBodyInteractive, IsMusicPlaying: true);

        Assert.Equal(PetVisualState.MusicPlayingAnimation, state.ResolveContinuousAnimation());
        Assert.Equal(PetDisplayMode.FullBodyInteractive, state.SelectedDisplayMode);

        PetVisualState stopped = state with { IsMusicPlaying = false };
        Assert.Equal(PetVisualState.FullBodyIdleAnimation, stopped.ResolveContinuousAnimation());
    }

    [Theory]
    [InlineData(PetDisplayMode.Compact, PetVisualState.CompactIdleAnimation)]
    [InlineData(PetDisplayMode.FullBodyInteractive, PetVisualState.FullBodyIdleAnimation)]
    public void IdleUsesSelectedDisplayMode(PetDisplayMode mode, string expectedAnimation)
    {
        Assert.Equal(expectedAnimation, new PetVisualState(mode).ResolveContinuousAnimation());
    }
}
