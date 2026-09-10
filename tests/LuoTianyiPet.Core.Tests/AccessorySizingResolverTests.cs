using LuoTianyiPet.Core;

namespace LuoTianyiPet.Core.Tests;

public sealed class AccessorySizingResolverTests
{
    [Fact]
    public void SmallWindowScalesMediaControlsToFitExactly()
    {
        AccessorySizing sizing = AccessorySizingResolver.Resolve(126);

        Assert.Equal(116, sizing.TrackInfoWidth);
        Assert.Equal(116d / 178d, sizing.MediaControlsScale, 10);
    }

    [Fact]
    public void NormalWindowKeepsMediaControlsAtNaturalSize()
    {
        AccessorySizing sizing = AccessorySizingResolver.Resolve(196);

        Assert.Equal(186, sizing.TrackInfoWidth);
        Assert.Equal(1, sizing.MediaControlsScale);
    }

    [Fact]
    public void LargeWindowCapsTrackInfoWidth()
    {
        AccessorySizing sizing = AccessorySizingResolver.Resolve(456);

        Assert.Equal(AccessorySizingResolver.MaximumTrackInfoWidth, sizing.TrackInfoWidth);
        Assert.Equal(1, sizing.MediaControlsScale);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(10)]
    [InlineData(0)]
    public void InvalidWindowWidthIsRejected(double width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AccessorySizingResolver.Resolve(width));
    }
}
