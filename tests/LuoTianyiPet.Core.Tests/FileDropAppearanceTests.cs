using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class FileDropAppearanceTests
{
    [Theory]
    [InlineData(AppearanceOptionIds.FullBodyLongHair, false)]
    [InlineData(AppearanceOptionIds.FullBodyCrystalDress, true)]
    [InlineData(AppearanceOptionIds.FullBodyClassicCatEars, true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("unknown-appearance", false)]
    public void Recycling_OnlyAvailableInModesTwoAndThree(string? style, bool expected)
    {
        Assert.Equal(expected, AppearanceOptionIds.AllowsFileDropRecycling(style));
    }

    [Fact]
    public void ModeOne_KeepsIndependentBunAnimation()
    {
        var settings = new AppSettings();
        Assert.False(AppearanceOptionIds.AllowsFileDropRecycling(settings.Appearance.FullBodyStyle));
        Assert.True(settings.FileTreats.EnableDesktopFileTreats);
        Assert.Equal(AppearanceOptionIds.BunEatingNew,
            AppearanceOptionIds.ResolveDefaultBunEatingStyle(settings.Appearance.FullBodyStyle));
    }
}
