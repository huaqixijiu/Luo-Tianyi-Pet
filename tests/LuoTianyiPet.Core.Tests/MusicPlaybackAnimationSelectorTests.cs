using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicPlaybackAnimationSelectorTests
{
    [Fact]
    public void CandidatePoolContainsOnlyTheTwoConfirmedMusicAnimations()
    {
        Assert.Equal(
            [PetVisualState.EnjoyMusicAnimation, PetVisualState.MusicSwayAnimation],
            MusicPlaybackAnimationSelector.Candidates);
    }

    [Theory]
    [InlineData(0, PetVisualState.EnjoyMusicAnimation)]
    [InlineData(1, PetVisualState.MusicSwayAnimation)]
    public void SelectionUsesTheInjectedRandomIndex(int index, string expected)
    {
        MusicPlaybackAnimationSelector selector = new(_ => index);

        Assert.Equal(expected, selector.Select());
    }

    [Fact]
    public void EachPlaybackSessionRequestsANewRandomSelection()
    {
        int nextIndex = 0;
        MusicPlaybackAnimationSelector selector = new(_ => nextIndex++);

        Assert.Equal(PetVisualState.EnjoyMusicAnimation, selector.Select());
        Assert.Equal(PetVisualState.MusicSwayAnimation, selector.Select());
    }
}
