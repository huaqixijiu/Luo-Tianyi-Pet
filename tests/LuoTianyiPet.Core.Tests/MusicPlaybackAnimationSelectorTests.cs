using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class MusicPlaybackAnimationSelectorTests
{
    [Fact]
    public void SettingsExposeAllThreeMusicAnimations()
    {
        Assert.Equal(
            [
                PetVisualState.EnjoyMusicAnimation,
                PetVisualState.MusicSwayAnimation,
                PetVisualState.OneClickSingingAnimation,
            ],
            MusicAnimationOptions.FixedOptions.Select(option => option.AnimationId));
    }

    [Fact]
    public void OrdinarySongsAlwaysUseEnjoyMusicInAutomaticMode()
    {
        MusicPlaybackAnimationSelector selector = new(_ =>
            throw new InvalidOperationException("Ordinary songs must not use the easter-egg pool."));

        Assert.Equal(
            PetVisualState.EnjoyMusicAnimation,
            selector.Select(MusicAnimationOptions.AutomaticSelection, "乐正绫"));
        Assert.Equal(
            PetVisualState.EnjoyMusicAnimation,
            selector.Select(MusicAnimationOptions.AutomaticSelection, null));
    }

    [Theory]
    [InlineData(0, PetVisualState.MusicSwayAnimation)]
    [InlineData(1, PetVisualState.OneClickSingingAnimation)]
    public void LuoTianyiSongsUseTheInjectedEasterEggIndex(int index, string expected)
    {
        MusicPlaybackAnimationSelector selector = new(_ => index);

        Assert.Equal(
            expected,
            selector.Select(MusicAnimationOptions.AutomaticSelection, "洛天依/乐正绫"));
    }

    [Fact]
    public void LegacyDisabledSingingFlagNoLongerRemovesAnAutomaticEasterEgg()
    {
        MusicPlaybackAnimationSelector selector = new(maximum =>
        {
            Assert.Equal(2, maximum);
            return 1;
        });

        Assert.Equal(
            PetVisualState.OneClickSingingAnimation,
            selector.Select(
                MusicAnimationOptions.AutomaticSelection,
                "洛天依",
                enableLuoTianyiSingingEasterEgg: false));
    }

    [Theory]
    [InlineData(PetVisualState.EnjoyMusicAnimation, PetVisualState.EnjoyMusicAnimation)]
    [InlineData(PetVisualState.MusicSwayAnimation, PetVisualState.MusicSwayAnimation)]
    [InlineData(PetVisualState.OneClickSingingAnimation, PetVisualState.OneClickSingingAnimation)]
    public void FixedSelectionIsUsedForLuoTianyiSongs(string selected, string expected)
    {
        MusicPlaybackAnimationSelector selector = new(_ =>
            throw new InvalidOperationException("Fixed selection should not use random."));

        Assert.Equal(expected, selector.Select(selected, "Luo Tianyi"));
    }

    [Theory]
    [InlineData(PetVisualState.MusicSwayAnimation)]
    [InlineData(PetVisualState.OneClickSingingAnimation)]
    public void FixedAnimationsAreTheOnlyAnimationForEveryArtist(string selected)
    {
        MusicPlaybackAnimationSelector selector = new();

        Assert.Equal(selected, selector.Select(selected, "言和"));
    }

    [Fact]
    public void FixedSingingRemainsTheOnlyAnimationWhenLegacyEasterEggFlagIsDisabled()
    {
        MusicPlaybackAnimationSelector selector = new();

        Assert.Equal(
            PetVisualState.OneClickSingingAnimation,
            selector.Select(
                PetVisualState.OneClickSingingAnimation,
                "洛天依",
                enableLuoTianyiSingingEasterEgg: false));
    }

    [Fact]
    public void NoneSelectionKeepsMusicVisualsDisabledForEveryArtist()
    {
        MusicPlaybackAnimationSelector selector = new();

        Assert.Equal(
            PetVisualState.NoMusicAnimation,
            selector.Select(MusicAnimationOptions.NoneSelection, "洛天依"));
        Assert.Equal(
            PetVisualState.NoMusicAnimation,
            selector.Select(MusicAnimationOptions.NoneSelection, "其他歌手"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-animation")]
    [InlineData(MusicAnimationOptions.RandomSelection)]
    public void LegacyOrInvalidSelectionsNormalizeToAutomatic(string? selection)
    {
        Assert.Equal(
            MusicAnimationOptions.AutomaticSelection,
            MusicAnimationOptions.NormalizeSelection(selection));
    }

    [Theory]
    [InlineData("洛天依")]
    [InlineData("洛天依/乐正绫")]
    [InlineData("乐正绫 feat. 洛天依")]
    [InlineData("乐正绫 洛天依")]
    [InlineData("乐正绫｜洛天依")]
    [InlineData("乐正绫 × 洛天依")]
    [InlineData("洛天依Official")]
    [InlineData("乐正绫 / 洛天依Official")]
    [InlineData("Luo Tianyi")]
    [InlineData("Luo Tianyi Official")]
    [InlineData("Yuezheng Ling Luo Tianyi")]
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
