using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicPlaybackAnimationSelectorTests
{
    [Fact]
    public void PoolContainsTheTwoCurrentLoopingAnimations()
    {
        Assert.Equal(
            [PetVisualState.EnjoyMusicAnimation, PetVisualState.MusicSwayAnimation],
            MusicAnimationOptions.FixedOptions.Select(option => option.AnimationId));
    }

    [Theory]
    [InlineData(0, PetVisualState.EnjoyMusicAnimation)]
    [InlineData(1, PetVisualState.MusicSwayAnimation)]
    public void RandomSelectionUsesTheInjectedIndex(int index, string expected)
    {
        MusicPlaybackAnimationSelector selector = new(_ => index);

        Assert.Equal(expected, selector.Select(MusicAnimationOptions.RandomSelection));
    }

    [Fact]
    public void EachPlaybackSessionRequestsANewRandomSelection()
    {
        int nextIndex = 0;
        MusicPlaybackAnimationSelector selector = new(_ => nextIndex++);

        Assert.Equal(PetVisualState.EnjoyMusicAnimation, selector.Select("random"));
        Assert.Equal(PetVisualState.MusicSwayAnimation, selector.Select("random"));
    }

    [Theory]
    [InlineData(PetVisualState.EnjoyMusicAnimation)]
    [InlineData(PetVisualState.MusicSwayAnimation)]
    public void FixedSelectionDoesNotUseRandom(string selectedAnimation)
    {
        MusicPlaybackAnimationSelector selector = new(_ =>
            throw new InvalidOperationException("Random selection should not be used."));

        Assert.Equal(selectedAnimation, selector.Select(selectedAnimation));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-animation")]
    public void InvalidSelectionSafelyFallsBackToRandom(string? selection)
    {
        MusicPlaybackAnimationSelector selector = new(_ => 1);

        Assert.Equal(PetVisualState.MusicSwayAnimation, selector.Select(selection));
    }

    [Theory]
    [InlineData("洛天依")]
    [InlineData("洛天依/乐正绫")]
    [InlineData("Luo Tianyi")]
    [InlineData("LUO-TIANYI & Yan He")]
    public void ArtistMatcherRecognizesLuoTianyiAndCollaborations(string artist)
    {
        Assert.True(MusicArtistMatcher.IsLuoTianyi(artist));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("洛天依然")]
    [InlineData("乐正绫")]
    public void ArtistMatcherRejectsOtherOrMissingArtists(string? artist)
    {
        Assert.False(MusicArtistMatcher.IsLuoTianyi(artist));
    }
}
