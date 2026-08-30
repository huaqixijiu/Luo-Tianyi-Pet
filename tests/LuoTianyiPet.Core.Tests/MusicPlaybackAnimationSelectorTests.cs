using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicPlaybackAnimationSelectorTests
{
    [Fact]
    public void LuoTianyiPoolContainsOnlyTheTwoConfirmedPerformanceAnimations()
    {
        Assert.Equal(
            [PetVisualState.EnjoyMusicAnimation, PetVisualState.MusicSwayAnimation],
            MusicPlaybackAnimationSelector.LuoTianyiPerformanceCandidates);
    }

    [Theory]
    [InlineData(0, PetVisualState.EnjoyMusicAnimation)]
    [InlineData(1, PetVisualState.MusicSwayAnimation)]
    public void LuoTianyiSelectionUsesTheInjectedRandomIndex(int index, string expected)
    {
        MusicPlaybackAnimationSelector selector = new(_ => index);

        Assert.Equal(expected, selector.SelectForArtist("洛天依"));
    }

    [Fact]
    public void EachPlaybackSessionRequestsANewRandomSelection()
    {
        int nextIndex = 0;
        MusicPlaybackAnimationSelector selector = new(_ => nextIndex++);

        Assert.Equal(PetVisualState.EnjoyMusicAnimation, selector.SelectForArtist("洛天依"));
        Assert.Equal(PetVisualState.MusicSwayAnimation, selector.SelectForArtist("洛天依"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("孙楠/刘畊宏/王赫野")]
    public void OtherOrUnknownArtistUsesTheHeadphoneCompanionAnimation(string? artist)
    {
        MusicPlaybackAnimationSelector selector = new(_ =>
            throw new InvalidOperationException("Random selection should not be used."));

        Assert.Equal(
            PetVisualState.EnjoyMusicAnimation,
            selector.SelectForArtist(artist));
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
